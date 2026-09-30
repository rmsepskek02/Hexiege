# C3 연속 공격 회차 스코프 교정 Plan

이번 교정은 공격 애니메이션을 매번 다시 시작하는 방식이 아니다. 전투 중인 유닛의 Attack 모션은 그대로 이어가고, 서버가 실제 공격을 새로 확정할 때마다 화면 타격이 사용할 새 회차 번호만 갱신한다. 이렇게 해야 애니메이션이 튀지 않으면서도 모든 Marker·Tracer가 정확한 서버 공격과 연결된다.

## 1. 기존 로직 제거 여부

기존 서버 권위 피해, C1/C2 회차·결과, Legacy VFX/SFX emitter는 제거하거나 비활성화하지 않는다. `_combatAnimationSent`도 전투 표현의 최초 시작 상태를 나타내는 용도로 보존한다. 이번 변경은 그 가드가 후속 scope 발행까지 차단하지 않도록 책임 경계만 분리한다.

## 2. 구현 항목

### 2.1 매 커밋 scope 발행 분리

수정: `NetworkCombatController.cs`

- `ExecuteAttack`이 반환한 유효한 `AttackPresentationScope`를 매 공격 커밋에서 사용한다.
- `_combatAnimationSent.Add(id)`는 최초 Attack 표현 시작 여부만 계산한다.
- `StartCombatClientRpc`는 정상 커밋마다 호출해 새 presentation revision과 instance·sequence·first hit를 전송한다.
- 최초와 후속 커밋 모두 `restartAttackCycle=false`를 유지한다.
- 유효하지 않은 scope는 gameplay를 되돌리지 않고 C3가 fail-closed 할 수 있도록 기존 read-only 경계를 유지한다.

근거: `NET-AUTH-001`, `NET-ACTION-SEQ`, `NET-PRESENT-002`.

### 2.2 UnitView 회차 커서 갱신과 애니메이션 연속성

수정: `UnitView.cs` 및 필요한 순수 정책 seam.

- 단조 증가 revision의 후속 커밋 scope를 수락하면 marker instance·sequence와 next hit를 새 회차로 교체한다.
- 이미 Attack 상태인 Animator는 CrossFade하지 않고 normalized time을 유지한다.
- 중복·오래된 revision/scope는 기존처럼 거부한다.
- 타겟 추적과 공격 방향 갱신은 유지하되 새 scope 때문에 이동/Idle을 거치지 않는다.

근거: `NET-FACING-002`, `NET-PRESENT-001`, `NET-PRESENT-002`.

### 2.3 RED 회귀 및 self-validation

수정: `RunUnitActionSelfValidation.cs`.

다음 실제 호출 형태를 검증한다.

1. 같은 유닛이 전투 이탈 없이 세 회차를 연속 커밋한다.
2. 세 회차 모두 서로 다른 단조 증가 sequence와 유효 scope를 발행한다.
3. 각 새 회차는 marker hit cursor를 0에서 시작한다.
4. 최초 Attack 시작 후 후속 두 회차는 Animator restart/CrossFade를 요구하지 않는다.
5. 중복·오래된 scope는 거부되고 최신 회차를 되돌리지 않는다.
6. scope 없는 marker는 계속 Invalid로 fail-closed 한다.

근거: `NET-ACTION-IDEMPOTENT`, `NET-PRESENT-002`, `NET-PRESENT-003`.

### 2.4 진단 표본 보강

수정 가능: `UnitAttackShadowObserver.cs`.

공용 16개 표본이 Invalid 하나로 모두 소진되어 Unmatched·Direction·Timing 원인을 숨기면 실패 종류별 bounded quota로 나눈다. 정상 로그 총량은 늘리지 않고 terminal 계수와 Android 한 줄 UTF-8 상한을 유지한다.

근거: `NET-PRESENT-004`, `LogRules.md`의 bounded 진단 원칙.

## 3. 위험과 방어

- **RPC 빈도 증가:** 전투 진입당 1회에서 공격 커밋당 1회가 된다. payload는 작은 원자 scope이며 공격 쿨다운 빈도를 따른다. 같은 데이터를 별도 채널로 나누지 않는다.
- **애니메이션 재시작 회귀:** 후속 RPC에서도 `restartAttackCycle=false`를 강제하고 이미 Attack 상태이면 CrossFade하지 않는 회귀를 둔다.
- **늦은 RPC 역행:** presentation revision 단조성 검사가 이전 scope로 cursor를 되돌리지 못하게 한다.
- **marker와 다음 회차 경계:** 새 커밋 때 cursor를 hit 0으로 교체하며, 지나간 marker를 새 결과에 소급 결합하지 않는다.
- **C2 gameplay 오염:** 새 코드는 scope 전달과 read-only 관측만 바꾸고 피해 적용 순서·결과 RPC는 변경하지 않는다.

## 4. 완료 gate

1. Unity Runtime/Editor 컴파일 오류 0.
2. 전체 `[UAS-DIAG]`, C2, C3 self-validation PASS.
3. 연속 공격 fixture에서 매 커밋 scope 발행 및 후속 CrossFade 0.
4. 문서 정합성 검사 0건.
5. 위 항목이 모두 PASS일 때만 Android Build And Run 실행.
6. 새 실기에서는 양쪽 v4 이상 동일 schema/session을 먼저 확인하고 C2와 C3 terminal을 독립 판정한다.

실기 PASS 전까지 C3는 계속 read-only `PresentationShadow`이며 실제 emitter 전환은 금지한다.

## 5. 구현 및 자동 검증 결과

### 5.1 적용 완료

- 공격 애니메이션의 최초 시작 상태와 서버 공격 회차 scope 발행 책임을 분리했다.
- 서버는 정상 공격 커밋마다 새 `AttackPresentationScope`를 발행한다.
- 이미 Attack 표현이 활성 상태인 후속 커밋은 Animator를 restart/CrossFade하지 않는다.
- 연속 세 회차 fixture가 모든 커밋의 scope 발행, sequence·revision 단조성, hit cursor 0 초기화, 후속 CrossFade 0을 검증한다.

### 5.2 자동 gate

- Unity Runtime/Editor 컴파일 오류: 0
- `[UAS-DIAG]`: PASS
- `[C2-AUTHORITY]`: PASS
- `[C3-PRESENTATION-SHADOW]`: PASS
- 문서 정합성 검사: 7종 모두 문제 없음
- Android Build And Run: `Succeeded`, 1,775초
- 산출물: `Build/Hexiege.aab`, 141,976,668 bytes, 2026-08-31 17:48:34
- 기기 설치·실행: Android Logcat에서 새 프로세스 PID 2354 로그 유입 확인

### 5.3 남은 실기 gate

자동 검증은 구현과 빌드 gate를 통과했다. 그러나 C3의 최종 완료 판정은 새 빌드로 동일 경기 Host/Client 로그를 수집한 뒤 내린다. 다음 분석에서는 먼저 양쪽 v4·`PresentationShadow`·combat schema 1·동일 session key를 확인하고, C2 PASS를 보존한 상태에서 `invalid`, `unmatched`, `direction`, `timing`을 다시 분리 판정한다. C3 실기 PASS 전에는 emitter를 전환하지 않는다.
