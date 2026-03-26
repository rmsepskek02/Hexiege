# Plan: 유닛 NGO NetworkObject 전환 및 이동 동기화

## 작업 범위

**이번 작업**: 유닛을 NGO NetworkObject로 전환하고, NetworkTransform 기반 이동·Walk·공격·사망 동기화 구축.

**이번 작업에서 제외 (후속 작업으로 분리)**:
- **HitDelay**: 유닛별 공격 애니메이션 타격 프레임에 맞춘 데미지 판정 지연.
  Pistoleer / Assault / Sniper 등 유닛마다 타격 프레임 위치가 달라
  Animation Event 기반으로 정밀하게 설계 필요.
  현재 작업(동기화 구조) 안정화 후 별도 진행.

---

## 설계 결정 요약

| 항목 | 결정 |
|------|------|
| 이동 위치 동기화 | NGO NetworkTransform (서버 position → 클라이언트 자동 보간) |
| Walk 애니메이션 동기화 | ClientRpc (이벤트 기반) |
| 공격 애니메이션 동기화 | ClientRpc (기존 유지) |
| 사망 동기화 | ClientRpc (기존 유지) |
| 게임 로직 실행 | 서버 전용 |
| 클라이언트 역할 | 애니메이션·렌더링 수신·표현만 담당 |
| 유닛 스폰 방식 | 서버: NetworkObject.Spawn() → NGO가 클라이언트에 자동 전달 |
| 클라이언트 예측 | 제거 (RTS 장르에서 수백ms 지연 허용 가능) |

---

## 유닛 상태 전환 및 타이밍

```
[Idle]
  │ 이동 명령
  ▼
[Moving]  ←── 서버: MoveAlongPath 실행, transform.position 갱신
               클라이언트: NetworkTransform 위치 수신 + StartWalkClientRpc 수신
  │ 이동 중 사거리 내 적 감지
  ▼
[AttackWait]  ←── 서버: Lerp 중단, StopWalkClientRpc 전송, TickCombat 공격 처리
                   클라이언트: Walk 정지, TriggerAttackAnimationClientRpc 수신
  │ 적 사망 / 사거리 이탈
  ▼
[Moving]  ←── 서버: Lerp 재개, StartWalkClientRpc 전송

[Moving / Idle / AttackWait]
  │ HP = 0
  ▼
[Dead]  ←── 서버: EntityDiedClientRpc 전송 후 NGO Despawn
             클라이언트: IsDead 설정 후 Destroy
```

**타이밍 제약**:
- Moving → AttackWait: Lerp 정지 즉시 `StopWalkClientRpc` 전송
- AttackWait → Moving: 적 감지 해제 즉시 `StartWalkClientRpc` 전송 후 Lerp 재개
- Any → Dead: 진행 중 이동/공격 코루틴 즉시 중단 후 `EntityDiedClientRpc`

---

## Step 1: 유닛 프리팹 — NetworkObject + NetworkTransform 추가 (Inspector 작업)

모든 유닛 프리팹 (Unit_Pistoleer, Unit_Assault, Unit_Sniper × 2팀 = 6개)에
NGO `NetworkObject` 및 `NetworkTransform` 컴포넌트 추가.

**추가 컴포넌트**:
- `NetworkObject` — NGO 네트워크 오브젝트 등록 (루트 오브젝트에 추가)
- `NetworkTransform` — 서버 position → 클라이언트 자동 보간 동기화

**NetworkTransform 설정값**:
- `Interpolate`: Interpolated (기본값) — 클라이언트 위치 보간 활성화
- `Sync Position X/Y/Z`: true
- `Sync Rotation Y`: true
- `In Local Space`: false (월드 좌표 기준)

**NetworkManager Prefabs 등록**:
- NGO는 NetworkObject.Spawn()에 사용할 프리팹이 반드시
  NetworkManager의 `NetworkPrefabs` 리스트에 등록되어야 함
- 유닛 프리팹 6개를 Inspector에서 수동 등록

**구현 방식**: 에디터 스크립트 (`AddNetworkTransformToPrefabs.cs`) 작성 후 사용자 실행
→ 메뉴: `Hexiege > Add NetworkTransform To Unit Prefabs`

---

## Step 2: UnitFactory — 서버 NGO 스폰으로 변경

**파일**: `UnitFactory.cs`

### 현재 구조
```
GameEvents.OnUnitSpawned → CreateUnitObject() → Instantiate(prefab)
// 서버·클라이언트 양쪽에서 각자 Instantiate 실행
```

### 변경 후 구조
```
[서버]
GameEvents.OnUnitSpawned → CreateUnitObject()
  → Instantiate(prefab)
  → NetworkObject.Spawn()   ← NGO가 클라이언트에 자동 전달
  → transform.SetParent(_unitParent)  ← 씬 계층 정리

[클라이언트]
NGO가 NetworkObject 프리팹 자동 Instantiate
SpawnUnitClientRpc 수신 → UnitView 초기화 (GameObject는 이미 존재)
```

### 변경 내용
- `CreateUnitObject()`:
  - 서버일 때만 Instantiate 실행 (`if (!NetworkContext.IsNetworkServer) return;` 또는 서버/싱글 분기)
  - `Instantiate` 후 `networkObject.Spawn()` 호출
  - 스폰 후 `transform.SetParent(_unitParent)` 호출

### 싱글플레이 호환성
- `NetworkContext.IsNetworkActive` 분기:
  - 싱글: 기존 Instantiate 방식 유지
  - 멀티: 서버만 Instantiate + NetworkObject.Spawn()

---

## Step 3: NetworkProductionController — UnitData 초기화 전용으로 변경

**파일**: `NetworkProductionController.cs`

### 현재 구조
```
SpawnUnitClientRpc(unitId, type, team, q, r, ...)
  → SpawnUnitWithId() → GameEvents.OnUnitSpawned 발행
    → UnitFactory.CreateUnitObject() → Instantiate(prefab)
```

### 변경 후 구조
```
SpawnUnitClientRpc(unitId, type, team, q, r, ...)
  → NetworkUnit 컴포넌트에 unitId 전달 OR UnitView 직접 초기화
  (prefab은 NGO가 이미 생성 완료)
```

### 변경 내용
- `SpawnUnitClientRpc`에서 `SpawnUnitWithId()` / `GameEvents.OnUnitSpawned` 발행 제거
- 클라이언트: NGO가 생성한 오브젝트를 찾아 UnitView 초기화만 수행

### UnitView 초기화 타이밍 해결 방안
NGO가 프리팹을 클라이언트에 자동 스폰하는 시점과
SpawnUnitClientRpc 도착 시점이 다를 수 있음.

**해결**: 유닛 프리팹에 `NetworkUnit` 컴포넌트 추가.
- 서버 스폰 시 `unitId`를 `NetworkVariable<int>`로 설정
- 클라이언트 `NetworkUnit.OnNetworkSpawn()`에서 unitId 확인 후 UnitView 초기화
- SpawnUnitClientRpc는 보조 데이터 전달용으로만 사용

---

## Step 4: NetworkUnitMovementController — 클라이언트 예측 및 SyncMovementClientRpc 제거

**파일**: `NetworkUnitMovementController.cs`

### 현재 구조 (제거 대상)
```
RequestMove()
  → 클라이언트: 로컬 즉시 MoveTo() (클라이언트 예측)
  → RequestMoveServerRpc 전송

RequestMoveServerRpc (서버)
  → 서버 MoveTo()
  → SyncMovementClientRpc(unitId, pathQ[], pathR[])

SyncMovementClientRpc (상대 클라이언트)
  → MoveTo(path) 실행
```

### 변경 후 구조
```
RequestMove()
  → RequestMoveServerRpc 전송만 수행 (로컬 예측 없음)

RequestMoveServerRpc (서버)
  → 서버 MoveTo()
  → NetworkTransform이 위치를 모든 클라이언트에 자동 전달
  (SyncMovementClientRpc 불필요)
```

### 변경 내용
- `RequestMove()`: 클라이언트 예측 `MoveTo()` 호출 제거
- `SyncMovementClientRpc` 메서드 제거
- `RequestMoveServerRpc`: `SyncMovementClientRpc` 호출 제거

---

## Step 5: UnitView — MoveAlongPath 서버 전용 유지 확인

**파일**: `UnitView.cs`

### 현재 상태 (이미 적용됨)
- `MoveTo()` 클라이언트 가드:
  ```csharp
  if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;
  ```
- `MoveAlongPath()` Walk 이벤트 발행 (서버 전용)
- `StartWalkAnimation()` / `StopWalkAnimation()` public 메서드

### NGO 전환 후 동작
- 서버: `MoveAlongPath()` 실행 → `transform.position` 직접 갱신
  → `NetworkTransform`이 모든 클라이언트에 자동 동기화
- 클라이언트: `MoveAlongPath()` 실행 안 함.
  `NetworkTransform` 보간 위치로 자동 표현.

**추가 변경 없음** — 기존 가드가 올바르게 작동.

---

## Step 6: NetworkCombatController — Walk ClientRpc 유지 확인

**파일**: `NetworkCombatController.cs`

### 현재 상태 (이미 적용됨)
- `StartWalkAnimationClientRpc(int unitId)` — Walk 애니메이션 시작
- `StopWalkAnimationClientRpc(int unitId)` — Walk 애니메이션 정지
- `GameEvents.OnUnitWalkStarted/Stopped` 구독 (서버 전용)

### NGO 전환 후 동작
- Walk RPC는 NGO 방식과 독립적으로 동작 → **변경 없음**

---

## 구현 순서

```
[1] Inspector: 유닛 프리팹 6개에 NetworkObject + NetworkTransform 추가
    (에디터 스크립트 작성 → 사용자 실행)
      ↓
[2] Inspector: NetworkManager Network Prefabs List에 유닛 프리팹 6개 등록
    (사용자 직접 수동 등록)
      ↓
[3] NetworkUnit 컴포넌트 신규 작성
    (unitId NetworkVariable, OnNetworkSpawn UnitView 초기화)
      ↓
[4] UnitFactory: 서버/싱글 분기
    - 멀티+서버: Instantiate → NetworkObject.Spawn() → SetParent
    - 싱글: 기존 Instantiate 유지
      ↓
[5] NetworkProductionController: SpawnUnitClientRpc UnitData 초기화 전용으로 변경
      ↓
[6] NetworkUnitMovementController: 클라이언트 예측 제거, SyncMovementClientRpc 제거
      ↓
[7] UnitView / NetworkCombatController: 기존 Walk RPC 구조 유지 확인
      ↓
[8] 컴파일 확인
      ↓
[9] 테스트: HOST·CLIENT 양측 이동/공격/사망 동기화 확인
```

---

## 위험 요소 및 고려사항

### NetworkTransform 보간 지연
- 기본 보간으로 클라이언트에서 유닛이 서버보다 약간 뒤처져 보일 수 있음
- NGO NetworkTransform의 `InterpolationBufferTickOffset` 조정으로 완화 가능

### Walk 애니메이션 + 위치 동기화 타이밍
- NetworkTransform 위치 업데이트와 Walk ClientRpc 도착 시점이 완전히 일치하지 않을 수 있음
- 미세한 불일치는 시각적으로 크게 부각되지 않을 것으로 예상
- 허용 불가 수준이면 NetworkAnimator 병행 검토

### 싱글플레이 호환성
- `NetworkContext.IsNetworkActive`로 싱글/멀티 분기 이미 존재
- 서버 전용 분기 추가 시 싱글플레이 코드 경로 보호 필수
- 싱글플레이: NetworkTransform 없음 → MoveTo는 기존대로 실행

### UnitView 초기화 타이밍
- NGO 프리팹 자동 스폰 시점 ≠ SpawnUnitClientRpc 도착 시점
- NetworkUnit.OnNetworkSpawn() 방식으로 해결 (Step 3 참고)

### 부모 Transform
- NGO 스폰된 NetworkObject는 기본적으로 씬 루트에 생성
- 스폰 후 `transform.SetParent(_unitParent)` 호출로 씬 계층 유지

---

## 아키텍처 제약 확인

- `UnitView.cs` — Presentation 레이어. NetworkBehaviour 상속 없음. `NetworkContext` 정적 홀더로 서버 여부 확인. ✅
- `NetworkCombatController.cs` — Infrastructure 레이어. NetworkBehaviour 허용. ✅
- `NetworkUnitMovementController.cs` — Infrastructure 레이어. NetworkBehaviour 허용. ✅
- `UnitFactory.cs` — Infrastructure 레이어. NetworkContext 정적 홀더 사용 가능. ✅
- `NetworkProductionController.cs` — Infrastructure 레이어. NetworkBehaviour 허용. ✅
- `NetworkUnit.cs` (신규) — Infrastructure 레이어. NetworkBehaviour 허용. ✅
- `UnitCombatUseCase.cs` — Application 레이어. Domain만 참조. 변경 없음. ✅

---

## 구현 완료 요약 (2026-03-26)

핵심 시스템은 동작 완료. 아래 버그들이 수정 과정에서 발생하고 해결됨:

| 버그 | 원인 | 해결 |
|------|------|------|
| NotServerException (SetParent) | 클라이언트에서 NetworkObject SetParent 시도 | 멀티+클라이언트 시 SetParent 스킵 |
| InvalidParentException | NGO NetworkObject를 일반 GameObject 하위에 배치 | SetParent 제거 (씬 루트에 생성) |
| CLIENT 좌표 불일치 | NetworkTransform이 서버(Blue) 도메인 좌표 그대로 전달 | NetworkUnit.LateUpdate()에서 Red 클라이언트 위치 보정 |
| Walk 애니메이션 미재생 (생성 직후) | StartWalkClientRpc 도착 시 유닛이 _unitObjects 미등록 | WaitForUnitId 폴링 + ApplyStartWalkWithRetry 1초 재시도 |
| 공격 포즈로 이동 | AttackWait 탈출 시 _attackCoroutine 미완료 상태 이동 재개 | `while (_attackCoroutine != null) yield return null` 추가 |
| 공격 후 Walk 방향 불일치 (간헐적) | _prevPosition이 공격 위치 기준으로 고정 | ResetMovementTracking() 추가 |

---

## 보류 항목 — 이동 전 회전 타이밍 개선

**현상**: 이동 전 회전이 선행되도록 구현했으나, 실기 테스트에서 회전보다 이동이 더 빠르게 느껴짐.

**현재 구현 상태**:
- `UnitView._rotationDuration = 0.3f` (0.15f에서 증가)
- `GameEvents.UnitFacingChangedEvent`에 `RotationDuration` 필드 추가 (서버→클라이언트 duration 동기화)
- `NetworkCombatController.TurnToFaceClientRpc(int unitId, float yAngle, float rotationDuration)` — 하드코딩 0.15f 제거
- 서버: `WaitForSeconds(_rotationDuration)` 대기 후 이동
- 클라이언트: `DORotate(yAngle, rotationDuration).SetEase(Ease.OutQuad)`

**원인 분석**:
- 클라이언트의 DORotate는 RPC 지연(RTT) 후에 시작 → 서버 이동보다 늦게 회전
- NetworkTransform 보간으로 이동 위치가 이미 도착해있는 상태에서 회전이 따라가는 시각 발생
- `_rotationDuration` 증가만으로는 해결 어려울 수 있음

**후속 작업 시 검토 방향**:
1. `_rotationDuration`을 더 늘리거나 `WaitForSeconds` 대기를 더 길게 설정
2. 클라이언트에서 이동 시작 전 강제 대기 추가 (NetworkTransform position 변화 감지 후 회전 완료 확인)
3. 이동-회전 타이밍을 완전히 서버 권위로 처리하는 구조 검토 (NetworkAnimator 병행)
