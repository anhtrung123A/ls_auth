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
    public static readonly ErrorDefinition UserNotFound =
        new("USR_404_NOT_FOUND", "user not found.");
    public static readonly ErrorDefinition PasswordConfirmationMismatch =
        new("USR_400_PASSWORD_CONFIRMATION_MISMATCH", "password confirmation does not match.");
    public static readonly ErrorDefinition PasswordOtpRequired =
        new("USR_400_PASSWORD_OTP_REQUIRED", "otp and login_id are required when token is not provided.");
    public static readonly ErrorDefinition PasswordOtpInvalidOrExpired =
        new("USR_400_PASSWORD_OTP_INVALID_OR_EXPIRED", "otp is invalid or expired.");
}
