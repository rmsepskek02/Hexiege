# LionKnight 두 번의 공격 타이밍 교정 — Research

LionKnight가 한 번 공격할 때 두 차례 타격하는 장면을 사용자가 지정한 `0:18`, `1:13` 프레임에 맞추려는 작업이다. 30fps에서 첫 타격은 `0.6초`, 두 번째는 `43/30초 ≈ 1.43333333초`다. 현재 저장된 게임 파일과 문서에는 예전 값이 남아 있으므로, 구현 때 실제 저장값과 안내 표기를 함께 바로잡아야 한다. **이 문서는 구현 전 조사이며 변경·Unity 검증·빌드·실기 PASS를 기록하지 않는다.**

## 사용자 목표와 디스크 조사 경계

- 사용자는 Unity Animation 창에서 LionKnight Attack 이벤트 두 개를 `0:18`, `1:13`으로 조정했다고 보고하고 task 문서와 후속 구현을 요청했다. 그러나 이번 읽기 전용 디스크 검사에서 `Assets/_Project/Animations/Units/LionKnight/LionKnight_Attack.anim`의 `m_Events`는 여전히 `OnAttackHit @ 0.22`, `@ 1.08`초였다. 파일 최종 수정일은 검사 시 2026-07-13으로 표시됐다. Unity 창의 편집 상태나 저장 여부는 이 파일 검사만으로 단정하지 않는다. 구현자는 **사용자 목표를 디스크에 저장 반영한 뒤 파일을 재확인**해야 한다.
- 저장 클립의 `m_SampleRate: 30`, stop time `2초`, loop `1`은 확인했다. 기존 두 이벤트를 목표 시각으로 옮기되, 이벤트를 중복 생성하거나 곡선·길이·loop를 임의 변경하지 않는다. float 저장값은 `0.6` 및 `1.4333333` 안팎일 수 있으므로 30fps frame 18·43에 해당하는지를 기준으로 비교한다.
- `LionKnight_Attack.anim.meta` GUID는 `45c00f46f91a76047a76e774cfa0f6ac`다. `LionKnight.controller`의 `Base Layer/Attack` state motion이 바로 이 GUID를 참조한다. Blue/Red `Unit_LionKnight` prefab은 같은 controller GUID `85ce28fd23241d24b84dc9149050c194`를 사용하고 `AnimationEventRelay`를 갖는다. 근거: 해당 clip/controller/meta 및 `Assets/_Project/Prefabs/Units/Transcendence/Unit_LionKnight_{Blue,Red}.prefab`.
- `Assets/_Project/Resources/Config/UnitStatsConfig.asset`의 실제 `unitType: 22`는 `hitFrameTimes: [0.22, 1.08]`, `attackCooldown: 3`이다. HP 500·공격력 90 등 나머지 스탯은 이번 타이밍 변경 범위가 아니다.
- `Assets/_Project/Scenes/Game.unity`에는 `type: 22`가 여러 컴포넌트에 나타난다. 실제 **UnitFactory** 오브젝트의 `_transcendencePrefabs` type 22 등록은 LionKnight Blue/Red prefab GUID `fb844688f9e49504584a5c95f185365b` / `60882e4b6ee1f20409867db71e9f6248`를 참조하나, 해당 등록에 `attackTimelineClip`은 명시돼 있지 않다. 다른 type 22 배열이나 인접 type 20 BearGuard 연결을 LionKnight 생산 배선으로 오인하지 않는다.
- `UnitAttackShadowProfileResolver.cs`는 LionKnight를 `Supported / MeleeContact / Impact 2 / secondary false`로 분류한다. `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`에는 BearGuard 등 전용 production timeline 검증 호출이 있지만 LionKnight 전용 clip/controller/scene/config 생산 gate 호출은 현재 없다. LionKnight가 다른 일반 회귀의 대상이라는 사실만으로 이 생산 연결이 검증됐다고 보지 않는다.
- `Assets/_Project/Docs/StatsReference.md` LionKnight 행은 `0:22,1:08(3:00)`과 2히트, `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`는 clip/config `2 @ 0.22, 1.08s / 동일`, `MigrationRequired`를 적는다. **StatsReference의 `0:22,1:08`은 프레임 표기**이고, 디스크의 `0.22,1.08`은 초 단위 직렬화값이다. 둘을 동일 수치로 취급하지 않는다. 사용자 목표를 구현할 때 StatsReference는 반드시 `0:18,1:13(3:00)`으로 수정하고 3초 공격 주기·HP·공격력 등은 보존해야 한다. 감사표도 실제 저장·검증 단계에 맞춰 후속 동기화 대상이다.

## 규칙 및 판정 경계

- `Assets/_Project/Docs/GameSystemRules.md` 유닛 규칙 인덱스 → `GameSystemRules_Units.md` **규칙 17**: 일반 공격 타격은 실제 Attack clip의 `OnAttackHit`에서 읽고 다중 이벤트를 시간 오름차순으로 수집한다. 수동 config는 이벤트가 없을 때 폴백이다. 따라서 첫 이벤트를 `HitIndex=0`, 두 번째를 `HitIndex=1`로 유지하는 것이 목표다.
- 같은 문서 **규칙 18**: 피해는 Animator 이벤트가 아니라 서버 타이머로 적용한다. 이벤트 시각 교정과 type 22 config 동기화는 필요하지만, 이번 범위에서 피해 writer·서버 권위·cooldown `3초`를 바꾸지 않는다.
- `GameSystemRules_UnitCombatSynchronization.md` **NET-PRESENT-001/002**: Animation Event는 로컬 표현 표식이며 검증된 AttackTimeline과 일치해야 한다. 다중 타격은 `AttackSequenceId + HitIndex`로 분리한다. 정적 marker/config 일치나 Unity self-validation PASS만으로 정확한 화면 접촉·서버 피해 동시성 또는 규칙 v2 전체 migration 완료를 주장할 수 없다.
- 이전 BearGuard `0:13` focused task는 별도 CLOSED다. LittleKnight의 과거 Host MOVE adapter failure 4건은 별도 OPEN이다. 이 비교 유닛의 미재발만으로 해결로 판단하지 않는다. LionKnight `MigrationRequired` 및 25종·역할교대·Legacy rollback/공식 CrossAudit도 이 task 작성만으로 달라지지 않는다.

## 2026-09-30 구현 후 저장 상태와 실기 증거

저장 파일의 LionKnight Attack 이벤트 두 개와 type 22 설정은 현재 `0.6`, `1.4333333`초(30fps `0:18`, `1:13`)로 일치한다. Game 씬 UnitFactory type 22에는 해당 Attack clip GUID가 명시됐고 LionKnight 전용 production gate 호출이 코드에 존재한다. 이는 디스크·코드 상태의 확인이며, 이 조사만으로 Unity 메뉴 PASS나 실제 화면의 정확한 접촉 시점을 증명하지 않는다. 위 「구현 전」 조사 문장은 당시 상태 이력으로 보존한다.

사용자가 승인한 `a2d9c80e143ab3ee03081f3925c936873e23978929e15528ec0b9b2a4c622225` 경기의 근거는 Android Host `_Logs/2026-09-30/11_37_logcat/RuntimeLog_device.txt`와 Editor Client `_Logs/_editor/2026-09-30/RuntimeLog.txt`다. 실제 생산 범위는 LionKnight와 LittleKnight **2/25종**이며 아래 집계는 LionKnight 단독이 아닌 경기 전체다.

| 경계 | Android Host | Editor Client |
|---|---|---|
| C2 결과 | 서버 결과 224, 실패 0 | 수락 224, 거부 0 |
| C3 조정기 | ready 224, 실패 0, 묶음 224/방출 224 | ready 217, 실패 7, 묶음 224/방출 217 |
| 필수 표현 | 199/199 | ready 범위에서 192/192 |
| local ROOT | PASS | PASS |

Client `coordinator-failure` 7건은 모두 `Expired`다. `observed - intended` 지연은 약 **0.527~1.192초**로 0.5초 한도를 초과했다. 첫 실패의 `instance=1`은 생산 기록상 LionKnight `UnitId=2`에 대응하며 같은 instance의 실패가 한 건 더 있다. 나머지 여섯 실패를 모두 LionKnight로 분류할 근거는 없다. Client 필수 표현 192/192는 **ready 217건 중 요구된 표현**의 일치이지, 만료된 7묶음까지 성공했다는 뜻이 아니다. 사용자는 큰 시각적 문제를 보지 못했으나 렉이 심했다고 보고했다. 두 로그에 FPS/frame-time 측정값이 없어 렉의 원인과 만료의 인과관계는 확정할 수 없다.

**판정: LionKnight focused task FAIL/OPEN.** Client 표현 만료 7건을 해소·재검증하기 전 육안 수용만으로 PASS/CLOSED로 바꾸지 않는다. 정확한 marker→권위 피해 offset, 공식 Root Pose CrossAudit Analyze, 전체 25종·역할교대·Legacy rollback 및 v2 완료도 이 2/25 경기로 검증되지 않았다.

## 2026-09-30 후속 2/25 실기 — 기존 실패 보존

후속 진단 계측 뒤 새 **Editor Host / Android Client** 경기 `a81345042552685a831405817424d1a4b7e2260e56708f4e6f8febb7c3aacb5b`(13:43:15~13:45:47)에서 Host가 LionKnight 8기와 LittleKnight 22기를 생산했다. 양측 C3 schedules 226·results/ready 216·pending/failures 0·묶음 216/216 방출·필수 표현 188/188, Host C2 결과 216/실패 0과 Client 수락 216/거부 0, 공간 불일치 0, 양측 local ROOT PASS·오류 0이었다. Client 진단은 arrival 216건·overflow/pending 0이고 `Expired` 0건이다. 사용자는 큰 시각적 문제를 보지 못했다. 근거: Editor Host `Assets/_Project/Docs/_Logs/_editor/2026-09-30/RuntimeLog.txt`, Android Client `Assets/_Project/Docs/_Logs/2026-09-30/13_46_logcat/RuntimeLog_device.txt`; 상세 이동·로그 경계는 `12_37_attack-presentation-expiry-diagnosis/{Research,Plan}.md`.

이는 **이번 경기의 무만료 관찰**이지 이전 Android Host/Editor Client 경기의 7 `Expired` 원인 규명이나 수정 증거가 아니다. 변경은 원인 분리용 계측뿐이었고 이번 실패가 재현되지 않아 실패 상세 `coordinator-expiry-timing-A/B`도 얻지 못했다. 이번 런에서 과거 심한 렉은 평가되지 않았고 공식 CrossAudit Analyze·정확한 접촉 시각도 확인되지 않았다. **LionKnight focused task FAIL/OPEN 유지**; 이전 증거와 2/25 범위, 전체 migration·역할교대·rollback 미완을 모두 보존한다.

## 2026-09-30 사용자 수용 판정 — LionKnight focused PASS/CLOSED

위 FAIL/OPEN 문장들은 당시 판정 이력이다. 사용자는 최신 `a8134504…c3aacb5b` 실기의 육안상 큰 문제 없음을 수용하고, LionKnight `0:18`·`1:13` focused 교정의 판정을 **PASS/CLOSED**로 분리했다. Editor Host / Android Client의 LionKnight 8기·LittleKnight 22기 **2/25종 혼합 경기 전체**에서 C2 결과/수락 216/216, C3 결과·ready 216/216·실패 0, 묶음 방출 216/216, 필수 표현 양쪽 188/188, Client `Expired` 0, 양쪽 local ROOT PASS가 이 focused 판정의 근거다. 이는 LionKnight만의 정밀 marker→피해 시차나 공식 CrossAudit PASS를 뜻하지 않는다.

첫 `a2d9…c622225` 경기의 Client `Expired` 7건은 소급 해소되지 않은 **별도 C3 표현 결함 OPEN**이다. 원인 미확정의 `12_37_attack-presentation-expiry-diagnosis` task에서 계속 추적한다. 과거 렉의 원인·인과, 정확한 접촉 시각, 25종 전체·역할교대·Legacy rollback·v2 완료 및 LittleKnight 과거 MOVE 4건도 이번 focused PASS 범위 밖이다.
