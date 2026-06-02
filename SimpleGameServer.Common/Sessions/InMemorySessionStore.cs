using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace SimpleGameServer.Common.Sessions;

/// <summary>
/// 단일 프로세스용 기본 세션 저장소. 프로세스 재시작 시 모든 세션이 소실된다.
/// 만료는 조회 시점에 지연 평가(lazy)한다 — 별도 청소 스레드를 두지 않는다.
/// </summary>
public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, Entry> _sessions = new();

    public Task<string> CreateAsync(SessionData data, TimeSpan ttl)
    {
        var sessionKey = GenerateKey();
        _sessions[sessionKey] = new Entry(data, ExpiryFrom(ttl));
        return Task.FromResult(sessionKey);
    }

    public Task<SessionData?> GetAsync(string sessionKey)
    {
        if (!_sessions.TryGetValue(sessionKey, out var entry))
            return Task.FromResult<SessionData?>(null);

        if (IsExpired(entry))
        {
            _sessions.TryRemove(sessionKey, out _);
            return Task.FromResult<SessionData?>(null);
        }

        return Task.FromResult<SessionData?>(entry.Data);
    }

    public Task RemoveAsync(string sessionKey)
    {
        _sessions.TryRemove(sessionKey, out _);
        return Task.CompletedTask;
    }

    private static string GenerateKey() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    private static DateTime ExpiryFrom(TimeSpan ttl) => DateTime.UtcNow.Add(ttl);

    private static bool IsExpired(Entry entry) => DateTime.UtcNow >= entry.ExpiresAt;

    private readonly record struct Entry(SessionData Data, DateTime ExpiresAt);
}
