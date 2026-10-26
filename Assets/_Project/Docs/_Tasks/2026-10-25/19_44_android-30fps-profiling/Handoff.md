# 다른 환경에서 이어 할 최적화·리팩토링 통합 검증 인계

현재 준비된 최적화 코드와 변경 전 비교 기준을 다른 Unity 환경으로 옮기고, 남은 리팩토링·버그 교정 검사를 한 번에 진행하기 위한 문서다. 이 문서만 읽어도 받을 파일, 먼저 확인할 상태, 테스트 순서와 PASS에 필요한 증거를 알 수 있도록 정리했다.

## 1. 먼저 확인할 현재 상태

인계 작성 기준은 **2026-10-26 KST**, 브랜치는 **work**, 게시된 기준 커밋은 **7c00f768d5c9f0ebb9583e93e4f6a4a45397f306**이다. 이 커밋에는 앞선 리팩토링, main VFX·서버 투사체 통합, 리팩토링 검증 로그·메뉴·기능 테스트 절차가 포함돼 있다.

**인계 초안 작성 시점에는 이번 최적화 source5파일과 인계 작성분이 미커밋·미푸시였다.** 이후 사용자 게시 승인에 따라 계측만 있는 기준을 **070ddcafa368dcfb94fb76cb3218000a6cacc474**로 로컬 커밋했다. 후속 버퍼/인계 커밋·최신 main 병합·원격 게시는 이 문서 갱신 시점에 대기다. 실제 게시 결과/최종 SHA는 전달 메시지와 수신 환경의 Git 상태로 확인한다. 원격 work가 여전히7c00f768이면 이번8marker와 버퍼 재사용 코드가 있다고 가정하면 안 된다. source 정적 검토는 완료했지만 이번 최적화의 Unity 컴파일·Android 빌드·실기·Profiler 캡처는 미실행이다.

| 유지할 기존 확인 | 정확한 의미 |
|---|---|
| 사용자 「컴파일 에러 없음」 | 당시 사용자가 확인한 버전의 컴파일 수용. 이번 미게시 최적화의 컴파일 결과로 바꾸지 않는다. |
| 사용자 「특별히 문제없어 보임」 | 실제 확인한 일반 플레이 범위 수용. 이를 다시 미확인으로 되돌리지 않는다. |
| 최신 싱글 피스톨러 방향 오류 없음 | 해당 테스트 시도에서 미재현. 기존 지속 방향 증상의 원인 해결 확정은 아니다. |
| re1 멀티 두 Host 경기의 기존 adapter 실패0 | 해당 두 경기에서 기존 이동 복귀 실패 미발견. 새 종료 후 진입 교정이나 모든 지형의 실행 완료와는 별개다. |

main `f367cb52`의 VFX/서버 투사체 변경은 이미 병합돼 있으므로 **외부 VFX 브랜치를 아직 기다리는 상태가 아니다.** 기존 부분 시각 수용은 유지하고, 이번 검증을 VFX 전체 기능 PASS로 확대하지 않는다. AI 생산시간 배율 적용은 사용자 결정으로 후속 AI 작업에 보류돼 있다.

## 2. 새 환경에서의 빠른 시작

1. 프로젝트 루트의 `AGENTS.md`, `CLAUDE.md`, `Assets/_Project/Docs/WORKFLOW.md`, `.claude/MEMORY.md`와 현재 사용자 승인·보류 범위를 먼저 읽는다. 아래 인계 파일과 프로젝트를 모두 전달받았는지 확인한다. 현재 최적화가 게시되지 않았으면 원격 work만으로는 부족하다.
2. 새 환경에서 Unity **6000.3.5f2**, Android Build Support/SDK·NDK·JDK, ADB 기기 연결, USB 디버깅, Profiler 대상 앱 연결을 실제로 확인한다. 사용자 PC에서 Unity/Android 사용 가능이라는 답변은 있었지만 **새 환경의 설치·연결 상태를 확인한 기록은 없다.**
3. Computer Use가 있다면 실제 Unity 창·현재 프로젝트 경로·컴파일 완료·연결 기기를 확인한 뒤 조작한다. 도구가 있다고 가정하거나 과거 빌드 시작을 완료로 기록하지 않는다. Lobby 씬 직접 시작 대신 Login부터 정상 진입한다.
4. 최적화 source가 있는 작업 환경 또는 별도 재현본을 확보하고 파일 hash를 확인한다. 현재 작업을 지우거나 되돌리지 않는다.
5. 확정한 최적화 묶음을 마무리한 뒤 아래 기능 검사를 한 번에 진행한다. 큰 탐색/렌더 최적화는 측정에서 병목을 확인한 뒤 선정한다.
6. 성능은 같은8marker의 변경 전/후 두 빌드를 비교한다. 기능 통제 메뉴와 기기 성능 측정은 **서로 다른 Host/Client 구성**이 필요하므로8절을 따른다.

현재 작성 환경에는 Unity·adb·dotnet 실행 수단이 확인되지 않아 실제 검사를 수행하지 못했다. 새 환경에서의 첫 실행은 과거 정적 검토를 다시 PASS로 선언하는 작업이 아니라 실제 컴파일·검사·기기 증거를 채우는 작업이다.

## 3. 전달 파일과 복원 방법

| 같은 폴더의 파일 | 역할 |
|---|---|
| [Handoff.md](Handoff.md) | 현재 상태·전체 남은 검사·다른 환경에서의 진행 순서 |
| [HandoffBaseline.json](HandoffBaseline.json) | 기준 전체 SHA, 두 patch의 순서/bytes/SHA256, source5개 변경 전/후 hash, 고정8marker, 미실행 상태 |
| [marker-only.patch](marker-only.patch) | 기준7c00f768에 계측8개만 추가하는 변경 전 비교용 patch |
| [buffer-only.patch](buffer-only.patch) | marker-only 상태에서 NCC3컬렉션 재사용만 추가하는 후속 patch |
| [Research.md](Research.md), [Plan.md](Plan.md) | 구현/정적 검토 근거와 Profiler 상세 설명 |

위 신규4파일에는 Unity meta가 함께 있으며, 기존 [기능 Testcase](../20_47_spawn-facing-and-repath-fix/Testcase.md)도 인계 자료에 포함한다. 메인이 만들 전달 ZIP의 위치는 `/workspace/scratch/Hexiege-optimization-handoff-2026-10-26.zip`이다. ZIP은 신규8개 문서/patch/meta와 기존 Profiler Research/Plan2개·기능 Testcase1개의 **저장소 상대 경로11파일**이며, **Unity 프로젝트 전체나 실행 파일, raw 게임 로그가 아니다.** ZIP 생성·실제 전달 여부는 최종 전달 결과에서 별도로 확인한다. 원본 프로젝트 또는 정확한 기준 커밋의 체크아웃도 필요하다.

진행 경로는 둘 중 하나다.

- **현재 최적화까지 게시되거나 작업 파일을 전달받은 경우:** 실제 source5개의 hash가 manifest의 optimized hash와 맞는지 확인하고 해당 작업본을 사용한다. 이미 적용된 코드에 patch를 다시 적용하지 않는다.
- **계측 기준070ddcaf를 받을 수 있는 경우:** 별도 기준본을 해당 전체 SHA로 준비하면 변경 전 source는 이미 완성돼 있다. 여기에 marker-only를 또 적용하지 않는다. 별도 변경 후 본에 buffer-only만 적용한다. 이 commit의5source hash는 manifest의 marker-only hash와 같다.
- **게시된7c00f768과 인계 ZIP만 받은 경우:** 활성 프로젝트와 다른 **별도 깨끗한 체크아웃**을 기준 전체 SHA로 준비하고 ZIP을 저장소 상대 경로대로 푼다. 변경 전 본에는 marker-only만, 변경 후 본에는 marker-only 다음 buffer-only를 순서대로 적용한다. 다른 코드·에셋·빌드 설정은 두 본에서 같게 유지한다.

| 준비할 빌드 | 정확한 구성 |
|---|---|
| 변경 전 계측 기준 |우선070ddcaf commit 자체. 대안은7c00f768 + marker-only.patch |
| 변경 후 최적화 |070ddcaf + buffer-only.patch. 대안은7c00f768 + marker-only.patch + buffer-only.patch |

**7c00f768 자체에는 새 marker가 없다.** marker 없는 원본의 전체 프레임/GC를 참고할 수는 있지만 marker 내부 시간의 변경 전 값으로 사용하지 않는다. 두 patch와 별도의 전체 working-tree patch를 중복 적용하지 않는다. 현재 진행 중인 작업 트리에 reset/clean/역patch를 적용하거나 기존 미커밋 파일을 덮어쓰지 않는다.

새 담당자는 별도 재현본에서 patch 적용 전 `git apply --check` 등 적용 가능성 검사를 하고, 실패하면 원문과 상태를 비교한다. `--reject`·fuzz·강제 덮어쓰기로 넘어가지 않는다. manifest hash는 **UTF-8/LF 원문 bytes** 기준이다. Windows 체크아웃이 CRLF이면 비교용 사본에서 줄바꿈만 LF로 정규화해 내용 차이를 분리하고, 실제 활성 파일을 무조건 재작성하지 않는다. 줄바꿈 외 차이가 남으면 복원을 완료했다고 판정하지 않는다.

patch 원본 SHA256은 다음과 같다. manifest에도 같은 값이 있다.

- marker-only: `dc105f4b8822a9b1c3cd10545d22a478fbed241d9c037e0f3b8da27644348d0f`
- buffer-only: `8841457706481a83cafeae089769419f707fd918dff9a15c3c5bd4a326c9f1c1`

기존 `/tmp`와 `/workspace/scratch`의 검토 파일은 이식의 필수 원본으로 삼지 않는다. 이 폴더에 복사한 patch/manifest를 전달받아야 한다. 메인과 코드 담당은 임시 별도 트리에서 기준→marker-only→optimized 순차 적용 및 역복원이 실제 원문 bytes와 일치함을 확인했지만, 이것은 Unity 빌드나 성능 PASS가 아니다.

## 4. 무엇이 최적화됐는지

기존 source5파일에 고정8marker를 추가했다. Application/Domain·기존meta·씬·에셋·게임 수치·네트워크 주기는 변경하지 않았다. marker의 코드/SDK 참조는 Editor 또는 Development Build에만 남는다.

NCC의 반복 전투 틱에서 만들던 ID 목록·중복 처리 집합·제거 목록을 controller 소유 버퍼로 재사용한다. 기존 타워/상태 콜백 뒤 snapshot 시점과 제거 목록 생성 시점, foreach3본문 순서는 보존한다. 공유 버퍼를 이미 사용하는 중첩 호출은 독립적인 새 세 컬렉션을 사용하고, finally에서 선택된 버퍼를 정리하며 공유 소유자만 사용 중 표시를 해제한다.

버퍼의 용량은 controller 수명 동안 남으므로 **총 RAM 감소나0B/프레임을 보장하지 않는다.** 초기 확장·중첩 fallback은 할당할 수 있고 HashSet.Clear의 CPU 비용도 있다. 전문/메인 원문 역복원·소유권·조건부 문법 선택·meta 대조와 QA 정적 blocker0을 확인했으나, 실제 FPS·GC 개선량·최신 기능 회귀는 미측정이다.

### 4.1 다음 최적화의 우선순위

먼저 같은 조건의 Android Host에서 현재 변경 전/후 캡처를 확보한다. 아래는 전부 수행하라는 목록이 아니라 **측정에서 해당 비용이 크게 나온 경우에만** 검토할 후보 순서다. 한 번에 가장 큰 근거가 있는 구간 하나를 바꾸고 같은 조건으로 재측정한 뒤, 확정 묶음의 기능 테스트를 모아 진행한다.

| 캡처에서 보인 비용 | 다음 조사 후보·보존할 경계 |
|---|---|
| 전투 탐색 CPU/호출 수가 큼 | `UnitCombatUseCase.FindFirstEnemyInDetectRange`·`FindFirstEnemyTarget`의 전체 순회. 타겟 선택 순서·거리·생존·동률 처리·서버 권위를 보존하고 공간 검색 구조가 필요하다는 결론을 미리 내리지 않는다. |
| 투사체 수에 따라 처리 시간이 커짐 | `CountActiveProjectiles`와 `CompactProjectiles`의 반복 순회/RemoveAt. 기존 순서·재진입·pool/수명/삭제와 피해 판정 경계를 지키며 비용이 확인된 부분만 정리한다. |
| 표현 생성/회수·렌더 비용이 큼 | VFX pool miss, 사망 ghost 수명, 실제 GPU/배치 비용을 구분한다. 시각 타이밍·종료 계약·품질을 임의로 낮추지 않고 CPU/메모리/GPU 중 어느 축인지 먼저 확인한다. |

추가 main `5861fcb4`의 NET-TIME-002 T-1/T-3a 문서 정정은 새 최적화 구현 지시가 아니다. 서버 투사체의 표현·관련 사망/화상 시작은 순수 Client만0.10초 지연, Host/싱글은0초인 기존 동작에 맞춘 규칙이며 서버 HP·판정·도메인 제거/Despawn은 늦추지 않는다. 해당 사망/화상 표현 보류 상한은0.3초이고 일반 사망·독은 즉시다. HP바는 현재 기능이 아니라 추후 예정이다. 예전 문장을 이유로 Host 지연을 추가하거나 실제 판정을 늦추지 않는다. 갱신된 WORKFLOW도 ServerProjectiles/ProjectileDeathHold를 기존 전체 검사의 내부 의존으로 명시하며 메뉴 실행이 실제 PASS라는 뜻은 아니다.

## 5. 먼저 실행할 공통 컴파일·Editor 검사

최적화 묶음의 현재 source가 갖춰진 뒤 다음을 진행한다. 여러 변경마다 반복하지 않고 최종 묶음에서 한 번 실행한다.

1. Unity에서 프로젝트를 열고 최신 Editor 컴파일을 완료한다. Android Development Build의 컴파일·NGO ILPP·실제 설치까지 별도로 확인한다. 창에서 Build 시작을 누른 것만으로 완료라고 기록하지 않는다.
2. Play를 종료하고 활성 네트워크/등록된 경기 서비스가 없는 상태에서 **Hexiege > Combat > Run Full Regression Suite**를 실행한다. 실제 종료 결과와 예외를 보관한다. 새 상류 VFX/투사체 검사도 포함될 수 있으나 기존 Setup 메뉴를 재실행하거나 씬/에셋을 임의로 고치지 않는다.
3. **Hexiege > Combat > Diagnostics > Self Validate Root Pose Audit**를 실행해 실제 self-validation 결과를 보관한다.
4. **Hexiege > Combat > Diagnostics > Run Refactoring Verification**을 실행한다. `[REFACTOR-VERIFY] Generation=refactor-verify-v1`의 같은 Run BEGIN·8case·END가 모두 있어야 한다.
5. 정지 상태에서는 FileSink가 열리지 않아 Console에만 남을 수 있다. `[REFACTOR-VERIFY]` 메시지의 해당 Run 전체를 복사해 **RefactoringVerification.txt**로 저장한다. 전체 Editor.log는 전달하지 않는다. 전체 suite와 Root self-validation 결과도 해당 메시지/필요 예외를 별도로 보관한다.

집중 검사의8case는 FIFO, Logcat 이름 소진, RootPose parser, 건물 제거 경로, Bloom 네트워크 역할, UI/Ticker/Bootstrap 수명이다. 환경 때문에 실행을 거부하면 NOT_RUN이며, 전체 suite가 초록색이라고 실제 기기/로그/모든 리팩토링 항목을 한꺼번에 PASS로 바꾸지 않는다.

## 6. 리팩토링 이후 버그 교정 V01~V10 통합 테스트

자세한 자연어 조작은 [기능 Testcase 전체](../20_47_spawn-facing-and-repath-fix/Testcase.md)를 그대로 따른다. 현재 게시 상태는 이 인계 문서가 기준이다. 해당 Testcase의 이전562888d2/16미커밋 기록은 당시 snapshot이고, 진단 코드는7c00f768에 게시됐다. **이번 미게시 최적화까지 받은 후 새 빌드로 검사**한다.

| 대상 | 실행·PASS에 필요한 증거 |
|---|---|
| V01 건물 제거 후 경로 갱신 | 집중 fixture 성공 + 싱글 출구 차단→해제→생산·이동의 실제 관찰/로그. 과거 adapter0만으로 해당 재현 완료를 가정하지 않는다. |
| V02 종료 뒤 전투 재진입 | 포기 뒤 살아 있는 유닛·상대 건물로 실제 `END-LATE-ENTRY` 성공 + 종료 집계와 재경기 정상 전투. 자연 late event가 없었다는 것과 통제 검사 성공을 구분한다. |
| V03 예약 FIFO | 실제 준비/완료/Retire/Clear·동일 ID 재등록·상한512의 `EDITOR-FIFO` 성공. |
| V04 연구 요청 검증 | 정상 착수/취소 확인 + 잘못된 요청에서 실제 거부 누계 증가·골드/연구 상태 불변. |
| V05 생산 요청·음수 취소 | 정상 생산/취소/랠리/자동생산 확인 +12개 부정 요청의 해당 생산 case 성공·상태 불변. |
| V06 생산 UI 수명 | 집중 UI/Ticker 검사 + 패널 반복 열기/닫기·건물 전환·한 번 입력의 중복 없음. |
| V07 Bootstrap 구독 해제 | 집중 수명 검사 + 실제 경기 destroy dispose/null와 새 경기 bind를 연결. 오류0만으로 해제 성공 판정하지 않는다. |
| V08 결과 화면 최신 통신 | 실제 대기 task 중 새 상대 신호를 반영하는 `HB-FRESH-DURING-PROBE` 성공. 자연 장애 복구 전체의 수용으로 확대하지 않는다. |
| V09 로그 이름 소진 | 실제 helper의999점유 거부/원본 보존 fixture 성공. 사용자 로그999개를 직접 만들거나 지우지 않는다. |
| V10 검증 도구 정정 | 실제 parser/네트워크 역할 fixture 성공. 전투/VFX 전체 수용과는 별개다. |

기능 검사는 **Editor Host + Android Client**로 한다. 양쪽 정상 요청을 먼저 확인하고, 부정 요청 검사 전에는 서로 전투하지 않고 자기 기지 근처에 랠리를 둔다. 양 팀의 생산과 연구가 실제 진행 중이어야 하며, 비행 중 투사체/파도도 없어야 한다. 메뉴가 전제 부족을 알리면 무리하게 강제 실행하지 않는다.

Editor 메뉴의 순서는 다음과 같다.

1. 양 팀에 여러 유료 생산 대기를 준비하고 연구를 마지막에 시작한 뒤 **Hexiege > Combat > Verification > Run Server Request Checks**. 실제12case, 첫 실패 중단, 개별 대기3초/전체 상한36초, 모든 종료에서 원래 timeScale 복원. 검사 중 양쪽 조작을 멈춘다.
2. 살아 있는 유닛·상대 건물을 남긴 채 포기로 종료하고 결과 화면에서 **Run Ended Combat Check**, 이어 **Run Result Heartbeat Check**. 같은 `Hexiege > Combat > Verification` 아래다. 연결·결과 감시가 살아 있고 이탈 판정 전이어야 한다. 각1case와 END가 필요하다.
3. 재경기에서 생산/이동/공격을 확인하고 이번에는 핵심 건물 파괴로 정상 승패를 낸 뒤 로비까지 진행한다. 양 peer의 경기 종료 로그가 남도록 종료 직후 앱/Play를 강제로 끄지 않는다.

미실행은 NOT_RUN, 조건 미발생/미충족은 NOT_EXERCISED 또는 해당 메뉴의 실제 미실행 표기로 남긴다. 오류0·표본0·일반 EVIDENCE만으로 PASS를 만들지 않는다. 실패가 있으면 해당 항목과 실제 증거를 남기고, 이미 수용된 다른 범위를 일괄 취소하지 않는다.

## 7. 더 넓은 리팩토링 검증 장부도 함께 인계

V01~V10만으로 모든 리팩토링의 세부 계약 검증이 끝나는 것은 아니다. 원본 장부는 [리팩토링 Plan5.1](../../2026-10-23/14_08_architecture-refactoring-audit/Plan.md)의 「리팩토링 후 한 번에 진행할 통합 검증 대상」이다. 다음은 해당 장부에 남은 **세부 실행 증거의 범위**다. 이전 사용자 컴파일/일반 플레이 수용은 유지하고, 기존 기록으로 확인된 범위를 먼저 대조해 중복 테스트를 줄인다.

| 별도 계약 | 아직 필요한 세부 근거·진행 방식 |
|---|---|
| 인증·UGS/Firebase 주입과 Google partial | 최신 Editor/Android 결합·정상 로그인과 기존 인증 정책 보존. Google 취소/실패·연동·명시 logout·재시작은 실제로 관찰한 분기만 기록한다. 기존 수용이 있는 부분은 재사용하고, 테스트 계정/환경이 없는 분기는 미실행으로 남긴다. |
| AI live 종족 provider·난이도 DTO | 새 경기/종족 변경에서 live 조회와 기존 난이도9값 복사·이미 연결된 행동을 확인한다. 생산시간 배율 신규 적용은 이 검사의 목표가 아니며 후속 AI 보류를 유지한다. |
| 공격 강화 반올림 공통화 | Lv0~5, base값0이하, Unity Mathf 반올림 경계, 파생 피해/힐 결과를 기존 계산과 대조한다. 전용 실제 수치 실행 근거 없이 일반 공격이 보였다는 것만으로 PASS를 만들지 않는다. |
| 인구의 미소비 참조 정리 | 건물/유닛 생성·사망4이벤트에 따른 증감, 타일 최대인구와 경기 reset/Dispose를 실제로 확인한다. |
| 건물 인접 영토 공통화 | 일반/ID 지정 배치의 인접 Owner 조건·중앙 타일·등록·이벤트/클라이언트 반영 순서를 확인한다. 정상 배치 관찰과 내부 이벤트 순서 검증을 구분한다. |
| 수입 정수 지급 공통화 | 기본/채굴 누적 분리, 분수 이월, 양수 지급, 차감 뒤 이벤트와 재진입/AI 배율·호출 순서를 확인한다. 골드가 증가하는 모습만으로 수치 경계를 통과 처리하지 않는다. |
| partial 분리·source gate·NGO 연결 | 최신 Unity 컴파일/Android ILPP·실제4요청 송수신과 기존 source gate가 분리된 파일까지 읽는지 확인한다. 이전 세대 검사 성공을 최신 native 실행으로 대체하지 않는다. |

현재 집중8case와 온라인12+1+1은 위 모든 수치·인증·이벤트 경계를 자동 검사하지 않는다. 새 환경 담당자는 기존 검사/실기 자료의 실제 범위를 먼저 확인하고, 빠진 계약은 가능한 실제 API/fixture로 확인한다. 전용 수단이 없는 항목은 미확인으로 명시하고 필요한 좁은 검증 준비를 별도 계획한다. 이 인계 작업에서 새로운 제품 코드나 검증 코드를 추가하지는 않았다.

## 8. Profiler 전후 측정은 반대 역할로 진행

| 목적 | Android | Editor |
|---|---|---|
| 위 통제 메뉴를 포함한 기능 검사 | Client | Host |
| 이번 NCC 버퍼 최적화의 기기 성능 비교 | **Host** | Client |

NCC 전투 틱은 멀티 서버 전용이다. Android Client에서 해당 marker가 안 보이거나 GC가0인 것은 이번 변경의 효과 증거가 아니다. 성능의 변경 전/후는 둘 다 Android Host로 고정한다. 기능 검사를 위한 역할과 서로 바꿔 해도 되지만 그 수치를 한 전후 쌍으로 섞지 않는다.

자세한 화면 설명은 [Plan3](Plan.md#3-사용자-pc의-profiler-사용과-캡처-절차)를 따른다. 필수 순서는 다음과 같다.

1. 두 버전 모두 Development Build + Autoconnect Profiler, **Deep Profiling OFF**. Profiler 대상이 Editor가 아닌 Android 앱인지 확인한다.
2. CPU/Memory 우선, 지원되는 GPU/Rendering/네트워크만 추가한다. 미지원/미발생은0으로 쓰지 않는다.
3. 같은 기기·OS·빌드 구성·맵·종족/유닛 구성과 수·경과 시간·카메라·해상도/품질·충전/발열 조건을 맞춘다. **유닛 적음 / 다수 이동 / 밀집 전투** 세 장면을 구분한다.
4. 각 장면을 관찰하고 실제 저장된 프레임 범위를 확인한다. Profiler history 상한 때문에30초 전체가 저장됐다고 가정하지 말고 필요하면 충분한 보관 범위나 여러 짧은 캡처를 쓴다.
5. CPU Timeline/Hierarchy에서 `Hexiege`를 검색해8marker의 Total/Self·Calls·GC Alloc을 본다. 중첩 Total을 더하지 않고, CPU/GPU는 병렬이므로 두 시간을 단순 합산하지 않는다. 목표 프레임 대기와 실제 계산도 구분한다.
6. Profiler Save로 원본 **.data 등 실제 저장 형식의 캡처 파일**을 변경 전/후·장면·역할별로 보관한다. RuntimeLog와 화면 스크린샷만으로 대체하지 않는다. Allocation Call Stacks는 별도 짧은 원인 추적에서만 켜 기본 자료와 분리한다.
7. Development/연결/계측 비용을 고려하고 최종 Release에서도30 FPS 지속·체감을 별도로 확인한다. CPU/GC 감소와 전체 메모리 감소는 별개다.

모든 캡처는 아직 미수집이다.30 FPS의 약33.3ms 예산과 평균뿐 아니라 긴 프레임·GC 끊김을 비교한다. 특정 기기/역할의 결과를 모든 Android의 통과로 일반화하지 않는다.

## 9. 남길 자료와 PASS 갱신 기준

| 자료 | 용도·수집 |
|---|---|
| 최신 컴파일/Android 빌드·ILPP 결과 | 실제 완료 여부, 소스 리비전/patch 상태와 오류. 빌드 버튼 클릭만으로 성공 처리하지 않음 |
| 전체 suite·Root self-validation·집중 검사 receipt | 실제 실행 결과, 집중 검사의 동일 Generation/Run BEGIN·case·END. 정지 상태 Console 자료도 별도 보관 |
| Editor/Android RuntimeLog | Login부터 경기 종료·로비까지의 양쪽 자료, 역할·경기·상태를 연결 |
| RefactoringVerification.txt | 집중 검사 메시지 전체. 자동 로그 ZIP 대상이 아니므로 별도 첨부 |
| 변경 전/후 Profiler 원본 | 동일8marker·기기/역할/장면/설정의 비교. 로그 ZIP 도구에 자동 포함되지 않으므로 별도 첨부 |
| 관찰 메모/필요 영상 | 실제 수행 항목·시각·누락 단계·방향/입력 이상을 기록. 원본 수치 자료와 분리 |

기기 로그는 **Hexiege > Logs > Save Device Logs**, raw 보관본은 **Hexiege > Logs > Create Log Archive ZIP**로 생성한다. 시작 날짜에 테스트 첫날을 포함하고 자정을 넘겼으면 전날도 선택한다. ZIP과 manifest를 기존 **private Hexiege-Logs 월별 Release**에 함께 올리며, Console 결과·Profiler 파일은 별도로 첨부한다. 원본 로그를 소스 Git에 강제 추가하거나 자동 삭제하지 않는다. 자세한 정책은 [LogRules](../../../LogRules.md) §1.10이다.

새 담당자는 항목별로 **확인한 리비전/patch 상태·환경·실제 동작·기댓값·근거 파일/Run·결과·미실행 이유**를 기록한다. 실제 검증이 기대와 맞으면 해당 범위만 PASS로 갱신한다. 기존 수용·이번 미재현·정적 검토·새 실행 PASS를 구분하고, 로그 부재/조건 미발생·기기 미지원·표본0을 PASS로 만들지 않는다.

결과 원본은 기존 리팩토링 장부와 기능 Testcase, 이 Profiler Research/Plan에 역할에 맞게 반영한다. 전역 규칙·메모리·에셋을 인계를 이유로 광범위하게 고치지 않는다. 이번 범위 밖인 AI 생산시간 배율, VFX 전체 재수용, 측정 근거 없는 추가 탐색/렌더 최적화는 자동 편입하지 않는다.
