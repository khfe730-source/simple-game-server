# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 상태

현재 리포지토리는 **계획 단계**이며, 소스 코드는 존재하지 않음. `plan.md`가 모든 설계 결정의 단일 출처(source of truth)임. 작업을 시작하거나 사용자 요청을 해석하기 전에 항상 `plan.md`를 먼저 확인할 것.

## 아키텍처 (계획 기준)

ASP.NET Core 기반 학습용 게임 서버. 4개 .NET 프로젝트로 구성된 단일 솔루션:

- **SimpleGameServer.Common** (classlib) — 공유 추상화 계층. 핵심은 `ISessionStore` 및 그 구현체들(`InMemorySessionStore`, `RedisSessionStore`, `DbSessionStore`, `RemoteSessionStore`). LobbyServer와 GameServer가 모두 참조.
- **SimpleGameServer.LobbyServer** (webapi) — 로그인/세션 발급 + 로비 채팅 WebSocket. **세션의 발원지**.
- **SimpleGameServer.GameServer** (webapi) — 게임 입장/아이템 사용 API + 게임 이벤트 WebSocket. **세션은 직접 발급하지 않고 검증만** 수행.
- **SimpleGameServer.Client** (console) — 테스트용 클라이언트.

각 서버 프로젝트는 동일한 레이어 구조 사용: `Controllers/` → `Services/` → `Repositories/` → `Infrastructure/DbConnectionFactory`. WebSocket은 `WebSockets/` 폴더 아래 핸들러+미들웨어 쌍으로 구현. DB는 SQLite + Dapper.

### 세션 관리 (이 프로젝트의 가장 중요한 설계 결정)

세션 저장소는 **인터페이스로 추상화**되어 `appsettings.json`의 `Session:Backend` 설정값(`InMemory`/`Redis`/`Db`/`Remote`)으로 교체됨. 상위 코드는 백엔드를 알지 않음. DI 등록은 `SessionStoreServiceCollectionExtensions.AddSessionStore(IConfiguration)` 헬퍼로 통일.

LobbyServer↔GameServer 세션 공유 전략은 백엔드에 따라 다름:
- **InMemory (기본 개발 환경)**: GameServer는 `RemoteSessionStore`로 LobbyServer의 `GET /api/auth/sessions/{key}` 엔드포인트를 HTTP 호출하여 위임 검증.
- **Redis/Db**: 두 서버가 동일 저장소를 직접 조회.

세션 검증 흐름:
- HTTP: `Authorization: Bearer {sessionKey}` 헤더 → `SessionAuthMiddleware` → `HttpContext.Items["Session"]`에 저장.
- WebSocket: 쿼리스트링 `?sessionKey=xxx` → `AcceptWebSocketAsync` 전에 검증, 실패 시 401.

이 추상화를 깨는 변경(예: 컨트롤러에서 ConcurrentDictionary 직접 접근)은 설계 의도와 충돌하므로 피할 것.

### 포트 분리

webapi 템플릿 기본값은 두 서버가 동일 포트(5000/5001)를 잡아 충돌함. 각 프로젝트의 `Properties/launchSettings.json`에서 명시 분리:

| 서버 | HTTP | HTTPS |
|---|---|---|
| LobbyServer | 5000 | 5001 |
| GameServer  | 5002 | 5003 |

GameServer의 `appsettings.json` → `Session:Remote:LobbyBaseUrl`은 Lobby HTTP 포트(`http://localhost:5000`)와 일관되어야 함.

## 공통 명령

```bash
# 솔루션 빌드
dotnet build

# 개별 서버 실행 (별도 터미널)
cd SimpleGameServer.LobbyServer && dotnet run
cd SimpleGameServer.GameServer  && dotnet run
cd SimpleGameServer.Client      && dotnet run

# 포트 임시 오버라이드
dotnet run --urls http://localhost:5002

# 로그인 API 헬스체크
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"loginName":"testuser","passphrase":"password123"}'
```

테스트 프로젝트는 아직 계획에 없음 — 사용자가 추가를 요청하기 전까지 생성하지 말 것.

## 작업 시 주의 사항

- **계획 변경은 plan.md에도 반영**. 코드만 바꾸고 plan.md를 두면 두 출처가 어긋남.
- 새 프로젝트를 솔루션에 추가할 때 `dotnet sln add` + 필요한 경우 `dotnet add <project> reference SimpleGameServer.Common`을 반드시 함께 실행.
- WebSocket 핸들러 신규 작성 시 LobbyServer 측 구현을 참조 패턴으로 사용 (`ConcurrentDictionary` 연결 관리 + 비동기 브로드캐스트).
- 비밀번호 해시, 로깅 미들웨어, 예외 미들웨어 등은 plan.md에서 의도적으로 보류된 항목 — 사용자가 명시적으로 요청할 때까지 추가하지 말 것.
