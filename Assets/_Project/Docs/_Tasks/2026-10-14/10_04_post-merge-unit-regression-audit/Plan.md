싱글플레이 추격 진행 교정의 기존 근거를 보존하고, 추가 승인된 타격 타이밍 교정의 적용·독립 검증 인계를 완료했다. 연속 공격 모션의 시간 기준과 실제 피해 예약을 같은 미래 타격 시점에 결합하는 코드를 반영했다. 광산·랠리는 제외하며 실제 화면·경기 수용은 아직 별도 검증이 필요하다.

# Plan — SOLO 추격 진행 및 타격 타이밍 교정

## 추가 승인된 전 유닛 historical-contract 감사 — 보고·Main 검토 완료 / 구현 없음

기존 Task 두 문서만 사용한다. 먼저 모든 실제 UnitType 이름별로 옛 Task 최종 사용자 승인/수용과 후속 변경을 추적해 기대 marker/피해 offset 예외/cycle/모델 방향/VFX 전달·복구와 출처를 Research에 모은다. 감사표·status는 옛 Task를 찾는 인덱스일 뿐 타이밍 권위가 아니며 현재 config를 기대값으로 재사용하지 않는다. 명확하지 않은 값·수용 범위·서로 다른 날짜의 주장은 미확정/변경 이력으로 분리한다.

Research의25행 historical 표와25행 현재 marker 표·최종 한정 판정에 대한 Main 검토를 완료했다. 두 담당 최종 감사 수령 및 Main 독립25clip/config/Factory·지정경로 diff 확인을 구분해 기록했다. Spear0:25는 옛 사용자 수용 원문으로 유지됨을 확인했다. 원래ss:ff 차이는 Little/Axe/Tank/Cannon/Dust/Rhino/Eagle와 Stream을 더한8종이며 모두 잘못/되돌아감이라고 판정하지 않는다. Axe/Dust/Rhino/Stream은 이미 옛Task의 explicit decimal artifact를 유지한다. Little/Tank/Cannon/Eagle의 원래frame 최종 의도 primary 부족과 Eagle 실제 Attack 선택·Quake 보조 dangling 참조는 이름별 한계다. Bear/Lion 실효주기1.6666/2는 기존 Factory clip-derived 정책 결과이지 새 cycle 변경/merge 회귀가 아니다.

10/14 shared SOLO clock의 일반23종 적용 경로 변경은 승인된 새 교정이며 clip reversion과 다르다(Torrent/Healer 제외 및 unsupported offset fallback 유지). 모든23종 실제 경기/시각 검증을 뜻하지 않는다. 과거 Fox peer-test 제안은 이전 focused 분석 당시 순서로 보존하며 현재 첫 우선순위로 유지하지 않는다. 이번 전 유닛 감사 보고는 완료했고 추가 교정·구현·전체 재테스트는 승인되지 않았다. Main 최종 source5 해시 불변, 새절 링크39개/누락0, check_docs 실제exit1/동일한 기존6건이다. 검사 재실행 없이 인계한다. 코드/Stats/클립/규칙/다른 문서·메모리·새 Task/TC·QA/Unity/build 변경 없이 원래 사용자 시각 우려·실기 수용 OPEN은 유지한다.

## 승인 경계

현재 사용자 승인 범위는 기존 SOLO pursuit 교정에 추가된 **SOLO impact timing 교정과 targeted bounded 진단**, 기존 [Research](Research.md)/이 Plan 갱신이다. 타이밍 소스 5파일 적용·Main 독립 검토/검증은 완료됐다. 문서 담당은 두 Task 문서와 별도 허용된 `.claude/mistakes.md`의 사건 추가만 편집한다. 광산·랠리·경로·네트워크·Stats·클립·프리팹·씬·Legacy→Reducer 전환 및 관련 없는 dirty 파일은 제외한다. 새 Task·TC/QA·status/roadmap/에이전트 메모리·상시 규칙은 작성/수정하지 않는다. native·빌드·실기 실행도 포함하지 않는다.

## 현재 타이밍 단계 — 적용·Main 독립 검증 완료 / native·실기 수용 OPEN

1. 동일한 실제 continuous Attack playback epoch를 retarget 뒤에도 유지한다. 새 playback 또는 객체 lifetime 시작에서만 epoch를 reset한다. 매 cycle Attack restart는 하지 않는다.
2. 해당 epoch에서 아직 오지 않은 절대 marker occurrence를 선택하고 captured target·sequence·hit 예약을 함께 고정한다. 과거 marker를 소급 승인하거나 로컬 marker를 HP writer로 쓰지 않는다. 공유 continuous policy는 계약이 맞을 때만 재사용한다.
3. Main의 읽기 전용 CC 검토에 따라 **기존 구현 동작을 보존한다**: SOLO CanAttack은 새 commit만 막고 pending hit·cooldown은 계속 진행하며, 피해 writer는 생존만 확인하고 ClearState는 hit를 취소하지 않는다. CC state Tick·즉시 freeze 적용·pending 피해 정책을 이번 교정에서 바꾸지 않는다. 스킬 규칙 13의 이동0/공격 비활성 규정은 이미 커밋된 타격의 pause/cancel까지 명시하지 않으므로 그 규범 정책은 OPEN이다. 기존 동작 보존에 새 사용자 정책 결정은 필요하지 않다. 실제 native pause와 미래 epoch 관측은 별도 검증이며 freeze 수용·모든 CC 타이밍 해결을 주장하지 않는다. 후속 CC 검증은 원래 의미의 보존 근거이지 새 규칙이 아니다.
4. long frame에서 missed cycle을 무제한 burst하지 않는다. 이미 커밋된 유한 hit는 원래 captured target 소유권을 유지하고, 유닛당 frame당 새 commit은 최대 하나로 제한한다.
5. bounded 진단은 epoch/loop/hit/target/due time/실제 HP 적용/marker 비교와 coverage를 구분한다. 관측되지 않은 Animator/pixel을 측정된 것으로 쓰지 않고 상한·누락·불완전 근거도 표시한다. 매 frame raw 출력·중복은 금지하며 기존 LogRules 계약을 유지한다.

계약 근거: [Units](../../../GameSystemRules/GameSystemRules_Units.md) U-COMBAT-PHASE·U-TARGET-COMMIT·U-ATK-TIMELINE(연속 재생·미래 occurrence·동일 회차/타겟/타임라인) 및 규칙 17(실제 clip marker에서 생성 시 추출). 규칙 18과 [CombatSync](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md) NET-AUTH-001·NET-ACTION-SEQ·NET-TIME-001은 권위 타이머·overshoot·공통 시계의 근거다. 규칙 18의 멀티 50ms tick을 SOLO 실행 사실로 간주하지 않는다.

위 승인 계약의 구현·실제 authority core CLR 검증·Main frozen 독립 실행과 5파일 최종 해시는 Research 「최종 타이밍 적용 및 검증」에 기록 완료했다. positive sorted marker(중복 포함)만 새 clock을 사용하며 ZERO/invalid/unsorted는 변경 없는 Legacy fallback, Torrent/Healer는 제외다. Bootstrap은 nonNetwork 블록에서 cooldown/pending 처리에 같은 SoloNow를 샘플링하는 차이만 있고 다른 단계 순서를 바꾸지 않았다. View close는 alive/current Domain의 커밋 HP를 취소하지 않으며 진짜 Domain death/removal/replacement만 owner를 무효화한다. commanded pause는 미래 occurrence만 이동시키고 이미 커밋된 due/target은 유지한다. CC 보존 CLR 근거는 freeze native 수용이나 새 pause/cancel 규칙이 아니다.

Main 새 Compile/회귀와 전체 manifest·Probe/artifact 전후 freeze 검증은 모두 exit0이다. 최종 native marker의 `ReservationJoin=unassigned`는 읽기 전용 metadata이며 full native HP join을 만들지 않았다. Main 최종 문서 검사 completion `b1cb09`는 실제 exit1, 동일한 기존 참조6건·신규 실패 없음·메모리 고아/감소 없음이다. 기존 pursuit 근거와 Factory 가설 철회는 보존한다. 현재 사용자 부분 실기 보고는 아래를 따르며 에이전트 native 메뉴·빌드는 NOT RUN, 전체 수용/first blend·native frame/pixel 정밀도·사용자 증상 인과는 OPEN이다.

### 2026-10-14 17:28 사용자 부분 테스트 후 현재 상태

focused source/log 분석 인계 완료: 사용자의 FoxMagician(type21) SOLO 비교 상대는 **이전 빌드 멀티**다. Main 재계수71 HP dispatch는 전부 Applied/음수 lateness0이며 승인2.25초보다 이르지 않은 관측 timer 근거다. programmer 전체 nominal anchor·큐64쌍 근거와 Main 제한 직접 표본은 Research에 구분했다. Fox Unresolved/default-key immediate는 양 모드 공통이므로 SOLO-only 즉시 표시나 Coordinator100ms 차이로 설명하지 않는다. SOLO 로컬 HP writer/멀티 ClientRPC 차이는 확인됐으나 현재 멀티 수신 지연은 미검증이다.

사용자 시각 reportedMismatch/원인/수용은 OPEN이며 새 교정은 선택하지 않았다. 판단에 필요한 것은 현재 빌드 focused Fox 멀티 trace·시각 상관 근거이지 전체 유닛 재테스트가 아니다. 한 종족 근접 관측도 전체 PASS가 아니다. 이번 분석은 읽기 전용이며 source5 불변은 programmer 보고, Stats·클립·config·소스·build·Unity 변경은 없다. 기존 CLR 수용과 실제 timer 관측을 native/pixel/human visual PASS로 승격하지 않는다. 광산·랠리는 계속 제외한다.

## 기존 pursuit 단계 — 소스 적용·Main 독립 인계 검토 완료 / 실기 미검증

- Main이 승인한 아래 소스 3파일 구현이 적용됐으며 최종 manifest·해시와 Main frozen 독립 실행 결과는 Research 「최종 적용 manifest 및 독립 검증」에 기록 완료했다. 소스 적용 수용은 runtime 수용이 아니다.
- `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitMovementContracts.cs`: 순수 SOLO pursuit-step helper의 remaining distance clamp/actual reached 적용 완료.
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`: SOLO Legacy pursuit helper 및 Advanced·actual reached·activePath 조건의 정확히 한 checkpoint 소비 적용 완료. held/acquire/invalidation에서는 소비하지 않는다. DomainProcessStep·mode flip·멀티 동작은 변경하지 않았다.
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`: helper→실제 checkpoint 소비 경로의 회귀 보강 적용 완료. programmer 원본 RED exit1→최종 GREEN exit0와 Main frozen 독립 GREEN exit0를 구분해 기록했다. Main 초기 RED 전달은 script/model 단계와 실행을 혼동한 보고로 즉시 철회됐으며 Main 독립 RED를 주장하지 않는다. 이번 한정 알고리즘 검증은 Unity 원래 증상 재현/실기 수용이 아니다. native 메뉴 NOT RUN, 새 TC/QA 문서는 없다.
- 근거는 [Units](../../../GameSystemRules/GameSystemRules_Units.md) 규칙 2의 경유 중심 순차 도달, 규칙 6의 월드 좌표 사거리, 규칙 10의 추격에서 공격으로 전환하는 조건이다. 규칙 11은 안전한 전투 종료 복귀를 보존하는 경계이지 일반 랠리/광산 교정 승인 근거가 아니다.
- `UnitView`의 bounded `GameLog.Dev` 태그 `[UAS-SOLO-PURSUIT]` lifecycle 진단 적용 완료. detail64/lifetime+cap1+terminal1, stall2/episode, UTF-8 body600+envelope 예약180과 FormatRejected/incomplete 처리·lifetime reset/OnDisable 종료를 manifest로 확인했다. 매 frame/tick raw 출력은 없다. [LogRules](../../../LogRules.md) §1.2·1.6·1.7·1.14의 분류·민감정보·스트리핑·전이 기록 경계를 유지한다.
- Main 최종 검토 완료: frozen 소스 해시 전후 일치, 독립 RunRegression GREEN exit0, Compile exit0(Runtime303 sources/errors0/warnings23, Editor32 sources/errors0/warnings0). 최종 문서 검사도 실제 exit1/정확히 동일한 기존 참조6건이며 신규 실패는 없다. native/빌드/실제 경기 NOT RUN. 구현+진단만 인계 완료하며 Root/domain·광산/랠리/타이밍의 별도 문제나 전체 runtime PASS로 확대하지 않는다. 현재 브랜치는 `codex/docs-checker-maintenance`이며 main은 fetch-only, merge/push 없음.

## 2026-10-14 최초 분석 계획 — 당시 승인 전 이력

1. **초안 공유:** Main 초기 비교 사실과 최신 SOLO 환경을 기록한다. Main/두 programmer 인계 전 상태는 미해결이며 PASS가 아니다.
2. **SOLO 실제 호출 체인:** `88f60e38`의 `NetworkContext.ActiveUnitMovementPipelineMode` getter 내부 `MovementPipeline.IsMatchActive` latch와 `UnitView.Initialize`의 `!NetworkActive` Legacy 선택을 구분하고, AStar/FlowField/Chase, attackLoop·쿨다운·AnimationAttackSpeed·marker까지 연결한다. 각 guard와 caller를 전체 함수 맥락으로 읽고 `path:line`을 커밋에 고정한다. 근거: Units 규칙 2·6·10·11·14, U-MOV-REPATH 및 U-ATK-TIMELINE.
3. **입력·지도 대조:** GameConfig→맵 생성/수신→projection→HexGrid/MineKind/walkability→SOLO 이동 소비→표현 변환을 비교한다. 근거: RandomMap 규칙 1·3·9·10; Units 규칙 2·10·11. mine 이동 불가와 건설 불가 영역 이동 허용을 분리한다.
4. **교정 보존과 변경 유입:** `b65751c6→0de4f868→88f60e38`에서 관련 파일 전체 diff/blob 및 caller wiring을 확인한다. 변경 원인은 exact commit과 해당 hunk로 좁힌다. grep 부재만으로 교정 소실을 선언하지 않는다.
5. **감사 결과 통합:** 최대 여섯 우선순위로 정적 facts와 조건부 hypotheses를 분리한다. 각 항목에 호출 체인·변경 커밋·기존 로그 관측점·반증 조건·부족한 증거를 붙인다. NET-AUTH/NET-TIME는 권위와 표현 분리의 기준이지 SOLO에 멀티 경로가 실행된다는 증거가 아니다.
6. **Main 검토 후 정지:** Main이 증거를 독립 확인하고 문서 검사 실행 결과를 전달한다. source root fault 후보가 있어도 runtime 원인·수정 성공으로 승격하지 않는다. 이후 교정은 사용자 승인 대기다.

## 제약과 완료 기준

Domain→Core/Unity, Application→Infrastructure/NGO 역참조를 피하고 GameBootstrapper 조합 루트를 유지한다. XZ domain 좌표와 Red `2*center-pos` 표현 변환을 한 번만 적용하는 계약을 읽기 기준으로 사용한다. 분석은 최신 main 작업 파일 적용을 요구하지 않는다.

초안 완료 기준은 두 문서 생성·범위 확인·Main 알림이다. 최종 분석 완료 기준은 SOLO 호출 체인과 변경 근거, 반증 가능한 관측점, 미해결 목록의 Main 검토까지다. 코드 보존/정합성 검사 성공은 네 증상의 실기 통과 판정이 아니다. 기존 dirty 파일을 보호하며 `.meta`는 생성하지 않는다.

Main의 현재 문서 검사는 exit 1/기존 참조 6건(전달값)이며, 목록과 메모리 기준선 알림의 경계는 Research 「문서 검사 경계」에 기록했다. 새 Task는 자동 검사에서 제외되고 `../../../` 링크는 Main 수동 확인이다. 다른 문서·검사기·메모리 기준선은 고치지 않으며 검사 PASS를 주장하지 않는다.

## 2026-10-14 분석 인계 시점 — 구현 승인 전 이력

두 investigator 최종 인계와 Main 독립 waypoint/history/에셋 확인을 Research 「2026-10-14 최종 코드 감사 — 현재 판정」 절에 통합했다. 위 분석 순서는 최초 계획의 스냅샷이다. SOLO waypoint flag 결함과 unsafe nonwalkable-goal 경로를 구분했으며, Factory의 최종 cooldown/marker 덮어쓰기 후속 근거로 config-vs-clip runtime 주기 결함 가설은 철회했다. Factory 최종값 덮어쓰기 코드 사실은 Main 직접 재독으로 독립 확인 완료했으며 runtime은 미확정이다. 같은 타겟 반복 scheduling·Walk·중간 광산 통과는 조건부/미해결이다.

이 분석 인계 시점에는 교정 구현·랠리 범위 확대가 승인되지 않았고 새 실기·TC/QA·build·소스/에셋/메모리 변경도 없었다. 이후 사용자 구현 승인은 상단의 좁은 SOLO 범위만 변경한다. 사용자 runtime 원인 확정과 해결 판정은 여전히 OPEN이며, 필요한 증거와 기존 로그 관측점은 Research에 기록했다.
