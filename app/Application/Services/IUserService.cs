using app.Application.Contracts;

namespace app.Application.Services;

public interface IUserService
{
    Task<ServiceResult> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> GetPasswordOtpAsync(GetPasswordOtpRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> UpdatePasswordAsync(UpdatePasswordRequest request, Guid? authenticatedUserId, CancellationToken cancellationToken);
}
