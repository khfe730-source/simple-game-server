# SimpleGameServer

ASP.NET Core 기반 학습용 게임 서버. HTTP REST API와 WebSocket을 직접 구현하며,
실제 게임 서버와 유사한 **로비 서버 / 게임 서버 분리 구조**와 **교체 가능한 세션 저장소**를 실습한다.

> 상세 설계와 구현 단계는 [`PLAN.md`](PLAN.md)를 참조. `PLAN.md`가 모든 설계 결정의 단일 출처(source of truth)다.

## 구성

| 프로젝트 | 종류 | 역할 |
|---|---|---|
| `SimpleGameServer.Common` | classlib | 공유 추상화 계층 (`ISessionStore` 및 구현체들). Lobby/Game이 모두 참조 |
| `SimpleGameServer.LobbyServer` | webapi | 로그인·세션 발급 HTTP API + 로비 채팅 WebSocket. **세션의 발원지** |
| `SimpleGameServer.GameServer` | webapi | 게임 입장·아이템 사용 API + 게임 이벤트 WebSocket. 세션은 **검증만** 수행 |
| `SimpleGameServer.Client` | console | 테스트용 콘솔 클라이언트 |

각 서버는 `Controllers/` → `Services/` → `Repositories/` → `Infrastructure/` 레이어 구조를 따르며,
WebSocket은 `WebSockets/` 아래 핸들러+미들웨어 쌍으로 구현한다.

## 기술 스택

- .NET 8 (ASP.NET Core)
- Dapper + SQLite (DB 접근 — 구조는 MSSQL과 동일, 연결 객체만 차이)
- ASP.NET Core 내장 WebSocket

## 세션 관리 (핵심 설계)

세션 저장소는 `ISessionStore` 인터페이스로 추상화되어 `appsettings.json`의 `Session:Backend`
설정값(`InMemory` / `Redis` / `Db` / `Remote`)으로 교체된다. 상위 코드는 백엔드를 알지 못한다.

- **InMemory (기본 개발 환경)**: GameServer는 `RemoteSessionStore`로 LobbyServer의
  `GET /api/auth/sessions/{key}` 엔드포인트를 HTTP 호출해 위임 검증한다.
- **Redis / Db**: 두 서버가 동일 저장소를 직접 조회한다.

검증 흐름:
- HTTP — `Authorization: Bearer {sessionKey}` 헤더 → `SessionAuthMiddleware` → `HttpContext.Items["Session"]`
- WebSocket — 쿼리스트링 `?sessionKey=xxx` → `AcceptWebSocketAsync` 전 검증, 실패 시 401

## 포트

| 서버 | HTTP | HTTPS |
|---|---|---|
| LobbyServer | 5000 | 5001 |
| GameServer  | 5002 | 5003 |

webapi 템플릿 기본값(5000/5001) 충돌을 피하기 위해 각 프로젝트의
`Properties/launchSettings.json`에서 명시 분리한다. GameServer의
`Session:Remote:LobbyBaseUrl`은 Lobby HTTP 포트(`http://localhost:5000`)와 일치해야 한다.

## 빌드 및 실행

```bash
# 솔루션 빌드
dotnet build

# 개별 서버 실행 (각각 별도 터미널)
cd SimpleGameServer.LobbyServer && dotnet run   # http://localhost:5000
cd SimpleGameServer.GameServer  && dotnet run   # http://localhost:5002
cd SimpleGameServer.Client      && dotnet run

# 포트 임시 오버라이드
dotnet run --urls http://localhost:5002
```

로그인 API 헬스체크:

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"loginName":"testuser","passphrase":"password123"}'
```

## 프로젝트 상태

현재 **계획 단계**로, 솔루션과 4개 프로젝트의 스캐폴딩만 존재한다.
구체적인 구현 단계(Step 1~7)는 [`PLAN.md`](PLAN.md)에 정리되어 있다.
