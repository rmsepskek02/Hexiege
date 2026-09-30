# QuakeSpirit 공격 타격 시점·표현 프로필 교정 조사

QuakeSpirit은 피해와 범위 공격 기능 자체는 이미 구현되어 있지만, 실제 공격 애니메이션이 땅을 때리는 순간과 서버가 사용하는 타격 시점 설정이 서로 다릅니다. 이번 작업은 새 공격 방식을 만드는 것이 아니라, 이미 에셋에 들어 있는 실제 타격 표식인 **1.667초**를 런타임의 기준으로 맞추고 QuakeSpirit을 정식 지원 표현 프로필로 전환하는 작업입니다. 피해 권위나 정상 동작 중인 공통 C2/C3 경로는 변경하지 않습니다.

**문서 상태:** 구현·자동 검증 및 focused Android Host/Client 로그 검증 완료 · **CONDITIONAL PASS / OPEN** · 화면 타격 시점·제외 대상·육안 회귀 확인 대기

## 1. 조사 결론

- QuakeSpirit의 양 진영 프리팹은 같은 Animator Controller를 사용한다.
- Controller의 `Base Layer/Attack` 상태는 `QuakeSpirit_Attack.anim`을 재생한다.
- Attack 클립은 5초, 30fps이며 `OnAttackHit` 이벤트가 **1.667초에 정확히 1개** 존재한다.
- 전용 공격 VFX 미리보기 도구도 QuakeSpirit의 타격 시점을 **1.667초**로 사용한다.
- 반면 실제 런타임 설정인 `UnitStatsConfig`의 QuakeSpirit 타격 시점은 **1.0초**다.
- 표현 프로필 resolver는 이 1.0초를 placeholder로 취급하여 QuakeSpirit을 `Unresolved`로 분류한다.
- 따라서 현재 결함은 “Animation Event가 없는 것”이 아니라 **검증된 에셋 marker 1.667초와 런타임 타임라인 1.0초가 불일치하고, 프로필 상태가 과거 placeholder 판정에 머문 것**이다.

## 2. 직접 확인한 증거

### 2-1. Controller와 양 진영 프리팹

- `Assets/_Project/Animations/Units/QuakeSpirit/QuakeSpirit.controller:115~128`
  - 상태 이름은 `Attack`, 재생 속도는 1이다.
  - 연결된 clip GUID는 `75f0862e387cda542979bed8db30ba62`다.
- `Assets/_Project/Animations/Units/QuakeSpirit/QuakeSpirit_Attack.anim.meta:2`
  - 위 GUID가 `QuakeSpirit_Attack.anim`의 GUID와 일치한다.
- `Assets/_Project/Animations/Units/QuakeSpirit/QuakeSpirit.controller.meta:2`
  - Controller GUID는 `aaaa3b1c11a444f4a965c443b80dd260`이다.
- `Assets/_Project/Prefabs/Units/Spirit/Unit_QuakeSpirit_Blue.prefab:379`
- `Assets/_Project/Prefabs/Units/Spirit/Unit_QuakeSpirit_Red.prefab:379`
  - 두 프리팹 모두 위 Controller GUID를 참조한다.

이 연결로 Blue/Red가 서로 다른 클립이나 Controller를 쓰는 가능성은 배제된다.

### 2-2. 실제 공격 marker

- `Assets/_Project/Animations/Units/QuakeSpirit/QuakeSpirit_Attack.anim:58929`
  - sample rate는 30fps다.
- 같은 파일 `:60112`
  - clip 종료 시각은 5초다.
- 같은 파일 `:119039~119045`
  - `OnAttackHit` 이벤트가 1.667초에 정확히 1개 존재한다.
- `Assets/_Project/Scripts/Editor/SpiritAttackVfxTestSpawner.cs:81~83`
  - QuakeSpirit 전용 미리보기 입력도 1.667초다.

서로 독립적인 실제 clip 데이터와 전용 미리보기 값이 같은 시각을 가리키므로 **1.667초를 현재 에셋의 검증된 타격 시점으로 사용한다.**

### 2-3. 런타임 설정 불일치

- `Assets/_Project/Resources/Config/UnitStatsConfig.asset:359~372`
  - QuakeSpirit은 `unitType: 15`, 공격 쿨다운 5초, `hitFrameTimes` 1.0초다.

현재 1.0초 값은 clip의 1.667초 marker보다 0.667초 빠르다. 서버 타격 예약과 로컬 공격 표식이 서로 다른 occurrence를 가리킬 수 있으므로 `GameSystemRules_UnitCombatSynchronization.md`의 `NET-PRESENT-002` 계약을 충족하지 못한다.

### 2-4. 표현 프로필 상태

- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs:62~68`
  - 타겟 고정, 근접 접촉 전달, 타이머 타격+보조 결과, Impact 1개, 보조 결과 있음으로 의미는 이미 구분되어 있다.
  - 그러나 지원 상태는 `Unresolved`, 사유는 `default-marker-placeholder-ground-impact-provisional`이다.
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs:3757~3774`
  - 현재 manifest 기대값은 Supported 15 / Unresolved 9 / N/A 1이다.
  - QuakeSpirit을 명시적 `Unresolved`로 요구하는 회귀 조건이 남아 있다.

타임라인을 1.667초로 정렬하면 QuakeSpirit은 `MeleeContact`, Impact 1개, 보조 결과 있음이라는 기존 의미를 유지한 채 `Supported`로 승격할 수 있다. 그 결과 manifest는 **Supported 16 / Unresolved 8 / N/A 1**이 된다.

### 2-5. 피해와 범위 공격은 이미 구현됨

- `Assets/_Project/Scripts/Application/Combat/SpecialAttackRegistry.cs:55~57`
  - QuakeSpirit은 `QuakeAttackBehavior`에 등록돼 있다.
- `Assets/_Project/Scripts/Infrastructure/Config/SpecialAttackConfig.cs:102~111`
  - 반경 1.0, 스플래시 비율 0.5 설정이 존재한다.
- `GameSystemRules_Units.md` 규칙 43
  - 주대상 직접 피해 100%, 주대상 제외 반경 내 적 유닛·적 건물 스플래시 50%, 아군 제외, 서버 권위라는 현재 계약을 정의한다.

이번 작업에서 이 피해 공식·대상 수집·방어력 적용·HP writer를 변경할 근거는 없다.

## 3. 원인 가설과 판정

| 우선순위 | 가설 | 증거와 판정 |
|---|---|---|
| 1 | 런타임 타격 시점 1.0초가 과거 placeholder로 남아 실제 marker 1.667초와 어긋난다 | clip 이벤트와 전용 미리보기는 1.667초이고 config만 1.0초다. **채택** |
| 2 | QuakeSpirit을 `Unresolved`로 고정하는 resolver와 self-validation 기대값이 정식 프로필 전환을 막는다 | resolver 사유와 검증 조건이 과거 placeholder를 명시한다. **채택** |
| 3 | Blue/Red 프리팹이 서로 다른 Controller나 clip을 사용한다 | 두 프리팹이 동일 Controller GUID를 사용하고 Attack 상태도 동일 clip GUID다. **배제** |
| 4 | Quake 특수 AoE 자체가 미구현이라 보조 결과를 지원할 수 없다 | 전용 handler, registry, 반경/비율 설정과 규칙 43이 모두 존재한다. **배제** |
| 5 | 공통 C2/C3 결과 또는 피해 writer 결함 때문에 타격 시점이 어긋난다 | 현재 증거는 Quake 고유 타임라인·프로필 불일치에 한정되며 공통 경로 결함 증거가 없다. **이번 범위에서 배제** |

## 4. 적용해야 할 규칙

- `GameSystemRules_Units.md` 규칙 17: 공격 상태는 Acquire/Align/Windup/Impact/Recovery 의미를 지킨다.
- `GameSystemRules_Units.md` 규칙 18: 실제 피해 판정과 적용은 서버 권위 타이머가 담당한다.
- `GameSystemRules_Units.md` 규칙 26: 범위 공격 결과는 같은 권위 Impact의 완결 결과로 표현한다.
- `GameSystemRules_Units.md` 규칙 27: Animation Event는 표현 marker이며 런타임 타격 프레임과 일치해야 한다.
- `GameSystemRules_Units.md` 규칙 43: QuakeSpirit 직접 100% + 적 유닛/건물 스플래시 50% 의미를 보존한다.
- `GameSystemRules_UnitCombatSynchronization.md` `NET-PRESENT-001`: Animation Event는 VFX/SFX 등 로컬 표현용이며 피해 writer가 아니다.
- 같은 문서 `NET-PRESENT-002`: 검증된 AttackTimeline이 정규 원본이고 완성 유닛의 Animation Event와 허용 오차 안에서 일치해야 한다.
- 같은 문서 `NET-PRESENT-003`: AoE 결과는 정규 결과 키와 완결 묶음으로 같은 표현 시각에 재생한다.

## 5. 작업 범위

### 포함

- QuakeSpirit 런타임 타격 시점 1.0초를 실제 marker 1.667초로 정렬
- QuakeSpirit profile을 `Supported`로 승격
- 기존 의미인 타겟 고정, 근접 접촉, Impact 1개, 보조 결과 있음을 유지
- self-validation의 Quake fixture와 25종 support partition 기대값 갱신
- 변경 후 Unity 메뉴 2개의 PASS 확인
- Android Build And Run 시작 후 사용자가 실기기로 Quake 공격 시점·AoE·회귀를 확인

### 비범위

- 정상 C2/C3 Coordinator, 결과 전송, 피해 writer, HP 동기화 경로 변경
- Quake 직접 피해·스플래시 반경·비율·대상 수집의 밸런스 변경
- Attack clip 재편집, 새 VFX/SFX 제작, Animator 상태 구조 변경
- 다른 `Unresolved` 유닛의 일괄 승격
- Legacy rollback 제거 또는 ActionSequence 전체 완료 선언

## 6. 검증 경계

- 2026-09-14 Quake 전용 1회성 Unity 셋업 메뉴가 PASS했고 `UnitStatsConfig`의 QuakeSpirit 타격 시점이 1.0초에서 1.667초로 저장됐다. 같은 실행에서 `OnAttackHit=1.667`, Controller의 실제 Attack clip, Blue/Red 프리팹 연결도 검증됐다.
- `Hexiege → Combat → Run Unit Action Self Validation`은 A1/B2/C2/C3 전체와 errors 0으로 PASS했다.
- 정확한 두 번째 메뉴 경로인 `Hexiege → Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit`도 errors 0으로 PASS했다.
- Android Build And Run은 2026-09-14 01:20경 `Starting Android build` 화면까지 확인했다. 이 문장은 당시 시점의 기록이며, 이후 최신 빌드가 설치·실행된 focused 멀티플레이 로그를 확보했다.
- 정적 코드 검사와 Unity self-validation PASS는 **구조·회귀 게이트**다. Android 빌드 시작은 실기 PASS가 아니다.
- 공격·피해·표현 전달·이동의 로그 축은 후속 focused 경기에서 확인했다. 다만 화면의 정확한 내려치기 프레임, 아군·범위 밖 제외, 체감 장시간 정지 없음과 비교 유닛의 육안 회귀는 사용자가 확인해야 한다.
- 이번 focused 결과를 25종 전체, Host/Client 역할교대, Legacy rollback 완료로 일반화하지 않는다.

## 7. 구현 후 현재 상태 (2026-09-14)

- `UnitStatsConfig`의 QuakeSpirit `hitFrameTimes`는 `[1.667]`이다.
- QuakeSpirit resolver는 `Supported / MeleeContact / Impact 1 / secondary true`다.
- 25종 manifest는 `Supported 16 / Unresolved 8 / N/A 1`이다.
- production asset 연결과 marker/config 일치를 영구 Unit Action self-validation에서 fail-closed로 검사한다.
- 실행에 사용한 1회성 셋업 스크립트와 `.meta`는 저장 검증 뒤 제거됐다.
- 최종 판정은 **자동·focused 멀티 로그 게이트 CONDITIONAL PASS / 육안 확인 대기(OPEN)**다.

## 8. Focused Android Host/Client 실기 로그 결과 (2026-09-14)

같은 경기의 Editor Host와 Android Client 로그를 `sharedSessionKey=9d43b8e44ec8ee1151f6ca8c1d031ee92b3390228067e6ee47327796d6eb3d75`로 결합했다. 이번 세션은 LittleKnight, Pistoleer, QuakeSpirit, SpearMan **4/25종**만 포함하므로 전체 roster·역할교대·Legacy rollback 완료로 확대하지 않는다.

- QuakeSpirit 10기(UnitId 5, 12, 16, 17, 24, 28, 30, 47, 110, 113)가 생산됐고 manifest에서 `Supported / MeleeContact / Impact 1`로 기록됐다.
- 첫 QuakeSpirit Unit 5는 `AwaitingStationarySample → Ready` 뒤 sequence 1을 stationary true, yaw 0°로 Accepted했다. 이후 yaw 22.079°의 `Misaligned`는 정렬 전 정상 보류이며 전체 `productionGateInvalid=0`이다.
- Host 공격은 commits 596, serverResults 770/failures 0이다. 양쪽 coordinator는 schedules 806, results/ready 952, pending/failures/duplicates 0, bundles 770/released 770, `aoeCompleteManifest=true`로 수렴했다. Client는 결과 770건을 전부 수락하고 거부 0이다.
- Host/Client 모두 required visual 909건을 909건 방출했고 `viewUnavailable`, duplicate, transport, presentation failure가 0이다. 공간 표현 mismatch는 0이며 최대 오차는 Host 0.089, Client 0.146으로 허용치 1.5 이하다.
- 같은 타격 결과에서 주대상 200과 50% 스플래시 100이 함께 확인됐다. 02:55:56에는 Building 9가 200, Building 8이 100을 받았고, 02:55:59에는 Unit 26이 200, Unit 27과 Building 9가 각각 100을 받았다.
- Host 이동은 165,383 frame 동안 rejected/invalid/gate/writer/handoff/stationary-Walk/failure/drop 0, 공간 계획/커밋 1,118/1,118이다. recoverable repath 5건은 repeated 0/fatal 0으로 닫혔다. QuakeSpirit은 AStar, Chase, PendingRepath, PostCombatResume를 모두 포함했고 BlockedRepath는 4 frame뿐이다.
- Client 복제 932건은 invalid/duplicate/revision/conflict/gap/order violation 0이며 presentation frame 94,825와 attack presentation start 260을 기록했다.
- ROOT는 Host endpoint 44, Client endpoint 41로 양쪽 PASS, errors/drop 0이다.
- 최신 Android PID 24233에서 UnitView 초기화 지연 123건은 완료 123건과 1:1이며 최종 `viewUnavailable=0`, 재시도 실패·소진 0이다. GameLog ERROR/FATAL과 NullReference는 0이고 경기 종료 Winner Red 뒤 disconnect 경고는 정상 teardown이다.
- 최신 실행의 U+27F3 경고는 0건이다. 파일에 남은 2건은 2026-09-13 PID 13509의 구 APK 기록이다.

### 판정 경계

- **로그 PASS:** 공격 결과 수렴, 직접 200·스플래시 100 동시 결과, AoE 묶음 완결, 표현 전달, 이동 권위·복제, ROOT, 최신 실행의 U+27F3 경고 미발생.
- **육안 OPEN:** 정확한 내려치기 화면 프레임, 아군·범위 밖 제외, 체감 장시간 정지 없음, 기존 비교 유닛의 화면상 공격·표현 회귀.
- **전체 판정:** **CONDITIONAL PASS / OPEN**.
