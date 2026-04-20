namespace app.Constants;

public sealed record ErrorDefinition(string Code, string Message);

public static class ErrorCodes
{
    public static readonly ErrorDefinition UserDuplicateLoginId =
        new("USR_409_DUPLICATE_LOGIN_ID", "user with provided login_id already exists.");

    public static readonly ErrorDefinition ValidationFailed =
        new("REQ_400_VALIDATION", "request validation failed.");
    public static readonly ErrorDefinition AuthInvalidCredentials =
        new("AUTH_401_INVALID_CREDENTIALS", "invalid login credentials.");
    public static readonly ErrorDefinition AuthInvalidRefreshToken =
        new("AUTH_401_INVALID_REFRESH_TOKEN", "invalid refresh token.");
    public static readonly ErrorDefinition AuthRefreshTokenRevokedOrExpired =
        new("AUTH_401_REFRESH_TOKEN_REVOKED_OR_EXPIRED", "refresh token is revoked or expired.");
}
