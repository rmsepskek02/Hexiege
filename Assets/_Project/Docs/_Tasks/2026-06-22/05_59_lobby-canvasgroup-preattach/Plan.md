# Plan: Lobby 패널 CanvasGroup 사전 부착 및 활성화

## 작업 목적 및 내용

로비 씬의 탭 패널 3개(ShopPanel, ProfilePanel, RankingPanel)를 활성화하고 CanvasGroup을 미리 부착합니다.
런타임에 자동으로 컴포넌트를 추가하는 방식에서, 에디터에서 미리 부착해두는 올바른 방식으로 전환합니다.

---

## 적용 규칙 (GameSystemRules_UI.md 근거)

| 규칙 | 내용 | 적용 |
|------|------|------|
| **규칙 5** (CanvasGroup 숨김/표시 패턴) | SetActive 대신 CanvasGroup 사용. 오브젝트는 항상 active 상태 유지 | 비활성 패널 3개 활성화 + CanvasGroup 부착 |

---

## 구현 계획

### 작업 1: 에디터 스크립트 작성

**파일**: `Assets/Editor/Setup/SetupLobbyPanelCanvasGroups.cs`
**메뉴**: `Hexiege/Setup/Lobby 패널 CanvasGroup 설정`

**동작**:
1. Lobby.unity 로드 (이미 열려있으면 재사용)
2. BattlePanel, ShopPanel, ProfilePanel, RankingPanel 탐색
3. 4개 패널 모두 `SetActive(true)` (m_IsActive: 1)
4. 4개 패널에 CanvasGroup 부착 (없는 경우에만)
5. CanvasGroup 초기값 설정:
   - BattlePanel: alpha=1, blocksRaycasts=true, interactable=true (기본 탭)
   - ShopPanel / ProfilePanel / RankingPanel: alpha=0, blocksRaycasts=false, interactable=false (숨김)
6. `MarkSceneDirty` + `SaveScene`

### 작업 2: LobbyRootView.cs 코드 수정

**파일**: `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/LobbyRootView.cs`

**변경 내용**:

```csharp
// Before (Awake)
_battlePanelGroup = EnsureCanvasGroup(_battlePanel);
_shopPanelGroup   = EnsureCanvasGroup(_shopPanel);
_profilePanelGroup = EnsureCanvasGroup(_profilePanel);
_rankingPanelGroup = EnsureCanvasGroup(_rankingPanel);

// After (Awake)
_battlePanelGroup  = _battlePanel?.GetComponent<CanvasGroup>();
_shopPanelGroup    = _shopPanel?.GetComponent<CanvasGroup>();
_profilePanelGroup = _profilePanel?.GetComponent<CanvasGroup>();
_rankingPanelGroup = _rankingPanel?.GetComponent<CanvasGroup>();
```

**EnsureCanvasGroup() 헬퍼 제거**: 더 이상 사용하지 않으므로 삭제.

---

## 수정/생성 파일 목록

| 구분 | 파일 | 내용 |
|------|------|------|
| 신규 | `Assets/Editor/Setup/SetupLobbyPanelCanvasGroups.cs` | 에디터 스크립트 |
| 수정 | `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/LobbyRootView.cs` | EnsureCanvasGroup → GetComponent |
| Inspector 작업 | `Assets/_Project/Scenes/Lobby.unity` | 에디터 스크립트 실행 시 자동 반영 |

---

## 위험 요소

| 항목 | 내용 | 대응 |
|------|------|------|
| CanvasGroup 누락 시 NullReference | GetComponent가 null 반환 시 SetPanelVisible에서 NPE 발생 가능 | SetPanelVisible은 이미 null 가드 있음 (`if (group == null) return`) |
| 에디터 스크립트 미실행 | 코드만 교체하고 스크립트 미실행 시 CanvasGroup=null | 에디터 스크립트 실행 필수 — 순서: 스크립트 실행 → 코드 수정 순으로 진행 |
