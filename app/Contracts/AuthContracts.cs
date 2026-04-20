using System.ComponentModel.DataAnnotations;

namespace app.Contracts;

public record LoginRequest(
    [param: Required]
    string LoginId,
    [param: Required]
    string Password
);
