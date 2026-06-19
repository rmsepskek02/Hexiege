# Research — NetworkErrorPopup 구조 ConfirmPopup에 맞추기

## 작업 목적

Login.unity 씬의 `NetworkErrorPopup`이 UIManager의 `ConfirmPopup`(Game.unity 원본)과
오브젝트 이름 및 RectTransform 구조가 달라 유지보수가 어렵다.
두 오브젝트의 계층 구조와 트랜스폼을 통일하여 일관성을 확보한다.

---

## 비교 기준

- **기준(목표)**: Game.unity → `ConfirmPopup` (UIManager 프리팹 원본)
- **수정 대상**: Login.unity → `NetworkErrorPopup`

---

## 현재 계층 구조 비교

### ConfirmPopup (Game.unity) — 기준
```
ConfirmPopup
  ├─ BlockingOverlay       (stretch)
  └─ Panel                 (anchor 0.1,0.3 ~ 0.9,0.7)
       ├─ MessageText       (anchor 0.08,0.4 ~ 0.92,0.88 / pivot 0.5,1)
       └─ ButtonRow         (anchor 0.08,0.08 ~ 0.92,0.35)
            ├─ ConfirmButton
            │    └─ Text
            └─ CancelButton
                 └─ Text
```

### NetworkErrorPopup (Login.unity) — 수정 대상
```
NetworkErrorPopup
  ├─ BlockingOverlay       (stretch) ✅
  └─ PopupBox              (anchor 0.167,0.380 ~ 0.833,0.620)
       ├─ CloseButton      (유지)
       ├─ MessageText       (anchor 0,1 ~ 0,1 / px 고정)
       └─ ButtonContainer   (anchor 0,1 ~ 0,1 / px 고정)
            ├─ ConfirmButton
            │    └─ Label
            └─ CancelButton
                 └─ Label
```

---

## 오브젝트별 상세 차이

### 1. PopupBox → Panel (이름 변경)

| 항목 | ConfirmPopup (기준) | NetworkErrorPopup (현재) | 변경 필요 |
|------|-------------------|------------------------|----------|
| 이름 | `Panel` | `PopupBox` | ✅ 변경 |
| AnchorMin | (0.1, 0.3) | (0.167, 0.380) | ✅ 변경 |
| AnchorMax | (0.9, 0.7) | (0.833, 0.620) | ✅ 변경 |
| AnchoredPosition | (0, 0) | (0, 0) | — |
| SizeDelta | (0, 0) | (0, 0) | — |
| Pivot | (0.5, 0.5) | (0.5, 0.5) | — |
| CanvasGroup | 있음 (alpha=1, interact=1, raycast=1) | **없음** | ✅ 추가 |
| VerticalLayoutGroup | **없음** | **있음** | ✅ 제거 |
| AnimatedPanel | 있음 (type:0, show:0.2, hide:0.15) | 있음 (동일) | — |
| Image | 있음 | 있음 | — |

### 2. MessageText

| 항목 | ConfirmPopup (기준) | NetworkErrorPopup (현재) | 변경 필요 |
|------|-------------------|------------------------|----------|
| 이름 | `MessageText` | `MessageText` | — |
| AnchorMin | (0.08, 0.4) | (0, 1) | ✅ 변경 |
| AnchorMax | (0.92, 0.88) | (0, 1) | ✅ 변경 |
| AnchoredPosition | (0, 0) | (360, -115) | ✅ 변경 |
| SizeDelta | (0, 0) | (640, 150) | ✅ 변경 |
| Pivot | (0.5, 1) | (0.5, 0.5) | ✅ 변경 |
| LayoutElement | **없음** | **있음** | ✅ 제거 |

### 3. ButtonContainer (ButtonRow → ButtonContainer로 통일)

| 항목 | ConfirmPopup (기준) | NetworkErrorPopup (현재) | 변경 필요 |
|------|-------------------|------------------------|----------|
| 이름 | `ButtonRow` | `ButtonContainer` | ConfirmPopup 쪽을 `ButtonContainer`로 맞춤 |
| AnchorMin | (0.08, 0.08) | (0, 1) | ✅ 변경 |
| AnchorMax | (0.92, 0.35) | (0, 1) | ✅ 변경 |
| AnchoredPosition | (0, 0) | (360, -269) | ✅ 변경 |
| SizeDelta | (0, 0) | (640, 110) | ✅ 변경 |
| Pivot | (0.5, 0.5) | (0.5, 0.5) | — |
| LayoutElement | **없음** | **있음** | ✅ 제거 |
| HorizontalLayoutGroup | 있음 | 있음 | — |
| HLG Padding | L:40 R:40 T:20 B:20 | L:0 R:0 T:0 B:0 | ✅ 변경 |
| HLG ChildAlignment | 4 (Center) | 0 (Upper Left) | ✅ 변경 |
| HLG Spacing | 50 | 20 | ✅ 변경 |

### 4. Label → Text (버튼 텍스트 이름 변경)

| 항목 | ConfirmPopup (기준) | NetworkErrorPopup (현재) | 변경 필요 |
|------|-------------------|------------------------|----------|
| ConfirmButton 텍스트 이름 | `Text` | `Label` | ✅ 변경 |
| CancelButton 텍스트 이름 | `Text` | `Label` | ✅ 변경 |
| Text anchor | (0,0) ~ (1,1) stretch | 미확인 | 확인 필요 |

### 5. CloseButton (NetworkErrorPopup 전용)

ConfirmPopup에는 없으나 NetworkErrorPopup에만 존재 → 유지.

| 항목 | 값 | 변경 필요 |
|------|---|----------|
| AnchorMin | (0.833, 0.867) | — |
| AnchorMax | (1, 1) | — |
| AnchoredPosition | (0, 0) | — |
| SizeDelta | (0, 0) | — |
| Pivot | (1, 1) 우상단 기준 | — |
| **LayoutElement** | IgnoreLayout=true (VerticalLayoutGroup 무시용) | ✅ 제거 (VLG 제거 후 불필요) |
| Image, Button | 있음 | — |

### 6. 변경 없는 항목

- `NetworkErrorPopup` 루트 이름 — 유지
- `ConfirmPopup` 이름 — 유지
- `BlockingOverlay` — 구조/트랜스폼 동일, 변경 없음

---

## 영향 범위

- **Login.unity 씬 파일만** 수정
- `ConfirmPopup.cs` 코드 변경 없음 (컴포넌트 참조는 Inspector fileID로 연결되므로 이름 변경과 무관)
- `LoginRootView.cs` 등 C# 코드 변경 없음

---

## 미확인 항목

- ConfirmButton / CancelButton 자체 RectTransform 상세값 (버튼 본체)
- Text(Label) 자식의 RectTransform 상세값
→ Plan.md 작성 전 추가 비교 필요
