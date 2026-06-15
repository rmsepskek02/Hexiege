# Research — Login 씬 규칙 위반 수정

## 이 작업이 왜 필요한가

Login.unity 씬 전수 점검(108개 GameObject, 규칙 1~6 전체) 결과,
GameSystemRules_UI.md 규칙 5(SetActive 금지)를 위반하는 항목이 1건 발견되었다.

나머지 규칙(1·2·3·4·6)은 모두 준수하고 있어 추가 수정이 불필요하다.

---

## 점검 방법

- Login.unity YAML 파일 직접 파싱 → 108개 GameObject 전수 확인
- 비활성 GO: 0개 (전체 활성)
- 배치된 MonoBehaviour 스크립트 목록 추출 → 각 코드 SetActive grep
- TMP 폰트 GUID 씬 전수 조회

---

## 준수 중인 항목 (이상 없음)

- Rule 1 (Canvas Scaler): 1080×1920 / ScaleWithScreenSize / Match=0 ✅
- Rule 2 (앵커 기반): 모든 SizeDelta = {x:0, y:0}, 100% 비율 기반 ✅
- Rule 3 (화면 비율): Screen.width/height 직접 참조 0건 ✅
- Rule 4 (SafeArea): SafeAreaContainer > SafeAreaFitter 부착, Background RaycastTarget=false ✅
- Rule 6 (폰트): Maplestory Light SDF(23개) / Bold SDF(21개) 총 44개 — 비허가 폰트 0건 ✅
- AnonymousWarningPopup._blockingOverlay: CanvasGroup 으로 올바르게 구현됨 ✅ (이전 세션에서 수정 완료)
- LoginRootView: ShowGroup/HideGroup 메서드에서 CanvasGroup 제어 ✅
- ConfirmPopup: CanvasGroup 올바르게 사용 ✅
- AnimatedPanel: SetActive 미사용, CanvasGroup으로만 제어 ✅

---

## 발견된 위반 사항

### [위반 1] Rule 5 — LoginBootstrapper.ShowLoading()

**파일**: `Assets/_Project/Scripts/Bootstrap/LoginBootstrapper.cs`

**위반 코드**:
- 라인 65: `[SerializeField] private GameObject _loadingIndicator;` — GameObject 타입
- 라인 219~223:
  ```csharp
  public void ShowLoading(bool show)
  {
      if (_loadingIndicator != null)
          _loadingIndicator.SetActive(show);  // ❌ SetActive 호출
  }
  ```

**`_loadingIndicator`란?**  
자동 로그인 / 비동기 요청 중 사용자에게 진행 상황을 알려주는 로딩 스피너 오브젝트.
SafeAreaContainer의 직속 자식으로 Login.unity에 배치됨.  
`ShowLoading(true/false)`가 AnonymousWarningPopup 진입 시, 익명 계속 클릭 시 등에서 호출된다.

**수정 방향**: `_loadingIndicator` 필드를 `CanvasGroup` 타입으로 변경.  
Show 시 `alpha=1/blocksRaycasts=true/interactable=true`,  
Hide 시 `alpha=0/false/false`.

---

## 영향 범위

| 항목 | 영향 |
|------|------|
| LoginBootstrapper.cs | `_loadingIndicator` 필드 타입 변경 → Inspector 재연결 필요 |

Inspector 재연결은 에디터 스크립트로 자동화 가능.
(`LoadingIndicator` 오브젝트에 CanvasGroup 추가 → LoginBootstrapper SerializedProperty 재연결)
