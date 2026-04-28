# Research: Phase 1 전투 후 Phase 2 후방 스냅 오작동 수정

**날짜**: 2026-04-27  
**작업 ID**: 01_17_phase2-backward-snap-fix  
**담당**: game-programmer 에이전트  
**연관 작업**:
- [15_00_phase1-target-reselect](../../2026-04-26/15_00_phase1-target-reselect/Plan.md) — Phase 1 타겟 재선택 + Phase 2 후방 스냅 방지 (5차 개선)
- [18_30_melee-spread](../../2026-04-26/18_30_melee-spread/Plan.md) — 근접 유닛 18슬롯 분산 + 슬롯 도달 후 전투 거리 직진

---

## 이 작업이 하려는 것 (자연어 설명)

근접 유닛 분산 작업(18_30) 이후 다음 두 가지 이상 현상이 발생했다.

1. **앞뒤 왔다갔다 현상**: 일부 불정령이 피스톨러에게 접근하다가 갑자기 뒤로 가고, 다시 접근했다가 또 뒤로 가는 동작을 반복한다.
2. **타겟 사망 후 뒷무빙**: 피스톨러가 죽으면 근처의 다른 피스톨러에게 이어서 접근하는 것이 아니라, 이전에 있던 타일로 되돌아간다.

이 두 현상은 18_30 작업의 핵심 변경("슬롯 도달 후 전투 거리까지 직진")이, 이전에 완성된 15_00 작업의 "Phase 2 후방 스냅 방지 로직"과 충돌하면서 발생한다.

15_00의 후방 스냅 방지는 불정령이 Phase 1에서 슬롯 위치(적으로부터 0.866f)에서 멈추던 당시를 전제로 설계됐다. 18_30 이후에는 불정령이 슬롯을 지나 적 바로 앞(0.3f)까지 전진하는데, 이 경우 후방 스냅 방지 로직이 오히려 1~2칸 뒤로 강제 스냅하는 역효과를 낸다.

---

## 1. 문제 정의

### 1-1. 증상

- **버그 A (앞뒤 왕복)**: 일부 불정령이 피스톨러에게 접근하다가 갑자기 뒤로 이동 → 다시 접근 → 다시 뒤로 이동 동작을 반복. 일부만 발생하고 다른 불정령은 정상 동작.
- **버그 B (타겟 사망 뒷무빙)**: 피스톨러가 사망했을 때 근처에 다른 피스톨러가 없으면, 이전에 있던 타일 방향으로 뒤로 이동하는 현상. 15_00 이전의 증상과 유사하지만 더 큰 폭으로 발생.

### 1-2. 테스트 환경

- 피스톨러(원거리, 제자리 공격)와 1단계 불정령(근접, 접근 공격) 교전
- 피스톨러는 이동하지 않고 제자리에서 공격
- 불정령은 순차적으로 피스톨러에게 접근

---

## 2. 근본 원인 분석

### 2-1. 핵심 변수 관계

| 변수 | 의미 | Phase 1 동안의 변화 |
|------|------|---------------------|
| `finalTarget` | A* 경로의 마지막 타일 = **성(Castle) 타일** | 변하지 않음 (최초 1회 설정) |
| `_unitData.Position` | 도메인 위치 (타일 좌표) | Phase 1 진입 시 마지막으로 ProcessStep된 타일 T0. **Phase 1 동안 절대 갱신 안 됨** |
| `nearestTile` (Phase 2) | 불정령 현재 뷰 위치 → WorldToHex 변환 결과 | Phase 1이 끝난 시점의 실제 물리 위치 기반 |

### 2-2. 15_00의 5차 개선 (Phase 2 후방 스냅 방지)

[UnitView.cs:1330-1336](../../../../../Scripts/Presentation/Unit/UnitView.cs#L1330-L1336)

```
if (HexCoord.Distance(nearestTile, finalTarget) > HexCoord.Distance(_unitData.Position, finalTarget))
{
    nearestTile = _unitData.Position;  // nearestTile이 성에서 더 멀면 T0으로 교체
}
```

**설계 전제 (15_00 작성 당시)**: Phase 1에서 불정령이 슬롯 위치(0.866f)에서 멈춘다. 따라서 `nearestTile ≈ T0 인접 타일`이고, `_unitData.Position = T0`과 거의 같은 위치다. 이 조건이 발동해도 교체 폭이 미미하다.

### 2-3. 18_30의 슬롯 도달 수정

[UnitView.cs:1264-1274](../../../../../Scripts/Presentation/Unit/UnitView.cs#L1264-L1274)

슬롯 위치(0.75~0.866f)에서 0.15f 이내에 도달하면 `_currentAttackPos = zero`로 초기화 → 이후 `enemyViewPos` 직접 추적 → 전투 거리(0.3f)까지 전진.

**변화된 현실**: Phase 1이 끝나는 시점에 불정령은 **적 바로 앞(0.3f)**에 있다. `nearestTile`은 **적의 타일** 또는 그 인접 타일이 된다.

### 2-4. 충돌 지점: 5차 개선의 역효과

`finalTarget = 성 타일`이고, 피스톨러가 성 방향 기준으로 측면이나 후방에 위치하는 경우:

```
예시:
  T0(Phase 1 시작 타일) → 성까지 5홉
  피스톨러 P1 타일      → 성까지 6홉  (P1이 성 방향과 어긋난 위치)

Phase 1 종료 시점:
  nearestTile = P1 타일  → 성까지 6홉
  _unitData.Position = T0 → 성까지 5홉

5차 개선 조건: 6 > 5 → TRUE → nearestTile = T0으로 강제 교체

결과: 불정령이 P1 바로 앞에 있었는데 T0(1~2칸 뒤)으로 스냅됨
```

이것이 **버그 B(타겟 사망 뒷무빙)**의 직접 원인이다.

### 2-5. 진동 반복 루프 (버그 A의 원인)

Phase 2에서 T0으로 스냅된 뒤 Phase 0이 재시작된다:

```
① Phase 2: 불정령이 T0으로 후방 스냅
② Phase 0: T0 → 성 방향 Lerp 시작
③ Lerp 첫 프레임: HasEnemyInDetectRange 체크
   → 아직 살아있는 다른 피스톨러 P2가 T0에서 0.866f 이내 (탐지 사거리 0.916f 이내)
   → interruptedByDetect = true → Phase 1 재진입
④ Phase 1: 불정령이 T0 → P2 슬롯 → P2 앞까지 접근 → 전투 → P2 사망
⑤ Phase 2: P2 타일도 성 기준 T0보다 멀면 → 5차 개선 발동 → T0으로 다시 스냅
⑥ ①로 돌아감 → 반복
```

**반복 조건**: T0 주변의 피스톨러들이 모두 성 방향 기준으로 T0보다 더 멀리 있을 때 루프가 끊이지 않는다. 이 조건을 만족하는 피스톨러가 모두 제거될 때까지 진동이 반복된다.

---

## 3. 코드 위치

| 항목 | 파일 | 라인 |
|------|------|------|
| Phase 1 슬롯 도달 처리 | `UnitView.cs` | 1264-1274 |
| Phase 1 이동 목표 결정 (`moveTarget`) | `UnitView.cs` | 1272-1274 |
| Phase 1 타겟 사망 재선택 | `UnitView.cs` | 1075-1116 |
| Phase 1 전투 종료 후 재선택 | `UnitView.cs` | 1204-1225 |
| **Phase 2 5차 개선 (문제 지점)** | `UnitView.cs` | **1330-1336** |
| `finalTarget` 설정 | `UnitView.cs` | 591 |
| `_unitData.Position` 갱신 (ProcessStep) | `UnitView.cs` | 960, 1398 |
| `ReleaseAttackSlotIfClaimed` | `UnitView.cs` | 522-531 |
| `HasEnemyInDetectRange` 구현 | `UnitCombatUseCase.cs` | 461-533 |
| `FindNearestEnemyInDetectRange` | `UnitCombatUseCase.cs` | 401-407 |
| `FindNearestEnemyPositionInDetectRange` | `UnitCombatUseCase.cs` | 419-448 |

---

## 4. 충돌 발생 조건 요약

| 조건 | 버그 A (앞뒤 왕복) 발생 | 버그 B (타겟 사망 뒷무빙) 발생 |
|------|------------|------------|
| 피스톨러가 성 방향 기준 T0보다 먼 위치 | ✓ | ✓ |
| 불정령이 Phase 1에서 전투 거리까지 전진 | ✓ | ✓ |
| Phase 2 후 T0에서 탐지 범위 내 살아있는 다른 피스톨러 존재 | ✓ (루프) | — |

피스톨러가 직접 불정령의 진행 방향(성 방향) 위에 있다면 두 버그 모두 발생하지 않는다. 측면 또는 후방에 위치할 때만 발생한다.

---

## 5. 기존 작업 영향 관계

| 기존 작업 | 18_30 이전 상태 | 18_30 이후 상태 |
|-----------|----------------|----------------|
| **15_00 5차 개선** | Phase 1 종료 시 불정령이 슬롯(≈T0 인근)에 있음 → `nearestTile ≈ _unitData.Position` → 5차 개선 거의 발동 안 함 | Phase 1 종료 시 불정령이 적 앞(0.3f)에 있음 → `nearestTile = 적 타일` → 적이 측면/후방이면 **5차 개선 매번 발동, 1~2칸 후방 스냅** |
| **15_00 1·2차 개선** (타겟 재선택) | 변화 없음 — 타겟이 없으면 Phase 2로, 있으면 재선택. 정상 동작 | 변화 없음 — 정상 동작 유지 |
| **18_30 슬롯 분산** | — | 슬롯 도달 → 전투 거리 직진 정상 동작. 이 전진이 5차 개선과 충돌 |

---

## 6. 수정 방향 (개요)

**수정 대상**: Phase 2의 5차 개선 조건 (`UnitView.cs` 라인 1330-1336)

**핵심 아이디어**: 5차 개선을 "불정령이 전투를 한 뒤 Phase 2에 진입한 경우"에는 적용하지 않는다. 전투를 했다는 것은 불정령이 슬롯을 지나 전투 거리까지 전진했다는 의미이므로, 이때 `nearestTile`(적 타일 근방)은 올바른 스냅 대상이다.

구체적인 구현 방안과 수정 코드는 Plan.md에서 결정.

---

## 7. 영향 범위

| 파일 | 변경 예상 |
|------|----------|
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | **수정** — Phase 2 5차 개선 조건 변경 |
| `Assets/_Project/Docs/_Tasks/2026-04-26/18_30_melee-spread/Plan.md` | 충돌 사항 추가 기록 (선택) |
| `Assets/_Project/Docs/_Tasks/2026-04-26/15_00_phase1-target-reselect/Plan.md` | 충돌 사항 추가 기록 (선택) |

---

## 8. 미결 질문 → 해결됨 (2026-04-27)

1. **5차 개선 조건 방식**: 조건 자체를 제거하는 방식 채택. 플래그 추가보다 설계가 단순함.
2. **전투 없이 Phase 2 진입**: 5차 개선 없이도 A* 재계산으로 즉시 복구되어 시각적 영향 미미함.
3. **원래 증상 재발**: Step 1 적용 후 테스트에서 재발 없음. 단, 아래 9절에 기술된 잔존 현상 확인됨.

---

## 9. 잔존 원인 분석 — 헥스 대각선 뒤쪽 감지 (2026-04-28 추가)

Step 1(5차 개선 제거) 적용 후에도 피스톨러와 불정령이 밀집된 지역에서 앞뒤 왔다갔다 현상이 잔존한다.

### 9-1. 헥스 격자 감지 거리 수치

`TileWidth = TileHeight = 1.0f` (HexMetrics.cs), `MeleeDetectDist + Epsilon = 0.916f`.

PointyTop 기준 인접 타일 중심 간 실제 월드 거리:

| 방향 | 거리 계산 | 결과 | 감지 여부 |
|------|-----------|------|-----------|
| 좌우 축방향 (같은 행) | `TileWidth = 1.0f` | **1.000f** | ✗ 감지 안 됨 |
| 대각선 (다른 행) | `√(0.5² + 0.75²)` | **≈ 0.901f** | ✓ 감지됨 |

FlatTop도 같은 수치 (상하 축방향 1.0f, 대각선 ≈0.901f).

**핵심**: 6방향 인접 타일 중 **4개(대각선)는 감지되고 2개(축방향)는 감지되지 않는다.** 그리고 앞쪽 대각선과 뒤쪽 대각선이 **동일한 거리**(≈0.901f)다.

### 9-2. 잔존 버그 발생 시나리오

```
(스폰 방향)
    ...
  [P_behind]   ← 뒤쪽 대각선, 0.901f → 감지됨, 성까지 더 멂
  [Spirit T0]  ← 불정령 현재 위치
  [P_forward]  ← 앞쪽 대각선, 0.901f → 감지됨, 성까지 더 가까움
    ...
(성 방향)
```

1. 불정령이 A* Phase 0으로 이동 중 T0를 지나갈 때 P_behind를 **감지 못함**.
   - P_behind가 T0의 축방향(1.0f) 위치에 있었기 때문.
2. T0 도착 → Phase 0 스텝 완료 감지 체크 (`UnitView.cs:992`).
   - P_behind가 이제 T0의 **뒤쪽 대각선**(0.901f)에 위치 → 감지됨.
   - P_forward도 앞쪽 대각선(0.901f) → 감지됨.
3. `FindNearestEnemyInDetectRange`가 P_behind와 P_forward 중 하나를 선택.
   - 두 적이 **동거리(0.901f)**이므로 내부 반복 순서에 따라 P_behind가 먼저 선택될 수 있음.
4. Phase 1: 불정령이 **P_behind를 향해 뒤로 이동** → 전투 → Phase 2 스냅(뒤쪽 타일).
5. Phase 0: 뒤쪽 타일 → 성으로 A* → T0 방향 경유 → 앞으로 이동.
6. T0 근처에서 다시 P_forward 감지 → Phase 1 → 앞으로 이동.

플레이어 시점: **뒤로 → 앞으로 → 뒤로 → 앞으로** 반복.

밀집 지역에서 더 자주 발생하는 이유: 인접 모든 방향에 피스톨러가 있어 뒤쪽 대각선에도 항상 적이 존재.

### 9-3. 코드 위치 (추가)

| 항목 | 파일 | 라인 |
|------|------|------|
| Phase 0 Lerp 중 감지 체크 | `UnitView.cs` | 811-817 |
| Phase 0 스텝 완료 후 감지 체크 | `UnitView.cs` | 992-1004 |
| Phase 1 최초 타겟 선택 | `UnitView.cs` | 1042 |
| Phase 1 타겟 사망 재선택 | `UnitView.cs` | 1088-1112 |
| Phase 1 전투 종료 재선택 | `UnitView.cs` | 1204-1225 |

### 9-4. 수정 방향

Phase 0 감지 체크와 Phase 1 타겟 선택 시, **감지된 적이 성 기준으로 불정령 현재 위치보다 뒤에 있으면(farther from Castle) 무시**한다.

판단 조건:
```
뒤쪽 적 = HexCoord.Distance(적 타일, finalTarget) > HexCoord.Distance(불정령 현재 타일, finalTarget)
```

- Phase 0 감지 체크: 뒤쪽 적이면 `shouldPursue / interruptedByDetect` 설정하지 않음 → Phase 0 계속.
- Phase 1 최초 타겟: 뒤쪽 적이면 Phase 1 진입 건너뜀 → Phase 2로 넘어감.
- Phase 1 재선택: 뒤쪽 적이면 재선택하지 않고 break → Phase 2로 이탈.

구체적인 구현 코드는 Plan.md Step 2 참조.

---

## 10. 잔존 원인 분석 — 18슬롯 배정 후방 이동 (2026-04-28 추가)

Step 1, 2 적용 후에도 피스톨러와 불정령이 밀집된 상황에서 불정령이 앞뒤로 왕복하는 현상이 잔존한다.

### 10-1. 18슬롯 시스템 동작 방식

`AttackPositionManager.cs`의 `ClaimAttackSlot`은 공격 목표 타일의 인접 타일 중심(0.866f 반경) 및 경계 위치(0.75f 반경)를 최대 18개 슬롯 후보로 구성한다. 그 중 **점유가 가장 적고 불정령의 현재 위치(unitViewPos)에 가장 가까운 슬롯**을 배정한다.

Phase 1에서 불정령의 이동은 2단계다:
1. **배정된 슬롯 위치까지 이동** (0.15f 이내 도달 판정)
2. **슬롯 도달 후 공격 목표(enemyViewPos)로 직접 이동**

### 10-2. 후방 슬롯 배정 시나리오

```
상황: 불정령(S)이 피스톨러 A를 향해 A 방향으로 0.6f 이동한 상태

시작 타일 T_origin: S의 Phase 1 시작 위치
현재 위치 T_current: T_origin에서 A 방향으로 0.6f 전진

A 사망 → 새 타겟 피스톨러 B 선택

B의 인접 타일 후보들 중 T_origin이 포함됨.
(T_origin은 B의 walkable neighbor이므로 슬롯 후보에 항상 포함)

슬롯 선택 기준: S의 현재 위치(T_current)에서 가장 가까운 후보.
  T_origin까지 거리: ≈ 0.6f
  B 주변의 다른 후보들까지 거리: 1.0f ~ 1.5f 이상

→ T_origin이 가장 가까운 슬롯으로 배정됨.
→ T_origin은 S의 현재 위치에서 0.6f 뒤에 있음.
→ 0.6f > 슬롯 도달 임계값(0.15f) → S가 T_origin까지 후방 이동 후 B에 접근.
```

이것이 **Step 1, 2로 해결되지 않는 후방 이동 현상의 직접 원인**이다.

밀집 상황에서 더 심하게 발생하는 이유: 불정령이 밀집해 있으면 B의 인접 타일 대부분이 이미 다른 불정령에게 점유되어 있고, 결국 T_origin처럼 후방에 있는 슬롯이 선택될 확률이 높아진다.

### 10-3. 코드 위치

| 항목 | 파일 | 라인 |
|------|------|------|
| 슬롯 배정 로직 (`ClaimAttackSlot`) | `AttackPositionManager.cs` | 전체 |
| Phase 1 슬롯 도달 판정 (0.15f) | `UnitView.cs` | 1373-1378 |
| Phase 1 이동 목표 결정 (`slotAssigned ? _currentAttackPos : enemyViewPos`) | `UnitView.cs` | 1381-1383 |

### 10-4. 수정 방향

**18슬롯(헥스 타일 기반 위치) → 12방향 각도 기반 배분으로 교체.**

공격 목표를 중심으로 360°를 12등분(30° 간격)한 12개의 방향을 정의한다. 불정령은 그 중 하나의 방향을 배정받고, **이동 목표 = 타겟 위치 + (해당 방향 × 전투 거리)**가 된다.

```
이동 목표 = target_position + Quaternion.Euler(0, angle, 0) * Vector3.forward * contact_distance
```

슬롯 위치가 항상 공격 목표 근처에 있기 때문에, 불정령은 현재 위치와 무관하게 **항상 앞쪽(공격 목표 방향)으로만 이동**하게 된다. 슬롯 도달 후 2단계 이동도 불필요해진다.

구체적인 구현 방법은 Plan.md Step 3 참조.

---

## 11. 잔존 원인 분석 — Phase 2 nearestTile 후방 스냅 (2026-04-28 추가)

Step 1, 2, 3 적용 후에도 특정 상황에서 불정령이 Phase 2 진입 시 뒤로 이동하는 현상이 발생한다.

### 11-1. 발생 시나리오

Phase 1에서 불정령이 적을 향해 이동하던 중, 불정령이 아직 헥스 경계를 넘기 전에 적이 사망하면 Phase 2로 진입한다. 이 시점에서 불정령은 T0(Phase 1 시작 타일) 내부의 한 지점에 있다.

```
상황:
  불정령(S)의 Phase 1 시작 위치 = T0 중심 + 0.2f (T1 방향으로 이미 0.2f 전진)
  타겟(P) = T0 앞의 인접 타일 T1
  12방향 슬롯 = T1 중심 + dir × 0.3f ≈ T0 중심 + 0.7f (T0→T1 방향 기준)

  Phase 1: S가 슬롯 위치(T0 중심 + 0.7f)로 이동 시작
  → 타겟(P)이 S가 슬롯에 도달하기 전에 사망

  타겟 사망 시점의 S 위치: T0 중심 + 0.4f (아직 T0/T1 경계를 못 넘음)

  Phase 2 진입:
    nearestTile = WorldToHex(T0 중심 + 0.4f) = T0  (0.4f < 경계 0.5f)
    _unitData.Position = T0  (Phase 1 진입 이후 도메인 좌표 갱신 안 됨)
    → nearestTile == _unitData.Position == T0

  Lerp: S가 (T0 중심 + 0.4f) → T0 중심으로 이동 = 0.4f 뒤로 이동
```

### 11-2. 핵심 조건

`WorldToHex`는 현재 뷰 좌표에서 **가장 가까운 타일 중심**을 반환한다. 현재 위치가 T0 중심으로부터 절반 거리 미만에 있으면 T0를 반환한다.

| 방향 | T0/T1 경계 거리 | Phase 1에서 넘기 전에 타겟 사망 가능성 |
|------|----------------|---------------------------------------|
| 축방향 (1.0f) | T0 중심 + 0.5f | 불정령이 0.5f 미만 이동 시 T0 반환 |
| 대각선 (0.901f) | T0 중심 + 0.45f | 불정령이 0.45f 미만 이동 시 T0 반환 |

Phase 1 진입 후 타겟이 조기 사망하거나, 감지 범위를 벗어나면 이 조건이 성립한다.

### 11-3. Phase 2 Lerp 중 적 감지 부재 문제

Phase 2의 Lerp 루프는 현재 `_unitData.IsAlive`만 체크한다:

```csharp
while (snapElapsed < snapDuration && _unitData.IsAlive)
{
    snapElapsed += Time.deltaTime;
    transform.position = Vector3.Lerp(snapStart, tileCenter, t);
    yield return null;
    // ← 적 감지 없음. Lerp 중에 새 적이 감지 범위 안에 들어와도 무시됨.
}
```

이 때문에 Phase 2 Lerp 도중 새로운 적이 감지 범위에 들어와도 불정령이 해당 타일 중심까지 이동 완료 후 Phase 0을 재시작하고, Phase 0 첫 감지 체크에서야 비로소 Phase 1로 진입한다. 반응이 한 박자 늦어진다.

### 11-4. 수정 방향

두 가지를 수정한다.

**수정 1 — Phase 2 앞쪽 타일 우선 선택**:
`nearestTile == _unitData.Position`인 경우(불정령이 Phase 1 시작 타일 내부에 있는 경우), `nearestTile`(T0) 대신 **성(finalTarget) 방향으로 더 가까운 forward neighbor 타일** 중 현재 위치에서 중심이 가장 가까운 타일을 사용한다. 이렇게 하면 Lerp가 앞쪽으로 이동하게 된다.

**수정 2 — Phase 2 Lerp 중 적 감지**:
Lerp while 루프 안에 앞쪽 적 감지 체크(Step 2와 동일한 forward filter)를 추가한다. 앞쪽 적을 감지하면 Lerp를 조기 종료하고 `transform.position = tileCenter`로 즉시 스냅 후, 정상적인 Phase 2 완료 처리(ProcessStep + A* 재계산)를 거쳐 Phase 0으로 넘어간다. Phase 0 첫 번째 감지 체크에서 즉시 Phase 1로 진입한다.

구체적인 구현 방법은 Plan.md Step 4 참조.
