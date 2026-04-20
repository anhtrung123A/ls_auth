namespace app.Contracts;

public record CreateUserRequest(
    string LoginId,
    DateTime RegisteredAtUtc,
    string RegisteredByEmail,
    string UpdatedByEmail
);

public record LoginRequest(
    string LoginId,
    string Password
);

public record RefreshTokenRequest(
    string RefreshToken
);

public record LogoutRequest(
    string RefreshToken
);
