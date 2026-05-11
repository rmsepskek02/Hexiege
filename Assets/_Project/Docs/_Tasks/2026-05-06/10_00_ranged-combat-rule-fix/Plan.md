# Plan — 원거리 유닛 이동 슬롯 규칙 위반 수정

작성일: 2026-05-06
Research: `_Tasks/2026-05-06/10_00_ranged-combat-rule-fix/Research.md`
규칙 근거: `Docs/GameSystemRules.md` (규칙 13, 15)

---

## 이 작업이 무엇인지

BUG-005/006 수정에서 원거리 유닛의 이동 슬롯을 해제한 코드가 GameSystemRules.md 규칙 13, 15를 위반한다.
이 작업은 해당 코드를 규칙에 맞게 교체한다.

**기존 접근법 (잘못됨):** 전투 진입 시 이동 슬롯 해제 → 슬롯 없는 상태로 전투  
**올바른 접근법:** 이미 클레임된 슬롯 위치로 스냅 → 슬롯 유지한 채 전투

---

## 수정 대상 파일

| 파일 | 수정 이유 |
|------|---------|
| `Presentation/Unit/UnitView.cs` | RunTileTraversal() 원거리 전투 진입 분기 교체 |

---

## Step 1 — 원거리 전투 진입 분기 교체

### 수정 위치

`UnitView.cs` → `RunTileTraversal()` → 원거리 적 감지 분기 (`kind == AttackKind.Ranged && HasEnemyInRange`)

현재 `PRE_COMBAT_SLOT_RELEASE` 로그와 `ReleaseV2MoveSlotIfClaimed()` 호출이 있는 구간.

### 수정 전 (규칙 위반 코드)

```csharp
// [실기 로그] 슬롯 해제 직후, 정지 전투 진입 직전 — 검증용 마커.
MovementLogger.Log(_unitData.Id, "PRE_COMBAT_SLOT_RELEASE",
    $"tile={_v2MoveSlotTile} mode=Ranged");

ReleaseV2MoveSlotIfClaimed();
ReleaseOccupancyIfPending();
_unitData.ClaimedTile = null;
```

### 수정 후 (규칙 13, 15 준수)

```csharp
// [규칙 13] 원거리 유닛은 이동 슬롯 위치에서 멈추고 즉시 공격한다.
// 슬롯을 해제하는 것이 아니라, 이미 클레임된 슬롯 위치(toPos)로 스냅한다.
//
// 이렇게 하면:
//   - 유닛이 물리적으로 슬롯 위치에 있으므로 유령 점유가 발생하지 않는다 (BUG-005/006 해결).
//   - 타일 위에 서 있으므로 시각적으로 타일을 벗어나 보이지 않는다.
//   - 슬롯이 유지되므로 뒤따르는 유닛들이 이 슬롯을 피해 다른 위치로 분산된다.
//   - [규칙 15] 전투 종료 후 이 슬롯 위치에서 A*를 바로 재개한다.
transform.position = toPos;

// elapsed를 최대값으로 설정하여, 전투 종료 후 continue 시 Lerp while 루프가
// 즉시 탈출하고 기존 TILE_ARRIVED 처리 흐름(ProcessStep, 로그)이 자연스럽게 실행되도록 한다.
elapsed = targetDuration;
```

### 변경 요약

| 항목 | 기존 | 변경 |
|------|------|------|
| `PRE_COMBAT_SLOT_RELEASE` 로그 이벤트 | 있음 | **제거** |
| `ReleaseV2MoveSlotIfClaimed()` 호출 | 있음 | **제거** |
| `ReleaseOccupancyIfPending()` 호출 | 있음 | **제거** |
| `_unitData.ClaimedTile = null` | 있음 | **제거** |
| `transform.position = toPos` | 없음 | **추가** |
| `elapsed = targetDuration` | 없음 | **추가** |

---

## Step 2 — STATIONARY_COMBAT_START 로그 수정

전투 위치가 `prevActualTile`(이전 타일)이 아닌 `to`(스냅된 슬롯 타일)이므로 로그를 수정한다.

```csharp
// 수정 전
MovementLogger.Log(_unitData.Id, "STATIONARY_COMBAT_START",
    $"currentTile={prevActualTile}");

// 수정 후
MovementLogger.Log(_unitData.Id, "STATIONARY_COMBAT_START",
    $"currentTile={to}");
```

---

## 전투 종료 후 흐름 (변경 없음)

수정 후 전투 종료 흐름은 기존 코드 변경 없이 자연스럽게 동작한다.

1. 전투 종료 → `_v2InStationaryCombat = false`
2. `yield return null; continue;` 실행
3. `elapsed >= targetDuration` → Lerp while 루프 탈출
4. `transform.position = toPos` (이미 스냅됨, 무효 연산)
5. `ProcessStep(from, to)` → `_unitData.Position = to`, FROM 점유 해제
6. `_unitData.ClaimedTile = null`, `prevActualTile = to`, `_pendingOccupancyTile = default`
7. `TILE_ARRIVED` 로그 출력
8. 다음 타일 처리로 진행

적이 아직 살아있으면 다음 타일 이동 시 다시 감지 → 또 스냅 → 또 전투 (자연 반복)

---

## 기존 로직 제거 근거

제거되는 코드들은 규칙 위반 코드이므로 안전하게 제거한다:

- `PRE_COMBAT_SLOT_RELEASE` 로그: 더 이상 발생하지 않는 이벤트이므로 제거
- `ReleaseV2MoveSlotIfClaimed()`: 규칙 13 위반 → 제거
- `ReleaseOccupancyIfPending()`: 규칙 13 위반 → 제거
- `_unitData.ClaimedTile = null`: 슬롯 유지 상태에서 null로 만드는 것은 불일치 → 제거 (post-Lerp 코드에서 정상 처리됨)

---

## 예상 위험 요소

| 위험 | 대응 |
|------|------|
| `elapsed = targetDuration` 설정이 같은 while 루프 내 다른 분기에 영향 | 원거리 분기에서만 설정. 해당 분기 이후 바로 `EnterStationaryCombat` + `continue`로 빠져나가므로 다른 분기에 도달하지 않음 |
| ProcessStep 중복 호출 | 전투 진입 시에는 ProcessStep 호출 없음. post-Lerp 코드에서만 1회 호출 |
| 이동 슬롯 미해제 누수 | 다음 타일 이동 시 `_v2MoveSlotTile != new_to` 조건에서 자동 해제. 기존 정상 흐름과 동일 |
| 사망 시 슬롯 잔류 | 사망 처리 코드에서 `ReleaseV2MoveSlotIfClaimed()` 호출함 — 기존 규칙 12 처리와 동일 |

---

## 수정 순서

1. `UnitView.cs` Step 1 — 슬롯 해제 코드 제거, toPos 스냅 + elapsed 설정 추가
2. `UnitView.cs` Step 2 — STATIONARY_COMBAT_START 로그 currentTile 변경
3. 런타임 테스트 후 로그 확인:
   - `PRE_COMBAT_SLOT_RELEASE` 이벤트가 더 이상 나오지 않아야 함
   - `STATIONARY_COMBAT_START currentTile={to}` 값이 SLOT_CLAIM tile과 일치해야 함
   - 유닛이 타일 위에 위치해야 함 (시각적 확인)
   - 피스톨러가 분산되어야 함
