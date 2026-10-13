# C3 서버 권위 공격 표현 단일화 — 조사

지금 공격은 서버가 피해 시점을 정하는 체계와, 애니메이션·탄환·레거시 큐가 화면의 피격 시점을 정하는 체계가 동시에 움직인다. 그래서 한쪽의 타이밍을 맞춰도 타겟 사망, 연속 공격, 원거리 탄환처럼 다른 경계에서 다시 어긋난다. 이번 작업은 개별 증상에 조건을 더하는 것이 아니라, 서버가 정한 공격 일정 하나에 애니메이션·탄환·피격 표현을 모두 맞추기 위해 현재 구조와 전환 경계를 확정하는 작업이다.

## 1. 조사 범위

- 최신 동일 버전 Android Host / Editor Client C3 v6 경기
- C1 공격 회차, C2 권위 결과, B3 이동과 C3 표현 사이의 책임 경계
- `UnitView.OnAttackHit`, `EffectManager.PlayTracer`, `HitPresentationQueue`의 실제 방출 순서
- C3 Shadow scheduler·observer와 네트워크 공격 결과 복제 경로
- 유닛 규칙과 멀티플레이 동기화 규칙 사이의 충돌

## 2. 최신 실기 증거

### 2.1 유효성

- 공통 세션: `48fddc9131924acca141170c1498a7ebabd2ae899177c7171c6c6c6b5fff8ccf`
- 양쪽 스키마: `c3-authoritative-result-presentation-shadow-v6`
- 모드: `PresentationShadow`, combat schema 1
- Android가 Host, Editor가 Client인 같은 경기다.

### 2.2 보존된 축

- C2 Host: 서버 결과 1,714건, 실패 0
- C2 Client: 수락 1,714건, 거부 0
- B3 Host: reject·invalid·writer 충돌·stationary Walk·fatal repath·drop 0
- B3 Client: 복제 identity·scope·revision 오류 0
- ROOT: Host 38 / Client 37 stable endpoint와 rotation evidence, 양쪽 로컬 PASS

따라서 이번 실패를 서버 피해 판정이나 이동 writer 문제로 되돌릴 근거가 없다.

### 2.3 C3 실패

- Host normal timing mismatch 13건, 최대 0.938416초
- Client normal timing mismatch 4건, 최대 0.708833초
- Host target-death/stop recovery mismatch 7건, 최대 0.934293초
- Client target-death recovery mismatch 3건, 최대 1.498579초
- Host unmatched Emit 1건, Client unmatched Marker 1건
- Client 방향 54건: same-revision 0 / revision-lag 35 / marker 시점 증거 부재 19
- Android의 비-UAS 크래시·ANR·미처리 예외는 0이며 화면 ERROR는 C3 terminal FAIL 1건이다.

이번 경기의 UnitType 커버리지는 9/25다. 구조 실패를 재현하고 원인을 확정하기에는 충분하지만 25종 전체 완료 증거는 아니다.

## 3. 현재 실행 흐름

### 3.1 서버 권위 흐름

1. 서버가 공격 회차와 다음 marker occurrence를 커밋한다.
2. 각 HitIndex에 절대 `ImpactServerTime`과 권위 방향을 부여한다.
3. 서버만 적중·피해·HP·결과를 확정한다.
4. Client는 정규 결과 키로 결과를 수신한다.

이 흐름은 C1·C2 실기에서 정상이다.

### 3.2 실제 화면 흐름

1. 로컬 `OnAttackHit` Animation Event가 VFX·SFX를 시작한다.
2. 원거리 공격은 `PlayTracer`를 실행한다.
3. 트레이서 도착 콜백이 로컬 피격 신호를 보낸다.
4. `HitPresentationQueue`가 공격자별 FIFO에서 보류 결과를 꺼낸다.
5. marker가 없으면 timeout, 타겟 사망, 공격자 사망 또는 StopCombat에서 잔여 표현을 flush한다.

이 흐름은 서버의 절대 Impact 시각이 아니라 로컬 애니메이션과 트레이서 이동 시간으로 두 번째 타격 시계를 만든다.

### 3.3 C3 Shadow 흐름

C3 v6는 정규 키·scope·revision으로 서버 결과와 Marker·Tracer·Enqueue·Emit을 비교한다. 그러나 read-only이므로 실제 방출 시점을 바꾸지 않는다. 검증기는 이중 시계를 정확히 발견했지만, 실제 화면은 계속 레거시 흐름을 따른다.

## 4. 규칙 충돌

### 최신 안정 ID 계약

- `U-ATK-TIMELINE`: 서버 시간축이 판정과 결과의 권위이며 로컬 Animation Event는 회차나 피해를 발생시키지 않는다.
- `NET-PRESENT-001`: Animation Event는 로컬 표현과 에셋 표식에만 사용한다.
- `NET-PRESENT-002`: 서버 예약과 Client marker cursor는 같은 절대 marker occurrence를 가리켜야 한다.
- `NET-PRESENT-003`: 피격 표현은 공격자 FIFO가 아니라 정규 결과 키에 연결한다. 닫힌 구회차는 재사용하지 않고 폐기 또는 Unmatched로 남긴다.
- `NET-PRESENT-004`: timeout은 정상 동기화 수단이 아니라 최후 복구 수단이다.

### 남아 있는 구 규칙

- `GameSystemRules_Units.md` 규칙 19는 피격 표현을 공격자 FIFO에 보류하고 timeout·타겟 사망·공격자 사망·StopCombat에서 즉시 방출하도록 요구한다.
- 규칙 26도 공격자 로컬 타격 신호가 FIFO를 방출하는 것을 전제로 한다.
- 규칙 21·22의 일부 설명도 FIFO가 정상 방출 경로라는 전제를 유지한다.

두 집합은 동시에 권위 원본일 수 없다. 구현 전에 구 규칙을 최신 안정 ID 계약에 맞게 폐기·대체 관계로 정리해야 한다.

## 5. 근본 원인

근본 원인은 특정 threshold나 한 유닛의 에셋이 아니다. 서버 권위 결과와 화면 표현이 각각 타격 시각을 결정하고, 새 C3가 구 emitter를 대체하지 않은 채 관찰만 해 온 것이 원인이다.

이 구조에서는 다음 봉합이 반복된다.

- scope가 없으면 scope 전달을 추가한다.
- 반복 marker가 나오면 lease를 추가한다.
- timeout이 늦으면 recovery로 분류한다.
- target death가 늦으면 별도 허용치를 고민한다.
- Client 방향이 늦으면 marker snapshot을 추가한다.

각 조치는 관측 정확도를 높였지만 실제 방출자의 이중 소유권을 없애지 못했다.

## 6. 채택할 구조

서버가 커밋한 공격 일정과 C2 결과를 입력으로 받는 단일 `Authoritative Presentation Coordinator`가 C3 표현 수명을 소유한다.

- 일정: attacker instance, sequence, commit revision, HitIndex, 의도 target·commit 방향, 예약 Impact 시각. 미래 적중과 피해자 수를 의미하지 않는다.
- 결과: 실제 Impact의 정규 키, result revision·최종 권위 방향, outcome, 피해량, HP, impact 위치. commit revision과 동일하다고 가정하지 않는다.
- Marker: 총구·무기 VFX와 SFX를 재생하는 표현 표식
- Tracer: 정해진 Impact 시각에 도착하도록 속도 또는 진행률을 보정하는 순수 시각 효과
- Impact 표현: 같은 정규 키의 권위 결과가 허용하는 시점에 한 번만 방출
- 종료: 타겟 전환·사망·중단은 미래 marker 허가와 미확정 예상 표현을 닫는다. 이미 확정된 결과는 종료 알림 뒤 도착해도 보존하며 기존 catch-up age·late-join baseline·View 수명 정책으로 표시한다.

Animation Event, 트레이서 콜백, timeout은 더 이상 실제 피격 표현의 권위 방출자가 아니다.

### 6.1 2026-09-07 구현 착수 시 계약 정정

9월 2일 계획의 “닫힌 구회차 폐기”는 이미 확정됐지만 아직 표시되지 않은 결과까지 지울 수 있어 위와 같이 범위를 좁혔다. immutable Schedule의 방향은 commit 의도이고 실제 Impact 방향은 별도의 서버 결과에서 확정한다. AoE 피해자 집합은 Impact 처리 완료 manifest가 있어야 알 수 있으므로 commit에 result ordinal 전체 범위를 미리 넣지 않는다. Marker는 선택적 관측이며 누락이 결과 표현을 막지 않는다.

표현 시간축은 기존 `NET-TIME-002` 정책을 따르며 네트워크 도착 지연 0이나 모든 상황의 완전 동시를 약속하지 않는다. 새 임의 지연값을 도입하지 않는다. 현재 첫 구현 범위는 순수 Coordinator와 실제 입력의 비교 연결이다. 이 단계의 빌드는 레거시가 화면을 계속 소유하므로 원거리 시각 지연이 해결된 빌드라고 보고해서는 안 된다. 일정 복제·결과 완료 manifest·단일 writer 소비자 전환은 별도 완료 증거가 필요하다.

## 7. 보존 범위

- C1 공격 회차와 서버 정렬·커밋
- C2 적중·피해·HP·결과 writer
- B3 이동·회전 writer와 NetworkTransform
- 기존 애니메이션, VFX, SFX, 트레이서 에셋
- 특수 공격별 서버 의미와 정규 결과 키
- 서버 권위와 Client read-only 표현 원칙

## 8. 예상 영향 파일

### Application

- `Application/Combat/Sequencing/UnitActionContracts.cs`
- `Application/Combat/Sequencing/UnitAttackPresentationPolicy.cs`
- `Application/Combat/Sequencing/UnitAttackResultPresentationShadow.cs`
- 신규 C3 Coordinator 및 순수 상태 전이 파일

### Infrastructure / Network

- `Infrastructure/Network/NetworkCombatController.cs`
- `Infrastructure/Network/NetworkUnitActionShadowState.cs`
- `Infrastructure/Network/NetworkUnit.cs`
- `Infrastructure/Network/UnitAttackShadowObserver.cs`
- `Infrastructure/Network/NetworkHealthSync.cs`

### Presentation

- `Presentation/Unit/UnitView.cs`
- `Presentation/Effects/HitPresentationQueue.cs`
- `Presentation/Effects/EffectManager.cs`
- `Presentation/Effects/TracerProjectile.cs`

### Editor validation

- `Editor/Combat/RunUnitActionSelfValidation.cs`

정확한 파일 수와 신규 타입 배치는 구현 직전 호출 관계를 다시 좁혀 확정한다. 레이어 의존 방향은 Application 계약을 Infrastructure와 Presentation이 소비하는 기존 구조를 유지한다.

## 9. 결론

C3 v6의 실패는 진단기 오탐으로 넘길 수 없다. 진단기는 실제로 존재하는 두 타격 시계를 드러냈다. 다음 단계는 개별 mismatch를 0으로 만드는 조건 추가가 아니라, 규칙 충돌을 먼저 정리하고 새 Coordinator가 유일한 정상 표현 방출자가 되도록 단계적으로 전환하는 것이다.
