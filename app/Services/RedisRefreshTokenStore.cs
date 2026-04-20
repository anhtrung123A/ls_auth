using StackExchange.Redis;

namespace app.Services;

public sealed class RedisRefreshTokenStore(IConnectionMultiplexer redis, ITokenService tokenService) : IRefreshTokenStore
{
    private readonly IDatabase _redisDb = redis.GetDatabase();

    public Task StoreAsync(string refreshToken, Guid sessionId, TimeSpan ttl, CancellationToken cancellationToken) =>
        _redisDb.StringSetAsync(GetRefreshRedisKey(refreshToken), sessionId.ToString(), ttl);

    public async Task<Guid?> GetSessionIdAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var raw = await _redisDb.StringGetAsync(GetRefreshRedisKey(refreshToken));
        if (raw.IsNullOrEmpty || !Guid.TryParse(raw!, out var sessionId))
        {
            return null;
        }

        return sessionId;
    }

    public Task DeleteAsync(string refreshToken, CancellationToken cancellationToken) =>
        _redisDb.KeyDeleteAsync(GetRefreshRedisKey(refreshToken));

    private string GetRefreshRedisKey(string refreshToken) => $"auth:refresh:{tokenService.HashValue(refreshToken)}";
}
