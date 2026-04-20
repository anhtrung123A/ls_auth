namespace app.Application.Services;

public interface ITokenService
{
    string GenerateAccessToken(Guid userId, string loginId);
    string HashValue(string rawValue);
    string GenerateRandomPassword(int length = 16);
    string GenerateOpaqueToken(int byteLength = 32);
    DateTime GetAccessTokenExpiryUtc(DateTime fromUtc);
    DateTime GetRefreshTokenExpiryUtc(DateTime fromUtc);
}
