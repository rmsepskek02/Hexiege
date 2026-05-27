# Plan — 게임 종료 UI 버그 수정

멀티플레이 포기 시 결과 UI가 표시되지 않는 버그(BUG-001)와
재경기 팝업이 GameEndUI 뒤에 가려지는 버그(BUG-002)를 수정합니다.
BUG-002는 Inspector에서 Canvas Hierarchy 순서 변경으로 이미 적용 완료되었으며,
BUG-001만 코드 수정이 필요합니다.

---

## 수정 항목 요약

| 버그 | 수정 방법 | 파일 | 상태 |
|------|-----------|------|------|
| BUG-001 포기 후 결과 UI 미표시 | ForfeitServerRpc에 OnGameEnd 발행 추가 | NetworkGameEndController.cs | 미완료 |
| BUG-002 재경기 팝업 가려짐 | Canvas Hierarchy 순서 변경 | Inspector (Game.unity) | ✅ 완료 |

---

## BUG-001 수정 계획

### GameSystemRules 근거

본 수정은 UI 시스템 규칙보다 **네트워크 게임 흐름 일관성**에 관한 수정입니다.
GameSystemRules.md에 직접 대응하는 규칙은 없으나, 포기 흐름과 정상 종료 흐름이
동일한 `OnGameEnd → GameEndUI 표시` 경로를 밟아야 한다는 아키텍처 원칙에 근거합니다.

### 수정 위치

**파일:** `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameEndController.cs`
**메서드:** `ForfeitServerRpc()`
**변경 규모:** 3줄 추가 (주석 1줄 + 코드 1줄 + 공백 1줄)

### 수정 내용

```csharp
// 수정 전
_announced = true;
Debug.Log($"[Network] 포기 처리. ...");

AnnounceWinnerClientRpc((int)winnerTeam, false);
```

```csharp
// 수정 후
_announced = true;
Debug.Log($"[Network] 포기 처리. ...");

// 포기 흐름은 GameEndUseCase를 경유하지 않으므로 서버에서 OnGameEnd를 직접 발행한다.
// (정상 종료 흐름은 GameEndUseCase가 먼저 OnGameEnd를 발행 → OnGameEndServer가 구독하는 경로)
// OnGameEndServer는 _announced=true 상태이므로 진입 즉시 return → 중복 처리 없음.
GameEvents.OnGameEnd.OnNext(new GameEndEvent(winnerTeam));

AnnounceWinnerClientRpc((int)winnerTeam, false);
```

### 수정 후 검증 포인트

1. 호스트가 포기 시 → 호스트 화면에 GameEndUI 표시 여부
2. 클라이언트가 포기 시 → 호스트 화면에 GameEndUI 표시 여부
3. Castle 파괴(정상 종료) 시 → 기존 동작 유지 여부 (OnGameEnd 중복 발행 없음)
4. 포기 후 재경기 버튼 정상 동작 여부

---

## BUG-002 수정 내용 (완료)

**변경 전 Canvas 순서:**
```
Canvas
├── Background
├── RematchRequestPopup  ← index 1 (SafeAreaContainer보다 앞 = 뒤에 렌더링)
└── SafeAreaContainer    ← index 2 (맨 위 렌더링 → GameEndPanel이 팝업을 가림)
```

**변경 후 Canvas 순서:**
```
Canvas
├── Background
├── SafeAreaContainer    ← index 1
└── RematchRequestPopup  ← index 2 (맨 위 렌더링 → 팝업이 항상 위에 표시) ✓
```

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| OnGameEnd 중복 발행으로 GameEndUI가 두 번 열릴 수 있음 | _announced = true 설정 후 발행하므로 OnGameEndServer 재진입 없음. GameEndUI.OnGameEnd는 단순 Show()이므로 중복 호출 시 이미 표시된 패널을 다시 표시하는 것이므로 부작용 없음 |
| 포기 후 OnNetworkRematchAvailable 발행 타이밍 변화 | AnnounceWinnerClientRpc 호출 순서는 변경 없음. OnGameEnd 발행 후 AnnounceWinnerClientRpc → 정상 순서 유지 |

---

## 작업 순서

1. `NetworkGameEndController.cs` `ForfeitServerRpc()` 수정 (game-programmer 위임)
2. 멀티플레이 실기기 테스트:
   - 호스트 포기 → 양측 결과 UI 확인
   - 클라이언트 포기 → 양측 결과 UI 확인
   - Castle 파괴 정상 종료 → 기존 동작 확인
   - 재경기 팝업 위치 확인 (BUG-002 검증)
