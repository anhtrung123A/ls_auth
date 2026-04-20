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
