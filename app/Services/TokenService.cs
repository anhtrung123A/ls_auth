using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using app.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace app.Services;

public sealed class TokenService(IOptions<JwtOptions> jwtOptions) : ITokenService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly SymmetricSecurityKey _signingKey = new(Encoding.UTF8.GetBytes(jwtOptions.Value.Secret));

    public string GenerateAccessToken(Guid userId, string loginId)
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
            expires: now.AddMinutes(_jwtOptions.AccessTokenMinutes),
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string HashValue(string rawValue)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));
        return Convert.ToHexString(bytes);
    }

    public string GenerateRandomPassword(int length = 16)
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

    public string GenerateOpaqueToken(int byteLength = 32) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

    public DateTime GetAccessTokenExpiryUtc(DateTime fromUtc) => fromUtc.AddMinutes(_jwtOptions.AccessTokenMinutes);

    public DateTime GetRefreshTokenExpiryUtc(DateTime fromUtc) => fromUtc.AddDays(_jwtOptions.RefreshTokenDays);
}
