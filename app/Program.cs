using System.Text;
using app.Constants;
using app.Data;
using app.Options;
using app.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var firstError = context.ModelState.Values
                .SelectMany(x => x.Errors)
                .Select(x => x.ErrorMessage)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            var error = ErrorCodes.ValidationFailed with
            {
                Message = firstError ?? ErrorCodes.ValidationFailed.Message
            };

            return new BadRequestObjectResult(new
            {
                Success = false,
                Status = StatusCodes.Status400BadRequest,
                Data = (object?)null,
                Error = new
                {
                    error.Code,
                    error.Message
                }
            });
        };
    });
builder.Services.AddDbContext<AuthDbContext>(options =>
{
    var connectionString = RequireConfig(builder.Configuration.GetConnectionString("Default"), "ConnectionStrings__Default");
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

var redisConnection = RequireConfig(builder.Configuration["Redis:Connection"], "Redis__Connection");
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));

var jwtSecret = RequireConfig(builder.Configuration["Jwt:Secret"], "Jwt__Secret");
builder.Services.Configure<JwtOptions>(options =>
{
    options.Secret = jwtSecret;
    options.AccessTokenMinutes = builder.Configuration.GetValue("Jwt:AccessTokenMinutes", 15);
    options.RefreshTokenDays = builder.Configuration.GetValue("Jwt:RefreshTokenDays", 7);
});

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
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IRefreshTokenStore, RedisRefreshTokenStore>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    db.Database.EnsureCreated();
}

app.Run();

static string RequireConfig(string? value, string envName)
{
    if (!string.IsNullOrWhiteSpace(value))
    {
        return value;
    }

    throw new InvalidOperationException($"Missing required configuration. Set environment variable: {envName}");
}
