# Plan — TorrentSpirit 파도 발사 시점 교정

사용자가 Attack 클립에 저장한 `2:10` 이벤트를 기준으로, 게임이 파도를 발사하도록 예약하는 시점을 맞춘다. 파도가 발사된 뒤 전방으로 이동하고 대상마다 닿을 때 피해 또는 회복을 주는 기존 기능은 그대로 유지한다. 문서와 자동 검증은 발사 시점과 접촉 시점을 구분하도록 고친다.

## 1. 범위와 변경하지 않을 것

- **기존 로직 제거 없음.** special-only 공격, 서버 wave 전선, 접촉별 첫 타격 hit-set, 적 건물 피해, 아군 유닛 회복, HP 동기화와 VFX 자산은 이번 교정에서 삭제·대체하지 않는다. 근거: `GameSystemRules_Units.md` 규칙 28~31.
- 사용자 저장 Attack 클립은 **재확인하거나 수정하지 않는다**. 사용자 지정 마커는 `2:10(30fps) = 70/30 ≈ 2.3333333초`다. 코드 담당은 사용자의 저장 완료를 전제로 설정과 생산 연결을 교정하되, 사용자 클립을 다시 배치하지 않는다. 자동 검증을 실행할 때 실제 자산 결합을 읽어 PASS/FAIL 판정하는 것은 구현 게이트이지 이번 문서 작성에서 클립을 다시 확인하는 절차가 아니다.
- 발사 뒤 이동 시간 `_waveTravelTime=0.5초`, 쿨다운 `4초`, 공격력·힐량·범위는 이번 타이밍 교정의 대상이 아니다. 두 0.5초 값 중 **unitType 18 `hitFrameTimes`만** 변경한다. 근거: 규칙 27~30.
- TorrentSpirit은 `Unresolved / LegacyFallback`과 `MigrationRequired`를 유지한다. 단일 마커 정렬을 `NET-DELIVERY-TRAVELING`의 회차·접촉별 식별 완료로 해석하지 않는다.

## 2. 수정 항목과 근거

| 항목 | 예정 작업 | 근거 |
|---|---|---|
| `Assets/_Project/Resources/Config/UnitStatsConfig.asset` | unitType 18의 단일 `hitFrameTimes`를 `0.5`에서 `70/30`초로 교정. 저장 가능한 float 표현은 코드 담당이 Unity 직렬화 결과에 맞게 결정한다. `attackCooldown=4` 유지. | `GameSystemRules_Units.md` 규칙 27·28·29; `NET-PRESENT-002` |
| `Assets/_Project/Docs/StatsReference.md` | TorrentSpirit 행의 옛 `0:50`과 “0.5s 임시”를 현행 `2:10(30fps)=70/30초`로 교정하고, 이는 **발사 시점**이며 적/아군별 접촉 적용은 이후 서버 전선에서 발생한다고 설명한다. 공격 주기 `4:00`과 공격·힐 설계값 유지. | 규칙 27~30; `NET-PRESENT-001/002`, `NET-DELIVERY-TRAVELING` |
| `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md` | 실제 적용 결과를 확인한 뒤 marker/config 현행값과 검증 범위를 갱신. 기존의 `MigrationRequired / Unresolved` 상태는 유지. | 규칙 27·29; `NET-DELIVERY-TRAVELING` |
| production 결합/검증 코드(코드 담당 조사 후 정확한 파일 확정) | Game 씬 생산 경로의 type 18 설정, Blue·Red 프리팹, Controller Attack state와 사용자 저장 마커가 한 세트로 결합되는지 검증한다. 영구 self-validation에는 단일 `OnAttackHit`, `70/30초` 설정, 올바른 production 바인딩, `Unresolved` 유지 조건을 fail-closed로 추가하는 방안을 검토한다. 클립을 재배치하지 않는다. | 규칙 27·31; `NET-PRESENT-001/002` |

`RunUnitActionSelfValidation.cs`의 기존 유닛별 production timeline gate를 재사용할 수 있는지 코드 담당이 확인한다. 정확한 production 경로와 추가 파일은 읽기 조사 후 이 Plan의 구현 결과 절에 기록한다. 이번 계획은 서버 wave/contact ID를 새로 설계하거나 `Supported`로 승격하는 승인이 아니다.

## 3. 구현 순서와 판정 경계

1. 코드 담당이 현재 생산 바인딩과 공격 타이머의 실제 사용처를 **클립 재확인 없이** 읽기 조사한다. `hitFrameTimes`를 바꾸면 서버 발사가 같은 시점으로 옮겨지는지 확인한다. 근거: 규칙 27~29, `NET-PRESENT-001/002`.
2. unitType 18 발사 예약을 `70/30초`로 교정하고 StatsReference·AssetMatrix를 구현 상태에 맞게 동기화한다. wave travel `0.5초`와 접촉별 처리, 쿨다운 `4초`는 유지한다. 근거: 규칙 28~30, `NET-DELIVERY-TRAVELING`.
3. 실제 production 경로가 사용하는 클립·Controller·Blue/Red 프리팹·설정의 불일치를 자동 게이트로 잡는다. 단일 발사 마커가 한 회차에 중복 파도를 만들지 않아야 한다. 자동 검증은 **자산/설정 정렬**까지만 증명하며 실기 접촉 동기화를 증명하지 않는다. 근거: 규칙 27·29·31, `NET-PRESENT-002`.
4. 문서 정합성 검사 `Tools/check_docs.py` 0건, Unity `Hexiege → Combat → Run Unit Action Self Validation`과 `Hexiege → Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit`의 PASS를 확인한 **뒤에만** Android Build And Run을 시작한다. 빌드는 시작 시도 경계까지만 담당하며 완료·설치·게임 실기 확인은 사용자가 한다. `PASS`를 얻지 못하면 빌드를 선행하지 않고 실패 원인을 보고한다.

## 4. 위험과 미완 범위

- `2:10`은 기존 `0.5초`보다 늦다. 기존 서버 발사 예약이 클립의 같은 occurrence를 가리키는지, 연속 Attack/provisional 경계에서 마커를 놓치지 않는지는 production 코드와 실기 증거를 구분해 판정한다. 단순 수치 정렬만으로 `NET-PRESENT-002` 전체가 완료됐다고 쓰지 않는다.
- 파도는 발사 뒤 대상 위치에 따라 다른 시각에 닿는다. `hitFrameTimes`와 `_waveTravelTime`을 합쳐 **모든 대상의 고정 피해 시각**으로 기록하지 않는다. 근거: 규칙 29, `NET-DELIVERY-TRAVELING`.
- 파도 VFX는 현재 시전자 위치에서 1회 재생되고 서버 전선과 함께 실제로 움직이지 않는다. 이 시각적 거리 문제는 이번 발사 시점 교정만으로 해결되지 않는다. 근거: 규칙 31 참고 문단.
- 회차/접촉별 exact-key, Host/Client 역할교대, 전체 25종, Legacy rollback, 실제 빌드 완료와 사용자 육안 판정은 별도 OPEN이다. 자동 PASS를 이들 실기 PASS로 확대하지 않는다.

## 5. 계획 상태

2026-09-30 문서 작성 단계. 사용자 제공 마커와 현행 config의 불일치를 기록했으며, 구현·Unity PASS·빌드 시작·실기 판정은 아직 이 Plan에 반영되지 않았다. 구현 결과와 계획 차이는 수행 뒤 날짜를 붙여 추가한다. Testcase/QA 문서는 사용자 요청이 없어 작성하지 않는다.

## 6. 구현·자동 검증·빌드 시도 상태 (2026-09-30 추가)

위 5절은 계획 작성 당시 상태로 보존한다. 이후 코드 담당이 발사 offset과 실제 생산 연결을 교정했고, 메인 세션은 새 Unity 자동 게이트 PASS를 확인했다. 다만 Build And Run의 Unity services 경고창에서 사용자 선택을 기다리는 중이므로 빌드가 시작됐다고 기록하지 않는다.

| 범위 | 확인된 결과 | 판정 경계 |
|---|---|---|
| `Assets/_Project/Resources/Config/UnitStatsConfig.asset` | type 18 `hitFrameTimes: [2.3333333]`, 쿨다운 `4` 유지 | `2:10` 발사 예약 설정; 발사 후 접촉 시각과 다름 |
| `Assets/_Project/Scenes/Game.unity` | type 18 `attackTimelineClip` 명시 연결 추가 | 생산 경로의 연결 교정; 사용자 실기 확인 아님 |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | TorrentSpirit production gate 추가 | 단일 발사 marker/설정·Controller·양 팀 프리팹·씬 연결의 자동 검증 범위 |
| Unity 자동 검사 | 새 `[UAS-DIAG][PRODUCTION-TIMELINE]` PASS(TorrentSpirit launch 포함), 새 `[UAS-ROOT-CROSS-AUDIT]` self-validation PASS | 자산/코드 게이트 PASS; 실제 파도 접촉·화면 타이밍·멀티 실기 PASS 아님 |
| 문서 검사 | `Tools/check_docs.py` 문제 0건 | 문서 정합성 검사 결과 |
| Android Build And Run | 메뉴 클릭 후 `Missing Project ID ... not a member ... this build will not access Unity services. Do you want to continue?` 경고창에서 사용자 선택 대기 | `Yes` 미선택. 빌드 파이프라인 시작·완료 미확인 |

**현재 판정:** 발사 offset/production gate 구현과 Unity 자동 게이트는 PASS, 빌드 시도는 경고창 대기, 사용자 실기와 전체 `TravelingArea` 회차/접촉 ID 이관은 OPEN이다. 사용자 선택 이후의 빌드 상태는 별도 증거를 확인해 추가한다. `Unresolved / LegacyFallback`, `MigrationRequired`, `waveTravelTime=0.5초`와 접촉별 서버 피해/힐은 유지한다. Testcase/QA는 작성하지 않는다.
