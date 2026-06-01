using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleGameServer.Common.Sessions;

/// <summary>
/// <c>Session:Backend</c> 설정값에 따라 <see cref="ISessionStore"/> 구현체를 등록하는 헬퍼.
/// Lobby/Game 공통으로 <c>builder.Services.AddSessionStore(builder.Configuration)</c> 한 줄로 사용한다.
/// </summary>
public static class SessionStoreServiceCollectionExtensions
{
    public static IServiceCollection AddSessionStore(this IServiceCollection services, IConfiguration configuration)
    {
        var backend = configuration["Session:Backend"] ?? "InMemory";
        return backend switch
        {
            "InMemory" => services.AddSingleton<ISessionStore, InMemorySessionStore>(),
            "Redis" or "Db" or "Remote" => throw NotYetImplemented(backend),
            _ => throw UnknownBackend(backend),
        };
    }

    private static NotSupportedException NotYetImplemented(string backend) =>
        new($"Session:Backend '{backend}' 백엔드는 아직 구현되지 않았습니다. 현재는 'InMemory'만 사용할 수 있습니다.");

    private static NotSupportedException UnknownBackend(string backend) =>
        new($"알 수 없는 Session:Backend 값 '{backend}'. 허용 값: InMemory | Redis | Db | Remote.");
}
