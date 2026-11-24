# Android 최적화 현황 정정과 다음 측정 조사

지금까지 적용한 최적화와 실제 기기에서 확인한 결과·한계를 기록한다. 현재는 로비/화면전환 후속을 보류하고 멀티 전투의 GPU·렌더링/대기 원인을 분리하는 계획을 준비한다. 현행 방향은34절과 Plan31절이며 앞선 날짜별 기록과 고유 원본을 보존한다. 전체 성능 개선은 아직 입증하지 않았다.

- 시작: 2026-10-28 14:10 KST 메인 승인·폴더 지정. 문서 담당 현재 시각 조회는 14:21 KST. 최초 승인은 Research/Plan 작성이었고, 이후 사용자 「문서기준으로 남은작업들 진행해줘」로 계획 실행을 승인했다. 아래10소스는 앞선 구현 묶음의 기준이며 현재 단계1의 원인 조사를 진행 중이다. 이번 native export 분석에서 추가 제품 교정은 하지 않았다.
- 실제 프로젝트: `D:\Projects\Hexiege\Hexiege`, `codex/android-performance-optimization`, HEAD `33ce4257a55332cd7347419488edbbe0d45f216a`(직접 읽기 확인). 추가 제품10소스는 미커밋이며 다른 dirty 설정·에셋은 보존한다.
- 목표: Android 30 FPS(약33.3ms). 지속 성능·전체 평균/p95·긴 프레임·종료 burst의 통제 전후 비교는 미완이다.
- 계획은 [Plan.md](Plan.md), 상세 과거 실행은 [최적화 이력](../../../History/ImplementationVerificationHistory.md#android-performance-20261027-capture-review)에 연결한다.

## 1. 가장 먼저 바로잡을 사실

| 쟁점 | 현재 판정 |
|---|---|
| “최적화 모두 완료” | 추가 10소스 구현·CLR·Unity 컴파일 단계 완료다. 전체 Android 성능 수용·30FPS 지속·종료 burst 해결은 미완이다. |
| 원래 NCC 성능 측정 | 멀티 서버 전용 TickCombat이므로 **Android Host + Editor Client**에서 전후 측정한다. Android Client의 0B·미발생 marker로 PASS할 수 없다. |
| PC-RECOVERY 스택 생략의 역할 | `UnitView.BeginPostCombatTrace`는 network active·server일 때만 trace를 생성한다. 일반 경기의 순수 Android Client에서는 이 생성 경로가 실행되지 않으므로 해당 스택 생략의 기여를 기대할 수 없다. |
| Android 역할·빌드 식별 | 이전10:36/10:38의 역할은 미확정으로 유지한다. 새19:48 자료는 Host marker·기기로그의 Host/IL2CPP·buildGuid·설치APK hash까지 확인했다(11절). prebuild GUID0으로 정확 source 연결은 미완이다. |
| OnDisable 하위 로그 | Profiler에서 비용은 확인했으나 정확 메시지·레벨·stack·빌드 연결은 없다. 소스 후보를 실제18개 로그라고 단정하지 않는다. |
| 기능 검사와 성능 검사 | 원래 통제 메뉴 기능 검사는 Editor Host + Android Client, NCC 성능은 반대 역할이다. 최근 안내의 역할 혼용을 정정하며 과거 실제 수용은 취소하지 않는다. |

## 2. 원래 인계 범위와 현재 대조

원본은 [Handoff 전문](../../2026-10-25/19_44_android-30fps-profiling/Handoff.md), [Research](../../2026-10-25/19_44_android-30fps-profiling/Research.md), [Plan](../../2026-10-25/19_44_android-30fps-profiling/Plan.md), [manifest](../../2026-10-25/19_44_android-30fps-profiling/HandoffBaseline.json)다. Handoff 최신 변경 커밋은 직접 확인한 `8df8245738d1ca2d979b94892446364b6f8bdab7`이다. manifest의 준비 당시 미게시 필드·옛 Research는 당시 이력이고 Handoff 최상단의 게시 완료가 후속 상태다.

| 구분 | 인계한 것·남은 것 |
|---|---|
| 원래 구현 | 기존5source에 고정8marker, NCC의 ID목록/중복집합/제거목록3scratch 재사용. 콜백 뒤 snapshot·중첩 fallback·finally와 권위·순서를 보존했다. |
| 게시 비교 기준 | marker-only `070ddcafa368dcfb94fb76cb3218000a6cacc474`, optimized `ff8bdf9542f468f02788ff9b582f601785234fde`. `7c00f768d5c9f0ebb9583e93e4f6a4a45397f306`+두patch는 복원 대안이며 활성 본에 재적용하지 않는다. |
| 현재5source 대조 | 문서 담당이 bytes의 CRLF만 LF로 정규화해 optimized manifest와 비교: NCC·GameBootstrapper.Network·ProductionTicker·UnitView.Movement4개 일치. ProjectilePresenter는 후속 변경으로 다름. 활성 파일은 재작성하지 않았다. |
| 실행 장부 | Full Regression Suite·Root self-validation·Refactoring Verification8case, V01~V10, 넓은 리팩토링7계약의 상세 증거가 포함됐다. 추가 10소스 CLR 확인으로 대체할 수 없다. |

8marker: `Hexiege.NetworkCombat.TickCombat`, `Hexiege.NetworkCombat.TargetQueries`, `Hexiege.NetworkCombat.TickProjectiles`, `Hexiege.Economy.TickIncome`, `Hexiege.UnitView.ShouldEngage`, `Hexiege.UnitView.ShouldEngageAt`, `Hexiege.ProjectileView.Update`, `Hexiege.Bootstrap.RepathAllAliveUnits`. 원래5source와 추가10source는 ProjectilePresenter1개가 겹치므로15개라고 합산하지 않는다.

## 3. 추가 10소스에서 완료한 것

실제 diff·상세 이력·로컬 결과 파일을 대조했다. 아래는 구현/CLR·정적 확인 목록이며 Android 성능 개선 확정 목록이 아니다.

| 제품 소스 | 추가 구현 |
|---|---|
| Presentation/Effects/WavePresentationPresenter.cs | emitter delegate·경기 owner clock 재사용. |
| Application/Combat/WavePresentationCoordinator.cs | baseline 제거 predicate 재사용. |
| Application/Combat/WavePresentationDiagnostics.cs | 동일900byte 예산의 짧은1part 조립, 긴 payload 기존 fallback 유지. |
| Presentation/Effects/ProjectilePresentationPresenter.cs | private Visual 재사용·반환 뒤 초기화·512보관 상한,24표본 뒤 버릴 문자열 생성 회피. 공개 Slot은 미재사용. |
| Application/GameLog.cs | 최종 한 줄 조립의 중간 문자열 제거. |
| Infrastructure/Factories/UnitFactory.cs | OnAttackHit 추출 List/확장배열/ToArray를 정확 크기 배열로 대체·다음 snapshot 재조회. |
| Presentation/Unit/UnitView.cs | native marker 검증 List 제거·거부 순서 보존, recovery UTF-8 예산 전체 임시 문자열 제거. |
| Application/Combat/Sequencing/UnitAttackResultPresentationShadow.cs | Retire 제거 버퍼·retained scope 집합 재사용/finally 정리. |
| Application/Combat/Sequencing/AuthoritativeAttackPresentationCoordinator.cs | 두 캡처 predicate를 순수 key 검색으로 대체. |
| Infrastructure/Debug/ConsoleSink.cs | exception 없는 정확 Movement/UnitView/PC-RECOVERY Info만 NoStacktrace. **스택 생략은 의도한 정책 변경**이며 Warning/Error/Exception·다른 로그는 보존. |

CLR 결과 재확인: Wave출력8,020쌍, Visual8개 확인, Projectile진단2,048, Compose65,536, Factory marker8,004, native marker12,008, scheduler5,000, key검색2,827, recovery Emit6,000·Sink384조합. 해당 메서드/stub 범위이며 Unity API·NGO·멀티 전체 회귀 PASS가 아니다. 수치와 한계는 [상세 이력](../../../History/ImplementationVerificationHistory.md#android-performance-20261028-disable-log), `.utmp/` 확인 자료에 보존하며 Android 개선율로 바꾸지 않는다.

메인 직접 관측 전달값: 최종 DLL2026-10-28 03:39:33(소스03:28 이후), 최근250KB Editor.log의 새 error CS 미발견·강제 동기 컴파일 뒤 reload/refresh 확인. 기존 npm/MCP 등 Console 전체0건은 아니다. File 메뉴 Build And Run을 시도했고 완료 감시는 하지 않았다. 이후 사용자 테스트 보고는 유지하되 실행 APK와 이 source hash 연결은 아직 없다.

## 4. 미완과 최신 캡처가 말해 주는 것

[최신 분석 원문](../../../History/ImplementationVerificationHistory.md#android-performance-20261028-latest-captures):10:36:53·10:38:14 기록은145,038,792/163,547,100bytes,각2,000프레임이다. SM-N971N·Android12까지 확인됐고 role/backend/APK 연결은 미확정이다.

| 근거 | 확인된 사실 | 말할 수 없는 것 |
|---|---|---|
| 10:36:53 frame1193 | CPU75.46ms, NetworkPostLateUpdate49.36ms. OnDisable6회18.67ms→하위로그18회16.19ms, Invoke직속로그30회13.52ms. | 정확 메시지/레벨/stack, PC-RECOVERY 생략의 적용/효과. |
| 이전02:07:29 frame1769 | CPU116.72ms,OnDisable15회·하위로그12회. | 최신6회·18회와 부하가 달라 개선율·회귀 판정 불가. |
| 10:38:14 일반표본 | frame1624 32.93ms(Wait24.43ms),1627 33.22ms,1617 29.96ms. | 전체 평균/p95/최악·30FPS 지속이나 실제 계산33ms라는 결론. |
| 이전 로딩/Canvas | 02:08:40 frame1759 Preloading/Canvas 긴 구간. | 반복 rebuild의 원인·실제 종료/씬phase·GPU 병목 확정. |

부모/자식 Total·GC는 합산하지 않는다. 전체 통계·동일 부하 전후 비교가 없으므로 구현 완료와 성능 미완은 동시에 성립한다. 최신 Editor312행의 PC-RECOVERY0건·역할 혼재를 Android18로그 종류로 연결하지 않는다.

## 5. 검증 필요 장부

NOT_RUN은 이 채팅의 최신 묶음에 연결되는 직접 실행 근거가 없다는 뜻이다. 이전 사용자 컴파일·일반 플레이·개별 VFX/전투 수용은 유지한다. suite 무언급 PASS는 실제 사용자 실행 회차에 적용하며 실행 자체를 만들어내는 규칙이 아니다.

| 축 | 현재 상태·필요 증거 |
|---|---|
| NCC Android Host 전후 성능 | 미완. 같은 marker·역할·source 차이·조건의 두 빌드 필요. |
| 종료로그 교정 효과 | 미확정. 실제role/build/종료action·로그별 위치/레벨·frame 연결 필요. |
| FullRegression/Root/Refactor8 | FullRegression 확인된 PASS·Root 실제 메뉴 PASS에 이어 Refactor8 교정 뒤 native 재실행8PASS/0FAIL을 반영했다(10절). 최초6PASS/2FAIL과 EditMode 명시 cleanup의 한계를 보존하며 온라인 기능·실기와 분리한다. |
| V01~V10 | 최신 전체 수행 근거 없음: NOT_RUN,조건미발생 NOT_EXERCISED. V01건물경로/V02종료뒤진입/V03FIFO/V04연구거부/V05생산거부/V06UI수명/V07구독해제/V08heartbeat/V09이름소진/V10도구정정의 기존 장부 유지. |
| 넓은7계약 | 인증/Google partial·AI live provider/9값·강화반올림·인구4이벤트·건물영토·수입정수·partial/source gate/ILPP의 최신 상세 receipt 필요. 기존 수용 먼저 재사용,8case로 모두 덮지 않음. |
| 사용자 체감·출시 성능 | 실제 사용자 관찰은 존중. Release30FPS 지속·발열/입력 체감은 Development와 별도 미확인. |

## 6. 보류와 자료 보존

| 보류 | 이유·재개 조건 |
|---|---|
| NGO 사전pool | Instantiate14회/6회의 객체·소유권 미확정. 정확 경로/수명·비용부터 확인. |
| compaction·서버예약List·legacyVFX | 순서·재진입·피해/표현 수명 위험에 비해 현재 프레임과 원인 연결 부족. |
| 로딩 비동기화·Canvas재구성 | UIManager1초 뒤 동기LoadScene 구현과 로딩/경기gate 보존. 같은phase의 중복rebuild 직접 근거 필요. |
| 품질·수치·네트워크간격 | 이번 목표 밖. gameplay/권위/표현 계약 유지. |

[삭제/보존 이력](../../../History/ImplementationVerificationHistory.md#android-performance-20261028-field-captures):중복/옛PC6파일809,378,986bytes는 승인 삭제됐고 재생성하지 않는다. 기준17:44·23:11·새 캡처·미확정17:40, `.utmp/` 소스/결과·runtime 로그는 보존한다. raw 삭제/업로드는 하지 않으며 이번 문서는 추가 삭제 승인이 아니다.


## 7. 계획 실행 승인과 native export 조사 진행

사용자 「문서기준으로 남은작업들 진행해줘」에 따라 계획 실행을 시작했다. 원인 미확정 제품 변경을 먼저 하는 것이 아니라 단계1의 자료 복구·정확한 호출 연결부터 진행한다.

메인이 Unity에서 native export 파일을 얻었고 문서 담당이 그 로컬 `Library/ProfilerDiagnostics/log-samples-1192-bf6c6815a9f44ce981c2276353b1eea2.txt` 원문 파일을 읽었다. **후속 확인에서 export 도중 NullReferenceException이 발견돼 완주 성공이 아닌 부분 출력으로 정정한다.** header는 loaded-profiler-log-samples-v1, raw frame1192(first0,last1999)이며 UI1193과 raw index의1차이를 구분한다. export는 캡처 파일명·APK identity·network role을 추론하지 않는다고 명시한다. 원문은 여기 복제하지 않는다.

선택 프레임에서 실제 출력된 부분에는 LogStringToConsole56개와 OnDisable6개, 총62개 sample block이 있다. 출력된62개에서 metadataCount=0·callstackCount=0을 확인했다. Profiler ancestor의 OnDisable·NetworkPostLateUpdate·일부 coroutine/transport 경로는 읽을 수 있지만 정확 메시지·레벨·실제 source callstack은 이 export로 복구할 수 없다. 메인 전달 Editor.log에서 Export.Export의 NullReferenceException(당시 line76)을 확인했고, 문서 담당이 파일18,397bytes·Render Thread(thread1) 시작에서 끝나며 완료 END가 없음을 다시 읽었다. sample name null 처리 가능성을 전문 프로그래머가 확인 중이며 수정 후 재실행 예정이다. 따라서 이0건 결론은 **추출된 부분 범위만** 가리키고 전체thread 순회·선택프레임 완주·캡처 모든프레임의 metadata 부재로 확대하지 않는다.

실제 지연을 줄이는 제품 교정은 아직 없다. 메인/전문 프로그래머 전달값으로 좁은 fallback 계측2소스와 prebuild manifest 준비 및 출력 동등성 확인을 진행했고 메인은 DLL16:40:15/19의 갱신을 확인했다. 상세 구현·빌드 연결/새실기는 아직 별도 확인 전이므로 이를 원인 교정·성능 PASS로 취급하지 않는다. 부분 native export를 원인 확정·PC-RECOVERY 비용 확정으로 바꾸지 않는다. 최신 APK/source 연결과 비교 가능한 전후 통계·기능 receipt는 계속 미확정이다.


## 8. fallback 계측 구현과 첫 native gate 확인

메인/전문 프로그래머가 보고한 좁은 계측 구현을 실제 파일과 로컬 `.utmp/profiler-export-20261028/InstrumentationResults.txt`에서 확인했다. 이 단계는 원인 식별 준비이며 실제 지연을 줄이는 추가 제품 교정이 아니다.

| 준비 | 구현·한계 |
|---|---|
| UnitView.OnDisable | 기존9개 cleanup 경로에 정적 `Hexiege.UnitView.Disable.*` marker를 넣었다. Editor/Development guard이며 Release 원래 본문/순서 동일을 CLR에서 확인했다. |
| ConsoleSink | 실제 호출 시점의 role·category·route를 나누는 정적 marker15개. inactive/starting은 Unknown으로 구분해 이전 Host/Client를 추정하지 않는다. startup의 `[CAPTURE-IDENTITY]` 한 줄은 buildGuid/version/backend/Unity/process/startupUtc를 직접NoStack로 남긴다. 이는 기존 사건로그와 별도의 의도한1회 식별 출력이며 Release에는 없다. |
| Development prebuild manifest | `Assets/Editor/Tools/Combat/PerformanceCaptureBuildManifest.cs`가 source12경로(기존제품10+exporter+manifest도구)의 정확bytes SHA256과 buildGuid/options/package/version을 `Library/ProfilerDiagnostics`에 시도별 기록하도록 구현됐다. **prebuild source receipt이며 성공·설치APK·실행앱의 증명이 아니다.** |
| 동등성·정적 검토 | 파일 결과:Development Sink2,304조합·Release384조합,identity정확1회NoStack·Release부재,Release OnDisable본문/순서동일. 메인전달 QA4파일읽기 검토 actionable결함없음. 실제Android marker·header·manifest연결은 다음빌드/실기에서 확인해야 한다. |

메인은 DLL16:40:15/19 갱신을 관측했다. 이후16:46근처 실제 Unity Full Regression Suite 메뉴 실행을 마쳤고 최근180KB Editor.log에서 UAS-DIAG self-validation·PRODUCTION-TIMELINE·C2-AUTHORITY·C3-PRESENTATION-SHADOW의 PASS를 직접 확인했다고 전달했다. 확인한 이름/범위의 native gate 증거로 반영하며 Root 별도 메뉴·Refactor8case·온라인V01~V10·성능 비교의 PASS로 확대하지 않는다.

exporter null 처리 교정 뒤 컴파일/재실행, Root 별도 self-validation·Refactor8case, File 메뉴 Build And Run 시도는 아직 남았다. native 부분출력의 한계는7절대로 유지한다. 계측이 실제 역할/build/category/route를 잡아야 다음 제품 교정 파일을 확정할 수 있다.


## 9. exporter 완주·Root 확인과 Refactor fixture 후속

7절의 최초 부분 출력은 실패 이력으로 유지한다. null 처리 교정 뒤 메인이 exporter를 재실행했고 문서 담당은 `Library/ProfilerDiagnostics/log-samples-1192-f231ae6c800643fea8e43b9dff90d881.txt`의26,537bytes와 header/완료END를 다시 읽었다. raw1192/UI1193, Main Thread frameMs=75.461235, scannedThreads=36,limited=False,completed=True,exported=62다. 이번 출력은 선택 프레임의36thread 순회를 완료했다. exported62개 모두 metadataCount=0·callstackCount=0이어서 선택 샘플의 정확 메시지·레벨·source stack을 되살릴 수 없다. 전체 캡처의 다른 프레임까지 부재라고 일반화하지 않는다. ancestor의 범위는 sample index/재귀자식 연속구간과 timing을 대조해 읽으며 같은 hierarchy Total을 중복 합산하지 않는다.

메인은 Root self-validation 실제 메뉴의 PASS를 확인했다. 별도 Refactoring Verification 첫 Run(보고된 식별 접두89f5)은8case 중6PASS/2FAIL이었다. 실패를 삭제하거나 묵시PASS로 바꾸지 않는다. 전문 프로그래머가 Offline EditMode의 일반 MonoBehaviour callback 자동 실행 가정이 fixture 문제임을 확인했고, 문서 담당은 `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.VerificationRuntime.cs` 실제diff를 읽었다. 패널 OnDisable·ticker OnDestroy의 실제 cleanup을 명시 dispatch하고 UniRx destroy trigger를 명시 통지하도록 수정했다. 이는 **Editor 검사 fixture 교정이며 제품 cleanup 동작 변경이 아니다.** EditMode 명시 dispatch 통과와 실제 PlayMode lifecycle dispatch를 구분한다. 교정 뒤 컴파일·native 재실행 결과는 아직 대기다.

다음 새 기기 측정은 Android Host + Editor Client로 역할을 고정한다. 밀집 전투30초를 저장하고, 포기 직전 Record를 새로 시작해 결과 화면 안정 뒤10초까지 별도 저장한다. 앱을 살린 채 Record 정지→Save한다. 메인은 사용자의 완료 보고 후 다음 분석에서 기기 role/build/session 로그를 자동 캡처할 수 있으며 이것은 아직 실행하지 않은 후속 절차다. 기존10:36/10:38 캡처를 이 역할의 증거로 재분류하지 않는다. File 메뉴 Build And Run 시도·설치/새실기는 아직 후속 상태로 구분한다.


## 10. Refactor8 native 재실행 완료와 빌드 전 상태

메인 직접 Editor.log 관측 전달값으로 Refactoring Verification 재실행 Run `615331741f2c4fb3abfb9b12b2426c0c`의 BEGIN~END가 연결됐고 종료17:29:40.935, Cases8/Passed8/Failures0을 확인했다. 최초 Run의6PASS/2FAIL은9절에 보존한다. 변경한 것은 `RunUnitActionSelfValidation.VerificationRuntime.cs` Editor fixture 한 파일이며 제품 cleanup은 바꾸지 않았다. UI/Ticker의 `EditModeExplicitCleanupDispatch;NotPlayLifecycleDispatch` EVIDENCE를 유지하고 실제 PlayMode callback 자동 전달·기기/멀티 전체 기능 수용으로 확대하지 않는다.

메인 전달 최종 Editor DLL 컴파일 갱신은17:19:50, runtime DLL 갱신은16:40:15다. 메인이 실제 File 메뉴의 Build And Run 항목을 클릭해 한 번 시도했다. 클릭 뒤 빌드 UI·로그·프로세스·APK의 진행/완료 감시는 전혀 하지 않았다. 상태는 시도 완료이며 빌드 성공·설치·새 기기 실행 결과는 미확인이다. Android Host의 새 계측 캡처·실제 build/session/role 연결·전후 통계와 종료 burst의 원인 교정/성능 수용은 계속 미완이다.

후속 사용자 캡처는 Android Host + Editor Client에서 밀집 전투30초와 종료 구간을 별도 파일로 저장한다. 포기 직전 Record를 새로 시작해 결과 화면 안정 후10초까지 기록하고 앱을 살린 채 Record 정지→Save한다. 저장 위치는 기존 `D:\Projects\Hexiege\Hexiege\ProfilerCaptures`다. 사용자의 완료 보고 뒤 새 marker·identity와 기기 role/build/session 로그를 연결하며 이전 캡처를 재사용하지 않는다.


## 11. 19:48 새 Host 캡처 최종 분석 — 원인 분해 성공·전후 비교 미완

사용자 새 테스트·저장 뒤 원문을 분석했다. 이번 작업의 제품 코드 추가 변경은0이며, 기존 Editor export 도구1파일을 통계 추출에 필요한 범위로 확장했다. 새 테스트 실패로 판정한 것이 아니라 계측이 실제 Host 비용을 나눠 보여 주는 데 성공한 단계다.

### 11.1 파일·역할·APK 연결

문서 담당이 파일 크기·CSV원문·기기 identity를 직접 읽었다. 메인이 UI에서 로드한 파일과 export 대응은 다음과 같다.

| 캡처 | data bytes | 완료 CSV(Library/ProfilerDiagnostics) |
|---|---:|---|
| Hexiege_2026-10-28_19-48-28.data | 183,911,436 | frame-summary-0e5795409d944675bf590d4485fc0aa0.csv |
| Hexiege_2026-10-28_19-48-32.data | 170,240,016 | frame-summary-453849f13c644219aeea5c2943874bb8.csv |

두 CSV의 END는 completed=True·valid2000·scanned2000·invalidSkipped0·limitedFalse다. runtime 기기 로그 `.utmp/device-analysis-20261028/unity-logcat-current.txt`에서 buildGuid `394a2cee75a84a83af94abf8009846f0`, backend IL2CPP, process20056을 읽었고 Host 역할 표식과 CSV Console.Host marker가 확인됐다. 기기로그 집계 전달값은19:46~19:49 Unity15,094행·Host727표식이다(수집 범위의 건수이며 CSV 두 파일의 합산값이 아님).

메인/프로그래머 대조로 로컬 빌드 APK와 설치APK의 SHA256 `81B26D2C85C95031D9CB280D81162C81B832777FA8EECC3CA2361FB19A3A5564`가 일치했다. 다만 prebuild manifest의 GUID가0이므로 이 실행GUID와 source12경로 manifest를 직접 연결하지 못했다. 앱/역할 식별이 나아진 사실과 정확 source 수용은 다른 축이다. 이전 역할 미확정 캡처를 소급해 Host로 재분류하지 않는다.

### 11.2 전체 저장 범위 통계와 두 파일의 겹침

문서 담당이 CSV를 다시 계산했다. 아래는 mainThreadFrameMs의 저장 범위 전체 통계이며 대기·로딩·전투 등을 포함한다. p95는 정렬한2,000개 중 nearest-rank95% 표본이다.

| 기록 | 유효N | 평균ms | p95ms | 관측최대ms | >50ms | >100ms |
|---|---:|---:|---:|---:|---:|---:|
| 19:48:28 | 2,000 | 33.975934 | 34.343426 | 442.400818 | 8 | 4 |
| 19:48:32 | 2,000 | 33.951417 | 34.245850 | 442.400818 | 7 | 4 |

**두 파일은 독립 전후 쌍이 아니다.** CSV1의 raw134~1999와 CSV2의 raw0~1865,1,866행이 frame index를 제외한49수치 열 전체에서 완전히 같다. CSV2에 추가된134행의 관측최대는41.135769ms다. 거의 같은 history 창을4초 간격으로 저장한 겹침이므로 평균 차이를 최적화 개선율로 계산하지 않고 표본수를4,000독립프레임으로 합산하지 않는다. 이 통계로 Release30FPS 지속·모든 Android 수용을 주장하지 않는다.

### 11.3 실제 큰 비용과 기존 의심의 분리

| 위치·근거 | 확인된 비용 | 해석 한계 |
|---|---|---|
| CSV1 raw313 / UI314(메인UI관측) | frame442.4ms. NetworkEarlyUpdate396.71ms → ServerRpc395.56ms. Instantiate204회252.80ms·4.7MB, AddComponent374회22.77ms, LogString7회13.19ms. | 이번 큰 spike는 서버RPC 안의 대량 생성 비용이 두드러진다. 정확 prefab·생성자/owner·생산/재로드 phase는 아직 연결하지 않았다. 부모/자식 시간을 더하지 않는다. |
| CSV1 raw1476 / UI1477(메인UI관측) | frame375.2ms. UpdatePreloading289.14ms·1.4MB, UpdateScript53.01ms·384KB. | 실제 로딩 처리 비용은 확인했으나 목적지scene·게임종료/재경기 단계·반복UI rebuild 원인은 미확정이다. |
| CSV2 raw140 / UI141(메인UI관측) | frame163.83ms. FinishFrameRendering66.20ms·0.8MB, NetworkEarlyUpdate46.19ms→ServerRpc45.82ms→Console.Host23회35.50ms→Console.Other23회35.44ms. OnDisable12회2.52ms·67.2KB 안 PostCombat12회2.36ms·66.6KB/HealEnd12회0.04ms·0.6KB. DelayedDynamicFrameRate45.79ms·0.8MB. | Console.Other35.44ms는 OnDisable 밖 ServerRpc의 직속 sibling 경로다. OnDisable 하위 로그로 합치지 않는다. 로그 비용은 완전히 해소되지 않았고 정확 Other 메시지/phase는 미확정이다. |
| CSV1 OnDisable 계측 총합(문서담당재계산) |26회. HealEnd inclusive총0.090114ms, PostCombat inclusive총3.020188ms. | 앞선 큰 OnDisable 로그를 HealEnd/PC-RECOVERY로 미리 확정할 수 없고 이번 자료에서 두cleanup이 전체 큰 spike를 설명하지 않는다. 서로 다른 category/route의 중첩 Console marker를 합산하지 않는다. |

Console role/category/route와 OnDisable 세부 marker가 실제 자료에 들어왔다. 그러나 frame·명시 종료action·runtime message를 완전히 일치시키는 phase 연결은 아직 미완이다. 정확 생성대상/owner·loading scene phase와 ServerRpc 직속 Console.Other의 메시지를 현재 자료에서 더 좁힌다. 힐/PC cleanup 비용이 작다는 관측을 전체 로그 병목 해소로 해석하지 않으며 사용자에게 추가 테스트부터 반복 요청하지 않는다.

### 11.4 Shadow 진단의 별도 실패 경계

프로그래머가 같은 session/run의 terminal 전 primary480행이 MaxNormal512−32=480 상한을 채웠고 마지막 기록이19:46:11.194임을 확인했다. terminal dropped769의 세부 stage는 상한 뒤 기록이 없어 복원할 수 없다. dropped>0은 이 진단 overallFAIL의 충분 원인이지만 **피해 누락769회나 게임플레이769실패의 증거가 아니다.** 이 진단의 완전성 문제를 성능 캡처의 테스트 실패·피해 결함과 분리해 별도 미완으로 남긴다.

### 11.5 다음 우선순위와 유지할 보류

1. 현재 raw313/UI314에서 Instantiate한 정확 prefab·호출자/owner·생성 수명과 source를 연결해 재사용/pool이 안전하고 필요한 경로인지 판단한다. NGO 객체라고 단정하거나 network pooling을 바로 구현하지 않는다.
2. raw1476/UI1477의 loading 목적지scene·실제 phase를 연결하고 기존 loading/맵gate를 보존하는 분산 가능성을 검토한다. 측정 한 프레임만으로 비동기로딩·Canvas재구성을 확정하지 않는다.
3. 기존10소스의 원래 marker-only/optimized 통제 전후 비교와 Release30FPS·온라인V01~V10 등 미완 장부는 유지한다. FullRegression/Root/Refactor8의 확인된 native 수용을 재사용하되 위 실기를 대신하지 않는다.

11절 분석 시점에는 제품 추가 교정을 하지 않았고 파일 삭제·raw로그 업로드도 하지 않았다. 이후 사용자 문서 기록·다음 교정 진행 승인과 현재 상태는12절을 따른다. 승인과 무관하게 원인 미확정 NGO pool을 바로 구현하지 않는다.


## 12. 사용자 기록 승인·다음 교정 조사 착수

사용자가 이번 분석의 문서 기록과 다음 교정 진행을 승인했다.11절의 전체 통계·겹침·Host/APK 연결과 source 미연결·남은 Other로그/생성/로딩 비용이 이미 기록돼 있음을 다시 대조했다. 이전의 제품교정 미착수는 그 분석시점 사실이며 현재 승인 범위를 막는 뜻이 아니다.

전문 프로그래머가 기존 자료와 source에서 ServerRpc 생성의 prefab/owner·수명,loading scene phase,Console.Other 메시지 경로를 연결하고 있다. 다음 교정의 정확 설계·파일·소유권 보존안을 확정하기 전이므로 구현 완료·성능 개선·NGO pool 적용으로 기록하지 않는다. 이 승인 범위에서 근거가 확정된 좁은 교정과 필요한 검증을 진행하고, 원래 사양/소유권 변경이나 새 범위는 분리해 확정한다.

이번 문서 갱신은 기존 Research/Plan에만 했으며 새 Testcase나 제품/씬/설정·Git 변경·삭제/업로드를 수행하지 않았다. 최종 구현·검증·빌드 결과는 실제 근거를 받은 뒤 별도 갱신한다.


## 13. 재경기 로그의 좁은 교정안 확정·구현 확인 중

사용자 다음 교정 진행 승인에 따라 메인이 범위를 아래 두 위치로 확정했다. 문서 담당은 실제 ConsoleSink/GameEndUI diff·발생부 Dev.Info와 `.utmp/rematch-log-correction-20261028/UiResults.txt`를 읽었다. Instantiate204회·Preloading289ms의 정확 원인은 여전히 불완전하므로 그 경로의 pool/비동기화 교정은 하지 않는다.

| 위치 | 확정 범위·의도한 변화 |
|---|---|
| ConsoleSink | exception=null·Info·system=Network·class=NetworkGameEndController·message가 정확히 `StartRematch: 동적 NetworkObject Despawn`인 한 종류만 기존 선택 스택 생략에 추가한다. 실제 발생부는 Dev.Info다. 새 특례는 data가 Event=로 시작하는 Ops를 제외하도록 구현됐으며 전문 프로그래머가 그 경계 테스트를 보완 중이다. **Info 호출스택 생략은 의도한 로그 정책 변경**이고 본문·횟수·순서·Ops/Warn/Error/Exception의 기존 경로를 유지한다. |
| GameEndUI.BuildRematchDiagFields |6개 상태값을 원래 순서로 읽고 같은 필드/구분자로 최종 문자열을 만든다. extraFields가 있을 때 기본 state 문자열을 만든 뒤 다시 복사하던 중간 문자열1개를 피한다. “6보간의 모든 중간 할당 제거”라고 주장하지 않는다. |

메인 보고로 device raw에서 해당 로그14행과 StartRematch→MapReadyServerRpc stack 경로를 직접 확인했고 전문 프로그래머의 전수 대조는 진행 중이다. 이 로그를 앞 캡처의 모든 Other35ms나 전체 spike와 완전히1:1 연결했다고 확대하지 않는다. gameplay·NGO수명·씬·material은 이번 제품 교정 대상이 아니다.

UiResults는 실제 함수의64개 bool조합×10개 extraFields×4개 culture=2,560조합에서 출력/UTF-8/read순서 동일을 보고한다. warm CLR 할당은 extra없음400→400B(절감 없음),3글자 extra664→408B,2,048글자4,752→4,496B다. 실제 Unity/Android 시간 개선율이 아니다. Sink의 Ops 제외 경계 검증·Unity 컴파일은 아직 최종 확인 전이며 이 두 변경의 native/실기PASS로 쓰지 않는다.

Development manifest에는 GameEndUI를 추가해13source경로를 기록하도록 수정됐다. prebuild GUID0과 실제 실행GUID 연결 미완은 이번 목록 확장만으로 해소되지 않는다. 최종 구현 검토·컴파일·필요 native gate·빌드 시도/실기 결과는 후속 근거로 별도 갱신한다.


### 13.1 Sink 동등성·Unity 컴파일 후속 확인

문서 담당이 `.utmp/rematch-log-correction-20261028/SinkDevelopmentResults.txt`와 `SinkReleaseResults.txt`를 직접 읽었다. Development/Release 각각56,448조합의 전후 출력·횟수·레벨·예외 검증 PASS이며 각18조합은 의도한 좁은 NoStack 변화다. 기존384조합도 PASS다. Ops제외와 Warning/Error/Exception 보존의 해당 harness 범위 확인이며 실제Android 시간 개선은 아니다.

메인 직접 관측 전달값으로 최종 source 뒤 runtime DLL21:36:04·Editor DLL21:35:57이 갱신됐고 Unity busy해제를 확인했다. 최근120KB Editor.log의 C#오류0과 refresh137.254초를 확인했다. 이는 이번 소스의 Unity 컴파일 확인이며 기존 Console 모든오류0이라는 뜻은 아니다. 최종 QA읽기 검토는 아직 대기다. File 메뉴 Build And Run은 준비 중으로 아직 클릭 전이며 새 기기 실행·Profiler 성능 측정은 하지 않았다.


### 13.2 최종 source·QA 확인과 재경기 전용 빌드 시도

메인이 최종 ConsoleSink·GameEndUI·PerformanceCaptureBuildManifest3source hash를 전문 프로그래머의 최종본과 대조해 일치를 확인했다. 최종 QA는 helper·Ops MergeData의 Event= 제외·manifest13entry를 읽고 추가 blocker없음을 확인했다. 앞13.1의 QA대기/클릭전 상태는 이 결과로 갱신한다. runtime DLL21:36:04·Editor DLL21:35:57이 최종 source보다 새롭고 최근120KB의 error CS0·Unity busy해제 확인은 유지한다.

메인이 실제 File 메뉴 Build And Run 항목을 클릭해 시도했다. 클릭 뒤 빌드UI·로그·프로세스·APK의 진행/완료 감시는 전혀 하지 않았다. 상태는 **시도 완료**이며 성공·설치·새 기기 실행/성능 효과는 미확인이다.

이번 교정을 확인할 다음 구간은 Android Host + Editor Client의 **재경기 수락 직전 Record 시작 → 새 경기 진입 후10초까지 기록 → Record 정지·Save**다. 앱을 살린 채 기존 ProfilerCaptures에 저장하고 파일명에 `rematch`를 붙이는 것을 권장한다. 앞선 밀집 전투/포기 측정은 당시 시나리오이며 이번 재경기 Despawn 로그 교정의 직접 확인을 대신하지 않는다. 동일 역할·로그route/건수·build/session을 연결해 실제 stack비용·할당과 큰 spike를 다시 분리한다. 정확Instantiate prefab/owner·로딩scenephase·독립전후쌍·Release30FPS는 계속 미완이다.


## 14. 2026-10-28 22:54 테스트·정책 적용 확인과 자료 정리(10-29 기록)

사용자 새 테스트 분석·삭제 내역의 기존 문서 반영 승인을 이어 기록한다. 이번 분석에서 제품 코드를 추가 수정하지 않았다. 앞13절의 새 실기 미확인은 다음의 정책 적용 관측 범위로만 갱신하며 전체 최적화 완료로 바꾸지 않는다.

문서 담당이 `Hexiege_2026-10-28_22-54-35.data` 270,551,420bytes와 `Library/ProfilerDiagnostics/frame-summary-35d0f6abce584969bc52aab47e7bcb39.csv`를 읽었다. END completed=True·valid/scanned2,000·invalidSkipped0·limitedFalse다. mainThreadFrameMs 재계산은 평균33.561749ms,p95(nearest-rank)34.322117ms,관측최대367.316895ms,50ms초과3프레임·100ms초과1프레임이다. 대기/로딩을 포함하는 저장 범위 통계이며 Release30FPS 지속의 증거가 아니다.

메인 UI 직접 관측 전달값: raw1420/UI1421의367.32ms 중 UpdatePreloading278.32ms·1.4MB,ScriptRunBehaviourLateUpdate51.11ms·384KB,DelayedStartup17.09ms·85.7KB다. 큰 로딩 지연은 남아 있으며 중첩 비용을 독립항처럼 더하지 않는다.

기기 raw `.utmp/device-analysis-20261028/unity-logcat-20261028-231225.txt`에서 문서 담당은 실행GUID `7241dbfdd897465c96a8aa60ce0bbecc`·IL2CPP·PID28253 및 정확 재경기 동적 Despawn Info20개 header를 직접 확인했다. 메인/프로그래머 전수 대조로 Host 실행의 대상20/20은 스택0이고 Warning42/42는 기존 스택을 유지했다. Exception은 발생하지 않아 실제 예외 스택 보존의 새 실기 PASS를 만들지 않는다. **이번 좁은 Info 정책이 기기에서 적용된 사실은 확인**했지만 이것만으로 전체 로그/수명·최적화 성능을 수용하지 않는다.

중요한 측정 한계: 대상 재경기20로그의 시간 구간은 이번 CSV에 포함되지 않았다. native profile raw1390에서 확인한 NoStack15개는 PC-RECOVERY이며 이번 재경기 Despawn20개와 다르다. 따라서 이번 자료로 대상20개의 절감ms·전체 프레임 개선율을 산출할 수 없다. 이번 시나리오는 정상 종료 두 경기 뒤 재경기·로비이며 이전 포기 시나리오와 달라 독립 전후쌍으로 비교하지 않는다. 테스트 실패가 아니라 정책 적용은 확인하고 그 효과ms는 미확인인 결과다.

**승인된 삭제 내역:** 메인이 D드라이브 절대경로·대상 containment·개별 파일을 검증하고 native PowerShell LiteralPath로3파일 합계170,274,612bytes를 삭제했다. 문서 담당도 아래3경로가 없음을 다시 확인했다.

- `ProfilerCaptures/Hexiege_2026-10-28_19-48-32.data` 170,240,016bytes.
- 같은 이름 `.highlights` 16,199bytes.
- `Library/ProfilerDiagnostics/log-samples-1192-bf6c6815a9f44ce981c2276353b1eea2.txt` 18,397bytes.

19:48:32는 완전 중복이 아니다.19:48:28과1,866행이 겹치고134개 고유 quiet프레임이 있었으므로 **그134프레임의 깊은 원본 sample 탐색 능력은 잃었다.** 해당 summary CSV `frame-summary-453849f13c644219aeea5c2943874bb8.csv` 248,499bytes는 보존해 통계/49수치 대조는 가능하다. 삭제한 partial62sample은 완료본 `log-samples-1192-f231ae6c800643fea8e43b9dff90d881.txt`에 포함돼 있으며 완료본은 남겼다. 메인 및 문서 담당이 최신22:54·기준19:48:28 존재를 확인했다. raw runtime·하네스·다른 기준은 보존했고 추가 삭제/업로드를 하지 않았다. 앞 문단의 삭제 파일명은 당시 관측 이력이지 현재 존재를 뜻하지 않는다.

남은 일은 큰 Preloading의 정확scene/phase·대량 생성prefab/owner·Other로그의 남은 비용·source와실행빌드연결 및 비교 가능한 전후 측정이다. 이번 정책 적용20개 확인을 그 미완 항목의 PASS로 확대하지 않는다.


## 15. 2026-10-29 로딩 교정의 원인 연결·대량 생성 조사 승인

사용자가 다음 로딩 교정 작업안에 「진행해」라고 명시 승인했다.14절의 분석·정책 적용/측정 한계·삭제/보존 내역은 그대로 유지하고 이 Task에서 이어 진행한다.

현재 단계는 큰 UpdatePreloading의 정확한 목적지scene·시작/종료/재경기 phase와 source를 연결하고, ServerRpc의 대량 Instantiate가 만드는 실제 prefab·owner·수명을 조사하는 것이다. 승인된 조사·교정 범위 안에서 근거를 확정하되 아직 구체 설계·교정 파일은 미확정이다. 비동기로딩 전환·NGO pool·게임플레이/씬 gate 변경을 이미 결정하거나 구현 완료로 기록하지 않는다.

실제 원인·교정안·구현·동등성/Unity/실기 근거를 받은 뒤 해당 축만 후속 갱신한다. 제품 성능 완료나 로딩 지연 해결은 아직 아니며 새 Testcase는 만들지 않는다. 이번 문서 갱신에서 제품/씬/설정·Git 변경·추가 삭제/업로드를 수행하지 않았다.


## 16. 로딩 owner 확인과 RankingView 행 풀 지연 생성 교정

15절 조사에서 확인한 저위험 부분만 구체안으로 확정해 구현을 진행한다. 문서 담당이 native object export의 크기/완료END, `.utmp/ranking-lazy-pool-20261029/OwnerEvidence.txt`·`Results.txt`,실제 RankingView diff를 읽었다.

native 원본 `Library/ProfilerDiagnostics/object-samples-1420-cd29cc7700794b71a0875852888b227a.txt`는3,211,345bytes,exported5,823·scannedThreads40·completedTrue·limitedFalse다. 메인/프로그래머가 EntityId를 복원해 RankRow.original22112에 해당하는 Instantiate10개·inclusive18.28334594ms를 찾고 LobbyRoot/RankingPanel metadata 및 Lobby.unity의 rowPrefab GUID와 대조했다. source owner는 `RankingView.InitializeIfNeeded → BuildRowPool(10행)`이다. 이 비용은 Awake/integration 안의 중첩 시간이며 부모Total에 더하지 않는다. 전체 UpdatePreloading278ms나 이전 ServerRpc의204Instantiate 전체를 이 owner라고 단정하지 않는다.

제품 교정은 `Presentation/UI/Views/Lobby/Ranking/RankingView.cs` 한 위치다. 로비 초기화에서 행10개를 만들지 않고 첫 RenderCurrentPage 직전에 준비한다. 서비스·초기 배선Warn·버튼/헤더·visibility 초기화는 유지한다. 생성할 행은 기존처럼10개이고 처음 만든 뒤 재사용하며 기존 열 정규화(열0 고정 포함)·minHeight96·preferredHeight−1·flexibleHeight0 표시 규격을 유지한다. 생성 직후 슬롯에 등록해 Clear/layout 일부 실패 뒤 재시도에서 중복 생성하지 않고 preparing/closed guard로 재진입/파괴된view 작업을 막는다.

**비용을 없애는 것이 아니라 첫 랭킹 표시로 옮기는 tradeoff**다. 랭킹을 열지 않는 로비 진입은 행 생성을 피하지만 첫 열기에서 비용을 낼 수 있다. 전체278ms 제거·로딩 완전 해결·Android 개선율을 약속하지 않는다. gameplay·씬gate·NGOpool·material 전체 공유·AsyncScene 전환은 이번 교정 대상이 아니다.

Results.txt는 실제 메서드의 lifecycle/render/layout/failure/reentry10case PASS(실제 NormalizeColumnWidths 포함)를 보고한다. QA읽기 검토의 추가blocker없음은 메인전달값이다. Development prebuild manifest는 RankingView를 더해14source경로가 됐고 native읽기도구의 EntityId 출력도 확장했다. Editor의 focused fixture `ValidateRankingLazyPool`을 등록했으며 새 Testcase.md는 만들지 않았다. **현재 Unity 컴파일·해당 fixture 실제 실행은 메인이 진행 중**이며 그 완료·기기시각/첫열기/실제성능 수용은 아직 기록하지 않는다.


### 16.1 Unity 컴파일·focused fixture 실제 실행 확인

메인 직접 관측 전달값으로 runtime DLL2026-10-29 05:54:05·Editor DLL05:54:11은 RankingView source05:13:35·fixture05:29:09보다 새롭다. 최종 QA읽기 검토는 blocker없음이다. 이전 CLR10case와 별도로 Unity의 focused fixture를 실제 실행해 Run `aef220da5719406fa2d06c0e480a8426`의 BEGIN/ASSERTIONS/END를 확인했다.

ASSERTIONS는 InitRows0·FirstRows10·Reused10·PartialRetained3·Final10·PreviewTrue·ClosedTrue이며 END PassedTrue·AssertionsPassedTrue·CleanupCompletedTrue다. **EditModeExplicitCleanup / NotPlayLifecycleOrVisualAcceptanceTrue**의 명시 범위를 유지한다. 이는 Unity에서 실제 메서드의 초기0행·첫10행·재사용·부분실패복구·닫힘 처리를 확인한 것이고 PlayMode callback 자동전달·화면배치/첫열기체감·Android 성능 수용은 아니다.

메인이 실제 File 메뉴를 열어 Build And Run 항목을 클릭하는 빌드 시도를 완료했다. 사용자 지시대로 클릭 이후 UI·로그·프로세스·APK 등 진행/완료 확인을 전혀 하지 않았으며 **완료 여부는 사용자 확인**이다. 빌드 성공·설치·새 기기 실행/성능은 아직 미확인이고 fixture 뒤 제품을 추가 수정하지 않았다. 앞선204Instantiate의 owner 추가 조사와 전체 async전환은 이번에 진행하지 않았으며 잔여로 유지한다.


## 17. 남은 최적화 묶음 선행·통합 검증 안내 승인

사용자가 「남은 최적화 먼저 다 마무리하고 구체적 검증 테스트 절차 안내」를 승인했다. 이 Task를 이어 현재 장부의 남은3축(대량Instantiate의 정확owner/수명,잔여로딩의scene/phase,Console.Other 등 남은로그비용)을 조사하고 근거 있는 범위를 구현·코드 검증한 뒤 통합 사용자 안내로 전달한다. 완료예정과 실제완료는 분리한다.

RankingView의 지연행풀 구현·CLR·Unity focusedfixture PASS와 직전 File Build And Run 시도는16절 근거대로 유지한다. 기기 첫랭킹열기/재열기·로딩 개선량과 사용자 실기는 아직 완료로 쓰지 않는다. 이번 승인도 원인 미확정의 NGO pool·전체async전환·품질저하를 자동 확정하는 뜻이 아니다. 실제source/owner·계약·위험이 확인된 수정안만 수행하고 남는 불확정은 이유와 함께 남긴다.

순서는 조사→근거있는구체안/구현→적절한CLR·정적/Unity검증→한 번의 통합 사용자 절차 안내다. 새 Testcase.md를 만들지 않고 [Plan13절](Plan.md#13-남은-묶음-마무리와-통합-사용자-검증-안내)의 테스트표를 최종 구현에 맞춰 채운다. 사용자 실행 전에는 실기PASS를 기록하지 않는다. 현재 문서 갱신에서 제품/씬/설정·Git 변경·추가 삭제/업로드는 하지 않았으며 후속 실제 결과를 기다린다.


## 18. 대량 생성 owner 확정 — HexTile의 저장 mesh bake 교정 준비

17절의 대량 생성 조사에서 메인/전문 프로그래머가 raw313의 실제 생성owner를 연결했다. 문서 담당은 `Library/ProfilerDiagnostics/object-samples-313-14cd128c7e54455dab4975d7e4b42103.txt` 769,649bytes와 완료END(exported1,462·scannedThreads35·completedTrue·limitedFalse)를 읽었다. 전수대조 전달값으로 전체 Instantiate204개252.80927152ms 중 HexTile187개246.89904252ms다. 이 시간은 포함Total이며 부모RPC/전체frame에 더하지 않는다.

현재 대상은 `Assets/_Project/Prefabs/Tiles/HexTile.prefab`이다. 문서 담당이 prefab의 ProBuilderMesh/Shape와 저장 m_Mesh fileID0을 확인했다. 전문 프로그래머의 package/source 추적에서 저장mesh가null인 구조가 runtime Awake rebuild로 이어짐을 확인했다. 이 근거로 메인은 특정 HexTile의 baked mesh asset 교정 범위를 확정했다. 사용자 남은 최적화 구현 승인에 따른 **해당 prefab/baked mesh·필요 Editor도구의 좁은 예외**이며 다른asset/scene/material을 임의 변경하는 권한은 아니다.

구체안은 Editor에서 현재 기하를 저장mesh로 bake하고 runtime ProBuilder 재계산을 피하는 도구를 준비하는 것이다. 적용 전후 기하·2submesh·material참조/순서·collider·transform과 runtime의 PB 의존을 비교한다. runtime PB component를 제거/비활성화해도 참조·클릭/배치/길찾기가 유지되는 근거를 먼저 확보한다. 아직 도구 구현 예정이며 실제asset적용·native 검증·Android 성능 결과는 없다.

위험은 mesh/submesh 또는material배열 순서 변경으로 외관/팀표시가 달라지거나 collider/transform 차이로 클릭·배치·경로가 달라지는 것이다. 원래 기하와 의미를 보존할 수 없는 경우 적용하지 않고 차이를 보고한다. baked mesh 도입은 PB authoring 기능/참조와 runtime 동작을 구분해 검토하며 manifest/검증근거에 실제 대상만 남긴다.

최종 사용자 통합표에는 맵타일 외관·클릭선택·기존배치조건·유닛경로·재경기 새맵교체를 포함한다. 전체로딩278ms와 Console.Other의 미확정 부분은 이 owner확정으로 자동 해결되지 않으므로 불필요한 async전환/로그변경을 하지 않는다. 전체최적화완료·기기향상률은 아직 아니다.


### 18.1 HexTile bake 실제 적용·동등성 확인

메인이 Unity에서 bake Run `7efabbce571c4eca9a0cc50e260d8838`을 실행했다. 문서 담당은 실제 `.utmp/hex-tile-bake-20261029/7efabbce571c4eca9a0cc50e260d8838/receipt.txt`를 읽어 target/mesh·전후prefab/mesh/meta hash·Vertices60·SubMeshes2·ColliderRays31·END PassedTrue를 확인했다. 메인 native종료 전달값은 PassedTrue·RestoredFalse이며 rollback이 아니라 성공 적용 상태다.

대상은 기존 `HexTile.prefab`과 새 `HexTile_RuntimeMesh.asset`이다. mesh indexCount54+18=72,24triangle을 대조했다. 메인/프로그래머의 정적 exact 대조로 prefab의 PB2블록 제거와 mesh참조2개 교체 외에 기존GUID/meta·남은5component fileID·TRS·material참조/순서·collider 설정은 보존됐다.31ray 비교는 전부hit했다는 뜻이 아니라 **전후hit/miss·표면결과가 동일**하다는 확인이다. 단순ray통과를 실제타일클릭/배치/길찾기 수용으로 확대하지 않는다.

최종source 읽기QA의 추가blocker없음과 메인의 Unity DLL11:35:07·compiler error0 확인을 전달받았다. Development manifest는 prefab/mesh2asset을 더해16entry이며 rawsource뿐 아니라 실제baked자산도 시도별receipt에 포함하도록 했다. 기존GUID0/실행앱연결 한계는 새목록만으로 자동해소되지 않는다.

bake의 실제 적용·동등성 검증은 완료했지만 Android프레임개선량·첫맵/재경기·시각/클릭·배치/경로의 사용자실기는 아직 없다. 최종Unity refresh 뒤 File 메뉴 Build And Run을 시도할 예정이고 성공/설치/기기실행은 확인 전이다. 로딩278ms·Other로그의 추가원인 불확정은 잔여조사로 남기며 전체렉해결이나 전후ms 개선을 주장하지 않는다. 최종사용자절차는 [Plan15절](Plan.md#15-최종-사용자-통합-검증-절차아직-미실행)에 정리한다.


### 18.2 최종 자산 QA·manifest 컴파일 보충

메인 전달 독립asset QA에서 normalizedFullTextExactTrue,receipt와현재prefab/mesh SHA일치,manifest16entry를 확인했다. PB2블록 제거·mesh참조2개 교체 외의 본문 보존과 자산대조 범위이며 새 사용자시각/입력 수용을 뜻하지 않는다. 메인은 manifest source11:43:43 뒤 Editor DLL11:48:25 갱신을 확인했다. 메인이 실제 File 메뉴 Build And Run 항목을 클릭해 최종 빌드 시도를 완료했다. 이후 UI·로그·프로세스·APK의 진행/완료 확인은 하지 않았다. 상태는 **시도 완료·성공/설치/새기기실기 미확인**이며 완료 여부는 사용자가 확인한다.

Plan15의 미실행 사용자표에는 Blocked빈공간선택불가·NoBuild빗금·금광·팀색/선택해제색복원과 랭킹첫열기→정렬/가능한다음이전→새로고침→재열기 중복없음을 보완했다. 이는 기댓값 안내이며 실기PASS가 아니다.


## 19. 2026-10-29 사용자 테스트 완료 보고와 13:27 캡처의 확인 범위

이 절은13:27 한파일의 당시판정이다. 이후 분할8파일에서 새로 확인한 생성/전투/로딩 범위와 남은 문제는20절이 후속 상태이며 당시 미관측을 현재도 전부미관측이라고 읽지 않는다.

사용자가 이번 테스트를 완료했다고 보고했고 순위가 실데이터가 아닌 의미와 검증 불가 항목을 물었다. **테스트 수행 보고는 존중하며, 저장된 구간에서 대상 phase를 확인하지 못한 것을 사용자가 테스트하지 않았다는 뜻으로 바꾸지 않는다.** 이번 추가 제품/asset 교정·파일삭제는 없고 Editor 읽기도구만 통계 범위로 확장했다.

### 19.1 새 기록과 저장 범위의 실제 성능

문서 담당이 `Hexiege_2026-10-29_13-27-14.data` 128,540,176bytes와 최종CSV `Library/ProfilerDiagnostics/frame-summary-eadedd83808d4c72bee00e7cde3ef951.csv` 976,428bytes를 읽었다. 메인계산 data SHA256은 `A646135DACE654578385F125790F7EE4EA51975DBACB6794BAD84DEC3A5FE118`이며 이전 캡처 재저장이 아닌 새 기록으로 확인했다. END completedTrue·valid/scanned2,000·invalidSkipped0·limitedFalse다. CSV는59marker·120열이며 앞선 제한CSV와 공통값2,000행 동일임을 메인/QA가 대조했다.

문서 담당 독립 재계산:평균33.356584ms,p95(nearest-rank)34.055458ms,관측최대55.013500ms,>50ms1프레임·>100ms0프레임. Wait평균18.365895ms,FinishFrameRendering평균5.845785ms다. 목표FPS 대기가 포함된main thread시간으로 실제계산이33ms라는 뜻은 아니다. 이전 큰로딩캡처보다 최대가작아도 동일부하/phase의 독립전후쌍이 아니므로 개선율·전체끊김해결을 산출하지 않는다.

59marker중46개가 전구간0회다. 기존고정8marker·Instantiate/PB·실제씬integration/unload·ServerRpc·Console/Disable 등의 **대상phase가 이저장범위에 들어왔다는 근거가 없다.** 이0회를 타일생성비용0/전투/종료/재경기 성공이나 최적화PASS로 사용하지 않는다. 다른시점의 실제경기/재경기/로비 로그는 존재하지만 공통frame anchor가 없어 이CSV와phase를1:1로 연결하지 못했다.

GC.Alloc은28,156calls이나 해당CSV에byte총량이 없어 총할당B를 산출하지 않는다. GC.Collect20samples가12frames에 있고 inclusive합66.416270ms이며 중첩가능성이 있으므로 독립pause20회의합으로 읽지 않는다. 관측최대 UI1283의Finish43.216ms·GC2samples7.688ms도 중첩가능해 합산하지 않는다. 메인은UI에서Finish43.21ms·ScriptUpdate0.20ms를 확인했다. GPU는 --로 미확정이며 이자료로GPU병목을 확정하지 않는다.

### 19.2 실행 앱·역할과 로그 정책의 확인

문서 담당이 `.utmp/device-analysis-20261029/unity-logcat-1327-test.txt` identity에서 buildGuid `dc812de4a36d46bc894c29ad73f434c8`·IL2CPP·PID4103을 읽었다. 메인/프로그래머의설치APK hash일치·Host대조를 전달받았다. 기기로그의 정확재경기Despawn8개는NoStack,Warning132개는스택유지로 확인됐고,실제경기/재경기/로비 실행로그도 있다. 이확인은 적용경로/기능실행의 증거이며 저장CSV의 대상phase/비용절감ms를 대신하지 않는다. prebuild와실행/프레임의정밀연결·통제baseline은 여전히별도다.

Shadow 진단FAIL3개도 별도로 남긴다. 뒤2회는dropped86/1,880의진단완전성한계가 있고 첫회는숨은legacy조건이미확정이다. 피해오류/피해누락으로 단정하거나 사용자테스트전체FAIL로 합치지 않는다. 누락분의 exact 전수대응 검증은 이로그로 할 수 없다.

### 19.3 순위 “실데이터 아님”의 정확한 의미와 안내 정정

문서 담당이 Lobby.unity의 `_developmentLayoutPreview: 1`과 RankingView의development guard를 직접 읽었다. 현재Development에서는 서버랭킹을조회하지 않고 **임시25개 항목의 레이아웃 미리보기**를 만든다. RefreshAsync는preview를적용한뒤return해운영query/cache를건드리지않고 헤더정렬/새로고침버튼은비활성이다. 실제사용자순위·전적·서버데이터가아니라는 뜻이지,화면의모든검사가무의미하다는뜻은아니다.

임시목록도 RenderCurrentPage→EnsureRowPool의실제경로를타므로 행초기화/첫10행/재사용·페이지표시/다음이전·열폭/높이·중복행 관찰은의미가있다. 기존CLR10case와Unity focusedfixturePASS는그범위에서유효하다. 다만 이번Profiler에는첫열기생성구간이확인되지않아실기첫열기비용은미측정이고,운영조회·실제정렬/갱신·실페이지데이터정확성을수용할수없다.

**이전Plan15의 「정렬→다음/이전→새로고침→재열기」안내는 현재preview설정을반영하지못했다.** 당시안내이력은이문장으로보존하고 현행표를페이지/재열기관찰·정렬/새로고침비활성정상으로정정한다. 사용자에게같은preview검사를다시반복하도록요청하지않는다. 운영조회검증은실데이터/서비스환경이준비돼preview를벗어나는별도범위이며이번분석에서설정을바꾸지않았다.

### 19.4 현재 자료로 검증할 수 없는 목록과 이유

| 항목 | 지금 확정하지 못하는 이유 | 유지되는 확인 |
|---|---|---|
| 전체 전후 개선율 | 동일역할/기기/부하/phase·빌드의독립baseline쌍없음 | 이번2,000프레임통계는유효 |
| HexTile 생성 효과·전투비용 | 관련marker가저장구간에미발생,기능로그와공통frame anchor없음 | bake기하/2submesh/31ray전후동등성·기존nativePASS유효 |
| 종료/재경기/로딩의 절감ms | 실제실행로그는있으나해당phase의CSV포함/연결증거없음 | 로그NoStack정책8개적용·Warn스택유지확인 |
| 랭킹 운영서비스·실페이지정확성 | Development임시25개로실서버query생략·정렬/갱신비활성 | 실제Render/pool초기화·재사용fixture·preview페이지관찰은의미있음 |
| 랭킹 첫열기 실기비용 | 첫생성구간을Profiler에서확인하지못함 | 지연풀구현·CLR/native검증유효 |
| GPU·Release/발열/장시간 | GPU값없음·Release/지속실기자료없음 | 해당Development/저장범위CPU관측만확정 |
| Shadow 일부 전수대응 | dropped와첫회legacy미확정으로세부기록부족 | 진단실패경계유지,피해오류로환산금지 |

이유가 다른 미확인을 한마디로 “테스트 불가/실패”로 합치지 않는다. 이번 파일외 다른새 .data가 없음을 메인이 프로젝트전체검색으로 확인했다. 원본·로그·하네스는보존했고 삭제·제품추가수정·운영설정변경은하지않았다.


<a id="split-eight-captures-20261029"></a>

## 20. 2026-10-29 분할8개 캡처 — 타일/로비 관측과 남은 긴 로그

사용자가 분할 저장한8개를 분석했다. 원본→CSV 연결은 `Library/ProfilerDiagnostics/split-summary-receipt-0b6567a9a54d4449865442ff4cd34b59.txt`의8건이며 모두2,000유효프레임·완주END다. 아래는 각 저장범위의 통계이며 독립 전후쌍이나 전체경기 통계가 아니다. p95는 nearest-rank다.

| 캡처 시각 | 평균ms | p95ms | 관측최대ms | >50ms | >100ms |
|---|---:|---:|---:|---:|---:|
| 14:57:46 | 33.671998 | 34.101192 | 455.416992 | 4 | 2 |
| 14:58:48 | 33.314706 | 34.298962 | 59.713116 | 1 | 0 |
| 15:00:23 | 33.378820 | 35.131691 | 59.360191 | 5 | 0 |
| 15:02:13 | 33.341440 | 34.531807 | 72.636154 | 1 | 0 |
| 15:02:40 | 33.467229 | 34.406769 | 137.570541 | 4 | 3 |
| 15:05:42 | 34.172982 | 35.149731 | 198.857834 | 25 | 9 |
| 15:08:26 | 34.022802 | 34.723885 | 636.781189 | 8 | 4 |
| 15:08:54 | 33.949555 | 34.348656 | 636.781189 | 5 | 3 |

인접 파일의 동일 수치 suffix/prefix는 차례로134/0/0/1,197/0/0/1,165행이다. 16,000프레임을 독립 표본으로 합산하지 않는다. 특히 마지막 두 파일의 raw969↔134는 같은636.781ms 사건이다. 기기로그에서 Host·기존과 같은 설치APK를 확인했고 경기시작14:57:41→종료15:02:07→재경기15:02:30/31→종료15:08:02→로비15:08:22가 관측됐다. 정확한 각 프레임의 phase는 실제 경로/시계 근거가 있는 범위만 연결한다.

### 20.1 실제 생성·로비 진입에서 확인한 교정 범위

상세 추출 장부 `split-details-receipt-c1bc01cf69ad4ad091f6215773e2057a.txt`는3건 모두완주다. 첫파일 raw1789의 `object-samples-1789-f12492f2d684452dae1ee663107b0a6c.txt`에서 Instantiate229회 합8.519343ms, 그중 HexTile211회 합6.878613ms이며 PB재계산 marker는0이다. 이번8파일은 실제 생성·NCC전투·로딩을 관측했으므로13:27 자료의 '해당구간 미포함' 한계를 이 범위에서 보완한다. 이전187타일246.899ms와 이번211타일6.879ms는 관측값 대조이며 동일부하 통제 전후 개선율로 계산하지 않는다.

마지막 파일 raw1029의 `object-samples-1029-5028e6fd7bf1456caf48b6f0ab747804.txt`에서는 Instantiate0·정확한 RankRow0이다(MyRankRow는 별개). 랭킹을 열기 전 해당 로비 진입프레임에서 행 생성 제거를 관측했다. 첫랭킹 열기 비용·운영 서버데이터 정확성까지 수용한 것은 아니다.

같은 로비프레임은361.426ms이고 Loading.UpdatePreloading292.778ms, Integrate167.488ms, asset GC102.342ms, Unload65.741ms가 남았다. 부모·자식/중첩 비용을 더하지 않는다. 타일bake·행지연생성이 전체 로딩을 없앤 것은 아니며 나머지 owner/수명/씬 계약을 확인하지 않은 전체async변환은 보류한다. 매프레임 호출되는 Loading.UpdatePreloading hook의2,000회를 실제씬로드2,000회로 읽지 않는다.

### 20.2 남은 가장 큰 지연 — 사망 Info의 동기 출력 경로

마지막 파일 raw134 상세 `log-samples-134-8c08473226e34db69c2eb749dd252bb6.txt`의636.781ms프레임에서 TickProjectiles603.557ms 아래 InfoDefaultStackPolicy/Other의 LogStringToConsole 한건598.9515ms가 확인됐다. 따라서 '투사체 계산이600ms'라고 해석하지 않는다. 15:05:42 파일 raw1762도198.858ms 중 ProjectileUpdate164.49ms/LogString163.39ms가 관측됐다.

598.9515ms sample 끝시각502537445.614633ms와 `.utmp/device-analysis-20261029/unity-logcat-split-monotonic.txt`의502537.445초가 연결된다. 해당시각의 유일한 primary는 NetworkCombatController의 서버 유닛사망 Info이며 로그 stack의 OnUnitDied→ApplyProjectileImpactDamage→ProcessProjectileFlight 경로도 profiler의 NCC.Update→TickProjectiles→Console.Host/Other와 맞는다. 별도 경기시작/로비 시계 anchor도 일치했다. 이는 **시각·로그 stack·호출경로의 연결**이며 profiler metadata를 복원한 결과는 아니다(선택 sample의 metadata/stack0). 독립QA도3개 상세원문과 단일 사망로그/stack의 일치를 확인했다. 첫맵·로비의 여러 Info 시계범위도 같은 clock domain을 지지하므로 이 로그를 시각·경로 연결 추론으로 식별할 수 있다. profiler 메시지 원문 복원으로 표현하지 않는다.

598ms를 호출스택 생성·Android sink·OS 중 각각 얼마로 나눌 근거는 아직 없다. 기존 선택 NoStack 정책의 적용확인은 유효하지만 다른 Info 동기출력 병목까지 해결된 것은 아니다. 다음 우선 조사대상은 이 사망 Info 경로와 남은 로비 loading이다. 이번 분석에서 제품/asset을 추가 변경하지 않았다.

### 20.3 완료와 남은 검증

| 범위 | 이번에 확인한 것 | 남은 한계 |
|---|---|---|
| HexTile | 실제 타일생성에서 PB0·생성시간 관측 | 통제baseline 개선율·모든 시각/상호작용의 에이전트 실기수용 아님 |
| Ranking | 해당 로비 진입프레임 RankRow 생성0 | 첫열기 비용·운영 서버데이터/정렬·갱신 미확인,preview 반복 요청 없음 |
| 전투/종료·재경기 | NCC/투사체·생성·로딩 실제관측 및 로그상 경기흐름 확인 | 전체 phase를 동일조건 전후쌍으로 비교하지 않음;긴 동기로그·로딩 잔존 |
| 전투진단 | 첫경기 dropped1,138 확인 | 수집상한 때문에 전수대응 불가;두번째 EVIDENCE나 dropped를 피해오류로 단정하지 않음 |
| 전체 성능 | 저장범위별 통계와 병목 원인 분해 | GPU·Release30FPS·장시간 발열·전체렉 해결 미확인 |

사용자가 테스트했다는 보고와 저장자료의 관측한계를 구분한다. 추가 테스트를 지금 반복 요청하지 않으며 원본8개·CSV·기기로그·검증근거를 보존했다. 이번 분석에서 삭제·제품/asset 교정·Git 변경은 없다.


### 20.4 후속 교정 실행 승인과 현재 진행 상태

사용자가 이번 분석의 문서 반영과 남은 교정 진행을 명시 승인했다. 우선 NCC `OnUnitDied`의 정확한 사망 Info에 기존 ConsoleSink 선택 NoStack 정책을 좁게 적용할 수 있는지 검토하고, 확정한 조건만 구현·출력/정책 동등성·Unity컴파일·필요회귀로 검증한다. Warning/Error/Exception 및 다른 메시지,로그 본문·횟수·순서와 게임 권위/피해/수명 계약은 유지한다. 아직 이번 후속 구현·검증 완료를 뜻하지 않는다.

동시에 남은 로비 loading과 맵준비 RPC의 실제 source/자료를 분해한다. 원인이 확정되지 않은 전체async변환·NGO pool·asset 변경으로 확대하지 않는다. 598.9515ms의 내부 비중이 미확정이므로 NoStack만으로599ms 전부를 절감한다고 예측하지 않는다. 새 기기 성능 효과는 실제 교정/빌드 뒤 별도 측정할 범위이며 기존 원자료와 과거 판정을 보존한다.


### 20.5 사망 Info 선택 NoStack 구현 — 검증 출처와 대기 범위

ConsoleSink 실제 source를 확인했다. Info·exception null·system `Network`·class `NetworkCombatController`·message 정확히 `서버: 유닛 사망`이며 data가 `Event=`로 시작하지 않는 경우만 기존 선택 NoStack 경로에 추가됐다. Ops Event는 제외한다. source SHA256은 `1293F9F8F20C1DBAA61E629FB078CE54B8E977DFBEC8B1B2B6EC9158EB9DE82F`다. 이는 해당 Info의 호출스택 생략이라는 의도된 정책변경이며 본문/횟수/순서·Warning/Error/Exception 보존 범위와 구분한다.

프로그래머는 실제source와 시각고정 seam의 GameLog.Compose를 조합해 Development/Release 각각145,152 matrix·정확36개 route변경,UTF-8본문/횟수/exception보존 PASS 및 실제Unity참조 csc Development 오류0/경고0을 보고했다. `.utmp/death-log-correction-20261029/RunMatrix.ps1`와 생성source가 존재하지만 결과txt는 없다. **실행 PASS의 출처는 프로그래머 보고이며 메인/문서 담당 직접실행 결과가 아니다.** QA는 전후 실제source 변환과 조건범위/조합수를 독립 검토해 blocker없음을 보고했다. Unity Editor의 실제 갱신·새기기 효과·최종빌드시도는 아직 대기다.

로딩/맵준비 RPC 분해용 Editor읽기도구는 작업중이며 해당 제품경로의 추가 변경은 없다. 선택NoStack으로599ms 전체를 절감했다고 기록하지 않는다.


### 20.6 잔여 RPC/로비 비용 분해와 교정 경계

메인 native 추출 장부 `split-hotspots-receipt-bd6e9a5895094d14b994df572c913b52.txt`는 첫파일 raw1789/마지막파일 raw1029 모두완주다. 대응 `frame-hotspots-1789-814530bdaf2b4fb1b0655ab431e306e1.csv`와 `frame-hotspots-1029-f7edd9d6184e41de80159cd827ee9c08.csv`는 Main Thread의 inclusive 및 직접자식 제외 self를 분리한다. 부모/자식·inclusive/self를 중복합산하지 않으며 float오차/겹침 때문에 정확한 전체CPU회계로도 쓰지 않는다.

첫 RPC190.301ms 중 self120.307ms는 미계측 managed내부를 더 분해할 수 없다. LogString16회 inclusive21.461/self20.833ms,AddComponent540회 inclusive21.867/self20.422ms가 관측됐다. material clone이 주원인이라는 근거는 없다. 로비에는 GC.MarkDependencies91.244ms,GCCollect40.752ms,Destroy self20.555ms,Integration self18.262ms,CanvasLayout self12.707ms가 남는다. runtime의 명시 GC/UnloadUnusedAssets 호출0이므로 중복 명시GC 제거라는 교정근거도 없다.

현재 원자료로 동등성을 유지하는 추가 교정을 확정할 수 없어 임의 PB/async/pool/material공유 변경은 하지 않았다. **정확 사망 Info 선택 NoStack 제품교정과 이번 한정 원인분해는 완료했으나 나머지 로딩/RPC 병목은 미해결**이다. 메인은 runtime DLL16:46:47/Editor DLL16:46:51 갱신을 관측했으며 최종source hash연결 확인과 실제 빌드시도 결과는 후속으로 구분한다. 이 시점에는 새빌드 시도/설치/기기효과 확인이 없다.


### 20.7 최종 source/Unity 확인과 빌드 시도

메인은 ConsoleSink SHA256 `1293F9F8F20C1DBAA61E629FB078CE54B8E977DFBEC8B1B2B6EC9158EB9DE82F` 일치·diff검사문제0·branch `codex/android-performance-optimization`을 직접 확인했다. runtime DLL16:46:47/Editor DLL16:46:51은 최종source 이후의 실제Unity컴파일 갱신이다. 이어 실제 File 메뉴→Build And Run 클릭 시도를 완료했다. 사용자 지시에 따라 클릭 뒤 UI/로그/프로세스/APK 진행·완료를 전혀 확인하지 않았다. **빌드 시도 완료이며 성공·설치·새실기 효과는 미확인**이다. 사망 Info 교정의 기기효과와 미해결 로딩/RPC를 별도로 유지한다.


## 21. 17:30〜17:34 새4개 — 정책 적용과 출력 지연의 분리

사용자가 새 저장분 분석과 오래된 불필요자료 정리를 승인했다. 최신 death-policy-summary-receipt는17:30:46/17:32:15/17:33:44/17:34:05 원본4개→CSV연결 모두완주이고 각2,000유효프레임이다. 독립QA 통계는 다음과 같다. 서로 다른부하의 저장값이며 통제 전후 개선율이 아니다.

| 캡처 | 평균ms | p95ms | 관측최대ms |
|---|---:|---:|---:|
| 17:30:46 | 33.4557 | 34.0979 | 237.8257 |
| 17:32:15 | 34.9680 | 35.3913 | 408.6802 |
| 17:33:44 | 57.7304 | 178.1747 | 1686.3376 |
| 17:34:05 | 59.2292 | 179.0069 | 2780.6990 |

프로그래머는 새APK hash `352758B9…C8C7A2`·bootGUID `f546…`·IL2CPP/Host 연결 및 사망Info116/116 NoStack·Warning74/74 stack보존을 보고했다. 선택정책의 기기적용은 확인됐지만 동기 로그출력 지연은 해결되지 않았다. 최신파일 raw1453은 NoStack 한건 LogString1580.097ms,raw1533은42건1637.5ms,raw1859는61건2723.939ms가 관측됐다. OS/전송/sink 내부비중은 아직 분리할 수 없다.3개 상세추출 결과를 기다리며 제품추가수정 없이 Editor읽기도구로 원인을 좁힌다.

### 21.1 승인한 오래된 idle 원본 정리

메인은13:27 idle `.data`128,540,176B와 `.highlights`16,199B,합2파일128,556,375B를 삭제했다. resolved D경로포함·크기/SHA 검증 후 LiteralPath로 개별삭제했고 `Library/ProfilerDiagnostics/cleanup-idle-20261029.json`에 삭제전 hash/bytes·Deleted true가 남아 있다. 해당2,000프레임 원본은 더이상 재열 수 없으며 요약CSV `frame-summary-d332a8fbc1c245da9299fd0b8bffd90d.csv`/`frame-summary-eadedd83808d4c72bee00e7cde3ef951.csv`는 보존했다.19절은 삭제전 분석이력으로 유지한다.

새4개·교정전8개·필수baseline 및 다른raw/검증근거는 보존했다.10:38/17:40 자료는 가치미확정으로 유지하며 임의추가삭제는 하지 않았다. 이번실행에서 runtime/asset교정은 없고 읽기용Editor도구만 추가됐다.


### 21.2 상세3개 완주와 남은 지연의 식별

최신 death-policy-details-receipt는 마지막파일 raw1453/1533/1859의3건 모두완주(각35threads·metadata/callstack0)다. 메인이 읽기도구source19:33:01→DLL19:36:15 갱신을 확인했다. 독립QA의 상세합계는 다음과 같다. elapsed sample시간을 같은양의 CPU계산으로 해석하지 않는다.

| raw프레임 | LogString 호출·합계ms | 하위 정책별 분해ms |
|---|---|---|
| 1453 | 1회·1580.097290 | DelayedAttackDamage 아래 InfoNoStack 한건 |
| 1533 | 42회·1637.499707 | Other Default2회708.524164 + PC NoStack40회928.975543 |
| 1859 | 61회·2723.939028 | Other Default21회2717.962603 + PC NoStack40회5.976425 |

프로그래머는 단조기기로그104건을 위1/42/61건의 순서와1대1 대응시켰고 최대시각차1.099ms를 보고했다. 대조표는 `.utmp/device-analysis-20261029/death-details-ordered-wall-join.csv`다. raw1453은17:33:25.437의 사망 Info,raw1533은 건물사망341.446ms·Bootstrap PATH removed-before-repath367.078ms와 PC40건(큰411/310ms 포함),raw1859는 서버 PROJECTILE-END3건1012.795ms(최대703.487ms)·NCC END stopped401.096ms·GameEnd전파368.185ms·전투틱정지333.346ms·overlay276.484ms 등 여러종류로 연결됐다. 이는 **시각·경로·건수·순서 join 추론**이며 metadata에서 정확메시지를 복원한 것은 아니다.

사망116건의 선택NoStack은 실제적용됐지만 NoStack 한건도1.58초이고 다른여러메시지에도 큰지연이 남았다. 정책적용을 출력지연 해결이나 전체개선율로 바꾸지 않으며 NoStack 자체회귀로도 단정하지 않는다. OS logd·PlayerConnection·Editor수신·스케줄 등 내부기여는 현재자료로 분리되지 않았다. 로그추가전체변경/제품·asset수정·새빌드·즉시추가테스트 요청은 이번분석에서 하지 않았다.

삭제QA도13:27 두파일 부재와요약CSV보존을 독립확인했다. 새4개·교정전8개/필수baseline·상세원문과하네스는 유지했다.


### 21.3 요약범위가 앞뒤에 포함된 중간 원본의 추가 정리

메인은17:33:44 `.data`476,490,456B와 `.highlights`16,199B,합476,506,655B를 추가삭제했다. 삭제전 hash/bytes·Deleted true는 `Library/ProfilerDiagnostics/cleanup-covered-20261029.json`에 보존했다. QA는119개 수치열의 중간2,000행이 보존17:32 원본 raw1284〜1999의716행과17:34 원본 raw451〜1734의1,284행에 순서대로 전부포함됨을 mismatch0으로 확인했다. 따라서21절 네파일 통계를 독립8,000프레임으로 합산하지 않으며 중간파일의 최대/긴프레임을 별도사건으로 중복계수하지 않는다.

이는 **요약범위 전체포함에 근거한 저장공간 정리**다. 다른thread·metadata·GPU·raw binary 전체동일은 검증하지 않았으므로 완전 바이트복제 삭제라고 표현하지 않는다. 고유 원시독점증거는 확인되지 않았지만 중간원본에서만 가능한 상세재조사 가능성을 포기한 한계는 남는다. 앞뒤17:32/17:34 원본과 중간 요약CSV `frame-summary-2103d12213654a1a8f4b1dc1938d648a.csv`(2,000행·END),4batch의원본sha장부·상세3개·시각join은 보존했다. 상세3개는 모두 보존17:34 원본만 의존한다.

이번정리 최종합계는 이전 idle2개128,556,375B와 추가2개476,506,655B를 합한 **4파일605,063,030B**다. 앞절의 '새4개 모두 보존/추가삭제 없음'은 추가정리 전 시점의 기록이며 최종원본 보존상태는 이 절을 따른다. 최신17:34·교정전필수기준과 나머지근거는 보존했고 임의추가삭제는 없다.


### 21.4 동기 콘솔 출력 지연의 추가 교정 승인

사용자가 이번결과를 문서에 기록하고 추가교정을 진행하도록 명시 승인했다. 다음대상은 NoStack조건 추가확대가 아니라 주스레드 동기콘솔 출력 지연이다. 프로그래머가 LogRules와 RuntimeLogger 보존경계를 조사해 구체안을 제시하고 메인 검토로 확정한 범위만 구현/검증한다. 현재 새제품코드는 미구현이며 OS/전송/Editor수신 등 내부원인은 여전히 미확정이다.

최종안이 로그정책을 바꾸면 본문·레벨·횟수·순서·예외/Warning/Error·RuntimeLogger 원본보존 중 무엇을 보존하고 어떤 출력경로/시점이 달라지는지 명시한다. 원시기록 손실을 숨긴 채 로그비용 제거로 보고하지 않는다. 확정안과 실제검증을 받기 전에는 교정완료/기기효과를 기록하지 않는다.21.3의 최종삭제4파일605,063,030B와 나머지보존상태는 그대로 유지한다.


### 21.5 승인된 구체안 — Android Development 일반 Info private 보존

메인이 구체안을 승인했다. Development Android nonEditor 일반 Dev.Info를 기존 RuntimeLogger의 실행별고유 private 세션에 원문/순서/필요managed caller정보와 함께 기록하고 성공시 중복Console을 생략한다. Ops Event=·Warning/Error/Exception·Editor/Release의 기존콘솔/객체/스택은 유지한다. owner는하나이며 씬간유지/quittingClose,파일고장시 현재로그부터Consolefallback+faultlatch/재귀안전알림을 적용한다. None정책과 기존NoStack을존중하되 Debug native wrapper stack 바이트동일은 보장하지 않는다.

일반Info가Logcat에없어지는 의도된 정책변경이며 기기private원본도 adb로함께회수한다. queue/worker/throttle/자동삭제는 없다. 파일I/O·kill완전무손실/OS지연원인분리는 미확정이다. LogRules1.8/1.10/1.12/1.13을승인계약으로갱신하고 이전문구는LogHistory H15에보존했다. 현재3구현파일+manifest/필요focused검사 작업예정이며 실제완료/컴파일/실기는 결과대기다. 제품씬/asset은변경하지않는다.


### 21.6 archive 구현·검증 결과와 실기 경계

최종구현은 **모든 GameLog**를 `persistentDataPath/DevelopmentLogs/<UTC-GUID>/RuntimeLog.txt`에 보존하고 성공한 일반Dev.Info만native중복을생략한다. 실제RuntimeLogger의seq/UTF8길이/END컨테이너·footer/closed분리는LogRules1.10에반영했다. archive를집계주근거로하고native와단순합산하지않는다. active파일footer없음은정상이며partialprefix/고장fallback중복을구분한다.

메인이 FinalSourceHashes.json의7개currenthash일치·diff문제0을확인했다. 실제 `ArchiveHarnessResults.txt`는972cases/962records PASS,SnapshotParserResults는healthy/live962·partial961prefix·손상4거부다. ActualHarnessReceipt/HealthyReadbackReceipt가sourcehash와결부된다. Editor/AndroidMono/IL2CPP/Release4compile오류0이며 standalone동명source교체의CS0436외경고0이다. Dev/Release경계각145,152 및DirectBoundary는프로그래머결과영수증으로구분한다.

메인실제Unity메뉴Run `ce0ef55115454a92aaab563be8977437` END PassedTrue/CleanupTrue다. native영수증은 `Library/ProfilerDiagnostics/device-archive-fixture-ce0ef55115454a92aaab563be8977437/verification-receipt.txt`:2records1692B·CallerTrue·NonePolicyTrue·FlushedFooterTrue이며nativeclosedTrue/consoleFallbacks0을메인이확인했다. **EditorSharedCoreCallerFile 범위이고 AndroidFS/IL2CPP는실행하지않았다.** runtimeDLL22:12:12/Editor22:12:24가최종source뒤갱신됐다. 기존APK adb경로접근exit0은회수가능성만확인하며새archive기기I/O성공이아니다. 실제 File Build And Run 메뉴클릭 시도를 완료했다. 사용자 요청에 따라 이후 UI/로그/프로세스/APK 진행·완료를 감시하지 않았다. 빌드 성공·설치·실행·기기성능효과는 미확인이며 로비GC/RPC120ms는 미해결이다.


## 22. 2026-10-30 새2개 — 파일분리 적용과 남은 Warning 콘솔 지연

메인 native 추출은 최신 `device-archive-summary-receipt-*`2건·`device-archive-log-details-receipt-*`5건·`device-archive-hotspots-receipt-*`2건 모두완주다. summary CSV `45961284639341caa8605b3954a9799b`와 `1f12961e3e00485c9b9f244bd50a6530`의STATS/END를 대조했다. 각2,000유효프레임이며 아래 두파일은 통제 전후쌍이 아니다.

| 캡처 | 원본bytes | 평균ms | p95ms | 관측최대ms | >50 / >100ms |
|---|---:|---:|---:|---:|---|
| 01:53:51 | 361425496 | 42.881860 | 35.050770 | 7923.245117 | 27 / 15 |
| 01:56:01 | 259622144 | 33.506173 | 34.722809 | 174.451538 | 5 / 2 |

첫SHA256 `1C3D5B3A16B4F8A2655A0A6BEAA90DCF78619B25CF30DD58F3379FCE564C6C46`,둘째 `9937E261B8E59F51B30B876C2A2B7CE7551FFCFEF0F7FBAA68B6624358D030BA`다. 전체경기/Release성능PASS·전후향상률로 해석하지 않는다.

### 22.1 실제기기 archive 성공범위와 수집한계

`.utmp/device-analysis-20261030/RuntimeLog-device-20261030-020128.receipt.json`은 IL2CPP·Host·buildGUID `33010a9205184e3fbf3596122cdd795a`,완결seq2,137·partialTail false를 확인한다. 회수파일2,693,000B의SHA256 `7D9502FB4BB3340B6844EE704DC488ED61D325270B3F61F0E5864F481CB666F4`는 기기원본/로컬 일치다. 사망 Info155건은파일에보존되고Logcat0으로 일반Info 파일분리가 기기에서 동작했다. archive를 GameLog 주근거로하고native중복을합산하지 않는다.

전체archive footer/별도closedreceipt가 없고현재pid도없어 전체종료 완전성이나세션전체fallback0은확정할수없다. 완결record prefix의유효성과전체종료수용을구분한다. 원시파일은private보관하며업로드/추가삭제는없다.

첫캡처의Write1,199회83.903ms(최대프레임합2.538345ms),CallerStack1,101회743.195ms(최대프레임합24.2555ms),FileOnly1,163회/Fallback·Failure0이 관측됐다. 둘째는Write216회10.706ms,CallerStack161회121.762ms,FileOnly209회/Fallback·Failure0이다. 이0은 저장캡처 범위다. 파일분리성공은 큰멈춤해결과 같지 않으며caller stack비용도 남았다.

### 22.2 가장 긴 지연은 보존된 Warning 콘솔 경로

첫raw1237의7,923.245ms 중 LogString12회7,852.317ms다. 같은프레임Write1.614ms/Caller20.425ms와구분한다.12출력은 `[UAS-ATTACK-SHADOW] presentation-shadow-failure` Warning의RuntimeLogger직접경로이며개별3,898.572ms/2,983.228ms도포함한다. raw1345 2,653.373ms·1894 2,001.963ms·1895 1,354.601ms·1226 1,074.160ms는 TimedDamageEffectChannel Poison Drop StaleStopOrClosed 콘솔Warning과연결됐다.

QA는동일PID/단조시각최대오차1.120ms·순서·16본문의archive exact대응을확인했다. 메인도귀속JSON16행/ExactArchive16·최대절대시각차1.120ms를직접검수했다. 실제owner는 `Infrastructure/Network/UnitAttackShadowObserver.cs`의Warn→RuntimeLogger직접호출과 `Application/Combat/TimedDamageEffectPresentation.cs`의DropWarn이다. 근거는 `.utmp/device-analysis-20261030/native-log-details-monotonic-join.json`이다. profiler metadata/callstack0이므로메시지직접복원아닌시각·순서·본문/경로연계추론이다. 경과시간을같은양의CPU계산으로보거나OS logd/PlayerConnection/Editor수신/스케줄중특정원인으로단정하지않는다. 일반Info교정은유효하지만남겨둔Warning출력의지연이추가확인됐다.

### 22.3 남은 로딩/RPC와 진단오류 해석

둘째raw1438은174.452ms:Loading112.039ms/Integrate89.636ms,CanvasLayout32.477ms(self27.750),GC13.823ms,Caller17.174ms다. raw1518은153.594ms:ServerRpc123.225ms(self57.700),Instantiate213회21.247ms,Caller18.138ms다. 중첩/부모자식을합산하지않는다. 새경기준비 경로는보이나정확로비phase로단정하지않으며기존로딩/RPC미해결을유지한다.

ERROR4건은전투전transport2건·공격진단terminal overflow/drop1,315 한건·재경기networkdespawn MOVE coverage0 terminal한건이다. 이4를실제게임오류4개로동일시하지않으며수집상한/진단조건과게임피해정확성을분리한다.

다음검토대상은 위두Warning의발생원인/출력정책과CallerStack비용이며 모든Warning비활성화로확대하지않는다. 이번에는읽기도구Exporter의3menu만추가했고메인이native9추출을완료했다(sourceSHA `455E061F985253A0D032A63338A5E68966776D3A4BCE171405F3DBF43ECFBB92`,standalone csc오류/경고0). 제품/asset추가교정·새빌드·재테스트요청·자료추가삭제는없다. 전체최적화완료/성능PASS는아니다.


### 22.4 후속 교정·안전 정리의 지속 승인

사용자가 추가교정과 불필요데이터의 즉시정리를 승인했다. 현재가설은 동기native Warning 출력지연,정상 stale/closed Drop의반복Warning,공격Shadow의수명판정이다. 가설을확정원인으로쓰지않고 source조사후 좁은출력정책/진단교정안을확정한다. 게임writer·HP·RPC는유지하며모든Warning비활성화로확대하지않는다. 실제구현은아직확정결과대기다.

QA가불필요중간산출/중복을감사하고메인이명시목록을검증한뒤안전정리한다. 고유raw·최신2캡처·비교필수baseline은필요증거로보존하며정리승인을근거없이전체raw삭제로해석하지않는다. 실제삭제는개별목록/근거/합계/보존대상을후속기록하고기존private custody를유지한다. 이번문서갱신에서추가삭제·제품수정·정책규범변경은없다.


### 22.5 확정한 출력운송 교정과 중간산출 정리

메인은Android Development nonEditor의성공archive후native중복생략을정확두Warning에추가하는안을승인했다. Shadow presentation-shadow-failure 정확message와TimedDamageEffectChannel 전체고정grammar/순서/경계를통과한Poison Drop StaleStopOrClosed WriterDamage만선택한다. Ops Event=/Exception객체·writerfault/reentry/unavailable는제외하고다른Warning/Error/Exception·Editor/Release·nativefallback은보존한다. 레벨/본문/횟수/순서/callerstack/archive를유지하며실패/Stopguard·cap/drop·게임writer/HP/RPC는변경하지않는출력운송교정이다.

RuntimeLogger 공유selector/새FileOnlyDiagnosticWarning marker·ConsoleSink selector·Exporter marker한개추가의3source가구현중이며코드완료/QA/native/기기효과는대기다. LogRules한정예외와LogHistory H16원문보존을반영했다.

메인은프로그래머미참조확인후 `.utmp/cleanup-csc-candidates-20261030.json`의21 DLL을path/bytes/SHA및resolved D/.utmp범위로직접검증해개별삭제했다. 합461,824B·후보잔여0이며 `cleanup-csc-result-20261030.json`이영수증이다. 고유raw/최신2캡처/비교baseline·source/harness/결과/compile로그/receipt/실제UnityLibrary는모두보존했다. 기존원시4파일605,063,030B정리와별도중간산출정리다.


### 22.6 두Warning 출력운송 구현·독립검증 확정

`.utmp/diagnostic-warning-20261030/FinalVerificationReceipt.json`을직접읽어source/hash·harness·compile·boundary연결을확인했다. 최종ConsoleSink `240174BC8C2194CE0BBB5FE5B11E5A70386AE385C7D3B8B4D9885D80D6D3578F`,RuntimeLogger `B871AFB057CE8C7C6084258C636AFC86726CDB738FF00492B9A1D6658D73DF9F`,Exporter `9B1ED63E26C172CF6A9788D18B6D2F04951410318DE2A93072385FB36F3D4D64`다. 새FileOnlyDiagnosticWarning marker로summary65항목이된다.

기존972cases/962records와새Warning2,255cases/2,168records PASS,파일실제쓰기·원문/순서·stack None/Full·Ops/exception·partialFlush/reentry 및현재/다음행fallback을검증했다. QA는저장실기56Warning을실제source로메모리컴파일해UAS16/Poison19선택·다른21native유지를독립재현했다. 이는기존실기본문에대한selector재현이며새기기의정책발생/성능측정이아니다.

Editor/AndroidRelease/비AndroidDev의각145,152sink+144direct경계는출력동등이다. csc4개exit0이며CS0436만25/11/9/25건(AndroidDevIL2CPP/Release/Editor/DevMono;standalone동명source/DLL경고),그외경고0이다.Exporter오류/경고0,selector warm CLR1,000calls0B는기기측정이아니다. 작업자fixture의제작시각과실제기기시각도구분한다.

실제Unity최종갱신·native공유파일fixture는대기다. Editor native는공유기록핵심만검증하며Android전처리selector실기는대신하지못한다. 빌드아직미시도·새기기효과미확인이다. 검증종료후생긴csc중간DLL은메인이개별안전정리할예정이며아직삭제완료로세지않는다.


### 22.7 최종Unity/native 공유핵심 확인과 중간DLL 정리

메인은최종3source hash/bytes일치·diff문제0과실제Unity DLL갱신을확인했다. runtime02:58:00은최종Runtime02:53:57이후,Editor02:52:01은Exporter02:49이후다. 실제메뉴Run `3f5b4371ee994f9fa602a178b4163047` END PassedTrue/CleanupTrue이며Scope는EditorSharedCoreCallerFile/AndroidFilesystemAndIL2CPPNotExercised다. `Library/ProfilerDiagnostics/device-archive-fixture-3f5b4371ee994f9fa602a178b4163047/verification-receipt.txt`는2records1692B·Caller/NonePolicy/FlushedFooter True와SHA256 `8E8160AC94C7F438BC047306BAD51953F36C7AAA9964B46F3D54AA118BC4FAF5`다. Android selector기기실행/성능확인은여전히별도다.

메인은fullresolved D/.utmp경계·SHA확인후검증완료csc DLL5개105,472B를개별삭제했다. `.utmp/cleanup-warning-csc-result-20261030.json`에기록됐으며이전21개461,824B와합쳐 **중간DLL26개567,296B**다. 앞선원시4파일삭제와혼합합산하지않으며source/raw/harness/receipt/compile로그/UnityLibrary는보존했다. 메인이 실제 Unity File→Build And Run 메뉴클릭 시도를 완료했다. 클릭후 UI/로그/프로세스/APK 진행·완료 감시는 하지 않았다. 빌드 성공·설치·실행·기기효과는 미확인이다.


## 23. 방 만들기 실패의 단계별 안전 진단 추가 승인

사용자가 방 만들기 실패 관련 증상확인 로그추가를명시요청했다. 이는기존최적화Task의후속진단이며새네트워크원인fix/재시도/인증초기화변경승인이아니다. Network규칙의역할독립성·TDD의Infrastructure경계·LogRules의민감정보/원시보관계약을유지한다.

기존회수영수증 `.utmp/room-create-diagnostics-20261030/collection-receipt-20261030-1201.json`을읽었다. 동일IL2CPP buildGUID `79a3d210d7eb4ddbaa1cf1b2cbdd970b`에서PID5303의11:47:32 RelayServiceException,PID26235의11:52:47 LobbyServiceException/inner RequestTimeout이보고됐다. 기존Relay stage가고정Allocate여서allocation/GetJoinCode를구분할수없다. 최신PID26925에는그실패가없다. 기존SDK code/reason/HTTP값이없어회선/서비스근본원인은미확정이다. private원시와기기/로컬hash연결·완결prefix는확인됐지만footer/closed영수증이없어전체실행종료완전성은미확인이다. 민감원문은문서에복제하지않는다.

승인구체안은 BattleViewModel→NetworkGameManager.HostGameAsync→RelayManager.CreateRelayAsync/LobbyManager.CreateLobbyAsync로optional diagnosticcontext를명시전달하고기존caller호환을유지하는것이다. 새Infrastructure/Debug/CustomRoomCreationDiagnostics helper와manifest경로를추가하는6파일범위다. 로컬attempt·Stopwatch elapsedms·enum단계/결과·안전bool(UGSstate/authreadable/signedIn/initializerReady/network/transport/listening/host/client),SDK예외분류·숫자ErrorCode/Reason·HttpStatusUnknown만Dev.Info `[CUSTOM-ROOM-CREATE]`로남긴다.

UIRequested→WaitCleanupBegin/Done→RelayAllocate/GetJoinCode/Transport→LobbyCreate→StartHost/Heartbeat/Notify/Returned/catchFail을연결한다. 원문Message/stack/Response·UID/email/token·roomname/code는새진단에복제하지않고기존Ops원인중복도만들지않는다. 추가networkcall/retry/authinit/cleanup변경0이며현재구현/검증결과는대기다. file-onlyInfo이므로기기archive로대조해야한다.


### 23.1 안전진단 구현·정적검증과 실제Unity 갱신

`.utmp/custom-room-diagnostics-20261030/FinalVerificationReceipt.json`을직접읽어6source SHA/bytes·5compile·harness결과를연결했다. 메인은6개currenthash/bytes전부일치·diff문제0을확인했고실제Unity Runtime12:14:03/Editor12:14:04가최종source12:09:13이후갱신됐으며helpermeta12:13:29생성도관측했다. manifest는23paths다. 공용로그규칙은변경하지않았다.

actualmethod추출/SDKstub의Editor22·AndroidDev22·Release15case PASS다. 서비스endpoint/UI/callback/기존Ops·log/null/return/catch순서동등,개인정보sentinel전체부재·attempt분리·UGS초기화전authread0·helperloggerthrow격리·Heartbeat/Notify Begin실패단계를확인했다. fullproject csc runtime4종/EditorTools오류0,기존다른파일경고각24/27/27/31·tools1이며변경runtime5파일경고0이다. 이는source추출/stub·정적컴파일근거이며ILPP/sourcegen/native기기실패재현은아니다. 실제실패원인을고친것이아닌안전단계로그추가다.

메인은완료standalone DLL/PDB10개9,346,800B를path/size/SHA·D/.utmp범위검증후개별삭제했다. `.utmp/cleanup-room-csc-result-20261030.json`이영수증이며이전26 DLL567,296B정리와별도다. 필요고유raw4archive·수집영수증/source/harness/결과/rsp/실제LibraryDLL은보존했다. 메인이 실제 File→Build And Run 메뉴클릭 시도를 완료했다. 클릭후 UI/로그/프로세스/APK 진행·완료 감시는 하지 않았다. 빌드 성공·설치·실행은 미확인이고 기기재현은 아직 미실행이며 근본원인은 미해결이다.


### 23.2 사용자 요청의 실제Editor 방만들기2회

사용자가 기기빌드 대신 메인의Editor 직접테스트를요청해 현재검증범위로우선했다. 메인은실제Play→자동로그인→TapStart→커스텀→방만들기를2회수행했고 두번모두코드표시·1/2상대대기화면을관측했다. 코드값은문서에복제하지않는다. `.utmp/custom-room-diagnostics-20261030/EditorNativeTestReceipt.json`을직접읽고시각/단계값을대조했다.

| attempt | 요청→UiReturned | attempt생성이후elapsedms | 일별원문행 | 진단 |
|---|---|---:|---|---|
| 1 | 13:06:56.860→13:06:59.415 | 2561.622 | 10501〜10533 | 25(단계24+state1) |
| 2 | 13:15:44.755→13:15:46.706 | 1950.159 | 10538〜10570 | 25(단계24+state1) |

각선택범위에서Failed/Missing/False/exception0·Warning/Error0이며전체일별로그무오류를뜻하지않는다. HostEntry의UGSready/Auth읽기가능/로그인·NetworkManager/Transport는true,진입당시listening/host/client는false였다.

| 단계경계ms | 1회 | 2회 |
|---|---:|---:|
| PreparationCleanup | 3.997 | 1.713 |
| RelayAllocate | 1485.341 | 1290.730 |
| RelayJoinCode | 241.019 | 251.865 |
| RelayTransport | 15.772 | 1.423 |
| LobbyCreate | 424.057 | 353.410 |
| NetworkHost | 330.440 | 10.627 |
| Heartbeat | 4.836 | 3.476 |
| HostNotified | 3.794 | 1.763 |

경계시간은호출/로그/await continuation부담을포함하며순수서비스서버latency가아니다. source로그는append되는일별원문이며영수증은그시점스냅샷hash/행범위다. UI관측은메인,원문단계검수는독립QA/메인으로구분한다.

메인은2번째방을13:22:53취소한뒤Disconnect시작→Heartbeat정지→Lobby삭제완료/Host퇴장→Disconnect완료를최신원문에서확인했다. 이후Play정지를클릭해원래Login hierarchy복원을관측했다. 앞영수증작성시미포함이던latercleanup은이메인후속관측으로보완하며전체종료/상대연결경기수용으로확대하지않는다.

현재Editor방생성2회성공범위다. 과거Android Relay예외/Lobbytimeout은재현하지못했고해결판정은불가하며상대접속/경기는미실행이다. 추가빌드/코드변경은없고유일원시로그는보존했다. 불필요새산출은없으며새기기빌드를반복요청하지않는다.


## 24. 2026-11-04 재개 분석 — 10월30일14:10 캡처

사용자 승인으로 이전테스트 분석을재개했다. **자료캡처/회수는2026-10-30,이번분석갱신은2026-11-04**다. 최신 `resumed-capture-receipt-*`의원본 `Hexiege_2026-10-30_14-10-35.data`454,387,560B/SHA256 `46333EDE0B20F73DA58A4CEE8E1111858F442646940BF39174FCB90C842AEC60`→summary/상위3hotspot4건연결은모두완주다. summary `frame-summary-666f1b865b114785bcc533a462e0c0d1.csv`의STATS/END를직접대조했고2,000valid/invalid0·평균33.379609ms·p9534.743038ms·관측최대53.471039ms·>50ms1/>100ms0이다. QA독립검산도일치했다.

캡처범위는정확두진단Warning23회·일반Info1,197회file-only,LogString0/Fallback·Failure0이다. Write1,220회합59.462345ms·최대프레임합1.052577ms(40회),CallerStack1,160회합662.1224ms·최대프레임합20.608383ms(39회)가남았다. 상위raw1061/1417/1327 hotspot의self1위는CallerStack 각19.727/17.487/17.692ms다. 합계는전체범위비용이고중첩/프레임최대와더하지않는다. 이전7.9초와현재53ms는통제동일조건전후쌍이없으므로개선율을산출하지않는다. 해당긴콘솔지연이이번범위에서미관측이고좁은정책이실제로동작했다는판정이다.

### 24.1 실제archive 보존/identity와 남은 증거경계

`.utmp/performance-analysis-20261030-1410/CollectionAnalysisReceipt.json`을직접읽었다. IL2CPP Host·runtimeGUID `70f00c3e704c40748949cf6792912369`·PID21234,archive4,329완결record/partialFalse·기기/로컬hash일치다. 전체회수범위의대상Warning116개(채널90/Shadow26)는원문보존/Logcat0,다른Warning5/Error2/Ops4는각native보존됐다. archive주집계/native대조로동일사건중복집계를피한다.

installed/local APK SHA256 `96B575B237E64AA96A70ED3ED9AA3ABAAB1748B4B39DBABB146AEC77A8ED2C8E`는같지만prebuildGUID0으로manifest를runtimeGUID에직접join할수없다. 정책sourcehash2종일치와실제동작은별도근거다. 현재pid없음·footer/closed영수증없음때문에전체종료무손실/세션전체fallback0은확정하지않는다. Error2는UAS networkdespawn terminalFAIL이며archive실패가아니다. 진단원인은미해결이고실제피해오류2개로단정하지않는다.

캡처시간축CSV를추가확보중이므로정확전투/결과phase와전체완료판정은대기다. 사용자에게신규기기테스트를반복요청하지않는다. 제품추가교정/새빌드는없고읽기Exporter한파일에메뉴만추가했다. 완료standalone DLL약39KB정리는예정이며삭제완료로기록하지않는다. 원시private자료/비교기준은보존한다.


### 24.2 시간축 연결 — 두번째경기 종료전/결과후 구간

최신 `resumed-frame-clocks-receipt-*`는같은원본SHA46333…AEC60에서clock CSV `resumed-frame-clocks-aa2102de95244a9aac44bc657e8f6aab.csv`81,551B/SHA256 `A28449ED7355C64B915947A41A2CD288EDE5A1B81FF25BFF799906150A4F6886`를2,000valid/invalid0·ENDcomplete로완주했다. QA는모든raw/framems가summary와문자열exact일치함을독립확인했다. sample0는wallclock자체가아니며첫start7470366.890426→마지막frame끝7537130.580223ms다.

동일PID 단조anchor11개에서wall offset43,498.453〜456초(3ms범위)를확인했다. ROOT14:09:04.187·popup14:10:51.543 등독립anchor로환산한캡처범위는10월30일14:09:28.819〜822부터14:10:35.583〜586이다. file-only GameEnd.Info14:10:25.181은직접mono행이아니며anchor환산7526725〜728ms경계는raw1688한프레임만교차한다. Projectile tick도1688까지있고1689부터0으로독립일치한다.

| 구간 | raw/N | 평균ms | p95ms | 관측최대ms | >50ms |
|---|---|---:|---:|---:|---:|
| 종료전 | 0〜1687 /1688 | 33.381757 | 34.800537 | 53.471039 | 1 |
| 종료경계 | 1688 /1 | 37.468151 | — | 37.468151 | 0 |
| 종료후 | 1689〜1999 /311 | 33.354806 | 34.083000 | 35.967075 | 0 |

즉두번째경기끝전약56초와종료/결과후약10초를포함한다. 첫경기끝14:07:25와ReturnToLobby14:10:52는범위밖이므로이번자료를로비loading검증으로쓰지않는다. 최종PhaseAnalysisReceipt로구간내정책Warning23건(UAS10/Poison13),경계모호0을확인했다. 발생시각14:09:30.033〜14:10:16.147의archive구간총계가Profiler FileOnlyDiagnosticWarning23과일치한다. 이는개별sample ID의1:1귀속이아니며경고/진단원인이해결됐다는판정도아니다. 현재관측범위에서콘솔긴지연미관측과좁은정책동작을확인했으며기존통제전후비율한계는유지한다.

읽기Exporter한파일2메뉴만추가했고runtime변경은없다. 최종sourceSHA `FBF4C7913587A66117D87E1D4FB042EDF016C534393055C63889164A43C7CF5F`,standalone compile오류/경고0·실제Unity nativeexport완료다. 메인은최종standalone DLL1개43,520B를개별삭제했으며 `.utmp/resumed-profiler-20261104/CleanupReceipt.json`의hash/bytes/Deleted true를확인했다. 앞서예정했던약39KB파일은확대된최종파일로교체됐으므로39KB와별도삭제로중복계수하지않는다. source/결과/원본/CSV/영수증은보존했다. 새기기재테스트요청은없으며CallerStack우선·로딩/RPC미해결은유지한다.


### 24.3 최종 phase 검산과 판정 범위

`.utmp/performance-analysis-20261030-1410/PhaseAnalysisReceipt.json`을직접대조했다. 원본/clock/summary/기기로그hash연결·2,000framems문자열exact·동일PID11anchor/3ms범위·종료전1688/경계1/후311을확인했다. anchor모델기반으로두번째경기말기약56.36초와결과대기약10.37초가확정범위다. 경계한프레임은종료전후로정확분할하지않는다. 로드/첫경기/로비복귀는이번범위밖이다.

대상Warning은전체archive116과캡처내23(UAS10/Poison13)을구분한다.23개모두종료전범위이며후/경계0,경계모호행0이다. 파일구간총계/순서창과marker23의일치는정책작동근거이며metadata ID가없어개별sample1:1연결이나원인문제해결을증명하지않는다. 추가제품수정/빌드시도/재테스트요청없이이번분석을마무리한다. 남은우선순위CallerStack과기존로딩/RPC·진단원인및전체종료한계는유지한다.


## 25. 2026-11-04 새 작업 승인 — 진단부담을 제외한 로비복귀 baseline

사용자가 Task문서 작성과 다음작업 진행을명시승인했다. 개발로그의native출력을줄인것만으로실제출시(shipping) 성능개선이입증되지는않는다는한계를합의했다. 따라서24절의로그정책작동/저장범위통계와shipping성능을구분한다. **Plan21.1의CallerStack 우선순위는아래새계획으로대체한다.** 이전수치/계획은당시이력으로유지하며통제동일조건향상률로바꾸지않는다.

목적은로비복귀에서상세Dev진단 compute와callerstack 부담을제외한명시측정구성을설계하고,최소phase계측만으로종료→결과→복귀버튼→사용가능로비를비교하는것이다. 같은기기/역할/맵/종료동작/빌드설정을고정해3회baseline을준비하고복귀시간·최대프레임·>100ms·GC를기록한다. 이후실제로확인한가장큰병목하나만교정하고동일조건3회후측정한다.

현재는승인과계획기록단계다. 진단제외구성의구체설계/구현·최소계측·baseline/후측정은아직실행하지않았다. 진단writer와게임writer를혼동하지않고HP/공격/권위/RPC·씬/입력계약을유지한다. 측정구성이표준실행/출시빌드와어떻게다른지표시하며원본private로그/기기·빌드식별과검증경계를보존한다. 무근거효과/완료·정상Dev진단을제외한조건의전체출시성능수용을주장하지않는다.


### 25.1 source 조사 — 진단제외와 로비완료 정의의 경계

현재source조사에서기존전역측정mode는확인되지않았다. NetworkCombatController는RootPose observer를GetComponent/AddComponent한뒤Initialize하고UAS.BeginSession을호출한다. UAS.BeginSession은clockhistory/분류/Coordinator결정이벤트를구독하고진단상태를구축한다. 따라서sink출력만끄면이관측/집계compute는남는다. 반면 `UnitAttackResultPresentationShadow.cs`의bridge Coordinator/IsActive·authoritative emitter소유권/표현은Release게임경로에도사용되므로bridge전체를비활성화하는것은진단제외가아니며금지한다.

복귀source는GameEndUI.ReturnToLobby→NetworkGameManager의연결정리/씬변경이벤트→SceneLoader→UIManager로연결된다. UIManager는1초realtime대기후동기LoadScene을호출한다. LobbyRootView.Start의ShowLoading(false)요청과실제ApplyLoadingVisibility(false)는최소표시시간코루틴때문에다를수있다. 따라서요청시각만로비사용가능완료로정의하지않는다.

HasForegroundModal은confirm/resultmodal/loading의일부실제입력점유를조회하지만별도legacy overlay refcount와blocking modal owners전부를포괄하는완료조건은아니다. 측정설정의정확제외범위와사용가능로비완료정의는이경계를반영해조사중이다. 아직제품코드수정/구성구현/측정은없으며shipping과동일한mode를완성했다고주장하지않는다.


### 25.2 승인한 최소phase 계측 구체안 — 구현중

메인이최소phase계측안을승인해프로그래머가구현중이다. 신규 `Infrastructure/Debug/LobbyReturnPerformanceDiagnostics`와GameEndUI.ReturnToLobby Begin·LobbyRootView InitReady·UIManager 실제loading hide/전체blocker입력허용관측,Exporter 고정marker·manifest의6cs(+신규meta)범위다. 게임/기존1초대기/네트워크cleanup은유지한다.

Android Development nonEditor에서시작시1회 private `PerformanceMeasurements/EnableLobbyReturnBaseline` 파일존재로opt-in하며읽기실패는failclosed다. Editor/Release는이설정파일I/O를하지않는다. Begin→InitReady→LoadingHidden→InputReady를기록하고timeout/재진입/다른scene은Abort로구분한다. 정적marker와고정소량private NoStack Info만사용한다.

이는구현중계획이며완료/native/기기성공은아니다. 상세진단compute/callerstack분리는여전히별도설계미완이다. bridge의실제표현은보존하고terminal구독해제는항상실행해야한다. 이번phase계측만으로진단제외baseline이라고부르지않으며아직새빌드/실기요청은없다.


### 25.3 최소phase 구현·정적검증 완료와 실제실행 미완

`.utmp/lobby-return-phase-20261104/FinalVerificationReceipt.json`을직접읽어6cs+meta의hash/bytes·5compile·actual-source fixture를대조했다. phase계측구현은완료했고5standalone csc exit0/error0,변경6파일warning0이다. 기존다른파일warning은Editor24/AndroidDevMono27/AndroidDevIL2CPP27/Release31/Tools1이다. source/reference RSP기반정적검증이며sourcegen/ILPP를실행하지않았다.

actual-source AndroidDev12case는phase/order/early-hidden/blocker/timeout/reentry/owner/domain-reset/fault/privacy/no-stack/fallback을검증했다. 실제UIManager snapshot추출2,048case는AndroidDev/일반AndroidRelease/Editor각구성에서통과했고일반Editor/Release는observer/log미생성으로비활성이다. Unity API/Destroy lifecycle/archive/nativefallback은stub이므로실제Unity/native/Android실기를대신하지않는다.

helper는Frame·RealtimeMs(Unity realtimeSinceStartupAsDouble)·ElapsedMs·최종UI snapshot을남겨phase연결에사용하며profiler/logcat epoch동일을가정하지않는다. `DetailedDiagnostics=Unchanged`를명시해상세진단compute/callerstack제외가아직안됐음을표시한다. 진단분리구체source계획은별도조사중이며추가코드변경은아직없다. 실제Unity/native·Android실기·baseline3회·빌드시도/실기요청은모두미실행이다.

메인은CleanupReceipt.json의정확10 DLL/PDB경로·hash/크기검증후9,376,640B를개별삭제했다. 원본캡처/source/harness/rsp/결과/영수증은보존했다. 이는이번phase검증중간산출정리이며앞선정리와중복계수하지않는다.


### 25.4 Release opt-in 최소측정안 승인 — 2파일 구현중

메인이Release opt-in 최소안을승인했다. 기존helper와manifest2파일만확장하고 **이안이상세Dev관측자별가드안을대체한다.** Dev관측자는Release컴파일에서자연제외하므로12〜16파일에개별skip을추가하지않는다. 실제게임bridge/writer·HP·RPC는보존한다.

Android define `HEXIEGE_LOBBY_RETURN_MEASUREMENT`를명시toggle메뉴로제어하되다른define은보존한다. prebuild는Development/ScriptDebug/ConnectProfiler없는Release옵션을검증하며일반File BuildAndRun을사용한다. 일반Release/Editor새I/O는0,Release측정은define과privateflag가둘다참일때만동작한다.

private `PerformanceMeasurements/<고유UTC-GUID>/frames.csv`와receipt를생성한다.4,096 bounded메모리/60초상한이며overflow는비교부적합이다. 구간중문자열/파일쓰기를하지않고종결후저장해저장비용을분리한다. 매Update realtime간격은wait포함wall간격이며CPU작업시간이아니다. End가Update전에발생한긴stall을lastpartial로누락할위험때문에end-crossing추가1sample을별도표시하고경계partial과구분한다. partial을완전최대프레임이라고보고하지않는다.

GC.CollectionCount(0)차이는수집횟수이며GC시간이아니다. ProfilerRecorder의GC Used/Reserved는availability/category/unit검증후사용하고미지원은Unavailable/null로남긴다. GC.Alloc·GC시간·CPU/GPU세부측정은불가,FrameTimingStats0을유지한다. buildGUID/options/backend/source/metricidentity·샘플수/SHA로귀속한다. 공식근거는Unity counters-reference와ProfilerRecorder API이며최종구현/검증때지원지표를대조한다.

현재SM-N971N의adb shell외부appfiles접근은성공했지만설치앱은Dev라새Release회수/hash는미검증이다. 실패하면회수불가로보고하고공유UI/권한우회로확대하지않는다. 현재2파일구현중·기기실행/효과/빌드는미실행이며사용자Testcase문서는추가하지않는다.


### 25.5 통제조건 정정 — 현재 UI에서 동일맵 반복을 가정하지 않는다

source/규칙을대조했다. GameBootstrapper.Map.CreateRootSeed는싱글에서MapRootSeed.Create를호출하며멀티는NetworkGameManager의로비준비경로에서같은함수를호출한다. MapRootSeed는GUID/UTC기반새seed를만든다. RandomMap현행규범은옛맵테스트모드를제거했고Host가확정한MapDefinition을사용한다. 따라서현재사용자UI로동일맵을3회고정할수있다는근거가없다.

25절의'동일맵3회'는실행가능성이확정된테스트요청이아닌통제조건목표였으며이전제를정정한다. 사용자에게불가능한동일맵반복을요청하지않는다. 무작위3회는같은입력의A/B가아니라동작시나리오분포관측이며확정향상률을산출하지않는다. seed강제/테스트모드복원은이번구현범위가아니다. 구체재현/부하/비교조건을실기절차전에별도검토해불충분한설계로반복재테스트를요청하지않는다. 측정mode는최종검증중이고새빌드/실기요청은없다.


### 25.6 Release 확장 구현·정적검증 완료와 실제 실행 선행조건

`.utmp/release-return-measurement-20261104/FinalVerificationReceipt.json`의2source/hash·6compile·fixture·미실행한계를직접대조했다. helperSHA `E095956563D9A33C2FD71176FCB301F92C5E07C19A96AC71C635943A8CE44D82`,manifestSHA `9F64ECC5BB0D4B1421649517F3714D8DC84F9364981D654F1245F6D43AB5063E`다.2파일Release확장은완료했다.6standalone csc exit0/error0·변경2파일warning0,기존다른파일warning24/27/27/31/31/1이다.

actual-source Release11case·BuildPolicy134case·기존Dev12case와UI snapshot2,048case의관련3구성검증을통과했다. 일반Editor/Release비활성도확인했다. 이는Unity API/ProfilerRecorder지원·lifecycle stub검증이며실제Unity/native·메뉴실행·새빌드·AndroidRelease회수/효과는미확인이다. 현재define/빌드옵션/privateflag를실제로변경하지않았고사용자테스트요청도없다.

시작시ready.json을써서새빌드/Release/수집mode/설정결과를확인하고ready SHA를attempt receipt와연결한다. 준비실패는비활성으로끝낸다. Scene.name은handle변경때만조회하지만전환할당가능성은있어0B보장을하지않는다. recorder지원불가null/Updatecadence와CPU시간의차이/end-crossing·outside·partial별도표시/overflow비교부적합을유지한다. 초기buffer/recorder설정의heap/GC영향과저장비용도측정구간밖이라고완전무영향으로간주하지않는다. 무작위맵/부하가동일하다는전제는없고확정향상률을약속하지않는다.

메인은CleanupReceipt의정확12 DLL/PDB경로·hash를검증해11,123,296B를개별삭제했다. 이번재개작업의이전phase10개9,376,640B와합계 **22개20,499,936B**다. 이전전체역사의정리와중복계수하지않는다. source/fixture/원본캡처/로그/rsp/결과/영수증은보존했다.


### 25.7 실제Unity 준비와 File BuildAndRun 시도

`NativePreparationReceipt.json`과 `BuildAttemptReceipt.json`(.utmp/release-return-measurement-20261104)을직접대조했다. 메인은실제Unity UI와명시된rmsep Editor.log commandline로D프로젝트가실행중임을확인했다. shell프로세스목록에서보이지않아Unity가꺼졌다고추정한것은부정확했으며UI/로그근거로정정한다. 실제native compile graph exit0,Assembly-CSharp/Editor DLL은최종source뒤UTC12:36:18에갱신됐고source/hash는영수증과연결된다.

새측정메뉴의실제표시/클릭/체크ON을확인했다. Android BuildProfiles에서DevelopmentBuild/Autoconnect를해제하고ScriptDebug/DeepProfile OFF를확인했다. FrameTimingStats등품질설정은바꾸지않았다. define ON증거는메뉴의getter/체크UI이며ProjectSettings 디스크persist를별도로확인하지않았다. 현재앱외부files의privateflag0B를adb로생성/존재확인했다.

메인은실제File→Build And Run 메뉴를클릭해시도했다. 클릭뒤snapshot/UI/로그/프로세스/APK상태를읽거나진행/완료를감시하지않았다. 빌드성공·설치·실행·새Release ready검증은미확인이다. 앞25.6의native미실행/설정미변경/빌드미시도는당시상태이며이후준비/시도는이절이대체한다. Release기기측정/효과·3회baseline은아직미실행이다.

사용자가빌드완료를알리면먼저ready.json에서새빌드/Release/수집mode를검증하고짧은1회수집을확인한다. 그뒤무작위맵/실현가능한통제조건에맞춰절차를정하며지금3회테스트를요청하지않는다.


### 25.8 첫 Release 싱글1회 자동저장 확인 — 메인 관측 범위

사용자가싱글경기결과→로비복귀1회를완료했다. `device-ready-20261104-135101.json`과 `device-attempt-1`의frames.csv/receipt.json/save-receipt.csv(.utmp/release-return-measurement-20261104)을직접대조했다. ReleaseOptIn/IL2CPP/isDebugBuild false/Single·networkActive false,buildGUID `701b7087d1ba4f1895c00cf3e19acd85`다. BeginReturn→LobbyReady→LoadingHidden→InputReady·phaseComplete true/endReason None·overflow false·UI ready/hidden/blockers clear가완결됐다.

메인이원격/로컬SHA일치를확인했다. 로컬ready SHA `DE070BED87E64FCEAA0FC127FDDAA43068F01AC15A671A9A9707EF447CEC0830`→attempt receipt→CSV SHA `BB2C9BD5413E5CECCF10E07D46ED5E20EEE9C00052C2449D39520544015C978B` 및save END의receiptSHA `5A47B6284F680446067ADFC4DDDA6A8401B5B947E5758B98616AADA9A1FBC010`가일치한다. 전체3원본은6,645B이며고유첫Release기준으로보존한다. 후속QA의AnalysisReceipt로4hash체인/31행연속성/phase4개/END/통계exact도독립검산완료했다.

복귀duration은1,154.127653ms로기존의도된1초대기를포함한다. 완전Update29간격은평균33.406659/p9540.090346/최대41.066115ms·>100ms0이다. **별도종료crossing은164.472423ms/frameGap2**로구간안하한151.216192ms+밖13.256231ms다. 따라서41ms를전체최대프레임으로보고하거나164ms를단일CPU프레임작업시간으로해석하지않는다. Update cadence는wait포함이며경계/partial을분리한다.

GC.CollectionCount(0)차이1은횟수다. GC Used/Reserved는각31reads에서실기지원이확인됐지만GC시간/GC.Alloc·CPU/GPU owner는미측정이다. 최종저장영수증직전2.268808ms는InputReady구간밖이고영수증자신의최종쓰기/Dispose시간은제외된다. comparisonEligible true는기술완결성만뜻하며동일맵A/B·향상률증거가아니다.

최초목적의싱글1회자동저장검증은PASS다. 로비전체성능개선/GC원인확정·멀티수용은아니다. 새코드/빌드/추가테스트요청·불필요파일삭제는없고원본을보존한다.


### 25.9 첫 Release 독립 검산 완료

`device-attempt-1/AnalysisReceipt.json`을직접대조했다. ready/frames/receipt/save4개hash의원격일치와체인,31행=head1+full29+endcross1·frame/time연속·phase4개·END·통계재계산이모두PASS다. 이는싱글1회파일수집/기술완결성검증의종료이며로비성능향상/CPU원인/멀티수용은아니다.

GC는 **종결근처counter delta1**로표현한다. production Finish는_ended직후_endCollection을읽으므로row의end-crossing관측모호성과receipt의종결근처delta를혼동하지않는다. 정확발생시각/GC소요·164ms의원인은증명하지못했다. outside13ms에서GC가일어났다고추정하는문구는판정에사용하지않는다.

자료는고유첫Release기준으로모두보존하며새불필요중간산출/삭제는없다. 다음은사용가능한기존Dev소유자자료와현행source를활용한끝경계비용진단이다. 추가사용자플레이/빌드/제품교정요청은없고향상률을확정하지않는다.


## 26. 2026-11-05 승인 — 복귀 끝 경계 분리와 기기 상태 맥락

사용자가측정분리·기기상태고려방안에진행을승인했다. 기존Release opt-in mode를유지하고1초의도대기·씬Load호출·로비초기화시간경계를추가해첫측정의구간안하한151.216192ms를더분해하는것이목적이다. 구체코드안은프로그래머가조사중이며아직설계확정/구현/검증·새빌드/측정은없다. 동기Load/1초대기/입력·게임동작은변경하지않는다.

첫싱글원본/ready/수집체인을보존하고164.472423ms를단일CPU프레임또는GC원인으로확정하지않는다. 추가경계가있어도owner별CPU시간을자동추정하지않으며기존Dev자료의소유자관측과Release경과시간을동일측정으로합치지않는다.

새기기맥락은측정전후ADB읽기로thermalstatus·배터리/충전·절전·가용CPU정보를별도수집하는계획이다. 미지원은명시하고과거자료에없던상태를소급추정하지않는다. 배터리온도는SoC온도와같지않으며단일상태값만으로쓰로틀원인을확정하지않는다.

첫receipt의targetFrameRate=-1/vSync0는Unity6.3 Android기본30fps와맞는설정이므로약33ms간격만으로기기성능한계라고판정하지않는다. FPS를자동변경하지않고같은기기/빌드/role·충전조건을우선맞춰비교한다. 무작위맵/부하의동일입력A/B한계는유지한다. 현재는계획기록단계이며별도Testcase/문서범위확대는없다.


### 26.1 승인한3파일 추가경계안과 현재기기조회

메인이helper/UIManager/LobbyRootView3파일범위를승인해구현중이다. SceneDelayBegin/SceneDelayEnd/SceneLoadCallBegin/SceneLoadCallReturned/LobbyStartEntered/LobbyStartCompleted6phase와phasebuffer8→32를추가하고long attempt ticket으로yield/stale/pending을분리한다. 기존1초wait·동기Load순서는불변이다. InputReadyEnd를연장하지않고동일ticket의저장대기구간후속점만받는다. 누락은NotObserved이며phase를배타구간으로가정해합산하지않고UGS fire-and-forget 완료로표현하지않는다. 실제구현/검증완료결과는아직대기다.

메인은현재기기지원/상태를별도조회했다. `device-context-preparation-20261104-160146/Receipt.json`(.utmp/release-return-measurement-20261104)과5query원문을대조했다. UTC11월4일16:01:46〜48/KST11월5일01:01:46〜48의조회시점에thermal exit0/HALReady true·ThermalStatus0·센서이름AP29.9/BAT29.3/SKIN29.6℃,lowPower0·배터리100%/USBpowered true·temp293(29.3℃)였다. CPU현재주파수읽기는지원되며정책별순간값이지상한/성능한계/쓰로틀증거가아니다.

이자료는현재조회지원/상태만확인한다. 배터리온도를SoC온도로보지않고벤더센서누락가능성도남긴다. 과거첫측정이나미래baseline상태로소급/선행확정하지않으며다음측정직전/직후별도조회한다. CPU/기기성능원인해결은입증되지않았고새빌드/테스트는없다.


### 26.2 LoadScene 경계 해석의 공식 근거

[Unity6.3 LoadScene 공식문서](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/scenemanagement/scenemanager/loadscene)를확인했다. LoadScene은즉시완료하지않고다음프레임에완료되는semi-asynchronous동작이다. 따라서SceneLoadCallBegin→Returned만으로전체씬전환CPU시간을계산하지않으며Returned→LobbyStartEntered 사이도별도로본다. 경계의역순/overlap은배타합산하지않는다.

Single mode의자동Resources.UnloadUnusedAssets는engine정리의가능한소유근거지만이번164ms원인확정은아니다. 앞절'동기Load API유지'는LoadSceneAsync로교체하지않는다는뜻이며즉시완료API라는주장이아니다. 추가phase로wall경계를나눠도engine세부CPU self/GC시간은여전히미측정이다. 코드범위/새counter는추가하지않는다.


### 26.3 3파일 구현·정적검증과 독립리뷰 완료

`.utmp/lobby-return-checkpoints-20261105`의FinalVerificationReceipt/ReviewReceipt/BaselineIdentityResults/CleanupReceipt를대조했다. 최종helperSHA `DB2D253D8A40AAF494A49E16938250D4684B30EF6E39E82C975833626F262D3B`,UIManager `290321CEEB434F12A9A16DFBB36BD039C9FCCDF7A1D0900CDDB16876944E964A`,LobbyRoot `A52E6DF7F7B3F133F2171599EFBE0FEF139B68C114516C003C6E41147341E12D`다. 이전baseline3source는기대SHA/bytes와동일재현됐고UI/Root는추가probe제외원문동일이다.

6csc exit0/error0·변경3파일warning0,기존다른파일warning24/27/27/31/31/1이다. Release11+추가checkpoint **최종9case**·DevMarker정상6점Abort0/실제Abort1·기존Dev12/UI snapshot2,048을통과했다. 이전7case중간값을최종검증수치로쓰지않는다. stale다른씬혼입은sceneName Lobby일때만ticket캡처해차단하고원래Game/Login의Wait1/Load1을보존한다. phase32/고정End/저장대기후기록·예외earlyreturn·UGSdispatch계약도검증했다.

QA는최종source/로그/hash/negative coverage를독립검토해미해결blocker0이다. QA가fixture를직접재실행한것은아니며Unity/SDK/recorder stub근거를실제native/기기실행으로격상하지않는다. LobbyStartCompleted는동기Start끝이고UGS InitializeAsync완료가아니다. 예전baseline에새phase가없으면Unavailable이지0이아니다. 기존1초의도대기는UIManager.LoadSceneRoutine이며GameEndUI countdown tick과혼동하지않는다.

메인은완료중간DLL/PDB12개11,144,596B를정확경로/hash검증후개별삭제했다. 이번checkpoint묶음정리이며이전정리와섞지않는다. 첫Release/raw·기기context·sourcebaseline/harness/rsp/log/receipt는보존했다. 현재메인의실제Unity갱신확인은진행중이며native완료/새빌드시도/새실기효과는미확정이다.


### 26.4 실제Unity 갱신/설정 확인과 빌드 시도

`.utmp/lobby-return-checkpoints-20261105/NativePreparationReceipt.json`과BuildAttemptReceipt를직접대조했다. 메인은실행중D프로젝트Unity창을확인/활성화하고AssetRefresh후최종native compile graph exit0(Editor.log26913·36초)을확인했다. 최종3sourcehash3/3일치,Runtime/Editor DLL은최종source뒤UTC11월4일16:16:01/02에갱신됐다.

실제Android BuildProfiles의Release·DevBuild/Autoconnect/ScriptDebug/DeepProfile OFF를기존설정유지로확인했다. D ProjectSettings에는Androiddefine `DOTWEEN;HEXIEGE_LOBBY_RETURN_MEASUREMENT`가디스크에존재한다. 기존기기privateflag0B도읽기로존재확인했으며이번에생성/변경하지않았다.

메인은File→Build And Run을실제클릭해시도완료했다. 클릭뒤snapshot/UI/로그/프로세스/APK나빌드진행·완료감시는없다. 성공·설치·실행·새ready/측정·성능효과는미확정이다. 앞26.3의native진행중/빌드미시도는당시이력이며이후상태는이절로대체한다.

다음은사용자완료보고뒤newready/sourcebuild/Release mode확인과그시점pre기기context수집→같은Single 결과에서로비복귀1회→postcontext/phase회수·구간분석이다. 준비시점thermal조회는이새측정의baseline으로소급하지않는다.3회테스트/동일맵통제A/B향상률을확정하거나추가로요청하지않는다.


## 27. 추가경계 Release 싱글1회 — 호출 반환 뒤 구간 분리

사용자가Single결과에서로비복귀1회를완료했다. `.utmp/lobby-return-checkpoints-20261105/device-session-20261104-175214`의ready·attempt-1 receipt/frames/save/privateAnalysis/CollectionReceipt와pre/post context를대조했다. ReleaseOptIn/IL2CPP/isDebug false/Single·build038b6efe…cab3eb1,31행=head1+full29+cross1(frameGap2)·10phase/6checkpoint존재bool전부true·4hash체인이완결됐다. 고유3원본은7,401B이며ready/context/분석근거와함께보존한다.

| 관측 wall 경계 | 시간ms |
|---|---:|
| Begin→고정End | 1139.247922 |
| SceneDelayBegin→End | 1005.004961 |
| SceneLoadCallBegin→Returned | 0.144307 |
| Returned→LobbyStartEntered | 133.067154 |
| LobbyStartEntered→Ready | 0.801385 |
| LobbyStartEntered→Completed | 0.830808 |

Completed는고정End뒤0.001577ms에저장대기중추가기록됐고측정End는연장되지않았다. 약133ms는Start본문에진입하기전이므로관측된Start본문의UGS dispatch/VM Bind등그자체를이133ms의직접원인에서배제할수있다. 로비UI전체를배제한것은아니다. 씬교체·이전씬정리·에셋GC·새씬Awake/OnEnable·sceneLoaded callback·대기는여전히가능한구성요소이며CPU owner로측정되지않았다. Research26.2의공식LoadScene 다음프레임완료/Single자동UnloadUnusedAssets근거와함께해석한다. 경계/중첩을배타합산하지않는다.

완전29Update간격평균33.503862/p9537.213731/최대42.295231ms다. 별도끝crossing149.274154ms=안하한134.101346+밖15.172808ms이며단일CPU프레임시간이아니다. GC0 near-end delta1만관측했고GC시간·CPU self·정확원인은미측정이다. comparisonEligible는기술완결성만뜻한다.

### 27.1 기기 상태 조회의 시점 한계

pre/post 조회값은thermal0·lowPower0·USBpowered true·배터리30.3→32.1℃다. 그러나CollectionReceipt상파일mtime UTC11월4일18:13:01(KST11월5일03:13:01)과post thermal조회18:20:39.6229584(03:20:39)의차이는458.6229584초,약7분39초다. **즉시post가아니며복귀당시열제한없음을증명하지않는다.** mtime1초정밀도·기기/호스트시계한계와중간transient미관측도남는다. ready UTC와앱realtime을더해정확이벤트UTC를만들지않는다. 배터리온도는SoC온도가아니고순간CPU주파수도성능부족/쓰로틀원인증거가아니다.

이전1.154초와이번1.139초는무작위맵/부하·계측이다른관측으로개선율을판정하지않는다. 코드원인·기기성능부족·성능향상은아직미증명이다. 현재추가코드/빌드/새사용자테스트요청없이기존자료/source읽기진단중이며삭제할불필요새중간물은없다. QA의한계문구갱신은후속확인으로구분하고현재확인된원본수치/체인과시간차를기록한다.


### 27.2 source 읽기 진단 — 배제 범위와 남은 후보

현행LobbyRootView.Start의계측위치와본문을대조했다. VM생성/Bind·UGS InitializeAsync dispatch는StartEntered뒤에있어관측된Returned→StartEntered133ms의직접원인에서배제한다. 하지만Lobby Awake/OnEnable·TMP/layout·로비UI전체를배제할수없다. 다음프레임씬통합/이전씬파괴·Single자동UnusedAssets/GC,새Scene Awake/OnEnable·sceneLoaded callback(Audio/BGM포함)·OS/frame대기가후보이며아직CPU소유원인을측정확정한것은아니다.

프로젝트Scripts에서명시GC.Collect/Resources.UnloadUnusedAssets호출은발견되지않았지만engine자동정리가없다는뜻은아니다. 현재Ranking은lazy이고기본탭은Battle이므로옛10행clone비용을현재원인/시간으로재사용하지않는다. 기존Dev자료는후보범주의보조자료만허용하며비율합산/현재Release시간환산은하지않는다.

현재근거는씬Root.Start앞구간으로좁힌것이며안전한제품교정을확정할수없다. 싱글1회성능개선/기기역량부족도입증하지못했다. 추가코드/빌드/사용자테스트요청은없고고유자료를모두보존한다.


## 28. 시스템 trace 준비 — 사용자 조작 없는5초 수집시험

사용자의진행상태질문뒤메인은앞선설명후백그라운드로작업한상태가아니었음을정정하고시스템trace준비를실행했다. 실제SDK perfetto CLI/help와atrace sched/freq/idle/gfx/view지원은확인됐지만지원목록과실제payload는구분한다. 제품코드/설정/Git/빌드/사용자플레이요청은없다.

`.utmp/lobby-return-checkpoints-20261105/trace-readiness-20261104-185426`의Record/CollectionReceipt/RetryRecord/RetryCollectionReceipt를직접대조했다. 첫 `/data/local/tmp` 출력은EACCES(trace exit1)로실패했고 `/data/misc/perfetto-traces`로재시도해trace/pull exit0·341,751B를회수했다. SHA256은 `CB543F94EAFDBD4DE8D6C3E02EE4304A4113E121DB8AE02830E9CEFB3D5F969D`다. 사용자조작없이당시앱PID11699에서5초수집한시험이며복귀재현/133ms원인분석은아니다.

로컬trace_processor 가용성/파싱은준비중이다. CPU/thread payload coverage·지원/손실은아직미검증으로완전수집/원인분석성공을선언하지않는다. 불필요다운로드/설치전메인검토를거치며system-wide trace는private원본으로보존하고외부업로드하지않는다.


### 28.1 최종준비 검증 — 시스템 자료와 앱 메타까지

trace준비폴더의processor-support/metadata-processor-support/final-readiness/ThreadNamesCollectionReceipt·PowerStateSnapshot을직접대조했다. 첫light341,751B는system sched2,308이나앱PID식별0/interned error1,116,두번째process metadata499,659B는PID11699/84threads·names0/appSched0이었다. 최종record_thread_names true자료466,435B의기기/로컬SHA는 `E018588A2FBB239785B74F439C03D08D3CB9A3DC6AA811A766BA86129024A52D`로같다.

최종payload는84중83thread names·UnityMain11845/UnityGfxDeviceW14104를식별했고system sched2,682/8CPU,CPU freq0〜3각94sample·idle8·6clock종류각2를확인했다. events span4.921943035초,tokenizer error/reportedloss0이다. 보고된loss0이절대완전무손실을보장하지는않는다. **앱sched/thread_state는0**이어서앱실행/대기귀속·GC함수stack·133ms원인은여전히미확정이다.19:06:56UTC별도조회Dozing은이상태와양립하지만각5초전체가잠들었다고단정하지않는다. 메인이폰을깨우거나앱실행조작하지않았다.

실제도구는단일portable `.utmp/Tools/perfetto/v58.2/trace_processor_shell.exe`14,439,936B·v58.2-add693d8b다. 공식tag v59.1 wrapper가v58.2 binary를핀한것이며'v59.1 binary다운로드'제안은실행상태가아니었다. config경로직접-c의localtmp권한실패뒤표준stdin전달(cat config→perfetto --txt -c -)로수집했고보안설정은바꾸지않았다. 앱source/Git/설정/빌드변경·외부raw업로드는없다.

후속짧은준비는Single진입→설정포기확인→결과화면대기경로가가능하다. OnForfeitConfirmed→GameEndUseCase.Forfeit/GameEndUI.ShowResult를사용하므로긴전투종료까지기다릴필요는없다. 다만초기맵부하라기존장시간전투와통제동일성/133ms재현은보장되지않는다. 향후사용자결과화면준비알림후실제trace시작→복귀버튼1회절차를안내할예정이며아직요청하지않았다. QA독립latest검수는대기,중간raw2개는정리후보만있고아직삭제하지않았다. 최종trace/config/SQL/log/receipt·기존실기원본은보존한다.


### 28.2 독립 준비 검수와 대체 시험원본 정리 마감

`independent-readiness-review.json`을직접대조했다. QA는최신namedtrace의선택SQL을독립재실행exit0으로검수해지원수치/최종tracehash·EXE/공식wrapper pin을확인했다. 판정은제한된수집/파싱준비PASS다. 앱sched/state0은5초sleep또는CPU활동부재의증명이아니며Dozing도별도조회시점만뜻한다. Unity phase와Perfetto시계정밀join·GC함수stack·133ms원인은미확정이다.

메인의CleanupReceipt와대조하면독립QA검수후대체된앞2idle raw341,751+499,659=841,410B를로컬삭제했고원격SHA대조중복trace3+config2,총5개1,309,013B를개별삭제했다. 최종namedtrace로컬본/config·SQL/log/영수증/공식도구·기존gameplay원본은보존했다. 앞2raw는실제복귀재현원본이아닌시험중간물이며향후그원문의재파싱은불가하다. 실패로그/초기SQL/통계영수증은유지한다. 앞28.1의미삭제는당시상태이며최종정리는이절을따른다.

source확인상Single GameEndUI는자동복귀카운트다운이없어Single→설정포기→결과대기가가능하다. 긴전투를기다릴필요는없지만기존장시간전투후부하동일성은보장하지않는다. 실제사용자준비후다음trace를수집할계획이며아직추가테스트요청은없다. 실제복귀trace/기능수정/성능효과는미실행·미확인이고앱강제활성화/제품source변경은없다.


## 29. 새복귀 앱기록과 시스템trace coverage 실패

`return-trace-20261104-193203`의CollectionValidation/Coverage/Start/DeviceFileTimes/PostContext영수증과attempt-2원문을대조했다. 새앱측정은Single Release로완결됐다. duration1119.668114ms·delay1001.019884ms·LoadCall0.084846ms·Returned→Start117.355269ms·StartCompleted0.987923ms,GCnear-end delta2다.31행/29full·10phase/6checkpoint true이며수집체인은보존했다. GC시간/CPU owner는미측정이다.

**시스템trace는이복귀의인과coverage에실패했다.** 60초16MB RING에101,449,728B가쓰이고84,672,512B/2,584chunks·6wrap이덮였다. 실제보존events는9.957819646초,UTC11월4일19:32:55.276〜19:33:05.234(KST11월5일04:32:55〜04:33:05)뿐이다. 앱새기록mtime은UTC11월5일01:02:41(KST10:02:41)이며동일복귀구간으로연결되지않는다. post조회UTC01:17:35도즉시post가아니다. mtime은save시각이고정밀phase시계동일성도미검증이다.

idle eventrate로foreground의60초/16MB용량을판단하고수동시각을기대한것은메인의수집설계오류다. 사용자가즉시눌러도초기부분이순환덮임될수있으며사용자탓으로단정하지않는다. dataLoss severity0만으로완전수집을수용하지않고bytes/chunks overwrite를반드시검사한다.117ms원인CPU/GC·개선율·기기역량/당시상태원인은여전히미확정이다. 실패trace와고유실기원본을보존했고삭제는없다.

### 29.1 수집 교정 준비 — 아직 실행/검증 전

프로그래머는private atomic8초32MB DISCARD로초기기록을보존하고input을유지하는구성을준비중이다. 기본DryRun/명시Execute,메인의깨어있는결과화면/PID·좌표/screenshot SHA확인후같은ADB실행안에서수집시작확인과복귀tap1회를연결한다. 짧은bounded종료/회수뒤새attempt·capacity/시계범위를검증하는계획이다. 새config631B는준비됐지만runner작성/검증전이며완료가아니다. 지금폰조작/추가유저테스트요청·제품/빌드변경은없다. 실제실수와방지절차는공용mistakes에기록했다.


### 29.2 atomic 수집교정 private 준비 마감

trace-readiness의AtomicRunnerPreparationReceipt/AtomicRunnerTestResults/FinalAndroidSyntaxAndGuardReceipt를대조했다. config631B/SHA `378FC18071AC9E1940FE5788808C276A1B246391C599F1A1E218408428B2E486`,최종runner11,767B/SHA `4E4823EA5F2DC3763B62ED4B0B7D94FC903F4CA3ADDFC5B5875EC7E9E6D19D62`다.8초32MiB DISCARD로초기기록을보존하며기본DryRun은읽기7개/remote변경0,명시Execute만trace/tap을허용한다.

스크린샷SHA·좌표/size·Awake·KeyguardServiceDelegate직접자식showing/inputRestricted의유일false·기본display0의단일mCurrentFocus exact package·현재PID/session0/source등록을검증한뒤같은shell에서start→session1→tap1→bounded종료한다. 실제기기에서window windows에는focus가없고displays에있어host/shell을검증된displays/policy형식에맞췄다. shell CRLF/CR은LF로정규화한다.30case/Mock회귀·QA순수guard11case검수후메인이최종preview Android sh-n exit0·native AWK policy0/focus1을확인했다. 현재game focus아닌상태의거부는정상이며fullPreflight true·FullExecute/capture/tap은아직미실행이다.

메인실기구문검사용임시기기파일3개는삭제했고실패trace약16MB·새앱측정2와로그/SQL/영수증은보존했다. afterCollectionAccepted는항상false로두고clock범위/discard capacity/hash·유일한새attempt/정밀Unity clock귀속을수동검토하기전원인판정을금지한다. 정수초TAP_EPOCH/uptime만으로117ms정밀join을하지않는다. 기존60초16MB RING운영안은obsolete이며재실행권고하지않는다.

이번실기는앱자동기록1.120초/117ms구간재관측은성공했으나CPUtrace원인분석은실패했다. 개선율/기능수정/성능효과를선언하지않는다. 즉시재테스트요청없이다음준비확인뒤사용자가원하면결과화면만준비하고메인이화면검증·시작/클릭을담당하는계획이다. 아직실행하지않았고QA최종영수증동결은후속이다.


## 30. 명시 승인 뒤 atomic 복귀1회 수집 — 정밀분석 대기

사용자unlock후직접Android테스트명시승인으로메인이깨어있는GameFocus와실제결과화면이미지(1080×2280·패배/Red)를검증했다. screenshot SHA608D4A4…7061과복귀버튼좌표540,1350을직접확인하고DryRun사전검사PASS뒤Execute단일tap·8초수집을실행했다. `atomic-run-20261105-020330-ae54ea1c2c3d4d708519409a2e7f5017`의OperationReceipt/coverage-results/새app-0 attempt-3/PostContextReceipt를대조했다.

trace13,965,686B/SHA `3C612B85790266728D08E4C89F96930BC9BCFB6CA94C810932B62E6AE045CBC0`의원격/로컬일치를runner가확인했다.32MiB DISCARD에written13,991,936B·overwritten/discard/wrap/tracewriterloss0이다. 보존REALTIME은UTC11월5일02:03:33.091〜02:03:41.002(약7.911초),tap uptime514721.55·새attempt mtime02:03:34가해당flow범위에있다. **Unity phase clock정밀join은대기**이며범위포함만으로인과수용하지않는다. Operation Accepted false를유지한다.

새앱기록은Single Release/EndNone/noOverflow·32행/full30·10phase/6checkpoint true다. duration1147.4685ms·Delay1026.231461ms·LoadCall0.112731ms·Returned→Start119.839231ms·StartCompleted0.9745ms·GCnear-enddelta1이다. 앱hash체인은QA독립검수중이고CPU분해도분석중이므로CPU/GC원인이나성공수치를추정하지않는다.

post thermal조회는UTC02:08:09.4068로save02:03:34뒤약4분35초이고이번run prethermal은미수집이다. 조회시점thermal0/배터리32.3℃·USBtrue/lowpower0를즉시post/실행중열제한부재로표현하지않는다. 동일APK·제품source/빌드변경없고새사용자테스트/추가tap요청도없다. 기존60초실패trace는보존하며새수집성공으로과거실패를소급변경하지않는다.


### 30.1 최종 독립검산 — 조건부 실행/대기 분해까지

같은atomic-run의privateConditionalAlignmentReview/privateIndependentValidationReceipt/DeviceContextAnalysisReceipt/FunctionCoverageReceipt를대조했다. 수집체인/자료검증은완료했고시계모델조건부분해도독립재계산됐다. 하지만일정offset·Update/phase시각이Main Running에있다는제약과tap후300ms탐색범위를가정한 **12후보**이며유일정렬/정밀시계교정은아니다.

LoadReturned→LobbyStart119.839231ms의조건부범위는scheduled Running90.642〜92.239920ms,Runnable(R/R+)2.076581〜4.438578ms,S24.758653〜25.522730ms다. 각고정후보에서상태합은정확히구간길이와일치하지만min/max가다른후보에서발생하므로범위끝값을서로합산하지않는다. Running은scheduler점유로CPU self/C#전용실행/GC시간이아니며S도특정대기원인을뜻하지않는다. 이모델안에서는단순scheduling대기만이대부분이라는가설보다Main실행비용성분을지지하지만함수인과는확정하지못한다.

UnityMain의thread_track/slice0으로GC/Unload/LoadScene/Destroy/Awake함수표식은관측되지않았다. 미관측은비용0이아니며Gfx inclusive표식도Main원인으로연결할수없다. thermal trace없음·prethermal미수집·post약4분35초뒤조회때문에실행중throttle도배제할수없다. cpufreq와인접clock snapshot만으로성능역량/정밀offset을확정하지않는다.

최종상태는 **수집검증완료/조건부분해완료/함수원인미확정**이다. 이전30절의QA검수/CPU분석대기는이후결과로보완한다. 제품교정·빌드·추가사용자테스트요청·raw삭제없이이번분석을마감하며모든고유원본을보존한다.


## 31. 사용자 우선순위 변경 — 밀집전투 검토, 화면전환 후속 보류

사용자가화면전환최적화의중요성은유지하되후속보류하고밀집전투병목을우선하도록방향을변경했다. 현재전투자료/source검토중이며새교정대상은미선정이다. 구현효과/기기검증완료로쓰지않고전문가선정결과뒤구체계획을갱신한다.

화면전환은수집검증/조건부12시계후보의Main Running성분분해까지완료했고함수/GC원인·정밀유일시계·실행중thermal상태는미확정이다(30.1). 이한계와첫Release/추가경계/atomictrace/실패trace/private영수증을보존한다. 후속재개는사용자우선순위복귀또는전투작업뒤명시선정과함수소유자/clock보완의필요성·방법을검토한때로하며보류를문제해결로바꾸지않는다.

전투관련기존근거는2절원래NCC/8marker인계,3절추가10source구현/CLR,11절Host분할자료·진단상한,20.2/21.2절투사체하위동기로그분해,22절Warning로그교정동기,24절실제전투말기정책적용/CallerStack잔여와통계다. 구현/정적검증과실제전투개선·전체shipping성능수용을구분한다. 로딩·NGO pool·compaction등미확정항목을임의로새전투교정대상으로선택하지않는다. 새Task/Testcase/source/UI/빌드/Git변경은없다.


### 31.1 선정한 작은 제품 교정 — DetectRange 존재 판정의 조기 종료

Units규칙6의월드좌표판정·규칙9의감지/공격사거리분리와서버권위계약을대조했다. 기존 `UnitCombatUseCase.HasEnemyInDetectRange/At` 두boolean은nearest전체scan의결과가null인지여부만소비한다. 따라서같은유효조건안최초존재확정시후속읽기를생략하는작은교정을선정했다. 기존Dev의ShouldEngage/At반복호출은후보근거지만최대전투병목이라는측정확정은아니다.

범위는UnitCombatUseCase.cs1파일이다. private3인자overload에기본false stopAtFirstMatch를추가하고boolean2개만true로호출한다. 기존 `dist<=limit && dist<minWorldDist` 조건안에서최초유효unit/building을반환한다. nearest공개2개·공격사거리·HexCoord/null/zero fallback·순회순서/동률·HP/RPC/게임권위는보존한다. 순회진단/이벤트/쓰기없고제품provider는dictionary/Transform.position읽기경로라는조사근거를따른다.

true일때후속조회가줄고false는기존전체순회를유지하는안이다. 실제true빈도·Release개선량은미측정이며로그교정이아니다. 현재구체계획기록단계로구현/검증완료를선언하지않고결과수신뒤이어갱신한다.


### 31.2 DetectRange 조기 종료 구현·검증 완료

`.utmp/detect-range-early-exit-20261105/FinalVerificationReceipt.json`·HarnessResults/CompileResults/SourceIdentity·CleanupReceipt를직접대조했다. UnitCombatUseCase.cs1파일SHA `6B2D7AFACF3F45ED71767C098C9898A95457DC51D1ECA63730E6FE523CD64E2F`다. boolean2call만true/optionalfalse·기존조건내unit/building조기반환으로구현했고nearest/좌표fallback/거리/권위를보존했다. 메인diff직접검수와QA최종source/추출/음성대조검수에중대이슈는없다.

실제baseline/new8메서드와UnityCoreModule Vector3/Mathf·실제HexCoord를사용한CLR fixture는6,366cases/59,872assertions PASS·조회감소2,757checks다. 인공256적의provider조회257→2,없는적false의조회동일·nearest ID/type/position/query동일을확인했고의도적오류negativecontrol도검출했다. entity/store/provider/mapper는fixture이므로실기효과/실제true빈도/최대병목증거가아니다.

Roslyn4구성exit0/error0·변경sourcewarning0,기존warning24/27/27/31은이전로그와행단위exact동일이다. diff검사도통과했다. 실제Unity Editor compile/native gameplay/Android성능은NOT RUN이며이번새빌드시도/사용자재테스트요청은없다. 화면전환후속보류는유지한다.

검증완료중간DLL/PDB8개8,283,028B는경계/hash검증후Deleted true/AllDeleted로정리됐다. source snapshots/추출메서드/harness/scripts/rsp/log/결과/영수증과고유raw는보존했다.


### 31.3 비교측정 준비와 빌드 시도 승인

사용자가비교측정준비와File BuildAndRun시도를승인했다. 프로그래머가한빌드안동일조건의read-only paired 비교를설계조사중이며아직구체안확정/계측구현전이다. 목표는실기boolean동등성·true빈도·조회량과조건부시간비교다. 반복계측부담이포함된결과를실제게임FPS향상으로해석하지않는다.

설계수신뒤범위/샘플조건·동등성/부작용검증을구체화하고최종검증과메인의실제빌드클릭결과를각각기록한다. 현재추가구현/새빌드클릭/새측정완료는없다. 기존첫제품교정의정적검증과새실기효과미확인을유지하며고유원본은보존한다.


### 31.4 승인된3파일 비교계측 구체안 — 구현 전

메인이전용Release계측3파일안을승인했다. UnitCombatUseCase에conditional observer/동결provider override·bounded snapshot과실제private baseline false/optimized true paired kernel,신규Infrastructure/Debug/DetectRangePerformanceDiagnostics(+meta)에opt-in owner/ready·start/completed·CSV/END/hash,기존manifest에 `HEXIEGE_DETECT_RANGE_COMPARISON` toggle/buildguard/source등록을추가한다. privateflag `PerformanceMeasurements/EnableDetectRangeComparison`은로비flag/define과독립이다.

Android nonEditor nonDevelopment 측정빌드의Single/Host만활성화하고Client거부·일반Editor/Release compileout/0I/O를유지한다.256query마다표본·최대256쌍/60초상한,같은bounded좌표snapshot의실제private두탐색을AB/BA교대한다. true/false·방문/조회량·kernel시간을기록하고snapshot/captureMs는별도로분리한다. fallbackskip과overflowinvalid를구분하고예외를게임으로전파하지않는다. 경기중I/O0·ready/최종CSV/END/hash체인으로회수한다.

권위optimized반환만게임에사용하고HP/RPC/씬/게임시간수치는바꾸지않는다. paired시간은탐색kernel한정이며실제Transform조회절감시간/FPS이득으로해석하지않는다. 단일새빌드/경기1회안이고검증뒤FileBuildAndRun시도는메인이담당한다. 현재구체계획기록만완료됐고구현/4compile·actualsourcefixture/native/실기는대기다. 범위가예상보다커지면메인검토전확대하지않는다.


### 31.5 비교계측 구현·검증과 native 준비 진행

`.utmp/detect-range-comparison-20261105/FinalVerificationReceipt.json`·FinalSourceHashes/CleanupReceipt를대조했다.3cs+신규meta구현완료이며hash/bytes는최종영수증에결부된다. 실제8메서드regression6,366cases/59,872assert와actual owner/kernel36checks(temp실제쓰기/readback/hash·경기중I/O0·종결/실패/역할/수명)가PASS다.6csc오류0·변경sourcewarning0,기존4구성경고행동일/추가Comparison31·Tools기존1이며QA최종중대이슈없음을확인했다.

최종수집은 **첫live=true 적감지부터60초**이고초기false/생산이동은소진하지않는다. 이후false포함·256stride/256pairs상한도달시sampling만끄고GameEnd후Update에서만저장한다. 강제종료로END없으면불완전이다. ready→종료후start→pairs.csv END→completed→END hash체인과backend/Unity/mode/define·snapshotcap2048·검사수정의를남긴다. 최초2warmup제외후validrow없으면시간효과NotExercised/불충분이다. completed는수집종결이지효과PASS가아니다.

snapshot captureTicks는전체scan/dictionary쓰기비용을포함하고pairedkernel시간은캐시dictionary좌표탐색이다. entity/store는동기read-only현재자료이며AB/BA도cachebias를완전히제거하지않는다. 실제Transform시간절감/FPS이득으로환산하지않는다. 권위반환/게임계약은유지한다.

메인은기기전용flag0B를생성/확인했고기존로비flag는유지했다. 당시old앱이라새ready/실기효과증거는아니다. 실제Unity DLL15:44:51/Editor15:44:54가새source뒤갱신됐고새메뉴표시·비교토글ON체크를확인했다. 토글후최종define domaincompile은진행중이며당시디스크old define과in-memory UI증거를구분한다. 아직최종빌드설정확인/FileBuildAndRun클릭전·새기기효과미확인이다.

완료중간DLL/PDB12개11,159,624B는경계/hash검증후AllDeleted true로정리됐다. 제품source/meta·before/추출/fixture/rsp/결과/compile로그·readback/영수증과고유원본은보존했다.


### 31.6 실제 native 준비와 비교빌드 시도 완료

메인은실행Unity의D프로젝트path를직접확인했고Comparison 메뉴ON체크와최종source후runtimeDLL15:44:51/Editor15:44:54갱신을관측했다. 최근Editor.log180KB의error CS0은확인범위이며전체Console무오류판정은아니다. ProjectSettings디스크는당시기존define이므로in-memory메뉴체크/새domaincompile·DLL갱신과구분한다.

실제BuildProfiles AndroidActive·Samsung선택·Dev/Autoconnect/Deep/ScriptDebug OFF·Export/AAB OFF를스크린샷으로확인한뒤File메뉴BuildAndRun항목을클릭해시도완료했다. 클릭뒤UI/log/process/APK/ADB를추가조회하거나완료감시하지않았다. 빌드성공/설치/실행/새ready는미확인이고사용자완료보고를기다린다.

후속은사용자새앱실행보고→메인ready의새buildGUID/mode확인→싱글AI또는AndroidHost1경기에서firstenemytrue부터60초관측→정상종료/포기GameEnd뒤결과대기·앱유지→메인CSV/END/hash회수다. Profiler Save는필요없고측정은동결snapshot의탐색kernel범위이지FPS효과입증이아니다. 아직새기기결과는없으며새Testcase는생성하지않았다.


### 31.7 새앱 ready 회수 — 실제 수집모드 준비 확인

사용자앱실행보고뒤메인이회수한 `.utmp/detect-range-comparison-20261105/device-c57f0c44/ready.json`을직접대조했다. SHA256 `62C6CB8E480A1B8FB9E15DAA6ACE52EE107A007EE82622BBA82C7355475FC986`,buildGUID `c57f0c44b9fc4f289dceb68f69d8f742`·ReleaseOptInDetectComparison/IL2CPP/isDebugfalse·전용define/stride256/cap256/time60·Stopwatch10MHz가확인됐다. 새빌드의실제수집모드준비근거이며pre-device-context.txt의thermal/battery/lowpower도별도회수했다.

started false/completed false/role빈값은startup ready스냅샷이다. 이파일만으로현재미플레이/첫감지미발생을단정하지않으며실제경기최종CSV/END는대기다. 사용자에게안내할절차는싱글AI1경기·첫교전뒤최소60초플레이→정상종료또는포기GameEnd→결과10초앱유지다. 이번문서작업에서는기기에실행/플레이지시를하지않았고아직결과/효과를판정하지않는다.


### 31.8 첫 실기 비교 결과 — 좁은 검증 마감

`.utmp/detect-range-comparison-20261105/device-c57f0c44`의capture5파일/remote SHA·stat·IndependentAnalysisReceipt와ActualDeviceAnalysisReceipt를대조했다. 메인/QA최종수치와18chain검증이일치했다. Single/IL2CPP Release·동일runtimeGUID에서TimeLimit60.024초로종결됐고54행모두status0/liveboolean동일·overflow0·END/hash체인이정상이다. 사용자는앱을종료하지않고저장했다.

전체13,759query 중true401/false13,358이며stride표본과구분한다. warmup2행제외52표본은true1/false51·candidateAt0이다. true행16은snapshot조회8→6·inspection21→19로작업감소가실기관측됐다. warmup행0의true5→2는성능집계에서제외한다. false51은조회438/inspection1181이각각동일해예상범위를유지한다.

시간은AB에서optimized빠름20/느림6,BA에서빠름4/느림22로실행순서영향이뚜렷하다. warmup후kernel합0.2531→0.2073ms를개선율/기기FPS/체감이득으로해석하지않는다. 동결dictionary커널·snapshotcapture비용/낮은true표본·candidate경로미관측한계를유지한다. 구현정상과조건부조회감소의좁은실기검증은완료했지만큰병목해결/체감성능은입증하지못했다.

pre/post약440초스냅샷은thermal0→0·배터리32.2→33.5℃·USBtrue다. 연속상태/당시쓰로틀부재의증거는아니다. 원시/CSV/chain/기기context를보존하고새삭제·제품수정·빌드는없다. 이번구현을유지해이항목은여기서마감하며자동재테스트요청없이다음전투병목을우선검토한다. 새대상은별도선정전확정하지않는다.


## 32. 다음 밀집전투 조사 — 기존 느린 프레임 재분석 시작

사용자승인흐름에따라기존전투캡처에서다음제품병목을조사한다.10월30일14:10 원본의source/hash/phase연결(24절)을재사용하고stack/GC/Wait·제품CPU비용을분리한다. 새코드계측·빌드·기기테스트/원본삭제는없다.

현재중간판정은이미확인한진단self가큰상위3프레임외에제품큰병목이확정되지않았다는것이다. 기존summary의NCC raw1510/render raw1419는후보이나자식/self분해가없어전체부모시간을제품순수비용으로읽지않는다. 동일원본에서최소2hotspot추출가능성을검토중이며필요한읽기Exporter 최소메뉴안은프로그래머수신/메인승인후계획에반영한다. 완료/비용·교정대상을아직확정하지않는다.


### 32.1 승인한 읽기Exporter 최소안 — 2프레임 hotspot

메인이 `Assets/Editor/Tools/Combat/ExportLoadedProfilerLogSamples.cs`1파일의새메뉴 `Hexiege/Combat/Diagnostics/Export Two Combat Work Frame Hotspots 2026-10-30`를승인했다. 같은 `Hexiege_2026-10-30_14-10-35.data`의raw1510(UI1511/NCC부모약7.048ms)·raw1419(UI1420/Finish부모약19.081ms)만추출한다. 부모수치는선정후보근거이지self제품비용이아니다.

기존StartSplitExport의선택sourceIndices/rawFrames/receiptPrefix를추가재사용하고기존배열/메뉴default의미는보존한다. 원본readonly·stoppedPlay/record/한창guard·1target/update·Load/SHA/bytes/raw/outputcomplete/END장부를재사용하며고유combat-work-hotspots-receipt prefix로연결한다. actualSDK Editor csc1검사뒤메인이실제UI메뉴를실행할예정이다. 현재계획기록단계로아직추출/분석완료가아니다. runtime계측/씬/빌드/새capture·기기테스트는없고ReleaseFPS환산도하지않는다.


### 32.2 2hotspot 실제 추출·검수 완료 — 추가 교정 근거 미확정

읽기Exporter SHA `942BC6A50F074F5798B4AC29DBC85580B75750B548231970D46CAFE0E3FDEF23`의구현/actualSDK Editor csc exit0/error0·기존CS8321 warning1/변경source0·diff검사를확인했다. 메인실제Editor DLL17:12:32갱신뒤native새메뉴를실행했다. `combat-work-hotspots-receipt-da4c8d5a0d904038a3223445f55e4636.txt`의같은원본454,387,560B/SHA46333…AEC60→raw1510/1419 두CSV completed true·ENDprocessed2를직접대조했고메인/QA독립검수도완료했다.

raw1510 frame32.753654ms의NCC TickCombat inclusive7.048154/self5.788589ms·TargetQueries0.769037ms가관측됐다. 미표기타워/wave등게임/진단혼재가남아self를특정순수제품CPU로확정하지않는다. raw1419 frame33.831001ms의Finish19.081268ms 안GfxWait13.994616/Semaphore13.992154ms가있고RenderLoop4.866ms다. 중첩합산하지않으며렌더19ms전부CPU작업이라는가설은배제하지만구체GPU/driver원인은미확정이다. 두프레임ArchiveCallerStack/GCCollect는요약에서0이다.

종료전전체1688프레임평균33.381757/p9534.800537/최대53.471039ms·>50ms1/>100ms0과알려진상위3프레임CallerStack self19.727/17.487/17.692ms를함께해석한다. 현재저장범위에서추가제품교정할큰병목은확정되지않았다. 억지수정/새빌드/재테스트없이분석을마감하며전체성능문제없음/Release30fps보장으로격상하지않는다.

완료standalone중간DLL/PDB2개1,122,048B는경계/SHA검증후AllDeleted true로정리됐다(.utmp/battle-work-export-20261105/CleanupReceipt.json). 원본/sourcebaseline/rsp/log/결과/영수증은보존했고runtime/씬/빌드/새기기capture변경은없다. FinalToolReceipt의native pending은작성시점상태이며후속실제완주는이절이대체한다.


## 33. 멀티1회 고부하 수집 보완 승인 — 설계 조사중

사용자가멀티1회고부하수집보완과구체테스트절차를승인했다. 기존자료는밀집전투부하가충분한지불확실하고DetectRange좁은실기검증만으로일반Release전체성능이입증되지는않았다. AndroidHost+EditorClient 연결·수동고부하시작·사전ready/END검증을포함하는최소실제수집설계를조사중이며아직구체안확정/구현전이다.

일반Release의전체Android로그가자동보존/회수된다고가정하지않는다. DetectRange동결커널과전체프레임은별도증거이며로그만으로GPU/driver원인을확정하지않는다. 멀티한번의테스트로모든역할/회귀를보장하지않고부하·표본·phase·역할/빌드·수집완결성부족은미검증으로남긴다. 전문설계수신뒤같은Plan에사전준비/단계조작/정상관찰·저장·부족자료판정을구체화한다. 현재코드/빌드/기기조작·새Task/Testcase/메모리확대는없다.


### 33.1 승인된7파일 Host 수동90초 측정안 — 구현 전

메인이7파일+신규meta안을확정승인했다. 기존v1 DetectRange비교는보존하고별도 `EnableHostBattleMeasurement` flag의v2 ReleaseHost 수동arm90초를추가한다. 구v1flag는v2를arm하지못한다. 준비중최대1Hz poll·ready nonce/build/canonical mapHash/MapVersion/NCC NetworkObjectId·현재NCC reference/IsSpawned를확인한command1회소비→작은ACK I/O→측정시작이며수집중I/O0이다. 실제Host(IsHost/IsServer/IsClient/listening)와remotepeer를확인해Client를거부한다.

| 파일 | 승인 변경 |
|---|---|
| DetectRangePerformanceDiagnostics.cs | Host owner·90초수동arm/identity/ACK·cadence8192/NCC4096/paired256 선할당buffer·동일frameID·양팀alive1Hz/activeTarget/damage계수·가용recorder/END체인 |
| PerformanceCaptureBuildManifest.cs | 새toggle/source/Release buildguard |
| NetworkCombatController.cs | opt-in Stopwatch outer finally scope로TickCombat wall/frame ID관측;원래return/예외/중첩finally·게임writer보존 |
| IGameServices.cs | 별도read-only map evidence interface;Application 계약만노출 |
| GameBootstrapper.cs | interface구현표시·기존GetLastMapPreparation본문재사용 |
| 새ExportHostBattleClientReadiness.cs(+meta) | 정확EditorClient·동일mapHash/NCCId/capturetoken·실제RuntimeLog path/offset/probe/readback·DLL/source anchor private영수증 |
| RuntimeLogger.cs | Editor전용 기존writer.BaseStream FileStream.Name의read-only activepath getter |

MapHandoff/NetworkMapTransfer를재소비하지않고공통map증거를기존bootstrapper에서읽는다.90초/overflow/role·NCC·scene·match변경은명시abortReason으로구분한다. cadence는wait포함프레임간격,NCC는같은frame의wallscope이며CPU self가아니다. MainThread/Render/GPU/GC/memoryRecorder는handle/description/valid/sampleprobe로ready가용성을검증하고미지원은Unavailable이지0이아니다. pair는보조AB/BA kernel비교로만해석한다. 종료/로비후CSV/END/hash를저장하며abort원본보존·kill/missingEND는불완전이다.

별도private scratch collector는메인명시stage만실행한다. 양쪽ready/anchor·commandwrite/readback검증→ACK확인→회수/END/hash·Editor active log offset-copy/원본성장·hash를연결하고PID전용logcat을최대20분/128MB hidden으로제한한다. ringclear/appkill없고부분절단은Incomplete다. 일반Release Info/stack부재를명시하고Editor RuntimeLog/logcat/측정자료만으로GPU원인/전체회귀를보장하지않는다. 게임/HP/RPC/협상/로그정책은불변이다. 현재구체계획기록단계로제품구현/검증·새게임테스트/빌드는아직없다.


### 33.2 기기 flag 준비와 사용자 시나리오 초안의 제약

메인이실제기기 `PerformanceMeasurements/EnableHostBattleCapture`0B를생성/확인했고기존EnableDetectRangeComparison은보존했다. 새flag우선v2선택은프로그래머확정안이며33.1의계획명EnableHostBattleMeasurement를대체한다. 현재old앱이므로새모드ready/실기활성증거는아니다. 구현초안/검증진행중이며최종검증뒤승인된FileBuildAndRun시도만할예정이다.

Buildings랠리규칙을읽었다. 유효타일에자기팀랠리를지정할수있으나유닛은랠리를경유한뒤적성으로진군하고도착하면랠리를끝낸다. 따라서자기진영랠리로20기정지보관을보장하지않는다. 타일/부대/경로·자원/교전조건에따라수치달성은달라질수있다. each12·최근피해/1Hz교전30bin은잠정가시부하수준이며최대밀집/모든전투성능을보장하지않는다. 실제gate/회수자료가부족하면수용하지않는다.


### 33.3 구현/QA 진행중과 Comparison 컴파일 재검증 대기

검증중NCC property이동의혼합CRLF삭제누락으로옛선언이남은중복을QA가발견했다. Editor전처리에서는제외돼Comparison구성검사가필요한결함이며현재정확1개로수정했다. 최종Comparison csc재실행은대기다. 빌드/기기실행은아직없고완료수치는최종결과수신전확정하지않는다. WORKFLOW에따라공용mistakes에검증중포착/영향과발생개수·전처리구성확인교훈을즉시기록했다.


### 33.4 Host v2 구현·정적검증과 collector 준비 완료

`.utmp/multiplayer-battle-20261105/FinalVerificationReceipt.json`·FinalSourceHashes/CompileResults/CleanupReceipt를대조했다.7cs+meta8hash로최종ownerSHA `A052A840B03C4E7A425D3D5F3D722CEF62F220DBEA1D7F7AD2ADEB1FB3FA8885`다.6csc오류0·변경source새warning0,actualHost98checks·kernel6,366/59,872·collector실제local파일+mockADB6checks를통과했고QA독립읽기중대이슈0이다. 중복property수정후최종Comparison구성도오류0이다. 실제최종native/기기수집효과는이정적근거와구분한다.

private `.utmp/multiplayer-battle-20261105/collect_host_battle.py` SHA `29D241DE06160C6B6C648942B744F4E7DCA9677655603D50019C5D73FA68C9F0`는boot/prepare/probe/start/collect/logcat-start/logcat-stop 명시stage다. 준비probe1회와END뒤end1회만RuntimeLogger직접고정native앵커를쓰고수집창중I/O/로그0을유지한다. 실제exact앵커를stream에서확인해야하며Release일반Dev로그/stack은보장하지않는다. Editor readiness offset은연결뒤시점이므로이전연결로그는전체RuntimeLog snapshot을별도보존한다.

Host END/3CSV/90초부하의 `hostTelemetryEligible`와Client anchor/hash/PID complete stream의 `logsVerified`는독립이다. 한쪽로그실패만으로Host자료를폐기하지않고전체상관분석에는둘다true를요구한다. 로그검증성공도OS가모든메시지를전달했다는보장은아니다.12+12/qualifying30bin은잠정가시부하조건이며최대밀집/모든회귀를보장하지않는다.

메인은기기flag준비/새ClientReady메뉴존재·ComparisonON을확인했고AssetRefresh후최종native갱신확인중이다. 현재최종native완료/빌드클릭/새사용자실기는대기다. 완료중간DLL/PDB13개11,253,716B는경계/SHA검증후AllDeleted true로정리했고고유raw/source/fixture/rsp/log/결과/영수증은보존했다.


### 33.5 실제 native 준비와 Host 측정 빌드 시도

메인은최종8/8sourcehash·branch codex/android-performance-optimization을직접확인했다. 실제AssetsRefresh후runtimeDLL21:36:53갱신·EditorDLL21:02:49/새EditorTools메뉴존재를각source시각과구분해관측했고최근Editor.log180KB errorCS0범위를확인했다. 이는전체Console무오류/기기수집수용은아니다.

실제BuildProfiles AndroidActive/Samsung SM-N971N·Dev/Autoconnect/Deep/ScriptDebug OFF·Export/AAB OFF를확인한뒤File→BuildAndRun메뉴클릭시도를완료했다. 이후UI/log/process/APK/ADB를조회하거나완료감시하지않았다. 성공·설치·새ready/실제Clientrole·멀티수집은미확인이며사용자완료보고뒤확인한다.

새앱확인시ReleaseOptInHostBattle mode/새build·nonce/지원metrics를먼저검증하고static boot-ready만으로진행하지않는다. freshness/requiresRestart false·PreparationTimeout아님을추가확인해20분지난측정세션으로새경기를시작하지않는다. boot기본stage는정적파일읽기이므로메인이현행readiness가드까지확인한뒤방생성을안내한다. 추가코드/빌드변경은없다.


### 33.6 준비 timeout 실패와 수명 교정 — Play 종료 뒤 초안 반영

사용자가처음부터재시작을승인했고메인이Unity PlayOFF/Login을확인해Assets문서반영을재개했다. 그전에는private TaskAddendumDraft에만기록했다. run-a4312e90은startup1200초PreparationTimeout/Dispose로requiresRestart true·prepare실패,logcat20분deadlineReached true/complete false여서NCC/frame CSV를수집하지못했다. 수집수명설계오류이며사용자책임이아니다. stream14891B·recoverydump73808B·Editor recoverycopy1824277B와실수기록을보존했다.

PrivateValidationReceipt의Host126/collector9와원본동일대기negativecontrol실패를근거로startup만료분기제거/Hostmanual대기유지·dynamicboot freshness/requiresRestart검증·종결stream보존재시작가드를준비했다. AppliedSourceReceipt는Play종료승인뒤owner/collector가검증private안과동일바이트로반영됨을기록한다. ownerSHA16A41C7D…299D2C/collectorD6FFA7B7…372A67이며실제Assets SDKComparison오류0/변경경고0이라는프로그래머검증과구분해연결한다. 아직native/새APK/기기효과는미실행이다.

운영은입장직후EditorClientReady anchor를확보하고부대/교전준비후fresh logcat-start→fresh probe→90초start로연결한다.20분/128MB stream상한은유지하며초기boot에도static파일외dynamicfresh/requiresRestart를확인한다. 이전prefix를보존하고재시작시기존probe증명은무효화해새probe를요구한다. 기존1Hz준비IO/optional recorder1slot부담은변경하지않았으며교정부담감소/실기수집성공을주장하지않는다. 최종native/빌드클릭/실기결과는후속기록한다.


### 33.7 timeout 교정 빌드 시도와 최종 정리

실제Assets반영hash/검증은33.6과동일하다. 메인은오늘Unity Login/Android/PlayOFF와소스수정이후Assembly-CSharp.dll갱신을확인했다. 이는Editor컴파일갱신근거이며Android실행성공/실기수용은아니다. 실제File메뉴를열어BuildAndRun항목을1회클릭했다. 사용자지시대로그뒤UI/log/process/APK/ADB의진행·완료를전혀감시하지않았다. 새Android설치/기동/실측은사용자앱실행보고뒤확인할미완범위다.

`.utmp/multiplayer-battle-timeout-fix-20261106/CleanupReceipt.json`은해당경계내완료중간DLL/PDB5개3,617,984B의개별resolved path/SHA/bytes·Deleted true/삭제후미존재를기록한다. Before/source/diff/fixture/log/결과/영수증과고유실패원본은보존했다. 이번추가작업은문서상태반영이며추가코드/기기/빌드변경은없다.


### 33.8 11월8일 멀티 수집 — 조기종료와90초 완주 분리

첫run-c35a4638/collected-20261108-042539는7.701275687초GameEnd abort·231frame/77NCC·286damage/7qualifying bin으로loadfalse였다. 로그검증은true이나성능수용자료는아니다. Save뒤Dispose/owner disable로앱1회측정임을확인했고로비재입장만으로재측정할수없어사용자가앱을재시작했다. 첫원본은보존하고성공자료로소급변경하지않는다.

새run-58b1c1b7/collected-20261108-043529의CollectionReceipt와상위IndependentAnalysisReceipt를직접대조했다. TimeLimit90초·aborted false/capacity false·42sampled qualifying bin·2,700frame/900NCC·546damage이며HostTelemetryEligible/logsVerified/eligible가true다. 실제EditorClient ready와probe/start exactanchor를검증해수집교정의실제작동을확인했다. 현재사용자는로비/앱종료가능안내를받는단계이며원본/로그를보존한다.

| 관측지표 | 독립검산값ms |
|---|---|
| boundary제외cadence2698개 | 평균33.346496·p5033.318500·p9535.891346·p9937.101500·최대39.506500·>50/>100각0 |
| NCC900 wall | 평균1.392339·p952.960400·최대10.356000 |
| latest completed GPU recorder2700개 | 평균27.366940·p9535.530964·최대52.975350 |

cadence는wait포함Update간격이며NCCwall은CPUself가아니다. latest Main평균19.037886/render5.692909ms와GPU도각완료sample값으로동프레임cadence귀속/독립합산·GPU병목확정을하지않는다. mask95전체2,700에서optional지원은확인됐으나GCcount44→54(delta10),GCCollectRecorder0은GC시간0/pause없음의증명이아니다.

42bin은연속42초가아니고캐시양팀>=12인fullframe은1,312/2,698이며후반Blue0〜4로줄어90초내내밀집이아니다. 이번조건의약30fps cadence안정표본과완결된수집을확보했지만전후개선율/전체Release성능·최대밀집/모든회귀를입증하지않는다. 후속QA의상세로그/paired검산은33.9절로연결하며수집로그검증과게임/진단원인해결을구분한다. 현행운영은전투준비근처freshlogcat·앱1회측정/다음앱재시작·교전초기start/원본보존이다. 새TC/제품/Git/빌드·추가테스트요청이나원본삭제는없다.


### 33.9 최종 로그/paired 검수와 남은 원인분리

두run에서회수된Editor-from-anchor/PID Android로그의literal Exception/NullReferenceException/FATAL/[ERROR]검색은0이었다. 검색범위한정이며Release전체Dev로그·전체오류0을뜻하지않는다. 첫조기GameEnd의정확원인과새경기의항복여부도미확정이다.

새pairs213행모두status0/boolean동등,candidate현재101/At112다. warmup제외211중true6/false205이며true6의snapshot조회합168→54, false조회4692→4692로조건부작업감소를확인했다. AB106은빠름98/느림6,BA105는빠름7/느림94의강한순서편향이있다. 전체평균baseline0.0072128/new0.0074033ms를개선율/실제FPS/Transform비용효과로표현하지않는다.

최종판정은90초표본통계/부하조건과로그/chain수집수용이며게임원인·전후성능효과미확정이다. 다음검토제안은latest GPU p95/max와렌더/대기의동일프레임원인분리다. 현재자료만으로코드교정을추가할근거는없고새빌드/테스트요청없이보존한다.


## 34. 11월8일 새 계획 — GPU·렌더링과 대기 원인 분리

사용자가이Task안의다음작업계획작성을명시승인했다. 이번에는느린GPU latest값이실제GPU작업때문인지,렌더제출/표시대기·프레임제한과관계있는지지원가능한자료로구분한다. 현재는읽기조사·설계중이며코드/설정변경·새빌드·새기기테스트를시작하지않았다.

사용자는한쪽병력감소가전투패배때문이었다고설명했다. 이를정상플레이로기록하며버그로추정하지않는다. 그렇다고90초내내양팀최대밀집이유지됐다는뜻은아니다. 기존90초자료의수집·통계는유효하고기존부하/표본한계도유지한다.

| 구분 | 현행 사실/한계 |
|---|---|
| 완료 | 90초Host자료/양쪽로그·체인검증,동결커널boolean/조건부조회감소관측 |
| 미완 | GPU/렌더링·표시/프레임대기의인과분리,실제작은교정과통제후측정 |
| 검증필요 | 실제graphics API/품질·해상도/프레임설정·GPU도구지원·동일프레임시계연결·열/전력맥락 |
| 보류 | 원인미확정제품변경·화면전환후속·강제목표개선율 |

cadence는CPU작업시간이아니고NCCwall은self가아니다. latestGPU/Render/Main값은같은CSV행이라는이유로동일프레임에귀속하지않는다. static ready와현재session/probe/실제도구지원은다르다. 조사결과수신뒤구체수단/시작·종료/출력/실패판정과필요한사용자절차를같은Plan에작성한다. 원인이안나오면억지코드교정을하지않고미확정을보고한다. 원본/private로그와실패이력은보존한다.


### 34.1 읽기 설정 감사와 구체 조사 순서

현행파일을대조하면Android default quality0은Mobile이고Mobile_RPAsset GUID5e6c…1858을참조한다. renderScale0.8/HDR on/MSAA1·depth/opaque off,main shadowmap1024/1cascade/distance50,SRPBatcher on/dynamicbatch off다. Mobile_Renderer는features없음/renderingMode0이다. PC pipeline/renderer설정을Android설정으로혼동하지않는다.

Game씬MainCamera는orthographic/size8·HDR on/dynamicResolution off,URP카메라는postprocessing/shadows on·stack없음/default renderer이며GlobalVolume의SampleSceneProfile을사용한다. 프로젝트FrameTimingStats0·Android graphics APIs의명시raw설정(auto off)은디스크근거다. 실제선택API/quality/refresh/targetFPS는runtime증거가필요하고targetFPS직접변경호출검색0만으로30fps원인을확정하지않는다.

그림자·불투명/투명중첩·VFX·후처리는가능한검토대상이며아직원인이아니다. 구체순서는runtime identity확인→지원probe→frame-linked CPU대기/GPU완료sample구분→비싼pass owner확인→확인된단일변수교정이다. 이번읽기조사에서는설정/코드/실행변경이없다.


### 34.2 공식지원 문서와 실패시 후퇴 조건

[Unity6000.0 GPU Usage](https://docs.unity3d.com/6000.0/Documentation/Manual/ProfilerGPU.html)는Android Vulkan미지원·OpenGL은NVIDIA/Intel기기한정으로안내한다. [FrameTimingManager](https://docs.unity3d.com/6000.0/Documentation/Manual/frame-timing-manager.html)는Android Vulkan/GLES를지원표에싣지만결과4frame지연·정확성미보장·GLES측정자체성능영향을명시한다. **이문서는6000.0이며실제6000.3.5f2/해당기기runtime probe는미실행**이다.

따라서UnityGPUProfiler를필수성공수단으로요구하지않고지원실패는Unavailable로남긴다. 기존CPUwait/렌더pass구조감사로후퇴하며임의API전환/툴설치는하지않는다. EditorFrameDebugger는draw/pass/재질/중복카메라구조확인용이지Android GPUms대체가아니다. 후속승인시runtime/config identity·설정hash계측후보를검토할수있지만지금변경하지않는다.

A/B는동일기기/Host/build·화면/줌/해상도·양팀수/교전량band와전후열/전원/주사율을기록하고한변수씩비교한다. 무작위맵/승패에따른부하변화는동일부하를보장하지않는다.60fps강제/VFX끄기/피해변경을baseline설정으로쓰지않고시각품질변경도별도승인/비교뒤만한다. cap/END/앵커·지원실패/역할/scene변경은중단·부분보존하며준비단계절대deadline을재도입하지않는다.

### 34.3 2026-11-11 최신 main 병합과 재개 상태

사용자 승인에 따라 메인 세션이 `33ce4257`에서 최신 `origin/main` `099e8a1b`로 fast-forward하고 autostash `2a359b15`를 재적용했다. 병합 전 154개 파일 6,044,588,477바이트를 `.utmp/main-merge-20261111/files`에 복사하고 SHA를 검증한 원본은 그대로 보존한다. 근거는 같은 폴더의 `BackupReceipt.json`과 `PostMergePreservationReview.json`이다.

재적용 때 문서 3개와 폰트 1개가 충돌했다. 문서는 최신 main의 지원 스킬·사운드 등 내용과 기존 최적화 이력·실수 기록을 함께 보존해 해결했다. 폰트는 전문 담당이 엄격한 superset 검증 후 해결 중이며 자동 병합 코드 5개의 의미 검토도 진행 중이다. 보존 검토의 99개 바이트 일치·17개 개행만 변경·24개 기존 삭제 상태 유지(DeletedStillAbsent)·14개 upstream overlap 분류는 최종 전체 보존 PASS를 뜻하지 않는다. 메타 4개는 격리 보존했고 6GB 백업은 삭제하지 않았다.

GPU·렌더링 신규 계측은 중단 전 구현되지 않았으며 이제 최소 구현안을 재개할 예정이다. 앞 절의 설정 감사는 병합 전 근거이므로 최신 코드·설정과 다시 대조해야 한다. 실제 기기 GPU 지원, 새 계측·빌드·실기 효과는 아직 확인하지 않았다.

### 34.4 병합 검증 완료와 GPU 계측 검증 진행

2026-11-11 후속 확인: `.utmp/main-merge-20261111/MergeReceipt.json`에서 HEAD와 origin/main이 `099e8a1b`, 브랜치는 `codex/android-performance-optimization`, 미해결 경로는 0임을 확인했다. 폰트는 main 299 glyph를 원문 설정·참조·pixel까지 보존한 local 301 glyph의 strict superset 검증 후 해결했다(`font-audit/SupersetProof.json`, `ResolutionReceipt.json`). 코드 5개는 QA 소스 검토에서 main 49개·local 44개 변경 블록 누락 0이며 Bootstrapper 인터페이스 추가 외 main 본문 유지로 확인됐다. 이는 소스 보존 검토이며 실제 Unity·게임 실행 수용과는 다르다. 백업과 autostash는 보존하고 push는 하지 않았다.

앞 절의 폰트/병합 검토 진행 중 상태는 이 결과로 대체한다. GPU 계측은 이제 3파일을 실제 작성해 검증 중이므로 앞 절의 신규 미구현 표기도 당시 상태다. optional CSV 저장과 collector 실패 격리의 QA blocker 2건을 교정 중이며 최종 완료·기기 지원·성능 효과는 아직 미확정이다. 초기 컴파일 오류 23건은 검사 하네스의 asmdef/DOTween 중복 편성 문제로 하네스를 수정 중이라는 전달 결과이며 제품 또는 병합 결함으로 단정하지 않는다. 최종 구현·검증 결과 수신 뒤 실제 변경 범위와 남은 상태를 갱신한다.

### 34.5 GPU 진단 신뢰성 보완의 구현·정적 검증 완료

`.utmp/gpu-render-diagnostics-20261111/FinalVerificationReceipt.json`을 대조했다. 변경은 DetectRangePerformanceDiagnostics owner·PerformanceCaptureBuildManifest·private collect_host_battle.py 총 3파일이다. 환경 snapshot과 optional delayed FrameTiming의 raw timestamp/별도 CSV·SHA/정확 END/행수 검증을 추가했고 기존 Host core 3CSV/수용 판정은 유지한다. optional CSV 저장·회수 실패가 core END/HostEligibility를 무효화하지 않도록 QA blocker 2건을 해결했으며 동일 최종 3파일 hash 검수에서 신규 중대 이슈는 없었다.

actual-source Host 상태/파일 검사 176건, 로컬 실파일·mock ADB collector 7건, 기존 탐색 kernel 6,366case/59,872assert가 통과했다. 최종 SDK csc 6구성은 모두 exit0/error0이고 경고는 순서대로 24/27/27/31/31/0, 변경 C# 2파일 경고 0이다. SourceMembershipProof는 현재 Project 소스 351/351 포함과 asmdef/firstpass 제외 경계를 확인했다. 초기 중복 DOTween 편성의 잘못된 하네스 실패 로그도 보존하며 이를 제품·병합 오류로 간주하지 않는다. 이 검증은 fixture/standalone 범위로 실제 Unity native·Android 기기 지원 검증을 대신하지 않는다.

**완료는 진단 신뢰성 보완과 정적 검증이다. GPU 원인 분리나 제품 성능·VFX 교정 완료가 아니다.** 설정은 변경하지 않았고 FrameTimingStats는 여전히 0, native/device 실행과 빌드 시도는 0이다. 원본 캡처·실패 자료·6GB 복구 백업을 보존한다.

다음 실제 GPU 수집에는 Frame Timing Stats를 명시적으로 활성화한 별도 진단 빌드 결정이 선행되어야 한다. 기존 APK를 다시 플레이하는 것만으로 지원 probe를 반복하지 않는다. 실제 API/품질/해상도·빌드/현재 세션 identity와 지원 여부를 먼저 확인한 뒤 동일 진단 옵션의 baseline을 확보한다. 임의 API·품질 변경이나 자동 빌드는 하지 않는다. Unity 메뉴/실제 컴파일 확인, 설정 변경 범위 확인, 승인된 File Build And Run 시도 후 사용자 실행 보고와 새 ready 검증 순서로 구체화하며 아직 새 테스트를 요청하지 않는다.

진단 해석 가드: frequency0은 clockUnavailable이고 delayed latest timing을 현재 cadence frame에 귀속하지 않는다. presentWait의 유한값 0은 필드 관측일 뿐 지원되는 대기 0의 증명이 아니다. CPU duration은 self가 아니며 epoch 정밀 join·GPU pass/owner·마지막 지연 tail은 미확정이다. 수집 API의 실제 할당/GPU 부하도 미측정이고 GLES에서는 측정 자체가 부하에 영향을 줄 수 있다. 해당 한계와 지원 실패를 사용자 검증 항목별 진단 자료에 함께 출력한다.

정리: 완료 중간 DLL/PDB 13개 11,902,016바이트만 경계·SHA·크기 확인 후 삭제했다(`.utmp/gpu-render-diagnostics-20261111/CleanupReceipt.json`). 최종 3소스는 동일하며 원본/Before/rsp/실패 로그/검증 영수증/병합 백업은 보존했다.

### 34.6 실행 준비 안내 오류와 진단 옵션 교정

사용자의 앱 실행 보고 후 회수한 새 runtime 환경은 frameTimingFeatureEnabled=false / Unavailable-FeatureDisabled였다. 실제 ProjectSettings와 최근 prebuild 기록도 0/false여서 GPU 지표 수집 준비가 되지 않은 빌드였다. 메인 세션이 빌드 준비 확인 전에 실행 안내를 앞세운 실수이며 사용자 조작 문제로 보지 않는다.

Play OFF 확인 후 진단 옵션 enableFrameTimingStats만 0→1로 변경했다. `.utmp/gpu-render-diagnostics-20261111/enable-frame-timing-20261111-092842/EnableReceipt.json`과 원문 backup/diff에 따르면 단 1바이트이며 역치환하면 이전 바이트와 동일하다. 다른 설정·게임 코드는 변경하지 않았다. 새 빌드/기기 runtime enabled·유효 GPU sample 반환은 아직 미확인이고 실제 GPU 원인·성능 효과도 미검증이다.

다음 순서는 실제 Unity 빌드 옵션 확인→File Build And Run 시도→사용자 실행 보고→새 build/session의 runtime feature enabled와 지원 probe 확인이다. 지원 실패를 0값으로 성공 처리하지 않는다. 계측 부담이 달라지므로 baseline/후측정 모두 동일 진단 옵션을 사용해야 하며 일반 제품 설정과의 향상률 비교는 하지 않는다. 진단 종료 뒤 원래 FrameTimingStats 0으로 복귀하고 설정/빌드 귀속을 확인해야 한다. 메인 실제 확인에서 열린 Unity의 Frame Timing Stats 체크박스는 처음 OFF였으므로 해당 항목만 ON으로 클릭하고 체크 표시를 확인했다. 최종 디스크 값 1과 hash는 앞선 옵션 교정 결과와 동일했다. Login/Android/Play OFF 상태에서 Project Settings를 닫고 실제 File > Build And Run을 1회 클릭했다. 클릭 이후 UI·로그·프로세스·APK·ADB 완료 감시는 하지 않았다. 빌드 성공·설치·새 실행·실기 GPU 지원은 미확인이다.

### 34.7 시작 전 GameEnd 수명 안내 오류와 후속 보완 진행

사용자 생산·교전 뒤 시작 gate는 Blue36/Red10·최근 피해 약65ms에서 거절됐고 측정은 0이었다. 메인은 started=false만 보고 앱 재시작 없이 재경기를 안내했지만 실제 재입장 후 completed=NotStarted-GameEnd/requiresRestart=true였다. owner는 시작 전 GameEnd에서도 Dispose한다. 이는 사용자 책임이 아닌 계측기 수명 안내 오류다. run-dac53f1d 회수는 completedChain=false·빈 token/session mismatch·Host/log 수용 false, frames/NCC/timing0행이며 새 GPU 데이터가 아니다. logcat20분 deadline/explicitStop=false 뒤 stop 요청과 Editor prefix3,028,221바이트를 포함한 원본은 보존한다.

사용자 앱/Play 종료와 보완·빌드 시도 승인 후 메인이 Login/Play OFF를 확인했다. 별도 render-diagnostic purpose의 자연 교전 시작에서 양팀12 강제 조건만 제거하는 보완을 진행한다. Host/peer/map/nonce/token/probe 및 최근 실제 damage guard는 유지한다. 기존 양팀12+30bin의 strict load 판정은 그대로 두고 자연 교전 진단과 밀집 성능 통계를 구분한다. 모든 prepare/probe/start는 END/completed/freshness/실제 requiresRestart를 확인하고 앱당1회 수명을 유지한다. 재경기 측정에는 반드시 앱을 재시작한다.

logcat은 입장 즉시가 아니라 부대 준비 후 fresh start→probe→capture로 연결해 준비 중20분 소진을 피한다. 준비 중 FrameTiming valid sample 지원 상태를 구분하는 추가 단계는 검토 중이며 확정 구현으로 쓰지 않는다. 품질/API/HP/RPC는 바꾸지 않고 FrameTimingStats1을 유지한다. 구현·정적 검증은 아래 최종 결과로 갱신한다. 실제 native·기기 지원/효과와 새 빌드 시도는 아직 대기다.

| 테스트 항목 | 확인할 진단 자료 | 판정 |
|---|---|---|
| 앱 세션 재사용 가능 여부 | 동적 readiness·END/completed·requiresRestart·owner 수명 | started=false만으로 수용하지 않음; 경기 종료 후 새 측정은 앱 재시작 |
| 자연 교전 시작 | render-diagnostic purpose·identity/probe ACK·최근 damage | 양팀12 미달을 곧바로 거부하지 않되 strict 밀집 수용과 분리 |
| 엄격한 부하 충족 | 양팀 alive와 qualifying bin 통계 | 기존12+30bin 유지, 미달이면 최대 밀집 효과 PASS 금지 |
| GPU 지원·자료 완결 | runtime feature/유효 timing 지원 단계(검토 중), CSV/END/hash | 새 지원 확인·구현 결과 전 실제 GPU 수집 성공으로 기록하지 않음 |

추가 재경기를 먼저 요구하지 않고 위 기준 보완과 검증·실제 준비 확인 후 필요한 최소 절차를 안내한다.

구체안: purpose 누락은 기존 StrictBattle로 취급해 하위 호환을 유지하고 RenderDiagnostic만 별도 시작 조건을 적용한다. purpose allowlist와 probe/start 동일 purpose·ACK/completed/collector 연결을 정확히 검증한다. accepted start ACK도 종결 owner 재사용 거부에 포함한다. 기술적90초 완결·strict 밀집 충족·GPU 진단 수용을 각각 판정한다. 준비1Hz의 유효 timestamp/양수 GPU 관측 status는 지원 관측일 뿐 전투 표본 저장과는 분리한다. owner/private collector와 필요한 최소 manifest 문구를 대상으로 6구성 csc·새 fixture를 검증할 예정이며 아직 결과 수신 전이다.

**자연 교전 모드 최종 구현·검증(2026-11-11):** `.utmp/render-purpose-20261111/FinalVerificationReceipt.json`/FinalSourceHashes와 QA 최종 대조를 확인했다. 실제 변경은 owner와 private collector 2파일이며 manifest는 바꾸지 않았다. 351개 현재 Project 소스 누락 없이 SDK csc6구성 오류0·변경 경고0, Host192/CollectorPurpose18/Collector9/kernel6,366case·59,872assert가 통과했다. 엔진/NGO/recorder fixture와 mock ADB 범위이며 실제 native·기기 실행은 아니다. 최종2hash QA 일치·미해결 중대 이슈0, 중간 DLL/PDB13개11,906,244바이트만 CleanupReceipt로 삭제하고 원본·소스·검증 자료는 보존했다.

최종 조건은 누락/null/empty purpose를 StrictBattle로 처리하고 기존12each+최근damage 및30 qualifying bin을 유지한다. 명시적 RenderDiagnostic은 각 팀 살아 있는 유닛1개 이상+최근 실제 damage와 기존 identity/probe guard를 요구한다. `--purpose RenderDiagnostic`을 prepare/probe/start에 일관되게 사용하고 회수에서도 정확 목적 binding을 확인한다. 기술적90초 완결/엄격 밀집 수용/GPU raw 지원·자료 완결/양쪽 로그 검증은 독립 축이다. Render 수용을 밀집 PASS나 GPU 병목·효과 입증으로 올리지 않는다.

모든 준비 경로는 END/completed(빈 token 포함)/accepted start ACK/freshness/requiresRestart를 검증한다. 앱당1회 owner 수명을 유지하며 재경기 수집은 반드시 앱을 재시작한다. 부대 준비 후 fresh bounded PID stream→새 목적 probe→start 순서다. preparationGpuObserved는 유효 raw timestamp와 양수 GPU 관측 여부일 뿐 수집 창 GPU 행을 추가하거나 지원 완결을 증명하지 않는다. delayed latest의 현재 cadence 귀속·CPU self 해석은 계속 금지한다. FrameTimingStats1을 유지했고 품질/API/HP/RPC는 바꾸지 않았다.

메인이 Unity Login/Android/Play OFF에서 실제 Compiling Scripts→Reloading Domain→준비 복귀를 관측했다. Assembly-CSharp.dll21:39:50은 owner19:17 수정 뒤 갱신됐으며 이는 Editor 반영 증거이지 Android 실행 성공이 아니다. 최종2소스 hash는 동일하고 FrameTimingStats 디스크1을 유지했다. 실제 File > Build And Run을1회 클릭했으며 이후 UI·로그·프로세스·APK·ADB 완료 감시는 전혀 하지 않았다. 빌드 성공·설치·새 기기 실행·GPU 지원·성능 효과는 미확인이다. 새 Testcase는 만들지 않는다.

다음 절차는 새 앱 실행 후 로비 보고→준비 GPU 관측 확인→멀티 입장→Editor anchor 확인→생산·자연 교전 준비 보고→그때 fresh logcat/probe/start(RenderDiagnostic)→자연 플레이 종료 후 결과 화면 유지→메인 회수·독립 축 검증이다. 90초는 자동 수집 상한이며 사용자가 반드시 유지할 조건이 아니다. 먼저 끝난 경기는 부분 자료로 해석하고 강제 항복·자동 재시험을 요구하지 않는다(후속 사용자 지적에 따른 운영 정정). 양팀12를 계속 유지하도록 요구하지 않으며 strict 밀집 충족은 별도 판정한다. 재경기에서 새 측정을 하려면 반드시 앱을 재시작한다.

### 34.8 RenderDiagnostic 실기 회수와 분석 마감

메인 Login/Play OFF 확인 후 기록했다. `.utmp/multiplayer-battle-20261105/run-7fe8227d/collected-20261111-234845/CollectionReceipt.json`과 같은 run의 `IndependentAnalysisReceipt.json`을 직접 대조했다. RenderDiagnostic/TimeLimit90초·aborted=false/capacity=false,2,699frame/900NCC/756damage/75sampled qualifying bin이며 core/hash/양쪽 anchor와 로그 회수 검증이 완료됐다. hostTechnicalComplete/denseLoadCriteriaMet/gpuDiagnosticEligible/logsVerified는 true다. strict hostTelemetryEligible=false는 목적 분리의 정상 결과이며 실패로 보지 않는다.

| 관측 대상 | 이번 표본 결과 | 해석 한계 |
|---|---|---|
| full Update 간격2,697개 | 평균33.350074/p9535.970193/p9938.504615/최대41.226346ms,>50·>100ms각0 | wait 포함 cadence이며 CPU self 아님 |
| NCC900 wall | 평균2.170229/p953.5304/최대7.5868ms | 전투 tick 경과시간이며 순수 CPU self 아님 |
| positive GPU2,700개 | 평균27.474437/p9535.657580/최대53.727888ms,>33.33ms251·>50ms1 | delayed timing이며 현재 cadence와 자동 연결 금지 |
| CPU main/render/present wait | 평균20.622915/6.141210/12.685081ms | 합산·단순 뺄셈으로 CPU 점유·C# self 산출 금지 |
| GC counter | delta14 | GC 시간·pause 원인 미측정 |

FrameTiming2,713행의 timestamp는 모두 고유하고0 ID는 없으나 인접 역순15건이다. mask31=2,700/mask15=13, frequency999999999이며 clock 폭90.444814초로 준비 head/지연 tail이 섞일 수 있다. 지원과 raw 자료 완결 관측은 성공했지만 개별 cadence frame 정렬·GPU pass/특정 VFX owner는 미확정이다.75sampled bin을 연속75초·최대 밀집90초로 표현하지 않는다.

로그 검수는 Android PID67행의 Exception/NullReferenceException/FATAL/[ERROR] 검색0, Editor anchor22,380행의 Exception/NullReferenceException/FATAL 검색0이다. 다만 Editor에는 UnitAttackShadowObserver END의 [ERROR]1건이 존재한다. 이는 종결 진단이다. 추가 소스 대조에서 UnitAttackShadowObserver.cs3298~3313은 _dropped를 LocalVerdict 분류기로 전달하고3381~3402의 분류기는 droppedCount>0만으로도 FAIL을 반환한다. 해당 END의 dropped202는 진단 완결성 FAIL의 충분조건이며 clientAccepted1056·clientRejected/targetMismatches/correlationFailures/terminalPreflightFailures/manifestFailures/gameplayWrites 각0이었다. 다만 다른 미출력 counter나 표현 상태까지 배제한 유일 원인 확정은 아니며 실제 전투 기능 오류 또는 전체 표현 정상 판정으로 확대하지 않는다. 성능 수집의 hash/자료 유효성과도 별도다. 검색 범위에서 크래시가 관측되지 않았다는 사실과 전체 오류0·피해 오류 없음은 다르다. Release 전체 Dev 로그·모든 경로를 포괄하지 않는다.

이번은 자연 플레이에서90초 상한까지 기록된 회수·기기 지원 관측 성공이며 제품 GPU 교정 효과 PASS가 아니다. 사용자가90초 조건 유지가 어렵다고 다시 지적했고, 앞 보완이12gate/종결 재사용만 바꾸고90초 상한은 남겼음을 인정했다. 앞으로 자연 종료 기록도 부분 자료로 해석하며90초 유지 강요·강제 항복·자동 재시험을 하지 않는다. 앱당1회 수명과 재경기 측정 시 앱 재시작 조건은 유지한다.

과거 baseline과는 main의 여러 변경·FrameTimingStats ON·무작위 맵/교전량이 달라 향상률을 계산하지 않는다. 열·전력 연속 trace도 없어 기기 한계를 단정하지 않는다. 다음은 렌더 비용 owner 분리 검토 제안이며 아직 특정 원인·교정 대상은 확정하지 않았다. 추가 코드·설정·빌드·사용자 재테스트 요청·원본 삭제는 없다. 원본과 hash/anchor/실패 자료는 보존한다.

### 34.9 Bloom 단일 변수 탐색 계획과 선정 근거

2026-11-12 사용자는 다음 렌더 원인분리 준비를 승인했고 메인은 구체 구현을 승인했다. 읽기 감사에서 Game의 단일 활성 GlobalVolume이 SampleSceneProfile을 참조하며 Bloom active/intensity0.25/threshold1/scatter0.5/highQualityFiltering1임을 확인했다. Vignette0.2·Tonemapping mode1·MotionBlur inactive, 카메라 HDR/postFX와 빈 stack, Mobile scale0.8은 유지한다. 후보는 Bloom 하나이며 아직 병목 원인으로 확정한 것은 아니다. 전체 postFX/VFX 비활성화나 영구 품질 낮춤은 하지 않는다.

변경 범위는 기존 owner와 private collector2파일의 opt-in RenderBloomExploration이다. Volume 런타임 profile을1회 clone하고 공유 asset은 바꾸지 않는다. A는 원래 값/override 상태, B는 Bloom intensity.overrideState=true/value0을 적용하는 안으로, active=false만 사용할 때의 pipeline default fallback을 피한다. 실제 resolved stack 관측 가능성은 검토 중이다. ABBA 각8초/총32초 상한 후 즉시 Bloom을 복구하고 측정창도 Stop(reason=BloomExplorationComplete,aborted=false)한다. 자연 GameEnd 뒤 기존 방식으로 저장한다. 조작32초/core90초를 따로 유지하면 불필요한 잔여 측정이 생기므로 새 목적만32초 창으로 정한다. 기존 StrictBattle/RenderDiagnostic의90초 상한은 바꾸지 않는다. 자연 조기 종료는 부분 자료로 보존하고32초·90초 유지나 양팀12를 사용자 조건으로 요구하지 않는다.

정상 END/Stop/fault/scene 변경/Disable/Destroy 및 예외 경로에서 원래 profile·Bloom 값/override를 복원한다. 외부에서 profile을 교체했다면 타 소유 상태를 덮어쓰지 않는 소유권 guard를 둔다. 기존 RenderDiagnostic/StrictBattle은 바꾸지 않고 HP/RPC/맵·게임 규칙·품질 asset은 유지한다. 권위 경계는 GameSystemRules_UnitCombatSynchronization의 권위 결과/HP·사망 상태 계약, 원시 보관은 LogRules1.10, 테스트 항목별 진단 대응과 검증 축 분리는 WORKFLOW를 따른다.

| 테스트 항목 | 남길 진단 자료 | 수용·한계 |
|---|---|---|
| 목적/원본·복구 | ready/정확 purpose·profile 소유권·A/B 값과 override·복구 receipt | 외부 profile 소유권 침범 금지, 정상/종결/예외 경로 복구 확인 |
| ABBA 경계 | phase/readPhase/age/cohort·전이 시각·부분 종료 이유 |2초 전이 제외는 heuristic이며 raw GPU source phase 보장이 아님 |
| GPU/부하 관측 | raw timestamp/지원·CPU frequency·교전량·카메라/열 상태·END/hash | 지연 GPU clock이 cadence에 미매핑이므로 read phase만으로 정확 A/B GPU 귀속 금지 |
| 탐색 결과 | phase별 보수적 통계·제외/미완 cycle·지원0/표본 부족 | 탐색만; 원인·절감률·FPS 향상 확정 금지 |

현재 감사/후보 선정과 진단 수단 구현·정적 검증을 완료했다. 실제 Unity native·기기 동작과 성능 효과는 미검증이다(2026-11-12 최종 검증 결과 반영). 새 빌드는 이번 요청에서 시도하지 않았고 기기 A/B 지원·복구·효과는 미검증이다. 원본·기존 실패 자료는 보존한다. 자연 전투의 부하/카메라/열 변화와 무작위 맵 차이, 지연 head/tail·전이·미완 cycle 때문에 비교가 성립하지 않으면 표본 부족으로 보고하고 재시험을 자동 요구하지 않는다. 최종 구현/검증 결과를 받은 뒤 실제 변경과 미완 범위를 갱신한다.

계획 보완: readiness의 bloomSupportReady/status를1Hz로 관측하고 prepare에서 scope 지원을 검사해 생산 전 부적합을 거절하며 start에서 재검증한다. camera VolumeStack의 resolved 값은 읽기 지연·갱신 순서가 미확정이므로 별도 관측으로 남긴다.2초 전이 guard로 원시 GPU source phase 정확성을 보장하지 않는다. 변경은 owner/private collector2파일이며 아래 정적 검증을 완료했고 빌드 시도0이다.


**최종 구현·검증 결과:** `.utmp/bloom-exploration-20261112/FinalVerificationReceipt.json`과 FinalSourceHashes/CleanupReceipt를 직접 읽었다. owner·private collector2파일의 최종 hash를 QA가 live와 독립 대조해 일치했으며 중대 미해결0이다. 영수증 qa 필드의 pending은 생성 당시 상태이고 이후 최종 QA 메시지로 마감됐다. SDK csc6구성 오류0·변경 owner 경고0, actual-source Host235/Collector13/kernel6,366case·59,872assert가 통과했다. engine/recorder/NGO fixture·mock ADB 범위이며 실제 native 검증은 아니다.

Bloom profile과 각 component를 deep clone하고 B는 intensity override=true/value0, A는 원래 값/override로 전환한다.32초 창 정상 종료·원복 후 자연 GameEnd 저장을 구현했다.25초에 네 phase를 진입했더라도 정상32초/실제 phase 시간 조건을 만족하지 않으면 완료로 수용하지 않는다. shared/default 비누출·이전 runtime 참조 복원·외부 profile 교체·종료/예외 정리는 fixture에서 확인했으나 Unity native deep clone/메모리·시각 원복은 NOT RUN이다.

read cohort의 실제 양수 GPU 개수/평균/p95, A/B resolved 일치, read frame의 부하·카메라 변화를 출력한다. raw GPU source phase는 Unknown이며2초 전이 제외와 resolved-stack-at-read는 정밀 source phase 보장이 아니다. 이 통계는 탐색 자료이고 Bloom 원인·절감률·FPS 향상을 확정하지 않는다. 기존 RenderDiagnostic/StrictBattle은 유지한다. 준비 scope 지원 거절·목적 binding·앱당1회·자연 조기 종료 부분 보존 조건도 유지한다.

이번 asset/설정 쓰기는 없었으나 개별 asset before snapshot이 없어 현재 hash만으로 전체 asset 바이트 전후 동일을 독립 입증한 것은 아니다. FrameTimingStats1은 유지했고 품질/API/targetFPS/shader/HP/RPC/맵 변경은 없다. 완료는 진단 수단 준비이며 native Unity·Android·빌드·효과 검증은0/미실행이다. 중간 DLL/PDB13개11,930,492바이트만 CleanupReceipt로 삭제했고 원본/Before/source/rsp/tests/logs/결과/영수증은 보존했다.

## 35. 전체 최적화 후보 장부와 선정 기준 재정리

2026-11-12 사용자는 실제 교정을 **예상 비용 절감이 큰 순서, 비슷하면 작업량이 작은 순서**로 선정하도록 승인했다. 관측 비용은 제거 가능한 비용이 아니며 GPU27ms>NCC7ms 같은 비교를 절감 순위로 쓰지 않는다. 관측시간·절감 추정·근거 신뢰도·조사 작업량·구현 작업량을 따로 기록한다. 현재 확정 절감ms가 없어 구현 순위는 미정이고, 기존 자료와 특정 소스가 연결된 전투 tick의 짧은 읽기 조사부터 한다. 이것은 최대 절감 후보 확정이나 P1 구현 선정이 아니다.

### 35.1 이미 확인한 효과와 구현만 확인한 항목

| 분류 | 확인한 사실 | 남은 한계 |
|---|---|---|
| HexTile | 과거187개 생성246.899ms와 후속211개6.879ms·ProBuilder0 관측 | 수량·부하가 달라 통제 개선율 아님 |
| DetectRange | 실제 true6표본 snapshot조회168→54, false 동일 | 커널 순서 편향·true 빈도 때문에 전체 FPS 효과 미입증 |
| Ranking | 해당 로비 진입 frame의 기존10행 생성이0으로 관측 | 첫 순위 열기로 비용 이동; 첫 열기 운영 데이터 미검증 |
| 선택 Dev 로그 | 선택한 Info/Warning 원문 파일 보존과 native 중복 생략 적용 확인 | 일반 Release 제품 성능 개선과 별도 |
| 구현·효과 미확정 | NCC3버퍼, internal visual pool512, Wave/predicate·UnitFactory 배열·문자열 교정 | CLR/소스·일부 관측 근거를 각 기기 효과로 확대하지 않음 |
| Bloom 탐색 수단 |2파일 구현·정적 QA 완료 | 사용자 새 우선순위에 따라 보류, 빌드·native·기기 A/B 미실행 |

### 35.2 전체 후보 장부

아래 각 행의 **예상 절감은 미확정, 구현 작업량은 미산정**이다. 조사 작업량도 정량 산정하지 않았으며 첫 전투 tick 읽기만 짧은 조사 범위로 정했다. 관측·구조 근거를 실행 승인이나 원인 확정으로 바꾸지 않는다.

| 후보 | 상태·근거 | 다음 행동 |
|---|---|---|
| 전투 계산 | NCC wall/self에 진단·표현·제품 계산 혼재, 최신900 wall평균2.170229/최대7.5868ms | 기존 tick 자료와 호출 소스 연결을 먼저 읽기 조사 |
| 적 탐색 | boolean early exit의 조건부 조회 감소 확인 | 반복 호출/true·false 분포와 남은 탐색 경로 대조 |
| 타일 검색 | 비용 owner·호출량 미확정 | tick에서 실제 검색 경로와 반복 여부 확인 |
| 길찾기 | 이전 MoveAlongPath 관측은 순수 길찾기 시간 아님 | 경로 재계산/이동 적용 구분 |
| AI 갱신 | 빈도·비용 실측 미확정 | 호출 주기·서버/Client 책임 확인 |
| 유닛 생성·삭제 | Instantiate 종별 비용은 별도, 생성량/종결 부하 변동 | owner별 생명주기·표본 연결 |
| 풀 고갈·반환·보관 | VFX/tracer GameObject pool 이미 존재 | 새 pool을 먼저 만들지 않고 miss/반환/용량 근거 확인 |
| VFX CPU | 개별 업데이트/생성·정리 owner 비용 미확정 | 전투 tick과 표현 경로 분리 |
| VFX GPU | 투명 중첩은 후보이며 pass별 비용 없음 | 지원 지표·실제 pass 근거 확보 전 원인 보류 |
| 일반 렌더링 | GPU 평균27.474437ms 관측, frame 귀속·pass owner 미확정 | 렌더/대기·그림자/overdraw 후보 구분 |
| 후처리 | Bloom 단독 탐색 수단 준비 완료 | 사용자 우선순위로 보류; 임의 품질 변경 없음 |
| GC·할당 | counter delta와 CLR 할당 교정은 있으나 GC pause owner 미확정 | hot path 할당과 pause·빈도를 별도 확인 |
| 메모리 잔존·보관 | bounded pool/retained collection 존재, 누수 증거 없음 | 의도된 보관·고수위·해제 수명 구분 |
| 물리 | 현재 비용 근거 부족 | 사용 구성·실제 호출 여부부터 확인 |
| UI | 과거 Canvas 비용은 당시 phase 관측 | 전투 HUD와 로딩/로비 UI 분리 |
| 네트워크 | RPC wall에 생성·로그·처리 혼재 | 전송·처리·표현 owner 분리, 권위 계약 유지 |
| 로딩·복귀 | Release 씬 반환→Root.Start 앞 약117~133ms·함수 원인 미확정 | 사용자 결정으로 후속 보류, 기존 원본 보존 |
| 진단 | Dev 출력·caller stack 부담 확인/선택 교정 | 제품 비용과 계측 부담을 구분 |
| 발열·전력 | 연속 trace 부재, snapshot만 존재 | 과거 thermal 원인 단정 없이 비교 시 상태 기록 |
| 타워 임시 배열 |48~64바이트 가정은 추정이며 미실측 작은 후보 | 비용·빈도 근거 전 보류 |

다음 실제 교정은 조사 뒤 예상 절감의 범위/신뢰도와 구현 작업량을 비교할 수 있을 때 선정한다. 안전한 동등 교정 근거가 없으면 억지 순위를 붙이거나 코드를 바꾸지 않는다. 현재 장부 작성과 아래 첫 읽기 조사를 완료했으며 제품1파일 국소 교정을 완료했고 새 소스·설정·빌드·사용자 테스트·원본 삭제는 없다.


**첫 읽기 조사 결과 — AttackRange 존재 조회:** UnitCombatUseCase.cs1284~1287/1295~1311의 HasEnemyInRange/At는 FindFirstEnemyTarget 결과의 존재 여부만 소비하지만1672~1740은 모든 적 유닛/건물의 거리와 최근접 대상을 끝까지 갱신한다. NCC898~899의 TryFindTarget 실패 후 존재 조회와 연결된다. 앞서 완료한 DetectRange 별도 함수의 교정과는 다른 경로다. raw1510의 target query40회0.769ms는 묶음 비용이며 AttackRange 개별 귀속·제거 가능한 시간은 아니다. Tick7.048/self5.789ms도 전부 절감 가능한 비용으로 보지 않는다.

사용자가 첫 항목 진행을 승인해 읽기 계약·동등성·작업 감소 가능성 검토를 진행 중이다. 읽기 계약 확인 후 메인이 아래 국소 교정안을 승인해 구현·CLR/컴파일 검증을 완료했다. 최종 결과는 아래 실제 추출 CLR·컴파일 범위로 확인했다. 다음은 boolean/nearest 호출자 분리, provider 부작용·순서·거리/zero/Hex fallback·NaN/Infinity·동률·권위 계약을 확인하고 동일 입력의 출력 동등성과 검사량 감소를 근거화하는 것이다. false는 전체 탐색, nearest API는 최근접 의미를 유지해야 하므로 무조건 단축하지 않는다. 국소 구현은1파일의 작음~중간 범위로 예상하지만 검증이 주 작업이며 실제 호출 빈도·true 비율·frame 절감은 미측정이다. 다른 후보보다 절감이 크다는 확정 순위는 아직 없다. 제품1파일 교정 외 새 소스·설정·빌드 변경은 없다.


**승인된 국소 교정안:** UnitCombatUseCase1파일에서 HasEnemyInRange/At 두 boolean 호출만 private FindFirstEnemyTarget의 stopAtFirstMatch:true를 사용한다. private 인자는 기본false여서 nearest 호출자는 그대로 전체 최근접 탐색한다. 기존 `dist <= max && dist < min` 조건 안에서 최초 유효 유닛/건물을 반환하며 Hex fallback·DetectRange·거리 계산·권위/HP/RPC는 바꾸지 않는다. provider는 lookup+Transform 읽기, mapper는 순수 수학이라는 계약 확인을 전제로 한다. true인 경우 첫 유효 후보 뒤 나머지 검사를 생략하고 false인 최악 조건은 전체 탐색 동일이다. 절감ms/FPS·실제 빈도는 미확정이다.

검증은 실제 source 전후 동일 입력의 boolean/nearest 결과, zero/Hex fallback·NaN/Infinity·동률·단위/건물 경계·provider 조회 순서/검사량을 대조하고 변경 구성 컴파일과 독립 QA를 분리한다. 현재 구현·CLR/컴파일 검증 완료이며 새 Testcase·테스트 UI·빌드·기기 실행은 없다. native·기기 실행과 실제 성능 효과는 미측정으로 남긴다.


**AttackRange 최종 결과:** `.utmp/attack-range-existence-20261112/SmallHarnessResults.txt`와 CompileResults.json을 직접 읽었다. 실제 추출 baseline/new 메서드와 Unity Vector3/Mathf를 참조한 인공 입력 CLR30case/210assert가 통과했다. best 표본 provider 조회9→2·후보 검사9→2, false와 nearest 작업은 동일했다. 이는 실제 게임79% 개선·frame/FPS 이득을 뜻하지 않는다. SDK6구성 exit0/error0, 경고24/27/27/31/31/0이며 변경 소스 경고0이다. 메인5hunk diff 검토와 QA 정적 중대0을 확인했다.

완료 범위는 UnitCombatUseCase1파일의 boolean2호출/private 기본false/기존 거리 조건 내 조기 반환2곳과 위 CLR·컴파일·소스 검토다. 실제 Unity native/Android 실행·호출 빈도/true 비율·FPS/절감ms는 미검증이고 빌드는 시도하지 않았다. 새 계측·asset·설정 변경 없이 기존 dirty와 원본을 보존한다. 최종 영수증/실제 source hash와 QA 대조가 완료됐고 중간 DLL/PDB13개11,853,820바이트만 CleanupReceipt로 삭제했다. baseline/new source/tests/results/logs/rsp/영수증은 보존했다.


**실기 준비 중 빌드 귀속 불일치 확인(2026-11-12):** 메인은 run-34d51d4c의 RenderDiagnostic prepare/Client anchor·accepted probe까지 수행했지만 capture는 시작하지 않았다. 기기 앱의 ready는11:36KST 생성이고 package lastUpdateTime은11:35:59였다. 최신 prebuild11:07:41에는 교정 전 UnitCombatUseCase(89EE76… Before)가 기록되어 있으나 현재 교정 소스는14:05:04 수정·262E14… After다. 따라서 설치 앱은 교정 전이며 이번 교정 검증에 사용할 수 없음이 확정됐다.

메인은 source 귀속 확인 전에 생산·교전 보고를 요청했다가 즉시 대조 후 철회하고 사용자 대기를 요청했다. logcat-stop을 실행하고 원본을 보존했으며 앱/경기 조작·capture·Bloom 시작·새 빌드 시도는 없었다. 사용자 책임이 아닌 준비 안내 순서 오류다. 다음은 File Build And Run으로 새 교정 포함 빌드를 준비한 뒤 실제 설치 앱의 새 build/session과 prebuild source 귀속을 먼저 확인해야 한다. 현재 기기 효과 미검증 상태를 유지하고 기존 자료로 교정 적용·FPS 개선을 주장하지 않는다.


### 35.3 교정 포함 빌드의 실기 수집과 분석 마감

run-3bd7fabf의 `collected-20261112-163749/CollectionReceipt.json`과 실제 CSV·로그를 대조했다. 최초163646 회수의 logsVerified=false는 stop receipt 전 상태였고 추가 회수한 최종163749는 true다. 테스트 실패나 재시험이 아니다. prebuild16:03:52의 UnitCombatUseCase After262E14…와 설치16:17:58·새 runtime build 및 installed/local APK hash 일치로 교정 포함 빌드 귀속을 확인했다. 이전 교정 전 앱 준비는 별도 이력으로 남긴다.

이번은 RenderDiagnostic/Bloom 미시작,90초 TimeLimit·aborted=false/capacity=false다. completed chain/Host technical/logsVerified/GPU diagnostic/성능 자료 분석 수용은 true이며 strict HostTelemetryEligible=false는 purpose 분리 정상이다. 사용자 자연 플레이를 수집했고 에이전트가 경기 화면을 직접 육안 수용한 것은 아니다.

| 자료 | 이번 결과 | 해석 |
|---|---|---|
| frame2,702행 | head1/full2,700/end1; full평균33.320457/p95약35.872/최대40.926692ms,>50·>100ms각0 | wait 포함 Update 간격 |
| NCC900 wall | 평균1.849843/p95약3.395/최대6.198ms | CPU self 아님 |
| positive GPU2,699개 | 평균27.456306/p95약35.562/최대45.983311ms,>50ms0 | 원시 timing2,716행과 cadence 개별 join 미완 |
| 부하·GC | damage725/qualifying bin50,Blue5~17·Red17~47,GC counter delta15 | 연속50초·최대 밀집·GC 시간 증명 아님 |

GPU timestamp 폭90.465970초의 준비 head/지연 tail·원시 clock 한계를 유지한다. DetectRange pairs256의 boolean 동등은 기존 탐색 커널 검증이며 이번 AttackRange 직접 효과 표본이 아니다. 무작위 맵/부하·빌드/계측 차이와 직접 AttackRange timing 부재 때문에 전후 개선율·실기 절감ms·FPS 효과는 입증하지 않는다. 이번은 교정 포함 빌드에서 안정적인 약30fps 표본과 회수 완결성을 확보한 것이다.

최종 Editor anchor에는 UnitAttackShadowObserver END [ERROR]1건이 추가됐고 Exception/NullReferenceException/FATAL 검색0이다. 해당 END의 dropped585/verdictFAIL·clientAccepted468, clientRejected/targetMismatches/correlationFailures/terminalPreflightFailures/manifestFailures/gameplayWrites 각0을 직접 확인했다. 기존 소스 분류상 dropped>0은 진단 완결성 FAIL의 충분조건이며 유일 원인이나 전체 표현 정상·제품 공격 오류를 확정하지 않는다. full snapshot의 ERROR2는 anchor 전 과거 Movement END1과 이번 AttackShadow END1로 구분한다. Android PID487행은 해당 오류/예외 검색0이며 Release 전체 오류0을 뜻하지 않는다.

새 제품 failure를 확정할 근거는 없고 이번 교정의 직접 시간 효과도 미증명이다. 결과 화면을 추가 유지할 필요는 없다. 새 코드·계측 확대·설정·빌드·재시험 요청·원본 삭제 없이 분석을 마감하고 모든 원본/영수증을 보존한다.


**관리 메모리 해석 보완:** 최종163749의 full2,700행을 직접 대조하면 gcUsedBytes 최소22,089,728·최대28,553,216바이트(십진22.089728~28.553216MB)다. 이는 관리 힙 사용 관측이며 전체 앱/네이티브/GPU 메모리가 아니다. gcCollectionCount0은57→72로 delta15이며15회의 끊김·GC 정지시간을 뜻하지 않는다. GC Allocated In Frame은 Unavailable-NoDescription이므로 CSV의0바이트를 무할당·할당 절감 증거로 쓰지 않는다. 누수·pool 적정량·반복 경기 후 잔존·이번 AttackRange 교정의 GC 절감은 미검증이다.

이번 항목은 구현·출력 동등/조건부 조회 감소·새 빌드 적용과 안정 표본 확보 범위로 마감한다. 직접ms/FPS 효과는 미증명으로 남긴다. 다음 후보는 사용자 기준에 따라 실제 비용·발생 빈도·제거 가능한 부분을 비교한 뒤 선정한다. 작업이 작다는 이유만으로 바로 구현하지 않고 미측정 절감치를 만들지 않는다. 현재 전문가의 읽기 비교 결과를 기다리며 새 코드·설정·빌드·실기·삭제는 없다.


### 35.4 다음 후보 비교와 렌더 구조 조사 방향

첫 비교에서 렌더 GPU 평균27.456/p95약35.562ms,>33.333ms227/2,699표본을 관측했다. 지연 GPU 표본이므로 현재 cadence frame에 귀속하지 않는다. 전투 tick은 약10Hz/900개 wall평균1.850·최대6.198ms이며 CPU self가 아니다. GC counter15와 옛 할당1,596call/0.595ms 관측도 byte·caller 근거가 없어 제거 가능한 비용으로 환산하지 않는다. 세 영역 모두 실질 제거 가능량은 산정 불가하므로 구현 순위는 미정이다.

다음 조사는 렌더 pass/VFX draw 구조의 owner를 확인하는 방향이다. 이는 최대 절감 후보 확정이 아니며 작은 후보부터 바로 구현하지 않는다. 현 Release 앱에서 FrameTiming 지표가 관측된 사실과 GPU Usage Profiler 모듈 지원은 별개다. [Unity6.3 GPU Usage 문서](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/profile-rendering/profiler-gpu)는 Android Vulkan 미지원을 명시하고, [원격 Frame Debugger 문서](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/profile-rendering/frame-debugger/attach)는 Development Build를 요구한다. Frame Debugger는 draw/pass 구조 근거이지 GPU ms 증명이 아니다.

현행 다음 단계는 기존 구조 근거와 지원 수단 확인이다. 지원 불가는0시간이 아니며 현재 API 변경·새 도구 구현·품질 낮춤·빌드·기기 테스트·자동 사용자 재시험 요청은 없다. 원본 보존과 비용/빈도/제거 가능량 분리 원칙을 유지한다.

다음 최소 행동은 기존 Editor Frame Debugger로 대표 전투 화면1프레임의 shadow/opaque/transparent/postFX draw·material 구조를 보조 확인하는 것이다. 현재 실행하지 않았고 기기 GPU ms의 증거로 쓰지 않는다. 정밀 기기 pass 시간은 도구 지원 확인이 선행된다. GPU 교정은 owner 특정 전 비용·품질 위험이 미정, CPU 공간 탐색 구조 변경은 검증 부담, GC 수명/pool 변경은 잔존 위험을 별도로 비교한다. 작은 GC 교정을 우선으로 승격하지 않는다.

메인 최종 화면은 Unity Login/Android·Play OFF여서 현재 대표 전투 프레임이 없고 Frame Debugger 구조 검수는 미실행이다. 조사 준비와 실제 프레임 검수를 구분한다. 다음에는 기존 빌드를 재사용할 수 있는 Editor 대표 전투 프레임 확보가 필요하지만 기기 GPU ms 증명이 되지는 않는다. 이번 앱/Editor 재실행·씬 변경·임의 생산 조작·새 빌드·삭제는 없다.

### 35.5 2026-11-12 최신 main 재병합·검증 결과

메인은 사용자 승인으로 최적화 브랜치에서099e8a1b→eaa883d4를 fast-forward하고 autostash d5b66064를 재적용했다. `.utmp/main-merge-20261112-current/BackupReceipt.json`에는163항목/총6,044,744,733바이트, 새 복사22개3,922,230바이트와 기존 검증 백업 재사용104개·기존 삭제 상태37개의 absence가 기록됐다. 원본과 복구 근거는 보존한다.

PROJECT_STATUS/WORK_HISTORY는 자동 병합됐고 현재 HEAD 원문의 비어 있지 않은 모든 행이 보존됐음을 직접 대조했다. 새 main의 지속 스킬 영역 상태·규칙 참조·이력과 기존 Android 최적화 기록을 함께 확인했다. 이 두 문서는 이번에 추가 수정하지 않았다. 폰트 충돌 해결과 아래 소스 보존·SDK/실제 Unity 검증을 완료했다(2026-11-12 최종 결과 반영). 실기 멀티·시각·성능 검증은 별도 미완이다. 새 성능 효과·기기 수용은 주장하지 않으며 최종 결과 수신 뒤 필요한 상태만 갱신한다.

폰트 수치 정정: 메인이 먼저 전달한 main300은 원문 검산 전 인계 수치 오류다. font-audit/main.asset/local.asset를 직접 세면 glyphTable/characterTable은 main315/local316이다. QA는 main 직렬화315레코드 exact 보존·missing0·「서」1개 추가·atlas2,034바이트 차이가 추가 UsedRect 안에만 있음을 검수했다. live는 local과 바이트 동일이다. 숫자 보고 오류와 보존 안전성 검증을 구분하며 SupersetProof 집계 축 정정과 native 검증은 아래 최종 결과로 마감했다.

후속 집계 축 확인: main unique ID300/local301과 직렬화 레코드315/316은 기존 중복 ID 레코드15개 때문에 다르다.315를 고유 글자 수로 쓰지 않으며 보존은315직렬화 레코드 multiset 누락0 기준이다. 기존 중복은 이번에 수정하지 않는다. Unity Runtime17:27:12/Editor17:27:16 갱신과 busy 종료는 메인 관측이며 전체 회귀 실행 결과는 아래 최종 PASS로 확인됐다.


**11/12 병합 검증 최종 마감:** HEAD와 origin/main은 eaa883d4로 같고 최적화 브랜치·unmerged0을 메인이 확인했다. 폰트는 main 직렬화315레코드와 local 추가1개를 exact 보존하고 atlas 차이2,034바이트는 추가 영역뿐이며 비캐시 설정/참조는 동일하다. PostApply 분류는109바이트 exact·11개행만 변경·6overlap·37기존 삭제 absence로163항목이며 총백업6,044,744,733바이트를 보존한다. overlap 코드3개는 upstream SkillZone 추가 외 기존 최적화 삭제/교체0으로 QA 확인했고 자동 병합 문서2개 보존도 별도 확인했다. 폰트 해결의 git add는 메인이 수행했으며 autostash d5b66064는 보존했다.

현재357개 runtime 소스 누락 없이 새 SkillZone을 포함한 SDK 대표4구성(Editor/DevIL2CPP/Release/Tools) 오류0, Unity Runtime17:27:12/Editor17:27:16 갱신 세대를 독립 검수했다. 실제 Unity 메뉴 Full Regression을17:29:26~17:30:21 실행해8개 terminal PASS를 확인했다. `.utmp/main-merge-20261112-current/native/NativeValidationReceipt.json`과 suffix 원문을 보존했다. 이는 source/asset/Editor fixture 검증이며 Android·실기 멀티·시각·성능 보증은 아니다. Login/Play OFF 유지, 새 APK 빌드·commit·push는 없다.

다음 실기 확인은 새 병합 빌드의 source 귀속을 먼저 확인한 뒤 공격·이동 정상 동작과 새 지속 스킬의 진입/이탈·만료/경기 종료 동작을 최소 확인하도록 권고한다. 새 테스트를 실행했거나 완료한 것으로 기록하지 않으며 자동 재시험·임의 빌드는 하지 않는다. 추가 리팩토링·코드/설정 수정 없이 이번 병합 검증을 위 범위로 마감한다.

### 35.6 제품 main 반영·push 완료와 측정 브랜치 분리

제품 선별 커밋 object `14247c6fa4aafaa9a47bfb0c561765b8020524d0`을 준비했다(부모 eaa883d4). 제품23파일은 타일 mesh·존재 조회·표현/버퍼·문자열·랭킹과 승인 로그 정책 및 필수4문서이며 측정 define/FrameTiming 설정·helper/tools·raw·Task/실수 기록은 제품 main 대상에서 제외한다. 제품 분리본 대표4구성 컴파일과 합성 tree 문서 검사는 통과했지만 native 검증은 이전 혼합본 범위이고 실기 전체·FPS 효과는 미증명이다.

제품23파일을 로컬 main14247c6에 반영했고 현재 codex/android-performance-optimization은 제품 부모와 측정 전용 분리를 담은49db109a로 fast-forward했다. 2026-11-12 사용자 명시 승인 후 제품 main만 원격 push를 완료했고 메인은 origin/main도14247c6과 동일함을 Git 출력으로 확인했다. 측정 브랜치는 push하지 않았다. 이는 새 검증·빌드·실기 실행을 뜻하지 않는다. 측정 설정/도구와 기존 Task·문서 이력은 최적화 브랜치에 보존했다. Task/메타는 기존 GUID를 그대로 복사했으며 새 Task/Testcase를 만든 것이 아니다. autostash3c66f2c4 재적용의 문서5충돌은 먼저 private 백업 후 검증 feature 원문으로 해결했다. 자동 병합 WORK_HISTORY는 feature와 전체 텍스트 동일·고유행 누락0을 확인했다. 원본 autostash와 충돌 백업을 보존하며 이번 Git add는 메인이 담당한다. 새 빌드·실기는 없고 분리본4구성 컴파일/문서 검사 통과와 이전 혼합본 native 검증의 범위를 구분한다.


## 36. 기존 기능을 보존하는 두 경로의 추가 최적화

2026-11-13 사용자는 추천 작업 진행을 승인하면서 기존 기능을 훼손하지 않는 것을 최우선으로 강조했다. 기존 Task 기록 승인은 계속 유효하다. 이번은 공격 결과 묶음의 중간 할당과 이동 복귀 fallback의 불필요한 전체 맵 순회만 줄인다. 원래 검증·결과 소유권·선택 순서·이동 경로/피해/RPC 동작을 유지하며 기존 코드는 검증 전 주석 비활성화 상태로 보존한다. UI 정수 캐시·타일 material은 후속 보류다.

### 36.1 승인된 구체 범위와 검증 경계

변경 대상은 NetworkAttackPresentationBundle.TryCreate와 UnitMovementUseCase 두 파일이다. 앞선 후보 명칭 AttackPresentationBundleAssembler 대신 실제 제품 소스의 NetworkAttackPresentationBundle을 기준으로 한다.

| 대상 | 줄일 작업 | 보존할 계약·안전 게이트 |
|---|---|---|
| 결과 묶음 TryCreate | 중간 List/ToArray를 호출별 독립 배열+count로 대체 | 최종 owned 배열/RPC shape·validation·정렬 비교·prefix 중복 검사·실패 false/results null 유지. 잘못된 nonHit에 복수 facts가 있어도 capacity 초과 예외 없이 거절. 공유 pool/전송 배열 재사용 없음 |
| 전방 복귀 fallback | 전체 grid Tiles 순회 대신 HexDirectionExtensions.Count의6방향 Neighbor/GetTile 조회 | 기존 후보 List와 Sort(distance,Q,R)·route 검증·RequestMoveCore·선택·early route·finalTarget 유지. 변경은 fallback 후보 열거만 |

근거는 유닛 규칙 U-MOV-ALIGN의7·8·11(스냅 없는 복귀·단일 권위 writer·같은 finalTarget의 안전 복귀)과 전투 동기화 NET-PRESENT-003의 완결/멱등 표현 계약이다. 원본 규칙을 바꾸거나 실패 gate를 완화해 최적화하지 않는다.

기존 검증 수단으로 baseline/new 출력 동등·호출별 독립 소유권·invalid/경계 맵·동률/route 선택을 확인하고 컴파일/독립 검토를 분리한다. 새 측정 도구·계측·테스트 UI/타이밍 채팅·새 Task/Testcase는 추가하지 않는다. 현재 구현·CLR/SDK 컴파일·정적 검토를 아래 범위로 완료했으며, 사용자 별도 실기와 절감ms/FPS 효과는 미증명이다.

배열 용량 구체화: HitApplied && facts.Length>0이면 facts.Length, 그 외는 facts.Length+1로 확보한다. invalid nonHit의 복수 facts도 prefix가 capacity를 넘지 않게 하며 기존 거절 순서·false/results null을 유지한다. 정상 성공 결과는 정확 길이의 호출별 owned 배열1개다. 이 크기 규칙도 아래 baseline/new 검증에 포함했다. native·실기 수용은 별도다.


**최종 구현·검증 결과(2026-11-13):** `.utmp/combat-allocation-neighbors-20261113/FinalVerificationReceipt.json`, FinalSourceHashes/FinalCompileResults/SourceIdentity/HarnessResults를 직접 대조했고 live2파일 hash가 일치했다. 실제 before/new 추출 메서드21case/79assert(bundle12·movement9)가 통과했다. 실제 제품 계약 타입 DLL·Unity/HexCoord/HexDirection을 사용했으며 주변 grid/mapper/route는 fixture라 native/RPC·실기 검증은 아니다.4구성(Editor/AndroidDevIL2CPP/Release/EditorTools) SDK compile exit0/error0, 기존 경고24/27/31/0·변경 파일 경고0, runtime357개 누락0이다. QA는 전체 소스/추출 hash와 fixture·컴파일 주장도 독립 검수해 중대 결함0을 확인했다.

movement의121타일 fixture에서 전체 열거 대신6조회로 같은 후보·선택 결과를 얻었고 교체 루프 밖은 exact 보존했다. bundle은 List 객체+backing+ToArray 중간 단계를 호출별 owned 배열1개로 줄이는 코드 구조이며 할당 바이트·ms는 실측하지 않았다. 동등성·검사 감소 근거를 실제 게임 FPS·경로/RPC 전체 수용으로 확대하지 않는다. 기존 동작 코드는 주석 비활성화로 보존했고 제품 변경은2파일뿐이다. 기존 dirty를 유지했으며 UI 정수 캐시/material은 계속 보류한다.

이번 완료 범위는 구현·private 검증 scratch·SDK compile·정적 QA다. 새 제품 로그/측정 도구·설정·빌드·사용자 실기·전체 기능 회귀는 실행하지 않았다. 실제 절감ms/FPS와 native 동작은 미검증이다. 추가 문서·새 Task/Testcase는 만들지 않는다.

정리 완료: 이번 private 검증 scratch에서 생성한 DLL/PDB11개7,801,856바이트만 CleanupReceipt.json으로 확인·삭제했다. Before/source/fixture/rsp/results/log/receipt와 다른 기존 자료는 보존했다.


### 36.2 실기에서 작업량·기능 보존을 확인하기 위한 준비

사용자는 절감 비용과 기존 기능 유지의 확인 준비, 준비 후 빌드 시도와 테스트 절차 안내를 승인했다. 이번에는 Development Android 전용 helper1개와 기존2method hook만 추가하는 계획이다. Application 경계를 지키고 기존 GameEvents.OnGameEnd/GameLog.Dev를 재사용한다. private flag·새 측정 mode·90초·채팅 교전 trigger 없이 자동 시작하며 READY/END 요약만 출력한다. 진단 구현·정적 검증은 아래 결과로 완료했다. 실제 native·기기 실행은 별도 미완이다.

실제 bundle 호출의 success/false/throw·결과 원소 수를 집계하고 성공당 제거된 List1/중간 backing1/ToArray 복사1과 복사 원소 수를 구조적으로 계산한다. 이는 GC 바이트·시간·FPS 실측이 아니다. 최대16회 원래 순수 TryCreate 결과와 verdict/null/전체 field·순서·배열 독립성을 비교한다. 이동 fallback은 실제 N tiles 대6lookup을 집계하고 최대16회 원래 순수 후보 계산·정렬 결과만 대조한다. A*·공격/피해/RPC/이동 writer를 다시 실행하지 않는다.

coverage0은 NotExercised, END 요약 없음은 Incomplete이며 diagFault는 기능 불일치와 분리한다. 기존 진단과 이번 source/build 귀속을 확인한 뒤 로그 기반 작업량·동등 관측만 수용한다. 기존2 Release 측정 define의 Development 금지 guard와 충돌하므로 빌드 준비 때 메인이 기존 토글을 OFF하고 Development ON으로 전환할 예정이다. 측정 tool 코드를 삭제하는 것이 아니다. 현재 빌드 시도는 없으며 준비·검증 뒤 실제 File Build And Run 클릭까지만 메인이 수행하고 완료 확인·실기 플레이는 사용자가 한다.

귀속 보완: 기존 PerformanceCaptureBuildManifest.cs의 Sources에 이번 helper·UnitMovementUseCase·NetworkAttackPresentationBundle3경로만 추가한다. 새 도구를 만드는 것이 아니라 실제 빌드 소스 귀속 확인 누락을 막는 범위다. 진단 구현과 아래 최종 검증을 완료했다. 사용자 절차는 정상 종료→로비 복귀만으로 하며 결과 화면10초·90초 유지를 요구하지 않는다. 요약 누락은 Incomplete, 관측 호출0인 분기만 NotExercised로 구분한다.


**진단 준비 최종 결과:** `.utmp/combat-allocation-neighbors-20261113/development-check/FinalVerificationReceipt.json`과 FinalSourceHashes를 직접 읽고 helper/meta·2hook·manifest의 live5hash 일치를 확인했다.4 SDK 구성 오류0·변경 파일 경고0, runtime358개 누락0이다. Release/Editor의 진단 전처리 블록을 제외한 기존2method 본문은 공백 무시 exact 보존했다. 실제 helper 본문의 대체 event/sink11assert와 기존 제품 추출21case/79assert가 통과했고 QA 독립 검수 중대 결함0이다. native/IL2CPP 변환·실기·전체회귀 PASS를 뜻하지 않는다.

[COMBAT-OPT] READY는 Protocol1·Session/Round·Role·Build=ArchiveHeader를 남긴다. GameEnd SUMMARY Complete=True/Faults0/각 Mismatches0과 해당 Samples>0일 때 그 부분을 Observed로 수용한다. 호출0은 NotExercised, SUMMARY 없음은 Incomplete다. 구조 카운터는 실제 제거 객체·복사·조회 작업량의 계산이며 byte/ms/FPS/net GC가 아니다. 최대16회 비교의 추가 작업·할당은 별도 진단 오버헤드다.

메인은 USB unauthorized 확인으로 사용자 연결 승인 대기와 기존2 Release 토글OFF/Development ON 준비를 진행 중이다. 실제 UI 설정 완료·File Build And Run 시도·새 기기 실행은 아직 미완이며 이후 클릭 결과를 받은 뒤 상태를 갱신한다. 사용자 절차는 Android Host+Editor Client 일반 전투→적 처치 후 이동→경기 정상 종료→로비 복귀→완료 알림이다.90초·교전 채팅 trigger·결과10초 유지를 요구하지 않는다. source/build 귀속과 READY/SUMMARY 원문 확인은 메인이 담당한다.

진단 준비 후 정리: development-check/CleanupReceipt.json의 중간 DLL/PDB10개7,847,212바이트만 삭제했고 다른 원본/소스/검증 자료는 보존했다. UI에서 Development ON·Autoconnect Profiler/Deep/Script Debugging OFF와 Detect 토글OFF를 메인이 확인했다. 이후 메인이 File Save Project 후 Android defines가 DOTWEEN만 남아 두 Release 측정 define OFF임을 확인했다. Development ON·Autoconnect Profiler/Deep/Script Debugging OFF를 화면에서 재확인하고 실제 File > Build And Run을1회 클릭했다. 직후 HoldOn MonoCompiler.Tick 화면만 관측했으며 빌드 완료 감시는 하지 않았다. 성공·설치·새 실행·실기 PASS는 미확인이다. ADB는 unauthorized 상태를2회 확인해 사용자 USB 허용 답변을 기다린다. 새 앱 실행 보고 뒤 Android Host+Editor Client 일반 전투·적 처치 후 이동·정상 종료→로비→완료 알림 절차를 안내하며90초·결과10초 대기는 요구하지 않는다.


### 36.3 Dev 기기 테스트의 작업량·동등성 관측 결과

`.utmp/combat-allocation-neighbors-20261113/device-test-20261113/Android-RuntimeLog.txt` 원본9,646,513바이트·SHA E07B4589…A7017A7을 직접 확인했다. 실행 archive는 seq1~8,260의 UTF-8 byte/record END와 완결 prefix가 정상이고 partial tail0이다. 같은 Session/Round1에서 READY와 GameEnd SUMMARY Complete=True/Faults0을 관측했다. 현재 빌드 manifest3source hash는 current와 일치하지만 prebuild GUID0과 runtime GUID를 직접 연결하지 못한 귀속 한계는 남긴다.

| 대상 | 실제 로그 결과 | 수용 범위 |
|---|---|---|
| bundle |279호출/성공279·false/throw0,16비교 mismatch0 | 해당 표본의 결과 field/order·독립 배열 비교 Observed |
| 구조적 할당·복사 | List279+중간 backing279=558객체 제거 계산,copy279회/286원소 | 구조 계산이며 net GC·byte·ms·FPS 실측 아님 |
| movement fallback |32호출,기존231타일×32=7,392방문 대6×32=192조회,차이7,200 | 국소 후보 열거 작업량; 전체 이동/A*선택 runtime 재실행 비교 아님 |
| 이동 후보 비교 |16비교 mismatch0/Fault0 | 순수 후보·정렬 표본 동등 Observed |

최대16회씩의 baseline 비교 작업/할당은 별도 진단 오버헤드다. 이번 구조 카운터를 전체 게임 비용 절감이나 GC 실측으로 바꾸지 않는다. Editor 원문도21:14~16의 실제 Client 역할과 같은 경기 근거가 있어 이전 역할 미확인 상태를 해소했다. 메인이 대조한 결과 END는 Client accepted279/rejected0·Host results279/failures0이지만 전체 기능·시각 수용을 대신하지 않는다.

Android 원문 Warning74/Error1의 UAS END FAIL은 별도 진단 축이다. 메인 추가 대조에서 presentation-shadow-failure invalid/Invalid의 Enqueued4+EmittedImmediate4 총8관측이 observer presentationInvalid→FAIL의 충분조건과 연결됐다. 근본 원인과 이번 교정 인과는 미확정이며 별도 result/presentation END의 EVIDENCE/failures0과 구분한다. 전체 게임 PASS·새 제품 결함 확정으로 쓰지 않는다.

자동 파일 저장은 Unity 기본 기능이 아니라 기존 RuntimeLogger 확장이다.11/12 제품 main 반영 사실과 최초 도입일은 다르며 최초일은 여기서 확정하지 않는다. 사용자 별도 테스트는 실행됐지만 이번 두 경로의 실기 표본 동등성·구조 작업 감소 관측 범위로만 마감한다. 직접 절감ms/FPS/GC바이트·전체 회귀는 미증명이다. 원시 로그·기존 자료를 보존하며 새 코드/규칙/빌드·삭제는 없다.

사용자 수용 범위 정리: 이번 두 최적화가 잘 됐다는 의미는 실제 호출에서 불필요 작업 감소와 표본 기능 동등성 근거를 확인했다는 범위다. 실제ms/FPS/net GC 절감·전체 게임 정상은 입증하지 않았고 별도 UAS FAIL은 미해결이다. 사용자의 이번 문서 정리 요청에 따라 이 경계를 유지한다.

로거 현행 원문 읽기 감사: core RuntimeLogger/LogSessionOwner/ConsoleSink의 승인 Dev archive·선택 NoStack 정책은 이미 제품 main14247c6에 반영됐다. 이번 COMBAT-OPT helper와2hook은 별도이며 로거의 최초 도입 또는 새 main 반영 완료로 혼동하지 않는다. LogRules1.10은 Editor 일별 이어쓰기·Android Development 실행별 private 원본, 회수/완결·hash 검증과 승인 private 업로드, 자동 삭제 철회와 수동 Editor30일/90일 정리를 구분한다. 이 수동 메뉴는 `_editor`만 대상으로 하며 Android 원본/회수 사본의 새 보관기간·자동삭제 정책을 정한 것이 아니다. 당시 읽기 감사에서는 정책을 바꾸지 않았고 이후 설명 보강과 아래 사용자 선택을 반영했다. 원시 삭제·Git 변경은 없다.

로거 설명 보강 완료: LogRules1.10/1.12에 Editor 일별·Android Dev 실행별 자동 파일과 일반 Release archive 없음, 각 메뉴의 실제 정리/회수/ZIP 범위 표를 추가했다. 정책 변경은 아니며 새 LogHistory 원문은 만들지 않았다. core GameLog/Owner/FileSink는 main14247c6과 동일하고 branch RuntimeLogger/ConsoleSink의 남은 차이는 측정 marker/getpath 등이다. LogcatCapture23~25의 오래된 주석은 이번 코드 변경 범위가 아니므로 후속 확인 사항으로만 남긴다. 별도 ADB 회수 성공과 기존 메뉴의 앱 파일 자동 회수 완성을 구분한다. 회수 검증 후 종료 세션 선택 삭제 권고와 기간/용량 자동 정리 중 사용자는 PC 복사·검증 후 종료 세션 선택 삭제를 확정했다. 기간/용량 자동 정리는 도입하지 않으며 보관일수·quota·자동삭제를 추가하지 않았다. 동기 파일/flush 비용은 무료가 아니고 기존 실패/완결성 계약을 유지한다.

보관 정책 선택 확정: 기기→PC 복사·원문hash/record 검증 뒤 종료·비활성 세션만 사용자 선택 확인 후 기기 원본을 삭제한다. 미회수·진행 중·검증 실패는 보호하고 PC 원본 정리는 기존 업로드 검증·별도 삭제 승인 계약을 유지한다. 정책 선택과 실제 기기 삭제·업로드 실행 승인은 다르다. 회수·ZIP 연결 지원 공백은 남아 있고 구현 계획은 준비 중이며 완료가 아니다. 이번 기기 삭제/코드/Git 실행은 없다.

### 36.4 Android Dev 로그 회수·선택 삭제·ZIP 연결 구현 계획

사용자는 선택한 보관 정책의 나머지 구현 완료를 지시했다. 새 EditorAndroidDevelopmentLogArchive.cs+meta, 기존 LogcatCapture의 internal ADB3API/결과형·오래된 주석 정정, 기존 LogArchiveZip의 고정 private root 후보 추가가 범위다. 기존 Task 지속 승인으로 기록하며 새 Task/Testcase·규칙 사본은 만들지 않는다.

메뉴는 `Hexiege/Logs/Android Development Logs`에서 목록→PC 복사 검증→선택 삭제로 진행한다. 회수 원문 RuntimeLog.txt와 CollectionReceipt.json의2고정 파일만 `.utmp/raw-logs/_Logs/Android/<KST날짜>/<session>/<collection-id>/`에 둔다. ZIP은 기존 Assets 후보를 보존하고 이 private root의2파일만 추가한다. raw는 소스 Git에서 제외하고 업로드는 사용자 수동·private 검증 계약을 유지한다.

기기 삭제 gate는 PC 원문/receipt 검증, remote 전후 size/SHA, seq·UTF-8길이·END/footer와 현재 PID 부재 확인이다. 심볼릭/경로 이탈·unknown은 fail-closed로 보호한다. 목록 checkbox와 bytes 표시 후2단계 확인을 거쳐 정확 RuntimeLog.txt만 rm하며 폴더를 삭제하지 않는다. 활성 파일은 snapshot 수집 가능하지만 삭제하지 않고, 완전 footer 없는 kill 원본도 정리를 허용하지 않는다. 자동 삭제·quota·기간은 추가하지 않는다.

순수 경계·receipt 변조·active PID·취소·remote 변경 fixture와 Editor SDK compile 후 메인이 읽기 전용 기기 수집·ZIP 실행을 확인할 예정이다. 현재 메뉴 구현·아래 수집/ZIP과 최종 safety/compile QA 결과를 확인했다. 실제 기기 삭제·업로드는0/미실행이다. 실제 결과 수신 뒤 LogRules 지원 상태를 갱신한다. 완료 후 main에는 로거 관련 코드/docs만 선별하고 실험 COMBAT-OPT·측정 옵션은 제외할 계획이다. 기존 원본과 사용자 dirty를 보존한다.


**실제 메뉴 수집·ZIP 검증(2026-11-14):** 메인이 Unity Android Development Logs 메뉴에서 기기 목록7개→최신 선택→PC 복사·검증을 실행했다. 고정 private root의 CollectionReceipt를 직접 읽었으며 원문9,649,377바이트·SHA f09f03f4…50c8679, remote 전후/PC size·hash 일치를 확인했다. 정상 종료 footer가 없어 SnapshotIncomplete:NoFooterActiveOrKilled로 분류했고 삭제 불가 보호는 예상 동작이다. 수집·분석 보관은 가능하지만 종료 세션 삭제 수용이 아니다.

기존 Create Log Archive ZIP UI로 android-logger-collection ZIP을 생성했다. 원문+receipt2entry/원본 총9,650,044바이트·압축540,970바이트·SHA0efbbf70…e13f68을 메인이 검수했다. raw256MiB/receipt64KiB는 도구 읽기 한도이며 앱 보관 quota가 아니다. `.utmp/android-development-logs-20261114/SafetyResults.txt`의 실제 safety36검사와 DeleteFlowResults의 DeleteSelected orchestration9모의 검사를 직접 확인했다. device 명령/삭제는0이며 ADB/dialog/JSON을 대체한 범위다. CompileResults는 MainRuntime/ToolsMainRuntime/ToolsCurrentRuntime3구성 오류0이다. QA source blocker0을 확인했으나 최종 설명 수정 뒤 sourcehash/compile을 다시 확인했고 QA5hash 일치·blocker0이다. 실제 기기 삭제·업로드는 미실행이며 main 선별 통합은 아래 결과로 완료했다. raw 본문을 문서에 복제하지 않고 원본을 보존한다.


**최종 검증·로컬 main 선별 통합 완료:** `.utmp/android-development-logs-20261114/FinalVerificationReceipt.json`과 `.utmp/logger-main-integration-20261114/IntegrationReceipt.json`을 직접 읽었다. main runtime354소스 exact byte mismatch0, 최종 main compile 오류0/기존 경고24, tools-main/current 오류·경고0이다. safety36/삭제 흐름 모의9와 QA5hash 일치·blocker0을 확인했다. 최종 후속 수정은 UI/manifest 문구와 private-source kind/note였고 SDK 재검증은 했으나 native 최종 metadata 화면 재실행까지 했다고 주장하지 않는다. junction 실행 검사는 sandbox 제한으로 NOT RUN, 실제 기기 삭제·업로드는0이다.

로컬 main9dd0ce93에는 새 AndroidDevelopmentLogArchive+meta·LogcatCapture·LogArchiveZip·LogRules의5파일만 통합했다. 현재 최적화 브랜치는9c8b7fd4로 병합했고 unrelated staged/worktree를 보존했다. main runtime·설정·COMBAT-OPT/측정 도구/Task 이력은 이번 선별에 포함하지 않았다. push는 아직 실행하지 않았다. 파일 I/O·연결/OS race의 절대 atomic 보장은 하지 않고 PC 원본과 검증 근거를 보존한다.

### 36.5 앱 로그·Logcat·ZIP 단일 수집 후속 조사

사용자는 하나의 메뉴에서 앱 로그와 Logcat을 함께 회수해 ZIP까지 만들고, 자동 생성 raw/meta/저장 폴더가 소스 Git에 나타나지 않도록 하는 후속 기능과 삭제 시점의 쉬운 설명을 요청했다. 기존 Task 지속 승인으로 조사 계획을 기록한다. 구체 설계 공유와 진행 지시를 거쳐 아래 범위를 구현 중이며 아래 최종 검증과 실제 메뉴 실행을 완료했다.

기존 Log.md·Task 문서는 raw 산출물과 구분해 보존한다. Git 제외는 실제 자동 생성 경로와 meta를 좁게 확인한 뒤 정하며 `_Logs` 전체·전체 txt·기존 문서를 광범위하게 무시하지 않는다. 수집 원문/receipt/ZIP의 완결·hash·private 보관, 활성/미검증 보호·PC 검증 후 종료 세션 선택 삭제 계약을 유지한다. 자동 삭제나 원격 업로드 실행을 요청한 것으로 해석하지 않는다.

다음은 메뉴 입구·기기/세션 선택·앱 원본/Logcat의 범위·ZIP/manifest·실패/취소 상태·중복/부분 자료·Git 제외 경로·삭제 시점 안내를 설계 대조하는 것이다. 아직 LogRules의 지원 완료 계약을 바꾸지 않으며 추가 main 병합·push·기기 삭제·업로드는 실행하지 않는다. 새 Task/Testcase는 만들지 않는다.


구체 흐름은 기존 AndroidDevelopmentLogArchive 창의 「선택 앱 로그+Logcat 수집·ZIP」 버튼1개다. 동일 기기·선택 session 원문을 회수하고 Logcat은 private `.utmp/raw-logs/_Logs/Logcat/<YYYY-MM-DD>/<collection-id>/RuntimeLog_device.txt`에 저장한 뒤 기존 ZIP 생성을 호출한다. 선택0은 중단하고 실제 명령/부분 수집 실패 시 ZIP도 중단하되 이미 회수한 원문은 보존한다. 명령 성공0건은 오류로 취급하지 않는 후속 경계는 아래를 따른다. 삭제는 수집 버튼과 별도로 둔다.

구현은 기존 Editor3파일+.gitignore 범위이며 LogRules 설명은 검증 결과에 맞춰 문서 담당이 갱신한다. raw/meta와 자동 `_editor`·HH_mm_logcat 폴더/date parent meta만 좁게 Git 제외하며 Log.md 문서는 보존한다. 기존 tracked metadata는 메인이 추적만 해제하고 PC 파일은 보존할 계획으로 실제 삭제가 아니다. 당시 추가 main 통합·push 전 상태였으며 아래 통합 결과를 확인했고 구현·검증은 아래 범위로 완료했다.

Git 제외 적용 결과: `.utmp/log-git-exclusion-20261114/Receipt.json`을 직접 확인했다. 이미 추적되던 자동 raw/meta/전용 폴더 산출물295개를 index에서만 해제했고 PC295파일 SHA 전후 동일·실제 삭제0이다. Log.md 등 문서.md/meta20개와 다른 staged 항목은 보존했다. 기존 .utmp/_zip 제외를 유지하고 `_editor`·HH_mm_logcat의 자동 경로/meta, 날짜 parent meta, `_Logs` raw txt/meta만 추가 제외한다. .gitignore는 staging됐으나 commit/push는 아직0이며 단일 수집 코드 검증·main 추가 통합도 미완이다. LogRules 지원 계약은 기능 마감 뒤 갱신한다.

통합 버튼 첫 실제 시도는 앱 원문 회수 성공 후 Logcat의 실패/빈 결과 공통 분기에서 중단됐다. 별도 동일 ADB 필터가 exit0/lines0여서 현재 Unity/crash 등 수집 필터에 일치하는 Logcat이0건임을 확인했다. 전체 Android 버퍼가 비었다는 증거는 아니다. 이전 중단은 이력으로 보존하고, 명령 성공0건을 명시 파일로 기록해 앱 원문+ZIP을 허용하도록 최소 보완을 완료했다. 실제 명령 실패는 계속 중단한다. RootMetaReceipt 추가로 추적 해제 총296항목·PC 삭제0·commit/push0이며 문서 파일은 보존했다.


**단일 수집 최종 검증·실행 완료:** `.utmp/unified-log-collection-20261114/FinalVerificationReceipt.json`과 NativeCollectionZipReceipt를 직접 대조했다. 최종4hash QA 일치·blocker0, 실제2method flow19assert와 tools-main/current compile 오류·경고0을 확인했다. 영수증의 native pending은 생성 당시 한계이며 이후 메인이 실제 통합 버튼 실행을 완료했다.

최종 상태는 앱 로그·Logcat 수집 및 ZIP 검증 완료/기기 원본 유지/자동 업로드 없음이다. 현재 필터 일치 Logcat0건을 조회 성공으로 명시했고0건 표시 파일을 포함했다. ZIP android-logs_01은1,082,426바이트·SHA f97adc3a…1f2e9e,5entry다. 앞선 중단 때 회수한 앱 원문/receipt2개+최종 원문/receipt2개+0건 Logcat1개로, 오늘 미보관 후보 전체라는 기존 ZIP 날짜 범위의 정상 동작이다. 메인은 .NET 재열기로5entry 각 bytes/hash·전체 ZIP SHA·0건 marker를 확인했다.

별도 GitHub 페이지는 기존 도구가 열었으나 업로드·로그인·원격 존재 검증은 하지 않았다. 실제 기기 삭제·업로드·새 commit/push·추가 main 병합은0이다. 추적 해제296개는 PC 삭제0이며 문서/meta를 보존했다. 수집 성공을 자동 정리·자동 업로드 승인으로 확대하지 않고 원문과 실패 이력도 보존한다.

**통합 메뉴 설명·중복 정리 후속 계획:** 사용자는 창 설명과 중복 메뉴 정리를 요청했고 메인이 구조를 공유한 뒤 구현을 위임했다. root의 Collect Device Logs and Create ZIP은 유지하고 같은 창을 여는 Android Development Logs alias MenuItem은 주석 비활성화한다. 단독 Save Device Logs/Create Log Archive ZIP은 Logs/Advanced, Clear Device Buffer/Clean Old Editor Logs는 Logs/Maintenance로 옮긴다. 통합 창 제목·상단 도움말을 맞추고 단독 PC 복사는 가능하면 고급 접기 안에 배치한다. runtime/data/delete/ZIP 로직은 바꾸지 않는다.

사용 안내는 세션=앱 실행1회(여러 경기 포함), Logcat=현재 버퍼 snapshot(선택한 과거 앱 세션 당시의 Logcat 복원 아님), ZIP=오늘 KST 이후 미보관 후보도 포함(선택 세션만 한정 아님)을 분명히 한다. 메뉴 구현은 freeze했고 LogRules 현행 경로를 정합화했다. 아래 정적 검증과 후속 native 메뉴 확인을 완료했다. 실제 삭제·업로드·새 문서/TC·Git 쓰기는 없다.

메뉴 정리 freeze 결과: MenuCleanupProof.json에서 활성5경로·중복0과 기존16method 본문 exact를 직접 확인했다. root Collect Device Logs and Create ZIP/Advanced의 단독 Save·ZIP/Maintenance의 Clear·Clean으로 분리했고 alias는 주석 비활성, 단독 앱 복사는 고급 foldout에 유지했다. Editor SDK1구성 오류·경고0을 확인했으며 새 fixture·수집/삭제/빌드 실행은 없다. 이 시점은 새 commit/push/main 추가 통합 전 이력이며 아래 완료 결과를 따른다. 메인은 Unity 재컴파일 후 root의 Maintenance/Advanced/통합메뉴만 표시되고 중복 alias가 없음을 확인했다. Advanced의 Save/ZIP·Maintenance의 Clear/Clean submenu는 열어 읽기만 했고 실행하지 않았다. 통합 창을 실제 열어 Device Logs and ZIP 제목·새 도움말·접힌 PC 복사 고급 foldout·별도 삭제 버튼을 확인했다. 이번 실제 기기 수집·삭제·빌드·업로드 재실행은0이다. 과거 실행 이력의 메뉴 경로는 소급 변경하지 않았다.

**후속 로그 방향(이번 구현 아님):** 사용자는 로거 main 통합 뒤 최적화를 재개하도록 승인했다. 향후 서버 전송→보관→분석/원본 다운로드 흐름과 직접 기기·Editor 수집을 병행하는 방향을 검토한다. offline·업로드 실패 시 로컬 원본을 보존하고 업로드 뒤 원격 hash 검증·접근 권한·개인정보·보관/삭제 정책을 먼저 확정해야 한다. 현재 서버 전송/보관 기능을 구현·완료한 것이 아니며 LogRules 현행도 바꾸지 않는다. 실제 원격 업로드·삭제는 실행하지 않는다.

로거 추가 main 통합 영수증을 수신·확인했다. 최적화 다음 후보는 전문 담당이 조사 중이고 구체 계획 전에는 새 구현 대상을 확정하지 않는다. 새 Task/Testcase·Git 작업은 하지 않는다.


**통합 메뉴·Git 제외의 로컬 main 반영 완료:** `.utmp/logger-unified-main-integration-20261114/IntegrationReceipt.json`을 직접 읽었다. 로컬 main85e032fc(부모9dd0ce93), 최적화 브랜치0e18a649에 반영했다. 제품 변경은 .gitignore·Editor3파일·LogRules5파일이고 자동 산출물336개 추적을 제거했다. 실파일296개는 그대로 보존하고 기존 missing40개는 absence를 유지했다. 다른 staged/worktree를 보존했으며 runtime/gameplay/settings는 변경하지 않았다. Task는 main 제품 대상이 아니며 push는0이다.

수집·ZIP·메뉴 검증과 실제 삭제/업로드는 계속 구분한다. 미래 서버 전송/보관 계획은 이번 구현이 아니고 다음 최적화 대상도 아직 조사 결과 대기다. 새 코드·빌드·실기·원시 삭제를 추가하지 않는다.


## 37. 타일에서 사용하지 않는 side material 복제 제거 계획

다음 제품 후보는 타일의 색상·선택·geometry를 그대로 두고 쓰지 않는 material 복제를 줄이는 것이다. HexTileView.Initialize의 renderer.materials는 타일T개에서 top/side2slot을 복제하지만 색상 변경은 top1개에만 쓰는 구조를 확인해 선정했다. 이는 GPU pass 원인 확정이나 전체 후보 중 최대 절감 순위가 아니라 안전하게 제거할 작업 구조의 근거가 있는 후보라는 뜻이다. UI 정수 캐시·타워 재작업·범용 로거 확대는 하지 않고 GPU pass 원인 조사는 보류한다.

착수 전 전역 side material 쓰기, 재초기화·외부 material 교체와 소유권을 검토한다. 조건을 충족하면 HexTileView1파일에서 sharedMaterials의 기존 top 우선/fallback0 선택 규칙을 유지하고 top1개만 명시 복제한다. 다른 slot은 exact 참조를 유지하며 직접 소유한 인스턴스만 정리한다. 색상·선택/해제·NoBuild·geometry 기능과 기존 transform은 바꾸지 않는다. MPB·shader·품질·계측 추가도 없다.

기대 구조 감소는 미사용 side clone T개이며 실제 메모리 byte/ms/FPS는 미측정이다. 현재 누수를 확정한 것이 아니며 구현과 아래 native EditMode API 검증을 완료했으나 PlayMode 지연 Destroy는 미검증이고 기기 focused 육안 검증은 아래 후속 결과로 확인했다. 기존 Task 지속 승인 아래 전문 담당이 착수 조건 충족 후1파일 교정을 진행하며 새 Task/Testcase·빌드·실기 요청은 없다.


**타일 교정 검증 결과(2026-11-14):** `.utmp/tile-material-ownership-20261114/native-0be64ef3757f4f9e865ccfe1ea7e9b90.json`을 직접 읽었다. 메인은20:36:35 임시 메뉴1회에서21checks PASS를 확인했고 제품 SHA9A137A…DA6B60/payload DLL SHA915B82…7534AA가 결부됐다. 기존 materials2clone 대 top1clone·side 공유 참조, 팀/선택색·기하/hatch·재초기화·외부 교체·명시 OnDestroy 소유 정리·shared asset 생존과 prefab/material3asset bytes 동일을 검증했다.

CompileResults는 Editor/Release/Tools3구성 오류0·기존 경고24/31/0·변경 파일 경고0이다. StaticPreservationProof는 event subscription/팀·선택색 본문 exact와 runtime358개 누락0을 확인했다. 새 영구 진단을 추가한 것이 아니며 FinalVerificationReceipt로 임시 Editor driver 정리·최종 QA까지 완료했다.

검증 범위는 native EditMode material API와 제품 OnDestroy 본문 직접 호출이다. PlayMode 지연 Destroy·기기 외형·실제 메모리 byte/ms/FPS는 미검증이며 누수 확정이 아니다. 해제 때 sharedMaterials 배열 readback·참조 복구 setter·명시 Destroy1회 비용도 새로 있어 순수 이득을 실측한 것은 아니다. 새 빌드·실기 교전 테스트는 없고 이번1파일 교정은 최적화 브랜치 미커밋/main 미포함·push0이다. 로거 main85e032fc 완료와 분리한다.

타일 최종 마감: FinalVerificationReceipt.json을 직접 읽어 QA native21/livehash 일치·blocker0과 신규 임시 driver.cs/meta2개+중간 SDK DLL/PDB6개 총8파일5,468,835바이트의 경로/hash 확인 삭제를 검증했다. private payload/source/Before/driver 사본/rsp/results/native 영수증은 보존했다. main85e032fc·최적화 브랜치0e18a649의 로거 통합과 달리 타일1파일은 현재 최적화 브랜치 미커밋/main 미포함·push0이다. PlayMode 지연 Destroy·실기 외형·memory byte/ms/FPS 미검증 한계는 그대로 유지한다.


### 37.1 기기 focused 기능 확인과 수집 범위 정정

사용자는 새 테스트 뒤 「표시 정상, 재진입도 확인」을 보고했다. 기존 native21검증과 아래 기기 실행·두 싱글 경기/복귀 근거를 합쳐 이번 타일 표시·재진입 focused 기능 검증은 PASS로 수용한다. 실제 memory byte/ms/FPS 절감·전체 게임/모든 수명 경로 수용은 아니다.

최신22:24 android-logs_02 ZIP548,135바이트·SHA7596af24…0552f7의3entry/전체 hash·PC 원본 일치는 확인했으나 선택 앱 원문은11/13의 이전 세션이었다. 새 Logcat이 새 세션 경로를 보여 메인이 읽기 전용 ADB 회수로 새 RuntimeLog를 확보했고 재시험은 요구하지 않았다. 새 원문은 `.utmp/tile-material-ownership-20261114/device-test-20261114/recovered/RuntimeLog.txt`2,645,705바이트·SHA48809887…9e9d4a8이며 remote 전후/local이 일치한다. 이 추가 회수 원문이 앞 ZIP에 들어 있다고 쓰지 않는다.

ReadIntegrity/AnalysisReceipt/ArchiveIntegrity를 직접 읽었다. seq1~2,252는 완결 record 수(줄 수 아님)이며 UTF-8 byte/END·tail0, 정상 close footer는 없다.21:51:27.909~22:23:25.252의 Development IL2CPP 새 실행에서 싱글2round GameEnd를 확인했고 관련 WARN/ERROR0이다. 이동은 round당 fallback4·비교4/mismatch0/fault0·구조924방문→24조회, bundle0은 싱글에서 NotExercised다. 이 로그만으로 타일 외형을 추론하지 않으며 육안 근거는 사용자 보고다.

메인은 installed/local APK SHA491a9634…49d3d3f 일치와 boot/runtime build GUID 일치를 확인했고 APK metadata에 새 material 소유권4메서드가 존재한다. 다만 prebuild GUID0·소스 목록에 HexTileView가 없어 exact 소스 SHA9A137…DA6B60까지 소급 확정하지 않는다. 이는 구현 존재 귀속과 전체 source digest의 차이다.

회수·사용자 육안·focused 기능 PASS와 실제 메모리/시간 효과·PlayMode 지연 Destroy·footer 완결성은 별도다. 이번 추가 기기 삭제·업로드·코드 수정·빌드·재시험은 없다. 새 원본·기존 ZIP·분석 영수증을 보존한다.

보관 보완: 메인이 통합 창 세션 목록을 갱신해 이번11/14 실행을 선택한 뒤22:41 android-logs_03 ZIP을 생성했다. CorrectedArchiveIntegrity.json을 직접 확인한 결과 ZIP169,979바이트·SHA24154dc6…cb42331, 이번 원문2,645,705바이트(SHA48809887…9e9d4a8)+receipt667바이트+Logcat40,265바이트의3entry/전체 및 각 hash·길이가 정상이다. 따라서 이번 원문은 현재03 ZIP에 정확히 보관됐다. 이전02 ZIP에 이번 원문이 없었다는 기록은02 범위의 이력으로 유지한다. AnalysisReceipt SHA30CE9061…BCDBCE7도 대조했다. 새 테스트·앱 조작·빌드·코드 변경·기기 삭제·업로드는0이며 focused PASS/효과 한계는 유지한다. 최종 독립 QA 읽기에서 record2,252/싱글2경기·관련 WARN/ERROR0/03 ZIP3entry·hash와 사용자 표시·재진입 수용 주장 blocker0을 확인했다. exact source SHA·기기 material 수·PlayMode 지연 Destroy·멀티·byte/ms/FPS는 미검증이며 footer 부재의 삭제 불가 보호도 유지한다.

### 37.2 동일 타일 입력의 Editor 자동 비교 측정 계획

2026-11-15 KST 사용자는 동일 타일 효과의 자동 비교 측정 재개를 승인했다. 이미 수용한 표시·재진입 focused 기능 PASS는 유지하고 추가 사용자 테스트를 요구하지 않는다. 같은 실제 prefab·동일 타일 수(대표231)에서 before materials API의2clone과 current top-only1clone을 비교한다. 실제 Unity EditMode native Material 수와 Profiler.GetRuntimeMemorySizeLong 합계,3~5회 반복 정리 뒤 잔존을 기록한다.

전문 담당이 작은 임시 driver/private payload를 준비하고 메인이 메뉴 실행·저장·분석을 담당한다. product SHA9A137…DA6B60은 바꾸지 않으며 새2 APK 빌드·상시 계측은 없다. 아래 최종 CSV 기반 비교 결과를 확보했고 정리·독립 QA 마감까지 완료했다. Editor 측정은 Android 실기 메모리·FPS가 아니며 시간값이 있어도 보조 관측으로만 해석하고 전체 원인·효과를 확정하지 않는다. 새 Task/Testcase·메모리 범위 확대는 없다.


**통제된 Editor 비교 결과:** 메인은2026-11-15 KST00:21:37 최종 메뉴 실행에서 PASS를 확인했다. `.utmp/tile-material-comparison-20261115/native-5245c7706f0d47269007d6914a2530ba.json`, 지정 rows CSV와 ComparisonAnalysis.json을 직접 대조했다. 같은 실제 prefab/231타일·각1회 warmup 뒤 ABBAABBA8배치(각4회) 모두 동일했다. product SHA9A137…DA6B60/asset hash는 불변이다.

| Editor 관측 | Before | Current | 차이 |
|---|---:|---:|---:|
| unique owned Material 수 |462 |231 |231개·50% 감소 |
| Profiler.GetRuntimeMemorySizeLong의 material별 합 |1,326,864B |723,492B |603,372B·589.230469KiB·약45.473538% 감소 |
| top만의 합 |432,432B |723,492B |현재 top 합은 증가 |
| side만의 합 |894,432B |0B |side 복제 제거 |

Material 개수50%와 메모리45.47%는 다른 지표다. side 삭제894,432B를 순감소로 쓰지 않고 top 증가를 반영한 net603,372B를 사용한다. 이 합은 유일 인스턴스별 Editor API 값이며 Android/프로세스 전체 RAM·GPU/공유 shader/texture 메모리가 아니다.

Before는 객체 파괴 후 추적 Material462개가 남지만 fixture가 명시 정리한 뒤0, Current는 제품 OnDestroy 본문을 명시 호출한 경로에서 객체 파괴 뒤0이다. 매회 global context358→358 복귀를 관측했다. 이는 통제 EditMode 정리 검증이며 실제 맵→로비 수명·PlayMode 지연 Destroy·Android 누수/메모리·FPS/ms 증거는 아니다.

첫00:14:40 PASS는 rows JSON 누락으로 세부 효과 근거에 쓰지 않았고 두 번째00:17:48은 측정용 NullReference로 실패했다. private assembly/baseline의 고유명 SaveV2 교정 뒤 최종 CSV 저장에 성공했지만 캐시가 원인이라고 확정하지 않는다. 원문 실패 이력을 보존했고 제품은 수정하지 않았다. 최종 영수증과 임시 driver 정리·독립 숫자 QA를 확인했다. 사용자 육안 정상·재진입 focused PASS를 유지하고 새 사용자 테스트·빌드·Git·업로드는 없다.

최종 마감: FinalVerificationReceipt/CleanupReceipt를 직접 확인했다. QA가8행/CSV hash·net603,372B·45.473538%를 독립 검산해 일치·blocker0이다. 신규 Assets driver.cs/meta와 불필요 Driver DLL/PDB4파일34,516바이트만 경로/hash 확인 후 삭제했고 private payload/source/CSV·3회 native 이력/log/proofs는 보존했다. 제품9A137…DA6B60은 불변이며 로거 main85e032fc와 달리 타일은 최적화 브랜치 미커밋/main 미포함이다. 제품·기기·빌드·Git 추가 변경0, 실제 맵/로비·PlayMode 지연 Destroy·Android수명/메모리·FPS 미검증 한계는 유지한다.

### 37.3 로그 창 최소 UI·제품 main 반영 및 push 결과

사용자는 로그 창 PC 복사 접기를 없애고 삭제 글자를 빨갛게 표시하는 최소 UI 수정, 완료 제품 최적화의 main 병합·push와 다음 후보 선정을 명시 승인했다. 기존 메뉴 구조와 수집/검증/ZIP/삭제 로직은 유지하며 전문 담당의 UI 구현과 아래 native 확인·제품 반영을 완료했다. LogRules는 PC 복사 항상 표시·삭제 적색 글자의 현행 안내만 최소 수정했다. native UI·main 통합·push 완료 범위는 아래 영수증으로 확인했다.

메인은 origin/main fetch 후 아래 제품 선별 통합·push를 완료했다. 대상은 타일 top material 소유 교정·이동 fallback6이웃·Bundle owned 배열 교정이며 COMBAT-OPT/측정 설정·임시 도구·raw·Task는 최적화 브랜치에 유지한다. PROJECT_STATUS/WORK_HISTORY는 다른 이력 혼입을 피하려고 이번에 수정하지 않고 실제 제품 반영 영수증은 이 Task에 기록한다. merge/push는 메인만 담당한다.

다음 최적화 후보는 전문 담당 조사 중이며 아직 구현을 확정하지 않았다. 실제 제거 가능 작업·빈도·안전성·검증 부담을 확인하고 사용자 기존 우선순위에 따라 비교한다. 현재 UI/제품 통합은 아래 범위로 완료됐으며 새 빌드·실기·메모리 문서 확대·삭제/업로드를 추가하지 않는다.

다음 조사 방향 확정: 렌더링에서 실제 화면에 필요 없는 중복 pass/연산이 있는지 먼저 점검한다. GPU 평균27.456/p9535.562ms·2,699표본 중227개>33.333ms는 큰 관측 비용의 근거이지 제거 가능량이 아니다. 조사 순서는 렌더 구조→전투 tick(900개 wall평균1.850/최대6.198ms)→작은 UI로 두되 구현 절감 순위로 단정하지 않는다. 쿨다운 문자열 cache를 작은 확실 작업으로 우선 추천했던 안은 실제 절감 최우선 근거가 부족해 철회했다.

품질 낮춤·pass 비활성화·새 계측 도구를 먼저 추가하지 않는다. 실제 중복/불필요함이 입증된 항목만 구현 계획을 세우며 현재 새 구현은 아니다. 제품 선별은 GP/QA PASS·대표4컴파일 오류0/변경 경고0·수집삭제 Safe~EOF exact를 확인했고 후속 native UI 확인과 Git 반영 영수증을 아래에서 확정했다. LogRules는 추가 수정 없이 freeze한다.


**제품 main·push 최종 완료(2026-11-15):** `.utmp/product-integration-20261115/git/IntegrationReceipt.json`을 직접 읽었다. 로컬 main a42ba8d9, 현재 최적화 브랜치 f4b2b942에 반영했고 메인은 push origin main 성공(14247c6f..a42ba8d9)과 remote refs/heads/main exact 일치를 확인했다. 포함한5파일은 로그 창 최소 UI·LogRules와 타일/이동/Bundle3제품 교정이다. 진단·측정 설정·Task·raw는 main에서 제외했고 unrelated staged·dirty/untracked 원문 hash는 보존했다.

실제 Unity 최신 화면에서 PC 복사 항상 표시/foldout 없음·삭제 적색·다른 버튼 일반색을 확인했다. 이번 삭제 클릭·수집 실행은0이며 QA 차단0이다. LogRules는 main 포함 freeze 상태로 추가 변경하지 않는다. 다음 렌더 조사는 방향 선정만 완료했고 구현은 미착수다. push/제품 반영을 새 실기·전체 FPS 효과 검증으로 해석하지 않는다.


## 38. 렌더 중복의 읽기 조사 결과

사용자의 「바로 진행」 승인으로 현재 화면을 유지하며 제거할 중복이 있는지 조사했다. Android Mobile0은 features가 비어 있는 MobileRenderer를 사용하고 depth/opaque texture도0이다. Game은 Main Base카메라1개·추가 stack 없음, 과거 기기 기록도 cameraCount1이었다. 전문 담당이 main/작업본의 MobileRP·GlobalSettings·Graphics를 비교해 내용 차이0(개행만 차이)을 확인했다.

Game GlobalVolume은 실제 SampleSceneProfile의 Bloom0.25/Vignette0.2를 사용한다. 후처리를 끄면 표시가 달라지므로 동일 화면을 보존하는 중복 제거로 취급하지 않는다. Lobby preview RenderTexture는 실제 사용처가 있고 캐러셀3캐릭터 동시 표현도 의도된 기능이라 불필요하다고 끄지 않는다.

현 단계에서는 같은 화면을 유지하면서 제거할 명백한 중복을 찾지 못했다. 절감량0을 증명한 것이 아니라 제거 가능량 미산정·수정안 없음으로 읽기 조사를 닫는다. 기존 GPU 약27ms를 특정 pass 비용으로 귀속할 수 없다. 후속 실제 Editor 전투 화면의 Frame Debugger 검수를 아래38.1에서 완료했다. Android pass 시간 검증은 아니다. 새 계측 도구가 필요한 단계는 아니며 게임 화면 없이 비교도 하지 않은 상태에서 효과를 바꾸지 않는다. 코드·설정·빌드·Git 변경0이다.


### 38.1 실제 Editor 전투 프레임 검수와 hatch 후보

메인은 native UI로 Login Play→자동 로그인/Tap to Start→Lobby Single Normal→TrainingCamp 건설/LittleKnight 생산을 진행했다. 앞3round 결과 뒤4번째 진행 경기에서 pause·Frame Debugger를 확보했다. 대표 최대 밀집·Android GPU ms·통제 전후 비교는 아니다. 추가 사용자 플레이 없이 직접 검토했고 최종 Play OFF/Login·Frame Debugger Disabled와 원래 GameView Mobile2(1080×2340)/0.15 복원을 확인했다.

최초18:9 Portrait preview(175×350/RT140×280)는89events=MeshSkinning15+RenderSingleCamera74였고 Transparent35 중 NoBuildHatch28연속(Event28~55), 다음56은 TMP Text였다. 이는 별도 preview 구성이다. 원래 Mobile2로 복구한 같은 paused game의 최종 기준은80events=Skinning15+Render65, Opaque5/Transparent27/Bloom16/MainLightShadow2/ColorLUT1/UberPost1/ScreenUI6/GUI4와 clear였다. NoBuildHatch20연속(Event27~46)을 첫/끝 선택으로 직접 확인했고 다음 Text와 구분했다. hatch RT864×1872/B10G11R11_UFloatPack32·최종UI1080×2340이다. **28→20은 aspect/해상도 차이이며 최적화 효과가 아니다.**

hatch 메시의 첫 항목은12vertices/18indices·draw1, shader는 SkillAimOverlay였다. Frame Debugger의 SRP 비호환·DynamicBatching off/context disabled to avoid z-fighting 표시는 관측 설명이며 실제 성능 원인으로 단정하지 않는다. HexGridRenderer의 CreateNoBuildHatch는 공유 mesh/material·그림자OFF이고12/18 공유 메시를 사용한다. 중복 생성이라기보다 타일별 submission 구조다.

**현행 상태:** 사용자의 반복 호출 감소 수정·테스트·비용 보고 승인에 따라 HexGridRenderer의 hatch-only single combined mesh/Renderer 제품 구현과 아래 SDK/native 검증을 완료했고 임시 우회 제거·최종 제품 compile은 완료했고 cleanup 영수증까지 확인했다. 제품 수정 전 단순 probe나 StaticBatch.Combine 진행 상태가 아니다. 실제 draw 감소와 기존 표시·선택·정리 기능 보존이 수용 gate이며 확인한 draw 감소와 제한된 native 결과는 아래와 같으며, 이후 사용자 전체 이상 없음 보고로 안내한 표시·동작 범위는 수용했다.

현재 전체 hatch H를 결합하고 보이는20/67 표본 수를 hardcode하지 않는다. NoBuild 표시는 각 필요 타일의 정상 기능이며 타일 collider/선택·맵 데이터·TileKind와 공유 shader의 조준/타워/힐은 유지한다. GameSystemRules_RandomMap의 이동/선택 허용·건설 금지 계약을 바꾸지 않는다. 작은 임시 Editor 메뉴/private payload로 같은 맵/control을 before/after 비교하고 Play OFF로 원복한다. 실패는 원래 renderer를 복구하고 수용을 보류한다. asset·품질·새 빌드·사용자 추가 플레이·원격 push는 변경/실행하지 않는다.

| 검증 항목 | 증거 | 한계 |
|---|---|---|
| 실제 draw 감소 | 임시 native driver/private 영수증·같은 frame/control의 Frame Debugger | 보이는 수와 전체 H 구분, 감소가 없으면 수용 보류 |
| 표시/정렬 유지 | 같은 맵 geometry·픽셀/정렬 비교의 실제 관측 | 확인된 범위만 수용, 전체 장치 시각 보장 아님 |
| 비용 tradeoff·수명 | geometry 메모리 증감·생성/정리 CPU(실측 가능 시)·종결 상태 | Android GPU27ms·FPS 효과로 환산 금지 |


**1차 static batching 이력(새 안의 검증 아님):** Play OFF 확인 후 기록했다. `.utmp/no-build-hatch-batching-20261115/SameMapFrameDebuggerObservation.json`과 native-9a30…json/CSV를 직접 읽었다. static batch native268검사/3cycle 소유권 검증은 통과했지만 같은 paused 싱글 맵의 실제 Frame Debugger는 before67개 hatch event(draw각1)→after Static Batch event1이면서 상세 Draw Calls67을 유지했다. total105→39는 event 수 감소일 뿐 실제 draw 감소는0이다. H전체87/visible67·1080×2340/RT864×1872 EditorDX11 범위이며 Android GPU 효과가 아니다. 이 안은 목적 달성 완료로 수용하지 않는다.

메인은 새 대안 Mesh.CombineMeshes(mergeSubMeshes=true/useMatrices=true)를 승인했다. grid-local NoBuildHatchCombined GO/Renderer1개를 만들고 각 hatch의 world 행렬·UV/color/indices를 보존한다. shader/material·layer/shadow/sorting 원설정과 tile collider/selection/TileKind는 바꾸지 않는다. 성공 후 원 hatch renderer의 enabled만false로 하고 예외면 원 renderer를 복구한다. 기존 생성 경로는 남기며 Clear/OnDestroy에서 소유 combined mesh/GO만 정리한다.

tradeoff는 전체H geometry 제출·단일 culling bounds·투명 sort 거리1개다. 실제 overlay 겹침/camera·동일 geometry/표시와 수명 검증이 필요하다. 새 안의 fixture/native 영수증을 새로 확보하고 기존268검사를 새 안 검증으로 재사용하지 않는다. 아래 새 native 결과를 확인했으며 최종 정리와 실기GPUms/FPS는 별도다.


**single combined 실제 결과:** Play OFF/Login 확인 후 SingleSameMapFrameDebuggerObservation.json과 single-native-e585…fixture/12c067…live 영수증·CSV를 직접 대조했다. 같은 Editor DX11 paused 싱글 맵 H69/visible53에서 before Event11~63의53개 DrawMesh(each draw1)→after Event11 NoBuildHatchCombined의 상세 Draw Calls1을 확인했다. 실제52draw·약98.113208% 감소다. 전체 event89→37은 별도 지표이며 기존 static의 event만 줄었던 결과와 다르다.

tradeoff는 submitted geometry636→828vertices(+192/약30.19%)이며 source mesh2,784B에 combined43,788B를 더해46,572B(+42.76KiB)를 보관한다. 원 오브젝트를 삭제한 효과가 아니다. 단일bounds의 전체H 제출과 메모리 증가를 draw 이득과 함께 보고한다.

새 fixture20hatch/3cycle의 world vertex·UV/color/triangle/material/tile collider·owned EditMode cleanup3508검사와 source mesh2,172+combined13,800=15,972B/정리 후2,172B 복귀를 확인했다. live3734검사와 CameraRT1080×2340 PNG diff0/max0은 그 정지 화면의 동일 픽셀 근거다. 실제 Frame Debugger target864×1872와는 다른 측정 축이므로 해상도를 혼동하지 않는다. SDK Editor/Release 오류0(기존 경고24/31)·payload 오류/경고0이다.

메인의 NoBuild 선택·줌/pan·유닛 통과/팀색 정상 관측은 수동 육안 이력이다. 이후 사용자는 안내한 타일 선택/카메라, 빗금 위 조준 원·타워 범위·힐 효과 겹침, 종료→로비→재진입의 전체 테스트에 “특별히 문제없는거 확인했어”라고 보고했다. **사용자 실기 표시·동작 수용 PASS**로 기록한다. 항목별 로그·개별 시나리오 관측값은 없으며 기기·빌드·역할이나 각 VFX 발동을 임의 확정하지 않는다. 다른 역할/카메라 전범위, Play 지연 mesh 참조 수명·누수 정량 검증, Android GPU시간/FPS 개선량은 여전히 미확인이다.

임시 Editor 우회2block 제거·driver 삭제·LogArchiveZip exact 원복·최종 제품 compile을 완료했다. cleanup 영수증을 확인했으며 새 Task/Testcase·메모리·Git 작업은 하지 않는다.

최종 제품 마감 근거: FinalCoreProof.json에서 native 검증 B8EC0331…A29FA6 대 최종69106FA6…021AE는 Editor 우회2block 제거 외 core byte exact임을 확인했다. FinalProductCompileResults의 Editor/AndroidRelease 오류0·변경 파일 경고0(기존24/31)이다. 메인은 LogArchiveZip 원본 SHA 일치/diff0과 임시 driver.cs/meta 부재를 확인했다. 따라서 제품 구현·Editor 국소 draw 효과 확인은 완료이며 앞의 geometry/메모리 tradeoff와 정량 수명·기기 성능 한계는 유지하며, 안내한 표시·동작은 사용자 전체 판정으로 수용했다.

정리 최종 확인: FinalVerificationReceipt/TemporarySourceCleanupReceipt/IntermediateCleanupReceipt를 직접 읽었다. 신규 임시 driver.cs/meta2개1,364바이트와 중간 DLL/PDB8개4,346,928바이트만 경로/hash 확인 후 삭제했다. LogArchiveZip87,640바이트/SHA A45653…2D34CF 원본 exact 복구를 확인했다. 최종 SDK/payload/source/baseline/rsp/log와 모든 native JSON/CSV/PNG는 보존했다. 기존 효과·tradeoff·미검증 경계는 유지하며 다른 문서·Git 작업은 없다.

최종 Unity 반영: 메인은 Runtime DLL16:50:27/Editor DLL16:50:37 재컴파일 이후 Login·Play OFF와 정상 편집 응답, 임시 Validation 하위 메뉴 부재를 직접 확인했다. Frame Debugger는 닫고 Mobile2 원래 preset을 유지했다. Console의 기존 MCP npm 실패2건은 제품 컴파일 오류와 구분하며 오류0으로 보고하지 않는다. 새 Android 실행·성능 검증은 아니다.

제품 반영·다음 제안(2026-11-15): LocalMergeReceipt/ProductCommitReceipt를 직접 확인했다. 메인은 HexGridRenderer.cs1파일(+106줄)만 main c1db2c41(부모a42ba8d9)에 반영하고 최적화 브랜치를328c400c로 통합했다. 제품 SHA69106FA6…021AE는 검증본과 같고 unrelated staging binary patch도 전후 동일하다. 측정 도구/define/settings/raw/Task는 main에서 제외했다. 메인이 push 성공 출력(main a42→c1db)과 ls-remote의 refs/heads/main c1db2c4115b62fe16abf5d1d590d189a26095b00 일치를 확인했다. PushVerificationReceipt를 직접 대조했으며 local main/origin main 동일·최적화 HEAD328c400c, 해당 제품 파일 main/HEAD 차이0과 기존 staging patch SHA775AB389…E5C0A7 동일을 확인했다. 작업폴더 checkout/reset/stash 없이 alternate index를 사용했고 기존 dirty는 보존했다. 이것은 새 실기·성능 검증이 아니다.

다음 제안은 NCC의 TryFindTarget→null 뒤 HasEnemyInRange 재호출에서 동일한 false 전수 검색을 재사용할 수 있는지 검토하는 것이다. 공격 가능·cooldown0·첫 실제 전체 검색에서 적 없음 조건과 null 사유/검색 실행 여부를 구분해야 한다. 권위·cooldown·거리·NaN/fallback·결과 동등성을 보존한 국소2회→1회 제거 가능성을 먼저 확인한다. 이전 early-exit 완료 기능의 재작업이 아니며 빈도/ms/FPS 절감은 미측정이다. 최대 절감 후보 확정이나 구현 승인이 아니라 제안만이며 새 코드·테스트는 없다. Bloom16events는 불필요 pass 근거가 없어 보류하고 최대5slot UI 정수 문자열은 작은 규모로 후순위에 둔다.

## 39. 공격 대상이 없을 때 반복되는 검색 조사

이미 적이 없다고 확인한 직후 같은 범위를 다시 훑는 작업을 줄일 수 있는지 조사한다. 기존 공격·이동 판단을 바꾸지 않고, 검색을 하지 않아 나온 null과 실제 검색에서 적이 없어 나온 null을 구분하는 것이 목적이다. 사용자는 조사·계획 보고 뒤 교정·자동 비교·컴파일·빌드 시도까지 승인했다. 현재 전문 담당이 제품2파일 구현과 private 실제 소스 비교/SDK 검증을 진행 중이며 결과는 아직 확정하지 않았다. 빌드는 메인의 File→Build And Run 시도만 진행할 예정이고 완료 감시는 하지 않는다.

### 39.1 확인한 경로와 효과의 경계

UnitCombatUseCase.TryFindTarget(969~984)는 null/사망→멀티 Client 권한→cooldown 순서로 조기 반환하고, 통과하면 FindFirstEnemyTarget을 실행한다. HasEnemyInRange(1350~1353)는 같은 private 검색의 존재 여부를 소비한다. NetworkCombatController907~908은 CanAttack이 허용한 TryFindTarget 결과가 null이면 HasEnemyInRange를 다시 호출한다. 따라서 공격 가능·기존 cooldown >0 guard 통과·첫 검색에서 해당 공격자의 사거리 내 유효 적 없음 조건에서는 동일 전수 검색을 두 번 실행하는 경로가 있다. 검색을 건너뛴 null을 적 없음으로 재사용해서는 안 된다.

World nearest-null과 존재 false는 같은 검색/Hex fallback 조건을 사용한다(1738~1859). 두 호출 사이 await/event/writer는 없다. 제품 UnitWorldPositionProvider35~44는 Factory lookup·Transform 읽기이며 HexMetricsCoordinateMapper39~45는 순수 좌표 계산이다. 상태를 바꾸는 임의 custom provider/mapper까지 동등하다고 보장하지 않는다. CanAttack은 ZoneBlocked 진단 카운터 부작용이 있어 현재 위치에서 정확히 한 번만 평가해야 한다.

영향 대상은 멀티 Host의 NCC 경로다. Single TryAttack과 Client 보간 호출에는 기존 호환 API를 유지하며 이 경로의 절감으로 설명하지 않는다. Game.unity의 interval0.1초와 코드 default0.05초를 구분하고 실제 호출 빈도는 미측정으로 둔다.

기대 효과는 해당 조건당 전수 검색2회→1회로 후보·거리·provider 읽기 한 번을 생략하는 것이다. 이 중복 부분만50%이며 전체 게임/검색/FPS50%라는 뜻이 아니다. 해당 경로가 발생하지 않으면 효과는0이다. 타겟 발견·cooldown/stun 등 모든 상황에서 줄어드는 것은 아니며 빈도·ms/FPS·할당량과 최대 절감 순위는 미확정이다. 새 장기 캐시·목록·전역 상태를 추가하는 안이 아니다.

조사 마감 보완: flag는 cooldown<=0 재판정이 아니라 기존 >0 guard를 통과한 실제 검색 결과를 전달하므로 NaN cooldown도 보존한다. provider null/공격자 zero의 Hex fullscan, 타겟 zero의 mapper fallback·NaN/Infinity/floatMax·strict<·unit 우선/동률·건물 여유거리는 원문 그대로다. 조건 발생 K회라면 K×(U+B) 후보 항목 열거를 생략할 수 있다(U/B는 순회 collection 항목 수). provider/거리 읽기는 적·생존 필터에 따른 부분집합이어서 같은 수로 단정하지 않는다. K·발화율·ms는 미측정이다. Client의 존재조회·HasEnemyInRangeAt/DetectRange는 변경 대상이 아니다.

최종 소스·SDK 결과: private ncc-target-query-reuse의 FinalVerificationReceipt/SourcePreservationProof를 직접 대조했다. 실제 추출 before/new 메서드와 NCC 소비자36case/232assert PASS, 빈 World provider/후보 열거6→3·Hex 열거6→3으로 해당 fixture의 검색2→1을 확인했다. found/gated/initial throw는 동일하다. 기존 CanAttack 본문·1회 평가와 거리/nearest/At/Detect·나머지 소스 보존을 확인했다. 실제 Unity 수학을 사용했으나 CanAttack/NetworkContext는 제어 seam인 CLR 검증이며 native gameplay·NGO/RPC 실기 실행이 아니다. Editor/AndroidRelease SDK 오류0(기존 경고24/31), 변경 메서드 새 경고0이며 Release 변경 파일 내 기존 CS0168경고2개는 이번 블록 밖이다. 현재 source358개 누락0이다.

새 상시 진단/로그는0이며 실기 K·ms·FPS·GCbytes는 미측정이다. 기존 로그에는 생략 횟수 counter가 없고 기능 관측 보조에 한정한다. 메인은 Android Development ON·Samsung SM-N971N 선택을 확인했다. Unity Refresh·QA 검토와 빌드 시도는 아래 범위로 완료했으며 성공·설치·실기 PASS는 아직 기록하지 않는다.

최종 native·빌드 시도 상태: 메인이 Unity Refresh·도메인 재로드 뒤 정상 편집 응답과 제품 C# 컴파일 오류0을 확인했다. Console의2error는 기존 MCP npm install/run build 오류로 제품 컴파일과 구분한다. QA는36case/232assert를 독립 재현했고 blocker0이다. 메인이 File→Build And Run을 클릭해 Detect Java Development Kit(JDK)/Checking JDK 진행창을 관찰한 뒤 사용자 요청에 따라 UI/로그를 통한 빌드 완료 감시를 중단했다. 시도 완료이며 빌드 성공·설치·실행은 미확인이다. 새 실기 검증·K/ms/FPS 측정이나 다른 파일/Git 변경으로 확대하지 않는다.

### 39.2 정상 재시도 결과 분석과 ZIP 완료창 분리(2026-11-16)

사용자는 정상 재시도·테스트 저장 뒤 분석과 ZIP 완료창 동작 수정을 요청했다. 로그 QA 분석과 완료창 구현·SDK 검증은 아래 범위로 완료했다. 실기 전체 기능 PASS나 실제 K/ms/FPS는 확정하지 않는다.

완료창은 확인=닫기만, 웹페이지 열기=사용자가 명시 선택할 때 기존 URL과 저장 폴더를 함께 여는 동작으로 분리한다. 확인/닫기/Esc에는 자동 열기가 없고 결과 경로 표시는 유지한다. 수집/검증/ZIP 내용·삭제·업로드 정책은 바꾸지 않으며 자동 업로드를 추가하지 않는다. LogRules의 현행 자동 열기 설명을 코드 freeze에 맞춰 갱신했고 TechnicalDesignDocument에는 해당 설명이 없었다. 새 Task/Testcase·메모리·Git 변경이나 원시 로그 본문 복제는 없다. 구현·분석 결과는 아래 범위로 마감한다.

최종 결과: 메인은 target-query-reuse_01 ZIP4,927,901바이트/4entry의 전체 ZIP SHA와4개 source bytes/SHA 일치를 검증했다. 정상 재시도의 Editor Client는03:55:47.522 시작 수신→03:58:37.432 결과→03:59:15.366 로비 복귀가 관측됐고 Host/Client 교전·client accepted3903/rejected0을 확인했다. 기존 Bundle1596/sample16 mismatch0·Movement30/sample16 mismatch0/Fault0은 이전 최적화 증거이며 이번 query 절감 증거가 아니다. 실기 K/ms/FPS·cooldown 개별 조건·적 소멸 뒤 이동 재개는 직접 검증하지 못했다.

Observer dropped203/10과 FAIL은 그대로 기록한다. 소스의 라인/UTF8 예산에 따른 관측 로그 생략 counter이며 네트워크 패킷 손실이 아니다. dropped>0만으로도 FAIL 조건을 충족하지만 유일 원인·다른 오류 배제나 이번 query 교정 인과는 입증하지 않았다. 스폰 순서 경고286건은 별도 관측이며 객체 미출현 회귀로 확정하지 않는다. 이전03:40:41의404는 잘못된 코드 참가 실패다. 정정 뒤03:40:49 참가·03:40:52 맵 검증 성공 이후 Host Ready1/2 무한 로딩은 별도 사건으로,404를 그 원인으로 연결하지 않으며 정상 재시도가 근본 원인 해결을 뜻하지 않는다.

ZIP 완료창은 FinalVerificationReceipt를 직접 확인했다. 확인/닫기/Esc는 닫기만, 웹페이지 열기(choice2)만 기존 URL OpenURL이었던 최초 분리를 검증했다. 이후2026-11-16 사용자 요청으로 choice2에만 저장 폴더 열기도 함께 연결하는 후속 수정을 완료했으며 확인/닫기/Esc는 열기0을 유지한다. ZIP/hash 수집 helper 본문은 exact이며 대표 Editor compile 오류0/경고0이다. 실제 변경 후 완료창 UI 선택은 미실행이므로 native UI 수용 PASS는 아니다. 원문 보존·자동 업로드/삭제 없음, 추가 코드·빌드·테스트·Git 작업은 없다.

후속 요청(2026-11-16): 사용자는 웹페이지 버튼 선택 시 저장 폴더도 함께 열도록 요청했다. 전문 담당이 그 분기만 수정했고 최종 source freeze·EditorTools 컴파일 오류0/경고0을 확인했다. choice2만 RevealInFinder(names.ZipAbsolute)+기존 OpenURL을 실행하며0/1/Esc는 열기0이다. Source SHA CCDF344F…696184C, 설명도 일치한다. 실제 UI 선택 실행은0으로 native 동작 수용은 미확인이다. 남은 최적화는 기존 Research35.2 장부의 완료 근거를 유지한 채 현시점 분류·추천을 준비한다. 전문 조사/QA 최종 후보 수신 전 순위·새 구현을 확정하지 않는다. 로그 완료창 외 새 최적화·Git 작업은0이다.

로그 작업 main 반영(2026-11-16): MergeReceipt를 직접 대조했다. 메인은 LogArchiveZip.cs+LogRules.md2파일만 local main a0ff3838(부모c1db2c41)에 반영하고 최적화 브랜치30437f43로 통합했다. 확인/닫기/Esc는 열기0, 웹 선택만 폴더+Release를 연다. 저장·수집·ZIP·선택 삭제·런타임 로거는 기존 main에 이미 포함된 기능이며 이번 추가는 완료창2파일이다. 기존 staged patch SHA775AB389…E5C0A7와 dirty tracked14개 hash를 보존했다. 최적화 코드·계측·raw/private Task는 이번 main 대상이 아니다. 사용자 push 요청이 없어 push0/원격 미반영이며 새 기기·성능 검증이 아니다.

## 40. 완료 항목을 제외한 남은 최적화 분류와 추천

기존 Research35.2 후보 장부와36~39 완료 근거는 보존한다. 타일 mesh/material·owned bundle 배열·이동6이웃·Detect/Attack 존재 early-exit·중복 query·hatch draw 감소를 다시 구현 후보로 선정하지 않는다. 최신 정상 재시도 분석은39.2/Plan36.4에 기록했다. 완료 항목의 Android GPU/ms/FPS 등 미측정 부분은 검증 잔여이며 구현 미완과 구분한다.

| 추천 관점 | 후보·현재 구조 | 기대와 아직 필요한 확인 |
|---|---|---|
| 비용 절감 규모를 먼저 조사 | 공격 결과 임시 collector 재사용 안전성: UnitCombatUseCase195~263/1160~1185의 호출별 collector+List+초기8 backing 생성 | 호출 간 임시 저장소만 재사용 가능한지 수명/재진입/예외/독립 소유권 조사. 최종 ToArray owned 결과는 보존. 네트워크 bundle 완료 경로와 별개의 상위 경로이며 실기 Bundle1596은 collector 할당 counter가 아님. bytes/ms 미측정으로 절대 절감1위 확정 불가 |
| 같은 동작의 불필요 반복 제거 확실성 | UI cooldown 정수 캐시: 최대5slot에서 매 frame 같은 정수 ToString | 표시/경계/초기화가 동일한 조건에서 초 값이 바뀔 때만 변환. 작은 범위이며 실비용 미측정 |

두 추천 모두 비용을 줄이는 후보이며 배타적인 분류가 아니다. 첫 후보는 잠재 규모·수명 안전성 조사, 둘째는 작은 반복 제거의 확실성 관점이다. 어느 쪽도 새 구현을 확정하거나 ms/FPS 효과를 입증한 상태는 아니다.

그 밖의 남은 조사 범주는 전투 탐색 범위·타일 검색·길찾기 재계산·AI 갱신, 유닛 생성/풀 용량·반환, VFX CPU/GPU·일반 렌더, GC/메모리 보관, 네트워크 중복 payload, UI·물리·로딩·발열이다. 각 구조·실제 발생 빈도·제거 가능 부분과 검증 부담을 읽고 비교해야 한다. 관측 GPU27ms나 tick시간 자체를 절감 가능량으로 쓰지 않으며 임의 품질 저하·풀 확대·새 계측을 먼저 적용하지 않는다.

### 40.1 공격 결과 collector 수명·재사용 조사

피해와 표현 결과는 그대로 두고 매 공격 수집에 필요한 임시 객체 생성만 줄이는 안을 조사한다. 최종 결과 배열은 호출마다 독립 소유로 유지한다. 사용자는 조사·설명 뒤 구현·자동 비교·빌드 시도를 승인했다. 제품1파일 구현·private 하네스 결과와 Unity 반영은 아래 범위로 확인했다. 최종 SDK/source 보존 영수증과 QA 독립 재현까지 확인했고 빌드 시도는 완료했다. 성공·설치·실행은 미확인이다. 이후 사용자 실기 저장 결과는 아래 범위로 관측했으며 직접 할당·ms/FPS는 미측정이다.

UnitCombatUseCase195~263/1131~1185 원문에서 invalid scope→권위 reject→nested guard를 거친 뒤 collector/List/초기8 backing을 생성하고 active로 지정하는 순서를 확인했다. core는1회 실행되고 finally에서 active를 해제한 뒤 ToArray/Status로 결과를 만든다. writer2172/2514가 active에 값을 기록하며 동기 이벤트 callback으로 재진입할 가능성이 있다. 그러므로 단일 전역 객체 공유나 active collector reset은 안전하지 않다.

제안은 UseCase instance마다 idle collector를 최대1개만 lazy 보관하는 방식이다. active 중에는 재사용/초기화/반환하지 않는다. 기존 active=null 시점을 유지하는 inner try/finally 뒤 ToArray·Status snapshot·Observation 반환을 수행하고, 전체를 감싼 outer finally에서 clear/reset·idle 반환한다. core가 throw해도 active 해제와 기존 예외 전파를 유지한다. attackerId/result key/ordinal/status/count를 모두 초기화하고 nested 호출이 외부 수집에 미치는 기존 효과도 보존한다. 필터·정렬·HP·RPC schema는 변경하지 않는다.

정상 warm 호출에서 collector/List/초기 backing3개 신규 생성을 줄이는 후보지만 최종 nonempty array·복사·capacity growth는 남는다. invalid/reject/nested는 기존 생성 회피 경로와 구분하며 모든 호출당3개 절감으로 쓰지 않는다. 기본8에서 최대256까지 커진 backing을 instance 수명 동안 보관할 수 있는 상주 메모리 tradeoff가 있다(바이트 미측정). facts는 int/bool/WorldPointXZ 값형으로 유닛/GameObject 참조를 담지 않는다. 효과는 구조적 예상이며 실제 빈도·bytes/ms/FPS는 미측정이다.

collector 소유권 최종 보완: Setup348에서 맵마다 새 UseCase를 생성하므로 cache는 경기 간 공유하지 않는다(이전 instance 실제 수명은 잔여 참조에 따름). nested push/pop·stack pool을 추가하지 않고 nested core의 피해가 outer collector에 들어갈 수 있는 기존 정책을 그대로 유지한다. Record/필터 본문과 writer는 바꾸지 않는다. 범용 pool이 아닌 UseCase instance-local idle1슬롯에서3guard 뒤 참조를 대여하며 즉시 idle=null로 비우고 최초 필요 시만 new→BeginReset한다. 로컬 collector는 owned 배열·Status snapshot·Observation 반환 완료까지 보유하고, 기존 inner finally의 active=null 시점을 유지한 뒤 outer finally ClearScope 후 idle이 빈 경우만 반납한다(최대1슬롯). readonly attacker/key2필드는 호출 scope 초기화를 허용하도록 바꿀 예정이며 Clear 비용0을 주장하지 않는다. 제품1파일 구현·자동 비교1차 결과와 Unity 반영은 아래 범위로 확인했고 SDK/QA 최종 마감까지 완료했다.

승인 후 진행 상태: 메인이 Login·Android·Play OFF를 확인해 Assets 편집을 허용했다. idle1슬롯 대여/반환, owned 배열, nested·순서·throw 보존 범위만 구현하며 다른 dirty·새 문서·메모리·Git 작업은 없다. 메인은 최종 검증 후 File→Build And Run을 시도했고 진행창 관찰 뒤 완료 감시를 중단했다. 사용자 최소 확인은 Android Host+Editor Client에서 일반 직격과 가능할 때 AoE 공격의 피해·처치/파괴→결과·로비 복귀이며, 통합 ZIP을 새 작업명 collector-reuse로 보관한다. 미관측 조건은 미검증으로 남긴다.

1차 검증 결과: private applied-fact-collector-reuse/HarnessResults.txt를 직접 확인했다. 실제 소스 collector/wrapper·Observed 거절 경로, 실제 값형과 core1회 stub의16case/1,170assert PASS다. eligible2회·facts<=8 표본에서 collector/List/backing은 각2→1,256facts2회 backing 생성12→6이며 최종 nonempty owned 배열2개는 유지했다. 이는 CLR fixture 작업량으로 실제 bytes/ms/FPS 절감이 아니다. 메인은 source E088… 유지와 Unity Refresh 뒤 Runtime DLL09:55:27 신규 생성·도메인 재로드·제품 컴파일 오류0을 확인했다. 기존24warnings와 MCP npm2error는 별도다. 이후 아래 최종 SDK·소스 보존·QA 및 빌드 시도까지 확인했다.

최종 마감: FinalVerificationReceipt SHA28AA77A0…4342016을 직접 확인했다. 제품 source E088E32D…28106FD, source 보존 proof8개 true·현재358source 누락0이다. QA가16case/1,170assert를 독립 재현했고 Editor/Release SDK 오류0(기존 경고24/31)·변경 파일 경고0을 확인했다. 실제 HP/RPC 실행·Android 생성 횟수/bytes/ms/FPS 검증은 아니며 owned 결과 배열·copy·capacity 보관 tradeoff는 남는다.

메인은 동일 제품의 Unity 새 DLL·도메인 reload·정상 편집 응답과 제품 컴파일 오류0을 확인했다. Android Development ON·Samsung SM-N971N 선택을 유지한 채 File→Build And Run을 클릭해 Detect JDK/Checking JDK 진행창을 관찰하고 사용자 요청에 따라 UI/로그 완료 감시를 중단했다. **빌드 시도 완료, 성공·설치·실행 미확인**이다. 새 앱에서 Android Host+Editor Client 일반/가능한 범위 공격의 피해·처치/파괴→결과·로비를 확인한 뒤 Editor Play를 중지하고 통합 ZIP 최신 세션을 작업명 collector-reuse로 보관하는 절차다. 새 문서·Git 작업이나 실기 PASS를 추가하지 않는다.

최신 사용자 실기 분석(2026-11-16): 메인은21:43 collector-reuse_01 ZIP7,309,297바이트/34entry(Android16+receipt16+Logcat1+Editor1)의 전체 ZIP과34개 bytes/SHA 일치를 확인했다. 구 세션이 섞여 있어 이번 최신 앱 실행만 분리했다. QA 대조에서 동일 shared session의 Host21:37:04.626→END21:42:00.597→로비21:42:03.228, Client21:37:05.061 시작→로비21:42:02.022를 확인했다. C3 server 결과/완결 bundle215→양쪽 released215, ready220·필수 표현190/190이며 Client resultAccepted215/reject0·transportFailure0·targetMismatch0·correlationFailure0이다. 이것만으로 실제 writer/직격·AoE 전종 전체 기능 수용을 확정하지 않는다.

기존 COMBAT-OPT Bundle215/sample16 mismatch0, Movement26(old6006→new156)/sample16 mismatch0·Fault0은 이전 두 최적화의 관측이다. collector 실기 직접 할당 횟수는 측정하지 않았으며215를 곱해 객체/bytes 절감량으로 쓰지 않는다. 실제 ms/FPS도 미검증이다. prebuild10:00:45의 E088 source 일치는 확인했으나 prebuild GUID0과 runtime GUID를 직접 join할 수 없어 완전한 installed source 귀속은 미확정이다.

이번 shadow FAIL은 dropped0이며 이전 예산 생략 사례로 설명하지 않는다. 별도 legacy presentation-shadow invalid 상세 로그는 Host/Client 각각 상한16이며 정확히16개 실패라는 뜻이 아니다. invalid 관측은 소스2563~2575/3284의 전체 FAIL 충분 조건이다. C3 presentation END failures0과 구분하며 유일 원인·전체 기능 정상으로 확대하지 않는다. Host MOVE의 MissingTile/RouteInvalidated 복구 반복62 FAIL도 별도 미해결 관측으로 남긴다. 새 collector와 인과 증거는 없으나 무시하거나 완료 판정하지 않는다. 따라서 정상 경기 진행/결과 전달 관측과 미해결 진단·collector 정량 효과 미검증을 분리한다. 추가 코드·새 Task·Git·raw 삭제는 없다.

최신 사용자 요청의 로그창·collector 결론(2026-11-16): 완료창은 별도 닫기 버튼을 제거한 확인/웹페이지 열기2버튼 구현을 freeze했다. 확인·Enter·Esc·창 X는 안내창만 닫고, 웹페이지 명시 선택만 폴더+페이지를 연다. 기존3버튼 검증은 이전 세대 이력이며2버튼 source SHA74F48666…445050E와 TwoButtonVerificationReceipt SHA E889ECF6…5E047D1을 직접 대조했다. 대표 Editor3tools 컴파일 exit0/오류0/경고0·로그0바이트이며 수집/ZIP/hash/검증/삭제 helper exact를 확인했다. 실제 native UI·브라우저/폴더 열기·빌드·Git 실행은0이다. ZIP 취소·삭제를 뜻하지 않는다.

collector는 실제 소스16case/1,170assert와 eligible warm 호출의 collector/List/backing3개 신규 생성 회피·owned 결과 유지로 구조적 목표를 달성했다. 실기215bundle의 Client reject0/transportFailure0과 GameEnd·로비는 관측했지만 실제 절감 bytes/ms/FPS를 측정하지 않았다. 공격 관측 invalid 상세 상한16 및 한 유닛 이동 복구 반복62 FAIL은 별도 미해결이며 이번 교정과의 인과 미확정이다. 따라서 구조 목표/범위 내 동등성 근거와 전체 회귀 PASS를 구분한다.

제품 선별 반영·push 완료(2026-11-16): 사용자는 최적화 결과 문서와 제품 main 병합·push를 승인했고 아래 영수증 범위로 완료했다. query+collector 및 로그 완료창/LogRules와 이번 제품 현황·이력 신규 행만 선별 반영했다. 기존 측정 행·설정·도구·raw/private Task는 main에 포함하지 않는다. PROJECT_STATUS/WORK_HISTORY의 main 공통 표에 이번 결과·한계만 추가했고 기존 본문은 보존했다. main 반영·push 완료와 별도 진단 FAIL 미해결을 구분한다. Git 실행은 메인만 담당한다.

QA 진단 분류 정정(2026-11-16): invalid16은 Host/Client 각각 상세 로그 상한으로 정확16실패가 아니다. 최초3표본에서는 DoT SourceId의 유닛 lookup 실패로 attacker=-1/key0을 허용하는 지속 피해/즉시 표현과 일반 LegacyIsValid의 조건 충돌을 확인했다. 같은 tick Enqueued/Emitted가 이중 관측되며16전체의 유일 원인으로 확대하지 않는다. 다음은 진단 적격 범위 조사이며 피해 삭제로 해결하지 않는다.

MOVE62는 Rabbit 한 유닛이(10,7)에서 존재하지 않는(11,6) 후보를 약2.05초 여러 frame 반복 거부한 뒤 사망한 관측이다. unsafe commit 차단·RepathRequired 자체는 안전 계약을 지켰지만 같은 후보 반복 원인은 post-combat/world-hex/경계/progress guard의 별도 조사 대상이다. FAIL을 낮춰 판정하지 않는다. collector 교정 원인이라는 주장과 기존부터 동일 문제였다는 주장 모두 확정하지 않는다. 제품 후보 문서의 상세 상한/한 유닛 반복 표현도 함께 바로잡았다.

최종 main·원격 반영: optimization-main-merge의 MergeReceipt/CandidateVerificationReceipt/PushVerificationReceipt를 직접 대조했다. main eda2dca0와 origin ls-remote exact 일치·push exit0, 최적화 브랜치 a64b31d1 통합을 확인했다. 제품2파일의 신규 hunk+로그2파일+제품 문서 신규2행의6파일만 포함했다. 작업본 통복사가 아니라 HEAD→working patch를 main에 적용해 기존 committed 진단을 제외했고 측정 도구/설정/raw/private Task는 포함하지 않았다. main354source 누락0·Editor/Release/Tools 오류0(기존 경고24/31/0), staging patch SHA775AB389…E5C0A7와 tracked working16개 hash 보존이다. 최신2버튼 완료창도 포함됐으며 이번 신규 빌드는0이다. 상세 상한16·한 유닛 복구 반복62 FAIL과 제한된 원인 근거/실기 bytes·ms·FPS 미검증은 유지한다. 제품 문서 추가 수정이나 Git 실행은 하지 않았다.

## 41. 이동 중심 도달·다음 타일 증상과 DoT 진단 교정 조사

사용자는 유닛이 이동 중 타일 중심에 도달하지 않은 상태에서 다음 타일을 사용하는 증상을 실제로 보고했다. 추가 사용자 설명은 Android와 Editor 양쪽, 교전 전 일반 이동에서도 중심 이탈이 관측됐다는 것이다. 일반 A* 이동의 centre checkpoint를 우선 조사하며 기존 PostCombat 한 유닛의 후보 거부62회와 같은 원인이라고 가정하지 않는다. 목표는 FAIL을 숨기는 것이 아니라 이동 원인을 교정하고, 허용된 DoT 표현이 일반 legacy 진단에서 잘못 분류되는지 확인하면서 실제 오류 보호를 유지하는 것이다.

앞 query/collector 제품 main·push 완료와 이번 조사는 별도 단계다. 현행은 DoT8파일 구현·소스/SDK 및 독립 QA 조건부 검증 완료, 이동1파일 교정의 C native 제한 PASS다. 실제 NGO/멀티와 사용자 일반 중심 증상은 미해결·미재현 범위로 남긴다. 최종 임시 driver cleanup 영수증을 확인해 정리까지 완료했다. 기존62회/상세 상한16과 최초3표본 근거는 보존하며 전체 원인으로 확대하지 않는다.

조사 분리 이력(2026-11-17 초기): 일반 A* centre checkpoint와 DoT provenance를 독립 조사했다. 이후 DoT8파일과 같은-cell 경계 추격1파일 교정·검증을 진행해 아래 결과까지 완료했다. 로그 판정도 Host16은 archive record 내부 DoT stack 근거가 있지만 Client16은 해당 stack0으로 직접 caller 미확인이다. source/교차 peer 대응은 추론으로만 사용한다. 고정24줄 오귀속 인계는 정정했고 실수 기록에 남겼으며 사용자에게 Client14확정으로 보고하지 않았다. 사용자 일반 중심 증상 해결이나 전체 실기 PASS는 없다.

DoT 재현·교정 진행(2026-11-17): 전문 담당 actual-source 재현은 owner 없음에서 HP100→90/TakeDamage1/Emit1은 정상인데 감사2회 invalid2, owner 있음은 같은 피해·Emit에 invalid0이었다. 명시 IsTimedDamage+unit/Immediate+세 키 default·유한 비음수 시각만 일반 Legacy 감사에서 분리하는 제안은 invalid0을 보였고 broken ordinary/정규 valid·손상 nondefault key/NaN 검출은 유지했다. 이는 교정 설계 재현이며 실제 NGO/멀티 PASS가 아니다.

메인은8파일 구현을 승인했다. GameEvents optional bool(false 기본)→ApplyTimedDamage writer true→NetworkHealth RPC 전달→HitQueue ObserveLegacy 적격 분리와 Resolver schema9→10, 기존 schema9 검증3파일 기대값10 갱신(검사 유지) 범위다. HP/Emit 순서·피해·정규 공격 검사는 보존한다. DoT 실제 코드·최종 소스/SDK와 독립 QA 조건부 검증은 완료했으며 후속 멀티는 양쪽 새 코드가 필요하다. Network 기술 문서의 현행 출처/schema와 LogRules 감사 적용 범위만 정합 수정했으며 과거 schema9 이력은 바꾸지 않았다.

일반 이동은 순수24path/1,353step에서 증상 미재현인 음성 결과다. 이 순수 음성 결과 이후 실제 MonoBehaviour의 A/B/C를 실행하고 경계 추격1파일을 교정했다(아래 별도 결과). 일반 이동 중심 이탈을 이 음성 결과로 부정하거나 DoT 교정과 같은 원인으로 확정하지 않는다.

실제 native1차 이동 결과(2026-11-17): Play OFF/Login 복원 뒤 native-bcbd…json/CSV·NativeFirstIndependentAnalysis를 직접 확인했다. Single Legacy 직선/꺾임과 실제 Host Reducer 직선/꺾임4case,627행에서 gap0·다음 target4전환 직전 오차0·조기 전환0, loopback/cleanup true였다. 증상은 미재현이며 전체 정상·사용자 증상 해결로 해석하지 않는다. Visual 측정에 쓴 PresentationPosition getter가 ProjectNow를 호출하는 부작용이 있어 root trajectory 근거와 자연 Visual 갱신 검증을 구분한다.

후속 실행 이력: private payload를 순수 Transform 읽기로 고쳐 실제 경기 Outer 맵 조건의 B/C 경계 probe를 완료했다. 이전 내부16×16 단순4case와 분리하고 captured exact root가 없으면 synthetic probe로 표시한다. 원래 실기 MOVE62와 exact root/yaw 인과는 미확정이지만 B synthetic 실제 추격 루프의 반복 정체와 C 교정 후 안전 접근을 확인했다. 이후 같은-cell 경계 추격 교정의 B/C 검증은 아래에 구분한다. 사용자 일반 이동 중심 증상은 미재현이며 추가 native 실행 전 문서 쓰기를 freeze한다.

공격 최종 상태는8파일 freeze·독립 QA 소스/CLR 조건부 PASS, SDK 오류0(기존 경고24/31/0)와 음성 변이 검출 확인이다. SkillZones의 현행 PASS 요약은 schema10으로 정정했고 역사9주석은 유지했다. 해당 source hash6AA1741…24B8·EditorTools 재컴파일 오류/경고0, 나머지7파일 불변이다. 실제 NGO 전송·양쪽 새 앱 멀티 수용은 미실행이므로 schema10에 맞는 새 코드 양쪽 검증이 필요하다.

경계 추격 B/C 독립 판독: B native-083989…는5case/795행이며 이전 A4case/627행과 다르다. 실제 loopback Host·spawned server·실제 Rabbit stats(range0.5/detect1)와 원래 EnterCombatPursuitV3 coroutine을 사용했고, frame마다 MoveNext해62연속 MissingTile·동일 root/yaw·이동0을 확인했다. 실제 Outer 맵의 같은 cell이지만 root0.499/140°는 synthetic 배치로 원래 Rabbit49 exact pose 재현이 아니다.

제품 교정은 UnitView.Movement.cs15줄 추가로 ReducerAuthoritative의 walkable 같은-cell 추격에만 [liveRootTile,liveRootTile]/index1 corridor를 제공한다. 기존 resolver의 거리 축소·제한 회전과 안전 commit 검사를 재사용한다. 공격 range/권위/Legacy·Single 직접 이동/다른-cell 경로는 바꾸지 않는다. 추격당2Hex 배열 lazy1개 비용이 있으며 일반 중심 증상을 고쳤다는 근거는 아니다. Editor/AndroidRelease SDK 오류0(기존 경고24/31)이다.

C native-f9dd3c…json/CSV/chase를 직접 읽고 독립 집계했다.5case848행·gap0/조기 중심 전환0, synthetic chase54frame에서 MissingTile0/안정거부0·전진25frame·회전53frame·진입 요청1을 확인했다. B62정체와 C안전 접근을 구분하며 같은 frame/dt 통제 향상률로 쓰지 않는다. 거리0.6000003→0.3605458은 world XZ이며 raw AttackRange0.5를 직접 임계거리로 비교하지 않는다. 기존 CalculateRangeLimits783~810은 근접 AttackRange<1에 MeleeContactDist0.3+Epsilon0.05=0.35world를 사용한다. C의 root0.3605458은0.35world 밖이고 다음 안전 후보<=0.35만 실제 HasEnemyInRangeAt true여서 원래 진입 요청1이 발생했다. fixture에 NCC/RPC ACK가 없어 원래 원자 guard가 후보 Root commit을 보류했다. 실제 Root 범위 도달·공격 commit·피해 실행은 검증하지 않았다.

C는 같은-cell 경계 추격의 제한된 안전 접근 근거다. 사용자 일반 중심 증상·정확한 발생 구간/유닛은 여전히 불확실하고, 실제 tile mesh pivot/model feet·Android/Client 보간·전체 기능/성능 수용은 미검증이다. 공격 DoT의 schema10·실제 NGO 미실행 한계도 유지한다.

임시 driver 정리 마감: TemporaryDriverCleanupReceipt를 직접 확인했다. 이번 own driver843바이트/meta62바이트2파일만 hash 검증·private 사본 보존 뒤 삭제했고 메인이 두 Assets 경로 부재와 제품 hash4A114… 불변을 확인했다. 제품/증거 payload·native JSON/CSV와 기존 자료는 유지하며 추가 코드 작업은 없다.

최신 사용자 테스트 조사(2026-11-17): 사용자는 “주로 속도가 빠른 토끼전사나 불정령2단계유닛과같은유닛”에서 중심 오버가 계속 보였고 멈춤·회전은 없으며 공격·피해는 육안상 문제없었다고 보고했다. 중심 현상의 구간/유닛 해석과 원인은 아직 확정하지 않는다. 이는 앞 synthetic 경계 추격 C의 제한 PASS와 구분하며 일반 중심 증상 해결로 기록하지 않는다. 최신2026-11-17_17_18_android-logs-test1_01.zip의 최종 분석은 아래 범위로 완료했고 로그 완료창 원복의 최종 native2버튼 표시·확인 닫기는 아래 범위로 검증 완료했다. 작업명 안내 누락으로 사용자가 임의 이름을 정한 사실을 수용하며 원본을 그 이름으로 식별한다. 이동 증상은 미해결이며 아래 관측을 새 전체 PASS·원인 교정 완료로 확대하지 않는다.

최신 로그·빠른 이동 재분석(2026-11-17): LatestLogAndFastMovementAnalysis-20261117.json을 직접 읽었다. 사용자 원본 test1_01 ZIP은277,096바이트·hash/각 source 검증을 확인한 자료이며 결과창 시험용 새 ZIP과 혼합하지 않는다. 마지막 실제 경기 Host13,641frame·센터 checkpoint497의 기록 오차0·반복 경계 거부0이다. 앞6개 MOVE FAIL은 관측0의 별도 run으로 분리한다. checkpoint 기록은 연속 궤적/모델 발이 타일 중심에 닿는 모습 전체의 증거가 아니므로 사용자 빠른 이동 중심 오버가 해결됐다고 하지 않는다. 사용자 표현의2단계는 불정령 유닛을 수식하며 토끼/중심 단계에 붙이지 않는다. 실제 enum 타입 매핑은 미확정이다.

같은 마지막 경기 C3 결과246·표현202/202·양쪽 result/presentation failures0을 확인했다. 명시 DoT 출처 로그0/legacy invalid 상세0은 관측이며 총 counter가 없어 source-lost DoT 전달/전종 수용을 입증하지 않는다. canonical clock invalid Host2/Client1·coverageUnknown true는 별도 한계로 유지한다. 사용자 공격·피해 육안 이상 없음 보고와 실제 NGO/DoT 완전 수용을 구분한다.

기존 C native Host 꺾임의 outgoing 횡이탈0.09589184world는 이전 fixture 재분석으로 이번 사용자 경기 재현이 아니다. 이전 중심은 정확히 소비했어도 다음 구간의 raw checkpoint Desired와 planned trajectory tangent 판정이 다를 가능성이1순위 가설이다. 정상60° 연속 회전 규칙이 있어 raw15°를 즉시 정지 조건으로 적용하지 않는다. 기존 CSV 각도 의미를 ALIGN1/3/5 의도와 대조한 뒤 구체안을 제안하는 단계이며 후속 교정은 미승인·미구현이다. Client 보간·발/pivot 대 root 차이도 가설로 남기고 추가 logger/빌드·사용자 재테스트는 요청하지 않는다.

로그 완료창 원복은 native 빈 cancel 방식에서3버튼이 보여 시각 검증을 통과하지 못했다. 최종 native bool2버튼 코드는 웹=ok/확인=cancel로 반영해 SDK 오류/경고0을 확인했다. Escape/X는 닫기이고 Enter는 기본 웹 버튼을 실행하는 한계가 있다. 메인은 Refresh 뒤17:39 fresh Unity 기본창에서 웹페이지 열기/확인 정확2버튼·별도 닫기/Cancel 없음과 확인 클릭의 창 닫기·외부창 열기0을 확인했다. 웹 버튼은 미클릭이며 소스 bool true 분기로만 확인했다. Escape/X는 native API 계약, Enter 기본 웹 동작은 한계로 유지한다. SDK 오류/경고0·source63F2B…4681 일치다. 원본 test1 분석 결과와 별도 UI 시험 산출물을 구분한다.

원복 UI 확인 산출물 구분: 메인의 결과창 시험용17:33 android-logs-test1_02 및17:38 _03 ZIP은 추가 수집 산출물이며 사용자 실전 분석은17:18 _01 원본만 사용했다. 기기 원본 삭제0이고 이번 원복 확인을 새 사용자 테스트/이동 증상 해결로 해석하지 않는다. 새 테스트 요청·추가 이동 제품 수정은 없다.

## 42. 정상 코너의 연속 감속 조사

사용자는 방향을 바꿀 때 멈추지 않고, 비정상적으로 빠르게 회전하지 않으며, 코너에 접근할수록 차량처럼 서서히 감속하는 교정 방향과 기존 Task 문서 작성을 명시 승인했다. 목적은 타일 중심 통과 순서를 지키면서 정상 코너를 연속적으로 부드럽게 이동하게 하는 것이다. 기존 로그·native 분석과 일반 중심 증상 미해결 기록은 유지하고 새 방향 합의를 전체 버그 해결로 쓰지 않는다.

정상 코너는 비영속도·중심 checkpoint 순서·기존270°/s 회전 상한을 유지한다. 차단/반전/공격 안전 stop은 정상 방향 전환과 별도이며 제거하지 않는다. 실제 이동 속도 배율을 보존하고 코너 이후 재가속을 고려한다. 사용자가 기존 Task 작성과 구현 진행을 승인했고 메인은 아래3파일 최소 설계를 승인했다. 전문 담당이 구현을 준비/진행하며 아직 제품 변경·측정 결과는 확정하지 않았다. 진단 필드·수용 대응의 최종 상세는 담당 결과 수신 뒤 정합 갱신한다. 새로운 Task/Testcase·메모리·규칙·Git 변경은 없다.

### 42.1 승인된3파일 최소 설계

제품 범위는 UnitMovementContracts.cs의 순수 속도 정책, UnitView.MovementAuthority.cs의 작은 코너 state·이동 예산 cap, 기존 UnitMovementAuthorityObserver의 종결 요약이다. 이동/회전 방향·270°/s·중심 checkpoint 순서·corridor·기존10/15° 판정 기준은 유지하고 속도 예산만 조절한다. ReducerAuthoritative만 적용하며 Single Legacy 직접 이동과 Client의 서버 복제 소비를 바꾸지 않는다.

초기 후보식은 코너 회전각 θ/270으로 회전시간 T, 다음 선분 길이 L/실효속도 v로 통과시간 S를 계산해 cornerSpeed=v×S/(S+T)로 둔다. 코너 전 v×T 거리에서 연속적으로 감속하고 중심 통과 후 남은 회전각이 줄수록 원래 실효속도로 연속 회복한다. 새로운 임의0.x 상수를 정답으로 고정한 것이 아니라 검증 대기인 초기식이다. v에는 기존 연구/둔화/빙결 등 속도 배율을 반영하고 직선 기본속도·재가속을 보존한다. 코너 식은 비선형이므로 최종 코너 속도가 버프 전후 정확히 같은 배율이라고 보장하지 않으며 stats/연구계수 자체는 바꾸지 않는다.

capture 판단에는 원래 이동 budget을 사용하고 실제 이동 travel cap만 감속한 값으로 제한한다. 감속 때문에 capture 범위가 축소되거나 travel cap을 넘겨 중심을 스냅하지 않아야 한다. 코너 state는 command/path 교체·취소·target acquire·빙결·사망·Initialize 때 stale 상태가 이어지지 않도록 수명을 정한다. 정상 코너는 비영속도를 목표로 하며 차단/반전/공격의 안전 정지는 별도 계약으로 유지한다.

위험은 감속 전후 budget 혼동·짧은 선분/고속/dt 경계·상태 잔존·멀티 표현 속도와 실제 이동의 불일치다. 기존 Units 이동규칙2·U-MOV-ALIGN의 중심/회전/속도 계약에 사용자 승인 감속 설명만 최소 정합 반영하며 새 기획 규칙을 임의 추가하지 않는다. 진단 대응은 기존 종결 요약의 최종 필드가 정해지면 테스트 항목과 연결한다. 구현·소스 CLR/SDK 결과는 아래 범위로 확인했으며 native/실기와 사용자 일반 중심 증상 전체 해결은 미확정이다.

### 42.2 소스 CLR·SDK 결과와 남은 한계

private corner-speed의 CurveResults.Frozen.csv/BoundaryResults/ProductCompileResults/EditorToolsCompileResults/TerminalBudgetResults를 직접 확인했다. 이는 기존 ActualCorner source CLR fixture의 두 구간 비교이며 사용자 실전 재현이나 native가 아니다. 정상30/60fps의 speed1/2/2.64 표본에서 정지0을 유지하고 횡이탈이 약14~32% 감소했다. speed2·60fps는0.1714287→0.1280133world, 구간 통과시간1.066667→1.133333초(+6.25%)다.30fps는0.1594017→0.1174338world,1.066667→1.166667초(+9.375%)다. 횡이탈 감소와 통과시간 증가를 함께 보고하며 전체 이동 해결/FPS 개선으로 해석하지 않는다.

큰 dt0.12/0.333초에서는 전후 모두 기존 direction gate에 따른 정지1~2frame이 남아 정상 코너 정지0 목표를 전범위 달성하지 못했다. 기존15° 판정은 이번 승인 범위에서 바꾸지 않고 OPEN으로 유지한다. 초기 cap 검사는 double1ULP 재적용 동일성 문제를 발견해 원문을 보존했고 최종 metadata 멱등 capErrors0이다. Boundary40검사 PASS·maxDoubleCapExcess0·float 변환 미소 초과 약1.776×10^-15이며 실제 float pose 동일성과 무한 정밀도 보장을 혼동하지 않는다.

Editor/AndroidRelease SDK 오류0·기존 경고24/31·변경 파일 경고0·현재358source 누락0, EditorTools28source 오류/경고0이다. 종결 budget valid/reset true·32줄·최대968bytes로 확인했다. UnitViewAnimator·Client 계산·Single Legacy 경로와 로그 메뉴는 변경하지 않았고 새 영구 계측 도구도 없다. 위 CLR 검증 시점의 native/실기는 미실행 이력이다. 이후 사용자 실기 보고와 미해결 판정은 아래에 별도 기록한다. 로그 작업명은 android-performance-correction이다.

| 확인 항목 | 기존 observer 확장 근거 | 한계/별도 근거 |
|---|---|---|
| 코너 구간·완결성 | cornerPolicy/Frames/Starts/Completed/Incomplete | 미완 구간을 완료로 취급하지 않음 |
| 정상 정지와 안전 정지 분리 | cornerNormalStops/cornerSafetyStops | 큰 dt OPEN·차단/공격 정지 별도 |
| 속도·기하·회전 | cornerMinMovingSpeed/MinRatio/MaxCross/MaxTurnExcess | minMovingSpeed는 정지0 증거를 대신하지 않음 |
| 관측 시간 | cornerFrameTimeSum | 여러 유닛 누적 관측시간, 경기 통과시간 아님. 통제 비교 시간은 private fixture CSV |

corner-end는 상태 변화 때만 bounded 기존 Dev observer 로그를 확장한다. 최종 필드 해석과 실제 수용은 지원/관측 범위에 한정하며 로그가 없는 조건은 미검증으로 남긴다.

최종 freeze 마감: FinalVerificationReceipt/FinalSourceHashes를 직접 읽었다. 최종 효과 근거는 CurveResults.Frozen.csv(SHA ACB1918…7167)이며 이전 Final.csv와 값/바이트 동일을 확인했다.3파일 최종 source hash는 Contracts C80A…B7024·Authority23D8…385D6·Observer07C0…9C33이며 메인이3/3독립 대조했다. 분류는 제품3파일 구현·CLR/SDK 검증 완료, 큰 dt 정지 잔여로 전체 요구 미달, 해당 freeze 시점의 native·실기/빌드 시도0은 이력이며 이후 사용자 테스트는 아래 재점검 범위로 분리한다. 기존 수치·비선형 배율/통과시간 tradeoff와 OPEN을 유지하고 추가 교정·코드 수정 요청은 없다.

### 42.3 감속 후 사용자 증상 지속·탐색 경로 재점검

사용자는 이번 감속 후에도 현상이 그대로이며 다음 이동 방향이 잘못된 것은 아닌지, 탐색 최적화가 관련됐는지 의문을 제기했다. 현행 판정은 실기 증상 미해결이다. 앞 source CLR의 횡이탈 감소·통과시간 증가는 사용자 현상 해결 증거가 아니다. 감속 rollback 승인은 없으므로 자동 원복하지 않고 추가 제품 수정 없이 조사한다.

메인이 최신20:44 android-performance-correction_01 ZIP442,295바이트/SHA1cc5b92d…e23e36·4entry를 검증했다. 최신 Host run의 corner-end107은 Rabbit93/Flame14,complete103/false4다. Host 최신 END/terminal이 없으나 raw tail20:43:08.015라는 사실만으로 단순 잘림을 단정하지 않고 원인을 미확정으로 둔다. terminal minRatio/maxCross 등은 확인 불가다. 따라서 부분 corner-end만으로 전체 감속 수용·효과를 확정하지 않는다.

두 담당의 독립 소스 재점검에서 a42ba8d9는 복귀 후보의 전체 grid 열거→인접6조회만 바꿨고 정렬·도달 검증·일반 FlowField·path index·현재/다음 tile 전달은 바꾸지 않았다. eda2dca0은 전투 범위 중복 조회를 제거한 변경이다. 탐색이 원인이라는 증거는 없지만 해당 소스가 그대로라는 이유만으로 원인을 배제하지도 않는다. 실제 path[i±1]/pending/rally 자료가 없어 방향 오류를 입증하기 부족하다.

기존 native case3 궤적 재시각화를 완료했다. HostReducer225samples(frame15198~15422)에서 B중심(4,-1.5)→C(4.5,-2.25),15313 Root가 B에 정확 도달한 뒤15314 targetC/첫 Root delta(+0.008872509,-0.000118136)이며 첫 진행과 B→C 방향각은55.547°다. A중심은 없어 첫 Root를 A*(3.007872105,-1.504115701)로 표기했으며 임의 중심을 만들지 않았다. private existing-native-case3-root-direction.svg/png의 최종 PNG1260×890/SHA d8e6f0b3…b4cf2를 메인이 시각 확인했다. 이는 감속 전 통제 fixture 재시각화로 이번 실기 재현·새 게임 테스트가 아니다. 그림 형태와 실제 증상이 같은지 사용자 응답은 대기하며 원인 확정·픽스 승인/PASS는 아니다. 실제 경로와 전달값 증거 확보 전 방향 교정·전체 원인·PASS는 확정하지 않는다. 새 제품/도구·Task/Testcase·메모리·Git 변경은 없다.

### 42.4 실제 경로 입력과 이동 방향 증거 추가

사용자는 비어 있고 이동 가능한 타일 방향에서도 현상이 발생하며 그림/설명 구분이 어려우니 정확히 분류할 로그를 요청했고, 기존 Task 이어 기록도 승인했다. 이번 단계는 원인 분류 증거 추가이며 버그 교정이 아니다. 실제 Move 입력과 Root/표현 관측을 묶어 ActualPathVsMotion을 대조하고, 사용자에게 그림 판단이나 정확한 채팅 시점을 다시 요구하지 않는다.

최종 제품 범위는 기존 UnitView.MovementAuthority.cs와 UnitMovementAuthorityObserver.cs2파일이다. path0/prev/current/next/goal·index/count, command/segment/reason/map revision·live tile/pending goal/rally와 Root before/after·desired/actual 방향·몸 방향·순수 VisualRoot·role/flip을 연결한다. 순수 관측만 추가하며 이동/탐색/감속/네트워크·규칙 기능은 바꾸지 않는다. Editor/Development만 호출과 인자 평가를 남기고 Release는 제거한다. 기존 GameLog 개발 Info와 observer 예산을 재사용한다.

| 사용자 현상 구분 | 예정 증거 | 단독 판정 금지·한계 |
|---|---|---|
| 잘못된 다음 tile 입력인지 | path index/count와 prev/current/next/goal·scope/revision | live tile≠prev path tile만으로 오류 아님: 공간 경계가 checkpoint보다 먼저 commit될 수 있음 |
| 올바른 입력인데 회전 궤적이 다른지 | Root before/after·desired/actual 방향·몸 방향 | angle 차이만으로 현재 규칙 FAIL 아님, EVIDENCE로 기록 |
| root와 관측 VisualRoot 위치 차이인지 | 순수 VisualRoot position·role/flip | pre-LateUpdate 표본이며 실제 화면 LateUpdate·모델 발/mesh·모델 자식 yaw 증거 아님 |
| 뒤늦은 고속 유닛/불완전 경기 | 타입별 표본·전이 후 짧은 buffer·skip/drop·개별 record | END 없어도 확보 record는 읽되 전체 coverage로 확대하지 않음 |

route/step/window/coverage/budget의 schema movement-direction-v1로 기존 Dev observer를 확장했다. 타입별 실제 코너 유닛 순번1/4/16/64의4사건+route/scope/phase 합산2사건+index1 초기 heading1사건=7×25타입×3줄의 최대525줄이며 기존 normal736을 공유하고 terminal32는 유지한다. 설정은1회다. followCalls2는 서로 다른2frame 보장이 아니므로 각 f값으로 해석한다. scopeBefore/After와 고정 runId/tokenSample로 route 교체 혼합을 차단하고 commit 관측값은 실제 commit 직후다. END가 없어도 확보 route+step 묶음은 판독하되 skip/drop·미관측 범위를 명시한다. 현재 진단 구현·CLR/SDK 검증 완료이며 사용자 원인/픽스/실기 PASS는 아니다. 이후 사용자가 새 빌드로 평소 플레이한11/18 실기 로그를 확보했고 아래에서 분석했다. 에이전트의 추가 빌드 시도는0이며 사용자 실행과 구분한다. 새 Task/Testcase·메모리·Git 변경은 없다.

최종 진단 검증: SDK Editor/Release358source 오류0(기존 경고24/31), 변경 파일 경고0·membership 누락0이다. fixture91검사(진단89+return/throw guard2) PASS, terminal32·최대968bytes·reset true, Release 전처리 후 공백 정규화한2파일 동등을 확인했다. 추가 원인 교정·속도 조정·로그 메뉴 변경은0이며 이 문단의 구현 freeze 시점에는 native/빌드/기기 수집 미실행이었으며, 이후 사용자 새 빌드·실기 수집은 아래 최신 분석으로 대체한다. 최종 Tools/hash 영수증까지 아래 범위로 마감했다.

당시 안내한 절차는 새 Development Build의 Android Host+Editor Client에서 토끼전사·불정령2단계 유닛의 평소 이동/교전 후 로비로 복귀하고 앱을 켜둔 채 통합 ZIP 최신 실행을 저장하는 것이다. 특정 채팅 시점·그림 판단을 요구하지 않으며1회로 모든 원인을 확정한다고 약속하지 않는다. 로그올리기:android-performance-correction.

최종 구현 마감: movement-direction-trace의 FinalVerificationReceipt(SHA AD742B3F…1F62CC7)를 직접 확인했다. Authority4D8FC…77D092/Observer7E28…F9B82D의2파일 freeze·메인2/2 hash 대조, 실제 추출 fixture91검사·최대 sample638bytes, terminal32/max968/reset true, Editor/Release358source 누락0·오류0(기존 경고24/31, 변경0)과 Tools 오류/경고0이다. Release 동등은 전처리 후 공백 정규화 소스2/2이며 IL/binary 불변을 검증한 것은 아니다.

rev는 유닛 navigation 관측 revision으로 FlowField cache 동일성의 증거가 아니며 reason은 fallback 입력이다. header/corner-end에 schema/frame 맥락을 보강했다. 진단 구현과 사용자 새 빌드 실행·기기 로그 수집은 완료했으며 실제 분석 범위는 아래와 같다. 에이전트 추가 빌드 시도는0이다. 원인 확정·증상 교정은 미완이며 Client 서버 path는 unknown·순수 VisualRoot 위치는 pre-LateUpdate 관측으로 한정한다. 새 파일·메모리·Git 작업은 없다.

### 42.5 11/18 방향 진단 실기와 구현 이력 대조

사용자 승인으로 기존 최신 분석을 기록한다.09:27 android-performance-correction1_01 ZIP132,910바이트/SHA cf89bd40…d9fe26과4entry를 직접 열어 확인했다. 앱 원문1,036,779바이트/SHA d6626458…b3d0cf는 회수 remote 전후와 local이 일치했다. 앱 footer는 미완성이지만 해당 Host/Client 경기 END가 존재하므로 앱 종료 완전성과 경기 분석 가능성을 구분한다. 원시 본문/전체 identity는 문서에 복제하지 않는다.

movement-direction-v1 route/step/window는 각16개다. trigger는 initial3/route-scope-phase6/geometric-corner7, window 완전11/partial5다. 실제 pose39호출 중 각도 계산 가능38·정지1unknown이며 stageAccepted39/throwfalse39다.16표본 모두 prev→current와 존재하는 current→next가 인접하고 desired는 current tile센터와 일치했다(최대 float 잔차 약6×10^-7world). 이는 해당 입력 표본 정합이며 전체 BFS/최단성·goal 의도 정상 판정이 아니다.

sample4 Rabbit은 prev(6,10)→current(5,10), target(3.75,-10.825)에 대해 Root(4.5,-11.258)가 이전 중심인 상태에서 실제 방향 오차51.0168195→44.1441458→37.141605°였다. geometric7의 첫 각차는21.0120641~51.6305453°다. sample1 Rabbit 출발은 몸120°·목표+Z에서9.029° 회전 후 몸 방향으로 전진해 실제 진행 대 목표각110.970998°와 거리0.8660002→0.888377→0.908818world의 초기 멀어짐을 보였다. 회전 중 몸 방향 전진 메커니즘을 이번 실기에서 확인했으며 잘못된 tile 선택 증거는 없었다. 사용자가 본 모든 장면의 원인까지 확정하지 않는다.

Host terminal은14,221frame(move13,960/align104)·centers413/maxCenterError 출력0·spatial477 planned=commit·repeat0/invalid0/dropped0·END EVIDENCE다. 감속 전체 active corner 집계는5,424frame/minRatio0.654909/maxCross0.236436/normalStops0/safety53이며 선정7표본과 같은 집합이 아니다. Client는 replicated333/invalid0·presentation91,598/attackStarts73이며 Client 서버 path는 설계상 unknown이다. 이 수치로 연속 궤적·모든 타일/유닛·전체 증상 해결 PASS를 선언하지 않는다.

역사 대조: Legacy는 Vector3.Lerp(fromPos,toPos,t)로 중심 간 직선 위치를 만들고 몸 회전은 Quaternion.RotateTowards로 분리한다. 현재 Reducer는 TrajectoryDirection/CandidatePosition을 결합한다. 핵심 planner는65ee51fa(2026-08-18),center capture는7b4a8927(08-25)에서 도입됐다. Game.unity의 _serverMovementPipelineMode=1은65ee51fa부터 있으며 최적화 직전070ddcaf^/070ddcaf(10/27 KST)와 현재가 같다. 코드 default Legacy와 씬 override1을 구분한다.070ddcaf^→HEAD의 Contracts/Authority/Game.unity committed diff0, Movement의 committed 변화는 ProfilerMarker이며 최근 dirty 감속/추격/진단은 별도다.

따라서 최적화 작업이 Legacy→Reducer를 켜서 원인이 됐다는 가설은 이력 근거가 없다. 다만 당시 실제 APK/실행 mode·체감 발생 시점은 미확정이므로 원래부터 사용자 증상이 있었다고 단정하지 않는다. 확인된 기계적 경로와 역사 근거만 기록하며 신규 교정 설계 승인·제품 변경·빌드·merge/push는 이번 분석에서0이다. 다음 수정은 사용자와 의도/범위를 합의한 뒤 별도 계획으로 결정한다.

회전 기반 전진의 계산 대조(2026-11-18): sample4는60.0006954−270×0.0332739316=51.016733868°로 실측51.0168195°와 대응하고, sample1은120.000008−270×0.0334406942=110.971020566°로 실측110.970998°와 대응한다. 현재 몸 회전 한도 후 그 방향으로 전진한다는 해석을 직접 계산으로 뒷받침하지만 사용자 전체 관측 장면·발생 시점까지 확정하는 것은 아니다.

### 42.6 옆걸음 없는 연속 코너 궤적의 사용자 의도

사용자는 몸이 회전하는 동안 옆걸음하는 모습을 최우선으로 금지하며, 위치만 중심 간 직선으로 보내고 몸 회전을 분리하는 안을 거부했다. 그 후속 후보는 철회·미채택이다. 과거 Legacy 구조 설명은 역사 분석으로 보존하지만 새 교정 방향으로 채택하지 않는다.

새 승인 방향은 코너에 앞서 회전을 준비하고 중심을 정확히 통과하는 연속 trajectory다. 옆걸음·정상 코너 강제 정지·급회전·중심 cut/스냅을 하지 않는 것이 목적이다. 사용자가 pre-turn·중심 통과·몸/이동 방향 일치 방향을 승인했고 메인이1차안을 공유한 뒤 전문 담당에게 구현을 지시했다. 제품4파일 연결과 아래 소스 제한 검증까지 완료했고 최종 SDK/Tools/hash 영수증까지 확인해 구현·제한 검증을 마감했다. 추가 승인 대기 상태가 아니다. 기존 “중심 소비 전 next 입력 금지” 계약과의 충돌을 검토해야 하므로 규칙을 임의로 바꾸지 않는다. 아래1차 설계 원리로 구현하며 최종 파일·실제 검증/수용 결과는 구현 결과를 받은 뒤 정합 갱신한다. 이번에는 기존 Task 기록만 수정하며 새 파일·제품 코드·Git 변경은 없다.

승인된 연속 궤적1차안: 공유 edge 중점을 강제하던1차안은 속도 급감으로 미채택했다. 현행은 whole center-to-center Bezier(handle=minL/3, Hermite 도함수 minL)를 먼저 구성하고 실제 Hex edge 교차 u를 역산해 분할한다. edge 교차점은 게임 checkpoint가 아니며 중심 공통 접선·동일 도함수 길이로 C1 연속을 준비하고 직선은 직선으로 유지한다. 단순 lookahead blend로 중심을 cut하지 않는다. 몸은 실제 Root→candidate chord 방향과 일치시키고270×dt 이내 회전으로 수용 가능한 진행량만 채택한다. curve의 진행 하위 구간 전체 안전 검사·거리 기반 진행·trial rollback·scope 원자 commit을 유지하며 기존 감속의 중복 적용 제거는 전문 담당이 설계/구현 중이다.

수용 범위는 빈 통로의 정상60° 코너 비정지·중심 정확 통과·급회전/옆걸음 없음이다. 초기 heading 불일치·150° 반전·차단 fallback은 정상 코너와 분리한다. 실제 결과는 아직 미검증으로 전체 해결/PASS를 쓰지 않는다. 기존 중심 소비 전 next 입력 금지 항목은 사용자 승인 pre-turn 준비 curve만 허용하도록 제한 정합이 필요하지만 승인 뒤 규칙1/3·동일 충돌2문장만 한정 정합했고 값·다른 계약은 바꾸지 않았다. 중심 소비·도메인 공간 commit·공격 권위·Client 서버 복제는 보존해야 한다.

구체화된 설계(구현 검증 미완): 두 half의 control point를 각각 해당 walkable Hex 안에 두어 convex hull로 curve 전체 통로 안전을 확보하는 안이다. 실제 frame chord는 기존 spatial sweep으로 별도 검증한다. 실제 진행 subcurve control-polygon 길이 상한과 local curvature bound로 arc budget을 보수적으로 제한하고 chord거리/270×dt를 별도 검사한다.32bin/고정 반복 캐시·읽기 전용 trial 후 최종 Root commit 뒤 u를 저장한다. Domain 예외까지 전체 rollback을 보장한다고 확대하지 않는다. geometry cache와 진행 u를 분리해 trial 복사 후 최종 commit에서만 저장하며 정상 frame마다 배열 생성·전체 path 검사를 하지 않는다. curve에는 기존 감속을 중복 적용하지 않고 fallback에는 기존 처리를 유지한다.

제품 범위는 Contracts/Authority/Observer와 UnitView.Initialize의 ResetCenterCurveRuntime1줄을 포함한4파일이며 Movement는 바꾸지 않는다.120° 초기 heading 불일치·150° 반전·차단·공격 전환 안전 처리는 유지한다. 초기 계획 뒤 제품 연결·소스 제한 검증으로 진행했으며 실제 게임 PASS는 아니고 승인된 규칙1/3과 같은 충돌2문장만 아래 근거로 한정 정합 수정했고 통합 검증 결과는 대기한다.

규칙 정합 이력(2026-11-18): 사용자의 pre-turn·중심 정확 통과·몸/이동 일치 승인에 따라 Units U-MOV-ALIGN1/3 및 상단·이동규칙2의 같은 충돌 문장만 제자리 갱신했다.2의270°/s·몸/실제변위1°와4~11 안전/공격 계약·StatsReference 값은 유지했다. 규칙에는 행동 원칙만 두고 고급 구현은 이 Task에 남긴다. 순수 정상32경로와 실제 메서드 추출 통합 fixture를 통과했으나 MonoBehaviour/NGO·실기/시각 수용은 미확인으로 전체 PASS가 아니다.

변경 전 원문 보존:

> incoming 선분은 현재 중심까지 완료하고, 다음 outgoing 선분의 첫 frame부터 이동과 회전을 함께 적용한다.

> 정상 코너는 중심을 소비한 다음 outgoing 선분에서 이동 중 점진 회전하고, 안전하게 이동 선회할 수 없는 경우에만 정지 정렬 후 진행한다.

> 1. 서버는 현재 위치와 현재 경유 중심으로 이번 틱의 `DesiredMoveDirection`을 계산한다. 기본 A*의 위치 trajectory는 현재 checkpoint까지 center-to-center로 진행한다. 다음 경유 중심은 현재 중심을 실제로 소비한 다음 선분부터 방향 입력이 되며, look-ahead를 현재 선분 위치에 섞어 중심을 자르거나 되돌아오는 곡선을 만들지 않는다.

> 3. 정상 코너에서는 현재 중심을 소비한 다음 frame부터 다음 경로 방향으로 이동과 회전을 함께 적용한다. 타일 경계나 waypoint 변경만을 이유로 속도를 0으로 만들지 않으며, 60° 전환은 outgoing 선분에서 이동 중 점진 회전한다. 단, 150° 반전 또는 10°/15° 방향 안전을 이동 중 만족할 수 없는 코너에서는 `AlignToMove`가 우선한다.

제품 연결·소스 제한 검증 상태(2026-11-18):4파일 연결 완료 후 최종 순수32경로/5,820검사에서 zero/center/turn/arc 오류0, 실제 TryPrepare/Reset/curve Resolve 추출38검사와 float 끝점 경계5검사를 통과했다. walkability는 stub이고 실제 MonoBehaviour/NGO/게임 실행이 아니다. trial 상태 불변·accepted u 재사용·scope/revision/owner reset·sliced index1·blocked 후 revision 재개·flip/hull/unsafe 축소·freeze/reverse를 확인한 범위다. SDK/Tools/hash 최종 영수증은 아래 범위로 확인했다.

독립 검토에서 Root==floatEnd/u0.99999999가 valid true/reached false/distance0으로 남는 문제를 실제 소스로 재현해 수정했다. Before 원문은 EndpointBoundary.Before.txt에 보존한다. 최종은 이미 Root==floatEnd일 때 zero 완료하고, 처음 floatEnd 후보를 사용할 때는 남은 arc budget 충족 시에만 u1/완료를 허용한다.

local profile은 코너 감속을 없애는 안이 아니다. 같은 S경로 명목2.28624world/s 대비 최저/진입1.275107world/s이며, profile을 끈 통제 variant의 비끝점 순간 속도0.477213world/s 급감과 비교해 비끝점 연속 frame 최대 감속폭1.782588→0.440294world/s로 완화했다. 변화는 여전히 남고 실제 자연스러움은 미검증이다. CPU 개선·전체 속도 보존·체감 PASS로 쓰지 않는다. 추가 튜닝 없이 최종 freeze했고 이후 사용자 실기 평가는 아래와 같이 일부 개선이나 부족이며 전체 runtime/visual 수용은 미완이다.

최종 구현·제한 검증 마감: center-curve FinalVerificationReceipt(SHA EEAABECF…7ED2FB5),FinalSourceHashes/FinalSourceIdentity/FinalSource.diff를 확인했다.4파일 최종 SHA는 Contracts1C1898B8…EBE49C1·Authority4FB92D0C…4529687·Observer70EA1017…59E127B·UnitViewABDF0A00…9C4016D다. 실제 pure block/Authority helper fixture exact·Reducer 원문 exact·Movement byte exact·UnitView Initialize Reset1호출 외 exact를 확인한 범위다.

Editor/Release358source 누락0·오류0(기존 경고24/31)이며 최종 영수증의 정확4파일 필터 기존 경고는 Release의 UnitView attackCrossFadeApplied(CS0219)와 _spatialRecoverablePending(CS0414)2개·신규0이다. 인계의1개 대신 영수증 원문2개로 대조했다. 이전3파일만의 변경 경고0을4파일 전체로 확대하지 않는다. Tools 오류/경고0이며 같은 runtime SHA7FEB4F99…D8E088A를 참조했다. pure32경로/5,820검사에서 float Root/yaw·normal zero0·중심 exact·270초과0, 몸/chord 최대 오차 약9.39×10^-6°,hull 잔차1.50×10^-6world(기존 tolerance10^-5 안),arc 잔차 최대3.11×10^-15를 확인했다. 무한 정밀도0이나 모든 지형/속도 보증은 아니다.

실제 추출 연결38·끝점5·진단91(최대631bytes)·terminal32(최대968bytes/reset true)도 통과했다. S명목2.28624world/s의60/30fps최저는1.275107/1.274192이며 비끝점 frame 감속폭0.4403world/s가 남는다. 독립 source 리뷰에서 끝점 문제 수정과 다른 확정 연결 결함 미발견을 확인했지만 해당 최종 freeze 검증 시점에는 실제 MonoBehaviour/NGO·Acquire/HP/RPC·Unity/Android 실기·빌드 실행0이었다. 이후 사용자 실기는 아래 회차로 분리한다. 따라서4파일 구현·소스 제한 검증 완료/실제 시각 자연스러움 미확인으로 분류하고 full runtime/전체25종 PASS는 선언하지 않는다.

### 42.7 실기 일부 개선과 생산 초기 방향 지연 재점검

사용자는 조금 개선됐지만 아직 부족하고, 남은 현상은 “중심을 지나 크게 돌아감”이라고 구체화했다. 감속 불만으로 해석하지 않는다. 별도로 생산 직후 이동 방향과 몸 방향이 다르면 제자리 회전으로 늦어지는 점도 개선해 달라고 보고했다. 현행은 일반 코너/정지 체감 수용 미완·생성 방향 개선 필요다. 이번 결과를 전체 실기 PASS로 쓰지 않는다. 생산 초기4파일과 기존 진단2파일 구현은 완료했고2026-11-23 재개 시점 소스 제한 검증까지 완료했고 실기를 기다린다. 큰 회전·terminal FAIL2 원인 분석과 실제 체감 수용은 별도 미완이다.

메인은14:41 android-performance-correction_02 ZIP379,685바이트/SHA d309a922…2715d64·6entry와 앞14:23 _01 ZIP198,877바이트/SHA1960719c…4f4d5c4·4entry 전체 일치를 확인했다. 최신 ZIP에 옛 Android session이 재포함되어 같은 경기를 중복 계산하지 않는다. 빈 setup FAIL과 실제 경기 run도 분리한다.

실제 Host07954b5a…14:02~04는 frames10,081/curve7,992/align699/centers432·maxCenterError출력0/repeat0/fatal0·END EVIDENCE이며 두 ZIP에 중복 포함됐다. 새 Host9cd1b8f3…14:38~39는 frames11,658/curve10,184/align843/centers540·maxCenterError출력0/repeat0/fatal2·END FAIL이다. 실제 adapter 거부2건은 별도 원인 분석 중이며 Client28c27dc5…는 다른 run이다. checkpoint 출력0이 전체 연속 궤적/육안 증상 해결을 뜻하지 않는다.

새 run 생산 초기 Rabbit unit0은 몸120°→경로300°의 약180° 불일치를 보였다. frame22200부터22220의 전진 관측까지 약0.667초이나 정확한 최초 전진은 window 분석 중으로 지연의 상한 근처 관측만으로 해석한다. 앞079 run의 unit0은 초기120°→60° 약60° 불일치에서도 이동0 정렬이 관측됐다. 전문 담당의 아래 spawn 초기 route pose안 구현·제한 검증은 완료했고 별도 담당은 fatal2와 큰 회전 경로를 조사한다. 구현·제한 검증 완료와 원인 전체 확정/실기 PASS를 구분한다. 새 Task/규칙·제품 코드·Git 변경은 없다.

생산 초기 정지 교정1차안: 현행 CompleteProductionSpawnUnit→기본Facing E→OnSpawn→Factory Instantiate identity→NGO Spawn→Initialize→OnProduced→Ticker 실제 경로 선택의 순서에서 초기 Root120°/desired300°의 지연이 발생한다. 승인안은 SpawnUnit의 호환 optional beforePublish callback으로 실제 UnitData 등록 뒤 OnSpawn 전에 생산 context의 실제 rally/혼잡/BFS 경로를1회 준비하고 initialFacing을 정하는 것이다. Ticker OnProduced는 같은 owned route를 소비해 정상 경로의 중복 검색을 추가하지 않는다. Factory Instantiate 때부터 Facing pose를 사용해 NGO Spawn 전에 확정하며 Client Root writer/RPC 서명은 바꾸지 않는다.

예정 제품은 UnitSpawnUseCase/UnitProductionUseCase/ProductionTicker/UnitFactory4파일이다. 준비 결과는 짧은 동기 handoff로 소유하고 예외/불일치/게임 종료에 정리한다. prepare 실패는 기존 spawn+안전 정렬로 복귀하며 callback 범위만 격리해 ghost unit을 막고 전체 예외를 삼키지 않는다. 기존 curve4파일은 추가 튜닝하지 않는다. “중심 지난 큰 회전”은 새 curve active 자체인지 이전 몸 방향 전진 fallback/route 교체 partial 처리인지 미확정으로 별도 조사하며 terminal2건 미완도 유지한다. 현재6파일 구현·제한 검증은 완료했으며 이안이 큰 회전까지 해결했다고 하지 않는다.

재개 상태(2026-11-23): 생산 초기4파일 및 initial-start/walkable A* curve·fallback peak2case END 관측을 위한 기존 진단2파일이 반영됐다. 기존 trajectory 식은 추가 수정하지 않았다. private spawn-initial-facing/Verification/Spawn.Results.txt의 source CLR8assert PASS와 Editor/Release358source 오류0(기존 경고24/31)을 확인했다. 소스/독립 제한 검증을11/23 결과로 마감했으며 통합 FinalReceipt 경로/SHA까지 확인했다.  실제 실기·빌드는 아직 미실행이다. 원래 “중심 지난 큰 회전”과 terminal FAIL2는 미완으로 유지하고 이번 구현을 사용자 증상 해결/PASS로 쓰지 않는다. 기존 기록은 보존하고 새 Task/메모리·제품 코드·Git 작업은 없다.

11/23 최종 소스 제한 검증: FinalSourceHashes6파일과 Verification/VerificationReceipt·FinalSourceIdentity를 대조했다. 공백 정리 후 scope를 재추출해 Spawn8/Ticker19(앞13 포함, 합산하지 않음)/Scope9 actual-source seam 검사, Factory6yaw 및 Instantiate pose의 NGO Spawn 이전 적용 source를 확인했다. 초기 curve는 관측 Root와 경로 첫3점만 제어한 비교로 old yaw120에서 held19·20번째 frame 이동→new yaw300에서 held0·첫 frame 이동, 첫 거리0.05772252584world 동일이다. 전체21점·native/NGO/BFS 완전 경로·UnitView 생명주기 실행이 아니다.

6파일 SDK Editor/Release358source 오류0·누락0(기존 경고24/31), 이번6파일 경고0·Tools 오류/경고0이다. 진단104검사·peak 최대519UTF8bytes/direction631bytes·terminal32줄/최대968/reset true를 확인했다. initial-start는 최초 command 각도0이어도 기존 타입별1budget을 쓰며 peak는 일반 walkable A*/PendingRepath의 curve/fallback 각1case를 END에만 남긴다. 큰 회전 해결 기능이 아니라 원인 분류 진단이다. Release Authority의 Dev 블록 제거 text는 Before와 동일하며 IL/binary 불변 증명은 아니다.

현행은 생산4+진단2파일 구현·소스 제한 검증 완료/신규 실기·빌드0이다. trajectory 수식·속도 추가 튜닝은 없으며 “중심 지난 큰 회전” 원인unknown·terminal FAIL2는 미완이다. 실제 자연스러움·NGO/생산 생명주기 전체 수용은 별도 사용자 검증이 필요하다. 통합 FinalReceipt까지 확인했으며 새 문서·코드·Git 작업은 없다.

11/23 통합 영수증 마감: private spawn-initial-facing-20261118/FinalReceipt.json을 직접 읽고 SHA CFBCD3F8832C00B08B6801CE48E3DF25F0EA4DAC6DA1C3FA74CD13F49F1E8D0E와 대조했다. 기존 소스 제한 검증·SDK/Tools 결과와 일치하며6파일 freeze다. 다음 실기 목적은 생산 초기 정렬 지연 확인과 “중심 후 큰 회전”의 curve/fallback 분류 증거 확보이지 큰 회전 교정 완료를 입증한 상태가 아니다. native/실기·빌드0과 terminal FAIL2 미해결은 유지한다.

### 42.8 11/23 실기 생산 초기 방향과 잔여 코너 이탈

사용자 승인으로22:58 android-performance-correction_01 ZIP184,674바이트/SHA177324a3…8d78c87·4entry size/hash 검증 자료를 기록한다. 실제 경기 Host b5d02354…/Client27e64ba0…와 관측0 setup Host/Client FAIL은 분리한다. 이번 로그를 새 제품 교정·빌드·Git 실행으로 확대하지 않는다.

initial-start 실기2표본은 type10/unit0 yaw180→180·bodyTargetAngle0·delta(0,-0.0577669144),Rabbit type25/unit2 yaw0→1.754432·bodyTargetAngle0·delta(0.00176882744,0.0577478409)에서 firstMoveAccepted였다. 생산 초기 교정이 이 두 표본에서 발현한 근거이며 모든 생산/유닛 전체 PASS는 아니다. 앞 source CLR held19→0의 첫3점 통제와 별도 증거다.

Host9,000frame·align478/move8,365·centers403/maxCenterError출력0, fatal/adapter/invalid/repeat/drop0·END EVIDENCE와 Client replicated396/invalid0·EVIDENCE를 확인했다. 앞 terminal FAIL2는 이번 경기 미재현이지 해당 원인을 고쳤다는 뜻이 아니다.

END의 peak-route/pose4행을 확보했다. curve active는 unit27/frame33098·index3/count11·path0(3,8),prev(5,6),target(5,5),next(5,4)·cmd1/seg3/rev3에서 cross0.113194704world였다. Root before(3.85776877,-7.0268507)→after(3.8631947,-6.98651743),yaw15.477→7.662다. fallback은 unit31/frame33093·index2/count19·path0(7,13),prev(6,13),target(6,12),next(7,11)·cmd1/seg2/rev2·reason curve-build-or-crossing에서 cross0.100157261world였다. before(4.391813,-13.4117622)→after(4.39984274,-13.3545084),yaw358.971→7.984다. fallback의 before cross0.108187보다 after0.100157이 작아 해당 frame은 이미 있던 offset을 줄이는 중이며 fallback이 이탈을 처음 만들었다고 단정하지 않는다.

cross는 prev→target 중심 직선까지 수직거리로 tile 중심 거리·mesh 발 위치가 아니다. 일반 walkable A* 조건의 경기 최대 curve/fallback2case만 기록한 자료다. 새 curve active에서도 이탈이 관측돼 큰 회전 전체 해결은 아니다. 두 담당이 source를 분석 중이며 중간 replan의 actualStart 재생성 가능성 등은 후보일 뿐 확정 원인은 아니다. 생산 초기 focused 관측과 남은 코너 원인 미확정을 구분하고 추가 제품 변경·빌드·Git 작업은0이다.

최종 좁힘·조건부 반증(11/23): 메인/독립 담당 계산은 active peak의 index3/path0(3,8)/prev path2(5,6)가 정상 인접 경로라면 path1=(4,7)이 강제됨을 확인했다. prev(5,6)→target(5,5)→next(5,4),시작 bisector약30°/끝0°·L약0.866의 정상 center-start C1 곡선은 측방 control0.144334921/max cross0.0641488533world다. 실제0.113194704는 약1.7645631배이므로 이 정상 center-start 곡선만으로 설명하기 부족하다. 인접 경로·좌표·기록 의미가 맞다는 전제의 반증이며 모든 원인 확정은 아니다.

확인 source 판본은 Contracts1C1898…49C1/Authority8E9673…BBDF0이다. TryPrepare는 path reference/index/command/segment/navigation revision 불일치 시 actualStart에서 Build/u0으로 재구성하되 기존 center 기반 tangent를 유지한다. unit27은 sampled frame32897(cmd1/seg1/rev0/count23)→peak33098(cmd1/seg3/rev3/count11)로 path/scope가 바뀌었다. 따라서 중간 actualStart 재구성이 이탈 확대의 유력 후보이나 생성 frame/Start/u·이전 path는 관측하지 못해 원인은 미확정이다. peak before를 그대로 새 Start에 넣으면 projection 단조 조건에서 거부되므로 해당 peak frame에 재생성됐다고 주장하지 않는다.

후속 추천은 기하가 같으면 curve 진행을 보존하면서 안전을 재검증하고 실제 경로 교체는 현재 pose 접선으로 재접속하는 방안을 검토하는 것이다. 설계 제안만이며 구현 승인/제품 변경은 아직 없다. 생산 초기2표본 firstMove 수용·큰 회전 미완·이전 terminal2 이번 미재현의 기존 판정은 유지한다.
