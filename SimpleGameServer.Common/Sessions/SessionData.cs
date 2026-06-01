namespace SimpleGameServer.Common.Sessions;

/// <summary>
/// 발급된 세션에 담기는 플레이어 식별 정보. 저장소 백엔드와 무관한 순수 데이터.
/// </summary>
public record SessionData(int PlayerId, string CharacterName, DateTime IssuedAt);
