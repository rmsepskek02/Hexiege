# Plan: 스플래시 화면 로그인 흐름 개선

## 작업 목적

로그인이 된 상태로 앱을 재실행할 때, 로그인 화면이 잠깐 보이는 문제를 수정한다.
자동 로그인 성공 시에도 "탭투스타트"를 거쳐 로비로 이동하도록 흐름을 변경하되,
탭 시 FadeOut 없이 즉시 씬을 이동하여 로그인 씬 배경이 노출되지 않도록 한다.

---

## 수정 파일

| 파일 | 변경 유형 |
|------|-----------|
| `Assets/_Project/Scripts/Presentation/UI/SplashOverlayView.cs` | 수정 (skipFade 모드 추가) |
| `Assets/_Project/Scripts/Bootstrap/LoginBootstrapper.cs` | 수정 (자동 로그인 성공 분기) |

씬/프리팹 변경 없음.

---

## 문제 분석 (1차 구현 후 발견)

1차 구현(SetTapCallback + ShowTapToStart)으로 탭투스타트 표시는 해결했으나,
탭 시 여전히 `FadeOut(0.5초)`이 먼저 실행되어 스플래시가 투명해지는 동안
그 뒤의 로그인 씬 배경이 노출되는 문제가 남았다.

```
[문제 흐름]
탭 → FadeOut 시작(alpha 1→0, 0.5초) → 이 동안 Login 씬 배경 노출 → GoToNextScene
```

FadeOut은 로그인 X 흐름에서 "스플래시가 사라지며 로그인 화면을 드러내는" 의도된 연출이다.
로그인 O 흐름에서는 FadeOut 없이 바로 씬을 이동해야 한다.

---

## 변경 상세

### 1. `SplashOverlayView.cs` — skipFade 모드 추가

**추가 필드:**
```csharp
// 탭 시 FadeOut을 건너뛰고 콜백을 즉시 실행할지 여부.
// 로그인 O 분기에서 사용 — 로딩 인디케이터가 화면을 덮으므로 FadeOut이 불필요하다.
private bool _skipFadeOnTap;
```

**`SetTapCallback` 시그니처 변경 (기존 호출 코드 호환 유지):**
```csharp
// skipFade = false가 기본값이므로 기존 호출(로그인 X 분기)은 변경 없음.
public void SetTapCallback(Action callback, bool skipFade = false)
{
    _tapCallback = callback;
    _skipFadeOnTap = skipFade;
}
```

**`OnPointerClick` 분기 추가:**
```csharp
public void OnPointerClick(PointerEventData eventData)
{
    if (!_tapToStartActive) return;

    if (_skipFadeOnTap)
    {
        // FadeOut 없이 즉시 콜백 실행.
        // 로딩 인디케이터(SortingOrder 300)가 화면을 덮으므로 배경 노출 없음.
        _tapToStartActive = false;
        _blinkTween?.Kill();
        _blinkTween = null;
        _tapCallback?.Invoke();
    }
    else
    {
        FadeOut(_tapCallback);
    }
}
```

### 2. `LoginBootstrapper.cs` — 로그인 O 분기에서 skipFade 적용

```csharp
if (autoOk)
{
    Debug.Log("[LoginBootstrapper] 자동 로그인 성공. 'Tap to Start' 후 Lobby 씬으로 이동합니다.");
    if (_splashOverlay != null)
    {
        // skipFade: true — 탭 시 FadeOut 없이 즉시 GoToNextScene 호출.
        // SceneLoader가 로딩 인디케이터를 표시하므로 Login 씬 배경이 노출되지 않는다.
        _splashOverlay.SetTapCallback(GoToNextScene, skipFade: true);
        UIManager.Instance?.ShowLoading(false);
        _splashOverlay.ShowTapToStart();
    }
    else
        GoToNextScene();
    return;
}
```

---

## 최종 흐름 비교

| 상태 | 탭 시 동작 |
|------|-----------|
| **로그인 O** | FadeOut 없이 즉시 GoToNextScene → 로딩 인디케이터 → 로비 |
| **로그인 X** | FadeOut(0.5초, 로그인 화면 드러남) → ShowLoginSelect |

---

## 변경 이유 및 근거

- **UI 규칙 L-3**: ShowLoading(false) — 로그인 씬 초기화 완료 시점에 로딩 인디케이터를 끈다.
- **UI 규칙 L-4**: `UIManager.Instance?.ShowLoading(false)` null-safe 패턴 사용.
- `SetTapCallback`의 `skipFade` 파라미터는 기본값 false이므로 로그인 X 분기 기존 코드 변경 불필요.

---

## 위험 요소

| 위험 | 내용 | 대응 |
|------|------|------|
| 기존 호출 코드 영향 | SetTapCallback 시그니처 변경 | skipFade 기본값 false — 기존 호출(로그인 X)은 동작 변화 없음 |
| 스플래시가 화면에 남는 문제 | skipFade 시 스플래시 오버레이가 사라지지 않음 | 로딩 인디케이터(SO=300)가 덮고, 씬 전환 시 Login 씬 전체가 파괴되므로 문제 없음 |
| 스플래시 없는 환경 | `_splashOverlay == null` (개발 중 직접 씬 진입) | `else GoToNextScene()` fallback 처리 유지 |

---

## 구현 완료 기준

- 로그인된 상태로 앱 재실행 시 스플래시 → "탭투스타트" 표시됨
- 탭하면 로그인 화면이 전혀 보이지 않고 로비로 이동함
- 로그인 X 상태(기존 흐름)는 동작에 변화 없음
