# Research — 유닛 회전 시스템 수정

## 이 작업이 필요한 이유

유닛이 전투를 마치고 이동을 재개할 때 이상한 방향을 바라보거나, A* 이동 중 타일을 바꿀 때 회전이 갑자기 툭 바뀌는 문제가 있습니다.
원인을 분석하면서 회전 시스템 전체에 두 가지 구조적 문제가 있다는 것을 확인했습니다.

1. **방향 계산 방식이 잘못됨**: 유닛이 어디로 돌아야 하는지를 타일 격자 좌표 차이로 계산합니다. 이 방식은 출발 타일과 목적 타일이 같은 특수한 상황에서 엉뚱한 방향(NE = 북동)을 돌려보냅니다. 반면, 현재 유닛의 실제 위치에서 목적 타일 중심까지의 벡터를 사용하면 어떤 상황에서도 항상 정확한 방향이 나옵니다.

2. **회전이 모든 상태에서 일관되지 않음**: 전투 추격 중에는 부드럽게 서서히 회전하지만, A* 이동이나 전투 종료 후에는 즉시 툭 바뀝니다. 규칙 7, 8에 따르면 모든 상황에서 서서히 회전해야 합니다. 또한 회전 속도를 개발 중에 조정할 수 없고 코드 안에 고정값으로 박혀 있습니다.

추가로, 전투 후 실제로 뒤쪽 타일로 이동하는 현상이 보고됐는데 현재 런타임 로그만으로는 이동 경로를 추적하기 어렵습니다. 이번 작업에서 이동 방향을 정밀하게 기록하는 로그를 추가하여 원인을 파악할 수 있도록 합니다.

---

## 현재 코드 상태

### 회전 계산 위치별 현황

| 상태 | 파일 | 방향 계산 방식 | 회전 방식 |
|------|------|--------------|----------|
| A* 이동 | UnitView.cs:832 | `FacingDirection.FromCoords(from, to)` (타일 좌표 차이) | `ApplyDirection` → **즉시 스냅** |
| 전투 종료 후 정렬 | UnitView.cs:953 | `FacingDirection.FromCoords(nearestTile, forwardTile)` (타일 좌표 차이) | `ApplyDirection` → **즉시 스냅** |
| 전투 추격 | UnitView.cs:1249 | `CalculateAttackAngle(enemyViewPos)` (월드 Atan2) | `RotateTowards` → **서서히** |
| 공격 중 (Update) | UnitView.cs:270 | `CalculateAttackAngle(target.position)` (월드 Atan2) | `RotateTowards` → **서서히** |

### ApplyDirection 구현 (UnitView.cs:435~440)
```
transform.rotation = Quaternion.Euler(0f, DirectionAngles[index], 0f);
```
→ DirectionAngles 테이블에서 고정 각도를 꺼내 즉시 스냅. 서서히 회전 없음.

### CalculateAttackAngle 구현 (UnitView.cs:449~)
```
Atan2 기반으로 타겟 월드 위치에서 직접 각도 계산
```
→ 실제 월드 벡터 사용. from==to 문제 없음.

### CombatRotationSpeed (UnitView.cs:183)
```
private const float CombatRotationSpeed = 270f;
```
→ const로 고정. Inspector에서 조정 불가.

---

## 문제 케이스 상세

### 케이스 1: nearestTile == forwardTile (from == to)
- 발생 조건: 전투 종료 후 유닛의 위치가 이미 forwardTile과 같은 타일 영역에 있을 때
- `FacingDirection.FromCoords(same, same)` → delta=(0,0) → `EstimateFlatTopDirection` 기본값 → **NE(북동) 방향 반환**
- 결과: 유닛이 엉뚱한 방향을 바라보며 이동 시작

### 케이스 2: 즉시 스냅 회전
- A* 이동 중 타일 전환 시 회전값이 툭 바뀜
- 전투 종료 후 정렬 시 회전값이 툭 바뀜
- 규칙 7, 8 위반

---

## 수정 대상 파일

- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
  - A* 이동 회전 (라인 832~835)
  - 전투 종료 후 정렬 회전 (라인 953~969)
  - `ApplyDirection` 메서드 (라인 435~440) 또는 대체
  - `CombatRotationSpeed` const → `[SerializeField]` 변환 (라인 183)

---

## 런타임 로그 추가 계획

현재 이동 방향을 추적하는 로그가 부족합니다. 다음 항목을 추가합니다:

| 로그 태그 | 기록 시점 | 기록 내용 |
|-----------|----------|----------|
| `ROTATION_TARGET_SET` | 회전 목표가 바뀔 때마다 | 현재 world position, 목표 world position, 계산된 각도, 상태(A*/정렬/추격) |
| `ALIGN_MOVE` | 정렬 Lerp 시작/종료 시 | 출발 world pos, 도착 world pos, 이동 방향이 앞인지 뒤인지 |
