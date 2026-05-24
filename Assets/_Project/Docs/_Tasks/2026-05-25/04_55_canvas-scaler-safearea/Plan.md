# Plan — Canvas Scaler 통일 및 Safe Area 적용

## 작업 개요

공통 UI 규칙(GameSystemRules.md)의 규칙 1(Canvas Scaler 설정)과 규칙 4(Safe Area)를 실제 씬에 반영한다.
Canvas Scaler를 에디터 스크립트로 일괄 변경하고, SafeAreaFitter 컴포넌트를 새로 구현하여 적용한다.

---

## 규칙 근거

- **규칙 1 (Canvas Scaler 설정)**: 모든 씬의 Canvas를 Scale With Screen Size / 1080×1920 / matchWidthOrHeight=0 으로 통일
- **규칙 4 (Safe Area)**: SafeAreaContainer + SafeAreaFitter 구조로 모든 UI에 Safe Area 적용

---

## 작업 항목

### 작업 1. Canvas Scaler 변경 에디터 스크립트

변경 대상:
- Lobby.unity Canvas 2: matchWidthOrHeight 0.5 → 0
- Game.unity Canvas: referenceResolution 540×960 → 1080×1920, matchWidthOrHeight 0.5 → 0

**구현 방법**
- 메뉴: `Hexiege/Setup/Canvas Scaler 통일 적용`
- 씬을 열지 않고도 동작하도록 씬 파일을 직접 수정하는 방식 또는,
  각 씬을 열고 Canvas Scaler 컴포넌트를 찾아서 값을 변경하는 방식 중 선택
- 실행 후 씬을 저장하고 스크립트 삭제 (1회성)

---

### 작업 2. SafeAreaFitter 컴포넌트 구현

**파일**: `Assets/_Project/Scripts/Presentation/UI/Common/SafeAreaFitter.cs`

**동작**:
- Awake() 또는 OnEnable()에서 `Screen.safeArea`를 읽어 RectTransform을 Safe Area 범위로 조정
- anchorMin, anchorMax를 Safe Area 픽셀 좌표 기준으로 계산하여 설정

```
예시 계산:
Rect safeArea = Screen.safeArea;
Vector2 anchorMin = safeArea.position;
Vector2 anchorMax = safeArea.position + safeArea.size;
anchorMin.x /= Screen.width;  anchorMin.y /= Screen.height;
anchorMax.x /= Screen.width;  anchorMax.y /= Screen.height;
rectTransform.anchorMin = anchorMin;
rectTransform.anchorMax = anchorMax;
```

---

### 작업 3. SafeAreaContainer 씬 적용 에디터 스크립트

**Game.unity 적용 구조**:
```
[UI] Canvas
  ├─ Background               ← 그대로 유지
  └─ SafeAreaContainer        ← 새로 추가 (SafeAreaFitter 부착)
       ├─ ProductionPopup      ← 이동
       ├─ BuildingPopup        ← 이동
       ├─ BuildingActionPanel  ← 이동
       ├─ InGameSettingsPanel  ← 이동
       ├─ ConfirmPopup         ← 이동
       ├─ GameEndPanel         ← 이동
       └─ GameHUD              ← 이동
```

**Lobby.unity 적용 구조**:
- 각 Canvas 아래에 SafeAreaContainer 추가
- 기존 자식 오브젝트를 SafeAreaContainer 안으로 이동

**ToastUI**:
- 별도 Canvas이므로 SafeAreaContainer 대신 Canvas 자체에 SafeAreaFitter 직접 부착

**메뉴**: `Hexiege/Setup/SafeArea 구조 적용`
- 실행 후 Inspector 연결 확인 (GameBootstrapper 등 SerializeField 참조 재확인 필요)

---

## 작업 순서

1. SafeAreaFitter.cs 스크립트 작성
2. Canvas Scaler 변경 에디터 스크립트 작성 → 사용자 실행 → 스크립트 삭제
3. SafeAreaContainer 적용 에디터 스크립트 작성 → 사용자 실행 → Inspector 연결 확인 → 스크립트 삭제

---

## 위험 요소

- Game.unity referenceResolution 변경 후 기존 UI가 고정 픽셀값을 쓰고 있다면 시각적 변화가 생길 수 있음 → 실기기 확인 필요
- SafeAreaContainer로 오브젝트 이동 시 GameBootstrapper의 SerializeField 참조가 끊길 수 있음 → 에디터 스크립트 실행 후 반드시 확인
