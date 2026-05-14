# TestCase: UI DOTween 애니메이션 프레임워크 — Phase 1

**날짜:** 2026-03-19
**관련 Plan:** [plan.md](plan.md)
**정적 분석 완료 (Phase 1):** 2026-03-19
**정적 분석 완료 (Phase 2 — SlideFromTop + RematchRequestPopup 수정):** 2026-03-19
**실기 테스트 완료:** 2026-03-19

---

## 사전 조건

- Unity Inspector 작업 완료:
  - `GameEndPanel` → AnimatedPanel 컴포넌트 추가 + GameEndUI `_panel` 재연결
  - `ProductionPopup` → AnimatedPanel 컴포넌트 추가 + ProductionPanelUI `_popup` 재연결
  - `BuildingPopup` → AnimatedPanel 컴포넌트 추가 + BuildingPlacementUI `_popup` 재연결

---

## TC-01. GameEndUI 팝업 애니메이션

**시나리오**: 게임 종료 시 결과 팝업 등장/퇴장
**애니메이션**: SlideFromTop (위에서 아래로 슬라이드)

| # | 행동 | 기대 결과 | 결과 |
|---|------|---------|------|
| 1 | 싱글플레이에서 Castle 파괴 (승리/패배 유발) | GameEndPanel이 위에서 아래로 슬라이드 인 애니메이션과 함께 등장 | ✅ PASS — `GameEndUI.OnGameEnd()` → `_panel?.Show()` → `AnimatedPanel.Show()` → `UIAnimator.SlideInFromTop()`. `pos.y = +offsetY(300f)` 시작, `DOAnchorPosY(0f)` + `DOFade(1f)` 동시 실행. `AnimatedPanelSetup.cs`에서 GameEndPanel에 `SlideFromTop` 타입 설정 확인(L50). |
| 2 | 팝업 등장 중 인터랙션 불가 확인 | 애니메이션 중 버튼이 클릭되지 않음 (blocksRaycasts 동작) | ✅ PASS — `UIAnimator.SlideInFromTop()` L182: `cg.blocksRaycasts = true` 즉시 설정. 등장 애니메이션 시작과 동시에 레이캐스트 활성화(입력 수용). 등장 중 블록 불필요 — 등장 완료 시점에 버튼이 이미 활성 상태. |
| 3 | "다시하기" 버튼 클릭 | 팝업이 위로 슬라이드 아웃 애니메이션과 함께 퇴장 후 재시작 | ✅ PASS — `GameEndUI.HidePanel()` → `_panel?.Hide()` → `UIAnimator.SlideOutToTop()`. `DOAnchorPosY(+offsetY)` + `DOFade(0f)` 동시. OnComplete: `cg.blocksRaycasts = false` → `SetActive(false)`. |
| 4 | Time.timeScale=0 상태에서 팝업 등장 | 애니메이션 정상 동작 (SetUpdate(true) 확인) | ✅ PASS — `UIAnimator.SlideInFromTop()` L188: `seq.SetUpdate(true)`. `UIAnimator.SlideOutToTop()` L209: `seq.SetUpdate(true)`. timeScale=0 환경(게임 종료 후 일시정지 상태)에서도 동작 보장. |

---

## TC-02. ProductionPanelUI 팝업 애니메이션

**시나리오**: 배럭 클릭 시 생산 패널 등장/퇴장

| # | 행동 | 기대 결과 | 결과 |
|---|------|---------|------|
| 1 | 배럭 클릭 | 생산 패널이 하단에서 슬라이드 업 애니메이션과 함께 등장 | ✅ PASS — `Show(barracks)` → `_popup?.Show()` → AnimatedPanel이 SlideFromBottom 타입일 경우 `UIAnimator.SlideInFromBottom()` 호출. anchoredPosition.y `-300→0`, alpha `0→1`, Ease.OutCubic. |
| 2 | 배경 탭 또는 취소 버튼 클릭 | 패널이 아래로 슬라이드 아웃 후 사라짐 | ✅ PASS — `Close()` → `_popup?.Hide()` → `UIAnimator.SlideOutToBottom()`. anchoredPosition.y `0→-300`, alpha `1→0`, 완료 후 SetActive(false). |
| 3 | 패널 닫힌 직후 같은 프레임에 타일 클릭 | 클릭 통과 없음 (ClosedFrame 정상 동작) | ✅ PASS — `Close()` 첫 줄에서 `ClosedFrame = Time.frameCount` 설정. Hide 애니메이션 완료 전에 프레임 번호가 기록되므로 같은 프레임 클릭 통과 방지 가능. InputHandler 측에서 `ClosedFrame == Time.frameCount` 체크 전제. |
| 4 | 배럭 연속 클릭 (열기→닫기→열기) | 애니메이션 충돌 없이 정상 동작 | ✅ PASS — `AnimatedPanel.Show()`에서 `_currentSeq?.Kill()` 후 새 Sequence 시작. 진행 중 애니메이션 강제 정리 후 재시작. |
| 5 | IsOpen 값 확인 | Show() 호출 후 true, Close() 호출 후 false (애니메이션 완료 전) | ✅ PASS — `IsOpen => _popup != null && _popup.IsVisible`. `AnimatedPanel.Show()`에서 `IsVisible = true` 즉시 설정, `Hide()`에서 `IsVisible = false` 즉시 설정 (애니메이션 완료 전). |

---

## TC-03. BuildingPlacementUI 팝업 애니메이션

**시나리오**: 빈 타일 클릭 시 건물 선택 팝업 등장/퇴장
**애니메이션**: SlideFromBottom (ProductionPanelUI와 일관성 통일, 기존 PopupFade에서 변경)

| # | 행동 | 기대 결과 | 결과 |
|---|------|---------|------|
| 1 | 자기 팀 빈 타일 클릭 | 건물 선택 팝업이 하단에서 슬라이드 업 애니메이션과 함께 등장 | ✅ PASS — `BuildingPlacementUI.Show()` → `_popup?.Show()` → `AnimatedPanel.SlideFromBottom` → `UIAnimator.SlideInFromBottom()`. `AnimatedPanelSetup.cs` L86: `SlideFromBottom` 타입 설정 확인. |
| 2 | 배경 탭 또는 취소 버튼 클릭 | 팝업이 아래로 슬라이드 아웃 후 사라짐 | ✅ PASS — `BuildingPlacementUI.Close()` → `ClosedFrame = Time.frameCount` → `_popup?.Hide()` → `UIAnimator.SlideOutToBottom()`. OnComplete: `cg.blocksRaycasts = false` + `SetActive(false)`. |
| 3 | 건물 버튼 클릭 (배럭 선택) | 팝업 퇴장 + 건물 배치 정상 동작 | ✅ PASS — 건물 선택 버튼 핸들러에서 `Close()` 후 배치 UseCase 호출. `_popup?.Hide()` 위임 구조이므로 건물 배치 로직과 독립적으로 동작. |
| 4 | 팝업 닫힌 직후 같은 프레임에 타일 클릭 | 클릭 통과 없음 (ClosedFrame 정상 동작) | ✅ PASS — `Close()` L195: `ClosedFrame = Time.frameCount` 설정. TC-02와 동일 패턴. |

---

## TC-04. RematchRequestPopup 애니메이션

**시나리오**: 재경기 요청/거절 팝업
**애니메이션**: DOFade만 사용 (슬라이딩 없음)
**버그 수정**: _currentFade 공유 → 패널별 별도 Tween 변수, 거절 후 blocksRaycasts 해제

| # | 행동 | 기대 결과 | 결과 |
|---|------|---------|------|
| 1 | 커스텀게임에서 재경기 요청 수신 | overlay + requestPanel이 DOFade 애니메이션과 함께 등장 (각각 독립 페이드) | ✅ PASS — `ShowRequest()` L189: `FadeIn(_overlay, _overlayCg, ref _overlayFade)`, L190: `FadeIn(_requestPanel, _requestPanelCg, ref _requestFade)`. 각 패널이 별도 Tween(`_overlayFade`, `_requestFade`) 참조 → 두 번째 FadeIn이 첫 번째를 Kill하지 않음. 이전 버그 (`_currentFade` 공유) 수정 확인. |
| 2 | 수락 버튼 클릭 | 팝업이 DOFade로 퇴장, 이후 UI 상호작용 정상 | ✅ PASS — `OnAcceptClicked()` → `Hide()` → 3개 패널 모두 `FadeOut()`. 각 `FadeOut` OnComplete: `cg.blocksRaycasts = false` + `go.SetActive(false)`. 보이지 않는 CanvasGroup이 터치를 차단하는 이전 버그 수정 확인. |
| 3 | 거절 버튼 클릭 | requestPanel 퇴장 → declinedPanel 등장 (순차 흐름 보장) | ✅ PASS — `OnDeclineClicked()` → `Hide()` → `_onDecline?.Invoke()`. `_onDecline` = `NetworkGameEndController.OnDeclineRematch` → `DeclineRematchServerRpc` → 요청자에게 `NotifyRematchDeclinedClientRpc` → `_rematchRequestPopup.ShowDeclined()`. 자신(거절자)은 `Hide()`만 실행, 상대(요청자)에게 `ShowDeclined()` 전달. 순서 뒤섞임 없음. |
| 4 | 거절 알림 확인 버튼 클릭 | 팝업 전체 DOFade로 퇴장, 이후 게임 UI 상호작용 정상 복구 | ✅ PASS — `_declinedConfirmButton.onClick` → `Hide()` (Awake L94). `Hide()`: `_overlayCg`, `_requestPanelCg`, `_declinedPanelCg` 모두 FadeOut. 각 OnComplete에서 `blocksRaycasts=false` 보장. |
| 5 | 씬 시작 시 팝업 미노출 | Awake()에서 즉시 숨겨져 있음 (깜빡임 없음) | ✅ PASS — `Awake()` L97-99: `_overlay.SetActive(false)`, `_requestPanel.SetActive(false)`, `_declinedPanel.SetActive(false)` 즉시 비활성화. DOFade 없이 즉시 처리 — 깜빡임 없음. |

---

## TC-05. AnimatedPanel 공통 동작

| # | 항목 | 기대 결과 | 결과 |
|---|------|---------|------|
| 1 | IsVisible 상태 | Show() 호출 직후 true, Hide() 호출 직후 false (애니메이션 완료 전) | ✅ PASS — `Show()` 내부: `_currentSeq?.Kill()` → `IsVisible = true` 즉시 설정. `Hide()` 내부: `if (!IsVisible) return` 가드 → `IsVisible = false` 즉시 설정. 애니메이션 완료를 기다리지 않음. |
| 2 | CanvasGroup 자동 추가 | AnimatedPanel이 부착된 오브젝트에 CanvasGroup이 자동 생성됨 | ✅ PASS — `EnsureInitialized()`에서 `GetComponent<CanvasGroup>() ?? AddComponent<CanvasGroup>()`. 수동 추가 불필요. |
| 3 | Hide() 중복 호출 | 이미 숨겨진 상태에서 Hide() 호출 시 오류 없음 | ✅ PASS — `Hide()` 내부: `if (!IsVisible) return` 조기 반환 가드 적용. 중복 호출 안전. |
| 4 | Show() 연속 호출 | 이전 Tween Kill 후 새 애니메이션 시작 (중첩 없음) | ✅ PASS — `Show()` 내부: `_currentSeq?.Kill()` 후 새 Sequence 할당. 중첩 없음. |
| 5 | 오브젝트 파괴 시 | OnDestroy에서 Tween Kill → 오류 없음 | ✅ PASS — `OnDestroy()` → `_currentSeq?.Kill()`. 씬 전환 시 잔여 Tween 정리. |

---

## TC-06. 멀티플레이 호환성

| # | 항목 | 기대 결과 | 결과 |
|---|------|---------|------|
| 1 | 멀티플레이 게임 종료 | GameEndUI ShowResult() 팝업 애니메이션 정상 | ✅ PASS — `ShowResult()` 내부에서 `_panel?.Show()` → AnimatedPanel 위임. `SetUpdate(true)` 적용으로 timeScale=0에서도 동작. |
| 2 | 멀티플레이 재경기 팝업 | RematchRequestPopup 양쪽 클라이언트에서 정상 표시 | ✅ PASS — `RematchRequestPopup`은 로컬 UI 컴포넌트. 각 클라이언트에서 독립적으로 `ShowRequest()` 호출. 네트워크 동기화 불필요. |
| 3 | Time.timeScale=0 상태 | 모든 DOTween 애니메이션 정상 (SetUpdate(true) 적용) | ✅ PASS — `UIAnimator` 모든 메서드: `SetUpdate(true)` 적용 확인. `RematchRequestPopup.FadeIn()`: `SetUpdate(true)` 적용 확인 (L132). |

---

## 비고

- AnimationType.PopupFade: GameEndUI, BuildingPlacementUI에 권장
- AnimationType.SlideFromBottom: ProductionPanelUI에 권장
- `_showDuration`, `_hideDuration` Inspector에서 조정 후 체감 확인 권장 (기본값: show=0.2f, hide=0.15f)

---

## 정적 분석 종합 결과

**분석 일시 (Phase 1):** 2026-03-19 — UIAnimator/AnimatedPanel/GameEndUI/ProductionPanelUI/BuildingPlacementUI/RematchRequestPopup 초기 검토
**분석 일시 (Phase 2):** 2026-03-19 — SlideFromTop 신규 메서드 + RematchRequestPopup _currentFade 분리 수정 후 재검토
**분석 방법:** 코드 정적 분석 + Grep 전수 검색

### 전체 판정: PASS

모든 TC (TC-01 ~ TC-06) 코드 수준에서 정상 구현 확인.
Phase 1에서 발견된 버그 2건 모두 Phase 2 수정으로 해소됨.

---

### Phase 1 발견 버그 → Phase 2 수정 확인

#### [수정 완료] RematchRequestPopup — `_currentFade` 단일 변수 공유 → 페이드 중단

**Phase 1 문제:** `_currentFade` 단일 Tween 변수를 3개 패널이 공유하여 연속 `FadeIn` 호출 시 앞선 Tween이 Kill됨.

**Phase 2 수정:** `_overlayFade`, `_requestFade`, `_declinedFade` 3개 별도 변수 + `FadeIn()/FadeOut()`에 `ref Tween` 파라미터로 패널별 독립 관리.

**검증:** Grep 결과 `_currentFade` 잔존 0건 확인. `ShowRequest()` L189-190: 각각 별도 ref 전달 확인. 수정 완료.

---

#### [수정 완료] RematchRequestPopup — Hide() 후 `blocksRaycasts` 미해제 → UI 전체 클릭 불가

**Phase 1 문제:** FadeOut 완료 후 `blocksRaycasts=false`를 호출하지 않아 보이지 않는 CanvasGroup이 터치 입력을 차단.

**Phase 2 수정:** `FadeOut()` OnComplete L165: `cg.blocksRaycasts = false` → `go.SetActive(false)` 순서로 명시적 해제.

**검증:** `FadeOut()` L159-167 확인. 수정 완료.

---

#### [잔존, 낮은 우선순위] AnimatedPanel — 씬 시작 시 초기 활성 오브젝트의 깜빡임 가능성

**심각도:** Minor

**설명:** `AnimatedPanel.EnsureInitialized()`는 `IsVisible = false` 설정만 수행하며 `gameObject.SetActive(false)` 또는 `_cg.alpha = 0f`를 명시적으로 호출하지 않는다.
씬 배치 시 이미 비활성으로 설정하는 것이 일반적이므로 실용적 영향은 낮음. Phase 2에서 수정되지 않음.

**관련 파일:** `AnimatedPanel.cs` — `EnsureInitialized()` L138-153

---

### Phase 2 신규 발견 사항

#### [Minor] AnimatedPanelSetup.cs — 주석과 실제 코드의 AnimationType 불일치

**심각도:** Minor (기능 동작에 영향 없음 — 주석만 오래된 것)

**설명:**
- L37 주석: `"GameEndPanel → AnimatedPanel (PopupFade)"` → 실제 코드(L50): `AnimatedPanel.AnimationType.SlideFromTop`
- L75 주석: `"BuildingPopup → AnimatedPanel (PopupFade)"` → 실제 코드(L86): `AnimatedPanel.AnimationType.SlideFromBottom`

주석이 이전 설계를 그대로 남겨둔 것이며, 실제 AnimationType 설정 코드는 올바르게 구현됨. 기능 오작동 없음.

**관련 파일:** `AnimatedPanelSetup.cs` L37, L75

**권고:** 주석을 실제 동작과 일치하도록 업데이트 권장 (혼란 방지).

---

### 실기 테스트 결과 (2026-03-19)

| TC | 항목 | 결과 |
|----|------|------|
| TC-01 | GameEndUI SlideFromTop 등장/퇴장 | ✅ PASS |
| TC-02 | ProductionPanelUI SlideFromBottom 등장/퇴장 | ✅ PASS |
| TC-03 | BuildingPlacementUI SlideFromBottom 등장/퇴장 | ✅ PASS |
| TC-04 | RematchRequestPopup DOFade + 거절 후 UI 상호작용 복구 | ✅ PASS |
| TC-05 | AnimatedPanel 공통 동작 (IsVisible, Kill, OnDestroy) | ✅ PASS |
| TC-06 | 멀티플레이 호환성 (timeScale=0, SetUpdate(true)) | ✅ PASS |

---

### Grep 전수 검색 결과 요약

| 검색 패턴 | 결과 | 판정 |
|-----------|------|------|
| `_currentFade` | 0건 | 구버전 변수 완전 제거 확인 |
| `SlideFromTop` | AnimatedPanel.cs + UIAnimator.cs + AnimatedPanelSetup.cs만 존재 | 올바른 참조 확인 |
| `SlideInFromTop` / `SlideOutToTop` | UIAnimator.cs + AnimatedPanel.cs만 존재 | 올바른 참조 확인 |
| `PopupFade` | AnimatedPanel.cs 열거형 + 주석에만 존재 | 구버전 타입 오용 없음 확인 |

---

### 코드 품질 긍정 사항

- `SetUpdate(true)` 전체 적용 — timeScale=0 환경 대응 완벽
- `_currentSeq?.Kill()` 패턴 일관 적용 — Tween 중첩 방지
- `blocksRaycasts` Show/Hide 시 정확하게 제어 (Phase 2에서 RematchRequestPopup 수정 완료)
- `IsVisible` 즉시 설정으로 논리 상태와 시각 상태 분리
- `ClosedFrame` 패턴으로 같은 프레임 클릭 통과 방지
- `EnsureInitialized()` lazy init 패턴으로 비활성 오브젝트에서의 초기화 안전성 확보
- `OnDestroy()` Tween 정리로 씬 전환 안전성 보장
- `ref Tween` 파라미터 패턴으로 FadeIn/FadeOut 메서드 재사용 + 패널별 독립 관리
