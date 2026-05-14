# Research — 유닛 회전 방향 버그 진단 로그 추가

## 작업 목적

전투 종료 후 유닛이 이동 방향과 다른 곳을 바라보며 이동하는 버그를 진단하기 위한 런타임 로그를 추가한다.
동시에, 이미 해결된 BUG-002의 진단 로그를 제거하여 로그 파일을 깔끔하게 유지한다.

---

## 현재 회전 처리 흐름

### 1. A* 이동 중 방향 설정
`MoveAlongPathV3` for 루프, 각 타일 진입 시마다 호출됨 (`UnitView.cs` 라인 ~832):

```csharp
HexDirection dir = FacingDirection.FromCoords(from, to);
dir = ViewConverter.FlipDirection(dir);
_unitData.Facing = dir;
ApplyDirection(dir);   // transform.rotation 즉시 스냅
```

- `from` → `to` 타일 방향을 뷰 기준으로 반전 후 `transform.rotation`에 즉시 스냅

### 2. 전투 이동(추격) 중 방향 갱신
`Update()` 매 프레임 실행:

```csharp
if (_combatTargetTransform != null)
{
    float angle = CalculateAttackAngle(_combatTargetTransform.position);
    transform.rotation = Quaternion.RotateTowards(..., CombatRotationSpeed);
}
```

- `_combatTargetTransform`이 null이 아닌 동안 매 프레임 적 방향으로 회전

### 3. 전투 종료 시 (`StopCombatAnimation`, 라인 ~1592)

```csharp
_combatTargetTransform = null;   // Update()의 회전 갱신 즉시 중단
_combatTargetId = -1;
```

- 이 시점에서 유닛의 rotation은 **마지막으로 추적하던 적 방향**으로 고정된다.

### 4. 정렬 Lerp (BUG-002 수정으로 추가된 블록, 라인 ~948~989)

```csharp
while (alignElapsed < alignDuration ...)
{
    transform.position = Vector3.Lerp(alignFromPos, alignView, at);
    // ← 회전 설정 코드 없음
    yield return null;
}
transform.position = alignView;
```

- **회전 설정 코드가 전혀 없다.**
- 전투 종료 시점의 rotation(적 방향)이 그대로 유지된 채 정렬 이동을 한다.

### 5. A* 재개 (ResumeFromForwardTileV3 + 외부 while 재진입)

새 path를 받아 for 루프가 다시 시작될 때 첫 타일 진입에서 `ApplyDirection`이 호출된다.
정렬 Lerp가 끝나기 전까지는 A*가 시작되지 않으므로, 이 호출이 지연된다.

---

## 버그 원인 추정

1. 유닛이 전투를 종료한다 → `StopCombatAnimation()` → rotation이 **적 방향**으로 고정
2. 정렬 Lerp 시작 → **회전 설정 없이** forwardTile 방향으로 걸어감
3. 정렬 Lerp 기간(1~2.6초) 동안 **적 방향을 바라보면서 앞으로 걷는 비정상 상태** 발생
4. 정렬 Lerp 완료 후 A* 첫 타일 진입 시에야 `ApplyDirection` 호출 → 올바른 방향으로 전환

로그를 통해 정확히 어느 시점에 어떤 rotation 값이 있었는지 추적하면 원인을 확정할 수 있다.

---

## 핵심 파일

| 파일 | 클래스 | 역할 |
|------|--------|------|
| `Presentation/Unit/UnitView.cs` | UnitView | 회전 처리, A* 이동, 전투 애니메이션 |

---

## 런타임 로그 분석 결과 (2026-05-14)

### 로그 통계 (전체 717KB)

| 로그 태그 | 발생 수 | 의미 |
|-----------|---------|------|
| `FACING_COMBAT_STOP` | 171회 | 전투 종료 매우 자주 발생 |
| `FACING_ALIGN_START` | **1회** | 정렬 Lerp가 거의 실행 안 됨 |
| `FACING_ALIGN_END` | **0회** | 정렬 완료 후 로그 한 번도 안 찍힘 |
| `FACING_AST_SET` | 375회 | A* 이동 방향 설정 정상 동작 |

### 버그 확인 케이스 (UnitID:85)

```
03:32:47.228  FACING_COMBAT_STOP     rotation.y=54.7  ← 전투 종료, 적 방향으로 고정
03:32:47.229  FACING_ALIGN_START     expectedDir=SE   rotation.y=54.7  ← 이동 시작, 회전 그대로
              [1.85초 동안 이동 — rotation 변화 없음]
03:32:49.079  FACING_AST_SET         dir=NW           rotation.y=0.0   ← 여기서야 교정됨
```

expectedDir=SE(도메인) → 뷰 기준 NW → rotation.y=0.0 이어야 하는데, 54.7°(약 NE 방향)로 1.85초 이동.

### FACING_ALIGN_END가 찍히지 않는 이유

`FACING_ALIGN_END`는 코드에는 존재하지만 도달하지 못하고 있음.

정렬 Lerp 안에서 `HasEnemyInDetectRange` 체크 시 적 감지 → `alignInterruptedByCombat = true; break;`로 Lerp 중단.
이때 `ENEMY_DETECTED` 로그는 찍히지 않음 (A* for 루프의 감지 분기와 달리 로그 없음).

중단 후 `interruptedByCombat = true; break;` → for 루프 탈출 → `needRepath = false` 이므로 outer while 종료 → `cleanup`. `MoveCleanupAndCompleteV3()`가 이동 완료 이벤트를 발생시키면 시스템이 도메인 위치(`_unitData.Position=(5,13)`)에서 새 경로를 재발급.

### 171회 FACING_COMBAT_STOP 중 1회만 FACING_ALIGN_START인 이유

전투 종료 후 대부분의 유닛은 정렬 Lerp에 도달하기 전에 새 적을 즉시 감지하여 `EnterCombatPursuitV3`를 재진입함. 정렬 Lerp는 전투 종료 → 정렬 시작 → Lerp 도중 적 감지 없이 완료되어야만 `FACING_ALIGN_END`까지 도달하는데, 현재 전투가 워낙 밀집되어 있어 그런 경우가 극히 드묾.

### 확정된 버그 원인

BUG-002 수정 시 추가한 정렬 Lerp 블록 (라인 ~908~1014)에 위치 이동 코드만 있고 **회전 설정 코드가 없음**. 전투 종료 시점의 적 방향 rotation이 Lerp 전체 구간 동안 그대로 유지됨.

---

## 제거할 로그 (BUG-002 해결 완료)

| 로그 태그 | 위치 | 제거 이유 |
|-----------|------|-----------|
| `RESUME_ALIGN_START` | MoveAlongPathV3 정렬 Lerp 시작부 | BUG-002 진단 완료 |
| `RESUME_ALIGN_INTERRUPT` | 정렬 Lerp 중 적 감지 시 | BUG-002 진단 완료 |
| `RESUME_ALIGN_END` | 정렬 Lerp 완료 후 | BUG-002 진단 완료 |
| `RESUME_DOMAIN_JUMP` | ResumeFromForwardTileV3 ProcessStep 직전 | BUG-002 진단 완료 |
| `PURSUIT_END_TARGET_DEAD` | EnterCombatPursuitV3 타겟 사망 경로 | BUG-002 진단 완료 |

---

## 추가할 로그 (회전 버그 진단)

| 로그 태그 | 삽입 위치 | 기록 내용 |
|-----------|-----------|-----------|
| `FACING_COMBAT_STOP` | `StopCombatAnimation()` 내부 | 전투 종료 시점의 rotation.y |
| `FACING_ALIGN_START` | 정렬 Lerp 시작 직전 | forwardTile 방향(from→to HexDirection), rotation.y |
| `FACING_ALIGN_END` | 정렬 Lerp 완료 후 | rotation.y (잘못된 방향이라면 여기서 확인) |
| `FACING_AST_SET` | A* for 루프 `ApplyDirection` 직후 | from, to, dir, rotation.y |
