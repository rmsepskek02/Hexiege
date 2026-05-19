# Research — ProductionPopup 반응형 UI 개선

## 이 작업은 무엇인가?

실기기(모바일 기기)에서 생산 팝업(ProductionPopup)을 열면, 생산 진행바의 초록색 게이지가
배경 이미지보다 좌우로 짧게 보여서 양 끝에 빈 틈이 생기는 문제가 있습니다.
이 문제의 근본 원인은 UI 요소들이 에디터 해상도 기준의 고정 픽셀 크기로 설정되어 있기 때문입니다.

이 작업은 두 가지를 목표로 합니다:
1. **즉각 버그 수정**: 생산바 게이지의 좌우 빈 틈 제거
2. **반응형 개선**: ProductionPopup 전체가 다양한 기기 해상도에서 올바르게 표시되도록 UI 구조 개선

---

## 현재 관련 파일

| 파일 | 역할 |
|------|------|
| `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs` | 생산 패널 UI 로직 |
| `Assets/_Project/Scripts/Presentation/UI/BuildingPanelBase.cs` | 공통 베이스 클래스 (팝업/헤더/닫기/철거) |
| `Assets/_Project/Scripts/Editor/SetupProductionPopupUI.cs` | 1회성 Inspector 자동 배선 에디터 스크립트 |

게임 씬의 ProductionPopup Prefab/GameObject는 Game.unity 씬 계층에 직접 배치되어 있음.

---

## 문제 분석: 생산바 게이지 좌우 빈 틈

### 원인

Unity UI Image의 `fillMethod = Horizontal`(좌→우 채우기) 방식으로 구현된 `_progressFill`은
부모 RectTransform의 크기에 의존한다.

**전형적인 문제 패턴**:
- 게이지 `_progressFill`의 RectTransform이 에디터 해상도 기준 고정 픽셀 offset(`Left`/`Right` 등)으로 설정되어 있음
- 실기기에서 Canvas Scaler가 해상도를 스케일하면 게이지 부모 컨테이너 크기와 게이지 자체 크기 사이에 픽셀 단위 불일치가 발생
- 결과: 배경은 올바른 폭으로 표시되지만 게이지는 좌우에 고정 픽셀만큼 짧게 표시됨

### 올바른 수정 방향

게이지 Image의 RectTransform을 **완전 stretch 앵커** (anchorMin=0,0 / anchorMax=1,1)로 설정하고
모든 offset(Left/Right/Top/Bottom)을 0으로 설정하면 부모 크기에 정확히 맞게 늘어남.

---

## 반응형 UI 원칙 (프로젝트 규칙)

`memory_ui.md` 및 `GameSystemRules.md`에 명시된 프로젝트 UI 규칙:

> **"모든 UI는 고정 픽셀 크기(sizeDelta 등) 없이 앵커 기반으로 제작 — 다기기 해상도 대응"**

현재 ProductionPopup이 이 규칙을 완전히 준수하고 있는지 Inspector에서 검증이 필요함.

### Canvas Scaler 설정 확인 필요 항목
- `UI Scale Mode`: `Scale With Screen Size` 여부
- `Reference Resolution`: 9:16 비율 기준 (모바일 portrait mode)
- `Match`: Width vs Height 비율

### ProductionPopup 구조상 반응형에 취약한 요소들

| 요소 | 취약 가능성 | 이유 |
|------|------------|------|
| `_progressFill` (게이지) | **높음** | fillMethod 이미지는 부모 크기 의존성 높음 |
| 팝업 전체 패널 높이 | 중간 | 고정 높이(px)이면 화면 비율에 따라 잘림 |
| UnitButtons Grid | 중간 | Grid Layout 셀 크기가 고정 px이면 버튼 크기 부정확 |
| 큐 슬롯 이미지 | 낮음 | AspectRatioFitter 적용 여부에 따라 다름 |

---

## 코드에서 파악한 생산바 관련 코드

`ProductionPanelUI.cs:651`:
```csharp
private void UpdateProgressBar()
{
    if (_progressFill != null && _currentBuilding != null && _production != null)
        _progressFill.fillAmount = _production.GetState(_currentBuilding.Id)?.Progress ?? 0f;
}
```

- `_progressFill`은 `Image` 타입 (`[Header("Progress")] [SerializeField] private Image _progressFill`)
- fillAmount(0~1) 방식으로 진행도를 표현함
- fillAmount 자체는 정상 — 문제는 Image가 렌더링되는 RectTransform 크기임

---

## 작업 범위

### 버그 수정 (즉각)
- `_progressFill` RectTransform: 완전 stretch → offset 0 검증 및 수정
- 배경 이미지(ProgressBar 배경)와 게이지 Image의 계층 구조 및 앵커 검증

### 반응형 개선 (전체)
- 팝업 전체 패널: 화면 비율에 맞는 앵커 기반 사이징 적용
- UnitButtons 영역: Grid Layout 셀 크기를 고정 px 대신 비율 기반으로 전환
- Canvas Scaler 설정이 Reference Resolution 기준(예: 1080×1920)과 일치하는지 확인

---

## 확인 필요 사항 (Inspector에서 직접 확인 필요)

아래 항목은 코드가 아닌 씬 데이터(prefab/씬 파일의 YAML)에 저장되어 있어
현재 정확한 수치는 유니티 에디터에서만 확인 가능합니다:

1. `_progressFill` Image RectTransform의 anchorMin/anchorMax/offsetMin/offsetMax
2. ProgressBar 배경 Image의 RectTransform 설정
3. 팝업 전체 패널 RectTransform (고정 높이 여부)
4. Canvas Scaler의 Reference Resolution 및 Match 값
5. UnitButtons GridLayoutGroup의 Cell Size 설정
