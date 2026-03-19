# UI 가이드라인 — Hexiege

**최종 수정일:** 2026-03-19
**적용 범위:** 모든 UI 씬 (Game.unity, Lobby.unity)

---

## 1. UI 분류 기준

### 1-1. Context(맥락)별 분류

| 분류 | 설명 | 현재 구현 예시 |
|------|------|--------------|
| **인게임 컨텍스트 패널** | 맵 상호작용으로 열리는 패널 (타일/건물 탭 시 등장) | BuildingPlacementUI, ProductionPanelUI |
| **게임 상태 알림 패널** | 게임 흐름 변화를 알리는 대형 UI | GameEndUI |
| **시스템 팝업** | 사용자 결정이 필요한 오버레이 다이얼로그 | RematchRequestPopup |
| **HUD** | 항상 표시되는 인게임 정보 | GameHudUI (골드, 인구, 타이머) |
| **토스트 알림** | 일시적 피드백 메시지 (자동 사라짐) | ToastNotification (구현 예정) |
| **로비 UI** | 로비 씬 탭/서브뷰 전환 | TabBarView, BattleRootView 서브뷰 |

---

### 1-2. 애니메이션 방향 기준

| 방향 | 의미 | 적용 분류 |
|------|------|---------|
| **하단 슬라이드 업** | "꺼내는" 느낌 — 맵 탭 → 패널 올라옴 | 인게임 컨텍스트 패널 (현재 및 향후 추가 패널 동일) |
| **상단 슬라이드 다운** | "드리우는" 느낌 — 게임 종료 선언 | 게임 상태 알림 패널 |
| **좌/우 슬라이드** | "전환하는" 느낌 — 탭 간 이동 | 로비 탭 전환 (UIAnimator SlideLeft/Right 구현 예정) |
| **DOFade만** | "나타나는" 느낌 — 위치 이동 없이 등장 | 시스템 팝업, 토스트 알림, 로비 서브뷰 전환 |
| **값 변화 애니메이션** | 숫자 증감, 색상 flash — 항상 표시되는 HUD 전용 | HUD |

---

## 2. 분류별 애니메이션 표준값

> `AnimatedPanel` 컴포넌트 Inspector 기본값 기준.
> 체감이 다르면 Inspector에서 개별 조정 가능.

### 인게임 컨텍스트 패널 (SlideFromBottom)
| 항목 | 값 |
|------|-----|
| AnimationType | `SlideFromBottom` |
| ShowDuration | `0.25f` |
| HideDuration | `0.2f` |
| SlideOffset | `300f` (px) |
| Ease | `OutCubic` (UIAnimator 고정) |
| 배경 오버레이 | 있는 경우 즉시 SetActive (하단 참조) |

### 게임 상태 알림 패널 (SlideFromTop)
| 항목 | 값 |
|------|-----|
| AnimationType | `SlideFromTop` |
| ShowDuration | `0.25f` |
| HideDuration | `0.2f` |
| SlideOffset | `300f` (px) |
| Ease | `OutCubic` (UIAnimator 고정) |

### 시스템 팝업 / 토스트 알림 (DOFade)
| 항목 | 값 |
|------|-----|
| 구현 방식 | `DOFade` 직접 호출 (AnimatedPanel 미사용) |
| FadeIn Duration | `0.2f` |
| FadeOut Duration | `0.15f` |
| SetUpdate | `true` (timeScale=0 대응) |

### 로비 탭 전환 (SlideLeft/Right) — 구현 예정
| 항목 | 값 |
|------|-----|
| AnimationType | `SlideLeft` / `SlideRight` (UIAnimator 추가 예정) |
| Duration | `0.25f` |
| Ease | `OutCubic` (예정) |

---

## 3. 배경 오버레이 처리 규칙

슬라이드 애니메이션이 있는 패널에 반투명 배경(Overlay)이 필요한 경우:

**규칙: 배경은 애니메이션 없이 즉시 SetActive**

| 시점 | 동작 |
|------|------|
| `Show()` 호출 | 배경 → `SetActive(true)` 즉시 (패널 슬라이드와 동시) |
| `Hide()` 완료 | 배경 → `SetActive(false)` 즉시 (패널 슬라이드 완료 후) |

### Inspector 연결 방법
1. 배경 오브젝트에 `CanvasGroup` 컴포넌트 부착
2. `AnimatedPanel` 컴포넌트의 `Background Overlay` 필드에 해당 CanvasGroup 연결
3. 배경 오브젝트는 초기 상태 `SetActive(false)` (Inspector에서 체크 해제)

> `Background Overlay` 필드가 비어있으면 기존 동작과 동일 — 기존 패널에 영향 없음.

### 씬 계층 예시
```
[UI] Canvas
  └─ ProductionPopup          ← AnimatedPanel 부착, BackgroundOverlay 연결
      ├─ Overlay              ← CanvasGroup 부착, alpha=0.5, 초기 비활성
      └─ ContentPanel         ← 실제 콘텐츠
```

---

## 4. 앵커 & 레이아웃 규칙

> 기기별 해상도 대응 (9:16 기준, Safe Area 적용 예정).

- **Canvas Scaler**: Scale With Screen Size, Reference Resolution 1080×1920
- **앵커 기반 배치 원칙**: 고정 픽셀값 대신 앵커 비율 사용 (반응형 팝업 참조)
- **반응형 팝업 에디터 스크립트**: `Hexiege/UI/Apply Responsive Popup Layout`
  - VerticalLayoutGroup 패딩(20) + spacing(8)
  - 슬롯에 AspectRatioFitter(1.5:1)
  - TMP `enableAutoSizing` 자동 활성화

---

## 5. 스타일 가이드

### 폰트
| 항목 | 값 |
|------|-----|
| 기본 폰트 | `Assets/_Project/Fonts/Maplestory Light SDF.asset` |
| 모든 TMP 텍스트 | 위 폰트 적용 (예외 없음) |

> 폰트 크기, 색상 팔레트, 버튼 스타일 등은 추후 UI 에셋 작업 진행 시 추가 기록.

---

## 6. Inspector 연결 체크리스트

### AnimatedPanel 부착 시
- [ ] `Animation Type` 설정 (SlideFromBottom / SlideFromTop / PopupFade)
- [ ] `Show Duration` / `Hide Duration` 기본값 확인
- [ ] `Slide Offset` (슬라이드 타입만, 기본 300f)
- [ ] `Background Overlay` — 배경이 있는 패널만 연결 (없으면 비워둠)
- [ ] 배경 오브젝트 초기 상태 `SetActive(false)` 확인

### CanvasGroup 관련
- [ ] AnimatedPanel이 있는 오브젝트: CanvasGroup 자동 추가됨 (수동 불필요)
- [ ] 배경 오버레이 오브젝트: CanvasGroup 수동 부착 필요
- [ ] 시스템 팝업(DOFade 직접 사용): CanvasGroup 수동 부착 필요

---

## 7. 향후 추가 UI 적용 가이드

### 인게임 컨텍스트 패널 추가 시
1. 패널 오브젝트에 `AnimatedPanel` 부착
2. `AnimationType = SlideFromBottom`
3. 반투명 배경이 필요하면 → 배경 오브젝트 생성 + CanvasGroup + `Background Overlay` 연결
4. 부모 스크립트에서 `_popup.Show()` / `_popup.Hide()` 호출

### 로비 탭 전환 추가 시 (구현 예정)
- `UIAnimator.SlideLeft()` / `SlideRight()` 추가 후 이 가이드 업데이트
- 현재 뷰: SlideOut → 새 뷰: SlideIn (방향에 따라)

### 토스트 알림 추가 시 (Phase 5 예정)
- `ToastNotification.cs` + `ToastManager.cs` 구현
- 애니메이션: DOFade (슬라이딩 없음)
- 성공(초록) / 실패(빨강) / 정보(파랑) 색상 구분
