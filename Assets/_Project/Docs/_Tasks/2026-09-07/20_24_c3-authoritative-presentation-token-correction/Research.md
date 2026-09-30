# C3 공격 연출 토큰 교정 — 조사

서버의 공격 결과는 양쪽 기기에 같은 수로 전달됐지만, 화면에서 맞는 효과를 보여 주는 순간은 여전히 늦었다. 새 비교기는 결과를 잘 연결했으나 화면은 기존 경로가 재생하고 있었다. 이번 작업은 공격 한 번의 일정과 확정 결과를 끝까지 같은 식별자로 전달하고, 효과 재생의 책임과 종료 처리를 명확히 하는 후속 교정이다.

## 1. 기존 계획과의 관계

상위 계획은 `_Tasks/2026-09-02/15_29_c3-authoritative-presentation-coordinator/Plan.md`다. 이번 Task는 그 비교 단계 이후 공격 표현 토큰과 실제 소비자 전환을 구체화한다. C1·C2·B3를 다시 설계하지 않는다. 비교 수렴과 실제 표현 PASS를 별도로 판단한다.

## 2. 20:00 경기 증거

- 세션: `209be7cc0eeab2810994f220837e6f6b5e15602f10d507c1d3834b0a6b0bfc36`, Editor Host / Android Client.
- 원본: `_Logs/_editor/2026-09-07/RuntimeLog.txt` terminal 9000~9005, `_Logs/2026-09-07/20_00_logcat/RuntimeLog_device.txt` terminal 71464~71504.
- C2: 서버 결과 1964/실패 0, Client 수락 1964/거부 0. Host 타겟 불일치 0, MOVE 실패 0, ROOT 양측 로컬 PASS.
- Coordinator 양측 results=ready=1964, pending/failures/duplicates=0. actualEmitter=Legacy, aoeCompleteManifest=false, presentationWrites=0.
- Host 표현: normal timing 20건/최대 0.923348초, recovery timing 5건/최대 0.923814초, unmatched Emit 4건. 방향 증거 invalid 2건, same-revision 방향 불일치 0.
- Client 표현: timing 3건/최대 0.804798초, recovery 2건/최대 0.871315초, unmatched 0. 방향 56건은 revision lag 43+invalid 13, same-revision 0.
- 포함 타입 9/25. 종료 Lobby 404는 별도의 전투 외 정리 문제이며 본 Task의 공격 판정 수정 근거가 아니다.

## 3. 해석과 미확정 경계

Host에도 큰 표현 지연이 있어 네트워크만의 문제로 볼 수 없다. 레거시 marker·tracer 도착·death flush가 여전히 방출을 소유한다는 런타임 증거와 일치한다. 다만 terminal 합계만으로 모든 개별 지연의 직접 호출 원인이 같다고 단정하지 않는다. 구현 전 exact hit별 Schedule→Result→Marker→Tracer→Emit 흐름을 재현해야 한다.

Client의 revision lag와 증거 부재는 동일 revision에서 잘못 조준했다는 증거가 아니다. 커밋 방향, 실제 Impact 방향, 현재 복제 방향을 혼합하지 않는 토큰이 필요하다. 방향 writer를 바꾸는 근거로 사용하지 않는다.

Schedule 2010과 Result 1964의 차이를 곧바로 유실로 계산하지 않는다. 취소된 미래 hit는 결과를 생성하지 않을 수 있으며 종료 계약과 함께 검사한다. 반대로 ready=1964는 실제 화면 emit=1964를 의미하지 않는다.

## 4. 필요한 계약

- 공격 토큰: instance+sequence+HitIndex로 예약 식별. commit revision/의도 방향과 실제 Result revision/Impact 방향은 구분한다.
- 결과 토큰: 피해자 종류·ID·효과·ordinal을 포함한 정규 키. schedule은 미래 hit나 피해자 수를 예측하지 않는다.
- Marker는 선택적 VFX 표식. marker 부재가 결과 표시를 막지 않고 tracer 도착이 결과 시각을 결정하지 않는다.
- 취소·사망은 미확정 미래 허가를 닫고 확정 결과는 보존한다. Result/종료 알림 순서 역전, ID reuse, 사라진 View를 처리한다.
- AoE 동시 표시에는 서버 완료 manifest가 필요하다. 현재 false이므로 이를 완료했다고 보고할 수 없다.
- 기존 공통 표현 시간축 및 늦은 결과 catch-up을 사용한다. 새 임의 지연이나 허용치로 오차를 가리지 않는다.

## 5. 조사할 구현 경계

Application의 AuthoritativeAttackPresentationCoordinator·UnitAttackPresentationPolicy·UnitAttackResultPresentationShadow, Infrastructure의 NetworkUnit·NetworkCombatController·UnitAttackShadowObserver, Presentation의 UnitView·HitPresentationQueue·EffectManager·TracerProjectile과 그 직접 호출처를 점검한다. 정확한 수정 파일은 호출 흐름 재현 후 구현 결과에 기록한다.

Unresolved 유닛의 LegacyFallback은 기존 에셋을 계속 표시해야 한다. Supported 토큰 fail-closed를 완화하거나 가짜 scope로 둘을 섞지 않는다. 기존 단일 gameplay writer, 레이어 의존 방향, 사거리·이동·타겟 규칙을 보존한다.
