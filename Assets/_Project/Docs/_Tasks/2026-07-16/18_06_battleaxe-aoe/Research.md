# Research — 도끼병(BattleAxe) 휩쓸기형 AoE 구현

## 이 작업이 무엇인지 (자연어 설명)

도끼병(BattleAxe)은 도끼를 크게 휘둘러 주변의 적을 한 번에 여러 마리 베는 근접 유닛입니다.
지금까지 게임의 모든 유닛은 "한 번 공격하면 한 마리만 때린다"는 단일 타깃 방식으로만
동작했습니다. 이 작업은 도끼병에게 **범위 공격(AoE)** 을 처음으로 부여하는 작업입니다.

도끼병이 공격할 때, 도끼병을 둘러싼 **주변 6개 타일 중 도끼병의 등 뒤 방향 타일 1개를
제외한 나머지 "전방 5개 타일"에 있는 모든 적 유닛**에게 동일한 피해(공격력 15)를 줍니다.
겹쳐 있는 유닛도 모두 맞습니다(휩쓸기형 규칙). 아군은 맞지 않습니다.

이 작업은 앞으로 진행할 특수 유닛 5종(BattleAxe / QuakeSpirit / TorrentSpirit /
MushroomBomber / BloomFairy) 중 **첫 번째**이며, 도끼병을 가장 단순한 형태의 AoE로
먼저 구현해 특수 공격 처리 구조의 기반을 잡는 것이 목적입니다.

---

## 대상 유닛 스펙 (StatsReference.md 기준)

| 항목 | 값 |
|------|----|
| UnitType | `BattleAxe` (enum 값 5, 인간계) |
| HP | 80 |
| 공격력 | 15 |
| 공격 사거리 | 0.5 (근접) |
| 감지 사거리 | 1.0 |
| 이동 속도 | 1 |
| 공격 쿨다운 | 1:02(3:05) — 타격 1.02초, 쿨다운 3.05초 |
| 생산 시간 / 비용 / 인구 | 20초 / 200 / 1 |
| 특수 능력 | **휩쓸기형 AoE** — 이동 방향 기준 전방 5타일 모든 유닛 동일 피해 |

### "전방 5타일"의 정확한 정의 (사용자 확정, 2026-07-16)

> 도끼병을 기준으로 **주변 타일 6개 중 도끼병의 뒤에 있는 타일 1개를 제외한 나머지 5개 타일**.

- 헥스 그리드에서 한 타일의 이웃은 6개(`HexDirection` 6방향).
- "뒤 타일" = 도끼병이 바라보는 방향(`Facing`)의 **반대 방향**(`Facing.Opposite()`) 이웃 1개.
- "전방 5타일" = 나머지 5개 이웃 타일.
- 부채꼴/원뿔형이 아니라 **인접 6타일 중 5개**라는 점이 핵심.

### AoE 피해 규칙 (StatsReference.md — 파도형/휩쓸기형)

> 범위 내 **모든 유닛**에게 동일 피해 적용 (같은 타일 겹침 여부 무관)

- 착탄형(중심 100% / 나머지 50%)과 달리 휩쓸기형은 **전원 동일 피해**.
- 즉 전방 5타일 위의 모든 적 유닛이 각각 공격력 15의 피해를 받는다.

---

## 현재 전투 코드 구조 (파악 결과)

### 피해 적용 수렴점 — `UnitCombatUseCase.ExecuteAttack`

파일: `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs:785`

```
private void ExecuteAttack(UnitData attacker, IDamageable target)
{
    // 공격 방향 계산 — 이 시점에 attacker.Facing이 타겟 방향으로 갱신됨
    HexDirection attackDir = FacingDirection.FromCoords(attacker.Position, target.Position);
    attacker.Facing = attackDir;

    // 데미지 적용 (단일 타깃)
    target.TakeDamage(attacker.AttackPower);

    // 공격 이벤트 발행
    GameEvents.OnEntityAttacked.OnNext(new EntityAttackedEvent(attacker, target));

    // 피격 이벤트 발행 — NetworkHealthSync가 구독하여 HP를 모든 클라이언트에 동기화
    GameEvents.OnEntityDamaged.OnNext(new EntityDamagedEvent(target, target.Hp, targetIsUnit,
        attackerId: attacker.Id, attackerIsUnit: true));

    // 타겟 사망 처리 (OnUnitDied / OnBuildingDied 발행 + 데이터 정리)
    if (!target.IsAlive) { ... }
}
```

**중요 사실**:
1. **단일/멀티 공통 수렴점**: 이 메서드는 단일플레이(`ApplyAttackDamage` → `ExecuteAttack`)와
   멀티플레이(`NetworkCombatController.ExecuteAttack` → `combat.ApplyAttackDamage` →
   `UnitCombatUseCase.ExecuteAttack`) **양쪽 모두**에서 실제 데미지를 적용하는 유일한 지점.
   → 여기에 AoE 로직을 넣으면 두 모드에 동시에 반영된다.
2. 공격 순간 `attacker.Facing`이 **타겟 방향으로 갱신**된다. 따라서 "뒤 타일"은
   `attacker.Facing.Opposite()`로 결정론적으로 구할 수 있다.
3. HP 동기화는 `OnEntityDamaged` 이벤트에 의존한다. AoE로 여러 유닛을 때리려면
   **각 피해 대상마다 동일한 피해+이벤트+사망 처리 절차를 반복**해야 멀티플레이 HP가 맞는다.

### 데미지 타이밍 (규칙 U-17/U-18)

- 데미지는 서버 타이머 기반으로 `HitFrameTimes`(Attack 클립 `OnAttackHit` 이벤트 시간)마다 적용.
- 도끼병은 단일 히트(휩쓸기 1회) → `HitFrameTimes` 원소 1개(타격 1.02초).
- ROADMAP F-4: 도끼병 Attack 클립에 `OnAttackHit` 이벤트 주입은 별도 에셋 작업으로 미완료.
  (스탯 Config에 폴백 타격 시간 입력 시 코드 동작은 검증 가능)

### 유닛/방향/좌표 API (구현에 필요한 도구)

| API | 위치 | 용도 |
|-----|------|------|
| `UnitData.Facing` (HexDirection) | `Domain/Unit/UnitData.cs:45` | 공격 시 타겟 방향으로 갱신됨 |
| `UnitData.Position` (HexCoord) | `Domain/Unit/UnitData.cs:38` | 도끼병 현재 타일 |
| `HexDirection.Opposite()` | `Domain/Hex/HexDirection.cs:92` | 뒤 방향 계산 |
| `HexDirection.Neighbor(origin)` | `Domain/Hex/HexDirection.cs:83` | 전방 타일 좌표 계산 |
| `_unitSpawn.Units` (Dictionary) | UnitCombatUseCase 내부 참조 | 전체 유닛 순회로 대상 수집 |
| `UnitData.Team` (TeamId) | `Domain/Unit/UnitData.cs:35` | 아군/적군 판별 |

### 유닛 데이터 정의 지점 (AoE 여부를 어디에 둘지)

- `UnitType` enum: `Domain/Unit/UnitType.cs` — BattleAxe = 5 등록됨.
- `UnitStats` + `StatValues`: `Domain/Unit/UnitStats.cs` — 스탯을 Dictionary로 보관.
  현재 필드: MaxHp, AttackPower, AttackRange, DetectRange, MoveSpeed, AttackCooldown, HitFrameTimes.
  → 특수 공격 타입을 담을 필드가 **아직 없음**.
- Config: Infrastructure의 `UnitStatsConfig`(ScriptableObject)에서 값 주입.

---

## 영향 범위 (방식 C 기준)

| 파일 | 예상 변경 | 신규/수정 |
|------|-----------|-----------|
| `Application/UseCases/UnitCombatUseCase.cs` | 단일 피해 절차를 재사용 헬퍼로 추출 + `ExecuteAttack` 말미에서 특수 공격 핸들러 호출 1줄 | 수정 |
| `ISpecialAttackBehavior` (인터페이스) | 특수 공격 1종 = 클래스 1개의 공통 계약 `Apply(SpecialAttackContext)` | 신규 |
| `SpecialAttackContext` | 핸들러에 전달할 컨텍스트(공격자, 주 타깃, 유닛 목록, 피해/이벤트 헬퍼) | 신규 |
| 특수 공격 레지스트리 | `UnitType → ISpecialAttackBehavior` 매핑. 도끼병만 등록 | 신규 |
| `SweepAttackBehavior` | 도끼병 전방 5타일+자기 타일 휩쓸기 구현 | 신규 |
| (검토) `Domain` 순수 함수 | "전방 5타일(+자기) 좌표 계산"을 순수 함수로 분리(테스트/재사용) | 신규(선택) |

- **레이어 배치**: 핸들러/레지스트리/컨텍스트의 정확한 레이어(Application vs Domain)와 파일 경로는
  아키텍처 제약(`.claude/MEMORY.md`: Application → Infrastructure 역참조 금지 등)에 맞춰 game-programmer가 확정.
- 멀티플레이 경로(`NetworkCombatController`)는 `ExecuteAttack`을 그대로 호출하므로 **직접 수정 불필요**
  (단, AoE가 다중 `OnEntityDamaged`를 발행하는 것을 NetworkHealthSync가 정상 처리하는지 확인 필요).

---

## 확정 결과 (사용자 승인 2026-07-16)

> 규칙 12에 따라 확인 후 확정. 상세 근거·구현은 Plan.md 참조.

1. **도끼병 자기 타일 겹친 적 → 포함** (D-1)
   - 실제 판정 범위 = **전방 5타일 + 도끼병 자기 타일**.
   - 이유: 겹침 허용 구조상 바로 붙은 적이 안 맞으면 어색.
2. **건물 공격 시에도 전방 적 유닛에 AoE 적용** (D-2)
   - 주 타깃이 건물이어도 전방 5타일(+자기 타일)의 적 유닛에는 AoE 적용.
   - 건물 자체는 AoE 대상이 아니며 주 타깃일 때만 단일 피해.
3. **주 타깃 중복 피해 방지 → 필수** (D-3)
   - 주 타깃은 기존 단일 경로로 1회만 피해. AoE 수집에서 주 타깃 Id 제외.
4. **특수 공격 구조 → 방식 C: 전략(핸들러) 분리** (D-4)
   - `ISpecialAttackBehavior` 인터페이스 + `UnitType` 키 레지스트리 + 유닛별 핸들러 클래스.
   - 이번엔 도끼병용 `SweepAttackBehavior` 1개만 구현, 나머지 4종은 뼈대만.
   - `UnitType` 키 매핑이라 인스펙터 배선 불필요. 신규 유닛 = 핸들러 추가 + 등록 1줄.

### 방향 기준 (사용자 주의사항 반영)

- 도끼병은 타겟에 따라 방향이 바뀐다. **AoE는 타겟을 향한 방향을 기준으로 판정한다.**
- `ExecuteAttack`이 데미지 직전 `attacker.Facing = FacingDirection.FromCoords(attacker.Position, target.Position)`로
  **타겟 방향으로 갱신**하므로, 그 방향(월드에서는 주 타깃 방향)을 기준으로 전방을 판정하면
  **타겟은 항상 전방에 포함**된다. 이동 중의 옛 방향이 아니라 타겟 방향이 기준.

> ⚠️ **설계 변경(2026-07-16 실기 후)**: 초기 타일 기준(전방 5타일+자기) 판정 D-1은 **월드 좌표 전방
> 부채꼴 판정(±120°, 반경 기본 1.0, Inspector 편집)** 으로 대체되었고, 타격 시점도 1.1667s로 보정됨.
> 상세는 **Plan.md "설계 변경 이력(2026-07-16)"** 참조.
