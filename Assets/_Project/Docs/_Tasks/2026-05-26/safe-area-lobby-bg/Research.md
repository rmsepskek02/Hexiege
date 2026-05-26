# Research — Safe Area 로비 배경이미지 처리

## 개요

이 문서는 실기기에서 로비 화면의 배경이미지가 Safe Area 경계에 맞춰 잘려 이질감이 발생하는 문제의 원인을 분석한다.
배경이미지는 Safe Area와 무관하게 전체 화면을 채워야 한다는 방향이 이미 결정되어 있으며, 이 문서는 그 구현을 위한 근거를 정리한다.

---

## 문제 현상

- 실기기(노치/홈바 있는 기기)에서 로비 배경색이 화면 전체를 채우지 않고 Safe Area 범위 안에서만 표시됨
- 화면 상하단 경계 근처에서 배경이 끊기는 이질감 발생
- 나머지 UI 요소(탭바, 컨텐츠)는 Safe Area 내에 있어야 하므로 정상이지만, 배경만 별개로 처리되어야 함

---

## 현재 씬 계층 구조 분석 (Lobby.unity)

씬 파일 직접 파싱 결과:

```
Canvas (또는 Canvas 부모)
└── SafeAreaContainer  ← SafeAreaFitter 컴포넌트 부착 (기기 Safe Area 범위로 크기 자동 축소)
    └── LobbyRoot  ← LobbyRootView + Image 컴포넌트(배경)
        ├── TabBarView
        └── ContentArea (전투/상점/프로필/랭킹 패널)
```

**문제의 핵심**: `LobbyRoot`에 부착된 `Image` 컴포넌트(어두운 남색, `r:0.059 g:0.059 b:0.102 a:1`)가 로비의 배경 역할을 하고 있다.
그런데 `LobbyRoot`의 부모가 `SafeAreaContainer`이므로, `SafeAreaFitter`가 컨테이너를 Safe Area 크기로 축소시킬 때 `LobbyRoot`의 배경 이미지도 함께 축소된다.

---

## GameSystemRules.md 근거

`GameSystemRules.md` **공통 UI 규칙 — 규칙 4. SafeArea 컨테이너 구조**에 명확히 정의되어 있다:

```
[UI] Canvas
  ├─ Background               ← 전체화면 배경 등 Safe Area 적용이 불필요한 요소
  └─ SafeAreaContainer        ← SafeAreaFitter 컴포넌트 부착
       └─ (모든 실제 UI 요소)  ← 팝업, HUD, 버튼 등 전부 이 안에 배치
```

현재 구조는 이 규칙을 위반하고 있다. 배경(`LobbyRoot`의 Image)이 `SafeAreaContainer` 안에 있어야 할 "실제 UI 요소"가 아닌데도 Safe Area 내부에 배치되어 있다.

---

## 영향 범위

- **수정 대상**: `Lobby.unity` 씬 계층 구조 (코드 변경 없음)
- **수정 방식**: Unity Inspector에서 씬 오브젝트 이동/생성 작업
- **영향 없는 영역**: `LobbyRootView.cs`, `SafeAreaFitter.cs` 등 코드 파일은 변경 불필요
- **주의 사항**: `LobbyRoot`가 `SafeAreaContainer` 안에 있는 것 자체는 올바름. 단, Image 배경이 `LobbyRoot`에 붙어있어 함께 제한받는 것이 문제임

---

## 수정 방향

아래 두 가지 방법이 가능하다. Plan.md에서 최종 방법을 결정한다.

**방법 A — 별도 BackgroundImage 오브젝트 분리 (권장)**
- `SafeAreaContainer`의 형제 노드로 `BackgroundImage` 빈 오브젝트 생성
- 전체 화면 stretch 설정 + Image 컴포넌트 추가
- `LobbyRoot`의 Image 컴포넌트 제거 또는 투명 처리

**방법 B — LobbyRoot의 Image를 Canvas 최상위로 이동**
- `LobbyRoot`를 `SafeAreaContainer`에서 꺼내 Canvas 직속으로 이동
- 단, `LobbyRoot`에는 `LobbyRootView` 스크립트가 붙어있고 하위 UI를 모두 포함하므로, Safe Area 적용이 필요한 UI도 함께 벗어나는 문제가 생김
- **이 방법은 부적합**

따라서 **방법 A**가 올바른 수정 방향이다.
