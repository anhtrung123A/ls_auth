using app.Services;
using Microsoft.AspNetCore.Mvc;

namespace app.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult FromServiceResult(ServiceResult result)
    {
        if (result.IsSuccess)
        {
            return StatusCode(result.StatusCode, new
            {
                Success = true,
                Status = result.StatusCode,
                Data = result.Data
            });
        }

        return StatusCode(result.StatusCode, new
        {
            Success = false,
            Status = result.StatusCode,
            Data = (object?)null,
            Error = new
            {
                Code = result.Error?.Code,
                Message = NormalizeMessage(result.Error?.Message ?? string.Empty)
            }
        });
    }

    private static string NormalizeMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return message;
        }

        return char.ToLowerInvariant(message[0]) + message[1..];
    }
}
