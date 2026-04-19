# Research — 근접유닛 적 감지 사거리 개선

## 1. 현상 요약

근접유닛이 이동 중 바로 옆 타일에 적이 있어도 무시하고 경로를 따라 앞으로 이동하는 현상.
**감지 사거리(DetectRange)가 따로 없고, 공격 사거리와 동일한 거리(0.3f)를 감지에 사용하기 때문.**

---

## 2. 현재 감지 흐름

```
UnitView.MoveAlongPath() (매 프레임)
  └─ UnitCombatUseCase.HasEnemyInRange()
       └─ FindFirstEnemyTarget()
            └─ 근접유닛: Vector3.Distance <= MeleeContactDist (0.3f) 판정
```

**핵심 파일 및 라인:**

| 파일 | 역할 | 라인 |
|------|------|------|
| `UnitView.cs` | 이동 중 매 프레임 감지 체크 | 532 |
| `UnitCombatUseCase.cs` | HasEnemyInRange → FindFirstEnemyTarget | 253, 266 |
| `UnitCombatUseCase.cs` | 근접/원거리 거리 분기 상수 정의 | 28-37 |
| `UnitStats.cs` | AttackRange 값 정의 (근접 = 0.5f) | 63-78 |
| `UnitData.cs` | AttackRange 프로퍼티 | 61 |

---

## 3. 문제 원인 상세

### 3-1. 감지 거리 불일치

헥스 타일 구조에서 인접 타일 중심 간 거리는 **~0.866f** (= TileHeight).

현재 근접유닛 감지 거리는 **MeleeContactDist = 0.3f** (두 유닛 메시가 맞닿는 거리).

→ 적이 인접 타일 중심(0.866f)에 있으면 0.3f 안에 들어오지 않아 감지 불가.

### 3-2. 감지 = 공격 사거리 (분리 없음)

`HasEnemyInRange()` → `FindFirstEnemyTarget()` → AttackRange 기준 판정.
**감지 사거리와 공격 사거리가 동일한 값을 사용** → 분리된 개념이 없음.

### 3-3. 경로 기반 이동의 한계

유닛은 A*로 계산된 경로(랠리포인트 기준)를 따라 이동.
경로가 적 타일을 거치지 않으면, Lerp 중 0.3f 이내로 접근하지 않아도 감지 안 됨.

예:
- 아군 유닛: 타일 A → C 이동 중
- 적 유닛: 타일 A의 인접 타일 B에 정지
- Lerp 경로(A→C)가 B에서 0.3f 이내를 지나지 않으면 → 감지 실패

---

## 4. 관련 코드 상세

### UnitCombatUseCase.cs — FindFirstEnemyTarget (라인 266-339)

```
근접유닛 판정 거리:
  유닛 타겟:   MeleeContactDist + Epsilon = 0.3f + 0.05f = 0.35f
  건물 타겟:   MeleeContactDist + BuildingDetectionRadius + Epsilon = 0.55f
원거리유닛:   AttackRange × TileHeight + Epsilon
```

**두 거리 역할:**
- `MeleeContactDist (0.3f)` = 공격 & 감지 모두 사용 중 (분리 없음)
- `BuildingDetectionRadius (0.2f)` = 건물 전용 추가 거리 (부분적 분리 시도)

### UnitView.cs — MoveAlongPath 전투 체크 (라인 520-629)

```
while (Lerp 이동 중):
    if HasEnemyInRange():        ← 공격 사거리(0.3f)로 감지
        // 멀티: 이벤트 발행 → 대기
        // 싱글: TryAttack 루프
```

매 프레임 감지 체크를 하지만, 체크 기준이 0.3f이기 때문에
인접 타일 0.866f 거리는 체크를 통과하지 못함.

---

## 5. HexCoord 폴백 메서드 (라인 350-386)

`_positionProvider`가 null일 때 사용하는 `FindFirstEnemyTargetByHexCoord()`:
```
int rangeThreshold = Max(1, CeilToInt(AttackRange))
// 근접유닛(0.5f) → threshold=1 → HexCoord.Distance=1 인접 타일까지 탐색
```

HexCoord 폴백에서는 이미 인접 타일(Distance=1)까지 탐색.
**월드 좌표 주경로(FindFirstEnemyTarget)만 0.3f 제한에 걸림.**

---

## 6. 영향 범위 (개선 시 수정 필요 파일)

| 파일 | 변경 내용 |
|------|---------|
| `UnitStats.cs` | `GetDetectRange()` 메서드 추가 |
| `UnitData.cs` | `DetectRange` 프로퍼티 + 생성자 파라미터 추가 |
| `UnitCombatUseCase.cs` | `HasEnemyInDetectRange()` + `FindFirstEnemyInDetectRange()` 추가, FindFirstEnemyTarget에 detect/attack 분리 |
| `UnitView.cs` | MoveAlongPath에서 감지 체크를 HasEnemyInDetectRange로 변경, 감지 후 재경로 로직 추가 |

**변경 불필요 파일:**
- `NetworkCombatController.cs` — 공격 판정은 AttackRange 유지, 네트워크 전투 로직 무변경
- `HexGrid.cs` — 타일 탐색 로직 무변경

---

## 7. 핵심 설계 고려사항

### DetectRange 값 결정
- 근접유닛 DetectRange = **1.0f (타일 단위)** = 월드 0.866f
- 인접 타일 중심을 포함하는 최소 거리

### 감지 후 동작 설계 (핵심 이슈)

감지(0.866f)와 공격(0.3f) 사이에 거리 차이가 있어,
단순히 감지 후 "대기"만 하면 → 적이 고정되어 있을 경우 공격 불가 상태 발생.

→ **감지 후 적에게 경로 재계산(reroute)하여 공격 사거리 내로 이동 후 전투** 가 필요.

예상 흐름:
```
감지(0.866f) → 현재 경로 중단
→ 적 위치로 재경로(인접 타일) → 이동
→ 공격 사거리(0.3f) 진입 → 전투 루프
```

### 네트워크 영향
- `UnitView.MoveAlongPath()`는 클라이언트 측에서 실행
- 감지 후 재경로(reroute)는 클라이언트 UnitView 내에서 처리
- 서버 전투 권한(NetworkCombatController)은 공격 사거리(AttackRange) 기준 유지 → 변경 없음
- 멀티플레이 감지 이벤트(`OnUnitEnteredCombat`) 발행 시점은 재경로 완료 후로 조정 필요
