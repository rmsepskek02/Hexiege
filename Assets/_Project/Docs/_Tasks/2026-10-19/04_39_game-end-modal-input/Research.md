# 게임 종료 결과 화면의 배경·입력 차단 — Research

게임이 끝난 뒤에도 뒤쪽 타일이나 설정 메뉴를 누를 수 있는 문제를 조사한다. 결과 화면에는 기존 공용 반투명 배경을 재사용하고, 그 위에 승패·안내 상태 문구·버튼을 모두 표시해 결과 화면에서 필요한 조작만 할 수 있게 하는 것이 목적이다. 결과 화면 바깥을 눌러도 닫히지 않고, 재경기를 시작하면 정상 게임 입력으로 돌아와야 한다.

> **현재 단계 (2026-10-19):** 사용자 6항목 정상 확인으로 해당 싱글·멀티 결과 화면 범위를 수용했고, 승인된 대체 주석 정리·최종 별도 컴파일·해시 검토도 완료했다(§7·§8). 게시 상태는 Git과 메인 최종 보고로 별도 확인한다. 에이전트 native/QA 실행과 사용자 실기를 구분하며 장치·정확한 역할·로그 측정까지 확대하지 않는다.

## 1. 사용자 확정 요청과 정정

| ID | 확정 내용 |
|---|---|
| U1 | 기존 `UIManager.BlockingOverlay` 공용 반투명 배경을 재사용한다. 별도 결과 전용 반투명 배경을 새로 만들지 않는다. |
| U2 | 승리/패배, 안내 상태 메시지, 결과 버튼을 포함한 전체 결과 UI를 공용 배경보다 위에 표시한다. |
| U3 | 결과 표시 중 뒤쪽 타일·설정 UI·랠리 지정·스킬 조준/발동 입력을 차단한다. |
| U4 | 결과 화면은 배경 탭으로 닫히지 않는다. 명시적 결과 버튼으로 진행한다. |
| U5 | 재경기 시작 시 결과 화면과 입력 차단을 해제하고 정상 게임 입력으로 복원한다. 실패 시 결과 화면 복원도 누락되지 않아야 한다. |
| U6 | 기존 중첩 팝업·로딩·재경기/로비 복귀 timer 정책을 유지한다. |

**배경 존재 여부 정정:** 최초 「반투명 배경이 있다」는 설명 뒤 사용자가 재확인해 「없다」고 정정한 것이 확정 상태다. 이를 미결 질문으로 다시 열지 않는다. 실제 싱글/멀티 발생 환경·역할·장치 정보는 아직 보고되지 않았다.

**실행 진입 경로 확정:** 사용자는 로그인·로비를 거쳐 게임에 진입했다고 확인했다. 이번 작업은 그 정상 진입 경로의 결과 화면을 대상으로 한다. 직접 Game 씬 진입을 위한 UIManager 생성 등 지원 확장은 범위 밖이며, 공용 호출의 기존 null-safe 계약은 유지한다.

## 2. 조사 기준과 관련 원본

위치는 파일·클래스·메서드 중심으로 기록한다. 씬의 fileID는 직접 확인한 직렬화 참조이며 실행 화면의 증거가 아니다. 코드로 확인한 흐름은 정적 분석이고, 사용자 입력 누수의 실행 원인을 확정한 것은 아니다.

| 원본 | 이번 판단에 쓰는 계약 |
|---|---|
| `GameSystemRules_UI.md` 「공통 UI 규칙」 규칙 4·5 | 공용 배경은 UIManager 단일 소유·Canvas 직속, CanvasGroup 표시/숨김, null-safe 호출, Show/Hide 점유 짝 유지 |
| 같은 절 규칙 8·9·10 | Popup/Modal 구분, Modal의 배경 탭 닫기 금지, 중첩 시 하위 입력 차단·LIFO·공유 배경 |
| `GameSystemRules_CanvasSortingOrder.md` 「전역 SortingOrder 대역 규칙」 및 「새 Canvas 추가 시 규칙」 규칙 1 | HUD 0 < 공용 배경 100 < 결과 등 패널 200 < 모달 250 < 로딩 300, Canvas Override와 GraphicRaycaster 동반 |
| `GameSystemRules_MatchFlowUI.md` 「공통 UI 규칙」 M-3·M-4·D-2·D-3·D-4·D-5·D-6·D-7 | 재경기 실패 복원, 싱글 countdown 없음, 멀티 평시 60초·이탈 시 30초, 재경기 버튼/로비 버튼 정책, 상태 줄과 모달·로딩의 역할 |
| `.claude/MEMORY.md` 「아키텍처 핵심 제약」 | GameBootstrapper 조합 루트, Presentation의 Netcode 직접 사용 방지, UIManager null-safe·직접 씬 진입 대응 |

세부 사양은 위 원본을 따른다. 최초 Task 작성 승인은 규칙·메모리·씬 수정 승인이 아니었으며, 이후 사용자 수용·갱신 승인은 §7에서 구분한다.

## 3. 구현 전 코드·씬 조사 기록

### 3-1. GameEndUI는 결과 패널만 표시하고 공용 배경을 얻지 않는다

- 파일: `Assets/_Project/Scripts/Presentation/UI/GameEndUI.cs`.
- `OnGameEnd`: 승패 텍스트와 색상을 설정한 뒤 `_panel?.Show()`를 호출하고 `Time.timeScale = 0f`로 둔다. 이 표시 경계에서 `ShowBlockingOverlay`를 호출하지 않는다.
- `RestoreResultScreenAfterMapLoadFailure`: `_panel?.Show()` 후 싱글 실패 상태 줄을 표시한다. 공용 배경 획득 호출은 없다.
- `Hide`: `StopCountdown()`과 `_panel?.Hide()`를 호출한다. `OnGameStarted`도 `Hide()`를 호출한다. 공용 배경의 자체 점유·반납 경계는 현재 없다.
- `ReturnToLobby`: 결과 Hide, 확인/알림 닫기, 로딩 표시의 기존 순서를 사용한다. 이 순서와 중첩 overlay 참조 카운터 관계를 보존해야 한다.

`timeScale=0`만으로 입력 차단을 보장할 수 있다는 근거는 없다. 아래 입력 코드가 별도의 Update·UI 이벤트 경로를 사용하기 때문이다.

### 3-2. 씬의 결과 화면에는 자체 배경 참조도 없다

- 파일: `Assets/_Project/Scenes/Game.unity`.
- `GameEndPanel` GO fileID `1309749079`: 전체 화면 앵커 `(0,0)`~`(1,1)`이며 결과 텍스트·상태 줄·다시하기·로비 버튼 참조가 같은 결과 구성에 묶여 있다.
- `GameEndUI` fileID `1309749081`의 `_panel`은 `1309749082` AnimatedPanel을 가리킨다. 이 AnimatedPanel의 `_backgroundOverlay`는 `{fileID: 0}`이다.
- 같은 GO의 Canvas `8000000007`은 OverrideSorting=true·SortingOrder=200이고, `8000000008` GraphicRaycaster가 함께 있다.

전체 화면 RectTransform·CanvasGroup·GraphicRaycaster가 있다는 사실만으로 빈 영역 전체가 raycast를 받는 Image라고 판정하지 않는다. 현재 정적 구성과 사용자 「배경 없음」 정정은 일치하지만 최종 화면·실제 터치 분포는 실행 확인하지 않았다.

### 3-3. AnimatedPanel의 Show는 미연결 배경을 대신 만들지 않는다

- 파일: `Assets/_Project/Scripts/Presentation/UI/Common/AnimatedPanel.cs`.
- `EnsureInitialized`: CanvasGroup을 확보하고 alpha=0·blocksRaycasts=false·interactable=false로 초기화한다.
- `Show`: 기존 tween을 중지하고 `IsVisible=true`로 둔다. `_backgroundOverlay != null`일 때만 그 CanvasGroup을 보이게 한다. UIManager의 공용 배경을 자동으로 획득하지 않는다.
- 따라서 GameEndPanel의 배경 미연결은 이 컴포넌트가 보완하지 않는다. 공용 배경 재사용 여부는 결과 UI의 명시적 생명주기에서 다뤄야 할 조사 지점이다.

### 3-4. 입력은 화면 raycast보다 먼저 처리되는 경로도 있다

- 파일: `Assets/_Project/Scripts/Presentation/Input/InputHandler.cs`.
- `Update`: 카메라 미주입 가드 뒤 마우스·터치 press/release를 받아 `HandleClick`으로 보낸다. 이 메서드에 게임 종료 상태를 이유로 return하는 공통 가드는 없다.
- `HandleClick`: 스킬 조준 중 return, 랠리 지정 분기를 **`IsPointerOverUI`보다 먼저** 처리한다. 따라서 뒤쪽 GraphicRaycaster 차단만 추가해도 이미 켜진 랠리 지정의 완료 호출이 자동 차단된다고 단정할 수 없다.
- 파일: `Assets/_Project/Scripts/Presentation/Input/SkillAimController.cs`. 별도 `Update`에서 조준 입력을 처리하고 `CancelAim`을 제공한다. 이 경로도 일반 맵 클릭 하나만 막는 설계로 충분한지 확인해야 한다.
- 파일: `Assets/_Project/Scripts/Presentation/UI/InGameSettingsUI.cs`. `Show`는 공용 배경을 Popup 모드로 획득한다. 맵 입력과 별도인 UI 버튼 경로도 결과 표시 중 입력 차단 대상이다.

### 3-5. 기존 게임 종료 정리 경로는 유지할 대상이다

- 파일: `Assets/_Project/Scripts/Presentation/UI/GameUIManager.cs`.
- `NotifyGameEnded`: 등록 UI에 `OnGameEnded()`를 호출하되 GameEndUI는 결과 표시 대상이므로 제외한다. 결과 표시는 GameEndUI 자체 OnGameEnd 구독이 맡는다.
- `InGameSettingsUI.OnGameEnded`는 설정을 닫는 기존 경로다. 생산·스킬 등 각 패널의 닫기/조준 정리와 결과 배경 획득의 이벤트 순서가 맞는지는 별도 검토가 필요하다.
- 결과 표시 후 배경 클릭으로 새 UI를 여는 문제와, 종료 직전 열린 UI의 정리 문제를 구분해 실제 경계를 확인한다.

### 3-6. UIManager 공용 배경의 참조 카운터·콜백은 별도 주의점이다

- 파일: `Assets/_Project/Scripts/Presentation/UI/UIManager.cs`.
- `ShowBlockingOverlay(Action onTap = null)`: 참조 카운터를 증가시키고 현재 버튼 listener를 지운 뒤, 콜백이 있으면 새 listener를 등록한다. 콜백 없는 Modal 모드는 배경 탭을 응답 버튼으로 사용하지 않는다.
- `HideBlockingOverlay`: 카운터가 양수면 감소시키며, 아직 다른 점유자가 있으면 반환하고 0일 때 실제 숨김·listener 정리를 수행한다.
- 이 API는 호출자 소유 토큰으로 반납자를 구별하지 않는다. 미보유자의 legacy Hide가 다른 UI의 참조를 소비할 위험과 중첩 닫힘 뒤 이전 callback 복원 부재는 기존 공유 API의 별도 문제다. 이번 결과 UI 보강의 소유·입력 차단 설계와 혼동해 전체 API 재설계로 확대하지 않는다.

## 4. 구현 전 판단과 계획 연결

확인된 공백은 결과 표시/복원 경계에 공용 배경 사용이 없고, 씬의 AnimatedPanel에도 배경이 연결되지 않았다는 점이다. 입력에는 UI raycast 이전 랠리 처리·독립 스킬 Update·설정 UI 경로가 있으므로 배경 시각 표시와 모든 입력 차단은 별도 축으로 설계해야 한다. 이 설명은 정적 근거이며 실제 관찰된 누수 전체의 유일 원인을 입증한 것은 아니다.

programmer 최종 인계를 반영한 [Plan](Plan.md)에 결과 전용 보호 lease, 게임 입력 gate, 랠리·스킬·카메라·설정 차단, 재경기/실패/로비/파괴 생명주기와 상위 모달 중 결과 버튼 차단을 정리했다. 기존 `GameUIManager.Initialize` → `GameEndUI.Initialize` 구독 순서를 보존해 종료 gate·기존 UI 정리가 결과 표시보다 먼저 실행되게 한다. 이 절은 실기 미검증이던 구현 전 조사이며 구현 결과는 §6, 이후 사용자 수용은 §7을 따른다.

## 5. 미확인 항목과 범위 제한

| 항목 | 현재 근거·미확인 사유 |
|---|---|
| 싱글/멀티·역할·장치별 재현 | 사용자 발생 환경 답변이 없다. 코드 공통 처리와 특정 환경 실기 재현을 합치지 않는다. |
| 실제 누수 경로·입력 순서 | 정적 경로만 확인. 종료 직전 입력·구독 순서·pointer release 타이밍은 실행/로그 미수집. |
| 최종 UI 시각·전체 화면 터치 차단 | 씬 직렬화 대조만 수행. Unity 화면·raycast 실행 확인 없음. |
| 종료 중첩 팝업과 timer 회귀 | 구현·별도 컴파일과 사용자 6항목 정상 보고를 구분한다(§6·§7). 정확한 초 단위 로그 계측까지 확인한 것은 아니다. |
| 직접 Game 씬 진입의 UIManager null | 사용자는 로그인·로비 경유를 확인했다. 직접 Game 씬 진입 지원·UIManager 생성은 이번 범위 밖이며 기존 null-safe 계약만 유지한다. |

최초 조사·구현 인계 단계에서 문서 담당은 새 Testcase·로그·메모리·상시 규칙·소스·씬을 만들거나 수정하지 않았다. `Research.md`를 먼저 작성하고 programmer 최종 설계 인계를 반영해 `Plan.md`를 작성했다. 구현 결과는 §6, 이후 사용자 수용과 승인된 상시 문서·메모리 갱신은 §7 이후의 마감 단계로 구분한다. 수동 Unity meta 파일은 생성하지 않았다.

## 6. 승인 구현 반영·별도 컴파일 완료

### 6-1. 실제 반영과 설계 보강

사용자는 별도 브랜치 없이 main에서 구현하도록 명시적으로 승인했다. game-programmer가 Plan의 기존 C# 파일을 수정했고 이후 추가 수정 없이 최종 인계를 전달했다. 문서 담당은 실제 코드의 다음 경계를 대조했다.

- `UIManager.AcquireBlockingOverlayModal`: owner ID 집합과 멱등 IDisposable로 결과 배경 점유를 legacy 참조 수와 분리했다. 실제 표시는 두 점유의 OR이고 보호 점유가 있으면 배경 tap callback을 실행하지 않는다.
- `GameEndUI.ShowResultWithInputBlock`·`ReleaseResultOverlay`: 결과 표시/실패 복원은 같은 표시 경계를 쓰고 lease 하나만 보유한다. 버튼 GO별 CanvasGroup은 상위 `HasForegroundModal`에 따라 입력만 잠그며 기존 Button.interactable 정책을 유지한다. 파괴 시 상태 이벤트 구독과 자신의 결과/팝업 참조만 정리한다.
- `GameUIManager.NotifyGameEnded`·`NotifyGameStarted`: 종료 정리 앞에서 입력을 차단하고 새 경기 UI 정리 끝에서 해제한다. 독립 GameEndUI 구독과 Bootstrapper의 manager→결과 초기화 순서는 유지했다.
- `GameplayInputBlockedChanged`를 동기로 발행하고 Bootstrapper가 pointer·카메라 gesture를 즉시 비우며 차단 전환에서 스킬 조준을 취소한다. 종료→시작이 같은 프레임이어도 Update가 중간 상태를 못 봐 입력 기억을 남기는 틈을 줄이는 보강이며 승인된 입력 복원 목적 안의 변경이다.
- `InputHandler`·`SkillAimController`·`CameraController`·`InGameSettingsUI`에 주입된 공통 판정과 HUD 설정 버튼 제어를 반영했다. 프로그램 카메라 배치와 정보 표시는 유지한다.
- `ConfirmPopup`·`RematchRequestPopup.IsOverlayHeld`와 Loading의 실제 raycast 차단을 상위 Modal 판정에 사용한다. `BlockingUiStateChanged`로 결과 버튼을 동기 갱신한다.
- `UIManager.ClearResultModalSource`는 popup identity가 일치할 때만 참조를 지운다. 옛 GameEndUI가 파괴될 때 새 경기 팝업 참조를 지우지 않도록 한 경계다.

기존 대체 로직은 사용자 검증 전 주석 비활성화 상태로 보존했다는 인계를 받았다. 이 구현 단계에서 테스트 수용 후 최종 삭제를 완료했다고 쓰지 않는다. 새 소스 파일은 추가하지 않았고 씬·프리팹은 수정하지 않았다. 기존 결과 Canvas 200·공용 배경 100·상위 Modal 250·Loading 300을 사용하는 계획이다. 실제 렌더링/입력 차단의 실행 성공은 아직 확인하지 않았다.

### 6-2. 변경 파일 목록

다음은 Plan과 최종 SHA manifest에서 대조한 구현 파일 목록이다. 경로는 `Assets/_Project/Scripts/` 기준이다.

| 영역 | 변경 파일 |
|---|---|
| 조합 루트 | `Bootstrap/GameBootstrapper.Setup.cs`, `Bootstrap/GameBootstrapper.Map.cs` |
| 입력·카메라 | `Presentation/Input/InputHandler.cs`, `Presentation/Input/SkillAimController.cs`, `Presentation/Camera/CameraController.cs` |
| 공용 UI·모달 | `Presentation/UI/UIManager.cs`, `Presentation/UI/Core/IUIManager.cs`, `Presentation/UI/ConfirmPopup.cs`, `Presentation/UI/Common/RematchRequestPopup.cs` |
| 경기 UI | `Presentation/UI/GameEndUI.cs`, `Presentation/UI/GameUIManager.cs`, `Presentation/UI/GameHudUI.cs`, `Presentation/UI/InGameSettingsUI.cs` |

구현 목록은 합계 13파일이다. Task 문서 변경은 같은 폴더의 `Research.md`·`Plan.md`이며 코드 파일과 구분한다. 외부 설정·meta 등 다른 작업의 변경은 이 목록에 포함하지 않는다.

### 6-3. 컴파일·최종 소스 검토 근거와 한계

| 항목 | 최종 인계·메인 독립 확인 |
|---|---|
| Runtime Roslyn 별도 컴파일 | exit 0, 오류 0·경고 23 |
| Editor Roslyn 별도 컴파일 | exit 0, 오류 0·경고 0 |
| 최종 소스 해시 | 메인이 manifest와 실제 13파일을 대조해 일치 13·불일치 0 확인 |
| 소스 공백 검사 | 메인의 `git diff --check` exit 0 |
| Unity/native·빌드·QA·사용자 실기 | 구현 인계 당시 모두 NOT RUN. 이후 사용자 실기 수용은 §7이며 에이전트 native·빌드·QA 실행으로 바꾸지 않음 |

별도 컴파일은 기존 참조를 사용하는 `dotnet "C:/Program Files/dotnet/sdk/9.0.306/Roslyn/bincore/csc.dll" @.utmp/rally_compile/rally.rsp` 및 `@.utmp/rally_compile/rally-editor.rsp`로 실행했다는 programmer 인계다. 메인은 최종 로그를 재계수해 위 오류·경고 수를 확인했다. 문서 담당은 컴파일을 재실행하지 않았다.

임시 근거는 `.utmp/rally_compile/game-end-runtime.log`, `game-end-editor.log`, `game-end-source-hashes.json`이며 영구 보존 근거 링크가 아니다. 주석 정리 전 구현 인계의 UIManager SHA256은 `3B671DAA66E2A4D57A277784302A8E8051F6F8F1A0C2B4D2D049EE4DE1F5704E`다. 문서 담당도 manifest의 파일 목록·해당 값을 읽었으며, 실제 전체 해시 일치 판정은 메인 전달값이다. 수용 후 정리 세대는 §8에서 구분한다.

로그인·로비 경유의 결과 화면을 대상으로 구현했다. 직접 Game 씬 지원 확장·역할별 재현 판정·기존 callback stack 전면 이행은 하지 않았다. 후속 사용자 실기 수용과 정리 상태는 §7을 따른다.

## 7. 사용자 6항목 정상 확인·수용과 후속 정리

사용자는 안내받은 「6가지 모두 테스트했고 정상」이라고 보고하고 문서 업데이트와 push를 승인했다. 다음은 안내 항목에 대응한 **사용자 보고 범위**이며 에이전트가 실행하거나 로그로 재검증한 결과가 아니다.

| # | 사용자에게 안내한 확인 범위 | 보고 결과 |
|---|---|---|
| 1 | 싱글 승패 결과·공용 배경, 뒤 타일/건물/설정·카메라·배경 탭 차단 | 정상 |
| 2 | 종료 직전 랠리 지정·스킬 조준의 취소와 입력 차단 | 정상 |
| 3 | 싱글 재경기 입력 복원과 로비 이동 시 잔여 차단 없음 | 정상 |
| 4 | 멀티 양쪽 입력 차단, 요청/거절 중 결과 버튼 차단, 수락 후 정상 진행 | 정상 |
| 5 | 멀티 상대 이탈 안내·timer, 재경기 비활성·로비 이동 가능 | 정상 |
| 6 | 에디터 싱글 맵 준비 강제 실패 후 결과 복원·재시도/로비 | 정상 |

이 범위의 결과 화면 입력 차단·복원 작업은 사용자 확인으로 수용한다. 장치명·정확한 Host/Client 배치·프레임/초 단위 로그·세부 시각 측정은 미확정이다. 사용자 에디터 강제 실패 확인을 에이전트 native 실행으로 바꾸지 않는다. 문구·timer의 기존 정책을 변경한 것도 아니다.

game-programmer는 승인 Plan과 사용자 수용에 따라 **이번에 대체한 UIManager·GameEndUI 주석만** 최종 제거했다. Editor/Player의 Roslyn RawKind·정확한 token Text가 정리 전후 동일하고 리터럴 보존 방식의 사본 mutation 검증도 완료했다. 정리 후 최종 컴파일·해시 확인은 §8을 따른다. 정리 이후 신규 사용자 실기를 뜻하지 않는다. 새 Testcase·로그·Task를 만들지 않고 외부 설정·meta는 보존한다.

## 8. 수용 후 대체 주석 정리·최종 소스 검토

이번 작업에서 대체된 UIManager 주석 49행·GameEndUI 주석 4행만 제거했다는 인계를 받았다. 기타 미참조 코드 정리는 하지 않았다. 메인은 Editor/Player 정리 전후 토큰 동일성 4행의 syntax 로그를 독립 열람했고, 최종 13파일 SHA manifest와 실제 파일을 대조해 일치 13·불일치 0을 확인했다. 이는 활성 동작 코드 무변경 근거이며 정리 이후 새로운 실기 수용이 아니다.

메인이 확인한 정리 후 Runtime 별도 컴파일은 오류 0·경고 23, Editor 최종 컴파일은 exit 0·오류 0·경고 0이다. Editor 실물 로그의 명령과 ExitCode 0을 확인했고 재실행 이후 소스 변경이 없어 최종 13파일 해시 일치 판정도 유지된다는 전달을 받았다. 임시 근거는 `.utmp/rally_compile/game-end-cleanup-source-hashes.json`, `game-end-cleanup-syntax.log`, `game-end-cleanup-runtime.log`, `game-end-cleanup-editor.log`이며 영구 보존 링크가 아니다. 최종 해시는 다음 메인 전달값을 따른다.

| 파일 | 정리 후 SHA256 |
|---|---|
| UIManager.cs | `F84A88668CAD4FD0B2706701560FD7A6A0D6DE5500614D927DFE4A85C8C77798` |
| GameEndUI.cs | `050F71D65BCC4FE7930B629ABCB6DD5985A77ABA00ED154C7C948042A2A15ACE` |

구현·사용자 수용·승인된 주석 정리·최종 별도 검사는 이 Task 범위에서 종결한다. 게시 상태는 Git과 메인 최종 보고로 별도 확인한다. 문서 담당은 source·git·외부 설정·로그·meta를 수정하지 않았다.
