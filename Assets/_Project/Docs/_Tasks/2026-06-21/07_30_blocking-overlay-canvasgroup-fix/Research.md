# Research: Tap to Start 후 화면이 어두워지는 문제

## 작업 목적

실기기에서 "Tap to Start" 화면을 탭하면 로그인 화면 전체가 검정 반투명 이미지로 덮이는 버그를 분석한다.

---

## 현상

- Tap to Start 전: 정상 (SplashOverlay가 화면 전체를 덮고 있음)
- Tap to Start 후: 로그인 화면 위에 검정 반투명 오버레이가 씌워진 상태로 보임

---

## 원인 분석

### 관련 파일
- `Assets/_Project/Scenes/Login.unity`
- `Assets/_Project/Scripts/Presentation/UI/ConfirmPopup.cs`

### 버그 원인 (단계별)

**1. `NetworkErrorPopup`이 씬에서 항상 활성화 상태로 시작**

Login.unity 씬 계층:
```
SafeAreaContainer
├─ LoginRoot
├─ AnonymousWarningPopup
└─ NetworkErrorPopup  ← m_IsActive: 1 (항상 활성)
    ├─ BlockingOverlay  ← m_IsActive: 1, Image color (0,0,0,0.6)
    └─ Panel (AnimatedPanel)
```

**2. `BlockingOverlay`에 CanvasGroup이 없음**

`ConfirmPopup.cs`의 `_blockingOverlay` 필드는 `CanvasGroup` 타입:
```csharp
[SerializeField] private CanvasGroup _blockingOverlay;
```

하지만 씬의 `BlockingOverlay` GameObject 컴포넌트 목록:
- RectTransform
- CanvasRenderer
- Image (color: r=0, g=0, b=0, **a=0.6**)
- **CanvasGroup 없음**

따라서 `_blockingOverlay`는 런타임에 **null**이 되고, `Hide()`를 호출해도 Image가 숨겨지지 않는다.

**3. `SplashOverlay`가 덮고 있어서 Tap 전에는 보이지 않음**

`SplashOverlay Canvas`는 Sort Order 200으로 모든 UI 위에 렌더링된다.
Tap 후 SplashOverlay가 페이드아웃되면, 그 아래에 항상 존재하던 검정 반투명 Image가 드러난다.

**4. `AnimatedPanel`(`_panel`)은 정상적으로 숨겨짐**

`AnimatedPanel.Awake()`에서 `_cg.alpha = 0f`로 초기화하므로 팝업 박스 자체는 보이지 않는다.
오직 `BlockingOverlay`의 Image만 항상 노출된 상태이다.

### 관련 코드 동작 흐름

```
ConfirmPopup.Hide() 호출 시:
  _blockingOverlay?.alpha = 0f     ← _blockingOverlay가 null이므로 실행 안 됨
  _blockingOverlay?.blocksRaycasts = false  ← 동일
  _panel.Hide()                    ← AnimatedPanel은 정상 동작
```

---

## 영향 범위

- Login 씬 `NetworkErrorPopup` 오브젝트만 해당
- 다른 씬의 ConfirmPopup은 UIManager 소속으로 별도 관리됨 (영향 없음)
- `ConfirmPopup.cs` 코드 자체는 수정 불필요 — 씬 Inspector 수정으로 해결 가능
