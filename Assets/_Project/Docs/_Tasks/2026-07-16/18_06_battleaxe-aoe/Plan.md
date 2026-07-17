# Plan — 도끼병(BattleAxe) 휩쓸기형 AoE 구현

## 이 계획이 무엇인지 (자연어 설명)

도끼병이 도끼를 휘두를 때 **자기 주변 6타일 중 등 뒤 1타일을 뺀 전방 5타일**에 있는
모든 적 유닛을 한꺼번에 같은 피해로 베는 기능을 만듭니다. 지금 전투 코드는 "한 번에 한 마리"만
때리므로, 실제 피해를 적용하는 딱 한 지점(`ExecuteAttack`)에 "도끼병이면 주변 적들도 함께 벤다"는
분기를 추가합니다. 이 지점은 싱글/멀티 공통 경로라 한 번만 고치면 두 모드에 모두 적용됩니다.

이번 단계는 특수 유닛 5종 중 첫 번째(도끼병)만 구현하며, 나머지 4종은 이후에 같은 구조 위에서
확장할 수 있도록 최소한의 뼈대만 잡습니다.

---

## ⚠️ 기존 로직 제거 여부 (규칙: WORKFLOW [4] 기존 로직 제거 규칙)

**이 작업은 기존 로직을 제거하지 않는다.** 기존 단일 타깃 피해 경로(`target.TakeDamage` +
`OnEntityDamaged` + 사망 처리)는 그대로 유지하고, 도끼병일 때만 **추가 대상들에 대해 동일 절차를
반복**하는 방식이다. 일반 유닛의 동작은 변하지 않는다.

---

## 근거 규칙 (GameSystemRules)

| 규칙 | 내용 | 이 작업에서의 적용 |
|------|------|-------------------|
| GameSystemRules_Units 규칙 16 (범위 공격 AoE) | "범위 공격은 대미지 계산 방식의 차이이며, 이동/상태 전환 규칙은 동일. 아군에게는 대미지를 주지 않는다." | 이동·상태 머신은 손대지 않고 **피해 계산만** 확장. 아군 제외. |
| StatsReference — 휩쓸기형 | "범위 내 모든 유닛에게 동일 피해 (겹침 무관)" | 전방 5타일 모든 적 유닛에 공격력 15 동일 적용. |
| GameSystemRules_Units 규칙 18 (서버 데미지 타이밍) | "데미지는 서버 타이머로만 적용, Animator 상태에 종속 금지" | AoE도 기존 `ExecuteAttack` 타이밍 경로 안에서 적용 — 별도 타이밍 로직 신설 안 함. |
| GameSystemRules_Units 규칙 19 (피격 표현 큐) | 피격 연출은 `OnEntityDamaged` 이벤트 기반 | AoE 각 대상마다 `OnEntityDamaged`를 발행해 HP 동기화·피격 연출을 정상 작동시킴. |

---

## 설계 결정 (사용자 승인 완료 2026-07-16)

### D-1. 판정 범위 = 전방 5타일 + 도끼병 자기 타일 (자기 타일 겹친 적 **포함**)
- 도끼병 `Facing.Opposite()`(등 뒤) 방향 이웃 1개를 뺀 5개 이웃 타일 **+ 도끼병 자기 타일**.
- 이유: 겹침 허용 구조상 근접 교전은 같은 타일에서 자주 발생하며, 바로 붙은 적이 안 맞으면 어색.

### D-2. 건물 공격 시에도 전방 적 유닛에 AoE 적용
- 주 타깃이 건물이든 유닛이든, 전방 5타일(+자기 타일)의 **적 유닛**에는 AoE 피해 적용.
- 건물 자체는 AoE 대상이 아니며 주 타깃일 때만 단일 피해.

### D-3. 주 타깃 중복 피해 방지 (필수)
- 주 타깃은 기존 단일 경로로 1회만 피해. AoE 수집 시 **주 타깃 Id를 제외**해 2회 피해 방지.

### D-4. 특수 공격 구조 = 방식 C (전략 핸들러 분리)
- 각 특수 공격을 독립 핸들러 클래스로 만들고, `UnitType` 키 레지스트리로 매핑.
- 이유: 특수 유닛 5종의 동작(휩쓸기/착탄/파도/DoT/힐)이 근본적으로 달라 각기 고유 코드가 필요.
  거대한 `switch`가 전투 핵심 메서드에 쌓이는 것을 막고, 신규 유닛 = 핸들러 추가 + 등록 1줄로
  `ExecuteAttack`을 다시 건드리지 않는다. 핸들러가 독립적이라 단위 테스트·디버깅이 쉽다.
- `UnitType` 키 매핑이므로 인스펙터 배선 불필요(데이터화 방식의 배선 비용 회피).
- 이번엔 도끼병용 핸들러 1개만 구현, 나머지 4종은 구조상 뼈대만 남긴다(규칙 6 범위 준수).

### 방향 기준 (사용자 주의사항)
- 도끼병은 타겟에 따라 방향이 바뀌므로, **AoE는 타겟을 향한 방향을 기준으로 5타일을 정한다.**
- `ExecuteAttack`이 데미지 직전 `Facing`을 타겟 방향으로 갱신하므로, 그 이후 `Facing.Opposite()`를
  뺀 5타일을 계산하면 **타겟은 항상 전방 5타일에 포함**된다. 이동 중 옛 방향이 기준이 되면 안 됨.

---

## 구현 상세 (방식 C — 전략 핸들러 구조)

> 레이어 배치·정확한 파일 경로·인터페이스 시그니처는 아키텍처 제약(`.claude/MEMORY.md`)에 맞춰
> game-programmer가 확정한다. 아래는 구조와 책임의 명세.

### 1. 재사용 피해 헬퍼 추출 — `UnitCombatUseCase`
현재 `ExecuteAttack` 안에 인라인된 "피해 1회 적용 + 이벤트 발행 + 사망 처리"를
**단일 대상용 재사용 헬퍼**로 뽑아낸다(동작 무변경, 순수 리팩터).
- 예: `ApplyDamageToVictim(UnitData attacker, IDamageable victim)` —
  `victim.TakeDamage(attacker.AttackPower)` → `OnEntityAttacked`/`OnEntityDamaged` 발행
  (attackerId=attacker.Id, attackerIsUnit=true) → 사망 시 `OnUnitDied`/`OnBuildingDied` 발행 +
  싱글플레이 전투 상태 정리.
- 주 타깃 경로와 AoE 경로가 **같은 헬퍼**를 쓰게 하여 멀티플레이 HP 동기화 일관성을 보장.

### 2. 특수 공격 계약 — `ISpecialAttackBehavior` (신규)
```
interface ISpecialAttackBehavior {
    void Apply(SpecialAttackContext ctx);
}
```
- 특수 공격 1종 = 이 인터페이스를 구현한 클래스 1개.

### 3. 컨텍스트 — `SpecialAttackContext` (신규)
핸들러가 동작에 필요한 것을 담아 전달:
- 공격자(`UnitData attacker`), 주 타깃(`IDamageable primaryTarget`),
- 전체 유닛 조회 수단(`_unitSpawn.Units` 접근), 재사용 피해 헬퍼(1의 델리게이트/인터페이스),
- (향후 힐/DoT용 확장 여지 — 이번엔 피해만 사용).

### 4. 레지스트리 (신규)
`UnitType → ISpecialAttackBehavior` 매핑. 초기화 시 도끼병만 등록:
- `BattleAxe → SweepAttackBehavior`
- 미등록 유닛은 특수 공격 없음(일반 단일 타깃).

### 5. `ExecuteAttack` 연결 (수정 — 1줄)
기존 단일 타깃 피해(헬퍼 호출) **직후**:
```
_specialAttacks.TryGet(attacker.Type)?.Apply(context);
```
- 이후 신규 특수 유닛이 늘어도 `ExecuteAttack`은 다시 수정하지 않는다.

### 6. `SweepAttackBehavior` (신규 — 이번 핵심)
도끼병 휩쓸기 로직:
1. 뒤 방향 = `attacker.Facing.Opposite()` (Facing은 `ExecuteAttack`이 타겟 방향으로 갱신한 값).
2. 대상 타일 집합 = 6개 이웃 중 뒤 방향 이웃 제외 5개 **+ `attacker.Position`(자기 타일, D-1)**.
3. `Units` 순회 → 조건에 맞는 적 유닛을 **먼저 리스트로 수집**(순회 중 사망 제거로 인한 컬렉션 변경 회피):
   - `unit.Team != attacker.Team` (아군 제외 — 규칙 16)
   - `unit.IsAlive`
   - `unit.Position`이 대상 타일 집합에 포함
   - `unit.Id != primaryTarget?.Id` (주 타깃 중복 제거 — D-3)
4. 수집된 각 유닛에 **재사용 피해 헬퍼(1)** 적용.
- (선택) "전방 5타일+자기" 좌표 계산은 `Domain` 순수 함수로 분리하면 테스트/재사용에 유리.

### 7. 데미지 타이밍 / 클립 이벤트
- 도끼병은 단일 히트(휩쓸기 1회) → `HitFrameTimes` 원소 1개.
- Attack 클립 `OnAttackHit` 이벤트 주입(ROADMAP F-4)은 **에셋 작업**으로 이번 코드 범위와 분리.
  Config 폴백 타격 시간으로 코드 동작 검증 가능.

### 향후 확장 (이번 범위 아님, 뼈대만)
- QuakeSpirit/TorrentSpirit/MushroomBomber/BloomFairy는 각각 `ISpecialAttackBehavior` 구현체를
  추가하고 레지스트리에 등록하면 된다. 힐/DoT는 `SpecialAttackContext`에 해당 수단을 확장해 지원.

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| AoE 다중 `OnEntityDamaged` 발행 시 멀티플레이 HP 동기화(NetworkHealthSync) 정상 처리 여부 | 각 대상 개별 이벤트 발행이 단일 타깃과 동일한 데이터 형식인지 확인. 멀티 실기 검증 필요(사용자). |
| 순회 중 사망 유닛 Dictionary 제거로 인한 컬렉션 변경 예외 | 대상 선수집 후 별도 루프 적용(위 1-b 4). |
| 주 타깃 2회 피해 | D-3 중복 제거(Id 비교). |
| `Facing`이 공격 순간 타겟 방향과 다를 가능성 | `ExecuteAttack`이 데미지 직전 `Facing`을 타겟 방향으로 갱신하므로 안전. AoE는 그 갱신 이후 계산. |
| 도끼병 프리팹/팩토리 미등록으로 실기 스폰 불가 | ROADMAP D-4 남은 작업(UnitFactory 등록/스탯 입력)과 의존. 코드 로직은 선행 구현 가능하나 실기 테스트 시 필요. |

---

## 검증 방법 (구현 후)

- 정적: 컴파일 통과, 일반 유닛 단일 타깃 동작 무변경 확인.
- 실기(사용자): 도끼병 전방 여러 타일에 적 배치 → 1회 공격에 전방 5타일(+자기 타일) 적 전원
  15 피해, 뒤 타일 적은 무피해, 아군 무피해 확인. 멀티플레이 HP 동기화 확인.
- TC/QA는 사용자 명시 지시가 있을 때만 진행(WORKFLOW [5-1~5-3]).

---

## 위임 계획

- 코드 구현: **game-programmer** 에이전트 (규칙 3). `.claude/MEMORY.md` 컨텍스트 전달.
- 밸런스/설계 확정이 추가로 필요하면: game-design-lead.

---

## 설계 변경 이력 (2026-07-16, 실기 피드백 반영)

초기 구현(타일 기준 판정 + 폴백 타격 시점)을 실기한 결과 두 가지 문제가 확인되어 아래와 같이 변경한다.

### 변경 1. 타격 시점 보정 (데미지-애니메이션 불일치)
- **문제**: BattleAxe_Attack 클립에 `OnAttackHit` 이벤트가 없어(특수 유닛 5종 공통) 데미지 시점이
  Config 폴백값(계획값 1.02s)으로 동작 → 실제 도끼 내리치는 프레임과 어긋남. 또한 피격 연출(규칙 19
  표현 큐)이 클립 이벤트를 못 받아 타임아웃(쿨다운×1.5) 후 늦게 방출됨.
- **변경**: 타격모션 구간(Unity Animation `초:프레임`, 30fps: `0:28`=28f=0.933s ~ `1:05`=35f=1.1667s)의
  **종료 시점 1.1667s**로 `hitFrameTimes` 보정(UnitStatsConfig, 커밋 `c03409a`).
  Unity에서 `Hexiege/Combat/Inject OnAttackHit Events (From Config)` 실행 시 클립 이벤트로 주입 →
  데미지·피격 연출 모두 클립 이벤트에 정렬.

### 변경 2. 판정 방식: 타일 기준 → 월드 좌표 전방 부채꼴 (D-1 대체)
- **문제**: 타일 소속(`unit.Position`) 기준 판정은 유닛이 타일 사이를 이어 움직이고 겹치며, 도끼 스윙이
  연속 반경/호를 그리는 것과 시각적으로 어긋남.
- **변경 (사용자 승인)**: **월드 좌표 기반 전방 부채꼴 판정**으로 교체. 기존 "전방 5타일 + 자기 타일
  (등 뒤 제외)"(D-1) 정의를 대체한다.
  - **기준 방향(forward)** = 도끼병 → 주 타깃 방향(월드). `ExecuteAttack`이 공격 순간 타겟을 향하게 하므로
    주 타깃 월드 좌표로 forward를 구한다.
  - **판정**: 각 적에 대해 도끼병으로부터의 **XZ 평면 거리 ≤ reach** 이고, forward와 이루는
    **각도 ≤ 부채꼴 반각(±120°)** 이면 피격. (Y는 UnitYOffset 때문에 무시 — XZ 거리 사용.)
  - **월드 좌표**는 `IEntityPositionProvider`(서버 권위)로 조회 — 기존 전투 사거리 판정(규칙 6)과 일관.
  - 겹쳐 붙은 적(거리≈0)은 자연 포함(자기 타일 겹침 취지 유지), 등 뒤는 각도로 자연 제외.
  - **아군/사망/공격자/주 타깃 제외(D-2·D-3)** 규칙은 그대로 유지.

### 변경 3. 튜닝 파라미터 Inspector 노출 — `SpecialAttackConfig`(신규 SO)
- `SweepAttackBehavior`는 순수 C#(Application)이라 인스펙터 편집 불가 → `UnitStatsConfig`와 동일 패턴의
  **신규 ScriptableObject `SpecialAttackConfig`(Infrastructure/Config)** 를 만들어 값을 Inspector에서 편집.
  - `sweepReach`(월드 반경, **기본값 1.0**) — 이 맵 인접 타일 중심 간 거리 ≈ 0.9~1.0 기준.
  - `sweepArcHalfAngle`(부채꼴 반각, 단위 도, **기본값 120**).
- GameBootstrapper가 시작 시 이 SO 값을 읽어 특수 공격 레지스트리/핸들러에 **float 값으로 주입**
  (Application이 Infrastructure SO를 직접 참조하지 않음 — MEMORY 레이어 규칙 준수).
- 향후 QuakeSpirit 반경 등 다른 특수 유닛 파라미터의 공용 자리로 확장 가능.

### 컨텍스트 확장
- `SpecialAttackContext`에 **월드 좌표 조회 수단**(`IEntityPositionProvider` 또는 `int id → Vector3` 델리게이트)과
  **reach/arc 값**을 추가 전달. `SweepAttackBehavior`가 이를 사용해 부채꼴 판정 수행.
- 판정 로직(전방 부채꼴 유닛 수집)은 순수 계산이므로 Domain 순수 함수로 분리 가능(선택, 테스트 용이).
  단, `Vector3` 사용 시 Domain의 UnityEngine 참조 금지 제약 확인 필요 — 제약에 걸리면 Application에 둔다.
