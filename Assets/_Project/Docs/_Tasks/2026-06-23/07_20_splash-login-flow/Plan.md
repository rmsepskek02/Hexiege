# Plan: 스플래시 화면 로그인 흐름 개선

## 작업 목적

로그인이 된 상태로 앱을 재실행할 때, 로그인 화면이 잠깐 보이는 문제를 수정한다.
자동 로그인 성공 시에도 "탭투스타트"를 거쳐 로비로 이동하도록 흐름을 변경한다.

---

## 수정 파일

| 파일 | 변경 유형 |
|------|-----------|
| `Assets/_Project/Scripts/Bootstrap/LoginBootstrapper.cs` | 수정 (자동 로그인 성공 분기 ~5줄) |

씬/프리팹/다른 스크립트 변경 없음.

---

## 변경 상세

### `LoginBootstrapper.cs` — `InitializeAndDispatchAsync()` 내 자동 로그인 성공 분기

**변경 전 (현재):**
```csharp
if (autoOk)
{
    Debug.Log("[LoginBootstrapper] 자동 로그인 성공. 스플래시를 닫고 Lobby 씬으로 이동합니다.");
    if (_splashOverlay != null)
        _splashOverlay.FadeOut(GoToNextScene);
    else
        GoToNextScene();
    return;
}
```

**변경 후:**
```csharp
if (autoOk)
{
    Debug.Log("[LoginBootstrapper] 자동 로그인 성공. 'Tap to Start' 후 Lobby 씬으로 이동합니다.");
    if (_splashOverlay != null)
    {
        _splashOverlay.SetTapCallback(GoToNextScene);
        UIManager.Instance?.ShowLoading(false);
        _splashOverlay.ShowTapToStart();
    }
    else
        GoToNextScene();
    return;
}
```

---

## 변경 이유 및 근거

### 왜 FadeOut 대신 ShowTapToStart를 사용하는가?

현재 `FadeOut`은 스플래시가 0.5초에 걸쳐 서서히 사라지는 동작이다.
이 과정에서 스플래시 오버레이의 alpha가 낮아지면서, 그 뒤에 있는
Login 씬의 배경이 사용자에게 보인다.
"탭투스타트"를 통해 탭하는 시점에 `FadeOut → GoToNextScene`으로 이어지면,
동일한 0.5초 페이드아웃이 발생하지만 **탭한 직후 SceneLoader.Load가 즉시 호출**되어
로딩 인디케이터가 화면을 덮으므로 Login 씬 배경이 보이지 않는다.

### 왜 ShowLoading(false)를 ShowTapToStart 직전에 호출하는가?

- **근거: UI 규칙 L-3** — 로그인 씬 초기화(Firebase + 자동 로그인)가 완료된 시점이므로,
  이전 씬(예: 로비에서 로그아웃)에서 켜 두었을 수 있는 로딩 인디케이터를 여기서 끈다.
- 로그인 X 흐름의 기존 동작과 동일한 위치에서 호출하여 일관성을 유지한다.
- **근거: UI 규칙 L-4** — `UIManager.Instance?.ShowLoading(false)` null-safe 패턴 사용.

### 기존 로그인 X 흐름과의 대칭성

```
[로그인 O - 변경 후]           [로그인 X - 기존 유지]
SetTapCallback(GoToNextScene)  SetTapCallback(ShowLoginSelect)
ShowLoading(false)             ShowLoading(false)
ShowTapToStart()               ShowTapToStart()
```

두 경로가 동일한 패턴을 사용한다. 탭 콜백만 다를 뿐이다.

---

## 위험 요소

| 위험 | 내용 | 대응 |
|------|------|------|
| GoToNextScene 중복 호출 | ShowTapToStart 후 탭 외 다른 경로로 GoToNextScene이 호출될 가능성 | 없음. 기존에도 FadeOut 이후 GoToNextScene이 한 번만 호출되는 구조이며, SetTapCallback은 동일한 단일 경로 |
| 로딩 인디케이터 상태 | ShowLoading(false) 호출 후 탭투스타트가 보이는 동안 로딩이 꺼진 상태 | 정상 동작. 로딩은 탭 후 GoToNextScene → SceneLoader.Load가 다시 켠다 |
| 스플래시 없는 환경 | `_splashOverlay == null` (개발 중 직접 씬 진입) | 기존과 동일하게 `else GoToNextScene()` fallback 처리 |

---

## 구현 완료 기준

- 로그인된 상태로 앱 재실행 시 스플래시 → "탭투스타트" 문구가 표시됨
- 탭하면 로비로 이동함
- 로그인 화면이 중간에 보이지 않음
- 로그인 X 상태(기존 흐름)는 동작에 변화 없음
