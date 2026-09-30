# SpearMan 0:25 타격 교정 — Research

SpearMan의 창 찌르기가 닿는 순간을 사용자가 정한 30fps `0:25`에 맞추기 위한 조사다. `0:25`는 25프레임, 즉 `25/30 = 0.8333333초`이며 `0.25초`가 아니다. 화면에서 이벤트를 조정했다는 사용자 보고를 목표로 삼되, 현재 디스크의 예전 값을 별도의 변경 전 관찰로 기록한다. **이 문서는 구현 전 조사이며 파일 교정·Unity 검증·빌드·실기 결과가 아니다.**

## 변경 전 확인

- 사용자 보고: Unity Editor에서 SpearMan Attack 이벤트를 `0:25`로 조정하고 저장했다. 추가 확인 절차 없이 이 목표대로 진행한다. 읽기 전용 디스크 확인에서는 `Assets/_Project/Animations/Units/SpearMan/SpearMan_Attack.anim`의 유일한 `OnAttackHit`가 아직 `0.24초`이고 최종 수정일이 2026-07-13이다. 이는 **디스크의 변경 전 관찰**이며 Editor에서 한 조작을 부정하는 판정이 아니다. 구현 단계에서는 최종 저장 디스크 값 `0.8333333초`를 확인해야 한다.
- 해당 클립은 `m_SampleRate: 30`, stop time `2초`, loop 설정 `1`이다. 기존 이벤트 한 개를 옮기는 작업이며 곡선·길이·loop나 이벤트 수를 바꿀 이유가 없다. 클립 GUID는 `4772066a52e78bc4cb4890c02068d0ea`이고 `SpearMan.controller`의 `Base Layer/Attack` motion이 이를 참조한다.
- `Assets/_Project/Resources/Config/UnitStatsConfig.asset`의 `unitType: 4`는 `hitFrameTimes: [0.24]`, `attackCooldown: 2`다. `Assets/_Project/Docs/StatsReference.md`의 SpearMan 행은 `0:24(2:00)`이다. 문서의 `0:24`는 30fps 프레임 표기이고 config의 `0.24`는 초 값이므로 서로 같은 시각이라는 뜻이 아니다. 구현 후 참조표는 `0:25(2:00)`이어야 하며 2초 주기는 그대로다.
- `Assets/_Project/Scenes/Game.unity`의 UnitFactory `_humanPrefabs` type 4에는 Blue/Red SpearMan prefab이 등록돼 있으나 `attackTimelineClip` 명시 참조가 없다. 양 prefab은 같은 SpearMan controller GUID `302eb96e50e71a64cb6b5624f11960b2`와 `AnimationEventRelay`를 가진다. 씬의 다른 type 4 참조가 아닌 이 생산 등록을 수정해야 한다.
- `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`는 SpearMan을 `MeleeContact · Single`, `TimerImpact`, VFX 없음, `MigrationRequired`로 기록한다. 투사체 에셋 추가나 다중 타격 변경은 이번 범위가 아니다. LionKnight/BearGuard 전용 생산 게이트 선례는 있으나 SpearMan 전용 영구 생산 검증 호출은 현재 `RunUnitActionSelfValidation.cs`에서 확인되지 않는다.

## 규칙과 판정 경계

- `GameSystemRules_Units.md` 규칙 17: 일반 공격 타격 시각은 실제 Attack 클립의 `OnAttackHit`에서 읽고 config 값은 이벤트가 없을 때 폴백이다. 따라서 실제 production 클립, type 4 설정, 표기값을 같은 25프레임에 정렬한다.
- 같은 문서 규칙 18 및 `U-ATK-TIMELINE`, `GameSystemRules_UnitCombatSynchronization.md`의 `NET-PRESENT-001/002`: 로컬 이벤트는 표현 표식이고 실제 피해는 서버 타이머가 확정한다. 이벤트 시각 교정이 서버 피해 writer 변경이나 정확한 화면 접촉·피해 동시성의 실측을 뜻하지 않는다.
- `.claude/mistakes/unit-action-correction.md`의 2026-09-14 생산 marker/config 사건처럼, resolver 또는 fixture만 통과해서는 실제 씬→Blue/Red prefab→controller→Attack clip→config 연결을 증명하지 못한다. 영구 fail-closed production gate가 필요하다.
- 구현 전 상태는 **OPEN**이다. Unity 검사 PASS, 사용자 육안 수용, 공식 CrossAudit, 25종 전체·역할교대·Legacy rollback 또는 v2 migration 완료는 아직 주장하지 않는다.

## 2026-09-30 구현 저장 후 — 정적 확인, Unity·실기 대기

위 「변경 전 확인」의 `0.24초`·미연결·미구현 서술은 당시 디스크 상태 이력이다. 후속 디스크 재확인에서는 SpearMan Attack 클립의 유일한 `OnAttackHit`가 `0.8333333초`(30fps `0:25`)로 저장됐고, `UnitStatsConfig.asset`의 `unitType: 4`도 `hitFrameTimes: [0.8333333]`이며 `attackCooldown: 2`는 유지됐다. `StatsReference.md` SpearMan 행은 `0:25(2:00)`이다.

`Game.unity`의 UnitFactory `_humanPrefabs` type 4에는 Blue/Red prefab을 유지한 채 `attackTimelineClip` GUID `4772066a52e78bc4cb4890c02068d0ea`가 명시됐다. `RunUnitActionSelfValidation.cs`에는 SpearMan 전용 production gate 호출과 실제 clip/controller/양 팀 prefab/scene/config·25프레임 단일 marker를 검사하는 코드가 추가됐다. **코드와 직렬화 파일의 존재를 확인한 것이지 게이트 실행 PASS를 확인한 것은 아니다.** 투사체·hit VFX는 없으며 `MigrationRequired`를 유지한다.

현재 판정은 **저장·정적 확인 / Unity 재컴파일·두 self-validation·빌드·사용자 실기 미실행·미확인, focused OPEN**이다. 정확한 화면 접촉과 서버 피해의 시간차, 공식 CrossAudit 및 전체 25종·역할교대·Legacy rollback은 이 단계의 근거가 없다.

## 2026-09-30 Unity 자동 검사 — 두 메뉴 PASS, 실기 대기

위 「Unity·실기 대기」는 자동 검사 실행 전 상태 이력이다. 메인 세션은 Windows Unity 6000.3.5f2에서 재컴파일/domain reload 뒤 Console 오류 0을 확인했다. `Hexiege > Combat > Run Unit Action Self Validation` 실행 결과 18:19:55 `[UAS-DIAG] self-validation PASS`였고, 하위 production timeline PASS가 표시됐다. `Hexiege > Combat > Diagnostics > Self Validate Unit Root Pose Cross Audit` 실행 결과 18:20:48 `[UAS-ROOT-CROSS-AUDIT] self-validation PASS`였다. Console의 노란 경고 1건은 원인 미확인으로, 이 문서에서 정상·무관 여부를 판정하지 않는다.

PASS 범위는 **Unity 재컴파일 후 오류 0과 두 self-validation**이다. 이는 SpearMan 저장 clip/config/생산 연결의 자동 검증을 통과했다는 근거지만 실제 공격 장면이나 서버 피해 시각의 실측은 아니다. 빌드·실제 경기·사용자 화면 판정·공식 Root Pose CrossAudit Analyze는 미실행이다. SpearMan focused 작업은 **OPEN**, `MigrationRequired` 유지이며 25종·역할교대·Legacy rollback/v2 전체 완료를 주장하지 않는다.

## 2026-09-30 역할 교대 실기 — SpearMan 0:25 focused PASS/CLOSED

위 실기 대기 문단은 자동 검사 직후의 상태 이력이다. 사용자는 SpearMan 공격을 실제 경기에서 보고 육안상 문제없다고 수용했다. 같은 세션의 두 경기는 모두 SpearMan과 LittleKnight **2/25종 혼합 경기**이며, 아래 결과·표현 수치는 SpearMan 단독이 아닌 **각 경기 전체 집계**다. 로그 근거는 `_Logs/2026-09-30/18_50_logcat/RuntimeLog_device.txt` 및 `_Logs/_editor/2026-09-30/RuntimeLog.txt`다.

| 공유 세션 키 / 역할 | 생산(경기 전체) | 결과·표현(경기 전체) | 이동·ROOT |
|---|---|---|---|
| `39af4c6d…52373a` Editor Host / Android Client | SpearMan 47(Blue 23/Red 24), LittleKnight 48(Blue 24/Red 24) | Host 597 결과/실패 0 ↔ Client 597 수락/거부 0; 양쪽 C3 ready 597/597, 필수 표현 474/474, 실패 0 | Host MOVE adapterFailures 0, 양쪽 local ROOT PASS |
| `9b0404e3…3a4f` Android Host / Editor Client | SpearMan 28(Blue 14/Red 14), LittleKnight 31(Blue 15/Red 16) | Host 298 결과/실패 0 ↔ Client 298 수락/거부 0; 양쪽 C3 ready 298/298, 필수 표현 271/271, 실패 0 | Host MOVE adapterFailures 0, 양쪽 local ROOT PASS |

첫 Host SpearMan unitId 2와 둘째 Host unitId 98의 공격 시작 gate `Ready`·commit `Accepted`를 확인했다. 두 경기 Host MOVE gate/writer/handoff failures 0, 경기 시각대 GameLog ERROR/FATAL 0이다. 이 실기 근거와 사용자 육안 수용으로 **SpearMan `0:25` 타격 교정의 focused 판정만 PASS/CLOSED**다.

정확한 SpearMan marker→서버 권위 피해 시간차는 미계측이고 공식 CrossAudit Analyze, 전체 25종·전면 역할교대·Legacy rollback/v2 migration은 완료하지 않았다. 따라서 `MigrationRequired`는 **별도 OPEN**이다. 과거 LionKnight Client C3 `Expired` 7건과 LittleKnight MOVE adapter 4건은 이 두 경기에서 재발하지 않았더라도 각각 별도 OPEN이며 원인 해결을 뜻하지 않는다. Stream/Fox 타임라인 INCONCLUSIVE는 미생산 유형이라 이 판정의 실패로 섞지 않는다. 별도의 빌드 완료 관찰을 주장하지 않는다.
