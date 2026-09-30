# FlameSpirit 6-hit focused 공격 교정 — Plan

> **최신 상태(2026-09-29):** 아래 2026-09-28의 옛 6시각·구현 전 계획은 이력이다. 현재 생산 마커/설정은 `20/30, 35/30, 43/30, 50/30, 57/30, 63/30`초다. 같은 세션 실기 결과와 미검증 경계는 문서 끝의 「2026-09-29 후속 결과」를 우선한다.

FlameSpirit의 여섯 타격 시각은 애니메이션과 설정에 이미 맞아 있다. 다음 구현은 실제 경기 생산에서 그 Attack 클립을 확정적으로 선택하게 하고, 자산 연결이 바뀌면 자동 검사에서 드러나게 하는 작은 교정이다. 화면·서버의 여섯 타격이 실제로 일치하는지는 그 뒤 별도 실기 확인이 필요하다.

## 구현 전제와 범위

- Research의 정적 관측 기준: Game 씬 type 10은 Blue/Red prefab만 참조하고 `attackTimelineClip`이 비어 있다. Controller `Base Layer/Attack`은 30fps·3초 `FlameSpirit_Attack.anim`(GUID `63b9b44d031f68f468d186399d915ee7`)을 재생하고 marker/config는 `[0.20, 1.05, 1.13, 1.20, 1.28, 2.03]`으로 일치한다.
- **기존 로직 제거 없음.** `UnitFactory`의 범용 폴백, 6-hit 서버 예약, Shadow resolver/sequence, C3 표현 lease는 유지한다. Marker/config의 임의 재조정·새 multi-hit engine·새 공격 사양은 범위 밖이다.
- 이 Plan은 구현 지시 문서이며 **이번 문서작성 단계에서 아래 파일을 수정하지 않는다**. 사용자의 기존 “문서작성하고 구현 및 빌드 시도까지” 승인 맥락을 기록하되, 실제 구현은 `WORKFLOW.md` [4]의 Plan 공유·승인 경계를 따른다.

## 최소 수정 항목

| 항목·대상 | 구현 방법 | 규칙 근거 | 위험 및 완료 전 gate |
|---|---|---|---|
| 1. `Assets/_Project/Scenes/Game.unity`의 UnitFactory `_spiritPrefabs` type 10 | 기존 Blue/Red 참조를 보존하고 `attackTimelineClip`에 실제 `FlameSpirit_Attack.anim`을 명시 연결한다. 단일 type 10 행과 GUID를 Editor에서 대조한다. | 유닛 규칙 17(실제 Attack 이벤트 단일 출처), `NET-PRESENT-002`(검증된 타임라인), Matrix의 AttackTimeline 명시 참조 요구 | YAML 참조 오입력·씬 저장 누락. 빌드 전 실제 Game 씬을 읽는 gate에서 clip과 양 팀 prefab 동일성 확인. |
| 2. `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | FlameSpirit 전용 **읽기 전용** production timeline gate를 등록한다. clip/controller/Blue·Red prefab GUID, `Base Layer/Attack` 단일 state의 motion·speed, 양 prefab Animator controller/relay, Game 씬 type 10의 clip+양 팀 prefab, `UnitStatsConfig.asset` type 10 단일 행·3초 폴백·6시각, 서버/Client 추출 경로를 검증한다. 명시 clip 우선 선택과 controller 순서 변화에도 선택이 유지됨을 확인한다. 기존 6-hit 순수 fixture는 재사용하고 자산 gate로 대체하지 않는다. | 유닛 규칙 17·18, `NET-ACTION-SEQ`, `NET-PRESENT-001/002`, `NET-ACTION-IDEMPOTENT`, Matrix의 MigrationRequired | 과도한 문자열 검사·잘못된 GUID 고정은 오탐 위험. 실제 자산/생산 경로를 대상으로 fail-closed, prefab·씬은 preview/읽기 전용, 오류 시 원인 식별 가능하게 한다. |

현재 증거로 `FlameSpirit_Attack.anim`, `FlameSpirit.controller`, Blue/Red prefab, `UnitStatsConfig.asset`, `UnitFactory.cs`, `NetworkCombatController.cs`, `UnitAttackShadowProfileResolver.cs`, `UnitView.cs`의 **수정은 계획하지 않는다**. gate가 실제 차이를 발견하면 관측값과 원인을 Research/Plan에 먼저 기록하고, 필요한 변경 범위를 재확정한다. 특히 marker 시각 변경은 사용자 요청 없이 하지 않는다.

## 빌드 전 검증 gate와 후속 확인

1. Editor 자산 gate에서 type 10 scene 등록·실제 state motion·양 팀 prefab·6 marker·설정 폴백·명시 선택을 검사한다. 샘플 데이터만의 PASS로 production PASS를 주장하지 않는다.
2. 기존 Unit Action self-validation과 Root Pose Cross Audit/Visual Root validator를 실행해 실패 0을 확인한다. 회귀 실패는 먼저 원인별로 분리한다. C2의 6-hit overshoot·취소·결과 순서와 C3의 인접 `HitIndex` 분리 fixture도 유지한다.
3. Unity C# 컴파일 오류 0 및 새 gate PASS를 확인한 **뒤에만** 승인된 빌드를 시도한다. 빌드 시작은 완료나 Android 실기 PASS가 아니다. 이번 문서작성 요청에서는 Unity/빌드 실행을 하지 않는다.
4. 후속 focused 실기에서는 실제 FlameSpirit 생산을 증명하고, 동일 경기 Host/Client에서 6개 `HitIndex 0..5`의 예약·확정 결과·필수 표현을 회차별로 대조한다. 밀집 1.05~1.28초 타격, 연속 루프/두 번째 회차, 타겟·공격자 사망/Stop 시 미확정 잔여 타격, 유효 결과 중복·누락·잘못된 다음 회차 결합을 따로 본다. 화면 접촉 타이밍은 사용자 육안 수용과 로그 측정의 경계를 구분한다. (`NET-ACTION-SEQ`, `NET-PRESENT-002/003`, `NET-CANCEL-003~005`, 유닛 규칙 18·19)

## 판정 한계

정적 정렬과 자동 gate 통과만으로 FlameSpirit focused PASS나 `MigrationRequired` 해소를 선언하지 않는다. 앞선 EmberSpirit 결과도 이 유닛의 생산·표현 증거가 아니다. 25종 전체, Host/Client 역할교대, Legacy rollback, 완전한 ActionSequence migration은 이 focused 범위의 완료 조건이 아니다. 실제 파일이 이 조사와 달라지면 새 파일의 직접 증거를 우선하고 계획을 갱신한다.

## 2026-09-29 후속 결과

2026-09-28 계획의 수치와 미실기 상태는 당시 기록으로 남긴다. 현재 생산 마커·설정은 사용자 확인 30fps 기준 `20/30, 35/30, 43/30, 50/30, 57/30, 63/30`초(마지막 전 타격 `1:27`)로 일치한다. `StatsReference.md`의 FlameSpirit 행에 현행 수치와 Unity 자동 검증 결과가 기록돼 있다.

Editor Host/Android Client 같은 경기(`sharedSessionKey=f7ed121d3e55d4eeb39d42eb3ce144f8f99565281734d017b6d00d73a930f6e1`, 00:08:38~00:11:39)에서 FlameSpirit 생산 및 Host 공격 시작 `Ready`·shadow commit `Accepted`가 확인됐다. Host commit 238, 서버 결과 723/실패 0, coordinator 794 schedule/723 result/723 ready/pending·failure 0, Host·Client 필수 표현 각각 483/483·실패 0, Client 결과 723 수락/0 거부다. 양쪽 UAS root local PASS/errors 0, Host 이동 rejected·invalid 0 및 Client 복제 invalid·공격 진입 순서 위반 0도 확인됐다. 근거 파일은 Research의 2026-09-29 절과 같다.

사용자는 화면에서 **“정상적으로 되는 것으로 보여”**라고 관찰했다. 단, 위 723·483 등은 FlameSpirit뿐 아니라 LittleKnight·Pistoleer가 포함된 **경기 전체** 수치다. 이 로그로 FlameSpirit 공격 회차별 여섯 `HitIndex`의 개별 offset이나 정확한 subframe 일치, 사망·Stop 취소, 25종·역할교대·Legacy rollback을 완료 처리하지 않는다. **focused 실기 긍정 근거 확보, 추가 정밀 대조 OPEN; `MigrationRequired`, v2 Complete 미달 유지.** 이번 결과 반영은 문서만이며 새 Task/Testcase·QA 문서는 만들지 않는다.

Android의 일시적 spawn/UnitView 경고 105건(UnitId 0~49)은 모두 이후 초기화됐고 최종 `viewUnavailable=0`이다. 경기 종료 시 연결 끊김 경고 1건과 구분하며, 최신 세션에 GameLog ERROR는 없다. 이 경고를 FlameSpirit 공격 실패로 해석하지 않는다.
