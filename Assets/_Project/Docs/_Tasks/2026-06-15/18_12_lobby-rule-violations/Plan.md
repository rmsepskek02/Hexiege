# Plan — Lobby 씬 규칙 위반 수정

## 이 작업으로 무엇을 고치는가

Lobby 씬 전수 점검에서 발견된 규칙 위반을 수정한다.

1. `LobbyUI.cs` — `_lobbyPanel` SetActive → CanvasGroup 전환 (Rule 5) — 현재 씬 미배치, 코드만 수정
2. `LoadingScreen > StatusText` — LiberationSans → Maplestory Light SDF 교체 (Rule 6) ✅ 완료

> ⚠️ `AnonymousWarningPopup._blockingOverlay`는 초기 분석 오류로 포함되었으나 Login.unity 소속. Login 씬 점검 작업으로 이관.

---

## 수정 항목 상세

### [수정 1] LobbyUI.cs — `_lobbyPanel` CanvasGroup 전환

**근거**: GameSystemRules_UI.md 규칙 5. UI 숨김/표시는 SetActive 대신 CanvasGroup으로 제어.

**변경 내용**:

```
[변경 전]
라인 56: [SerializeField] private GameObject _lobbyPanel;
라인 127~128: _lobbyPanel.SetActive(true);
라인 337~338: _lobbyPanel.SetActive(false);

[변경 후]
라인 56: [SerializeField] private CanvasGroup _lobbyPanel;
라인 127~128:
    _lobbyPanel.alpha = 1f;
    _lobbyPanel.blocksRaycasts = true;
    _lobbyPanel.interactable = true;
라인 337~338:
    _lobbyPanel.alpha = 0f;
    _lobbyPanel.blocksRaycasts = false;
    _lobbyPanel.interactable = false;
```

**Inspector 작업 필요**: `_lobbyPanel` 오브젝트에 CanvasGroup 컴포넌트 추가 후 필드 재연결. 에디터 스크립트로 자동화.

---

### [수정 2] AnonymousWarningPopup.cs — `_blockingOverlay` CanvasGroup 전환

**근거**: GameSystemRules_UI.md 규칙 5.

**변경 내용**:

```
[변경 전]
라인 41~42: [Tooltip("...")][SerializeField] private GameObject _blockingOverlay;
라인 101: _blockingOverlay.SetActive(true);
라인 108: _blockingOverlay.SetActive(false);

[변경 후]
라인 41~42: [Tooltip("...")][SerializeField] private CanvasGroup _blockingOverlay;
라인 101:
    _blockingOverlay.alpha = 1f;
    _blockingOverlay.blocksRaycasts = true;
    _blockingOverlay.interactable = true;
라인 108:
    _blockingOverlay.alpha = 0f;
    _blockingOverlay.blocksRaycasts = false;
    _blockingOverlay.interactable = false;
```

**Inspector 작업 필요**: `_blockingOverlay` 오브젝트에 CanvasGroup 추가 후 필드 재연결. 에디터 스크립트로 자동화.

---

### [수정 3] LoadingScreen > StatusText — 폰트 교체

**근거**: GameSystemRules_UI.md 규칙 6. 허용 폰트: Maplestory Light SDF / Bold SDF 두 가지만.

**변경 내용**: Lobby.unity 씬에서 `LoadingScreen > SafeAreaContainer > StatusText` 오브젝트의 TMP 컴포넌트 폰트를 `LiberationSans SDF` → `Maplestory Light SDF`로 교체.

**방법**: 에디터 스크립트에서 TMP 컴포넌트를 찾아 font 에셋을 교체하거나, Inspector에서 직접 교체.

---

## 에디터 스크립트 계획

메뉴: `Hexiege/Setup/Lobby 규칙 위반 수정`

처리 순서:
1. Lobby.unity 씬 열기 확인 (이미 열려있어야 함)
2. `LobbyPanel` 오브젝트에 CanvasGroup 추가 (없으면) → alpha=1/blocksRaycasts=true/interactable=true 초기값
3. `LobbyUI` 컴포넌트의 `_lobbyPanel` SerializedProperty를 CanvasGroup으로 재연결
4. `AnonymousWarningPopup`의 `_blockingOverlay` 오브젝트에 CanvasGroup 추가 (없으면) → alpha=0/blocksRaycasts=false/interactable=false 초기값 (기본 숨김 상태)
5. `AnonymousWarningPopup` 컴포넌트의 `_blockingOverlay` SerializedProperty를 CanvasGroup으로 재연결
6. `LoadingScreen > SafeAreaContainer > StatusText`의 TMP 폰트를 Maplestory Light SDF로 교체
7. EditorSceneManager.MarkSceneDirty() → 저장 안내

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| `_lobbyPanel` CanvasGroup 초기값이 alpha=0이면 처음부터 안 보임 | 에디터 스크립트에서 alpha=1로 초기화 |
| `_blockingOverlay` CanvasGroup 초기값이 alpha=1이면 팝업 없이 차단 오버레이가 항상 보임 | 에디터 스크립트에서 alpha=0/blocksRaycasts=false로 초기화 |
| 씬이 Lobby.unity가 아닌 상태에서 에디터 스크립트 실행 | 스크립트에서 현재 씬 이름 검증 후 경고 출력 |

---

## 수정 파일 목록

```
[수정]
- Assets/_Project/Scripts/Presentation/UI/LobbyUI.cs
- Assets/_Project/Scripts/Presentation/UI/Views/Login/AnonymousWarningPopup.cs

[추가]
- Assets/Editor/Setup/FixLobbyRuleViolations.cs  (에디터 스크립트, 1회성)

[씬 수정 — 에디터 스크립트 실행 후]
- Assets/_Project/Scenes/Lobby.unity
```

---

## 작업 순서

1. `LobbyUI.cs` 코드 수정
2. `AnonymousWarningPopup.cs` 코드 수정
3. `FixLobbyRuleViolations.cs` 에디터 스크립트 작성
4. 사용자에게 Unity에서 실행 요청
5. 씬 저장 확인
