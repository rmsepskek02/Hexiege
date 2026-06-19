# Plan — NetworkErrorPopup 구조 ConfirmPopup에 맞추기

## 작업 개요

Login.unity 씬의 NetworkErrorPopup 오브젝트 계층을 Game.unity의 ConfirmPopup 기준으로 통일한다.
C# 코드 변경은 없으며, Unity Editor Inspector 작업만으로 완료된다.

---

## 수정 대상 파일

- `Assets/_Project/Scenes/Login.unity` — Unity Editor Inspector에서 직접 수정

---

## 전체 변경 목록 (순서대로 진행)

### STEP 1. PopupBox → Panel (이름 + RectTransform + 컴포넌트)

**오브젝트**: `NetworkErrorPopup > PopupBox`

| 작업 | 내용 |
|------|------|
| 이름 변경 | `PopupBox` → `Panel` |
| AnchorMin | `(0.167, 0.380)` → `(0.1, 0.3)` |
| AnchorMax | `(0.833, 0.620)` → `(0.9, 0.7)` |
| AnchoredPosition | 유지 (0, 0) |
| SizeDelta | 유지 (0, 0) |
| Pivot | 유지 (0.5, 0.5) |
| CanvasGroup 추가 | Alpha=1, Interactable=true, BlocksRaycasts=true |
| VerticalLayoutGroup 제거 | 컴포넌트 삭제 |

> AnimatedPanel, Image, CanvasRenderer는 그대로 유지.

---

### STEP 2. MessageText (RectTransform + 컴포넌트)

**오브젝트**: `NetworkErrorPopup > Panel > MessageText`

| 작업 | 내용 |
|------|------|
| 이름 | 유지 (`MessageText`) |
| AnchorMin | `(0, 1)` → `(0.08, 0.4)` |
| AnchorMax | `(0, 1)` → `(0.92, 0.88)` |
| AnchoredPosition | `(360, -115)` → `(0, 0)` |
| SizeDelta | `(640, 150)` → `(0, 0)` |
| Pivot | `(0.5, 0.5)` → `(0.5, 1)` |
| LayoutElement 제거 | 컴포넌트 삭제 |

---

### STEP 3. ButtonContainer (RectTransform + 컴포넌트)

**오브젝트**: `NetworkErrorPopup > Panel > ButtonContainer`

| 작업 | 내용 |
|------|------|
| 이름 | 유지 (`ButtonContainer`) |
| AnchorMin | `(0, 1)` → `(0.08, 0.08)` |
| AnchorMax | `(0, 1)` → `(0.92, 0.35)` |
| AnchoredPosition | `(360, -269)` → `(0, 0)` |
| SizeDelta | `(640, 110)` → `(0, 0)` |
| Pivot | 유지 (0.5, 0.5) |
| LayoutElement 제거 | 컴포넌트 삭제 |
| HorizontalLayoutGroup Padding | `L:0 R:0 T:0 B:0` → `L:40 R:40 T:20 B:20` |
| HorizontalLayoutGroup ChildAlignment | `0` → `4` (Center) |
| HorizontalLayoutGroup Spacing | `20` → `50` |

---

### STEP 4. ConfirmButton (RectTransform + 컴포넌트)

**오브젝트**: `NetworkErrorPopup > Panel > ButtonContainer > ConfirmButton`

| 작업 | 내용 |
|------|------|
| 이름 | 유지 (`ConfirmButton`) |
| AnchorMin | `(0, 1)` → `(0, 0)` |
| AnchorMax | `(0, 1)` → `(0, 0)` |
| AnchoredPosition | `(155, -55)` → `(0, 0)` |
| SizeDelta | `(310, 110)` → `(0, 0)` |
| Pivot | 유지 (0.5, 0.5) |
| LayoutElement 제거 | 컴포넌트 삭제 |

---

### STEP 5. CancelButton (RectTransform + 컴포넌트)

**오브젝트**: `NetworkErrorPopup > Panel > ButtonContainer > CancelButton`

| 작업 | 내용 |
|------|------|
| 이름 | 유지 (`CancelButton`) |
| AnchorMin | `(0, 1)` → `(0, 0)` |
| AnchorMax | `(0, 1)` → `(0, 0)` |
| AnchoredPosition | `(485, -55)` → `(0, 0)` |
| SizeDelta | `(310, 110)` → `(0, 0)` |
| Pivot | 유지 (0.5, 0.5) |
| LayoutElement 제거 | 컴포넌트 삭제 |

---

### STEP 6. Label → Text (이름 변경)

**오브젝트**: ConfirmButton > Label, CancelButton > Label

| 작업 | 내용 |
|------|------|
| `ConfirmButton > Label` 이름 변경 | `Label` → `Text` |
| `CancelButton > Label` 이름 변경 | `Label` → `Text` |
| RectTransform | 이미 동일 (anchor 0,0 ~ 1,1 stretch) — 변경 없음 |

---

### STEP 7. CloseButton (변경 없음)

**오브젝트**: `NetworkErrorPopup > Panel > CloseButton`

- NetworkErrorPopup 전용 기능 — 유지
- STEP 1의 Panel Anchor 변경에 따라 상대 위치가 자동 조정되므로 별도 수정 불필요

---

## 작업 방식

모든 작업은 **Unity Editor Inspector에서 직접** 수행한다.

1. Login.unity 씬 열기
2. Hierarchy에서 각 오브젝트 선택
3. Inspector에서 RectTransform 값 수정 및 컴포넌트 추가/제거
4. 씬 저장 (Ctrl+S)

---

## 위험 요소

- `ConfirmPopup.cs`의 `_panel` 필드는 AnimatedPanel **컴포넌트** 참조 (fileID 기반) → 이름 변경해도 참조 유지됨
- `_confirmButtonText`, `_cancelButtonText` 필드도 TMP 컴포넌트 참조 → Label→Text 이름 변경과 무관
- VerticalLayoutGroup 제거 후 자식 위치가 앵커 기반으로 재배치되므로 MessageText, ButtonContainer Anchor 설정을 반드시 같이 변경해야 레이아웃이 깨지지 않음
