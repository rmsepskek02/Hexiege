# Research: 반투명 배경 오버레이 SafeArea 미적용 문제 및 UIManager 통합

## 작업 목적

실기기에서 "Tap to Start" 후 로그인 화면이 검정 반투명 이미지로 덮이는 버그를 수정하면서,
프로젝트 전체에 반투명 배경 오버레이가 SafeArea의 영향을 받는 구조적 문제를 발견했다.

단순 버그 수정을 넘어, **모든 반투명 배경 오버레이를 UIManager가 단일 소유하는 구조로 통합**하여
SafeArea 문제를 근본적으로 해결하고 일관된 UI 아키텍처를 확립한다.

---

## 1단계 문제: Tap to Start 후 화면이 어두워지는 버그

### 현상
- Tap to Start 전: 정상 (SplashOverlay가 화면 전체를 덮고 있음)
- Tap to Start 후: 로그인 화면 위에 검정 반투명 오버레이가 씌워진 상태로 보임

### 원인 (단계별)

**1. `NetworkErrorPopup` / `AnonymousWarningPopup`이 항상 활성 상태로 시작**

```
SafeAreaContainer
├─ AnonymousWarningPopup  ← m_IsActive: 1 (항상 활성)
│   ├─ BlockingOverlay  ← m_IsActive: 1, Image color (0,0,0, a=0.6)
│   └─ Panel (AnimatedPanel)
└─ NetworkErrorPopup  ← m_IsActive: 1 (항상 활성)
    ├─ BlockingOverlay  ← m_IsActive: 1, Image color (0,0,0, a=0.6)
    └─ Panel (AnimatedPanel)
```

**2. BlockingOverlay에 CanvasGroup이 없어 런타임에 null**

두 팝업의 `_blockingOverlay: CanvasGroup` 필드가 런타임에 null → Hide() 호출 시 Image가 숨겨지지 않음.

**3. SplashOverlay가 Tap 전까지 덮고 있어 문제가 보이지 않음**

SplashOverlay Canvas는 Sort Order 200. Tap 후 페이드아웃되면 아래에 항상 존재하던 검정 Image들이 드러남.

---

## 2단계 문제: 반투명 배경 오버레이의 구조적 불일치

### 전체 씬 반투명 배경 오브젝트 현황

| 씬 | 오브젝트 | 계층 경로 | SafeArea 안/밖 | CanvasGroup | color.a |
|----|---------|---------|---|---|---|
| Login | BlockingOverlay | SafeAreaContainer > AnonymousWarningPopup > BlockingOverlay | **안 (문제)** | ✓ | 0.6 |
| Login | BlockingOverlay | SafeAreaContainer > NetworkErrorPopup > BlockingOverlay | **안 (문제)** | ✓ | 0.6 |
| Game | Overlay | RematchRequestPopup > Overlay | Canvas 직속 (올바름) | ✓ | 0.6 |
| Game | Background | Canvas > Background | Canvas 직속 (올바름) | ✓ | 0.49 |
| UIManager | BlockingOverlay | UIManager Canvas > SafeAreaContainer > ConfirmPopup > BlockingOverlay | **안 (문제)** | - | 0.6 |

> Login의 두 BlockingOverlay는 에디터 스크립트로 CanvasGroup을 추가했으나, SafeArea 안에 위치하는 근본 구조 문제는 미해결 상태.

### SafeArea 문제가 실제로 발생하는 조건

SafeAreaFitter는 SafeAreaContainer의 RectTransform을 기기의 Safe Area 범위에 맞게 축소한다.
노치/펀치홀/홈바 영역에서 SafeAreaContainer의 anchorMin/anchorMax가 변경되어,
그 안에서 Full-Stretch(anchorMin=0,0 / anchorMax=1,1)로 설정된 BlockingOverlay도
전체화면이 아닌 Safe Area 범위 안에서만 펼쳐진다.
→ 노치 영역, 홈바 영역에 검정 배경이 없이 투명하게 보임.

---

## 반투명 배경 패턴 현황 (전수 조사)

현재 프로젝트에는 두 가지 배경 패턴이 혼재한다.

### 패턴 A — 직접 소유 방식 (Modal 팝업)
각 팝업이 자신의 BlockingOverlay CanvasGroup을 SerializedField로 직접 소유.
배경 탭 시 아무 동작 없음 — 터치만 차단.

| 클래스 | 필드 | 위치 | 문제 여부 |
|-------|------|------|---------|
| ConfirmPopup | `_blockingOverlay: CanvasGroup` | UIManager Canvas > SafeAreaContainer 안 | **SafeArea 문제** |
| AnonymousWarningPopup | `_blockingOverlay: CanvasGroup` | Login SafeAreaContainer 안 | **SafeArea 문제** |
| RematchRequestPopup | `_overlay: GameObject` | Canvas 직속 | 위치는 올바름, 하지만 구조 불통일 |

### 패턴 B — 공유 배경 방식 (Popup 타입)
Canvas 직속 Background 오브젝트 + `SharedBackgroundButton` 컴포넌트. 배경 탭 시 현재 열린 팝업을 닫음.

| 클래스 | 필드 | 설명 |
|-------|------|------|
| InGameSettingsUI | `_sharedBackground: SharedBackgroundButton` | Canvas 직속 Background 참조 |
| BuildingPlacementUI | `_sharedBackground: SharedBackgroundButton` | 동일 |
| ProductionPanelUI | `_sharedBackground: SharedBackgroundButton` | 동일 (상속) |

### AnimatedPanel
선택적 `_backgroundOverlay: CanvasGroup` 필드 보유. 팝업 Show/Hide 시 배경 오버레이를 함께 제어하는 공통 컴포넌트.

---

## UIManager 현황

| 항목 | 내용 |
|------|------|
| 파일 | `Assets/_Project/Scripts/Presentation/UI/UIManager.cs` |
| 생명주기 | Login 씬에서 1회 생성 → DontDestroyOnLoad |
| Canvas Sort Order | 100 |
| 현재 관리 UI | ConfirmPopup, LoadingIndicator |
| 씬 배치 | `[UI Systems] Canvas > UIManager Canvas > SafeAreaContainer > {ConfirmPopup, LoadingIndicator}` |

UIManager는 이미 DontDestroyOnLoad로 모든 씬에서 유지되며,
Canvas SortingOrder=100으로 대부분의 씬 UI 위에 렌더링된다.
→ UIManager가 공유 BlockingOverlay를 소유하기에 이상적인 위치.

---

## 결론: 통합 필요성

| 문제 | 설명 |
|------|------|
| SafeArea 미적용 | ConfirmPopup, AnonymousWarningPopup의 BlockingOverlay가 SafeAreaContainer 안에 있어 노치/홈바 영역 미커버 |
| 패턴 불일치 | Modal은 직접 소유, Popup은 SharedBackgroundButton으로 다른 패턴 혼재 |
| 구조 불통일 | RematchRequestPopup은 위치는 맞지만 UIManager와 무관하게 독립 동작 |

→ 모든 반투명 배경 오버레이를 **UIManager 단일 소유 구조로 통합**하여 패턴을 일원화한다.
