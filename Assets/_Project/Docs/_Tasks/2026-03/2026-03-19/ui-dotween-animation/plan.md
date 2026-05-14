# Plan: UI DOTween 애니메이션 프레임워크

**날짜:** 2026-03-19
**관련 Research:** [research.md](research.md)

---

## 설계 결정

- `AnimatedPanel`: base class 아닌 **컴포넌트 방식** — 기존 스크립트 구조 변경 최소화
- `UIAnimator`: static 헬퍼 — 공통 애니메이션 패턴 재사용
- 팝업 패널 GameObject에 `CanvasGroup` + `AnimatedPanel` 추가
- `ToastManager`: 싱글턴 — 알림 큐 관리
- 레이어: 모두 `Presentation/UI/Common/` (아키텍처 위반 없음)

---

## Phase 1 — 프레임워크 + 팝업류

### 1-1. UIAnimator.cs (신규)

**경로**: `Assets/_Project/Scripts/Presentation/UI/Common/UIAnimator.cs`

```csharp
public static class UIAnimator
{
    // ── 팝업 등장/퇴장 ──
    // 등장: CanvasGroup.DOFade(0→1) + Transform.DOScale(0.9→1, Ease.OutBack)
    public static Sequence PopupShow(CanvasGroup cg, Transform t, float duration = 0.2f);

    // 퇴장: DOFade(1→0) + DOScale(1→0.9) → OnComplete callback
    public static Sequence PopupHide(CanvasGroup cg, Transform t,
        System.Action onComplete = null, float duration = 0.15f);

    // ── 패널 슬라이드 ──
    // 하단에서 슬라이드 업: DOAnchorPosY + DOFade
    public static Sequence SlideInFromBottom(RectTransform rt, CanvasGroup cg,
        float offsetY = 300f, float duration = 0.25f);

    // 아래로 슬라이드 아웃
    public static Sequence SlideOutToBottom(RectTransform rt, CanvasGroup cg,
        System.Action onComplete = null, float offsetY = 300f, float duration = 0.2f);

    // 상단에서 슬라이드 다운: DOAnchorPosY + DOFade (GameEndUI용)
    public static Sequence SlideInFromTop(RectTransform rt, CanvasGroup cg,
        float offsetY = 300f, float duration = 0.25f);

    // 위로 슬라이드 아웃
    public static Sequence SlideOutToTop(RectTransform rt, CanvasGroup cg,
        System.Action onComplete = null, float offsetY = 300f, float duration = 0.2f);

    // ── 버튼 피드백 ──
    public static Tween ButtonPunch(Transform t, float duration = 0.15f);

    // ── 색상 전환 ──
    public static Tween ColorTransition(Graphic graphic, Color target, float duration = 0.15f);

    // ── 인게임 HUD ──
    // 숫자 증감: DOCounter (from→to)
    public static Tween CountTo(TMP_Text text, int from, int to, float duration = 0.4f);

    // 텍스트 flash (골드 증가=노랑, 감소=빨강, 일반=흰색)
    public static Sequence FlashText(TMP_Text text, Color flashColor, float duration = 0.3f);

    // HP바 채움: Image.DOFillAmount
    public static Tween FillTo(Image fill, float to, float duration = 0.25f);
}
```

---

### 1-2. AnimatedPanel.cs (신규)

**경로**: `Assets/_Project/Scripts/Presentation/UI/Common/AnimatedPanel.cs`

팝업/패널 GameObject에 직접 부착하는 컴포넌트.
CanvasGroup + RectTransform DOTween 애니메이션 캡슐화.

```csharp
public class AnimatedPanel : MonoBehaviour
{
    // Inspector 설정
    [SerializeField] private AnimationType _animationType = AnimationType.PopupFade;
    [SerializeField] private float _showDuration = 0.2f;
    [SerializeField] private float _hideDuration = 0.15f;
    [SerializeField] private float _slideOffset = 300f; // SlideFromBottom 전용

    // 애니메이션 타입
    public enum AnimationType { PopupFade, SlideFromBottom, SlideFromTop }

    // CanvasGroup 자동 추가 (없으면)
    private CanvasGroup _cg;
    private RectTransform _rt;
    private Sequence _currentSeq;

    // 표시 여부
    public bool IsVisible { get; private set; }

    // 팝업 표시
    public void Show()
    {
        _currentSeq?.Kill();
        gameObject.SetActive(true);
        // animationType에 따라 UIAnimator 호출
    }

    // 팝업 숨김
    public void Hide(System.Action onComplete = null)
    {
        _currentSeq?.Kill();
        // 애니메이션 완료 후 SetActive(false)
    }

    private void OnDestroy() => _currentSeq?.Kill();
}
```

---

### 1-3. 기존 팝업 수정

#### GameEndUI.cs
```csharp
// AnimationType: SlideFromTop (위에서 아래로 슬라이드)
// Show(): SlideInFromTop → 게임 종료 결과 표시
// Hide(): SlideOutToTop → 닫기
[SerializeField] private AnimatedPanel _panel; // AnimationType = SlideFromTop
```

#### ProductionPanelUI.cs
```csharp
// AnimationType: SlideFromBottom (하단에서 슬라이드 업)
[SerializeField] private AnimatedPanel _popup; // AnimationType = SlideFromBottom
```

#### BuildingPlacementUI.cs
```csharp
// AnimationType: SlideFromBottom (ProductionPanelUI와 일관성 통일)
// 기존 PopupFade → SlideFromBottom 변경
[SerializeField] private AnimatedPanel _popup; // AnimationType = SlideFromBottom
```

#### RematchRequestPopup.cs
```csharp
// 슬라이딩 없이 DOFade만 사용 (기존 유지)
// ShowRequest(): _overlay + _requestPanel에 DOFade 적용
// ShowDeclined(): _overlay + _declinedPanel에 DOFade 적용
// Hide(): DOFade 후 SetActive(false)
// [버그 수정] 거절 후 blocksRaycasts 미해제 → Hide() 완료 시 모든 패널 blocksRaycasts=false 보장
// [버그 수정] _currentFade 단일 변수 공유 → 패널별 별도 Tween 변수로 분리
```

---

## Phase 2 — 버튼 피드백 + 프로그레스바

### 버튼 클릭 피드백
- 생산 버튼(Pistoleer/Assault/Sniper): 클릭 시 `UIAnimator.ButtonPunch()`
- 자동생산 활성화: `UIAnimator.ButtonPunch()` + `UIAnimator.ColorTransition()` (강조색)
- 건물 배치 버튼: `UIAnimator.ButtonPunch()`

### 프로그레스바
- `ProductionPanelUI._progressFill`: `UIAnimator.FillTo()` 적용
  - 단, 매 프레임 Update에서 호출되므로 DOTween 남용 방지 — 값 변화 감지 후 한 번만 Tween

---

## Phase 3 — 로비 탭/뷰 전환

### TabBarView.cs
```csharp
// Before: button.colors = colors (즉시)
// After: UIAnimator.ColorTransition(tabImage, targetColor)
```

### 서브뷰 전환 (BattleRootView 및 각 서브뷰)
- 현재 뷰: `UIAnimator.PopupHide()` (DOFade 1→0)
- 새 뷰: `UIAnimator.PopupShow()` (DOFade 0→1)

---

## Phase 4 — 인게임 HUD

### GameHudUI.cs
```csharp
// 골드 변화 감지 시:
int prev = _lastGold;
_lastGold = gold;
UIAnimator.CountTo(_goldText, prev, gold);          // 숫자 증감 애니메이션
UIAnimator.FlashText(_goldText, gold > prev         // 증가=노랑, 감소=빨강
    ? Color.yellow : new Color(1f, 0.4f, 0.4f));
```

---

## Phase 5 — 토스트 알림 시스템 (신규)

### ToastNotification.cs (신규)
**경로**: `Assets/_Project/Scripts/Presentation/UI/Common/ToastNotification.cs`

```csharp
public class ToastNotification : MonoBehaviour
{
    // 메시지 텍스트, 배경 색상 (성공/실패/정보)
    public void Show(string message, ToastType type, System.Action onComplete);
    // 등장: SlideInFromBottom + DOFade → 2초 유지 → DOFade 퇴장
}
```

### ToastManager.cs (신규)
**경로**: `Assets/_Project/Scripts/Presentation/UI/Common/ToastManager.cs`

```csharp
public class ToastManager : MonoBehaviour
{
    public static ToastManager Instance { get; private set; }

    // 외부 호출 API
    public void ShowInfo(string message);    // 파랑
    public void ShowSuccess(string message); // 초록
    public void ShowFail(string message);    // 빨강

    // 큐: 현재 알림 퇴장 완료 후 다음 알림 표시
}
```

### 연동 (향후 구현 시)
- `NetworkBuildingController.BuildFailedClientRpc` → `ToastManager.Instance.ShowFail("건물 배치 실패")`
- `NetworkProductionController.EnqueueFailedClientRpc` → `ToastManager.Instance.ShowFail("생산 큐 추가 실패")`

---

## 수정 파일 전체 목록

| 파일 | 변경 내용 | Phase |
|------|---------|-------|
| `Common/UIAnimator.cs` | 신규 — static 헬퍼 | 1 |
| `Common/AnimatedPanel.cs` | 신규 — 팝업 컴포넌트 | 1 |
| `GameEndUI.cs` | `GameObject→AnimatedPanel` 참조 교체 | 1 |
| `ProductionPanelUI.cs` | `GameObject→AnimatedPanel` 참조 교체 | 1 |
| `BuildingPlacementUI.cs` | `GameObject→AnimatedPanel` 참조 교체 | 1 |
| `RematchRequestPopup.cs` | DOFade 적용 | 1 |
| `ProductionPanelUI.cs` | 버튼 ButtonPunch, FillTo | 2 |
| `BuildingPlacementUI.cs` | 버튼 ButtonPunch | 2 |
| `TabBarView.cs` | DOColor 탭 전환 | 3 |
| 로비 서브뷰 각각 | PopupShow/Hide 뷰 전환 | 3 |
| `GameHudUI.cs` | DOCounter + FlashText | 4 |
| `Common/ToastNotification.cs` | 신규 | 5 |
| `Common/ToastManager.cs` | 신규 | 5 |

---

## 위험 요소

| 위험 | 내용 | 대응 |
|------|------|------|
| AnimatedPanel.IsOpen 대체 | 기존 `IsOpen => _popup.activeSelf` 로직 | `AnimatedPanel.IsVisible` 속성으로 대체 |
| ClosedFrame 타이밍 | Hide() 완료 시점이 애니메이션 종료 후로 지연 | Hide() 호출 시점에 ClosedFrame 설정 (애니메이션 완료 전) |
| ProductionPanelUI 프로그레스바 | Update에서 매 프레임 DOTween → Tween 과다 생성 | 값 변화 감지 시에만 Tween 시작, 이전 Tween Kill |
| RematchRequestPopup _currentFade 공유 | 패널별 페이드가 단일 변수 공유 → 이전 Tween Kill 위험 | 패널별 별도 Tween 변수(_overlayFade, _requestFade, _declinedFade)로 분리 |
| RematchRequestPopup 거절 후 UI 비상호작용 | blocksRaycasts가 해제되지 않아 전체 UI 클릭 불가 | Hide() 완료 OnComplete에서 모든 CanvasGroup blocksRaycasts=false 보장 |

---

## 테스트 체크리스트

### Phase 1
- [ ] GameEndUI 등장/퇴장 SlideFromTop 애니메이션 (위에서 아래로)
- [ ] ProductionPanelUI 팝업 등장/퇴장 SlideFromBottom 애니메이션
- [ ] BuildingPlacementUI 팝업 등장/퇴장 SlideFromBottom 애니메이션 (PopupFade → 변경)
- [ ] RematchRequestPopup DOFade 애니메이션 (슬라이딩 없음)
- [ ] RematchRequestPopup 거절 후 UI 상호작용 정상 복구 확인
- [ ] AnimatedPanel.IsVisible 값 정확성 확인

### Phase 2
- [ ] 유닛 생산 버튼 클릭 ButtonPunch
- [ ] 자동생산 롱프레스 활성화 색상 전환
- [ ] 건물 배치 버튼 ButtonPunch
- [ ] 생산 프로그레스바 FillTo 부드러운 증가

### Phase 3
- [ ] 로비 탭 전환 색상 보간
- [ ] 서브뷰 전환 Fade 애니메이션

### Phase 4
- [ ] 골드 증가 시 CountTo + 노란 flash
- [ ] 골드 감소 시 CountTo + 빨간 flash
- [ ] 인구 변화 시 CountTo

### Phase 5
- [ ] 토스트 알림 슬라이드 등장/퇴장
- [ ] 성공/실패/정보 색상 구분
- [ ] 연속 알림 큐 처리
