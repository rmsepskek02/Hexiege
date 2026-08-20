# game-programmer 메모리

## 프로젝트 기본
- Hexiege — 모바일 1v1 헥사 RTS / Unity 6000.0.x (URP), C# 9.0, NGO 2.9.2
- 레이어: Domain → Application → Core → Infrastructure → Presentation → Bootstrap
- asmdef 없음 (전부 Assembly-CSharp). 주석은 **한국어**, 초급자도 이해할 수준으로 상세히.
- ⚠️ `Hexiege.Application` 네임스페이스 때문에 **수식 없는 `Application` 은 `UnityEngine.Application` 이 아니다.**
  반드시 `UnityEngine.Application.` 로 수식할 것.

## 지켜야 할 규칙 (CLAUDE.md 요약)
- **git 명령 절대 금지** (규칙 5) — 검증도 git 없이 한다. 변경 전후 비교는 호출자에게 맡긴다.
- 계획서/요청 **범위만** 구현 (규칙 6). 추가 리팩터링·개선은 제안만.
- **추정 금지** (규칙 10) — 근거(파일:행)를 직접 확인하고 답한다. 확정 못 한 것은 "미확정" 으로 남긴다.
- 판단이 모호하면 스스로 결정하지 말고 보고 (규칙 12).

## 세부 메모
- 네트워크 인프라 전반 · 종료 시점 가드 관례 → [`network-infra.md`](./network-infra.md)

## 작업 습관 (확인된 것)
- **진입점의 이름만 보고 판단하지 않는다.** `Update()` 가 없다 / 코루틴이다 / `grep` 에 안 잡힌다 —
  셋 다 근거가 되지 못한다. 본문과 호출 경로를 끝까지 따라간다.
  (예: `ReconnectionHandler.WaitAndForceWin` 은 30초 코루틴이지만 `OnNetworkDespawn` 이
   `StopCoroutine` 으로 정리하므로 구멍이 아니다.)
- **부호가 비슷한 가드를 구별한다.** `!IsServer`(서버만 통과) vs `IsServer`(클라만 통과)는 정반대다.
  고치기 전에 그 가드가 무엇을 막는 것인지 확인한다.
- **중괄호 균형 검증은 주석·문자열 리터럴을 걷어낸 뒤** 센다. 주석 안에 코드를 인용하면
  단순 `grep`/`count` 가 오탐한다(실제로 겪음).
- `LogEvent` enum 은 `Application/Interfaces/ILogSink.cs` 에 있다 (2026-08-20 기준 멤버 37개).
