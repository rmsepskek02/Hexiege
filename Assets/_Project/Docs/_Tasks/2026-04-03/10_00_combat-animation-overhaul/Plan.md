# Plan: 전투/애니메이션 시스템 전면 재정비

작성일: 2026-04-03
작업 배경: 2주 이상 간헐적 버그가 지속됨. 규칙과 로직을 명확히 정의하고 구현을 정비함.

---

## 1. 확정된 6가지 규칙

### 규칙 1 — 적 감지 즉시 공격 애니메이션 전환
- 서버가 사거리 내 적을 감지하면 즉시 클라이언트에 알린다
- 클라이언트는 이 신호를 받는 즉시 Walk → Attack CrossFade (블렌드 적용)
- 50ms TickCombat을 기다리지 않는다

### 규칙 2 — 유닛별 개별 데미지 타이밍
- 공격 애니메이션 시작 신호와 동시에 서버에서 HitFrameTime 타이머 시작
- HitFrameTime 경과 후 서버에서 데미지 적용
- HitFrameTime: Assault=0.133s, Pistoleer=0.833s, Sniper=2.000s
- AttackCooldown(=클립 길이): Assault=0.2s, Pistoleer=2.0s, Sniper=3.0s

### 규칙 3 — 모든 애니메이션 전환은 반드시 블렌드
- Walk → Attack: CrossFade 0.08초
- Attack → Walk: CrossFade 0.10초
- 즉시 전환(블렌드 없음)은 허용하지 않음
- Walk 애니메이션은 이동 시작 시 1회만 시작, 매 타일마다 재시작 금지

### 규칙 4 — 공격 중 타겟이 소멸 또는 이탈해도 현재 공격 사이클 완료
- 적이 사거리를 벗어나도 현재 공격 애니메이션 사이클이 끝날 때까지 공격 상태 유지
- 사이클 완료 판단 기준: 공격 쿨다운 만료 (쿨다운 = 클립 길이로 설정되어 있음)
- 서버에서 Animator 상태를 읽지 않고 순수 타이머로 판단

### 규칙 5-1 — 타겟 소멸 + 사거리 내 다른 적 없음 → 진행중인 공격 애니메이션 완료 후 이동 재개
- 서버가 StopCombatClientRpc 전송
- 클라이언트: Attack → Walk CrossFade (블렌드 적용)
- 서버: 이동 경로 재탐색 후 MoveAlongPath 재시작

### 규칙 5-2 — 타겟 소멸 + 사거리 내 다른 적 있음 → 공격 방향만 전환
- 서버가 ChangeTargetClientRpc 전송 (타겟 ID 포함)
- 클라이언트: Attack 애니메이션 루프 유지, 새 타겟 방향으로 부드럽게 회전만 적용
- 애니메이션 재시작 없음

---

## 2. 서버/클라이언트 역할 분리 원칙

| 책임 | 담당 |
|------|------|
| 적 감지 (HasEnemyInRange) | 서버 |
| 타겟 결정 | 서버 |
| 위치/이동 (Transform) | 서버 → NetworkTransform → 클라이언트 보간 |
| 회전 (Rotation) | 서버 즉시 스냅 → NetworkTransform → 클라이언트 보간 |
| 데미지 계산 및 적용 | 서버 |
| 공격 사이클 완료 판단 | 서버 (쿨다운 타이머 기준) |
| 애니메이션 실행 | 클라이언트 (RPC 신호 수신 후 처리) |
| 애니메이션 블렌드 | 클라이언트 |

---

## 3. 전투 진입 신호 단일화

### 문제 (기존)
StartCombat 신호가 두 곳에서 발생:
- MoveAlongPath가 적 감지 즉시 OnUnitEnteredCombat 이벤트 발행
- TickCombat이 50ms마다 StartCombatClientRpc 전송

두 경로가 Unity 실행 순서(Update → 코루틴)에 따라 경쟁하며 딕셔너리 등록이 꼬여 간헐적으로 RPC가 무시됨.

### 해결 (신규)
**StartCombat 신호는 MoveAlongPath 단독 담당.**

TickCombat 역할을 다음으로 제한:
- 쿨다운 감소
- 데미지 코루틴 실행
- 타겟 변경 감지 → ChangeTargetClientRpc
- 전투 종료 감지 → StopCombatClientRpc

TickCombat에서 StartCombatClientRpc 전송 완전 제거.

---

## 4. Walk 애니메이션 단일 시작

### 문제 (기존)
MoveAlongPath 루프 안에서 매 타일(for문 순회)마다 Walk CrossFade를 호출함.
→ Walk 애니메이션이 매 칸마다 처음부터 재시작되어 공격 전환 시 블렌드 어색함.

### 해결 (신규)
이동 시작 시점에 딱 1회만 Walk CrossFade 호출.
이후 이동 루프 안에서 Walk 재시작 코드 제거.
Walk 애니메이션은 Loop 설정이므로 자동으로 루프됨.

---

## 5. 공격 사이클 완료 판단 — Animator → 쿨다운

### 문제 (기존)
서버에서 `WaitForAttackCycleEnd()` 코루틴이 Animator의 normalizedTime을 매 프레임 읽어 사이클 완료를 판단.
→ 서버와 클라이언트의 Animator 상태가 다를 수 있어 불안정.

### 해결 (신규)
`AttackCooldown = 클립 길이` 로 이미 설정되어 있음.
쿨다운이 만료되는 시점 = 애니메이션 한 사이클 완료 시점과 동일.
서버에서 쿨다운이 0이 될 때까지 yield return null로 대기.
`WaitForAttackCycleEnd()` 코루틴 제거.

---

## 6. 생산 직후 버그 원인 및 해결

### 원인
생산된 유닛이 첫 번째 타일로 이동을 시작하는 순간 적이 이미 사거리 안에 있는 경우:
- 서버: MoveAlongPath 시작과 동시에 StartCombatClientRpc 전송
- 클라이언트: NGO 비동기 스폰 구조상 NetworkObject 수신 → unitId NetworkVariable 수신이 별도 패킷으로 도착
- 현재 NetworkUnit.OnNetworkSpawn()에서 unitId가 아직 -1이면 WaitForUnitId() 코루틴으로 **매 프레임 폴링**하며 팩토리 등록 대기
- 폴링은 최소 1프레임 후에 감지되므로, RPC가 그 사이에 도착하면 GetUnitObject(unitId) = null → 처리 무시

### 근본 원인
`WaitForUnitId()` 가 폴링 방식이어서 등록 완료 전 RPC를 놓치는 것.
`OnValueChanged` 콜백은 값이 바뀌는 **그 프레임에 즉시** 호출되므로 폴링보다 빠르고 정확하다.

### 해결
`WaitForUnitId()` 폴링 코루틴 제거.
`_unitId.OnValueChanged` 콜백으로 교체.

- `OnNetworkSpawn()` 시점에 값이 이미 있으면 즉시 등록 (기존 로직 유지)
- 값이 아직 없으면 `_unitId.OnValueChanged += OnUnitIdReceived` 로 콜백 등록
- 콜백에서 팩토리 등록 후 콜백 해제 (1회성)

이렇게 하면 unitId가 도착하는 즉시 같은 프레임에 팩토리 등록 완료 → 이후 RPC도 정상 처리됨.

---

## 7. 생산 직후 버그 — 두 번째 방어선: StartCombat 재시도 코루틴

### OnValueChanged만으로 해결되지 않는 케이스

OnValueChanged 콜백은 unitId가 도착하는 **그 프레임에 즉시** 팩토리 등록을 완료한다.
그러나 NGO 패킷 순서는 보장되지 않는다.

| 패킷 도착 순서 | OnValueChanged 효과 |
|---------------|-------------------|
| unitId 먼저 → StartCombatRpc 나중 | ✅ 등록 완료 후 RPC 처리 → 정상 |
| StartCombatRpc 먼저 → unitId 나중 | ❌ RPC 도착 시점에 아직 미등록 → RPC 무시됨 |

두 번째 케이스에서는 OnValueChanged가 아무 도움이 되지 않는다.
StartCombatClientRpc가 수신된 시점에 등록 자체가 아직 일어나지 않았기 때문이다.

### 해결: ApplyStartCombatWithRetry 코루틴 추가

`StartWalkAnimationClientRpc`에 이미 적용된 패턴(`ApplyStartWalkWithRetry`)을 동일하게 적용.

- `StartCombatClientRpc` 수신 시 즉시 코루틴 시작
- `GetUnitObject(unitId)`가 null이면 최대 1초간 매 프레임 재시도
- 등록 완료되는 즉시 `StartCombatAnimation()` 호출

### 두 방어선의 역할 분담

| 방어선 | 구성 요소 | 해결하는 케이스 |
|--------|---------|--------------|
| 1차 | NetworkUnit.OnValueChanged | unitId 먼저 도착 → RPC 나중 도착 |
| 2차 | ApplyStartCombatWithRetry | RPC 먼저 도착 → unitId 나중 도착 |

두 개가 함께 있어야 모든 NGO 패킷 순서 시나리오를 커버한다.
OnValueChanged만 있으면 RPC가 unitId보다 먼저 도착하는 케이스를 놓친다.
Retry만 있으면 매 프레임 폴링이 발생하여 비효율적이다.

---

## 8. 규칙 위반 발견 목록 (2026-04-03 점검)

전체 코드 점검 결과 발견된 규칙 위반 6건.

### 위반 1 — 규칙 2: HOST TryAttack 즉시 데미지 [심각도: 높음]

**파일:** `UnitCombatUseCase.cs:69`

**문제:**
```csharp
// 현재 (잘못됨)
if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return null;
```
HOST는 IsNetworkServer=true이므로 조건을 통과 → TryAttack이 HitFrameTime 없이 즉시 데미지 적용.
결과: HOST에서 데미지가 두 번 들어감 (TryAttack 즉시 1회 + DelayedAttackDamage 2초 후 1회).

**수정:**
```csharp
// 수정 후: 네트워크 모드에서는 서버/클라이언트 구분 없이 TryAttack 완전 차단
if (NetworkContext.IsNetworkActive) return null;
```

---

### 위반 2 — 규칙 1: 쿨다운 중 적 감지 시 StartCombatClientRpc 미전송 [심각도: 높음]

**파일:** `NetworkCombatController.cs:361~371`

**문제:**
OnUnitEnteredCombatHandler에서 TryFindTarget이 null(쿨다운 중)이면 HasEnemyInRange 재확인 후 return.
쿨다운 중에도 사거리 내 적이 있으면 StartCombatClientRpc를 즉시 전송해야 하는데 전송하지 않음.
결과: 클라이언트가 Walk 상태를 유지하다가 쿨다운이 끝난 후에야 Attack 전환 → 최대 AttackCooldown만큼 지연.

**수정:**
HasEnemyInRange가 true이면 쿨다운 여부와 무관하게 StartCombatClientRpc 즉시 전송.
단, 쿨다운 중일 때는 타겟 ID를 모르므로 타겟 ID 없이 신호만 전송하는 별도 처리 필요.

---

### 위반 3 — 규칙 4: 공격 사이클 판단에 HasEnemyInRange 혼용 [심각도: 높음]

**파일:** `UnitView.cs:447~467`

**문제:**
```
while (HasEnemyInRange) → while (Cooldown > 0) → if (HasEnemyInRange) continue
```
HasEnemyInRange는 실시간 거리 계산이므로 Lerp 이동 중 예측 불가능한 시점에 false가 될 수 있음.
"공격 사이클 완료" 판단에 HasEnemyInRange가 끼어들면, 쿨다운이 남아있어도 루프 구조상 의도치 않은 분기 발생 가능.

**수정:**
"현재 공격 사이클 완료" 판단은 순수 타이머(AttackCooldownRemaining ≤ 0)만 사용.
HasEnemyInRange는 사이클 완료 후 "이동 재개 vs 공격 유지" 판단에만 사용.

---

### 위반 4 — 규칙 5-2: 쿨다운 중 타겟 소멸 시 방향 전환 미동작 [심각도: 중간]

**파일:** `NetworkCombatController.cs:234~242`

**문제:**
TickCombat에서 타겟 변경 감지는 TryFindTarget 성공 시에만 작동.
TryFindTarget은 쿨다운 체크 포함 → 쿨다운 중이면 null → 타겟 변경 감지 블록에 진입 불가.
결과: 타겟 소멸 직후 쿨다운 중이면 다른 적이 사거리 내에 있어도 방향 전환 미동작.

**수정:**
쿨다운과 무관하게 사거리 내 가장 가까운 적을 탐색하는 별도 메서드로 타겟 변경 감지.

---

### 위반 5 — 규칙 3: 이미 Walk 상태에서 Walk CrossFade 재호출 [심각도: 중간]

**파일:** `UnitView.cs:469~475`

**문제:**
전투 종료 후 이동 재개 시 CrossFadeInFixedTime(StateWalk)를 무조건 호출.
이미 Walk 상태인 경우에도 호출되면 Walk 루프가 처음부터 재시작 → 모션 끊김.

**수정:**
현재 Animator 상태를 확인하여 이미 Walk이면 CrossFade를 호출하지 않고 speed만 1로 복원.

---

### 위반 6 — 규칙 3: StopCombatAnimation() 의도 불명확 [심각도: 낮음]

**파일:** `UnitView.cs`

**최초 관찰:**
StopCombatAnimation()이 비어있어 Attack→Walk 전환이 명시적으로 보이지 않음.

**수정 시도 및 철회:**
Walk CrossFade를 StopCombatAnimation()에 추가했으나, 서버가 쿨다운 대기 중인 동안
클라이언트만 Walk 애니메이션이 재생되어 제자리 걷기 현상이 발생함.

**올바른 설계:**
빈 메서드가 의도된 설계임. Walk CrossFade는 `StartWalkAnimationClientRpc`에서 처리.
StopCombatAnimation()은 "전투 종료" 신호를 받았음을 표시하는 용도이며,
실제 애니메이션 전환은 서버가 이동을 재개하는 시점에 연동됨.

**수정:**
빈 메서드 유지 + 의도를 명확히 설명하는 주석 추가.

---

### 구현 중 발생한 제자리 걷기 현상 분석 (2026-04-03)

**증상:**
타겟 사망 후 유닛이 최대 3초(Sniper 기준 AttackCooldown = 클립 길이) 동안 제자리에서 **Walk 애니메이션을 재생한 뒤** 이동 재개.

**원인 — 위반 6 수정의 부작용:**
위반 6 수정으로 `StopCombatAnimation()`에 Walk CrossFade를 추가했는데,
이 메서드는 서버 이동 재개 여부와 무관하게 클라이언트에서 즉시 호출됨.

흐름:
```
타겟 사망
  → 서버: while (AttackCooldownRemaining > 0) 쿨다운 대기 중 (규칙 4)
  → 서버: StopCombatClientRpc 전송
  → 클라이언트: StopCombatAnimation() → Walk CrossFade 즉시 재생  ← 문제
  → 서버: 아직 쿨다운 대기 → NetworkTransform 위치 변화 없음
  → 결과: Walk 애니메이션 재생 + 이동 없음 = 제자리 걷기
```

**올바른 흐름 (규칙 기준):**
```
타겟 사망
  → 서버: while (AttackCooldownRemaining > 0) 쿨다운 대기 (규칙 4)
  → 서버: 쿨다운 만료 → 이동 재개 → StartWalkAnimationClientRpc 전송
  → 클라이언트: StartWalkAnimationClientRpc 수신 → Walk CrossFade
```
Walk CrossFade는 서버가 실제로 이동을 시작하는 시점에 한 번만 발생해야 함.
`StopCombatAnimation()`은 의도적으로 빈 메서드로 유지.

**수정:**
`StopCombatAnimation()`을 빈 메서드로 복원. Walk는 `StartWalkAnimationClientRpc` 도착 시에만 시작.

**규칙 재확인:**
처음에 이 현상을 "타겟 사망 후 즉시 이동 재개가 안 되는 버그(규칙 5-1 위반)"로 오판했으나,
규칙 4와 규칙 5-1을 확인한 결과 타겟 소멸/이탈 **무관하게** 현재 공격 사이클 완료 후 이동 재개가 올바른 규칙.
쿨다운 대기 자체는 의도된 동작이며, Walk 애니메이션이 서버 이동보다 먼저 재생된 것이 실제 버그였음.

---

## 11. 추가 발견 버그 (2026-04-04)

### 버그 A — TickCombat/핸들러 실행 순서 경쟁으로 StartCombatClientRpc 미전송 [심각도: 높음]

**증상:** 사거리 내 적 감지 시 공격 애니메이션으로 전환되지 않고 Walk 상태 그대로 제자리에서 걷는 현상 (간헐적).

**원인:**
Unity Update() → 코루틴 재개 순서에 의한 같은 프레임 내 경쟁 조건.

```
[같은 프레임]
Update(): TickCombat
          → TryFindTarget 성공 → _unitCombatTargets[A] = B 등록 ← 먼저 실행
Coroutine: MoveAlongPath HasEnemyInRange 감지
          → OnUnitEnteredCombatHandler 호출
          → if (_unitCombatTargets.ContainsKey(A)) return; ← true! → RPC 미전송
```

TickCombat이 먼저 `_unitCombatTargets`에 등록하면, 핸들러의 `ContainsKey` 가드가 이미 전투 중으로 판단하고 `StartCombatClientRpc`를 보내지 않음.
클라이언트는 Attack 애니메이션으로 전환 신호를 받지 못해 Walk를 유지.

**수정 방향:**
`_unitCombatTargets`(타겟 추적용)와 별도로 `_combatAnimationSent` HashSet(RPC 전송 여부 추적용)을 분리.
핸들러의 `ContainsKey` 가드를 `_combatAnimationSent`로 교체.
StopCombat 정리 시 두 컬렉션 모두 Remove.

---

### 버그 B — 첫 ExecuteAttack 50ms 오프셋으로 애니메이션-쿨다운 사이클 불일치 [심각도: 중간]

**증상:** 타겟 소멸 후 공격 애니메이션이 한 사이클 더 재생된 뒤 도중에 Walk로 전환.

**원인:**
`StartCombatClientRpc`는 `OnUnitEnteredCombatHandler`에서 T=0에 전송되지만,
`ExecuteAttack`(쿨다운 리셋)은 그 다음 TickCombat(T≈50ms)에서 처음 실행됨.

```
T=0:    StartCombatClientRpc → 클라이언트 애니메이션 시작
T=50ms: TickCombat → ExecuteAttack → 쿨다운 = AttackCooldown (3s)

서버 사이클: T=0.05 → 3.05 → 6.05 ...
애니메이션:  T=0    → 3.0  → 6.0  ...

타겟 소멸 후 Walk: T=3.05 (애니메이션 루프 경계 T=3.0에서 50ms 지난 뒤)
→ 클라이언트 입장에서 공격 애니메이션이 루프 직후 도중에 Walk로 전환되는 것처럼 보임
```

**수정 방향:**
`OnUnitEnteredCombatHandler`에서 `StartCombatClientRpc`와 동시에 `ExecuteAttack`도 호출.
서버 공격 사이클이 T=0에서 시작 → 애니메이션 루프와 동기화.
쿨다운이 0일 때(`attackResult.HasValue`)에만 `ExecuteAttack` 호출 (쿨다운 중 진입 시는 해당 없음).

---

## 9. 수정할 파일 목록

| 파일 | 수정 내용 | 상태 |
|------|----------|------|
| `NetworkUnit.cs` | WaitForUnitId() 폴링 코루틴 제거 / OnValueChanged 콜백으로 교체 | ✅ 완료 |
| `UnitView.cs` | Walk CrossFade를 이동 시작 1회로 제한 / WaitForAttackCycleEnd() 제거 / 쿨다운 대기 방식으로 교체 | ✅ 완료 |
| `NetworkCombatController.cs` | TickCombat에서 StartCombatClientRpc 제거 / OnUnitEnteredCombatHandler 단독 담당 | ✅ 완료 |
| `NetworkCombatController.cs` | StartCombatClientRpc에 ApplyStartCombatWithRetry 재시도 코루틴 추가 | ✅ 완료 |
| `UnitCombatUseCase.cs` | TryAttack 네트워크 차단 조건 수정 (HOST 즉시 데미지 버그 — 위반 1) | ✅ 완료 |
| `NetworkCombatController.cs` | 쿨다운 중 적 감지 시 StartCombatClientRpc 전송 추가 (위반 2) | ✅ 완료 |
| `UnitView.cs` | Walk 재호출 방지 / _attackToWalkBlend 추가 / StopCombatAnimation 원복 (위반 3,5,6) | ✅ 완료 |
| `NetworkCombatController.cs` | 쿨다운 중 타겟 변경 감지 로직 수정 (위반 4) | ✅ 완료 |
| `UnitView.cs` | StopCombatAnimation() 빈 메서드로 복원 | ✅ 완료 |
| `UnitView.cs` | 타겟 사망/이탈 구분 작업 | ❌ 취소 — 규칙 4가 두 경우 모두 커버함 |
| `NetworkCombatController.cs` | TickCombat 쿨다운 감소를 실제 경과 시간(elapsed)으로 교체 / 오버슈트 이월 | ✅ 완료 |
| `UnitStats.cs` | GetAttackCooldown 값을 실제 클립 길이로 업데이트 | ✅ 완료 |
| `NetworkCombatController.cs` | _combatAnimationSent HashSet 추가 / ContainsKey 가드 교체 (버그 A) | ✅ 완료 |
| `NetworkCombatController.cs` | OnUnitEnteredCombatHandler에서 ExecuteAttack 동시 호출 (버그 B) | ✅ 완료 |

---

## 10. 변경하지 않는 것

- 데미지 적용 로직 (ApplyAttackDamage, DelayedAttackDamage)
- ChangeTargetClientRpc / StopCombatClientRpc 로직
- NetworkTransform 기반 위치/회전 동기화
- 유닛 스탯 (AttackCooldown, HitFrameTime)
- 싱글플레이 전투 흐름
- ApplyStartWalkWithRetry 재시도 코루틴 (Walk RPC는 등록 직후 도착할 수 있으므로 유지)
