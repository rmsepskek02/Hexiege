# 생산 대기 해제 후 방향과 전투 복귀 이동 문제 조사

생산 건물의 출구가 막혔다가 열릴 때 새 유닛의 방향이 이동 중에도 어긋난다는 신고와, 별도 멀티플레이 로그에서 확인된 전투 후 이동 복귀 실패를 먼저 바로잡는다. 두 현상을 같은 원인으로 묶지 않고 현재 코드·재현·로그를 대조해 필요한 교정이나 제한된 관측만 추가한다.

- 시작: 2026-10-25 20:47 KST. 작성 전 `TZ=Asia/Seoul date`로 확인했다.
- 사용자 결정: 「최적화 우선 보류, 이런 문제들부터 바로잡자」. 기존 Task 문서 작성·자율 진행 승인을 유지한다.
- 기준 소스: `work / 572d94af28cb8ee1f8142c49bea31a6f72e33fc5`.
- 현재 상태(2026-10-26 KST): 메인 계획 검토/GO 뒤 멀티 제거 완료 통지 교정5파일과 싱글 개발용 관측3파일을 적용·전문 정적 대조했다. 메인 독립 대조와 QA 정적 검토까지 마쳤으며 Unity/기기 검증은 아직 없다. 싱글 지속 방향 원인은 미확정이다.
- 보존: 기존 리팩토링 후속 기록2파일과 Profiler 초안6파일의 미커밋 상태를 유지한다. 새 Task/문서 meta는 이 작업의 추가 범위다.

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
| Unity 컴파일·기존 회귀 검사 | 미실행 |
| 싱글 막힘→해제→생산→이동 방향 재현 | 사용자 기존 한 번 관찰, 교정 후 확인 없음 |
| 멀티 전투 종료 복귀/역할별 검증 | 저장 실패 근거 있음, 교정 후 확인 없음 |

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

source는 기존8 C#이며 신규 source/meta는 없다. 이번 변경은 기존 리팩토링 Task2+보류 Profiler 문서/meta6+새 교정 Task 문서/meta5+source8의21파일이다. 현재 `work` HEAD/마지막 원격 기준은572d94af이며 추가21파일은 미커밋·미푸시다. 최초 작성일10-25와 당시 저장 로그는 원문으로 보존하고 이 결과만10-26 후속으로 기록한다. Profiler 최적화는 계속 보류이며 네트워크/GPU·30FPS 설정을 바꾸지 않았다.
