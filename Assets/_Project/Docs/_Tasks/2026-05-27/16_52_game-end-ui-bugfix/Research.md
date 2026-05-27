# Research — 게임 종료 UI 버그 수정

멀티플레이에서 포기(Forfeit) 시 게임 종료 화면이 호스트에게 표시되지 않는 버그와,
재경기 요청 팝업이 게임 종료 화면 뒤에 가려져 클릭할 수 없는 버그를 분석합니다.
두 버그 모두 TC-MULTI-END-001에서 실기기 테스트를 통해 발견되었습니다.

---

## 버그 목록

| ID | 증상 | 원인 유형 |
|----|------|-----------|
| BUG-001 | 포기 후 호스트측 결과 UI 미표시 (클라이언트측은 정상 표시) | 코드 로직 누락 |
| BUG-002 | 재경기 팝업이 GameEndUI 뒤에 가려져 클릭 불가 | Canvas Hierarchy 순서 |

---

## BUG-001: 포기 후 결과 UI 미표시

### 흐름 분석

**정상 종료 (Castle 파괴) 흐름:**

```
[서버] GameEndUseCase → GameEvents.OnGameEnd 발행
  → NetworkGameEndController.OnGameEndServer 구독
    → _announced = true
    → AnnounceWinnerClientRpc(winnerTeamIndex, isRandomMatch)
      → IsServer(호스트): !IsServer = false → OnGameEnd 재발행 안 함 (이미 1번에서 발행됨) ✓
      → 클라이언트: !IsServer = true → OnGameEnd 발행 → GameEndUI 표시 ✓
```

**포기 (Forfeit) 흐름:**

```
[사용자] InGameSettingsUI → IForfeitService.RequestForfeit()
  → NetworkGameEndController.ForfeitServerRpc()
    → _announced = true
    → AnnounceWinnerClientRpc(winnerTeamIndex, false)
      → IsServer(호스트): !IsServer = false → OnGameEnd 발행 안 함 ← ❌ 버그
      → 클라이언트: !IsServer = true → OnGameEnd 발행 → GameEndUI 표시 ✓
```

### 근본 원인

`AnnounceWinnerClientRpc` 내부의 `!IsServer` 가드:

```csharp
// NetworkGameEndController.cs:201-204
if (!IsServer)
{
    GameEvents.OnGameEnd.OnNext(new GameEndEvent(winnerTeam));
}
```

이 가드는 **"정상 종료 시에는 서버에서 이미 `GameEndUseCase`가 `OnGameEnd`를 발행했으므로 중복 발행하지 않는다"** 는 전제로 작성된 코드입니다.

그러나 포기 흐름에서는 `GameEndUseCase`를 전혀 거치지 않습니다. `ForfeitServerRpc`가 직접 `AnnounceWinnerClientRpc`를 호출하므로, 서버(호스트)에서 `OnGameEnd`가 한 번도 발행되지 않아 `GameEndUI`가 표시되지 않습니다.

### 수정 대상 파일

- `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameEndController.cs`
  - 수정 위치: `ForfeitServerRpc()` 메서드 내부
  - `_announced = true` 설정 후, `AnnounceWinnerClientRpc` 호출 직전

### 수정 시 안전성 검토

`ForfeitServerRpc`에서 `GameEvents.OnGameEnd.OnNext()`를 추가로 발행할 경우:
- `OnGameEndServer`가 이 이벤트를 받아 다시 호출될 수 있음
- 그러나 `_announced = true`가 이미 설정된 상태이므로, `OnGameEndServer`는 진입 즉시 `return`으로 탈출 → **이중 처리 없음** ✓

---

## BUG-002: 재경기 팝업 Canvas Hierarchy 순서

### 분석

**Canvas 구조 (수정 전):**
```
Canvas
├── Background          ← index 0, 뒤에 렌더링
├── RematchRequestPopup ← index 1
└── SafeAreaContainer   ← index 2, 맨 위에 렌더링
     ├── GameEndPanel   ← SafeAreaContainer 안에 있어서 RematchRequestPopup 위에 렌더링
     └── ...
```

Unity는 Canvas 자식 중 **Hierarchy 하단(높은 index)** 오브젝트를 나중에 그려 화면 위에 표시합니다.
`SafeAreaContainer`가 `RematchRequestPopup`보다 뒤(높은 index)에 있었으므로, SafeAreaContainer 내부의 `GameEndPanel`이 `RematchRequestPopup` 위에 렌더링되어 가려지는 버그가 발생했습니다.

### AnimatedPanel.Show() 검증

`GameEndPanel`의 `AnimatedPanel.Show()` 코드에는 `SetAsLastSibling()` 등 Hierarchy 순서를 동적으로 변경하는 코드가 없습니다. 순수하게 DOTween 애니메이션만 처리합니다. → Inspector 순서 변경으로 영구 수정 가능 ✓

### 수정 내용 (Inspector — 이미 적용 완료)

**Canvas 구조 (수정 후):**
```
Canvas
├── Background          ← index 0
├── SafeAreaContainer   ← index 1
└── RematchRequestPopup ← index 2, 맨 위에 렌더링 ✓
```

### 다른 UI 영향 검토

| 대상 | 영향 |
|------|------|
| SafeAreaContainer 내부 7개 UI (GameHUD, BuildingPopup, ProductionPopup, BuildingActionPanel, GameEndPanel, InGameSettingsPanel, ConfirmPopup) | SafeAreaContainer 내부 상대 순서 변화 없음 → 영향 없음 ✓ |
| Background (GameEndPanel _backgroundOverlay) | Canvas 위치 변동 없음 → 영향 없음 ✓ |
| 재경기 팝업 활성 시 GameHUD 가림 | RematchRequestPopup의 _overlay(전체화면 반투명)가 SafeAreaContainer 위에 렌더링 → 의도된 동작 ✓ |

### GameSystemRules Rule 4 검토

Rule 4: "실제 UI 요소는 전부 SafeAreaContainer 안에 배치한다."

`RematchRequestPopup`은 현재 SafeAreaContainer 밖(Canvas 직속)에 있어 Rule 4를 엄밀히 따르면 위반입니다.
그러나 z-ordering 목적의 의도적 배치이며, `RematchRequestPopup` 내 UI 요소들은 전체화면을 덮는 오버레이(`_overlay`)와 함께 동작하므로 Safe Area 외부로 삐져나올 위험이 낮습니다.
향후 `RematchRequestPopup`에 독립 Canvas + 높은 Sort Order 방식으로 전환할 경우 Rule 4 완전 준수 가능 (현재는 현행 구조 유지).

---

## 정적 분석 체크포인트

### NetworkGameEndController.cs

- `OnGameEndServer`의 `_announced` 가드: ForfeitServerRpc에서 `_announced = true` 설정 후 OnGameEnd 발행 시 재진입 방지 정상 동작 확인 ✓
- `AnnounceWinnerClientRpc`의 `!IsServer` 가드: 정상 종료 시 서버 중복 발행 방지 목적 — 포기 흐름에서 서버 발행 누락이 버그 원인
- `ForfeitServerRpc`의 `RequireOwnership = false`: 클라이언트(Red 팀)도 호출 가능 — 올바른 설정 ✓
- ClientId 0 = Blue, 0이 아닌 = Red 매핑: NetworkGameManager와 동일 규약 ✓
