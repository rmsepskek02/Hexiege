# 게임 종료 결과 화면의 배경·입력 차단 — Plan

게임이 끝나면 기존 공용 반투명 배경 위에 승패·안내 메시지·결과 버튼을 함께 표시하고, 뒤쪽 게임 화면을 더 이상 조작할 수 없게 한다. 재경기와 실패 복원에서는 필요한 입력만 다시 열며, 기존 결과 문구·카운트다운·중첩 팝업의 동작은 유지한다. 로그인·로비를 거쳐 게임에 들어오는 경로를 대상으로 한다.

> **현행 상태 (2026-10-19):** 사용자 6항목의 싱글·멀티 정상 확인으로 해당 범위를 수용했고 대체 주석 정리·최종 별도 컴파일·해시 검토도 완료했다(§8). 게시 상태는 Git과 메인 최종 보고로 별도 확인한다. 에이전트 native·빌드·QA 실행과 사용자 보고를 구분하고 장치·정확한 역할·로그 측정은 추정하지 않는다.

## 최상단 — 기존 로직 변경·비활성화와 안전 근거

기존 로직을 대체하는 부분은 먼저 **주석 처리로 비활성화**하고, 삭제는 사용자 테스트 수용 후 WORKFLOW의 최종 정리 단계에서 한다. 관련 없는 비활성 코드까지 정리하지 않는다.

**현행 정리 경계:** 사용자 6항목 정상 확인 후 이번에 대체한 주석만 최종 제거했다. 활성 토큰 동일성·최종 별도 컴파일·해시 검토도 완료했다. 아래 최초 안전 근거는 보존한다.

| 대상 | 대체 방향 | 안전 근거·유지 범위 |
|---|---|---|
| `UIManager.ShowBlockingOverlay`의 실제 표시·배경 탭 listener 설정과 `HideBlockingOverlay`의 카운터 0 즉시 숨김 분기 | legacy 참조 수와 보호된 Modal 점유를 함께 보고 실제 표시/클릭 상태를 갱신 | legacy Show/Hide의 참조 증감 계약은 남긴다. 결과 소유권만 익명 Hide와 분리하므로 다른 패널 정리 호출이 결과 배경을 소모하지 않는다. 기존 callback 하나를 보존하고 범용 callback stack으로 확장하지 않는다. |
| `GameEndUI` 결과 표시·실패 복원의 직접 `_panel.Show()` 경계와 Hide/파괴 정리 | 결과 전용 lease 획득·반납과 게임 입력 차단 경계를 묶음 | 승패·상태 문구·timer 계산·버튼 listener는 대체하지 않는다. 반복 표시·복원에서도 lease는 하나만 보유한다. |
| `GameUIManager` 종료/시작 알림 경계 | 종료 정리 앞에서 게임 입력을 막고 새 경기 UI 정리가 끝난 뒤 해제 | 기존 등록 UI 정리 훅·독립 결과 구독을 유지한다. Bootstrapper의 manager→결과 UI 초기화·구독 순서를 보존한다. |
| 입력·조준·설정·카메라의 기존 입력 진입부 | 기존 처리 앞에 주입된 입력 허용 판정과 gesture 정리를 추가 | 정상 경기에서 기존 입력 로직은 유지하며, 프로그램에 의한 카메라 배치와 결과 UI의 응답 버튼을 차단하지 않는다. |

새 source 파일·씬·프리팹 생성은 계획하지 않는다. 버튼별 CanvasGroup이 없으면 기존 버튼 GO에 런타임으로 추가한다. `AnimatedPanel` root나 Tween은 건드리지 않는다.

## 1. 근거 원본과 범위

[게임 규칙 인덱스](../../../GameSystemRules.md)를 먼저 읽고 다음 원본과 실제 계약을 대조했다. 여기의 ID는 아래 각 수정 항목의 근거를 가리킨다.

| ID | 규칙 원본 | 적용 계약 |
|---|---|---|
| R1 | [공통 UI](../../../GameSystemRules/GameSystemRules_UI.md) 「공통 UI 규칙」 규칙 4·5 | UIManager 공용 배경 단일 소유·CanvasGroup·Show/Hide 짝·null-safe |
| R2 | 같은 문서 같은 절 규칙 8·9·10 | Modal 배경 탭 닫힘 금지·하위 입력 차단·중첩 LIFO·공유 배경 |
| R3 | [Canvas 정렬](../../../GameSystemRules/GameSystemRules_CanvasSortingOrder.md) 「전역 SortingOrder 대역 규칙」 및 「새 Canvas 추가 시 규칙」 규칙 1 | 공용 막 100·결과 200·상위 Modal 250·로딩 300, 기존 Canvas/GraphicRaycaster 유지 |
| R4 | [경기 흐름 UI](../../../GameSystemRules/GameSystemRules_MatchFlowUI.md) 「공통 UI 규칙」 M-3·M-4·D-2·D-3·D-4·D-7 | 결과 복원·재경기 선택지·로비 탈출·싱글 countdown 없음·멀티 timer 정책 유지 |
| R5 | 같은 문서 같은 절 D-5·D-6 | 알림/재경기 중첩 Modal과 응답·자동 복귀 유지 |
| R6 | [네트워크 규칙](../../../GameSystemRules/GameSystemRules_Network.md) 「🔴 최상위 원칙 — 플레이어는 자신이 Host 인지 Client 인지 알 수 없다」 | 같은 결과 UI 동작의 역할 독립성. 특정 역할에서 재현됐다고 단정하지 않음 |

R1~R6은 기존 계약이고, [Research의 U1~U6](Research.md#1-사용자-확정-요청과-정정)는 이번 사용자 확정 요청이다. 버튼 활성 정책·상태 메시지·승패·네트워크/재경기 timer를 새로 정하지 않는다.

## 2. 생명주기와 입력 상태

게임 입력 차단 상태는 `GameUIManager.BlocksGameplayInput`으로 관리하고 Bootstrapper가 `Func<bool>` 판정을 소비자에게 주입한다. 결과 패널이 잠깐 숨겨져도 새 경기가 준비되기 전까지 게임 입력을 다시 열지 않는다.

| 경계 | 게임 입력·결과 배경 | 유지할 동작 |
|---|---|---|
| 게임 종료 | **입력 차단 → 기존 IGameUI 정리 → 결과 lease 획득·Show** | 기존 승패 텍스트와 멀티 countdown 진입 |
| 결과 상태 줄 변경·재경기 요청/대기 | 입력 차단 유지·lease 변경 없음 | 상태 줄·D-2/D-3 버튼 정책·D-7 timer 그대로 |
| 재경기 시작을 위해 결과 Hide | 결과 lease만 반납, 게임 입력 차단은 유지 | 준비 중 이전 경기 입력이 열리지 않음 |
| 싱글 맵 재로드 실패 | 게임 입력 재차단·결과 lease 확보·Show | M-4의 기존 실패 상태 줄, 별도 countdown/팝업 추가 없음 |
| 새 경기 시작 | 기존 UI 정리·결과 Hide가 끝난 뒤 게임 입력 허용 | 이전 pointer/조준/카메라 gesture가 다음 경기에 이어지지 않음 |
| 로비 이동·파괴 | 자신이 가진 결과 lease만 반납·관련 구독 해제 | 기존 Confirm/Alert 정리 후 Loading 표시 순서 유지 |

**이벤트 순서 확정:** 기존 `GameEndUI`의 독립 OnGameEnd 구독을 유지한다. Bootstrapper의 `GameUIManager.Initialize` → `GameEndUI.Initialize` 순서를 불변조건으로 보존해 선행 manager 구독이 gate 차단·기존 UI 정리를 하고 후행 결과 구독이 lease 획득·Show를 수행한다. manager가 결과 표시를 직접 호출하거나 기존 결과 구독을 비활성화하지 않는다. 두 Initialize는 재호출 시 Dispose·재구독하므로 재경기 초기화에서도 같은 순서를 보존해야 한다. GameEnd의 종료/실패 복원에서 gate=true를 멱등 보강하되 기존 종료 정리 루프를 다시 호출하지 않는다.

근거: R1·R2·R4·R5, U2~U6. 직접 Game 씬 진입을 위한 UIManager 생성은 범위 밖이고 기존 null-safe 호출은 유지한다.

## 3. 파일별 변경 계획

경로는 모두 `Assets/_Project/Scripts/` 기준이며 같은 이름의 다른 컴포넌트를 대신 수정하지 않는다.

| 파일 | 변경할 책임·API | 근거 |
|---|---|---|
| `Presentation/UI/UIManager.cs` | `AcquireBlockingOverlayModal()`이 `IDisposable` lease를 반환하도록 추가한다. private 중첩 lease와 owner ID 집합으로 보호 점유를 관리하고 중복 Dispose를 무해하게 한다. legacy count와 분리해 익명 Hide가 결과 점유를 반납하지 못하게 한다. 실제 표시 조건은 `legacyCount > 0 || 보호 owner 존재`다. 보호 점유 중 배경 tap callback은 실행하지 않는다. 최신 legacy callback 하나만 보존한다. | R1·R2, U1·U4·U6 |
| `Presentation/UI/Core/IUIManager.cs` | 위 lease 획득 API와 `BlockingUiStateChanged` 이벤트 계약을 추가한다. 기존 Show/Hide API를 지우거나 호출자 전체를 lease로 이행하지 않는다. | R1·R2, U1·U6 |
| `Presentation/UI/ConfirmPopup.cs` | 기존 `_overlayShown`에 대한 읽기 전용 `IsOverlayHeld`를 노출한다. Show/Hide의 점유·입력 반납 정책은 바꾸지 않는다. | R2·R5 |
| `Presentation/UI/Common/RematchRequestPopup.cs` | 기존 `_overlayShown`의 읽기 전용 `IsOverlayHeld`를 노출한다. 요청 화면뿐 아니라 거절 안내 등 실제 점유를 반영한다. | R2·R4·R5 |
| `Presentation/UI/GameEndUI.cs` | 결과 lease 하나를 멱등 획득·반납한다. 종료 표시·실패 복원에서 확보하고 Hide/새 경기/파괴 경계에서 자신 소유분만 반납한다. 공유 게임 입력 상태 참조와 상위 UI 상태 변경 구독을 추가한다. Restart/Lobby 각 기존 버튼 GO의 전용 CanvasGroup을 캐시하고 없으면 런타임으로 추가한다. | R1~R5, U1~U6 |
| `Presentation/UI/GameUIManager.cs` | `BlocksGameplayInput`과 명시적 차단/해제 전환을 보유한다. 종료 UI 정리 앞에서 차단하고 새 경기 UI 정리가 끝나야 해제한다. 실패 복원도 같은 상태를 사용한다. | R2·R4·R6, U3·U5 |
| `Presentation/Input/InputHandler.cs` | 가장 앞에 주입된 게임 입력 판정을 두어 UI raycast 이전 랠리 분기도 차단한다. 차단 전후 press/release 기억을 정리해 지난 입력이 다음 경기로 이월되지 않게 한다. | R2, U3·U5 |
| `Presentation/Input/SkillAimController.cs` | `BeginAim` 진입 거부, 진행 중 조준 취소, 발동 직전 재확인으로 독립 Update 경로도 막는다. 기존 `CancelAim` 정리 경로를 사용한다. | R2, U3·U5 |
| `Presentation/Camera/CameraController.cs` | 팬·휠·핀치·edge 등 사용자 카메라 입력을 차단하고 제스처를 정리한다. `SetPosition`/bounds 등 프로그램에 의한 맵 배치 API는 유지한다. | R2, U3·U5 |
| `Presentation/UI/InGameSettingsUI.cs` | `Show` 진입에서 게임 입력 허용을 검사한다. 기존 열림·닫힘과 종료 정리 정책 자체를 교체하지 않는다. | R2, U3 |
| `Presentation/UI/GameHudUI.cs` | 설정 버튼의 입력 허용 상태를 반영한다. 결과 중에도 기존 정보 표시는 유지한다. | R2·R4, U2·U3 |
| `Bootstrap/GameBootstrapper.Setup.cs` | 유일한 조합 루트에서 입력·스킬·카메라 등의 `Func<bool>` 판정을 주입한다. | 공용 메모리의 GameBootstrapper 조합 루트, R2 |
| `Bootstrap/GameBootstrapper.Map.cs` | UI 초기화/재경기 경계에서 같은 판정과 RematchRequestPopup 참조를 UIManager/결과 UI에 전달한다. 중복 구독·옛 씬 참조가 남지 않게 한다. | 공용 메모리의 조합 루트, R1·R4·R5 |

새 NetworkBehaviour·RPC·Application→Infrastructure 참조를 도입하지 않는다. 결과 문구·승패·자원·맵·랠리 이동·전투 구현은 변경하지 않는다.

## 4. 상위 팝업·로딩과 결과 버튼의 입력 분리

공용 배경 100 위 결과 200, 그 위 Modal 250·Loading 300을 유지한다. 결과 전체를 상위로 올리거나 자체 반투명 Image를 추가하지 않는다. 승패·상태 줄·버튼은 모두 현재 결과 UI에 남는다. 근거: R1·R3, U1·U2.

상위 Modal/Loading 중에는 Restart/Lobby **각 버튼 GO의 전용 CanvasGroup.interactable**만 false로 둔다. root AnimatedPanel CanvasGroup·Tween을 조작하지 않아 fade 중 경쟁을 피한다. `Button.interactable`의 기존 D-2/D-3 활성 사유는 보존하고 상위 입력 차단이 끝나면 그 정책대로 다시 누를 수 있게 한다. D-3의 항상 활성인 로비 선택지가 상위 Modal 아래에서 동시에 눌릴 수 있다는 뜻은 아니다. 근거: R2·R4·R5.

UIManager는 ConfirmPopup·RematchRequestPopup의 실제 `IsOverlayHeld`와 Loading의 실제 `blocksRaycasts`를 판정한다. 임의 legacy refcount 양수나 requestShowing 하나로 상위 Modal을 판정하지 않는다. 기존 Show/Hide·Loading 실표시 변경에서 `BlockingUiStateChanged`를 발행하고 GameEndUI가 즉시 버튼 gate를 갱신한다. 최초 결과 표시에서도 현재 값을 동기 조회해 변경 이벤트를 놓쳤다고 버튼이 열리지 않게 한다.

Confirm.Hide는 현재처럼 Hide 진입 시 즉시 입력을 반납하는 정책을 유지한다. fade 종료까지 점유를 늘리지 않는다. 중첩 callback 전체 stack 복원은 이번 범위 밖이다. 보호된 결과 점유가 있는 동안 legacy Hide가 실행돼도 공용 배경은 유지한다. 마지막 보호 점유 반납 후에는 남은 legacy count와 최신 callback 하나에 따라 갱신한다.

## 5. 위험과 보존 경계

| 위험 | 계획에서 지킬 경계 |
|---|---|
| 종료 정리 중 기존 UI가 익명 Hide를 부름 | 결과 보호 점유는 legacy count와 분리하고 종료 gate를 정리보다 먼저 적용한다. 기존 미보유 Hide 전수 교정으로 확대하지 않는다. |
| 결과 Show/복원 반복·중복 Hide | 결과 lease는 하나만 확보하고 반납 후 참조를 비운다. owner Dispose는 멱등이다. |
| 결과 Hide가 새 경기 시작보다 먼저 실행됨 | 결과 가시성과 게임 입력 허용을 분리한다. 새 경기 UI 정리 종료만 게임 입력을 연다. |
| 상위 Modal 닫힌 뒤 버튼 정책 유실 | 전용 CanvasGroup만 제어한다. 기존 Button.interactable 값·문구·timer를 덮어쓰지 않는다. |
| 구독/manager가 재경기 이후 남음 | Bootstrapper 재주입·UIManager 상태 통보·GameEnd 파괴 시 구독 해제를 점검한다. |
| 직접 Game 씬 지원 범위 확대 | 로그인·로비 경유만 이번 대상이다. 새 manager 생성·씬 배선은 추가하지 않는다. |
| 실제 문제 환경·실행 원인 오판 | 싱글/멀티 발생 환경 미확인·실행 재현 없음 유지. 정적 설계가 사용자 실기 성공을 의미하지 않는다. |

기존 UIManager legacy callback 복원 부재·미보유 반납은 별도 기존 결함이며 이번에는 결과 owner 보호에 필요한 부분만 추가한다. timer/문구 상태 머신 전체 재설계나 원격 이탈·승패 정책 변경은 범위 밖이다.

## 6. 구현 후 확인 범위와 승인

명시적 계획 승인을 받아 game-programmer가 main에서 구현했고 기존 참조의 Roslyn 별도 컴파일을 완료했다. 이 결과와 Unity·실기를 구분한다. 새 Testcase 파일·QA 실행은 요청되지 않아 만들거나 실행하지 않았다.

사용자 확인 시에는 결과 배경 위 전체 문구/버튼 표시, 배경 탭 닫힘 없음, 맵/설정/랠리/스킬/카메라 누수 없음, 재경기 입력 복원, 실패 결과 재표시, 상위 Modal 아래 결과 버튼 차단과 닫힌 뒤 복원, 기존 로비 복귀 timer 정책을 구분해 기록한다. 실제 싱글·멀티·역할·장치 범위는 확인된 만큼만 보고한다.

구현 인계 당시 문서 담당은 코드·씬·상시 문서·메모리를 수정하지 않은 최초 기록 단계였다. 이후 상시 문서·메모리 갱신과 사용자 실기 수용은 §8을 따른다. 에이전트 native 실행과 사용자 확인은 구분한다.

## 7. 최종 반영과 검증 상태

Plan §3의 기존 C# 13파일에 구현을 반영했다. 보호된 공용 배경 lease, 게임 입력 gate, 상위 모달 중 버튼별 CanvasGroup 제어와 재경기 생명주기는 실제 코드와 대조했다. 추가로 `GameplayInputBlockedChanged`를 동기 발행해 같은 프레임의 종료→시작에서도 pointer·카메라 기억을 즉시 비우고 차단 시 진행 조준을 취소한다. GameEnd 파괴 시 Modal source는 identity를 비교해 자신의 참조만 정리한다. 승인된 목적을 유지하는 보강이며 새로운 UI·씬·프리팹은 추가하지 않았다.

programmer의 최종 Runtime/Editor Roslyn 컴파일은 각 exit 0이다. 메인은 로그를 독립 재계수해 오류 0/0·경고 23/0, manifest와 실제 13파일 해시 일치·불일치 0, 소스 공백 검사 exit 0을 확인했다. 정확한 파일 목록·명령·임시 로그/manifest 위치·최종 해시는 [Research §6](Research.md#6-승인-구현-반영별도-컴파일-완료)을 따른다. 문서 담당의 컴파일 재실행 결과는 아니다.

구현 인계 단계에서 Unity/native 실행·빌드·QA·사용자 실기는 NOT RUN이었다. 이후 사용자 6항목 수용은 §8에서 구분하며 에이전트 실행으로 확대하지 않는다. 대체 주석의 최종 정리 상태도 §8을 따른다.

## 8. 사용자 수용과 승인된 정리

사용자가 안내한 6가지 시나리오를 모두 테스트해 정상이라고 보고하고 문서 갱신·push를 승인했다. 싱글 결과 배경/입력 차단, 종료 직전 조준 정리, 재경기·로비 복원, 멀티 양쪽 및 요청/거절/수락, 상대 이탈 정책, 에디터 싱글 강제 실패 복원의 상세 대응은 [Research §7](Research.md#7-사용자-6항목-정상-확인수용과-후속-정리)을 따른다. 이 제품 동작 범위는 사용자 확인으로 수용한다.

game-programmer가 이번 변경의 UIManager 49행·GameEndUI 4행 대체 주석만 제거했고 메인이 Editor/Player 활성 토큰 동일성과 최종 소스 13해시 일치를 확인했다. 정리 후 Runtime 오류 0·경고 23, Editor exit 0·오류 0·경고 0을 확인했으며 상세 임시 근거·해시는 [Research §8](Research.md#8-수용-후-대체-주석-정리최종-소스-검토)을 따른다. 구현·수용·정리·별도 검사 범위는 종결하며 게시 상태는 Git과 메인 최종 보고로 별도 확인한다. 기존 외부 변경은 보존하고 새 TC·로그·Task는 만들지 않는다. 실제 장치·정확한 역할·계측 로그까지 확인했다는 의미는 아니다.
