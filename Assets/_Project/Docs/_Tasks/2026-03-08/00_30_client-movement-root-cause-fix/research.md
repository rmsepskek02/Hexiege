# Research: 클라이언트 유닛 이동 완전 불작동 - 진짜 원인

**작성일:** 2026-03-08
**작업명:** client-movement-root-cause-fix

---

## 확정된 근본 원인 (3중 구조)

### 원인 1 (가장 핵심): `_networkUnitMovement` SerializeField가 Inspector에 미할당

`GameBootstrapper.cs` 전체 코드베이스에서 `_networkUnitMovement`가 등장하는 위치:
- 선언 (line 96): `[SerializeField] private NetworkUnitMovementController _networkUnitMovement;`
- 사용 (line 622): `isNetworkMode ? _networkUnitMovement : null` ← 우리가 추가한 코드

이 SerializeField는 siege 동기화 작업 이전에는 **선언만 되고 어디에도 사용된 적이 없었던 미사용 필드**. Inspector에 연결된 이유가 없음 → 항상 `null`.

결과:
```csharp
_productionTicker.Initialize(..., isNetworkMode ? null : null);  // 실제로 null 전달
// ProductionTicker._networkMovement = null
```

### 원인 2: `BroadcastMoveIfServer`가 항상 조기 반환

```csharp
private void BroadcastMoveIfServer(int unitId, List<HexCoord> path)
{
    if (_networkMovement == null) return;  // ← 항상 여기서 return
    ...
}
```

`_networkMovement == null` → RPC 전송 없음 → 클라이언트는 이동 명령을 받지 못함.

### 원인 3: 클라이언트의 `OnUnitProduced` 조기 반환

```csharp
private void OnUnitProduced(UnitProducedEvent e)
{
    if (IsNetworkClient) return;  // ← 클라이언트에서 항상 여기서 return
    ...
}
```

클라이언트는 RPC도 못 받고, 독립적으로 처리하지도 않음 → **이동 로직 진입 자체 불가**.

---

## 왜 타일 색은 변하는가

도메인 이동 처리(서버) → 타일 소유권 변경 → `NetworkTileSync` ClientRpc → 클라이언트 타일 색 변경.
UnitView 시각 이동과 완전히 분리된 독립 경로.

## 왜 공격 애니메이션은 재생되는가

`NetworkCombatController.TriggerAttackClientRpc` → `UnitView.TriggerAttackAnimation()` 직접 호출.
이동 로직과 무관한 독립 경로.

---

## 올바른 수정 방향

### 잘못된 방향 (현재)
- 서버가 경로를 계산하고 별도 RPC로 클라이언트에 전송
- SerializeField 미할당, 타이밍 문제, 타일 충돌 등 복합 문제

### 올바른 방향
- **초기 이동(랠리 + Castle 접근)**: 클라이언트가 독립적으로 처리
  - 랠리포인트는 이벤트에 포함 → 서버/클라이언트 동일 입력 → 동일 경로(결정론적 A*)
  - `SpawnUnitClientRpc`가 `GameEvents.OnUnitProduced` 발행 → `ProductionTicker.OnUnitProduced` 실행
- **Siege 이동**: 서버 권위 유지 (원래 화면 불일치 원인)
  - `TickSiege`는 클라이언트에서 이동 명령 스킵 (`if (isClient) continue;`)
  - 서버가 `BroadcastMoveClientRpc`로 전송 (동적 탐색으로 SerializeField 의존 제거)
- **Siege RPC 수신 시 타일 충돌 방지**: `serverAuthoritative` 플래그로 재탐색 스킵

---

## 수정 전후 흐름 비교

### 수정 전 (깨진 상태)
```
서버: OnUnitProduced → BroadcastMoveIfServer → _networkMovement == null → 아무것도 안 함
클라이언트: SpawnUnitClientRpc → OnUnitProduced → IsNetworkClient → return
결과: 클라이언트 유닛 이동 없음
```

### 수정 후
```
서버: OnUnitProduced → unitView.MoveTo(path) (서버 시각 이동)
클라이언트: SpawnUnitClientRpc → GameEvents.OnUnitProduced → OnUnitProduced (독립 실행)
           → unitView.MoveTo(rallyPath) → MoveTowardEnemyCastle → 정상 이동
Siege: 서버 → BroadcastMoveClientRpc(동적탐색) → unitView.MoveTo(path, serverAuthoritative:true)
```

---

## 영향 받는 파일

- `Assets/_Project/Scripts/Presentation/Production/ProductionTicker.cs`
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnitMovementController.cs`
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
