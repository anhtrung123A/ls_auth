using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using app.Constants;
using app.Contracts;
using app.Data;
using app.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AuthDbContext>(options =>
{
    var connectionString = RequireConfig(builder.Configuration.GetConnectionString("Default"), "ConnectionStrings__Default");
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

var redisConnection = RequireConfig(builder.Configuration["Redis:Connection"], "Redis__Connection");
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));

var jwtSecret = RequireConfig(builder.Configuration["Jwt:Secret"], "Jwt__Secret");
var accessTokenMinutes = builder.Configuration.GetValue("Jwt:AccessTokenMinutes", 15);
var refreshTokenDays = builder.Configuration.GetValue("Jwt:RefreshTokenDays", 7);
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    db.Database.EnsureCreated();
}

app.MapPost("/users", async (CreateUserRequest request, AuthDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.LoginId) ||
        string.IsNullOrWhiteSpace(request.RegisteredByEmail) ||
        string.IsNullOrWhiteSpace(request.UpdatedByEmail))
    {
        return ErrorResponse(ErrorCodes.UserRequiredFields, StatusCodes.Status400BadRequest);
    }

    var loginId = request.LoginId.Trim().ToLowerInvariant();
    if (await db.Users.AnyAsync(x => x.LoginId == loginId))
    {
        return ErrorResponse(ErrorCodes.UserDuplicateLoginId, StatusCodes.Status409Conflict);
    }

    var generatedPassword = GenerateRandomPassword();
    var user = new User
    {
        LoginId = loginId,
        PasswordHash = HashPassword(generatedPassword),
        RegisteredAtUtc = request.RegisteredAtUtc,
        PasswordChangedAtUtc = null,
        RegisteredByEmail = request.RegisteredByEmail.Trim().ToLowerInvariant(),
        UpdatedByEmail = request.UpdatedByEmail.Trim().ToLowerInvariant()
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    return SuccessResponse(new
    {
        user.Id,
        user.LoginId,
        user.RegisteredAtUtc,
        user.RegisteredByEmail,
        user.UpdatedByEmail,
        GeneratedPassword = generatedPassword
    }, "User created successfully.", StatusCodes.Status201Created);
});

app.MapPost("/auth/login", async (
    LoginRequest request,
    AuthDbContext db,
    IConnectionMultiplexer redis) =>
{
    if (string.IsNullOrWhiteSpace(request.LoginId) || string.IsNullOrWhiteSpace(request.Password))
    {
        return ErrorResponse(ErrorCodes.AuthRequiredFields, StatusCodes.Status400BadRequest);
    }

    var loginId = request.LoginId.Trim().ToLowerInvariant();
    var user = await db.Users.FirstOrDefaultAsync(x => x.LoginId == loginId);
    if (user is null || user.PasswordHash != HashPassword(request.Password))
    {
        return ErrorResponse(ErrorCodes.AuthInvalidCredentials, StatusCodes.Status401Unauthorized);
    }

    var now = DateTime.UtcNow;
    var refreshToken = GenerateOpaqueToken();
    var refreshTokenHash = HashPassword(refreshToken);
    var refreshExpiresAt = now.AddDays(refreshTokenDays);
    var session = new UserSession
    {
        UserId = user.Id,
        RefreshTokenHash = refreshTokenHash,
        CreatedAtUtc = now,
        ExpiresAtUtc = refreshExpiresAt
    };

    db.UserSessions.Add(session);
    await db.SaveChangesAsync();

    var redisDb = redis.GetDatabase();
    await redisDb.StringSetAsync(GetRefreshRedisKey(refreshToken), session.Id.ToString(), refreshExpiresAt - now);

    var accessToken = GenerateAccessToken(user.Id, user.LoginId, signingKey, accessTokenMinutes);
    return SuccessResponse(new
    {
        AccessToken = accessToken,
        AccessTokenExpiresAtUtc = now.AddMinutes(accessTokenMinutes),
        RefreshToken = refreshToken,
        RefreshTokenExpiresAtUtc = refreshExpiresAt
    }, "Login successful.");
});

app.MapPost("/auth/refresh", async (
    RefreshTokenRequest request,
    AuthDbContext db,
    IConnectionMultiplexer redis) =>
{
    if (string.IsNullOrWhiteSpace(request.RefreshToken))
    {
        return ErrorResponse(ErrorCodes.AuthRefreshRequired, StatusCodes.Status400BadRequest);
    }

    var redisDb = redis.GetDatabase();
    var redisKey = GetRefreshRedisKey(request.RefreshToken);
    var sessionIdRaw = await redisDb.StringGetAsync(redisKey);
    if (sessionIdRaw.IsNullOrEmpty || !Guid.TryParse(sessionIdRaw!, out var sessionId))
    {
        return ErrorResponse(ErrorCodes.AuthInvalidRefreshToken, StatusCodes.Status401Unauthorized);
    }

    var session = await db.UserSessions.FirstOrDefaultAsync(x => x.Id == sessionId);
    if (session is null || session.RevokedAtUtc is not null || session.ExpiresAtUtc <= DateTime.UtcNow)
    {
        await redisDb.KeyDeleteAsync(redisKey);
        return ErrorResponse(ErrorCodes.AuthRefreshTokenRevokedOrExpired, StatusCodes.Status401Unauthorized);
    }

    var user = await db.Users.FirstOrDefaultAsync(x => x.Id == session.UserId);
    if (user is null)
    {
        await redisDb.KeyDeleteAsync(redisKey);
        return ErrorResponse(ErrorCodes.AuthInvalidRefreshToken, StatusCodes.Status401Unauthorized);
    }

    var providedHash = HashPassword(request.RefreshToken);
    if (!string.Equals(providedHash, session.RefreshTokenHash, StringComparison.Ordinal))
    {
        await redisDb.KeyDeleteAsync(redisKey);
        return ErrorResponse(ErrorCodes.AuthInvalidRefreshToken, StatusCodes.Status401Unauthorized);
    }

    var now = DateTime.UtcNow;
    var newRefreshToken = GenerateOpaqueToken();
    var newRefreshHash = HashPassword(newRefreshToken);
    var newRefreshExpiry = now.AddDays(refreshTokenDays);
    var newSession = new UserSession
    {
        UserId = user.Id,
        RefreshTokenHash = newRefreshHash,
        CreatedAtUtc = now,
        ExpiresAtUtc = newRefreshExpiry
    };

    session.RevokedAtUtc = now;
    session.ReplacedBySessionId = newSession.Id;
    db.UserSessions.Add(newSession);
    await db.SaveChangesAsync();

    await redisDb.KeyDeleteAsync(redisKey);
    await redisDb.StringSetAsync(GetRefreshRedisKey(newRefreshToken), newSession.Id.ToString(), newRefreshExpiry - now);

    var accessToken = GenerateAccessToken(user.Id, user.LoginId, signingKey, accessTokenMinutes);
    return SuccessResponse(new
    {
        AccessToken = accessToken,
        AccessTokenExpiresAtUtc = now.AddMinutes(accessTokenMinutes),
        RefreshToken = newRefreshToken,
        RefreshTokenExpiresAtUtc = newRefreshExpiry
    }, "Token refreshed successfully.");
});

app.MapPost("/auth/logout", async (
    LogoutRequest request,
    AuthDbContext db,
    IConnectionMultiplexer redis) =>
{
    if (string.IsNullOrWhiteSpace(request.RefreshToken))
    {
        return ErrorResponse(ErrorCodes.AuthRefreshRequired, StatusCodes.Status400BadRequest);
    }

    var redisDb = redis.GetDatabase();
    var redisKey = GetRefreshRedisKey(request.RefreshToken);
    var sessionIdRaw = await redisDb.StringGetAsync(redisKey);
    if (!sessionIdRaw.IsNullOrEmpty && Guid.TryParse(sessionIdRaw!, out var sessionId))
    {
        var session = await db.UserSessions.FirstOrDefaultAsync(x => x.Id == sessionId);
        if (session is not null && session.RevokedAtUtc is null)
        {
            session.RevokedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    await redisDb.KeyDeleteAsync(redisKey);
    return SuccessResponse(new { Revoked = true }, "Logged out and refresh token revoked.");
});

app.Run();

static string GenerateAccessToken(Guid userId, string loginId, SymmetricSecurityKey signingKey, int accessTokenMinutes)
{
    var now = DateTime.UtcNow;
    var token = new JwtSecurityToken(
        claims:
        [
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, loginId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ],
        notBefore: now,
        expires: now.AddMinutes(accessTokenMinutes),
        signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

    return new JwtSecurityTokenHandler().WriteToken(token);
}

static string HashPassword(string rawValue)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));
    return Convert.ToHexString(bytes);
}

static string GenerateRandomPassword(int length = 16)
{
    const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@$%";
    var randomBytes = RandomNumberGenerator.GetBytes(length);
    var chars = new char[length];
    for (var i = 0; i < length; i++)
    {
        chars[i] = alphabet[randomBytes[i] % alphabet.Length];
    }

    return new string(chars);
}

static string GenerateOpaqueToken(int byteLength = 32)
{
    return Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength))
        .Replace("+", "-")
        .Replace("/", "_")
        .TrimEnd('=');
}

static string GetRefreshRedisKey(string refreshToken) => $"auth:refresh:{HashPassword(refreshToken)}";

static string RequireConfig(string? value, string envName)
{
    if (!string.IsNullOrWhiteSpace(value))
    {
        return value;
    }

    throw new InvalidOperationException($"Missing required configuration. Set environment variable: {envName}");
}

static IResult SuccessResponse(object? data, string message, int statusCode = StatusCodes.Status200OK)
{
    var payload = new
    {
        Status = "success",
        Data = data,
        Message = NormalizeMessage(message)
    };

    return Results.Json(payload, statusCode: statusCode);
}

static IResult ErrorResponse(ErrorDefinition error, int statusCode)
{
    var payload = new
    {
        Status = "error",
        Data = (object?)null,
        Error = new
        {
            Code = error.Code,
            Message = NormalizeMessage(error.Message)
        }
    };

    return Results.Json(payload, statusCode: statusCode);
}

static string NormalizeMessage(string message)
{
    if (string.IsNullOrWhiteSpace(message))
    {
        return message;
    }

    return char.ToLowerInvariant(message[0]) + message[1..];
}
