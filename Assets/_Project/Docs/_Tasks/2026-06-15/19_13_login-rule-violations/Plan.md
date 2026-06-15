# Plan — Login 씬 규칙 위반 수정

## 이 작업으로 무엇을 고치는가

Login.unity 씬 전수 점검에서 발견된 규칙 위반 1건을 수정한다.

- `LoginBootstrapper.cs` — `_loadingIndicator` SetActive → CanvasGroup 전환 (Rule 5)

---

## 수정 항목 상세

### [수정 1] LoginBootstrapper.cs — `_loadingIndicator` CanvasGroup 전환

**근거**: GameSystemRules_UI.md 규칙 5. UI 숨김/표시는 SetActive 대신 CanvasGroup으로 제어.

**변경 내용**:

```
[변경 전]
라인 65: [SerializeField] private GameObject _loadingIndicator;
라인 219~223:
    public void ShowLoading(bool show)
    {
        if (_loadingIndicator != null)
            _loadingIndicator.SetActive(show);
    }

[변경 후]
라인 65: [SerializeField] private CanvasGroup _loadingIndicator;
라인 219~232:
    public void ShowLoading(bool show)
    {
        if (_loadingIndicator == null) return;
        if (show)
        {
            _loadingIndicator.alpha = 1f;
            _loadingIndicator.blocksRaycasts = true;
            _loadingIndicator.interactable = true;
        }
        else
        {
            _loadingIndicator.alpha = 0f;
            _loadingIndicator.blocksRaycasts = false;
            _loadingIndicator.interactable = false;
        }
    }
```

**Inspector 작업 필요**: `LoadingIndicator` 오브젝트에 CanvasGroup 컴포넌트 추가 후 필드 재연결. 에디터 스크립트로 자동화.

---

## 에디터 스크립트 계획

메뉴: `Hexiege/Setup/Login 규칙 위반 수정`
파일: `Assets/Editor/Setup/FixLoginRuleViolations.cs`

처리 순서:
1. Login.unity 씬 열기 확인 (이미 열려 있어야 함)
2. `LoadingIndicator` 오브젝트에 CanvasGroup 추가 (없으면) → alpha=0/blocksRaycasts=false/interactable=false 초기값 (기본 숨김)
3. `LoginBootstrapper` 컴포넌트의 `_loadingIndicator` SerializedProperty를 CanvasGroup으로 재연결
4. EditorSceneManager.MarkSceneDirty() → 저장 안내

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| `_loadingIndicator` CanvasGroup 초기값이 alpha=1이면 씬 시작 시 로딩 스피너가 항상 보임 | 에디터 스크립트에서 alpha=0으로 초기화 |
| 씬이 Login.unity가 아닌 상태에서 에디터 스크립트 실행 | 스크립트에서 현재 씬 이름 검증 후 경고 출력 |

---

## 수정 파일 목록

```
[수정]
- Assets/_Project/Scripts/Bootstrap/LoginBootstrapper.cs

[추가]
- Assets/Editor/Setup/FixLoginRuleViolations.cs  (에디터 스크립트, 1회성)

[씬 수정 — 에디터 스크립트 실행 후]
- Assets/_Project/Scenes/Login.unity
```

---

## 작업 순서

1. `LoginBootstrapper.cs` 코드 수정
2. `FixLoginRuleViolations.cs` 에디터 스크립트 작성
3. 사용자에게 Unity에서 Login.unity 열고 실행 요청
4. 씬 저장 확인
