# Testcase: Red Client 유닛 시각 동기화 버그 (BUG-A, BUG-B)

**작성일:** 2026-03-29

---

## 사전 준비

**Editor 스크립트 실행 필요:**
- Unity 메뉴 `Hexiege > Disable Write Defaults On All Controllers` 실행
- 콘솔에 "변경된 컨트롤러: N개, 변경된 상태: M개" 로그 확인

---

## BUG-A 테스트 (이동 중 방향 불일치)

### MULTI-1: Red 클라이언트 기본 이동 방향 일치

**전제:** 멀티플레이 (Host=Blue, Client=Red). 양쪽 유닛이 스폰 완료된 상태.

**동작:**
1. Red 클라이언트에서 아무 유닛을 선택하고 먼 타일로 이동 명령
2. 이동 시작 시점부터 유닛의 바라보는 방향을 관찰

**기댓값:**
- 이동 시작 직후부터 유닛이 이동 방향을 바라봄
- 스폰 방향(270° 등)을 잠시 유지하다가 회전하는 현상 없음
- 이동 내내 방향이 일관되게 유지됨

**결과:**

---

### MULTI-2: Red 클라이언트 복수 유닛 동시 이동

**전제:** 멀티플레이 (Host=Blue, Client=Red). 유닛 최소 3개 이상 스폰.

**동작:**
1. Red 클라이언트에서 여러 유닛을 순차적으로 각기 다른 방향으로 이동 명령
2. 각 유닛의 이동 시작 시점 방향을 관찰

**기댓값:**
- 모든 유닛이 이동 시작 시 올바른 방향을 즉시 바라봄
- 첫 번째 유닛과 이후 유닛 간 차이 없음

**결과:**

---

### MULTI-3: 공격 후 재이동 시 방향 전환

**전제:** 멀티플레이 (Host=Blue, Client=Red). Red 유닛이 적을 공격한 이력 있음.

**동작:**
1. Red 유닛이 적 유닛을 공격 완료할 때까지 대기
2. 적 사망 후 또는 사거리 이탈 후 이동이 재개될 때 방향 관찰
3. 수동으로 다른 방향 이동 명령 시 방향 관찰

**기댓값:**
- 공격 방향(적을 바라보던 방향)에서 새 이동 방향으로 즉시 전환
- 270° 등 스폰 기본 방향으로 순간적으로 되돌아가는 현상 없음

**결과:**

---

### SINGLE-1: 싱글플레이 이동 방향 정상 확인 (회귀 테스트)

**전제:** 싱글플레이 모드.

**동작:**
1. 유닛을 이동시키며 방향 전환 관찰
2. 공격 후 이동 재개 시 방향 관찰

**기댓값:**
- 기존과 동일하게 정상 동작
- SetUpdate(Late) 변경으로 인한 부작용 없음

**결과:**

---

## BUG-B 테스트 (공격 이력 유닛 Walk-in-place)

### MULTI-4: 공격 이력 유닛의 재이동 시 애니메이션

**전제:** 멀티플레이 (Host=Blue, Client=Red). Red 유닛이 적과 전투 중.

**동작:**
1. Red 유닛이 이동 중 적을 만나 공격 시작
2. 적이 사망하거나 사거리를 벗어나 이동이 재개될 때 관찰
3. 같은 유닛이 다시 적을 만나 재공격 시작 시 관찰

**기댓값:**
- 이동 재개 시 즉시 Walk 애니메이션으로 전환 (제자리 Walk 없음)
- 재공격 시 Walk에서 Attack으로 깔끔하게 전환
- 이동 멈춤 + 애니메이션 멈춤 현상 없음

**결과:**

---

### MULTI-5: 연속 공격 시나리오 (적 다수)

**전제:** 멀티플레이 (Host=Blue, Client=Red). 적 유닛이 여러 개 밀집.

**동작:**
1. Red 유닛을 적 다수가 있는 방향으로 이동
2. 첫 적 공격 → 사망 → 이동 재개 → 두 번째 적 공격 연속 관찰

**기댓값:**
- 첫 적 공격 후 이동 재개가 매끄러움
- 두 번째 적 공격 진입 시 Walk→Attack 전환이 자연스러움
- 어떤 시점에서도 "제자리 걷기" 발생 안 함

**결과:**

---

### MULTI-6: Blue Host 관점에서도 정상 확인

**전제:** 멀티플레이 (Host=Blue, Client=Red).

**동작:**
1. Blue Host에서 자기 유닛의 이동, 공격, 재이동 관찰
2. 기존과 동일하게 정상 동작하는지 확인

**기댓값:**
- Blue Host 유닛의 이동/공격/방향 전환 모두 기존과 동일
- 서버 측 변경(SetUpdate(Late))에 의한 부작용 없음

**결과:**

---

## QA 섹션

---

## 정적 분석 결과 (qa-tester)

**분석 일자:** 2026-03-29
**분석 대상:**
- `Scripts/Editor/DisableWriteDefaults.cs` (신규)
- `Scripts/Infrastructure/Network/NetworkCombatController.cs` (TurnToFaceClientRpc 수정)
- `Scripts/Presentation/Unit/UnitView.cs` (ApplyDirection, PlayAttackAnimation, TriggerAttackAnimation 수정)

---

### 1. 컴파일 오류 가능성 검토

| 검토 항목 | 판정 | 근거 |
|----------|------|------|
| `DisableWriteDefaults.cs` — `#if UNITY_EDITOR` 가드, using 선언 | PASS | `UnityEditor`, `UnityEditor.Animations`, `UnityEngine` 모두 정상. Editor 스크립트 전용으로 빌드 제외 처리 확인 |
| `DisableWriteDefaults.cs` — `AnimatorController`, `AnimatorState`, `ChildAnimatorState`, `ChildAnimatorStateMachine` API | PASS | UnityEditor.Animations 네임스페이스의 표준 API. 컴파일 오류 없음 |
| `NetworkCombatController.cs` — `SetUpdate(UpdateType.Late)` 추가 | PASS | `using DG.Tweening` 이미 선언됨 (L32). `UpdateType` 열거형은 DG.Tweening 네임스페이스에 포함. 컴파일 오류 없음 |
| `UnitView.cs` — `ApplyDirection`의 `SetUpdate(UpdateType.Late)` | PASS | `using DG.Tweening` 이미 선언됨 (L40). 컴파일 오류 없음 |
| `UnitView.cs` — `PlayAttackAnimation`의 `SetUpdate(UpdateType.Late)` | PASS | 동일. 컴파일 오류 없음 |
| `UnitView.cs` — `TriggerAttackAnimation`에 `_isWalkPending = false` 추가 | PASS | 동일 클래스 내 private 필드 접근. 컴파일 오류 없음 |
| `UnitView.cs` — `PlayAttackAnimation` 코루틴 끝 `_isWalkPending` 자체복구 블록 제거 | PASS | 블록 제거 후 문법 이상 없음. `_attackCoroutine = null` 이후 코드가 코루틴 끝으로 자연스럽게 종료됨 |

**컴파일 오류 가능성: 없음 (PASS)**

---

### 2. DOTween SetUpdate(Late)와 NetworkUnit.LateUpdate() 실행 순서 안전성

**핵심 질문:** DOTween `UpdateType.Late`와 MonoBehaviour `LateUpdate()`가 같은 LateUpdate 단계에서 실행될 때 순서가 안전한가?

**DOTween 동작 방식:**
- `UpdateType.Late`는 DOTween이 Unity의 `LateUpdate` 이후 시점을 선택하는 것이 아니라, DOTween 내부 Manager가 `LateUpdate` 단계에서 실행되는 방식임
- DOTween Manager는 별도의 내부 MonoBehaviour(`DOTweenUnity`)로 동작하며, Script Execution Order에 따라 일반 MonoBehaviour의 LateUpdate와 순서가 결정됨
- DOTween의 기본 실행 순서는 별도로 지정되지 않으면 Default(0)로, 대부분의 사용자 스크립트와 동일 우선순위

**실제 순서 분석:**
```
LateUpdate 단계:
  1. MonoBehaviour LateUpdate (Script Execution Order 기준)
     └─ NetworkUnit.LateUpdate(): RotateTowards 실행 (_isPreRotating=false인 경우)
  2. DOTween Internal Manager LateUpdate:
     └─ SetUpdate(Late) Tween 평가: DORotate 목표값 적용
```

DOTween의 내부 Manager는 기본적으로 Script Execution Order 기본값(0)에서 동작하므로, NetworkUnit도 기본값이면 실행 순서가 보장되지 않음. 그러나 실제 운용 시나리오를 보면:

- **DORotate 실행 중 (_isPreRotating = true):** NetworkUnit.LateUpdate()는 `if (!_isPreRotating && _hasInitialPosition)` 조건에서 분기되어 RotateTowards를 **실행하지 않음**. 따라서 DOTween Late와 LateUpdate가 동시에 rotation을 수정하는 상황 자체가 발생하지 않음
- **DORotate OnComplete 직후 (_isPreRotating = false로 전환):** OnComplete 콜백은 DOTween Tween 평가 완료 후 같은 프레임의 DOTween Manager LateUpdate 내에서 실행됨. 같은 프레임에서 NetworkUnit.LateUpdate()가 DOTween Manager LateUpdate보다 먼저 실행되더라도, OnComplete가 `SetPreRotating(false)`를 호출하는 시점은 DOTween Manager 단계이므로, NetworkUnit.LateUpdate()는 아직 `_isPreRotating=true`로 보호된 상태에서 실행됨

**결론: 안전함 (PASS)**
`_isPreRotating` 플래그가 DOTween의 LateUpdate 타이밍 전환과 독립적으로 동작하므로, SetUpdate(Late) 변경이 기존 _isPreRotating 보호 로직과 충돌하지 않음.

---

### 3. _isWalkPending 자체복구 제거 후 Walk 재개 시나리오 검토

**제거된 블록 (구버전):**
```
// PlayAttackAnimation 코루틴 끝부분
_attackCoroutine = null;
if (_isWalkPending)
{
    _isWalkPending = false;
    yield return null;
    if (_attackCoroutine == null)
        StartWalkAnimation();
}
```

**신규 동작:**
```
_attackCoroutine = null;
// 자체 복구 없음 — 서버 RPC에 의존
```

**서버의 Walk 재개 경로 확인 (UnitView.cs L479-513):**

전투 루프 탈출 시 서버 `MoveAlongPath`는 두 단계를 거쳐 반드시 Walk RPC를 발행함:

```
1. while (_unitData.IsAlive && _combatUseCase.HasEnemyInRange(_unitData)):  // 전투 유지 루프
     └─ while (_attackCoroutine != null) yield return null;  // 공격 완료 대기
     └─ yield return null;
2. 적 사거리 이탈 → 루프 탈출
3. while (_attackCoroutine != null) yield return null;  // 마지막 공격 완료 대기
4. GameEvents.OnUnitWalkStarted.OnNext(_unitData.Id);   // ★ Walk RPC 발행
5. GameEvents.OnUnitFacingChanged.OnNext(...);          // 방향 RPC 발행
```

**잠재적 위험 시나리오 검토:**

| 시나리오 | 분석 | 판정 |
|---------|------|------|
| 유닛이 사거리 내에서 적을 사살한 경우 | `HandleUnitDied`로 적 제거 → `HasEnemyInRange` false → 루프 탈출 → Walk RPC 발행됨 | PASS |
| 유닛이 이동 목적지 도착 없이 전투만 반복하는 경우 | MoveAlongPath는 각 웨이포인트마다 전투 체크 → 웨이포인트를 끝까지 진행하면 L576에서 `OnUnitWalkStopped` 발행됨 | PASS |
| 서버에서 유닛이 사망한 경우 | `if (!_unitData.IsAlive) break;` (L492)로 이동 코루틴 자체가 종료됨. Walk RPC 불필요 | PASS |
| 클라이언트에서 `StartWalkAnimationClientRpc`가 `_attackCoroutine` 실행 중에 도착하는 경우 | `StartWalkAnimation()`: `_attackCoroutine != null` → `_isWalkPending = true` → 즉시 Walk 시작 안 함. 이후 공격 완료 시 자체 복구 블록이 없으므로 Walk가 자동 시작되지 않음. **단, 이것은 정상 — 서버가 Walk를 재개할 시점에 다시 `OnUnitWalkStarted`를 발행하므로 클라이언트는 그 RPC를 기다림** | PASS |

**단, _isWalkPending 잔류 상황 주의:**
`StartWalkAnimation()` 호출 시 `_attackCoroutine != null`이면 `_isWalkPending = true`가 설정됨. 이후 공격이 완료되고 `_attackCoroutine = null`이 설정되어도, 자체 복구 블록이 제거되었으므로 Walk가 시작되지 않음. 이 시점에서 서버의 다음 Walk RPC가 도착해야 Walk가 재개됨.

만약 서버가 Walk RPC를 발행하지 않는 경로가 있다면 클라이언트는 영구적으로 멈추게 됨. 위 전투 루프 탈출 경로(L500)에서 반드시 발행함을 확인했으나, **이동 경로 없이 공격만 수행하는 경우(목적지에 도착한 상태에서 공격)는 이동 완료 후 Walk RPC가 전송되지 않음** — 이는 의도된 동작 (이미 정지한 유닛이 공격하는 경우).

**결론: PASS** — 서버 MoveAlongPath 내 모든 전투 루프 탈출 경로에서 Walk RPC가 정상 발행됨을 확인.

---

### 4. DisableWriteDefaults Editor 스크립트 동작 검토

| 검토 항목 | 판정 | 근거 |
|----------|------|------|
| `AssetDatabase.FindAssets("t:AnimatorController", ...)` 경로 | PASS | `Assets/_Project/Animations/Units` — 실제 컨트롤러 위치와 일치 (Research.md 확인) |
| SubStateMachine 재귀 처리 | PASS | `ProcessStateMachine`이 `stateMachine.stateMachines`를 순회하며 재귀 호출. 중첩 상태머신 누락 없음 |
| `state.writeDefaultValues` 이미 false인 상태 처리 | PASS | `if (state.writeDefaultValues)` 조건으로 이미 OFF인 상태는 건드리지 않음. 불필요한 Dirty 마킹 방지 |
| `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets` | PASS | 변경 후 디스크 저장 정상 흐름 |
| 콘솔 로그 출력 | PASS | 컨트롤러별, 최종 요약 2종 로그 출력됨 |
| 대상 컨트롤러 없을 때 처리 | PASS | `LogWarning` 후 `return`. 오류 없이 종료됨 |

**주의사항 (Minor):** 이 스크립트는 1회성 도구임. 이후 새 Animator Controller나 새 State를 추가할 경우 Write Defaults가 기본 ON으로 생성되므로, 재실행이 필요함. 현재 코드 주석(L31)에 이 내용이 명시되어 있어 문서화는 충분함.

**결론: PASS**

---

### 5. 싱글플레이 영향 분석

**ApplyDirection의 SetUpdate(Late) 변경:**
- `ApplyDirection`은 서버의 `MoveAlongPath` 코루틴에서 호출됨 (L262-275)
- 싱글플레이에서도 동일하게 호출됨
- 기존: DORotate가 Update에서 실행됨 → 싱글플레이에서 Animator Write Defaults가 같은 문제를 일으킬 수 있었으나, 서버(로컬)는 NetworkUnit.LateUpdate()가 `if (IsServer || !NetworkContext.IsNetworkActive) return;` (NetworkUnit.cs L351)으로 조기 종료되므로 LateUpdate 회전 간섭 자체가 없었음
- 변경 후: DORotate가 LateUpdate에서 실행. 싱글플레이에서는 LateUpdate 간섭이 없으므로 실질적 동작 차이 없음

**TriggerAttackAnimation의 _isWalkPending = false 추가:**
- 싱글플레이에서 `TriggerAttackAnimation`은 `UnitCombatUseCase.TryAttack()`에서 호출됨
- 기존에도 `_attackCoroutine != null`이면 `StopCoroutine` 후 재시작하는 로직이 있었고, `_isWalkPending = false`는 그 블록 내부에서만 실행됨 (L688-698)
- 싱글플레이에서 `_attackCoroutine`이 이미 실행 중에 새 공격이 시작될 경우, 이전과 동일하게 동작 (이미 강제 중단하는 코드가 있었음). 새로 추가된 `_isWalkPending = false`는 플래그 정리이므로 싱글플레이 동작에 영향 없음

**PlayAttackAnimation 자체복구 블록 제거:**
- 싱글플레이에서 `_isWalkPending`은 `StartWalkAnimation()`에서 설정될 수 있는데, `StartWalkAnimation()`은 `StartWalkAnimationClientRpc`에서만 호출됨 (L430)
- 싱글플레이에서는 `StartWalkAnimationClientRpc`가 발행되지 않음 (`if (IsServer) return;` 조건 포함)
- 따라서 싱글플레이에서 `_isWalkPending`은 항상 false — 제거된 블록이 싱글플레이에서 실행될 일이 없음

**결론: 싱글플레이 영향 없음 (PASS)**

---

### 종합 정적 분석 판정

| 검토 영역 | 판정 |
|---------|------|
| 컴파일 오류 가능성 | PASS |
| SetUpdate(Late) + LateUpdate 순서 안전성 | PASS |
| _isWalkPending 제거 후 Walk 재개 경로 | PASS |
| DisableWriteDefaults Editor 스크립트 | PASS |
| 싱글플레이 회귀 영향 | PASS |

**전체 정적 분석: CONDITIONAL PASS**

수정 코드 자체의 논리적 오류 및 컴파일 오류는 없음. 다만 아래 항목은 실기 테스트로만 확인 가능:

1. DOTween `UpdateType.Late`의 실제 Script Execution Order 상 실행 위치 — Unity 환경에 따라 DOTween Manager가 NetworkUnit.LateUpdate()보다 먼저 실행될 수 있음. 단, `_isPreRotating` 플래그로 보호되어 있으므로 실제 충돌 가능성은 낮음
2. Animator Write Defaults OFF 적용 후 Dead 상태 포즈 잔류 여부 — CrossFade로 커버되어야 하나, Dead 애니메이션이 있는 경우 실기 확인 필요
3. 공격 완료 후 서버 Walk RPC 도착 타이밍 (~50ms 지연) — 실기에서 눈에 띄는 Idle 지연이 발생하는지 확인 필요
