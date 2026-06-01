# ASP.NET Core 실습 프로젝트 계획

> 목표: HTTP REST API + WebSocket 로비 서버 + 게임 서버 직접 구현  
> 난이도: 핵심 개념 체험에 집중  
> 예상 소요: 1~2일

---

## 프로젝트 개요: SimpleGameServer

실제 게임 서버와 유사한 구조로 구성

- **LobbyServer**: HTTP API (로그인, 캐릭터 조회) + WebSocket (로비 채팅, 접속자 목록)
- **GameServer**: HTTP API (게임 입장, 아이템 사용 등) + WebSocket (실시간 게임 이벤트)
- **Client**: 콘솔 테스트 클라이언트
- **DB**: SQLite (구조는 MSSQL과 동일, 연결 객체만 차이)

---

## 기술 스택

- .NET 8 (ASP.NET Core)
- Dapper (DB 접근)
- SQLite (로컬 개발용 — 추후 MSSQL 교체 가능)
- WebSocket (ASP.NET Core 내장)

---

## 프로젝트 구조

```
SimpleGameServer/
├── SimpleGameServer.sln
│
├── SimpleGameServer.Common/             # 공유 추상화 (세션 등)
│   ├── Sessions/
│   │   ├── ISessionStore.cs
│   │   ├── SessionData.cs
│   │   ├── InMemorySessionStore.cs     # 기본 (개발용)
│   │   ├── RedisSessionStore.cs        # (선택) Redis 백엔드
│   │   ├── DbSessionStore.cs           # (선택) DB 백엔드
│   │   ├── RemoteSessionStore.cs       # GameServer가 Lobby InMemory 검증 위임용
│   │   └── SessionStoreServiceCollectionExtensions.cs  # 설정 기반 DI 등록
│   └── SimpleGameServer.Common.csproj
│
├── SimpleGameServer.LobbyServer/       # HTTP API + WebSocket
│   ├── Controllers/
│   │   ├── AuthController.cs           # 로그인 API
│   │   └── PlayerController.cs        # 캐릭터 정보 API
│   ├── WebSockets/
│   │   ├── LobbyWebSocketHandler.cs   # WebSocket 연결 관리 + 브로드캐스트
│   │   └── WebSocketMiddleware.cs     # WebSocket 미들웨어
│   ├── Services/
│   │   ├── AuthService.cs
│   │   └── PlayerService.cs
│   ├── Repositories/
│   │   ├── AuthRepository.cs
│   │   └── PlayerRepository.cs
│   ├── Models/
│   │   ├── LoginRequest.cs
│   │   ├── LoginResponse.cs
│   │   └── PlayerInfo.cs
│   ├── Infrastructure/
│   │   └── DbConnectionFactory.cs
│   ├── Program.cs
│   └── appsettings.json
│
├── SimpleGameServer.GameServer/        # HTTP API + WebSocket 게임 서버
│   ├── Controllers/
│   │   └── GameController.cs          # 게임 입장, 아이템 사용 등 API
│   ├── WebSockets/
│   │   ├── GameWebSocketHandler.cs   # WebSocket 연결 관리 + 브로드캐스트
│   │   └── WebSocketMiddleware.cs    # WebSocket 미들웨어
│   ├── Services/
│   │   └── GameService.cs
│   ├── Repositories/
│   │   └── GameRepository.cs         # 캐릭터 상태/아이템 DB 접근
│   ├── Models/
│   │   └── GameModels.cs
│   ├── Infrastructure/
│   │   └── DbConnectionFactory.cs
│   ├── Program.cs
│   └── appsettings.json
│
└── SimpleGameServer.Client/            # 테스트용 콘솔 클라이언트
    └── Program.cs
```

---

## 세션 관리 설계 (확장성 우선)

세션 저장소를 인터페이스로 추상화하고, **기본은 인메모리**, 운영 시 Redis/DB로 **설정만 바꿔 교체** 가능하도록 설계.

### 인터페이스

```csharp
// SimpleGameServer.Common/Sessions/ISessionStore.cs
public interface ISessionStore
{
    Task<string> CreateAsync(SessionData data, TimeSpan ttl);
    Task<SessionData?> GetAsync(string sessionKey);
    Task RemoveAsync(string sessionKey);
}

public record SessionData(int PlayerId, string CharacterName, DateTime IssuedAt);
```

### 구현체 (Strategy 패턴)

| 구현체 | 백엔드 | 특징 |
|---|---|---|
| `InMemorySessionStore` | `ConcurrentDictionary` | **기본값**, 단일 프로세스 한정, 재시작 시 소실 |
| `RedisSessionStore` | StackExchange.Redis | 다중 서버 공유, TTL 네이티브 지원 |
| `DbSessionStore` | Dapper + `sessions` 테이블 | 다중 서버 공유, 별도 인프라 불필요 |
| `RemoteSessionStore` | HTTP | GameServer가 Lobby의 InMemory 세션을 검증 위임할 때만 사용 |

### 설정 기반 주입

```jsonc
// appsettings.json
"Session": {
  "Backend": "InMemory",           // InMemory | Redis | Db | Remote
  "TtlMinutes": 60,
  "Redis":  { "ConnectionString": "localhost:6379" },
  "Remote": { "LobbyBaseUrl": "http://localhost:5000" }
}
```

```csharp
// SessionStoreServiceCollectionExtensions.cs
public static IServiceCollection AddSessionStore(this IServiceCollection services, IConfiguration cfg)
{
    var backend = cfg["Session:Backend"] ?? "InMemory";
    return backend switch
    {
        "Redis"  => services.AddSingleton<ISessionStore, RedisSessionStore>(),
        "Db"     => services.AddScoped<ISessionStore, DbSessionStore>(),
        "Remote" => services.AddHttpClient<ISessionStore, RemoteSessionStore>(c =>
                        c.BaseAddress = new Uri(cfg["Session:Remote:LobbyBaseUrl"]!))
                        .Services,
        _        => services.AddSingleton<ISessionStore, InMemorySessionStore>(),
    };
}

// Program.cs (Lobby/Game 공통)
builder.Services.AddSessionStore(builder.Configuration);
```

### 세션 검증 흐름

**HTTP 요청** — `Authorization: Bearer {sessionKey}` 헤더
- 커스텀 `SessionAuthMiddleware`에서 `ISessionStore.GetAsync` 호출 → `HttpContext.Items["Session"] = data` 저장
- 실패 시 401 반환

**WebSocket 연결** — 쿼리스트링 `wss://.../ws/lobby?sessionKey=xxx`
- 브라우저 WS API가 커스텀 헤더를 못 보내는 경우가 많아 쿼리스트링이 표준
- `AcceptWebSocketAsync` 전에 검증, 실패 시 401로 거부

### LobbyServer ↔ GameServer 세션 공유 — 백엔드별 트레이드오프

| 백엔드 | 공유 방식 | 장점 | 단점 |
|---|---|---|---|
| InMemory (기본) | Lobby: `InMemorySessionStore`<br>Game: `RemoteSessionStore`로 Lobby HTTP 호출 | 인프라 불필요, 학습 단순 | 매 요청마다 네트워크 1홉, Lobby 장애 시 동반 장애 |
| Redis | 양쪽 모두 `RedisSessionStore` | 빠름, 다중 인스턴스 확장 용이 | Redis 인프라 필요 |
| Db | 양쪽 모두 `DbSessionStore` | 추가 인프라 없음, 트랜잭션 일관성 | RDB 부하 증가, 캐시 별도 필요 |

→ **`ISessionStore` 인터페이스만 일치하면, GameServer는 `appsettings.json` 한 줄 변경으로 검증 방식을 전환**할 수 있음. 이게 추상화의 핵심 가치.

### 면접 어필 포인트
- "세션 저장소를 인터페이스로 추상화해서 인메모리 → Redis → DB로 **무중단 교체 가능**하게 설계했습니다"
- "InMemory 모드에서도 GameServer가 동일 인터페이스의 `RemoteSessionStore`로 Lobby에 위임하므로 **상위 코드는 백엔드를 모름**"

---

## 구현 단계

### Step 1. 솔루션 및 프로젝트 생성

```bash
# 솔루션 생성
mkdir SimpleGameServer && cd SimpleGameServer
dotnet new sln -n SimpleGameServer

# Common (공유 라이브러리 — ISessionStore 등)
dotnet new classlib -n SimpleGameServer.Common
dotnet sln add SimpleGameServer.Common

# LobbyServer
dotnet new webapi -n SimpleGameServer.LobbyServer
dotnet sln add SimpleGameServer.LobbyServer
dotnet add SimpleGameServer.LobbyServer reference SimpleGameServer.Common

# GameServer (HTTP API + WebSocket 이므로 webapi 템플릿 사용)
dotnet new webapi -n SimpleGameServer.GameServer
dotnet sln add SimpleGameServer.GameServer
dotnet add SimpleGameServer.GameServer reference SimpleGameServer.Common

# Client (테스트용 콘솔)
dotnet new console -n SimpleGameServer.Client
dotnet sln add SimpleGameServer.Client

# 패키지 설치 — LobbyServer
cd SimpleGameServer.LobbyServer
dotnet add package Dapper
dotnet add package Microsoft.Data.Sqlite
cd ..

# 패키지 설치 — GameServer (DB 접근 필요)
cd SimpleGameServer.GameServer
dotnet add package Dapper
dotnet add package Microsoft.Data.Sqlite
cd ..
```

---

### Step 2. DB 설정 (SQLite)

```sql
-- players 테이블
CREATE TABLE players (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    login_name      TEXT NOT NULL UNIQUE,
    passphrase      TEXT NOT NULL,
    character_name  TEXT NOT NULL,
    level           INTEGER DEFAULT 1,
    created_at      DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- 테스트 데이터
INSERT INTO players (login_name, passphrase, character_name)
VALUES ('testuser', 'password123', 'TestHero');
```

---

### Step 3. LobbyServer — HTTP API 구현

**[POST] /api/auth/login** — 로그인  
**[GET] /api/player/{id}** — 캐릭터 정보 조회

```
Request:  POST /api/auth/login
Body:     { "loginName": "testuser", "passphrase": "password123" }

Response: 200 OK
Body:     { "playerId": 1, "characterName": "TestHero", "sessionKey": "abc123" }
```

구현 순서:
1. `Models/` — LoginRequest, LoginResponse, PlayerInfo DTO 정의
2. `Infrastructure/DbConnectionFactory.cs` — SQLite 연결 생성
3. `Repositories/AuthRepository.cs` — Dapper로 쿼리 호출
4. `Services/AuthService.cs` — `ISessionStore.CreateAsync`로 세션 발급
5. `Controllers/AuthController.cs` — HTTP 엔드포인트
   - `POST /api/auth/login` — 로그인 및 세션 발급
   - `GET  /api/auth/sessions/{key}` — **GameServer의 `RemoteSessionStore`가 호출**할 InMemory 검증 엔드포인트
6. `Middlewares/SessionAuthMiddleware.cs` — `Authorization: Bearer {key}` 검증
7. `Program.cs` — DI 등록 (`AddSessionStore(Configuration)`, 미들웨어 파이프라인)

---

### Step 4. LobbyServer — WebSocket 구현

**WS /ws/lobby** — 로비 접속, 채팅 브로드캐스트

```
클라이언트 → 서버: { "type": "chat", "message": "안녕하세요" }
서버 → 전체:       { "type": "chat", "from": "TestHero", "message": "안녕하세요" }

접속 시 → 서버 → 전체: { "type": "join", "name": "TestHero", "onlineCount": 3 }
종료 시 → 서버 → 전체: { "type": "leave", "name": "TestHero", "onlineCount": 2 }
```

구현 순서:
1. `WebSockets/LobbyWebSocketHandler.cs` — ConcurrentDictionary로 연결 관리 + 브로드캐스트
2. `WebSockets/WebSocketMiddleware.cs` — `/ws/lobby` 업그레이드 처리
   - 쿼리스트링에서 `sessionKey` 추출 → `ISessionStore.GetAsync`로 검증
   - 실패 시 401 후 `context.Response.CompleteAsync()`로 종료
   - 성공 시 `AcceptWebSocketAsync` 진행 + 연결 dict에 `PlayerId` 키로 등록
3. `Program.cs` — WebSocket 미들웨어 등록

```csharp
// Program.cs
app.UseWebSockets();
app.UseMiddleware<WebSocketMiddleware>();
```

---

### Step 5. GameServer — HTTP API + WebSocket 구현

**[POST] /api/game/enter** — 게임 입장  
**[POST] /api/game/item/use** — 아이템 사용  
**WS /ws/game** — 실시간 게임 이벤트 (이동, 공격 등 브로드캐스트)

```
WS 메시지 예시:
클라이언트 → 서버: { "type": "move", "x": 10, "y": 20 }
서버 → 전체:       { "type": "move", "from": "TestHero", "x": 10, "y": 20 }
```

구현 순서:
1. `Models/GameModels.cs` — 요청/응답 DTO 정의
2. `Infrastructure/DbConnectionFactory.cs` — SQLite 연결 생성 (LobbyServer와 동일 패턴)
3. `Repositories/GameRepository.cs` — 캐릭터 상태/아이템 DB 접근
4. `Services/GameService.cs` — 게임 로직 (`HttpContext.Items["Session"]`에서 PlayerId 획득)
5. `Controllers/GameController.cs` — HTTP 엔드포인트
6. `WebSockets/GameWebSocketHandler.cs` — 실시간 이벤트 브로드캐스트 (WS 인증은 Lobby와 동일 패턴)
7. `Program.cs` — `AddSessionStore(Configuration)` + `SessionAuthMiddleware` 등록
   - 기본 `appsettings.json`은 `"Backend": "Remote"`로 두어 Lobby InMemory를 위임 검증
   - Redis/DB로 전환 시 LobbyServer와 동일 백엔드로 변경하면 끝

---

### Step 6. Client 구현

```
Client 실행 흐름:
1. POST /api/auth/login 호출 → 세션키 수신
2. WS /ws/lobby 연결 → 채팅 메시지 송수신
3. POST /api/game/enter 호출 → 게임 입장
4. WS /ws/game 연결 → 이동/공격 이벤트 송수신
```

```csharp
// 사용 클래스
// HttpClient       — HTTP API 호출
// ClientWebSocket  — WebSocket 연결 (.NET 내장)
```

---

### Step 7. 실행 및 테스트

#### 포트 분리

webapi 기본 템플릿은 두 서버 모두 5000/5001을 잡기 때문에 동시 실행 시 충돌함. **`Properties/launchSettings.json`에서 명시적으로 분리**.

| 서버 | HTTP | HTTPS |
|---|---|---|
| LobbyServer | 5000 | 5001 |
| GameServer  | 5002 | 5003 |

```jsonc
// SimpleGameServer.LobbyServer/Properties/launchSettings.json
"profiles": {
  "http": {
    "commandName": "Project",
    "applicationUrl": "http://localhost:5000",
    "environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development" }
  }
}
```

```jsonc
// SimpleGameServer.GameServer/Properties/launchSettings.json
"profiles": {
  "http": {
    "commandName": "Project",
    "applicationUrl": "http://localhost:5002",
    "environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development" }
  }
}
```

GameServer의 `appsettings.json`에서 Lobby 베이스 URL도 동일하게 맞춰야 함:
```jsonc
"Session": {
  "Backend": "Remote",
  "Remote": { "LobbyBaseUrl": "http://localhost:5000" }
}
```

> CLI에서 임시로 덮어쓸 때는 `dotnet run --urls http://localhost:5002` 또는 환경 변수 `ASPNETCORE_URLS` 사용.

#### 실행

```bash
# LobbyServer (5000)
cd SimpleGameServer.LobbyServer && dotnet run

# GameServer (5002) — 별도 터미널
cd SimpleGameServer.GameServer && dotnet run

# 클라이언트 여러 개 띄워서 테스트
cd SimpleGameServer.Client && dotnet run
```

HTTP API 확인:
```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"loginName":"testuser","passphrase":"password123"}'
```

---

## 면접에서 활용 포인트

| 구현 항목 | 면접 언급 포인트 |
|---|---|
| Controller-Service-Repository | "레이어 분리 구조로 설계했습니다" |
| DI 등록 | "Scoped/Singleton 생명주기를 구분해서 등록했습니다" |
| Dapper + SQLite | "인터페이스 기반 설계로 MSSQL 전환이 DbConnectionFactory만 수정하면 됩니다" |
| ISessionStore 추상화 | "세션 저장소를 인터페이스로 분리해 InMemory → Redis → DB를 설정 한 줄로 교체 가능하게 설계" |
| RemoteSessionStore | "InMemory 환경에서도 동일 인터페이스로 다른 서버에 검증을 위임 — 상위 코드는 백엔드를 모름" |
| WebSocket 브로드캐스트 | "ConcurrentDictionary로 연결 관리, 비동기 브로드캐스트 구현" |
| GameServer WebSocket | "로비와 동일한 구조로 게임 이벤트 실시간 브로드캐스트 구현" |
| async/await 전체 적용 | "IO 바운드 작업은 전부 비동기로 처리했습니다" |

---

## 참고

- [ASP.NET Core 공식 문서](https://learn.microsoft.com/ko-kr/aspnet/core/)
- [ASP.NET Core WebSocket 가이드](https://learn.microsoft.com/ko-kr/aspnet/core/fundamentals/websockets)
- [Dapper GitHub](https://github.com/DapperLib/Dapper)
