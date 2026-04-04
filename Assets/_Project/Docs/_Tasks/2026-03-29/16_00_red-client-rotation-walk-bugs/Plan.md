# Plan: Red Client 유닛 시각 동기화 버그 (BUG-A, BUG-B)

**작성일:** 2026-03-29
**Research 참조:** `Research.md`

---

## BUG-A 수정 계획: Animator Write Defaults → rotation 리셋 문제

### 접근법: 이중 방어 (Write Defaults OFF + LateUpdate 강제 적용)

Write Defaults를 OFF로 바꾸는 것만으로도 해결될 가능성이 높지만, 향후 Animator Controller 수정 시 재발 방지를 위해 코드 레벨 방어도 함께 적용.

### 수정 1: Animator Controller Write Defaults OFF

**대상 파일:**
- `Animations/Units/Assault/Assault.controller`
- `Animations/Units/Sniper/Sniper.controller`
- `Animations/Units/Pistoleer/Pistoleer.controller`

**작업:** Editor 스크립트로 모든 상태의 `m_WriteDefaultValues`를 0으로 일괄 변경.

**방법:** Unity Editor 메뉴 스크립트 작성 → 사용자가 실행 → 자동 적용.

**위험 요소:**
- Write Defaults OFF로 전환 시, 이전에 Write Defaults에 의존하던 프로퍼티(scale, position 등)가 예상과 다르게 동작할 수 있음
- Idle 상태 진입 시 Walk 포즈가 남을 수 있음 → CrossFade 블렌딩으로 커버 (현재 이미 CrossFade 사용 중)
- Dead 상태 진입 시 이전 상태 포즈 잔류 가능 → AnyState→Dead 트랜지션이 이미 있으므로 정상 동작 예상

### 수정 2: NetworkUnit.LateUpdate에서 Animator 리셋 방어 (코드 레벨 보험)

현재 LateUpdate의 delta 기반 RotateTowards는 720°/s로 매 프레임 ~12° 이동. Animator가 매 프레임 리셋하면 수렴이 극히 느려짐.

**변경:** DORotate OnComplete 직후 rotation을 `_lastDORotateAngle`에 저장하고, LateUpdate에서 Animator 리셋을 감지하면 즉시 해당 각도로 스냅.

**수정 대상:** `NetworkUnit.cs`

```
추가 필드:
  - private float _lastConfirmedAngle = -1f;  // DORotate 또는 Initialize 완료 후 확정 각도
  - _isPreRotating 해제 시점에 현재 rotation을 _lastConfirmedAngle에 저장

LateUpdate 변경:
  - _isPreRotating==false && _hasInitialPosition==false (아직 이동 시작 전) 구간에서:
    현재 rotation이 _lastConfirmedAngle과 5° 이상 차이나면 → 즉시 _lastConfirmedAngle로 스냅
  - 이동 시작 후(delta 기반 회전 활성)에는 기존 RotateTowards 유지
```

**위험 요소:**
- _lastConfirmedAngle이 올바르게 갱신되지 않으면 잘못된 방향으로 고정될 수 있음
- Write Defaults OFF가 정상 적용되면 이 방어 코드는 사실상 동작하지 않음 (보험 역할)

### 수정 3: TurnToFaceClientRpc DORotate를 LateUpdate 타이밍으로 변경

DOTween은 기본적으로 Update에서 실행되어 Animator Internal Animation Update보다 먼저 동작.
DORotate를 `.SetUpdate(UpdateType.Late)`로 변경하면 LateUpdate에서 실행되어 Animator 리셋 이후에 적용.

**수정 대상:** `NetworkCombatController.cs` TurnToFaceClientRpc

```
변경 전:
  unitObj.transform.DORotate(...).SetEase(Ease.OutQuad).OnComplete(...)

변경 후:
  unitObj.transform.DORotate(...).SetEase(Ease.OutQuad).SetUpdate(UpdateType.Late).OnComplete(...)
```

**수정 대상:** `UnitView.cs` PlayAttackAnimation, ApplyDirection

동일하게 `.SetUpdate(UpdateType.Late)` 추가.

**위험 요소:**
- Write Defaults OFF가 적용되면 불필요한 변경이 됨
- 하지만 Animator가 rotation을 건드리는 어떤 상황에서든 방어 가능
- DOTween의 Late 업데이트는 Animator 평가 이후에 실행되므로 항상 DOTween이 "마지막 말"을 함

---

## BUG-B 수정 계획: 공격 이력 유닛의 Walk-in-place 현상

### 접근법: 상태 머신 단순화 + 명확한 우선순위

### 수정 4: TriggerAttackAnimation에서 _isWalkPending 초기화

**수정 대상:** `UnitView.cs` TriggerAttackAnimation

```
변경:
  public void TriggerAttackAnimation(int targetId, bool targetIsUnit)
  {
      ...
      if (_attackCoroutine != null)
          StopCoroutine(_attackCoroutine);

      _isWalkPending = false;  // ★ 추가: 새 공격 시작 시 Walk 보류 해제
      ...
  }
```

**이유:** 새 공격이 시작되면 이전 공격의 Walk 보류 상태는 무의미. 새 공격 완료 후 서버가 다시 Walk RPC를 보내므로 안전.

### 수정 5: PlayAttackAnimation의 1프레임 대기 제거

**수정 대상:** `UnitView.cs` PlayAttackAnimation

```
변경 전:
  if (_isWalkPending)
  {
      _isWalkPending = false;
      yield return null;  // 1프레임 대기
      if (_attackCoroutine == null)
          StartWalkAnimation();
  }

변경 후:
  // _isWalkPending 처리 제거 — 서버의 Walk RPC에 의존
  // 공격 완료 후 Walk 재개는 서버가 OnUnitWalkStarted를 발행하여
  // StartWalkAnimationClientRpc로 클라이언트에 전달
```

**이유:**
- _isWalkPending 패턴은 클라이언트가 자체적으로 Walk를 복구하려는 시도
- 하지만 서버가 전투 루프 탈출 시 반드시 OnUnitWalkStarted를 발행하므로, 클라이언트가 자체 복구할 필요 없음
- 이 패턴을 제거하면 "제자리 Walk" 타이밍 갭 자체가 사라짐

**위험 요소:**
- 서버의 OnUnitWalkStarted 발행이 지연되면 클라이언트에서 Walk 시작이 늦어질 수 있음
- 하지만 현재도 서버가 전투 루프 탈출 즉시 이벤트를 발행하므로 실질적 지연은 RPC 전송 시간(~50ms) 이내

### 수정 6: StopWalkAnimation에서 _isWalkPending 정리 보강

현재 이미 `_isWalkPending = false`를 설정하고 있어 추가 변경 불필요.
수정 5에서 _isWalkPending 자체를 미사용으로 만들면 이 부분도 정리 대상.

---

## 수정 우선순위 및 순서

```
[1단계] 수정 1: Write Defaults OFF (Editor 스크립트) ← BUG-A 핵심
[2단계] 수정 3: DORotate에 SetUpdate(UpdateType.Late) 추가 ← BUG-A 코드 방어
[3단계] 수정 4+5: _isWalkPending 정리 ← BUG-B 핵심
[4단계] 수정 2: _lastConfirmedAngle 스냅 (선택) ← BUG-A 추가 보험

→ 1단계 + 2단계만으로 BUG-A 해결 기대
→ 3단계로 BUG-B 해결 기대
→ 4단계는 테스트 후 필요 시 적용
```

---

## 수정 파일 요약

| 파일 | 수정 내용 | 단계 |
|------|----------|------|
| `Animations/Units/*/Controller` | Write Defaults OFF (Editor 스크립트) | 1 |
| `NetworkCombatController.cs` | TurnToFaceClientRpc DORotate에 SetUpdate(Late) | 2 |
| `UnitView.cs` | ApplyDirection, PlayAttackAnimation DORotate에 SetUpdate(Late) | 2 |
| `UnitView.cs` | TriggerAttackAnimation에 _isWalkPending=false 추가 | 3 |
| `UnitView.cs` | PlayAttackAnimation의 _isWalkPending 블록 제거 | 3 |
| `NetworkUnit.cs` | _lastConfirmedAngle 스냅 로직 (선택) | 4 |

---

## 위험 요소 및 대응

| 위험 | 대응 |
|------|------|
| Write Defaults OFF 시 애니메이션 포즈 잔류 | CrossFade 블렌딩이 이미 적용되어 있어 자연스러운 전환 기대. 테스트로 확인 |
| _isWalkPending 제거 시 Walk 복구 지연 | 서버 RPC가 ~50ms 내 도착하므로 실질적 영향 미미 |
| SetUpdate(Late) 부작용 | DOTween Late 업데이트는 Unity LateUpdate 타이밍에 실행. 기존 LateUpdate 로직과 동일 프레임에서 실행되므로 순서만 주의 |
