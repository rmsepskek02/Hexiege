# Plan: 이동 전 회전 타이밍 수정

**작성일:** 2026-03-27

---

## 목표

`TurnToFaceClientRpc`의 프리-회전(DORotate)이 이동 시작 시 `NetworkUnit.LateUpdate` 델타 회전에 의해 무력화되는 문제 수정.
이동 전 회전이 완전히 완료된 후 이동이 시작되는 것처럼 보이도록 개선.

---

## 구현 방법

### 핵심 변경: `_isPreRotating` 플래그

`TurnToFaceClientRpc`가 DORotate를 실행하는 동안 LateUpdate의 델타 기반 회전을 **차단**한다.
DORotate 완료 후 플래그를 해제하여 이동 중 자연스러운 회전으로 전환.

```
[수정 후 흐름]
TurnToFaceClientRpc 수신
  → SetPreRotating(true)
  → DORotate(yAngle, rotationDuration).OnComplete(() => SetPreRotating(false))

LateUpdate 델타 회전
  → _isPreRotating=true이면 완전 스킵 (DOTween 보호)
  → _isPreRotating=false이면 기존 RotateTowards 실행
```

---

## 파일별 변경 내용

### 1. `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs`

**필드 추가**:
```csharp
/// <summary>
/// TurnToFaceClientRpc의 DORotate 실행 중 플래그.
/// true이면 LateUpdate의 델타 기반 회전을 차단하여 DOTween 회전을 보호.
/// DORotate OnComplete 콜백에서 false로 해제.
/// </summary>
private bool _isPreRotating;
```

**메서드 추가**:
```csharp
/// <summary>
/// 프리-회전(TurnToFaceClientRpc DORotate) 활성/비활성 설정.
/// NetworkCombatController에서 DORotate 시작/완료 시 호출.
/// </summary>
public void SetPreRotating(bool value) { _isPreRotating = value; }
```

**LateUpdate 델타 회전 조건 추가**:
```csharp
// _isPreRotating=true이면 DOTween 프리-회전 중 → 델타 회전 차단
if (!_isPreRotating && _hasInitialPosition)
{
    // 기존 delta 기반 RotateTowards 코드
}
```

---

### 2. `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`

**TurnToFaceClientRpc 수정**:
```csharp
NetworkUnit networkUnit = unitObj.GetComponent<NetworkUnit>();
networkUnit?.ResetMovementTracking();
networkUnit?.SetPreRotating(true);  // ← 추가

unitObj.transform.DOKill();
unitObj.transform.DORotate(new Vector3(0f, yAngle, 0f), rotationDuration)
                 .SetEase(Ease.OutQuad)
                 .OnComplete(() => networkUnit?.SetPreRotating(false));  // ← 추가
```

---

## 위험 요소

| 위험 | 내용 | 대응 |
|------|------|------|
| DOTween OnComplete 미호출 | DOKill() 등으로 중단 시 OnComplete 미발행 → `_isPreRotating`이 true로 고착, 이후 이동 시 회전 먹통 | `ResetMovementTracking()` 진입 시 `_isPreRotating=false`로 리셋 추가 (안전망) |
| 다음 이동 명령 시 플래그 잔존 | 공격 중 새 이동 명령 → TurnToFaceClientRpc 재호출 → DOKill 후 OnComplete 미호출 | 위 안전망으로 동일하게 대응 |

---

## 구현 순서

```
[1] NetworkUnit.cs
    - _isPreRotating 필드 추가
    - SetPreRotating(bool) 메서드 추가
    - ResetMovementTracking()에 _isPreRotating = false 추가 (안전망)
    - LateUpdate 델타 회전에 !_isPreRotating 조건 추가
      ↓
[2] NetworkCombatController.cs
    - TurnToFaceClientRpc에 SetPreRotating(true) 추가
    - DORotate에 OnComplete(() => SetPreRotating(false)) 추가
      ↓
[3] 컴파일 확인
      ↓
[4] 테스트: HOST·CLIENT 양측에서 이동 시 회전이 이동보다 먼저 완료되는지 확인
```
