# RabbitTrickster 공격 클립 명시 연결 계획

토끼 유닛의 공격 시간은 실제로 재생하는 공격 동작에서 읽도록 고정합니다. DustSpirit에 있는 연결 기능을 그대로 쓰고, 연결이 다시 빠지거나 다른 동작으로 바뀌면 기존 자동 검증에서 발견할 수 있도록 최소한만 보강합니다. 기존 로직을 제거하거나 공격 수치를 조정하지 않습니다.

**최신 판정(2026-09-28):** 사용자가 Attack의 타격 표식을 0:20으로 저장하고 설정·영구 검증 기대값을 맞춘 새 빌드를 육안 수용하여 Rabbit focused PASS/CLOSED다. 아래 계획과 실기 전 OPEN 문장은 당시 이력이며, 새 경기 근거와 한계는 문서 끝의 「새 0:20 빌드 실기 후 완료 판정」을 따른다.

## 승인·역할·상태

- 사용자 문서·구현·빌드 진행 승인에 따라 메인 문서 검토와 game-programmer 구현, 메인의 두 파일 patch 검토까지 완료됐다. 문서 담당은 상태 동기화만 수행한다.
- 당시 **명시 연결 유지 / 0.6666667초 production 계약 정합화 완료 / 교정 후 Unity 메뉴·새 빌드·새 실기 대기 / 타격 시각 우려 OPEN**. 아래 두 메뉴 PASS와 사용자 확인 빌드·실기는 0.18초 버전의 당시 이력이다. 새 값의 실제 접촉 프레임은 아직 계측하지 않았다.
- 근거의 상세 경로·GUID는 같은 폴더의 [Research.md](Research.md)를 따른다.

## 제한된 변경 항목(승인된 계획 원문 보존)

### 1. 실제 생산 등록 연결

대상: `Assets/_Project/Scenes/Game.unity` 한 곳.

실제 `Hexiege.Infrastructure.UnitFactory`(script GUID `ffbee3fe087401948ae349db70b1d746`)의 `_transcendencePrefabs`에서 `type.intValue == 25`인 행을 찾는다. Blue `f8057fd8cc6c0ec4fbab3017b937d894` / Red `651c84fc382894a48b555c1c17bfc9d6`가 맞는지 확인한 후, `attackTimelineClip`에 실제 Attack GUID `8d21000f295b9774a9cb2540a71b8ce7` / fileID `7400000`을 연결한다. 다른 컴포넌트의 타입 25 행이나 다른 유닛은 변경하지 않는다.

근거: `GameSystemRules_Units.md` 규칙 17 및 `U-ATK-TIMELINE`, `GameSystemRules_UnitCombatSynchronization.md`의 `NET-PRESENT-002`. 이미 존재하는 UnitFactory 명시 선택 경로를 재사용하며 런타임 선택 알고리즘은 변경하지 않는다.

### 2. 기존 self-validation 최소 보강

대상: `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`.

DustSpirit production 검증 형태를 재사용해 Rabbit 연결 검증을 기존 실행 경로에 추가한다. 별도 검증 프레임워크·런타임 계측은 만들지 않는다.

- 실제 씬 UnitFactory의 해당 등록이 정확히 한 개이며, 명시 참조·양 팀 프리팹·양 팀 Controller·정확한 `Base Layer/Attack` motion이 같은 Attack을 가리키는지 확인한다. speed 1과 root motion 비활성도 보존한다.
- 같은 생산 추출 함수가 Attack 길이 2초와 단일 marker 0.18초를 읽고, 기존 config 2초 / `[0.18]`와 일치하는지 확인한다. 불일치는 데이터를 임의 보정하지 않고 검증 실패로 보고한다.
- 실제 Attack3를 앞에 둔 목록과 순서를 뒤집은 목록, 목록이 없는 경우에도 명시 Attack이 선택되는지 확인한다. 명시값 없는 alternate-first 경우는 Attack3 / 1.1초 / marker 없음이라는 위험을 재현하는 비교 입력으로만 사용한다.
- 서버/싱글·Client 양쪽이 기존 공통 선택·추출 경로를 유지하는지 확인한다. 기존 DustSpirit 등 검증과 Supported 상태는 그대로 보존한다.
- 씬 검증은 기존처럼 preview로 열고 finally에서 닫아 현재 Login 씬과 사용자 작업 상태를 바꾸거나 저장하지 않는다.

근거: `GameSystemRules_Units.md` 규칙 17·18, `GameSystemRules_UnitCombatSynchronization.md`의 `NET-PRESENT-001`·`NET-PRESENT-002`. 검증은 생산 연결의 퇴행 방지이며 실기 결과를 대신하지 않는다.

## 후속 진행 경계(최초 계획 이력 보존)

메인 검토 → game-programmer의 위 두 항목 구현 및 필요한 컴파일·기존 자동 검증 확인 → 승인된 빌드 시작 → 사용자의 빌드 완료·실기 확인 순서다. 현재 문서 담당은 어느 단계도 실행하지 않는다. TC 작성, QA 위임·실행, git 명령은 하지 않는다.

변경 금지: UnitStatsConfig 수치, Attack/Attack3 클립·이벤트·Controller·프리팹 내용, 다른 유닛 등록, resolver/manifest의 Supported 상태, 서버 피해 writer·표현 소유권·Fox 예외. 추가 공통 코드 수정이나 수치 튜닝이 필요해지면 범위를 확대하지 말고 메인에 보고한다. 전체 유닛 migration·역할 교대·rollback 완료도 주장하지 않는다.

문서 변경은 이 폴더의 Research.md / Plan.md뿐이다. 문서 정합성 검사는 WORKFLOW [11]에 따라 읽기 전용으로 수행하되, `_Tasks/`는 검사 대상 밖이므로 0건을 이 계획 자체의 자동 검증 PASS로 해석하지 않는다.

## 구현 결과(2026-09-28 추가)

- **구현 완료:** `Game.unity` 실제 UnitFactory의 Rabbit 행에 지정 Attack 참조 한 행 연결. `RunUnitActionSelfValidation.cs`에 Rabbit production gate와 실행 호출 추가. 문서 담당이 현재 파일에서 직접 확인했다.
- **계획과 차이:** 메인 patch 검토 전달 기준 bounded 두 파일 범위 유지. 런타임 공통 코드·수치·클립·다른 유닛·Supported 상태는 변경하지 않았다.
- **미완:** Unity 메뉴 검증 대기, 빌드 미시도, 실기 OPEN. 검증 코드 추가를 자동 검증 PASS로 기록하지 않으며 전체 migration 완료를 주장하지 않는다.
- **이번 문서 갱신:** Research/Plan과 상시 네 파일(AssetMatrix Rabbit 두 행, Units 규칙 17, PROJECT_STATUS, ROADMAP)만 동기화한다. 최초 문서 전용 범위는 위 이력으로 보존한다. TC/QA/git·메모리 갱신은 수행하지 않으며 최종 문서검사는 메인이 담당한다.

### 자동 검증 후속 결과(2026-09-28)

메인이 실제 Unity 메뉴에서 15:34:26 `[UAS-DIAG]` 및 Rabbit explicit Attack/Attack3-first를 명시한 `[UAS-DIAG][PRODUCTION-TIMELINE]` PASS, 15:35:16 `[UAS-ROOT-CROSS-AUDIT] self-validation PASS`를 확인해 전달했다. Console error 0 / warning 1이며, 경고는 메인 설명상 앞선 동시 새로고침 source version warning이다. 최종 재컴파일 이후 현재 error 0을 확인했지만 warning 0으로 바꾸어 기록하지 않는다.

위 구현 직후의 검증 대기 기록은 이력으로 남긴다. 메인이 문서검사 0건 확인 후 Unity File → Build And Run을 클릭했고, 직후 `Checking prerequisites / Starting Android build` 화면을 직접 확인해 전달했다. 현재는 **빌드 시작(완료·실기 사용자 확인 대기) / 실기 OPEN**이다. 사용자 지시대로 완료 확인은 하지 않았으며, 시작 화면과 자동 검증 PASS를 빌드 성공·실기 PASS·전체 migration 완료로 확대하지 않는다.

## 최신 실기 후 교정 접근 제안(2026-09-28, 구현 승인 전)

사용자는 빌드 후 같은 세션의 Host/Client 경기를 확인했고, RabbitTrickster의 피해 적용이 공격 애니메이션에 비해 약간 빠르게 보인다고 보고했다. [Research.md](Research.md)의 `sharedSessionKey=7d4f85a6b980b02035e532b36068e5ca349ae35610294a423d3a4ebe2e2654a5` 로그는 결과 전달과 표현 집계를 뒷받침하지만 실제 접촉 프레임이나 이른 정도를 측정하지 않는다. 위 문단의 ‘빌드 시작’은 그 시점의 기록이며 최신 상태를 대체하지 않는다. **타격 시각 판정은 OPEN**이다.

향후 교정은 다음 순서의 **제안**이며, 이번 문서 요청은 변경 승인이나 구현 지시가 아니다.

1. Unity의 실제 `Base Layer/Attack` 생산 클립을 프레임별로 확인하여 공격 동작의 실제 접촉 프레임을 측정한다. 필요하면 동일 조건의 Host/Client 화면과 결과 표시 시점을 대응시켜 관찰 차이를 기록한다. 로그 집계만으로 접촉 시각을 추정하지 않는다.
2. 측정 결과와 현재 `OnAttackHit @ 0.18초`를 비교한 뒤에만 타이밍 변경 여부를 판단한다. 변경이 필요하다면 생산 클립의 marker, `UnitStatsConfig`의 hit 값, 명시 클립 연결과 영구 production validation gate를 한 계약으로 검토한다. 임의 오프셋을 넣거나 0.18초를 지금 바꾸지 않는다. 근거는 `GameSystemRules_Units.md` 규칙 17이다.
3. 변경을 별도로 승인받아 적용하는 경우에도 권위 피해는 기존 서버 타이머가 실행하고, Client는 서버 확정 결과만 표현한다. 로컬 Animation Event를 피해 writer로 만들거나 네트워크/표현 경로를 재작성하지 않는다(`GameSystemRules_Units.md` 규칙 18, `GameSystemRules_UnitCombatSynchronization.md`의 `NET-PRESENT-001`·`NET-PRESENT-002`). 수정 후에는 생산 연결 자동 검증과 같은 조건의 실기 시각을 다시 확인해야 한다.

현재는 **문서화만 완료**했다. 코드·설정·애니메이션 값은 변경하지 않았고 타격 시각 PASS나 전체 migration 완료를 선언하지 않는다.

## 사용자 지정 marker 후속 교정 결과(2026-09-28 현재)

이 절은 위의 **당시 제안·금지 범위·0.18초 기록을 소급 수정하지 않고** 후속 승인·작업 결과를 기록한다. 사용자가 Unity Animation에서 `RabbitTrickster_Attack`의 단일 `OnAttackHit`을 0:20(30fps, 파일 저장값 0.6666667초)으로 옮겼다. 이전 `7d4f85a6…e2654a5` Editor Host/Android Client 경기는 옛 0.18초 버전이며 새 값의 동기 검증이 아니다.

| 수정 항목과 확인 결과 | 규칙 근거 | 남은 검증 |
|---|---|---|
| 사용자 저장 Attack 클립: 2초/30fps, 단일 marker `0.6666667초`; 기존 Game 씬 type 25 명시 GUID 연결 유지. `.anim`은 이 문서 작업에서 편집하지 않음 | `GameSystemRules_Units.md` 규칙 17, `U-ATK-TIMELINE` — 실제 생산 Attack marker와 선택 클립의 단일 시간축 | 새 Unity production gate 실행 |
| 별도 game-programmer가 `UnitStatsConfig.asset` type 25 `hitFrameTimes`를 `[0.6666667]`로 동기화. 실제 파일에서 확인, attackCooldown 2초 유지 | 규칙 17, `U-ATK-TIMELINE` — marker와 서버가 읽는 타격 offset 정합성; 규칙 18 — 피해 writer는 서버 타이머 유지 | 새 빌드에서 권위 피해 시각 확인 |
| `RunUnitActionSelfValidation`의 Rabbit production 기대값을 2초·marker 1개/0.6666667초로 교정하고 설정과 추출 marker의 일치 검사는 유지. 실제 코드의 호출·기대값·대조를 확인 | 규칙 17, `U-ATK-TIMELINE` — 생산 클립 회귀 gate; 규칙 18 — 로컬 event를 피해 writer로 전환하지 않음 | 변경 후 Unity 메뉴 PASS 미확인 |
| 이 Plan·Research 및 지정된 상시 문서만 현재값/증거 경계로 동기화. 과거 0.18초 경기 기록은 당시 이력으로 유지 | 규칙 17·18, `U-ATK-TIMELINE` — 설정 계약과 실기 타격 판정을 분리 | 새 Editor Host/Android Client 실기와 사용자 시각 확인 |

**현재 판정:** 새 사용자 지정값의 production 계약 정합화와 설정·검증 코드 교정은 완료. 이번 교정 후 Unity 메뉴, 새 빌드, 새 실기는 아직 수행되지 않았으므로 타격 시각 focused OPEN이다. 새 Testcase는 만들지 않으며 Supported 상태·서버 피해 경로·전체 migration 판정은 변경하지 않는다.

## 새 0:20 빌드 실기 후 완료 판정(2026-09-28)

위 OPEN은 실기 전 상태 기록이다. 사용자가 새 빌드를 육안 확인하고 Rabbit 공격이 “알맞게 나오는 것 같다”고 수용했다. 같은 `sharedSessionKey=3fc75ef4357e734ee1db9e95b7a20192e7920086df0e531e1425ea234b7beded`의 Editor Host/Android Client 로그에서 Rabbit 각 15기·LittleKnight 각 13기 생산, 경기 전체 Host 138 schedules/129 results/resultFailures 0 ↔ Client 129 accepted/0 rejected, 양쪽 필수 표현 122/122와 실패·중복·전송 실패 0을 확인했다. Host commits 113/correlationFailures 0/targetMismatches 0/drop 0, 양쪽 local ROOT PASS/errors·drop 0 및 MOVE END EVIDENCE다. 상세 로그 경계는 [Research.md](Research.md)의 후속 증거 절을 따른다.

**현재 판정: Rabbit 명시 Attack 연결·사용자 지정 0:20 타이밍 focused PASS/CLOSED.** 근거는 규칙 17·`U-ATK-TIMELINE`의 생산 marker/config 일치, 규칙 18의 서버 타이머 유지, 새 빌드 사용자 시각 수용과 동일 경기 결과·표현 집계다. 수치는 Rabbit 전용이 아니고 정확한 animation→권위 피해 offset은 미계측이다. 공식 Root Pose CrossAudit, 25종 전체/역할교대/Legacy rollback/C3 전체 migration은 완료되지 않았다. 성능 렉은 별도 결함이다. 새 Testcase·다음 유닛 Task는 만들지 않으며 다음 교정 유닛도 사용자 선택 전에는 확정하지 않는다.
