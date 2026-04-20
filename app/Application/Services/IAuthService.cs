using app.Application.Contracts;

namespace app.Application.Services;

public interface IAuthService
{
    Task<ServiceResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken);
}
