# C3 서버 권위 공격 표현 단일화 — 교정 계획

이번 작업은 늦은 탄환이나 타겟 사망 같은 증상을 하나씩 예외 처리하지 않는다. 서버가 정한 공격 일정 하나를 기준으로 애니메이션, 탄환, 피해 결과, 피격 효과가 움직이도록 C3의 실제 표현 경로를 교체한다. C1·C2·B3와 서버 권위는 유지하고, 화면의 타격 시각을 따로 결정하던 레거시 FIFO·트레이서 도착·사망 flush를 검증 가능한 순서로 퇴역시킨다.

> **기존 로직 처리 원칙:** 사용자 실기 멀티플레이 PASS 전에는 레거시 방출 코드를 삭제하지 않는다. 먼저 새 Coordinator를 read-only/differential 모드로 붙이고, 다음으로 단일 emitter 전환에서 레거시 경로를 비활성화한다. 최종 삭제는 전환 후 사용자 실기 PASS와 문서 업데이트 단계에서만 진행한다.

> **2026-09-07 착수 경계:** 이번 최초 구현/빌드는 순수 Coordinator와 실제 입력의 비교 단계를 우선 완성한다. 비교 단계는 레거시 화면을 유지하므로 시각적 타격 지연 교정 완료가 아니다. 서버 일정 복제·AoE 완료 manifest·공통 표현 시간축·실제 단일 writer 배선은 각각 구현/검증 여부를 별도 기록한다. 아래 전체 계획을 최초 빌드의 완료 목록으로 해석하지 않는다.

> **2026-09-07 첫 비교 실기 결과:** 동일 Android Host / Editor Client 경기에서 C2 결과 2,591건과 B3·ROOT는 보존됐지만 C3 레거시 비교는 Host timing mismatch 8건·unmatched emit 2건·방향 증거 오류 1건, Client revision-lag 45건·방향 증거 부재 26건으로 FAIL했다. 별도로 생산 142개 중 공격한 111개만 유효 retire됐는데 무공격 31개의 `Retire(None)`이 Coordinator 실패로 잘못 집계됐다. Assault의 `TargetAcquirePriority`가 provisional Attack 전에 일반 Held Walk를 삽입한 실기 회귀도 확인됐다. 상세 교정은 `2026-09-07/05_37_assault-action-handoff-c3-lifecycle-correction` Task를 따른다.

## 1. 목표

1. 공격마다 서버가 확정한 단일 일정만 타격 시각의 권위로 사용한다.
2. 같은 정규 결과 키의 표현을 Host와 Client에서 각각 최대 한 번 방출한다.
3. Animation Event와 트레이서는 결과를 결정하지 않는 순수 표현 소비자가 된다.
4. 사망·중단 시 미래 미확정 표현을 닫고 이미 서버가 확정한 결과는 보존한다. 큐 전체 flush는 하지 않는다.
5. Client는 일정의 commit 의도와 실제 결과의 Impact revision·최종 권위 방향을 구분한다.
6. C1·C2·B3·ROOT의 현재 PASS를 보존한다.
7. 새 에셋 제작 없이 기존 애니메이션·VFX·SFX·트레이서를 재사용한다.

## 2. 비목표

- 서버 적중·피해·방어·HP 계산 재설계
- B3 이동 경로와 회전 writer 변경
- 공격 사거리·타겟 선택 규칙 변경
- 애니메이션 클립 또는 신규 VFX 제작
- 허용 오차 상향으로 현재 C3 실패 숨기기
- 25종 공격 프로필의 밸런스 수치 변경

## 3. 근거 규칙

| 계획 항목 | 근거 |
|---|---|
| 서버 일정 단일 권위 | `U-ATK-TIMELINE`, `NET-PRESENT-002` |
| Animation Event를 표현 표식으로 제한 | `NET-PRESENT-001` |
| Hitscan 결과와 표현의 동일 회차·HitIndex | `NET-DELIVERY-HITSCAN` |
| FIFO 대신 정규 결과 키 결합 | `NET-ACTION-IDEMPOTENT`, `NET-PRESENT-003` |
| timeout을 정상 경로로 사용하지 않음 | `NET-PRESENT-004` |
| 타겟 사망 후 다른 대상·회차로 이전 금지 | `NET-CANCEL-004` |
| 동일 revision만 실제 방향 실패 판정 | `NET-PRESENT-003` 방향 검증 단락 |
| Client가 Root·Aim을 쓰지 않음 | `NET-FACING-002`, `NET-PRESENT-003` |

## 4. Phase 0 — 규칙 충돌 제거

### 변경

- `GameSystemRules_Units.md` 규칙 19의 공격자 FIFO·정상 timeout·사망/StopCombat flush를 폐기 대상으로 명시하고 정규 결과 키 기반 Coordinator 계약으로 대체한다.
- 규칙 26의 AoE 동시 방출 의미는 보존하되 같은 sequence·HitIndex의 서버 완료 manifest로 모든 결과가 도착한 것을 확인한 뒤 동시 방출하도록 바꾼다.
- 규칙 21·22에서 FIFO 정상 동작을 전제로 한 설명을 최신 안정 ID 계약에 맞춘다.
- `GameSystemRules_UnitCombatSynchronization.md`에 Schedule·Result·Marker·Impact Presentation의 소유권과 타겟 사망 시 폐기 의미를 명확히 한다.

### 완료 조건

- 같은 상황에 FIFO flush와 정규 키 폐기를 동시에 요구하는 문장이 없다.
- 구현 계획의 각 수명 전이가 하나의 규칙으로 판정 가능하다.

## 5. Phase 1 — 공격 표현 일정 계약 확정

### 변경

Application 계층에 불변 공격 표현 일정 계약을 둔다. 최소 정보는 다음과 같다.

- attacker instance와 unit ID
- attack sequence, commit revision, HitIndex
- victim kind와 victim ID
- delivery kind
- 커밋 당시 의도 방향·목표 위치(실제 Impact 방향·착탄 위치와 구분)
- 절대 marker/Impact server time
- 예정 hit 식별 범위(미래 피해자 수나 ResultOrdinal 전체 집합은 포함하지 않음)

Schedule은 커밋 기록으로 불변이다. 실제 Impact에서 C2 결과가 정규 결과 키·result revision·최종 권위 방향·outcome·HP를 확정한다. commit과 impact revision을 동일하다고 요구하지 않으며 정확한 공격 instance·sequence·HitIndex에 결합한다. AoE 결과 개수/키 집합은 서버 타격 완료 시 명시적 묶음/manifest로 발행한다. 일정은 hit 성공을 보장하지 않는다.

### 완료 조건

- 시간 근접, 공격자 ID 단독, 현재 타겟 재조회 없이 exact key로만 결합한다.
- 잘못된 instance·sequence·revision·HitIndex는 fail-closed 한다.
- allocation과 buffer 상한이 명시되어 대규모 전투에서도 무제한 누적되지 않는다.

## 6. Phase 2 — 순수 Authoritative Presentation Coordinator 구현

### 상태

- `AwaitingSchedule`
- `Scheduled`
- 선택적 `MarkerObserved` 관측 플래그(결과 방출의 필수 선행 상태 아님)
- `ResultObserved`
- `ReadyToPresent`
- `Presented`
- `Cancelled/Retired` 미래 허가 상태와 별도의 확정 결과 보존 상태

네트워크 reorder를 고려해 Schedule·Marker·Result가 어떤 순서로 도착해도 같은 최종 상태로 수렴한다. 정상 표현은 `Presented`로 한 번만 전환한다.

### 핵심 정책

- Hitscan의 피격 표현은 권위 Impact 시각과 결과에 맞춘다.
- 결과가 늦게 도착하면 즉시 catch-up하되 다음 marker나 다음 회차에 붙이지 않는다.
- 결과보다 marker가 늦으면 이미 지난 권위 타격을 트레이서 도착까지 다시 보류하지 않는다.
- 타겟 사망·전환·StopCombat은 미확정 미래 허가를 retire한다. 이미 확정된 결과는 종료 알림 전후 도착 순서와 무관하게 보존한다. 표시 여부는 기존 0.50초 transient age·late-join baseline·View 수명을 따른다.
- AoE는 동일 sequence·HitIndex의 완료 manifest를 충족한 결과 묶음을 함께 방출한다. manifest가 없으면 완전 묶음을 추정하지 않으며 단일 writer 전환 gate 미충족으로 남긴다.
- Marker가 영원히 도착하지 않아도 유효 결과는 결과 정책에 따라 처리한다. 네트워크 지연은 수신 지연과 로컬 예약 지연으로 나눠 측정하며 기존 `NET-TIME-002` 외 임의 delay를 추가하지 않는다.
- timeout은 데이터 유실을 terminal failure로 닫는 최후 안전망이며 정상 표현을 생성하지 않는다.

### 완료 조건

- 순수 RED→GREEN 회귀에서 reorder, duplicate, multi-hit, AoE, target death, attacker stop, late marker, late result를 모두 검증한다.
- 같은 키의 presentation emit은 정확히 1회다.

## 7. Phase 3 — 서버 일정과 Client 복제 원자성

### 변경

- `NetworkCombatController`가 C1 커밋에서 선택한 실제 다음 marker occurrence와 절대 Impact 시각을 Schedule DTO로 발행한다.
- C2 `AttackImpactResult`는 현재 단일 서버 writer를 유지한다.
- Client는 commit 의도와 Impact 결과의 exact revision·권위 방향을 각각 보존한다. marker 순간 현재 NetworkVariable이나 현재 타겟으로 과거 Impact 방향을 재계산하지 않는다.
- spawn/despawn/reuse와 sequence retire 경계에서 이전 일정이 새 인스턴스로 넘어가지 않도록 한다.

### 완료 조건

- Schedule과 C2 결과는 같은 instance·sequence·HitIndex로 결합하며 각각 commit/impact revision을 보존한다.
- 지연된 방향 증거가 도착하면 같은 Impact revision으로 검증한다. marker 부재·정상 전송 지연을 실제 조준 실패로 위조하지 않으며 unresolved evidence를 PASS로 간주하지 않는다.
- Client가 Simulation Root, NetworkTransform, 서버 Aim, 피해를 쓰는 경로는 0이다.

## 8. Phase 4 — Presentation 소비자 전환

### UnitView / Animation Event

- `OnAttackHit`은 무기 발사점, 공격 VFX·SFX, marker 관측만 제공한다.
- 유효 Schedule이 없는 provisional·소모 후 반복 marker는 표현 결과를 방출하지 않는다.
- Attack 클립 연속 재생과 타겟 교체 시 restart 금지 계약은 보존한다.

### Tracer

- 발사 순간의 시작점·목표점과 권위 Impact 시각으로 남은 비행 시간을 계산한다.
- 이미 Impact 시각이 지났으면 피격 표현을 다시 지연하지 않고 tracer를 축약·catch-up하거나 생략한다.
- `onArrive`는 순수 시각 종료 알림이며 HP 텍스트·피격 VFX·결과 dequeue 권한을 갖지 않는다.

### Hit presentation

- Coordinator의 exact result bundle만 HP 텍스트·피격 VFX·타격 반응을 방출한다.
- 타겟 사망과 StopCombat은 미래 허가 수명 종료 입력이며 이미 확정된 결과의 취소 명령이 아니다. 소멸 View에 반응을 쓰지 않고 결과 상태를 보존한다.

### 완료 조건

- 실제 피격 표현 방출 호출자는 Coordinator adapter 하나다.
- 기존 에셋과 공격 애니메이션은 변경 없이 재사용된다.

## 9. Phase 5 — Differential 전환과 레거시 비활성화

### 1단계: 비교 모드

- 레거시 emitter는 실제 화면을 유지한다.
- 새 Coordinator는 동일 입력으로 예정 방출 키와 시각만 기록한다.
- exact key, 방출 횟수, 권위 Impact 대비 시간, 종료 사유를 양쪽에서 비교한다.

### 2단계: 단일 emitter 모드

- 새 Coordinator adapter만 실제 피격 표현을 방출한다.
- 레거시 FIFO·timeout·death/stop flush는 비활성화하되 코드는 보존한다.
- gameplay writer와 서버 결과 경로는 변경하지 않는다.

### 3단계: 제거

- 사용자 Host/Client 실기 PASS와 25종 누적 gate 뒤 레거시 코드를 삭제한다.
- 삭제 전 공격자 FIFO를 참조하는 특수 공격과 문서가 없는지 전수 검색한다.

## 10. Phase 6 — 진단기와 회귀 gate

### 자동 검증

- 권위 일정과 결과 exact-key 결합
- Schedule/Marker/Result 6가지 도착 순열 동일 수렴
- normal impact, late-result catch-up, late-marker 무재방출
- target death/attacker death/StopCombat 미래 허가 폐기 + 확정 결과 보존(결과/종료 알림 양쪽 순서)
- 단일 hit, multi-hit, 명시적 완료 manifest 기반 AoE bundle 및 부분 유실 fail-closed
- Marker 영구 부재에도 확정 결과 수렴, commit 방향과 Impact 방향 변경 허용
- 연속 Attack과 타겟 교체에서 animator restart 0
- tracer 도착 콜백의 gameplay/presentation emit 권한 0
- same-revision 방향 일치와 revision-lag 별도 분류
- duplicate/reuse/retire/capacity fail-closed
- C1·C2·B3 전체 기존 self-validation 회귀

### 실기 검증

- 동일 schema·pipeline·combat revision·shared session인 Android/Editor 한 경기만 채점한다.
- Host/Client 역할을 바꾼 두 번째 경기로 복제 방향을 교차 확인한다.
- 원거리·근접·다중 타격·AoE를 포함한 대표 4종으로 먼저 구조 gate를 확인한다.
- 구조 gate PASS 뒤 누적 25종 UnitType 커버리지를 완료한다.
- 육안으로 공격 시작, 타겟 교체, 탄환 도착, HP 변화, 피격 반응을 확인한다.

### 최종 PASS 기준

- C2 결과 실패·거부 0
- C3 invalid·duplicate·ambiguous·unmatched·timeout 정상 방출 0
- 동일 키 presentation emit 정확히 1회
- 공통 표현 시간축의 Impact 대비 정상 로컬 예약 오차가 규칙 허용 범위 이내. 네트워크 지연 초과 catch-up은 별도 집계하며 지연 0을 약속하지 않음
- same-revision 방향 mismatch 0
- 최종 동일 Impact revision 검증의 미해결 증거 0(일시적 수신 지연과 실제 방향 실패 분리)
- 타겟 사망·전환 뒤 구회차 flush 0
- B3 writer 충돌·stationary Walk·fatal repath 0
- 양측 ROOT와 CrossAudit PASS
- Android 비-UAS Error·FATAL·ANR·미처리 예외 0

## 11. 위험 요소와 대응

| 위험 | 대응 |
|---|---|
| 결과가 네트워크 지연으로 Impact 뒤 도착 | 서버 시각 anchor를 사용해 즉시 catch-up하고 중복 방출 금지 |
| tracer를 축약하면 시각적으로 빨라 보임 | 남은 시간 기반 속도 보정과 최소 표시 정책을 데이터로 분리하되 피해를 늦추지 않음 |
| 특수 공격이 FIFO 전제를 사용 | 전환 전 전체 호출처 감사, delivery/profile별 adapter로 exact-key bundle 유지 |
| AoE 여러 피해가 순차 표시됨 | 서버 완료 manifest로 확인한 동일 HitIndex bundle을 원자 방출; 누락을 완전 묶음으로 추정 금지 |
| 타겟 사망 직전 결과 표현이 사라짐 | 이미 확정된 결과는 미표시 상태여도 보존; 미래 미확정 허가만 종료 |
| Client가 최신 방향을 늦게 받음 | commit 의도와 실제 Impact revision·방향을 별도로 보존하고 결과 기준 검증 |
| 대규모 전투 메모리 증가 | 공격자 instance·sequence별 bounded state와 retire 정리 |
| Shadow 장기 유지로 이중 구조가 다시 고착 | 비교 모드 종료 gate와 단일 emitter 전환 조건을 이번 Plan의 필수 단계로 고정 |

## 12. 구현 순서와 중단 조건

1. 규칙 충돌 정리
2. 순수 계약과 Coordinator RED 작성
3. Coordinator GREEN 및 전체 자동 회귀
4. 서버 Schedule 복제와 Client exact 입력 연결
5. Presentation 소비자 adapter 연결
6. differential 실기
7. 단일 emitter 전환
8. Host/Client 교차 실기와 25종 누적 검증
9. 사용자 PASS 후 레거시 삭제와 최종 문서 갱신

각 단계에서 C2 결과, B3 writer 또는 서버 권위가 회귀하면 다음 단계로 진행하지 않고 해당 단계만 되돌려 원인을 교정한다. C3 수치만 낮추기 위해 C1·C2·B3를 재설계하지 않는다.
