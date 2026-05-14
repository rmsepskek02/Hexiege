# Research: 유닛 NGO NetworkObject 전환 및 이동 동기화

## 작업 목표

유닛을 NGO(Netcode for GameObjects)가 직접 관리하는 `NetworkObject`로 전환하여
HOST와 CLIENT 간 유닛 위치를 `NetworkTransform`으로 완전 동기화.
이동·공격·사망 상태 전환의 타이밍 불일치를 구조적으로 해결.

---

## 현재 구조 (문제)

### 유닛 생산 및 스폰 흐름

```
[서버]
UnitProductionUseCase → OnUnitProduced 이벤트
  → NetworkProductionController.OnUnitProduced()
    → SpawnUnitClientRpc(unitId, type, team, q, r, ...)
      → GameEvents.OnUnitSpawned
        → UnitFactory.CreateUnitObject()
          → Instantiate(prefab)  ← 일반 GameObject

[클라이언트]
SpawnUnitClientRpc 수신
  → unitSpawn.SpawnUnitWithId(...)
    → GameEvents.OnUnitSpawned
      → UnitFactory.CreateUnitObject()
        → Instantiate(prefab)  ← 서버와 독립적으로 생성
```

### 이동 흐름

```
[클라이언트 본인 유닛]
InputHandler → NetworkUnitMovementController.RequestMove()
  → 로컬 즉시 MoveTo() (클라이언트 예측)
  → RequestMoveServerRpc 전송

[서버]
RequestMoveServerRpc 수신 → 검증 → 서버 MoveTo()
  → SyncMovementClientRpc(unitId, pathQ[], pathR[]) 전송 (요청자 제외)

[상대 클라이언트]
SyncMovementClientRpc 수신 → MoveTo(path) 실행
```

### 근본 문제

서버와 클라이언트가 **각자 독립적으로 `MoveAlongPath` 코루틴을 실행**.
같은 경로를 실행해도 프레임 타이밍 차이로 두 머신의 유닛 위치가 항상 미세하게 다름.

이 위치 차이에서 파생되는 버그:
- 사거리 밖 공격 (서버/클라이언트 위치 기준 다름)
- 잘못된 공격 방향 (타겟 위치 불일치)
- Walk/Attack 전환 타이밍 불일치
- 전투 대기 중 클라이언트만 계속 이동하는 현상

---

## 유사 게임 참고

### Clash of Clans (Supercell) — 채택 방향
- 서버가 유닛 상태(위치, HP, 타겟)를 권위 있게 관리
- 클라이언트는 서버 데이터 기반으로 시각화
- **우리 방향**: NGO가 서버 위치를 클라이언트에 자동 동기화 (NetworkTransform)

---

## NGO NetworkObject 방식 설계

### 핵심 원칙

- 서버만 유닛의 `transform.position`을 직접 이동
- NGO `NetworkTransform`이 서버 위치를 모든 클라이언트에 자동 전달 + 보간
- 클라이언트는 위치를 NetworkTransform에서 받고, 애니메이션만 ClientRpc로 제어

### 새로운 유닛 스폰 흐름

```
[서버]
UnitProductionUseCase → OnUnitProduced 이벤트
  → UnitFactory.CreateUnitObject()
    → Instantiate(prefab)
    → NetworkObject.Spawn()  ← NGO가 클라이언트에 자동 전달
  → SpawnUnitClientRpc(unitId, type, team, q, r, ...)  ← UnitData 초기화 정보 전송

[클라이언트]
NGO가 자동으로 프리팹 Instantiate (NetworkObject 동기화)
SpawnUnitClientRpc 수신 → UnitData 생성 → UnitView 초기화 (GameObject는 이미 존재)
```

### 새로운 이동 흐름

```
[클라이언트 입력]
InputHandler → NetworkUnitMovementController.RequestMoveServerRpc 전송

[서버]
RequestMoveServerRpc 수신 → 검증 → 서버 UnitView.MoveTo()
  → MoveAlongPath 코루틴: transform.position 직접 갱신
  → NetworkTransform이 위치를 모든 클라이언트에 자동 동기화

[클라이언트]
NetworkTransform으로 위치 수신 + 보간 (별도 이동 코루틴 불필요)
Walk 애니메이션: StartWalkAnimationClientRpc / StopWalkAnimationClientRpc로 제어
```

---

## 변경 필요 파일 및 컴포넌트

### 신규 컴포넌트

| 컴포넌트 | 위치 | 역할 |
|---------|------|------|
| `NetworkObject` | 유닛 프리팹 루트 | NGO 네트워크 오브젝트 등록 |
| `NetworkTransform` | 유닛 프리팹 루트 | 서버 position → 클라이언트 자동 동기화 |

### NetworkManager 설정

- 유닛 프리팹 6개를 NetworkManager의 **Network Prefabs List**에 등록 필요
- NGO는 등록된 프리팹만 네트워크 스폰 가능

### 수정 파일

| 파일 | 변경 내용 |
|------|----------|
| `UnitFactory.cs` | 서버: `Instantiate` 후 `NetworkObject.Spawn()` 추가. 클라이언트: prefab 직접 Instantiate 대신 NGO 생성 오브젝트 찾아 초기화 |
| `NetworkProductionController.cs` | `SpawnUnitClientRpc`에서 prefab 생성 제거. UnitData 초기화만 담당 |
| `NetworkUnitMovementController.cs` | `SyncMovementClientRpc` 제거 (NetworkTransform이 위치 담당). 클라이언트 예측 제거 |
| `UnitView.cs` | `MoveTo()` 클라이언트 가드 (이미 추가됨). MoveAlongPath 서버 전용 유지 |
| `NetworkCombatController.cs` | Walk ClientRpc (이미 추가됨) 유지 |
| `GameEvents.cs` | `OnUnitWalkStarted/Stopped` (이미 추가됨) 유지 |

---

## 핵심 해결 과제

### [과제 1] 클라이언트에서 UnitView 초기화 타이밍

NGO가 클라이언트에 프리팹을 자동 생성하는 시점과
`SpawnUnitClientRpc`로 UnitData가 도착하는 시점이 다를 수 있음.

**해결**: `SpawnUnitClientRpc`에서 `UnitFactory.GetUnitObject()`로 오브젝트를 찾아 초기화.
NGO 오브젝트의 `NetworkObject.NetworkObjectId`와 우리 `unitId` 매핑이 필요.

→ 더 간단한 방법: `NetworkUnit` 컴포넌트를 프리팹에 추가.
  서버 스폰 시 unitId를 NetworkVariable로 동기화.
  클라이언트는 NetworkUnit.OnNetworkSpawn()에서 unitId를 확인 후 UnitData 대기.

### [과제 2] NetworkManager Prefab 등록

NGO는 `NetworkObject.Spawn()`에 사용할 프리팹이 반드시 NetworkManager의
`NetworkPrefabs` 리스트에 등록되어 있어야 함.

**해결**: Inspector에서 수동 등록 (에디터 스크립트로 자동화 가능).

### [과제 3] 클라이언트 예측 제거

현재 `NetworkUnitMovementController.RequestMove()`에서
클라이언트가 로컬 `MoveTo()`를 즉시 실행 (클라이언트 예측).
NGO 방식에서는 서버만 이동하므로 제거 필요.
→ 클릭 후 서버 처리까지 약간의 지연 발생 (네트워크 RTT).
→ 허용 가능한 수준 — RTS 장르에서 수백ms 반응성은 일반적.

### [과제 4] 부모 Transform 동기화

현재 유닛이 `[World]/Units` 하위에 배치됨.
NGO 스폰된 NetworkObject는 기본적으로 씬 루트에 생성됨.
→ 스폰 후 `transform.SetParent(unitParent)` 호출 필요.

---

## 유닛 상태 전환 및 타이밍

```
[Idle]
  │ 이동 명령 (서버 검증 후)
  ▼
[Moving]
  서버: MoveAlongPath 실행, transform.position 갱신
  클라이언트: NetworkTransform 위치 수신 + Walk ClientRpc 수신
  │
  │ 사거리 내 적 감지
  ▼
[AttackWait]
  서버: Lerp 중단, StopWalkClientRpc, TickCombat 공격 처리
  클라이언트: Walk 정지, TriggerAttackAnimationClientRpc 수신
  │
  │ 적 사망 / 사거리 이탈
  ▼
[Moving] (재개)

[Any] ──HP=0──▶ [Dead]
  서버: EntityDiedClientRpc → 클라이언트 사망 처리 + NGO Despawn
```

---

## Walk 애니메이션 동기화 (기존 작업 유지)

이미 구현된 내용:
- `GameEvents.OnUnitWalkStarted/Stopped` — Walk 상태 이벤트
- `UnitView.StartWalkAnimation() / StopWalkAnimation()` — 클라이언트 수신 메서드
- `NetworkCombatController.StartWalkAnimationClientRpc/StopWalkAnimationClientRpc` — 전파 RPC

NGO 전환 후에도 동일하게 사용.
