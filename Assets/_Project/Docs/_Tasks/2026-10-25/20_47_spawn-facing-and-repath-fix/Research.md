# 생산 대기 해제 후 방향과 전투 복귀 이동 문제 조사

생산 건물의 출구가 막혔다가 열릴 때 새 유닛의 방향이 이동 중에도 어긋난다는 신고와, 별도 멀티플레이 로그에서 확인된 전투 후 이동 복귀 실패를 먼저 바로잡는다. 두 현상을 같은 원인으로 묶지 않고 현재 코드·재현·로그를 대조해 필요한 교정이나 제한된 관측만 추가한다.

- 시작: 2026-10-25 20:47 KST. 작성 전 `TZ=Asia/Seoul date`로 확인했다.
- 사용자 결정: 「최적화 우선 보류, 이런 문제들부터 바로잡자」. 기존 Task 문서 작성·자율 진행 승인을 유지한다.
- 기준 소스: `work / 572d94af28cb8ee1f8142c49bea31a6f72e33fc5`.
- 현재 상태(2026-10-26 KST): 원격 main의`f367cb52`까지 병합한 현재 HEAD는`562888d2`다.10절의 제한 진단·집중 검사·Testcase16경로를 보존·복원했고 새 투사체 경로에 맞춰 서버 요청 검사의 무전투/비행 없음 전제만 보강했다(12절). 새 source·Task 추가분은 미커밋·미푸시이며 실제 새 Unity 컴파일/검사·기기 실행은 아직 없다. 기존 사용자 컴파일/일반 플레이 수용과 이번 싱글 방향 오류 미재현, 원인 미확정은 유지한다.
- 보존: 앞선 구현8b9605af와 종료 후 가드 교정의 당시 기록은 유지한다. 이번 진단 준비는 제품 에셋·기존meta·Profiler·원시 로그를 직접 변경하지 않으며, 별도 main 병합으로 들어온 VFX/서버 투사체·에셋 변경은12절에 구분했다.9절 초기 동기화,11절의2750decd 병합은 당시 기록이고 최신 병합 상태는12절을 따른다.

## 1. 문제를 분리한 현재 판정

| 항목 | 확인한 사실 | 미확인·다음 조치 |
|---|---|---|
| 싱글 생산 재개 후 방향 어긋남 | 사용자 관찰: Pistoleer에서 한 번, 생산 건물 주변이 막혀 대기하다 주변 건물 파괴 뒤 생산됐고 이동 중에도 계속 방향이 어긋남 | 다른 유닛·팀 색상·정확 시각 미확인. 현재 로그에 해당 spawn yaw/Root와 보이는 mesh 방향을 함께 판정할 증거가 없어 원인 미확정. 단순 초기 회전 지연으로 종결하지 않음 |
| 멀티 전투 후 복귀 실패 | 저장 Editor 로그1285/1308행의 unit54/64: `post-combat-no-safe-forward-center`, Unreachable, 같은 시작/root(5,16)→goal(6,16),환경revision6. 해당 run의 MOVE 종료는 adapterFailures2로 FAIL | 출구 차단 후 생산된 Pistoleer와 같은 사건이라는 근거 없음. 복귀 resolver·차단 판정·허용 fallback의 좁은 교정안 검토 |
| 별도 관측부족 종료 | 다른 run은 frames/client samples0인 상태에서 FAIL 종료 | 실제 이동 결함이나 성공으로 자동 분류하지 않음. 통계/관측 경계 유지 |
| Lobby 참가404 | 잘못되거나 이미 없는 코드에 대한 not found 운영 오류 한 사건 | 기존 catch→오류 안내→입력 복구 코드와 로그10533~10547의 팝업 닫힘·오버레이 해제·재입력 뒤 참가 성공 흐름을 읽기 확인했다. 사용자 입력이 실제로 틀렸는지는 미확인, 별도 교정 대상으로 확정하지 않고 정책/서버 처리 유지 |

## 2. 기존 근거와 원인 한계

원시 로그·경기별 판정의 원본은 [기존 Research16.59](../../2026-10-23/14_08_architecture-refactoring-audit/Research.md#1659-사용자-테스트-보고와-저장-로그의-한정-검토2026-10-25-kst)다. 메인이 private Release 압축본의 CRC·SHA를 확인했고 QA 전체 검토와 메인/문서 담당의 한정 원문 대조를 별도로 기록했다. 원시 자료는 임시 작업 경로에 두며 이번 Task가 원본을 수정·업로드·이동하지 않는다. 로그의 실제 빌드 SHA는 미기록이므로 현재 리팩토링 세대의 회귀로 단정할 수 없다.

메인·전문 코드 검토에서는 기본 E 방향·생성 방향 전달·싱글 Legacy 이동·점진 회전·생산 차단 시 return 구조가 리팩토링 전에도 존재했다. 공통 이동 소유권·공격 회전 억제·행동 종료 시 반환도 있어 상시 두 writer 충돌을 원인으로 확정할 수 없다. Pistoleer 양팀의 회전속도/RootMotion/controller 기본값만으로 신고 순간의 실제 Root/mesh 방향은 알 수 없다. 따라서 필요한 경우 이벤트 수와 수명을 제한한 개발용 관측으로 원인을 구분한다.

## 3. 보존할 계약

- [유닛 규칙](../../../GameSystemRules/GameSystemRules_Units.md)의 U-MOV-ALIGN·U-MOV-REPATH 및 이동 규칙45: 안전한 현재 pose에서 이동 재개, 단일 권위 writer, 건물 차단 시 상태·재경로와 진행 이력 유지.
- [전투 동기화](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md)의 NET-AUTH-001·NET-ROOT-001·NET-ROOT-002: 서버 판정과 Simulation Root/Visual Root 책임 분리.
- 위치 스냅·피해/사거리·공격 타이밍·상시 네트워크 주기·전달 방식·Animator/에셋 수치를 임의로 바꾸지 않는다.
- 개발 관측을 추가하더라도 root와 mesh를 구별하고 로그의 생략/미관측을 PASS로 만들지 않는다. VFX 외부 브랜치와 AI 생산시간 배율은 이번 교정 범위에 포함하지 않는다.

## 4. 진행과 검증 상태

| 단계 | 현재 상태 |
|---|---|
| 사용자 신고·저장 로그 확인 | 위 범위로 확인 |
| 정확 source 교정/진단 설계 | Plan1.1/1.2의 메인 전체 검토/GO 뒤 구현 반영 |
| 구현·정적 대조 | 전문 원문 역복원/기존meta 대조·메인 독립 대조·QA 정적 검토 완료 |
| Unity 컴파일·기존 회귀 검사 | 이번 캡처의 실제 Git SHA/별도 컴파일·기존 검사 결과는 미확인 |
| 싱글 막힘→해제→생산→이동 방향 재현 | 새 SINGLE에서 제한 진단 수집, 사용자 이번 오류 미재현. 지속 증상 완전 해결은 미확정(9절) |
| 멀티 전투 종료 복귀/역할별 검증 | 새 두 Android Host run의 adapter0, 별도 종료 후 handoff 실패2. 전경기 PASS로 확대하지 않음(8절) |

[Profiler Task](../19_44_android-30fps-profiling/Research.md)는 사용자 결정으로 보류했다. marker5개는 코드 전 제안일 뿐 적용하지 않았으며 30 FPS 목표나 품질 설정도 변경하지 않는다. 이번 기능 문제와 별도 성능 최적화를 섞지 않는다.

## 5. 제거 완료 통지의 source 순서 결함 후보

문서 담당은 RemoveBuilding의 실제 상태 복구/인덱스 삭제 뒤 반환, FlowFieldService의 Died 구독, Bootstrap의 Died 즉시 재경로와 GameEvents의 기존 소비자 안내를 직접 읽었다. 전문은 Died 콜백 안의 이전 차단 상태로 캐시가 재생성되고 제거 완료 후 갱신이 없는 순서를 제안의 근거로 제시했다. 이 source 문제는 Plan1.2의 새 Removed 이벤트로 한정 교정했다. 아래7절의 구현·정적 확인과 실제 실행 여부를 구분한다. 로그에서 캐시 재생성 순간을 관측한 것은 아니므로 unit54/64의 모든 원인을 확정한 것으로 쓰지 않는다.

## 6. 싱글의 좁은 관측안과 구분

전문 정확안 `/tmp/hexiege-solo-spawn-facing/design.json`을 문서 담당이 직접 읽었다. 생산에서 실제 차단됐던 첫 싱글 Pistoleer를 동일 UnitData 참조로 연결하고, View가 랠리/기본 이동을 받기 직전부터 최대4초/18줄로 Root·Visual/Animator/Hips 축과 실제 이동 방향·소유권을 관측한다. source3파일만 개발 guard로 추가하며 방향을 고치는 코드는 아직 없다. 몸체 raw 축은 시각적 정면의 보정값이 아니고 한 개체/4초 밖의 사건을 포괄하지 않는다. 결과를 보고 실제 수정 여부를 정한다.

## 7. 구현·정적 대조 결과(2026-10-26 KST)

두 안은 메인의 전체 계획 검토/GO 뒤 적용했다. 멀티 source4파일은 실제 건물 제거 완료 뒤 OnBuildingRemoved를 발행하고 FlowField/즉시 재경로 구독을 이 채널로 옮겼다. 기존 사망 알림의 등록 조회·환불·Factory·게임 종료 순서는 보존했다. 기존 전체 검사에는 공개 UseCase/API를 사용하는 건물 제거 수명 회귀를 추가했다. Play/네트워크/경기 서비스가 활성 상태이면 fixture를 거부하므로 실행은 Play 종료 상태에서 해야 한다. 상태 격리를 무시한 skip을 PASS로 만들지 않는다.

싱글3파일은 첫 blocked→released Pistoleer를 같은 UnitData 참조로 연결하는 개발 진단만 추가했다. `[UAS-SPAWN-FACING]`의 begin/sample/end는 최대4초·0.25초 간격·sample≤16/총≤18줄이다. 실제 시간 경계에서는16보다 적은 샘플이 나올 수 있다. Release에는 추가 진단이 선택되지 않는다. 방향 보정·회전속도·위치/Animator 변경은 없으며 지속 방향 문제를 고쳤다고 기록하지 않는다. Root/Visual/Animator/Hips 원시 축과 실제 이동을 나눠 영상/정상 동종 사례와 해석해야 한다.

| 증거 | 확인 범위와 한계 |
|---|---|
| 멀티 전문 정적 receipt | `/tmp/hexiege-building-removal-fix/static-review.json`: 승인된5source 변환 외 원문 bytes·기존meta5/UnitCombat·Movement·Observer 보존. 준비한 fixture의 RED/GREEN은 미실행 |
| 싱글 전문 정적 receipt | `/tmp/hexiege-solo-spawn-facing/static-review.json`: 3source 역복원 bytes/기존meta·Release 선택 nonblank 원문 동일. 줄 예산18/기간4초와 예시 최대폭601바이트(envelope120 포함)는 정적 확인이며 실제기기 로그 측정 아님 |
| 메인 독립·QA 정적 검토 | `/tmp/hexiege-spawn-repath-main-independent.json`을 문서 담당도 직접 읽었다. 정확8source/기존meta8 보존, 진단3source의 Android·Standalone Release 선택 nonblank 원문 동일과 기존 UnitCombat/Movement/Observer 무변경을 메인이 확인했다. 메인이 전달한 QA 정적 검토는 blocker 없음이며 실제 컴파일/재현 PASS는 아니다. |
| Unity 컴파일·기존 suite·Android 재현 | 미실행. 멀티 캐시 교정의 실제 경기 효과와 싱글 신고 원인은 아직 미확인 |

Ticker/Factory/View가 아직 준비되지 않았거나 identity가 맞지 않으면 begin 로그가0일 수 있다. 이 진단은 미초기화나 모든 생산을 포착하는 장치가 아니며 로그가 없다는 사실을 정상 PASS로 판정하지 않는다.

source는 기존8 C#이며 신규 source/meta는 없다. 이번 변경은 기존 리팩토링 Task2+보류 Profiler 문서/meta6+새 교정 Task 문서/meta5+source8의21파일이다. 구현 마감 당시 커밋 전 snapshot은 `work / 572d94af` 기준 추가21파일 미커밋·미푸시 상태였다. 최초 작성일10-25와 당시 저장 로그는 원문으로 보존하고 이 결과만10-26 후속으로 기록한다. Profiler 최적화는 계속 보류이며 네트워크/GPU·30FPS 설정을 바꾸지 않았다.


## 8. re1 멀티 재검토 — 기존 복귀 실패와 종료 후 진입을 구분(2026-10-26 KST)

이 절의 source 무변경 문장은 로그 분석 마감 당시의 기록이다. 이후 사용자 후속 진행 지시에 따른 한 가드 교정은9절에 별도로 기록한다.

메인이 private Hexiege-Logs의 re1 asset675753338·추가 SINGLE asset675768842를 읽기 전용으로 받아 ZIP CRC·SHA를 확인했다. 이 절은 그중 re1 캡처의 분석이다. 자료는 `/tmp/hexiege-re1-log-review/raw/RuntimeLog.txt`와 `RuntimeLog_device.txt`, 집계 원본은 같은 폴더의 `main-scan.json`이다. 문서 담당도 원문 종료/실패 순서와 현재 코드의 OnGameEndHandler/OnUnitEnteredCombatHandler를 읽었다. ZIP 자체 확인은 원격 manifest나 실제 빌드 Git SHA 검증을 대신하지 않는다.

이 캡처는 Android Host+Editor Client의 두 멀티 run이며 같은 lobby/session 식별자를 재사용해도 시작 약20초와156초인 run을 구분했다. 기존 `post-combat-no-safe-forward-center`/이동 adapter 실패는 두 Host에서0이었다. ROOT는 양쪽 두 run의4종료 모두 PASS다. 이는 이번 관측 범위의 결과이며 이전 특정 지형 사례 전체나 모든 이동/공격의 통과를 뜻하지 않는다. 양 파일의 싱글/생산방향 진단0은 사용자가 먼저 멀티만 시험했다고 한 내용과 일치한다.

첫 Host run의 MOVE 종료는 별도 `combatPresentationHandoffFailures=2`로 FAIL이다. 기기 로그63430행03:05:25.795 포기→63441행25.808 전투 중지→63648/63742행 같은 frame5663의 NativeEpochClose(game-end)→63929/63953행 native-entry-not-applied→63936/63960행 provisional-apply handoff 실패가 연결된다. 대상 unit51/41은 Type1 **Assault**이며 Pistoleer가 아니다.65308행 summary는 adapterFailures0/handoff2,65342행 END FAIL이다. 현재 source는 게임 종료에서 `_combatStopped=true`를 세우지만 전투 진입 이벤트 handler는 spawn/server 가드 뒤 이 flag를 확인하지 않는다. 종료 후 남은 같은 frame의 전투 진입이 이미 닫힌 native owner로 연결되는 경계의 후속 교정 후보이며, 정상 진행 중의 이동 실패나 기존 adapter 문제로 재분류하지 않는다. 두 번째 Host run의 handoff는0이다.

이번에는 source를 고치거나 진단을 확대하지 않았다. 실제 빌드 SHA가 로그에 없어 전체8b9605af 동일 리비전이라고 확정하지 않고, 관측된 동작/오류와 현재 source 검토를 구분한다. Profiler 최적화는 계속 보류다.

### 추가 SINGLE — 제한 진단 수집과 사용자 한정 미재현

추가 SINGLE의 Editor 로그는 re1 Editor 전체10,251,817바이트를 **정확한 prefix**로 포함하고 새745,029바이트를 덧붙인 자료다. 문서 담당도 bytes로 비교했다. 새 물리줄27556~29481만 별도 싱글 증거로 읽었고, 앞부분의 멀티 실패를 새 사건으로 중복 집계하지 않았다.27557/27615행의 새 세션과27636~27640행03:14:53의 ObstacleOpen·AI Human-Tech/Easy 초기화가 있고, 추가 구간 ERROR는0이다. 원본과 분석 요약은 `/tmp/hexiege-single-retest-review/raw/RuntimeLog.txt`, `main-scan.json`, `spawn-facing-analysis.json`이다.

사용자 최신 답변은 **「이번에는 방향 오류가 보이지 않았음」**이다. 이는 이번 조건의 미재현 관찰로 수용하며 모든 상황의 방향 문제 해결이나 기존 지속 증상의 원인 확정을 뜻하지 않는다.

| 같은 개체의 기록 | 직접 확인한 결과·해석 |
|---|---|
| 27952행 begin | Unit11/Blue/Barracks7, blockedAttempts23810, 초기 RootYaw 약120°, Legacy. 실제 생산 대기 해제와 관측 연결이 작동했다. |
| 27954/27955행 이동 샘플 | age약0.253초는 실제 이동/목표 yaw0°에 Root/Visual/Mesh 약50.59°, age약0.505초는 세 값 모두0°로 정렬됐다. 기록된 이동 샘플은 이2개다. 지속 이동 오류의 재현 증거는 없다. |
| sample3~15 | 정지·Action 소유권·IntentNone의 공격 상태이며 Root/Visual yaw약55.12°. Mesh/Hips raw 축 차이를 이동 방향 오류로 세지 않는다. 기록된15샘플의 Root와 Visual yaw는 일치하지만 샘플 사이 모든 프레임/최종 픽셀을 증명하지 않는다. |
| 27992행 end와 주변 | age약3.992초/view-disabled,15samples로 종료했다. 같은 frame31048에서27993행 owner retire·27997행 해당unit HP0이 연결돼 사망/비활성화에 따른 정상 수명 종료로 읽는다. begin+sample15+end의17줄은18줄/4초 한도 안이며 format-rejected는0이다. |

개발용 계측의 실제 수집을 확인한 것이며 CPU/GPU/30 FPS 측정이나 source 전체 SHA 검증은 아니다. 이 관측에서는 공격 후 다시 걷기로 돌아가는 전환 전에 사망했다. 추가 source 관찰로 [Pistoleer_Attack.anim](../../../../Animations/Units/Pistoleer/Pistoleer_Attack.anim)의 빈 path Euler Y30→20→10→15→30 키와 [Walk.anim](../../../../Animations/Units/Pistoleer/Pistoleer_Walk.anim)의 Euler curve 없음, [controller](../../../../Animations/Units/Pistoleer/Pistoleer.controller)의 Walk WriteDefaultValues0을 문서 담당도 읽었다. 공격→걷기 때 mesh 회전이 남을 수 있다는 것은 아직 Unity에서 재현하지 않은 가설이며 이번 정지 공격의 축 차이나 기존 지속 증상의 원인으로 확정하지 않는다. 에셋/코드를 수정하지 않았고 무한 진단 확대도 하지 않았다.

현재 source는8b9605af이며 이번 변경은 기존 Task2문서의 후속 기록뿐이다. 새 source 수정·Profiler 구현·기기 재실행·원시 로그 변경은 없다. 남은 별도 항목은 종료 후 전투 진입 경계이며, 싱글은 이번 미재현 관찰과 기존 원인 미확정의 한계를 함께 유지한다.

## 9. main 동기화 확인과 종료 후 전투 진입 가드 교정(2026-10-26 KST)

사용자가 main 변경을 받은 뒤 충돌 없이 병합하고 계속 진행하도록 지시했다. 메인은 원격 main을 fetch하고 `origin/main=7c66e4c5`, `work=8b9605af`를 확인했으며, `git merge origin/main` 결과는 `Already up to date.`였다. 사용자는 새 변경이 없으면 넘어가도록 답했다. 실제 추가 병합분·충돌 해결·새 merge commit은 없고 기존 미커밋 Task 기록을 보존했다.

[Plan1.4](Plan.md#14-게임-종료-뒤-늦은-전투-진입-차단2026-10-26-kst-구현-전-계획)를 메인이 검토하고 GO한 뒤, 전문 담당이 [NetworkCombatController.cs](../../../../Scripts/Infrastructure/Network/NetworkCombatController.cs)의 `OnUnitEnteredCombatHandler` 첫 가드를 `if (!IsSpawned || !IsServer || _combatStopped) return;`로 바꾸고 종료 뒤 늦은 이벤트를 설명하는 한국어 주석 두 줄을 추가했다. 문서 담당도 실제 diff와 해당 본문을 읽었다. 타겟 캐시나 provisional 표현을 등록하기 전에 종료 상태에서 반환하도록 바꾼 것이며 Observer 오류를 생략하거나 닫힌 native owner를 다시 열지 않는다.

기존 `_combatStopped`는 게임 종료에서 true, 서버 spawn과 despawn에서 false로 초기화된다. 이 부분과 정상 경기의 타겟·공격 처리, 기존 확정 결과·피해 정책은 변경하지 않았다. 관측된 이벤트 입구의 누락을 교정한 것이며 별도 atomic ACK 경로 전체나 싱글 지속 방향 문제의 원인을 해결한 것으로 확대하지 않는다.

메인은 source/meta682개 baseline 대조에서 NCC만 변경됐고 나머지는 보존됐으며, 정확한 주석 두 줄·가드 변경 외 원문 bytes가 같다고 전달했다. 이는 메인 독립 정적 대조 결과이고 Unity 컴파일·suite·실기 PASS가 아니다. 현재 작업은 기존 C#1파일+Task2문서의3경로이며 미커밋·미푸시다. 실제 컴파일·새 빌드·멀티 포기/정상 종료·재경기의 새 전투 시작 확인은 아직 실행하지 않았다. 최적화는 계속 보류하며 추가 애니메이션/VFX·원시 로그 변경은 없다.

## 10. 사용자 확인을 보존한 통합 검증 대상(2026-10-26 KST)

사용자는 미확정 항목을 정리하고 필요한 로그와 따라 할 절차를 준비해, 다음 테스트 자료로 항목별 PASS를 판정하도록 요청했다. 앞선 「컴파일 에러 없음」은 당시 버전의 컴파일 수용이고 「특별히 문제없어 보임」은 실제 확인한 일반 플레이의 수용이다. 이를 미확인으로 되돌리지 않는다. 다만 악성 요청·예약 상한·파일명 소진처럼 실제 실행 여부가 알려지지 않은 특수 조건과, 그 뒤 수정한 종료 후 전투 진입은 별도 증거가 필요하다. 이번 싱글 방향 오류 미재현과 원인 미확정도 함께 유지한다.

[리팩토링 Plan5.1](../../2026-10-23/14_08_architecture-refactoring-audit/Plan.md#51-리팩토링-후-한-번에-진행할-통합-검증-대상--누적-계획2026-10-25-kst)은 전체 누적 장부다. 이번 범위는 최근 사용자에게 설명한 다음 열 항목으로 한정하며 인증·경제·반올림 등 전체 행을 재검증 목록에 더하지 않는다. 표의 담당 방식은 준비 계획이며 실제 실행 결과가 아니다.

| ID | 수정은 적용됐으나 증거를 채울 경계 | 이번 확인 방식 |
|---|---|---|
| V01 | 건물 제거 완료 뒤 경로 갱신 | 기존 실제 건물 제거 fixture와 출구 차단→해제→생산·이동 관찰. 최근 멀티 두 경기 adapter0은 보존하되 해당 조건 전수 실행으로 확대하지 않는다. |
| V02 | 게임 종료 뒤 늦은 전투 진입 차단 | 실제 이벤트 입구와 초기화·종료 수명의 집중 검사, 멀티 포기/정상 승패·재경기와 기존 handoff 종료 집계. |
| V03 | 예약 FIFO와 살아 있는 예약 정합성 | 실제 Coordinator의 완료·Retire·Clear·같은 ID 재등록·512상한을 집중 검사. |
| V04 | 연구 요청의 실제 연구소·생존·소유권 검증 | 통제된 실제 요청 처리 검사로 잘못된 요청의 무변경과 정상 요청 보존을 확인. 일반 사용자에게 잘못된 네트워크 요청을 수동 작성하게 하지 않는다. |
| V05 | 생산 요청의 실제 소유권·음수 취소 검증 | 통제된 실제 요청 처리 검사와 정상 생산·취소·랠리·자동생산 동작을 연결. |
| V06 | 생산 패널 중복 구독·닫힘/대상 전환 뒤 누르기 수명 | 통제된 수명 검사와 패널 반복 조작·한 번 입력당 한 번 처리되는지 관찰. 자동생산을 사용자가 켠 상태와 닫힌 패널의 남은 입력을 구분한다. |
| V07 | Bootstrap 재경로 구독 해제 | 실제 구독·해제 수명 검사와 경기 종료·재진입 관찰. 오류가 없다는 이유만으로 구독 해제를 판정하지 않는다. |
| V08 | 결과 화면 도달 확인 대기 중 최신 통신 재검사 | 통제된 대기/신호 갱신 경계 검사. 정상 결과 화면 체류만으로 타이밍 경쟁을 실행했다고 하지 않는다. |
| V09 | Logcat 파일명 상한 소진 시 기존 파일 보존 | 임시 폴더에서 실제 파일명 helper의 상한 중단과 기존 bytes 보존을 검사. 사용자 로그999개 생성·삭제는 요구하지 않는다. |
| V10 | RootPose 이동 단계 parser와 Bloom 역할 fixture 정정 | 기존 실제 parser와 역할 fixture를 집중 메뉴에서 실행. 검증 도구 PASS를 전체 전투·VFX PASS로 확대하지 않는다. |

Editor 메뉴·정확 범위의 코드 전 계획은 [Plan8](Plan.md#8-남은-수정-항목의-통합-검증-준비2026-10-26-kst-구현-전-계획)이다. 전문 담당의 진단/fixture 준비와 실제 실행 결과를 구분하고, 미실행은 `NOT_RUN`, 조건이 발생하지 않은 경우는 `NOT_EXERCISED`로 남긴다. PASS는 실행한 case의 기댓값과 증거가 일치할 때만 부여한다. `Generation=refactor-verify-v1`은 새 진단/검사 세대를 가리키며 빌드 Git SHA가 확인됐다는 뜻은 아니다. 원시 자료는 기존 private 보관 정책을 따르고 AI 생산시간 배율·외부 VFX 통합·Profiler 최적화는 이번 범위에서 제외한다.

## 11. 검증 준비 구현과 main 후속 병합(2026-10-26 KST)

Plan8의 메인 검토/GO 뒤 기존 runtime C#8파일의 제한 진단과 신규 Editor partial2개를 구현했다. 기존 종료 후 전투 진입 가드를 보존하고 실제 guard·구독·초기화·정리·최신 신호 재검사에 읽기 전용 근거를 더했다. Editor 집중 메뉴의8case와 서버 부정 요청12case, 종료 뒤 진입·최신 통신 case를 준비했으며, 조작 순서·전제·기댓값·수집 자료는 [Testcase.md](Testcase.md)에 작성했다. 전체 변경은 기존 source8+신규 Editor source/meta4+기존 Task2+Testcase/meta2의16경로다. 실제 실행 결과를 아직 받지 않았으므로 이 기록은 항목별 PASS가 아니다.

초기9절의 동기화 이후 원격 main에2750decd가 추가돼 메인이 이를 충돌 없이 병합했다. 현재 Git HEAD는`e312feac`이며, 메인이 전달한 병합 대조에서는 검증 준비16경로의 bytes가 모두 보존됐다. 새 main 변경은 로비 방 코드 문구와 기존 UI Task 쪽이며 외부 VFX 작업을 통합한 것은 아니다. 앞선 `Already up to date.`는 그때의 관측으로 남기고 최신 병합 상태와 구분한다.

전문 QA의 추가 blocker 없음과 메인 정적 대조 완료를 전달받았다. 메인 검토는 변경 C#10개 문법 트리 오류0, Release 조건 선택32개에서 새 진단 상태 없음·Android의 Editor seam 제외, 문서 검사/diff 오류0이다. 이는 source/조건부 선택 검토이며 실제 Unity 컴파일·NGO ILPP·집중 메뉴·멀티 통제 검사·Android 재테스트는 미실행이다. 이 작업 환경의 source 확인을 native/기기 PASS로 바꾸지 않는다. 검증 준비16경로는 미커밋·미푸시이고 기존 사용자 수용 범위는 유지한다. 진단/검사 세대 표식은`refactor-verify-v1`이며 Git SHA 증거를 대신하지 않는다. Editor 정지 상태의 receipt는 Console에서 해당 Run만 복사하고, 플레이 원시 로그는 기존 private 보관 정책에 따라 공유한다.


## 12. main VFX·투사체 통합과 검증 준비 보존(2026-10-26 KST)

사용자 병합 지시에 따라 메인이 원격 main `f367cb52`를 통합했고 병합 HEAD는`562888d2`다. 이전11절의 「외부 VFX를 통합하지 않음」은2750decd 병합 당시의 기록이다. 이번에는 VFX·서버 권위 투사체 A~D·관련 에셋/문서가 main에서 들어왔으며, 이를 해당 기능의 새 실기 수용으로 해석하지 않는다.

병합 전 검증 준비16경로를 별도 백업과 stash로 보존하고 병합 뒤 복원했다. 메인의 독립 대조에서는 원래16파일이 모두 존재하고, 기존 runtime8파일의 진단 patch 추가/삭제 내용이 상류 행 이동과 무관하게 stash와 같으며, 비충돌 main 경로도 상류 원문과 같았다. 보관 stash와 백업은 남겼다. 병합 충돌은 UnitView에서 양쪽 수명/진단 내용을 결합하고, Mushroom 감사 행은 main Homing 정책과 실제1초 marker·기존 사용자 수용을 함께 보존했다. 메모리 baseline은 양쪽 역할별 증가를 보존한 뒤 공식 도구로 game-programmer9030→9059행 증가만 반영했으며 metadata/change_log는 유지했다.

새 main에서는 투사체가 별도 권위 시간으로 진행하므로, 서버 요청 통제 검사가 생산 시계만 멈춘 상태에서 진행 중 공격을 함께 검사하면 정상 변화와 잘못된 요청의 부작용을 혼동할 수 있다. 이에 신규 Editor 검증 partial의 실행 전제에 실제 `CountActiveProjectiles()==0`와 `_torrentLaunchCount==0` 읽기 검사를 추가했다. 기존 무전투 전제와 함께 비행/파도가 있으면 실행하지 않는다. 제품 투사체 tick·시간·피해를 멈추거나 정책을 바꾸지 않으며 새 런타임 기능 수정도 하지 않았다.

메인이 전달한 최종 source 확인은 변경 C#41개의 Editor/Android 개발/Android Release 선택123개에서 문법 오류0, QA 추가 blocker 없음, 문서 검사/현재 작업 diff 오류0이다. 이는 선택된 source 문법·보존 대조이며 실제 Unity 컴파일·NGO ILPP·전체 검사·Android/멀티 실행 PASS가 아니다. 상류의 기존 공백은 원문으로 보존했다. 현재 검증 준비16경로는 미커밋·미푸시이고 테스트 결과는 대기다.

새 main의 로그 ZIP 생성 도구를 [Testcase](Testcase.md)의 수집 절차에 연결했다. 이 도구는 RuntimeLog만 자동 선택하므로 Console 검사 결과인 `RefactoringVerification.txt`는 별도 첨부가 필요하다. 원시 자료의 private 보관 정책과 기존 사용자 수용·AI 생산시간 배율/최적화 보류는 유지하며, main VFX 전체 검증을 이번 열 항목 PASS로 섞지 않는다.
