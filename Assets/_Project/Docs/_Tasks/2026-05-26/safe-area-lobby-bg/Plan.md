# Plan — Safe Area 로비 배경이미지 처리

## 이 작업은 무엇인가?

로비 화면을 노치/홈바가 있는 실기기에서 보면 배경색이 화면 전체를 채우지 않고 Safe Area 안쪽 경계에서 끊겨 보이는 문제를 수정한다.
원인은 배경 이미지가 Safe Area 축소 영역 안에 들어가 있기 때문이다. 이 작업은 Unity Inspector에서 씬 계층 구조를 조정하는 것이며, **코드 변경은 없다**.

---

## 근거 — GameSystemRules.md Rule 4

```
[UI] Canvas
  ├─ Background               ← 전체화면 배경 등 Safe Area 적용이 불필요한 요소
  └─ SafeAreaContainer        ← SafeAreaFitter 컴포넌트 부착
       └─ (모든 실제 UI 요소)  ← 팝업, HUD, 버튼 등 전부 이 안에 배치
```

> "전체화면을 채워야 하는 배경 요소는 SafeAreaContainer 밖(Canvas 직속)에 배치한다."

---

## 현재 구조 (문제 상태)

```
Canvas
└── SafeAreaContainer  ← SafeAreaFitter 부착 (Safe Area 크기로 축소됨)
    └── LobbyRoot      ← Image 컴포넌트(남색 배경 r:0.059 g:0.059 b:0.102 a:1) + LobbyRootView.cs
        ├── TabBarView
        └── ContentArea
```

`LobbyRoot`에 붙은 `Image` 컴포넌트가 배경 역할을 하는데, 이 오브젝트가 `SafeAreaContainer` 안에 있어서 Safe Area 크기만큼만 그려진다.

---

## 수정 후 목표 구조

```
Canvas
├── LobbyBackground    ← 새로 생성. Image 컴포넌트(남색 배경). Canvas 직속으로 Safe Area 무관
└── SafeAreaContainer  ← SafeAreaFitter 부착 (기존 그대로)
    └── LobbyRoot      ← Image 컴포넌트 제거(또는 alpha=0). LobbyRootView.cs는 유지
        ├── TabBarView
        └── ContentArea
```

---

## 수정 항목

### 항목 1 — `LobbyBackground` 오브젝트 생성

- **위치**: `Canvas`의 직속 자식. **`SafeAreaContainer`보다 위(=먼저 그려짐)에 배치**
  - 이유: Unity UI의 렌더 순서는 위에서 아래로 그려지므로, 배경이 맨 아래에 있어야 다른 UI 위에 겹쳐지지 않음
- **RectTransform**: Anchor Min = (0, 0), Anchor Max = (1, 1), Offset 전부 0 → 화면 전체 채움
- **Image 컴포넌트 추가**: Color = `r:0.059 g:0.059 b:0.102 a:1` (현재 LobbyRoot Image와 동일)

### 항목 2 — `LobbyRoot`의 Image 컴포넌트 비활성화 또는 제거

- `LobbyRoot`에서 Image 컴포넌트를 **제거**한다.
  - `LobbyRoot`는 `LobbyRootView.cs`와 하위 UI를 담는 컨테이너 역할이므로, 배경을 별도 오브젝트로 분리한 후 이 Image는 불필요하다.
  - **단, 테스트 통과 전까지는 제거 대신 `alpha = 0` 또는 컴포넌트 체크박스 비활성화 처리** (원상복구 가능하게).
  - 최종 삭제는 사용자 실기 테스트 통과 후 진행.

---

## 위험 요소

| 항목 | 내용 | 대응 |
|------|------|------|
| 렌더 순서 | `LobbyBackground`가 `SafeAreaContainer`보다 아래(나중) 배치되면 배경이 UI 위를 가림 | Hierarchy에서 `LobbyBackground`를 `SafeAreaContainer`보다 앞(위쪽)에 배치 |
| 이미지 색상 불일치 | 기존 Image 컬러와 다르게 지정하면 배경색이 달라짐 | r:0.059 g:0.059 b:0.102 a:1 그대로 사용 |
| 코드 참조 없음 확인 | 혹시 `LobbyRootView.cs` 또는 다른 스크립트가 LobbyRoot의 Image를 직접 참조하는지 확인 필요 | Research.md 분석 기준 해당 참조 없음 — 코드 변경 불필요 |

---

## 작업 방식

이 작업은 Unity Inspector(씬 계층 편집)만 사용하는 작업이다. 코드 파일 변경 없음.

사용자가 직접 Lobby.unity를 열어 Inspector에서 아래 순서로 작업한다:

1. `Canvas` 하위에 빈 GameObject 생성 → 이름을 `LobbyBackground`로 지정
2. `LobbyBackground`를 Hierarchy에서 `SafeAreaContainer` **위쪽(앞)**으로 이동
3. `LobbyBackground`의 RectTransform에서 Anchor Min/Max를 (0,0)/(1,1)로 설정, Offset 모두 0으로 설정
4. `LobbyBackground`에 `Image` 컴포넌트 추가 → Color를 `#0F0F1A` (r:0.059 g:0.059 b:0.102 a:1)로 설정
5. `SafeAreaContainer > LobbyRoot`에서 `Image` 컴포넌트 체크박스를 해제(비활성화)

---

## 코드 변경 없음

이 Plan에서 수정하는 파일은 `Lobby.unity` 씬 파일 하나뿐이며, 이는 Unity Inspector 조작으로 저장된다. `LobbyRootView.cs`, `SafeAreaFitter.cs` 등 C# 파일은 변경하지 않는다.
