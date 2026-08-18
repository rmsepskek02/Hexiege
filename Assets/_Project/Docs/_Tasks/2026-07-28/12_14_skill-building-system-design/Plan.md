# Plan — 스킬 건물 시스템 (구현 관점)

## 이 계획이 무엇인가 (자연어 서두)

이 문서는 스킬 건물 시스템을 **실제 코드로 구현하기 위한 구현 계획서**입니다. 스킬 건물은 플레이어가 버튼을 눌러 전장에 즉각적인 효과(범위 피해, 장판 지속 피해, 버프/디버프/제어/회복)를 일으키는 특수 건물입니다.

이 계획은 "무엇을 새로 만들고 무엇을 기존 코드에서 고쳐, 어떤 순서로, 어떤 아키텍처 제약을 지키며 구현하는가"를 정합니다. 다만 **이번 범위는 스킬 "프레임워크(그릇)"까지**입니다. 즉 스킬을 담을 데이터 구조(ScriptableObject 스키마), 타입별 실행기, 상태변경 시스템, 조준 입력, 서버 RPC의 **틀**을 만들되, **개별 스킬의 구체 목록과 수치(어떤 스킬이 몇 초 쿨다운에 얼마의 피해)는 이번에 정하지 않고 데이터로 별도 확정**합니다.

각 항목 끝의 `→ 규칙 N`은 SSoT 문서 `GameSystemRules_Skills.md`의 근거 규칙입니다(WORKFLOW [4] — 각 항목의 근거를 GameSystemRules에 연결).

---

## ⚠️ 기존 로직 제거 여부 (WORKFLOW [4] — 문서 최상단 필수 명시)

**이번 작업에 "기존 로직 제거"는 없습니다.** 모든 변경은 추가(additive)이거나, 기존 헬퍼에 파라미터/모드를 얹는 확장입니다.

- `BuildingType` enum·`BuildingStatsConfig.asset`은 **변경하지 않습니다**(규칙 1 — 직렬화·RPC 안정성).
- 기존 특수공격/DoT/HoT/랠리/카메라 코드는 **시그니처 유지 + 확장**만 합니다(회귀 방지).
- 만약 구현 중 불가피하게 기존 분기를 대체해야 할 경우, **삭제 대신 주석 처리(비활성화)를 기본**으로 하고, 그 근거와 함께 이 절에 추가 기재한 뒤 [6] 사용자 테스트 통과 후에만 최종 삭제합니다.
- **건설 진입점은 이미 가능**: 스킬 건물(FlightFacility / MagicSpirit / WillowShrine)은 현재도 정상 배치·건설된다. 따라서 **배치/건설 로직 추가 작업은 없으며**, 이 계획은 "건설된 스킬 건물에 UI + 스킬 발동 로직을 얹는" 것에 한정된다.

---

## 0. main 병합 델타 대조 (2026-07-31 — 연구 강화 시스템 + ×10 스케일 + 방어력 + DamageCalculator)

origin/main이 병합되며 **연구소 유닛 강화 시스템 · 전투 스탯 ×10 스케일 · 방어력(Defense) 신규 스탯 · DamageCalculator**가 들어왔다. 이는 우리 스킬 Plan(특히 Phase 2 유효 스탯, 스킬 데미지)과 직접 겹친다. 아래는 병합 전 가정 대비 **어긋난 것 / 재사용 가능해진 것 / 새로 맞출 것**의 대조 결과다(상세 근거는 Research.md §6).

### 0-1. 재사용 가능해진 것 (우리 작업이 줄어든 부분)

- **"유효 스탯" 중앙 접근자가 이미 존재한다.** main은 스탯을 재계산하지 않고 **쓰는 순간 배율/증가치를 곱하는 소급 레이어**(`UnitUpgradeUseCase`, `(B)`방식)를 도입했다. 우리가 Phase 2에 만들려던 유효 스탯 오버레이와 **같은 철학**이다. 실제 단일 읽기 지점이 이미 배선돼 있다:
  - 공격력: `UnitCombatUseCase.EffectiveAttack(attacker)` → `_upgrade.GetEffectiveAttack(team,type)` (직격 L1246·스플래시 L1502에서 사용).
  - 이동속도: `UnitCombatUseCase.GetUnitMoveSpeedMultiplier(unit)` → `_upgrade.GetMoveSpeedMultiplier(...)`, **`UnitView` 이동이 이미 이 훅을 읽는다**(L920·L1363).
  - 방어 감쇄: `UnitCombatUseCase.ComputeFinalDamage(...)` → `DamageCalculator.ApplyDefense(raw, defense)` (직격·스플래시 공용).
  - → **따라서 상태효과(버프/디버프/둔화)는 새 읽기 지점을 전수 교체할 필요 없이, 이 세 접근자에 "상태 배율"을 얹는 것**으로 대부분 해결된다. Phase 2의 최대 위험(readonly 스탯 → 읽기 지점 누락)이 크게 완화됨. `UnitData` 스탯이 readonly라는 우리 가정도 **여전히 유효**(main도 `Defense`를 readonly 스냅샷으로 추가).
- **`DamageCalculator`(Domain 순수) — 스킬 데미지가 통과할 방어 감쇄 공식이 준비됨.** `ApplyDefense(raw, defense)`, K=120, 최소 1, 방어 0이면 무감쇄(하위호환). 타입 A 즉발 피해가 이 파이프라인을 재사용한다.
- **DoT는 방어력 미적용이 이미 구조로 보장됨.** DoT 틱 sink `ApplyTimedDamageToUnit`은 `ComputeFinalDamage`를 거치지 않고 `target.TakeDamage(amount)`를 직접 호출한다(규칙: DoT 무감쇄). → **타입 B(장판)가 기존 `ApplyDamageOverTime`를 재사용하면 자동으로 "무감쇄 DoT"**가 된다.
- **`NetworkUpgradeController`(신규) = `NetworkSkillController`가 미러링할 최신 참고 패턴.** 래퍼→ServerRpc, 팀 소유권(SenderClientId), 대상 지정/브로드캐스트 ClientRpc, `OnResearchCompleted` 훅(App→Infra 의존성 역전), **`GameServicesLocator.Current` 지연 해석**(스폰 레이스 방지)까지 우리가 그대로 따를 골격.
- **연구 서버 틱 진입점 확정** — 우리 스킬 쿨다운/상태 틱이 붙을 정확한 자리: 싱글=`GameBootstrapper.Update`(`!IsNetworkMode`), 멀티=`NetworkCombatController.TickCombat`(서버, `TickResearch(elapsed)` 호출 L312) 바로 옆.
- **UseCase 딕셔너리 상태 보관 = 검증된 관례.** `UnitUpgradeUseCase`가 (팀,그룹,스탯)→레벨·진행중을 딕셔너리로 보관 → 우리 `SkillActivationUseCase`의 글로벌 쿨다운 `Dictionary<int,float>`(3-5)와 동일 성격.

### 0-2. 어긋난 것 (Plan 가정 수정 필요)

- **`InputHandler`가 바뀌었다.** `Initialize(...)` 마지막 인자로 `ResearchPanelUI researchPanelUI = null`이 추가됐고, `HandleClick`에 `buildingAtPos.Type == BuildingType.Research → _researchPanelUI.Open(...)` 분기가 생겼다(랠리 분기와 `CanShowActionPanel` 폴백 사이). → **우리 스킬 건물 라우팅은 이 Research 분기 바로 옆에 추가**하고, `Initialize` 시그니처는 이미 확장돼 있으니 우리 스킬 패널 인자를 뒤에 더 붙인다.
- **`IGameServices`에 `GetUpgradeUseCase()` 추가 + `GameServicesLocator.Current` 신설.** 우리는 `GetSkillActivationUseCase()`를 같은 방식으로 추가하고, `NetworkSkillController`는 `GameServicesLocator.Current`로 서비스를 지연 해석한다(구 `NetworkBuildingController._services` OnNetworkSpawn 캐시보다 이 최신 패턴을 따른다).
- **스킬 데미지는 "UnitData 공격자"가 없다.** `ComputeFinalDamage(attacker, target, raw)`/`ApplyFixedDamageToVictim(attacker,...)`는 **`UnitData attacker`를 요구**한다(Tank/CannonCart 건물 2배 판정·이벤트 attribution). 스킬은 건물이 시전자라 이 경로를 그대로 못 쓴다. → **건물/스킬 출처 전용 피해 경로를 신설**한다(§9-1 확정 9-2: `DamageCalculator.ApplyDefense` 직접 호출, Tank 2배 미적용, 기존 유닛 공격 경로 무변경).

### 0-3. 새로 맞출 것 (통합 작업 — 방식 확정)

- **×10 스케일 데이터 주의.** 전투 스탯이 ×10로 커졌고 `DamageCalculator` K=120이 그 스케일에 맞춰졌다. → **`SkillDefinition`의 피해·힐 수치는 반드시 ×10 스케일로 저작**한다(구 "10 피해" ≈ 신 100). 3-1 스키마에 주의 명시.
- **타입 A 즉발 피해 → `DamageCalculator.ApplyDefense` 통과(방어 감쇄됨). 타입 B DoT → 무감쇄**(§9 확정 9-3). 두 타입의 방어 상호작용이 다른 것은 의도된 설계.
- **상태효과 × 연구 배율 = 곱연산, 합성 위치 = 기존 세 접근자**(§9 확정 9-1·9-4).

> **[P1]/[P2] 표기**: 각 파일이 Phase 1(타입 A·B 지점 피해) 산출물인지, Phase 2(타입 C 전역 상태변경) 산출물인지 표시한다. Phase 정의는 6장 참조.

### Domain (순수 C#, Core/Unity 참조 금지)
- `Scripts/Domain/Skill/SkillMechanicType.cs` **[P1]** — enum `{ InstantAreaDamage(A), AreaDotDamage(B), GlobalStatusChange(C) }`(C 멤버는 선언만, 실행은 P2). → 규칙 11~13
- `Scripts/Domain/Skill/SkillAimType.cs` **[P1]** — enum `{ Instant, PointTarget }`. → 규칙 15, 16
- `Scripts/Domain/Status/StatusEffectKind.cs` **[P2]** — enum `{ MoveSpeedMul, AttackDisabled, AttackPowerMul, ... , HealOverTime }`(제어·버프·디버프·회복 통합 표현). → 규칙 13
- `Scripts/Domain/Status/StatusEffect.cs` **[P2]** — 값 객체(Kind/Magnitude/RemainingDuration/SourceTeam). 부여 시각·잔여시간 관리 단위.
- `Scripts/Domain/Status/UnitStatusState.cs` **[P2]** — 한 유닛의 활성 상태효과 목록 + **유효 스탯 계산**(기본 스탯 + 활성 효과 → EffectiveMoveSpeed/CanAttack/EffectiveAttackPower 등). Domain 순수.

### Application (유스케이스/실행기/인터페이스, Netcode 직접 참조 금지)
- `Scripts/Application/Skill/ISkillExecutor.cs` **[P1]** — `void Execute(SkillActivationContext ctx)` 전략 계약(특수공격 `ISpecialAttackBehavior` 패턴 원용). → 규칙 7
- `Scripts/Application/Skill/InstantAreaDamageExecutor.cs` **[P1]** — 타입 A. → 규칙 11
- `Scripts/Application/Skill/AreaDotDamageExecutor.cs` **[P1]** — 타입 B. → 규칙 12
- `Scripts/Application/Skill/GlobalStatusChangeExecutor.cs` **[P2]** — 타입 C. → 규칙 13
- `Scripts/Application/Skill/SkillExecutorRegistry.cs` **[P1]** — `SkillMechanicType → ISkillExecutor`(UseCase 내부 생성, `SpecialAttackRegistry`와 동일 성격). P1엔 A·B만 등록, C는 P2에서 1줄 추가. → 규칙 7
- `Scripts/Application/Skill/SkillActivationContext.cs` **[P1]** — 실행기에 넘길 컨텍스트(시전 건물·팀·조준 좌표·유닛/건물 목록·좌표 조회·재사용 피해/DoT/힐/상태부여 델리게이트·스킬 파라미터). `SpecialAttackContext`와 유사. 상태부여 델리게이트는 P2에서 연결.
- `Scripts/Application/UseCases/SkillActivationUseCase.cs` **[P1]** — **발동 오케스트레이션의 단일 진입점**: 재검증(건물 생존·글로벌 쿨다운·유효 타일) → 실행기 호출 → 글로벌 쿨다운 설정 → 결과 이벤트. **글로벌 쿨다운 상태를 이 UseCase가 `Dictionary<int,float>`로 보관**(3-5 확정). **플레이어 터치 입력과 AI가 공유하는 발동 API**(아래 3-8 AI 발동 지원). → 규칙 3, 25, 26
- `Scripts/Application/Services/StatusEffectSystem.cs` **[P2]** — 상태효과 부여/해제/지속시간 틱(서버 권위). `_activeTimedEffects`(HoT/DoT)와 병렬 소유·틱 패턴. → 규칙 13
- `Scripts/Application/Interfaces/ISkillDataProvider.cs` **[P1]** — 종족 키(RaceId) + 슬롯 → 스킬 정의 조회 인터페이스(의존성 역전: 구현은 Infrastructure). → 규칙 1, 6, 7
- `Scripts/Application/Interfaces/INetworkSkillController.cs` **[P1]** — 멀티에서 발동 요청 래퍼 인터페이스(Presentation→Application, 구현은 Infrastructure). NGO 직접 의존 회피.

### Infrastructure (Config SO / NetworkBehaviour)
- `Scripts/Infrastructure/Config/SkillDefinition.cs` **[P1]** — `ScriptableObject`. 아이콘/조준방식/쿨다운/타입/파라미터(아래 5장 스키마). C용 필드(StatusKind/Magnitude/TargetsAllies)는 스키마에 함께 두되 사용은 P2. → 규칙 7
- `Scripts/Infrastructure/Config/SkillLoadoutConfig.cs` **[P1]** — `ScriptableObject`. **RaceId → SkillDefinition[]**(최대 5). `ISkillDataProvider` 구현 제공. → 규칙 1, 4, 6
- `Scripts/Infrastructure/Network/NetworkSkillController.cs` **[P1]** — `NetworkBehaviour`. `RequestActivateSkill(...)` 래퍼 → `...ServerRpc` → 서버 재검증·실행 → `...ClientRpc`(VFX/오버레이). → 규칙 25, 26

### Presentation (MonoBehaviour/UI)
- `Scripts/Presentation/UI/BuildingSkillPanelUI.cs` **[P1]** — 스킬 건물 전용 패널(`: BuildingPanelBase`). 슬롯 1~5 동적 채움(종족 로드아웃), 슬롯 6 철거(고정), 쿨다운 오버레이 바인딩. → 규칙 8, 9
- `Scripts/Presentation/Input/SkillAimController.cs` **[P1]** — 지점 조준 모드(press→드래그 추적→조준점 이동→엣지 스크롤→release 발동/취소) 상태 머신. → 규칙 17~24
- `Scripts/Presentation/Effects/SkillAimReticle.cs` **[P1]** — 조준점(범위 원) 월드 표시(반경 시각화). 아트는 플레이스홀더. → 규칙 17
- `Scripts/Presentation/UI/SkillCooldownOverlay.cs` **[P1]** — 슬롯 위 radial(clockwise) fill + 남은초 텍스트. 아트는 플레이스홀더. → 규칙 10

> **패널 방식 확정(안 B)**: 스킬 UI는 **전용 `BuildingSkillPanelUI : BuildingPanelBase` 신설**로 확정한다(기존 `BuildingActionPanelUI` 확장안은 폐기). 스킬 특유의 동적 슬롯·쿨다운 오버레이·조준 연동이 많아 전용 패널이 응집도·완성도(절대규칙 7)에서 유리하다. `BuildingPanelBase`의 Show/Close/철거/헤더/`ClosedFrame`/`IGameUI`는 그대로 재사용한다.
>
> **하단 X(취소) 버튼 = 기존 UI 에셋 재사용**: 취소용 X 버튼은 **신규 제작하지 않고 기존 UI 에셋을 재사용**한다(규칙 20). 별도 `SkillCancelButton.cs`를 신설하기보다, HUD에 기존 X 에셋을 배치하고 `SkillAimController`가 release 시 그 영역 히트를 취소로 판정한다(규칙 21 겹침 회피는 배치·여백 튜닝으로 처리).

---

## 2. 수정 파일 목록 (전부 추가적 확장)

| 파일 | Phase | 변경 내용 | 근거 규칙 |
|------|-------|-----------|-----------|
| `Presentation/Input/InputHandler.cs` | P1 | `HandleClick`의 **신규 Research 라우팅 분기 바로 옆에** 스킬 건물(FlightFacility/MagicBuilding) → `BuildingSkillPanelUI.Open` 분기 추가. 최상단 랠리 분기 옆에 스킬 조준 모드 가드 추가(또는 `SkillAimController`에 위임). **`Initialize(...)` 시그니처는 main에서 `ResearchPanelUI` 인자가 이미 추가됨 → 그 뒤에 스킬 패널 인자 확장** | 8, 16, 17 |
| `Presentation/Camera/CameraController.cs` | P1 | `EdgeScroll(screenPos, dt)` 신규 메서드 추가(이동 후 기존 `ClampPosition()` 재사용) | 18, 23 |
| `Application/UseCases/UnitCombatUseCase.cs` | P1(B)·P2(C) | P1: 타입 B 반경 DoT 진입점(반경 수집 + `ApplyDamageOverTime`) + **건물/스킬 출처 즉발 피해 경로**(타입 A, `DamageCalculator.ApplyDefense` 직접 호출, §0-2). P2: 타입 C 회복(HoT) + 기존 접근자(`EffectiveAttack`/`GetUnitMoveSpeedMultiplier`/`ComputeFinalDamage`)에 상태 배율 합성 + `CanAttack` 게이트 신설. `SkillActivationContext`에 델리게이트로 노출 | 11, 12, 13 |
| `Application/Interfaces/IGameServices.cs` | P1 | `GetSkillActivationUseCase()` 추가(기존 `GetUpgradeUseCase()`와 동일 방식) | 아키텍처 |
| `Application/UseCases`(전투 틱 진입점) | P1(쿨다운)·P2(상태) | 서버 틱의 **`TickResearch(elapsed)` 호출 바로 옆에** 글로벌 쿨다운 틱(P1) + `StatusEffectSystem.Tick(dt)`(P2) 추가(싱글=`GameBootstrapper.Update`, 멀티=`NetworkCombatController.TickCombat` L312, 이중 틱 금지) | 3, 13, 25 |
| `Domain/Unit/UnitData.cs` | P2 | **변경 없음 확정**(스탯 readonly 유지) — 9-1=A로 합성은 `UnitCombatUseCase` 접근자에서, `CanAttack` 게이트는 Application/UnitView에 둠(도메인 무변경) | 13 |
| `Bootstrap/GameBootstrapper*.cs` | P1·P2 | P1: 스킬 UseCase·실행기(A·B)·`SkillLoadoutConfig`·`SkillAimController`·`BuildingSkillPanelUI`·`NetworkSkillController` 생성·주입·배선(연구 시스템 배선 `_unitUpgrade`/`_networkUpgradeController` 옆). P2: `StatusEffectSystem`·타입 C 실행기 추가 배선 | 아키텍처 |

> **`Domain/Building/BuildingData.cs`는 변경하지 않는다** — 글로벌 쿨다운 상태는 `SkillActivationUseCase`가 `Dictionary<int,float>`로 보관하기로 확정(3-5). 도메인 확장 없음.
> **`Presentation/UI/BuildingActionPanelUI.cs`는 변경하지 않는다** — 스킬 UI는 전용 `BuildingSkillPanelUI` 신설(안 B 확정). 기존 액션 패널은 비스킬 건물 전용으로 그대로 둔다. (스킬 건물 클릭 라우팅 분기만 `InputHandler`에서 추가.)
> **`Domain/Combat/DamageCalculator.cs`·`Application/UseCases/UnitUpgradeUseCase.cs`는 변경하지 않고 재사용만 한다**(스킬 데미지·상태 배율이 이들을 호출/합성).

---

## 3. 클래스·데이터 설계

### 3-1. 스킬 정의 SO (`SkillDefinition`, Infrastructure Config) → 규칙 7

`SpecialAttackConfig`/`BuildingStatsConfig`와 동일한 Infrastructure Config SO 패턴. 데이터 스키마(필드)만 정의하고 개별 스킬 값은 이번 범위 밖.

| 필드 | 타입 | 설명 | 규칙 |
|------|------|------|------|
| `Icon` | `Sprite` | 스킬 버튼 아이콘 | 7, 9 |
| `AimType` | `SkillAimType` | Instant / PointTarget | 15, 16 |
| `Cooldown` | `float`(초) | 발동 후 **건물 글로벌** 쿨다운 | 3, 7 |
| `Mechanic` | `SkillMechanicType` | A/B/C | 11~13 |
| `Radius` | `float` | (A/B) 원형 반경 | 11, 12 |
| `Duration` | `float` | (B) DoT 지속 / (C) 상태 지속 | 12, 13 |
| `DamagePerSecond` / `TotalDamage` | `float`/`int` | (A) 즉발 피해 / (B) 초당 피해 | 11, 12 |
| `StatusKind` | `StatusEffectKind` | (C) 상태 종류 | 13 |
| `Magnitude` | `float` | (C) 강도(이속 배율·힐량 등) | 13 |
| `TargetsAllies` | `bool` | (C) 아군(긍정)/적(부정) 대상 | 13 |

> **코드는 타입별 실행기만 구현하고 수치는 SO 주입.** 새 스킬 추가 시 원칙적으로 SO만 추가(규칙 7). 개별 스킬 목록·수치는 데이터로 별도 확정(범위 밖).
>
> **⚠️ ×10 스케일 주의(main 병합):** 전투 스탯이 ×10로 스케일됐고 `DamageCalculator`(K=120)가 그 스케일에 맞춰졌다. `DamagePerSecond`/`TotalDamage`/힐 수치는 **반드시 ×10 스케일로 저작**한다(구 감각 "10 피해" ≈ 신 100). 데이터 저작 시 유닛 공격력(`UnitStatsConfig`, ×10 반영값)과 같은 축으로 맞출 것.

### 3-2. 종족 키 분기 로드아웃 (`SkillLoadoutConfig`) → 규칙 1, 6

- **enum 미변경.** `MagicBuilding`을 Spirit/Trans가 공유하므로, 로드아웃은 enum이 아니라 **RaceId 키**로 분기: `SkillLoadoutConfig`가 `Dictionary<RaceId, SkillDefinition[]>`(최대 5, 규칙 4)를 보유. 각 종족은 스킬 건물이 정확히 1종이라 RaceId로 유일 결정.
- 조회: 건물 클릭 시 `building.Team` → `GameRaceContext.BlueRace/RedRace` → `ISkillDataProvider.GetLoadout(race)`. `BuildingActionPanelUI.Show`가 이미 이 race 변환을 사용(재사용).
- FlightFacility(Human)도 동일 경로(별도 enum이지만 RaceId 키로 통일).

### 3-3. 타입 A/B/C 실행기 + 재사용 지점 연결

- **타입 A — 즉발 범위 피해** (`InstantAreaDamageExecutor`) → 규칙 11
  - 조준 좌표를 중심으로 `BlastAttackBehavior.CollectEnemyUnitsInRadius`(유닛) + `QuakeAttackBehavior.CollectEnemyBuildingsInRadius`(건물) 재사용(2단계 선수집→적용).
  - **피해는 `DamageCalculator.ApplyDefense(raw, defense)`(방어 감쇄)를 통과**한다(확정 9-3): `raw`=SkillDefinition 피해값(×10 스케일), `defense`=피격 대상 팀 방어(`UnitUpgradeUseCase.GetDefense`, 건물은 0). → 유닛/타워 데미지와 동일한 감쇄 공식.
  - **출처가 건물(시전 건물)이라 `UnitData attacker`가 없다** → **건물/스킬 출처 전용 즉발 피해 경로를 UseCase에 신설**한다(확정 9-2). 이 경로가 `DamageCalculator.ApplyDefense`를 직접 호출하고 `OnEntityDamaged`를 발행(Tank 2배 미적용, 기존 유닛 공격 경로 무변경). 아군 제외.
- **타입 B — 범위 지속 피해(장판)** (`AreaDotDamageExecutor`) → 규칙 12
  - 반경 수집(위와 동일) → 각 적 유닛에 `UnitCombatUseCase.ApplyDamageOverTime`(discrete 초 단위 틱) 부여. 신규 진입점(스킬 전용 튜닝값)으로 `ApplyBlastDot`/`ApplyInfernoDot`처럼 값 분리.
  - 틱은 기존 `TickTimedEffects(dt)`가 그대로 소비(추가 틱 루프 불필요).
  - **DoT는 방어력 미적용(무감쇄)**(확정 9-3): DoT 틱 sink `ApplyTimedDamageToUnit`이 `ComputeFinalDamage`를 우회해 `TakeDamage`를 직접 호출하므로, 타입 B는 자동으로 무감쇄가 된다(규칙과 일치). 타입 A(감쇄)와 방어 상호작용이 다른 것은 의도된 설계.
- **타입 C — 전역 상태변경** (`GlobalStatusChangeExecutor`) → 규칙 13, 15
  - 조준 없음(전역 즉시). `TargetsAllies`에 따라 아군/적 유닛 전체 순회 → `StatusEffectSystem.Apply(unit, StatusEffect)`.
  - 회복은 `StatusEffectKind.HealOverTime`으로 표현하고 내부적으로 기존 `ApplyTimedEffect(Heal)`(HoT) 재사용 → 회복은 우선 전역 즉시(규칙 13, 추후 지점형 전환 여지).

### 3-4. 상태변경 시스템 (버프 개념 최초 도입) [Phase 2] → 규칙 13

**버프 개념 최초 도입이지만, main 병합으로 "유효 스탯 접근자"가 이미 존재**하여 초기 계획보다 작업이 준다(§0-1). `UnitData` 스탯은 여전히 readonly이며(직접 mutable 변경안은 폐기 — 중첩·만료 원복 문제), **기존 연구 배율 레이어(`UnitUpgradeUseCase`)와 동일한 읽기 지점(`UnitCombatUseCase` 접근자)에 상태 배율을 곱연산으로 합성**한다(확정 9-1·9-4 — 별도 오버레이 미도입):

- **기존 단일 읽기 지점(재사용)**:
  - 공격력 → `UnitCombatUseCase.EffectiveAttack(attacker)` (이미 연구 배율 반영).
  - 이동속도 → `UnitCombatUseCase.GetUnitMoveSpeedMultiplier(unit)` (`UnitView` 이동이 읽음 L920·L1363).
  - 방어/피해 → `ComputeFinalDamage` → `DamageCalculator.ApplyDefense`.
  - → **버프(공격↑)·디버프(공격↓)·둔화(이속↓)·방어 변화는 위 세 접근자에 상태 배율을 곱/합**하는 것으로 처리. 새 읽기 지점 전수 교체 불필요.
- **여전히 신규가 필요한 것**(연구 레이어에 대응 훅이 없는 상태):
  - **공격 불가(빙결/기절)** — 공격 게이팅 훅이 없음. `UnitCombatUseCase`/`UnitView` 전투 진입에 `CanAttack(unit)` 게이트 신설.
  - **이동 완전 정지(빙결=이속 0)** — `GetUnitMoveSpeedMultiplier`가 0을 반환하도록 상태 반영(확정 9-5: 배율 0 우선. 구현 중 A* 이동 코루틴이 0 배율을 안전 처리하는지 검증 → 문제 시에만 별도 게이트 추가).
  - **감지 사거리 변화(DetectRange)** — 대응 훅 없음(변경 스킬을 넣을 경우에만 신규).
- **Domain**: `UnitStatusState`(유닛별 활성 `StatusEffect` 목록) — 순수 계산(상태 배율 산출). Core/Unity 미참조.
- **Application**: `StatusEffectSystem`이 부여/해제/지속시간 감소를 서버 권위로 관리(`_activeTimedEffects`·`UnitUpgradeUseCase._active`와 동일 딕셔너리 소유·틱 패턴). 회복은 기존 HoT(`ApplyTimedEffect(Heal)`) 재사용.
- **합성(확정 9-1·9-4)**: 상태 배율은 `UnitCombatUseCase`의 기존 접근자(`EffectiveAttack`/`GetUnitMoveSpeedMultiplier`/`ComputeFinalDamage`) 안에서 `_upgrade` 배율과 **곱연산**으로 합성한다(예: 공격력 = 기본 × 연구배율 × 상태배율). `StatusEffectSystem`은 대상별 상태 배율을 이 접근자에 제공한다.
- **멀티 동기화**: 상태 부여/해제를 ClientRpc로 전파(연출·유효 스탯 재현). 값 권위는 서버.

### 3-5. 건물 글로벌 쿨다운 상태 + UI 오버레이 [Phase 1] → 규칙 3, 10

- **상태 보관 위치 = `SkillActivationUseCase` 딕셔너리(확정)**: 스킬 발동/판정이 서버 권위 UseCase에 있으므로, **`SkillActivationUseCase`가 `Dictionary<int(buildingId), float remaining>`로 보유**하고 서버 틱에서 감소시킨다. 도메인 `BuildingData`에 스킬 전용 필드를 늘리지 않아 응집도 유지(`BuildingData` 변경 없음). (대안이던 BuildingData 필드 방식은 폐기.)
- **글로벌**: 한 건물의 어느 스킬을 써도 그 건물 슬롯 1~5 전부 잠금(규칙 3).
- **UI 오버레이**: `SkillCooldownOverlay` — `Image.fillMethod = Radial360`, `fillClockwise = true`, 발동 시각 기준 로컬 카운트다운으로 `fillAmount` 감소 + 남은초 TMP 텍스트(규칙 10). 쿨다운 값·발동 시각은 서버가 ClientRpc로 전파(권위=서버, 규칙 25). 오버레이는 패널이 열려 있는 동안만 갱신.

### 3-6. 지점 조준 입력 모드 (`SkillAimController`) → 규칙 17~24

랠리 모드(`ProductionPanelUI.IsSettingRallyPoint` + `InputHandler.HandleClick` 최상단 분기) 패턴을 기반으로 확장하되, 스킬은 press/drag/release 상태 머신이라 전용 컨트롤러로 분리.

- **진입**: 스킬 버튼 `EventTrigger.PointerDown`(생산 유닛 버튼 `SetupUnitButtonBySlot`와 동일 패턴)에서 조준 모드 on + 슬롯의 스킬 반경으로 `SkillAimReticle` 표시. → 규칙 17
- **드래그 추적**: 매 프레임 `Mouse.current`/`primaryTouch` 위치 → `InputHandler.ScreenToXZPlane` 재사용 → 조준점 이동. → 규칙 17
- **엣지 스크롤**: 조준점이 화면 가장자리 여백 안이면 `CameraController.EdgeScroll(screenPos, dt)` 호출(신규), 이동 후 기존 `ClampPosition()`이 **맵 경계 정지**를 자동 처리. → 규칙 18, 23
- **조준점 맵 clamp**: 조준점 월드 좌표를 맵 타일 범위로 clamp(최외곽 타일 한계). → 규칙 22, 24
- **release 분기**: 손 뗀 지점이 하단 X 위면 취소, 아니면 발동(좌표 1개 확정). → 규칙 19, 20
- **X 겹침 회피**: X 버튼은 화면 끝보다 안쪽, 엣지 스크롤은 진짜 가장자리 여백만(상수 분리, 값은 실기 튜닝). → 규칙 21
- **입력 소유권**: 조준 모드 중 `CameraController` 드래그 팬·`InputHandler` 타일 선택 억제(모드 플래그 가드) — 랠리 모드가 `IsPointerOverUI`보다 먼저 처리되는 것과 동일 우선순위.

### 3-7. 서버 권위 발동 RPC (`NetworkSkillController`, Infrastructure) → 규칙 25, 26

**최신 참고 = `NetworkUpgradeController`(main 병합).** `NetworkBuildingController`보다 이쪽의 최신 관례를 미러링한다.

- **요청(클라→서버)**: `RequestActivateSkill(int buildingId, int skillSlot, int q, int r)` 래퍼 → `RequestActivateSkillServerRpc(...)`(`[ServerRpc(RequireOwnership=false)]`). 즉시형(타입 C)은 좌표 없이 `slot`만.
- **서비스 해석 = `GameServicesLocator.Current` 지연 해석**(`NetworkUpgradeController.ResolveServices()` 미러 — 씬 오브젝트 스폰 레이스로 `_services`가 null로 굳는 버그 방지). `IGameServices`에 **`GetSkillActivationUseCase()` 신규 추가**(기존 `GetUpgradeUseCase()`와 동일 방식).
- **서버 재검증**(클라 입력 불신뢰, 규칙 26): ① 서비스/UseCase null → ② 발신자 팀 소유권(`SenderClientId`→Host=Blue/Client=Red) + 건물 소유 → ③ **건물 생존** → ④ **글로벌 쿨다운 만료** → ⑤ (지점형) 전송 좌표가 **유효 맵 타일**인지 재확인(규칙 22 clamp를 서버에서 재적용).
- **실행**: 재검증 통과 시 `SkillActivationUseCase.Activate(...)`(플레이어/AI 공유 진입점 §3-8) → 실행기(A/B/C) → 판정·피해(DamageCalculator)·상태변경 **서버 실행**(규칙 25) → 글로벌 쿨다운 설정.
- **전파(서버→클라)**: 실패는 요청 클라에게만 targeted ClientRpc, 발동 성공(VFX·쿨다운 오버레이 시작·상태효과 재현)은 **양 클라 브로드캐스트**(상대 스킬 효과가 내 화면에도 재현돼야 함 — `ResearchLevelClientRpc` 브로드캐스트와 동일 사유). 조준점 이동/범위 미리보기/엣지 스크롤은 **로컬 표현일 뿐**(규칙 25).
- **AI 발동**: 서버 컨텍스트의 AI는 RPC 왕복 없이 `SkillActivationUseCase.Activate`를 직접 호출(§3-8).
- **메서드명**: `...ServerRpc`/`...ClientRpc` 접미사 필수(아키텍처 제약).

### 3-8. AI 발동 지원 — 발동 진입점을 입력과 분리 (확정 요구사항) [Phase 1] → 규칙 25

**AI도 스킬을 사용한다**(시나리오 변경 예정). 따라서 스킬 발동 로직이 플레이어 터치 입력에 종속되면 안 되며, **플레이어와 AI가 동일한 발동 요청 API를 공유**하도록 설계한다.

- **단일 발동 진입점 = `SkillActivationUseCase.Activate(buildingId, skillSlot, HexCoord? aimCoord)`.** 재검증·실행·쿨다운·상태변경이 모두 여기서 일어난다. "누가 요청했는지"(플레이어/AI)와 무관하게 동일 경로.
- **플레이어 경로**: `SkillAimController`(조준·좌표 확정) → (멀티) `NetworkSkillController.RequestActivateSkill` → 서버 → `SkillActivationUseCase.Activate` / (싱글) 직접 `Activate`.
- **AI 경로**: AI 판단 로직(예: `AIOpponentController`)이 대상 건물·슬롯·조준 좌표를 골라 **같은 `SkillActivationUseCase.Activate`를 서버에서 직접 호출**. AI는 이미 서버 권위 컨텍스트에서 도므로 RPC 왕복 없이 UseCase를 직접 호출하면 된다(규칙 25 — 서버 실행).
- **입력/조준 계층(`SkillAimController`)은 "좌표를 만드는 어댑터"일 뿐**, 발동의 본체가 아니다. AI는 이 어댑터를 거치지 않고 좌표를 직접 계산해 넣는다.
- **범위 주의**: 실제 AI의 스킬 판단 로직(언제·어떤 스킬을 쓸지)과 AI 시나리오 문서 변경은 **별도 후속 task**다. 이번엔 **시스템이 AI 발동을 지원하도록 진입점을 분리·개방**하는 것까지가 요구사항이다.

### 3-9. 자산(아트) 정책 — 플레이스홀더 (확정)

- 스킬 **아이콘·VFX·조준 범위 원(`SkillAimReticle`)·쿨다운 오버레이 아트**는 **플레이스홀더로 구현**한다(단색 스프라이트/기본 도형 등). 실제 아트는 개별 스킬 기획 확정 후 제작 예정.
- **예외 — 하단 X(취소) 버튼은 기존 UI 에셋 재사용**: 신규 제작하지 않는다(규칙 20). HUD에 기존 X 에셋을 배치하고 `SkillAimController`가 release 시 그 히트 영역을 취소로 판정(규칙 21 겹침 회피는 배치·여백 튜닝).
- 플레이스홀더라도 규칙 10(radial clockwise + 남은초 숫자)·규칙 17(범위 원)의 **동작 요건은 충족**해야 한다(아트만 임시, 로직은 정식).

---

## 4. 아키텍처 제약 준수 방법 (MEMORY 제약별)

| 제약 | 이 계획에서 지키는 방법 |
|------|------------------------|
| Domain → Core 참조 금지 | `SkillMechanicType`/`StatusEffect`/`UnitStatusState`는 Domain 순수 C#. Core·UnityEngine 미참조(좌표·orientation 필요 시 기존 정적 홀더 사용) |
| Application → Netcode 직접 참조 금지 | 발동 요청은 `INetworkSkillController` 인터페이스(Application 선언) 경유. 멀티 분기는 `NetworkContext` 정적 홀더로 판단(`BuildingPanelBase`와 동일) |
| NetworkBehaviour는 Infrastructure만 | `NetworkSkillController`만 NetworkBehaviour, RPC 메서드명 `...ServerRpc`/`...ClientRpc` |
| Application → Infrastructure 역참조 금지 | 스킬 데이터는 `ISkillDataProvider`(Application 인터페이스) ← `SkillLoadoutConfig`(Infrastructure 구현). SO 튜닝값은 GameBootstrapper가 float/데이터로 UseCase에 **주입**(`SpecialAttackConfig` 선례) |
| GameBootstrapper 유일 조합 루트 | 스킬 UseCase·실행기·SO·상태시스템·조준 컨트롤러·NetworkSkillController 생성/주입/배선을 전부 GameBootstrapper에서 수행 |
| Inspector(SO) 값 우선 | 스킬 수치·쿨다운·반경은 `SkillDefinition` SO(Inspector), 코드 폴백 최소 |
| enum 미변경 | `BuildingType` 불변, RaceId 종족 키로 로드아웃 분기(규칙 1) |

---

## 5. 데이터 스키마 정의 범위 (개별 스킬 제외)

- 이번 Plan은 **`SkillDefinition` SO 스키마(3-1) + `SkillLoadoutConfig` 구조(3-2)까지만** 정의합니다.
- **개별 스킬(각 건물 슬롯 1~5의 스킬 목록)과 수치(쿨다운·반경·지속·피해·상태 강도)는 이번 범위 밖** — 데이터로 별도 확정(규칙 "추후 데이터로 확정할 항목").
- `Testcase.md`는 이번에 작성하지 않습니다(미구현·사용자 미지시, WORKFLOW [5-1]).

---

## 6. 구현 단계 (2페이즈 확정 — 각 페이즈 끝에 사용자 실기 테스트)

전체를 **2페이즈로 나눠 각 페이즈 끝에 사용자 실기 테스트**를 둔다. Phase 1은 스탯을 전혀 건드리지 않는 **순수 추가분**만이라 기존 유닛/전투에 회귀 위험이 없고, Phase 2는 스탯 읽기 지점을 교체하므로 회귀 검증을 별도로 붙인다.

> `Testcase.md`는 이번에 작성하지 않는다(사용자 미지시, WORKFLOW [5-1]). 각 페이즈 끝의 "사용자 실기 테스트"는 사용자가 직접 확인하는 [6] 단계를 뜻한다.

### Phase 1 — 타입 A·B(지점 피해) + 프레임워크 골격 (스탯 무변경, 순수 추가분)

산출물(전부 [P1]): 스킬 데이터 SO/로드아웃, 타입 A·B 실행기, 전용 스킬 패널 UI, 지점 조준 입력(드래그+엣지스크롤+clamp+X취소), 건물 글로벌 쿨다운+오버레이, 서버 발동 RPC, AI 발동 지원 진입점.

1. **Domain(P1)**: `SkillMechanicType`(C 멤버는 선언만)·`SkillAimType`.
2. **스킬 데이터 SO·로드아웃(P1)**: `SkillDefinition`(스키마 전체, C 필드 포함)·`SkillLoadoutConfig`·`ISkillDataProvider` + 플레이스홀더 에셋.
3. **발동 코어(P1)**: `SkillActivationContext` + `SkillActivationUseCase`(재검증·쿨다운 딕셔너리·**AI/플레이어 공유 `Activate` 진입점**) + `SkillExecutorRegistry`(A·B 등록).
4. **실행기 A·B(P1)**: `InstantAreaDamageExecutor`·`AreaDotDamageExecutor` — 기존 반경 수집(`CollectEnemyUnitsInRadius`/`CollectEnemyBuildingsInRadius`) 재사용. A = **건물/스킬 출처 즉발 피해 경로 신설**(`DamageCalculator.ApplyDefense` 통과, ×10 데이터). B = `ApplyDamageOverTime`(무감쇄 DoT) 진입점을 `UnitCombatUseCase`에 노출. `IGameServices.GetSkillActivationUseCase()` 추가.
5. **글로벌 쿨다운 틱(P1)**: 서버 틱 진입점에 쿨다운 감소 추가(이중 틱 금지).
6. **UI(P1)**: `BuildingSkillPanelUI`(전용, `BuildingPanelBase` 재사용) 슬롯 1~5 동적 채움 + `SkillCooldownOverlay`(radial clockwise + 숫자, 플레이스홀더 아트).
7. **지점 조준 입력(P1)**: `SkillAimController`·`SkillAimReticle`(플레이스홀더) + `CameraController.EdgeScroll` + 하단 X(기존 UI 에셋 재사용) + 조준점 맵 clamp.
8. **멀티(P1)**: `NetworkSkillController`(ServerRpc/ClientRpc) + 서버 재검증. 싱글 검증 후 멀티 배선.
9. **GameBootstrapper 배선(P1)** — 각 단계와 함께 점진 주입.

→ **[6] Phase 1 사용자 실기 테스트**(타입 A·B 발동·조준·쿨다운·멀티 동기화 확인).

### Phase 2 — 타입 C(전역 상태변경) + 상태 배율 합성

산출물(전부 [P2]): Domain 상태값 객체, `StatusEffectSystem`, 타입 C 실행기, **기존 유효 스탯 접근자에 상태 배율 합성** + 공격 게이트(`CanAttack`) 신설. (main 병합으로 이동/공격/방어 읽기 지점이 이미 단일화돼 있어 "전수 교체"가 아니라 "기존 접근자 합성"으로 축소 — §0-1.)

1. **Domain(P2)**: `StatusEffectKind`·`StatusEffect`·`UnitStatusState`.
2. **상태 시스템 골격(P2)**: `StatusEffectSystem`(부여/해제/틱). 아직 읽기 지점 미연결(부여해도 무효과), 컴파일 안전.
3. **상태 배율 합성(P2, 확정 9-1·9-4)**: 기존 단일 접근자에 상태 배율을 **곱연산**으로 접어 넣는다 — `EffectiveAttack`(공격 버프/디버프), `GetUnitMoveSpeedMultiplier`(둔화/빙결=배율 0, 확정 9-5), 방어는 `ComputeFinalDamage`/`DamageCalculator` 경로. **신규 게이트**: `CanAttack(unit)`(빙결/기절 시 공격 불가), 필요 시 `DetectRange` 변경. **상태효과가 없으면 기존과 완전히 동일**(무변경 보장) — 회귀 검증 대상.
4. **타입 C 실행기(P2)**: `GlobalStatusChangeExecutor` + 레지스트리에 C 등록. 회복은 기존 HoT(`ApplyTimedEffect(Heal)`) 재사용. `SkillActivationContext` 상태부여 델리게이트 연결.
5. **상태 틱(P2)**: 서버 틱의 `TickResearch`/`TickTimedEffects` 옆에 `StatusEffectSystem.Tick(dt)` 추가(이중 틱 금지). 멀티 상태 동기화(부여/해제 ClientRpc).
6. **GameBootstrapper 배선(P2)** — 상태 시스템·타입 C 실행기 추가 주입.

→ **[6] Phase 2 사용자 실기 테스트**(버프·디버프·둔화·빙결·회복 확인 + **기존 유닛/연구 강화 무변경 회귀 검증** — 상태 배율이 연구 배율과 올바르게 합성되는지 포함).

> 각 단계 종료 시 컴파일 가능 상태 유지. Phase 1 완료 후 사용자 테스트 통과를 확인하고 Phase 2에 착수한다.

---

## 7. 위험 요소 + 완화책

| 위험 | 영향 | 완화책 |
|------|------|--------|
| 상태 배율을 기존 접근자에 접을 때 일부 지점 누락 | 둔화/빙결/버프가 일부만 적용 | main이 이미 단일화한 3접근자(`EffectiveAttack`/`GetUnitMoveSpeedMultiplier`/`ComputeFinalDamage`)에만 합성 → 누락 위험 축소. 신규 게이트(`CanAttack`)·이속 0 A* 처리만 별도 검증 |
| 상태 배율 × 연구 배율 합성 오류 | 강화 유닛에 스킬 걸면 수치 붕괴 | 곱연산으로 확정(9-4). 연구만/상태만/둘 다 3케이스 회귀 확인 |
| 스킬 데미지가 방어 파이프라인을 안 타면 방어력 무시 | 밸런스 붕괴 | 타입 A는 `DamageCalculator.ApplyDefense` 통과, 타입 B는 의도적 무감쇄(확정 9-3) |
| ×10 스케일 미인지 데이터 저작 | 스킬 피해가 1/10로 무의미 | `SkillDefinition` 수치는 ×10 스케일 저작(3-1 주의), 유닛 공격력과 같은 축 |
| 건물 출처 피해에 UnitData attacker 강제 재사용 | 컴파일/NRE 또는 잘못된 2배 | 건물/스킬 출처 전용 피해 경로 신설(확정 9-2), Tank 2배 미적용 |
| 빙결 배율 0을 A* 이동 코루틴이 오처리 | 유닛이 얼지 않거나 예외 | 확정 9-5 — 구현 중 0 배율 안전 처리 검증, 문제 시에만 별도 정지 게이트 |
| 조준 입력과 카메라 팬·타일 선택 입력 소유권 충돌 | 조준 중 오작동(팬/선택) | 조준 모드 플래그를 `CameraController`/`InputHandler`가 우선 가드(랠리 모드 우선순위 패턴 재사용) |
| 글로벌 쿨다운 권위-표시 불일치 | 클라 오버레이가 서버와 어긋남 | 쿨다운은 서버 권위, 클라는 발동 ClientRpc의 시각·쿨다운값으로 로컬 카운트다운(규칙 10, 25) |
| DoT/HoT 이중 틱 | 피해·회복 2배 | 서버 틱 단일화(`GameBootstrapper.Update` 싱글 / `NetworkCombatController.TickCombat` 멀티) — 기존 `TickTimedEffects`/`TickWaves` 옆에만 추가 |
| 유닛/건물 Id 카운터 충돌(반경 수집) | 잘못된 대상 제외 | 유닛/건물 버퍼 분리(규칙 29 교훈, `QuakeAttackBehavior` 선례 준수) |
| 멀티 좌표 신뢰 | 치트/맵 밖 발동 | 서버가 좌표 유효 타일 재확인 + clamp 재적용(규칙 26) |
| enum/직렬화 변경 유혹 | 서버/클라 정합성 붕괴 | `BuildingType` 불변, RaceId 종족 키 분기(규칙 1) |
| 상태효과 멀티 동기화 누락 | 클라에서 버프/제어 미재현 | 부여/해제 ClientRpc 전파, 유효 스탯 클라 재계산(서버 권위 값 기준) |
| 발동 진입점이 입력에 종속되면 AI 재사용 불가 | 후속 AI 스킬 작업 시 경로 이원화 | 발동 본체를 `SkillActivationUseCase.Activate`로 단일화, 조준/입력은 좌표 어댑터로 분리(3-8). AI는 어댑터 없이 서버에서 직접 호출 |

---

## 8. 이번 범위 명시

- **수행(계획):** 스킬 프레임워크(데이터 SO 스키마 / 타입별 실행기 / 상태변경 시스템 / 글로벌 쿨다운·오버레이 / 지점 조준 입력 / 서버 RPC / AI 발동 지원 진입점)의 2페이즈 구현 계획.
- **확정 결정 반영(설계):** ① UI = 전용 `BuildingSkillPanelUI` 신설(안 B). ② 글로벌 쿨다운 = `SkillActivationUseCase` 딕셔너리 보관(안 A). ③ AI 발동 지원(발동 진입점을 입력과 분리, 공유 API). ④ 스탯 변경 = 유효 스탯 오버레이. ⑤ 자산 = 플레이스홀더(단, 하단 X는 기존 UI 에셋 재사용). ⑥ 건설 진입점은 이미 가능(배치/건설 추가 작업 없음).
- **확정 결정 반영(main 병합 통합, §9):** 9-1 상태 배율 = 기존 접근자 합성 / 9-2 스킬 피해 = 건물 전용 경로 신설(`DamageCalculator` 직접) / 9-3 타입 A 감쇄·타입 B 무감쇄 / 9-4 곱연산 / 9-5 빙결 = 이속 배율 0 우선.
- **범위 밖:** 개별 스킬 목록·수치(데이터로 별도), 회복의 지점형 전환(추후 재결정), 개별 스킬 쿨다운(현재 건물 글로벌만), **AI의 실제 스킬 판단 로직·AI 시나리오 문서 변경**(별도 후속 task — 이번엔 시스템이 AI 발동을 지원하는 진입점까지), 정식 아트(아이콘/VFX/조준 원/오버레이), `Testcase.md`(미지시).
- 실제 코드/프리팹/에셋 변경은 **사용자 명시 승인 후** 별도 진행(현재는 계획 단계).

> **착수 준비 상태:** §9 통합 결정 5건이 모두 확정됨으로써 Plan에 미결정 분기가 남아 있지 않다. **Phase 1(타입 A·B + 프레임워크 골격)은 대안 분기 없이 곧바로 착수 가능**하며, Phase 2도 합성 방식(9-1·9-4·9-5)이 확정되어 구현 경로가 단일하다. 남은 실기 확인 항목은 9-5의 "A* 0 배율 안전 처리"뿐(Phase 2 구현 중 검증). 실제 착수는 사용자 명시 승인 시점에 시작한다.

---

## 9. 통합 방식 확정 (main 병합 후 신규 — 사용자 승인 완료, 권장안 채택)

아래 5건은 main 병합으로 생긴 통합 설계 선택 지점이며, **사용자가 권장안대로 전부 확정**했다. 선택 근거는 유지한다. 이 확정으로 **Phase 1은 대안 분기 없이 곧바로 착수 가능**하다.

| # | 결정 지점 | 확정 | 근거 |
|---|-----------|------|------|
| 9-1 | **상태 배율 합성 위치** | **(A) 기존 `UnitCombatUseCase` 접근자에 합성** — `EffectiveAttack`/`GetUnitMoveSpeedMultiplier`/`ComputeFinalDamage` 안에서 연구 배율(`_upgrade`)과 상태 배율을 함께 접는다. 별도 오버레이 미도입 | 이미 단일 읽기 지점이라 배선·회귀 위험 최소 |
| 9-2 | **스킬(건물) 출처 피해 경로** | **(A) 건물/스킬 출처 전용 즉발 피해 경로 신설** — `DamageCalculator.ApplyDefense` 직접 호출, Tank 2배 미적용. **기존 유닛 공격 경로(`ComputeFinalDamage`/`ApplyFixedDamageToVictim`)는 무변경** | 의미상 명확, 기존 경로 회귀 없음 |
| 9-3 | **타입별 방어 상호작용** | **현행 유지** — 타입 A 즉발 = 방어 감쇄 O, 타입 B 장판 DoT = 무감쇄 | 규칙(DoT 무감쇄)과 일치, 기존 DoT 구조 그대로 |
| 9-4 | **상태 배율 × 연구 배율 연산** | **곱연산** — 예: 둔화 0.5 × 이속연구 1.32 = 0.66배 | 직관적, 각 레이어 독립 |
| 9-5 | **빙결 시 이동 정지 표현** | **이동속도 배율 0 우선** — `GetUnitMoveSpeedMultiplier`가 0 반환. 구현 중 A* 이동 코루틴이 0 배율을 안전 처리하는지 검증 → 문제 시에만 별도 게이트 추가 | 최소 추가로 표현 가능, 배율 경로 재사용 |

> 위 확정은 Phase 1·Phase 2 산출물에 반영됨(§0, 3-3, 3-4, 6, 7). 9-5의 "A* 0 배율 검증"만 Phase 2 구현 중 실기 확인 항목으로 남고, 나머지는 계획 확정.

---

## 10. Phase 2(타입 C — 전역 상태변경) 완료 결과 (2026-08-05, 실기+멀티(클라) 테스트 PASS)

> **자연어 요약:** 이 계획서 §6에서 예정했던 "전역 상태변경(버프/디버프/CC/힐)"이 실제로 구현되어 실기·멀티(클라이언트 재현)로 검증되었습니다. 이로써 스킬 메커니즘 3종(A 즉발 피해 / B 장판 DoT / C 상태변경)이 모두 완성되었습니다. 계획대로 "상태효과가 없으면 기존과 완전히 동일(무변경 보장)"을 지켜 회귀가 없음을 실기로 확인했고, 계획 단계에서 열려 있던 "빙결 시 이동 정지(9-5)"는 구현·검증 과정에서 방식이 구체화되었습니다.

### 계획대로 구현된 항목

- **타입 C 실행기 + 상태효과 시스템(§6, 규칙 13):** Domain `Status/{StatusEffectKind,StatusEffect,UnitStatusState}`(순수 C# — 상태 목록→공격/이속 배율·`CanAttack` 순수 계산) + Application `Services/StatusEffectSystem`(유닛Id→상태 딕셔너리, **서버 권위** 부여/틱) + `Skill/GlobalStatusChangeExecutor`(타입 C, 조준 없음 전역 즉시, `TargetsAllies`로 대상팀 결정→상태 부여). `StatusEffectKind` = None/MoveSpeedMul/AttackPowerMul/AttackDisabled/Freeze(=이속0+공격불가)/HealOverTime.
- **상태 배율 합성 = 곱연산(확정 9-1·9-4):** `UnitCombatUseCase`의 `EffectiveAttack`(기본×연구×상태)·`GetUnitMoveSpeedMultiplier`(연구×상태, 빙결 시 0)에 상태 배율을 연구 강화 배율과 곱연산 합성. **신규 공격 게이트 `CanAttack(unit)`**(빙결/기절 시 데미지 봉쇄) — 싱글=`TryAttack`, 멀티=`NetworkCombatController.TickCombat` 2곳에 삽입. **무상태면 배율1·CanAttack true → 기존과 완전 동일(회귀 안전, 실기 확인).**
- **타입별 방어 상호작용(9-3) 현행 유지 / 회복=HoT 재사용(규칙 13):** 회복(HealOverTime)은 상태 시스템에 미저장, 기존 HoT(`ApplyTimedEffect` Heal) 재사용(HP는 `NetworkHealthSync`가 이미 동기화).
- **상태 틱 이중 틱 금지:** 서버 틱(`GameBootstrapper.Update` 싱글 / `NetworkCombatController.TickCombat` 멀티)에 `StatusEffectSystem.Tick` 추가, 클라 미러는 Update.
- **멀티 동기화:** `StatusAppliedClientRpc((int)team,(int)kind,mag,dur)`로 빙결/둔화/버프 브로드캐스트(클라가 자기 유닛 재현, 서버는 skip). 회복은 HP 동기화로 재현(이중 힐 방지).

### 계획과 달라진/구체화된 점

- **9-5 "빙결 시 이동 정지"가 두 갈래로 구체화:** 계획은 "이동속도 배율 0 우선, A* 0 배율 안전 처리 검증"이었다. 실제로는 ① **이동 코루틴이 매 프레임 유효 이동배율을 재조회**하도록 바꿔(경로 발급 시 1회 캡처 → 라이브) 배율 0(빙결)이 즉시 정지로 반영되고, **둔화(부분배율)도 즉시 라이브로 걸린다**(계획엔 없던 부수 개선). ② 여기에 더해 **빙결 시 `Animator.speed=0`으로 걷기 애니 프레임을 고정**(제자리 걷기 방지)하고 `UnitAnimState.Frozen`·`OnUnitFreezeChanged`로 순수 클라에도 정지 프레임을 재현한다. ⚠️ **미세 잔여:** 전투 종료 후 정렬 Lerp 구간은 여전히 캡처값을 사용(라이브 아님) — 필요 시 후속.
- **UI 버튼 균일화(계획 외 후속):** 스킬 슬롯 CostContainer를 `SetActive(false)`로 숨기면 레이아웃 행 높이가 붕괴 → **CanvasGroup alpha=0(HideChildKeepLayout)**로 숨겨 행 높이를 보존(버튼 크기 균일).
- **플레이스홀더 확장:** 종족별 플레이스홀더 스킬이 타입 A·B 2개에서 **5슬롯**(1 폭탄A / 2 빙결 / 3 공격버프 / 4 둔화 / 5 회복)으로 확장됨. 스킬 버튼은 임시 텍스트 라벨. **최종 스킬 기획·아이콘 확정 전까지 유지되는 테스트용.**

### 정리(cleanup)

- 개발 중 넣었던 **진단 로그 코드를 LogRules 준수 위해 제거** → `IRuntimeLogSink`/`RuntimeLoggerSink`는 **삭제됨**(상시 기능으로 기재 금지). 로그 파일 `Docs/_Logs/2026-08-04/16_49_skill-status-debug/RuntimeLog_host.txt`는 LogRules대로 보존. 좌표화 때 주석 비활성화했던 코드 3곳도 삭제 완료.
- **교훈:** 로그 작업 착수 전 반드시 `Docs/LogRules.md`(RuntimeLogger 파일 기록·raw Debug.Log 금지)를 먼저 확인할 것.

### 실기 확인 결과 (PASS)

공격버프 · 빙결(이속0 + 공격 봉쇄 + 만료 후 복귀) · 둔화(0.5 라이브) · 회복(HoT) · **무상태 회귀(기존 유닛/연구 강화 무변경)** + 순수 클라이언트 재현까지 모두 확인.

### 남은 것 (별도 작업)

① 건물 파괴 시 열린 스킬 패널/조준 UI 원복 미구현(스킬 포함 4개 건물 패널 공통 갭, `BuildingPanelBase` `OnBuildingDied` 구독→Close 방식 제안) ② 구체 스킬 목록·수치·아이콘(기획) 보류 ③ 둔화 전투종료 정렬 Lerp 잔여(위 9-5). **규칙 문서:** `GameSystemRules_Skills.md`의 구현 상태 블록을 "타입 C Phase 2 구현 완료·실기+멀티 PASS"로 갱신(규칙 13 본문은 구현과 일치해 무수정).
