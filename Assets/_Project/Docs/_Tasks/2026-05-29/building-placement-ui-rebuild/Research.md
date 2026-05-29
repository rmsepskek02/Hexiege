# Research — BuildingPlacementUI 재설계

## 작업 목적

BuildingPlacementUI는 실기기 테스트에서 패널 높이 부족, 버튼이 패널 테두리를 침범, 골드 아이콘 Y축 정렬 불일치 등 레이아웃 구조 자체에 문제가 있어 FAIL 판정을 받았습니다.
수치 조정만으로는 해결이 어렵다고 판단하여 패널 전체를 GameSystemRules에 맞게 처음부터 재설계합니다.

---

## 현재 코드 구조

**파일**: `Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs`

### Inspector 참조 필드

| 필드 | 타입 | 설명 |
|------|------|------|
| `_popup` | AnimatedPanel | 팝업 래퍼 (Show/Hide 담당) |
| `_sharedBackground` | SharedBackgroundButton | 외부 탭 닫기 |
| `_buildingButtons` | List\<Button\> (9개) | 건물 선택 버튼 |
| `_buildingButtonIcons` | List\<Image\> (9개) | 각 버튼의 건물 아이콘 |
| `_buildingCostTexts` | List\<TextMeshProUGUI\> (9개) | 각 버튼의 비용 텍스트 |
| `_cancelButton` | Button | 팝업 닫기 버튼 (X 이미지) |

> 골드 아이콘 Image 참조 필드는 현재 코드에 없음. 버튼 내부 UI 구성 변경 시 추가 필요.

### 주요 동작 흐름

- `Show(coord, team)`: 팝업 열기 → 종족별 건물 리스트 조회 → 버튼 바인딩 → CanvasGroup으로 빈 슬롯 숨김 → 비용 색상 평가
- `Close()`: 팝업 닫기 → 비용 텍스트 색상 초기화(흰색) → 이벤트 구독 해제
- 빈 슬롯 처리: `CanvasGroup.alpha=0` (SetActive 사용 안 함 → 레이아웃 공간 유지)

---

## 현재 씬 계층 구조 (Game.unity 분석)

```
BuildingPopup
  anchorMin={0,0}, anchorMax={1,1} ← 전체화면 차지 (문제)
  └── (하위 오브젝트 1개)
        └── BuildingPanel
              ├── CancelButton (X 이미지, anchorMin={0.87,0.78}, anchorMax={1,1})
              └── BuildingButtons (그리드 영역)
                    └── BuildingImage × 9 (버튼들)
```

BuildingPopup이 전체 화면을 차지하는 래퍼 역할이고, 실제 시각적 패널은 내부에 있는 BuildingPanel입니다. 이 구조를 유지하되 BuildingPanel의 앵커 설정이 핵심입니다.

---

## 실기 테스트 결과 (Testcase.md 기준)

| TC ID | 결과 | 원인 |
|-------|------|------|
| TC-SINGLE-BP-001 | FAIL | 패널 높이 부족, 버튼 가로가 테두리 침범, 버튼 크기 작음 |
| TC-SINGLE-BP-002 | FAIL | 골드 아이콘 Y축 정렬 불일치 |

---

## 재설계 방향 요약 (논의 완료)

| 항목 | 결정 |
|------|------|
| 패널 높이 | SafeArea 세로의 40% |
| 패널 위치 | 하단 고정 (SafeAreaContainer 기준) |
| 버튼 배치 | 3열 × 3행 (총 9슬롯) |
| 버튼 크기 | 가용 공간을 균등 분할 (정사각형 강제 없음) |
| 그리드 방식 | B안 — 중첩 Layout Group (VerticalLG → 행별 HorizontalLG) |
| 버튼 내부 | 좌측: 건물 아이콘 / 우측: 골드 아이콘(위) + 비용 텍스트(아래) |
| 헤더 | 없음 |
| 닫기 버튼 | 기존 CancelButton (X 이미지) 재사용, 위치 조정 |
| 빈 슬롯 | CanvasGroup.alpha=0 (기존 코드 유지) |

---

## 영향 범위

- **Game.unity**: BuildingPopup 하위 계층 구조 전면 재구성
- **BuildingPlacementUI.cs**: 골드 아이콘 Image 참조 필드 추가 필요
- **GameSystemRules.md**: Rule 2 보완 — Layout Group 반응형 패턴 명세 추가
