# 에이전트 공용 컨텍스트

> **모든 에이전트는 작업 시작 전 이 파일을 반드시 읽을 것.**

---

## 프로젝트 개요
- 장르: 모바일 1v1 RTS, 헥스 타일맵 기반 공성전 (9:16 세로)
- 엔진: Unity 6000.0.x (URP), C# 9.0, NGO 2.9.2
- 씬: Login.unity (Build Index 0), Lobby.unity (Build Index 1), Game.unity (Build Index 2)
- 레이어: Domain → Application → Core → Infrastructure → Presentation → Bootstrap

---

## 아키텍처 핵심 제약 (위반 시 컴파일 오류 또는 런타임 버그)

| 제약 | 내용 |
|------|------|
| Domain → Core 참조 금지 | `using Hexiege.Core` in Domain 파일 불가 → HexOrientationContext 정적 홀더 사용 |
| GameBootstrapper | 유일한 의존성 조합 루트 — 다른 곳에서 직접 의존성 주입 금지 |
| NetworkBehaviour 위치 | Infrastructure 레이어에만 (Presentation/Application 금지) |
| Application → Netcode | Unity.Netcode 직접 참조 금지 → NetworkContext 정적 홀더 사용 |
| Application → Infrastructure 역참조 금지 | Application 인터페이스가 Infrastructure 구체 클래스를 반환/노출 금지. 필요 시 Application 계층(`Scripts/Application/Interfaces/`)에 인터페이스 선언 → Infrastructure가 구현(의존성 역전). 사례: `IUnitFactory`(←UnitFactory), `IGameServices`(←GameBootstrapper), `IEntityPositionProvider`, `IForfeitService` |
| Assembly Definitions | 없음 — 네임스페이스 규약으로만 레이어 경계 관리 |
| NGO RPC 메서드명 | 반드시 `ServerRpc`/`ClientRpc`로 끝나야 함 |
| NGO 설정 | Enable Scene Management = ON 필수 |
| UIManager | Login 씬에서 1회 생성 → DontDestroyOnLoad. 호출은 항상 `UIManager.Instance?.Method()` null-safe 패턴. Lobby/Game 씬 직접 진입 시 Instance=null 가능 |
| 공통 UI 호출 | `UIManager.Instance?.ShowConfirm(...)` / `UIManager.Instance?.ShowLoading(bool, string)` — 씬 직접 참조 금지 |

---

## 절대 규칙 참조
→ `d:/Dmain/dev/Portfolio/Hexiege/Hexiege/CLAUDE.md`

## 작업 사이클 상세 참조
→ `d:/Dmain/dev/Portfolio/Hexiege/Hexiege/Assets/_Project/Docs/WORKFLOW.md`

---

## 에이전트별 MEMORY.md 경로

| 에이전트 | MEMORY.md 경로 |
|---------|---------------|
| game-programmer | `d:/Dmain/dev/Portfolio/Hexiege/Hexiege/.claude/agent-memory/game-programmer/MEMORY.md` |
| game-design-lead | `d:/Dmain/dev/Portfolio/Hexiege/Hexiege/.claude/agent-memory/game-design-lead/MEMORY.md` |
| qa-tester | `d:/Dmain/dev/Portfolio/Hexiege/Hexiege/.claude/agent-memory/qa-tester/MEMORY.md` |
| asset-prompt-crafter | `d:/Dmain/dev/Portfolio/Hexiege/Hexiege/.claude/agent-memory/asset-prompt-crafter/MEMORY.md` |
| project-orchestrator | `d:/Dmain/dev/Portfolio/Hexiege/Hexiege/.claude/agent-memory/project-orchestrator/MEMORY.md` |
| document-manager | `d:/Dmain/dev/Portfolio/Hexiege/Hexiege/.claude/agent-memory/document-manager/MEMORY.md` |

---

## 주요 문서 경로

| 문서 | 경로 |
|------|------|
| 프로젝트 현황 | `Assets/_Project/Docs/PROJECT_STATUS.md` |
| 로드맵 | `Assets/_Project/Docs/ROADMAP.md` |
| 기획서 | `Assets/_Project/Docs/GameDesignDocument.md` |
| 기술설계 | `Assets/_Project/Docs/TechnicalDesignDocument.md` |
| 작업 사이클 규칙 | `Assets/_Project/Docs/WORKFLOW.md` |
| 에이전트 & 문서 인덱스 | `AGENTS.md` |

---

## 좌표계 핵심
- XZ 평면 (Y=0 바닥, Y=높이)
- HexMetrics.HexToWorld() → Vector3(x, 0f, z)
- ViewConverter: Red팀 좌표 반전 `2*center - pos` (X, Z만 반전, Y 보존)
- ViewConverter.Setup()은 LoadMap() 내 렌더링 전에 호출 (ApplyConfig() 직후)

---

## 공통 중요 교훈
- P2P(Relay) 매칭 호스트 결정은 `GetMatchmakingResults`(전용 서버/Multiplay용, P2P 클라 호출 시 404)가 아니라 **Lobby CreateOrJoin(matchId=lobbyId) 원자 선점**으로 해야 함 — 먼저 만든 쪽=호스트. 매칭 자체는 정상, 호스트 결정만 404였음 (2026-07-17, A방식, 커밋 `a3dbc73`). **간헐 버그라 초기 정상 확인·지속 테스트 중(확정 PASS 아님)**, 레거시 코드 비활성화만·미삭제. task: `_Tasks/2026-07-16/19_09_matchmaker-404-host-determination/`
- Y Scale 0.4 on tile prefabs is INTENTIONAL (등각 효과) — 절대 변경 금지
- Inspector 값이 코드 기본값보다 우선 (ScriptableObject overrides code)
- QA 에이전트 제안 → 반드시 컴파일 확인 후 적용
- Scene NetworkObjects → Despawn/Respawn 시 리셋 → GameBootstrapper flag 사용
- TeamAssigner 삭제됨 (2026-03-20) — NetworkGameFlow.WaitForTeamAndSendReady()에서 팀 직접 할당
- 코드 정리 Phase 1 완료 (2026-06-23) — 약 30개 파일 히스토리성 주석/폐기 코드 제거. `GameBootstrapper.Setup.cs` 환불 캐시 종족 목록은 `refundRaces` 지역 변수 1개로 통합(중복 배열 제거). 구조 변경(switch→Dictionary)은 Phase 2 예정
- IUnitFactory 인터페이스 도입 완료 (2026-06-26) — `IGameServices.GetUnitFactory()` 반환 타입을 `UnitFactory`(Infrastructure 구체) → `IUnitFactory`(Application 인터페이스)로 변경하여 Application → Infrastructure 역방향 의존 제거. 신규 `Application/Interfaces/IUnitFactory.cs`(3 멤버). 의존성 역전 패턴(인터페이스는 Application, 구현은 Infrastructure)을 새 추상화 작업의 기본 방식으로 적용할 것. 동작 변경 없음, 싱글/멀티 실기 PASS. 브랜치 `claude/code-refactor-cleanup-jsa24o`
- Android AAB 빌드 용량 최적화 완료 (2026-07-15, main 반영) — `codex/asset-size-optimization`에서 AAB **190.66 MB → 125.30 MB** 절감. 핵심은 3D 건물/유닛 텍스처 Android max texture size `1024 → 512`; `_Old` 미사용 에셋, normal-map PNG, roughness PNG 정리와 보수적 FBX import 조정도 수행. UI 배경/초상화/건물 아이콘/UI 스프라이트/TMP 폰트는 품질 확인 전 유지. 상세/롤백 기준은 `Assets/_Project/Docs/AABSizeOptimization.md`.
- 인게임/로비 볼륨·음소거·프로필 UI 로직 연결 교훈 (2026-07-09, 실기 PASS) — ① **음소거는 저장값 보존형**: Master 채널만 -80dB로 눌러 전체 무음, BGM/SFX 논리 볼륨값(PlayerPrefs)은 보존 → 언뮤트 시 원복. mute 플래그 + PlayerPrefs `"Muted"` 영속화(AudioManager `SetMuted/IsMuted/ResetAllVolumes`, GameSystemRules_Sound 규칙 27). ② **프로그램적 슬라이더 값 설정은 `SetValueWithoutNotify`** — `slider.value=`는 onValueChanged 발화로 자동 언뮤트 부작용. ③ **VerticalLayoutGroup 형제 크기 불균등은 `ChildForceExpandHeight`만으론 부족** → 빈 래퍼(선호높이 0) vs 콘텐츠 형제 불균형은 `LayoutElement.preferredHeight=0`+`flexibleHeight=1` 비율 가중치로 해결(고정 픽셀 금지). ④ **GameObject 재부모화는 파괴/재생성 대신 `Transform.SetParent()`** → 기존 Serialized 참조(fileID) 안 깨짐. ⑤ **Editor 자동 배선의 이름 기반 매칭 오연결 위험**(`_backButton`이 `OffButton`에 잘못 연결된 사례) → 참조 적으면 수동 배선이 안전. task: `_Tasks/2026-07-09/06_09_ingame-lobby-volume-profile-ui/`, `.../09_58_lobby-setting-tab-wiring/`
- 사운드 시스템 실기 버그 3종 수정 교훈 (2026-07-08) — ① BGM 크로스페이드 중단 시 `StopCoroutine`만으로는 페이드아웃 채널의 AudioSource가 계속 재생되어 이전 BGM이 겹침 → active가 아닌 채널을 즉시 `Stop()`해야 함(GameSystemRules_Sound 규칙 8 명문화). ② 에디터 스크립트로 TMP 폰트 지정 시 `EditorUtility.SetDirty()` 없으면 씬 저장에 반영 안 됨. ③ `AudioMixer.SetFloat`은 실패 시 조용히 false 반환 → 진단 로깅 필요. task: `_Tasks/2026-07-07/12_28_sound-system-bugfix/`
- Google 로그인(GPGS) SHA-1 교훈 (2026-06-27) — GPGS `signIn()`이 성공하려면 SHA-1이 ① Firebase Console(OAuth 클라이언트) ② Play Console GPGS 사용자 인증 정보 ③ **실제 빌드 키스토어** 세 곳에서 모두 일치해야 한다. 실기 즉시 `Canceled`/DEVELOPER_ERROR 발생 시, logcat의 `PlayGamesServices[SignInAuthenticator]` 태그 `Cert SHA1 fingerprint`(=APK 실제 서명 SHA-1)를 먼저 확인해 등록값과 대조할 것. 근본 원인은 실제 빌드 키스토어가 등록 시점 키스토어와 다른 파일이어서 실제 서명 SHA-1이 미등록이었던 것. 추가로 최초 로그인은 `Authenticate()`(세션 확인만) 아닌 `ManuallyAuthenticate()`(실제 signIn)를 호출해야 함(GPGS Plugin 2.1.0). 잔여: UGS OIDC 브릿지 `id provider not found`(UGS Dashboard OIDC 제공자 미등록, 멀티플레이 제한, 별도 미해결). task: `_Tasks/2026-06-27/12_26_google-login-debug/`
### 2026-07-16 - Current auth/profile state

- `codex/profile-cloudsave-leaderboard-port` completes the first lobby profile/ranking cloud slice and is intended for merge to main.
- Completed: UGS Cloud Save profile service/use case, UGS Leaderboards ranking service/use case, ProfileView stats/nickname/rank UI, NicknameSetupView, NicknameChangePopup, RankingView/RankRowView, editor setup scripts, scene/package wiring.
- Next task: email verification flow cleanup. EmailVerifyView should receive the attempted email explicitly and handle unverified sign-up abandonment separately from existing unverified-login retry.
