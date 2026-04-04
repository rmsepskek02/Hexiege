# Plan: Initialize()가 TurnToFaceClientRpc 결과를 덮어쓰는 버그 수정

> **상태:** 미해결 (2026-03-29)
> 수정은 적용되었으나 시각적 개선 없음. 상세 내용은 `../15_00_deferred-red-client-bugs/BugReport.md` 참조.

## 수정 전략

`TurnToFaceClientRpc`가 이미 실행된 유닛에 대해 `Initialize()`가 rotation을 덮어쓰지 않도록
`NetworkUnit`에 플래그를 추가한다.

---

## 수정 파일 목록

### 1. `NetworkUnit.cs`

**추가할 내용:**

```csharp
/// <summary>
/// 이 유닛에 TurnToFaceClientRpc가 수신되었는지 여부.
/// Initialize()가 나중에 도착해도 rotation을 덮어쓰지 않도록 보호하는 플래그.
///
/// 설정 시점: TurnToFaceClientRpc에서 ResetMovementTracking() 직후
/// 해제 시점: ResetMovementTracking() 호출 시 (다음 이동 사이클 시작 전)
/// </summary>
public bool HasReceivedTurnToFace { get; private set; }

/// <summary>
/// TurnToFaceClientRpc 수신 완료 플래그 설정.
/// NetworkCombatController.TurnToFaceClientRpc에서 호출.
/// </summary>
public void MarkTurnToFaceReceived() { HasReceivedTurnToFace = true; }
```

**ResetMovementTracking()에 추가:**
```csharp
public void ResetMovementTracking()
{
    _hasInitialPosition = false;
    _isPreRotating = false;
    _lateUpdateLogCount = 0;
    HasReceivedTurnToFace = false;  // ← 추가
}
```

---

### 2. `NetworkCombatController.cs` — TurnToFaceClientRpc

`ResetMovementTracking()` 호출 직후 `MarkTurnToFaceReceived()` 추가:

```csharp
networkUnit?.ResetMovementTracking();
networkUnit?.MarkTurnToFaceReceived();   // ← 추가
networkUnit?.SetPreRotating(true);
```

---

### 3. `UnitView.cs` — Initialize()

rotation 설정 전에 `HasReceivedTurnToFace` 플래그를 확인하여 조건부 적용:

```csharp
// _networkUnit을 먼저 캐시 (기존 코드 — 이미 Initialize 내부에 있음)
_networkUnit = GetComponent<NetworkUnit>();

int index = (int)_unitData.Facing;
if (index >= 0 && index < DirectionAngles.Length)
{
    // TurnToFaceClientRpc가 이미 이 유닛의 rotation을 설정했으면 덮어쓰지 않음.
    // 다른 NetworkObject의 RPC는 도착 순서가 보장되지 않으므로,
    // SpawnUnitClientRpc(→Initialize)가 TurnToFaceClientRpc보다 늦게 도착할 수 있음.
    if (_networkUnit != null && _networkUnit.HasReceivedTurnToFace)
    {
        Debug.Log($"[Initialize] unitId={_unitData.Id} TurnToFace 이미 수신됨 — rotation 설정 스킵");
    }
    else
    {
        float spawnAngle = DirectionAngles[index];
        if (ViewConverter.IsFlipped)
            spawnAngle = (spawnAngle + 180f) % 360f;
        Debug.Log($"[Initialize] unitId={_unitData.Id} IsFlipped={ViewConverter.IsFlipped} facing={_unitData.Facing} spawnAngle={spawnAngle}");
        transform.DOKill();
        transform.rotation = Quaternion.Euler(0f, spawnAngle, 0f);
    }
}
```

---

## 변경 흐름 정리

### 정상 케이스 (SpawnUnit → TurnToFace 순서)
```
1. SpawnUnitClientRpc → Initialize()
   HasReceivedTurnToFace=false → rotation=spawnAngle 설정 (기존 동작 유지)
2. TurnToFaceClientRpc
   ResetMovementTracking() + MarkTurnToFaceReceived(true) → DORotate(visualAngle)
```

### 버그 케이스 수정 후 (TurnToFace → SpawnUnit 순서)
```
1. TurnToFaceClientRpc
   ResetMovementTracking() + MarkTurnToFaceReceived(true) → DORotate(visualAngle)
2. SpawnUnitClientRpc → Initialize()
   HasReceivedTurnToFace=true → rotation 설정 스킵 ✓ (DORotate 결과 보존)
```

---

## 위험 요소

| 위험 | 내용 | 대응 |
|------|------|------|
| 싱글플레이 영향 | `_networkUnit=null`이면 기존 동작 그대로 | `_networkUnit != null &&` 조건으로 안전 처리 |
| Blue host 영향 | `LateUpdate`에서 `IsServer=true`로 조기 리턴 | 영향 없음 |
| ResetMovementTracking 과호출 | 다음 이동 사이클에서 플래그가 남아있을 경우 | ResetMovementTracking()에서 false로 초기화하여 방지 |

---

## Inspector 작업

없음.

---

## 진단 로그 정리 계획

수정 후 동작 확인이 완료되면 아래 Debug.Log 제거:
- `NetworkCombatController.cs` — `[TurnToFace]`, `[TurnToFace OnComplete]`
- `NetworkUnit.cs` — `[LateUpdate]` 5회 제한 로그
- `UnitView.cs` — `[Initialize]`
