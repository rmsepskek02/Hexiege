# Plan: 게임포기 시 로딩 인디케이터가 사라지지 않는 버그 수정

## 작업 개요

게임포기(Forfeit)는 게임 종료 UI(GameEndUI)만 표시하는 흐름입니다.
로딩 인디케이터는 로비로 이동할 때(씬 전환 시)에만 사용해야 합니다.
따라서 포기 확정 시 로딩 인디케이터를 켜는 코드를 제거하고,
GameSystemRules도 이에 맞게 수정합니다.

---

## 수정 파일 목록

| 파일 | 수정 내용 |
|------|-----------|
| `Assets/_Project/Scripts/Presentation/UI/InGameSettingsUI.cs` | `OnForfeitConfirmed()` 내 `ShowLoading(true)` 호출 제거 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UI.md` | 규칙 L-2에서 "게임 포기(멀티)" 항목 제거 |

---

## 수정 세부 내용

### 1. `InGameSettingsUI.cs` — `OnForfeitConfirmed()` 수정

**현재 코드 (333~337번 줄):**
```csharp
if (_forfeitService != null)
{
    // 멀티플레이 포기는 서버 RPC 왕복 + 결과 동기화/씬 전환까지 시간이 걸리므로
    // 그 사이 사용자가 멈춘 화면을 보지 않도록 전역 로딩 인디케이터를 띄운다.
    // 싱글플레이 포기는 즉시 결과창(GameEndUI)이 떠서 로딩이 불필요하므로 띄우지 않는다.
    // 로딩을 끄는 책임은 목적지 씬 Bootstrapper가 담당한다(UI 규칙 L-3).
    if (NetworkContext.IsNetworkActive)
        UIManager.Instance?.ShowLoading(true, "게임을 포기하는 중...");

    _forfeitService.RequestForfeit();
}
```

**수정 후:**
```csharp
if (_forfeitService != null)
{
    // 포기는 씬 전환 없이 같은 씬 안에서 GameEndUI를 표시하므로 로딩 인디케이터를 띄우지 않는다.
    // 로딩 인디케이터는 씬 전환(로비 복귀 등)에서만 사용한다(UI 규칙 L-2).
    _forfeitService.RequestForfeit();
}
```

**GameSystemRules 근거:**
- **규칙 L-2**: 로딩 인디케이터는 씬 전환이나 비동기 작업 시작 시 사용. 포기는 같은 씬 안에서 GameEndUI만 표시하므로 해당 없음.

---

### 2. `GameSystemRules_UI.md` — 규칙 L-2 수정

**현재 규칙 L-2:**
> 해당하는 상황: 씬 전환(LoginScene/LobbyScene/GameScene), 로그아웃, **게임 포기(멀티)**, 로비 복귀, 재경기.

**수정 후:**
> 해당하는 상황: 씬 전환(LoginScene/LobbyScene/GameScene), 로그아웃, 로비 복귀, 재경기.
> 게임 포기(멀티)는 씬 전환 없이 GameEndUI만 표시하므로 해당 없음.

---

## 위험 요소

- **없음.** 기존에 켜지는 로딩 인디케이터를 아예 안 켜는 단순 삭제이므로 side-effect 없음.
- 멀티플레이 포기 후 로비 복귀 버튼을 누르는 경우는 `GameEndUI.ReturnToLobby()`에서 별도로 `ShowLoading(true)`를 올바르게 호출하므로 영향 없음.
