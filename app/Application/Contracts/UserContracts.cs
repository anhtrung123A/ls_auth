using System.ComponentModel.DataAnnotations;

namespace app.Application.Contracts;

public record CreateUserRequest(
    [param: Required]
    string LoginId,
    DateTime RegisteredAtUtc,
    [param: Required]
    [param: EmailAddress]
    string RegisteredByEmail,
    [param: Required]
    [param: EmailAddress]
    string UpdatedByEmail
);

public record GetPasswordOtpRequest(
    [param: Required]
    string LoginId
);

public record UpdatePasswordRequest(
    string? LoginId,
    string? Otp,
    [param: Required]
    string Password,
    [param: Required]
    string PasswordConfirmation
);
