# StreamSpirit production 공격 타임라인 교정 조사

StreamSpirit는 공격 애니메이션과 VFX 에셋 연결은 존재하지만, 서버가 피해를 확정하는 시각과 화면의 공격 marker 시각이 서로 다르다. 이번 조사는 실제 production 컨트롤러·클립·프리팹·설정 연결을 따라가 차이를 확정하고, 아직 구현되지 않은 서버 권위 발사체를 구현된 것처럼 취급하지 않으면서 현재 TimerImpact의 타격 시점만 화면 marker와 맞추기 위한 것이다.

**문서 상태:** `production marker/config + LegacyFallback VFX focused 실기 PASS · 서버 권위 발사체 이관은 별도 미완료`

## 1. 목표와 비목표

- 목표: StreamSpirit의 현행 서버 TimerImpact를 실제 Attack marker `0.50초`와 일치시킨다.
- 목표: production 에셋 연결과 LegacyFallback 표현 보존을 영구 검증해 과거 VFX 누락 회귀를 차단한다.
- 비목표: 서버 권위 발사체 생성·비행·착탄 시스템 구현.
- 비목표: StreamSpirit를 `Supported` 프로필로 승격하거나 실제로 없는 착탄 증거를 만든다.
- 비목표: 이동·회전·애니메이션 방향 보정, 다른 24종 유닛 일괄 변경, Testcase 작성.

## 2. production 연결 확인

| 항목 | 확인 결과 |
|---|---|
| UnitType | `StreamSpirit = 17` |
| 서버 설정 | 조사 당시 `hitFrameTimes = [0.17]` → 교정 후 `[0.50]`, `attackCooldown = 1.15` 유지 |
| Attack 클립 | `StreamSpirit_Attack.anim`, 길이 `1.50초`, `Loop Time=ON` |
| 실제 marker | `OnAttackHit` 1개, `0.50초` |
| Animator Controller | `StreamSpirit.controller`의 `Attack` state가 위 클립 GUID를 직접 참조 |
| Blue/Red prefab | 양쪽 모두 같은 production controller, `ApplyRootMotion=0` |
| 공격 VFX | `UnitEffectConfig → EffectPreset_StreamSpirit_Attack → vfx_streamspirit_attack` 연결 존재 |
| 현재 resolver | `Unresolved / ProjectileImpact / projectile-timeline-unresolved` |
| 현재 runtime | 권위 발사체가 없는 `TimerImpact` |

조사 당시 서버 피해 결과는 공격 시작 후 약 `0.17초`에 확정됐지만 화면의 공격 VFX marker는 `0.50초`에 발생해 같은 공격 회차에서 약 `0.33초` 어긋났다. 구현 단계에서 서버 TimerImpact 설정을 `[0.50]`으로 교정했다.

Attack 클립의 `Loop Time=ON`은 공통 전투 표현이 Attack 상태를 한 번 시작한 뒤 연속 재생하는 현행 구조의 일부다. 이를 끄는 것은 이번 결함의 최소 수정이 아니며, 공통 source-marker lease가 각 서버 공격 회차에 marker를 한 번만 결합하도록 유지해야 한다.

## 3. 과거 회귀와 반드시 보존할 계약

2026-09-07 실기에서 StreamSpirit 공격 VFX가 보이지 않는 회귀가 있었다. 에셋 누락이 아니라 `Unresolved` 공격에 default scope를 전달하고 공통 fail-closed gate가 marker를 VFX 호출 전에 차단한 것이 원인이었다.

현재 계약은 다음과 같다.

- `Supported` 공격은 정확한 presentation scope를 요구하고 잘못된 scope를 fail-closed 처리한다.
- StreamSpirit처럼 아직 `Unresolved`인 공격은 정규 scope를 조작해 만들지 않고 LegacyFallback marker VFX를 보존한다.
- source marker는 Stop-before-marker 상황에서도 확정된 이전 공격 회차의 VFX를 한 번 보존하되, 다음 loop에서 재방출하거나 새 타겟으로 전이하지 않는다.

이번 교정은 이 계약을 변경하지 않는다. 타임라인을 맞춘다는 이유로 StreamSpirit를 `Supported`로 승격하면 실제 발사체가 없는 상태를 숨기고, 이전과 같은 표현 회귀 또는 잘못된 착탄 판정을 만들 수 있다.

## 4. 원인 가설과 반증 가능한 예측

### 가설 1 — 서버 설정과 production marker 불일치가 직접 원인

가장 가능성이 높다. 설정은 `0.17초`, 실제 marker는 `0.50초`다.

**예측:** `hitFrameTimes`를 `0.50초`로 바꾸면 현행 TimerImpact의 피해 확정 시점과 marker/VFX 시점이 같은 공격 회차에서 일치한다. production 클립과 컨트롤러는 바꿀 필요가 없다.

### 가설 2 — production prefab이 조사한 클립과 다른 클립을 사용

정적 GUID 추적상 가능성은 낮다. Blue/Red prefab은 같은 controller를 참조하고 controller의 Attack state가 조사한 clip GUID를 직접 참조한다.

**예측:** 영구 검증에서 controller·Attack motion·양 prefab 연결을 exact GUID로 확인하면 다른 클립 사용 가설이 기각된다. 어느 하나라도 바뀌면 검증은 fail-closed 해야 한다.

### 가설 3 — 타임라인 교정 과정에서 Unresolved VFX 보존이 다시 깨짐

과거 실제로 발생한 회귀이므로 위험도가 높다.

**예측:** StreamSpirit가 계속 `Unresolved / ProjectileImpact / projectile-timeline-unresolved`이고 LegacyFallback marker가 1회 허용되는 계약을 함께 검증하면, 타임라인 교정이 정규 scope 위조나 VFX 차단으로 변질되지 않는다.

### 가설 4 — `0.50초` 정렬만으로 서버 권위 발사체까지 완성됨

거짓 가설이다. 현재 코드에는 StreamSpirit 전용 권위 발사체의 생성·비행·착탄 레코드가 없다.

**예측:** 설정 정렬 후에도 resolver 상태는 `Unresolved`로 남아야 한다. 추후 발사체 구현은 발사 시점, 이동 궤적, 타겟 사망·소멸, 착탄 위치, 취소·중복 방지 계약을 별도 Task에서 설계해야 한다.

## 5. 최소 교정 범위

1. `UnitStatsConfig.asset`의 StreamSpirit `hitFrameTimes`를 `[0.50]`으로 교정한다.
2. Unit Action Self Validation에 StreamSpirit production gate를 추가한다.
3. gate는 실제 Attack clip marker, controller motion, Blue/Red prefab controller와 `ApplyRootMotion=0`, runtime hitFrameTimes, VFX preset 연결을 검사한다.
4. resolver의 `Unresolved / ProjectileImpact / projectile-timeline-unresolved`와 LegacyFallback 표현 보존을 함께 검사한다.
5. `UnitCombatAssetMatrix.md`에는 구현·자동 검증 결과만 사실대로 갱신하고, 실제 게임 확인 전 Complete로 표시하지 않는다.

## 6. 관련 규칙

- `GameSystemRules_Units.md` 규칙 15: 공격 중 서버가 확정한 타겟 방향을 유지한다. 이번 작업에서 회전값을 섞지 않는 근거다.
- 같은 문서 규칙 17: Animation Event는 타격 표현 시점이며 피해 권위 자체가 아니다. 서버 TimerImpact와 표현 marker를 같은 시간축으로 맞추되 책임은 분리한다.
- 같은 문서 규칙 19: 피해·공격 처리는 서버 권위로 유지한다.
- 같은 문서 규칙 22: Animator 상태는 서버 행동 상태에서 파생되며 게임 판정 writer가 아니다.
- `GameSystemRules_UnitCombatSynchronization.md` `NET-AUTH-001`: 피해와 행동 회차의 권위는 서버에 있다.
- 같은 문서 `NET-PRESENT-001~003`: 결과와 표현은 같은 공격 회차로 결합하되 표현 도착이 gameplay 결과를 쓰지 않는다.
- 같은 문서 `NET-DELIVERY-PROJECTILE`: 발사체 공격은 발사·비행·착탄을 권위적으로 분리해야 한다. 이 계약이 아직 없으므로 StreamSpirit를 Supported로 승격하지 않는다.
- 같은 문서 `NET-CANCEL-001`: 향후 발사체는 공격자·타겟 소멸과 예약 취소를 명시적으로 처리해야 한다. 이번 TimerImpact 정렬로 그 구현을 대체하지 않는다.

## 7. 위험과 대응

| 위험 | 대응 |
|---|---|
| marker만 보고 실제 발사체가 완성됐다고 오판 | resolver를 Unresolved로 유지하고 별도 후속 Task로 분리 |
| 과거 StreamSpirit VFX 부재 재발 | LegacyFallback + source-marker 1회 보존을 production gate에 포함 |
| 다른 클립을 우연히 검사 | controller Attack motion GUID와 Blue/Red prefab controller를 exact 연결로 검사 |
| 공격 loop를 끄거나 매 회차 클립을 재시작 | Loop Time과 연속 Attack 상태를 유지하고 기존 per-cycle lease 계약 보존 |
| 설정 직렬화에서 다른 유닛 행 수정 | enum index가 아니라 `unitType intValue=17`인 정확한 entry만 수정·검증 |
| 자동 검증을 실기 완료로 오인 | 메뉴 PASS 후 빌드 시작까지만 수행하고 실제 생산 테스트는 사용자가 판정 |

## 8. 조사 결론

StreamSpirit의 현재 직접 결함은 production Attack marker `0.50초`와 서버 `HitFrameTimes 0.17초`의 불일치다. 컨트롤러와 양 진영 prefab은 조사한 클립에 연결되어 있고 VFX 에셋 체인도 존재한다. 따라서 이번 작업은 서버 TimerImpact를 `0.50초`로 정렬하고 이 연결을 영구 gate로 고정하는 최소 교정이 적절하다.

다만 StreamSpirit의 목표 프로필은 ProjectileImpact이고 실제 서버 발사체는 아직 없다. 이번 변경 뒤에도 `Unresolved / LegacyFallback`을 유지하며, 발사체 구현은 후속 Task에서 별도로 진행한다.

## 9. 구현 결과 — 2026-09-22

- `UnitStatsConfig.asset`의 StreamSpirit `hitFrameTimes`를 `0.17초`에서 `0.50초`로 교정했다.
- Unit Action Self Validation에 StreamSpirit production gate를 추가했다.
- gate는 exact controller/clip GUID, Attack state motion과 speed, `1.5초 · 30fps · Loop`, 유일한 `OnAttackHit @ 0.50초`, Blue/Red prefab controller와 `ApplyRootMotion=0`, root UnitView와 VfxSpawnPoint 참조, UnitEffectConfig attack preset, preset VFX prefab을 검사한다.
- resolver는 수정하지 않았으며 `Unresolved / ProjectileImpact / TimerImpact / projectile-timeline-unresolved`과 `KnownUnresolved` audit 분류를 gate에서 고정했다.
- 공격 animation, controller, Blue/Red prefab, VFX asset, 공통 런타임 로직은 변경하지 않았다.

후속 실행에서 Unity `Run Unit Action Self Validation`과 `Unit Root Pose Cross Audit` self-validation이 모두 PASS했고 최신 코드 C# 컴파일 오류는 0건이었다. Android 빌드 뒤 사용자가 Editor Host + Android Client 실기를 완료했으며 결과는 아래와 같다.

## 10. 사용자 실기 로그 결과 — 2026-09-22

- 공통 `sharedSessionKey=c50c68f41779b35a5a63f32479e8ad88b4886c71205ad1dba0aff69e5c0c9ad7`에서 두 경기를 확인했다.
- 첫 경기 Host `58cf105b…e50cb8`: starts 290, markers 286, VFX attempts/started 286/286, failures 0, timer impacts 284, complete 284, incomplete 6, unmatched marker/VFX/timer 0, duplicates 0, overflow false. 최대 start→marker `0.501130초`, start→timer `0.514834초`, marker↔timer 차이 `0.095232초`였다.
- 첫 경기 Client `334bd475…fadfff2`: starts 290, markers 288, VFX attempts/started 288/288, failures 0, complete 288, incomplete 2, unmatched 0, duplicates 0, overflow false. 최대 start→marker는 `0.732351초`였다.
- 둘째 경기 Host `054a67a6…6611e9c`: starts 321, markers 317, VFX attempts/started 317/317, failures 0, timer impacts 317, complete 317, incomplete 4, unmatched 0, duplicates 0, overflow false. 최대 start→marker `0.581307초`, start→timer `0.514173초`, marker↔timer 차이 `0.093398초`였다.
- 둘째 경기 Client `a54b495a…653000`: starts 321, markers 315, VFX attempts/started 315/315, failures 0, complete 315, incomplete 6, unmatched 0, duplicates 0, overflow false. 최대 start→marker는 `0.633411초`였다.
- 합계는 Host VFX `603/603`, Client VFX `603/603`, 실제 재생 실패 0건이다. 모든 VFX 결과는 `particleSystems=1`, `playbackActive=True`였다.
- incomplete는 network-despawn terminal에 남은 미완료 회차이며 `vfxFailures`로 분류되지 않았다. `dropped 686`, `817`로 상세 로그가 한도를 초과했으므로 각 미완료 원인이 단순 진행 중, 정상 취소, 타겟 소멸 중 무엇인지는 이번 자료만으로 확정하지 않는다. 일반 UAS END의 FAIL은 이 상세 로그 한도 초과와 전체 25종 중 `coveredUnitTypes 3/25`, `4/25`인 focused 실행 범위 때문이며 StreamSpirit 전용 terminal 결과를 부정하지 않는다.
- 최신 Host/Client ROOT summary는 PASS였고 구조·이동 오류는 0건이었다. 사용자는 진단 로그 추가 전 육안상 특별한 문제가 없어 보였다고 확인했고, 이번 로그에서도 VFX 누락은 재현되지 않았다.
- Client 최대 marker `0.732351초`는 timing variance 사실로 남기되, VFX가 실제로 모두 시작됐으므로 누락으로 확대 해석하지 않는다.

**판정:** StreamSpirit의 production marker/config 정렬과 `Unresolved` LegacyFallback VFX 보존 범위는 focused PASS다. 서버 권위 발사체, 발사/착탄 exact-key 상관관계, 전체 25종 Host/Client 역할교대와 Legacy rollback은 이번 증거 범위가 아니므로 `MigrationRequired / Unresolved`를 유지한다.
