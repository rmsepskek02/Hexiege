# Research — 스킬 건물 시스템 (구현 관점)

## 이 조사가 무엇인가 (자연어 서두)

Hexiege의 세 종족(Human / Spirit / Transcendence)에는 각각 **스킬 건물**이 하나씩 있습니다. 스킬 건물은 유닛을 뽑거나 자원을 캐는 대신, 플레이어가 직접 버튼을 눌러 전장에 즉각적인 효과(피해·상태 변화 등)를 일으키는 특수 건물입니다. 지금까지 이 세 건물은 화면에 놓이고 얻어맞기만 하는 껍데기였고, 클릭하면 철거 버튼만 있는 범용 패널이 떴습니다.

이 문서는 **"스킬 건물을 실제 코드로 구현하기 위해, 지금 코드베이스에 무엇이 이미 있고(재사용) 무엇을 새로 만들어야 하는지(신규)"를 파일 경로·클래스명 근거와 함께 정리한 조사서**입니다. 설계 규칙 자체(마나 없음, 최대 5개, 3×3 UI, 서버 권위 등)는 이미 `GameSystemRules_Skills.md`(규칙 1~26)에 확정돼 있으므로, 이 문서는 그 규칙을 "어느 기존 시스템 위에 얹을 수 있는가"에 집중합니다.

> **단일 소스(SSoT):** 설계 규칙의 권위 문서는 `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Skills.md`(규칙 1~26). 이 조사서와 Plan.md의 모든 판단은 그 규칙에 근거를 연결합니다.
>
> **범위:** 이번은 **스킬 프레임워크(그릇)** 구현을 위한 조사이며, 구체 스킬 목록·수치(어떤 스킬이 몇 초 쿨다운에 얼마의 피해)는 데이터(ScriptableObject)로 별도 확정합니다. 조사 단계에서 코드/프리팹은 수정하지 않았습니다(읽기만).

---

## 1. 스킬 건물 3종 매핑 (현재 상태)

| 종족 | 스킬 건물 자산명 | 프리팹 | BuildingType enum |
|------|------------------|--------|-------------------|
| Human | FlightFacility | `Building_FlightFacility_Blue/Red` | `FlightFacility` (= 3) |
| Spirit | MagicSpirit | `Building_MagicSpirit_Blue/Red` | `MagicBuilding` (= 5) |
| Transcendence | WillowShrine | `Building_WillowShrine_Blue/Red` | `MagicBuilding` (= 5, Spirit과 공유) |

- 근거: `Assets/_Project/Scripts/Domain/Building/BuildingType.cs` — `FlightFacility = 3`, `MagicBuilding = 5`. 파일 주석에 **"열거형 멤버 순서 변경 시 직렬화(ScriptableObject/Scene) 및 RPC(`(int)` 캐스트) 정합성이 깨진다"** 명시 → 규칙 1(enum 미변경) 준수 필요.
- 세 건물 모두 현재 **배치·피격만 되는 시각 오브젝트**. 스킬 로직·데이터·전용 UI는 전무.
- 클릭 시 범용 액션 패널(`BuildingActionPanelUI`)을 공유하며 현재 철거 버튼만 활성.

---

## 2. 재사용 가능한 기존 시스템 (파일 경로·클래스 근거)

### 2-1. 원형 반경 AoE 판정 헬퍼 → 타입 A/B 재사용 (규칙 11, 12)

- **유닛 수집:** `Assets/_Project/Scripts/Application/Combat/BlastAttackBehavior.cs`
  - `internal static void CollectEnemyUnitsInRadius(SpecialAttackContext ctx, UnitData attacker, Vector3 center, float radiusSqr, List<UnitData> result)` — 착탄 중심에서 XZ 평면 `sqrMagnitude ≤ radiusSqr` 판정, 아군/사망 제외. MushroomBomber·QuakeSpirit이 공유하는 공용 헬퍼(이미 `internal static`).
- **건물 수집:** `Assets/_Project/Scripts/Application/Combat/QuakeAttackBehavior.cs`
  - `internal static void CollectEnemyBuildingsInRadius(...)` — 동일 원형 반경으로 적 건물 수집(아군 건물 제외). 유닛/건물 Id 카운터가 달라 **버퍼 분리**(규칙 29 교훈).
- **어셈블리:** asmdef 없음(전부 Assembly-CSharp) → Application 신규 스킬 실행기에서 위 `internal static` 헬퍼를 **직접 호출 가능**(추가 노출 불필요).
- 좌표 조회는 `SpecialAttackContext.WorldPositionOf(IDamageable)`(내부 `IEntityPositionProvider` 우선, `HexToWorld` 폴백) — 서버 권위 좌표.

### 2-2. DoT 초 단위 틱 시스템 → 타입 B 재사용 (규칙 12)

- `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`
  - `enum TimedEffectKind { Heal, Damage }`, `class ActiveTimedEffect`(TargetId/SourceId/Kind/TotalAmount/Duration/Elapsed/AppliedAmount/TickInterval), `List<ActiveTimedEffect> _activeTimedEffects`.
  - **DoT 진입점**: `ApplyDamageOverTime`(discrete 초 단위 틱, `TickInterval > 0`), 이를 감싼 `ApplyBlastDot`(MushroomBomber, 초당 2/3초)·`ApplyInfernoDot`(InfernoSpirit, 초당 5/3초). 각각 튜닝값이 달라 **별도 진입점**으로 분리돼 있음.
  - **틱 처리**: `public void TickTimedEffects(float dt)` — `TickInterval > 0`이면 discrete 데미지 경로(`TickDiscreteDamageEffect`), 아니면 연속(프레임 diff) 힐 경로. 만료/사망 시 제거.
- 스킬 타입 B(장판)는 이 DoT 부여를 "반경 내 다수 대상"으로 확장한 형태 — 반경 수집(2-1) + `ApplyDamageOverTime` 조합으로 구현 가능.

### 2-3. HoT(지속 회복) 시스템 → 타입 C 회복에 일부 재사용 (규칙 13)

- 같은 `UnitCombatUseCase.cs`:
  - `public void CastHeal(UnitData healer, int targetId)` → `ApplyTimedEffect(healer, target, TimedEffectKind.Heal, _bloomHealAmount, _bloomHealDuration)`.
  - `public void ApplyTimedEffect(UnitData source, UnitData target, TimedEffectKind kind, int totalAmount, float duration)` — HoT 연속 회복 upsert(대상별 1레코드, 재부여=리셋).
  - `ApplyHealToUnit(healer, target, amount, showText)`, `PublishHealCompletionText(...)`, `EntityHealedEvent`/`OnEntityHealed`, `NetworkHealthSync.SyncHealClientRpc`(힐 HP 증가 동기화).
- 타입 C 회복은 **우선 전역 즉시(조준 없음)** 방식으로 이 HoT 경로를 재사용 가능(규칙 13, 규칙 15). 단 대상이 "부상 아군 전체"이므로 대상 선정 로직은 신규.

### 2-4. 지점 지정 입력의 선례 → 지점 조준 확장 기반 (규칙 16, 17, 19)

- `Assets/_Project/Scripts/Presentation/Input/InputHandler.cs`
  - `HandleClick(screenPos)` **최상단 분기**가 랠리 모드 선례: `_productionUI.IsSettingRallyPoint && Time.frameCount != _productionUI.RallyPointSetFrame`이면 `ScreenToXZPlane` → `ViewConverter.FromView` → `HexMetrics.WorldToHex` → `CompleteRallyPointSetting(coord)`. **UI 히트 판정(IsPointerOverUI)보다 먼저** 처리.
  - `ScreenToXZPlane(Vector2)` — XZ 평면(Y=0) 레이캐스트 헬퍼(스킬 조준점 월드 좌표 변환에 재사용).
- 랠리 모드 진입/완료: `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs`
  - `bool IsSettingRallyPoint`, `int RallyPointSetFrame`(프레임 가드), `OnRallyPointClick()`(플래그 set + 팝업 hide), `CompleteRallyPointSetting(HexCoord)`.
  - **버튼 PointerDown/PointerUp 패턴**(누른 채 조준의 선례): `SetupUnitButtonBySlot`가 `EventTrigger`의 `PointerDown`/`PointerUp` 엔트리로 유닛 버튼 입력을 처리 → 스킬 버튼의 "누른 채 드래그 → 손 떼면 발동"(규칙 17~19)의 입력 훅으로 확장 가능.
- **차이(신규 필요):** 랠리는 "탭으로 1점 지정"이지만, 스킬은 "누른 채 드래그 추적 + 조준점(범위 원) 이동 + 엣지 스크롤 + 손 떼면 발동/취소"라 상태가 더 풍부 → InputHandler 분기만으로는 부족, 전용 조준 컨트롤러 신설이 자연스러움(Plan 참조).

### 2-5. 범용 건물 패널(3×3 그리드) → 스킬 버튼 슬롯 1~5 확장 기반 (규칙 8, 9)

- `Assets/_Project/Scripts/Presentation/UI/BuildingActionPanelUI.cs`
  - `[SerializeField] List<Button> _allSlotButtons`(9칸), `[SerializeField] List<Button> _activeSlotButtons`(현재 DestroyButton 1개), 내부 `List<CanvasGroup> _slotCanvasGroups`.
  - `OnShow(BuildingData)` 오버라이드에서 전체 슬롯 alpha=0 → 활성 슬롯만 alpha=1. **CanvasGroup.alpha 제어**를 쓰는 이유: `SetActive(false)`는 GridLayoutGroup 레이아웃에서 제외돼 정렬이 깨지므로 alpha/interactable/blocksRaycasts만 0으로.
- 베이스 `Assets/_Project/Scripts/Presentation/UI/BuildingPanelBase.cs`
  - `Show(BuildingData)`/`Close()` 생명주기 + `OnShow`/`OnBeforeClose`/`BeforeDemolish` 훅, `_headerText`, 철거/환불, `ClosedFrame`(같은 프레임 클릭 차단), `IGameUI`(OnGameStarted/OnGameEnded에서 Close).
- **확장 지점:** 스킬 건물은 슬롯 1~5에 스킬 버튼, 슬롯 6 철거(고정), 7~9 예약(규칙 9). 현재 `_activeSlotButtons`가 정적 리스트라 **건물 타입/종족별로 동적으로 채우는 로직**이 신규 필요. **결정 확정: 전용 `BuildingSkillPanelUI : BuildingPanelBase` 신설**(기존 `BuildingActionPanelUI`는 비스킬 건물 전용으로 유지, 변경 없음). `BuildingPanelBase`의 Show/Close/철거/헤더/`ClosedFrame`/`IGameUI`는 재사용.

### 2-6. 서버 권위 전투/RPC 패턴 → 스킬 발동 RPC 참고 (규칙 25, 26)

- `Assets/_Project/Scripts/Application/UseCases/TowerCombatUseCase.cs`
  - `class TowerCombatUseCase`, `public void Tick(float dt)`, `IEntityPositionProvider _positionProvider` — **서버에서만** 타워 순회·판정. 스킬 발동/판정도 동일하게 서버 UseCase에서 실행(규칙 25).
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkBuildingController.cs`(NetworkBehaviour, Infrastructure)
  - **RPC 래퍼 패턴**: `public void RequestDemolish(int id)` → `RequestDemolishServerRpc(id)`. UI(Presentation)는 래퍼만 호출(NGO 직접 의존 회피).
  - **서버 재검증 템플릿**(`RequestBuildServerRpc`): ① `_services`(IGameServices, OnNetworkSpawn 캐시) null 체크 → ② UseCase null 체크 → ③ 발신자 팀 소유권(`rpcParams.Receive.SenderClientId` → Host=Blue/Client=Red) → ④ 자원/조건 검증 → ⑤ 서버에서 UseCase 실행 → ⑥ `ClientRpc` 전파. `[ServerRpc(RequireOwnership = false)]`, 메서드명 `...ServerRpc`/`...ClientRpc` 접미사.
- 스킬 발동 RPC는 이 템플릿을 그대로 따르되 재검증 항목만 교체: **건물 생존 / 건물 글로벌 쿨다운 만료 / (지점형) 전송 좌표가 유효 맵 타일**(규칙 26).

### 2-7. 건물 스탯·타입 데이터

- `Assets/_Project/Scripts/Domain/Building/BuildingType.cs` — enum(위 1장).
- `Assets/_Project/Scripts/Domain/Building/BuildingData.cs` — `IDamageable`. `Id/Type/Team/Position/MaxHp/Hp(private set)/IsAlive`. **`float AttackCooldownRemaining { get; set; }`**(현재 AutoTower 전용, `TowerCombatUseCase`만 사용). **결정 확정: 스킬 글로벌 쿨다운은 `BuildingData`가 아니라 `SkillActivationUseCase`의 `Dictionary<int,float>`로 보관**(BuildingData 변경 없음).
- **건설 진입점은 이미 가능**: 세 스킬 건물은 현재도 정상 배치·건설된다(범용 건물 배치 경로 공유) → 배치/건설 로직 추가 작업 없음.
- `Assets/_Project/Scripts/Domain/Building/BuildingStats.cs` + `Assets/_Project/Resources/Config/BuildingStatsConfig.asset` — 건설 비용(세 건물 모두 200 골드, Inspector 값 우선), `BuildingStats.GetTotalInvestedCost(type, race)`(철거 환불).

### 2-8. 카메라 조작(팬/줌) → 엣지 스크롤 확장 대상 (규칙 18, 23)

- `Assets/_Project/Scripts/Presentation/Camera/CameraController.cs`
  - `SetBounds(center, size)` → `_mapBounds`/`_hasBounds`. `SetPosition(pos)`. Orthographic + X 55도 틸트.
  - **`ClampPosition()`가 이미 맵 경계 정지를 구현**: 카메라 위치 → 화면 중심 지면 look-at 좌표 변환 → 타일 경계 내 clamp(줌 레벨 `halfW`/`halfH` 반영) → 역변환. **엣지 스크롤이 이 ClampPosition을 그대로 재사용하면 규칙 23(맵 경계 정지)이 자동 충족**.
  - 팬은 `HandlePan()`의 드래그(월드 좌표 차이). 엣지 스크롤은 "화면 가장자리 근접 시 방향 팬"이라 **신규 팬 메서드**(예: `EdgeScroll(Vector2 screenPos, float dt)`)가 필요하나, 이동 후 `ClampPosition()`을 그대로 태우면 경계 처리 재사용 가능.

### 2-9. GameBootstrapper 주입 흐름 (규칙: 유일한 조합 루트)

- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` / `GameBootstrapper.Setup.cs` / `GameBootstrapper.Network.cs`.
  - `IGameServices`(Application 인터페이스) 구현 — `GetBuildingPlacement()`/`GetResource()`/`GetUnitFactory()` 등. `NetworkBuildingController._services`가 이걸 캐시.
  - `InputHandler.Initialize(...)`, `BuildingActionPanelUI.Initialize(buildingPlacement, resource, networkBuildingController)` 등 Presentation 의존성 주입 지점.
  - `SpecialAttackConfig`(SO)의 float 튜닝값을 **`UnitCombatUseCase` 생성자에 float로 주입**(Application→Infrastructure 역참조 회피). 스킬 UseCase/데이터 주입도 동일 방식.
  - `SpecialAttackRegistry`(`UnitType → ISpecialAttackBehavior`)는 UseCase가 내부 생성(순수 전략 테이블). 스킬 실행기 레지스트리도 동일 성격으로 배치 가능.

### 2-10. 튜닝 SO / 전략 레지스트리 선례

- `Assets/_Project/Scripts/Infrastructure/Config/SpecialAttackConfig.cs` + `Resources/Config/SpecialAttackConfig.asset` — Infrastructure Config SO 패턴(스킬 데이터 SO의 참고).
- `Assets/_Project/Scripts/Application/Combat/ISpecialAttackBehavior.cs` + `SpecialAttackRegistry.cs` — **전략(Strategy) + 키 레지스트리** 패턴. 스킬 타입 A/B/C 실행기를 `SkillMechanicType → ISkillExecutor`로 매핑하는 데 그대로 원용 가능.

### 2-11. main 병합 신규 시스템 (2026-07-31 — 연구 강화 / ×10 스케일 / 방어력 / DamageCalculator)

origin/main 병합으로 우리 스킬 구현과 직접 겹치는 시스템이 들어왔다. 파일 경로·재사용 지점:

- **소급 배율 레이어 = `Application/UseCases/UnitUpgradeUseCase.cs`(신규).** 스탯 재계산 없이 "쓰는 순간" 배율/증가치를 곱하는 `(B)`방식. 중앙 접근자: `GetEffectiveAttack(team,type)`, `GetMoveSpeedMultiplier(team,type)`, `GetDefense(team,type)`, `GetRegenPerSecond(team)`, `ScaleByGroupAttack(team,group,base)`. **키는 (팀,그룹,스탯) — 팀 전체·영구·기간 없음.** 우리 상태효과(유닛 인스턴스별·기간·중첩)는 저장을 공유하지 않되, **같은 읽기 지점에서 합성**해야 한다.
- **유효 스탯 단일 읽기 지점(이미 배선됨):**
  - 공격력: `Application/UseCases/UnitCombatUseCase.cs` `EffectiveAttack(attacker)`(L254~255) — 직격 L1246·스플래시 L1502.
  - 이동속도: `UnitCombatUseCase.GetUnitMoveSpeedMultiplier(unit)`(L265~268), **`Presentation/Unit/UnitView.cs` 이동이 읽음**(L920·L1363).
  - 방어 감쇄: `UnitCombatUseCase.ComputeFinalDamage(...)`(L1201~1225) → `DamageCalculator.ApplyDefense`.
- **`Domain/Combat/DamageCalculator.cs`(신규, 순수).** `ApplyDefense(raw, defense)`, K=120, 최소 1, **방어 0이면 무감쇄(하위호환)**. 직격/스플래시가 `ComputeFinalDamage` 경유로 공유. → 스킬 타입 A가 통과할 방어 파이프라인.
- **DoT 무감쇄 확인**: `UnitCombatUseCase.ApplyTimedDamageToUnit`(L1800~1822)이 `ComputeFinalDamage`를 우회해 `target.TakeDamage(amount)` 직접 호출 → **DoT는 방어력 미적용**. 타입 B(장판)가 `ApplyDamageOverTime`를 재사용하면 자동으로 무감쇄.
- **방어력 스냅샷 = `Domain/Unit/UnitData.cs` `int Defense { get; }`(신규, readonly).** 생성 시 `UnitStats.GetDefense(type)` 스냅샷(Phase 1 전 유닛 0). 실제 방어는 `_upgrade.GetDefense`로 소급 조회. → **`UnitData` 스탯 readonly 가정 여전히 유효**.
- **그룹 정의 = `Domain/Unit/UpgradeGroup.cs`(신규).** `enum UpgradeGroup`(인간/정령/초월 그룹), `enum UnitUpgradeStat{Attack,Defense,MoveSpeed,Regen}`, `UpgradeGroupHelper.GetGroup(type)`. 스킬은 유닛 그룹과 무관하나, 힐 스케일(`ScaleByGroupAttack`) 참고.
- **서버 권위 RPC 최신 참고 = `Infrastructure/Network/NetworkUpgradeController.cs`(신규).** 래퍼→ServerRpc, 팀 소유권(SenderClientId), targeted 실패/브로드캐스트 완료 ClientRpc, `OnResearchCompleted` 훅(App→Infra 역전), **`GameServicesLocator.Current` 지연 해석**(스폰 레이스 방지). `IGameServices.GetUpgradeUseCase()`(신규, `Application/Interfaces/IGameServices.cs` L95).
- **서버 틱 진입점**: 멀티=`Infrastructure/Network/NetworkCombatController.cs` L312 `_services?.GetUpgradeUseCase()?.TickResearch(elapsed)`, 싱글=`GameBootstrapper.Update`. 우리 쿨다운/상태 틱이 붙을 자리.
- **InputHandler 변경**: `Presentation/Input/InputHandler.cs` `Initialize(...)` 마지막에 `ResearchPanelUI researchPanelUI = null` 추가, `HandleClick`에 `BuildingType.Research → _researchPanelUI.Open` 분기(L253~260). 우리 스킬 건물 라우팅은 이 분기 옆에 추가.
- **배선**: `Bootstrap/GameBootstrapper.Setup.cs` L344~346 `_unitUpgrade = new UnitUpgradeUseCase(); _unitCombat.SetUpgradeUseCase(_unitUpgrade); _towerCombat.SetUpgradeUseCase(_unitUpgrade);`, L508~514 InputHandler에 `_researchPanelUI` 주입. 우리 스킬 배선은 여기 옆에.
- **×10 스케일**: 전투 스탯이 ×10로 커졌고 `DamageCalculator` K=120이 그 스케일 기준. → **스킬 피해·힐 데이터도 ×10 스케일로 저작**(유닛 공격력과 같은 축).

> **델타 결론:** 우리가 Phase 2에 만들려던 "유효 스탯 오버레이"는 이미 존재하는 소급 배율 레이어(`UnitUpgradeUseCase`) + 단일 읽기 접근자와 **같은 패턴**이다. 따라서 상태효과는 새 저장을 두되 **기존 3접근자에 합성**하는 것으로 대부분 해결되어 Phase 2 작업이 준다. 다만 **공격 불가(빙결)·DetectRange 변경**은 대응 훅이 없어 신규가 필요하고, **스킬(건물) 출처 피해**는 UnitData 공격자를 요구하는 기존 경로를 못 써 전용 경로가 필요하다. 결정 필요 항목은 Plan.md §9.

---

## 3. 신규로 만들어야 하는 영역 (요약 — 상세 설계는 Plan.md)

> **확정 결정(사용자 논의 반영):** ① UI = 전용 `BuildingSkillPanelUI` 신설. ② 글로벌 쿨다운 = `SkillActivationUseCase` 딕셔너리. ③ **AI도 스킬 사용** → 발동 진입점을 입력과 분리해 플레이어/AI가 `SkillActivationUseCase.Activate` 공유(AI 실제 판단 로직은 별도 후속 task). ④ 스탯 변경 = 유효 스탯 오버레이. ⑤ 아트 = 플레이스홀더(단 하단 X 취소 버튼은 기존 UI 에셋 재사용). ⑥ 건설 진입점은 이미 가능. 구현은 **2페이즈**(Phase 1: 타입 A·B 지점 피해 / Phase 2: 타입 C 상태변경), 각 페이즈 끝에 사용자 실기 테스트. (상세는 Plan.md 6장.)


| 영역 | 왜 신규인가 | 관련 규칙 |
|------|-------------|-----------|
| 스킬 데이터 SO(`SkillDefinition`) + 종족별 로드아웃 | 스킬 정의(아이콘/조준방식/쿨다운/타입/파라미터)와 건물별 스킬셋 데이터가 전무 | 7, 8 |
| 타입별 실행기(A/B/C) + 레지스트리 | 기존 특수공격 레지스트리는 유닛용(`UnitType`), 스킬용(`SkillMechanicType`)은 없음 | 11~13 |
| **상태변경(버프/디버프/제어/회복) 시스템** | **버프 개념이 게임 내 최초.** 단 main 병합으로 유효 스탯 접근자(`EffectiveAttack`/`GetUnitMoveSpeedMultiplier`/`ComputeFinalDamage`)가 이미 존재 → 버프/디버프/둔화는 이 접근자에 **상태 배율 합성**으로 축소(§2-11). **공격 불가(빙결)·DetectRange 변경**만 대응 훅이 없어 신규 필요. `UnitData` 스탯은 여전히 readonly | 13 |
| 건물 글로벌 쿨다운 상태 + UI 오버레이 | 건물 단위 쿨다운 잠금·틱·해제 + 시계방향(radial) 오버레이+숫자 표시가 전무 | 3, 10 |
| 지점 조준 입력 모드 | 누른 채 드래그·조준점(범위 원)·엣지 스크롤·조준점 맵 clamp·하단 X 취소(겹침 회피)가 전무 | 17~24 |
| 스킬 발동 서버 RPC | 좌표만 전송 + 서버 재검증(생존·쿨다운·유효 타일) 경로가 전무 | 25, 26 |

### 핵심 기술 제약 (구현 시 반드시 고려)

- **`UnitData`의 이동/공격/사거리 스탯은 readonly(get-only).** (근거: `Domain/Unit/UnitData.cs` L52~72 — `MaxHp`/`AttackPower`/`AttackRange`/`DetectRange`/`MoveSpeed`, 그리고 신규 `Defense`(L107)까지 모두 setter 없음. 변경 가능한 것은 `Hp`(private set)·`Facing`·`AttackCooldown(Remaining)`뿐.) → 타입 C의 둔화/빙결/버프는 원본을 직접 못 바꾸므로 유효 스탯 합성이 필요하지만, **main 병합으로 그 합성 지점(`EffectiveAttack`/`GetUnitMoveSpeedMultiplier`/`ComputeFinalDamage`)이 이미 존재**한다(§2-11). 상태 배율을 이 접근자에 접어 넣는 방식이 유력(Plan §9-1). 신규는 공격 게이트·이속0 A* 처리·DetectRange 변경으로 국한.
- **스킬(건물) 출처 피해는 `UnitData attacker`가 없다.** `ComputeFinalDamage`/`ApplyFixedDamageToVictim`가 UnitData 공격자를 요구하므로 스킬 타입 A는 **건물/스킬 출처 전용 피해 경로**(`DamageCalculator.ApplyDefense` 직접 호출)가 필요(Plan §0-2, §9-2). 타입 B DoT는 무감쇄(기존 구조 그대로).
- **×10 스케일 데이터**: `SkillDefinition` 피해·힐 수치는 ×10 스케일로 저작(§2-11).
- **enum 미변경**(규칙 1): `MagicBuilding`을 Spirit/Trans가 공유 → 스킬 로드아웃은 enum이 아닌 **종족 키(RaceId, `building.Team`→`GameRaceContext.BlueRace/RedRace`)로 분기**. 각 종족은 스킬 건물이 정확히 1종이므로 RaceId 키로 로드아웃이 유일하게 결정됨.
- **Domain → Core 참조 금지 / Application → Netcode 직접 참조 금지 / NetworkBehaviour는 Infrastructure만.** 상태효과 값 객체는 Domain, 판정·틱은 Application, RPC는 Infrastructure로 분리.

---

## 4. 영향 범위 — 수정될 기존 파일 목록 (추가 위주, 파괴적 변경 없음)

| 파일 | 예상 변경 성격 |
|------|----------------|
| `Presentation/Input/InputHandler.cs` | 스킬 조준 모드 훅 + 스킬 건물 라우팅 분기(**main 신규 `Research` 분기 옆**). `Initialize`는 main에서 `ResearchPanelUI` 인자가 이미 붙음 → 뒤에 스킬 패널 인자 확장 |
| `Presentation/Camera/CameraController.cs` | 엣지 스크롤 팬 메서드 추가(기존 `ClampPosition` 재사용) |
| `Bootstrap/GameBootstrapper*.cs` | 스킬 UseCase/실행기/데이터 SO/조준 컨트롤러/신규 NetworkSkillController 주입·배선(**`_unitUpgrade`/`_networkUpgradeController`/`_researchPanelUI` 배선 옆**) |
| `Application/Interfaces/IGameServices.cs` | `GetSkillActivationUseCase()` 추가(main `GetUpgradeUseCase()`와 동일 방식) |
| `Application/UseCases/UnitCombatUseCase.cs` | P1: 타입 B 반경 DoT + **건물/스킬 출처 즉발 피해 경로**(`DamageCalculator.ApplyDefense`). P2: 기존 접근자(`EffectiveAttack`/`GetUnitMoveSpeedMultiplier`/`ComputeFinalDamage`)에 상태 배율 합성 + `CanAttack` 게이트 |
| `Application/UseCases/`(전투/틱 진입점) | 서버 틱의 **`TickResearch` 호출 옆**에 쿨다운·상태 틱 추가(`GameBootstrapper.Update` / `NetworkCombatController.TickCombat` L312) |
| `Domain/Unit/UnitData.cs` (조건부) | 원칙 무변경(readonly 유지). 합성을 도메인에 두는 안(§9-1) 채택 시에만 `UnitStatusState` 참조 |

> **변경 없이 재사용만 하는 신규 파일**: `Domain/Combat/DamageCalculator.cs`(스킬 A 데미지), `Application/UseCases/UnitUpgradeUseCase.cs`(유효 스탯 접근자·힐 스케일), `Infrastructure/Network/NetworkUpgradeController.cs`(RPC 패턴 미러 참고).
> **변경 없음**: `BuildingType` enum·`BuildingStatsConfig.asset`(규칙 1, 건설비 200), `BuildingData.cs`(쿨다운=UseCase 딕셔너리 확정), `BuildingActionPanelUI.cs`(전용 패널 신설). 위 표의 변경은 전부 **추가(additive)**이며 제거·비활성화 대상은 **없음**.

---

## 5. 조사 중 발견한 주의점

1. **[main 병합으로 완화]** readonly 스탯이라 "MoveSpeed=0" 대입은 여전히 불가하지만, 이동(`UnitView`→`GetUnitMoveSpeedMultiplier`)·공격력(`EffectiveAttack`)·방어(`ComputeFinalDamage`)의 **읽기 지점이 이미 단일화**돼 있어(§2-11), 상태 배율을 이 접근자에 접어 넣으면 대부분 해결된다. 남은 신규는 **공격 불가 게이트(`CanAttack`)·이속 0 시 A* 처리·DetectRange 변경**뿐. 상태 배율 × 연구 배율 합성 연산은 밸런스 결정(Plan §9-4).
2. **지점 조준의 입력 흐름이 랠리보다 무겁다.** 랠리는 release 한 번이면 끝이지만, 스킬은 press→매 프레임 드래그 추적→엣지 스크롤/조준점 이동→release 분기(발동/취소)라 프레임 단위 상태 머신이 필요. `CameraController.HandlePan`·`InputHandler.Update`가 이미 press/drag/release를 각자 소비하므로 **입력 소유권 충돌**(조준 중 카메라 팬·타일 선택 억제)에 주의.
3. **글로벌 쿨다운 오버레이는 "패널 열려 있는 동안"만 갱신하면 충분**(규칙 10은 버튼 위 표시). 매 프레임 남은 시간을 `SkillActivationUseCase` 딕셔너리에서 읽어 radial fill + 숫자 갱신. 다만 쿨다운 상태 자체는 **서버 권위**(규칙 25)라 클라 오버레이는 서버가 전파한 발동 시각/쿨다운으로 로컬 카운트다운.
4. **하단 X 취소와 하단 엣지 스크롤 겹침**(규칙 21)은 실기 튜닝 대상 — X는 화면 끝보다 안쪽, 엣지 스크롤은 진짜 가장자리 여백만. 여백 폭은 Plan에서 상수로 두되 값은 실기 조정.
5. **어셈블리 분리 없음(Assembly-CSharp 단일)** 덕분에 `internal static` AoE 헬퍼를 스킬 실행기가 바로 재사용 가능(추가 public 노출 불필요) — 회귀 위험 최소.
