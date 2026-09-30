# FoxMagician production 공격 타임라인 교정 조사

> **최신 상태 — 2026-09-28 사용자 재테스트 반영:** Fox 피해 표시 지연 focused 교정은 사용자 수용. 약간 이른 현행 타이밍은 유지하고 재튜닝은 향후 projectile 구현으로 이관한다. 전체 migration은 미완이며 최신 근거는 §21이다. 이전 OPEN 기록은 당시 이력이다.

FoxMagician은 production 공격 애니메이션과 VFX 연결은 존재하지만, 서버가 피해를 확정하는 TimerImpact 시각과 화면의 공격 marker 시각이 서로 다르다. 이번 조사는 실제 production 컨트롤러·클립·프리팹·설정 연결을 따라가 차이를 확정하고, 아직 구현되지 않은 서버 권위 발사체를 구현된 것처럼 취급하지 않으면서 현행 TimerImpact의 타격 시점만 실제 marker와 맞추기 위한 것이다.

**문서 상태:** `FoxMagician bounded runtime diagnostic 구현 및 자동 검증 PASS · 후속 빌드 시작 전 · 실제 경기 로그 미검증으로 VFX 재생 및 정확한 1.00초 정렬은 미판정 · MigrationRequired / Unresolved / LegacyFallback 유지`

## 1. 목표와 비목표

- 목표: FoxMagician의 현행 서버 TimerImpact를 실제 Attack marker `1.00초`와 일치시킨다.
- 목표: production 에셋 연결과 미지원 발사체 경계를 영구 검증해 설정 회귀와 허위 지원 승격을 차단한다.
- 비목표: 서버 권위 발사체 생성·비행·착탄 시스템 구현.
- 비목표: FoxMagician을 `Supported` 프로필로 승격하거나 실제로 없는 tracer·착탄 증거를 만든다.
- 비목표: 공격 animation, controller, Blue/Red prefab, VFX asset 또는 공통 runtime/resolver 동작 변경.
- 비목표: 다른 유닛 일괄 변경과 Testcase 작성.

## 2. production 연결 확인

| 항목 | 확인 결과 |
|---|---|
| UnitType | `FoxMagician = 21` |
| 서버 설정 | `hitFrameTimes = [2.25]`, `attackCooldown = 4.00` |
| Attack 클립 | `FoxMagician_Attack.anim`, 길이 `4.00초`, `30fps`, `Loop Time=ON` |
| 실제 marker | `OnAttackHit` 1개, `1.00초` |
| Animator Controller | `FoxMagician.controller`의 `Attack` state가 위 클립을 직접 참조, state speed `1.0` |
| Blue/Red prefab | 양쪽 모두 같은 production controller, `ApplyRootMotion=OFF` |
| VFX spawn | 양 prefab의 UnitView가 각 prefab의 유일한 `VfxSpawnPoint`를 참조, local position `(0, 0, 0.011)` |
| 공격 VFX | `UnitEffectConfig → EffectPreset_FoxMagician_Attack → vfx_foxmagician_charge`, ParticleSystem 존재 |
| 현재 resolver | `Unresolved / ProjectileImpact / TimerImpact` |
| 미지원 사유 | `projectile-system-unresolved` |

현재 서버 TimerImpact는 공격 시작 후 `2.25초`에 피해를 확정하도록 설정되어 있으나, production Attack clip의 유일한 화면 marker는 `1.00초`다. 같은 공격 회차에서 `1.25초`의 직접적인 설정 불일치가 있다.

Attack 클립의 `Loop Time=ON`은 공통 전투 표현 구조의 일부다. 이를 끄거나 공격 회차마다 클립을 재시작하는 것은 이번 결함의 최소 수정이 아니다.

## 3. 원인 가설과 조사 판정

### 가설 1 — 서버 설정과 production marker 불일치가 직접 원인

**강하게 지지됨.** runtime 설정은 `2.25초`, 실제 production marker는 `1.00초`다.

**반증 가능한 예측:** `unitType=21`의 `hitFrameTimes`만 `1.00초`로 교정하면 현행 TimerImpact의 결과 시점과 marker 시점이 같은 기준값을 사용한다. production animation과 controller는 바꿀 필요가 없다.

### 가설 2 — production prefab이 조사한 클립과 다른 클립을 사용

**정적 증거로 기각됨.** Blue/Red prefab은 같은 FoxMagician controller를 사용하고, controller의 `Attack` state는 조사한 FoxMagician 공격 clip을 직접 참조한다.

**회귀 예측:** controller·Attack state motion·양 prefab 연결 중 하나라도 달라지면 영구 검증은 fail-closed 해야 한다.

### 가설 3 — VFX 에셋 또는 spawn 연결 누락이 직접 원인

**정적 연결 누락은 기각됨.** UnitEffectConfig에서 exact preset과 VFX prefab까지 연결되며 ParticleSystem이 존재한다. 양 prefab의 UnitView도 유일한 VfxSpawnPoint를 참조한다.

다만 정적 연결은 실제 기기 재생 성공의 증거가 아니다. runtime 재생 여부는 구현 후 사용자의 실제 생산 테스트와 로그로 판정한다.

### 가설 4 — `1.00초` 정렬만으로 서버 권위 발사체까지 완성됨

**거짓 가설이다.** 현재 resolver가 명시하듯 권위 발사체 시스템은 미구현이다. 설정 정렬은 TimerImpact 시점을 맞출 뿐 발사체 생성·비행·착탄 또는 tracer 상관관계를 만들지 않는다.

따라서 교정 뒤에도 `MigrationRequired / Unresolved / LegacyFallback`을 유지해야 한다.

## 4. 반드시 보존할 계약

- 피해와 공격 회차의 gameplay 권위는 서버에 유지한다.
- Animation Event는 표현 시점이며 피해 writer가 아니다.
- `Supported` 공격의 잘못된 presentation scope는 fail-closed 한다.
- FoxMagician처럼 `Unresolved`인 공격은 실제로 없는 projectile scope나 tracer를 만들어내지 않고 기존 LegacyFallback 표현을 보존한다.
- 타이밍 교정만으로 resolver를 `Supported`로 승격하지 않는다.
- StreamSpirit 전용 runtime observer를 이름만 바꿔 복제하지 않는다. 우선 exact production gate와 설정만 교정하고, 실기 로그가 부족할 때 별도의 bounded 진단을 설계한다.

## 5. 최소 교정 범위

1. `UnitStatsConfig.asset`의 명시값 `unitType=21` entry에서 `hitFrameTimes`를 `[2.25]`에서 `[1.00]`으로 교정한다.
2. Unit Action Self Validation에 FoxMagician exact production gate를 추가한다.
3. gate는 clip/controller/Attack state, marker, Blue/Red prefab, ApplyRootMotion, UnitView/VfxSpawnPoint, VFX preset chain, runtime 설정과 resolver 미지원 경계를 함께 검사한다.
4. 직렬화 entry는 `enumValueIndex`가 아니라 `(int)UnitType.FoxMagician`, 즉 명시값 `21`로 찾고 검증한다.
5. `UnitCombatAssetMatrix.md`에는 구현·자동 검증 결과만 사실대로 갱신하고, 실기 확인 전 Complete로 표시하지 않는다.

## 6. 관련 규칙

- `GameSystemRules_Units.md` 규칙 15: 공격 중 서버가 확정한 타겟 방향을 유지한다. 이번 타이밍 교정에 회전 보정을 섞지 않는다.
- 같은 문서 규칙 17: Animation Event는 타격 표현 시점이며 피해 권위 자체가 아니다.
- 같은 문서 규칙 19: 피해·공격 처리는 서버 권위로 유지한다.
- 같은 문서 규칙 22: Animator 상태는 서버 행동 상태에서 파생되며 게임 판정 writer가 아니다.
- `GameSystemRules_UnitCombatSynchronization.md` `NET-AUTH-001`: 피해와 행동 회차의 권위는 서버에 있다.
- 같은 문서 `NET-PRESENT-001~003`: 결과와 표현은 같은 공격 회차로 결합하되 표현 도착이 gameplay 결과를 쓰지 않는다.
- 같은 문서 `NET-DELIVERY-PROJECTILE`: 발사체 공격은 발사·비행·착탄을 권위적으로 분리해야 한다. 이 계약이 아직 없으므로 FoxMagician을 Supported로 승격하지 않는다.
- 같은 문서 `NET-CANCEL-001`: 향후 발사체는 공격자·타겟 소멸과 예약 취소를 명시적으로 처리해야 한다. 이번 TimerImpact 정렬로 그 구현을 대체하지 않는다.

## 7. 위험과 대응

| 위험 | 대응 |
|---|---|
| marker 정렬을 발사체 완료로 오판 | resolver를 `Unresolved`로 유지하고 후속 Task 경계를 명시 |
| 다른 유닛 설정 행 수정 | `enumValueIndex`를 금지하고 `unitType intValue=21` exact entry만 수정·검증 |
| 다른 clip을 우연히 검사 | prefab controller → Attack state motion → clip GUID 연결을 exact 검증 |
| 정적 VFX 연결을 실제 재생 PASS로 오판 | 실제 생산 테스트와 runtime 로그 전에는 재생 성공을 미확정으로 유지 |
| 공통 runtime에 불필요한 유닛별 분기 추가 | 설정과 Editor production gate만 최소 변경 |
| 기존 LegacyFallback 표현 회귀 | resolver 미지원 상태와 VFX preset chain을 함께 검증 |
| 자동 검증을 실기 완료로 오인 | 두 메뉴 PASS 후 빌드는 시작만 하고 완료 확인·실기 판정은 사용자에게 인계 |

## 8. 조사 결론

FoxMagician의 현재 직접 결함은 production Attack marker `1.00초`와 서버 TimerImpact 설정 `2.25초`의 불일치다. Blue/Red prefab은 조사한 controller와 clip에 연결되어 있고 VFX 에셋 및 spawn chain도 정적으로 존재한다. 따라서 `unitType=21`의 타격 시점을 `1.00초`로 정렬하고 이 연결을 영구 gate로 고정하는 최소 교정이 적절하다.

다만 실제 서버 권위 발사체는 아직 없다. 이번 변경 뒤에도 `MigrationRequired / Unresolved / LegacyFallback`을 유지하며, 발사체 생성·비행·착탄과 exact-key 결합은 별도 후속 Task로 다룬다.

## 9. 구현 결과 — 2026-09-22

- `UnitStatsConfig.asset`의 명시값 `unitType=21` FoxMagician 행에서 `hitFrameTimes`만 `2.25초`에서 `1.00초`로 교정했다. `attackCooldown=4.00`과 다른 스탯은 변경하지 않았다.
- Unit Action Self Validation에 FoxMagician exact production gate를 추가했다.
- gate는 controller/clip GUID, Attack state motion과 speed, `4.00초 · 30fps · Loop`, 유일한 `OnAttackHit @ 1.00초`, Blue/Red controller와 Root Motion, root UnitView와 유일한 VfxSpawnPoint 참조 및 local position, UnitEffectConfig의 명시값 `21` 행, exact preset/VFX prefab과 ParticleSystem 존재를 검사한다.
- resolver는 수정하지 않았으며 `Unresolved / TargetLocked / ProjectileImpact / TimerImpact / projectile-system-unresolved`과 `KnownUnresolved` audit 분류를 gate에서 고정했다.
- 중복을 줄이기 위해 기존 StreamSpirit prefab 검증을 범용 production prefab 검증 helper로 통합했으며 기존 StreamSpirit 검증 범위는 유지했다.
- animation, controller, Blue/Red prefab, VFX asset, resolver, runtime observer와 공통 runtime 코드는 변경하지 않았다.

## 10. 자동 검증 결과 — 2026-09-22

- Unity forced synchronous recompile에서 Tundra build success를 확인했고 Runtime/Editor C# 오류 `CS`는 0건이었다.
- `Tools/check_docs.py`를 UTF-8 환경에서 실행해 문제 없음을 확인했다.
- Unity 메뉴 `Hexiege → Combat → Run Unit Action Self Validation`은 PASS했다.
- `[UAS-DIAG][PRODUCTION-TIMELINE] PASS`에 FoxMagician exact production timeline contracts가 포함됐다.
- Unity 메뉴 `Hexiege → Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit`은 PASS했다.
- Unity Console은 error 0건이며 기존 warning 1건만 남았다.

**현재 판정:** FoxMagician의 production marker/config 정렬과 exact asset gate는 자동 검증 PASS다. Unity `File → Build And Run`을 실행했고 화면의 `Checking prerequisites / Starting Android build`로 Android 빌드 시작을 확인했다. 빌드 완료 여부는 확인하지 않았으며 사용자 담당이다. 실제 게임 production 실기 및 runtime VFX 재생도 미검증이므로 실기 PASS 또는 Complete로 판정하지 않는다. 서버 권위 발사체와 tracer도 여전히 미구현이므로 `MigrationRequired / Unresolved / LegacyFallback`을 유지한다.

## 11. 최신 실기 로그 분석 — FoxMagician 89기

최신 실기 세션에서는 FoxMagician 89기가 생산되어 공통 공격·결과·표현·이동 계약을 충분한 수량으로 통과했다. 그러나 현행 공통 로그는 FoxMagician만의 공격 시작부터 Animation Event, 실제 source VFX 재생 결과, 서버 TimerImpact까지를 한 회차로 연결하지 않는다. 따라서 아래의 오류 0 수치는 공통 계약의 건전성을 보여 주지만, FoxMagician VFX가 실제로 재생됐는지와 marker·TimerImpact가 정확히 1.00초에 정렬됐는지를 직접 증명하지는 않는다.

### 11.1 공통 공격·결과 계약

| 관측 축 | 최신 실측 |
|---|---|
| 생산 수 | FoxMagician 89기 |
| Host 공격 | commits 114 / impactSamples 109 / correlationFailures 0 / invalid 0 / targetMismatches 0 / dropped 0 / manifestFailures 0 |
| Coordinator | schedules 119 / results 109 / ready 109 / pending 0 / failures 0 / duplicates 0 |
| Client 결과 | accepted 109 / rejected 0 |
| 공통 표현 | expectedVisual 108 / presentationEmits 108 / viewUnavailable 0 / duplicateAttempts 0 / transportFailures 0 / failures 0 |

이 수치는 Host 결과와 Client 수락, Coordinator 준비 상태, 공통 결과 표현이 오류 없이 수렴했음을 뜻한다. 다만 `presentationEmits=108`은 공통 결과 표현의 방출 수치이지 FoxMagician source VFX prefab의 실제 `ParticleSystem.Play` 성공 수치가 아니다.

### 11.2 이동·ROOT와 cross audit 해석

- Host와 Client의 local movement/ROOT 판정은 모두 PASS였고 이동·복제 오류는 없었다.
- latest cross analyzer의 `INCONCLUSIVE`는 day-level Host 로그에 동일 movement observer schema가 여러 세션 중복 포함되어 역할 분류를 거부한 로그 패키징 문제다.
- 따라서 이 `INCONCLUSIVE`를 FoxMagician 또는 공통 gameplay movement failure로 해석하지 않는다. 동시에 로그 패키징 문제를 근거로 cross audit PASS를 새로 만들어내지도 않는다.

### 11.3 아직 결론낼 수 없는 축

현행 로그에는 FoxMagician 전용으로 다음 흐름을 같은 공격 key에 연결한 증거가 없다.

1. 공격 시작
2. `OnAttackHit` marker 도달
3. source VFX 재생 시도
4. source VFX started/failure 결과와 ParticleSystem·playback 증거
5. 서버 TimerImpact

그러므로 이번 세션만으로는 다음 두 항목을 판정하지 않는다.

- FoxMagician의 source VFX가 Host와 Client에서 실제로 재생됐는가.
- attack start 기준 marker와 서버 TimerImpact가 정확한 1.00초 occurrence에 함께 정렬됐는가.

이는 gameplay 실패가 확인됐다는 뜻이 아니라, 필요한 FoxMagician 전용 관측 이음매가 공통 로그에 없다는 뜻이다.

## 12. 조사 결론 갱신 — bounded runtime diagnostic 조건 충족

기존 조사와 계획은 production gate와 설정 교정 뒤에도 실기 로그가 부족하면 별도의 bounded 진단을 설계하도록 경계를 남겼다. 최신 89기 세션은 공통 계약 오류 0을 확인했지만 FoxMagician 전용 VFX 재생 및 1.00초 정렬을 판정할 수 없었으므로 그 조건이 충족됐다.

후속 진단은 `FoxMagician + LegacyFallback`만 관측하는 read-only 계측이어야 한다. 공격 시작, marker, source VFX attempt와 started/failure, ParticleSystem 수와 playback 상태, 서버 TimerImpact를 현행 공격 key 하나로 연결하고 제한된 상태 저장소와 terminal summary를 사용한다. 진단은 `gameplayWrites=0`을 유지하며 피해 승인, HP, 공격 회차, 타겟, 쿨다운, Animator, VFX 재생 자체를 변경하지 않는다.

후속 진단이 필요한 상태이므로 이 Task를 Complete/PASS로 닫지 않는다. resolver, 피해 writer, `UnitStatsConfig`, animation/controller, Blue/Red prefab, VFX asset과 권위 projectile/tracer 지원 상태도 이번 문서 갱신으로 바꾸지 않는다. Testcase.md는 만들지 않으며, 향후 구현 뒤 Android Build And Run은 에이전트가 시작만 하고 완료 확인은 사용자가 담당한다.

## 13. bounded runtime diagnostic 구현 결과 — 2026-09-23

FoxMagician의 실제 공격 흐름을 다음 실기 로그에서 직접 판정할 수 있도록 `foxmagician-production-timeline-v1` 읽기 전용 진단을 추가했다. 이 진단은 게임 동작을 고치는 코드가 아니라, 이미 실행된 공격 시작·Animation Event·source VFX 반환·서버 TimerImpact를 한 회차로 묶어 기록하는 관측 장치다.

- `FoxMagician + LegacyFallback`만 관측한다.
- 공격 시작, marker, VFX attempt/result, ParticleSystem 양수 결과, playback 활성 결과, 최대 ParticleSystem 수, 서버 TimerImpact와 writer 결과를 기록한다.
- 서버에서는 개발 진단 전용 correlation ticket을 `ExecuteAttack`의 타격 코루틴에 전달하고, 공격 표현 revision이 발급되면 `(unitId, presentationRevision)`에 결합한다. TimerImpact는 이 ticket을 통해 exact flow만 찾으므로 시간 근접 또는 FIFO 추정을 사용하지 않는다.
- flow와 ticket 양방향 mapping은 모두 64회차 상한을 공유한다. 용량이 차면 완료된 가장 오래된 flow만 retire하며, Bind 뒤 Host start flow가 만들어지지 않은 실패도 TimerImpact unmatched 경계에서 mapping을 회수한다.
- terminal에는 starts, markers, VFX attempts/started/failures, particle/playback 집계, TimerImpact, complete/incomplete, 단계별 unmatched, duplicate, overflow, dropped, 최대 시간 차와 `gameplayWrites=0`을 남긴다.
- 피해 승인·HP·타겟·쿨다운·Animator·VFX 실행 결과·resolver·에셋과 projectile 지원 상태는 변경하지 않았다.

수정 파일은 `UnitAttackShadowObserver.cs`, `UnitView.cs`, `NetworkCombatController.cs`, `RunUnitActionSelfValidation.cs` 네 개다. StreamSpirit의 기존 schema·flow·FIFO 선례와 검증 계약은 변경하지 않았다.

Unity 재컴파일 결과 Runtime/Editor C# 오류는 0건이다. `Run Unit Action Self Validation`과 `Self Validate Unit Root Pose Cross Audit`은 모두 PASS했고 Console error는 0건이다. 문서 정합성 검사도 문제 0건이다. 다만 이 자동 PASS는 진단 장치의 구조와 불변식을 검증한 결과이며, FoxMagician 실제 경기의 VFX 재생 및 1.00초 정렬 증거는 새 빌드 실기 로그를 분석하기 전까지 미검증이다. 따라서 Task는 아직 Complete가 아니다.

## 14. 실기 증거에 따른 타임라인 판단 정정 — 2026-09-27

### 14.1 사용자 설계와 재현된 결함

사용자가 확정한 의도는 `OnAttackHit`에서 시작하는 charge VFX의 후반부에 실제 피해가 적용되는 것이다. 최신 실기에서는 피해가 VFX 시작과 거의 동시에 적용되는 현상이 육안으로 관측됐고, 전용 진단이 같은 현상을 재현했다.

- 공통 세션: `7d75ac21d83e860563715880575f90a923f1ea374ebc4a2bdbf6301e7d11219b`
- Host terminal: starts 1,137 / markers 1,124 / VFX started 1,124 / TimerImpact 1,127 / complete 1,123 / VFX failure 0
- Host의 exact flow에서 marker, VFX result와 `writerResult=Applied` TimerImpact가 대부분 같은 `serverTime`에 기록됐다.
- marker와 TimerImpact의 최대 차이도 `0.201530초`여서 2초 길이 charge VFX의 후반부가 아니라 시작 구간이다.
- production VFX의 ParticleSystem emission duration은 2초이고, 현행 `OnAttackHit`은 공격 시작 후 1.00초다.

따라서 이전의 “production marker 1.00초와 TimerImpact를 같은 occurrence로 맞춘다”는 가설은 사용자 설계에 대해 반증됐다. Animation Event는 charge VFX 시작 marker이고, 서버 피해 TimerImpact는 그 뒤의 별도 gameplay impact다. 2026-09-22에 `hitFrameTimes`를 2.25초에서 1.00초로 바꾼 결정은 이 두 의미를 잘못 동일시했다.

### 14.2 원인 가설 재평가

1. **가설 A — VFX 호출이 직접 피해를 쓴다:** 기각. VFX는 `UnitView.OnAttackHit`에서 재생되지만 실제 피해는 서버 `DelayedAttackDamage`가 `hitFrameTimes`를 기다린 뒤 단일 writer를 호출한다.
2. **가설 B — 두 독립 경로가 같은 1.00초를 사용해 동시에 보인다:** 확정. clip marker와 `UnitStatsConfig.hitFrameTimes`가 모두 1.00초이며 로그의 exact flow도 이를 재현했다.
3. **가설 C — 네트워크 지연 때문에 우연히 겹쳤다:** 기각. Host에서 같은 exact ticket의 marker/VFX/TimerImpact가 반복적으로 같은 시각에 결합됐다.
4. **가설 D — VFX 재생 실패가 피해를 이르게 보이게 했다:** 기각. Host VFX started 1,124 / failure 0이고 유효 Client 표본도 started 31 / failure 0이다.

### 14.3 교정 기준

- `OnAttackHit @ 1.00초`와 source charge VFX 시작은 유지한다.
- 서버 피해 TimerImpact는 우선 기존 production baseline인 `2.25초`로 복원한다. 이는 VFX 시작 1.25초 뒤이며 2초 emission의 후반 구간이다.
- 2.25초는 이번 코드 교정의 baseline이지 최종 육안 튜닝 완료값이 아니다. 사용자가 새 빌드에서 VFX 후반부와 피해 숫자·HP 변화가 자연스럽게 일치하는지 확인한 뒤 확정한다.
- 영구 production gate는 더 이상 marker와 피해 offset의 동일성을 강제하지 않는다. `VFX marker=1.00초`, `TimerImpact=2.25초`, `marker < impact < attackCooldown(4.00초)`를 서로 다른 계약으로 검증한다.
- 실제 발사체는 여전히 없으므로 `MigrationRequired / Unresolved / LegacyFallback`을 유지한다.

### 14.4 진단기에서 별도로 발견된 결함

Client는 동시 미완료 flow 64개가 상한을 채운 뒤 starts 95 / complete 31 / incomplete 64 / overflow 1,041 / 단계별 unmatched 346으로 포화됐다. Host도 집계 자체는 유지했지만 정상 detail line이 5,168건 drop되어 terminal이 FAIL이었다. 이는 gameplay 실패가 아니라 대량 FoxMagician 경기에서 전용 진단의 bounded capacity와 detail 예산이 실제 동시성보다 작은 관측 결함이다.

이번 피해 타이밍 교정의 gameplay 원인과는 분리하되, 다음 실기에서 Host/Client 양쪽의 타임라인을 끝까지 판정하려면 진단 capacity·detail 정책도 회귀 없이 보강해야 한다. 진단 보강은 `gameplayWrites=0`을 유지하며 공격·피해·VFX 실행 경로를 변경하지 않는다.

### 14.5 구현 직전 추가 발견 — 생성 시 설정 덮어쓰기

`UnitFactory`의 서버 생산 경로와 Client 생성 경로는 Attack 클립에서 `OnAttackHit` 시각을 추출해 `UnitData.HitFrameTimes`를 덮어쓴다. FoxMagician의 클립에는 1.00초 이벤트가 있으므로 설정 에셋만 2.25초로 복원하면 실제 서버 피해는 계속 1.00초다. 이전 2.25초 설정도 런타임에서는 같은 덮어쓰기에 의해 무력화됐을 수 있다. 이 경로를 놓친 것이 앞선 조사·계획의 또 다른 오류다.

이번 교정은 양쪽 생성 경로에서 FoxMagician만 설정의 2.25초를 보존하고, 다른 유닛의 기존 클립 추출 방식은 유지해야 실제 피해 시점을 바꿀 수 있다. 영구 검증은 설정 값뿐 아니라 생산·Client 생성 경로에서 해당 덮어쓰기 예외가 살아 있는지 확인해야 한다.

## 13. bounded runtime diagnostic 구현 결과 — 2026-09-23

FoxMagician 전용 관측 공백을 닫기 위한 개발 진단이 구현됐다. 이 구현은 공격이나 VFX 동작을 바꾸지 않고 이미 발생한 production 사건을 같은 회차로 결합해 기록한다. 자동 검증은 통과했지만 이 진단이 들어간 빌드의 실제 경기 로그는 아직 없으므로, VFX 재생과 정확한 1.00초 정렬은 계속 미판정이다.

### 13.1 스키마와 bounded 상태

- 전용 스키마는 `foxmagician-production-timeline-v1`이다.
- `FoxMagician + LegacyFallback`만 관측하며 flow capacity는 64다.
- 한 flow는 attack start, `OnAttackHit` marker, VFX attempt, VFX result, 서버 TimerImpact를 보관한다.
- VFX result는 started/failure와 함께 `particlePositive`, `playbackActive`, `maxParticleSystems` 증거를 terminal에 집계한다.
- 완료된 가장 오래된 flow를 우선 retire하고, 미결합·중복·overflow·drop을 별도 집계한다.
- flow와 correlation ticket의 양방향 dictionary도 각각 64 상한을 공유한다. 완료 flow retire 시 양방향 binding을 함께 제거하고, TimerImpact가 flow와 결합되지 않은 경우에도 해당 ticket binding을 정리해 장기 경기의 진단 상태 누적을 막는다.

### 13.2 FIFO 없는 exact correlation

Legacy TimerImpact에는 presentation revision이 직접 전달되지 않으므로, 개발 진단 전용 correlation ticket을 공격 실행 경계에서 예약한다. 이 ticket은 `ExecuteAttack` coroutine에 전달되고, 같은 공격의 presentation revision이 발급되는 시점에 `(unitId, presentationRevision)` key와 Bind된다. 이후 서버 TimerImpact는 동일 ticket으로 정확한 flow를 찾는다.

따라서 TimerImpact를 “같은 공격자의 가장 오래된 미완료 회차”나 시간 근접, 공격자 FIFO로 추정 결합하지 않는다. ticket이 0이거나 양방향 binding, unit, target kind/id가 일치하지 않으면 unmatched로 남기고 다른 flow에 붙이지 않는다.

### 13.3 read-only 경계와 terminal

- 모든 세부 로그와 terminal은 `gameplayWrites=0`을 명시한다.
- observer는 공격 회차, 피해 승인·적용, HP, 타겟, 방향, 쿨다운, Animator, VFX 재생과 resolver를 쓰지 않는다.
- terminal은 starts, markers, vfxAttempts, vfxStarted/failures, particle/playback 증거, timerImpacts, complete/incomplete, 단계별 unmatched, duplicates, overflow, dropped와 최대 start→marker, start→TimerImpact, marker→TimerImpact 차이를 출력한다.
- expected impact offset은 `1.000초`로 고정 검증한다.

### 13.4 실제 수정 코드

- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

설정, resolver, 피해 writer의 의미, animation/controller, Blue/Red prefab, VFX asset과 projectile/tracer 지원 상태는 변경하지 않았다.

### 13.5 자동 검증과 현재 판정

- Runtime/Editor Unity recompile: C# `CS` 오류 0건.
- `Tools/check_docs.py`: 문제 0건.
- Unity 메뉴 `Hexiege → Combat → Run Unit Action Self Validation`: PASS.
- Unity 메뉴 `Hexiege → Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit`: PASS.
- Unity Console: error 0건 / warning 1건.

현재는 후속 Android Build And Run을 시작하기 전이다. 실제 경기에서 `foxmagician-production-timeline-v1` Host/Client terminal과 세부 flow를 수집하지 않았으므로 이 Task는 Complete/PASS가 아니다. resolver는 `MigrationRequired / Unresolved / LegacyFallback`을 유지하고, 실제 VFX started/playback 및 1.00초 marker↔TimerImpact 정렬은 새 로그로만 판정한다.

## 16. 피해 시점 재분리 구현 및 검증 — 2026-09-27

- `UnitStatsConfig`의 FoxMagician 단일 피해 시각을 `2.25초`로 되돌렸다. Attack clip의 유일한 `OnAttackHit @ 1.00초`는 charge VFX 시작으로 유지했다.
- `UnitFactory`의 서버 생산·Client 생성 양쪽에서 FoxMagician만 clip marker가 설정 피해 시각을 덮어쓰지 않게 했다. 다른 유닛의 marker 추출 계약은 유지했다.
- 진단 expected impact를 2.25초로 바꾸고 대량 경기 flow capacity를 256으로 높였다. 정상 detail은 64줄로 제한하되 집계는 유지하고, 8초 넘은 미완료 flow만 만료/FAIL로 별도 표시한다. observer는 `gameplayWrites=0`인 읽기 전용 상태다.
- 첫 Unity 자체 검증은 Fox terminal 최악값 한 줄이 정확히 1,000 UTF-8바이트라서 FAIL했다. 중복 필드와 필드명을 줄여 995바이트로 교정한 뒤 재실행했다.
- 최종 Unity `Run Unit Action Self Validation` PASS, `Self Validate Unit Root Pose Cross Audit` PASS, Console error 0건. `Tools/check_docs.py` 문제 0건.
- 이는 에셋/생성 경로/진단 계약의 자동 검증이다. 실제 Android 경기의 HP 감소·피해 숫자가 VFX 후반부에 보이는지는 아직 사용자 실기로 확인해야 한다. 이 Task는 실기 판정 전 OPEN이며 projectile/tracer는 여전히 `MigrationRequired / Unresolved / LegacyFallback`이다.
- 두 PASS 후 Android `Build And Run`을 실행했지만 Unity가 `Unable to sign the application; please provide passwords!`로 차단했다. 사용자 키스토어 비밀번호 입력 전에는 빌드가 시작된 것으로 간주하지 않는다.

## 18. 2026-09-27 후속 실기 로그 정정 — 동일 경기 Host/Client

앞선 §15~16의 「실제 경기 로그 없음」과 서명 차단은 **당시 상태**로 보존한다. 이후 확보한 동일 `sharedSessionKey=67d84b7da7a97b206fe075c2e654380618b2be790896c23f4f164865a2c1d6a1`의 Editor Host (`_Logs/_editor/2026-09-27/RuntimeLog.txt`)와 Android Client (`_Logs/2026-09-27/04_34_logcat/RuntimeLog_device.txt`)를 `foxmagician-production-timeline-v1` 종료 행 및 Host 상세 행으로 재확인했다. 이전 2026-09-23 Host 종료 행은 `maxMarkerTimerDelta=0.201530`, `expectedImpactOffset=1.000`으로 marker와 피해가 근접하던 **옛 빌드** 증거이며 아래 새 경기 수치로 덮어쓰지 않는다.

| 종료 집계 | Editor Host | Android Client |
|---|---:|---:|
| starts / markers / vfxStarted | 250 / 246 / 246 | 250 / 217 / 217 |
| vfxFailures / particlePositive / playbackActive | 0 / 246 / 246 | 0 / 217 / 217 |
| timerImpacts / complete / incomplete | 245 / 242 / 8 | 0 / 217 / 33 |
| unmatched(각 단계) / duplicates / overflow / dropped / expired | 모두 0 | 모두 0 |
| cappedDetails / expectedImpactOffset | 1,169 / 2.250초 | 837 / 2.250초 |

Host 최대 `start→marker=0.998558초`, `start→timer=2.243118초`, `marker→timer=1.743038초`다. Client `timerImpacts=0`은 서버 TimerImpact를 Client가 직접 기록하지 않는 역할상의 예상값이며 피해 누락 수로 읽지 않는다. 양쪽 `verdict=EVIDENCE`, `gameplayWrites=0`이다.

Host의 **상세 기록에 남은 동일 unitId+revision의 marker·timer 10회차**를 `serverTime(timer-impact) - serverTime(marker)`로 재계산하면 `1.239891~1.278933초`, 평균 `1.255681초`다. 그중 실제 writer `Applied` 6회차는 `1.239891~1.259609초`, 평균 `1.250256초`이고 나머지 4회차는 `TargetUnavailable`이다. 상세 줄은 64개 상한으로 이후 다수가 생략(`cappedDetails=1,169`)됐으므로 **이 10회차를 전체 245 TimerImpact의 분포나 전 회차 완료율로 일반화하지 않는다**. 종료 집계의 Host `incomplete=8`, Client `incomplete=33`은 종료 시 미완료 회차로만 기록하며 정상 취소와 관측 누락을 분류할 자료는 없다.

**판정:** 새 경기 로그는 Fox charge VFX가 양쪽에서 실제 시작하고 ParticleSystem/재생 활성 상태를 반환했으며, 관측 가능한 Host 상세 회차에서 marker와 서버 TimerImpact가 약 1.25초 분리된 것을 지지한다. 이는 **시점 분리의 로그 증거**이지 VFX의 **시각적 종료 시각** 또는 HP 숫자의 화면상 일치를 측정한 증거가 아니다. 사용자가 Fox 타이밍 육안 테스트를 별도로 진행해 알릴 예정이므로 Task는 **OPEN / 최종 PASS 아님**이다. 서버 권위 projectile·tracer·발사/착탄 exact-key 이관도 여전히 미완료이며 `MigrationRequired / Unresolved / LegacyFallback`을 유지한다.

## 19. 17_21 경기의 피해 표시 지연 — 2026-09-27 추가 정정

**후속 검증 범위 인계:** 구현 담당자가 추가 중인 real writer fixture는 Fox/Dust/Lion × scoped/unscoped × unit/building의 HP 1회 적용·이벤트 immediate/key 보존을 대상으로 한다. marker→queue→text 전체 runtime 통과 증거로 간주하지 않는다. 후속 bounded/unscoped 진단은 큐 receive/emit과 spawner 생성 성공 bool 범위이며 **VFX 종료는 미계측, 공격별 exact correlation은 불가**다. 새 종료 timestamp나 동일 공격의 VFX 종료→숫자 지연 실측이 생겼다고 보고하지 않는다. 구현/실행 결과는 별도로 확인하며 실기 OPEN을 유지한다.

사용자는 VFX가 끝난 뒤 1~2초 늦게 대미지가 보인다고 보고했다. 서버 피해 예약만 복원해도 피해 숫자의 표시 대기가 남을 수 있으므로, 이번 후속 교정은 두 책임을 구분한다.

- 원본: `Assets/_Project/Docs/_Logs/2026-09-27/17_21_logcat/RuntimeLog_device.txt`와 `Assets/_Project/Docs/_Logs/_editor/2026-09-27/RuntimeLog.txt`.
- 동일 경기 키: `79c8f7ebefae1b5a0568999311c0822978d176adacfa9599b546bc3ac6ca9dec`. 이번에는 **Android Host / Editor Client**이며 이전 04_34 경기와 역할이 반대다.
- Fox 종료 집계는 양쪽 starts/markers/vfxAttempts/vfxStarted/particlePositive/playbackActive/complete 각 17, incomplete·VFX 실패·단계별 unmatched·duplicates·overflow·dropped·expired 모두 0이다. Host timerImpacts=17, Client=0(서버 전용 관측)이다.
- Host maxMarkerTimerDelta=1.271813초, maxStartTimer=2.271737초, expectedImpactOffset=2.250초. 상세 상한으로 cappedDetails는 Host 21 / Client 4이며, VFX 실제 종료와 피해 숫자 실제 Emit 시각은 현행 Fox 진단에 없다. 따라서 VFX 종료→숫자 표시 1~2초는 사용자 관찰이지 로그로 측정한 구간이 아니다.
- 실제 생산 기록은 FoxMagician과 BoulderSpirit이다. manifest의 DustSpirit 등장은 실전 사용 증거가 아니며 **DustSpirit 실기 판정은 미확인/OPEN**이다.

**[🔴 2026-09-27 correction — original kept: Plan §16.4의 exact rendezvous 보호 가정 정정]** `HitPresentationQueue.OnEntityDamaged`는 유효한 PresentationResultKey만 exact rendezvous로 처리한다. unscoped Legacy 결과는 공격자 FIFO에 들어가고, `OnLocalAttackHit`의 unscoped 신호는 빈 큐에서 보존되지 않는다. 따라서 marker가 피해보다 먼저인 Fox Legacy 경로를 exact rendezvous가 보호한다고 일반화한 설명은 잘못됐다. 진단용 exact ticket은 gameplay 결과 키 또는 실제 표시의 exact 결합을 구현한 것이 아니다.

marker 1.00초에 빈 신호가 소진되고 피해 2.25초가 이후 적재되면 다음 루프 marker 또는 중단/timeout 방출까지 표시가 늦어질 수 있다. 4초 주기의 다음 marker는 약 5초이고, 2초 효과가 약 3초에 끝난다고 가정하면 약 2초 지연이다. 이는 **설정·코드로 설명한 가능 경로이며 화면 종료/Emit 실측값이 아니다**. 공통 Supported 결과·표현 집계의 오류 0도 Fox Unresolved 숫자 표시의 정상 근거로 전용하지 않는다.

교정은 Fox의 **unscoped 확정 결과 표현만 즉시 방출**해 다음 공격 marker 대기를 없애는 범위다. 기존 Supported 소유권과 유효 exact key 경로를 우회하지 않고, 다른 Legacy 유닛 FIFO를 일괄 변경하지 않는다. 실제 HP writer·피해 2.25초·VFX marker 1.00초·쿨다운 4초는 유지한다. 구현 담당자가 실제 분기와 결과 전달 경로를 확인한 뒤 코드 결과를 인계해야 구현 완료로 갱신한다. 현재는 문서 반영만 완료하며 실기 OPEN, projectile/tracer 보류 및 MigrationRequired / Unresolved / LegacyFallback 유지다.

## 20. 2026-09-28 구현·자동 검증 완료 및 실기 인계

**2026-09-28 최신 상태 — 구현·자동 검증 PASS / 실기 OPEN:** Fox unscoped 주 타깃 피해 writer가 기존 immediatePresentation을 지정하고 이벤트·RPC·큐의 기존 즉시 표시 경로를 재사용하도록 구현했다. 피해 2.25초/VFX 시작 1.00초는 유지한다. 메인 세션 직접 실행·Editor.log 확인 인계 기준, `Run Unit Action Self Validation` 02:46:05 PASS(새 `ValidateFoxMagicianPrimaryPresentation` 포함), `Self Validate Unit Root Pose Cross Audit` 02:49:34 PASS. Runtime/Editor 정적 컴파일 오류 0, Console error 0 / warning 20. 이번 교정 빌드는 미시도이며 사용자 Fox+DustSpirit 재테스트 대기다. 진단은 인스턴스당 최대 128줄의 unscoped receive/emit·TryShowDamage 성공만 기록하고 공격자 부재는 생략한다. **VFX 종료·공격별 exact correlation은 미계측이며 marker→queue→text 전체 실동작 PASS가 아니다.**

- 구현 파일: `UnitCombatUseCase.cs`, `HitPresentationQueue.cs`, `FloatingHpTextSpawner.cs`, `RunUnitActionSelfValidation.cs`(구현 담당자 완료 인계).
- real writer fixture 12조합: Fox/Dust/Lion × scoped/unscoped × unit/building. HP 1회 적용·이벤트 immediate/key 보존 검증이며 실제 marker→queue→text 통합 실행 검증으로 확대하지 않는다.
- 이전 §19의 구현/자동 검증 대기 기록은 당시 이력으로 보존하고 이 절이 최신 상태다. 신규 실기 결과와 VFX 종료 타임스탬프는 아직 없다. DustSpirit은 이전 경기에서 생산되지 않아 실전 정상 판정 불가다.
- 증거 출처: 메뉴 시각·Console 수치는 메인 세션 직접 관측 인계, 정적 컴파일은 구현 담당자 인계다. 문서 담당자의 Editor.log 직접 읽기는 접근 거부로 재확인하지 못했다.

## 21. 2026-09-28 사용자 재테스트 결과 반영

원본은 `Assets/_Project/Docs/_Logs/2026-09-28/03_50_logcat/RuntimeLog_device.txt`와 `Assets/_Project/Docs/_Logs/_editor/2026-09-28/RuntimeLog.txt`다. Editor Host / Android Client 동일 `sharedSessionKey=be8b171a5d2d58c113c4702b8ea5d8d054bf5855322f2ad45c097796bd94d4f5`의 **두 경기**를 runId/startedAt으로 분리했다. `03_41_logcat`은 겹치는 gameplay 캡처이므로 합산하지 않았다. Host attack runId는 `0e16018819d949288f14f378c63f6d83` / `bebba109c44540b7bf81caa3a49c4958`, Client는 `fc947d78f77d4d27bb90a2bcf0462472` / `74702837e689471c960b66edb37c34ab`이다.

**Fox 표시 지연 focused 교정: 사용자 수용 / 현행 타이밍 유지.** 양 peer에서 received/emitted 61쌍씩이 모두 같은 frame, immediate=True, dispatch=text-played다. localTime 차이의 최대는 Host 7.097ms / Client 2.445ms, 평균은 1.07182ms / 1.71633ms다. 이는 큐 수신→텍스트 생성 호출 성공 구간이며 VFX 종료→동일 공격 피해 표시의 exact 측정이 아니다. VFX 실제 시작은 Host 14+65=79, Client 14+64=78, 실패 0이다. 미완료는 Host 1/Client 1이고 원인은 미확정이다. 종료 disconnect 경고를 그 원인으로 단정하지 않는다. 사용자는 약간 이른 느낌은 있지만 현재 Fox 타이밍을 명시적으로 수용했으며 재튜닝은 향후 발사체 구현 때로 미룬다. VFX 1.00초/피해 2.25초를 유지한다. 이번 표시 지연 focused 범위는 수용됐으나 완벽한 화면 일치, 권위 projectile/tracer, 전체 migration·25종·역할교대·rollback 완료로 확대하지 않는다. MigrationRequired / Unresolved / LegacyFallback 유지.

**DustSpirit: focused runtime evidence 확인 / CONDITIONAL PASS·OPEN.** 실제 생산 Dust 49기/Fox 10기이며 이번 실제 생산 타입 중 Supported는 Dust뿐이다. 서버 결과 84+166=250과 Client 수락 250이 일치하고 각 peer 필수 표현은 83+153=236/236, 결과·표현 실패/중복/공간 mismatch/recovery 0이다. Host 이동 52,714+94,799=147,513 frame에서 gate/handoff/stationaryWalk/errors 0이며 이 이동 집계는 경기 전체이지 Dust 전용 집계는 아니다. 각 경기 양 peer local ROOT PASS지만 공식 CrossAudit 실행 결과는 아니다. 초기화 지연 59건은 59건 완료, retry failure 0이다. 게임 로그 ERROR/FATAL/실제 Exception 발생 없음(스택의 Exception 매개변수 문자열은 제외). Dust 공격 접촉·대상 전환 등에 대한 명시적 사용자 육안 수용은 없어 Complete/최종 PASS로 닫지 않는다. 이전 17_21 경기의 Dust 부재 판단은 당시 사실로 보존하며 이번 두 경기에서 해소됐다.

이번 변경은 승인된 결과 문서 반영만이며 코드·TC·새 Task·빌드 실행은 하지 않았다. 다음 유닛은 별도 조사·선정 대상으로 남기며 이 문서 갱신이 구현 착수 승인은 아니다.
