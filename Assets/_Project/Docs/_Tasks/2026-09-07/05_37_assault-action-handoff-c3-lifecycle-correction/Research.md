# 어설트 공격 진입 멈춤과 C3 종료 수명 교정 — 조사

이번 작업은 Walk 애니메이션을 삭제하는 작업이 아니다. 유닛이 공격 사거리에 들어왔을 때 서버 위치는 즉시 멈춰야 하지만, 화면은 정지된 Walk 자세를 잠깐 끼우지 않고 현재 이동에서 Attack 표현으로 바로 이어져야 한다. 동시에 공격한 적 없는 유닛의 정상 종료를 C3 실패로 잘못 세는 문제를 고쳐, 실제 공격 표현 불일치와 진단기 허위 실패를 분리한다.

## 1. 사용자 관찰과 로그 구성

- 사용자 관찰: Assault가 이동하다 적이 공격 사거리에 닿는 순간 잠깐 멈춰 보임
- 경기: Android Host / Editor Client
- 공통 세션: `3a3e92c1d440168db5bf377933e14eb038b21d7edfdcec53a5467b02c3665ccc`
- Android 로그: `Assets/_Project/Docs/_Logs/2026-09-07/04_58_logcat/RuntimeLog_device.txt`
- Editor 로그: `Assets/_Project/Docs/_Logs/_editor/2026-09-07/RuntimeLog.txt`

## 2. 보존된 축

- C2 서버 결과 2,591건, 결과 실패 0
- Client 결과 수락 2,591건, 거부 0
- B3 이동의 reject·invalid·writer 충돌·stationary Walk 위반·fatal repath 0
- Host/Client ROOT 로컬 판정 PASS
- Android 비-UAS 크래시·ANR·미처리 예외 0

서버 피해 writer, 권위 Root 및 복제 구조를 되돌릴 근거는 없다.

## 3. Assault 멈춤의 직접 원인

공격 사거리 진입에서 이동 reducer가 `TargetAcquirePriority + NoIntent`로 서버 위치를 멈추는 것은 규칙에 맞다. 문제는 `UnitMovementPresentationPolicy.ShouldHoldWalk`가 이 전환을 일반 정지와 구분하지 않아 `SetMovementHeldAnimation(true)`를 선택한다는 점이다. `HoldMovementAnimation()`은 Walk 첫 자세로 이동한 뒤 Animator 속도를 0으로 만든다. 그 다음에야 서버 Action 회전과 provisional Attack 이벤트가 시작된다.

실측 Assault Unit 1은 최초 정지 표본에서 공격 가능 방향까지 약 0.202초, 실제 회차 커밋까지 약 0.297초가 걸렸다. 이 기간의 위치 정지는 정상이나 Attack 선행 표현이 이어져야 하며, 정지 Walk가 중간에 삽입되는 것은 규칙 위반이다.

Walk 클립은 정상 이동과 재경로 보류 표현에 필요하므로 삭제 대상이 아니다. 잘못된 것은 에셋이 아니라 행동 handoff에 일반 Held 정책을 적용한 호출 관계다.

## 4. C3 허위 실패의 직접 원인

경기에서 생산 완료된 유닛은 142개다. 유효 공격 인스턴스를 가진 유닛 111개는 정상 retire됐고, 공격한 적 없는 나머지 31개는 `AttackerInstanceId.None`으로 Despawn했다. `NetworkUnit.OnNetworkDespawn()`이 이를 무조건 Coordinator에 전달하고 Coordinator가 Invalid 실패로 세어 양측 `failures=31`이 됐다.

Schedule 2,648개와 Result 2,591개 중 모든 결과가 `ready=2591, pending=0`으로 수렴했다. 따라서 31건은 공격 결과 결합 실패가 아니라 정상 수명 부재를 잘못 분류한 진단기 실패다.

## 5. 별도로 남아 있는 C3 비교 실패

허위 실패와 별개로 레거시 실방출은 Host timing mismatch 8건, unmatched emit 2건, 방향 증거 오류 1건을 만들었다. Client에도 revision-lag 45건과 방향 증거 부재 26건이 있다. 현재 빌드는 `actualEmitter=Legacy`, `presentationWrites=0`인 비교 단계이므로 신규 Coordinator가 화면을 바꾼 결과가 아니다. 이 작업은 수명 허위 실패를 제거하고 실제 비교 실패가 가려지지 않도록 유지한다. AoE 완료 manifest와 단일 emitter 전환 전에는 레거시를 성급하게 삭제하지 않는다.

## 6. 영향 범위

- 이동 표현 정책과 실제 적용: `UnitMovementContracts.cs`, `UnitView.cs`
- 이동 진단과 회귀: `UnitMovementAuthorityObserver.cs`, `RunUnitActionSelfValidation.cs`
- C3 수명: `AuthoritativeAttackPresentationCoordinator.cs`, `NetworkUnit.cs`
- C3 회귀: `RunAuthoritativeAttackPresentationValidation.cs`

## 7. Round 2 실기 — StreamSpirit 공격 VFX 회귀

- 경기: Editor Host / Android Client
- 공통 세션: `a13eb957a9d5ad7afc930e8a5a6e00735dc965a9b1b39c54d6924e08428dd898`
- Android 로그: `Assets/_Project/Docs/_Logs/2026-09-07/09_43_logcat/RuntimeLog_device.txt`
- 사용자 육안 관찰: 2단계 물정령 `StreamSpirit`의 공격 VFX가 보이지 않음

에셋 연결은 정상이다. `UnitEffectConfig`의 UnitType 17은 `EffectPreset_StreamSpirit_Attack`을 참조하고, 그 프리셋은 `vfx_streamspirit_attack.prefab`을 참조한다. 로그에서도 StreamSpirit이 생성·이동하고 Unit 26이 공격 시작 gate의 `Ready`까지 도달했다.

직접 원인은 C3 지원 상태와 표현 gate를 잘못 결합한 것이다. `UnitAttackShadowProfileResolver`는 StreamSpirit을 `Unresolved / ProjectileImpact / projectile-timeline-unresolved`로 분류하므로 `ExecuteAttack`이 정규 Shadow token과 presentation scope를 만들지 않는다. 그런데 호출부는 default scope에도 `impactEnabled=true`를 보내고, `StartCombatAnimation`의 lease arm은 실패한다. 이후 `OnAttackHit`은 lease 소비 실패로 `EffectManager.PlayUnitAttack` 전에 반환하므로 기존 Legacy VFX까지 사라진다.

이는 “Unresolved는 비교에서 격리하되 기존 gameplay/VFX는 보존한다”는 기존 전환 원칙을 위반한 구현 회귀다. 미지원 타입에 가짜 스코프를 만들거나 strict gate를 느슨하게 해서는 안 된다. 지원 상태별 표현 정책을 분리해 `Supported`만 정규 lease를 요구하고, `Unresolved`는 Legacy VFX 경로를 유지해야 한다.

같은 경기에서 B3는 reject·invalid·gate·writer·stationary Walk 실패 0, 공간 commit 1,193/1,193이고 ROOT는 양측 PASS였다. C2도 서버 결과 2,587/실패 0, Client 수락 2,587/거부 0이다. C3 종료 수명은 양측 `failures=0`, `ready=2587`, `pending=0`으로 Round 1의 `Retire(None)` 허위 실패 교정이 확인됐다. 다만 Legacy 표현 비교는 Host 방향 2·timing 14·emit unmatched 2, Client 방향 67(revision-lag 50, scope mismatch 1, evidence invalid 16)·timing 1로 여전히 FAIL이다. 비-UAS 크래시·ANR·미처리 예외는 없었다.
