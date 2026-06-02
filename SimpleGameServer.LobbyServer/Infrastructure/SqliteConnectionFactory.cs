using System.Data;
using Microsoft.Data.Sqlite;

namespace SimpleGameServer.LobbyServer.Infrastructure;

/// <summary>
/// <c>ConnectionStrings:GameDb</c> 설정값으로 SQLite 연결을 생성하는 <see cref="IDbConnectionFactory"/> 구현.
/// </summary>
public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("GameDb")
            ?? throw new InvalidOperationException("ConnectionStrings:GameDb 가 설정되지 않았습니다.");
    }

    public IDbConnection CreateConnection() => new SqliteConnection(_connectionString);
}
