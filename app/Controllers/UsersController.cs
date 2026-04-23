using app.Application.Contracts;
using app.Application.Services;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace app.Controllers;

[Route("users")]
public sealed class UsersController(IUserService userService) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await userService.CreateUserAsync(request, cancellationToken);
        return FromServiceResult(result);
    }

    [HttpPost("password/otp")]
    public async Task<IActionResult> GetPasswordOtp([FromBody] GetPasswordOtpRequest request, CancellationToken cancellationToken)
    {
        var result = await userService.GetPasswordOtpAsync(request, cancellationToken);
        return FromServiceResult(result);
    }

    [HttpPut("password")]
    public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordRequest request, CancellationToken cancellationToken)
    {
        var authenticatedUserId = GetAuthenticatedUserIdOrNull();
        var result = await userService.UpdatePasswordAsync(request, authenticatedUserId, cancellationToken);
        return FromServiceResult(result);
    }

    private Guid? GetAuthenticatedUserIdOrNull()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(sub, out var userId) ? userId : null;
    }
}
