using Dapper;

namespace SimpleGameServer.LobbyServer.Infrastructure;

/// <summary>
/// 기동 시 SQLite 스키마(players 테이블)를 보장하고 테스트 데이터를 시드한다.
/// 학습용 로컬 개발 편의를 위한 것으로, 운영에서는 마이그레이션 도구로 대체될 영역.
/// </summary>
public sealed class DbInitializer
{
    private const string CreatePlayersTableSql = """
        CREATE TABLE IF NOT EXISTS players (
            id              INTEGER PRIMARY KEY AUTOINCREMENT,
            login_name      TEXT NOT NULL UNIQUE,
            passphrase      TEXT NOT NULL,
            character_name  TEXT NOT NULL,
            level           INTEGER DEFAULT 1,
            created_at      DATETIME DEFAULT CURRENT_TIMESTAMP
        );
        """;

    private const string SeedTestPlayerSql = """
        INSERT OR IGNORE INTO players (login_name, passphrase, character_name)
        VALUES ('testuser', 'password123', 'TestHero');
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public DbInitializer(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task InitializeAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        await EnsurePlayersTableAsync(connection);
        await SeedTestPlayerAsync(connection);
    }

    private static Task EnsurePlayersTableAsync(System.Data.IDbConnection connection) =>
        connection.ExecuteAsync(CreatePlayersTableSql);

    private static Task SeedTestPlayerAsync(System.Data.IDbConnection connection) =>
        connection.ExecuteAsync(SeedTestPlayerSql);
}
