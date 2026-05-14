# Research: Initialize()가 TurnToFaceClientRpc 결과를 덮어쓰는 버그

## 작업 배경

Red 클라이언트에서 유닛이 이동 중 잘못된 방향을 향하는 버그.
TurnToFaceClientRpc가 올바른 각도(150°)로 회전을 완료했음에도,
이후 spawn rotation(270°)으로 강제 리셋되는 현상.

---

## 버그 재현 조건

- 멀티플레이 (Host=Blue, Client=Red)
- Red 클라이언트에서 유닛 이동 시작 관찰
- 첫 번째 유닛은 정상, 이후 유닛들은 잘못된 방향

---

## 로그 분석 (실측)

```
[TurnToFace OnComplete] unitId=0 finalAngle=150.0   ← DORotate 150°로 완료
[LateUpdate] unitId=0 computedAngle=150.0 currentRot=270.0  ← 270°로 리셋됨!
```

- TurnToFaceClientRpc → DORotate → OnComplete(150°) 정상 실행 확인
- 그러나 직후 LateUpdate에서 currentRot=270° 감지
- 270° = DirectionAngles[E]=90 + Red flip 180° = Initialize()의 spawn rotation 값과 일치

---

## 근본 원인

### RPC 도착 순서 보장 없음 (다른 NetworkObject 간)

서버가 유닛 생성 시 두 종류의 RPC를 전송:

| RPC | NetworkObject | 역할 |
|-----|--------------|------|
| `SpawnUnitClientRpc` | NetworkUnitSpawnController | Initialize() 호출 → spawn rotation 설정 |
| `TurnToFaceClientRpc` | NetworkCombatController | DORotate → 이동 방향으로 회전 |

NGO는 **동일 NetworkObject 내 RPC 순서만 보장**한다.
서로 다른 NetworkObject의 RPC는 도착 순서가 비결정적이므로,
`TurnToFaceClientRpc`가 먼저 처리되고 `SpawnUnitClientRpc`가 나중에 도착하는 경우 발생.

### 버그 시나리오

```
[클라이언트 수신 순서 — 비정상 케이스]
1. TurnToFaceClientRpc 수신
   → ResetMovementTracking() → SetPreRotating(true) → DORotate(150°)
2. DORotate 0.15초 후 완료 → OnComplete: finalAngle=150° → SetPreRotating(false)
3. SpawnUnitClientRpc 수신 (늦게 도착)
   → Initialize() 호출
   → transform.DOKill()  [진행 중인 DOTween 없음, 무해하지만]
   → transform.rotation = Quaternion.Euler(0, 270, 0)  ← 150° 덮어씀!
4. LateUpdate: _isPreRotating=false, _hasInitialPosition=false (ResetPositionTracking 후)
   → 첫 프레임은 위치만 저장
   → 다음 프레임부터 delta 기반 회전 계산 시작
   → 270°에서 서서히 150° 방향으로 회전 중 (간헐적 회전 관찰됨)
```

### 왜 첫 번째 유닛은 정상인가

첫 번째 유닛은 스폰 시점부터 약간의 시간 여유가 있어 `SpawnUnitClientRpc`가
`TurnToFaceClientRpc`보다 먼저 안정적으로 도착함.
이후 유닛들은 배틀 진행 중 빠르게 스폰되어 두 RPC가 거의 동시에 전송/수신됨.

---

## 영향 범위

### 직접 영향
- `UnitView.Initialize()` — spawn rotation 설정 로직
- `NetworkUnit` — 플래그 관리
- `NetworkCombatController.TurnToFaceClientRpc` — 플래그 설정 위치

### 간접 확인 필요
- 싱글플레이에서는 NetworkUnit이 없으므로 영향 없음 (Initialize() 조건 분기 필요)
- Blue host는 LateUpdate 건너뜀(IsServer=true) + 직접 ApplyDirection 사용 → 영향 없음

---

## 관련 코드 위치

| 파일 | 라인 | 내용 |
|------|------|------|
| `UnitView.cs` | 160~188 | Initialize() — spawn rotation 설정 (문제 지점) |
| `NetworkUnit.cs` | 195~230 | ResetMovementTracking / ResetPositionTracking |
| `NetworkCombatController.cs` | 464~503 | TurnToFaceClientRpc |

---

## 부가 관찰 사항 (이번 작업 범위 외)

- unitId=2의 마지막 LateUpdate: `computedAngle=-210.0` — Atan2의 ±180° 불연속점 통과
  → RotateTowards가 쿼터니언 기반이므로 시각적 문제 없음, 무시 가능
- unitId=1, 3에 TurnToFace 로그 없음 → 이 유닛들은 spawn rotation(270°)에서 LateUpdate가 올바른 방향으로 수렴하며 동작 중 (정상 케이스로 판단)
