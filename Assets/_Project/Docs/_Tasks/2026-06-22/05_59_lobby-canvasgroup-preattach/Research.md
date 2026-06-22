# Research: Lobby 패널 CanvasGroup 사전 부착 및 활성화

## 작업 목적 및 내용

로비 씬의 탭 패널(ShopPanel, ProfilePanel, RankingPanel)이 비활성 상태로 씬에 저장되어 있고,
CanvasGroup 컴포넌트도 씬에 미리 부착되지 않은 문제를 수정합니다.

컴포넌트 부착은 런타임 코드가 아닌 에디터에서 미리 해두는 것이 원칙이므로,
에디터 스크립트로 씬을 수정하고, 런타임 코드도 이에 맞게 단순화합니다.

---

## 현재 상태

### 씬 상태 (Lobby.unity)

| 패널 | m_IsActive | CanvasGroup 부착 여부 |
|------|-----------|----------------------|
| BattlePanel | 1 (활성) | 없음 (런타임 자동 추가) |
| ShopPanel | 0 (비활성) | 없음 (런타임 자동 추가) |
| ProfilePanel | 0 (비활성) | 없음 (런타임 자동 추가) |
| RankingPanel | 0 (비활성) | 없음 (런타임 자동 추가) |

### 코드 상태 (LobbyRootView.cs:80~88)

```csharp
private void Awake()
{
    _battlePanelGroup = EnsureCanvasGroup(_battlePanel);
    _shopPanelGroup = EnsureCanvasGroup(_shopPanel);
    _profilePanelGroup = EnsureCanvasGroup(_profilePanel);
    _rankingPanelGroup = EnsureCanvasGroup(_rankingPanel);
}
```

`EnsureCanvasGroup()`은 CanvasGroup이 없으면 런타임에 `AddComponent`로 추가하는 헬퍼입니다.

### 문제점

1. **SetActive(false) 상태 패널** (규칙 5 위반)
   - LobbyRootView는 CanvasGroup으로 패널 표시/숨김을 처리하는데, 패널이 SetActive=false면 CanvasGroup 변경이 적용되지 않아 탭 전환 시 패널이 보이지 않습니다.

2. **런타임 CanvasGroup 자동 추가 방식**
   - 컴포넌트는 에디터에서 미리 부착해두는 것이 원칙 (Inspector에서 확인/수정 가능, 의도 명확)
   - 현재 방식은 Inspector에서 CanvasGroup이 보이지 않아 혼란 유발

---

## 영향 범위

- **에디터 스크립트 (신규)**: Lobby.unity 씬 수정
- **LobbyRootView.cs 수정**: `EnsureCanvasGroup()` → `GetComponent<CanvasGroup>()`
- **EnsureCanvasGroup() 헬퍼 메서드**: 더 이상 불필요 → 제거
