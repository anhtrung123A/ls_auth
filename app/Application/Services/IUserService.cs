using app.Application.Contracts;

namespace app.Application.Services;

public interface IUserService
{
    Task<ServiceResult> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken);
}
