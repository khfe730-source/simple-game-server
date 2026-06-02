using SimpleGameServer.Common.Sessions;
using SimpleGameServer.LobbyServer.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Session:Backend 설정값에 따라 ISessionStore 구현체를 등록 (현재 InMemory만 지원).
builder.Services.AddSessionStore(builder.Configuration);

// DB 연결 팩토리. ConnectionStrings:GameDb 기반 SQLite 연결을 생성.
builder.Services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
builder.Services.AddSingleton<DbInitializer>();

var app = builder.Build();

// 기동 시 SQLite 스키마 보장 + 테스트 데이터 시드 (개발 편의).
await app.Services.GetRequiredService<DbInitializer>().InitializeAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.Run();
