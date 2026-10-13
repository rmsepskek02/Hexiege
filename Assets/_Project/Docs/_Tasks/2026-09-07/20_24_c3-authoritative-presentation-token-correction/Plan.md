# C3 공격 연출 토큰 교정 — 계획

공격 결과는 정상인데 화면 효과가 늦는 문제를 고친다. 공격의 예약 정보와 실제 타격 결과를 같은 공격 식별자로 전달하고, 화면 효과가 애니메이션 이벤트나 탄환 도착을 기다리지 않도록 책임을 정리한다. 서버의 피해 판정과 기존 에셋은 유지한다.

> 레거시 코드는 사용자 멀티 실기 PASS 전 삭제하지 않는다. 비교 검증을 유지하면서 단일 표현 writer로 전환할 준비를 갖추고, 전환 경기에서는 중복 방출을 막기 위해 기존 경로를 비활성화한다. manifest·시간축·실제 소비자 연결이 미완이면 비교 단계로 명시하며 시각 교정 완료라고 보고하지 않는다.

## 1. 범위와 근거

| 구현 항목 | 규칙 근거 |
|---|---|
| 예약 토큰과 확정 결과 토큰 구분 | NET-ACTION-SEQ, NET-ACTION-IDEMPOTENT |
| commit 의도/Impact 최종 방향 구분 | NET-FACING-002, NET-PRESENT-003 |
| Marker 선택적 소비와 tracer 권한 제거 | NET-PRESENT-001~003, Units 규칙 19·20 |
| 공통 표현 시각·지연 결과 처리 | NET-TIME-002~005 |
| 취소/사망 뒤 확정 결과 보존 | NET-CANCEL-003~005 |
| 명시적 완료 manifest 기반 AoE | Units 규칙 26, NET-PRESENT-003 |
| 경기 고정 single emitter | 동기화 문서 「10. 현재 구조에서의 전환 규칙」 |

규칙 원본은 `GameSystemRules/GameSystemRules_Units.md`와 `GameSystemRules/GameSystemRules_UnitCombatSynchronization.md`다. 피해·HP·사거리·타겟 선택·B3 Root writer 변경, 신규 에셋 제작, 허용 오차 상향은 범위 밖이다.

## 2. 실패 재현부터 고정

Host의 실제 late Emit/Tracer와 Client의 revision evidence 누락을 별도 재현한다. 정상/복구 합계만 낮추지 않고 exact token별 시간·입력 순서·실제 방출자를 확인한다. 다음 회귀를 구현 경계에서 고정한다.

- marker보다 Result가 먼저 도착하고 marker가 영원히 오지 않는 경우
- commit 뒤 타겟이 움직여 Impact 방향/revision이 바뀐 경우
- confirmed Result가 사망/StopCombat 전후로 도착하는 두 순서
- 취소된 future hit에 결과가 없는 경우와 잘못된 구회차 결과를 재사용하는 경우
- tracer 도착이 0.9초 늦어도 결과가 추가 지연되지 않는 경우
- multi-hit 및 AoE 결과 일부 유실, 중복, 순서 역전, ID reuse
- Supported 무효 scope 억제와 Unresolved LegacyFallback의 기존 VFX 보존

## 3. 토큰 계약과 전송

Schedule에 instance·sequence·HitIndex·예약 시각·commit 의도를 보존하고, Result에 실제 Impact revision·방향·피해자별 정규 결과 키를 보존한다. 현재 타겟이나 최신 NetworkVariable을 재조회해 과거 방향을 만들지 않는다. marker 1개를 AoE 피해자별 Result와 1:1이라고 가정하지 않는다.

같은 키의 중복은 멱등 처리하고 payload 충돌·용량 초과·만료는 실패 증거를 남긴다. 미래 허가 종료와 확정 결과 상태를 별도 관리해 사망 후 도착 결과도 권위 HP를 보존한다. 기존 age와 late-join baseline 밖 transient 표현은 되살리지 않는다.

## 4. 완결 결과와 단일 소비자

AoE는 서버가 해당 HitIndex 처리를 마친 뒤 완료 manifest/완결 묶음을 발행하도록 연결한다. 전체 개수·키 수신을 확인하기 전 완전한 묶음으로 간주하지 않는다. 타격 프레임 수·첫 결과·ordinal 최댓값으로 완료를 추정하지 않는다. 파도/Periodic은 접촉·틱별 시각을 유지한다.

실제 표현 adapter는 C2 확정 결과와 공통 표현 시각을 소비한다. Animation Event는 무기 VFX/SFX 표식, tracer callback은 시각 종료 역할만 갖는다. 결과가 늦으면 기존 catch-up 정책을 따르고 다음 marker나 다음 공격으로 넘기지 않는다. 표시용 HP·피격 반응과 서버 HP를 구분하며 서버 HP는 지연하지 않는다.

## 5. 전환 순서

1. 실패 재현 회귀와 토큰 계약 구현.
2. 서버/Client 토큰 전송과 종료 순서 검증.
3. 완료 manifest·표현 시간축·실제 adapter 연결 및 기존 호출처 감사.
4. 비교 모드에서 키·결과 수·예정 방출·레거시 실방출을 대조. 실제 writer는 하나로 유지.
5. 전환 준비 gate가 충족되면 다음 경기 시작 시 신규 single emitter 모드를 선택. 레거시 코드는 보존하되 중복 방출 비활성화.
6. 전용/전체 Unity self-validation PASS 후 Build And Run. 실패하면 원인 교정 후 재검증.
7. Android/Editor 역할 교대 실기, 대표 타입 구조 gate, 이후 25종 누적 검증.
8. 사용자 최종 PASS 뒤에만 레거시 최종 삭제.

## 6. 엄격한 PASS gate

- 동일 shared session·schema·pipeline·프로필의 완료 로그 양측 존재.
- C2 실패/거부 0, gameplay 중복 writer 0, B3 authority·stationary Walk·fatal repath 실패 0.
- 유효 결과 정규 키별 실제 emit 최대 1회, 누락/중복/다른 회차 결합 0. baseline/age에 따른 명시적 생략과 실제 누락을 분리.
- 정상 표현 시간 오차는 기존 기준 이내. 늦은 네트워크 수신과 로컬 추가 대기를 분리하고 Host 실제 지연을 허용치 상향으로 통과시키지 않음.
- 동일 Impact revision 방향 불일치 0, 미해결 방향 증거 0. 일시적 revision lag를 서버 조준 오류로 합산하지 않음.
- 사망·StopCombat 전체 flush 0, confirmed Result 소급 취소 0, future 취소 hit의 가짜 결과 생성 0.
- AoE 완료 manifest 검증 및 multi-hit 독립 방출 통과. manifest=false 상태를 전환 완료로 보고하지 않음.
- Unresolved 기존 에셋 표현 보존, Supported scope fail-closed 유지.
- ROOT 양측 및 요구되는 교차 검증 통과. 이번 9종만으로 25종 완료를 주장하지 않음.
- 모바일 오류는 UAS terminal/전투 예외/종료 Lobby 오류를 구분해 미해결 항목을 명시. Lobby 404를 C3 PASS의 근거로 사용하지 않음.

## 7. 현재 상태

2026-09-07 20:00 경기의 C2·B3·로컬 ROOT 및 비교 입력 수렴 증거를 확보했으나 실제 C3 표현은 FAIL이다. 본 문서는 후속 구현 계획이며 구현 완료·자동 검증 PASS·빌드 성공·실기 PASS 기록이 아니다.
