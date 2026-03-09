# Plan: 클라이언트 유닛 이동 타일 충돌 수정

**작성일:** 2026-03-07
**작업명:** client-movement-tile-conflict
**담당:** game-programmer

---

## 목표

서버 권위 이동(BroadcastMoveClientRpc)을 수신한 클라이언트에서
`MoveAlongPath`의 로컬 타일 충돌 체크를 건너뜀.
서버가 결정한 경로를 클라이언트가 그대로 따라가도록 수정.

---

## 변경 파일

### 1. `UnitView.cs`

**`MoveTo()` 시그니처에 `serverAuthoritative` 파라미터 추가**

```csharp
// 변경 전
public void MoveTo(List<HexCoord> path)

// 변경 후
public void MoveTo(List<HexCoord> path, bool serverAuthoritative = false)
```

**`MoveAlongPath()` 동일하게 파라미터 전달**

```csharp
// 변경 전
_moveCoroutine = StartCoroutine(MoveAlongPath(path));

// 변경 후
_moveCoroutine = StartCoroutine(MoveAlongPath(path, serverAuthoritative));
```

**`MoveAlongPath()` 내 타일 충돌 체크 및 ClaimedTile 선점 조건 추가**

```csharp
// 변경 전
if (_movementUseCase != null && _movementUseCase.IsTileBlockedBySameTeam(_unitData, to))
{
    List<HexCoord> newPath = _movementUseCase.RequestMove(_unitData, finalTarget);
    ...
}
_unitData.ClaimedTile = to;

// 변경 후
// 서버 권위 이동에서는 타일 충돌 체크 생략 (서버가 경로를 결정했으므로 클라이언트 로컬 타일 상태로 재탐색 불필요)
if (!serverAuthoritative &&
    _movementUseCase != null && _movementUseCase.IsTileBlockedBySameTeam(_unitData, to))
{
    List<HexCoord> newPath = _movementUseCase.RequestMove(_unitData, finalTarget);
    ...
}
if (!serverAuthoritative)
    _unitData.ClaimedTile = to;
```

- `ProcessStep()` 호출은 유지 (unit.Position 업데이트 필요)
- `ClaimedTile = null` 정리 코드(429, 432번 라인)도 유지

---

### 2. `NetworkUnitMovementController.cs`

**`BroadcastMoveClientRpc`에서 `serverAuthoritative: true` 전달**

```csharp
// 변경 전
unitView.MoveTo(path);

// 변경 후
unitView.MoveTo(path, serverAuthoritative: true);
```

**`SyncMovementClientRpc`에서도 동일하게 적용** (상대방 클라이언트에서 상대 유닛 이동 시)

```csharp
// 변경 전
unitView.MoveTo(path);

// 변경 후
unitView.MoveTo(path, serverAuthoritative: true);
```

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| `ProcessStep` 미호출 시 unit.Position 미갱신 → 전투 사거리 판정 오류 | `ProcessStep` 유지 |
| ClaimedTile 미설정으로 다른 로컬 이동(싱글플레이)에서 겹침 발생 가능 | `!serverAuthoritative` 조건으로 싱글플레이/서버는 기존 동작 유지 |
| 기존 RequestMove 로컬 예측 이동(`RequestMove()` in NetworkUnitMovementController)은 비서버 권위 → 기존 동작 유지 | `serverAuthoritative` default = false |

---

## 수정 범위

- 수정 파일: `UnitView.cs`, `NetworkUnitMovementController.cs`
- 신규 파일: 없음
- 싱글플레이 영향: 없음 (`serverAuthoritative` default = false)
- 서버(Host) 영향: 없음 (서버는 `BroadcastMoveClientRpc`에서 `if (IsServer) return;` 으로 스킵)
