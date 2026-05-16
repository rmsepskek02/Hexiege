# Plan — 건물 생성/파괴 시 유닛 이동 멈춤 현상 수정

## 작업 개요 (자연어 설명)

건물이 생성되거나 파괴될 때 이동 중인 유닛들이 잠깐 멈추는 현상을 수정한다.
현재는 경로를 새로 받을 때 이동 코루틴을 완전히 중단하고 재시작하기 때문에 멈춤이 발생한다.
이를 "다음 타일 도착 시점에 경로를 교체"하는 방식으로 바꿔서, 유닛이 멈추지 않고
자연스럽게 새 경로로 전환되도록 한다.

---

## GameSystemRules.md 근거

> **규칙 4 (경로 재계산 시점)**
> "건물 건설 / 건물 파괴 시 해당 시점에 모든 유닛의 경로를 즉시 재계산한다."

이 규칙은 "경로를 즉시 재계산"할 것을 요구한다.
**경로 계산 자체**는 지금과 동일하게 즉시 수행한다.
변경하는 것은 "계산된 경로를 언제 적용하느냐"이며,
타일 도착 시점까지 (최대 타일 하나 이동 시간) 지연 적용하는 것은 규칙 4와 충돌하지 않는다.

---

## 수정 파일 및 변경 내용

수정 대상은 `UnitView.cs` 단 하나다.
`GameBootstrapper.cs`, `FlowFieldService.cs`는 변경하지 않는다.

---

### UnitView.cs

#### 1. 필드 추가

```
_pendingPath (List<HexCoord>)
    — OnPathInvalidated에서 저장한 "대기 중인 새 경로".
      MoveAlongPathV3 내 다음 타일 도착 시점에 적용 후 null로 초기화.

_currentNextTileCoord (HexCoord?)
    — 현재 Lerp 중인 목표 타일 좌표.
      MoveAlongPathV3에서 Lerp 시작 직전에 set, 완료 직후 null로 초기화.
      OnPathInvalidated에서 "지금 향하는 타일에 건물이 생겼는지" 체크에 사용.
```

#### 2. `OnPathInvalidated()` 수정

기존: 항상 `MoveTo(newPath)` 호출 → 코루틴 즉시 중단

변경 후 처리 흐름:

```
1) 기존 가드 조건 유지 (IsNetworkActive, _hasDestination, IsAlive, _movementUseCase)
2) IsInCombat() → 전투 중이면 return (기존과 동일)
3) 새 경로 계산 (기존과 동일)
4) _currentNextTileCoord 체크:
   - null 이거나 (이동 중이 아님), 해당 타일이 walkable → _pendingPath = newPath 저장
   - 해당 타일이 walkable이 아님 (건물이 그 위에 생겼음) → 기존처럼 MoveTo(newPath) 호출
5) _pendingPath를 저장한 경우, MoveTo는 호출하지 않음
```

#### 3. `MoveAlongPathV3()` 수정

각 타일 Lerp 완료 직후(ProcessStep 직후)에 아래 처리를 추가한다.

```
if (_pendingPath != null)
{
    path를 _pendingPath로 교체
    _pendingPath = null
    pathIndex를 현재 위치 기준으로 재설정
    외부 while 재진입 (continue)
}
```

- _currentNextTileCoord는 각 Lerp 시작 시 set, Lerp 완료 시 null로 초기화한다.

#### 4. `MoveTo()` 수정

외부에서 직접 `MoveTo()`가 호출되는 경우(랠리포인트 이동 등)에
`_pendingPath`가 남아있으면 새 MoveTo 명령과 충돌한다.

```
MoveTo() 진입 시 _pendingPath = null 처리 추가
```

---

## 예상 동작 결과

| 상황 | 변경 전 | 변경 후 |
|------|---------|---------|
| 건물이 멀리 생성됨 | 모든 유닛 코루틴 중단 → 멈춤 | 다음 타일 도착 시 경로만 교체 → 멈춤 없음 |
| 건물이 유닛이 향하던 타일에 생성됨 | 코루틴 중단 → 멈춤 | 동일하게 즉시 코루틴 재시작 → 멈춤 (불가피, 극히 드묾) |
| 전투 중(공격 또는 추격) 건물 생성 | `IsInCombat()` 차단으로 repath 없음 | 동일 (변경 없음) |

---

## 위험 요소

| 항목 | 내용 |
|------|------|
| `_pendingPath` 미초기화 | `MoveTo()` 직접 호출 시 초기화 누락 → 이전 pending 경로가 적용될 수 있음. MoveTo 진입부에서 반드시 null 처리 필요. |
| Lerp 중 path 길이 불일치 | 교체 시점에 pathIndex를 현재 UnitData.Position 기준으로 다시 찾아야 함. 현재 위치가 새 path에 없는 경우 예외 처리 필요. |
| 멀티플레이 타이밍 | 서버 전용 코루틴이므로 클라이언트에는 영향 없음. |

---

## 변경 범위 요약

| 파일 | 변경 종류 |
|------|-----------|
| `UnitView.cs` | 필드 추가 2개, 메서드 수정 3개 (`OnPathInvalidated`, `MoveAlongPathV3`, `MoveTo`) |
| `GameBootstrapper.cs` | 변경 없음 |
| `FlowFieldService.cs` | 변경 없음 |
