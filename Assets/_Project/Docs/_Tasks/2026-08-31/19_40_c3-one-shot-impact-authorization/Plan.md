# C3 1회성 타격 표현 허가 교정 Plan

공격 애니메이션을 멈추거나 매 공격마다 처음부터 재생하지 않는다. 대신 서버가 보내는 한 공격 회차를 화면 타격에 사용할 수 있는 제한된 허가권으로 취급한다. 한 번 공격하는 유닛은 한 번, 여러 번 타격하는 유닛은 설정된 횟수만 연출하고, 그 뒤 반복되는 애니메이션 표식은 다음 서버 회차가 오기 전까지 조용히 무시한다.

## 1. 기존 로직 보존

서버 권위 피해 writer, C1/C2 reservation·authorization·result, Attack 상태 유지와 타겟 추적 회전, 기존 VFX/SFX/Tracer/HitPresentationQueue는 제거하거나 교체하지 않는다. 이번 변경은 그 emitter에 들어가기 전의 승인 생명주기만 교정한다. C3는 계속 read-only `PresentationShadow`이며 실기 PASS 전 emitter 전환은 금지한다.

## 2. 구현 계획

### 2.1 1회성 허가 상태

`UnitAttackPresentationPolicy.cs`에 allocation 없는 순수 상태를 둔다.

- `Closed`: scope 없음 또는 provisional/Stop/사망/이동 전환
- `Armed`: 유효 instance·sequence와 `[firstHitIndex, impactCount)` 소비 가능
- `Consumed`: 마지막 HitIndex를 소비했으며 새 단조 증가 scope 전까지 재사용 불가
- 유효 범위를 벗어난 firstHitIndex, impactCount 0, scope 0은 열지 않는다.
- marker 소비는 scope를 반환하고 cursor를 한 번만 증가시킨다.

근거: `NET-PRESENT-001`, `NET-PRESENT-002`, `NET-ACTION-IDEMPOTENT`.

### 2.2 UnitView 실제 방출 gate

`UnitView.OnAttackHit`에서 BloomFairy 별도 힐 연출을 제외한 네트워크 공격은 유효 허가 소비를 먼저 시도한다. 실패하면 observer·VFX·SFX·Tracer·`OnLocalAttackHit`보다 앞에서 반환한다. 성공한 scope만 Marker 관측과 원거리 Tracer 콜백에 전달한다. 마지막 hit 소비 시 허가는 즉시 Consumed가 된다.

`StartCombatAnimation`의 새 유효 scope는 허가를 Armed로 교체하되 Animator가 이미 Attack이면 CrossFade하지 않는다. provisional, suppression, Walk/Held, StopCombat, 사망은 허가만 닫고 마지막 presentation revision 상한은 보존한다.

근거: `U-ATK-TIMELINE`, `NET-PRESENT-002`, `NET-PRESENT-003`.

### 2.3 RED 회귀

`RunUnitActionSelfValidation.cs`에서 실제 호출 모양을 순수 seam으로 검증한다.

1. scope 없는 반복 Marker는 모두 방출 불가다.
2. 단일 hit scope 뒤 Marker를 여러 번 호출해도 정확히 한 번만 성공한다.
3. 다음 sequence를 Armed하면 CrossFade 없이 정확히 한 번 다시 성공한다.
4. 다중 hit는 오름차순 HitIndex를 정확한 개수만 반환한다.
5. 중복·오래된 scope와 범위 밖 firstHitIndex는 최신 상태를 되돌리지 않는다.
6. 닫힌 뒤에도 발사 시 캡처한 원거리 scope 값은 변하지 않는다.
7. clear 뒤 이전 회차가 재사용되지 않는다.

이 테스트는 이전처럼 dispatch DTO만 검사하지 않고 `scope 1회 → Marker N회` 종료 수명까지 검증한다.

## 3. 위험과 방어

- **멀티 hit 조기 종료:** impactCount와 firstHitIndex를 함께 검증하고 마지막 유효 hit 뒤에만 닫는다.
- **원거리 착탄 유실:** Tracer 콜백은 발사 때 소비한 scope의 값 복사본을 사용한다.
- **싱글/힐 회귀:** 네트워크 C3 공격 허가만 적용하고 기존 싱글과 BloomFairy 별도 경로를 보존한다.
- **애니메이션 끊김:** 허가 종료는 emitter만 닫으며 Animator 상태·normalized time·회전 추적을 변경하지 않는다.
- **진단기 위장 PASS:** 억제된 Marker는 VFX를 방출하지 않는 정상 no-op이고, emitter가 scope 없이 실행된 경우만 Invalid 실패로 유지한다.

## 4. 완료 gate

1. 새 production-shaped 회귀를 포함한 전체 UAS/C2/C3 self-validation PASS.
2. Unity Runtime/Editor 컴파일 오류 0.
3. 문서 정합성 검사 7종 0건.
4. 위 항목이 모두 PASS일 때만 Android Build And Run.
5. 새 동일 경기에서 C2 결과 수렴을 보존하고 양쪽 C3 Invalid 0을 먼저 확인한다.
6. Invalid 제거 뒤 남은 unmatched·direction·timing만 별도 원인으로 판정한다.

## 5. 구현 결과

- RED: 마지막 HitIndex 소비 뒤에도 `Armed`가 남는 fixture가
  `A single-hit presentation scope must authorize exactly one marker and close immediately after consumption.`으로 실패함을 Unity에서 확인했다.
- GREEN: `AttackPresentationImpactLease`가 마지막 유효 HitIndex 직후 `Consumed`가 되며 내부 scope를 지운다.
- 실제 경계: 네트워크 일반 공격의 `OnAttackHit`은 lease 소비 실패 시 observer·VFX·SFX·Tracer·`OnLocalAttackHit`보다 앞에서 반환한다.
- 수명 정리: provisional/suppression, Walk/Held, StopCombat, 타겟 교체, 사망, 초기화에서 lease를 닫는다.
- 보존: 싱글플레이와 BloomFairy 별도 힐 연출, 서버 권위 피해 writer, Attack 루프와 타겟 추적 회전은 변경하지 않았다.
- 자동 검증: Unity 컴파일 오류 0, 전체 UAS/C2/C3 self-validation PASS, 문서 정합성 검사 7종 0건.
- Android Build And Run: `Succeeded`, 1,431초. `Build/Hexiege.aab` 141,976,144 bytes, 2026-08-31 20:30:33.
