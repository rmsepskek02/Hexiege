# Plan — 전체 UI 규칙 준수 검증

코드 전수 감사와 씬 YAML 파일 검토를 통해 10개 공통 UI 규칙 중 7개(Rule 1, 4, 5, 7, 8, 9, 10)는 이미 준수 중임을 확인했다.
이 작업은 나머지 3개 규칙(Rule 2, 3, 6)을 Unity Editor에서 직접 검증하고 미준수 항목이 있으면 수정하는 것이다.

---

## 이미 확인 완료된 규칙 (작업 불필요)

| 규칙 | 결과 | 근거 |
|------|------|------|
| Rule 1 (Canvas Scaler) | ✅ 준수 | 씬 YAML 직접 확인: 모든 Canvas 1080×1920 / 0 |
| Rule 4 (SafeArea) | ✅ 준수 | 씬 YAML 직접 확인: SafeAreaContainer + SafeAreaFitter 모두 존재 |
| Rule 5 (CanvasGroup 패턴) | ✅ 준수 | 코드 전수 감사 완료 |
| Rule 7 (골드 부족 색상) | ✅ 준수 | 코드 전수 감사 완료 |
| Rule 8 (팝업/모달 구분) | ✅ 준수 | 코드 전수 감사 완료 |
| Rule 9 (배경 탭 닫기) | ✅ 준수 | 코드 전수 감사 완료 |
| Rule 10 (LIFO 중첩) | ✅ 준수 | 코드 전수 감사 완료 |

---

## 작업 항목

### 작업 1. Rule 2 검증 — 앵커 기반 레이아웃

Unity Editor에서 아래 씬/패널의 RectTransform을 확인한다.
고정 픽셀 Left/Right/Top/Bottom offset이 설정된 요소가 있으면 앵커 비율로 변환한다.

**확인 대상 — Game.unity**:
- `[UI] Canvas > SafeAreaContainer > GameHUD` 및 자식들
- `[UI] Canvas > SafeAreaContainer > ProductionPopup` 및 자식들
- `[UI] Canvas > SafeAreaContainer > BuildingPopup` 및 자식들
- `[UI] Canvas > SafeAreaContainer > BuildingActionPanel` 및 자식들
- `[UI] Canvas > SafeAreaContainer > InGameSettingsPanel` 및 자식들
- `[UI] Canvas > SafeAreaContainer > ConfirmPopup` 및 자식들
- `[UI] Canvas > SafeAreaContainer > GameEndPanel` 및 자식들

**확인 대상 — Lobby.unity**:
- 각 Canvas 아래 View들의 RectTransform

**판단 기준**:
- anchorMin = anchorMax (앵커가 한 점에 고정) + sizeDelta로 크기 설정 → 앵커 비율로 전환 검토 필요
- anchorMin ≠ anchorMax (stretch 앵커) + offset 0 → 정상
- 고정 크기가 의도된 요소(버튼, 아이콘 등)는 예외 허용

---

### 작업 2. Rule 3 검증 — Filled/Simple 부모의 자식 앵커

`Image.Type.Filled` 또는 `Image.Type.Simple` 타입의 이미지 안에 자식 이미지가 있는 경우,
자식 이미지의 앵커가 비율 기반(anchorMin ≠ anchorMax)인지 확인한다.

**이미 수정된 항목**:
- `Game.unity > ProductionPopup > ProductionPanel > ProgressBar > Fill`: anchorMin.x=0.14, anchorMax.x=0.86, offset=0 (2026-05-19 수정 완료)

**추가 확인 대상**:
- Game.unity 전체에서 Image.type이 Filled인 오브젝트 탐색
- 자식 이미지가 있다면 anchorMin/anchorMax 확인
- Lobby.unity에서도 동일하게 확인

---

### 작업 3. Rule 6 검증 — 기본 폰트 (Maplestory Light SDF)

Unity Editor에서 아래 항목을 확인한다.

**확인 방법**:
1. Project Settings > TextMeshPro > Default Font Asset 확인
2. 각 씬에서 TMP 텍스트 컴포넌트의 Font Asset 필드 확인

**폰트 에셋 경로**: `Assets/_Project/Fonts/Maplestory Light SDF.asset`

**확인 대상**:
- Game.unity의 HUD 텍스트들 (골드, 인구, 타이머)
- Game.unity의 팝업 내 텍스트들
- Lobby.unity의 각 View 텍스트들

**예외**:
- `FloatingHpText` TMP는 독립 Material을 사용하며 의도된 예외 (memory_ui.md 기록됨)

---

## 작업 순서

1. Unity Editor에서 Game.unity 씬 열기
2. Rule 3 확인 (Filled 이미지 자식 앵커) — 범위가 좁아 빠름
3. Rule 2 확인 (앵커 기반 레이아웃) — 각 패널 순서대로
4. Rule 6 확인 (폰트) — TMP 텍스트 샘플링
5. 미준수 항목 발견 시 즉시 수정 → Lobby.unity 동일 반복

---

## 위험 요소

- Rule 2 수정 시 레이아웃이 의도와 달라질 수 있음 → 수정 후 Game View에서 1080×1920, 1080×2340 해상도 모두 확인
- Rule 3 수정은 소수 항목이 예상되므로 위험 낮음
- Rule 6의 경우 전체 폰트를 바꾸면 폰트 크기/줄간격이 달라질 수 있음 → 수정 전 현재 텍스트 상태 확인 필수
