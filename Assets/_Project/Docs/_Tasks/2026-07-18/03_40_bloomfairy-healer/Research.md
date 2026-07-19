# Research — BloomFairy(꽃요정) 힐러 유닛 구현

## 이 작업이 무엇인지 (자연어 설명)

BloomFairy는 초월계의 **힐러**로, 지금까지의 특수 유닛 4종(적을 때리는 유닛)과 근본적으로 다릅니다.
**공격을 아예 하지 않고, 부상당한 아군을 자동으로 회복**시킵니다.

BloomFairy는 전장을 이동하다가 회복 사거리(4타일) 안에 **부상당한 아군**이 있으면 멈춰서 힐
애니메이션을 재생하고, 그 아군에게 **3초 동안 총 20 HP를 회복시키는 버프(HoT)** 를 한 번 걸어줍니다.
버프를 건 뒤에는 쿨다운(3초) 후 다음 대상을 다시 찾습니다. 부상 아군이 사거리에 없으면 다른
유닛들처럼 적 진영을 향해 이동합니다.

기존 전투 시스템은 "적을 감지해 공격하는" 구조뿐이라 아군을 대상으로 하는 로직이 전혀 없습니다.
따라서 이 작업은 **아군 대상 타겟팅 + 힐러 상태머신 + 시간 지속 효과(HoT) 시스템**을 새로 만드는
것이 핵심입니다. 힐의 실제 적용/연출/멀티 동기화는 TorrentSpirit에서 만든 힐 서브시스템을 재사용합니다.

---

## 대상 유닛 스펙 (StatsReference.md 기준)

| 항목 | 값 |
|------|----|
| UnitType | `BloomFairy` (enum 값 27, 초월계) |
| HP | 50 |
| 공격력 | **없음 (공격 불가)** |
| 회복(힐) 사거리 / 감지 사거리 | 4.0 / 4.0 |
| 이동 속도 | 1 |
| 쿨다운 | 1:00(3:00) — 힐 발동 타이밍 1.0s / 쿨다운 3.0s |
| 생산 / 골드 / 인구 | 20초 / 150 / 1 |
| 특수 | 아군 단일 지정 힐 — 3초간 총 20 HP 회복(HoT), 공격 불가 |

### 확정된 설계 결정 (사용자, 2026-07-18)

1. **자동 타겟팅**: 사거리 4.0 내 **부상 아군(HP < MaxHp)** 중 **잃은 체력 비율(%)이 가장 큰** 대상 우선.
2. **동률(같은 %)**: **거리가 가까운** 유닛 선택.
3. **본인 포함** (자가 회복 허용).
4. **아군 유닛만** (건물 힐 불가).
5. **미발견 시**: 사거리 내 부상 아군이 없으면 일반 유닛처럼 A*로 이동(적 진영 향해).
6. **힐 시전**: 부상 아군 발견 → 정지 → **힐 애니메이션**(공격 유닛처럼 시전 중 이동 없음) → **힐 타격
   타이밍(`HitFrameTimes[0]`=1.0s)** 에 대상에 **HoT 버프 부여** → 쿨다운 3s → 재탐색. (구현 확정 2026-07-18:
   회복 발동은 애니 이벤트 `OnAttackHit`이 아니라 `HitFrameTimes` 타이머로 구동 — 기존 데미지와 동일 관례,
   `OnAttackHit`은 연출 전용. Plan.md (c) 참조.)
7. **HoT 버프**: 대상에 붙어 **3초간 총 20 HP** 회복(서버 권위 틱). **1회 부여**이며 부여 후 요정과
   독립적 — 대상이 사거리를 벗어나도 진행.
8. **재적용 = 갱신(중첩 없음)**: 버프가 남은 대상에 또 힐 들어오면 3초·20 리셋.
9. **재탐색**: 다음 힐 대상 선택 시 대상이 풀피/사거리 이탈이면 다른 부상 아군.
10. 모든 회복 **서버 권위**.

---

## 현재 코드 구조 (파악 결과)

### 1. 전투 상태머신 = `UnitView.MoveAlongPathV3` (Presentation, 코루틴)
- A* 이동 코루틴(`UnitView.cs:859`)이 매 스텝 `_combatUseCase.HasEnemyInDetectRange(_unitData)`(1002)로
  전투 진입을 결정 → `EnterCombatPursuitV3`(1274) → `EnterCombatLoopV3`(1413).
- **전부 "적 감지"에 기반**한다. 아군을 감지/추종하는 경로가 없다.
- **BloomFairy 관련**: 이 코루틴에 **힐러 분기**를 추가해야 한다 — 적 감지 대신 "부상 아군 감지 →
  정지 → 힐 애니 → HoT 부여 → 쿨다운 → 재탐색"의 힐 루프(공격 루프와 병렬).

### 2. 타겟 탐색 = `UnitCombatUseCase` (적 전용)
- `FindFirstEnemyInDetectRange`/`FindNearestEnemyInDetectRange`/`HasEnemyInDetectRange` 등은 모두
  `unit.Team == attacker.Team || !unit.IsAlive → continue`로 **적만** 찾는다.
- **BloomFairy 관련**: 이 팀 필터를 반대로 한 **부상 아군 탐색** 메서드를 신설해야 한다.
  (아군 + `Hp < MaxHp` + 잃은 % 최대 + 동률 시 거리 최소, 본인 포함, 아군 유닛만.)

### 3. 힐 서브시스템 = 재사용 (TorrentSpirit에서 구축, 규칙 30)
- `UnitData.Heal(int amount)`(MaxHp 클램프, 죽은 유닛 무동작), `GameEvents.OnEntityHealed`/
  `EntityHealedEvent`, `NetworkHealthSync` 힐(HP 증가) 동기화(`SyncHealClientRpc` + 클라 재발행),
  `FloatingHpTextSpawner` 치유 색상 텍스트 — **힐 적용·연출·멀티 동기화는 이미 완성**.
- **BloomFairy 관련**: HoT의 각 틱마다 `UnitData.Heal` + 힐 이벤트로 이 인프라를 그대로 사용.

### 4. 시간 지속 효과(HoT/DoT)는 없음 ← 핵심 신규 작업 (공용)
- 현재 "대상에 시간 지속 효과"를 관리하는 시스템이 없다. TorrentSpirit 힐은 즉발이었다.
- **BloomFairy HoT(회복)** 뿐 아니라 **잔여 특수 유닛의 DoT(피해)** 도 동일 시스템이 필요:
  - InfernoSpirit: DoT 5/초 × 3초, MushroomBomber: DoT 2/초 × 3초.
- → **"유닛에 붙는 시간 지속 효과(HoT=회복 / DoT=피해)" 공용 시스템**을 만들면 3유닛이 재사용한다.
  서버 권위 틱(TickWaves처럼 싱글=GameBootstrapper·멀티=NetworkCombatController에서 tick), 각 틱마다
  누적량을 회복/피해로 적용(기존 힐/피해 이벤트로 동기화).

### 5. 서버 틱 진입점 (기존 패턴 재사용)
- 파도(`TickWaves`)가 이미 싱글=`GameBootstrapper.Update`(`!IsNetworkMode` 가드)·멀티=
  `NetworkCombatController`(IsServer)에서 호출된다. HoT/DoT 틱도 동일 지점에 추가.

### 6. special-only / 특수 공격 아키텍처와의 관계
- BloomFairy는 **적 주 타깃이 없어 `ExecuteAttack` + `ISpecialAttackBehavior`(적 공격 흐름)에 얹을 수
  없다.** TorrentSpirit(special-only이지만 적을 때림)과도 다르다.
- → BloomFairy는 **힐러 전용 경로**(아군 타겟팅 + 힐러 상태머신 + 힐 액션)가 필요하다. 특수 공격
  레지스트리에 얹기보다, 상태머신이 "이 유닛은 힐러"임을 알고 힐 루프를 타게 하는 구조가 자연스럽다.
  (구체 방식은 Plan에서 결정.)

---

## 영향 범위 (예상)

| 파일/영역 | 예상 변경 | 구분 |
|-----------|-----------|------|
| HoT/DoT 지속 효과 시스템 | 서버 권위 틱 + 대상별 효과 레코드(회복/피해, 갱신 규칙) | 신규(공용) |
| `Application/UseCases/UnitCombatUseCase.cs` | 부상 아군 탐색 메서드(팀 필터 반대 + HP/% + 거리), 힐 액션(HoT 부여) | 수정 |
| `Presentation/Unit/UnitView.cs` | 힐러 상태머신 분기(부상 아군 감지 → 정지 → 힐 애니 → 재개) | 수정 |
| `Bootstrap/GameBootstrapper` / `NetworkCombatController` | HoT/DoT 틱 호출 추가(파도와 동일 지점) | 수정 |
| `Domain/Unit/` | (검토) 힐러 여부/역할 식별 수단 | 검토 |
| `UnitStatsConfig`(asset) | BloomFairy(27) 스탯 입력 | 에셋 |
| `UnitEffectConfig` / 힐 애니 클립 | 힐 이펙트(있으면)·클립 `OnAttackHit`(힐 타이밍) 주입 | 에셋 |
| UnitFactory 씬 등록 / 생산 매핑 | type 27 확인 | 에셋/씬 |

힐 적용/연출/동기화(`UnitData.Heal`·`OnEntityHealed`·`NetworkHealthSync`·`FloatingHpTextSpawner`)는 **무변경 재사용**.

---

## 현재 상태 (구현 전제)
- `UnitType.BloomFairy` = 27 등록됨. 프리팹 `Unit_BloomFairy_Blue/Red` 존재.
- **UnitStatsConfig에 27 미입력** → 폴백값(공격력 1 등) 사용 중.
- UnitFactory 씬 등록·생산 매핑(초월계 식물 라인 등) 확인 필요.
- 힐 애니 클립 `OnAttackHit`(=힐 발동) 미주입.

---

## 핵심 난이도 / 확정 항목 (Plan에서 결정)
1. **힐러 식별 방식**: 상태머신이 BloomFairy를 힐러로 인식하는 수단(UnitType 조건 vs "역할/지원" 추상화).
2. **HoT/DoT 공용 시스템 형태**: 대상별 효과 레코드 구조, 서버 틱 배치(레이어), 틱당 적용 방식(누적/분할),
   갱신 규칙(중첩 없이 리셋), 대상 사망/풀피 시 처리.
3. **부상 아군 탐색**: 잃은 % 최대 + 동률 거리 tiebreak. 아군 유닛만(건물 X), 본인 포함.
4. **힐러 상태머신**: 공격 루프와 병렬 구조. 힐 발동은 클립 `OnAttackHit` 기준(공격 유닛의 타격 타이밍 재해석).
5. special-only이지만 **적 공격 흐름과 분리** — 힐러 전용 경로 배치.

---

## 완료 결과 / QA 반영 (2026-07-18)

구현·QA 완료. 확정된 사실을 문서에 반영한다(히스토리 보존을 위해 본문은 그대로 두고 하단에 append).

### 힐 주기 = 4.0초 (확정 설계, 버그 아님)

BloomFairy의 힐 1회 주기는 **총 4.0초**다 = **힐 발동 준비 1.0초(`HitFrameTimes[0]`) + 발동 후 쿨다운 3.0초**.

⚠️ **쿨다운 예외**: 이 프로젝트의 다른 모든 유닛은 `AttackCooldown`이 "발동 준비를 포함한 전체 주기"다(공격 시작 시점에 쿨다운 시작, `TryAttack` 패턴). 그러나 **BloomFairy만 `AttackCooldown`(3.0s)이 힐 발동 준비(1.0s)를 포함하지 않아** 실제 주기가 4.0s가 된다. 이는 "힐을 건 뒤 3초를 쉰다"는 힐러 체감을 위한 **의도된 설계**이며 버그가 아니다. 다른 유닛과 동일하게(발동 준비 포함) 되돌리지 말 것. → GameSystemRules_Units 규칙 36에 명문화.

### QA 이슈 처리 결과

| 이슈 | 내용 | 처리 |
|------|------|------|
| 이슈1 | 힐 주기가 4초로, 다른 유닛(전체 주기=쿨다운)과 달라 버그로 오인될 소지 | **설계 확정 — 코드 무변경, 문서만 반영**(규칙 36 신설, StatsReference 행 명시) |
| 이슈2 | 힐러가 경로 끝(최전선) 도달 후 부상 아군을 감시하지 않고 유휴 상태 | `HealerIdleWatchV3` 유휴 감시 루프 추가로 수정 완료(규칙 35) |
| 이슈3 | 힐 캐스트(시전) 도중 대상이 풀피가 되어도 계속 시전 | `CastHeal`에 `Hp < MaxHp` 가드 추가로 수정 완료 |

### 추가 QA 반영 (2026-07-19) — HoT 힐 텍스트 표시 방식 변경

HoT 회복 중 힐 플로팅 텍스트가 **틱마다 자잘하게 여러 번** 뜨던 것을, **효과가 끝날 때 회복 후 현재 HP(절대값)로 1회만** 뜨도록 변경(기존 즉발/파도 힐과 표시 형식 통일, 실기 확정). **HP 회복 자체는 종전 그대로 틱마다 서서히** 오르며(HP바 상승·멀티 동기화 유지) — 바뀐 것은 힐 텍스트 표시 시점과 표시 형식뿐이다.

- **틱 힐 텍스트 억제**: HoT 각 회복 틱은 힐 텍스트를 띄우지 않는다(HP바 갱신·멀티 동기화용 힐 이벤트는 종전대로 발행 — 텍스트만 skip).
- **완료 시 1회 표시**: HoT가 정상 종료(대상 풀피 도달 또는 지속시간 만료)될 때 회복 후 현재 HP(절대값)로 힐 텍스트 1회 표시(기존 즉발/파도 힐과 동일 형식).
- **사망 시 생략**: 회복 도중 대상 유닛 사망 시 힐 텍스트 미표시.
- **HoT 경로 한정**: TorrentSpirit 파도 즉발 힐·기타 즉발 힐·모든 데미지 텍스트는 무변경.
- 구현: `EntityHealedEvent`에 `ShowText` 플래그 추가(HoT 틱 `ShowText=false`, 종료 시 `ShowText=true`로 1회 — 표시값은 기존 즉발/파도 힐과 동일한 현재 HP, 표시 전용 `HealAmount` 플래그는 두지 않음), `NetworkHealthSync`가 `SyncHealClientRpc`로 `ShowText` 전파(멀티도 완료 시 1회), `ActiveTimedEffect.ActualHealed`에 실제 회복량 누적(완료 텍스트 표시 여부 `>0` 판정, 재부여 시 리셋).
- 규칙 문서 반영: `GameSystemRules_Units.md` **규칙 37 신설**(규칙 30 연출 항목·규칙 34 종료 처리 항목에 상호참조 추가).
