# FlameSpirit 6-hit focused 공격 교정 — Research

> **최신 상태(2026-09-29):** 아래 2026-09-28의 `0.20/1.05/1.13/1.20/1.28/2.03` 정적 관측과 미실기 서술은 **구현 전 이력**이다. 현재 생산 마커·설정은 `20/30, 35/30, 43/30, 50/30, 57/30, 63/30`초로 정렬돼 있다. 같은 세션 Host/Android Client 실기 결과와 남은 검증 범위는 문서 끝의 「2026-09-29 교정·실기 상태」를 따른다.

FlameSpirit의 여섯 번 연속 공격이 실제 게임에서 어떤 애니메이션과 설정을 사용하고, 서버 피해와 양쪽 화면 표현이 같은 공격 회차에 묶이는지 확인한다. 이 문서는 구현 전 정적 조사다. 타격 시각을 새로 정하거나 이미 정상인 다중 타격 엔진을 다시 설계하지 않는다.

## 조사 경계

- 기준: 2026-09-28, 사용자 지정 작업 폴더. `AGENTS.md`, `CLAUDE.md`, `WORKFLOW.md`, `GameSystemRules.md`, 유닛·전투 동기화 규칙, `UnitCombatAssetMatrix.md`, 관련 실수 기록과 실제 YAML/C# 소스를 읽었다. 사용자 제공 `.claude/MEMORY.md` 전문도 참조했다.
- 이번에는 Unity 실행, 빌드, 경기·기기 로그 검증을 하지 않았다. 아래의 “일치”는 파일 정적 대조이며 실기 PASS가 아니다. 앞선 EmberSpirit 사용자 수용은 FlameSpirit의 검증 증거가 아니다.

## 생산 경로에서 직접 확인한 사실

| 층 | 관측 | 근거 |
|---|---|---|
| Game 씬 등록 | `Game.unity`의 `UnitFactory._spiritPrefabs`에 `type: 10` FlameSpirit Blue/Red가 각 해당 prefab GUID로 연결돼 있다. 이 행에는 `attackTimelineClip`이 **없다**. | `Scenes/Game.unity`의 UnitFactory type 10 행; Blue `30850a40f0ca8b940a1afaf12c1d82c1`, Red `4c3b5c6f718fbf142afc55d5b3e004fb` |
| 양 팀 prefab | Blue/Red의 Animator 모두 같은 `FlameSpirit.controller` GUID `ff8c21c56cd90ab449d7ddd2a4eb4351`을 참조하며 Animator와 같은 객체에 `AnimationEventRelay`가 있다. | `Prefabs/Units/Spirit/Unit_FlameSpirit_{Blue,Red}.prefab` |
| 실제 Attack state | Controller `Base Layer/Attack`은 speed 1, motion GUID `63b9b44d031f68f468d186399d915ee7`로 `FlameSpirit_Attack.anim`을 가리킨다. | `Animations/Units/FlameSpirit/FlameSpirit.controller` 및 `.anim.meta` |
| Attack clip | 저장된 30fps·3초 루프 클립에 `OnAttackHit`가 정확히 6개: `[0.20, 1.05, 1.13, 1.20, 1.28, 2.03]`초. | `FlameSpirit_Attack.anim`의 `m_Events`, `m_SampleRate`, `m_AnimationClipSettings` |
| 설정 폴백 | `UnitStatsConfig.asset`의 유일하게 확인한 type 10 행은 `attackCooldown: 3`, `hitFrameTimes` 위 6개와 동일. **이 행의 유일성은 별도 Editor gate에서 확인해야 한다.** | `Resources/Config/UnitStatsConfig.asset` type 10 블록 |
| 생산 추출 | `UnitFactory`의 서버/싱글 생성 및 Client 초기화 모두 `GetAttackTimelineClip` → clip length → `GetHitFrameTimes`를 사용한다. 명시 참조가 null이면 `runtimeAnimatorController.animationClips`에서 이름에 `Attack`이 포함된 **첫 클립**을 선택한다. FlameSpirit은 현재 이 폴백에 의존한다. | `UnitFactory.cs`의 두 초기화 경로와 선택 함수 |
| 프로필·예약 | Resolver는 FlameSpirit을 `Supported / MeleeContact / TargetLocked / TimerImpact / 6 / secondary false`로 반환하고 런타임 타격 수·오름차순을 검사한다. 서버 전투는 `HitFrameTimes` 길이만큼 `hitIndex`별 독립 예약/코루틴을 만든다. | `UnitAttackShadowProfileResolver.cs`, `NetworkCombatController.cs` 공격 예약 구간 |
| 표현 | `AnimationEventRelay`가 `UnitView.OnAttackHit`로 전달한다. UnitView는 승인된 scope와 `HitFrameTimes.Length`로 타격·source marker lease를 연다. 6회 범용 동작은 존재하지만 **FlameSpirit production 연결·실기 증명은 별개**다. | `AnimationEventRelay.cs`, `UnitView.cs` attack presentation 구간 |

Matrix의 FlameSpirit 행(`MeleeContact · Single · Damage · MultiImpact(6)`, `TimerImpact`, `MigrationRequired`)은 위 정적 타격 개수·시각과 일치한다. `MultiImpact(6)`은 한 공격 회차의 `HitIndex 0..5` 여섯 개이지, AoE 여섯 피해자가 아니다. 서버 피해는 marker 이벤트가 직접 적용하지 않고 서버 타이머가 적용한다(유닛 규칙 17·18, `NET-PRESENT-001`).

## 정렬과 불일치

- **정렬:** Attack state motion, clip marker 6개, type 10 설정 폴백 6개, resolver의 expected impact count 6은 정적으로 맞는다. `0.20/1.05/1.13/1.20/1.28/2.03`을 임의 변경할 근거는 없다.
- **생산 계약의 빈틈:** 실제 Game 씬 type 10에 명시 `attackTimelineClip`이 없다. 현재 컨트롤러에서 단일 Attack을 찾을 가능성은 있지만, `SelectAttackTimelineClip`은 실제 Attack state를 검사하지 않고 이름/열거 순서에 의존한다. 현재 오류 클립 선택이나 누락 피해가 **관측된 것은 아니다**. 불일치라는 말은 `AttackTimeline` 명시 참조·영구 검증 요구와의 계약상 빈틈을 뜻한다.
- **검증 빈틈:** `RunUnitActionSelfValidation.cs`에는 Ember/Dust/Rabbit 등의 production timeline gate가 있으나 FlameSpirit 전용 clip/controller/Blue·Red prefab/Game 씬/config exact 대조 gate는 확인되지 않았다. 기존 FlameSpirit fixture는 6개 수와 overshoot 등 순수 엔진 경로를 시험하며 실제 0.20…2.03초 자산 연결을 검증하지 않는다. `ValidateUnitCombatSetup.cs`는 범용 Visual Root·Animator/relay 구조 감사이며 이 6-marker 타임라인의 대체가 아니다.
- **미확인:** 연속 Attack 재생 중 커밋 뒤 첫 marker 선택, 1.05~1.28초의 밀집 4-hit, 각 `HitIndex`의 서버 결과/Host·Client 표현 상관관계, 타겟·공격자 사망 시 잔여 타격 취소, 루프/다음 회차 중복 억제, 육안 접촉 시점은 정적 파일만으로 PASS 불가. 타격 시각의 시각적 적합성도 사용자 관측이 필요하다.

## 판단

현재 증거로 필요한 최소 구현 후보는 **Game 씬 type 10에 실제 Attack clip 명시 연결**과 **그 연결·6-marker/설정/양 팀 prefab을 fail-closed로 검사하는 영구 production gate**다. 기존 resolver, 서버 6-hit 예약, exact-key/lease 엔진의 변경은 현재 증거로 정당화되지 않는다. 코드·자산 교정 후에도 Unity 자동 검증과 실제 생산 경기를 통과하기 전에는 `MigrationRequired` 및 FlameSpirit focused 미검증을 유지한다. 25종 전체, 역할교대, rollback 완료를 주장하지 않는다.

## 2026-09-29 교정·실기 상태

- **수치 정정(원문 보존):** 09-28 조사·판단의 6시각은 당시 정적 기록이다. 현재 `FlameSpirit_Attack.anim`의 `OnAttackHit`와 `UnitStatsConfig.asset`의 `hitFrameTimes`는 사용자 확인 30fps 마커 `0:20, 1:05, 1:13, 1:20, 1:27, 2:03`에 해당하는 `20/30, 35/30, 43/30, 50/30, 57/30, 63/30`초다. 특히 `1:27`은 옛 `1:28`을 대체한다. 현행 수치·Unity 자동 검증은 `StatsReference.md` FlameSpirit 행 참조.
- **같은 경기의 직접 근거:** Editor Host `_Logs/_editor/2026-09-29/RuntimeLog.txt`와 Android Client `_Logs/2026-09-29/00_12_logcat/RuntimeLog_device.txt`, `sharedSessionKey=f7ed121d3e55d4eeb39d42eb3ce144f8f99565281734d017b6d00d73a930f6e1`(00:08:38~00:11:39). FlameSpirit 실제 생산 및 Host의 FlameSpirit `production-start-gate result=Ready`와 `shadow-commit status=Accepted`가 기록됐다.
- **경기 전체 집계:** Host commit 238, 서버 결과 723·실패 0·correlation failure 0·target mismatch 0; coordinator schedule 794/result 723/ready 723/pending 0/failure 0; 양쪽 필수 표현 483/483·failure 0. Client는 결과 723 수락/0 거부. 양쪽 UAS root local summary는 PASS/errors 0, Host movement rejected/invalid 0, Client replication invalid/attack-entry order violation 0이다. 이 집계는 **FlameSpirit·LittleKnight·Pistoleer 혼합 경기 전체**이며 FlameSpirit 단독 타격 건수가 아니다.
- **기기 경고 분리:** Android의 spawn/UnitView 일시 경고 105건(UnitId 0~49)은 이후 초기화가 완료됐고 최종 `viewUnavailable=0`이다. 종료 시 연결 끊김 경고 1건은 별개다. 이 최신 세션에 GameLog ERROR는 없다. 경고를 FlameSpirit 타격 실패로 분류하지 않는다.
- **판정 경계:** 사용자는 화면상 **“정상적으로 되는 것으로 보여”**라고 관찰했다. 따라서 이번 생산·공격 시작 및 같은 경기의 결과/표현 수렴은 focused 긍정 근거다. 다만 이 경계 로그만으로 FlameSpirit 한 회차의 `HitIndex 0..5`별 실제 offset, animation→피해 subframe 정렬, 타깃/공격자 사망 시 잔여 타격, 25종·역할교대·rollback을 증명하지 않는다. `MigrationRequired`와 v2 Complete 미달은 유지한다.
