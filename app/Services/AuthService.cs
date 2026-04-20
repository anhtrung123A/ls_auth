using app.Constants;
using app.Contracts;
using app.Data;
using app.Entities;
using Microsoft.EntityFrameworkCore;

namespace app.Services;

public sealed class AuthService(
    AuthDbContext db,
    ITokenService tokenService,
    IRefreshTokenStore refreshTokenStore) : IAuthService
{
    public async Task<ServiceResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.LoginId) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ServiceResult.Failure(ErrorCodes.AuthRequiredFields, StatusCodes.Status400BadRequest);
        }

        var loginId = request.LoginId.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(x => x.LoginId == loginId, cancellationToken);
        if (user is null || user.PasswordHash != tokenService.HashValue(request.Password))
        {
            return ServiceResult.Failure(ErrorCodes.AuthInvalidCredentials, StatusCodes.Status401Unauthorized);
        }

        var now = DateTime.UtcNow;
        var refreshToken = tokenService.GenerateOpaqueToken();
        var refreshExpiresAt = tokenService.GetRefreshTokenExpiryUtc(now);
        var session = new UserSession
        {
            UserId = user.Id,
            RefreshTokenHash = tokenService.HashValue(refreshToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshExpiresAt
        };

        db.UserSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        await refreshTokenStore.StoreAsync(refreshToken, session.Id, refreshExpiresAt - now, cancellationToken);

        return ServiceResult.Success(new
        {
            AccessToken = tokenService.GenerateAccessToken(user.Id, user.LoginId),
            AccessTokenExpiresAtUtc = tokenService.GetAccessTokenExpiryUtc(now),
            RefreshToken = refreshToken,
            RefreshTokenExpiresAtUtc = refreshExpiresAt
        }, "Login successfuls.");
    }

    public async Task<ServiceResult> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return ServiceResult.Failure(ErrorCodes.AuthRefreshRequired, StatusCodes.Status400BadRequest);
        }

        var sessionId = await refreshTokenStore.GetSessionIdAsync(request.RefreshToken, cancellationToken);
        if (sessionId is null)
        {
            return ServiceResult.Failure(ErrorCodes.AuthInvalidRefreshToken, StatusCodes.Status401Unauthorized);
        }

        var session = await db.UserSessions.FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null || session.RevokedAtUtc is not null || session.ExpiresAtUtc <= DateTime.UtcNow)
        {
            await refreshTokenStore.DeleteAsync(request.RefreshToken, cancellationToken);
            return ServiceResult.Failure(ErrorCodes.AuthRefreshTokenRevokedOrExpired, StatusCodes.Status401Unauthorized);
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == session.UserId, cancellationToken);
        if (user is null)
        {
            await refreshTokenStore.DeleteAsync(request.RefreshToken, cancellationToken);
            return ServiceResult.Failure(ErrorCodes.AuthInvalidRefreshToken, StatusCodes.Status401Unauthorized);
        }

        var providedHash = tokenService.HashValue(request.RefreshToken);
        if (!string.Equals(providedHash, session.RefreshTokenHash, StringComparison.Ordinal))
        {
            await refreshTokenStore.DeleteAsync(request.RefreshToken, cancellationToken);
            return ServiceResult.Failure(ErrorCodes.AuthInvalidRefreshToken, StatusCodes.Status401Unauthorized);
        }

        var now = DateTime.UtcNow;
        var newRefreshToken = tokenService.GenerateOpaqueToken();
        var newRefreshExpiry = tokenService.GetRefreshTokenExpiryUtc(now);
        var newSession = new UserSession
        {
            UserId = user.Id,
            RefreshTokenHash = tokenService.HashValue(newRefreshToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = newRefreshExpiry
        };

        session.RevokedAtUtc = now;
        session.ReplacedBySessionId = newSession.Id;
        db.UserSessions.Add(newSession);
        await db.SaveChangesAsync(cancellationToken);

        await refreshTokenStore.DeleteAsync(request.RefreshToken, cancellationToken);
        await refreshTokenStore.StoreAsync(newRefreshToken, newSession.Id, newRefreshExpiry - now, cancellationToken);

        return ServiceResult.Success(new
        {
            AccessToken = tokenService.GenerateAccessToken(user.Id, user.LoginId),
            AccessTokenExpiresAtUtc = tokenService.GetAccessTokenExpiryUtc(now),
            RefreshToken = newRefreshToken,
            RefreshTokenExpiresAtUtc = newRefreshExpiry
        }, "Token refreshed successfully.");
    }

    public async Task<ServiceResult> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return ServiceResult.Failure(ErrorCodes.AuthRefreshRequired, StatusCodes.Status400BadRequest);
        }

        var sessionId = await refreshTokenStore.GetSessionIdAsync(request.RefreshToken, cancellationToken);
        if (sessionId is not null)
        {
            var session = await db.UserSessions.FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
            if (session is not null && session.RevokedAtUtc is null)
            {
                session.RevokedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        await refreshTokenStore.DeleteAsync(request.RefreshToken, cancellationToken);
        return ServiceResult.Success(new { Revoked = true }, "Logged out and refresh token revoked.");
    }
}
