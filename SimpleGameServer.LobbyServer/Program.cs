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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.Run();
