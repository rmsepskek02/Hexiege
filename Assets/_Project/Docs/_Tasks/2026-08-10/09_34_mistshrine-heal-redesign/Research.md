# Research — MistShrine(물안개 신전) 힐 건물 재설계

**작성일:** 2026-08-10
**작업 폴더:** `Assets/_Project/Docs/_Tasks/2026-08-10/09_34_mistshrine-heal-redesign/`

---

## 이 작업이 무엇이고 왜 하는가 (자연어 설명 — 기술 용어 없이)

Hexiege의 초월 종족에는 **MistShrine(물안개 신전)** 이라는 건물이 있습니다.
이름 그대로 "회복시켜 주는 건물"로 만들어 두었지만, **지금까지 실제로 아무 회복도 하지 않습니다.**
건물을 지을 수는 있고 적에게 맞으면 부서지지만, 회복 기능은 한 번도 만들어진 적이 없습니다.

그리고 문서를 확인해 보니 **기획서와 기술 문서가 이 건물을 잘못 설명하고 있었습니다.**
두 문서는 MistShrine을 "초월 종족의 **방어 포탑**(적을 자동으로 공격하는 건물)인데 회복 기능도 딸려 있다"고 적어 두었는데,
실제 게임 데이터와 코드에서는 초월 종족의 방어 포탑은 **VineTower**라는 다른 건물이고,
MistShrine은 **공격을 전혀 하지 않는 별개의 회복 건물**로 되어 있습니다.
즉 문서 두 개가 서로 다른 건물을 하나로 착각해 적어 놓은 상태였습니다.

이번 작업은 그래서 두 가지를 합니다.

1. **잘못된 문서를 바로잡습니다.** "초월 방어 포탑 = MistShrine"이라는 틀린 설명을 "초월 방어 포탑 = VineTower"로 고치고,
   MistShrine은 공격하지 않는 별도 회복 건물이라는 점을 분명히 적습니다.
2. **MistShrine이 앞으로 어떻게 동작할지를 확정해 문서로 남깁니다.**
   사용하면 건물 주변에 **물안개**가 깔리고, 물안개가 유지되는 동안 **그 안에 있는 아군 부대와 아군 건물이 매초 체력을 회복**합니다.
   물안개가 걷히면 회복이 멈추고, 잠시 기다렸다가 다시 쓸 수 있습니다.

**이번 작업에서 코드는 한 줄도 건드리지 않습니다.** 문서만 정리합니다.
실제 게임에 기능이 들어가는 것은 이 문서를 바탕으로 **다음 작업**에서 진행합니다.

---

## 1. 문서 간 불일치 — 조사 결과

### 1-1. 잘못된 서술 (정정 대상)

| 문서 | 위치 | 잘못된 내용 |
|------|------|------------|
| `GameDesignDocument.md` | 건물 시스템 §4 방어 타워 (구 215행 부근) | `Transcendence(MistShrine): 공격력 15 / 쿨다운 5.0s / 범위 힐 기능 (1 HP/s, 범위 3타일)` — MistShrine을 방어 타워로 서술 |
| `GameDesignDocument.md` | 종족 시스템 초월계 (구 466행) | `**방어 타워**: Trans MistShrine — 힐 기능 포함, 쿨다운 5.0s` |
| `GameDesignDocument.md` | 종족 시스템 초월계 종족 특성 (구 470행) | `MistShrine 범위 힐 (1 HP/s, 범위 3타일)` — 재설계 이전 수치 |
| `TechnicalDesignDocument.md` | 건물 타입 주석 (구 1304행) | `// AutoTower×3종족 (Human=CannonTower, Spirit=RuneSpire, Trans=MistShrine)` |

### 1-2. 올바르게 구분하고 있던 소스 (근거)

| 소스 | 근거 |
|------|------|
| `Assets/_Project/Scripts/Domain/Building/BuildingType.cs` | `AutoTower = 2`, `HealShrine = 6` — **서로 다른 enum 값**. 주석에도 비생산 건물로 `AutoTower`와 `HealShrine`이 나란히 별개 항목으로 명시 |
| `Assets/_Project/Docs/Assets/AssetList.md` (49~50행) | `Transcendence \| VineTower \| AutoTower ★ \| 방어 타워` / `Transcendence \| MistShrine \| HealShrine ★ \| 힐 건물` — 두 줄로 명확히 분리 |
| `Assets/_Project/Docs/StatsReference.md` | 초월계 §방어 건물 = `방어포탑 (VineTower) \| AutoTower`, §특수 건물 = `힐 건물 (MistShrine) \| HealShrine` |
| `GameSystemRules_Buildings.md` 방어 타워 규칙 11 | "Spirit(RuneSpire), Transcendence(**VineTower**)는 이 규칙을 따르지 않는다" — 초월 방어 타워를 VineTower로 명시 |

### 1-3. 결론

**GDD / TDD 쪽이 오류이며 정정 대상이다.**
Transcendence 방어 타워 = **VineTower**(`BuildingType.AutoTower` = 2),
MistShrine = **별도 힐 건물**(`BuildingType.HealShrine` = 6).

---

## 2. 현재 구현 상태

| 항목 | 상태 |
|------|------|
| MistShrine 회복 로직 | **미구현.** `HealShrine`을 대상으로 하는 회복 코드가 없다 |
| MistShrine 전용 UI 패널 | **없음** |
| 회복 범위 표시 UI | **없음** |
| 물안개 지속 VFX | **없음** — `VFXSFXList.md` 기준 MistShrine 등록 VFX는 `vfx_mistshrine_destroy` / `vfx_mistshrine_upgrade` 뿐 |
| 문서상 표기 | `GameSystemRules_Upgrade.md` 후속 보류 목록, `StatsReference.md`, `PROJECT_STATUS.md`, `ROADMAP.md` 모두 "MistShrine 힐 — 미구현(보류)" 로 기재 |

---

## 3. 기존 코드 자산 조사 — 무엇을 재사용할 수 있고 무엇이 없는가

### 3-1. UI — 재사용 가능

**`Presentation/UI/BuildingPanelBase.cs` — 재사용 가능(상속)**
- 제공 기능: `AnimatedPanel` 팝업 애니메이션, 헤더 제목(`building.Type.ToString()`), 닫기[X] 버튼,
  **철거 버튼 + 환불 금액 표시**(`BuildingStats.GetTotalInvestedCost` / 2, 멀티는 `RequestDemolish`),
  배경 탭 닫기(`UIManager.ShowBlockingOverlay(Close)`), `IGameUI`(`OnGameStarted`/`OnGameEnded` 시 자동 Close).
- **건물 파괴 시 자동 Close 내장** — `GameEvents.OnBuildingDied` 구독 → `e.Building.Id == _currentBuilding.Id`면 `Close()`.
  구독 해제는 `.AddTo(this)`(UniRx). 상속만 하면 이 동작을 자동으로 얻는다.
- 확장 훅: `OnShow(building)` / `OnBeforeClose()` / `BeforeDemolish()`.
- 선례: `ResearchPanelUI : BuildingPanelBase`(연구 패널), `ProductionPanelUI`, `BuildingActionPanelUI`, `BuildingSkillPanelUI`.

**`Presentation/UI/SkillCooldownOverlay.cs` — 재사용 가능(순수 표시 컴포넌트)**
- 공개 API는 `SetCooldown(float remaining, float total)` 하나뿐이며 스킬 전용 로직이 전혀 없다.
- radial fill(`_fillImage.fillAmount = remaining / total`) + 남은 초(`Mathf.CeilToInt`) 표시, `remaining <= 0`이면 자동 숨김.
- **주의: `total`(발동 시점 총 쿨다운)이 필요**하다 → 로직 측이 남은 시간과 총 쿨다운을 모두 노출해야 한다.

**`Presentation/UI/BuildingSkillPanelUI.cs` — 재사용 불가**
- 종족 로드아웃 5슬롯(3×3 그리드: 1~5 스킬 / 6 철거 / 7~9 예약) + **지점 조준**을 전제로 만들어졌다.
- MistShrine은 슬롯도 조준도 없으므로 전제가 성립하지 않는다 → **전용 패널 신설이 맞다.**

**`Presentation/UI/ProductionPanelUI.cs` — 조작 패턴 참고**
- 롱프레스 임계값 `LongPressThreshold = 0.5f`, `_longPressTriggered` 플래그로 탭/롱프레스 분기.
- 탭: `if (!_longPressTriggered) OnUnitTap(...)` / 롱프레스: `OnUnitLongPress(...) → HandleToggleAuto(...)`.
- **자동 중 탭 = 자동 해제**: `if (state != null && state.IsAutoMode && state.AutoTypes.Contains(type)) { HandleToggleAuto(type); ... }`.
- 자동 상태 시각 표시(테두리 회전 머티리얼·인디케이터): `state.AutoTypes.Contains(_activeUnitTypes[i])`로 슬롯별 판정.

### 3-2. 자동 모드 상태 구조 — 그대로 쓰면 안 되는 부분

**`Domain/Building/ProductionState.cs`**
- `public List<UnitType> AutoTypes { get; }` + `public bool IsAutoMode => AutoTypes.Count > 0;`
  → 자동 여부를 **리스트에서 파생**시켜 불일치를 원천 차단하는 구조(2026-06-05 구조 개선 주석).
- MistShrine은 **순환할 목록이 없다**(기능이 하나뿐). 리스트 파생 방식은 의미가 없고, **bool 1개**가 적절하다.

**`Infrastructure/Network/NetworkProductionController.cs` — 3단 구조는 그대로 차용 가능**
- `RequestToggleAuto(barracksId, unitType, team)` → `ToggleAutoServerRpc(...)` → 서버 팀 검증
  (`senderClientId == 0 → Blue`, 불일치 시 경고 후 무시) → `ToggleAutoProduction` 실행 → `SyncQueueStateClientRpc`로 전체 클라 브로드캐스트.
- MistShrine 자동 토글도 이 `Request → ServerRpc(팀 검증) → ClientRpc` 3단 구조를 그대로 따르면 된다.

### 3-3. 발동 로직 — `SkillActivationUseCase` 재사용 불가

`Application/UseCases/SkillActivationUseCase.cs` 확인 결과, 아래 전제 때문에 MistShrine에 쓸 수 없다.

1. **타입 게이트가 차단한다** — `Activate` ② 단계:
   ```csharp
   if (!IsSkillBuilding(building.Type)) return false;
   // IsSkillBuilding: type == BuildingType.FlightFacility || type == BuildingType.MagicBuilding
   ```
   `HealShrine`은 여기서 무조건 `false`로 튕긴다.
2. **슬롯 인덱스 전제** — `Activate(int buildingId, int skillSlot, Vector3? aimWorld)`. MistShrine에는 슬롯 개념이 없다.
3. **종족 로드아웃 전제** — `_dataProvider.GetLoadout(race)`로 `SkillData`를 꺼낸 뒤 `skill.Cooldown` / `skill.AimType` / `skill.Mechanic`을 사용.
4. **실행기 레지스트리 전제** — `_registry.TryGet(skill.Mechanic)`으로 A/B/C 실행기를 찾는다. MistShrine의 동작은 이 세 메커니즘 중 어디에도 해당하지 않는다.

→ **전용 UseCase 신설이 필요하다.**

**단, 쿨다운 관리 패턴은 그대로 차용할 수 있다 (같은 파일):**
- `Dictionary<int, float> _cooldownRemaining` (건물 Id → 남은 시간)
- `Dictionary<int, float> _cooldownTotal` (radial fill 비율 계산용 총 쿨다운 — 오버레이가 요구)
- `TickCooldowns(float dt)` — 만료 키 수집 후 제거(순회 중 변경 예외 회피), GC 절감 재사용 버퍼
- `StartCooldownLocal(buildingId, cooldown)` — **서버 발동 시 호출 + 멀티 클라이언트가 브로드캐스트를 받아 로컬 미러로도 호출**
  (파일 상단 주석: "클라이언트는 발동을 직접 하지 않고, 쿨다운 표시용 로컬 미러(StartCooldownLocal)만 브로드캐스트로 받는다")
- 틱 진입점: 싱글 = `GameBootstrapper.Update`, 멀티 서버 = `NetworkCombatController.TickCombat`, 순수 클라 = 오버레이 미러 감소. **이중 틱 금지.**

### 3-4. 대상 수집 헬퍼 — 전부 적 대상 전용 (신규 필요)

| 헬퍼 | 위치 | 대상 |
|------|------|------|
| `CollectEnemyUnitsInRadius` | `Application/Combat/BlastAttackBehavior.cs` (`internal static`) | **적 유닛만** |
| `CollectEnemyBuildingsInRadius` | `Application/Combat/QuakeAttackBehavior.cs` (`internal static`) | **적 건물만** |
| `CollectEnemyUnitsInRadiusDomain` | `Application/UseCases/UnitCombatUseCase.cs` | **적 유닛만**(도메인 월드 좌표 + TeamId 기준, 스킬용) |
| `CollectEnemyBuildingsInRadiusDomain` | `Application/UseCases/UnitCombatUseCase.cs` | **적 건물만**(스킬용) |

→ **아군 유닛 + 아군 건물 수집 헬퍼는 존재하지 않는다. 신규 작성 필요.**
팀 필터가 반대이고, 시전 건물 자신과 본기지(Castle)도 포함해야 한다는 점이 기존 헬퍼와 다르다.
(선례: BloomFairy 힐러가 "팀 필터 반대·본인 포함"으로 부상 아군을 탐색한다 — `GameSystemRules_Units.md` 규칙 32~37. 다만 그쪽은 유닛 전용 단일 지정이다.)

### 3-5. 힐 서브시스템 — 유닛 전용 (건물 경로 신규 필요)

- `Domain/Unit/UnitData.cs`에 `public void Heal(int amount)` 존재 (MaxHp 클램프).
- `Domain/Building/BuildingData.cs`에는 **회복 메서드가 없다** — `TakeDamage(int damage)`만 있고 `Hp`는 `private set`.
- `GameEvents.OnEntityHealed`(`EntityHealedEvent`)는 존재하며 `IDamageable entity` + `bool isUnit`을 담는다 → **이벤트 구조 자체는 건물도 표현 가능**.
- 멀티 동기화 `Infrastructure/Network/NetworkHealthSync.cs`의 힐 경로는 현재 유닛을 전제로 동작한다
  (`GameEvents.OnEntityHealed.OnNext(new EntityHealedEvent(unit, unit.Hp, isUnit: true, ...))`).
- HoT는 `UnitCombatUseCase.TimedEffectKind` 버킷(`Heal` / `Damage`)으로 `(TargetId, Kind)` 키 관리 →
  같은 `Heal` 버킷에 넣으면 서로 덮어쓴다(`GameSystemRules_Upgrade.md` 규칙 7이 자연회복을 별도 채널로 뺀 이유).

→ **건물 회복 경로(도메인 회복 + 멀티 HP 동기화)를 신규로 마련해야 한다.**
→ 물안개 힐은 **자연회복·BloomFairy 힐과 겹치지 않는 독립 채널**로 구현해야 한다.

### 3-6. 회복 텍스트 — 기존 경로로 커버 가능 (신규 UI 불필요)

`Presentation/UI/FloatingHpTextSpawner.cs` 확인 결과:
```csharp
Vector3 worldPos = evt.IsUnit
    ? _positionProvider.GetUnitWorldPosition(evt.Entity.Id)
    : _positionProvider.GetBuildingWorldPosition(evt.Entity.Id);
```
→ `ShowHeal(EntityHealedEvent)`가 이미 `IsUnit` 분기로 **건물 월드 좌표를 지원**한다(피격 텍스트 `ShowDamage`와 동일 구조).
따라서 **건물 위 회복 텍스트를 위한 신규 UI는 필요 없다.**
(코드 주석에는 "현재 힐 대상은 유닛만"이라 적혀 있으나 분기 자체는 건물을 이미 처리한다.)

또한 `evt.ShowText == false`인 이벤트는 텍스트를 그리지 않고 HP 적용/동기화만 한다 →
**힐 틱과 텍스트 표시 주기를 분리하는 기존 메커니즘이 이미 존재**한다(BloomFairy HoT가 "완료 시 1회 표시"로 억제한 방식).

### 3-7. 지면 데칼 셰이더 — 범위 표시 UI에 재사용 여지

`Assets/_Project/Shaders/SkillAimOverlay.shader`
(Transparent + ZWrite Off + **ZTest LEqual + Offset -1,-1** + Cull Off)
= 지형(ProBuilder 실린더 타일)에는 가려지지 않고 불투명 유닛/건물에는 정상적으로 가려지는 지면 데칼.
스킬 조준원의 z-fighting을 해결한 검증된 자산이다. **ZTest Always는 금지**(투시 문제).
→ MistShrine 범위 표시 반투명 원형 UI에 재사용 여지가 있다.

---

## 4. 영향 범위

### 4-1. 이번 작업(문서)의 영향 범위

문서만 수정하며 **코드·프리팹·씬·에셋은 일절 건드리지 않는다.**

| 문서 | 영향 |
|------|------|
| `GameSystemRules/GameSystemRules_Buildings.md` | MistShrine 물안개 힐 시스템 섹션 신설(규칙 1~27) + 방어 타워 섹션에 교차 참조 |
| `GameSystemRules.md` | 인덱스 표·빠른 참조 갱신 |
| `GameSystemRules/GameSystemRules_UI.md` | MistShrine 패널 UI 섹션 신설(규칙 1~9) |
| `GameSystemRules/GameSystemRules_Upgrade.md` | 후속 보류 항목 갱신, 자연회복과 중첩 명시, 규칙 1 ×10 값 재검토 주석 |
| `GameDesignDocument.md` | 방어 타워 오류 정정, MistShrine 절 신설(§5), 종족 특성 갱신, 버전/일자 |
| `TechnicalDesignDocument.md` | 건물 타입 주석 정정, 버전/일자 |
| `StatsReference.md` | 특수 건물 표 MistShrine 항목 갱신(미확정 명시) |
| `PROJECT_STATUS.md` | 보류 항목 상태 갱신 + 확정 설계 항목 추가 |

### 4-2. 향후 구현 작업이 건드릴 영역 (참고 — 이번 범위 아님)

- **Domain:** `BuildingData` 회복 경로
- **Application:** MistShrine 전용 UseCase 신규, 아군 유닛/건물 수집 헬퍼 신규, 힐 채널 분리
- **Infrastructure:** MistShrine 네트워크 컨트롤러(NetworkBehaviour) 신규, 건물 HP 힐 동기화
- **Presentation:** MistShrine 전용 패널 신규, 범위 표시 오브젝트 신규
- **에셋:** 물안개 지속 VFX **신규 제작 필요**, 범위 표시 머티리얼
- **씬/Inspector:** 패널 프리팹 배치·배선, 범위 표시 오브젝트 배선

---

## 5. 조사 중 발견한 부가 이슈

1. **`GameSystemRules_Buildings.md`의 규칙 번호가 섹션마다 1부터 다시 시작한다.**
   기존에도 "랠리포인트 규칙 2", "건물 철거 규칙 4~6", "방어 타워 규칙 1~12"가 각각 독립 번호였고,
   다른 문서(`GameSystemRules_Upgrade.md` 등)가 `GameSystemRules_Buildings.md 규칙 9`처럼 **섹션명 없이** 인용한 사례가 있다.
   MistShrine 섹션이 추가되면 모호성이 커지므로, 이번에 파일 서두에 "섹션명을 함께 적는다"는 인용 규칙을 명시하고
   `GameSystemRules_Upgrade.md`의 해당 인용에 섹션명을 보강했다.

2. **`ROADMAP.md`에도 MistShrine 관련 미확정 수치 표기가 있다**
   (`MistShrine 힐량/범위 (Trans) | 1 HP/s / 범위 3 | 확정 — 플레이테스트 후 조정`, 그리고 후속 보류 목록).
   이번 작업 지시 범위(문서 8종)에는 `ROADMAP.md`가 포함되어 있지 않아 **수정하지 않았다.**
   재설계로 이 수치가 "확정"이 아니라 "미확정"이 되었으므로 **추후 사용자 승인 후 갱신이 필요하다**(CLAUDE.md 규칙 6 — 범위 초과 금지).

3. **`GameDesignDocument.md` 변경 이력 표에 1.10.0 / 1.11.0 항목이 누락되어 있다**(헤더 버전은 1.11.0인데 표의 최신 항목은 1.9.0).
   이번 작업과 무관한 기존 결손이며, 임의 보완하지 않고 기록만 남긴다.

4. **`FloatingHpTextSpawner.ShowHeal`의 주석("현재 힐 대상은 유닛만")이 코드보다 좁게 서술되어 있다.**
   코드는 이미 `IsUnit` 분기로 건물을 처리하므로, 건물 힐 구현 시 주석을 함께 갱신하는 것이 좋다(구현 단계 항목).
