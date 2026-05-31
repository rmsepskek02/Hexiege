# Plan — Lobby 씬 UI 공통 규칙 준수 수정

## 작업 목적 및 내용

Research.md에서 발견한 25건의 규칙 위반 항목을 수정한다.
모든 수정은 Lobby.unity 씬의 Inspector 설정 변경이며, 코드(C#) 변경은 없다.
변경량이 많으므로 Editor 1회성 스크립트를 그룹별로 작성하여 사용자가 실행하는 방식으로 진행한다.

---

## 수정 그룹 분류

| 그룹 | 대상 | 위반 건수 | 근거 규칙 |
|------|------|----------|----------|
| A | Toast Canvas | 4건 (규칙 1×1, 규칙 2×2, 규칙 5×1) | 규칙 1, 2, 5 |
| B | LoadingScreen | 3건 (규칙 2×3) | 규칙 2 |
| C | TabBar / ContentArea | 2건 (규칙 2×2) | 규칙 2 |
| D | VLG 내 버튼·텍스트 | 16건 (규칙 2×16) | 규칙 2 |

---

## ⚠️ 기존 로직 제거 규칙 적용

이번 작업은 Inspector 값 변경이므로 "기존 로직 제거" 항목은 없다.
단, SizeDelta 값 교체 시 기존 SizeDelta 값을 (0,0)으로 덮어쓰는 것이 유일한 "제거"에 해당하며,
이는 앵커 비율이 대신 크기를 결정하게 되므로 안전하다.

---

## 그룹 A: Toast Canvas 수정

### A-1. CanvasScaler 추가 (규칙 1)

**근거:** 규칙 1 "모든 씬의 Canvas는 Scale With Screen Size / 1080×1920 / Match=0으로 통일한다"

**수정 내용:** Toast Canvas GameObject에 CanvasScaler 컴포넌트 추가

| 항목 | 값 |
|------|-----|
| UI Scale Mode | Scale With Screen Size |
| Reference Resolution | 1080 × 1920 |
| Match Width Or Height | 0 |

---

### A-2. Toast > Background 앵커 비율화 (규칙 2)

**근거:** 규칙 2 "모든 UI 요소는 고정 픽셀 크기 대신 앵커 비율 기반으로 배치한다"

**현재 상태:**
```
anchorMin: (0.5, 0.5)  anchorMax: (0.5, 0.5)
AnchoredPosition: (0, 0)
SizeDelta: (700, 80)
Pivot: (0.5, 0)
```

**수정 후:**
```
anchorMin: (0.176, 0.5)   anchorMax: (0.824, 0.542)
AnchoredPosition: (0, 0)
SizeDelta: (0, 0)
Pivot: (0.5, 0.5)
```

계산 근거 (기준 해상도 1080×1920):
- 가로: 700÷1080 ≈ 0.648 → 좌우 여백 각 (1-0.648)÷2 ≈ 0.176
- 세로: 현재 Pivot=(0.5,0)이므로 Background 바닥면이 캔버스 중앙(y=0.5)에 위치,
  상단은 중앙+80px → anchorMin.y=0.5, anchorMax.y=0.5+80÷1920≈0.542
- 현재 시각적 위치를 그대로 유지하며 고정 픽셀만 제거

---

### A-3. Toast > Background > Message 앵커 비율화 (규칙 2)

**근거:** 규칙 2 "offsetMin, offsetMax 등 고정 픽셀값 사용 금지"

**현재 상태:**
```
anchorMin: (0, 0)  anchorMax: (1, 1)
SizeDelta: (-24, -24)   ← 좌우 12px, 상하 12px 고정 패딩
```

**수정 후:**
```
anchorMin: (0.017, 0.15)  anchorMax: (0.983, 0.85)
SizeDelta: (0, 0)
```

계산 근거:
- 가로 패딩 12÷700 ≈ 0.017
- 세로 패딩 12÷80 = 0.15

---

### A-4. Toast CanvasGroup 초기값 수정 (규칙 5)

**근거:** 규칙 5 "숨김 상태: alpha=0, blocksRaycasts=false, interactable=false"

**현재 상태:**
```
alpha: 0
blocksRaycasts: 1  ← false여야 함
interactable: 1    ← false여야 함
```

**수정 후:**
```
alpha: 0
blocksRaycasts: 0
interactable: 0
```

참고: ToastUI.cs의 Awake()→ClearAll()이 런타임에 이미 올바르게 초기화하므로 동작 변화 없음. Inspector 초기값을 규칙에 맞게 정렬하는 목적.

---

## 그룹 B: LoadingScreen 수정

### B-1. Spinner 앵커 비율화 (규칙 2)

**근거:** 규칙 2 "모든 UI 요소는 고정 픽셀 크기 대신 앵커 비율 기반으로 배치한다"

**현재 상태:**
```
anchorMin: (0.5, 0.5)  anchorMax: (0.5, 0.5)
AnchoredPosition: (0, 80)
SizeDelta: (120, 120)
```

**수정 후:**
```
anchorMin: (0.444, 0.511)  anchorMax: (0.556, 0.573)
AnchoredPosition: (0, 0)
SizeDelta: (0, 0)
```

계산 근거:
- 가로: 120÷1080 ≈ 0.111 → 반폭 0.056 → min=0.5-0.056=0.444, max=0.5+0.056=0.556
- 세로 중심: 0.5 + 80÷1920 ≈ 0.542 → 반높이 60÷1920 ≈ 0.031 → min=0.511, max=0.573

---

### B-2. LoadingScreen > StatusText 앵커 비율화 (규칙 2)

**현재 상태:**
```
anchorMin: (0.5, 0.5)  anchorMax: (0.5, 0.5)
AnchoredPosition: (0, -60)
SizeDelta: (700, 80)
```

**수정 후:**
```
anchorMin: (0.176, 0.448)  anchorMax: (0.824, 0.490)
AnchoredPosition: (0, 0)
SizeDelta: (0, 0)
```

계산 근거:
- 가로: 700÷1080 ≈ 0.648 → 반폭 0.324 → min=0.176, max=0.824
- 세로 중심: 0.5 - 60÷1920 ≈ 0.469 → 반높이 40÷1920 ≈ 0.021 → min=0.448, max=0.490

---

### B-3. CodeInput > Text Area 앵커 비율화 (규칙 2)

**현재 상태:**
```
anchorMin: (0, 0)  anchorMax: (1, 1)
SizeDelta: (-40, -10)   ← 좌우 20px, 상하 5px 고정 패딩
```

**수정 후:**
```
anchorMin: (0.02, 0.05)  anchorMax: (0.98, 0.95)
SizeDelta: (0, 0)
```

계산 근거:
- 가로 패딩: CodeInput 가로는 VLG 너비를 따르므로 전체 폭 대비 약 2%로 근사
- 세로 패딩: CodeInput 높이 100px 기준 → 5÷100 = 0.05

---

## 그룹 C: TabBar / ContentArea 수정

### C-1. TabBar 앵커 비율화 (규칙 2)

**근거:** 규칙 2 "sizeDelta 고정 픽셀값 사용 금지"

**현재 상태:**
```
anchorMin: (0, 0)  anchorMax: (1, 0)
SizeDelta: (0, 140)   ← 하단 탭바 높이 140px 고정
```

**수정 후:**
```
anchorMin: (0, 0)  anchorMax: (1, 0.073)
AnchoredPosition: (0, 0)
SizeDelta: (0, 0)
```

계산 근거: 140 ÷ 1920 ≈ 0.073

---

### C-2. ContentArea 앵커 비율화 (규칙 2)

**현재 상태:**
```
anchorMin: (0, 0)  anchorMax: (1, 1)
AnchoredPosition: (0, 70)
SizeDelta: (0, -140)
```

**수정 후:**
```
anchorMin: (0, 0.073)  anchorMax: (1, 1)
AnchoredPosition: (0, 0)
SizeDelta: (0, 0)
```

TabBar가 하단 0~7.3% 영역을 차지하므로 ContentArea는 7.3%~100% 영역을 사용한다.

---

## 그룹 D: VLG 내 버튼·텍스트 수정 (규칙 2)

**근거:** 규칙 2 "VerticalLayoutGroup + Control Child Size + Child Force Expand를 활성화하면 CellSize 없이 가용 공간을 자동으로 균등 분배한다"

### 수정 방향

현재 각 패널의 VerticalLayoutGroup이 `ChildControlHeight: 0`으로 설정되어 있어, 자식의 SizeDelta.y 고정값이 그대로 높이로 사용되고 있다.

**수정 방법:**
1. 각 패널 VLG: `ChildControlHeight → 1`, `ChildForceExpandHeight → 1`으로 변경
2. 각 자식에 `LayoutElement` 컴포넌트 추가:
   - 현재 SizeDelta.y=100인 버튼 → `flexibleHeight: 2`
   - 현재 SizeDelta.y=50인 텍스트 → `flexibleHeight: 1`
3. 모든 자식의 SizeDelta → (0, 0)으로 변경

이 방식으로 버튼이 텍스트보다 2배 높이를 차지하는 비율을 유지하면서, 절대 픽셀 대신 가용 공간을 비율로 분배한다.

### 수정 대상 목록

**BattleMainPanel VLG:**
| 오브젝트 | 현재 SizeDelta.y | 수정 후 flexibleHeight |
|----------|-----------------|----------------------|
| SingleplayBtn | 100 | 2 |
| CustomBtn | 100 | 2 |
| RandomBtn | 100 | 2 |

**CustomGamePanel VLG:**
| 오브젝트 | 현재 SizeDelta.y | 수정 후 flexibleHeight |
|----------|-----------------|----------------------|
| CreateRoomBtn | 100 | 2 |
| JoinByCodeBtn | 100 | 2 |
| BackBtn | 100 | 2 |

**CustomHostPanel VLG:**
| 오브젝트 | 현재 SizeDelta.y | 수정 후 flexibleHeight |
|----------|-----------------|----------------------|
| CodeText | 50 | 1 |
| PlayersText | 50 | 1 |
| StatusText | 50 | 1 |
| ErrorText | 50 | 1 |
| CancelBtn | 100 | 2 |

**CustomJoinPanel VLG:**
| 오브젝트 | 현재 SizeDelta.y | 수정 후 flexibleHeight |
|----------|-----------------|----------------------|
| CodeInput | 100 | 2 |
| JoinBtn | 100 | 2 |
| BackBtn | 100 | 2 |

**RandomMatchPanel VLG:**
| 오브젝트 | 현재 SizeDelta.y | 수정 후 flexibleHeight |
|----------|-----------------|----------------------|
| StatusText | 50 | 1 |
| CancelBtn | 100 | 2 |

---

## 구현 방식

### Inspector 작업이 필요한 항목
모든 수정이 Inspector 값 변경이므로 각 그룹별로 1회성 Editor 스크립트를 작성하여 사용자에게 실행 요청한다.

| 스크립트 | 담당 그룹 | 메뉴 경로 |
|----------|----------|----------|
| `FixLobbyUiGroupA.cs` | 그룹 A (Toast) | `Hexiege/Fix/LobbyUI/GroupA_Toast` |
| `FixLobbyUiGroupB.cs` | 그룹 B (LoadingScreen) | `Hexiege/Fix/LobbyUI/GroupB_LoadingScreen` |
| `FixLobbyUiGroupC.cs` | 그룹 C (TabBar/ContentArea) | `Hexiege/Fix/LobbyUI/GroupC_TabBar` |
| `FixLobbyUiGroupD.cs` | 그룹 D (VLG 버튼·텍스트) | `Hexiege/Fix/LobbyUI/GroupD_VLG` |

### 실행 순서
1. 그룹 A → 실행 후 Unity Editor에서 Toast UI 확인
2. 그룹 B → 실행 후 LoadingScreen 시각 확인
3. 그룹 C → 실행 후 TabBar/ContentArea 레이아웃 확인
4. 그룹 D → 실행 후 각 패널 버튼·텍스트 레이아웃 확인
5. 씬 저장 후 에디터 Play Mode 실기 확인

### 주의 사항
- 각 Editor 스크립트는 실행 전 `Lobby.unity`가 열려 있어야 한다
- 실행 후 씬을 반드시 저장(Ctrl+S)해야 변경 사항이 유지된다
- 그룹 D의 flexibleHeight 비율 조정 후 실제 레이아웃이 의도한 모습과 다르면 비율값을 수동으로 조정할 수 있다
- 그룹 A의 Toast Background 앵커 계산값은 근사치이므로 실기 확인 후 미세 조정이 필요할 수 있다

---

## 위험 요소

| 항목 | 위험 | 대응 |
|------|------|------|
| 그룹 C (TabBar/ContentArea) | 비율 전환 후 기존 UI 요소 위치 어긋남 가능 | 실기 확인 후 anchorMax.y 값 미세 조정 |
| 그룹 D (VLG 전환) | flexibleHeight 비율이 실제 UI 높이와 다를 수 있음 | 패널별 실기 확인 후 flexibleHeight 수동 조정 |
| 그룹 A (Toast) | 앵커 계산값이 근사치 | Canvas Scaler 추가 후 Play Mode에서 Toast 표시 확인 |
