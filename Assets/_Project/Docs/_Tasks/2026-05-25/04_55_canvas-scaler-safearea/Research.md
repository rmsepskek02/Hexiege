# Research — Canvas Scaler 통일 및 Safe Area 적용

## 작업 개요

이 작업은 공통 UI 규칙(GameSystemRules.md 규칙 1, 4)을 실제 씬과 코드에 반영하는 것이다.
현재 씬마다 Canvas Scaler 설정이 제각각이고, Safe Area를 처리하는 컴포넌트가 없어
노치·펀치홀·하단 홈바가 있는 기기에서 UI가 가려질 수 있는 상태다.

크게 두 가지 작업으로 구성된다.
1. **Canvas Scaler 통일**: 모든 씬의 Canvas를 1080×1920 / matchWidthOrHeight=0 으로 통일
2. **Safe Area 적용**: SafeAreaFitter 컴포넌트를 새로 구현하고, 모든 씬에 SafeAreaContainer 구조를 적용

---

## 현재 Canvas Scaler 상태

씬 파일 직접 확인 결과 (2026-05-25):

| 씬 | Canvas | referenceResolution | matchWidthOrHeight | 변경 필요 여부 |
|----|--------|--------------------|--------------------|--------------|
| Lobby.unity | Canvas 1 (line 7151) | 1080×1920 | 0 | 없음 (이미 목표값) |
| Lobby.unity | Canvas 2 (line 7468) | 1080×1920 | 0.5 | matchWidthOrHeight 0.5 → 0 |
| Game.unity | Canvas (line 20378) | 540×960 | 0.5 | referenceResolution + matchWidthOrHeight 모두 변경 |

---

## SafeAreaFitter 현황

- 현재 프로젝트에 SafeAreaFitter 컴포넌트 없음
- Unity 내장 `Screen.safeArea` API를 사용하여 SafeAreaContainer의 RectTransform을 Safe Area 범위로 조정하는 스크립트를 새로 작성해야 함
- 경로: `Assets/_Project/Scripts/Presentation/UI/Common/SafeAreaFitter.cs`

---

## 현재 씬 UI 구조

### Game.unity — [UI] Canvas 직속 자식 오브젝트

씬 파일에서 확인한 목록:

| 오브젝트 | SafeAreaContainer 이동 여부 |
|---------|--------------------------|
| Background | 밖에 유지 (전체화면 배경, 규칙 4 적용 제외) |
| ProductionPopup | Container 안으로 이동 |
| BuildingPopup | Container 안으로 이동 |
| BuildingActionPanel | Container 안으로 이동 |
| InGameSettingsPanel | Container 안으로 이동 |
| ConfirmPopup | Container 안으로 이동 |
| GameEndPanel | Container 안으로 이동 |
| GameHUD | Container 안으로 이동 |

### ToastUI

- 별도 Canvas를 가진 DontDestroyOnLoad 오브젝트
- 해당 Canvas에 SafeAreaFitter 직접 적용 (규칙 4)

### Lobby.unity

- MVVM 구조로 되어 있으며 Canvas 2개 보유
- 각 Canvas 아래에 SafeAreaContainer 추가 필요

---

## 주의사항

### Canvas Scaler 변경 시 주의
- Game.unity의 referenceResolution을 540×960 → 1080×1920으로 변경하면 논리 캔버스 크기가 2배로 커짐
- **앵커 기반으로 제작된 UI는 영향 없음** — 비율이 유지되기 때문
- 고정 픽셀값(sizeDelta, offsetMin, offsetMax 등)이 남아 있는 UI 요소가 있다면 시각적으로 달라 보일 수 있어 실기기 확인 필요

### SafeAreaContainer 추가 시 주의
- 기존 Canvas 직속 오브젝트들을 SafeAreaContainer 안으로 이동하면 Hierarchy 구조가 바뀜
- GameBootstrapper 등에서 Inspector 연결(SerializeField)로 참조하던 오브젝트들의 연결이 끊길 수 있음
- 에디터 스크립트로 이동 처리 시 참조 자동 유지 여부 확인 필요
