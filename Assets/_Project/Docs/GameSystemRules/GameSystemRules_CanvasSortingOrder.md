# Game System Rules — Canvas SortingOrder

## 이 문서의 목적 (자연어 설명)

Hexiege는 여러 개의 UI가 화면에 겹쳐서 그려집니다. HUD 위에 패널이 뜨고,
패널 위에 확인 팝업이 뜨고, 그 위에 로딩 화면이 뜨는 식입니다.
이렇게 겹쳐 그려지는 UI는 "그리는 순서 번호(SortingOrder, 줄여서 SO)"를 가집니다.
**번호가 클수록 더 앞(위)에 그려집니다.**

문제는 이 번호를 각 UI가 제각각 정하면, 어떤 팝업이 다른 팝업에 가려져
안 보이는 버그가 생긴다는 점입니다. (실제로 인게임 설정 창의 포기 확인 팝업이
설정 창 뒤에 깔려 안 보이던 버그가 있었습니다.)

이 문서는 그런 가림 버그를 막기 위해, **어떤 종류의 UI가 어떤 SortingOrder
대역을 써야 하는지**를 프로젝트 전체 기준으로 정리한 것입니다.
새 Canvas를 추가하거나 기존 UI의 그리기 순서를 바꿀 때는 반드시 이 문서를
기준으로 삼고, 기준을 벗어나는 값을 쓸 경우 이 문서를 함께 갱신해야 합니다.

---

## 목차

- [전역 SortingOrder 대역 규칙](#전역-sortingorder-대역-규칙)
- [씬별 Canvas 구조](#씬별-canvas-구조)
- [전역 프리팹 Canvas](#전역-프리팹-canvas)
- [새 Canvas 추가 시 규칙](#새-canvas-추가-시-규칙)

---

## 전역 SortingOrder 대역 규칙

UI의 성격에 따라 아래 대역을 사용한다. 같은 성격의 UI는 같은 대역 안에 둔다.

| SortingOrder | 대역 이름 | 용도 |
|---|---|---|
| 0 | HUD | 각 씬의 메인 UI 루트 (HUD, 로비 UI, 로그인 UI 등) |
| 100 | UIManager Canvas | 전역 공통 UI 루트. BlockingOverlay(패널 뒤 반투명 배경), ConfirmPopup/LoadingIndicator/BlockingOverlay 소속 |
| 200 | 패널 | 일반 패널 본체 (인게임 설정, 건물/생산 팝업, 게임 종료 패널 등). Canvas Override로 부여 |
| 250 | 모달 팝업 | 패널 위에 떠야 하는 모달(ConfirmPopup 등). Canvas Override로 부여 |
| 300 | 로딩 인디케이터 | 모든 UI보다 위에 떠야 하는 최상위 로딩 화면 |

- 대역 간 우선순위: HUD(0) < UIManager(100) < 패널(200) < 모달(250) < 로딩(300)
- BlockingOverlay는 SO=100이므로 패널(200)·모달(250)보다 항상 뒤에 그려진다.
  → 반투명 배경이 패널이나 팝업 본체를 덮지 않는다.

---

## 씬별 Canvas 구조

### Login.unity

| GO 이름 | SortingOrder | OverrideSorting | 역할 |
|---|---|---|---|
| Canvas | 0 | false | Login 메인 UI 루트 |
| UIManager Canvas | 100 | false | 전역 공통 UI (ConfirmPopup/LoadingIndicator/BlockingOverlay 소속) |
| SplashOverlay Canvas | 200 | false | 부팅 스플래시 오버레이 |
| NetworkErrorPopup | 200 | true | 네트워크 에러 팝업 |
| AnonymousWarningPopup | 200 | true | 익명 로그인 경고 팝업 |

### Lobby.unity

| GO 이름 | SortingOrder | OverrideSorting | 역할 |
|---|---|---|---|
| [UI] Canvas | 0 | false | 로비 UI 루트 |

### Game.unity

| GO 이름 | SortingOrder | OverrideSorting | 역할 |
|---|---|---|---|
| [UI] | 0 | false | 인게임 HUD 루트 |
| Panel (InGameSettings 자식) | 200 | true | 인게임 설정 패널 본체 |
| BuildingPopup | 200 | true | 건물 정보 팝업 |
| BuildingActionPanel | 200 | true | 건물 배치/액션 패널 |
| ProductionPopup | 200 | true | 유닛 생산 팝업 |
| GameEndPanel | 200 | true | 게임 종료 패널 |

---

## 전역 프리팹 Canvas

씬에 종속되지 않고 프리팹 자체에 독립 Canvas를 가진 UI.

| 대상 | SortingOrder | 비고 |
|---|---|---|
| LoadingIndicator.prefab | 300 | 독립 Canvas, OverrideSorting=true |
| ConfirmPopup.prefab | 250 (예정) | 독립 Canvas, OverrideSorting=true — 현재 작업으로 추가 예정 |

---

## 새 Canvas 추가 시 규칙

**규칙 1. Canvas Override 추가 시 GraphicRaycaster 함께 추가**
독립 Canvas(OverrideSorting=true)를 가진 UI 요소는 자체 GraphicRaycaster를
반드시 함께 가져야 한다. Canvas만 추가하고 Raycaster가 없으면 그 UI 안의
버튼·입력이 동작하지 않는다.

**규칙 2. 대역을 벗어나는 값은 이 문서 업데이트 필수**
위 [전역 SortingOrder 대역 규칙](#전역-sortingorder-대역-규칙)에 정의되지 않은
SortingOrder 값을 사용해야 하는 경우, 임의로 값을 정하지 말고 이 문서에
새 대역 또는 항목을 추가한 뒤 적용한다. 문서와 실제 씬 값은 항상 일치해야 한다.

**규칙 3. DontDestroyOnLoad GO는 씬 계층 루트에 배치**
씬 전환에도 살아남아야 하는 GameObject(DontDestroyOnLoad 대상)는 반드시
씬 계층의 **루트**에 배치한다. 다른 GO의 자식으로 두면 씬 전환 시 부모와 함께
파괴되어 DontDestroyOnLoad가 무력화된다.
