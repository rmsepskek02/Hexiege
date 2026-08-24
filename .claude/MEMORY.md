# 에이전트 공용 컨텍스트

> **모든 에이전트는 작업 시작 전 이 파일을 반드시 읽을 것.**
>
> 이 파일은 **시스템 프롬프트에 자동 주입되지 않는다.** 각 에이전트 정의 `.claude/agents/<이름>.md` 의 「작업 시작 전 반드시 `Read` 할 것」 지시를 따라 **`Read` 로 직접 열어야** 도달한다(그 지시를 2026-08-21에 6개 정의 파일 전부에 넣어 위 문장이 비로소 사실이 되었다).
> 그리고 **여기서부터** 문서 인덱스 `AGENTS.md`(→ 아래 「주요 문서 경로」)와 각자의 `.claude/agent-memory/<이름>/MEMORY.md`(→ 아래 「에이전트별 MEMORY.md 경로」)로 이어진다. 이 파일이 그 두 곳으로 가는 **분기점**이다.

### 자동 주입 경계 (2026-08-21 프로브 실측)

qa-tester · game-design-lead · asset-prompt-crafter · project-orchestrator 4개 에이전트에게 **도구를 하나도 쓰지 말고 시스템 프롬프트 내용만으로 답하라**고 지시해 확인했다. 4건 결과가 일치한다.

| 층 | 에이전트 시스템 프롬프트에 자동 주입되는가 |
|---|---|
| `CLAUDE.md` | ✅ **예** — 규칙 번호 · `2026-03-03` 날짜 · 체크리스트 항목까지 정확히 인용했다 |
| 에이전트 정의 `.claude/agents/<이름>.md` | ✅ **예** — 단, **자기 것만** (6개가 각각 별도 파일) |
| `AGENTS.md` | ❌ 아니오 — 문서 인덱스인데 도달 경로가 **이 파일 경유뿐**이다 |
| `.claude/MEMORY.md` (이 파일) | ❌ 아니오 |
| 각 에이전트 `MEMORY.md` | ❌ 아니오 |

- 즉 메모리는 **잘려서 일부만 보이는 것이 아니라 애초에 0행이 실린다.** 에이전트가 메모리를 아는 유일한 방법은 `Read` 이고, **`Read` 에는 200행 제한이 없다.**
- **자동 주입되는 것은 다른 곳에 옮겨 적지 않는다** — 사본은 원본이 바뀌는 순간 조용히 거짓이 된다. `CLAUDE.md` 는 사용자·메인 세션의 소통용 기초 문서이며 자동 주입은 부수효과다.
- ⚠️ **에이전트 프롬프트에 적힌 사실 주장은 검증 없이 인용하지 않는다.** `lines after 200 will be truncated` 한 줄을 검증 없이 믿어 여러 문서로 퍼뜨린 것이 2026-08-21 정정 작업의 원인이다.

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

## 🔴 에이전트 메모리 갱신 규칙 (예외 없음)

1. 자기 `MEMORY.md` 갱신은 **`Read` → `Edit`**. **`Write` 로 파일 전체를 다시 쓰지 않는다.**
2. **비어 있다고 가정하지 않는다** — 갱신 전 반드시 먼저 읽는다.
3. 기존 항목 **삭제는 그 내용이 틀렸다고 확인했을 때만** 하고 **지운 이유를 함께 남긴다.** "정리했다"·"간결하게 줄였다"는 삭제 사유가 못 된다.
4. **`MEMORY.md` 는 「인덱스 + 매 작업마다 필요한 것」만 담는다.** 지나간 작업 기록·세부 사항은 토픽 파일로 빼되 **반드시 인덱스에서 링크한다.**
   - **1차 기준은 성격이다** — 이유는 절삭이 아니라 **매 작업마다 읽는 파일이라 가벼워야 하기 때문**이다. (종전 "200행 초과분은 잘려서 안 보인다"는 **거짓으로 확인됐다** → 위 「자동 주입 경계」)
   - **2차 기준은 경고선 250행.** 실측 근거: 인덱스 구조가 잡힌 쪽이 game-programmer 132 · document-manager 131 · asset-prompt-crafter 166, 이력이 쌓인 쪽이 game-design-lead 254 · project-orchestrator 325 · qa-tester 502(2026-08-21 기준).
   - **토픽 파일에는 상한을 두지 않는다** — 필요할 때만 선택적으로 읽으므로. (`work-history.md` 819행은 문제가 아니다)
5. **링크 없는 토픽 파일은 존재하지 않는 것과 같다.** 자동 주입이 없다는 것이 밝혀진 지금 **오히려 더 중요해졌다** — 인덱스의 링크가 토픽 파일에 도달하는 **유일한 발견 경로**다. 링크를 빠뜨리면 그 파일은 아무도 찾지 못한다.
6. 토픽으로 옮길 때는 **에이전트 폴더 전체 행수 합이 줄지 않아야 한다** — 이동과 삭제를 구분하는 유일한 검증법.

> **실측 사고 기록(실제 손실):** 2026-08-17 `675203ae` game-programmer **-378행**(미복구) · 2026-08-20 `bcf45ec1` 같은 파일 **-18행**(`405538c7` 복원) · game-programmer 고아 토픽 **16개 1,839행**. 손실 경로가 ①덮어쓰기 ②고아 토픽(링크 누락)으로 **서로 다르므로 위 6개 항목을 모두 지킨다.** (커밋 해시·증감 수치는 당시 호출 세션이 측정한 값이며, 규칙 5로 git 명령을 쓸 수 없어 이 문서에서 재검증하지 않았다.)
>
> **품질 항목(손실 아님):** **세분화 권장 3개** — qa-tester 502 · project-orchestrator 325 · game-design-lead 254. 종전에는 이것을 "200행 초과 = 잘려서 안 보임"으로 적어 **손실 위험**으로 분류했으나 **2026-08-21 프로브로 그 전제가 거짓임이 확인됐다.** 잘리는 것이 아니라 **읽는 비용**의 문제이므로 **손실 방지가 아니라 품질 개선(오해 방지) 항목**이다.
>
> 🔴 **그 결과 드러난 사실: 「3개 에이전트 1,081행 분산」은 애초에 불필요한 작업이었다.** 없는 문제를 고치려고 1,081행을 옮길 뻔했고, **그 이동 자체가 2026-08-17과 같은 모양의 실제 손실 위험**이었다. 전제를 검증하지 않은 채 착수하는 대량 이동이 가장 위험하다.

---

## 에이전트별 MEMORY.md 경로

| 에이전트 | MEMORY.md 경로 |
|---------|---------------|
| game-programmer | `.claude/agent-memory/game-programmer/MEMORY.md` |
| game-design-lead | `.claude/agent-memory/game-design-lead/MEMORY.md` |
| qa-tester | `.claude/agent-memory/qa-tester/MEMORY.md` |
| asset-prompt-crafter | `.claude/agent-memory/asset-prompt-crafter/MEMORY.md` |
| project-orchestrator | `.claude/agent-memory/project-orchestrator/MEMORY.md` |
| document-manager | `.claude/agent-memory/document-manager/MEMORY.md` |

---

## 주요 문서 경로

| 문서 | 경로 |
|------|------|
| 프로젝트 현황 | `Assets/_Project/Docs/PROJECT_STATUS.md` |
| 로드맵 | `Assets/_Project/Docs/ROADMAP.md` |
| 기획서 | `Assets/_Project/Docs/GameDesignDocument.md` |
| 기술설계 | `Assets/_Project/Docs/TechnicalDesignDocument.md` |
| 작업 사이클 규칙 | `Assets/_Project/Docs/WORKFLOW.md` — **자동 주입 없음.** `CLAUDE.md` 체크리스트 [1]이 읽으라고 지시하는 대상이므로 경로를 반드시 유지한다 |
| 에이전트 & 문서 인덱스 | `AGENTS.md` — **자동 주입 없음.** 필요한 문서를 찾아갈 때의 인덱스이며, 도달 경로는 이 파일 경유뿐이다 |

> `CLAUDE.md` 는 **자동 주입되므로 이 파일에 옮겨 적지 않는다**(종전의 「절대 규칙 참조 → `CLAUDE.md`」 절은 2026-08-21에 이 사유로 제거). `WORKFLOW.md` 는 자동 주입되지 않으므로 위 표에 경로를 남겼다 — 종전의 「작업 사이클 상세 참조」 절은 이 표 행과 같은 내용이라 표로 합쳤다.

---

## 좌표계 핵심
- XZ 평면 (Y=0 바닥, Y=높이)
- HexMetrics.HexToWorld() → Vector3(x, 0f, z)
- ViewConverter: Red팀 좌표 반전 `2*center - pos` (X, Z만 반전, Y 보존)
- ViewConverter.Setup()은 LoadMap() 내 렌더링 전에 호출 (ApplyConfig() 직후)

---

## 공통 중요 교훈
- **MistShrine 물안개 힐 구현 완료 · 에디터 싱글플레이 실기 검증 완료 / ⚠️ 멀티 미검증 (2026-08-12)** — 전용 `MistShrineUseCase`(물안개 인스턴스 목록 + 물안개별 독립 누적기 + **매 틱 대상 재수집**, `_activeTimedEffects` HoT/DoT 목록 미사용 → 규칙 14 독립 채널 구조적 보장) + `BuildingPanelBase` 상속 전용 패널 + 건물 회복 경로 신설(`BuildingData.Heal` + **전용 동기화 RPC**로 검증된 유닛 힐 경로 무변경). **로그 실측 검증**: 범위 원 경계 = 실제 회복 판정 일치(회복 최대 2.29 / 탈락 최소 3.12 vs 반경 3.00, 모순 0건) · 범위 이탈 즉시 끊김 · 매 틱 +10 · 중첩 해소 1회 + 동률 시 Id 작은 쪽. **⚠️ 과대 표기 금지 — 멀티 실기 미검증**(범위 판정 코드는 싱글·멀티 공유라 판정 로직은 유효하나 **HP 동기화·클라 표시·RPC 팀 검증·쿨다운 미러는 멀티 고유 경로로 실행된 적 없음**) · **물안개 VFX 미제작(물안개가 눈에 안 보임)** · 버튼 아이콘 미제작 · **밸런싱 수치 전부 임시값**. ⚠️ **교훈 3가지**: ① **"중첩 해소 코드를 넣었다" ≠ "중첩 해소가 동작한다"** — 물안개마다 틱 위상이 달라(실측 `.36x` / `.73x`, 동시 틱 0건) "이번 프레임 발화분끼리" 비교하는 해소 코드가 **비교 대상 1개뿐이라 죽은 코드**였고, 겹친 물안개 2개가 **초당 회복량 2배**를 만들었다(3초에 +60, 기댓값 +30). 판정 로직은 **그 로직이 실제로 실행되는 조건까지** 확인할 것. 수정=**활성 물안개 위상 정렬 + 소유권 판정을 발화 여부와 분리**(커밋 `be17148`), 불변식을 **규칙 8-1에 보강 기재**(문서에 없으면 되돌려짐). ② **직렬화 필드에 잘못 저장된 값은 코드 기본값 수정으로 절대 안 고쳐진다**(Inspector 우선 원칙) — 고치는 법은 씬/에셋 값 직접 수정 / **필드명 변경으로 옛 직렬화 키 버리기**(`[FormerlySerializedAs]`를 일부러 안 붙이는 것이 수정 그 자체) / 런타임 무시 경로. ③ **비활성 상태로 씬에 저장된 오브젝트는 `Show()`의 `SetActive(true)` 순간 `Awake`가 처음 실행**되어, `Awake`의 `SetActive(false)`가 켜자마자 자기를 다시 끈다 → 런타임 전용 플래그로 자기 비활성화만 건너뛸 것(`MistShrineRangeIndicator`·`SkillAimReticle` 동일 함정). 진단 로그는 `848d891`→`939bd87`→`be17148`→`cfe73bb`(전량 제거), 로그 파일은 LogRules대로 `_Logs/2026-08-10/14_12_mistshrine-heal-implementation/RuntimeLog_host.txt`에 보존. task `_Tasks/2026-08-10/14_12_mistshrine-heal-implementation/`(Plan §9-4 · §10), 규칙 `GameSystemRules_Buildings.md`(8-1 보강·상태 정정)·`GameSystemRules_UI.md`(상태 정정).
- **건물 파괴 시 열린 패널/조준 UI 원복 구현·실기 PASS (2026-08-08, 커밋 `8c7fa01`)** — 건물 패널 4종(생산/건물액션/스킬/연구) 공통 베이스 `BuildingPanelBase`가 `GameEvents.OnBuildingDied` 구독→파괴 건물이 현재 표시/조준 중 건물(`_currentBuilding.Id` 매칭, **`IsOpen` 아님** — 조준 중엔 `_popup.Hide()`로 IsOpen=false)이면 `Close()`→각 패널 `OnBeforeClose`로 조준 취소·랠리 마커 숨김 자동 연계. 코드 변경 `BuildingPanelBase.cs` 1파일(자식 무변경). 멀티=`NetworkCombatController.HandleBuildingDied` 클라 재발행 커버. ⚠️ **교훈: MonoBehaviour 베이스에서 자식(`ResearchPanelUI`)이 자체 `OnDestroy`를 선언하면 베이스 `OnDestroy`가 은닉(hide)되어 베이스 해제 로직이 누락되므로, 베이스의 이벤트 구독 해제는 신설 `OnDestroy`보다 `.AddTo(this)`(UniRx, 관용 패턴: BuildingFactory/UnitFactory/HitPresentationQueue)가 안전.** 규칙 `GameSystemRules_UI.md`(공통 UI 팝업 규칙 11 신설). task `_Tasks/2026-08-08/07_40_building-death-ui-restore/`.
- 스킬 시스템 **타입 C(전역 상태변경: 버프/디버프/CC/힐) Phase 2 구현·실기+멀티(클라) 테스트 PASS (2026-08-05)** — 스킬 메커니즘 3종(A/B/C) 전부 완료. Domain `Status/{StatusEffectKind,StatusEffect,UnitStatusState}`(순수 계산) + Application `Services/StatusEffectSystem`(서버 권위 부여/틱) + `Skill/GlobalStatusChangeExecutor`(조준 없음 전역 즉시). 유효 스탯 접근자(`EffectiveAttack`/`GetUnitMoveSpeedMultiplier`)에 상태 배율을 연구 강화 배율과 **곱연산 합성** + 공격 게이트 `CanAttack`(빙결/기절 시 데미지 봉쇄) — **무상태면 배율1·CanAttack true라 기존과 완전 동일(회귀 안전).** 빙결=이속0+`Animator.speed=0` 애니 정지(`UnitAnimState.Frozen`·`OnUnitFreezeChanged` 클라 동기화), 둔화=이동 코루틴 매 프레임 유효 배율 재조회로 라이브(전투종료 정렬 Lerp 구간만 캡처값 미세 잔여), 회복=기존 HoT 재사용, 멀티=`StatusAppliedClientRpc`(회복은 HP 동기화로 재현). UI 버튼 균일화=CostContainer를 SetActive(false) 대신 CanvasGroup alpha=0로 숨겨 행 높이 보존. **정리(cleanup): 개발용 진단 로그 코드를 제거하며 `IRuntimeLogSink`/`RuntimeLoggerSink` 삭제(상시 기능 아님), 로그 파일은 LogRules대로 보존, 좌표화 주석 비활성 코드 3곳 삭제.** ⚠️ **교훈: 로그 작업 착수 전 반드시 `Docs/LogRules.md`(RuntimeLogger 파일 기록·raw Debug.Log 금지)를 먼저 확인할 것** — 이번에 이를 뒤늦게 준수해 진단 로그를 걷어냄. **건물 파괴 시 스킬 패널/조준 UI 원복 = 2026-08-08 구현 완료·실기 PASS**(BuildingPanelBase OnBuildingDied 구독→현재 건물 Id 매칭 시 Close, 4개 건물 패널 공통, 구독 해제 `.AddTo(this)`). **남은 것: 구체 스킬 목록/수치/아이콘(기획, 현재 종족별 플레이스홀더 5슬롯 테스트용) · 둔화 전투종료 정렬 Lerp 잔여만.** task `_Tasks/2026-07-28/12_14_skill-building-system-design/`(Plan/Research 하단 "Phase 2 완료 결과"), 규칙 `GameSystemRules_Skills.md`(13, 구현 상태 갱신).
- 스킬 건물 시스템 Phase 1(타입 A 즉발 범위 피해·B 장판 DoT + 프레임워크 + 3×3 패널 UI/쿨다운 오버레이 + 모바일 탭 조준) 구현·**실기기 테스트 PASS (2026-08-04)**. 이번 사이클 핵심 4건: ① **조준 지점 좌표화** — 조준 중심을 타일 스냅(HexCoord) → 연속 도메인 월드 Vector3로. **착탄 반경 판정은 원래 연속 원(중심 월드+반경 유클리드)이라 무변경, "중심 입력"만 연속화.** 서버 재검증도 유효타일(HasTile) → 맵 경계 안 점(point-in-bounds, `Core/HexMetrics.{ComputeMapWorldBounds,IsWithinMapBounds,ClampToMapBounds}` 신규, 최외곽 타일 바깥선까지 엄밀 clamp). HexGrid(Domain) Vector3 불가 → Core 수학+클로저 주입(GameBootstrapper `_grid` 캡처=맵 재로드 대응). RPC=NGO 2.9.2 Vector3 기본 직렬화(int q,r 폐지). ② **조준원 지면 데칼 렌더링** — 원인=조준원(y=0.05)과 HexTile(ProBuilder 실린더) coplanar z-fighting. 신규 셰이더 `Assets/_Project/Shaders/SkillAimOverlay.shader`(ZTest LEqual + Offset -1,-1 + ZWrite Off + Cull Off) = 지형엔 안 가려지고 불투명 유닛/건물엔 가려지는 데칼(**ZTest Always 금지** — 유닛/건물까지 덮어 규칙 22-1 위반). 셋업 스크립트가 머티리얼 자동 생성·배선, **씬 재셋업 필요**. ③ **취소 버그 근본 수정** — 실기(Android)에서 취소 X 위에서 손을 떼도 발동·쿨다운. 원인=손 뗀 프레임에 `TryGetPointerScreenPos` 마우스 분기가 합성 마우스 좌표(0,0)를 유효로 반환해 캐시 폴백을 가로챔 → release 프레임엔 캐시된 마지막 드래그 좌표(`_lastDragScreenPos`)로만 판정. ④ **쿨다운 안내 토스트** — 기존 ToastUI(에셋 기반)에 `ToastKey.SkillOnCooldown` + `ToastMessageConfig.asset` key:4 "스킬이 쿨다운 중입니다". **미완(당시 기준·과대 표기 금지):** 타입 C(전역 상태변경)는 당시 enum만 선언·실행기 미구현이었으나 **2026-08-05 Phase 2로 구현 완료(위 항목 참조)** · 건물 파괴 시 패널/조준 UI 원복은 **2026-08-08 구현 완료(위 항목 참조)** · 구체 스킬 목록/수치(기획) 보류. 규칙 `GameSystemRules_Skills.md`(1~26). task `_Tasks/2026-07-28/12_14_skill-building-system-design/`·`_Tasks/2026-08-04/04_46_skill-aim-coordinate-based/`.
- 연구소 유닛 강화 시스템 + 전투 스탯 ×10 + 연구 패널 UI 구현·멀티 실기 완료 (2026-07-31). ① 방어 감쇄 = 순수 함수 `Domain/Combat/DamageCalculator.ApplyDefense`(K=120, floor 1, 하드캡 65%, `raw<=0`·`defense<=0`이면 원본 반환→하위호환). ② 팀 배율 상태 = Application `UnitUpgradeUseCase`(선례 `ResourceUseCase._incomeMultipliers`), (B) 소급 강화 = 유닛 스냅샷 미변경·사용 지점 조회. ③ 네트워크 `Infrastructure/NetworkUpgradeController` — 완료 레벨 양 클라 브로드캐스트/진행 소유자만/파괴 100% 환불, **`OnNetworkSpawn` 서비스 미등록 스폰 레이스는 `ResolveServices()` 지연 재조회로 수정**. ④ **연구 패널 UI = `ResearchPanelUI : BuildingPanelBase` + 매트릭스/진행 2-레이어(연구소 단위)** — 초기 "생산 패널 패턴"에서 확정 변경(규칙 13). ⑤ **×10은 config `.asset`에 ×10 커밋 반영(적용에 쓰였던 셋업 스크립트는 역할 종료 후 제거됨)**(1회 실행 필요, Inspector 값 우선). 후속 보류(과대 표기 금지): UI 레이아웃·헤더 아이콘·AI 연구 실기·MistShrine 힐(미구현)·싱글 자연회복 실기. task `_Tasks/2026-07-22/10_08_unit-upgrade-system/`, 규칙 `GameSystemRules_Upgrade.md`.
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
### 2026-07-16 - Email verification flow cleanup

- Email verification screen now receives explicit context via `LoginRootView.ShowEmailVerify(email, origin)`.
- `EmailVerificationOrigin.SignUpPending` means a just-created Firebase email user is waiting for verification; back/cancel should confirm and delete the current unverified Firebase user.
- `EmailVerificationOrigin.ExistingUnverifiedLogin` means an existing unverified account attempted login; back should sign out and return to login without deleting the Firebase user.
- Task docs: `Assets/_Project/Docs/_Tasks/2026-07-16/14_20_email-verification-flow-cleanup/`.

### 2026-07-18 - Email verification flow complete

- User device verification passed: explicit email display, signup cancel confirmation, Firebase unverified user deletion, continue verification staying on screen, relaunch from verification returning to verification, and relaunch from nickname setup returning to nickname setup.
- Auto login now gates unverified email accounts back to verification and verified-but-no-nickname accounts back to nickname setup before Lobby.
- `SplashOverlay` must skip fade only for Lobby scene transition; Login-scene panel callbacks need fade out so the overlay stops blocking UI.
