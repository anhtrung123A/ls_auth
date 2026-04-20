namespace app.Application.Services;

public interface IRefreshTokenStore
{
    Task StoreAsync(string refreshToken, Guid sessionId, TimeSpan ttl, CancellationToken cancellationToken);
    Task<Guid?> GetSessionIdAsync(string refreshToken, CancellationToken cancellationToken);
    Task DeleteAsync(string refreshToken, CancellationToken cancellationToken);
}
