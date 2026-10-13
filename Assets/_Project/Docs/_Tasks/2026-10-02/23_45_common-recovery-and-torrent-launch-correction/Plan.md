# 공통 이동 복구 및 TorrentSpirit 승인 발사 교정 계획

안전한 우회 경로를 가진 유닛이 전투 후 불필요하게 막힘 상태에 머무르지 않도록 하고, 서버가 승인한 TorrentSpirit 공격이 원래 목표의 소멸 때문에 사라지지 않도록 교정한다. 기존 이동 안전 검사와 다른 유닛의 타깃 추적 규칙은 그대로 지킨다. 시작 방향을 고정하거나 새 목표로 바꾸는 작업은 아니다.

**기존 로직 변경 근거:** 안전한 경로의 첫 hop을 직접 중심 후보의 엄격한 거리 감소 조건으로 제외하는 결합, 유효한 동일 경로를 실패 반복으로 취급하는 결합, acquire에서 이전 Lerp 후보를 사용하는 결합을 좁게 교정한다. Torrent의 dead/missing primary prelaunch 생략은 사용자가 승인한 규칙29 예외로 대체한다. 공통 안전 guard 자체를 제거하거나 다른 유닛까지 우회시키지 않는다.

## 1. 현재 상태

**최신 실기 현행 — 2026-10-03:** 사용자 경기 확보, 이동·승인 발사 subset 관측 통과. Client 텍스트320중1건 누락·시계 결함OPEN, HP정상/415VFX유지. 아래 실기 대기는 과거 이력이며 현재는 §9이다. 전체C3/migration PASS로 닫지 않는다.

**최종 통합 인계 — 2026-10-03:** 구현·정적 컴파일 및 변경 assembly 두 기존 GUI 메뉴 PASS. Android Build And Run은 실제 시도 후 JDK 확인 preflight 진행만 관찰했으며 완료/성공 미확인이다. 사용자 완료 확인·runtime 교정 판정 OPEN, Torrent Unresolved/LegacyFallback 유지. 아래 준비·대기 상태와 §7 대기 체크는 당시 이력이며 현행 결과는 §8을 따른다.
**현재 상태 — 2026-10-03:** 구현 및 정적 컴파일 인계 도착. 실제 중앙 예약 소유 구조는 §7을 따른다. Unity GUI 검증·빌드 시도는 대기이며 runtime OPEN이다. 아래 초안/제안 대기 문장은 준비 이력이다. 최종 완료 및 실기 PASS 체크는 하지 않는다.

Research/Plan 및 승인 예외 규칙 반영 단계다. 구현·컴파일·변경 assembly의 Unity 메뉴·Android 빌드 시도·실기 결과는 아직 완료 인계가 없다. 프로그램 에이전트 파일별 제안은 도착 후 추가하며 문서 작성이 그 제안을 기다리는 상호 대기는 하지 않는다. 전체 판정 OPEN, Torrent `Unresolved / LegacyFallback` 유지.

**최신 진행:** 메인이 초안을 승인하고 제한된 프로그램 제안을 검토한 뒤 구현 GO를 전달했다. 위 제안 대기는 최초 작성 이력으로 보존하며 현재 파일별 경계는 §3에 반영됐다. 완료 체크박스는 검증 인계 전까지 미체크로 유지한다.

## 2. 수정 항목 및 규칙 근거

> 아래 수정 항목은 구현 인계를 받았으나 실기 교정 PASS가 아니라는 구분을 위해 최초 계획 체크를 보존한다. 현재 구현/통합 실행 여부는 §8에서 체크하고 runtime 수용은 미체크다.
- [ ] **안전한 경로와 직접 중심 후보 분리:** 직접 접근에 쓸 중심을 못 찾더라도 유효 provider 경로는 별도로 corridor 검증 후 사용한다. 옆방향 첫 hop의 거리 동률을 실패로 일반화하지 않는다. 근거: `GameSystemRules_Units.md`의 `U-MOV-ALIGN`, `U-MOV-REPATH` 및 규칙2·4·45. 실제 unsafe 반복은 계속 거부한다.
- [ ] **동일 경로 false Blocked 교정:** 검증된 재사용 경로와 실제 no-progress/unsafe 반복 요청을 구분한다. Blocked 재진입·해제 조건을 원래 command/segment와 일관되게 처리하며 budget 증가나 외부 이벤트를 위조하지 않는다. 근거: Units `U-MOV-REPATH`, 규칙45.
- [ ] **타깃 acquire의 stale Lerp 후보 교정:** 현재 서버 Root와 실제 acquire 결정을 기준으로 후보를 검증하고 과거 align 보간값을 재사용하지 않는다. Root/domain writer와 corridor 수락은 유지한다. 근거: Units `U-MOV-ALIGN`, `U-ATK-ALIGN`, 규칙10·11·13; sync `NET-FACING-001`.
- [ ] **진단 coverage gap 교정:** blocked-only episode도 이후 해제·완료·재교전·사망 capture를 연결하고 fallback guard의 실제 결정과 route 승인/거부 이유를 남긴다. private planner ordinal은 제공되지 않는 한 unavailable로 유지한다. unit+command+segment+episode, sourceFrame/time과 cached provider/spatial 시각을 분리한다. 기존 bounded memory ring·UTF-8 Android-safe 출력·counter/overflow/eviction을 유지하며 추가 query/A* 또는 gameplay 변이와 매 프레임 파일 출력은 금지한다. 근거: `LogRules.md`의 런타임 로그 규약 및 Units `U-MOV-REPATH`.
- [ ] **Torrent 전용 서버 승인 발사 예약:** 원래 타깃 identity, 예정 시각, 마지막 유효 서버 aim을 예약 수명에 보존한다. 살아 있는 원래 primary는 현재 방향, lost primary는 마지막 유효 aim을 사용한다. 고정 시작 방향이나 새 retarget은 금지한다. 예약은 서버 승인 이후만 유효하고 primarydeath/rangeexit만으로 닫지 않는다. 발사 전 공격자 사망/gameend는 취소한다. 근거: Units 규칙29·`U-TARGET-COMMIT`·`U-IMPACT-INDEPENDENT`; sync `NET-DELIVERY-TRAVELING`, `NET-CANCEL-003`, `NET-CANCEL-004`.
- [ ] **정확히 한 번 생성 및 실효 contact 유지:** timer/marker/중복 호출이 서버 파도를 중복 생성하지 않도록 기존 identity와 단일 writer를 유지한다. 발사 승인과 contact 결과를 구분하고 VFX-only나 가짜 primary HitApplied로 처리하지 않는다. 적 건물 피해·적 유닛 피해·아군 유닛 힐과 접촉 hit-set은 유지한다. 근거: Units 규칙18·28·29·30, sync `NET-ACTION-IDEMPOTENT`, `NET-PRESENT-001`, `NET-PRESENT-003`.

## 3. 구현 경계

### 메인의 프로그램 제안 제한 검토 인계(2026-10-02)

- `UnitView`: no-center fallback은 다음 frame의 General 경로로 처리하고 실제 SpatialRecoverable guard는 유지한다. 즉시 ShouldEngage는 현재 Root/NoIntent를 사용하되 pure trajectory candidate의 Acquire 계약은 유지한다. blocked trace의 후속 transition 및 guard decision을 보강한다.
- `UnitMovementUseCase`: 검증된 A* 첫 인접 hop을 사용해4→4 옆방향 경로를 허용한다. 전역 strict-closer 조건 완화가 아니다.
- `UnitCombatUseCase`와 `NetworkCombatController`: 싱글 pending hits와 네트워크 delayed coroutine 양쪽에서 승인 예약 생성/소비를 연결한다. 공통 ApplyDamageCore의 targetalive 검사는 변경하지 않는다.
- `TorrentAttackBehavior`: 발사 geometry를 재사용한다. 필요한 경우 최소 Application 예약 helper와 `.meta`만 추가한다. bounded active capacity는 engineering limit으로 정하되 초과 시 commit 전에 거부하고 활성 예약을 eviction하지 않는다.
- identity는 lifecycle+공격자 객체 identity+로컬 단조 launch reservation으로 구성한다. 원래 타깃 ID 재사용을 차단하고 Stop/target switch로 승인 예약을 취소하지 않는다. 공격자 사망·gameend·despawn/shutdown은 취소한다. fake C3 sequence/Supported 전환은 금지한다.
- EmptySameTile 처리는 실제 안전한 중심 도착 completion에만 한정한다. Legacy Torrent 월드 aim과 generic SimulationFacingAim의 차이는 남는 경계이며 이번 교정으로 완전 U-ATK 정합이라고 주장하지 않는다.
- Bootstrap의 기존 shutdown 호출 연결이 꼭 필요하면 변경 필요성을 메인에 먼저 알린다. 새 정책을 추가하지 않는다.

관련 파일 후보는 Research §4를 따른다. 프로그램 에이전트는 먼저 파일별 변경 경계와 기존 writer/cancellation 연결을 메인에 제안한다. 충돌이 있으면 사용자에게 변경 전에 알린다. 이번 Torrent 예외를 일반 TargetLocked/Melee/Hitscan 또는 모든 TravelingArea에 전파하지 않는다. asset/stats/clip, resolver 승격, 랠리 Fatal, 성능 교정, TC/QA 파일 및 새 validator는 금지한다.

## 4. 통합 확인과 빌드 시도

- [ ] Runtime/Release/Development/Editor 해당 컴파일 결과와 errors/warnings를 구분해 인계한다. Release 진단 제외와 byte 제한도 확인하되 새 validator를 만들지 않는다.
- [ ] Unity 수동 refresh/domain reload가 끝난 변경 assembly에서 기존 Unit Action self-validation을 실행하고 최신 결과를 확인한다.
- [ ] `Hexiege → Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit`의 최신 결과를 확인한다. 두 메뉴 PASS는 실기 버그 PASS가 아니다.
- [ ] 메인이 기존 Android 설정으로 Build And Run을 시도하고 시작/미시작/사전조건 차단을 정확히 기록한다. signing 비밀번호 탐색·설정 변경·완료 모니터링은 하지 않는다. 완료와 기기 실행 확인은 사용자에게 맡긴다.

## 5. 사용자 일반 플레이 확인 안내

빌드 실행 뒤 Torrent와 근접·원거리 비교 유닛을 평소처럼 사용하여 전투 종료 후 우회 이동과 새 교전을 관찰한다. 로그로 안전 route 수락·실제 양수 이동·Blocked 해제 및 stale 후보 미사용을 같은 episode에서 대조한다. 다른 종류의 동일 runtime 교정 증거는 해당 종류가 실제로 재현·복구된 경우에만 주장한다.

Torrent는 승인 뒤 primary 사망·소멸, 살아 있는 primary의 이동/사거리 이탈, provisional 및 발사 전 공격자 사망/gameend를 나눠 관찰한다. 원래 target identity와 current/last-valid aim, 예정 launch, exactly-once wave, Client 표현, 서버 적 유닛/건물 contact 피해·아군 힐을 연결해 확인한다. 살아 있는 primary가 움직여도 시작 방향으로 고정되지 않아야 한다. collection 완결성과 기능 판정은 분리하며, 기존 파도 보정 근거는 별도 이력으로 유지한다.

## 6. 최종 문서 인계

- [ ] 실제 구현 파일과 계획 차이, static/menu/build 사실을 인계받은 뒤 현재 상태와 결과 절을 갱신한다.
- [ ] 실기 미확인 범위는 OPEN으로 남긴다. 이전 조사·메모리 이력을 삭제하지 않으며 완료 후 필요한 workflow 문서만 최소 갱신한다.
- [ ] 문서 checker와 memory baseline 검토는 메인과 조율하며 동시 실행하지 않는다.

## 7. 실제 구현 인계 — 최종 완료 전(2026-10-03)

> 이 절의 GUI/build 대기 체크는 당시 인계 상태로 보존한다. 이후 실제 수행 결과는 §8이다.

- [x] 구현 코드 인계 수령:10 C# + helper meta, 정확한 목록은 Research §6. §2 체크박스는 실기 교정 판정과 혼동하지 않도록 아직 미체크이며, 아래 구현 인계와 분리한다.
- [x] 정적 컴파일 인계: Runtime/Release/Development/Editor exit0·errors0, warnings20/24/20/0. Runtime276 sources 누락0, helper 정확히1회 포함. §4의 정적 확인 중 byte/Release 진단 경계는 별도 근거 없이 완료로 확대하지 않는다.
- [ ] 변경 assembly의 Unity refresh/domain reload 및 두 기존 GUI 메뉴 결과 인계.
- [ ] Android Build And Run 시도 사실 인계(시작/미시작/차단 구분). 완료 모니터링 없음.
- [ ] 사용자 실기와 동일 reservation/episode 근거로 교정 판정. 전체 OPEN, Torrent Unresolved/LegacyFallback 유지.

**초기 제안과 실제 소유 구조 차이:** `UnitCombatUseCase.TorrentLaunch.cs`가 서버/싱글 중앙 예약 tick을 소유한다. single pending hits나 network delayed coroutine 자체가 예약 발사 소비자가 아니다. 예약 cap1024, service epoch+단조 key+객체 identity, cooldown/commit 전 확보, 실제 wave 생성 전 제거를 사용한다. 원래 primary aim은 매 서버 frame 갱신하고 상실 시 last-valid 방향을 사용하며 발사 origin은 현재 caster 위치다. 공격자 사망·gameend·despawn/shutdown·서비스 교체는 close/logging, Stop/target switch는 유지한다. 일반 ApplyDamageCore targetalive와 C3 gate는 변경하지 않는다.

공통 이동 인계는 General no-center fallback·현재 Root acquire·실제 안전한 중심 도착의 typed EmptySameTile completion이다. 검증된 A* 첫 인접 route checkpoint의 동일 goal distance만 허용하고 실제 SpatialRecoverable guard 및 corridor 안전은 유지한다. diagnostics guard/post-Blocked/launch→wave key 연결을 추가했다. private ordinal과 Legacy aim의 공통 Facing 완전 정합은 주장하지 않는다. 기존 규칙을 지키고 충돌은 사용자에게 변경 전에 알리며, 새 TC/QA/validator는 만들지 않는다.

## 8. 최종 구현·통합 인계 및 사용자 확인 잔여(2026-10-03)

- [x] §2의 범위 구현 인계 수령. 중앙 예약 owner/실제 변경11파일은 Research §6; 실제 runtime 교정 검증과 분리한다.
- [x] 정적4종 compile exit0/errors0·warnings20/24/20/0 및 Runtime276 sources 누락0/helper1회 인계.
- [x] 변경 코드 Unity refresh/domain reload 완료. Console errors0·compile warnings20, self-validation 뒤 총warning25 확인(무경고 아님).
- [x] 00:19:46 실제 RunUnitActionSelfValidation 메뉴 네 부분 PASS.
- [x] 00:20:28 실제 Root Pose Cross Audit self-validation 메뉴 PASS.
- [x] File > Build And Run 실제 시도, JDK Checking progress 관찰: **ATTEMPTED / preflight started**. completion/PASS를 뜻하지 않는다.
- [ ] 사용자 빌드 완료·설치·기기 실행 확인. Codex 완료 모니터링·비밀번호 탐색·설정 변경 없음.
- [ ] §5 실제 플레이의 shared recovery/Torrent launch/contact 근거 확인. runtime OPEN 및 다른 종류 동일 실패/교정 미입증 경계 유지.

실제 코드·meta 전체 목록은 Research §6에 누락 없이 기록했다. 기존 규칙 충돌은 사용자에게 변경 전에 알리는 영구 선호를 유지한다. 문서/메모리 최소 갱신 후 checker와 증가 baseline 반영은 메인에 맡기며 동시 실행하지 않는다. 이전 메뉴 대기·조사·파도 보정 이력은 삭제하지 않는다.

## 9. 최신 경기 결과와 남은 문제(2026-10-03)

- [x] 사용자 실제 경기 확보. 이전 Codex 빌드 완료 직접 관찰로 소급하지 않는다.
- [x] 이번 경기 이동·발사 subset 관측 통과: 예약421=발사415+gameend취소6; current215/last-valid200, exact wave집합415 일치·중복0. lost-primary HP변화43(유닛 피해37/건물1/힐5).
- [x] MOVE99761frames 실패0·planned1141=committed1141·checkpoints941·Blocked0. 이전 장기 stall trigger 미재현이므로 보편적 수정 증명 아님.
- [x] contact publish/receive320 및 Client HP권위 유지 확인; 만피heal919 정상zero 분리.
- [ ] Client text 완전성: Host320/Client319, epoch4c5fc1a2f46db585/wave1/impact2/unit4 누락. coordinator clock회귀32.959ms와 결과거부 확인; 해당HP500→300·damage200은 정상 반영됐다.
- [ ] 별도 clock/text defect 교정·재시험. 현재는 기록·설명 및 후속 제안만, 구현 미승인.
- [ ] 전체C3/Supported migration·formal CrossAudit·보편적 이동 교정 입증. 전체OPEN·Unresolved/LegacyFallback 유지.

후속 제안은 공유 monotonic presentation clock과 권위 contact bounded retention/retry다. Update Math.Max와 receive raw clock 차이뿐 아니라 payload 미래 차이295.664ms가250ms guard를 넘는 경계도 함께 검토해야 한다. Math.Max 단독 해결·tolerance 확대·catch-all·무조건 accept는 안 된다. epoch/exact-key dedupe·finite/time validation·expiry·HP authority를 유지하며 실제 변경은 별도 사용자 승인 뒤 수행한다. 다른 유닛 규칙 충돌은 변경 전에 알린다.

원본session/역할·수치·경고분류·raw anchors는 Research §9. 이번 요청으로 코드·빌드·새Task/TC/validator·규칙·에셋/스탯/클립 변경 없음. 과거이력 보존, Checker/baseline은 메인이 실행한다.
