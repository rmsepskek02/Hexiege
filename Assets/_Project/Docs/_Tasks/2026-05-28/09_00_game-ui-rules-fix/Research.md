# Research — Game UI 규칙 전면 준수 작업

이 문서는 Game 씬에 존재하는 모든 `GameSystemRules.md` 위반 항목을 조사한 결과입니다.
주요 목적은 세 가지입니다.
1. 건물 배치/유닛 생산/액션 패널 세 곳의 버튼 위치·크기를 올바르게 만들고, 세 패널이 동일한 높이와 동일한 CancelButton 위치를 가지도록 수정한다.
2. 게임 종료 UI 등 나머지 UI의 규칙 2(앵커 비율 기반 배치) 위반 항목을 수정한다.
3. 작업 중 설계 판단이 필요한 항목을 명확히 정리하여 임의로 결정하지 않도록 한다.

---

## 1. 조사 방법

- `Game.unity` YAML 파일 전수 파싱 (21,424줄)
- `m_SizeDelta`가 `{x:0, y:0}`이 아닌 항목 중 `anchorMin == anchorMax`(포인트 앵커)인 경우를 규칙 2 위반으로 판정
- 각 UI 컴포넌트 스크립트의 SerializeField 목록과 대조하여 GO 역할 파악

---

## 2. GameSystemRules 위반 현황 요약

| 규칙 | 상태 |
|------|------|
| 규칙 1 (Canvas Scaler) | ✅ 정상 — Scale With Screen Size / 1080×1920 / Match=0 |
| 규칙 2 (앵커 비율 기반 배치) | ❌ **위반 57건** (포인트 앵커 + 고정 sizeDelta) |
| 규칙 4 (SafeArea 구조) | ✅ 기본 구조 정상 |
| 규칙 5 (CanvasGroup 숨김/표시) | ✅ 정상 |

---

## 3. 위반 항목 전체 목록 (57건, UI별 그룹)

### 그룹 A — 건물 배치 팝업 (BuildingPlacementUI)

| GO 이름 | anchorMin/Max | sizeDelta | 역할 |
|---------|--------------|-----------|------|
| BuildingButtons1 | (0,1) | (1010, 0) | 건물 버튼 행 컨테이너 |
| BuildingButtons2 | (0,1) | (1010, 100) | 건물 버튼 행 컨테이너 |
| BuildingButtons3 | (0,1) | (1010, 100) | 건물 버튼 행 컨테이너 |
| Button1 | (0,1) | (326, 100) | 건물 버튼 |
| Button2 | (0,1) | (326, 100) | 건물 버튼 |
| Button3 | (0,1) | (326, 100) | 건물 버튼 |
| CancelButton | (1,1) | (100, 80) | 닫기 버튼 |

### 그룹 B — 유닛 생산 팝업 (ProductionPanelUI)

| GO 이름 | anchorMin/Max | sizeDelta | 역할 |
|---------|--------------|-----------|------|
| UnitButtons | (0,1) | (1010, 0) | 유닛 버튼 컨테이너 |
| UnitButtons | (0,1) | (1010, 100) | 유닛 버튼 컨테이너 (2번째 행) |
| Slot1~Slot9 | (0,1) | (326, 110) | 유닛 버튼 슬롯 (최대 9개) |
| SlotImage | (0,1) | (150, 150) | 슬롯 내부 유닛 초상화 이미지 |
| Slot1~Slot3 | (0,1) | (90, 80) | 생산 큐 슬롯 (3개) |
| DestroyButton | (0,1) | (326, 100) | 철거 버튼 |
| UpgradeButton | (0,1) | (326, 100) | 업그레이드 버튼 |
| RallyPointButton | (0,1) | (326, 100) | 랠리포인트 버튼 |
| Buttons | (0,1) | (1010, 100) | 하단 버튼 행 컨테이너 |
| GoldText | (0,1) | (100, 20) | 골드 텍스트 |
| PopText | (0,1) | (100, 20) | 인구 텍스트 |
| GoldIcon | (0,1) | (60, 60) | 골드 아이콘 |
| PopIcon | (0,1) | (60, 60) | 인구 아이콘 |
| CancelButton | (1,1) | (100, 80) | 닫기 버튼 |

### 그룹 C — 비생산 건물 액션 패널 (BuildingActionPanelUI)

| GO 이름 | anchorMin/Max | sizeDelta | 역할 |
|---------|--------------|-----------|------|
| DestroyButton | (0,1) | (326, 100) | 철거 버튼 |
| Buttons | (0,1) | (1010, 100) | 버튼 행 컨테이너 |
| CancelButton | (1,1) | (100, 80) | 닫기 버튼 |

### 그룹 D — 인게임 설정 메뉴 (InGameSettingsUI)

| GO 이름 | anchorMin/Max | sizeDelta | 역할 |
|---------|--------------|-----------|------|
| Panel | (0.5, 0.5) | (550, 350) | 설정 패널 본체 |
| CloseButton | (1,1) | (45, 45) | 닫기 버튼 |
| SoundButton | (0.5, 0.5) | (400, 150) | 사운드 토글 버튼 |
| ForfeitButton | (0.5, 0.5) | (400, 150) | 포기 버튼 |
| CancelButton | (0,1) | (180, 150) | 취소 버튼 (ConfirmPopup 내) |
| ConfirmButton | (0,1) | (180, 150) | 확인 버튼 (ConfirmPopup 내) |
| ButtonRow | (0.5, 0) | (400, 80) | 버튼 행 컨테이너 |

### 그룹 E — 게임 종료 UI (GameEndUI)

*시각적 문제 없음. 규칙 2 위반 항목만 수정.*

| GO 이름 | anchorMin/Max | sizeDelta | 역할 |
|---------|--------------|-----------|------|
| (Game.unity YAML에서 `_resultText`, `_restartButton`, `_backToLobbyButton`, `_countdownText`에 해당하는 RectTransform별도 확인 필요) |  |  |  |

> 🔴 **조사 미완료**: GameEndUI 내부 GO들의 정확한 이름이 YAML에서 추출되지 않았음. Plan 작성 전 scene에서 직접 확인 필요.

### 그룹 F — HUD (GameHudUI)

| GO 이름 | anchorMin/Max | sizeDelta | 역할 |
|---------|--------------|-----------|------|
| GoldImage | (0,0) | (40, 40) | 골드 아이콘 |
| GoldText | (0,0) | (100, 50) | 골드 텍스트 |
| PopulationImage | (0,0) | (40, 40) | 인구 아이콘 |
| PopulationText | (0,0) | (120, 50) | 인구 텍스트 |
| BlueHexTile | (0,0) | (40, 40) | 블루 타일 카운트 아이콘 |
| BlueHexTileText | (0,0) | (80, 50) | 블루 타일 카운트 텍스트 |
| RedHexTile | (0,0) | (40, 40) | 레드 타일 카운트 아이콘 |
| RedHexTileText | (0,0) | (80, 50) | 레드 타일 카운트 텍스트 |

> ⚠️ **HUD 바 자체(GameHUD GO)**가 anchorMin=(0,1)/anchorMax=(1,1) + sizeDelta.y=100px (고정 높이 100px)로 설정되어 있을 가능성. Plan 작성 전 확인 필요.

### 그룹 G — 재경기 요청 팝업 (RematchRequestPopup)

| GO 이름 | anchorMin/Max | sizeDelta | 역할 |
|---------|--------------|-----------|------|
| ButtonArea | (0.5, 0) | (212, 88) | DeclinedPanel 확인 버튼 |

> RequestPanel, DeclinedPanel 자체는 이전 Round 1에서 anchor (0.1,0.3)~(0.9,0.7)로 비율 기반 변환 완료. 내부 ButtonArea만 미완료.

---

## 4. 특이 사항

### ConfirmPopup 내 버튼 (CancelButton, ConfirmButton)
- GO 이름이 CancelButton/ConfirmButton이지만 이것은 **InGameSettingsUI 내부의 ConfirmPopup** 소속
- BuildingPanelBase의 _cancelButton과 다른 GO
- `anchor (0,1)`, sizeDelta `(180, 150)` — 팝업 하단 버튼 배치

### SlotImage (150×150px)
- ProductionPanelUI 유닛 버튼 슬롯 내부의 초상화 이미지
- 슬롯 크기(326×110)에 비해 이미지(150×150)가 크게 설정되어 있음 — 비율 불일치 가능성
