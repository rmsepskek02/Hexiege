# BoulderSpirit 공격 시점 조사 — Research

> **최신 상태(2026-09-29, 1:10 사용자 육안 수용 후):** 30fps `1:10` = **40/30초 ≈ 1.3333333초**의 생산 marker·type 14 설정·영구 gate 저장과 Unity 두 self-validation PASS 뒤, Android Host/Editor Client 새 경기에서 BoulderSpirit 7기 생산 및 혼합 2종 공격 결과/표현 수렴을 확인했다. 사용자는 새 1:10 타격이 **“육안상 문제없었어”**라고 확인했다. 따라서 BoulderSpirit 공격 시각의 focused 판정은 **PASS/CLOSED**다. 정확한 BoulderSpirit 단독 marker→피해 시차는 미계측이고, 이전 1.50초 결과는 이력이다. LittleKnight 이동 결함과 전체 migration gate는 별도 OPEN이다.

BoulderSpirit의 공격이 어느 애니메이션을 실제로 쓰고 언제 피해를 주도록 저장돼 있는지 확인한다. 문서의 `1:15`와 에셋의 `1.15초`는 같은 표기인지 알 수 없으므로, 먼저 저장된 생산 경로를 비교한다. 이 문서는 2026-09-29 03:13 기준 정적 조사이며 실제 경기의 화면·피해 시점 검증은 아니다.

## 현재 저장값과 생산 경로

| 대상 | 직접 확인한 내용 | 출처 |
|---|---|---|
| Attack 클립 | `BoulderSpirit_Attack.anim`(GUID `a08b07c6564fb5741ace267fd675b087`)은 30fps, stop time 4초, loop 활성. `OnAttackHit`은 **1개, time `1.15`초**다. 30fps의 정수 프레임으로 환산하면 34.5프레임이므로 `1:15` 프레임 표기와 동일하다고 볼 수 없다. | `Assets/_Project/Animations/Units/BoulderSpirit/BoulderSpirit_Attack.anim`의 `m_SampleRate`, `m_AnimationClipSettings`, `m_Events`; 같은 경로 `.anim.meta` |
| Controller | `BoulderSpirit.controller`(GUID `834665be97ae7284b87a3eb1eae7d3a2`)의 `Base Layer/Attack` state는 speed 1, motion이 위 Attack 클립 GUID다. | `Assets/_Project/Animations/Units/BoulderSpirit/BoulderSpirit.controller`의 `Base Layer`와 `AnimatorState Attack`; `.controller.meta` |
| 양 팀 prefab | Blue/Red 모두 위 controller GUID를 Animator에 연결하고 `m_ApplyRootMotion: 0`; Animator와 같은 객체에 `AnimationEventRelay`가 있다. | `Assets/_Project/Prefabs/Units/Spirit/Unit_BoulderSpirit_{Blue,Red}.prefab`의 Animator/Relay; 두 `.prefab.meta` |
| Game 씬 생산 등록 | `UnitFactory._spiritPrefabs`의 **type 14**는 Blue GUID `ed8f7c54de615284ead6cca046630693`, Red GUID `37aeff26a5cd70148bb93cb5b56c30fe`를 참조한다. 이 행에 `attackTimelineClip` 명시 참조는 없다. 씬의 다른 type 14 행은 `_spiritPrefabs` 밖이므로 종족 배열을 구분해야 한다. | `Assets/_Project/Scenes/Game.unity`의 `_spiritPrefabs` type 14 행(41719~41721), 각 prefab `.meta` |
| 설정 폴백 | `UnitStatsConfig.asset`의 `unitType: 14`는 `attackCooldown: 4`, `hitFrameTimes: [1.15]`다. 현재 Attack 클립의 길이·이벤트와 숫자가 일치한다. | `Assets/_Project/Resources/Config/UnitStatsConfig.asset`의 type 14 블록(190~198) |
| 실제 추출 방식 | `UnitFactory`는 서버/싱글 생성과 Client 초기화 양쪽에서 선택한 클립 길이를 공격 주기로, `OnAttackHit` 시간을 타격 offset으로 읽는다. 명시 clip이 없으면 controller의 `animationClips` 중 이름에 `Attack`이 들어간 **첫 클립**을 쓴다. 따라서 지금의 연결은 실제 Attack state와 일치할 수 있어도 state 자체를 확인하는 선택 계약은 아니다. | `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs`의 생성·Client 초기화, `GetAttackTimelineClip`·`SelectAttackTimelineClip`·`GetHitFrameTimes` |
| 프로필·자동 gate | Resolver는 BoulderSpirit을 `Supported / MeleeContact / Impact 1 / secondary false`로 분류한다. 기존 `RunUnitActionSelfValidation`의 production timeline 목록에는 BoulderSpirit 전용 scene/controller/prefab/marker/config exact gate가 없다. 범용 검증의 PASS를 이 연결의 증거로 바꿀 수 없다. | `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs`의 BoulderSpirit case; `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`의 등록 목록 |
| VFX 에셋 | `UnitEffectConfig.asset` type 14는 `attackPreset`, `hitPreset`, `tracerPreset` 모두 null이고 공용 `deathPreset`만 참조한다. `VFXSFXList.md`의 `vfx_boulderspirit_attack.prefab`/SFX 항목은 목록에 있으나, 해당 이름의 실제 공격 VFX/SFX 파일은 현재 에셋 경로 검색에서 찾지 못했다. 사망 프리셋은 공격·피격 VFX의 대체가 아니다. | `Assets/_Project/Resources/Config/UnitEffectConfig.asset` type 14(76~80); `Assets/_Project/Docs/Assets/VFXSFXList.md`; `Assets/_Project/Prefabs/VFX/Units/` 및 EffectPresets 파일 목록 |

## 문서 표기와 판단 경계

- `StatsReference.md` BoulderSpirit 행은 `1:15(4:00)`로 표기한다. 이것이 **초:프레임(30fps)**이라면 공격 시각은 `1 + 15/30 = 1.50초`; 저장된 Animation Event·설정의 `1.15초`와 **0.35초 차이**다. 반대로 십진 초 `1.15`를 콜론으로 적은 것이라면 숫자는 일치한다. 표기 의도는 문서만으로 확정할 수 없다. `UnitCombatAssetMatrix.md`는 `1 @ 1.15s / 동일`이라고 기록한다.
- `UnitStatsConfig.asset`의 type 14 `maxHp: 800`과 `StatsReference.md`의 BoulderSpirit HP 900도 별도 문서/설정 불일치다. 이번 공격 시점 결정의 근거로 삼지 않고, 스탯 변경은 범위 밖으로 둔다.
- 2026-09-20 유닛 공격 교정 실패 기록에는 InfernoSpirit 교정 스크립트가 enum 선언 인덱스를 잘못 써서 BoulderSpirit의 offset을 일시적으로 0.50초로 바꿨다가, 실제 직렬화 `unitType: 14`를 확인해 **1.15초로 복구**한 사건이 있다. 현재 값은 이 복구 결과와 일치한다. 검증 코드는 `enumValueIndex`가 아니라 실제 값으로 행을 찾아야 한다. 출처: `.claude/mistakes/unit-action-correction.md`의 2026-09-20 사건.

현재 저장된 clip/config 1.15초를 임의로 1.50초로 바꿀 근거는 없다. production clip을 명시 참조하고 실제 Attack state·양 팀 prefab·설정값을 한 번에 읽는 자동 gate가 없는 것이 확인된 계약상 빈틈이다. 이는 현재 클립 오선택이나 피해 타이밍 버그가 **실기로 관측됐다**는 뜻이 아니다. 공격 접촉 프레임, 서버 결과와 Host/Client 표현의 상관관계, 반복 회차·사망/Stop 처리, VFX의 시각적 필요성은 미확인이다. `MigrationRequired` 및 전체 25종·역할교대·rollback 미완 상태를 유지한다.

## 2026-09-29 사용자 결정 후 정정

**[🔴 2026-09-29 수치 정정 — 위 결정 전 기록 보존:]** 사용자는 `1:15(4:00)`을 30fps의 초:프레임 표기로 확정했다. `1:15 = 1 + 15/30 = 1.50초`이고 `4:00 = 4초`다. 따라서 현재 저장된 `BoulderSpirit_Attack.anim`의 단일 `OnAttackHit @ 1.15초`와 `UnitStatsConfig.asset` type 14의 `hitFrameTimes=[1.15]`는 **서로는 일치하지만 확정된 의도 1.50초보다 0.35초 이르다**. 현재 저장값이 잘못됐으며 두 값을 1.50초로 함께 교정해야 한다. 출처는 `StatsReference.md` BoulderSpirit 행, 위 정적 파일 대조 및 이번 사용자 결정이다.

이 결정은 BoulderSpirit의 타격 시각에만 적용된다. 다른 유닛의 `분:프레임` 표기가 같은 방식으로 잘못 저장됐는지는 **별도 감사**가 필요하며 이번 작업에서 일괄 변경하지 않는다. 생산 clip 명시 연결과 실제 scene/prefab/controller/clip/config를 함께 확인하는 gate 필요성은 그대로다. 이 정정은 **의도와 저장값의 불일치 확정**이지, 교정 빌드의 실기 타격 시점·멀티플레이 PASS는 아니다.

## 2026-09-29 실기 후 상태

같은 `sharedSessionKey=06d034abf18c778a02cf56c57c1b8cdf81d9b8f46a74aba5e1a4356161e1a6c4`의 Editor Host 12:43~12:45와 Android Client 로그를 대조했다. 현재 저장 에셋은 Attack 클립 단일 `OnAttackHit @ 1.50초`, type 14 설정 `[1.5]`·쿨다운 4초, BoulderSpirit production gate의 1.50초 기대값이다. 위 1.15초 표는 **03:13 교정 전 정적 스냅샷**이지 현재값이 아니다. 출처: `Assets/_Project/Animations/Units/BoulderSpirit/BoulderSpirit_Attack.anim`, `Assets/_Project/Resources/Config/UnitStatsConfig.asset`, `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`의 BoulderSpirit gate.

- Host 로그에 BoulderSpirit UnitId 2 생산, 공격 시작 gate Ready와 shadow-commit Accepted가 있다. Host 결과 **453/실패 0**, Client **453 수락/거부 0**, 양쪽 필수 표현 **414/414·실패 0**이다. 두 peer ROOT는 각각 **local PASS/errors 0**이다. 출처: `Assets/_Project/Docs/_Logs/_editor/2026-09-29/RuntimeLog.txt`의 12:43~12:45 production/start/END, `Assets/_Project/Docs/_Logs/2026-09-29/12_47_logcat/RuntimeLog_device.txt`의 동일 key END.
- **453과 414는 BoulderSpirit+LittleKnight 경기 전체 집계**다. BoulderSpirit 개별 공격 회차의 정확한 1.50초 marker→권위 피해 또는 화면 접촉 subframe은 이 로그로 증명되지 않는다. 사용자는 BoulderSpirit이 육안상 **약간 늦지만 수용 가능**하다고 평가했다. FPS/frame-time은 계측되지 않았고 지연의 원인도 확정하지 않는다.
- 별도 문제: Host `UAS-MOVE-AUTH`는 `adapterFailures=4`로 **FAIL**했다. 실패 UnitId **16/19/21/26은 같은 Host 생산 로그에서 모두 LittleKnight**이며, `post-combat-no-safe-forward-center`, `RejectedInvalidPath`, `Unreachable`, `(5,15)→(6,15)`이다. BoulderSpirit 공격 실패로 분류하지 않지만 **경기 전체 이동 gate가 통과했다**고도 적지 않는다. Client MOVE 수치 0 및 양쪽 local ROOT PASS는 Host MOVE-AUTH FAIL을 지우지 못한다. 이것이 사용자가 본 약간의 타격 지연 원인이라는 증거도 없다. 출처: 위 Host 로그의 production, 12:45:21~23 movement-adapter-failure 및 MOVE terminal.

**판정:** BoulderSpirit 1.50초 생산 설정·focused 화면 수용과 이번 경기의 공격 결과/표현 수렴은 확인됐다. 정확한 단독 타격 시각, 공식 cross-audit, 25종 전체·역할교대·Legacy rollback 및 LittleKnight 이동 결함은 OPEN이다. 전체 세션을 무오류 PASS 또는 규칙 v2 Complete로 표기하지 않는다.

## 2026-09-29 후속 요청 — 1:10 목표와 별도 이동 FAIL의 직접 경로

사용자는 앞서 약간 늦지만 수용 가능하다고 평가했던 **1:15 버전**을 다시 조정해 BoulderSpirit의 단일 공격 이벤트를 **1:10**으로 옮겨 구현해 보자고 요청했다. 이는 이전 시험이 실패했다는 판정이 아니라 새 의도다. 30fps의 초:프레임 표기에서 `1:10 = 1 + 10/30 = 40/30초 ≈ 1.3333333초`; 현행 `1:15 = 45/30 = 1.5초`보다 **5프레임(1/6초) 이르다**. 화면 지연량을 5프레임으로 측정했다는 뜻은 아니다. `4:00` 주기·클립 길이는 그대로 둔다.

**직접 확인한 현재값(새 구현 전):** `Assets/_Project/Animations/Units/BoulderSpirit/BoulderSpirit_Attack.anim`은 30fps·4초이며 단일 `OnAttackHit`의 저장 time은 `1.5`; `Assets/_Project/Resources/Config/UnitStatsConfig.asset`의 실제 `unitType: 14`는 `attackCooldown: 4`, `hitFrameTimes: [1.5]`; `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`의 BoulderSpirit production gate도 `1.5f`를 기대한다. `Assets/_Project/Docs/StatsReference.md` BoulderSpirit 행과 매트릭스는 현재 1:15/1.50초 버전을 기술한다. 이번 문서 편집에서 이 파일들은 변경하지 않는다. 기존 Game 씬의 명시 clip 연결·양 팀 prefab/controller는 이번 재조정 대상이 아니다. 새 1:10 빌드와 사용자 화면 결과는 아직 없다.

**별도 LittleKnight 이동 FAIL — 확인 가능한 인과 사슬:** 같은 12:43~12:45 Host 로그에서 UnitId 16/19/21/26은 생산 기록상 모두 LittleKnight다. 네 건은 `source=post-combat-no-safe-forward-center`, `authoritativeStart=(5,15)`, `goal=(6,15)`, `pathStatus=Unreachable`, `candidatePathStart=unavailable`, `decision=RejectedInvalidPath`로 기록됐고 Host MOVE-AUTH terminal은 `adapterFailures=4`, `verdict=FAIL`이다. `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`의 post-combat 분기는 `UnitMovementUseCase.TryFindReachableForwardRejoinTile`이 false일 때 `RequestLatestAuthoritativeRepath(finalTarget)`로 돌아간다. 이 재탐색이 성공하지 않으면 null 경로가 되고, `UnitRepathProgressGuard.Evaluate`가 유효 경로 서명을 만들지 못해 `RejectedInvalidPath`를 돌려준다. `UnitView.TryAcceptRepathDecision`은 이 경우 objective를 `MarkBlocked()`하고 fail-closed 로그를 남긴다. 전방 후보는 `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs`의 인접·walkable·목표 방향·직접 경로 조건을 통과해야 한다. **그러나 어떤 타일/장애물/상태 때문에 이번 후보와 (5,15)→(6,15) 경로가 막혔는지, 반복 재현되는지는 이 저장 로그만으로 확정되지 않는다.** 이는 BoulderSpirit 타격 문제와 별개이며 사용자가 본 지연의 원인이라는 FPS/frame-time 증거도 없다. 이 Task에서 이동 코드를 고치지 않는다.

## 2026-09-29 1:10 구현 저장 확인 — Unity·실기 대기

이전 절의 “새 구현 전” 1.50초와 “아직 적용되지 않음”은 문서 작성 당시의 스냅샷이다. 현재 저장 파일을 다시 읽어 `BoulderSpirit_Attack.anim`의 **30fps·4초 클립에 단일 `OnAttackHit @ 1.3333333초`**, `UnitStatsConfig.asset` 실제 직렬화 **`unitType: 14`의 `attackCooldown: 4`, `hitFrameTimes: [1.3333333]`**, `RunUnitActionSelfValidation.cs` BoulderSpirit gate의 marker/config **`1.3333333f` 기대값(40/30초)**을 확인했다. `StatsReference.md`의 BoulderSpirit 행도 `1:10(4:00)`으로 동기화했다. 기존 scene 명시 연결·양 팀 prefab/controller·다른 유닛을 이번 교정 대상으로 삼지 않았다. 출처: 위 세 저장 파일의 BoulderSpirit event/type 14/gate 블록과 `Assets/_Project/Docs/StatsReference.md` BoulderSpirit 행.

이는 **파일에 새 값이 저장됐다는 정적 확인**이다. 두 Unity self-validation, 빌드, 새 Editor Host/Android Client 경기와 사용자 화면 수용은 아직 결과가 없다. 이전 세션 `06d034ab…e1a6c4`의 453 결과·414/414 필수 표현과 “약간 늦지만 수용 가능”은 **1.50초 버전**에만 귀속한다. 그 경기의 LittleKnight MOVE-AUTH 4건 FAIL과 원인 미확정 경계도 그대로 남긴다.

## 2026-09-29 1:10 Unity 검증·빌드 시작 관찰

위 ‘Unity·실기 대기’는 저장 직후의 상태다. 메인 세션 관찰에 따르면 Unity의 처음 두 Unit Action 실행은 **Assets Refresh/domain reload 전의 낡은 1.5초 검증 메시지로 실패**했다. Refresh·재컴파일 뒤 새 도메인에서 약 13:21 `Hexiege/Combat/Run Unit Action Self Validation`이 PASS했고, 약 13:22 `Hexiege/Combat/Diagnostics/Self Validate Unit Root Pose Cross Audit`도 PASS했다. 초기 실패를 현재 저장된 1:10 에셋의 최종 검증 실패로 해석하지 않으며, 재검증 PASS가 초기 실패 이력을 지우지도 않는다. 이 절은 메인 세션의 Unity 관찰 전달값이며 이 문서 작업에서 Unity를 재실행하지 않았다.

Refresh 도중 Docs 에셋 관련 일시적인 `[Worker1] Import Error Code(4)` 경고가 있었다. domain reload 뒤 **새로 관찰된 Console 오류는 없었지만**, Console 이력에는 이전 오류 한 건이 남아 있다. 따라서 전역 `Console Error 0`이나 경고 0으로 기록하지 않는다. `File > Build And Run`을 눌렀고 화면에는 `Checking prerequisites / Starting Android build`가 표시됐다. **빌드 시작만 확인**했으며 완료·설치·실기 성공 여부는 감시하거나 확인하지 않았다.

새 **1:10 버전**의 Editor Host/Android Client 공격 결과·화면 타이밍은 사용자 확인 대기다. 위 `06d034ab…e1a6c4` 경기의 사용자 수용·453/414 집계는 여전히 **이전 1.50초 버전**에만 해당한다. LittleKnight Host MOVE-AUTH 4건 FAIL도 별도 OPEN이며 이번 Unity 메뉴 PASS로 해결됐다고 판단하지 않는다.

## 2026-09-29 1:10 후속 경기 — Android Host / Editor Client

새 `sharedSessionKey=0f0bbb3247a104cc839894f0c181ca05f7ba6960a415ba3be12f0662d923eb60`의 Android **PID 8899 Host**와 Editor Client 로그를 대조했다. Host 생산 로그의 BoulderSpirit은 **7기**이고 LittleKnight와 함께 나온 **2/25종** 경기다. 출처: `Assets/_Project/Docs/_Logs/2026-09-29/18_13_logcat/RuntimeLog_device.txt`의 PID 8899 생산·END, `Assets/_Project/Docs/_Logs/_editor/2026-09-29/RuntimeLog.txt`의 18시대 동일 key END.

- 공격 결과: Host `serverResults=283`, `resultFailures=0`, `ready=283`, `commits=168`; Client `clientResultAccepted=283`, `clientResultRejected=0`, `ready=283`. 양쪽 필수 표현은 `expectedVisual=265`, `presentationEmits=265`, 실패 0이며 표현 공간 mismatch 0이다. **283/265/168은 BoulderSpirit 단독이 아닌 혼합 경기의 집계**다.
- Host MOVE-AUTH `adapterFailures=0`, terminal core의 gate/writer/handoff 등 실패 0이다. 양쪽 `UAS-ROOT-POSE`는 각각 **local PASS/errors 0**이다. 이전 `06d034ab…e1a6c4` 경기의 LittleKnight adapterFailures 4건은 이번 경기에서 재발하지 않았지만, 원인 규명·수정·동일 조건 재현이 없어 그 결함은 **OPEN**으로 둔다. local ROOT PASS를 공식 cross-audit PASS로 승격하지 않는다.
- 새 경기의 Android PID 8899 Unity `E/F`는 0건, Editor의 해당 18시대 `ERROR/FATAL/Exception`은 0건으로 확인했다. StreamSpirit/Fox 전용 timeline `INCONCLUSIVE`는 해당 타입의 starts 0인 미생산 범위라 이번 BoulderSpirit 판정과 무관하다.

이는 **1:10 변경 후 경기의 집계 수렴** 근거이지 BoulderSpirit 개별 `OnAttackHit`→권위 Impact의 정확한 `1.3333333초` 차이나 화면 체감 시점의 증명이 아니다. 새 1:10에 대한 사용자 육안 평가는 아직 없으며, 1.50초 버전의 ‘약간 늦지만 수용 가능’을 새 버전에 이월하지 않는다. 빌드 완료 여부도 이 로그만으로 별도 확정하지 않는다. 25종 전체·역할교대·Legacy rollback/v2 Complete는 미완이다.

## 2026-09-29 1:10 사용자 화면 확인 — focused 종료

위 ‘육안 평가 대기’는 로그 분석 당시 상태다. 사용자가 **“BoulderSpirit 1:10 타격 육안상 문제없었어”**라고 새 버전을 직접 수용했다. 앞 절의 Android Host/Editor Client 동일 경기 결과·표현 수렴과 함께, **BoulderSpirit 1:10 공격 시각 focused PASS/CLOSED**로 기록한다. 이는 사용자의 화면 판정이지 BoulderSpirit 개별 marker→권위 피해의 정확한 시간차 계측은 아니다. 1.50초 버전의 별도 화면 수용·453/414 이력은 그대로 보존한다. LittleKnight의 과거 Host MOVE adapterFailures 4건은 새 경기에서 0건이었어도 원인·수정이 입증되지 않아 별도 OPEN이고, 공식 cross-audit·25종 전체·역할교대·Legacy rollback/v2 Complete도 미완이다. 빌드 완료를 별도 관찰했다고 주장하지 않는다.
