# Plan — LoadingIndicator 최소 표시 시간 보장

## 이 작업이 하는 일

로딩 인디케이터가 눈에 보이지도 않게 깜빡이고 사라지는 현상을 방지한다.
UIManager 하나에서만 수정하면 Login·Battle 씬을 포함한 모든 호출부가 자동으로 혜택을 받는다.

---

## 규칙 근거

| 수정 항목 | 근거 |
|-----------|------|
| UIManager 단일 수정 | `GameSystemRules_UI.md` — 공통 UI 규칙 (단일 책임, UIManager가 LoadingIndicator를 단독 소유) |
| SetActive 대신 CanvasGroup 유지 | `GameSystemRules_UI.md` — 공통 UI 규칙 5 |

---

## 수정 대상

**파일**: `Assets/_Project/Scripts/Presentation/UI/UIManager.cs`

---

## 구현 설계

### 추가할 내부 상태 변수

```csharp
// 로딩 인디케이터가 표시된 시각. ShowLoading(true) 호출 시 기록한다.
private float _loadingShowTime;

// ShowLoading(false) 지연 처리 코루틴 참조. 중복 실행 방지에 사용한다.
private Coroutine _hideLoadingCoroutine;

// 최소 표시 시간(초). Inspector에서 조정 가능하도록 SerializeField로 선언한다.
[SerializeField] private float _loadingMinDuration = 1f;
```

### ShowLoading() 수정 방향

```
ShowLoading(true) 호출 시:
  1. 진행 중인 hideLoading 코루틴이 있으면 취소한다 (이미 대기 중인 숨김 취소)
  2. _loadingShowTime = Time.realtimeSinceStartup 기록
  3. 즉시 ApplyLoadingVisibility(true) 실행

ShowLoading(false) 호출 시:
  1. 진행 중인 hideLoading 코루틴이 있으면 취소한다 (중복 방지)
  2. 경과 시간 계산: elapsed = Time.realtimeSinceStartup - _loadingShowTime
  3. elapsed >= _loadingMinDuration 이면 즉시 ApplyLoadingVisibility(false)
  4. elapsed < _loadingMinDuration 이면 코루틴 시작 → (남은 시간) 대기 후 ApplyLoadingVisibility(false)
```

### 코루틴 구현

```csharp
private IEnumerator HideLoadingAfterMinDurationCoroutine(string statusTextToSet)
{
    float elapsed = Time.realtimeSinceStartup - _loadingShowTime;
    float remaining = _loadingMinDuration - elapsed;

    if (remaining > 0f)
        yield return new WaitForSecondsRealtime(remaining);

    // StatusText 초기화
    if (_loadingStatusText != null)
        _loadingStatusText.text = statusTextToSet;

    ApplyLoadingVisibility(false);
    _hideLoadingCoroutine = null;
}
```

`WaitForSecondsRealtime`을 사용하는 이유:
씬 전환 직전에 Time.timeScale이 0으로 설정되는 상황이 있을 수 있으므로,
게임 시간과 무관하게 실제 경과 시간 기준으로 대기한다.

---

## 수정 후 ShowLoading 전체 코드

```csharp
public void ShowLoading(bool show, string message = "")
{
    if (show)
    {
        // 대기 중인 숨김 코루틴이 있으면 취소한다 (show → hide → show 패턴 대응)
        if (_hideLoadingCoroutine != null)
        {
            StopCoroutine(_hideLoadingCoroutine);
            _hideLoadingCoroutine = null;
        }

        // 표시 시각 기록
        _loadingShowTime = Time.realtimeSinceStartup;

        // StatusText 업데이트
        if (_loadingStatusText != null && !string.IsNullOrEmpty(message))
            _loadingStatusText.text = message;

        ApplyLoadingVisibility(true);
    }
    else
    {
        // 대기 중인 숨김 코루틴 중복 방지
        if (_hideLoadingCoroutine != null)
        {
            StopCoroutine(_hideLoadingCoroutine);
            _hideLoadingCoroutine = null;
        }

        // 숨김 시 StatusText에 설정할 최종 값 결정
        string textToSet = string.Empty;

        // 최소 표시 시간이 지났으면 즉시, 아니면 코루틴으로 지연
        float elapsed = Time.realtimeSinceStartup - _loadingShowTime;
        if (elapsed >= _loadingMinDuration)
        {
            if (_loadingStatusText != null)
                _loadingStatusText.text = textToSet;
            ApplyLoadingVisibility(false);
        }
        else
        {
            _hideLoadingCoroutine = StartCoroutine(
                HideLoadingAfterMinDurationCoroutine(textToSet));
        }
    }
}
```

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| ShowLoading(true)가 호출되지 않은 상태에서 ShowLoading(false) 호출 시 elapsed가 비정상적으로 크게 나올 수 있음 | `_loadingShowTime`을 초기값 0으로 두면 elapsed가 매우 커져 즉시 숨김 처리됨 → 문제 없음 |
| 씬 전환 후 UIManager 오브젝트가 DontDestroyOnLoad라 코루틴이 살아 있음 | UIManager가 항상 존재하므로 코루틴도 정상 동작. 씬 전환 후 코루틴이 완료되면 이미 숨겨진 상태 |
| 코루틴 대기 중 게임이 일시 정지(Time.timeScale=0) | WaitForSecondsRealtime 사용으로 실제 시간 기준 대기 → 문제 없음 |

---

## 구현 순서

```
[1] UIManager.cs 수정
    - _loadingMinDuration, _loadingShowTime, _hideLoadingCoroutine 필드 추가
    - ShowLoading() 로직 교체
    - HideLoadingAfterMinDurationCoroutine() 추가
    ↓
[2] 사용자 테스트
    - 빠르게 완료되는 로그인 흐름에서 LoadingIndicator가 최소 1초 표시되는지 확인
```
