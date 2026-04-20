using app.Application.Contracts;
using app.Application.Services;
using Microsoft.AspNetCore.Mvc;

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
}
