# C3 연속 공격 회차 스코프 교정 Research

이번 작업은 유닛이 전투 상태를 유지하며 여러 번 공격할 때, 공격 애니메이션은 자연스럽게 계속 재생하면서도 화면의 각 타격이 서버가 확정한 정확한 공격 회차에 빠짐없이 연결되도록 고치는 작업이다. 서버 피해·HP·사망 권위와 기존 VFX/SFX emitter는 바꾸지 않고, 첫 공격 이후 사라지는 표현용 회차 정보만 교정한다.

## 1. 최신 실기 판정

- 공통 세션: `8baeb13051d8550781197666a449cfbd8e90d7e882154f67465120ed810ecb15`
- 역할: Android Host / Editor Client
- 양쪽 계약: `observerSchema=c3-authoritative-result-presentation-shadow-v4`, `pipelineMode=PresentationShadow`, `combatSchema=1`
- B3 이동: Host/Client 모두 `EVIDENCE`, invalid·writer 충돌·정지 보행 위반 0
- ROOT: Host/Client 모두 `PASS`, 증거 drop 0
- C2: 서버 결과 1,278건, Host failure 0, Client accepted 1,278 / rejected 0
- C3: Host `invalid=2,230`, Client `invalid=2,255`로 양쪽 `FAIL`
- 비-UAS FATAL·ANR·예외: 현재 앱 PID와 경기 시간 범위에서 0

v3 대비 v4는 Host unmatched 915→33, direction mismatch 59→0, Client unmatched 866→37, direction mismatch 320→24로 exact correlation을 크게 개선했다. 그러나 이 개선은 C3 PASS가 아니며, 대량 Invalid를 먼저 제거해야 남은 방향·시간 오차를 유효하게 재판정할 수 있다.

## 2. 확정 원인

`NetworkCombatController.TickCombat`은 서버 쿨다운이 끝날 때마다 `ExecuteAttack`을 호출해 새로운 Shadow 공격 회차를 생성한다. 그러나 그 직후 정확한 `AttackPresentationScope`를 전달하는 `StartCombatClientRpc`가 `_combatAnimationSent.Add(id)` 안에 있다.

이 집합은 전투 이탈이나 Walk 재개까지 유지되므로 같은 전투에서 첫 공격만 RPC를 받는다. 후속 공격도 서버 결과는 정상 생성되지만 Host/Client `UnitView`의 marker cursor는 새 instance·sequence를 받지 못한다. 첫 회차의 hit 수를 소비한 뒤 `TryAcquirePresentationShadowMarkerScope`가 실패하고 Marker/Tracer는 `attackerInstanceId=0, sequenceId=0`인 Invalid 관측으로 기록된다.

즉 문제는 네트워크 연결, C2 피해 판정 또는 진단기 임계치가 아니다. **Animator 시작을 한 번으로 제한하는 가드가 매 공격 커밋의 데이터 발행까지 함께 막은 수명 결합 결함**이다.

## 3. 규칙 근거

- `NET-AUTH-001`: 공격 회차 생성과 타격 결과는 서버만 확정한다.
- `NET-ACTION-SEQ`: 모든 공격 회차는 단조 증가하는 `AttackSequenceId`와 `HitIndex`로 식별한다.
- `NET-PRESENT-001`: Animation Event는 표현 표식이며 실제 공격 회차를 만들지 않는다.
- `NET-PRESENT-002`: 같은 Attack 표현 상태에서도 새 커밋은 새 scope를 발행하고, 이미 재생 중인 Animator는 restart하지 않는다.
- `NET-PRESENT-003`: 표현 단계는 FIFO가 아니라 정규 결과 키와 정확한 회차 scope로 상관한다.

## 4. 영향 범위

직접 수정 후보:

- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

진단 표본이 한 종류의 Invalid로 먼저 소진되는 문제가 확인되면 다음 파일의 bounded failure quota도 함께 교정한다.

- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`

변경 금지:

- 서버 타겟·사거리·피해·HP·사망 판정
- C1/C2 authorization/result 의미
- B3 이동 및 Simulation Root writer
- 기존 Legacy VFX/SFX/HP 텍스트 emitter
- Attack 클립과 Animation Event 에셋

## 5. 조사 결론

후속 공격마다 기존 `StartCombatClientRpc`를 그대로 반복 호출해도 `restartAttackCycle=false`와 `UnitView`의 현재 Attack 상태 검사가 CrossFade를 막을 수 있다. 다만 코드 책임을 명확히 하기 위해 “전투 표현 시작 여부”와 “커밋 scope 발행 여부”를 서로 다른 변수로 계산하고, scope 발행은 `_combatAnimationSent` 가드 밖에서 매 커밋 실행해야 한다. 이 구조를 연속 N회 공격 회귀로 잠근 뒤에만 새 빌드를 만든다.
