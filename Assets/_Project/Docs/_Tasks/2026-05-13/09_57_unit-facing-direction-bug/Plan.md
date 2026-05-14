# Plan — 유닛 회전 방향 버그 진단 로그 추가

## 작업 목적

전투가 끝난 뒤 유닛이 이동 방향과 다른 방향을 바라보며 이동하는 현상의 원인을 파악하기 위해 런타임 로그를 추가한다.
동시에, 이미 해결이 확인된 BUG-002 진단 로그를 제거하여 새 버그 관련 로그를 쉽게 찾아볼 수 있도록 한다.

코드 동작 변경 없이 **로그 추가/제거만** 수행한다.

---

## 변경 파일

`Presentation/Unit/UnitView.cs` 단일 파일만 수정.

---

## 1. 제거할 로그 (BUG-002 해결 완료)

### 1-1. `RESUME_ALIGN_START` 로그 제거
- **위치**: MoveAlongPathV3 정렬 Lerp 시작 직전 (라인 ~944~946)
- **제거 대상**:
  ```csharp
  MovementLogger.Log(_unitData.Id, "RESUME_ALIGN_START",
      $"forwardTile={forwardTile} alignDist={alignDist:F4} "
      + $"alignDuration={alignDuration:F4}");
  ```

### 1-2. `RESUME_ALIGN_INTERRUPT` 로그 제거
- **위치**: 정렬 Lerp while 루프 내 적 감지 분기 (라인 ~964~967)
- **제거 대상**:
  ```csharp
  MovementLogger.Log(_unitData.Id, "RESUME_ALIGN_INTERRUPT",
      $"enemyId={redetected.Value.id} isUnit={redetected.Value.isUnit} "
      + $"currentPos={transform.position}");
  ```

### 1-3. `RESUME_ALIGN_END` 로그 제거
- **위치**: 정렬 Lerp 완료 후 최종 스냅 다음 (라인 ~991~992)
- **제거 대상**:
  ```csharp
  MovementLogger.Log(_unitData.Id, "RESUME_ALIGN_END",
      $"forwardTile={forwardTile} pos={transform.position}");
  ```

### 1-4. `RESUME_DOMAIN_JUMP` 로그 제거
- **위치**: `ResumeFromForwardTileV3` 내부 ProcessStep 직전 (라인 ~1377~1380)
- **제거 대상**:
  ```csharp
  MovementLogger.Log(_unitData.Id, "RESUME_DOMAIN_JUMP",
      $"from={_unitData.Position} to={forwardTile} "
      + $"tileDist={HexCoord.Distance(_unitData.Position, forwardTile)}");
  ```

### 1-5. `PURSUIT_END_TARGET_DEAD` 로그 제거
- **위치**: `EnterCombatPursuitV3` 내 TargetDestroyedNoMore 경로 (라인 ~1161~1162)
- **제거 대상**:
  ```csharp
  MovementLogger.Log(_unitData.Id, "PURSUIT_END_TARGET_DEAD",
      $"unitPos={transform.position} domainPos={_unitData.Position} targetId={targetId}");
  ```
  위 로그와 함께 붙어있는 주석 `// [BUG-002 진단 로그] ...`도 제거.

---

## 2. 추가할 로그 (회전 버그 진단)

### 2-1. `FACING_COMBAT_STOP` — 전투 종료 시점 rotation 기록
- **위치**: `StopCombatAnimation()` 내부, `_combatTargetTransform = null` 직후
- **기록 내용**: 전투가 끝난 시점에 유닛이 어느 방향을 바라보고 있는지
  ```csharp
  MovementLogger.Log(_unitData.Id, "FACING_COMBAT_STOP",
      $"rotation.y={transform.eulerAngles.y:F1}");
  ```

### 2-2. `FACING_ALIGN_START` — 정렬 Lerp 시작 직전 방향 기록
- **위치**: MoveAlongPathV3 정렬 Lerp while 루프 시작 직전 (현재 `RESUME_ALIGN_START` 자리)
- **기록 내용**: forwardTile로 향하는 방향(HexDirection)과 현재 rotation.y
  - `nearestTile`은 `HexMetrics.WorldToHex(unitDomainPos)`로 계산 (이미 `unitDomainPos`가 앞에서 계산됨)
  - `alignDir = FacingDirection.FromCoords(nearestTile, forwardTile)` (뷰 반전 전 도메인 방향)
  ```csharp
  HexCoord nearestTileForLog = HexMetrics.WorldToHex(unitDomainPos);
  HexDirection alignDirForLog = FacingDirection.FromCoords(nearestTileForLog, forwardTile);
  MovementLogger.Log(_unitData.Id, "FACING_ALIGN_START",
      $"from={nearestTileForLog} to={forwardTile} expectedDir={alignDirForLog} "
      + $"rotation.y={transform.eulerAngles.y:F1}");
  ```

### 2-3. `FACING_ALIGN_END` — 정렬 Lerp 완료 후 rotation 기록
- **위치**: 정렬 최종 스냅(`transform.position = alignView`) 직후
- **기록 내용**: Lerp 완료 시점의 rotation.y — 잘못된 방향이 유지되고 있는지 확인
  ```csharp
  MovementLogger.Log(_unitData.Id, "FACING_ALIGN_END",
      $"rotation.y={transform.eulerAngles.y:F1}");
  ```

### 2-4. `FACING_AST_SET` — A* 타일 이동 방향 설정 기록
- **위치**: MoveAlongPathV3 for 루프 내 `ApplyDirection(dir)` 직후
- **기록 내용**: A*가 설정하는 방향과 rotation.y — 올바른 방향으로 전환되는 시점 확인
  ```csharp
  MovementLogger.Log(_unitData.Id, "FACING_AST_SET",
      $"from={from} to={to} dir={dir} rotation.y={transform.eulerAngles.y:F1}");
  ```

---

## 위험 요소

| 항목 | 위험 | 대응 |
|------|------|------|
| `nearestTile` 계산 추가 | 로그 전용 변수 추가 — 코드 흐름에 영향 없음 | 로그 전용 변수명에 `ForLog` 접미사 사용으로 명확히 구분 |
| FACING_AST_SET 빈도 | 모든 타일 이동마다 기록되어 로그 양이 많을 수 있음 | 분석 후 불필요하면 제거 |

---

## 4. 실제 버그 수정 — 정렬 Lerp 시작 시 회전 교정 (2026-05-14)

### 수정 파일

`Presentation/Unit/UnitView.cs` 단일 파일.

### 수정 위치

`FACING_ALIGN_START` 로그 직후 (라인 ~956).

### 수정 내용

`FACING_ALIGN_START` 로그 다음에 아래 코드를 추가한다.
`alignDirForLog`는 이미 바로 위에서 계산된 도메인 방향 변수이므로 추가 계산 없음.

```csharp
// 전투 종료 후 고정된 적 방향 rotation을 정렬 이동 방향으로 즉시 교정.
// alignDirForLog는 도메인 방향 → FlipDirection으로 뷰 방향으로 변환 후 ApplyDirection.
HexDirection alignViewDir = ViewConverter.FlipDirection(alignDirForLog);
_unitData.Facing = alignViewDir;
ApplyDirection(alignViewDir);
MovementLogger.Log(_unitData.Id, "FACING_ALIGN_CORRECTED",
    $"dir={alignViewDir} rotation.y={transform.eulerAngles.y:F1}");
```

### 기대 로그 패턴 (수정 후)

```
FACING_COMBAT_STOP    rotation.y=54.7   ← 전투 종료, 아직 틀린 방향
FACING_ALIGN_START    rotation.y=54.7   ← Lerp 시작 전, 아직 틀린 방향
FACING_ALIGN_CORRECTED  dir=NW  rotation.y=0.0   ← 즉시 교정됨
FACING_ALIGN_END      rotation.y=0.0   ← 교정된 채로 완료
```

### 위험 요소

| 항목 | 위험 | 대응 |
|------|------|------|
| `_unitData.Facing` 갱신 | 도메인과 뷰 회전 불일치 방지를 위해 Facing도 업데이트 | `ApplyDirection`과 함께 세트로 갱신 |
| `alignDirForLog` 재사용 | 이미 선언된 변수 재사용 — 추가 계산 없음 | 변수명으로 용도 명확 |

### 작업 순서

1. `UnitView.cs` — `FACING_ALIGN_START` 로그 직후에 회전 교정 코드 4줄 삽입
2. 컴파일 에러 없음 확인
3. 런타임 테스트 후 `FACING_ALIGN_CORRECTED` 로그 확인

---

## 3. MovementLogger 경로 변경

**파일**: `Application/Services/MovementLogger.cs`

`LogRelativePath` 상수를 현재 작업 폴더에 맞게 변경한다.

```csharp
// 변경 전
"/../Assets/_Project/Docs/_Logs/2026-05-11/23_19_unit-movement-redesign/RuntimeLog.txt"

// 변경 후
"/../Assets/_Project/Docs/_Logs/2026-05-13/09_57_unit-facing-direction-bug/RuntimeLog.txt"
```

---

## 작업 순서

1. `MovementLogger.cs` — 로그 경로 변경
2. `UnitView.cs` — BUG-002 로그 5개 제거
3. `UnitView.cs` — 회전 진단 로그 4개 추가
4. 컴파일 에러 없음 확인
