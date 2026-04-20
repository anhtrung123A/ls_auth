using app.Contracts;

namespace app.Services;

public interface IUserService
{
    Task<ServiceResult> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken);
}
