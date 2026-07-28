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

---

## 1. 신규 파일 목록 (레이어별, 경로 제안)

### Domain (순수 C#, Core/Unity 참조 금지)
- `Scripts/Domain/Skill/SkillMechanicType.cs` — enum `{ InstantAreaDamage(A), AreaDotDamage(B), GlobalStatusChange(C) }`. → 규칙 11~13
- `Scripts/Domain/Skill/SkillAimType.cs` — enum `{ Instant, PointTarget }`. → 규칙 15, 16
- `Scripts/Domain/Status/StatusEffectKind.cs` — enum `{ MoveSpeedMul, AttackDisabled, AttackPowerMul, ... , HealOverTime }`(제어·버프·디버프·회복 통합 표현). → 규칙 13
- `Scripts/Domain/Status/StatusEffect.cs` — 값 객체(Kind/Magnitude/RemainingDuration/SourceTeam). 부여 시각·잔여시간 관리 단위.
- `Scripts/Domain/Status/UnitStatusState.cs` — 한 유닛의 활성 상태효과 목록 + **유효 스탯 계산**(기본 스탯 + 활성 효과 → EffectiveMoveSpeed/CanAttack/EffectiveAttackPower 등). Domain 순수.

### Application (유스케이스/실행기/인터페이스, Netcode 직접 참조 금지)
- `Scripts/Application/Skill/ISkillExecutor.cs` — `void Execute(SkillActivationContext ctx)` 전략 계약(특수공격 `ISpecialAttackBehavior` 패턴 원용). → 규칙 7
- `Scripts/Application/Skill/InstantAreaDamageExecutor.cs` — 타입 A. → 규칙 11
- `Scripts/Application/Skill/AreaDotDamageExecutor.cs` — 타입 B. → 규칙 12
- `Scripts/Application/Skill/GlobalStatusChangeExecutor.cs` — 타입 C. → 규칙 13
- `Scripts/Application/Skill/SkillExecutorRegistry.cs` — `SkillMechanicType → ISkillExecutor`(UseCase 내부 생성, `SpecialAttackRegistry`와 동일 성격). → 규칙 7
- `Scripts/Application/Skill/SkillActivationContext.cs` — 실행기에 넘길 컨텍스트(시전 건물·팀·조준 좌표·유닛/건물 목록·좌표 조회·재사용 피해/DoT/힐/상태부여 델리게이트·스킬 파라미터). `SpecialAttackContext`와 유사.
- `Scripts/Application/UseCases/SkillActivationUseCase.cs` — **서버 권위 발동 오케스트레이션**: 재검증(건물 생존·글로벌 쿨다운·유효 타일) → 실행기 호출 → 글로벌 쿨다운 설정 → 결과 이벤트. → 규칙 25, 26
- `Scripts/Application/Services/StatusEffectSystem.cs` — 상태효과 부여/해제/지속시간 틱(서버 권위). `_activeTimedEffects`(HoT/DoT)와 병렬 소유·틱 패턴. → 규칙 13
- `Scripts/Application/Interfaces/ISkillDataProvider.cs` — 종족 키(RaceId) + 슬롯 → 스킬 정의 조회 인터페이스(의존성 역전: 구현은 Infrastructure). → 규칙 1, 6, 7
- `Scripts/Application/Interfaces/INetworkSkillController.cs` (조건부) — 멀티에서 발동 요청 래퍼 인터페이스(Presentation→Application, 구현은 Infrastructure). NGO 직접 의존 회피.

### Infrastructure (Config SO / NetworkBehaviour)
- `Scripts/Infrastructure/Config/SkillDefinition.cs` — `ScriptableObject`. 아이콘/조준방식/쿨다운/타입/파라미터(아래 5장 스키마). → 규칙 7
- `Scripts/Infrastructure/Config/SkillLoadoutConfig.cs` — `ScriptableObject`. **RaceId → SkillDefinition[]**(최대 5). `ISkillDataProvider` 구현 제공. → 규칙 1, 4, 6
- `Scripts/Infrastructure/Network/NetworkSkillController.cs` — `NetworkBehaviour`. `RequestActivateSkill(...)` 래퍼 → `...ServerRpc` → 서버 재검증·실행 → `...ClientRpc`(VFX/오버레이). → 규칙 25, 26

### Presentation (MonoBehaviour/UI)
- `Scripts/Presentation/Input/SkillAimController.cs` — 지점 조준 모드(press→드래그 추적→조준점 이동→엣지 스크롤→release 발동/취소) 상태 머신. → 규칙 17~24
- `Scripts/Presentation/Effects/SkillAimReticle.cs` — 조준점(범위 원) 월드 표시(반경 시각화). → 규칙 17
- `Scripts/Presentation/UI/SkillCooldownOverlay.cs` — 슬롯 위 radial(clockwise) fill + 남은초 텍스트. → 규칙 10
- `Scripts/Presentation/UI/SkillCancelButton.cs` (또는 기존 HUD에 배치) — 하단 중앙 X 취소 영역. → 규칙 20, 21

> **패널 확장 방식 결정 제안**: 스킬 건물 슬롯 1~5의 동적 구성 + 쿨다운 오버레이는 (안 A) `BuildingActionPanelUI`에 "스킬 모드"를 추가하거나, (안 B) `BuildingSkillPanelUI : BuildingPanelBase` 전용 클래스를 신설하는 두 안이 있습니다. **완성도 우선 원칙(절대규칙 7)** 관점에서 스킬 특유의 동적 슬롯·오버레이·조준 연동이 많으므로 **안 B(전용 패널 신설, `BuildingPanelBase` 재사용)**를 권장합니다. 최종 채택은 사용자 승인으로 확정합니다.

---

## 2. 수정 파일 목록 (전부 추가적 확장)

| 파일 | 변경 내용 | 근거 규칙 |
|------|-----------|-----------|
| `Presentation/Input/InputHandler.cs` | `HandleClick` 최상단 랠리 분기 옆에 스킬 조준 모드 가드 추가, 또는 `SkillAimController`에 입력 위임 | 16, 17 |
| `Presentation/UI/BuildingActionPanelUI.cs` | 스킬 건물 클릭 시 전용 패널로 라우팅(안 B 채택 시) / 또는 슬롯 동적 채움(안 A) | 8, 9 |
| `Presentation/Camera/CameraController.cs` | `EdgeScroll(screenPos, dt)` 신규 메서드 추가(이동 후 기존 `ClampPosition()` 재사용) | 18, 23 |
| `Domain/Unit/UnitData.cs` | `UnitStatusState` 참조 + 유효 스탯 접근자(EffectiveMoveSpeed/CanAttack 등) 노출 | 13 |
| `Application/UseCases/UnitCombatUseCase.cs` | 타입 B 반경 DoT 부여 진입점(반경 수집 + `ApplyDamageOverTime`), 타입 C 회복 진입점(HoT 재사용)을 `SkillActivationContext`에 델리게이트로 노출 | 12, 13 |
| `Application/UseCases`(전투 틱 진입점) | 서버 틱에 `StatusEffectSystem.Tick(dt)`·글로벌 쿨다운 틱 추가(`GameBootstrapper.Update` 싱글 / `NetworkCombatController.TickCombat` 멀티, 이중 틱 금지) | 3, 13, 25 |
| `Domain/Building/BuildingData.cs` (조건부) | 글로벌 쿨다운을 BuildingData에 둘 경우 `SkillCooldownRemaining` 필드 추가(대안: UseCase 딕셔너리) | 3 |
| `Bootstrap/GameBootstrapper*.cs` | 스킬 UseCase·실행기·`SkillLoadoutConfig`·`StatusEffectSystem`·`SkillAimController`·`NetworkSkillController` 생성·주입·배선(유일 조합 루트) | 아키텍처 |

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

### 3-2. 종족 키 분기 로드아웃 (`SkillLoadoutConfig`) → 규칙 1, 6

- **enum 미변경.** `MagicBuilding`을 Spirit/Trans가 공유하므로, 로드아웃은 enum이 아니라 **RaceId 키**로 분기: `SkillLoadoutConfig`가 `Dictionary<RaceId, SkillDefinition[]>`(최대 5, 규칙 4)를 보유. 각 종족은 스킬 건물이 정확히 1종이라 RaceId로 유일 결정.
- 조회: 건물 클릭 시 `building.Team` → `GameRaceContext.BlueRace/RedRace` → `ISkillDataProvider.GetLoadout(race)`. `BuildingActionPanelUI.Show`가 이미 이 race 변환을 사용(재사용).
- FlightFacility(Human)도 동일 경로(별도 enum이지만 RaceId 키로 통일).

### 3-3. 타입 A/B/C 실행기 + 재사용 지점 연결

- **타입 A — 즉발 범위 피해** (`InstantAreaDamageExecutor`) → 규칙 11
  - 조준 좌표를 중심으로 `BlastAttackBehavior.CollectEnemyUnitsInRadius`(유닛) + `QuakeAttackBehavior.CollectEnemyBuildingsInRadius`(건물) 재사용(2단계 선수집→적용).
  - 피해 적용은 `UnitCombatUseCase.ApplyFixedDamageToVictim`(임의 량 즉발, 이미 존재) 델리게이트 재사용. 아군 제외.
- **타입 B — 범위 지속 피해(장판)** (`AreaDotDamageExecutor`) → 규칙 12
  - 반경 수집(위와 동일) → 각 적 유닛에 `UnitCombatUseCase.ApplyDamageOverTime`(discrete 초 단위 틱) 부여. 신규 진입점(스킬 전용 튜닝값)으로 `ApplyBlastDot`/`ApplyInfernoDot`처럼 값 분리.
  - 틱은 기존 `TickTimedEffects(dt)`가 그대로 소비(추가 틱 루프 불필요).
- **타입 C — 전역 상태변경** (`GlobalStatusChangeExecutor`) → 규칙 13, 15
  - 조준 없음(전역 즉시). `TargetsAllies`에 따라 아군/적 유닛 전체 순회 → `StatusEffectSystem.Apply(unit, StatusEffect)`.
  - 회복은 `StatusEffectKind.HealOverTime`으로 표현하고 내부적으로 기존 `ApplyTimedEffect(Heal)`(HoT) 재사용 → 회복은 우선 전역 즉시(규칙 13, 추후 지점형 전환 여지).

### 3-4. 상태변경 시스템 (버프 개념 최초 도입) → 규칙 13

**최대 신규 덩어리.** `UnitData`의 이동/공격/사거리 스탯이 전부 **readonly(get-only)**(근거: `Domain/Unit/UnitData.cs` L52~72)이므로 원본을 직접 못 바꿈. 따라서 **유효 스탯 오버레이** 방식을 도입:

- **Domain**: `UnitStatusState`(유닛별 활성 `StatusEffect` 목록). 기본 스탯 + 활성 효과를 합성해 **유효 스탯 계산**을 제공(`EffectiveMoveSpeed`, `CanAttack`, `EffectiveAttackPower`, `EffectiveDetectRange`…). 순수 계산, Core/Unity 미참조.
- **Application**: `StatusEffectSystem`이 부여/해제/지속시간 감소를 서버 권위로 관리(`_activeTimedEffects`와 동일 소유·틱 패턴). 빙결=`MoveSpeedMul 0` + `AttackDisabled`, 둔화=`MoveSpeedMul <1`, 버프=`AttackPowerMul >1` 등으로 **하나의 시스템으로 통합**(규칙 13).
- **읽기 지점 연결**: 이동 속도(`UnitView` 이동/A*), 공격 가능·공격력·쿨다운(`UnitCombatUseCase`), 감지(`DetectRange` 사용처)가 **유효 스탯을 읽도록** 갈아끼움. 조사 다음 단계에서 스탯 읽기 지점 전수 파악 필요(Research 5-1).
- **배치 판단**: 값 객체·유효 스탯 계산=Domain, 부여/틱/해제 오케스트레이션=Application(서버 권위). 멀티 동기화는 상태 부여/해제를 ClientRpc로 전파(연출·유효 스탯 재현).

### 3-5. 건물 글로벌 쿨다운 상태 + UI 오버레이 → 규칙 3, 10

- **상태 보관 위치(제안)**: 스킬 발동/판정이 서버 권위 UseCase에 있으므로, **`SkillActivationUseCase`가 `Dictionary<int(buildingId), float remaining>`로 보유**하고 서버 틱에서 감소시키는 안을 권장(도메인 `BuildingData`에 스킬 전용 필드를 늘리지 않아 응집도 유지). 대안(BuildingData `SkillCooldownRemaining` 필드)은 AutoTower `AttackCooldownRemaining`와 대칭이라 단순하지만 도메인 확장이 큼 → 사용자 승인으로 택1.
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

`NetworkBuildingController`의 래퍼+ServerRpc+ClientRpc 템플릿을 그대로 따름.

- **요청(클라→서버)**: `RequestActivateSkill(int buildingId, int skillSlot, int q, int r)` 래퍼 → `RequestActivateSkillServerRpc(...)`(`[ServerRpc(RequireOwnership=false)]`). 즉시형(타입 C)은 좌표 없이 `slot`만.
- **서버 재검증**(클라 입력 불신뢰, 규칙 26): ① `_services`(IGameServices) null → ② 발신자 팀 소유권(`SenderClientId`→Blue/Red) + 건물 소유 → ③ **건물 생존** → ④ **글로벌 쿨다운 만료** → ⑤ (지점형) 전송 좌표가 **유효 맵 타일**인지 재확인(규칙 22 clamp를 서버에서 재적용).
- **실행**: 재검증 통과 시 `SkillActivationUseCase.Activate(...)` → 실행기(A/B/C) → 판정·피해·상태변경 **서버 실행**(규칙 25) → 글로벌 쿨다운 설정.
- **전파(서버→클라)**: `ActivateSkillClientRpc(...)`로 VFX·쿨다운 오버레이 시작·상태효과 재현. 조준점 이동/범위 미리보기/엣지 스크롤은 **로컬 표현일 뿐**(게임 상태 미변경, 규칙 25).
- **메서드명**: `...ServerRpc`/`...ClientRpc` 접미사 필수(아키텍처 제약).

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

## 6. 구현 순서 (단계마다 컴파일 유지)

1. **Domain 값 객체·enum**(`SkillMechanicType`/`SkillAimType`/`StatusEffectKind`/`StatusEffect`/`UnitStatusState`) — 독립, 컴파일 안전.
2. **상태변경 시스템 골격**(`StatusEffectSystem` + `UnitData` 유효 스탯 접근자) — 아직 읽기 지점 미연결(부여해도 무효과), 컴파일 안전.
3. **읽기 지점 연결**(이동/전투/감지가 유효 스탯 참조) — 스탯 읽기 지점 전수 반영. 상태효과 없으면 기존과 동일(무변경 보장).
4. **스킬 데이터 SO·로드아웃**(`SkillDefinition`/`SkillLoadoutConfig`/`ISkillDataProvider`) + 에셋 생성.
5. **실행기 A/B/C + 레지스트리 + `SkillActivationContext`/`SkillActivationUseCase`** — 기존 AoE/DoT/HoT 델리게이트 재사용.
6. **글로벌 쿨다운 상태·틱** — 서버 틱 진입점에 추가.
7. **UI: 전용 패널(안 B) + 슬롯 1~5 동적 채움 + 쿨다운 오버레이** — 즉시형(타입 C) 먼저 end-to-end 연결.
8. **지점 조준 입력**(`SkillAimController`/`SkillAimReticle` + `CameraController.EdgeScroll` + X 취소) — 타입 A/B 연결.
9. **멀티: `NetworkSkillController`(ServerRpc/ClientRpc) + 서버 재검증** — 싱글 검증 후 멀티 배선.
10. **GameBootstrapper 배선** — 각 단계와 함께 점진 주입.

> 각 단계 종료 시 컴파일 가능 상태 유지. 상태효과·조준·RPC는 서로 독립이라 순서 조정 가능하되, 3(읽기 지점 연결)은 무변경 보장을 반드시 실기 확인.

---

## 7. 위험 요소 + 완화책

| 위험 | 영향 | 완화책 |
|------|------|--------|
| readonly 스탯 → 유효 스탯 레이어 도입 시 이동/전투 읽기 지점 누락 | 둔화/빙결/버프가 일부만 적용 | 스탯 읽기 지점 전수 grep(MoveSpeed/AttackPower/AttackRange/DetectRange) 후 체크리스트로 반영, 3단계 무변경 실기 검증 |
| 조준 입력과 카메라 팬·타일 선택 입력 소유권 충돌 | 조준 중 오작동(팬/선택) | 조준 모드 플래그를 `CameraController`/`InputHandler`가 우선 가드(랠리 모드 우선순위 패턴 재사용) |
| 글로벌 쿨다운 권위-표시 불일치 | 클라 오버레이가 서버와 어긋남 | 쿨다운은 서버 권위, 클라는 발동 ClientRpc의 시각·쿨다운값으로 로컬 카운트다운(규칙 10, 25) |
| DoT/HoT 이중 틱 | 피해·회복 2배 | 서버 틱 단일화(`GameBootstrapper.Update` 싱글 / `NetworkCombatController.TickCombat` 멀티) — 기존 `TickTimedEffects`/`TickWaves` 옆에만 추가 |
| 유닛/건물 Id 카운터 충돌(반경 수집) | 잘못된 대상 제외 | 유닛/건물 버퍼 분리(규칙 29 교훈, `QuakeAttackBehavior` 선례 준수) |
| 멀티 좌표 신뢰 | 치트/맵 밖 발동 | 서버가 좌표 유효 타일 재확인 + clamp 재적용(규칙 26) |
| enum/직렬화 변경 유혹 | 서버/클라 정합성 붕괴 | `BuildingType` 불변, RaceId 종족 키 분기(규칙 1) |
| 상태효과 멀티 동기화 누락 | 클라에서 버프/제어 미재현 | 부여/해제 ClientRpc 전파, 유효 스탯 클라 재계산(서버 권위 값 기준) |

---

## 8. 이번 범위 명시

- **수행(계획):** 스킬 프레임워크(데이터 SO 스키마 / 타입별 실행기 / 상태변경 시스템 / 글로벌 쿨다운·오버레이 / 지점 조준 입력 / 서버 RPC)의 구현 계획.
- **범위 밖:** 개별 스킬 목록·수치(데이터로 별도), 회복의 지점형 전환(추후 재결정), 개별 스킬 쿨다운(현재 건물 글로벌만), `Testcase.md`(미구현·미지시).
- 실제 코드/프리팹/에셋 변경은 **사용자 명시 승인 후** 별도 진행(현재는 계획 단계).
