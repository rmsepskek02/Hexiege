# C3 1회성 타격 표현 허가 교정 Research

이번 작업은 공격 애니메이션이 계속 반복되는 동안 서버가 승인하지 않은 추가 타격 이펙트가 화면에 나오는 문제를 고친다. 공격 모션과 타겟 방향 추적은 자연스럽게 유지하되, 서버가 확정한 공격 회차의 정해진 타격 횟수만 VFX·SFX·트레이서·피격 표현을 만들 수 있게 한다. 서버 피해·HP·사망 권위와 기존 C1/C2 결과 writer는 변경하지 않는다.

## 1. 최신 실기 근거

- 공통 세션: `d8ed6d4a9ab3049d2b3bc9ab5d641d86909e0e33a73b37647388b2e81fc15890`
- 역할: Editor Host / Android Client
- 양쪽 계약: C3 v4, `PresentationShadow`, combat schema 1
- C2: 서버 결과 2,566 / 실패 0, Client 수락 2,566 / 거부 0
- C3 Host: invalid 3,800, unmatched 48, direction 3, timing 26
- C3 Client: invalid 3,777, unmatched 88, direction 37, timing 59
- 이동: Host invalid·rejected·writer conflict 0, Client 복제 1,949 / invalid 0
- ROOT: 양쪽 로컬 PASS, evidence drop 0
- 최신 경기 비-UAS FATAL·ANR·예외: 0

## 2. 재현된 production 호출 형태

`StartCombatAnimation`이 유효 scope를 받으면 `_attackPresentationImpactEnabled=true`와 instance·sequence·next hit cursor를 저장한다. 첫 Marker는 유효 scope를 소비해 정상 연출을 만든다. 그러나 마지막 HitIndex 뒤 cursor가 프로필의 hit 수를 넘으면 scope 획득만 실패하고 impactEnabled는 계속 true다. 이후 Attack 루프 Marker는 기본 scope로 관측되면서도 기존 emitter를 실행한다.

Unit 1의 로그에서는 sequence 1 정상 Marker 뒤 sequence 2가 오기 전에 scope 0 Marker가 반복됐다. 이는 진단기 임계치가 아니라 실제 `OnAttackHit` 방출 gate 실패다.

## 3. 규칙 근거

- `U-ATK-TIMELINE`: 로컬 Animation Event는 서버 회차나 피해를 만들지 않는다.
- `NET-PRESENT-001`: provisional·승인 전 Marker는 표현을 방출하지 않는다.
- `NET-PRESENT-002`: 매 커밋 scope를 발행하고, 각 scope는 프로필 HitIndex만 한 번 소비한다.
- `NET-ACTION-IDEMPOTENT`: 같은 정규 결과 키와 표현 단계는 최대 한 번 적용한다.
- `NET-PRESENT-003`: 피격 표현은 FIFO가 아니라 정확한 회차·HitIndex에 결속한다.

## 4. 직접 영향 범위

- `UnitAttackPresentationPolicy.cs`: 1회성 타격 표현 허가의 순수 상태/소비 seam
- `UnitView.cs`: 유효 scope 소비를 실제 emitter 앞의 최종 gate로 적용
- `RunUnitActionSelfValidation.cs`: production과 같은 반복 Marker 호출 회귀
- 규칙·Task·QA/mistake 문서

변경하지 않는 범위는 서버 타겟·사거리·정렬·피해 판정, C2 결과 전송, B3 이동, Attack 클립과 Animation Event 에셋, 기존 VFX/SFX 구현체다.

## 5. 조사 결론

별도 bool과 cursor를 부분적으로 고치는 대신 공격 표현 허가를 하나의 상태로 묶어야 한다. 새 유효 scope는 허가를 열고, 각 Marker는 정확히 하나의 HitIndex를 소비하며, 마지막 소비가 허가를 닫는다. scope 소비에 실패한 Marker는 observer 호출을 포함한 모든 공격 emitter보다 먼저 반환한다. 원거리 Tracer는 발사 때 이미 소비한 불변 scope를 콜백에 캡처하므로 허가 종료 뒤에도 같은 회차로 정상 착탄할 수 있다.
