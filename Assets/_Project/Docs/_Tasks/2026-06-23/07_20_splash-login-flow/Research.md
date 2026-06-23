# Research: 스플래시 화면 로그인 흐름 개선

## 작업 목적

앱을 재실행했을 때 이미 로그인이 되어 있는 경우, 로그인 화면이 잠깐 노출되는 문제를 수정한다.
로그인 여부와 관계없이 항상 "탭투스타트"를 거쳐 다음 화면으로 이동하도록 흐름을 통일한다.

- **로그인 O**: 스플래시 → "탭투스타트" → 탭 → 로비
- **로그인 X**: 스플래시 → "탭투스타트" → 탭 → 로그인 화면 (이미 동작 중, 변경 없음)

---

## 씬 구조

| 씬 | 내용 |
|----|------|
| `Login.unity` | 스플래시 오버레이 + 로그인 패널들이 **같은 씬** 안에 공존 |
| `Lobby.unity` | 로비 |
| `Game.unity` | 인게임 |

---

## 관련 파일

| 파일 | 역할 |
|------|------|
| `Scripts/Bootstrap/LoginBootstrapper.cs` | Login 씬 진입점. Firebase 초기화 → 자동 로그인 분기 → 씬 전환 |
| `Scripts/Presentation/UI/SplashOverlayView.cs` | 스플래시 오버레이 UI. 로딩 중 / 탭투스타트 / 페이드아웃 담당 |
| `Scripts/Presentation/UI/Views/Login/LoginRootView.cs` | 로그인 패널 전환 및 Back 스택 관리 |
| `Scripts/Presentation/UI/SceneLoader.cs` | 씬 전환 유틸. ShowLoading(true) + 1초 딜레이 후 씬 전환 |

---

## 현재 흐름 분석

### 로그인 O (자동 로그인 성공) — 문제가 있는 흐름

```
LoginBootstrapper.Start()
→ _splashOverlay.SetStatus("로딩 중...")
→ Firebase 초기화 완료
→ TryAutoLoginAsync() 성공
→ _splashOverlay.FadeOut(GoToNextScene)   ← 0.5초 페이드아웃
→ GoToNextScene()
→ SceneLoader.Load("Lobby")               ← 내부에서 1초 딜레이 후 씬 전환
```

**문제 원인:**
스플래시 오버레이가 0.5초 동안 페이드아웃(alpha 감소)되는 동안,
그 뒤에 있는 Login 씬의 배경이 사용자에게 노출된다.
SceneLoader.Load까지 합산하면 총 **1.5초** 동안 Login 씬 배경이 보인다.

(`LoginRootView.HideAll()`이 호출되어 로그인 패널들은 숨겨져 있지만,
씬 배경 자체가 스플래시 오버레이 뒤에 항상 있으므로 페이드아웃 시 노출된다.)

### 로그인 X — 정상 흐름 (변경 없음)

```
LoginBootstrapper.Start()
→ _splashOverlay.SetStatus("로딩 중...")
→ Firebase 초기화 완료
→ IsLoggedIn = false → 자동 로그인 불가
→ _splashOverlay.SetTapCallback(ShowLoginSelect)
→ UIManager.Instance?.ShowLoading(false)
→ _splashOverlay.ShowTapToStart()          ← "탭투스타트" 표시
→ 탭
→ FadeOut 후 ShowLoginSelect()             ← 로그인 선택 화면 노출
```

---

## 코드 핵심 위치

### `LoginBootstrapper.cs` — 자동 로그인 성공 분기 (171~184번째 줄)

```csharp
if (autoOk)
{
    // 현재: 탭 없이 즉시 FadeOut → GoToNextScene
    if (_splashOverlay != null)
        _splashOverlay.FadeOut(GoToNextScene);
    else
        GoToNextScene();
    return;
}
```

이 부분이 수정 대상이다.

### `SplashOverlayView.cs` — ShowTapToStart / SetTapCallback

- `SetTapCallback(Action)`: 탭 이후 실행할 콜백을 주입
- `ShowTapToStart()`: 상태 텍스트 숨김 + "탭투스타트" 깜빡임 + 탭 입력 허용
- `FadeOut(Action)`: 오버레이 페이드아웃 후 콜백 호출

---

## 적용 GameSystemRules

| 규칙 | 내용 | 적용 근거 |
|------|------|-----------|
| UI 규칙 L-3 | ShowLoading(false) 책임은 목적지 씬 Bootstrapper | 자동 로그인 성공 시에도 ShowTapToStart 직전에 ShowLoading(false) 호출 필요 (이전 씬에서 켜진 로딩 인디케이터 끄기) |
| UI 규칙 L-4 | null-safe 패턴 필수 | `UIManager.Instance?.ShowLoading(false)` |
| UI 규칙 5 | CanvasGroup 숨김/표시 패턴 | SplashOverlayView 기존 방식 유지 |

---

## 영향 범위

- 수정 파일: `LoginBootstrapper.cs` 1곳 (자동 로그인 성공 분기, 약 5줄 변경)
- 씬/프리팹 변경 없음
- 로그인 X 흐름 변경 없음
- Lobby 씬 / 이후 흐름 변경 없음
