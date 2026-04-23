using app.Application.Contracts;
using app.Constants;
using app.Infrastructure.Persistence;
using app.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Security.Cryptography;

namespace app.Application.Services;

public sealed class UserService(
    AuthDbContext db,
    ITokenService tokenService,
    IConnectionMultiplexer redis) : IUserService
{
    private readonly IDatabase _redisDb = redis.GetDatabase();
    private static readonly TimeSpan PasswordOtpTtl = TimeSpan.FromMinutes(1);

    public async Task<ServiceResult> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var loginId = request.LoginId.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.LoginId == loginId, cancellationToken))
        {
            return ServiceResult.Failure(ErrorCodes.UserDuplicateLoginId, StatusCodes.Status409Conflict);
        }

        var generatedPassword = tokenService.GenerateRandomPassword();
        var user = new User
        {
            LoginId = loginId,
            PasswordHash = tokenService.HashValue(generatedPassword),
            RegisteredAtUtc = request.RegisteredAtUtc,
            PasswordChangedAtUtc = null,
            RegisteredByEmail = request.RegisteredByEmail.Trim().ToLowerInvariant(),
            UpdatedByEmail = request.UpdatedByEmail.Trim().ToLowerInvariant()
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success(new
        {
            user.Id,
            user.LoginId,
            user.RegisteredAtUtc,
            user.RegisteredByEmail,
            user.UpdatedByEmail,
            GeneratedPassword = generatedPassword
        }, "User created successfully.", StatusCodes.Status201Created);
    }

    public async Task<ServiceResult> GetPasswordOtpAsync(GetPasswordOtpRequest request, CancellationToken cancellationToken)
    {
        var loginId = request.LoginId.Trim().ToLowerInvariant();
        var userExists = await db.Users.AnyAsync(x => x.LoginId == loginId, cancellationToken);
        if (!userExists)
        {
            return ServiceResult.Failure(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
        }

        var otpKey = GetPasswordOtpKey(loginId);
        var existingOtp = await _redisDb.StringGetAsync(otpKey);
        if (!existingOtp.IsNullOrEmpty)
        {
            var existingTtl = await _redisDb.KeyTimeToLiveAsync(otpKey);
            if (existingTtl.HasValue && existingTtl.Value > TimeSpan.Zero)
            {
                return ServiceResult.Success(new
                {
                    ExpiresInSeconds = (int)Math.Ceiling(existingTtl.Value.TotalSeconds)
                }, "Password OTP is still active.");
            }
        }

        var otp = GenerateSixDigitOtp();
        await _redisDb.StringSetAsync(otpKey, otp, PasswordOtpTtl);

        return ServiceResult.Success(new
        {
            ExpiresInSeconds = (int)PasswordOtpTtl.TotalSeconds
        }, "Password OTP generated.");
    }

    public async Task<ServiceResult> UpdatePasswordAsync(UpdatePasswordRequest request, Guid? authenticatedUserId, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Password, request.PasswordConfirmation, StringComparison.Ordinal))
        {
            return ServiceResult.Failure(ErrorCodes.PasswordConfirmationMismatch, StatusCodes.Status400BadRequest);
        }

        User? user;
        if (authenticatedUserId.HasValue)
        {
            user = await db.Users.FirstOrDefaultAsync(x => x.Id == authenticatedUserId.Value, cancellationToken);
            if (user is null)
            {
                return ServiceResult.Failure(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.LoginId) || string.IsNullOrWhiteSpace(request.Otp))
            {
                return ServiceResult.Failure(ErrorCodes.PasswordOtpRequired, StatusCodes.Status400BadRequest);
            }

            var loginId = request.LoginId.Trim().ToLowerInvariant();
            var cachedOtp = await _redisDb.StringGetAsync(GetPasswordOtpKey(loginId));
            if (cachedOtp.IsNullOrEmpty || !string.Equals(cachedOtp!, request.Otp.Trim(), StringComparison.Ordinal))
            {
                return ServiceResult.Failure(ErrorCodes.PasswordOtpInvalidOrExpired, StatusCodes.Status400BadRequest);
            }

            user = await db.Users.FirstOrDefaultAsync(x => x.LoginId == loginId, cancellationToken);
            if (user is null)
            {
                return ServiceResult.Failure(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            await _redisDb.KeyDeleteAsync(GetPasswordOtpKey(loginId));
        }

        user.PasswordHash = tokenService.HashValue(request.Password);
        user.PasswordChangedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success(new
        {
            user.Id,
            user.LoginId,
            user.PasswordChangedAtUtc
        }, "Password updated successfully.");
    }

    private static string GenerateSixDigitOtp()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6");
    }

    private static string GetPasswordOtpKey(string loginId) => $"auth:password:otp:{loginId}";
}
