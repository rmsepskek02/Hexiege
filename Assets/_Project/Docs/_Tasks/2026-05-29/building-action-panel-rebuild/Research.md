# Research — BuildingActionPanelUI 재설계

## 작업 목적

BuildingActionPanel은 비생산 건물(채굴소 등)을 탭했을 때 나오는 팝업입니다.
실기 테스트에서 패널이 화면 하단에서 떠있고, X 버튼 위치가 다른 패널과 다르며, GoldText가 패널 밖으로 벗어나는 문제가 발견됐습니다.
씬 YAML을 직접 파악한 결과, GameSystemRules를 위반하는 구조적 문제가 다수 확인됐습니다.

---

## 현재 계층 구조

```
BuildingActionPanel (래퍼 + BuildingActionPanelUI + AnimatedPanel)
  └── BuildingPanel (패널 프레임 Image)
        ├── HeaderText (TMP)
        ├── CancelButton (X 버튼)
        └── UnitsButtons (VerticalLayoutGroup)
              ├── UnitButtons (HorizontalLayoutGroup)
              │     ├── DestroyButton (_demolishButton) ← 실제 사용
              │     │     ├── (아이콘 Image 등)
              │     │     └── GoldText (_demolishRefundText) ← 실제 사용
              │     └── Button2 등 (미사용 잔재)
              └── Buttons (HorizontalLayoutGroup)
                    └── 미사용 버튼 3개
```

BuildingActionPanelUI 코드에서 실제로 사용하는 요소는
**HeaderText, CancelButton, DestroyButton, GoldText** 4개뿐입니다.
나머지는 ProductionPanelUI 복제 시 남은 불필요한 잔재입니다.

---

## GameSystemRules 위반 목록

### Rule 2 위반 (앵커 기반 배치 — 고정 픽셀 금지)

| 오브젝트 | 위반 내용 |
|---------|-----------|
| BuildingActionPanel (래퍼) | anchoredPosition=(0,-75), sizeDelta=(0,-150) — 전체화면 래퍼인데 고정 오프셋 존재 |
| HeaderText | anchorMin=(0,0.5), anchorMax=(1,0.5) Y축 단일점 앵커 + anchoredPosition=(-25,210), sizeDelta=(-150,50) 모두 고정 픽셀 |
| UnitButtons | VLG childControlHeight=false로 인해 sizeDelta.y=100 고정 픽셀이 그대로 사용됨 |
| Buttons | 동일 — sizeDelta.y=100 고정 픽셀 |
| DestroyButton | anchoredPosition=(889,-50), sizeDelta=(346,100) 고정 픽셀 |

### Rule 2 심각 위반

| 오브젝트 | 위반 내용 |
|---------|-----------|
| **GoldText** | anchorMax.x=**1.1** — 앵커 최대값이 1.0 초과. 부모 경계 밖에 위치하는 구조적 오류. TC FAIL의 직접 원인 |

### Layout Group 설정 오류

| 오브젝트 | 문제 |
|---------|------|
| UnitsButtons VLG | childControlHeight=false, childForceExpandHeight=false — 자식 높이를 전혀 제어하지 않아 고정 픽셀 높이 그대로 사용 |

---

## 실기 테스트 결과 (TC 기준)

| TC ID | 결과 | 원인 |
|-------|------|------|
| TC-SINGLE-BAP-001 | FAIL | 패널 높이 조정 필요, X 버튼 위치가 다른 패널과 상이, 화면 하단에서 떠있음 |

TC-SINGLE-BAP-002~005는 PASS (닫기/배경탭/철거/환불 기능은 정상)

---

## 필요한 실제 요소 정리

BuildingActionPanelUI 코드(BuildingPanelBase 포함) 필드 기준:

| Inspector 필드 | 오브젝트 | 현재 연결 |
|---------------|---------|-----------|
| `_popup` | AnimatedPanel | BuildingActionPanel에 직접 부착 |
| `_sharedBackground` | SharedBackgroundButton | 공유 Background |
| `_headerText` | HeaderText | ✓ 연결됨 |
| `_cancelButton` | CancelButton | ✓ 연결됨 |
| `_demolishButton` | DestroyButton | ✓ 연결됨 |
| `_demolishRefundText` | GoldText | ✓ 연결됨 |

---

## 재설계 방향

- 래퍼 오프셋 제거 (anchoredPosition=0, sizeDelta=0)
- 불필요한 계층(UnitsButtons, UnitButtons, Buttons, 미사용 버튼들) 제거
- 내부를 단순하게 재구성: HeaderText(앵커 기반) + CancelButton + DemolishArea(HLG)
- GoldText anchorMax.x 수정 (1.1 → 정상값)
- CancelButton 위치를 BuildingPlacementUI와 동일하게 통일
- BuildingPanel anchor는 BuildingPlacementUI와 동일하게 (0,0)~(1,0.4) 유지
