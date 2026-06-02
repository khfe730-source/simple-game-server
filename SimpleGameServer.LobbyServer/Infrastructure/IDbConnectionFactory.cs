using System.Data;

namespace SimpleGameServer.LobbyServer.Infrastructure;

/// <summary>
/// DB 연결 생성을 추상화한다. 구현체만 교체하면 SQLite → MSSQL 등으로
/// 전환할 수 있어 상위 Repository 코드는 DB 종류를 알지 않는다.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>열려 있지 않은 새 연결을 생성한다. 호출 측에서 Open/Dispose를 책임진다.</summary>
    IDbConnection CreateConnection();
}
