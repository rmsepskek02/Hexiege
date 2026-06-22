# Research — LoadingIndicator 최소 표시 시간 보장

## 이 작업이 하는 일

로딩 인디케이터가 너무 빠르게 사라져서 눈에 보이지 않는 문제를 해결한다.
예를 들어 씬 전환 전에 ShowLoading(true)를 호출한 직후 ShowLoading(false)가 바로 호출되면,
로딩 인디케이터가 잠깐 깜빡이고 사라지거나 아예 안 보일 수 있다.
모든 상황에서 로딩 인디케이터가 최소 1초 동안은 화면에 유지되도록 수정한다.

---

## 현재 상태 분석

### ShowLoading 구현 (UIManager.cs)

```csharp
public void ShowLoading(bool show, string message = "")
{
    if (_loadingStatusText != null)
    {
        if (!string.IsNullOrEmpty(message))
            _loadingStatusText.text = message;
        else if (!show)
            _loadingStatusText.text = string.Empty;
    }

    ApplyLoadingVisibility(show);   // ← 즉시 처리, 최소 시간 로직 없음
}
```

`ShowLoading(false)` 호출 시 즉시 `ApplyLoadingVisibility(false)`가 실행된다.
표시된 지 1초도 안 됐어도 지연 없이 즉시 숨긴다.

---

### ShowLoading 호출 지점 현황

| 호출 위치 | 패턴 |
|-----------|------|
| `LoginBootstrapper.cs` → `_bootstrapper.ShowLoading(true/false)` | Login 씬 전반 |
| `EmailLoginView.cs` | 이메일 로그인 처리 |
| `SignUpView.cs` | 회원가입 처리 |
| `LoginSelectView.cs` | 구글/게스트 로그인 처리 |
| `AnonymousWarningPopup.cs` | 익명 계정 링크 처리 |
| `EmailVerifyView.cs` | 이메일 인증 대기 처리 |
| `BattleViewModel.cs` | 전투 씬 로딩 처리 (여러 곳) |

모두 `UIManager.Instance?.ShowLoading()` 또는 `_bootstrapper.ShowLoading()` 경유로 호출된다.
`LoginBootstrapper.ShowLoading()`은 내부적으로 `UIManager.Instance?.ShowLoading()`에 위임한다.

---

### 최소 표시 시간이 필요한 대표 시나리오

1. **씬 전환 시**: `ShowLoading(true)` 후 인증 확인이 매우 빠르게 완료 → 곧바로 `ShowLoading(false)` 또는 씬 전환 발생 → 인디케이터가 보이지 않음
2. **비동기 처리가 빠른 경우**: 캐시된 세션으로 Firebase 인증이 즉시 완료 → ShowLoading이 깜빡이고 사라짐
3. **BUG-A 작업과 연관**: LoadingIndicator를 SafeAreaContainer 밖으로 이동한 후에도 너무 빠르게 사라지면 효과 없음

---

## 영향 범위

| 파일 | 변경 유형 |
|------|----------|
| `UIManager.cs` | 코드 수정 (최소 표시 시간 로직 추가) |

---

## 설계 검토 — 어디서 처리할 것인가

**옵션 A: 각 호출부(View 등)에서 처리**
- 각 View가 ShowLoading(true) 후 최소 1초를 기다렸다가 ShowLoading(false) 호출
- 단점: 호출부가 7곳 이상이므로 중복 코드가 많고 누락 위험 있음

**옵션 B: UIManager.ShowLoading()에서 처리 (채택)**
- UIManager가 ShowLoading(false) 호출 시 최소 표시 시간이 지나지 않았으면 코루틴으로 지연
- 장점: 모든 호출부가 자동으로 혜택을 받음. 단일 책임 원칙.
- 단점: UIManager가 MonoBehaviour이므로 코루틴 사용 가능 (문제 없음)

→ 옵션 B 채택
