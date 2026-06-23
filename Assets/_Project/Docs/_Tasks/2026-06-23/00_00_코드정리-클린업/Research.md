# Research — 코드 정리(클린업) Phase 1

## 이 작업이 무엇이고 왜 하는가 (자연어 설명)

이 작업은 Hexiege 코드 안에 쌓여 있는 "히스토리성 주석"과 "이미 사라진 코드에 대한 설명"을 걷어내고, 비어 있는 섹션 헤더와 중복된 코드 한 곳을 정리하는 작업입니다.

개발을 진행하면서 코드 안에 `// [2026-05-20] ...`, `// [Phase 2] ...` 같은 "언제 무엇을 바꿨다"는 메모를 많이 남겨 두었습니다. 이런 변경 이력은 원래 git(버전 관리 시스템)이 기록해 주는 역할이라, 코드 안에 그대로 두면 오히려 코드를 읽을 때 시선을 분산시키고 파일을 길게 만듭니다. 따라서 "변경 이력" 성격의 주석은 지우고, "왜 이렇게 짰는지(WHY)"를 설명하는 진짜 도움이 되는 주석은 그대로 남깁니다.

이 문서는 그중 **Phase 1(즉시 가능한 클린업)** 만 다룹니다. BuildingTypeHelper의 switch를 Dictionary로 바꾸거나 HexMetrics 중복 setup을 제거하는 등 코드 구조를 손대는 **Phase 2(구조 개선)** 는 이 작업의 범위가 아닙니다.

핵심 원칙: 이 작업은 **주석과 빈 줄, 그리고 동작이 동일한 중복 한 곳**만 건드립니다. 실제 게임이 돌아가는 로직(런타임 동작)은 한 글자도 바뀌지 않습니다.

---

## ⚠️ 작업 시작 전 사용자 확인이 필요한 사항 (CLAUDE.md 규칙 10·12)

실제 코드를 읽어본 결과, 이번 요청서의 가정과 **다른 점 두 가지**가 발견되었습니다. 추정으로 진행하지 않고 먼저 보고합니다.

### 확인 1 — 날짜 주석의 실제 분포가 요청서 목록보다 훨씬 넓음

요청서에는 5개 파일이 예시로 적혀 있었으나, 실제로 `[2026-XX-XX]` / `[Phase X]` 형식 주석은 **약 30개 파일에 걸쳐 150곳 가까이** 존재합니다 (UseCases, Infrastructure/Network, Presentation/UI, Domain, Bootstrap 등 거의 전 레이어).

→ **정리 범위를 어디까지로 할지 사용자 결정 필요.**
- (A) 요청서에 명시된 5개 파일 + 명확한 제거 대상만
- (B) 전체 코드베이스의 모든 히스토리성 날짜/Phase 주석

(상세 위치 목록은 본 문서 하단 "부록 A" 참조)

### 확인 2 — `_enableAI` / `_confirmPopup` 블록은 "폐기 코드 설명"이 아니라 의도된 주석일 가능성

요청서는 이 두 블록을 "이미 삭제된 코드에 대한 설명이 남은 것"으로 추정했으나, 실제 내용은 다릅니다 (아래 "제거 대상 2" 참조). 단순 삭제가 적절한지, WHY 주석으로 보존할지 판단이 필요합니다.

---

## 영향 범위 분석 (런타임 동작에 미치는 영향)

| 정리 종류 | 런타임 영향 | 근거 |
|-----------|------------|------|
| 히스토리성 주석 제거 | 없음 | 주석은 컴파일 결과물에 포함되지 않음 |
| 폐기 코드 설명 블록 제거 | 없음 | 이미 주석 처리된 코드 또는 설명 텍스트 |
| 빈 섹션 헤더 제거 | 없음 | 주석 줄 |
| 중복 RaceId 배열 → 단일 선언 | 없음 | `new[] { Human, Spirit, Transcendence }`와 동일 원소·순서를 보장하면 동작 동일 |

전 항목이 컴파일 산출물에 영향을 주지 않거나(주석), 동일 동작을 보장하는 리팩토링(중복 배열)입니다. 단, 중복 배열 개선은 코드 변경이므로 **game-programmer 위임 + 동작 동일성 검증**이 필요합니다(CLAUDE.md 규칙 3·11).

---

## 제거/개선 대상 상세 (실제 코드 확인 결과)

### 제거 대상 1 — 날짜/단계 형식 변경 이력 주석

요청서에 명시된 핵심 파일에서 확인된 실제 내용:

**`GameBootstrapper.cs`**
- 41행: `// [2026-05-20] using Unity.Netcode 제거 — ...` (43행까지 이어지는 블록)
- 79행: `// [Phase 2] UnitAnimationData 제거 — Animator(Mecanim)가 대체`
- 216행: `// [2026-05-15] 혼잡도 기반 분산 시스템 (v2) — 핵심 인스턴스.` (블록 헤더, 이하 설명은 WHY 성격 — 분리 판단 필요)
- 240행: `// [2026-04-30] 새 규칙 4 — 건물 변경 시 즉시 모든 유닛 경로 재계산(eager).` (블록 헤더)
- 468행: `/// [2026-05-20] NetworkManager.Singleton 직접 호출 → NetworkContext...로 단일화.`
- 476행: `// [2026-05-20] ActionDisposable 내부 클래스 제거.` (478행까지)

**`GameBootstrapper.Map.cs`**
- 130행: `// [2026-05-20] IForfeitService 주입 추가:`
- 167행: `// 15. [2026-04-30] 새 규칙 4 — ...` (날짜만 제거, 단계 번호·설명 보존 검토)
- 171행: `// 16. [로딩 인디케이터 끄기] ...` (번호+라벨 혼합 형식)
- 195행: `// [2026-05-15] 혼잡도 시스템 정리.`
- 208행: `// [2026-05-20] 인구 UseCase 이벤트 구독 해제 — ...`
- 216행: `// [2026-04-30] eager 재경로 트리거 구독 정리. ...`

**`GameBootstrapper.Setup.cs`**
- 308행: `// [2026-05-15] 혼잡도 시스템 인스턴스 초기화.` (블록 헤더)
- 319행: `// [2026-05-20] Action → Subject 통일: ...`
- 514행: `// [2026-05-15] CastleApproachManager(v1) 대신 혼잡도 시스템(v2) ...`

**`HexGrid.cs`**
- 49행: `// [2026-05-20] 팀별 소유 타일 카운터 (성능 최적화)` (섹션 헤더 내 날짜)
- 83행 / 167행 / 232행: `/// [2026-05-20] ...` (XML 문서 주석 내 날짜)

**`UnitMovementUseCase.cs`**
- 40행: `// [2026-05-20] 이동 시 위치 역인덱스 동기화를 위해 ...`
- 138행: `// [2026-05-20] 위치 역인덱스 동기화 — ...`
- 148행: `// [2026-04-30] 새 규칙 11/15 — 전투 종료 후 "앞쪽 가장 가까운 타일" 찾기` (섹션 헤더)

**`BuildingType.cs`**
- 25~28행 블록:
  ```
  // [2026-05-20] 각 멤버에 정수 값을 명시 부여.
  //   - 기존 순서와 동일한 값을 그대로 부여하므로 기존 ScriptableObject/Scene 직렬화 데이터에 영향 없음.
  //   - 신규 건물 추가 시 반드시 마지막 빈 번호를 명시 부여하여 기존 인덱스 보존.
  //   - RPC 직렬화(NetworkBuildingController)에서 (int)BuildingType 캐스트가 명시값으로 안정화됨.
  ```
  → 첫 줄(`[2026-05-20] 각 멤버에 정수 값을 명시 부여`)은 이력성. 그러나 2~4번째 줄은 **"왜 명시 정수값을 쓰는가"라는 WHY 설명**이라 보존 가치가 높음. 날짜만 떼고 WHY는 보존하는 방향 권장.

> **제거 기준 (요청서 명시):** `[2026-XX-XX]`·`[Phase X]` 등 변경이력 성격만 제거하고, 유효 코드에 대한 WHY/주의사항 주석은 보존한다. 위 목록에서 "블록 헤더"·"WHY 설명 포함"으로 표시한 항목은 **날짜 토큰만 제거하고 설명 본문은 남기는** 부분 정리가 적절하다.

### 제거 대상 2 — 폐기된 코드 설명 블록 (실제 내용은 요청서 추정과 다름)

**`GameBootstrapper.cs` 71~77행 (`_enableAI`)**
```csharp
// [AIConfig 이전] _enableAI는 AIConfig.enableAI 필드로 이전되었다.
//   이제 AI On/Off는 Resources/Config/AIConfig.asset의 enableAI 값으로 결정한다.
//   (Project 창에서 씬을 열지 않고도 토글 가능 → 테스트 편의성)
//   검증 안전을 위해 삭제 대신 주석 처리. 사용자 테스트 통과 후 제거 예정.
// [Header("AI 설정")]
// [Tooltip("AI 활성화 여부. ...")]
// [SerializeField] private bool _enableAI = true;
```
→ 요청서 추정(`[Phase 2] ... Phase 3 출시 시 제거 예정`)과 **실제 내용이 다름**. 실제로는 `[AIConfig 이전]` 라벨이며, "삭제 대신 주석 처리, 사용자 테스트 통과 후 제거 예정"이라는 **비활성화 우선 원칙(WORKFLOW.md)** 에 따른 보류 상태. 단순 제거 전 사용자 확인 필요.

**`GameBootstrapper.cs` 174~178행 (`_confirmPopup`)**
```csharp
// [2026-06-18] _confirmPopup SerializeField 제거.
//   기존: GameBootstrapper가 ConfirmPopup을 직접 들고 있었으나 어디에도 주입되지 않는 死(dead) 참조였다.
//   확인 팝업이 필요한 View(InGameSettingsUI)는 자체 _confirmPopup을 보유하고 있으며,
//   전역 확인 팝업이 필요한 경우 UIManager.Instance(IUIManager)를 통해 호출한다.
//   (InGameSettingsUI.Initialize는 IUIManager를 받지 않으므로 파라미터를 추가하지 않는다.)
```
→ 이미 삭제된 필드에 대한 설명. 날짜 라벨은 이력성이지만, 본문은 "왜 이 필드가 없는지 / 확인 팝업을 어디서 얻는지" 알려주는 WHY 성격. 통째 제거할지, 날짜만 떼고 WHY를 남길지 판단 필요.

### 제거 대상 3 — 빈 섹션 헤더

**`NetworkGameFlow.cs` 42~44행**
```csharp
// ====================================================================
// Inspector 설정
// ====================================================================

// ====================================================================
// 내부 상태
// ====================================================================
```
→ `Inspector 설정` 섹션은 헤더만 있고 그 아래 내용이 전혀 없이 바로 `내부 상태` 섹션으로 이어짐. 빈 헤더 3줄 제거 대상. (런타임 영향 없음)

### 개선 대상 4 — 중복 RaceId 배열

**`GameBootstrapper.Setup.cs` `InitializeBuildingStatsFromConfig()` 내부 (112행 시작)**
- 186행: `foreach (var race in new[] { RaceId.Human, RaceId.Spirit, RaceId.Transcendence })`
- 224행: `foreach (var race in new[] { RaceId.Human, RaceId.Spirit, RaceId.Transcendence })`

동일 배열 리터럴이 한 메서드 안에서 2번 생성됨. 개선 방향:
- (a) 메서드 상단에 지역 변수 `var allRaces = new[] { RaceId.Human, RaceId.Spirit, RaceId.Transcendence };` 선언 후 두 곳에서 재사용. (순서·원소 100% 동일 보장)
- (b) `(RaceId[])System.Enum.GetValues(typeof(RaceId))` 사용.
  - ⚠️ 주의: `RaceId` enum의 실제 멤버가 정확히 `Human, Spirit, Transcendence` **세 개뿐인지** 확인 필요. `None`/`Neutral` 등 추가 멤버가 있으면 동작이 달라짐. 확인 전까지는 (a)가 안전.
  - (참고: `GameBootstrapper.cs` 395행은 싱글플레이 상대 종족 선택에 이미 `Enum.GetValues(typeof(RaceId))`를 사용 중. 단 그건 "정의된 모든 종족 중 무작위" 의도라 빈 멤버가 없다는 전제임.)

→ 현재 동작과의 동일성을 보장하려면 **(a) 지역 변수 방식을 기본 권장**. (b)는 RaceId enum 정의 확인 후 사용자 승인 시 채택.

---

## 부록 A — 전체 코드베이스 날짜/Phase 주석 분포 (확인 1 참고용)

요청서 5개 파일 외에 추가로 발견된 파일 (정리 범위 확장 시 대상):

- `Application/UseCases/BuildingPlacementUseCase.cs` (32, 273행)
- `Application/UseCases/UnitProductionUseCase.cs` (188, 557, 601행)
- `Application/UseCases/UnitSpawnUseCase.cs` (38, 125, 139행)
- `Application/UseCases/PopulationUseCase.cs` (10, 30, 86행)
- `Application/Events/GameEvents.cs` (586행), `Application/Events/ToastKey.cs` (15행)
- `Infrastructure/Factories/UnitFactory.cs` (17, 251, 340행), `BuildingFactory.cs` (16행)
- `Infrastructure/Network/NetworkProductionController.cs`, `NetworkCombatController.cs`(627·651·668·683), `NetworkBuildingController.cs`, `NetworkGameEndController.cs`(51·190·317·362·415)
- `Presentation/UI/GameEndUI.cs`, `NetworkStatusUI.cs`, `LobbyUI.cs`, `ConfirmPopup.cs`, `InGameSettingsUI.cs`, `BuildingPlacementUI.cs`, `BuildingPanelBase.cs`, `Common/ToastUI.cs`, `Common/RematchRequestPopup.cs`(다수), `Views/Login/AnonymousWarningPopup.cs`
- `Presentation/Production/ProductionTicker.cs` (61, 189, 270, 576, 637, 704행)
- `Presentation/Grid/HexGridRenderer.cs`, `HexTileView.cs`
- `Presentation/Unit/UnitView.cs` (다수 — 5·12·53·140·198·214·323·339·392·580·593·609·666·796·921·925·1051·1085·1088·1096·1101·1481·1484행 등)
- `Core/ViewConverter.cs` (130행)
- `Domain/Building/BuildingData.cs` (65행)

> 위 목록은 정적 grep 결과이며, 각 항목은 "날짜만 제거" 인지 "WHY 보존" 인지 개별 판단이 필요하다. 범위 확장 결정 시 game-programmer가 파일별로 분류하여 처리한다.

---

## 결론

- Phase 1 클린업은 런타임 동작에 영향이 없는 안전한 작업이다.
- 단, (1) 정리 범위(5개 파일 vs 전체), (2) `_enableAI`/`_confirmPopup` 블록의 처리 방침, (3) RaceId 중복 개선 방식((a) vs (b)) — 세 가지는 **사용자 확인 후** Plan을 확정해야 한다.
- 코드 변경이 포함되므로(중복 배열, 주석 제거) 실제 수정은 game-programmer 위임 + 사용자 승인 후 진행한다(CLAUDE.md 규칙 3·11).
