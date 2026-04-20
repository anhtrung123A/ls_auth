using System.ComponentModel.DataAnnotations;

namespace app.Application.Contracts;

public record LoginRequest(
    [param: Required]
    string LoginId,
    [param: Required]
    string Password
);
