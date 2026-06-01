namespace SimpleGameServer.Common.Sessions;

/// <summary>
/// 세션 저장소 추상화. 상위 코드는 이 인터페이스만 알고, 실제 백엔드
/// (InMemory/Redis/Db/Remote)는 <c>Session:Backend</c> 설정으로 교체된다.
/// </summary>
public interface ISessionStore
{
    /// <summary>세션을 발급하고 조회에 사용할 세션 키를 반환한다.</summary>
    Task<string> CreateAsync(SessionData data, TimeSpan ttl);

    /// <summary>세션 키로 세션을 조회한다. 없거나 만료되었으면 <c>null</c>.</summary>
    Task<SessionData?> GetAsync(string sessionKey);

    /// <summary>세션을 제거한다(로그아웃 등).</summary>
    Task RemoveAsync(string sessionKey);
}
