# Research — Lobby 씬 규칙 위반 수정

## 이 작업이 왜 필요한가

Lobby.unity 씬을 전수 점검한 결과, 공통 UI 규칙(GameSystemRules_UI.md)을 위반하는 항목이 3가지 발견됐다.

1. **Rule 5 위반 2건**: UI 요소를 숨길 때 `SetActive(false)` 대신 CanvasGroup을 사용해야 하는데, 두 곳에서 여전히 `SetActive`를 직접 호출하고 있다.
2. **Rule 6 위반 1건**: 모든 TMP 텍스트는 Maplestory 폰트만 사용해야 하는데, LoadingScreen의 StatusText가 Unity 기본 폰트(LiberationSans SDF)를 사용하고 있다.

---

## 점검 방법

- Lobby.unity YAML 파일 직접 파싱 → 173개 GameObject 전수 확인
- 비활성 GO: 4개 (ProfilePanel/ShopPanel/RankingPanel → CanvasGroup 관리 ✅, Pistol → 3D 모델 ✅)
- 관련 스크립트 전체 SetActive grep
- TMP 폰트 GUID를 씬 파일에서 전수 조회

---

## 발견된 위반 사항

### [위반 1] Rule 5 — LobbyUI.cs `_lobbyPanel.SetActive()`

**파일**: `Assets/_Project/Scripts/Presentation/UI/LobbyUI.cs`

**위반 코드**:
- 라인 56: `[SerializeField] private GameObject _lobbyPanel;` — GameObject 타입
- 라인 127~128: `_lobbyPanel.SetActive(true);` — 초기화 시 패널 표시
- 라인 337~338: `_lobbyPanel.SetActive(false);` — 게임 시작 시 패널 숨김

**`_lobbyPanel`이란?**  
멀티플레이 로비 진입 화면 전체를 감싸는 패널 오브젝트. 게임이 시작되면 숨겨진다. SetActive(false)로 비활성화하면 DOTween 등 내부 로직이 멈추고 레이아웃이 무너질 수 있다.

**수정 방향**: `_lobbyPanel` 필드를 `CanvasGroup` 타입으로 변경. Show 시 `alpha=1/blocksRaycasts=true/interactable=true`, Hide 시 `alpha=0/false/false`.

---

### [위반 2] Rule 5 — AnonymousWarningPopup.cs `_blockingOverlay.SetActive()`

**파일**: `Assets/_Project/Scripts/Presentation/UI/Views/Login/AnonymousWarningPopup.cs`

**위반 코드**:
- 라인 41~42: `[SerializeField] private GameObject _blockingOverlay;` — GameObject 타입
- 라인 101: `_blockingOverlay.SetActive(true);` — 팝업 표시 시 차단 오버레이 활성화
- 라인 108: `_blockingOverlay.SetActive(false);` — 팝업 숨김 시 차단 오버레이 비활성화

**`_blockingOverlay`란?**  
AnonymousWarningPopup이 열릴 때 뒤쪽 입력을 차단하는 투명 오버레이 오브젝트. 사용자가 팝업 외부를 탭해도 반응하지 않도록 막는 역할. `ConfirmPopup`의 `_blockingOverlay`는 이미 CanvasGroup으로 수정됐지만 이 파일은 남아있었다.

**수정 방향**: `_blockingOverlay` 필드를 `CanvasGroup` 타입으로 변경. Show/Hide 시 동일하게 CanvasGroup 제어.

---

### [위반 3] Rule 6 — LoadingScreen > StatusText LiberationSans 사용

**씬 위치**: `LoadingScreen > SafeAreaContainer > StatusText`

**위반 내용**: TMP 텍스트 컴포넌트가 Unity 기본 폰트인 `LiberationSans SDF`를 사용 중. 씬 파일에 `LiberationSans SDF Material (Instance)` 오브젝트가 포함돼 있는 것으로 확인됨 (GUID: `8f586378b4e144a9851e7b34d9b748ee`).

**수정 방향**: Inspector에서 StatusText의 TMP 폰트를 `Maplestory Light SDF`로 교체. 코드 수정 불필요 — Inspector 작업만으로 해결 가능.

---

## 영향 범위

| 항목 | 영향 |
|------|------|
| LobbyUI.cs | `_lobbyPanel` 필드 타입 변경 → Inspector 재연결 필요 |
| AnonymousWarningPopup.cs | `_blockingOverlay` 필드 타입 변경 → Inspector 재연결 필요 |
| LoadingScreen StatusText | Inspector 폰트 교체 (코드 없음) |

두 스크립트 모두 SerializeField 타입이 바뀌므로, 씬/프리팹에서 기존 Inspector 연결이 끊긴다. 에디터 스크립트로 CanvasGroup 추가 및 재연결을 자동화할 수 있다.

---

## 준수 중인 항목 (이상 없음)

- Rule 1 (Canvas Scaler): 3개 Canvas 모두 ScaleWithScreenSize / 1080×1920 / Match=0 ✅
- Rule 2 (앵커 기반): sizeDelta 위반 없음 ✅
- Rule 4 (SafeArea): [UI] Canvas > SafeAreaContainer, LoadingScreen > SafeAreaContainer, Toast Canvas 모두 SafeAreaFitter 부착 ✅
- Rule 5 (LoginRootView): `SetActivePanel()` 내부에서 CanvasGroup으로 제어 ✅
- Rule 5 (LobbyRootView): 탭 전환 전부 CanvasGroup ✅
- Rule 6 (기타 텍스트): Maplestory Light SDF 10회 / Bold SDF 57회 참조 ✅

---

## 기타 검토 항목

- `RaceSelectionView.cs:201` `_characterRoots[i].SetActive(true)` — 3D 캐릭터 모델 오브젝트 활성화. UI Rule 5 적용 대상 아님 ✅
- `NetworkStatusUI.cs` SetActive 호출 — Lobby.unity에 미등록 스크립트, Lobby 씬과 무관 ✅
- `FloatingHpTextSpawner.cs`, `FloatingHpText.cs` SetActive — 오브젝트 풀링 패턴, UI 숨김/표시 목적이 아님 ✅
