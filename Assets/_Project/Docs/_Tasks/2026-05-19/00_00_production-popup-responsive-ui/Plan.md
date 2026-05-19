# Plan — ProductionPopup 반응형 UI 개선 ✅ 완료 (2026-05-19)

## 이 작업은 무엇을 왜 하는가?

실기기에서 생산 팝업을 열면 초록색 게이지 바가 배경보다 좌우로 짧게 표시되어 빈 틈이 생깁니다.
이는 UI 요소가 에디터 해상도 기준의 고정 픽셀값으로 설정되어 있어서, 실기기의 다른 해상도에서는
화면에 맞게 올바르게 늘어나지 못하기 때문입니다.

이 작업에서는 문제의 원인이 되는 RectTransform 설정을 앵커 기반으로 수정하고,
ProductionPopup 전체가 다양한 기기 해상도에서 올바르게 표시되도록 개선합니다.

---

## 적용 규칙 (GameSystemRules.md 근거)

- **UI 전역 규칙**: "모든 UI는 고정 픽셀 크기(sizeDelta 등) 없이 앵커 기반으로 제작 — 다기기 해상도 대응"
  - 출처: `memory_ui.md` UI 전역 규칙 섹션 (2026-04-05 확정)
- **반응형 팝업 UI**: "ProductionPopup / BuildingPopup 자식 오브젝트들을 앵커 기반 배치로 전환"
  - 출처: `memory_ui.md` 반응형 팝업 UI 섹션

---

## 구현 접근법

### 핵심 원칙
Unity Inspector에서만 확인 가능한 RectTransform 수치들은 에디터 스크립트를 통해
진단 → 수정하는 방식으로 진행합니다.
코드 수정 없이 Inspector 설정만으로 해결 가능한 경우, 에디터 스크립트 1회 실행으로 처리합니다.

---

## 구현 단계

### 단계 1: 현재 상태 진단 (런타임 MonoBehaviour)

**목적**: Play Mode 진입 시 자동으로 관련 RectTransform 값들을 Unity Console에 출력

**작성 파일**: `Assets/_Project/Scripts/Presentation/UI/ProductionPopupDiagnostic.cs`
- Play Mode 시작 시 `Start()`에서 자동 실행
- 에디터에서 ProductionPopup GameObject에 컴포넌트로 임시 부착
- 출력 항목:
  - Canvas의 ScaleMode, Reference Resolution, Match 값
  - ProductionPopup 루트 패널의 anchorMin/anchorMax/offsetMin/offsetMax
  - `_progressFill` Image의 RectTransform 정보
  - ProgressBar 배경(부모) 오브젝트의 RectTransform 정보
  - GridLayoutGroup의 Cell Size, Spacing 정보

**실행 방법**: 사용자가 Game.unity 씬을 열고 ProductionPopup GameObject에 컴포넌트 부착 → Play Mode 진입 → Console 로그 확인 후 공유

---

### 단계 2: 문제 수정 (에디터 스크립트)

진단 결과를 바탕으로 아래 항목을 수정하는 에디터 스크립트 작성:

**작성 파일**: `Assets/_Project/Scripts/Editor/FixProductionPopupResponsive.cs`
- 메뉴: `Hexiege/Fix/ProductionPopup 반응형 수정`
- Game.unity 씬에서 실행

**수정 항목 (우선순위 순)**:

#### 수정 1 — 생산 게이지 RectTransform (최우선 버그 수정)
게이지 Image(`_progressFill`)를 부모에 완전히 맞춤:
- anchorMin = (0, 0), anchorMax = (1, 1)
- offsetMin = (0, 0), offsetMax = (0, 0)
- 적용 근거: fillMethod=Horizontal Image는 부모 크기에 정확히 맞아야 게이지가 좌우 빈 틈 없이 채워짐

#### 수정 2 — 팝업 전체 패널 사이징
팝업 루트 패널이 고정 높이(px)로 설정된 경우 → 화면 비율 기반으로 변경:
- anchorMin = (0.5, 0), anchorMax = (0.5, 0) → 하단 중앙 앵커
- 고정 너비 대신 화면 너비의 비율(예: 90%) 기반 설정
- 적용 근거: 모바일 portrait 9:16 기기별 화면 크기 대응 필수

#### 수정 3 — Grid Layout Cell Size 비율 기반 전환
UnitButtons GridLayoutGroup이 고정 px Cell Size인 경우:
- ContentSizeFitter 또는 비율 기반 Cell Size로 전환
- 셀 높이/너비를 AspectRatioFitter로 제어하는 방식 적용
- 적용 근거: 기기 해상도 차이로 버튼이 잘리거나 간격이 부정확해지는 문제 방지

---

### 단계 3: Canvas Scaler 확인

진단 결과에 Canvas Scaler 이상이 있는 경우:
- `UI Scale Mode = Scale With Screen Size`
- `Reference Resolution = 1080 × 1920` (9:16 portrait 기준)
- `Match = 0` (Width 기준) 또는 `Match = 1` (Height 기준) — portrait에서는 Height 기준(1) 권장

이 항목은 에디터 스크립트 대신 Inspector에서 직접 수정 권고
(Canvas Scaler는 Canvas 루트에 하나라 수동 수정이 안전함)

---

## 수정 전후 비교

| 항목 | 수정 전 | 수정 후 |
|------|---------|---------|
| 게이지 RectTransform | 고정 픽셀 offset | stretch 앵커, offset=0 |
| 팝업 패널 크기 | 고정 px 높이 | 화면 비율 기반 앵커 |
| 버튼 셀 크기 | 고정 px Cell Size | AspectRatioFitter 비율 제어 |
| 실기기 게이지 | 좌우 빈 틈 발생 | 배경과 정확히 일치 |

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| 에디터에서는 정상이었던 UI가 수정 후 틀어질 수 있음 | 에디터에서 Play Mode로 미리 확인 후 실기기 테스트 |
| Grid Layout 수정 시 버튼 위치/크기 변경 가능성 | 진단 로그 먼저 확인 후 필요한 항목만 수정 |
| Canvas Scaler 수정 시 게임 전체 UI 영향 | Canvas Scaler는 별도로 신중하게 처리 |

---

## 승인 후 구현 순서

1. **DiagnoseProductionPopupUI.cs 작성** → 사용자 실행 → 진단 로그 공유
2. 진단 결과 기반으로 **FixProductionPopupResponsive.cs 작성** → 사용자 실행
3. 에디터 Play Mode 확인 → 실기기 테스트
4. 이상 없으면 에디터 스크립트 2개 삭제 (1회성)

---

## 변경 파일 목록

| 파일 | 변경 내용 |
|------|----------|
| `Presentation/UI/ProductionPopupDiagnostic.cs` | 신규 생성 (1회성 진단용 MonoBehaviour, 이후 삭제) |
| `Editor/FixProductionPopupResponsive.cs` | 신규 생성 (1회성 수정용 에디터 스크립트, 이후 삭제) |
| `Game.unity` (씬 파일) | 에디터 스크립트 실행 결과로 RectTransform 값 변경 |
