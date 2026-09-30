# Game Programmer — UI 시스템

전역 UIManager, BlockingOverlay, SceneLoader, Loading Indicator, Canvas SortingOrder, 팝업/패널 패턴.

---

## 🔴 이 파일이 담는 것 — 색인에서 내려온 상세 목차 (2026-09-29 이동)

> 이 목록은 `MEMORY.md` 「토픽 파일 인덱스」의 **이 파일 항목**에 들어 있던 상세다.
> 색인은 **매 작업마다 읽는 파일**이라 거기에는 한 줄 포인터만 남기고(C-8 · C-11)
> 실측값 · 작업 순서 · 함정 조건을 여기로 내렸다.
> 🔴 **삭제가 아니라 이동이다** — 문장은 그대로 옮겼고, 맨 앞의 자기 파일 링크 한 조각만 덜어냈다
>   (B-6 · E-16 — 폴더 총합 행수가 줄지 않는 것이 이동과 삭제를 가르는 검증법이다).
> ⚠️ 색인의 포인터는 **지우지 않았다** — 링크가 빠진 토픽 파일은 존재하지 않는 파일이 된다(C-10).

- UIManager, BlockingOverlay, SceneLoader, LoadingIndicator, Canvas SortingOrder,
  CanvasGroup/레이아웃/팝업/ToastUI 패턴, 생산·연구 패널 실측 구조,
  **건물 패널 골격(`Row0~2`) + 회전 테두리 머티리얼 `_Radius`·`_Inset` 공유 함정**,
  🔴 **알림 팝업(제목 + 본문 + 버튼 1개) 프리팹 전제 조건(2026-09-21) — `ShowAlert` 는 코드만으로
  성립하지 않는다(Panel 의 VerticalLayoutGroup 이 없으면 기존 팝업까지 회귀) · Panel 864×768 실측값 ·
  `ChildForceExpandHeight = true` 면 `preferredHeight` 가 전부 무시된다 · 높이 검산 370/262px ·
  셋업 스크립트는 멱등이라 프리팹이 깨졌을 때 되돌리는 수단으로도 쓴다**,
  🔴 **결과 화면의 상대 이탈 반영 단계 7(2026-09-21) — 이탈 문구를 코드 1곳(`const`)에만 두는 법과
  그 검증 grep · 30초 재시작을 규칙 M-3 의 전체 길이 재시작과 다른 진입점으로 가르는 법 ·
  「연결이 끊겼습니다」 팝업을 결과 화면에서만 억제한 방법과 그 근거(왜 「이탈 신호」가 아니라
  「결과 화면이 떠 있는가」로 걸었는가) · 인게임 경로가 무영향인 이유**,
  🔴 **결과 화면 「재경기 준비 중」 상태(2026-09-22) — 「사람을 기다리면 타이머가 돈다,
  시스템을 기다리면 멈춘다」 판단 기준 · 🔴 수락자는 자기 버튼 입력으로, 요청자는 서버 통보로
  **서로 다른 신호**로 같은 상태에 들어가는 이유 · 한도를 두 상수의 **곱**으로 두는 이유
  (한 창만 재면 정상 경로에서 거짓 신호가 난다) · 한도를 멈추는 자리 4곳 ·
  🔴 코루틴이 자기 자신을 StopCoroutine 으로 끊는 함정 · ShowLoading 을 쓰지 않은 근거(규칙 D-3) ·
  `RestoreRematchButton` 의 문구 복원을 가드 밖으로 꺼낸 변경**,
  🔴 **`GameEndUI` 재경기 상태 진단 로그(2026-09-27, 동작 무변경) — 「조용한 조기 반환」이 만드는
  진단 불가 상태 · 역할 표시를 `NetworkContext.IsNetworkServer` 로 얻는 법과 `NetworkActive=` 를
  함께 실어야 하는 이유(디스폰 시 Reset) · 「가드에는 로그를 넣지 않는다」 관례의 진짜 적용 범위 ·
  `[Conditional]` 이 인자 계산까지 지우는 성질을 이용해 조건식도 릴리스에서 없애는 법 ·
  시그니처를 안 바꾸고 `Origin=` 으로 호출처를 가르는 법 · 한 경기당 줄 수 계수.**
  🔴 **자동 복귀 카운트다운이 만료되지 않는 버그의 진단 로그 4자리(2026-09-27 2차, 동작 무변경) —
  `[CallerMemberName]` 로 호출부 7곳을 한 글자도 안 고치고 「누가 `StopCountdown()` 을 불렀는가」를
  가르는 법(과 메서드 그룹 변환이 있으면 못 쓴다는 단서) · 🔴 1순위 용의자 =
  `ReturnToLobby()` 첫 줄이 **지금 돌고 있는 자기 코루틴**을 `StopCoroutine` 하는 것과
  같은 파일 `RematchPreparingLimitCoroutine` 만 그 방어를 갖고 있는 비대칭 ·
  「`StartCoroutine` 이 비활성 오브젝트에서 실패했다」를 씬 실측으로 배제한 근거 4가지 ·
  `ShowResult()` 호출부 0건.**
  **[🔴 2026-09-27 정정 — 위 줄은 지우지 않는다(B-7)]** 🔴 **그 「1순위 용의자」 가설은 실기로 반증됐다** —
  `StoppedBy=ReturnToLobby` 가 찍힌 뒤에도 뒤 코드(Heartbeat 정지 · 퇴장 통보 · Shutdown)가 전부 실행됐고
  만료도 3회 관측됐다(메인 세션 로그 실측). 「카운트다운이 멈췄다」고 본 것은 Unity 의 **Error Pause** 로
  플레이 모드가 멈춰 있었기 때문이다. ⚠️ **그 방어(자기 핸들 먼저 null)를 `CountdownCoroutine` 에 새로
  넣지 말 것 — 필요 없다.**
  **[🔴 2026-09-29 — 위 2026-09-27 2차 항목 끝줄 「… 호출부 0건」에 대한 정정. 원문은 지우지 않는다(B-7)]**
  🔴 **그 메서드는 2026-09-29 회차(규칙 M-4)에 최종 삭제됐다**(죽은 코드 52줄). 「호출부 0건」은 사실로서
  참이지만 **가리키는 대상이 코드에 없다**(잔존 `grep` 실측 **0건**). ✅ 「카운트다운 시작 자리 셋뿐」은 유효.
  🔴 **실패 직후 「재경기 준비 중」으로 되돌아가는 버그 수정 + 상태 줄 문구 4개 개행(2026-09-27 3차,
  여기서 동작이 바뀐다) — 수락 접수 신호가 둘인데 🔴 도착 순서가 역할마다 뒤집힌다는 것(로컬 RPC 의
  동기 실행)과 그래서 「ⓑ 만 삼킨다」가 Host 수락자에서 무효인 이유 · 두 신호의 공통 입구 하나로
  모아 「먼저 온 하나」만 통과시키는 법 · 🔴 `_rematchFailed` 로 막으면 영구 고착되는 이유 ·
  회차 경계 표시를 내리는 자리 3곳과 「상대가 요청했다」 자리가 빠지면 무엇이 깨지는가 ·
  평시 문구를 const 로 뺀 이유와 맨 앞 `\n` 의 뜻 · 🔴 씬 실측으로 개행 레이아웃 위험을 배제한 근거.**
  🔴 **「다시하기」 버튼 활성 조건을 실패 사유까지 보게 좁힌 것(2026-09-27 4차, 여기서 동작이 바뀐다) —
  🔴 「깃발 하나로 상황을 판별한다」가 맞는지 보려면 **그 깃발을 켜는 시계의 길이**를 봐야 한다는 것
  (이탈 확정 30초 시계 때문에 진짜 연결 끊김에서도 **약 30초 동안** 「상대가 없다」와 「다시 해 보라」가
  동시에 뜨던 실측 30.35초) · 문구를 가르는 판단을 세우면 **그 판단을 공유해야 하는 컨트롤이 또 있는지**
  함께 봐야 한다는 것(문구는 규칙 18 을 따르는데 버튼만 따르지 않았다) · 기준식을 프로퍼티 하나로 모으고
  `SelectRematchFailedFormat` 의 반환값·역할은 건드리지 않는 법 · 호출부 3곳 확인 결과표 ·
  ⚠️ 주석에 문구를 베껴 적어 grep 을 무용하게 만든 함정 **2회째**.**
  🔴 **실패 통보가 **먼저** 오고 수락 신호가 **뒤에** 오는 경로(2026-09-28 5차, 여기서 동작이 바뀐다) —
  🔴 §15-6 과 증상은 같고 순서만 반대라 기존 판별(`_rematchAcceptSignalHandled`)이 **정당하게 통과**한다는 것 ·
  「같은 채널의 앞 구독자가 다른 채널을 동기 발행하면 뒤 구독자가 보는 상태는 이미 그 결과가 반영된 상태」 ·
  🔴 **회차 경계 세 자리의 비대칭 한 칸이 결함의 본체**이고 그 칸을 채워야 새 가드가 정상 경로를 막지 않는 이유 ·
  가드를 `_opponentLeft` **뒤**에 둔 근거(D-7 우선순위와 `Guard=` 분포를 같은 순서로 읽는다) ·
  🔴 도달 불가가 된 두 줄을 **삭제가 아니라 주석 비활성화**한 근거(`WORKFLOW.md` [4] 절차) ·
  「하나뿐이다」류 단정 주석 4자리를 전수 grep 으로 고친 것 · 🔴 주석 문구 사본 함정 **3회째**.**
  🔴 **그 두 줄의 최종 삭제(2026-09-28 6차, 동작 변경 0건) — WORKFLOW [4] 절차의 마지막 단계이고
  삭제를 미루는 것도 앞당기는 것도 위반이라는 판단 근거 · 🔴 근거 주석 3가지(원래 걱정 · 다른 방법으로
  해소됐다 · 되살릴 조건)를 남기면서 「비활성화해 두었다」라는 **상태 서술만** 고치는 일 ·
  낱말 grep 으로는 못 잡히는 자리를 **삭제한 식별자 이름 grep** 으로 한 번 더 찾는 법 ·
  ✅ **`git diff` 없이 「동작 변경 0건」을 측정으로 증명하는 법 — 주석·문자열을 걷어낸 코드 본문의
  SHA-256 을 수정 전/후로 비교(행수·중괄호는 증거가 못 된다)** · 🔴 주석 사본 함정 **4회째**이고
  베끼면 안 되는 것이 UI 문구뿐 아니라 **대입문·로그 키/필드 값**까지라는 것 ·
  XML 태그 균형은 **닫는 태그 기준**으로 세야 한다는 것(`<b`→`<br/>`, `<para`→`<paramref` 오탐).**
  🔴 **재경기 실패 3경로를 상태 줄 문구 하나로 묶은 것(2026-09-22 사용자 확정) — 공통 진입점
  `EnterRematchFailedState()` · 문구 분기 우선순위 「이탈 > 실패 > 평시」와 그 이유(결과보다 원인) ·
  실패 깃발을 내리는 자리 3곳 · 🔴 팝업을 쓰지 않은 근거(규칙 M-3 개정, 팝업이 「로비로」를 가린다) ·
  깃발을 카운트다운 재시작보다 먼저 세워야 하는 이유**
  🔴 **규칙 M-4 싱글 맵 준비 실패 안내(2026-09-29, 구현 + 실기 + 죽은 코드 52줄 최종 삭제까지 완료) —
  🔴 `Initialize()` 가 실패 깃발을 **읽히기 전에** 지워 조용히 실패하는 함정과 그 자리에 남긴 경고 주석 ·
  🔴 실패 뒤에도 `LoadMap()` 이 결과 화면을 **두 번 더 닫는다**(그래서 통보 자리에서 `Show()` 금지 —
  「깃발만 세우고 반환 후에 켠다」 계약) · 싱글에는 자동 복귀 타이머가 없다 ·
  실기 실측표와 🔴 미검증 5가지 · `ProjectMap` 실패는 여전히 안내 없음(범위 밖).
  **[🔴 2026-09-29 2차 정정 — 위 줄은 지우지 않는다(B-7). 끝의 「`ProjectMap` 실패는 안내 없음」
  한 조항만 더 이상 참이 아니다]**
  🔴 **후속 H·I·J·K-1·K-2·L·M(2026-09-29 2차, 구현 + 실기 검증 완료) — 투영 단계 실패도 싱글 실패 UI 를
  띄운다 · 🔴 문자열 상수의 단일 소스를 **레이어 방향**으로 정하는 법(새 의존 간선 0개)과 🔴 **마침표
  차이가 사양이라 합치면 안 되는 문구** · 🔴 주석 사본 함정 **6회째**이고 세는 대상이 **XML 태그 토큰 ·
  검사가 세는 타입 이름**까지 넓어졌다(선례 파일 3개는 아직 오탐을 낸다 — 내 위반으로 오해하지 말 것) ·
  씬 자리표시 두 자리를 비운 것이 **폴백을 없애는 일**이라는 점 · 상태 줄 쓰기 setter 일원화(직접 대입 0건) ·
  🔴 지난 회차 미검증 ⓐ~ⓔ 중 **닫힌 것(Unity 컴파일 · K-2 전제)과 여전히 열린 것**의 구분표.****
  🔴 **「경기 종료 후 UI」 마무리 회차(2026-09-29 3차 + 2026-09-30 실기 통과) — 씬과 함께 파괴되는 팝업이
  `DontDestroyOnLoad` 쪽 **공용 참조 카운터**를 점유한 채 사라지면 다음 씬이 막힌다는 것과 고칠 자리를
  `OnDestroy()` 로 고른 근거 · 🔴 주석 사본 함정 **7회째를 막은 것은 절차뿐**이고 판정 기준이
  「낱말이 나오는가」가 아니라 **「따옴표로 값을 베꼈는가」**라는 것 · 값 자리 개수로 「고칠 값이 있는가」를
  가르는 판정자와 그 **재는 방법 정정**(낱말 줄 수 ≠ 값 자리 수) · 같은 문구를 합치지 않는 근거를
  **사건**으로 가르는 법 · 씬 자리표시를 비울 때의 fileID 확인 · 색인 압축의 방식 ·
  🔴 **실기 판정은 「고쳤다」가 아니라 「증상 0건」**이고 확인 등급이 **육안**인 이유(팝업 두 개의 로그 호출 0건) ·
  🔴 **코루틴 클래스 번호로 빌드를 가르는 기법의 적용 한계** · ✅ **규칙이 보장한다고 적은 것을 코드가
  지키는지 대조하기**와 **형제 비대칭** 이라는 버그 탐색 수단 2가지.**
  🔴 **두 팝업의 관측 공백을 메운 회차(2026-09-30 · 동작 무변경) — 「막이 안 꺼졌다」를 가르려면
  팝업 쪽 줄과 관리자 쪽 줄이 **둘 다** 있어야 하는 이유(부재로 하는 판정은 약하다) ·
  🔴 **확인/알림 팝업의 닫기가 점유한 적 없는 몫을 놓는다**는 발견과 그래서 직전 회차의 「증상 0건」이
  근거 강도가 낮았다는 것(고치지 않고 보이게만 했다) · 규칙 D-5 구조를 **화면 문구를 한 자도 적지 않고**
  확인하는 법(적용 뒤 상태를 되읽는다) · ✅ 주석 사본 함정 **8회째를 만들지 않은 방법**과
  🔴 **새로 쓰는 글에서는 그 낱말을 아예 피하는 것이 싸다**는 판정 · 검사의 음성 통제 1회.**

## 전역 UIManager

- `SingletonMonoBehaviour<UIManager>` + `IUIManager`, DontDestroyOnLoad. Login 씬에서 1회 생성, 전 씬 공유
- **반드시 루트 GameObject로 배치** (DontDestroyOnLoad는 루트만 작동. 자식 배치 시 씬 전환마다 재생성+즉시파괴 → UIManager.Instance==null)
- IUIManager API: `ShowConfirm(message, onConfirm, onCancel=null, confirmLabel, cancelLabel)`, `ShowLoading(bool show, string message="")`, `ShowBlockingOverlay(Action onTap=null)` / `HideBlockingOverlay()`, `LoadSceneWithDelay`
- null-safe: 미생성 씬(Lobby/Game) 단독 실행 시 `UIManager.Instance?.` 패턴

---

## BlockingOverlay (UIManager 단일 소유)

- 각 팝업이 개별 소유하던 반투명 배경을 UIManager 단일 소유로 통합 (SafeArea 갇힘 해결)
- `_blockingOverlay`(CanvasGroup) + `_blockingOverlayButton`(Button). Modal(onTap=null, 입력차단만) / Popup(onTap!=null, 터치 시 콜백) 2모드
- **중첩은 `_blockingOverlayRefCount` 참조 카운터** — 0일 때만 실제 숨김. 언더플로 가드(`if(>0)`)
- 패널 전환 시 카운터 누수 주의: RematchRequestPopup 요청→거절 전환 시 `_overlayShown` bool 가드 + ShowOverlayOnce/HideOverlayOnce로 항상 0/1만 점유

---

## SceneLoader

- `Hexiege.Presentation` 정적 유틸리티. 모든 씬 전환 단일 진입점. `UIManager.LoadSceneWithDelay` 위임 (ShowLoading(true) 즉시 → 1초 대기 → LoadScene)
- 상수: `SceneLoader.Lobby` 등
- **Infrastructure→SceneLoader 직접 호출 금지** → GameEvents 경유 (OnNetworkBackToLobby / OnNetworkRematchStarting)

---

## Loading Indicator

- **ShowLoading 호출 위치**: 코루틴 외부에서 동기 실행 필수 (코루틴 내부면 다음 프레임 실행 → 텍스트 지연)
- **ShowLoading(false) 책임자(규칙 L-3)**: Login=LoginBootstrapper, Lobby=LobbyRootView.Start(), Game=GameBootstrapper.LoadMap()
- **초기 메시지 누락 주의**: ShowLoading(true) 메시지 없이 호출하면 텍스트 공백. 반드시 메시지 함께
- 최소 표시 시간(`_loadingMinDuration`, 기본 1f): ShowLoading(false) 시 미경과면 WaitForSecondsRealtime 지연 후 숨김. `_loadingShowTime` 기록 + `_hideLoadingCoroutine` 중복 hide 방지
- 독립 Canvas SortingOrder=300 (다른 팝업에 가리지 않도록)
- **ShowLoading(true)는 씬 전환이 실제로 일어나는 경우에만 사용**: ShowLoading(false)를 호출할 책임자(규칙 L-3)는 씬 전환 후의 Bootstrapper/RootView다. 씬 전환이 없으면 ShowLoading(false)가 호출되지 않아 로딩 인디케이터가 영구히 남는다.
- **포기(Forfeit)는 씬 전환 없이 GameEndUI만 표시 → ShowLoading 불필요**: 2026-06-26 `InGameSettingsUI.OnForfeitConfirmed()`에서 `ShowLoading(true)` 호출 제거(씬 전환 없어 해제 불가). GameSystemRules_UI.md 규칙 L-2 "게임 포기(멀티)" 항목 함께 제거.

---

## Canvas SortingOrder (최종 확정 구조)

```
SO 0   → [UI] Canvas (Game 씬 HUD)
SO 100 → UIManager Canvas (BlockingOverlay)
SO 200 → 각 패널 Canvas Override (BuildingPopup, BuildingActionPanel, InGameSettings, GameEndPanel, ProductionPopup)
SO 250 → ConfirmPopup 독립 Canvas (모달 팝업 — 항상 패널 위)
SO 300 → LoadingIndicator 독립 Canvas
```
- 게임 씬 패널이 UIManager보다 높은 SO 필요 시 Canvas Override 사용 (Override Sorting=true + GraphicRaycaster). GameSystemRules_UI Rule 4
- 참조: `GameSystemRules/GameSystemRules_CanvasSortingOrder.md`

---

## CanvasGroup 패턴 (Rule 5 — SetActive 금지)

- `SetActive(false)` 대신 `alpha=0; blocksRaycasts=false; interactable=false`
- `SetActive(true)` 대신 `alpha=1; blocksRaycasts=true; interactable=true`
- **이유**: SetActive(false)는 LayoutGroup에서 완전 제외(재활성화 시 레이아웃 깨짐), DontDestroyOnLoad에서 Awake 미호출
- 컴포넌트 부착은 런타임 AddComponent가 아닌 에디터에서 미리 (GameSystemRules_UI Rule 5)
- **신규 UI 뷰 체크리스트**: CanvasGroup 부착, `_canvasGroup` 연결, Show/Hide CanvasGroup 패턴

---

## 레이아웃 패턴

### VLG 자식 고정픽셀 → 앵커 기반 전환
- VLG: `childControlHeight=true`, `childForceExpandHeight=false`
- 각 자식 LayoutElement: `preferredHeight=원래SizeDelta.y`, `flexibleHeight=0`, 나머지 -1. 자식 sizeDelta=0
- ⚠️ childForceExpandHeight=true면 버튼이 비정상적으로 커짐
- ⚠️ flexibleHeight>0이면 추가 분배받아 크기 변동

### 균등 분배 (버튼 크기 불일치)
- 슬롯 아이콘 native size가 VLG preferredHeight 배분(Phase2)에서 Row 불균등 유발. childForceExpandHeight는 Phase3만 작동
- 해결: Row에 LayoutElement(preferredHeight=0, flexibleHeight=1), Slot에 (preferredWidth=0, flexibleWidth=1)

### Safe Area 전체화면 배경
- 전체화면 배경은 SafeAreaContainer 밖(Canvas 직속). anchor(0,0)~(1,1), offsetMin=offsetMax=0
- raycastTarget=false 필수. Hierarchy 순서는 SafeAreaContainer보다 위(먼저 그려짐=뒤에 표시)
- GameSystemRules_UI Rule 4

### IgnoreLayout 배지 (Lock Icon 등)
- Slot GO에 HorizontalLayoutGroup 있으면 배지가 가로로 나란히 배치됨 → `LayoutElement.ignoreLayout=true` 필수

---

## ConfirmPopup / 팝업

- ConfirmPopup: 범용 확인 팝업. Show(message, confirmLabel, cancelLabel, onConfirm, onCancel). 독립 Canvas SO=250
- ConfirmPopup.prefab 루트에 자체 Canvas 없으면 부모 UIManager Canvas(SO=100) 따라감 → 패널(SO=200)에 가려짐
- AnimatedPanel은 항상 active 상태 → `_panel.Show()` 직접 호출, SetActive(true) 선호출 불필요
- BlockingOverlay(CanvasGroup): Show alpha=1/blocksRaycasts/interactable=true, Hide 0/false/false

### 알림 팝업(제목 + 본문 + 버튼 1개) — 프리팹 전제 조건 (2026-09-21)

- `ConfirmPopup.ShowAlert(title, message, buttonLabel, onClick)` 는 코드만으로 성립하지 않는다.
  `ApplyTitle()` 이 제목을 `SetActive(false)` 로 숨길 때 **그 자리가 레이아웃에서 사라져야** 하는데,
  그건 **Panel 에 VerticalLayoutGroup 이 있어야** 성립한다. 없으면 제목 자리가 빈 공간으로 남아
  제목 없는 기존 확인/취소 팝업의 생김새까지 바뀐다(= 회귀).
  같은 이유로 `SetCancelButtonVisible(false)` 는 ButtonRow 의 HorizontalLayoutGroup 에 의존한다.
- Panel 실측 **864 × 768px** (Canvas 1080×1920, Match=0(width), Panel 앵커 X 0.1~0.9 / Y 0.3~0.7).
  VLG 값: padding 69/69/92/61 · spacing 38 · MiddleCenter · ChildControl W/H ✓ · ForceExpandW ✓ ·
  **ForceExpandH ✗**. LayoutElement: TitleText pref 70 / flex 0, MessageText pref -1 / **flex 1**,
  ButtonRow pref 207 / flex 0.
- 🔴 **`ChildForceExpandHeight = true` 면 자식이 남는 높이를 균등 분할해 `preferredHeight` 가 전부
  무시된다.** 고정 높이 + "남는 높이는 한 칸만 가져간다(flexibleHeight=1)" 구조를 쓰려면 반드시 false.
  검산: 제목 없음 768−92−61−38−207 = **370px**(변경 전 369px 과 사실상 동일) /
  제목 있음 768−92−61−38×2−70−207 = **262px**.
- 셋업 스크립트: `Assets/Editor/Setup/SetupConfirmPopupAlertLayout.cs`
  (메뉴 `Hexiege/Setup/Setup ConfirmPopup Alert Layout`, 1회성).
  🔴 **1회성이지만 멱등이라 다시 실행해도 결과가 같다 — 이 프리팹의 레이아웃이 깨졌을 때
  되돌리는 수단이 이 스크립트다.** 첫 실행 로그는 「새로 만든 것 6건 / 갱신한 것 없음」이고,
  두 번째 실행부터는 갱신 쪽에 숫자가 잡히는 것이 정상이다.
  스크립트 패턴 자체는 [architecture.md](architecture.md) 「기존 프리팹 '에셋' 을 고치는 스크립트」가 단일 소스다.
  ⚠️ **이 스크립트를 삭제할지 `Tools/` 로 옮길지는 사용자 판단 대기다**
  (`WORKFLOW.md` [5-2] 는 `Setup/` 을 「실행 후 삭제 가능」으로 규정하는데 위 재사용 가치와 어긋난다).
  단일 소스는 `Assets/_Project/Docs/ROADMAP.md` 의 2026-09-21 신설 행.
- ⚠️ **제목·취소 버튼 숨김에 `CanvasGroup` 이 아니라 `SetActive` 를 쓴 것은 의도된 선택이며,
  공통 UI 규칙 5(CanvasGroup 숨김/표시 패턴)의 예외로 규칙 문서에 적을지는 사용자 판단 대기다.**
  근거(규칙 5 의 두 금지 사유가 여기서는 성립하지 않는다)는 `ConfirmPopup.ApplyTitle()` 의 XML 주석에 있다.
  🔴 **규칙 5 를 고치지 말 것** — 결정 전이다.

### 팝업 CloseButton 무반응 패턴
- CloseButton GO가 씬에 있어도 C#에 `[SerializeField] Button _closeButton` 필드 없으면 Inspector 연결 불가 → 무반응. 필드 추가 + OnCloseButtonClicked()→Hide()
- 컴포넌트 교체 시 `_panel` 등 슬롯 재연결 필수 (null이면 Show/Hide의 `if(_panel!=null)` 분기 전부 스킵)
- 팝업이 LoginRootView + LoginBootstrapper 양쪽 슬롯 연결 필요한 경우 있음 (Bootstrap이 Initialize, RootView가 Show)

---

## ToastUI

- 싱글턴 MonoBehaviour, IPointerClickHandler. `ToastUI.Show(ToastKey)` 정적 진입점. Queue 방식. DontDestroyOnLoad 독립 Canvas
- **씬 루트(부모 없음)에 배치** ([UI] Canvas 자식이면 씬 전환 파괴). SetActive(false) 금지(CanvasGroup.alpha=0)
- OnGameStarted/OnGameEnd 구독 자동 정리. ClearAll/FinishCurrent에서 SetActive 금지(루트 비활성 시 Update 정지)
- ToastKey는 Application/Events에 위치(2026-05-20 이동). ToastMessageConfig가 message/duration 보유
- **같은 키 중복 누적 방지(2026-09-08 수정, 실기 미검증)** — `Enqueue`가 ① 표시 중인 키와 같으면
  큐에 넣지 않고 `RestartCurrentDisplay()`(페이드 트윈 Kill → 알파 1 복구 → `_remainingDuration = _currentDuration`),
  ② `_queue.Contains(key)`면 무시, ③ 다른 키만 큐에 넣는다. 종전에는 무조건 `_queue.Enqueue`라
  버튼 5연타 = 같은 문구 5회 연속 노출이었다(빗금 타일뿐 아니라 골드 부족·인구 초과 등 **전 토스트 공통**).
  - `_currentKey`/`_currentDuration` 필드 신설. **duration 조회(`_config.TryGet`)는 `TryShowNext` 한 곳뿐** —
    되돌릴 때는 `_currentDuration` 재사용(조회 중복 금지).
  - 🔴 페이드아웃 중 재요청 시 **시간만 되돌리면 흐릿한 채로 남거나 트윈 완료로 사라진다.**
    `Kill()`은 기본 `complete:false`라 `OnComplete(FinishCurrent)`를 부르지 않는다 → Kill 후 알파 복구가 정답.
  - 중복 방지 덕분에 큐 길이 ≤ ToastKey 종류 수-1(현재 5) → `Queue<T>.Contains` O(n)로 충분(HashSet 불필요).
  - `ToastMessageConfig.asset` 실측: 6종 전부 `duration = 1`.
  - `GameSystemRules_UI.md` 「생산 패널 UI」 규칙 25(토스트 표시 방식)를 같은 커밋에서 함께 고쳤다 —
    「서로 다른 종류는 큐에 쌓고, 같은 종류는 누적하지 않는다(표시 중이면 시간만 리셋, 대기 중이면 무시)」.
    같은 문서 「생산 패널 UI」 규칙 28의 ToastKey 표도 3종 → 6종으로 채웠다(`ToastMessageConfig.asset` 실측 기준).
- **큐 로직 실행 검증 방법(재사용 가능)** — Presentation이라 Unity 컴파일은 불가하지만,
  `Enqueue`~`ClearAll` 본문을 `sed`로 **그대로 잘라** CanvasGroup/Tween/Time/Config 스텁과 함께 `mcs`로
  빌드하면 프레임 단위 시뮬레이션이 된다(`Text.text` setter를 표시 로그로 삼으면 표시 순서·횟수 측정 가능).
  DOTween 대역은 `Kill()`이 OnComplete를 부르지 않는 동작까지 재현해야 의미가 있다.

---

## 생산 패널(ProductionPopup) 실측 구조 — Game.unity (연구 패널 룩 기준)

- **루트 `ProductionPopup`**: full-stretch RectTransform + `Canvas(overrideSorting=true, sortingOrder=200)` + `GraphicRaycaster` + `AnimatedPanel(SlideFromBottom)` + `CanvasGroup` + `ProductionPanelUI`. (ProductionPanelUI/AnimatedPanel 모두 **루트에 같이** 부착)
- **프레임 `ProductionPanel`(루트의 유일한 자식)**: 앵커 (0,0)-(1,0.5) pivot(0.5,0) = **하단 절반·전체 너비**. Image sprite guid `c043043e4ea60bb4fb7a7fb7d7121c5e`(Simple).
- **헤더 `_headerText`**: Bold SDF(guid `96af9a121e352e245859ce1ae3a13b2b`).
- **닫기 `_cancelButton`**: **아이콘 스프라이트** guid `f5dbb98a85baad04eab27646d18ebdcc`(텍스트 라벨 없음), 우상단 앵커 (0.883,0.852)-(0.993,0.97) pivot(1,1).
- **유닛 버튼 Image**: 슬롯/버튼 프레임 sprite guid `704bb204bdd807f4abaa769a332ca9e4`(연구 버튼·행 배경 재사용 후보). 큐 슬롯 이미지는 sprite 없음.
- 폰트 GUID: Light SDF `58c71976882d99940aedcaa81b1248c5`, Bold SDF `96af9a121e352e245859ce1ae3a13b2b`. UIColorConfig `ce7db35dba9189c4e9d9c510f0a3bbce`(= Resources/Config/UIColorConfig.asset).

## 연구 패널 재구성 — 에디터 하베스트 패턴 (셋업 스크립트는 제거됨, 2026-07-27 이력)

- **접근(이력)**: 생산 패널 프리팹이 없다(씬 오브젝트). 그래서 셋업 에디터 스크립트가 에디터 실행 시 씬의 `ProductionPanelUI`를 리플렉션으로 읽어 배경 sprite/폰트/닫기아이콘/버튼 sprite/색상/Canvas SO/하단절반 앵커를 **라이브 하베스트**해 연구 패널에 적용했음. GUID 하드코딩 없음 → 블라인드/멱등 안전. 셋업 완료 후 스크립트는 역할 종료로 제거됨.
- `HarvestProductionStyle()` → `ProductionStyle` struct. `GetPrivateField(obj, "_headerText"/"_cancelButton"/"_unitButtons"/"_unitCostTexts")` 리플렉션(BaseType까지 탐색), `FirstUnityObject<T>(IList)`로 리스트 첫 요소.
- **레이어 버그 수정**: 구 연구 패널은 Canvas 오버라이드가 없어 BlockingOverlay(SO=100) 아래에 그려짐 → 루트에 `Canvas(overrideSorting, SO=200)` + `GraphicRaycaster` 추가.
- **멱등 이관**: 구조 재구성 시 루트 VLG/ContentSizeFitter 제거, 구 `HeaderRow`/`PlaceholderNote` 자식 삭제, `TrackContainer`는 재사용. 에셋 미발견 시 폴백 + 경고 로그(추정 배선 금지 규칙).
- ResearchPanelUI/ResearchTrackListView/ResearchTrackRowView 런타임 코드는 **미변경**(회귀 없음). 에디터 스크립트만 수정.
- ⚠️ 연구 패널은 여전히 CanvasGroup 즉시 alpha 방식(AnimatedPanel 슬라이드 애니메이션은 미적용 — 필요 시 후속).

## 건물 패널 UI 골격 — 씬 실측 (2026-08-10 확인 / 2026-08-21 복구)

> 2026-08-17 `675203ae` 로 `MEMORY.md` 에서 소실됐던 블록. 아래 중 **`Row0/Row1/Row2` 부모 구조는
> 다른 문서 어디에도 없는 유일본**이라 여기로 옮겨 보존한다.
> (`_allSlotButtons` 9개·index 5=철거 / `CostContainer` alpha=0 은 `WORK_HISTORY.md` 에도 있다.)

- `BuildingActionPanel` = 3×3 그리드 9슬롯 원본. `_allSlotButtons` 9개, **index 5 = 철거 버튼**
- 슬롯 자식 구조: `Slot1 { IconImage, CostContainer { GoldIcon, CostText } }`,
  **부모는 `Row0` / `Row1` / `Row2`(각각 HorizontalLayoutGroup)** ← 유일본
- 패널은 오버라이드 Canvas SortingOrder **200**(`GameSystemRules_CanvasSortingOrder.md`) — 복제하면 자동 충족
- 미사용 슬롯은 **`CanvasGroup.alpha=0`**, `SetActive(false)` 금지(GridLayout 정렬 붕괴)

### 자동모드 테두리 회전 머티리얼 — 공유 시 값이 달라지는 함정 (유일본)

- 에셋: `Assets/_Project/Materials/UI/mat_ui_rotatingborder.mat`
  (셰이더 `Shaders/UI/RotatingBorderUI.shader`)
- **에셋에 저장된 값은 `_Speed` 5 / `_Thickness` 0.05 뿐이다.**
- ⚠️ **`_Radius` · `_Inset` 은 `ProductionPanelUI` 가 런타임 인스턴싱으로만 덮어쓴다.**
  → 다른 패널이 이 머티리얼을 그대로 공유하면 두 값이 에셋 기본값으로 남아 **패널마다 테두리가 다르게 보인다.**
  새 패널에 붙일 때는 값을 런타임에 직접 세팅하거나 전용 머티리얼을 만든다.

---

## 씬 YAML 점검

- MonoBehaviour SerializeField 미연결: `{fileID: 0}`
- AnimatedPanel m_IsActive: MonoBehaviour(114) GUID 매칭 → m_GameObject fileID → 해당 GO body m_IsActive
- 폰트: Maplestory Light/Bold SDF (LiberationSans SDF 금지 — Rule 6)
- Canvas Scaler 1080×1920 ScaleWithScreenSize (Rule 1)

---

## Result screen — reflecting the opponent's leave, step 7 (2026-09-21)

Rule source: `GameSystemRules_UI.md` 「공통 UI 규칙」 **D-1**(timer wording) · **D-2**(rematch button off) ·
**D-3**(lobby button never off) · **D-4**(restart the countdown at **30s**). The verdict itself is steps 5·6 →
[network-infra.md](network-infra.md).

| File | What |
|---|---|
| `Presentation/UI/GameEndUI.cs` | one subscription to `GameEvents.OnNetworkOpponentLeft` → `OnOpponentLeft()`; `CountdownCoroutine` now takes `float totalSeconds`; leave-only entry `RestartCountdownForOpponentLeft()` |
| `Presentation/UI/NetworkStatusUI.cs` | suppresses the `DisconnectPanel` popup while the result screen is up |

- 🔴 **The leave wording lives in exactly one place**, `private const string OpponentLeftCountdownFormat`,
  and splits from the normal wording by a **single ternary inside `CountdownCoroutine`**. Verification grep is
  `grep -rn "초 뒤 로비로 이동합니다" Assets/_Project/Scripts/` → must be 1 hit (the const).
  ⚠️ **Do not quote that wording in comments** — a comment copy makes that grep unusable
  (this bit in the same session: two comments quoted it and had to be reworded).
- 🔴 **D-4's 30s is a `private const`, not a `[SerializeField]`** — same reason as step 6: a serialized field
  is written into `Game.unity` and the scene value then beats the code default (the `_autoReturnSeconds`
  30→60 case needed both places). ✅ Consequence: **step 7 needs no scene edit.**
- 🔴 **The 30s restart must not share an entry point with 규칙 M-3's restart** (that one restarts at the
  **full** length and is still unimplemented). Shape used: `CountdownCoroutine(float totalSeconds)` is the
  shared body, the two normal starts pass `_autoReturnSeconds`, and **only** `RestartCountdownForOpponentLeft()`
  passes 30s. M-3 will need its own entry method.
- **D-3 held by doing nothing**: `_backToLobbyButton` is never set to `false` anywhere in the repo; the only
  assignment left is `= true` in `RestoreRematchButton()`.
- **`RestoreRematchButton()` now refuses to re-enable the rematch button once `_opponentLeft` is set.**
  It is called by the rematch-declined *and* rematch-map-failed channels, either of which can arrive **after**
  the leave notification and would otherwise re-enable a button D-2 just turned off. The lobby-button line in
  the same method is untouched (D-3).
- `_opponentLeft` is reset in `Initialize()` (called per `LoadMap()`), so a rematch never starts on the
  previous match's wording.

### 🔴 Why the 「연결이 끊겼습니다」 popup is suppressed, and how (D-1)

- **The collision**: the leave verdict closes the connection (규칙 17), that shutdown reaches
  `NetworkGameManager.HandleClientDisconnected` (which `BackToLobby`/`DisconnectAsync` never unsubscribe, so it
  fires on *intentional* exits too), which raises `OnServerDisconnected`, which `NetworkStatusUI` turns into the
  `DisconnectPanel` popup — **on top of the result screen, covering the D-1 wording.**
- **Chosen fix**: `NetworkStatusUI` subscribes to `GameEvents.OnGameEnd` and sets `_resultScreenShown`;
  `OnServerDisconnected` returns early (with one dev-axis log line) while that flag is true.
- **Why keyed on 「the result screen is up」 rather than 「an opponent-left signal arrived」**: the flag is set
  when the result screen appears, i.e. **before** any disconnect can arrive, so the suppression cannot lose a
  race. Keying it on `OnNetworkOpponentLeft` would depend on that RPC/event landing before the transport
  callback — an ordering nobody can prove from code, and the normal-exit branch sends its RPC in the same frame
  as `Shutdown()`. 🔴 It also covers the branch where the *notification* never arrives at all.
- **Why in `NetworkStatusUI` and not in Infrastructure**: `OnServerDisconnected` is also the in-match path, and
  the owner of "which screen is showing" is Presentation. Suppressing at the publisher would kill both.
- ✅ **Untouched path**: a real in-match disconnect (before game end) still shows the popup — `_resultScreenShown`
  can only be true after `OnGameEnd`. `ForceWin` on a client whose link dropped also still shows it (that client
  never received `AnnounceWinnerClientRpc`, so no `OnGameEnd`).
- ⚠️ **Consequence to keep in mind**: after game end, *any* disconnect is now silent on that screen, including
  「my own internet died and the watchdog therefore refuses to judge」. The user is not trapped — D-3 keeps the
  lobby button on and D-4 auto-returns — but the only sign is the log line.
- ⚠️ **Unverified (no Unity here)**: compilation and every runtime behaviour, including whether the popup
  actually used to overlap (it is inferred from the code path, not observed).

> **[🔴 2026-09-22 correction — nothing above is deleted (`.claude/MEMORY.md` B-7). The answer to that last
> bullet came back **no**.]**
>
> 🔴 **`NetworkStatusUI` is not placed in any scene or prefab, so that popup never appeared at all.**
> Measured: the guid in `Presentation/UI/NetworkStatusUI.cs.meta` (`adc83ceead3e36246ba137d2ceede0a6`) has
> **0 hits** across every `*.unity` and `*.prefab` under `Assets`, and the string `NetworkStatusUI` — including
> `Start()`'s *"네트워크 상태 모니터링 시작"* — has **0 hits** in both 2026-09-22 logs (editor and device).
> The file header says *"씬 구조 (Inspector에서 수동 배치)"*, which is a **precondition, not a fact**.
> - **What this falsifies**: only the sentence *"…covering the D-1 wording"* above, read as **present tense**.
>   The collision is **not happening now** because the popup does not exist. The **ping (RTT) readout does not
>   run either**, and `DisconnectPanel` has no instance.
> - ⚠️ **The guard stays. This is not a reason to remove code.** Place `NetworkStatusUI` later and the overlap
>   becomes real, so the guard is valid prevention. **Nothing in the code was touched in the round that found
>   this** (docs only).
> - 🔴 **The *"✅ Untouched path"* bullet above is likewise true only about the code path** — an in-match
>   disconnect cannot show a popup that is in no scene. 2026-09-22 field test: the device (**Host**) was killed
>   mid-match and the remaining editor (**Client**) showed **no popup and no verdict**.
> - 🔴 **The lesson, not the fix, is the point**: before describing what a scene-placed component does, check
>   its placement by guid. `.claude/mistakes.md` **2026-09-22** is that entry.
> - **Single source for the fact and for the step-7 diagnosis correction**:
>   `Assets/_Project/Docs/TechnicalDesignDocument.md` 「결과 화면 이탈 판정·통보 구조」, the 2026-09-22 block.
>   The work item is the 「`NetworkStatusUI` 미배치」 row in `Assets/_Project/Docs/ROADMAP.md`;
>   🔴 **how to handle it (place it? is the popup needed at all?) is undecided** — do not read the note in that
>   row about the result screen carrying the notice instead as a decision.


## Result screen — step 8, rule D-6 (2026-09-22): the first real `ShowAlert` call site

Rule source: `GameSystemRules_UI.md` **D-6** (with **D-5** for the popup, **D-4** for the 30s countdown,
**8 · 9** for modal). Plan row: `_Tasks/2026-09-16/06_27_post-game-leave-ui/Plan.md` §2, step **8**.

| File | What |
|---|---|
| `Presentation/UI/GameEndUI.cs` | 3 new `const` strings (title/message/button label) · non-serialized field `_rematchRequestPopup` · `HandleUnansweredRematchRequestOnOpponentLeft()` called at the end of the existing `OnOpponentLeft()` |
| `Presentation/UI/Common/RematchRequestPopup.cs` | state flag `_requestShowing` + `public bool TryCloseUnansweredRequest()` |

- 🔴 **The rematch request popup is `RematchRequestPopup`, and it IS placed** — unlike `NetworkStatusUI`.
  Measured: guid `d26ab2269c84ef641957a86f95840bd0` (from `RematchRequestPopup.cs.meta`) has **1 hit**, in
  `Assets/_Project/Scenes/Game.unity`, on GameObject `[UI]/RematchRequestPopup`, `m_IsActive: 1`, parent
  `[UI]` active, own Canvas `m_OverrideSorting: 1` / `m_SortingOrder: 250`, and **all 5 serialized fields
  are non-zero**. That is why `FindFirstObjectByType<RematchRequestPopup>()` is a safe resolution here
  (checking placement first is the `.claude/mistakes.md` 2026-09-22 lesson).
- 🔴 **D-6 is conditional and the code must be too.** The rule's first sentence limits it to *"the request
  popup is up and has not been answered yet"*. D-1 separately says an opponent-leave must **not** open a new
  popup (the timer text carries it). So `TryCloseUnansweredRequest()` returns **false when nothing was up**
  and `GameEndUI` then returns before `ShowAlert` — an unconditional alert would break D-1.
  Alpha cannot be used for that test (mid-fade it is neither 0 nor 1) → explicit bool flag, set in both
  `ShowRequest` overloads, cleared in `Hide()` (all close paths pass through it) and `ShowDeclined()`.
- **One subscription, extended — not a second one.** `GameEvents.OnNetworkOpponentLeft` still has exactly
  **1** subscription in `GameEndUI` (`grep -c` = 1). Two subscriptions to the same signal would leave the
  order of "close the popup" vs "open the alert" unprovable.
- **No new timer** (D-6 clause 3). The 30s is step 7's `RestartCountdownForOpponentLeft()`, called one line
  earlier in the same handler. The alert never self-closes; the button or D-4's expiry ends the screen.
- **The "로비로" button reuses `OnBackToLobbyClicked`** — the exact handler the lobby button installs — so
  countdown stop, `timeScale = 1`, loading indicator and the multiplayer NGO shutdown delegation cannot
  drift apart between the two buttons. `ConfirmPopup.OnConfirmClicked()` calls `Hide()` **before** the
  callback, so the overlay ref-count is released before the scene change.
- **Overlay ref-count across the handoff is safe**: `RematchRequestPopup.Hide()` takes it 1→0 and
  `ShowAlert` 0→1 in the same frame (no render in between), so rule order (close, then alert) costs nothing.
- ✅ **Prefab preconditions verified this time** (they are what D-5 depends on): `ConfirmPopup.prefab` has all
  **7** serialized fields non-zero including `_titleText`, Panel has a VerticalLayoutGroup with
  `m_ChildForceExpandHeight: 0` / spacing 38, ButtonRow HLG spacing 50. `UIManager._confirmPopup` is wired in
  `Login.unity`. `UIAnimator` sets `SetUpdate(true)` on every sequence, so the alert animates under
  `Time.timeScale = 0`.
- ✅ **No scene work**: the three D-6 strings are `const` (not `[SerializeField]`, which the scene would
  override) and the popup reference is resolved at runtime instead of wired.

### 🔴 Open defect found while doing this — NOT fixed, user decision pending

`ConfirmPopup` lives under `UIManager` (DontDestroyOnLoad; its only placement is `Login.unity`), while
`GameEndUI` and `RematchRequestPopup` die with the Game scene. So if the user **ignores** the alert and D-4's
30s expires, `ReturnToLobby()` changes scene **with the alert still open**: the Lobby comes up covered by the
alert plus the blocking overlay (ref-count stuck at 1), and pressing the leftover button then invokes
`OnBackToLobbyClicked` on a **destroyed** `GameEndUI` (`StopCoroutine` / `_panel.Hide()` on a destroyed
object). It self-recovers — `ConfirmPopup.Hide()` runs before the callback, so the overlay does clear — but it
leaves a stale popup and one exception.
🔴 **There is no `HideAlert`/`HideConfirm` on `IUIManager`/`UIManager`**, so `GameEndUI` cannot close it, and
adding one touches the shared API that Plan step 1 deliberately isolated. **Same trap will apply to step 10
(M-3 · M-4).** Options (not chosen): add `HideAlert()` to `IUIManager` + `UIManager` and call it from
`ReturnToLobby()`; or have `ConfirmPopup` close itself on scene unload (changes behaviour for every popup).

---

## 결과 화면 「재경기 준비 중」 상태 — 두 쪽이 서로 다른 신호로 들어간다 (2026-09-22)

`GameEndUI`. The status line is **one place only**: `_countdownText`. New state text const
`RematchPreparingStatusText = "재경기 준비 중..."`.

### 🔴 The rule that decided the whole design

> **「사람을 기다리면 타이머가 돈다. 시스템을 기다리면 타이머가 멈춘다.」**

*Requested* (waiting for a **person** to accept) → the 60s auto-return countdown **keeps running**; if the
opponent leaves the popup untouched the answer never comes, so the user must not be trapped.
*Accepted* (waiting for the **server** to build a map) → countdown **stops**, because that work terminates.

### 🔴 Entry paths differ by side — this is the actual fix

- **Accepter**: subscribes to `GameEvents.OnLocalRematchAccepted` — **its own button press**, not a server
  reply. Waiting for a reply would mean *nothing happens for a Client whose Host is already gone* (the
  ServerRpc evaporates) while a Host would proceed — i.e. the screen would differ by role.
  Note this subscription runs **alongside** the controller's on the same channel (see `network-infra.md`
  for why the controller's side is now try/catch-wrapped).
- **Requester**: subscribes to the new `GameEvents.OnNetworkRematchAccepted` (server notice). Without it the
  requester never learns acceptance happened and its own 60s expiry would drag it to the lobby mid-preparation.
- `EnterRematchPreparingState()` is **idempotent** (`_rematchPreparing`) because the accepter receives the
  broadcast notice too, and also returns early when `_opponentLeft` is already true.

### The limit clock — 🔴 it judges nothing

`RematchPreparingLimitCoroutine` only **restores the status line** (by restarting the countdown at full
length, rule M-3). It decides no win/loss, no leave, no disconnect. Do not bolt judgement onto it — this
screen already has two judging clocks (auto-return countdown, result-screen leave watch).

- Length is **computed, never literal**:
  `NetworkMapTransfer.TransferTimeoutSeconds * (NetworkMapTransfer.MaxResendCount + 1)`.
  One window = `TransferTimeoutSeconds`; window count = initial send 1 + `MaxResendCount` resends.
  🔴 **A single window is wrong** — one lost packet triggers a resend, the limit would expire on the
  *normal* path, the status line would revert and then the scene would suddenly reload. A false signal.
  🔴 There is **no digit `10` or `20` anywhere in `GameEndUI.cs`** (verified by grep) so a future change to
  rule 16's resend count follows automatically.
- **Stop sites — four, all wired** (a missed one leaves a dead clock):
  `OnRematchMapFailed()` · the `OnNetworkRematchStarting` subscription · `OnOpponentLeft()` · `OnDestroy()`.
  Plus a fifth defensive call in `Initialize()`.
- 🔴 **Self-stop hazard**: the coroutine nulls `_rematchPreparingCoroutine` **before** doing its work, or
  `StopRematchPreparingLimit()` reached from the restart path would `StopCoroutine(itself)` and cut the
  method off halfway.
- `RestartCountdownFromFullLength()` guards on `_opponentLeft` — otherwise a late map-failure notice would
  overwrite rule D-4's 30s leave countdown with 60s.

### 🔴 Rejected on purpose — do not "improve" these later

- **`ShowLoading` during preparation.** `LoadingScreen.Show()` sets `blocksRaycasts = true`, which would make
  the 「로비로」 button unpressable — a direct rule D-3 violation. Only the status line changes.
  (The pre-existing loading on `OnNetworkRematchStarting` stays untouched; its literal string happens to read
  the same as the new const, and they were **not** merged because that line was out of scope.)
- Blocking the buttons outright (adds another clock) and a separate failure popup (rule D-1 forbids it).

### `RestoreRematchButton()` — label restored outside the guard (behaviour change)

The label assignment used to sit **inside** `if (_restartButton != null && !_opponentLeft)`, so on a leave
judgement the text stayed frozen at 「요청 중...」. Now: label → 「다시하기」 **unconditionally**;
`interactable = !_opponentLeft` keeps the D-2 guard. `OnOpponentLeft()` calls this method (after setting
`_opponentLeft = true`, order matters) instead of poking `interactable` directly.
✅ `_backToLobbyButton.interactable = false` remains **0 occurrences repo-wide** (rule D-3).

⚠️ **Unverified**: compilation and runtime. Static checks only — brace balance 36/36 after stripping
comments/strings; no scene or prefab was touched (every new value is a `const` or a referenced constant).

### 재경기 실패 — 3경로를 상태 줄 문구 하나로 묶었다 (2026-09-22, 사용자 확정)

🔴 **재경기가 안 되는 길은 셋인데 사용자는 구분할 수 없고 구분할 필요도 없다.** 전부 「재경기가 안 됐다」다.
So all three converge on **one** entry point, `EnterRematchFailedState()` — **never copy the handling into the
three sites.** Call sites: 2 (the third path shares a handler).

| path | how it arrives | handler |
|---|---|---|
| ① couldn't send the accept at all | controller publishes `OnNetworkRematchMapFailed` **locally** from its catch | `OnRematchMapFailed()` |
| ② server says map prep failed | same channel, via ClientRpc (original use) | `OnRematchMapFailed()` |
| ③ prep limit expired with no notice | — | `RematchPreparingLimitCoroutine()` |

- Text: `RematchFailedCountdownFormat = "재경기를 시작할 수 없습니다. {0}초 후 로비로 돌아갑니다."` —
  a **format string in one const**, same shape as `OpponentLeftCountdownFormat`.
- `EnterRematchFailedState()` does four things, **in this order**: stop the prep limit → set `_rematchFailed`
  → `RestoreRematchButton()` → `RestartCountdownFromFullLength()` (rule M-3, full length).
  🔴 **The flag must be set *before* the countdown restarts** — the coroutine reads it every second, so
  setting it after makes the first second show the normal text and then flip (visible flicker).
- 🔴 **`CountdownCoroutine`'s branch is now 3-way and the priority is 이탈 > 실패 > 평시.**
  The two can hold at once: the prep limit expires first, then the leave judgement (30s) lands. The screen
  must keep the **later, more fundamental** fact, because 「상대가 떠났다」 is the *cause* of
  「재경기가 안 됐다」 — showing the cause beats showing the effect, and the reverse order would destroy the
  only chance to tell the user the opponent left.
- **`_rematchFailed` is cleared in three places** (a stale flag makes the screen keep claiming failure while a
  retry is already running): the `SetupRematchButton` onClick, `EnterRematchPreparingState()`, `Initialize()`.
- 🔴 **No popup.** The user chose "status line, and amend rule M-3" over M-3's 2026-09-16 決 of
  「알림 팝업으로 알린다」. A popup covers/blocks the 「로비로」 button → rule D-3 violation. **`ShowAlert` was
  not added** (the only `ShowAlert` call in the file remains step 8's D-6 opponent-left alert).
  The reason is written next to the const so nobody puts a popup back.
- Buttons unchanged in behaviour: `RestoreRematchButton()` restores the label and leaves `interactable`
  false when `_opponentLeft`; `_backToLobbyButton.interactable = false` stays **0 occurrences**.

### 재경기 실패 문구를 사유에 따라 가른다 — 단계 9 (2026-09-24, 규칙 18)

앞 절(2026-09-22)이 **실패 3경로를 문구 하나로 묶었다.** 여기서는 그 **「실패」 분기 안쪽이 둘로 갈린다.**
🔴 **우선순위는 그대로 「이탈 > 실패 > 평시」** — 바뀐 것은 **실패 안쪽**뿐이다.

| 사유 | 상수 | 문구 |
|---|---|---|
| `OpponentDisconnected` | `RematchFailedByOpponentLeftCountdownFormat`(신설) | 「상대방이 나가서 재경기를 시작할 수 없습니다. {0}초 후 …」 |
| 그 밖(모르는 경우 포함) | `RematchFailedCountdownFormat`(그대로) | 「재경기를 시작할 수 없습니다. {0}초 후 …」 |

- 🔴 **문구 분기는 여전히 한 자리다** — `CountdownCoroutine` 안의 **삼항 3분기 그대로**이고, 실패 쪽 항만
  `string.Format(SelectRematchFailedFormat(), seconds)` 로 바뀌었다. 🔴 **`SelectRematchFailedFormat()` 은
  형식 문자열을 고르기만 하고 화면에 쓰지 않는다** — 분기를 다른 메서드로 흩지 않기 위한 형태다.
  (본문에 `if` 를 늘어놓으면 「문구를 쓰는 자리가 한 곳」이라는 성질이 깨진다.)
- **사유는 `EnterRematchFailedState(RematchMapFailureCause cause)` 가 받아 `_rematchFailureCause` 에 보관한다.**
  🔴 **사유를 깃발(`_rematchFailed`)보다 먼저 대입한다** — 코루틴이 매 초 둘을 함께 읽으므로 순서가 뒤집히면
  첫 1초에 잘못된 문구가 보인다(깃발이 카운트다운 재시작보다 앞이어야 하는 것과 같은 이유의 순서).
- **실패 3경로가 모두 사유를 넘긴다**: ① 수락 전송 실패 → `Unknown`(컨트롤러가 로컬 발행) ·
  ② 서버 통보 → 서버가 판정한 값 · ③ 한도 만료 → `Unknown`.
  🔴 **①③ 이 `Unknown` 인 근거는 「모른다」이지 「연결 끊김이 아니다」가 아니다** — 상대가 나갔을 수도,
  내 회선일 수도 있다. 단정하면 거짓이 될 수 있어 중립 문구를 쓴다(`CLAUDE.md` 규칙 10). 코드 주석에 남겼다.
- **`_rematchFailureCause` 를 되돌리는 자리는 `_rematchFailed` 와 완전히 같은 3곳**
  (`SetupRematchButton` onClick · `EnterRematchPreparingState` · `Initialize`). 같은 줄 바로 아래에 붙여
  한쪽만 빠뜨릴 수 없게 했다.
- **구독은 늘지 않았다** — `GameEvents.OnNetworkRematchMapFailed` 의 타입만 payload 로 바뀌었고
  구독은 여전히 `GameEndUI` **1곳**, 발행은 **2곳**(자세한 내용은 [network-infra.md](network-infra.md) 같은 날짜 절).
- 🔴 **문구를 주석에 베껴 적지 않았다** — 단계 7 에서 겪은 그대로다(주석 사본 하나가 「문구가 한 곳에만 있는가」
  grep 을 무용하게 만든다). 주석에서는 문구 대신 **상수 이름**을 가리키고, 뜻을 말해야 할 때는 규칙 18 의 표현
  (「상대가 나갔다」)을 쓴다 — **리터럴의 부분 문자열이 되지 않는 낱말이어야 한다.**
  검증 grep: `grep -rn '상대방이 나가서' Assets/_Project/Scripts Assets/Editor` → **1건(상수뿐)**.
- ✅ **`ShowAlert` 실호출은 늘지 않았다**(규칙 D-6 이탈 알림 1건 그대로 — 규칙 M-3 은 상태 줄이다) ·
  `_backToLobbyButton.interactable = false` **0건 유지**(규칙 D-3) · **씬·프리팹 0건**(새 값은 전부 `const`).
- ⚠️ **미검증**: 컴파일(Unity 없음)과 런타임 전부. 확인한 것은 중괄호 균형과 `mcs` 파싱(구문 오류 0)뿐이다.

### 🔴 `GameEndUI` 재경기 상태 진단 로그 — 조기 반환의 침묵을 없앤 자리 (2026-09-27, 동작 무변경)

**왜 넣었는가 (이 절의 존재 이유가 곧 교훈이다).** 실기에서 **수락 접수 통보(`OnNetworkRematchAccepted`)는
양쪽에 도착했는데 한쪽(Client)만 상태 줄·타이머가 반응하지 않는** 현상이 나왔다. 그런데
**컨트롤러의 「발행했다」 로그까지만 있고 `GameEndUI` 가 그것을 받아 무엇을 했는지는 0줄**이라,
① 핸들러가 불렸는가 ② 가드에 막혔는가 ③ 어느 가드인가를 **하나도 확인할 수 없었다.**
`EnterRematchPreparingState()` 의 조기 반환 두 개(`_rematchPreparing` · `_opponentLeft`)가
**둘 다 조용히 반환**하기 때문이다.

🔴 **재사용할 교훈: 「조용한 조기 반환」은 그 자체로 진단 불가 상태를 만든다.** 상태 기계의 가드가
화면 전이를 막는 자리라면, 막았다는 사실과 **어느 가드였는지**가 로그에 남아야 한다.

- **로그 자리 11곳 / 전부 `GameLog.Dev`(개발 축) · 새 `LogEvent` 키 0개** —
  `EnterRematchPreparingState`(정상 진입 Info 1 + 가드 Warn 2) ·
  `StopRematchPreparingLimit`(Info 1, **돌고 있었을 때만**) ·
  `RematchPreparingLimitCoroutine`(한도 만료 Warn 1) ·
  `OnRematchMapFailed`(Warn 1) · `EnterRematchFailedState`(Warn 1) ·
  `RestartCountdownFromFullLength`(재시작 Info 1 + 가드 Warn 1) ·
  `OnOpponentLeft`(진입 Info 1 + 중복 가드 Warn 1).
- 🔴 **레벨 규약: 정상 전이 = `Info` / 가드에 막힘·실패 전이 = `Warn`.** 근거는 「로그를 읽는 사람은
  `[WARN]` 만 훑는다」 — 막힌 자리를 Info 로 섞으면 이번처럼 **정상 줄 사이에 묻힌다.**
- 🔴 **역할 표시는 `NetworkContext.IsNetworkServer`** (Application 정적 홀더). Presentation 이 `NetworkManager`
  를 직접 보지 않고도 역할을 얻는 유일한 수단이고, **새로 만들 필요가 없었다**(이 파일이 이미 같은 홀더의
  `IsNetworkActive` 를 읽는다). ⚠️ **그 홀더는 디스폰 시 `Reset()` 으로 둘 다 `False` 가 되므로
  `IsServer=False` 만으로는 「Client 였다」와 「네트워크가 이미 내려갔다」를 구분할 수 없다** →
  `NetworkActive=` 를 **함께** 싣는다. 함께 싣는 깃발 3개는 `RematchPreparing=` `OpponentLeft=` `RematchFailed=`.
- **필드 조립은 `[Conditional]` 두 개가 붙은 `void` 헬퍼 3개**(`LogRematchDiagInfo` / `...Warn` /
  `LogRematchLimitStopIfRunning`) 안에서만 일어난다 → 릴리스에서 **문자열 조립까지** 사라진다(LogRules 1.7).
  🔴 **`LogRematchLimitStopIfRunning` 이 조건 판별을 헬퍼 *안*에서 하는 이유**: `[Conditional]` 은
  **호출문 전체(인자 계산 포함)** 를 지우므로, 판별을 안에 두면 릴리스에 **조건식조차 남지 않는다.**
  호출부에 `if` 를 쓰면 그 조건식은 남는다. (방어 호출이 5자리라 무조건 찍으면 잡음이 된다.)
- ⚠️ **이 프로젝트 관례 「가드에는 로그를 넣지 않는다」의 진짜 적용 범위** — 그것은 **매 틱 도달하는 가드**
  (서버 전투 틱 · RPC 발신 가드)를 두고 한 말이고 근거는 LogRules 1.14 금지 8(매 틱 로깅 금지)이다.
  여기 가드들은 **한 경기에 많아도 두세 번** 도달하므로 그 금지에 걸리지 않는다. **관례를 기계적으로
  적용해 이번 침묵을 그대로 두는 것이 오히려 규칙의 목적(전이 시점 관측)을 배반한다.**
- 🔴 **메서드 시그니처를 하나도 바꾸지 않았다.** `EnterRematchFailedState(cause)` 에 진단용 `origin` 인자를
  더하지 않고, **호출부 두 곳이 각자 `Origin=` 을 먼저 남기는** 방식으로 「어디서 불렸는지」를 가른다
  (`Origin=MapFailedNotice` / `Origin=PreparingLimitExpired`). 호출부가 둘뿐이라 성립한다.
- ⚠️ **한도 초를 진입 줄에 실으면서 `TransferTimeoutSeconds * (MaxResendCount + 1)` 식이 두 자리가 됐다**
  (코루틴 + 진입 로그). 숫자를 베껴 쓰지 않는다는 규약은 지켰지만 **식이 복제된 것은 사실**이므로,
  한도 만료 줄에 **실제로 기다린 초(`WaitSeconds=`)** 를 따로 남겨 두 값을 대조해 검산할 수 있게 했다.
- **한 경기당 줄 수(실측 아님 — 코드 경로 계수)**: 재경기 성공 1회 = **2~3줄**(수락한 쪽 3 · 요청한 쪽 2,
  수락자는 버튼 입력과 서버 통보로 두 번 들어와 `Guard=AlreadyPreparing` 1줄이 더 찍힌다) ·
  재경기 실패 = **4~5줄** · 상대 이탈 = **1~2줄**. 이탈 통보 발행은 정상 퇴장 1 + 자체 감시 1(후자는
  `_resultScreenLeaveJudged` 로 가드)이라 **중복 가드 줄은 최대 1줄**이다.
- ⚠️ **미검증**: Unity 컴파일·런타임 전부. 확인한 것은 ① 중괄호 균형(주석·문자열 제거 후 46/46) ②
  `mcs -langversion:latest` 파싱에서 **구문 오류 0**(남은 28건 전부 `CS0246`/`CS0234` 외부 참조 누락) ·
  `[Conditional]` 의 void 반환 요건 위반(`CS0578`) **0건**이다. 🔴 **타입 해석이 실패했으므로
  멤버 단위 검사(메서드 이름 오타 등)는 이 방법으로 확인되지 않는다.**

### 🔴 자동 복귀 카운트다운이 만료되지 않는다 — 진단 로그 4자리 (2026-09-27 2차, 동작 무변경)

**무엇을 좁히려고 넣었는가.** 실기 로그에서 `RestartCountdownFromFullLength` 의 재시작 줄
(`TotalSeconds=60`)은 정상인데 **60초 뒤 `ReturnToLobby()` 의 흔적이 하나도 없었다**
(`Heartbeat 정지` · `정상 퇴장 통보` · `Shutdown 완료` 전부 0줄). 즉 **「StartCoroutine 을 불렀다」와
「코루틴이 끝까지 돌아 복귀까지 갔다」 사이가 통째로 관측 불가**였다.

| 자리(메서드) | 가르는 것 |
|---|---|
| `CountdownCoroutine` 첫 줄(첫 `yield` 전) | 「불렀다」 vs **「본문이 실제로 돌기 시작했다」** + `TotalSeconds=` |
| `CountdownCoroutine` 루프 탈출 직후(`ReturnToLobby()` 앞) | **「끝까지 돌았다」 vs 「중간에 끊겼다」** + `TotalSeconds=` |
| `StopCountdown()` 첫 줄 | **「누가 끊었는가」** — `StoppedBy=`(호출부 7곳 중 어디) |
| `ReturnToLobby()` 첫 줄 | 「만료는 했는데 **복귀 메서드에 들어왔는가**」 |

- 레벨은 **넷 다 `Info`** — 전부 정상 흐름에도 도달하는 자리라 `Warn` 은 과하다
  (직전 회차의 「정상 전이 Info / 가드에 막힘 Warn」 규약과 어긋나지 않는다: 여기엔 가드가 없다).
- 🔴 **호출자 식별은 `[System.Runtime.CompilerServices.CallerMemberName]` 선택적 인자**로 했다 —
  `private void StopCountdown([CallerMemberName] string caller = null)`. **호출부 7곳을 한 글자도
  건드리지 않았다**(컴파일러가 각 호출부에 문자열 리터럴을 심는다). 🔴 **재사용 가치가 큰 수법이다**:
  진단용 인자를 넘기려고 호출부를 고치는 순간 그 자체가 동작 변경 위험이 되는데, 이 방법은 그 위험이 0 이다.
  ⚠️ 단서 — **메서드 그룹 변환이 있으면 쓸 수 없다**(`Action a = StopCountdown;` 은 선택적 인자가 붙으면
  깨진다). 이번엔 `grep -rn StopCountdown` **10건 전부 같은 파일**이고 직접 호출 7 + 선언 1 + 주석 2 라 성립했다.
- 판별(`_countdownCoroutine == null` 이면 0줄)은 **`[Conditional]` 헬퍼 `LogCountdownStopIfRunning` 안**에서
  한다 — 직전 회차 `LogRematchLimitStopIfRunning` 과 **같은 이유·같은 모양**(호출부에 `if` 를 쓰면
  릴리스에 조건식이 남는다).
- **한 경기당 늘어나는 줄 수(코드 경로 계수, 실측 아님)**: 정상 자동 복귀 **4줄**(본문 시작 1 + 만료 1 +
  복귀 1 + 정지 1) · 「로비로」 버튼 **3줄** · 카운트다운 재시작 1회마다 **+2**(정지 1 + 새 본문 1) ·
  재시작 없는 정지(`EnterRematchPreparingState`)는 **+1**. 🔴 **매 초 찍는 줄은 0개.**

#### 🔴 코드를 읽고 짚인 1순위 용의자 — `ReturnToLobby()` 안의 「자기 자신 StopCoroutine」

`CountdownCoroutine` 만료 → `ReturnToLobby()` **첫 줄**이 `StopCountdown()` →
`StopCoroutine(_countdownCoroutine)` 인데 **그 핸들이 지금 실행 중인 바로 그 코루틴**이다.
🔴 **`ReturnToLobby()` 의 실제 일(팝업 닫기 · `timeScale=1` · `Hide()` · `ShowLoading` ·
`BackToLobby()`)이 전부 그 줄 *뒤*에 있다** — 그래서 만약 그 자리에서 실행이 끊기면
**로그로 찾던 증거(Heartbeat·퇴장 통보·Shutdown)가 정확히 전부 사라진다.** 관측된 로그 서명과 일치한다.

⚠️ **같은 파일이 이 함정을 이미 한 번 밟았다** — `RematchPreparingLimitCoroutine` 은
「자기 핸들을 먼저 `null` 로 비운다」는 **방어를 갖고 있고 그 주석이 *「이 코루틴이 그 자리에서 끊겨
뒤 코드가 실행되지 않는다」* 고 단언**한다. `CountdownCoroutine` 에는 **그 방어가 없다.**
🔴 **그 비대칭 자체가 단서다.**

🔴 **단, 「`StopCoroutine(자기)` 가 현재 프레임의 실행을 즉시 끊는가」는 확인되지 않았다**
(Unity 없음, 실측 0). 이 파일의 주석은 「끊긴다」로 적혀 있으나 그것이 실측인지 추정인지 알 수 없다.
**고치지 않고 관측 수단만 넣은 이유가 이것이다** — 다음 실기에서 **「만료 1줄 + 복귀 1줄 +
`StoppedBy=ReturnToLobby` 1줄이 찍히고 그 뒤가 없다」** 면 이 가설이 확정된다.

> **[🔴 2026-09-27 정정 — 이 가설은 실기로 **반증**됐다. 🔴 위 원문은 한 글자도 지우지 않는다(`.claude/MEMORY.md` B-7).
> 이 블록은 document-manager 가 2026-09-27 실기 검증 결과를 문서에 반영하면서 덧붙였다 — **사실 최신화이며 코드에 관해 새로 정한 것은 없다.**]**
>
> 🔴 **「그 뒤 코드가 실행되지 않는다」는 성립하지 않는다.** 위 진단 로그가 들어간 채로 돌린 2026-09-27 실기에서
> **만료 3회 전부, `StopCountdown()` 뒤의 코드가 끝까지 실행됐다**(`_Logs/_editor/2026-09-27/RuntimeLog.txt`, 내가 직접 읽었다).
>
> | 시각 | 로그 |
> |---|---|
> | 17:08:54.981 | 자동 복귀 카운트다운 만료 — 로비로 복귀한다 (`TotalSeconds=60`) |
> | 17:08:54.981 | 로비 복귀 시작 — 카운트다운 정지 + timeScale 복원 + 씬 전환 |
> | 17:08:54.982 | 자동 복귀 카운트다운 정지 (`StoppedBy=ReturnToLobby`) |
> | 17:08:54.983 | Heartbeat 코루틴 정지 |
> | 17:08:54.990 | 정상 퇴장 통보 전송 |
> | 17:08:54.992 | NetworkManager Shutdown 완료 |
>
> - ✅ **`StoppedBy=` 분포** — `ReturnToLobby` **3** · `EnterRematchPreparingState` **3** · `RestartCountdownForOpponentLeft` **2**. **`Guard=` 0건.**
>   **즉 이 회차에 넣은 진단 로그 4자리가 의도한 일을 그대로 했다** — 「불렀다 / 돌기 시작했다 / 끝까지 돌았다 / 누가 끊었는가」가 전부 구별됐다.
> - 🔴 **진짜 원인은 코드가 아니라 테스트 환경이었다** — **강제 실패의 결말 줄이 `[ERROR]` 레벨이라 Unity 의 Error Pause 로 플레이 모드가
>   멈춰 있었다**(사용자 보고). 멈추면 코루틴도 돌지 않는다. ⚠️ **이 사실은 로그에 남지 않는다.**
>   **절차의 단일 소스는 `Assets/_Project/Docs/TechnicalDesignDocument.md` 「개발용 강제 실패 플래그 (에디터 전용)」 절의 2026-09-27 블록**이다.
> - ✅ **그러므로 위 가설을 세운 것과 「고치지 않고 관측 수단만 넣은 것」은 둘 다 옳았다** — 가설을 확정으로 취급해 `StopCountdown()` 호출
>   순서를 바꿨다면 **멀쩡한 코드를 고친 뒤 그것을 「수정」으로 기록했을 것**이다. 위 원문이 스스로 적어 둔 유보
>   (*「`StopCoroutine(자기)` 가 즉시 끊는가는 확인되지 않았다」*)가 이번 결과를 **정정이 아니라 확인**으로 만들었다.
> - ⚠️ **그래서 `CountdownCoroutine` 에 방어를 넣을 근거는 이 관측에서 나오지 않는다** — 위 「비대칭 자체가 단서다」는 **여전히 관찰로서 유효**하지만,
>   🔴 **이 실기는 그 방어가 없어도 뒤 코드가 실행된다는 것을 보여 주었다.** 넣을지 말지는 이 문서가 정하지 않는다.
> - 🔴 **같은 실기에서 별개의 버그 1건이 드러났다 — 재경기 실패 직후에 「재경기 준비 중」 상태로 2차 진입한다**(수락 접수 두 경로 사이에
>   실패 처리가 끼어들어 `_rematchPreparing` 가드가 풀린다. 메인 세션 코드 확정 · **수정 진행 중 · 미검증**).
>   **증상·타임라인·회차 비교의 단일 소스는 `Assets/_Project/Docs/_Tasks/2026-09-16/06_27_post-game-leave-ui/Plan.md` §15-6**,
>   교훈은 `.claude/mistakes.md` 2026-09-27 항목이다.

#### 배제한 가설 — `StartCoroutine` 이 조용히 실패했다

**성립하지 않는다(씬·코드 실측).** ① `Game.unity` 의 `GameEndPanel`(fileID `1309749079`)에
**`GameEndUI` 와 `AnimatedPanel` 이 같은 GameObject 에 붙어 있고** `_panel` 이 그 자신을 가리키며
**`m_IsActive: 1`**, 부모 `SafeAreaContainer` · `[UI]` 도 활성이다. ② `AnimatedPanel` 은 **`SetActive` 를
쓰지 않는다**(공통 UI 규칙 5 — CanvasGroup `alpha`/`blocksRaycasts`/`interactable` 로만 제어).
③ 런타임에 그 오브젝트나 조상을 끄는 코드가 없다(`GameEndPanel`/`SafeAreaContainer` 대상 `SetActive`
0건, `Canvas.enabled=false` 0건). ④ **컴포넌트를 `enabled=false` 로 끄는 것은 코루틴을 멈추지 않는다** —
멈추는 것은 GameObject 비활성화뿐이다.
✅ **덤으로 확인**: `ShowResult(...)` 는 **호출부가 0건**이라 카운트다운을 시작하는 자리는
`OnGameEnd` · 이탈 재시작 · 전체 길이 재시작 **셋뿐**이다(중복 코루틴 가설도 배제된다).

> **[🔴 2026-09-29 정정 — 위 줄은 한 글자도 지우지 않는다(`.claude/MEMORY.md` B-7).]**
> 🔴 **위 줄이 가리키는 그 메서드는 2026-09-29 회차(규칙 M-4)에서 최종 삭제됐다** — 죽은 코드 52줄,
> 삭제 시점 호출부 0건. 즉 **「호출부 0건」이라는 서술은 사실로서 참이지만 가리키는 대상이 코드에 없다.**
> ✅ **결론(「카운트다운을 시작하는 자리는 셋뿐」)은 그대로 유효하다** — 세 자리 전부 살아 있다.
> 삭제 경위·「동작 변경 0건」 증명은 이 파일 맨 끝 **「규칙 M-4 — 싱글 맵 준비 실패 안내 (2026-09-29)」** 절.

### 🔴 실패 직후 「재경기 준비 중」으로 되돌아가는 버그 수정 + 상태 줄 개행 (2026-09-27 3차)

**증상(실기 로그, 메인 세션 실측)**: 재경기 수락 → 준비 진입 → 강제 실패 → 실패 문구 → **13ms 뒤 다시
「재경기 준비 중...」** → 20초 뒤 다시 실패 문구.

**원인**: 수락 접수 신호가 **둘**(ⓐ `OnLocalRematchAccepted` 자기 버튼 · ⓑ `OnNetworkRematchAccepted`
서버 통보)인데 수락자는 둘 다 받는다. 정상 경로에서는 2차가 `_rematchPreparing` 가드에 막히지만,
**두 신호 사이에 실패 처리가 끼어들면 `StopRematchPreparingLimit()` 이 그 깃발을 내려** 2차가 통과한다.

🔴 **메인 세션의 진단 중 한 가지를 실기 로그 + 코드로 정정했다 — 2차는 ⓑ 가 아니라 ⓐ 다.**
로그 순서가 「ⓑ 발행(.205) → 준비 진입 1차(.206) → 강제 실패(.208) → … → 준비 진입 2차(.218)」인데,
`OnLocalRematchAccepted` 의 **첫 구독자는 컨트롤러**(`SendAcceptRematchSafely`)이고 수락자가 Host 면
`AcceptRematchServerRpc` → `NotifyRematchAcceptedClientRpc` 가 **같은 호출 흐름에서 로컬 실행**되므로
ⓑ 가 ⓐ 의 `OnNext` **안에서 먼저** 도달한다. `GameEndUI` 의 ⓐ 구독은 그 뒤에 불린다.
🔴 **그래서 도착 순서는 역할마다 다르다** — 수락자가 Host 면 ⓑ→ⓐ, Client 면 ⓐ→ⓑ(RPC 가 망을 타므로).
⚠️ **「ⓑ 만 삼킨다」는 consume-once 안은 Host 수락자에서 아무 효과가 없다**(막아야 할 2차가 ⓐ 이므로).
**재사용할 교훈: 두 신호의 「먼저/나중」을 설계 전제로 삼지 말 것 — 로컬 RPC 의 동기 실행 때문에 역할이
순서를 바꾼다.**

**수정 — 두 신호의 공통 입구 하나 + 회차 단위 표시 하나** (`Presentation/UI/GameEndUI.cs`)

| 무엇 | 자리 |
|---|---|
| 새 판별 깃발 `_rematchAcceptSignalHandled` | `_rematchPreparing` 바로 아래 |
| 공통 입구 `HandleRematchAcceptSignal(RematchAcceptSignal)` | 「재경기 준비 중」 절 머리 |
| 진단용 private enum `RematchAcceptSignal { LocalAccept, ServerNotice }` | 같은 자리 |
| 회차 경계 핸들러 `OnOpponentRematchRequested()` + 구독 `_rematchRequestedSubscription` | 같은 자리 / `Initialize` |

- 🔴 **ⓐ·ⓑ 둘 다 이 입구를 지난다. 순서를 보지 않고 「먼저 온 하나」만 통과시킨다.**
  그 결과 `EnterRematchPreparingState()` 의 **호출부가 1곳으로 줄었다**(종전 2곳).
- 🔴 **기존 가드 두 개(`_rematchPreparing` · `_opponentLeft`)는 한 글자도 안 고쳤다.** 새 판별은 그 **앞**이다.
  둘은 **다른 불변식**이다 — 가드는 「같은 상태에 두 번 들어가지 않는다」, 새 판별은 「한 회차의 수락 접수를
  한 번만 처리한다」. 합치면 실패 처리가 내리지 못하는 값으로 경쟁을 이기는 성질이 사라진다.
- 🔴 **`_rematchFailed` 로 막으면 안 되는 이유(코드 주석에 남겼다)**: 그 깃발을 내리는 자리가
  요청 버튼 onClick 과 `EnterRematchPreparingState` 인데 **수락자는 요청 버튼을 안 누르고**, 진입이 막히면
  후자에도 못 닿아 **영구 고착**된다.
- 🔴 **표시를 내리는 자리는 「회차 경계」 셋뿐 — 신호는 내리지 않는다**(두 번째 신호가 내리게 하면
  세 번째가 다시 통과하고 「한 회차에 한 번」이 「신호 두 개마다 한 번」으로 약해진다):
  ① `Initialize()` ② `SetupRematchButton` onClick(**내가 요청** = 새 회차) ③ `OnOpponentRematchRequested()`
  (**상대가 요청** = 새 회차). 🔴 **③ 이 없으면 실패 뒤 상대의 재요청을 수락할 때 삼켜진다** —
  수락자는 ② 를 지나지 않기 때문이다. 이것이 구독을 하나 더 늘린 유일한 이유이며,
  `GameEvents.OnNetworkRematchRequested` 구독자는 이로써 2개(+ `RematchRequestPopup`)다.
- 진단: 삼킨 줄 `Guard=AcceptAlreadyHandled, Signal=…` / 통과한 줄 `Signal=…`. 공통 필드에
  `AcceptHandled=` 를 더해 **깃발 4개**가 됐다. 새 `LogEvent` 0개 · 기존 `[Conditional]` 헬퍼 재사용.

**세 경로 검증(코드 추적 — 실기 아님)**: 수락자 = 먼저 온 1개만 진입(순서 무관) · 요청자 = ⓑ 만 오고
② 가 표시를 내려 둔 상태라 통과 · 상호 동의 = 양쪽 다 ② 를 지났으므로 ⓑ 통과.

**B. 상태 줄 문구 4개 개행(사용자 확정)** — `"…습니다. {0}초"` → `"…습니다.\n{0}초"`(공백 없음).
- 🔴 **평시 문구를 `NormalCountdownFormat` const 로 빼서 넷을 한 절에 모았다**(종전엔 `CountdownCoroutine`
  안 보간 문자열). 실패 문구 2개는 다른 절에서 이 절로 **이동**(문자열 무변경). 이유를 절 머리말·상수 주석에
  적었다: **하나가 메서드 안쪽에 숨어 있으면 다음에 고칠 때 그 하나만 옛 모양으로 남고, 상태마다 줄 수가
  달라져 화면이 튄다.**
- ⚠️ **평시 문구는 `\n` 이 맨 앞**이라 첫 줄이 빈다 — 넷의 줄 수를 2줄로 맞춰 높이가 변하지 않게 한 것.
  🔴 **사용자 확인이 필요한 유일한 판단**(사용자 지시문 「'n초' → '\n n초'」를 그대로 적용한 결과).
- 알림 팝업 문구(`OpponentLeftAlert*`, 규칙 D-5·D-6)와 `RematchPreparingStatusText`(남은 초 없음)는 **무수정**.
- ⚠️ 잔존 사본 1건(범위 밖, 미수정): `_countdownText` 의 `[Tooltip]` 안 예시 `'30초 후 로비로 돌아갑니다.'`
  — 화면에 안 뜨는 에디터 설명문이지만 「문구가 한 곳뿐인가」 grep 을 2건으로 만든다.

**씬 실측(수정 없음) — 개행으로 밀릴 위험 없음**: `CountdownText`(fileID `194798719`)는 컴포넌트가
RectTransform + CanvasRenderer + TMP **뿐**(ContentSizeFitter·LayoutElement 없음), 부모 `GameEndPanel`
(`1309749079`)에도 **LayoutGroup 없음**. 앵커 `min(0,0)`~`max(1,0.35)` · `sizeDelta 0` → **높이가 앵커로
고정**돼 글자 수가 rect 를 바꾸지 않는다. TMP: Center/Middle(`m_HorizontalAlignment:2`,
`m_VerticalAlignment:512`), fontSize **72 고정**(autoSize off), overflow `Overflow`, wrap on.
형제 4개는 겹치지 않는 앵커 띠(ResultText 0.70~0.85 · RestartButton 0.55~0.70 · LobbyButton 0.35~0.50 ·
CountdownText 0.00~0.35)라 **한 줄이 늘어도 형제가 밀리지 않는다.** 기준 해상도 1080×1920 →
띠 높이 672px, 줄 높이 ≈84px 이므로 여유가 크다.

⚠️ **미검증**: Unity 컴파일·런타임 전부. 확인한 것은 ① 중괄호 균형(주석·문자열 제거 후 51/51)
② `mcs -langversion:latest` 오류 **28건이 전부 `CS0246`/`CS0234` 외부 참조 누락**(구문 오류 0, 수정 전과
같은 개수) ③ 위 grep 들. 🔴 **타입 해석이 실패하므로 멤버 단위 오타는 이 방법으로 확인되지 않는다.**

**[🔴 2026-09-27 4차 갱신 — 위 「B. 상태 줄 문구 4개 개행」 항목의 서술은 한 글자도 지우지 않고 덧붙인다(B-7)]**

**① 평시 문구는 한 줄로 확정됐다(사용자가 실제 화면을 보고 확정) — 맨 앞 `\n` 제거.**
`NormalCountdownFormat` 최종값 `"{0}초 후 로비로 돌아갑니다."`. 🔴 **위 673행의 「넷의 줄 수를 2줄로
맞춰 높이가 변하지 않게 한 것」은 이 자리에서는 더 이상 근거가 아니다** — 평시 문구에는 남은 초 앞에
올 문장이 아예 없어서 `\n` 을 맨 앞에 두면 **화면 첫 줄이 그냥 빈 줄로 남는다.**

🔴 **「줄 수를 맞춰야 화면이 안 튄다」가 성립하지 않는 근거(씬 실측, 추정 아님)** — 위 「씬 실측」
문단과 같은 사실이지만 **결론이 반대 방향으로 쓰인다**: `CountdownText`(fileID `194798719`)에는
`ContentSizeFitter` 도 `LayoutGroup` 도 없고(부모 `GameEndPanel` 도 없음) 앵커 `(0,0)~(1,0.35)` ·
`sizeDelta 0` 이라 **rect 가 고정**이다. 즉 줄 수가 달라져도 **사각형 크기가 변하지 않아 형제 UI 가
밀리지 않는다.** Center/Middle 정렬이라 두 줄이 되면 사각형 안에서 세로 가운데 기준으로 위아래
반 줄씩 퍼질 뿐이다. **→ 일반 교훈: 「레이아웃이 튄다」를 근거로 쓸 때는 그 오브젝트에
ContentSizeFitter/LayoutGroup 이 실제로 붙어 있는지 씬을 먼저 열어 본다. 앵커 고정 사각형이면
글자 줄 수는 레이아웃에 영향이 없다.**

✅ **그래도 유지되는 규약 2개(지우지 말 것)**: ⓐ **`\n` 뒤에 공백을 넣지 않는다**(가운데 정렬이라
공백 하나가 그 줄 전체를 한쪽으로 민다) ⓑ **문구 네 개는 한 자리(상수 4개)에 모아 둔다.**

**② 위 676행의 「잔존 사본 1건(범위 밖, 미수정)」 = `_countdownText` 의 `[Tooltip]` 안 예시 문구는
2026-09-27 에 정리됐다(사본 0건).** Tooltip 은 예시 문자열을 지우고 **문구의 단일 소스가 상수 4개라는
사실만 가리키게** 바꿨고, 바로 위에 「Tooltip 에 표시 문구를 적지 않는다」는 이유 주석을 4줄 달았다.
🔴 **일반 교훈: 화면에 안 뜨는 에디터 설명문(Tooltip/Header)도 문구 사본이다** — 「문구가 한 곳뿐인가」
grep 을 오염시키고, 문구를 고칠 때 한쪽만 남는다. 상태에 따라 여러 문구가 뜨는 자리면 **예시를 적는
것 자체가 틀린 설명**이 된다.

**③ 같은 사실을 못 박은 주석이 파일 안에 3자리 있었다.** 문자열 한 곳만 고치면 나머지가 **거짓 주석**이
된다: ⓐ 절 머리말 「개행 규약」 불릿(현 `GameEndUI.cs` 105~128행) ⓑ `NormalCountdownFormat` 의 XML
주석 마지막 단락 ⓒ 🔴 **`CountdownCoroutine` 안 삼항 연쇄 위의 주석 「네 문구 모두 남은 초 앞에서
줄을 나눈다」(현 994~996행)** — ⓒ 는 호출자 지시에 없었고 이번 grep 으로 찾았다. **교훈: 문구·규약을
바꿀 때는 그 규약을 서술한 주석을 `grep` 으로 전수 조사한다(상수 주석 자리만 보면 메서드 안쪽 사본을
놓친다).**

⚠️ **미검증(이번 변경)**: Unity 컴파일·런타임. 확인한 것은 ① 중괄호 균형(주석·문자열 제거 후
**51/51** — 3차 작업과 동일) ② 문구 grep 이 **상수 4개 외 0건** ③ 동작 코드 심볼 전수 존재 확인
(`SelectRematchFailedFormat` · `HandleRematchAcceptSignal` · `_rematchAcceptSignalHandled` ·
`EnterRematchFailedState` · `RematchPreparingLimitCoroutine` · `StopCountdown` 호출 7곳) ④ 삼항 연쇄
3줄 원문 그대로. **동작 코드 diff 0줄**(바뀐 것은 문자열 리터럴 1개와 주석·Tooltip 뿐).

### 🔴 「다시하기」 버튼 활성 조건을 실패 사유까지 보게 좁혔다 (2026-09-27 4차, 여기서 동작이 바뀐다)

대상은 `Presentation/UI/GameEndUI.cs` **한 파일**, 바뀐 동작 코드는 **2줄**(조건식 1 · 판별식 1).
규칙: **D-2 유지(제거가 아니라 AND)** · **D-3 무변경** · **D-8 무변경**(① 항은 가드 밖 그대로).

**결함의 모양 — 「모순된 화면」은 강제 실패 테스트 전용이 아니었다**

- 종전 조건은 `interactable = !_opponentLeft` **하나**였고 **실패 사유를 보지 않았다.**
- 그래서 재경기 맵 준비가 `RematchMapFailureCause.OpponentDisconnected` 로 실패하면
  상태 줄은 「상대가 없다」(`RematchFailedByOpponentLeftCountdownFormat`)라고 말하는데
  **버튼은 활성으로 복구돼 「다시 해 보라」고 권했다.**
- 🔴 **왜 30초나 벌어지는가 — 두 시계의 길이가 다르다.** 이탈 확정은
  `NetworkGameEndController.ResultScreenSilenceTimeoutSeconds = 30f` 가 침묵을 30초 확인한 **뒤에야**
  `_opponentLeft` 를 켠다. 맵 준비 실패 통보는 그보다 훨씬 먼저 온다.
  실측(메인 세션이 로그 직독): 실패 진입 `20:57:20.703 | Cause=OpponentDisconnected, OpponentLeft=False`
  → 이탈 확정 `20:57:51.053 | SilenceSeconds=30.6` → **간격 30.35초.**
  🔴 **교훈: 「깃발 하나로 상황을 판별한다」가 맞는지 보려면 그 깃발을 켜는 시계의 길이를 봐야 한다.**
  같은 사실을 아는 경로가 둘이면 **빠른 쪽이 먼저 화면을 만든다.**

**최종 조건식 (A안 — 사용자 확정)**

```csharp
_restartButton.interactable = !_opponentLeft && !IsRematchFailedByOpponentDisconnected;
```

- 🔴 **되살리는 장치를 만들지 않았다**(A안의 핵심). 순단이었다면 다음 맵 전송이 또 실패할 뿐이고,
  되살리려면 **이 화면에 시계가 하나 더 늘어난다.** 이 화면은 이미 시계가 셋이다
  (자동 복귀 카운트다운 · 맵 준비 한도 · 이탈 감시).
- 🔴 **`OnOpponentRematchRequested()` 에 실패 깃발을 내리는 처리를 넣지 않았다** — 「상대가 요청을
  보내왔다 = 사유가 거짓으로 판명됐다」는 판단은 **승인 범위 밖**(사용자 확인 대기).

**판별 기준을 한 자리로 모았다 (CLAUDE.md 규칙 7)**

- 신설 `private bool IsRematchFailedByOpponentDisconnected =>
  _rematchFailureCause == RematchMapFailureCause.OpponentDisconnected;` (필드 바로 아래).
- 읽는 곳 **둘** — `SelectRematchFailedFormat()`(문구) · `RestoreRematchButton()`(버튼).
  `SelectRematchFailedFormat` 의 **반환값·역할은 무변경**(형식 문자열 하나만 돌려준다) —
  본문의 **조건식만** 프로퍼티 읽기로 바뀌었다.
- 검증 grep: `_rematchFailureCause == RematchMapFailureCause.OpponentDisconnected` 가 **1건**(정의부)이어야 한다.
- 🔴 **둘이 같은 기준을 보는 것은 우연이 아니다** — 문구를 둘로 가른 판단 자체가
  `GameSystemRules_RandomMap.md` 규칙 18 의 *「앞쪽은 다시 시도할 여지가 있고 뒤쪽은 없다」* 였다.
  즉 **「다시 시도할 여지가 있는가」 = 「버튼을 켜도 되는가」.**
  **문구만 그 판단을 따르고 버튼은 따르지 않는 상태**가 이 버그였다 —
  🔴 **교훈: 문구를 가르는 판단을 세울 때 그 판단을 공유해야 하는 컨트롤이 또 있는지 함께 본다.**

**호출부 3곳 확인 결과 (하나도 어긋나지 않는다)**

| 호출부 | 확인 |
|---|---|
| 거절 통보 구독 `OnNetworkRematchDeclined` | 새 조건이 **잘못 끄지 않는다.** 거절 ClientRpc 는 `TargetClientIds = requesterId` 로 **요청자에게만** 가고, 요청은 `_restartButton.onClick` 을 반드시 거치며 그 안에서 `_rematchFailureCause = Unknown` 으로 내려간다(`OnLocalRematchRequested.OnNext` 발행처 **전 리포지토리 1곳** = 그 onClick). 그 시점 사유는 Unknown → 조건 true → **종전과 동일하게 복원** |
| `OnOpponentLeft` | 무변경. `_opponentLeft = true` **뒤**에 부르는 순서 그대로 |
| `EnterRematchFailedState` | 🔴 **`_rematchFailureCause = cause;` 가 `RestoreRematchButton()` 보다 먼저**다(직접 확인). 그 순서에는 이미 이유가 주석에 있다(코루틴이 매 초 값을 읽어 첫 1초 깜빡임 방지) — **그 주석 덕분에 새 조건이 첫 프레임부터 옳게 판단한다.** 지우지 말 것 |

**주석에서 실제로 걸린 함정**

- ⚠️ ② 항 주석을 쓰면서 상태 줄 문구를 **그대로 베껴 적었다** → 「문구가 코드에 한 곳만 있는가」 grep 이
  무용해진다(`OpponentLeftCountdownFormat` 에서 이미 겪은 그 함정, 이번이 **2회째**).
  즉시 **상수 이름 참조**로 고쳤다. 🔴 **문구를 가리킬 때는 문구가 아니라 상수 이름을 쓴다.**

⚠️ **미검증(이번 변경)**: Unity 컴파일·런타임. 확인한 것은 ① 중괄호·괄호 균형(주석·문자열 제거 후
**51/51 · 228/228** — 3차·이전 작업과 동일) ② 문구 grep — `상대방이 나가서…` **1건** ·
`초 뒤 로비로 이동합니다` **1건** ③ `_restartButton.interactable` 대입 **2곳뿐**
(`false` = onClick 요청 시 · 위 조건식) ④ `_backToLobbyButton.interactable = false` **0건 유지**(D-3) ⑤ 삼항
연쇄 3줄 · `HandleRematchAcceptSignal` · `_rematchAcceptSignalHandled`(15건) · 타이머 상수 원문 그대로.
**씬·프리팹 0건 · 문서 0건 · git 명령 0건.**

### 🔴 실패 통보가 **먼저** 오고 수락 신호가 **뒤에** 오는 경로 (2026-09-28 5차, 여기서 동작이 바뀐다)

대상은 `Presentation/UI/GameEndUI.cs` **한 파일**, 늘어난 동작 코드는 **8줄**(새 가드 6 + 회차 경계 대입 2),
주석 비활성화 **2줄**. 규칙: **D-7 우선순위(이탈 > 실패 > 평시) 유지** · **D-2 · D-3 · D-8 무변경** ·
**`GameEvents`/enum/`Origin=` 값 추가 0**.

**결함 — §15-6 에서 고친 버그와 증상은 같고 순서만 반대다**

- §15-6(2026-09-27 3차)이 막은 것은 **「수락 → 실패 → 수락」**(신호 둘 사이에 실패가 끼어 `_rematchPreparing`
  가드가 풀리는 것)이었고, 새 판별 `_rematchAcceptSignalHandled` 가 **두 번째** 신호를 삼켜 해결했다.
- 🔴 **이번 경로는 「실패 → 수락」이고, 그 수락은 회차의 *첫* 신호라 그 판별을 정당하게 통과한다.**
  실측(2026-09-28 `_Logs/_editor/2026-09-28/RuntimeLog.txt` 719행 이후, 강제 실패 플래그 ④):
  `실패 상태 진입(.031)` → `수락 접수 신호를 처리한다 | Signal=LocalAccept` **「이 회차의 첫 신호다」**(.033)
  → `준비 상태 진입 | LimitSeconds=20`(.034) → 20초 뒤 다시 실패.
- **순서가 뒤집히는 이유**: `GameEvents.OnLocalRematchAccepted` 는 **구독자가 둘**(컨트롤러 ·
  `GameEndUI`)이고 컨트롤러가 앞이다. 컨트롤러의 `SendAcceptRematchSafely()` 가 **RPC 를 보내기 전에**
  예외를 맞으면 그 `catch` 가 실패를 **로컬에서 동기 발행**한다 → 이 화면의 실패 처리가 **먼저 끝나고**
  그 뒤에 `GameEndUI` 의 수락 구독자가 불린다.
  🔴 **재사용할 교훈: 「같은 채널의 앞 구독자가 *다른* 채널을 동기 발행하면, 뒤 구독자가 보는 상태는
  이미 그 다른 채널의 결과가 반영된 상태다.」** 구독 순서를 바꿔 고치려는 유혹이 생기지만
  **순서에 의존하는 해법은 역할(Host/Client)이 순서를 또 뒤집는다** — 순서와 무관하게 결과가 같아야 한다.

**수정 — 두 자리. 🔴 둘 중 하나만 넣으면 이미 통과한 테스트가 깨진다**

| # | 자리 | 무엇 |
|---|---|---|
| ① | `OnOpponentRematchRequested()` | `_rematchFailed` · `_rematchFailureCause` 도 함께 내린다 |
| ② | `EnterRematchPreparingState()` | `_opponentLeft` 가드 **뒤**에 `if (_rematchFailed)` 가드 + `Guard=RematchFailed` |

- 🔴 **①이 없으면 ②가 정상 경로를 막는다.** 실패 깃발을 내리는 자리가 여태 **둘**(`Initialize()` ·
  「다시하기」 onClick)뿐이라 **수락하는 쪽은 그 어느 자리도 지나지 않았다**(요청 버튼을 안 누른다).
  실측 증거(2026-09-27): `상대의 재경기 요청 도착 | RematchFailed=True` →
  `수락 접수 신호를 처리한다 | RematchFailed=True`. 그 경로는 사용자가 실기로 통과 확인한 것이다.
  ⚠️ **회차 경계 세 자리가 비대칭이었다는 것이 결함의 본체다** — `Initialize()` 와 onClick 은 깃발 셋을
  전부 내리는데 `OnOpponentRematchRequested()` 만 `_rematchAcceptSignalHandled` 하나만 내렸다.
  🔴 **일반 교훈: 「회차 경계」처럼 같은 역할의 자리가 여러 개면 *무엇을 내리는가*를 표로 맞춰 본다.
  한 칸이 비어 있으면 그 칸을 지나는 경로에서만 조용히 깨진다.**
- 🔴 **①이 깃발의 *뜻*을 확정한다 — 「켜져 있다 = 이번 회차는 이미 실패로 끝났다」.** 내리는 활성 자리
  셋이 전부 회차 경계(새 판 · 내가 요청 · 상대가 요청)라서 그 뜻이 항상 참이고, **②의 가드는 그 뜻에만
  의존한다.** 그래서 `_rematchFailed` 주석에 **「회차 경계가 아닌 자리에서 이 깃발을 내리지 말 것」**을
  못 박았다(자리가 하나라도 늘면 ②가 정상 경로를 막기 시작한다).
- **①의 부수 효과(의도된 개선)**: 상대 요청이 도착하면 상태 줄이 **실패 문구 → 평시 문구**로 돌아간다.
  상대가 요청을 보냈다 = 상대가 살아 있다 → 실패 문구는 그 순간부터 거짓이다. 근거를 주석에 적었다.
  ⚠️ **「다시하기」 버튼은 되살리지 않는다** — `RestoreRematchButton()` 을 부르지 않으므로
  `IsRematchFailedByOpponentDisconnected` 로 꺼져 있던 버튼은 **그대로 꺼져 있다**(D-2 무변경).
  🔴 즉 **「상태 줄은 평시 · 내 다시하기는 비활성 · 수락 팝업은 떠 있다」는 조합이 생긴다.** 나가는 길
  (「로비로」, D-3)과 앞으로 가는 길(수락)이 둘 다 열려 있어 갇히지 않으므로 승인 범위대로 두었다.
- 🔴 **②의 가드 순서를 `_opponentLeft` 뒤에 둔 근거**: 두 조건이 동시에 참일 수 있고(실패 뒤 이탈 확정),
  그때 **더 근본적인 사실은 「상대가 떠났다」**다(D-7 의 「이탈 > 실패 > 평시」, 그리고 `CountdownCoroutine`
  삼항 연쇄가 이미 그 순서다). 세 가드를 그 순서로 놓으면 **로그의 `Guard=` 분포를 문구 우선순위와 같은
  순서로 읽을 수 있다.** 화면 결과는 순서와 무관하다(둘 다 조기 반환) — 갈리는 것은 `Guard=` 값뿐이다.
- **가드에 로그를 넣은 이유는 기존 둘과 같다** — 결과가 똑같이 「화면이 안 바뀐다」이므로 어느 가드에
  막혔는지 로그로 갈려야 한다. `Guard=` 값이 이제 셋이다(`AlreadyPreparing` · `OpponentLeft` ·
  **`RematchFailed`**). 새 `LogEvent` 0개 · 기존 `[Conditional]` 헬퍼 재사용.

**🔴 `EnterRematchPreparingState()` 안의 `_rematchFailed = false` 두 줄 — 주석 비활성화(삭제 아님)**

가드가 true 일 때 반환하므로 그 아래에서 `_rematchFailed` 는 **항상 false**(사유도 늘 같은 자리에서 함께
내려가므로 항상 `Unknown`)라 **두 줄이 도달 불가 = 영구 no-op** 이 됐다.
🔴 **삭제하지 않은 근거는 `WORKFLOW.md` [4] 「기존 로직 제거 규칙」이다** — *「검증 전까지는 '제거' 대신
'비활성화(주석 처리)'를 기본으로 하고, 최종 삭제는 [6] 사용자 테스트 통과 후 [7] 문서 업데이트 전에
수행한다」*. 즉 **이 판단은 내 취향이 아니라 프로젝트 절차**다.
- ⚠️ **원래 주석의 근거를 문장째로 비활성화 블록 안에 옮겨 적었다**(「지난 시도가 실패했더라도 …
  실패 문구가 되살아난다」). 그 걱정이 **없어진 것이 아니라 다른 방법으로 해소됐다**는 것과
  **되살려야 하는 조건**(가드를 없애거나 조건을 좁히면)을 함께 적었다.
- `_rematchFailed` 필드 주석의 「내리는 자리」 목록도 **4항목**으로 고쳤다 — 신설 ③(상대가 요청) +
  ⚠️ 비활성화된 `EnterRematchPreparingState`. **목록이 코드와 어긋나면 다음 사람이 잘못된 지도를 본다.**

**거짓이 된 주석 4자리를 함께 고쳤다(전수 grep 으로 찾았다)**

① `_rematchRequestedSubscription` 필드 주석 「`_rematchAcceptSignalHandled` 를 내리는 것 **하나뿐**이다」
② `Initialize()` 의 그 구독 위 주석 「이 구독은 **화면을 아무것도 바꾸지 않는다**」
③ `OnOpponentRematchRequested()` XML 주석의 같은 문장
④ `_rematchAcceptSignalHandled` 주석의 「🔴 **왜 `_rematchFailed` 로 막으면 안 되는가**」 단락 —
   🔴 **지우지 않고 「전제가 바뀌었다」를 덧붙였다.** 그 단락이 말한 영구 고착의 원인은
   **「깃발을 내릴 기회가 없다」**였고 ①이 그 기회를 만들었다. 그리고 **그 판별을 가드로 대체할 수는
   없다는 것**(실패가 없는 정상 재경기에서 두 번째 신호를 삼키는 것은 그 판별뿐)도 함께 적었다.
🔴 **일반 교훈: 「하나뿐이다」 · 「아무것도 안 한다」처럼 *수를 단정하는 주석*은 그 자리에 무언가를
더하는 순간 거짓이 된다. 동작을 늘렸으면 그 단정을 `grep` 으로 전수 조사한다.**

**경로 4개 확인(코드 추적 — 실기 아님)**

| # | 경로 | 결과 | 근거 |
|---|---|---|---|
| 1 | 실패 → **내가** 다시하기 → 상대 수락 | ✅ 진입 | onClick 이 깃발 셋을 내린다 → 서버 통보 시 `_rematchFailed=false` |
| 2 | 실패 → **상대가** 재요청 → 내가 수락 | ✅ 진입 | **①이 내린다.** 구독 순서와 무관(팝업 구독자는 표시를 건드리지 않는다) |
| 3 | **실패 통보 뒤 수락 신호** | 🔴 `Guard=RematchFailed` 로 막힘 | `_rematchPreparing=false`(실패 처리가 내렸다) · `_opponentLeft=false` → 세 번째 가드가 잡는다 |
| 4 | 이탈 판정 뒤 수락 신호 | `Guard=OpponentLeft`(무변경) | 그 가드가 새 가드보다 **앞**이므로 둘이 동시에 참이어도 이쪽이 남는다 |

- 3 에서 삼킨 뒤 **고착되지 않는다**: `_rematchAcceptSignalHandled` 는 true 로 남지만 회차 경계 세 자리가
  모두 그것을 내리고, 그 회차에는 서버 통보가 애초에 오지 않는다(RPC 가 나가지 못한 회차다).

**⚠️ 실기에서 로그 한 줄의 값이 바뀐다(의도)** — `상대의 재경기 요청 도착` 줄은 공통 필드를 **깃발을
내린 뒤에** 조립하므로 `RematchFailed=True` → **`RematchFailed=False`** 로 바뀐다.
✅ **그것이 ①이 동작했다는 증거로 쓰인다**(문구·레벨·필드 구성은 한 글자도 바꾸지 않았다).

⚠️ **미검증(이번 변경)**: Unity 컴파일·런타임 전부. 확인한 것은 ① 중괄호·괄호·대괄호 균형
(주석·문자열 제거 후 **52/52 · 230/230 · 34/34** — 직전 회차 51/51 · 228/228 에서 **새 가드 1블록 +
괄호 2쌍**만큼 늘었고 그 증분이 정확히 설명된다) ② `mcs -langversion:latest` 오류 **28건이 전부
`CS0246`/`CS0234` 외부 참조 누락**(구문 오류 0 — 직전 회차와 **같은 개수**) ③ 문구 grep —
`상대방이 나가서` **1건**(상수뿐) · `초 뒤 로비로 이동합니다` **1건** · `재경기 준비 중...` **7건**
(수정 전과 동일) ④ `_restartButton.interactable` 대입 **2곳** · `_backToLobbyButton.interactable = false`
**0건**(D-3) ⑤ 삼항 연쇄 3줄 · 타이머 상수식 2자리 · 강제 실패 플래그 · 문구 상수 4개 원문 그대로
⑥ 수정 파일 **1개뿐**(`find -mmin` 실측). **씬·프리팹 0건 · 문서(.md) 0건 · git 명령 0건.**

🔴 **주석 사본 함정을 이번에도 한 번 밟았다(3회째, 커밋 전에 스스로 잡았다)** — 새 주석에
`상대방이 나가서` 와 실패 문구를 **베껴 적어** 검증 grep 이 2건으로 늘어난 것을 grep 으로 발견하고
전부 **상수 이름 참조**로 고쳤다. ⚠️ **이 함정은 「알고 있다」로 막히지 않는다** — 주석을 쓰고 난 뒤
**문구 grep 을 반드시 돌린다**를 절차로 둘 것.

### 🔴 위 두 줄의 **최종 삭제** — WORKFLOW [4] 절차의 마지막 단계 (2026-09-28 6차, 동작 변경 0건)

> 바로 위 「5차」 절이 **주석 비활성화**한 두 줄을 **지운** 회차다. 위 절은 한 글자도 지우지 않는다(B-7).
> **[🔴 정정 — 위 절의 「`_rematchFailed` 필드 주석의 「내리는 자리」 목록도 **4항목**으로 고쳤다」는
> 이제 「4항목 중 하나는 삭제됐다」로 읽어야 한다. 활성 자리는 **셋**이다.]**

**대상**: `Assets/_Project/Scripts/Presentation/UI/GameEndUI.cs`
`EnterRematchPreparingState()` 안의 주석 비활성화된 대입 2줄(실패 깃발 내리기 + 실패 사유 되돌리기).

**🔴 「최종 삭제」를 언제 해도 되는지의 판단 근거는 절차 문장 하나다** — `WORKFLOW.md` [4]:
「주석 처리된 기존 로직의 최종 삭제는 **[6] 사용자 테스트 통과 후, [7] 문서/메모리 업데이트 전**」.
즉 **삭제를 미루는 것도, 앞당기는 것도 규칙 위반**이다. 이 회차는 그 창 안에 정확히 들어간다.

#### 🔴 절차상 이 단계에서만 일어나는 일 — 「비활성화해 두었다」는 서술이 **거짓이 된다**

근거 주석을 **보존하면서** 그 안의 **상태 서술만** 고치는 작업이 함께 온다. 보존해야 하는 것 3가지:

1. **원래 주석의 문장**(무엇을 걱정해서 그 줄이 있었나)
2. 그 걱정이 **없어진 것이 아니라 다른 방법으로 해소됐다**는 것
3. **되살려야 하는 조건**(가드를 없애거나 조건을 좁히면 **두 줄을 함께** 복원)

→ 코드 줄이 사라져도 이 셋은 남는다. 고친 것은 「비활성화했다」 → **「최종 삭제했다 + 언제 + 무슨 근거로」**
(WORKFLOW [4] 순서 4단계 중 **세 번째 단계**임을 명시해 절차 위반이 아님을 남겼다).

#### 「수를 단정하는 주석」 전수 조사 — 이번에는 **3자리**

`grep -rn "주석 비활성화\|주석 처리\|비활성화" Assets/_Project/Scripts --include=*.cs` 를 돌려
**70건**(수정 후 실측) 중 이 회차 소유 **3자리**만 골라 고쳤다(나머지는 다른 회차/다른 작업의 것 — 건드리지 않았다).

| # | 자리 | 고친 내용 |
|---|---|---|
| ① | `EnterRematchPreparingState()` 의 비활성화 블록 | 헤더 「주석 비활성화」 → **「최종 삭제」** + 삭제 시점·실기 근거 2구간 |
| ② | `_rematchFailed` 필드 주석 「내리는 자리」 목록 4항목 | ④항을 **「최종 삭제됐다」**로 + 목록 뒤에 🔴 **「활성 자리는 위 세 곳뿐 — 네 번째 자리는 없다」** 한 문장 신설 |
| ③ | `_rematchAcceptSignalHandled` 의 2026-09-28 갱신 단락 | 그 단락 **위쪽 역사 단락이 「내리는 자리」로 꼽은** `EnterRematchPreparingState` 가 삭제됐다는 한 줄 추가 |

🔴 **③을 왜 범위에 넣었는가** — 그 자리는 「비활성화」라는 낱말을 쓰지 않아 grep 에 **안 걸린다.**
그런데 **현재시제로 「내리는 자리는 onClick 과 `EnterRematchPreparingState`」라고 단정**하고 있었다.
즉 **낱말 grep 만으로는 거짓이 된 주석을 다 못 찾는다** — 삭제한 **식별자 이름**(`EnterRematchPreparingState`)
으로도 한 번 더 grep 해야 한다.

#### ✅ 「동작 변경 0건」을 git 없이 **엄격히** 증명한 방법 (재사용할 것)

`git diff` 금지(규칙 5) 상황에서 「주석만 고쳤다」를 **판단이 아니라 측정**으로 만드는 방법:

> **주석·문자열 리터럴을 걷어낸 뒤 남은 코드 본문의 SHA-256 을 수정 전/후로 비교한다.**
> 수정 전 `85b678e2797d6ef0…` → 수정 후 **완전 동일**. (중괄호 52/52 · 괄호 230/230 · 코드행 456 도 동일.)

- 중괄호 균형만으로는 **대입 한 줄이 늘거나 줄어든 것을 못 잡는다.** 해시는 잡는다.
- 스트리퍼는 `/tmp` 스크래치패드에 두고 돌렸다(프로젝트에 파일을 남기지 않는다). 구현 요점:
  `//`·`/* */`·`"..."`·`'.'` 를 상태 기계로 제거 → 각 행 `strip()` → 빈 행 제거 → 해시.
  🔴 문자열 **내용**까지 지우므로 보간 `$"{x}"` 의 중괄호 오탐이 사라진다(기존 메모 「파이썬으로
  스트립 후 세는 것이 유일하게 신뢰할 수 있다」의 **해시 버전**).
- 🔴 **전체 행수는 늘어난다**(2044 → 2067) — 주석을 더 자세히 썼기 때문이다. **행수는 증거가 아니다.**

#### ⚠️ 주석 사본 함정 — **4회째.** 이번에도 **쓰고 난 뒤 grep 으로** 잡았다

새 주석에 ⓐ `_rematchFailed = false;` / `_rematchFailureCause = …Unknown;` **대입문 그대로**와
ⓑ 로그 필드 값 `Guard=RematchFailed` 를 베껴 적었다 → `grep -c "_rematchFailed = false"` 가 **3 → 4**,
`Guard=RematchFailed` 가 **1 → 2** 로 오염됐다. 산문으로 바꿔 3·1 로 되돌렸다.
🔴 **함정의 범위가 「상태 줄 문구」보다 넓다**: 베끼면 안 되는 것은 **검증 grep 의 대상이 되는 모든 토큰** —
UI 문구 · **대입문** · **로그 키/필드 값** 전부다. 절차: **주석을 쓴 뒤 그 회차의 검증 grep 을 전부 재실행.**

#### 실기 근거를 이 회차에서 직접 재확인한 결과 (일치)

`Assets/_Project/Docs/_Logs/_editor/2026-09-28/RuntimeLog.txt` (1,122행, 두 구간)

| 구간 | 확인한 것 | 결과 |
|---|---|---|
| 834~943 | 실패 가드에 막힌 진단 줄 | 1건 (18:43:37.809) |
| 834~943 | 진입 **성공** 진단 줄 · 카운트다운 중단 출처가 이 메서드인 줄 | **각 0건** → 삭제한 두 줄에 도달하지 않는다 |
| 944~1122 | 실패 가드에 막힌 진단 줄 | **0건** → 새 가드가 정상 경로를 막지 않는다 |
| 944~1122 | 실패 진입 → 상대 재요청(깃발 내려감) → 진입 성공 → 재경기 시작 | 4줄 순서대로 관측 |
| 두 구간 | 예상 밖 `[ERROR]` | **0건** (944+ 의 `[ERROR]` 1건은 **강제 실패 플래그**가 일부러 낸 것) |

#### ⚠️ 미검증으로 남는 것

- **Unity 컴파일·런타임 전부.** 코드 본문 해시가 같으므로 **컴파일 결과가 달라질 수 없다**는 것이
  근거이지만, `mcs` 로 이 파일을 단독 빌드할 수는 없다(`Unity.Netcode`·`UnityEngine` 의존).
- 삭제 후 **재경기 실기 재검증은 하지 않았다** — 2026-09-15 의 마커 주석 삭제와 같은 판단이다.
- XML 주석 태그 균형은 확인했다(`<para>` 52/52 · `<item>` 33/33 · `<list>` 11/11 · `<b>` 249/249).
  ⚠️ `<b` 로 세면 `<br/>` 이, `<para` 로 세면 `<paramref` 가 섞인다 — **닫는 태그 기준으로 세라.**

---

## 🔴 규칙 M-4 — 싱글 맵 준비 실패 안내 (2026-09-29, 구현 + 실기 검증 + 최종 삭제 완료)

**무엇이 됐나** — 싱글에서 맵 준비가 실패했을 때 화면이 비지 않는다.
① **최초 경기** = Bootstrap 이 `ShowConfirm` 모달(「다시하기」/「로비로」)을 띄운다.
② **재경기(다시하기)** = **결과 화면을 되살리고 상태 줄에 한 줄**로 알린다.
🔴 ③ **싱글에는 자동 복귀 타이머가 없다**(규칙 D-4 의 싱글 적용 범위).

- 실패 통보는 **싱글 전용 이벤트 채널** `GameEvents.OnSingleMapPreparationFailed`(`GameEvents.cs:1278`,
  발행 `Bootstrap/GameBootstrapper.Map.cs:579`, 구독 `GameEndUI.cs:912`).
- ✅ **`LoadMap()` 시그니처는 무변경** — 기존 호출부 3곳 그대로, 모달의 재시도가 더해져 총 4곳.
- ✅ **씬·프리팹 작업 0건**(실측). 규칙 D-6 단계 8 과 같은 이유다 — 참조를 직렬화 필드로 두지 않았다.
- 🔴 **죽은 진입점 메서드 1개(52줄)를 이 회차에 최종 삭제**했다(호출부 0건, `WORKFLOW.md` [4] 의 마지막 단계).
  그 메서드를 전제로 적혀 있던 서술 3곳은 같은 날 정정했다(이 파일 `:617` 블록 ·
  [MEMORY.md](MEMORY.md) 색인 · 🔴 [network-infra.md](network-infra.md) 「Phase 7」 **절 머리 블록**).
- 강제 실패 수단(⑤)과 그 표식 설계 → [network-infra.md](network-infra.md) 「강제 실패 플래그 ⑤」.

### 🔴 (1) `Initialize()` 가 깃발을 지워버리는 함정 — 조용히 실패한다

싱글 맵 준비 실패를 알리는 깃발 `_restartMapLoadFailed`(`GameEndUI.cs:650`)를 세워 두고
`LoadMap()` 이 돌아온 뒤에 읽는 구조를 만들었다. 🔴 **그런데 관용을 따라 `Initialize()` 의
「깃발 내리는 자리」 묶음에 이것도 함께 넣었다**(`_opponentLeft` · `_rematchFailed` ·
`_rematchAcceptSignalHandled` 가 이미 그 자리에 있다 — **나란히 두면 맞아 보인다**).

- 🔴 **`Initialize()` 는 `LoadMap()` 의 뒷단계에서 불리고, 실패 통보는 그보다 앞 단계에서 온다.**
  → 실패한 회차에서는 **깃발이 세워진 뒤에 `Initialize()` 가 지나간다.** 거기서 지우면 깃발은
  **읽히기 전에 사라지고, 결과 화면이 영영 되살아나지 않으며, 아무 오류도 나지 않는다.**
- 구현 중에 스스로 잡고 **그 자리에 「여기에 넣지 말 것」 경고 주석**을 남겼다(`GameEndUI.cs:764`).
  🔴 **경고를 그 자리에 두는 것이 핵심** — 다음 사람이 「새 판이니까 지운다」로 다시 넣을 자리가 바로 거기다.
- ✅ **되돌리는 자리는 `OnRestartClicked()` 한 곳뿐**(`:1066` 부르기 직전 · `:1087` 읽은 직후).
  지난 회차의 값이 남을 창이 애초에 없다.

> 🔴 **일반화(재사용할 것)**: **「깃발을 세우는 단계」와 「되돌리는 단계」가 같은 함수 호출 안에서
> 어느 쪽이 먼저 지나가는지 확인하지 않으면, 초기화 관용이 그 깃발을 삼킨다.**
> 확인 방법은 하나 — **두 자리가 같은 호출 스택 안에 있는지, 있으면 순서가 어떤지 코드로 따라간다.**

### 🔴 (2) 실패 뒤에도 `LoadMap()` 은 끝까지 진행하며 결과 화면을 **두 번 더 닫는다**

실패 통보를 받은 **그 자리에서** `Show()` 하면 안 된다. 통보는 맵 재로드의 **한가운데**에서 오고,
재로드는 실패한 뒤에도 끝까지 진행한다:

| 순서 | 무엇이 결과 화면을 다시 닫는가 |
|---|---|
| ① | `_gameEndUI.Initialize()` 의 끝이 `Hide()` |
| ② | `GameEvents.OnGameStarted` 발행 → `GameUIManager`(`:109`·`:166`) → `GameEndUI.OnGameStarted()` 가 또 `Hide()` |

- 🔴 **UniRx 발행이 동기(같은 호출 스택)라는 것은 맞다 — 그런데 동기이기 **때문에** 통보받은 자리에서
  `Show()` 하면 같은 호출 안에서 두 번 도로 꺼진다.** 「동기니까 순서가 보장된다」로 닫으면 틀린다.
- ✅ **그래서 계약은 「깃발만 세우고 반환 후에 켠다」** — 그 **단일 소스는 이번 task Plan 의 「C-계약」**
  (`Assets/_Project/Docs/_Tasks/2026-09-28/11_50_single-map-failure-ui/Plan.md` §4-C).
  구현은 `OnRestartClicked()` 가 `LoadMap()` 에서 돌아온 직후 한 자리에서만 되살린다.
- ⚠️ **팝업을 쓰지 않은 이유는 규칙 M-3 의 2026-09-22 개정과 같다** — 모달이 「로비로」 버튼을 덮어
  **스스로 나갈 길을 가린다**(규칙 D-3). 재경기 구간은 **이미 떠 있는 상태 줄**을 쓴다.

### 🔴 (3) 주석에 식별자를 베껴 적어 검증 grep 을 오염시켰다 — 같은 함정 **5회째**

`ShouldStartAutoReturnCountdown`(`GameEndUI.cs:678`)의 XML 주석에 **판별식 식별자를 그대로** 적어,
「판별 수단이 새로 생겼는가」를 세는 grep 이 **코드 5 + 주석 2** 로 측정됐다. 커밋 전에 스스로 잡아 사본을 제거했다.
(🔴 실측 확인: 지금 `GameEndUI.cs` 안의 완전 수식 히트는 **코드 5 + 주석 1**이고, 남은 주석 1건은
`:728` 의 **기존** 주석이다 → 사본은 제거돼 있다.)

- 🔴 **결론이 다섯 번째로 재현됐다 — 「알고 있다」로는 막히지 않는다.**
  이번에 막은 것은 **지식이 아니라 절차**였다: **주석을 쓴 뒤 그 회차의 검증 grep 을 전부 재실행한다.**
  🔴 **그 항목(4회째)을 읽고 시작했는데도 밟았다** — 읽기는 예방 수단이 아니다.
- 베끼면 안 되는 것의 범위는 4회째에 확정된 그대로다: **검증 grep 의 대상이 되는 모든 토큰**
  (UI 문구 · 대입문 · 로그 키/필드 값 · **식별자**).

### ✅ (4) 코드 본문 SHA-256 으로 「동작 변경 0건」을 증명했다 — **2회째 재사용**

방법은 2026-09-28 6차 절(이 파일 「✅ 「동작 변경 0건」을 git 없이 **엄격히** 증명한 방법」)과 같다:
주석·문자열 리터럴(보간·verbatim 포함)을 파싱으로 걷어내고 공백을 제거한 **코드 본문의 SHA-256** 을
수정 전/후로 비교 → **52줄 삭제 전후가 완전히 동일(12,735자)**.
🔴 **행수·중괄호 총계는 증거가 못 된다**(대입 한 줄의 증감을 못 잡는다). 해시는 잡는다.
✅ **두 회차 연속으로 통했으니 「삭제·정리 회차의 기본 증명 수단」으로 굳힌다.**

### 실기 실측 (로그 원본 확인)

| 무엇 | 실측 |
|---|---|
| 싱글 카운트다운 시작 | **0건** (규칙 D-4 싱글 미적용이 화면에서 확인됨) |
| 「다시하기」 실패 화면 유지 | **약 315.9초** (타이머가 없으니 사용자가 누를 때까지 남는다) |
| 강제 실패 발화 | **2회** |
| 멀티 자동 복귀 만료 | **60.164초(에디터) / 60.286초(실기)** |
| 이탈 재시작 | **30초** 발화 |
| 실기 `[ERROR]` | **0건** |

### 🔴 미검증으로 남은 것 (지우지 말 것)

- ⓐ **삭제 후 재검증 0건** — 52줄을 지운 뒤 실기를 다시 돌리지 않았다(2026-09-15·09-28 과 같은 판단).
- ⓑ **Unity 에디터 컴파일 미확인**(`GameEndUI.cs` 는 `UnityEngine`/`UniRx` 의존, 헤드리스 불가).
- ⓒ **setter 매 초 호출의 성능·깜빡임 미측정.**
- ⓓ **최초 실패 모달의 z-order·오버레이 상호작용 미확인.**
- ⓔ 🔴 **팝업·문구·깜빡임은 로그에 남지 않는다** — 근거는 **사용자 육안 보고(「모두 정상」) 하나뿐**이다.

### 계획에 없던 판단 1건 (사용자에게 보고됨)

최초 경기와 재경기가 **같은 실패 지점**을 지나므로 **재경기 구간에는 모달을 띄우지 않는다**
(`IsRestartMapLoadInProgress`(`:622`)를 Bootstrap 이 읽어 건너뛴다).
🔴 **안 하면 모달이 결과 화면의 「로비로」 버튼을 덮어 규칙 D-3 위반**이다.

### 🔴 범위 밖으로 남긴 것

- 🔴 **`ProjectMap` 실패(맵을 격자에 새기는 단계)에는 여전히 안내가 없다** —
  규칙 M-4 가 이를 포함하는지 불분명해 **판단하지 않았다**(규칙 12).
- 라벨 사본 · 상태 줄 일원화의 남은 **2줄(멀티 경로)** · 씬 자리표시 문자열 · `ILogSink.cs` 문구 사본.

**[🔴 2026-09-29 2차 — 위 「범위 밖」 두 줄은 지우지 않는다(B-7). 둘 다 이 회차(후속 H~M)에서 닫혔다]**
🔴 **`ProjectMap` 실패에도 안내가 생겼고**(아래 참조), 라벨 사본 · 상태 줄 남은 2줄 · 씬 자리표시 문자열 ·
`ILogSink.cs` 문구 사본도 **전부 처리됐다.** 사용자 승인 후 진행된 후속 항목 H·I·J·K-1·K-2·L·M 이다.

---

## 🔴 규칙 M-4 후속 H~M — 투영 실패 안내 + 문구·상태 줄 일원화 (2026-09-29 2차, 구현 + 실기 검증 완료)

**무엇이 됐나** (과대 표기 금지 — 아래 「미확인」과 함께 읽어야 한다)

| 항목 | 결과 |
|---|---|
| 투영 단계 실패 | **싱글 실패 UI 를 띄운다**(준비 실패와 같은 화면 경로를 탄다) |
| 씬 자리표시 문자열 | **두 자리** 비움 — `[UI]/SafeAreaContainer/GameEndPanel/{CountdownText, ResultText}`(되돌리기 값 `124124`) |
| `ILogSink` 주석 사본 | 제거 — 문구 리터럴 **전체 4 → 3**(실측: `BattleViewModel:339` · `GameEndUI:406` · `GameBootstrapper.Map.cs:76`, `ILogSink.cs` 안 **0건**) |
| 버튼 라벨 | 단일 소스화 — `GameEndUI.RestartButtonLabel`(`:297`) · `BackToLobbyButtonLabel`(`:308`) |
| 상태 줄 쓰기 | **완전 일원화** — `SetStatusLineText()`(`:1375`) 밖의 `_countdownText.text` 직접 대입 **0건**(실측) |
| 강제 발화 수단 | 투영 실패용 신설(메뉴 **8개**) → [network-infra.md](network-infra.md) 「강제 실패 플래그 ⑥」 |

### 🔴 (5) 문자열 상수의 「단일 소스를 어디에 두는가」는 **레이어 방향**이 정한다

- 버튼 라벨 2개를 **Presentation(`GameEndUI`)의 `public const`** 로 모으고 Bootstrap 이 그것을 참조한다
  (`GameBootstrapper.Map.cs:95`·`:106`). 근거: **Bootstrap → Presentation 은 허용 방향이고 그 참조가 이미
  있어 새 의존 간선이 0개**다. 반대로 Bootstrap 에 두고 Presentation 이 참조하면 **방향이 뒤집힌다.**
  🔴 **일반화: 「어느 쪽이 더 원본 같은가」로 정하지 말고 「어느 방향이 허용되는가 + 이미 있는 간선인가」로
  정한다.** 그러면 단일 소스화가 의존성 부채를 만들지 않는다.
- 🔴 **그리고 「같아 보이는 문구」를 함부로 합치지 않았다.** 실패 문구 리터럴 3건은 **의도적으로 남았다**:
  - `GameBootstrapper.Map.cs:76` = `"맵 준비에 실패했습니다"` (**마침표 없음** — 규칙 M-4 확정 표기, 모달)
  - `GameEndUI.cs:406` = `"맵 준비에 실패했습니다."` (**마침표 있음** — 상태 줄은 문장이다)
  - `BattleViewModel.cs:339` = 로비 경로(최초 경기 전송 실패)라 맥락 자체가 다르다
  🔴 **마침표 차이가 사양이다. 합치면 「중복 제거」가 아니라 규칙이 정한 표기를 바꾸는 것이 된다.**
  ⚠️ **판단 기준: 사본을 합치기 전에 「두 문자열이 정말 같은가」를 바이트로 확인한다.** 눈으로 같아 보이는
  것과 같은 것은 다르다.

### 🔴 (6) 주석 사본 함정 — **6회째.** 세는 대상이 또 넓어졌다

이번 회차에서 **두 번** 밟았고, 두 번 다 **막은 것은 지식이 아니라 「쓰고 나서 grep」 절차**였다.

- **(a) XML 태그 토큰**: 주석 산문에 `<returns>` 라는 **태그 이름 자체**를 적어 태그 균형이 **9/8** 로
  어긋났다(여는 것만 늘었다). 현재 실측은 `<returns>` 8 / `</returns>` 8 로 **일치**한다.
- **(b) 검사가 세는 타입 이름**: 신규 파일 머리말에 **`EditorPrefs` 라는 타입 이름**을 적어
  「가드(`#if UNITY_EDITOR`) 밖에 에디터 전용 참조가 0건인가」 검사가 걸렸다.
  ✅ 고친 뒤 실측: `ForcedMapProjectionFailure.cs` 는 가드가 `:48`, 첫 `EditorPrefs` 언급이 `:59` → **가드 안**.
- 🔴 **세는 대상의 누적 목록**: 문구 → 대입문·로그 키/필드 값 → 식별자 → **이제 XML 태그 토큰과
  「검사가 세는 타입 이름」까지.** 즉 **「그 회차의 검증 grep 이 세는 모든 토큰」**이 대상이고, 목록을
  외우는 방식으로는 막히지 않는다(6회 연속 실패했다).
- ⚠️ **선례 파일 3개의 머리말은 아직 같은 오탐을 낸다**(범위 밖이라 고치지 않았다 — 규칙 6):
  `ForcedMapTransferFailure.cs:23`·`:28`(가드 `:40`) · `ForcedAcceptSendFailure.cs:29`·`:36`(가드 `:49`) ·
  `ForcedMapPreparationFailure.cs:38`(가드 `:52`). 🔴 **다음에 이 검사를 돌리는 사람은 이 3건을
  「내가 만든 위반」으로 착각하지 말 것.**

### 🔴 실기 검증 결과 — 「코드 판독 판정」이 「실기 확인」으로 올라간 것 (K-2)

K-2 가 건드린 **멀티 두 자리**는 지난 회차에 「코드를 읽어 안전하다고 판정」한 것이었다. 이번에 **양쪽에서
정상 발화가 관측**됐다 → 🔴 **「비운다 → 곧 채운다」 전제가 깨지지 않았다.**

| 자리 | 실측 |
|---|---|
| `EnterRematchPreparingState()` 의 상태 줄 쓰기 | 에디터 **23:28:43.573** `StoppedBy=EnterRematchPreparingState` → **.574** 「재경기 준비 상태 진입 — 자동 복귀 타이머 정지 + 상태 줄 교체」(`LimitSeconds=20`) · 실기 **23:28:43.560** 같은 `StoppedBy` |
| `StopCountdown()` 의 텍스트 비우기 | 에디터 **23:29:00.182** `StoppedBy=RestartCountdownForOpponentLeft` → **.183** 재시작 `TotalSeconds=30` |

- ✅ **멀티 60초 정상** — 에디터 23:28:37.752 · 실기 23:28:37.874(`TotalSeconds=60`).
- ✅ **싱글 타이머 없음 유지** — 23시대 싱글 구간 **카운트다운 시작 0건**, 로비 복귀는 **버튼만**.
- ✅ **오류** — 에디터 `[ERROR]` **2건 = 투영 강제 실패뿐** · 실기 `[ERROR]` **0건**.
- 🔴 **투영 실패 뒤에도 `LoadMap` 이 계속 진행**(23:29:25.819 AI 초기화)됨이 실기로 확인됐다 —
  이 절 위쪽 「(2) 실패 뒤에도 `LoadMap()` 은 끝까지 진행한다」의 **C-계약 전제가 실측으로 뒷받침됐다.**

### 🔴 지난 회차 미검증 ⓐ~ⓔ — 닫힌 것과 여전히 열린 것 (원문은 위에 그대로 있다)

| 지난 항목 | 지금 |
|---|---|
| ⓑ **Unity 에디터 컴파일 미확인** | ✅ **닫혔다** — 사용자가 실제로 빌드해 돌렸고 로그가 남았다. ⚠️ **단, 「경고 0건」은 확인된 것이 아니다.** |
| ⓐ **삭제(52줄) 후 재검증 0건** | ⚠️ **부분적으로만 닫혔다** — 삭제를 포함한 빌드에서 같은 파일의 카운트다운·재경기 준비·이탈 재시작 경로가 정상 발화했다(위 표). **전 경로 재검증은 아니다.** |
| ⓒ setter 매 초 호출의 성능·깜빡임 | 🔴 **여전히 열림** — 로그에 남지 않는다. 측정 안 했다. |
| ⓓ 최초 실패 모달의 z-order·오버레이 | ⚠️ **사용자 육안 등급** — 「테스트절차대로 확인했고」 보고뿐이다. |
| ⓔ 팝업·문구·깜빡임은 로그에 자리가 없다 | 🔴 **여전히 유효한 한계다.** 이번 회차의 **화면 확인 4가지**(투영 실패 모달 · 결과 화면 되살아남과 상태 줄 · 승리/패배 글씨 · 씬 두 자리의 빈 칸)도 **전부 사용자 육안 등급**이다. |

⚠️ **승리/패배 글씨를 따로 적는 이유**: L 이 `ResultText` 의 씬 값을 비웠으므로, **런타임 대입
(`_resultText.text = isWin ? …`, `:1055`)이 유일한 공급원**이 됐다. 씬에 값이 남아 있을 때는 그 대입이
실패해도 뭔가 보였지만 **이제는 빈 칸이 된다.** 🔴 **자리표시 문자열을 지우는 것은 「보기 좋게 하는 일」이
아니라 폴백을 없애는 일이다** — 그래서 육안 확인 항목에 넣었다.

### 🔴 이번에도 미확인으로 남는 것 (지우지 말 것)

- ⓐ 🔴 **진짜 투영 실패는 발화 0회** — 강제 실패로만 봤다(격자 크기·헥스 방향이 어긋나야 나므로
  정상 경로에서 관측 불가).
- ⓑ 🔴 **`MapProjectionUseCase.Project` 의 실패 지점 전수 미계수** — 밖에서는 `IsSucceeded` 하나로
  합쳐지므로 **이번 방식은 실패 지점 개수와 무관하게 동작한다.** 그래서 세지 않았다.
- ⓒ 🔴 **경고 0건 미확인**(컴파일은 됐지만 경고는 보지 않았다).
- ⓓ **화면 확인 4가지는 사용자 육안**(위 표 참조).
- ⓔ 🔴 **싱글 투영 실패는 에디터에서만 확인** — 실기기 세션에서는 멀티만 돌았다.
- ⓕ ⚠️ **`.meta` 미생성** — 새 스크립트의 GUID 는 Unity 가 만든다.

---

## 🔴 「경기 종료 후 UI」 마무리 회차 — 오버레이 점유 누수 · 주석 사본 · 씬 자리표시 (2026-09-29 3차)

단일 소스(계획·검증 수치)는 `Assets/_Project/Docs/_Tasks/2026-09-29/16_16_post-game-ui-close-out/`
의 `Research.md` · `Plan.md` 다. 여기에는 **다음에 또 쓸 판단**만 적는다.

### (1) 🔴 씬 전환에서 파괴되는 팝업이 「공용 참조 카운터」를 점유한 채 사라지면 다음 씬이 막힌다

- 구조: 반투명 막(BlockingOverlay)의 소유자는 `UIManager` 하나이고 **켠 곳의 수를 카운터로 센다.**
  그 객체는 `DontDestroyOnLoad` 라 **씬이 바뀌어도 카운터가 초기화되지 않는다**(초기화는 `Awake()`
  한 곳뿐이고 로그인 씬에서 한 번만 돈다).
- 🔴 **그래서 「씬에 놓여 있어 씬 전환 때 파괴되는 팝업」은 `OnDestroy()` 에서 자기 점유를 놓아야 한다.**
  버튼으로 닫는 경로는 닫기 메서드가 놓아 주므로 **구멍은 「아무도 누르지 않고 씬이 바뀌는 경로」** 뿐이다
  (이번 자리는 자동 로비 복귀 타이머 만료).
- ✅ **판단 근거가 된 선례**: 같은 부류를 `GameEndUI.ReturnToLobby()` 가 이미 한 번 고쳤는데
  그 수정은 **`UIManager` 가 들고 있는 팝업만** 덮는다. 🔴 **「씬에 있는 팝업」과 「UIManager 가 든 팝업」은
  수명이 달라서 한쪽 수정이 다른 쪽을 덮지 않는다** — 고칠 자리를 고를 때 이 구분을 먼저 한다.
- 🔴 **두 곳에 흩어 넣지 않았다** — 로비 복귀 자리에 닫는 호출을 더하는 대안은 **그 한 경로만** 덮는다.
  `OnDestroy()` 는 모든 파괴 경로를 덮어 더 일반적이다(그 자리 주석도 「두 곳에 흩어지면 나중에
  한쪽만 고쳐진다」고 이미 경고하고 있었다).
- ⚠️ **순서**: 점유 해제를 Tween 정리보다 **앞**에 뒀다 — 같은 파일의 닫기 메서드가 「점유를 먼저 놓고
  연출을 정리」하는 순서라 **새 순서를 만들지 않았다.**
- ⚠️ **중복 호출 안전은 두 겹**이다(팝업 쪽 자기 점유 깃발 · 매니저 쪽 언더플로 검사). 그래서
  닫기 메서드로 이미 놓은 뒤에 파괴돼도 카운터가 두 번 줄지 않는다.
- 🔴 **과대 표기 금지 — 증상을 실기로 본 적이 없다.** 코드 판독뿐이라 보고를 「빠진 호출을 더했다」로
  적었다. 실기 재현 조건은 **요청 창이 뜬 쪽이 아무것도 누르지 않고 자동 복귀를 기다리는 것**이다.

### (2) 🔴 주석 사본 함정 — 7회째를 만들지 않은 방법은 이번에도 「절차」뿐이었다

- 🔴 **판정 기준을 「그 낱말이 나오는가」가 아니라 「따옴표로 감싸 값을 베꼈는가」로 못 박은 것이
  이번 회차의 핵심 판단이다.** 낱말로 세면 **기능을 설명하는 산문까지 지우게 되고**(과잉 수정),
  그것은 6회 재발과 **반대 방향의 사고**다. 실측 예: 파일 머리말 30줄 안에 버튼 라벨 낱말이 3건인데
  **따옴표 사본은 1건**이고 나머지 2건은 클래스 설명 산문이라 **지우면 설명이 사라진다.**
  ✅ **그래서 완료 조건을 「0」이 아니라 「2 가 남아야 정상」으로 적었다.**
- 🔴 **가리키는 방법은 부류마다 다르다**(6회가 좁혀 온 결론): 화면 문구·파라미터 예시는 **성격**으로,
  금지 판정의 대상이 되는 타입은 **성격 표현**으로, 마크업 태그는 **역할**로 가리킨다.
- ✅ **금지 토큰을 적어도 되는 유일한 자리는 「왜 그것을 쓰지 않았는가」를 적는 문장**이다 —
  이번에 고친 세 파일의 머리말에 그 한 줄을 함께 남겼다(본보기는 이미 올바르던 네 번째 파일).
- ✅ **쓰고 나서 돌렸다** — 주석을 고친 직후 그 회차의 검증 셈 전부(문구·가드 밖 타입 언급·값 상수
  4개·XML 태그 균형·과거 회차 토큰)를 다시 돌렸고 **새 사본 0건**이었다. 🔴 **「알고 있다」가 아니라
  이 절차가 막았다** — 6회 전부 아는 상태에서 밟혔다.
- ⚠️ **XML 태그 균형은 닫는 태그 기준으로 센다**(`<b`→`<br/>` · `<para`→`<paramref` 오탐).
  ⚠️ **`//` 줄 주석에는 `<b>` 를 쓰지 않는다** — 문서 주석이 아니라 렌더링되지 않고, 태그를 세는
  검사에만 걸린다. 이번에 한 번 썼다가 「」로 바꿨다.

#### 🔴 그다음 회차가 쓸 판정자 — 「사본을 고칠 값이 있는가」는 **값 자리 개수**로 가른다 (2026-09-29 3차 후속)

같은 파일 안의 사본이라도 **고쳐서 얻는 것이 있는지는 부류마다 다르다.** 기준 셋을 이 순서로 본다.

1. 🔴 **값 자리가 1곳인가** — 사본 1건을 지우면 그 문구를 세는 셈이 **정확히 1(값)** 이 된다. **고칠 값이 있다.**
2. ⚠️ **값 자리가 여러 곳인가** — 애초에 「한 곳만 있는가」 검증이 성립하지 않으므로 사본을 지워도
   **셈이 여전히 여러 건**이다. 효과가 낮다.
3. 🔴 **값 자리가 수십 곳인 넓은 낱말인가**(버튼 라벨로 흔한 두 글자 낱말 따위) — **검증 자로 쓸 수 없는 낱말**이라
   사본을 고쳐도 **효과 0**이다. 계획서가 「이 낱말의 코드 전체 grep 은 너무 넓어 검증 자로 쓰지 말 것」이라
   미리 못 박은 것과 같은 판정이다.
4. ✅ **주석 사본이 「왜 그것을 쓰지 않았는가 / 왜 그 값을 넣으면 안 되는가」의 단일 소스이면 지우면 뜻이 사라진다** —
   그 자리는 금지 토큰의 **유일하게 합법적인 자리**이므로 유지한다.

⚠️ **그래서 두 기준이 반대를 가리키는 자리가 생긴다** — 「같은 파일·같은 블록의 어투를 맞춰야 한다」(문서 품질)와
「고쳐도 셈이 안 변한다」(검증 효과)가 충돌하는 경우다. 🔴 **그때는 스스로 정하지 말고 사용자 판단으로 넘긴다**
(`CLAUDE.md` 규칙 12). 이번 회차에 **그 유형을 한 자리 남겨 보고했다**(같은 파일 위쪽 메서드의 파라미터 설명 두 줄).

**[🔴 2026-09-29 3차 후속 정정 — 위 1~4 는 지우지 않는다(B-7). 재는 방법 한 가지가 틀렸다]**
🔴 **「값 자리가 몇 곳인가」를 「그 낱말이 나오는 줄이 몇 줄인가」로 재면 크게 부풀려진다.**
같은 회차에 내가 버튼 라벨 세 낱말의 값 자리를 **수십 곳**으로 보고했으나, **따옴표 리터럴만 뽑아
다시 세니 각각 1곳 · 4곳 · 5곳**이었다(나머지는 전부 그 낱말이 들어간 **주석·식별자·다른 문장**이다).
🔴 **그래서 「효과 0」이라는 내 판정도 한 낱말에 대해서는 틀렸다** — 값이 1곳인 낱말은 위 기준 1에 해당해
**고칠 값이 있었다.**
✅ **재는 방법을 고정한다: `grep -c <낱말>` 이 아니라 「주석 줄을 걷어낸 뒤 따옴표 리터럴만 뽑아 세기」다**
(파이썬 한 줄짜리 추출이면 된다). 🔴 **낱말 줄 수는 값 자리 수가 아니다** — 이 프로젝트에서
「검증 자로 쓰기엔 너무 넓다」를 판단할 때도 같은 함정이 있다.

⚠️ **낫표(「」)로 버튼·상태를 이름처럼 가리키는 산문은 사본이 아니다** — 이 프로젝트 주석의 **기존 관례**이고
수십 자리에 있다. 🔴 **판정 대상은 「따옴표로 감싸 값을 베낀 것」**이며, 낫표 산문까지 세면 기능 설명을 지우는
**과잉 수정**이 된다(이 절 (2) 의 판정 기준과 같은 이유).

### (3) 🔴 「같은 문구 여러 벌」을 합치지 않는다는 판정은 **사건**으로 가른다

- 같은 뜻의 실패 안내가 **세 자리**에 있는데 합치면 안 되는 근거가 둘이다.
  ⓐ **마침표 유무가 사양이다**(상태 줄은 규칙 D-7 이 문장으로, 모달 본문은 규칙 M-4 가 마침표 없이 확정).
  ⓑ 🔴 **사건 자체가 다르다** — 한 자리는 **전투 씬에 들어가기 전**(접속·전송 단계, 규칙 M-2)이고
  나머지 둘은 **전투 씬 안**이다. 🔴 **서로 다른 규칙은 따로 개정되므로 합치면 「거짓 단일 소스」가 된다.**
- 🔴 **맨 리터럴을 상수로 올리는 것이 「합치기」의 첫 걸음**이라서 그 자리는 상수화하지 않고
  판정 주석만 남겼다(상수화 여부 자체는 범위 밖으로 남겼다).

### (4) ⚠️ 씬 자리표시 문자열을 비울 때

- 빈 값의 직렬화 형태는 `m_text:` 뒤 **공백 한 칸**이다(같은 씬에 이미 그 형태가 여러 자리 있다).
  🔴 **그 형태를 따르면 총 행수가 유지되고, 총 행수 유지가 「다른 직렬화가 함께 다시 써지지 않았다」의
  검증 수단**이 된다.
- 🔴 **되돌리기 값이 여러 자리에서 같은 문자열이면 값으로는 가릴 수 없다 — fileID 로 가른다.**
  그래서 고치기 전에 각 행의 **컴포넌트 fileID 와 `m_GameObject` fileID 를 둘 다 확인**했고,
  `_headerText` 배선이 같은 fileID 를 그대로 가리키는지도 함께 확인했다.
- 🔴 **자리표시를 지우는 것은 폴백을 없애는 일이다** — 런타임 대입이 유일한 공급원이 되므로
  **육안 확인 항목이 된다**(이 파일의 2026-09-29 2차 항목 L 이 같은 판정을 내렸다).

### (5) 🔴 색인(`MEMORY.md`) 압축 — 쪼개기가 아니라 「상세를 내리고 한 줄 포인터만 남기기」

- 실측: 색인 **347행 → 182행**(권고선 250 — C-11). 줄인 것은 **포인터의 길이**이고 **개수는 그대로 19개**다
  (링크가 빠진 토픽 파일은 존재하지 않는 파일이 된다 — C-10).
- 🔴 **내린 상세는 각 토픽 파일 맨 앞의 「이 파일이 담는 것」 절로 옮겼다** — 그 절이 그 파일의
  회차별 목차 역할을 하므로 **색인이 하던 안내가 사라지지 않는다.**
- ✅ **이동과 삭제를 가르는 검증은 폴더 총합 행수뿐이다**(E-16): **6,154 → 6,236**(+82, 순증).
  🔴 `--update-baseline` 은 회차 마지막에 메인 세션이 한 번만 돌린다 — 에이전트가 돌리지 않는다.
- ⚠️ **작업 방식은 `Read` → 행 범위를 잘라 옮기고 그 자리에 새 포인터를 넣는 치환**이며,
  🔴 **`Write` 로 색인을 통째로 덮어쓰지 않았다**(B-3 — `Write` 는 내용을 파괴하면서 성공한다).

### 🔴 (6) 실기 통과 — 판정은 「고쳤다」가 아니라 **「증상 0건」**이다 (2026-09-30 실측)

- 🔴 **끝까지 증상을 실기로 본 적이 없다.** 계획서 §3-1 이 완료 보고를 *「빠진 호출을 더했고 증상이
  관측되지 않았다」* 로 못 박았고, **실기 통과 뒤에도 그 문구가 그대로 옳다.** 「고친 버그」로 올려 적지 말 것 —
  올려 적으면 다음 회차가 「이미 검증된 수정」으로 취급해 회귀 감시를 빼먹는다.
- ✅ **누수가 나는 그 경로 자체는 실기에서 정확히 재현됐다**(기기 = Host): 자동 복귀 카운트다운 시작
  `23:01:18.266` → 재경기 요청 수신 `23:01:48.606`(= 요청 창이 떠 **오버레이를 점유한 상태**) →
  만료·로비 복귀 `23:02:18.528`. **60.262초**이고, 그 사이 **수락·거절 로그 0건**(기기 파일 전체 0건),
  만료 줄의 `RematchPreparing=` 도 거짓이다 → **응답 없이 떠 있는 채로 만료**된 것이 확정.
  즉 (1) 이 노린 경로가 맞고, 그 상태로 씬이 바뀌었는데 **막이 남지 않았다.**
- ✅ **양쪽 `[ERROR]` 0건**(에디터 2,508행 · 기기 139,067행, 내가 직접 재실측). `[WARN]` **8건**
  (에디터 7 · 기기 1)은 전부 연결 끊김 · 무반응 이탈 판정 경로의 **예상된** 것이다.
- 🔴 **그런데 「막이 없었다」를 증명한 것은 로그가 아니다** — 다음 항 (7).

### 🔴 (7) 이 회차의 확인 등급은 **육안**이다 — 팝업 두 개에 로그 호출이 0건이라서

- 실측: `RematchRequestPopup.cs` · `ConfirmPopup.cs` 의 로그 호출(`GameLog.` / `Debug.Log`) **각 0건.**
  🔴 그래서 ⓐ 팝업이 닫혔는가 ⓑ 오버레이 막이 있었는가 ⓒ 건물 창 제목 네 개가 정상인가 — **세 가지 전부
  사용자 육안 확인**이고 로그에는 한 줄도 남지 않는다. 이번 근거는 **사용자가 로비에 막이 없음을 화면으로
  확인해 준 것** 뿐이다.
- 🔴 **다음에 이 자리를 또 검증해야 하면 착수 전에 정해라: 「로그 한 줄을 넣을 것인가, 육안으로 남길 것인가」.**
  로그를 넣으면 회귀를 기계로 잡을 수 있지만, 팝업 표시/해제는 사용자 조작마다 나는 사건이라
  LogRules 의 「상태 전이만 · 매 틱 금지」를 통과할 설계가 따로 필요하다(이번 회차에는 넣지 않았다 — 범위 밖).

### 🔴 (8) 「이 로그가 새 빌드의 것인가」를 코루틴 클래스 번호로 가르는 기법의 **적용 한계**

- 이 프로젝트는 스택 트레이스에 찍히는 컴파일러 생성 코루틴 클래스 번호(`<CountdownCoroutine>d__NN`)로
  빌드 세대를 갈라 온 적이 있다. 🔴 **이번에는 그 기법이 아무것도 가르지 못했다.**
- 실측: 이번 기기 로그 **`d__61`** · **직전 회차 기기 로그도 `d__61`**(2026-09-29 23:33).
  그보다 앞선 로그는 `d__49` · `d__59`(2026-09-29 13:17)로 **달랐다.**
  🔴 **번호는 그 코루틴을 가진 클래스(`GameEndUI`)의 멤버 순서가 바뀔 때만 바뀐다** — 그 회차에는
  `GameEndUI` 를 고쳤으니 번호가 움직였고, 이번 수정은 `RematchRequestPopup` 한 파일이라
  **번호가 움직일 이유가 애초에 없었다.**
- 🔴 **절차로 고정한다: 번호로 빌드를 가르려면 먼저 「이번에 고친 클래스가 그 번호의 주인인가」를 확인한다.**
  주인이 아니면 그 번호는 빌드 판별에 **쓸 수 없다.** 같은 번호를 보고 「구 빌드다」로 판단하면
  **멀쩡한 실기 결과를 무효로 돌리는** 정반대 결론을 낸다.
- ✅ **대신 쓸 수 있는 것**: 고친 파일이 **로그를 남기는 클래스라면** 그 줄의 유무·필드 변화가 판별자가 된다.
  🔴 **이번처럼 고친 파일의 로그가 0건이면 로그만으로 빌드를 가를 수 없다**((7) 과 같은 뿌리) —
  그때는 빌드/설치 시각 같은 **로그 밖 근거**로 가르고, **가르지 못했다는 사실을 보고에 적는다.**
- ⚠️ **읽는 법 함정**: 스택 프레임은 `네임스페이스.<코루틴이름>d__NN:MoveNext()` 꼴이라 **그 코루틴을 가진
  클래스 이름이 찍히지 않는다.** 주인은 **같은 시각의 이웃 프레임**(`GameEndUI:ReturnToLobby()` 등)과
  로그 태그로 확정해야 한다 — 번호만 보고 주인을 단정하면 위 판정이 처음부터 무의미해진다.

### ✅ (9) 버그를 찾는 수단으로 실제로 통한 것 — 「규칙이 보장한다고 적은 것을 코드가 지키는지 대조」

- 이번 회차의 유일한 진짜 버그는 **규칙 D-6 본문이 「응답하지 않고 두어도 화면에 갇히지 않는다」를
  보장으로 적고 있는데 그 전제를 깨는 자리**를 찾아 나온 것이다.
  🔴 **규칙 문서의 「보장한다 / 절대 ~하지 않는다」 문장은 그 자체가 테스트 명세다** — 문장마다
  「코드의 어느 줄이 이것을 지키는가」를 짚으면 **지키는 줄이 없는 자리**가 드러난다.
  코드에서 출발해 버그를 뒤지는 것과 달리 **대상이 유한해서 끝이 있다.**
- ✅ **두 번째로 통한 것: 형제 비대칭.** 같은 부류가 `GameEndUI.ReturnToLobby()` 에서는 이미
  공용 팝업을 닫는 것으로 해결돼 있었는데 **이 팝업만 빠져 있었다.**
  🔴 **「같은 계열 중 하나만 처리돼 있으면 나머지를 의심한다」** — 이 파일 위쪽
  「Open defect found while doing this」 항목이 **해결된 쪽의 기록**이고, 그것을 읽은 것이 빠진 쪽을 찾은 길이었다.

---

## 🔴 두 팝업의 관측 공백을 메운 회차 — 요청 팝업 · 확인/알림 팝업 · 막 점유 수 (2026-09-30)

단일 소스(계획·완료 조건·기준선 B1~B12)는
`Assets/_Project/Docs/_Tasks/2026-09-30/15_01_popup-log-coverage/Plan.md` 다.
로그 규칙 쪽 판정(키 0개 · 축 B · 심각도 · 새 필드 목록)은 [logging.md](logging.md) 의
같은 날짜 절에 적었다. 여기에는 **UI 쪽에서 다음에 또 쓸 판단**만 적는다.

### (1) 🔴 「막이 안 꺼졌다」를 로그로 가르려면 **팝업 쪽 줄과 관리자 쪽 줄이 둘 다** 있어야 한다

- 팝업 쪽 줄은 **「나는 놓았다」**까지만 말한다. **「그런데도 막이 안 꺼졌다」**는 말해 주지 못한다 —
  점유 수는 `UIManager` 안에만 있고 **밖에서 읽을 수단이 없다**(비공개, 읽기 접근자 없음).
- 관리자 쪽 줄만으로도 부족하다. **경계에서만** 남기므로 「안 줄었다」가 **줄의 부재**로 드러나고,
  🔴 **부재로 하는 판정은 약하다.**
- ✅ **그래서 두 줄을 함께 읽는다** — 팝업이 「놓았다」를 남겼는데 관리자 쪽 「꺼졌다」가
  따라오지 않으면 **그 시점에 다른 점유자가 남아 있었다**가 확정된다.
- 🔴 **스팸을 막는 자리는 관리자 쪽 하나뿐이다.** 그 막은 건물·생산·스킬 패널과 배치 UI ·
  설정 메뉴 · 로그인 팝업들이 함께 쓰고(선언·관리자·이번 두 팝업을 뺀 실사용 파일이
  표시 **7** · 해제 **9**), 게임 중 반복해서 열고 닫힌다. → **경계에서만** 남긴다.

### (2) 🔴 확인/알림 팝업의 닫기가 **점유한 적 없는 몫을 놓는다** — 이번에 고치지 않고 「보이게」만 했다

- 요청 팝업에는 자기 점유 여부를 기억하는 깃발이 있어 **점유 중일 때만** 놓는다.
  🔴 **확인/알림 팝업에는 그 깃발이 없고 닫기가 조건 없이 점유 해제를 부른다.**
  그 팝업이 떠 있지 않았는데도 닫기가 불리면 **다른 팝업의 몫이 한 칸 줄어든다**
  (관리자 쪽 언더플로 검사가 음수만 막는다).
- **그 경로가 실제로 있다** — 결과 화면이 로비로 나가는 자리가 **조건 없이** 그 닫기를 부른다.
  그 자리 주석의 *「떠 있지 않았다면 아무 일도 일어나지 않는다」* 는 **팝업 본체에는 맞고
  점유 수에는 맞지 않는다.**
- 🔴 **그래서 직전 회차(파괴 시 점유 해제 추가)의 「증상 0건」은 근거 강도가 낮았다.**
  만료 경로가 로비 복귀 자리를 **반드시 지나므로**, 점유가 0이 된 것이
  **새로 넣은 파괴 시 해제 때문인지 이 조건 없는 해제 때문인지 구별되지 않았다.**
- ✅ **이번 회차가 그 질문을 닫는 수단을 만들었다** — 닫기 줄에 **닫히기 직전에 실제로 떠 있었는가**를
  싣고, 그 뒤에 관리자 쪽 경계 줄이 따라오는지를 본다. 🔴 **고치는 것은 별건이고 사용자 확정 사항이다**
  (근거 없이 고치지 않으려고 먼저 보기로 했다).

### (3) ✅ 규칙 D-5 구조를 **화면 문구를 한 자도 적지 않고** 확인하는 법

- 그 팝업의 표시 메서드가 받는 인자는 **셋 다 화면에 그대로 뜨는 글자**다. 그것을 기록에 실으면
  「그 글자가 코드에 한 곳(상수)에만 있는가」를 세는 검사가 오염된다.
- 🔴 **대신 구조 두 개를 참/거짓으로 싣는다** — 제목 자리가 보이는가 · 두 번째 버튼이 숨겨졌는가.
  그 둘이 곧 규칙 D-5 가 정한 구조(타이틀 + 본문 + 버튼 1개)다.
- ✅ **「시켰다」가 아니라 「그렇게 됐다」를 읽는다** — 인자 값이 아니라
  **적용 뒤의 오브젝트 상태를 되읽어** 싣는다. 그러면 Inspector 배선이 빠져 아무 일도 일어나지
  않은 경우가 그대로 드러난다(이 팝업의 제목 자리는 미배선이어도 조용히 넘어가는 구조다).
- ⚠️ **이 줄만으로는 두 알림 자리(규칙 D-6 이탈 알림 ↔ 규칙 M-3·M-4 맵 실패 알림)를 가를 수 없다.**
  가르려면 화면 글자를 싣거나 **공개 시그니처에 표식을 더해야** 해서 둘 다 못 한다.
  → **앞뒤 줄로 읽는다**(두 자리 모두 자기 기록을 이미 남긴다).

### (4) ✅ 주석 사본 함정 — **8회째를 만들지 않았다.** 이번에도 막은 것은 절차다

- 🔴 **이번 회차의 위험은 「새로 쓰는 문자열 리터럴」 쪽이었다.** 로그 메시지에
  「알림 팝업을 …」처럼 **화면 문구 낱말을 품은 리터럴**을 두 자리 썼고,
  **쓴 직후 셈을 돌려** 코드 리터럴 안의 그 낱말이 **1 → 3** 으로 늘어난 것을 잡았다.
  → **성격 표현**(「고를 것이 없는 한 버튼 팝업」 · 「두 갈래 중 고르는 팝업」)으로 바꿔
  **기준선 1 로 되돌렸다.**
- 🔴 **판정 기준: 사본인가 아닌가를 따지기 전에, 새로 쓰는 글에서는 그 낱말을 아예 피하는 것이 싸다.**
  기존 산문은 건드리면 과잉 수정이지만(2026-09-29 3차의 결론), **내가 새로 쓰는 글은 선택이 자유롭다** —
  같은 뜻을 성격 표현으로 쓰면 **셈이 한 칸도 움직이지 않는다.**
- ✅ **식별자 사본 0건** — 점유 헬퍼 · 상태 깃발 · 막 표시/해제 메서드 이름을 새 주석에 한 번도
  적지 않았다(전부 「역할」로 가리켰다). ⚠️ **남은 3건은 착수 전부터 있던 것**이고 수치가 그대로다.
- ✅ **`//` 줄 주석에 마크업 태그 0건** · XML 태그 균형 3파일 전부 일치 · 중괄호/괄호 균형 일치.
- 🔴 **검사의 음성 통제를 1회 돌렸다** — 사본 1건·태그 1건·중괄호 1건을 일부러 깨뜨린 사본에서
  세 검사가 전부 걸리는 것을 확인했다(「통과했다」와 「실패를 잡는다」는 다른 명제다).

### (5) ⚠️ 이 회차가 확인하지 못한 것 · 범위 밖으로 남긴 것

- 🔴 **Unity 컴파일 미확인**(세 파일 모두 `UnityEngine`·TMP·DOTween 의존 → 헤드리스 불가).
- 🔴 **실기 관측 0건** — 만든 것은 **관측 수단**이다.
- ⚠️ **기존 주석 사본 1건을 발견했으나 고치지 않았다** — 알림 팝업의 본문 문구를
  **따옴표로 감싼 예시**가 관리자와 그 계약 파일의 문서 주석에 **각각 1건** 있다.
  🔴 **착수 전부터 있던 것이고, 값 자리가 두 곳이라 「한 곳만 있는가」 검증 자체가 성립하지 않는다**
  (2026-09-29 3차의 판정 기준 2 — 고쳐도 셈이 여러 건으로 남아 효과가 낮다).
