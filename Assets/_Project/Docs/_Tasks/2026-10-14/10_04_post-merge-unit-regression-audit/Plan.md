이번 SOLO 공통 이동 복귀는 사용자 2회 싱글 테스트 육안 수용과 Main runtime 분석에 한정해 **focused PASS/CLOSED**다. controlled 검증과 최신 source 세대 보존은 별도 근거로 유지한다. native 메뉴 실행 PASS·전체25종·멀티 동등성·global PASS로 확대하지 않으며 랠리·광산은 사용자 명시 제외다. 추가 코드·빌드·TC·새 Task 없이 기존 문서에 결과를 반영한다.

# Plan — SOLO 추격 진행 및 타격 타이밍 교정

## 사용자 승인 main 반영 — 병합·원격 push 완료

Main 실제 결과 인계에 따라 기존 교정 focused PASS/CLOSED를 유지한 채 feature20파일 commit `dbc48a8c`→main `88f60e38` fast-forward→no-ff merge `21897ddc`→원격 main push(`88f60e38..21897ddc`)를 완료했다. 원시 로그 tracked/staged0·로컬 untracked 로그/meta 보존을 확인했다는 Main 전달 근거이며 문서 담당의 Git 재확인이 아니다. 문서 담당은 Git·충돌 조율·코드 수정·새 Task 작성을 하지 않는다.

상세 충돌 해결·보호 비교·정적 검사·checker·로그 제외·잠금 정리는 [Research의 완료 영수증](Research.md#main-merge-publication) 한 곳에 기록했다. native self-validation·build 신규 실행은 없고 병합·게시를 새 gameplay 수용 또는 전체 동등성 PASS로 확대하지 않는다. 이번 Research·Plan 영수증 수정 자체의 추가 commit·push는 Main 후속 예정으로, 기존 push 완료와 구분한다.

**사전 계획 이력의 후속:** 당시 fetch·분기 확인과 예상 meta 충돌은 Research에 원문을 보존했다. 이후 실제11충돌 main GUID 보존 해결·보호 diff0·병합 tree 정적 errors0·checker23 tests OK를 수령했다. 문서 참조 기존6오류와 meta 빈 필드 whitespace는 한계로 유지하며 전체 무오류로 바꾸지 않는다. 추가 문서 검사·Git은 Main 담당이다.

## 2026-10-15 공통 이동 복귀 — SOLO focused PASS/CLOSED

[Research의 새 2경기 최종 분석](Research.md#solo-return-two-match-acceptance)에 사용자 육안 수용·RETURN episode/coverage·미종결 후속 분류와 관측 한계를 통합했다. 수정 전 provider 재현과 기존 checkpoint 성공은 당시 근거로 보존하며 이번 수용은 새 경기 증거에 근거한다. 전체 전투/멀티·전25종·native 메뉴 PASS는 판정하지 않는다. 상세 수치는 Research 한 곳에 유지한다.

- **승인된 목표:** SOLO 추격 중 실제 위치와 논리 타일의 정합성을 유지하고, 대상 소실 뒤 다른 대상 획득 또는 원래 이동의 재개를 확인할 수 있도록 교정·진단 로그를 함께 반영한다. 아래 구체 계획은 수령한 설계에 대한 Main 검토 승인 내용이며 문서 담당의 추가 설계가 아니다.
- **기존 규칙 근거:** [Units](../../../GameSystemRules/GameSystemRules_Units.md)의 이동 규칙 3(싱글 동일 구조), 규칙 8(전투 종료 후 이동 재개 정렬), `U-MOV-ALIGN`의 7·8·11항(비 스냅 복귀·공통 권위 이동·같은 타일 중심 복귀), `U-MOV-REPATH`의 5~8항(진전·유효 경로·보류와 완료 구분)을 유지한다. 규칙 변경 필요성이 발견되면 Main에 먼저 보고하며 이번 문서 갱신으로 새 정책을 만들지 않는다.
- **보존·제외:** 기존 공격·타격 타이밍과 멀티플레이 동작, 광산·랠리포인트, 성능·VFX 세부 조정은 변경하지 않는다. 스냅·거리 guard 완화·새 유닛별 예외·대기 시간 제거는 승인된 해결책으로 간주하지 않는다. 기존 로직 제거 계획은 없으며 제거 필요 시 근거와 승인 범위를 먼저 명시한다.
- **현재 판정:** 이번 SOLO 공통 복귀 focused PASS/CLOSED다. 사용자 두 경기 육안 수용과 RETURN149행·공간586표본 및 open14 후속 분류를 근거로 하며 로그의 미종결을 이동 실패14로 바꾸지 않는다. Rabbit의 별도 target-loss resume 미관측은 유지하지만 그 이유만으로 강제 재시험하지 않는다. 기존 [controlled 영수증](Research.md#solo-return-final-receipt)과 runtime 수용은 별도 축이며 native 메뉴 실행 여부/PASS는 이 로그로 입증하지 못한다. 랠리·광산과 전체 동등성은 이번 판정 밖이다.

### 수령한 구체 교정 계획 — 승인 당시 계획 / 실제 결과는 Research 영수증

현재 pose를 강제로 옮기는 대신, 실제 이동이 검증을 통과한 타일 경계를 순서대로 내부 상태에도 반영하도록 한다. 대상이 사라진 뒤에는 이동 명령을 냈다는 사실이 아니라 실제로 다시 움직이고 위치가 일치한 사실을 복귀 성공의 근거로 남긴다. 규칙 근거는 위 Units 이동 계약이며 추가 게임 규칙 변경은 없다.

1. **공통 SOLO Legacy 이동 적용:** `UnitView.ApplyMovementAuthorityFrame`의 SOLO Legacy 분기에서 기존 `EvaluateSpatialCandidate → UnitSpatialTransitionPolicy → Preflight` 검증을 거친 뒤 Root 이동과 `CommitPreflightedSpatialTransitionsPreservingFacing`을 사용한다. A*·Chase·복귀 align 모두 실제 경계 전이를 순차 commit한다. 새 API 필요 시 구현 전에 Main에 보고한다.
2. **중복·상태 보존:** SOLO endpoint의 기존 `ProcessStep`/`OnEntered`가 같은 전이를 중복 처리하지 않도록 하고 `prevActualTile`은 SOLO의 committed 값을 기준으로 한다. 멀티 Legacy 기존 분기는 그대로 보존한다. 기존 복귀 planner, `InvalidStart` guard, blocked budget, 공격 ownership, nonwalkable terminal contact 계약은 변경하지 않는다. 중복 처리 방지의 실제 변경 위치·방식은 적용 receipt로 확인하며 기존 공통 로직 삭제를 승인한 것으로 확대하지 않는다.
3. **확인 로그:** SOLO 개발 로그에 `start / provider / resume-success / blocked / cap / end`를 bounded 전이 기록으로 남긴다. 필드는 unit/type/mode/episode/RootHex/LogicalHex/distance/provider/reason이다. `resume-success`는 실제 비영 이동과 위치 정합을 확인한 뒤에만 기록하고, 매 frame 출력은 하지 않는다. 정확한 상한·포맷·적용 위치는 구현 receipt를 기다리며 문서 담당이 수치를 정하지 않는다.
4. **예정 코드 범위 — 총 2파일:** `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`와 `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`다. **2026-10-15 Main 독립 검토 정정:** 문서 담당이 「UnitView와 SelfValidation 파일 2개」로 오독했던 표현을 바로잡았다. SelfValidation은 1파일이며 총 3파일 계획이 아니다. 나머지 계획은 Main 적합 판정·코더 조건부 GO 전달 상태다. 실제 적용 manifest·검증 결과는 별도 인계를 기다린다. 새 API·범위 확대 필요 시 먼저 보고한다. 문서 담당은 코드·자산을 수정하지 않는다.
5. **예정 검증:** 실제 공용 policy·commit·역 index·provider 재현 경로, 중복 event 방지, 비인접 전이, 거부 시 무변경을 검증하고 actual View native 검증 gate를 기존 SelfValidation에 준비한다. Runtime/Editor 정적 compile을 구분해 기록한다. gate 준비·fixture 실행·native 실행은 서로 다르며, native 실행·실기·빌드는 이 계획 수령으로 수행되거나 통과한 것이 아니다. 기존 수정 전 재현을 새 교정 결과로 재사용하지 않는다.

상세 실행 결과는 [Research](Research.md#solo-return-final-receipt) 한 곳에 모으고 PROJECT_STATUS·ROADMAP·WORK_HISTORY·Matrix는 요약·링크만 갱신한다. 위 목록의 예정·인계 대기는 승인 당시 계획을 보존한 것이며 현행 적용·검증 경계는 상단과 Research 영수증을 따른다. programmer 기존 메모리 교훈 추가는 receipt에 기록하며 공용·문서 담당 메모리는 중복 교훈/구조 변경이 없어 추가하지 않는다. 최종 문서 검사·성장 baseline은 Main 담당이다.

## 2026-10-15 사용자 싱글 경기 분석 반영 — 당시 분석 단계 이력

상시 승인에 따라 이번 경기 결과를 [Research의 단일 상세 기록](Research.md#solo-match-2026-10-15)에 통합했다. Fox 조기 피해·근접 불발의 관측 범위 재발 근거는 없고 UI 수신·방출의 같은 frame 대응도 독립 확인됐다. 다만 상한 뒤 UI의 Due·charge VFX/SFX·정확한 native 예약 상관·픽셀 표시는 확정하지 않는다. Bloom 종료 경고는 피해 누락 증거와 구분하며 새 코드 수정이나 추가 로그·빌드·전체 재테스트를 요구하지 않는다. 기존 clock/pursuit 승인·개별 수용은 유지하고25종·멀티·시각 전체 판정은 확장하지 않는다. 아래 controlled 검증·실기 미실행 표시는 이전 구현 인계의 경계이며 이번 경기 관측으로 전부 덮어쓰지 않는다. 문서 검사는 Main이 실행한다.

## 2026-10-14 Fox 원래 의도 복원 — 경기 전 구현·controlled 검증 인계 이력

**현재 승인 범위:** 원래 수용된 교정의 입증된 소실/오적용 복원만이다. 기존 승인·적용된 SOLO clock·pursuit 교정은 유지한다. 신규 start/impact gate 통합·native lease migration·멀티 gate 복사·overlap/ZeroException 사양만 미승인 범위 확장으로 철회한다. Main의 범위 확장 정책 질문은 철회했고 사용자가 이 신규 정책을 결정해야 원래 복원이 끝난다는 전제도 철회한다. 원래8종 표기 차이는 missing-fix 목록이 아니다. Fox 원래2:25 명확화로 수행한8파일 적용과 현행 숫자 문서 갱신은 이 승인 범위에 속한다.

**Fox 기대값 정정:** 사용자 원래 피해 표기 `2:25@30fps`는 `85/30 = 2.8333333초`다. 2.25초는 잘못된 환산·mirror baseline이며 새 정책의 이전 승인값이 아니다. clip의 damage event2.8333333초는 보존하고 typed charge1초를 추가한 실제 source 적용을 확인했다. 아래 옛 literal·로그·수용 문단은 이력으로 남기되 원래 의도를 판정하는 현행 oracle로 쓰지 않는다. native callback은 HP를 직접 쓰지 않고 SOLO/멀티 양 모드의 권위 설정·Factory 소비·타이머 예약이 피해 기대값을 적용한다. 이는 원래 의도 복원이지 새 타이밍 튜닝이 아니다. source 적용·구현 담당 controlled 검증·Main 독립 검증 및 native/실기 수용을 분리한다.

1. **작업 보존·이전 — 완료:** Main은 보존 커밋 `532e2033`의 37파일을 `codex/unit-movement-attack-sync-audit`로 FF 이전했다. 문서 담당 현재 ref 확인에서 HEAD/두 브랜치가 같은 full hash다. Main byte 비교 전달값 exact10/line-ending-only27/other0/missing0은 Windows CRLF 변환 외 내용 소실 없음의 근거이며 문서 담당 독립 byte 비교는 아니다. Main Push 없음; 이 단계는 기존 작업 보존이지 새 복원 코드 적용이 아니다. 상세 receipt는 [Research](Research.md)의 현행 절에 둔다.
2. **기대 계약 고정 — 현행 기준:** 마지막 사용자 수용 교정에서 SOLO expected를 도출하고 입증된 누락/오적용과 비교한다. original 차이8종은 자동 교정·missing-fix 목록이 아니다. Fox2.25초 오류는 사용자 원래2:25 의도 명확화로 복원했으며 새 일반 유닛 정책이 아니다. [Units](../../../GameSystemRules/GameSystemRules_Units.md) 규칙 17·18 및 `U-ATK-TIMELINE`의 marker/권위 타이머 분리 구조를 유지한다. 이전 규칙의2.25 문구는 역사적 잘못 환산된 기준으로 분리했고 현행 Fox 수치·typed callback 문서도 실제 source와 정합화한 상태다.
3. **Fox 구현·검증 receipt — Main 독립 controlled GREEN 추가:** full8 manifest와문서 담당 해시 일치는 [Research](Research.md#fox8파일-적용-receipt--구현-담당-검증과-main-독립-검증-분리)에 보존했다. 구현 담당 RED→GREEN에 더해 Main 새 Runtime/Editor/Probe compile·Mono Probe exit0를 수령했다. actual YAML2.83333325 입력에서2.25초 HP200 유지·serialized due2.8333332538604736 피해1회/immediateTrue, 실제 Factory predicate=True·기존 validator authority core PASS·formatter995bytes/C3v12가 Main 독립 한정 결과다. Main RED나 AssetDatabase/native Animator/relay fixture·메뉴·멀티/시각/build PASS는 아니다. curve prefix·최신8manifest 보존,16종·Spirit9종 합본 감사와 Main 최종23종 실행까지 완료됐으며 아래 구현 전 미실행 계획은 이력으로 보존한다.
4. **Fox 현행 문서 동기화 — source 적용·Main 한정 독립 GREEN 반영:** StatsReference/Units/CombatSync/Matrix의 Fox85/30·typed charge1초·Factory 소비·권위 HP 경계를 유지하며 Main 실제 controlled 결과를 추가했다. VFXSFXList는 기존 숫자 변경 대상이 없어 무변경이다. native/멀티/시각 미실행과 Main prefix·최신8manifest 보존·25종 감사·최종23종 실행 완료를 분리하고 옛 Task 이력은 재작성하지 않는다. General source4 제안은 미구현 범위 확장으로 철회됐으며 원래 복원의 진행 대기 patch가 아니다. 이번 후속 GO는 기존7문서의 현행 표시 정정과 WORKFLOW에 따른 실제 범위 확장 실수의4칸·목차 기록이다. 새 사양·Task·TC/QA·메모리는 만들지 않는다.

### 기존 로직 보존·제거 경계

**2026-10-15 최종 종결:** [Research의25행 합본 감사](Research.md#artifact-restoration-audit)에16종·Spirit9종 최종 결과와 Main 최신 독립 실행·산출물을 통합했다. 25종 반영 감사 완료·추가 확정 누락 없음, 일반23종·25후보 controlled PASS, Torrent·Bloom 정적 연결 확인이다. 기존 개별 수용을 유지하며 native 미실행을 구현 미완이나 전체 재테스트 요구로 바꾸지 않는다. 상태3문서는 요약·링크만 반영했다. 최종 문서 검사는 Main이 실행한다.

기존 `OnAttackHit` 처리 본문을 private `ProcessAttackMarker(bool chargeOnly)` helper로 옮겨 보존한다. 다른 유닛은 `OnAttackHit`에서 `false`로 호출해 기존 동작을 유지한다. 공통 처리 로직을 삭제하거나 다른 타입의 정책을 변경하는 작업이 아니다. Fox만 charge/impact를 분리하고, 과거 charge marker를 이유로 두었던 Factory의 Fox 제외 조건을 없앤다. 근거는 [Units](../../../GameSystemRules/GameSystemRules_Units.md) 규칙 17의 실제 `OnAttackHit` 추출, 규칙 18의 권위 타이머 피해, `U-ATK-TIMELINE`의 표현 marker와 권위 예약 분리다. 이 계획의 구체 차이는 사용자 원래 `2:25` 의도 복원이며 오래된 Fox2.25 수치 예외를 새 기대값의 권위로 사용하지 않는다.

### 구현 담당 최종8파일 변경 목록 — Main 기술 승인 근거

| 파일 | 승인된 변경 | 보존·금지 경계 |
|---|---|---|
| `Assets/_Project/Animations/Units/FoxMagician/FoxMagician_Attack.anim` | events만 변경: `OnAttackCharge @ 1초` 추가, 기존 `OnAttackHit @ 85/30초` 보존 | 기존 OnAttackHit 인자 보존; events 외 본문 해시 불변을 증명. clip 길이/loop/curve/모델 변경 없음 |
| `Assets/_Project/Scripts/Presentation/Unit/AnimationEventRelay.cs` | `OnAttackCharge(AnimationEvent)`를 UnitView에 중계 | 기존 OnAttackHit 중계와 다른 유닛 동작 유지 |
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | 기존 본문을 `ProcessAttackMarker(bool chargeOnly)`로 보존. 다른 유닛 OnHit는 false. Fox charge는 true로 기존 lease→VFX/SFX 경로를 사용하고 즉시 return. Fox OnHit는 native Impact metadata만 기록하고 return | charge는 recipient observe/LocalAttackHit/tracer/HP까지 도달하지 않음. Fox OnHit는 VFX 추가 재생·charge lease 소비 없음. Root/일반 marker 동작 유지 |
| `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs` | `ShouldUseAttackClipHitFrameTimes`의 Fox 제외 조건 제거, `markerCount > 0`이면 추출값 사용. SOLO/server/client 생성 finalHit가 actual `OnAttackHit @ 85/30`을 사용 | 기존 extractor는 OnAttackHit만 읽으므로 charge는 피해 offset 수집에서 제외. 다른 유닛 기존 marker 소비 유지 |
| `Assets/_Project/Resources/Config/UnitStatsConfig.asset` | type21의 hit fallback만 `85/30초`로 정합화 | 다른 필드·행·유닛 스탯 변경 없음. clip 미사용 fallback과 실제 생성 finalHit를 따로 확인 |
| `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs` | expected Impact를 `85d/30`로 정정하고 formatter/self-fixture를 정합화. typed charge/VFX와 damage metadata를 구분 | 읽기 전용 observer 유지; 진단이 HP·예약·clock을 쓰지 않음. 기존 bounded/terminal 계약 보존 |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | 기존 Fox gate에서 actual typed asset 이벤트·config·Factory·observer를 검증하고 real writer Probe 실행 근거를 마련 | resolver/Supported 승격 없음. 문자열 존재 검사만으로 실제 writer 검증을 대신하지 않음 |
| `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs` | Fox 주석의 잘못된 피해 숫자만 정정 | HP writer·예약 로직·Immediate bool은 변경하지 않음. writer 실행은 기존 실제 경로로 검증 |

Root/모델/VFX 에셋·preset·SFX·다른 유닛 수치·resolver는 변경하지 않는다. source-only lease 보존과 일반 유닛 동작을 유지하며 Fox charge에서 recipient 채널을 발화시키지 않는다. Factory가 damage marker를 소비하는 것과 native callback이 HP를 적용하는 것은 별개다. **native HP writer 추가 금지**이며 현재 권위 writer만 피해를 적용한다.

### 실제 실행 검증 계획 — 구현 전 이력 / 현행 결과는 상단 receipt

- **RED:** 실제 실행 전이며 `NOT RUN`이다. 준비된 fixture·기존 검사 PASS·과거2.25 로그를 이번 RED 실행으로 기록하지 않는다. 구현 담당이 실제 실행·assertion·exit와 입력 세대를 인계한 뒤에만 RED/GREEN으로 표기한다.
- **실제 writer Probe:** captured target에 대해2.25초에는 HP 적용이 없고, 첫 due `85/30초`에 정확히 한 번 권위 HP를 적용함을 확인한다. charge callback은 HP/LocalAttackHit/tracer를 발생시키지 않고 damage OnAttackHit은 charge VFX를 추가 재생하거나 lease를 소비하지 않음을 실제 경로로 검증한다. SOLO/server/client Factory 최종값과 fallback 정합을 구분해 확인한다.
- **기존 Fox gate:** charge1초·damage85/30의 함수명/인자/typed 의미, events 외 clip 해시, config type21 한정 변경, Factory 양 모드 소비, observer expected/terminal fixture, 기존 resolver·VFX 참조 불변을 확인한다. 다른 유닛의 helper false 경로 보존도 확인한다. gate 준비는 native 메뉴 실행 PASS가 아니다.
- **컴파일 도구·참조:** Main은 실제 설치 `D:/dev/6000.3.5f2/Editor`의 csc `-version` exit0/Roslyn4.3.1 및 참조372/404개 존재, 원래 Bee C 경로의 D 설치 경로 remap 확인을 인계했다. 이는 컴파일 환경 확인이지 프로젝트 컴파일이나 fresh native 검증이 아니다. 구현 담당은 원래 response 파일 경로와 해시, 각 원래 C 참조→실제 D 참조의 exact path 매핑, 유지한 프로젝트 참조, 누락/해소 결과, 실제 csc 경로·명령·출력 파일·exit/errors/warnings를 receipt에 남긴다. exact C prefix/참조별 목록은 아직 이 문서에 전달되지 않았으므로 임의로 생성하지 않는다.
- **출력 분리:** 실제 D Unity 도구와 remapped 참조로 별도 scratch 출력 디렉터리에 컴파일한다. `Library` DLL·Bee 원본 response·Unity 로드 산출물을 덮어쓰지 않는다. old C paths 그대로 또는 다른 설치 참조로 검증했다고 주장하지 않는다. 이 문단은 구현 전 출력 계획이며 최종 실제 컴파일·실행 완료 결과는 Research의 반영 감사 절을 따른다.
- **동작 코드 보호:** UnitCombatUseCase는 주석 수치만 변경이므로 문자열 리터럴을 보존하는 동작 본문 비교로 writer/Immediate 무변경을 증명한다. shared helper는 전체 삭제가 아니라 보존·분기라는 diff 근거를 함께 인계한다.

### 후속 문서·수용 경계

**현재 인계 경계:** Fox controlled HP GREEN에 더해25종 반영 감사·Main 최종23종 실행과 기존 두 fixture PASS를 수령했다. 기존 clock/pursuit는 보존됐고 charge1은 impact85/30 lease를 소비하지 않는다. 새 일반 gate·native lease·overlap 제안은 철회했으며 실기 수용·전체 migration 완료와 구분한다.

### 철회된 범위 확장 제안 — 원래 복원 blocker 아님

Main이 제안했던 General23 통합과 동일 XZ 대상의 새 overlap 정책 질문은 원래 구현 복원에서 벗어난 범위 확장이었으며 사용자 명확화로 철회됐다. 실제 General gate/native lease migration/복사된 멀티 gate/overlap 예외 규칙은 적용하지 않았다. 기존 멀티 zeroAim/InvalidPose와 SOLO Legacy 공격 허용 차이는 기존 동작의 비교 사실이지 새 정책 선택이 필요한 복원 blocker가 아니다. 이 Task에 신규 ZeroException 미래 사양·각도 우회·snap/aim fallback을 추가하지 않고 generic source 작업도 진행하지 않는다. 기존 관측 사실을 지우거나 전체25종 현재 실행이 해결됐다고 바꾸는 정정이 아니다.

**구현 전 문서 반영 계획 이력:** 실제 patch와 Main 확인 이후 Fox 현행 항목만 StatsReference/Units/CombatSync/Matrix 및 숫자·marker 주장을 가진 VFXSFXList에 반영한다. status/ROADMAP/WORK_HISTORY도 실제 적용·검사 receipt 범위만 요약한다. 옛 Task의 날짜별2.25 실행·수용 이력은 지우거나 소급 변경하지 않고 이번 Task 현행 정정으로 연결한다. 적용·static/CLR·native·빌드·사용자 경기 수용은 별도 축이며 native/빌드/게임 PASS를 미리 적지 않는다. GeneralSoloEntry 범위 확장 제안은 철회됐고 전25종 반영 감사는 최종 수령·종결했다. Fox 계획 승인이나 구현이 전체 유닛 해결을 뜻하지 않는다.

이번 최종 문서 범위는 기존 Research/Plan·상시4문서의 Fox 항목과 별도 승인된 상태3문서·실수 기록이다. 코드·에셋 구현은 구현 담당의8파일 범위이며 문서 쓰기와 겹치지 않는다. 새 Task·TC/QA·메모리·Unity/build·Git 쓰기는 수행하지 않는다. 최종 문서 검사는 Main이 실행하며 문서 담당은 중복 실행하지 않는다.

이번 새 문서 검사는 UTF-8 출력으로 실제 완료했으며 exit1/기존 참조6건, 고아·감소 없음이다. 오류 문서와 기준선은 범위 밖이라 수정하지 않았다. Task 자동검사 제외·상대 링크 별도 확인 및 초기 실행 환경 실패는 [Research](Research.md)의 「이번 실제 문서 검사」에 기록했다. 아래 과거 「검사 재실행 없음」 문장은 당시 이력이지 이번 실행 상태가 아니다.

## 이전 전 유닛 historical-contract 감사 — 보고·Main 검토 이력 / 현행 복원 계획은 위 절

기존 Task 두 문서만 사용한다. 먼저 모든 실제 UnitType 이름별로 옛 Task 최종 사용자 승인/수용과 후속 변경을 추적해 기대 marker/피해 offset 예외/cycle/모델 방향/VFX 전달·복구와 출처를 Research에 모은다. 감사표·status는 옛 Task를 찾는 인덱스일 뿐 타이밍 권위가 아니며 현재 config를 기대값으로 재사용하지 않는다. 명확하지 않은 값·수용 범위·서로 다른 날짜의 주장은 미확정/변경 이력으로 분리한다.

Research의25행 historical 표와25행 현재 marker 표·최종 한정 판정에 대한 Main 검토를 완료했다. 두 담당 최종 감사 수령 및 Main 독립25clip/config/Factory·지정경로 diff 확인을 구분해 기록했다. Spear0:25는 옛 사용자 수용 원문으로 유지됨을 확인했다. 원래ss:ff 차이는 Little/Axe/Tank/Cannon/Dust/Rhino/Eagle와 Stream을 더한8종이며 모두 잘못/되돌아감이라고 판정하지 않는다. Axe/Dust/Rhino/Stream은 이미 옛Task의 explicit decimal artifact를 유지한다. Little/Tank/Cannon/Eagle의 원래frame 최종 의도 primary 부족과 Eagle 실제 Attack 선택·Quake 보조 dangling 참조는 이름별 한계다. Bear/Lion 실효주기1.6666/2는 기존 Factory clip-derived 정책 결과이지 새 cycle 변경/merge 회귀가 아니다.

10/14 shared SOLO clock의 일반23종 적용 경로 변경은 승인된 새 교정이며 clip reversion과 다르다(Torrent/Healer 제외 및 unsupported offset fallback 유지). 모든23종 실제 경기/시각 검증을 뜻하지 않는다. 과거 Fox peer-test 제안은 이전 focused 분석 당시 순서로 보존하며 현재 첫 우선순위로 유지하지 않는다. 이번 전 유닛 감사 보고는 완료했고 추가 교정·구현·전체 재테스트는 승인되지 않았다. Main 최종 source5 해시 불변, 새절 링크39개/누락0, check_docs 실제exit1/동일한 기존6건이다. 검사 재실행 없이 인계한다. 코드/Stats/클립/규칙/다른 문서·메모리·새 Task/TC·QA/Unity/build 변경 없이 원래 사용자 시각 우려·실기 수용 OPEN은 유지한다.

## 이전 SOLO 교정의 승인 경계 — 당시 적용 범위 이력

현재 사용자 승인 범위는 기존 SOLO pursuit 교정에 추가된 **SOLO impact timing 교정과 targeted bounded 진단**, 기존 [Research](Research.md)/이 Plan 갱신이다. 타이밍 소스 5파일 적용·Main 독립 검토/검증은 완료됐다. 문서 담당은 두 Task 문서와 별도 허용된 `.claude/mistakes.md`의 사건 추가만 편집한다. 광산·랠리·경로·네트워크·Stats·클립·프리팹·씬·Legacy→Reducer 전환 및 관련 없는 dirty 파일은 제외한다. 새 Task·TC/QA·status/roadmap/에이전트 메모리·상시 규칙은 작성/수정하지 않는다. native·빌드·실기 실행도 포함하지 않는다.

## 이전 타이밍 단계 — 적용·Main 독립 검증 근거 / 새 복원 완료는 아님

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

**[2026-10-15 후속 수용 — 당시 원문 유지: 위 OPEN은 분석 인계 당시 기록이다. 현재 이번 SOLO 공통 복귀는 사용자2경기와 runtime 증거의 focused PASS/CLOSED이며 상단 판정이 현행이다. 광산·랠리와 전25종·멀티/global 축은 별개다.]**
