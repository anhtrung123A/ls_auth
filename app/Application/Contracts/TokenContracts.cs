using System.ComponentModel.DataAnnotations;

namespace app.Application.Contracts;

public record RefreshTokenRequest(
    [param: Required]
    string RefreshToken
);

public record LogoutRequest(
    [param: Required]
    string RefreshToken
);
