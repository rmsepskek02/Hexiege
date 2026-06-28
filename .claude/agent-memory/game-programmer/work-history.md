# Game Programmer — 작업 이력 (상세)

MEMORY.md의 최근 작업 요약에서 더 오래된/상세한 작업 기록을 보관하는 파일. 날짜 역순.

---

## GameBootstrapper.Setup.cs 하드코딩 배열 파생 (2026-06-25) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-25/07_23_하드코딩배열-파생/`
**커밋**: `8d74e06` (main)

환불 캐시 초기화에 쓰이던 하드코딩 건물 목록 배열 2개를 `BuildingTypeHelper` 공개 API 파생으로 교체. 동작/환불 값 불변, 구조만 단일 소스로 통합. 수정 파일 1개(`GameBootstrapper.Setup.cs`).

- **`stage1Buildings`**: 1단계 생산건물 9개 하드코딩 배열 → `Array.FindAll((BuildingType[])Enum.GetValues(typeof(BuildingType)), t => BuildingTypeHelper.GetStage(t) == 1)` 파생
- **`nonProductionBuildings`**: 비생산 건물 6개(Castle 제외) 하드코딩 배열 → `Array.FindAll(..., t => !BuildingTypeHelper.IsProductionBuilding(t) && t != BuildingType.Castle)` 파생
- `using System;` 추가(Enum/Array 짧은 이름 사용). 환불 캐시 foreach 루프(단계 체인 순회 + 비생산 누적)는 변경 없음 — 변수명·타입(`BuildingType[]`) 동일 유지로 그대로 동작
- **효과**: 신규 생산건물 추가 시 `BuildingTypeHelper._buildingTable` 한 줄 추가만으로 환불 캐시 목록까지 자동 반영(Setup.cs 무수정). Phase 2 lookup table 통합의 연장선 — 건물 목록 단일 소스화 완성
- **선택지**: 안 1(BuildingTypeHelper에 목록 조회 공개 메서드 추가) vs 안 2(Setup.cs에서 기존 공개 API로 직접 파생). 사용자가 **안 2(도메인 레이어 무변경)** 선택, 곧바로 교체(주석 처리 단계 없음 — 파생 결과가 기존 배열과 값 동치이고 환불 계산이 순서 무관임을 Research에서 검증)
- **테스트**: 사용자 실기 PASS(2026-06-25) — 생산 건물/비생산 건물 철거 환불 금액 변경 전과 동일하게 정상 표시

---

## 코드 구조 개선 Phase 2 (2026-06-25) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-23/15_37_구조개선-Phase2/`
**브랜치**: `claude/code-refactor-phase2-structural` (커밋 3838c4d)

동작 보존 리팩토링(입출력/동작은 변경 전과 동일, 구조만 정리). 수정 파일 2개.

**① BuildingTypeHelper switch → Dictionary lookup table**
- `BuildingTypeHelper.cs`(Domain): IsProductionBuilding/GetStage/GetNextStage 3개 메서드가 각각 동일 건물 목록을 switch로 중복 나열하던 것을 단일 `Dictionary<BuildingType, BuildingMeta>`(`_buildingTable`)로 통합
- `BuildingMeta` private readonly struct: IsProduction(bool)/Stage(int)/NextStage(BuildingType?). 값 타입이라 Dictionary 값 저장에 추가 힙 할당 없음
- 세 메서드는 `_buildingTable.TryGetValue` 단순 조회. 미등록 = 비생산(false/0/null). 비생산 7종은 table 미등록으로 처리(default 동작 일원화)
- 신규 생산건물 추가 시 table 한 줄만 추가 → 세 질문(생산여부/단계/다음단계) 자동 정합. 한 곳 누락으로 인한 데이터 불일치 위험 제거
- CanUpgrade/CanShowActionPanel은 위 메서드 호출 기반 → 무수정 자동 반영. 시그니처/반환타입/네임스페이스 동일 → 호출부 무영향
- PrimalSanctuary(동물A 3단계): 초기 Research에서 IsProductionBuilding 누락 의심했으나 qa-tester 정적분석 결과 기존 switch에도 포함돼 있었음(오판). table에 `(true, 3)` 명시하여 동작 보존

**② HexMetrics 중복 setup 제거**
- `GameBootstrapper.Network.cs` StartNetworkGame: HexMetrics 수동 설정 4줄(Orientation/Context/TileWidth/TileHeight) → `ApplyConfig(HexOrientation.FlatTop, oc)` 1줄로 대체
- ApplyConfig는 UnitYOffset까지 설정 → 수동 4줄에 빠져 있던 UnitYOffset 부분중복(partial dup) 해소
- ApplyConfig 멱등: 멀티 경로에서 StartNetworkGame 1회 + LoadMap 내부 1회 = 2회 실행되나 같은 값 재대입이라 부작용 없음(변경 전에도 동일 값 2회 설정 상태였음)
- 순서 제약 유지: ApplyConfig(FlatTop) → GridCenter → ViewConverter.Setup → LoadMap. 싱글 경로는 미변경

**테스트**: SINGLE-TC-01~07 + MULTI-TC-01~02 전 항목 사용자 실기 PASS(2026-06-25)
**보존 코드**: 기존 switch 3개 본문 + 수동 4줄은 주석 처리로 보존 중. 사용자 지침상 별도 삭제 지시 있을 때만 제거(현재 보존)

---

## 코드 정리(클린업) Phase 1 (2026-06-23)
- 약 30개 파일에서 히스토리성 주석(`[2026-XX-XX]`/`[Phase X]` 라벨) + 구방식→현재방식 전환 설명 주석 제거
- 폐기 코드 제거: `GameBootstrapper.cs` `_enableAI` 블록(주석 처리 코드+메모), `_confirmPopup` 전환 설명 블록 / `NetworkGameFlow.cs` 빈 섹션 헤더
- `GameBootstrapper.Setup.cs`: 환불 캐시 계산에서 두 번 새로 만들던 `new[] { RaceId.Human, RaceId.Spirit, RaceId.Transcendence }` → `refundRaces` 지역 변수 1개로 통합. 원소·순서 동일 → 동작 보장
- 순수 주석/폐기코드 정리로 런타임 동작 불변. 구조 변경(switch→Dictionary 등)은 Phase 2 별도 진행
- 클린업 원칙: 단독 이력 주석은 줄 통째 제거, WHY 설명 본문은 보존(날짜 토큰만 제거)

---

## 스플래시 화면 로그인 흐름 개선 — skipFade 모드 (2026-06-23) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-23/07_20_splash-login-flow/`

**문제**: 로그인된 상태로 재실행 시 탭 → FadeOut(0.5초) → 이 동안 Login 씬 배경 노출. 스플래시가 투명해지는 동안 로그인 화면이 보여 어색한 전환 발생.

**해결**: `SplashOverlayView`에 `_skipFadeOnTap` bool 필드 추가. `SetTapCallback` 두 번째 파라미터 `skipFade=false`(기본값) 추가 — 기존 로그인 X 호출 코드 변경 없음. `OnPointerClick` skipFade 분기: true면 FadeOut 없이 즉시 `_tapCallback` 호출, false면 기존 `FadeOut(_tapCallback)`.

**LoginBootstrapper 자동 로그인 성공 분기 변경**:
```csharp
_splashOverlay.SetTapCallback(GoToNextScene, skipFade: true);
UIManager.Instance?.ShowLoading(false);
_splashOverlay.ShowTapToStart();
```
로딩 인디케이터(SortingOrder=300)가 탭 직후 즉시 화면 커버 → FadeOut 없어도 Login 씬 배경 미노출.

**흐름 비교**:
- 로그인 O: 탭 → 즉시 GoToNextScene → SceneLoader.Load("Lobby") → 로딩 인디케이터(SO=300) → 로비
- 로그인 X: 탭 → FadeOut(0.5초, 로그인 화면 드러남) → ShowLoginSelect (기존 동작 유지)

**수정 파일**:
- `Presentation/UI/SplashOverlayView.cs` — `_skipFadeOnTap` 필드, `SetTapCallback(callback, bool skipFade=false)`, `OnPointerClick` 분기
- `Bootstrap/LoginBootstrapper.cs` — 자동 로그인 성공 분기 `SetTapCallback(..., skipFade:true)` + `ShowLoading(false)` + `ShowTapToStart()`

---

## 로그인 팝업 CloseButton 무반응 수정 (2026-06-23) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-23/04_49_cancel-button-fix/`

**원인 패턴**: CloseButton GO가 씬에 활성화 상태로 존재해도 C# 코드에 `[SerializeField] private Button _closeButton` 필드가 없으면 Inspector 연결 자체가 불가 → 클릭 리스너 등록 안 됨 → 무반응.

**수정 내용**:
- `AnonymousWarningPopup.cs`: `_closeButton` 추가 + `OnCloseButtonClicked()` → `Hide()`. `SetInteractable()`에 포함 (로그인 진행 중 취소 방지).
- `NetworkErrorPopup.cs`: `_closeButton` 추가 + `OnCloseButtonClicked()` → `Hide()`. 기존 `_confirmButton`(ConfirmButton GO)은 유지.

**씬 구조 확인 패턴**: Login.unity 내 팝업 Inspector 연결 상태는 씬 파일에서 MonoBehaviour 섹션의 SerializeField 값(`{fileID: 0}` = 미연결)으로 확인 가능.

---

## AI 시나리오 ScriptableObject 종족별 재구조화 (2026-06-10) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-10/01_06_ai-scenario-scriptableobject-restructure/`

**핵심**: `DifficultyLevel`(enum) / `BuildOrderStep`(struct) / `AIActionType`(enum)을 Infrastructure → **Domain 레이어로 이동**.
- `Domain/AI/DifficultyLevel.cs`, `Domain/AI/BuildOrderStep.cs` (AIActionType 포함) 신규
- Domain은 UnityEngine 참조 금지 → BuildOrderStep에서 [Tooltip]/[Header] 제거, [Serializable](System)만 유지
- Infrastructure(`AIScenarioConfig.cs`/`LocalPlayerDifficulty.cs`/`AIConfig.cs`)는 중복 정의 삭제 후 `using Hexiege.Domain;`로 참조
- 참조 파일 전부(AIOpponentController/BattleViewModel/DifficultySelectView)에 `using Hexiege.Domain;` 확인. AIOpponentController는 DifficultyParams/GameRaceContext 때문에 `using Hexiege.Infrastructure;` 유지

**시나리오 에셋 구조 변경**: 종족당 단일 에셋 + 3시나리오 묶음.
- 레거시 `AIScenarioConfig_Human_A/B/C.asset` 폐기 → `AIScenarioConfig_{Human|Spirit|Transcendence}.asset` (각 `scenarios[0/1/2]` ScenarioBundle 배열)
- `GameBootstrapper.Setup.cs` `LoadScenarioBundleForRace()`: `GameRaceContext.RedRace` 기반 switch로 종족별 경로 결정 후 `Random.Range`로 1개 선택. (구 `LoadRandomHumanScenario` 제거됨)
- 타이밍: `GameRaceContext.Set`이 `InitializeAI`보다 먼저 실행되어 RedRace 확정 보장
- `AIScenarioConfig.cs`는 레거시 호환용 `scenarioName`/`_steps` 필드를 아직 보유(향후 제거 가능)

---

## 사운드 시스템 (AudioManager + SFX/BGM 분리) (2026-06-10) 🔵 코드 완료 / Inspector + 실기 예정

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-10/09_28_sound-system/` / 브랜치 `claude/sound-system-review-itwt0t`

**핵심 설계**: EffectManager=VFX 전용, AudioManager=BGM+SFX 전담 (GameSystemRules_Sound 규칙 1). VFX+SFX 쌍은 같은 호출지점에서 두 매니저 연달아 호출(규칙 15).

**신규 파일**:
- `Infrastructure/Config/SoundConfig.cs` — BGM 4종(login/lobby/battle/gameEnd)+crossfadeDuration, UnitSoundEntry/BuildingSoundEntry List→Dict 캐싱. Domain 타입(UnitType/BuildingType)만 키. **이전 세션에서 이미 생성됨**.
- `Presentation/Audio/AudioManager.cs` — `SingletonMonoBehaviour<AudioManager>`(DontDestroyOnLoad). `enum BgmType{Login,Lobby,Battle,GameEnd}` + `struct UiSoundEntry`(UI SFX는 AudioManager 직접 보유, 규칙 4). BGM 크로스페이드(AudioSource A/B 번갈아, unscaledDeltaTime), SFX 풀(동시 8개, spatialBlend=0), 볼륨 PlayerPrefs(0~1→`Log10(Max(v,0.0001))*20` dB). Awake에서 activeSceneChanged+OnGameStarted+OnGameEnd 구독, Initialize()에서 현재 씬 BGM 즉시 재생.

**수정 파일 (SFX 코드 주석 비활성화 — "SOUND_SYSTEM_REFACTOR" 마커, 실기 PASS 후 삭제)**:
- `EffectPreset.cs` — _sfxClip/_sfxVolume 주석. VfxPrefab만 활성.
- `EffectManager.cs` — SFX 풀/Play SFX 블록 전부 주석. `using System.Collections;` 잔존(무해).
- `UnitView.cs` — OnAttackHit/OnUnitDied에 `AudioManager.Instance?.PlayUnitAttackSfx/PlayUnitDeathSfx` 추가(VFX 호출 바로 아래).
- `NetworkUnit.cs` — OnNetworkDespawn 클라 사망에 PlayUnitDeathSfx 추가.
- `LoginBootstrapper.cs` — `_soundConfig` + Start() 맨앞 `AudioManager.Instance?.Initialize(_soundConfig)`.
- `InGameSettingsUI.cs` — 볼륨패널/슬라이더3. _soundButton→볼륨패널 토글(CanvasGroup alpha).

**Inspector 작업 필요**: AudioMixer 에셋 생성(Master→BGM/SFX, Exposed Master/BGM/SFXVolume), Login.unity AudioManager+A/B AudioSource+SFX Container, SoundConfig.asset, InGameSettingsUI 슬라이더, EffectPreset SfxClip → SoundConfig 이전.

---

## 싱글플레이 AI 시스템 Phase 1~5 + UI (2026-06-07) 🔵 코드 완료 / AI 시나리오 후 실기 예정

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-07/16_40_ai-system-implementation/`

**신규 파일**:
- `Infrastructure/LocalPlayerDifficulty.cs` — DifficultyLevel enum(Easy/Normal/Hard) + 정적 홀더(LocalPlayerRace 동일)
- `Infrastructure/Config/AIConfig.cs` — DifficultyParams 중첩 × Easy/Normal/Hard ScriptableObject + `public bool enableAI = true`
- `Infrastructure/Config/AIScenarioConfig.cs` — BuildOrderStep 플랫 리스트 ScriptableObject
- `Application/Services/AIOpponentController.cs` — Tick() 기반. 빌드오더(Phase 1~4), 반응 시스템(R1 유닛열세/R2 골드과잉/R3 채굴소 파괴), BFS 건물 배치, MiningPost 병행 트랙
- `Presentation/UI/Views/Lobby/Battle/DifficultySelectView.cs` — SingleplayDifficulty 상태 시 표시
- `Assets/Editor/AIConfigSetup.cs`, `Assets/Editor/FixDifficultySelectViewLayout.cs`

**수정 파일**:
- `GameEvents.cs` — `UnitProducedEvent`에 `BarracksId` 필드 추가 (AI 콜백 연속 생산)
- `ResourceUseCase.cs` — `SetIncomeMultiplier(TeamId, float)` + `_incomeMultipliers Dictionary`. TickTeamIncome 배율 적용
- `GameBootstrapper.cs`/`.Setup.cs`/`.Map.cs` — `_enableAI` 주석(AIConfig.enableAI로 이전), `InitializeAI()` 로드/조기반환/난이도/시나리오 랜덤 선택, `if (!NetworkContext.IsNetworkActive) InitializeAI()`
- `BattleViewModel.cs` — SingleplayDifficulty 상태, CmdSelectDifficulty Subject, NavigateBack 케이스
- `BattleRootView.cs` — `_difficultySelectView` Bind/Unbind

**AI 설계 핵심**:
- **콜백 기반 연속 생산**: `StartProduction` 시 `_lineProduction[barracksId]=unitType` 기록 + 시드 1회 `EnqueueUnit` → `OnUnitProduced` 구독에서 Red팀 생산 시 해당 배럭 재호출 (자동생산 미사용, 규칙 23)
- **BFS 배치**: Red 성채 BFS → walkable + Red 소유 + 기존 생산 건물 인접 6타일 제외 → 최근접
- **MiningPost 병행 트랙**: Phase 2/4 진입 시 활성화 → 미점령 광산 타겟 → SetRallyPoint → mineCheckInterval 주기 PlaceMiningPost → 성공 시 ClearRallyPoint + 종료

---

## 전체 유닛 사망 VFX 적용 (2026-06-08) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-08/19_08_unit-death-vfx/`

**배경**: EffectManager.PlayUnitDeath()는 UnitEffectConfig.GetDeath(type) preset null이면 즉시 반환. Pistoleer만 연결, 나머지 23종 null. 코드 흐름은 이미 정상.

**해결**: 코드 변경 없음 — 에셋 작업만.
- `EffectPreset_Unit_Death_Common.asset` 신규 (vfx_unit_death.prefab + 사망 SFX)
- `SetUnitDeathVfxAll.cs` (메뉴 `Hexiege/Setup/Set Unit Death VFX (All Units)`) — GUID 기반 로드 → EffectPreset 생성 → 24종 deathPreset 일괄 연결
- 기존 `EffectPreset_Pistoleer_Death.asset` 삭제

**에디터 스크립트 패턴**: `CreateInstance<T>()` → `CreateAsset()` → `SerializedObject`로 private 필드 설정. `GUIDToAssetPath(guid)` → `LoadAssetAtPath<T>()` (파일 이동/이름변경에 안정적).

---

## 유닛 VFX 디테일 개선 (2026-06-08) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-08/14_44_vfx-scaling-mode-fix/`

- **작업 1 ScalingMode**: 유닛 VFX 프리팹 3개 ParticleSystem `scalingMode: 1`(Local) → `0`(Hierarchy). 1회성 `VfxScalingModeFixer.cs`. 이후 루트 Transform Scale로 전체 크기 조절 가능.
- **작업 2 VfxSpawnPoint**: `UnitView._vfxSpawnPoint` 추가. `OnAttackHit()` 위치=`_vfxSpawnPoint.position`, 회전=`Quaternion.LookRotation(transform.forward)`. `EffectManager.PlayUnitAttack(UnitType, Vector3, Quaternion)`로 확장.
  - **핵심 교훈**: VfxSpawnPoint가 스켈레톤 본 하위면 `.rotation`에 본 회전(약 0,-90,-90)이 섞임 → 위치는 그대로, **회전은 반드시 `Quaternion.LookRotation(transform.forward)`로 대체**.
- **작업 3 퍼짐 제거**: vfx_unit_death 3개 PS `startSpeed`=0 (YAML 직접 수정).

---

## NetworkGameManager 고아 필드 + Game씬 NGM 제거 (2026-06-06) ✅ 완료 (싱글+멀티 PASS)

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-06/00_08_networkgamemanager-cleanup/`

- `GameBootstrapper.cs` — `_networkGameManager` SerializeField 3줄 제거 (고아)
- `RemoveGameSceneNGM.cs` — Game씬 NGM 제거 1회성 (실행 완료)

**근본 원인**: NGM은 Lobby에서 생성 후 DontDestroyOnLoad 유지인데 Game.unity에도 별도 배치 → 인스턴스 중복.

**Editor 씬 NGM 제거 패턴**: `FindObjectsByType<T>(FindObjectsInactive.Include, ...)` + `go.scene.name == "Game"` 필터 + Additive 임시 로드 → `Undo.DestroyObjectImmediate` → `SaveScene` → CloseScene.

**교훈**: DontDestroyOnLoad 오브젝트는 생성 씬 하나에만 배치.

---

## 신규 유닛 프리팹 컴포넌트 자동 부착 (2026-06-05) ✅ 스크립트 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-05/22_14_unit-prefab-component-setup/` / `Assets/Editor/Setup/SetupNewUnitPrefabs.cs` (메뉴 `Hexiege/Setup/신규 유닛 컴포넌트 부착`)

**배경**: 32개 신규 유닛 프리팹. Root에 UnitView/NetworkObject/NetworkTransform/NetworkUnit, _Mesh 자식에 AnimationEventRelay.

**패턴**: `LoadPrefabContents` → 처리 → `SaveAsPrefabAsset` → `UnloadPrefabContents`. Inspector 값은 `SerializedObject.FindProperty` → `ApplyModifiedPropertiesWithoutUndo()`. `GetComponent == null` 후 AddComponent(멱등). `_Mesh` 키워드로 직속 자식 탐색.

---

## BuildingView Missing Script 정리 (2026-06-05) ✅ 완료 (PASS)

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-05/22_42_missing-buildingview-script/` / `Assets/Editor/RemoveMissingScripts.cs`

**원인**: `BuildingView`(GUID `c178b6f3e086351409b946635cbfae71`) 삭제됐으나 Spirit/Transcendence 프리팹 8개에 Missing 참조 잔존(ManaRift/SpiritNexus/ElderTree/FungalNode × Blue/Red).

**해결**: `LoadPrefabContents` + `GameObjectUtility.RemoveMonoBehavioursWithMissingScript`. 교훈: 스크립트 삭제 시 부착됐던 모든 프리팹 Missing 참조 함께 정리.

---

## 방어 타워(AutoTower) 공격 기능 구현 (2026-06-01) ✅ 완료 (PASS)

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-01/05_01_defense-tower-implementation/` / 신규 `Application/UseCases/TowerCombatUseCase.cs`

- `Tick(float dt)`: AutoTower 순회 → 쿨다운 감소 → 0 이하 시 적 탐색 → 데미지
- 타겟: `Vector3.Distance` 가장 가까운 적 유닛(건물 제외)
- 멀티 가드: `NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer`이면 조기 반환
- **팀→종족 변환**: Domain에서 GameRaceContext 직접 참조 불가 → `Func<TeamId, RaceId>` 델리게이트 주입

## Human CannonTower 초기 방향 설정 (2026-06-02) ✅ 완료 (PASS)

**수정**: `BuildingFactory.GetInitialRotation(race, type, team)` — Human+AutoTower만 분기. `ViewConverter.IsFlipped`로 로컬 팀 판별. 내 포탑 `Quaternion.identity` / 상대 포탑 `Quaternion.Euler(0,180,0)`. 원칙: 팀 색깔이 아닌 "내 진영 vs 상대 진영" 기준.

## UnitStatsConfig 미사용 필드 제거 (2026-06-02) ✅ 완료
- `AttackKind` enum, `StatValues.Kind`, `GetAttackKind()`, `attackKind`, `occupancySize` 제거. 미사용 필드 확인 시 코드베이스 전체 Grep 필수.

---

## 자동생산 재등록 슬롯 버그 구조 개선 (2026-06-05) ✅ 완료 (PASS)

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-05/11_31_auto-unregister-currentisauto-fix/`

**근본 원인**: `CurrentIsAuto`가 수동 관리 필드 → 자동 해제 시 reset 누락 → `TryConvertCurrentToAuto` 잘못 거부 → 슬롯 중복/누락.

**구조 개선** (`Domain/Building/ProductionState.cs`): `IsAutoMode`와 동일. backing field + 파생 getter:
```csharp
get => _currentIsAutoFlag && CurrentProducing.HasValue && AutoTypes.Contains(CurrentProducing.Value);
```
**`PendingQueue.Count == 0` 조건**: `TryConvertCurrentToAuto`는 큐가 빌 때만 허용. GameSystemRules 규칙 20. setter 호환 유지(`_currentIsAutoFlag` 갱신만).

## 자동생산 완료 사이클 슬롯2 깜빡임 수정 (2026-06-05) ✅ 완료 (PASS)

**수정**: `UnitProductionUseCase.CompleteProduction` — `ChargeVisibleSlots`+`OnProductionQueueChanged` 직접 발행 제거 → `OnUnitProduced` 후 즉시 `TryStartNext(state)`. fallback: `!CurrentProducing.HasValue`이면 이벤트 수동 발행. AddNewAutoSlot 2026-04-19 수정과 동일 패턴.

---

## 건물 업그레이드 생산 상태 처리 오류 수정 (2026-05-31) ✅ 완료 (PASS)

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-31/21_03_upgrade-production-state-fix/` / `Presentation/Production/ProductionTicker.cs` `OnBuildingUpgraded()`

- **버그1 (환불 누락)**: `UnregisterBarracks(oldId)`는 `Remove()`만 → 환불 없음. `CancelAllQueue(oldId)`로 교체(CurrentProducing 환불 + PendingQueue IsCharged=true 환불 + UnregisterBarracks). 근거: 건물 철거 규칙 5.
- **버그2 (랠리포인트 초기화)**: `RegisterBarracks(newBuilding)`이 새 빈 상태 생성 → RallyPoint 유실. `CancelAllQueue` **전에** `GetState(oldId)?.RallyPoint` 저장 → 후 `SetRallyPoint(newId, saved)` 복원.
- **수정 순서(바꾸면 재발)**: ① savedRallyPoint 저장 ② CancelAllQueue ③ RegisterBarracks ④ SetRallyPoint 복원

---

## 코드 리팩토링 Group 3/5/6 완료 (2026-05-20) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-19/10_46_code-refactoring/Plan.md`

**Group 3 (레이어 의존 제거)**:
- A: NetworkContext 교체 — ProductionTicker/GameEndUI `NetworkManager.Singleton` 직접 호출 제거
- B: NGM 주입화 — LobbyUI/NetworkStatusUI `OnClientConnectedCallback` 직접 구독 제거. NGM에 `OnAllPlayersReady(int)`/`OnServerDisconnected`/`GetCurrentRttMs()`/`IsNetworkRunning`/`ShutdownNetwork()` 추가
- D: ServerRpc 래퍼화 — BuildingPanelBase/BuildingPlacementUI/ProductionPanelUI `*ServerRpc` 직접 호출 제거. NetworkBuildingController에 RequestBuild/RequestDemolish/RequestUpgrade, NetworkProductionController에 RequestEnqueue/RequestCancelSlot/RequestSetRallyPoint/RequestToggleAuto 추가
- E(Combat): NetworkCombatController가 UnitView GetComponent → GameEvents 발행. OnNetworkCombatStarted/TargetChanged/Stopped/OnNetworkWalkStarted 4개 신규
- E(GameEnd): NetworkGameEndController가 GameEndUI/RematchRequestPopup/GameUIManager 직접 호출 → GameEvents 발행. **IForfeitService 인터페이스 신규** (`Application/Interfaces/IForfeitService.cs`) — GameEndUseCase(싱글)/NetworkGameEndController(멀티) 구현. GameBootstrapper.Map.cs에서 `NetworkContext.IsNetworkActive` 분기 주입.

**Group 5 (O(n) 캐시화)**:
- UnitSpawnUseCase `_unitsByPosition: Dictionary<HexCoord, List<UnitData>>` + `NotifyUnitMoved(unit, from, to)` (UnitMovementUseCase.ProcessStep이 호출)
- BuildingPlacementUseCase `_buildingsByPosition` → GetBuildingAt O(1)
- HexGrid `_ownedTileCounts: Dictionary<TeamId, int>` → CountTilesOwnedBy O(187)→O(1)
- PopulationUseCase `_usedPopulationByTeam` + 이벤트 구독 증감. IDisposable, ClearAll에서 Dispose

**Group 6**:
- 6-1: BuildingType enum 0~31 명시값(순서 보존)
- 6-2: UnitData/BuildingData `:this(...)` 위임
- 6-7: OnUnitEnteredTile `Action` → `Subject<UnitEnteredTileEvent>`
- 6-8: GameEvents.OnToastRequested 신규, ToastUI 구독. NetworkBuildingController/NetworkProductionController reason→ToastKey 매핑 발행
- 6-13: GameBootstrapper.IsNetworkMode → NetworkContext.IsNetworkActive 단일화
- 6-15: ToastKey Presentation→Application/Events 이동. IUnitView 인터페이스 신규(`Application/Interfaces/IUnitView.cs`). UnitFactory `GetComponent<IUnitView>()` 사용, `using Hexiege.Presentation` 제거

**검증**: Presentation `using Unity.Netcode` 0건, Infrastructure `using Hexiege.Presentation` 0건, ServerRpc 직접 호출 Presentation 0건 (Grep).

**Inspector 수동**: GameEndUI/NetworkStatusUI에 `_networkGameManager` 연결. NetworkGameEndController 빈 슬롯 무관(제거됨).

**미처리**: IUnitView.SetDependencies가 Infrastructure 구체타입 인자 받음 — IUnitFactory/IBuildingFactory 추출은 본 범위 외.

---

## 건물 철거 시스템 (2026-05-18) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-17/18_15_building-demolish/`

- `UnitProductionUseCase.CancelAllQueue(barracksId)` 신규: ① ClearRallyPoint(UnregisterBarracks 이전 필수) ② CurrentProducing 환불 ③ PendingQueue IsCharged=true 환불, false 환불없이 제거 ④ 상태 초기화 ⑤ OnProductionQueueChanged ⑥ UnregisterBarracks
- `BuildingPlacementUseCase.DemolishBuilding(buildingId)` 신규: OnEntityDied 발행 → RemoveBuilding
- `ProductionPanelUI.OnDemolishButtonClick()`: 싱글 CancelAllQueue+AddGold(TotalInvested/2)+DemolishBuilding / 멀티 RequestDemolishServerRpc
- `NetworkBuildingController.RequestDemolishServerRpc`: 소유권+Castle아님+존재 검증 → CancelAllQueue+AddGold+DemolishBuilding → DemolishBuildingClientRpc(`if(IsServer)return`)
- `BuildingFactory.Awake()` OnEntityDied 구독: BuildingData 필터 → `_buildingObjects.TryGetValue` → Destroy
- `BuildingView.cs` 삭제(책임 BuildingFactory로), `MiningEffectView.cs` 삭제(미사용)

**건물 프리팹 구조**: Root GO(Transform ONLY) + Child GO(Mesh). BuildingFactory `_buildingObjects` Dict로 Id→GO 관리. **골드 환불**: `BuildingStats.GetTotalInvestedCost(type, race)/2`. MiningPost 철거 UI는 별도 연기.

---

## 건물 업그레이드 시스템 + 단계별 생산건물 (2026-05-17) ✅ 코드 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-17/02_16_building-upgrade-system/`

- `BuildingType` 단일 Barracks 제거 → 종족별 라인 × 단계(1/2/3) 26종
- `BuildingTypeHelper`(Domain) 신설: IsProductionBuilding/GetStage/GetNextStage/CanUpgrade
- `BuildingData.Stage` 파생 프로퍼티. `BuildingStats.GetUpgradeCost(BuildingType)` — 종족 무관 단일
- `GameEvents.OnBuildingUpgraded`(BuildingUpgradedEvent: OldBuildingId, NewBuilding)
- `BuildingPlacementUseCase.UpgradeBuilding(id, race)` — 기존 제거 후 next stage 교체, 타일 IsWalkable/Owner 유지
- `BuildingFactory.UpgradeBuildingObject` — **새 GO 먼저 생성 → 기존 GO Destroy** (빈 타일 방지)
- `ProductionPanelUI`: BuildingUnitMapping(BuildingType→유닛라인업) Inspector 구조. 단계별 잠금 `_activeUnitLocks[i]`, `_unitLockIndicators`. 잠금 탭 시 `ToastKey.UpgradeRequired`. 업그레이드 버튼 신규
- `NetworkBuildingController.RequestUpgradeServerRpc`/`UpgradeBuildingClientRpc` 신규
- **Barracks→IsProductionBuilding 치환**: UnitProductionUseCase.RegisterBarracks, ProductionTicker.OnBuildingPlaced/OnEntityDied, InputHandler 타일 클릭
- **SetupBuildingStatsConfig.cs**: 1행→24행. 기본값 1단계 30HP/100G/80U, 2단계 45/150/120, 3단계 60/200/0 (Trans HP ×1.6~2)
- **Inspector 전체 완료(2026-05-18)**: BuildingFactory/BuildingPlacementUI/ProductionPanelUI 매핑·잠금·업그레이드, BuildingStatsConfig 32종, ToastMessageConfig key3 UpgradeRequired
- **주의 — 직렬화**: enum 순서 변경 → 기존 Barracks=1 인덱스 직렬화가 다른 값으로 덮어쓰임. Inspector 전체 재검토 필요

## 건물 스탯 확정 + Config 32종 + AttackCooldown 필드 (2026-05-18) ✅ 완료
- `BuildingStatsConfig.cs` BuildingTypeEntry에 human/spirit/transcendenceAttackCooldown 3개. `BuildingStats.StatValues`에 AttackCooldown, `GetAttackCooldown(type, race)` 신규. GameBootstrapper 주입. asset 3→32종.
- AutoTower(type:2): Human(CannonTower) HP50/150G/15/5.0s, Spirit(RuneSpire) 150/200/15/3.5s, Trans(VineTower) 100/175/15/5.0s. 비타워는 0f.

---

## ProductionPopup UI 레이아웃 재구성 + 2/3단계 랠리 마커 버그 (2026-05-17~18) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-17/15_30_production-popup-ui-layout/`

1. BuildingIconEntry blue/red 분리 → `GetBuildingIcon(type, team)`
2. 철거 환불 누적: 1단계 건설비+모든 업글비 합산 50%. `BuildingStats._totalInvestedCostCache`(Set/Get). GameBootstrapper 단계 체인 순회 채움
3. 2유닛 건물 [유닛1][빈][유닛2]: `_unitButtonGroups(List<CanvasGroup>)`, 슬롯1 alpha=0
4. HeaderText 건물명: `_headerText.text = barracks.Type.ToString()`
5. UpdateButtonPortraits 2유닛 매핑: slot0=list[0], slot1=스킵, slot2=list[1] (슬롯2 잔존 버그 수정)
6. **2/3단계 랠리 마커 미표시(핵심)**: ProductionTicker가 OnBuildingPlaced만 구독 → 업그레이드 새 건물 미등록. `SubscribeEvents()`에 OnBuildingUpgraded 추가, 핸들러 UnregisterBarracks(Old)+RegisterBarracks(New). 전종족 PASS(2026-05-18)

**Sprite 명명**: `bld_{buildingtype소문자}_{blue|red}.png` @ `Assets/_Project/Sprites/Buildings/`

## Rule 20 슬롯0 확장 (2026-05-17) ✅ 완료
`UnitProductionUseCase.ToggleAutoProduction`에 슬롯0 체크: `CurrentProducing==type && !CurrentIsAuto`이면 CurrentIsAuto=true+AutoTypes.Add+Normalize. 슬롯0 수동 생산 중 자동등록 → 중복없이 슬롯0 자체 전환. BUG-15와 조건 상호배타.

---

## 건물 생성/파괴 시 유닛 이동 멈춤 수정 (2026-05-17) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-16/17_00_building-repath-freeze-fix/` / `Presentation/Unit/UnitView.cs`

**문제**: 건물 생성/파괴 시 RepathAllAliveUnits→OnPathInvalidated→MoveTo로 코루틴 즉시 재시작 → 1~2프레임 멈춤.

**수정**: `_pendingPath`/`_currentNextTileCoord` 필드 추가. `OnPathInvalidated()`: 현재 Lerp 중 다음 타일에 건물 생기면 즉시 MoveTo(뚫고가기 방지), 그 외 `_pendingPath` 저장만(코루틴 유지). `MoveAlongPathV3()` 타일 도착 직후 _pendingPath 소비. "부드러운 교체=기본, 즉시 재시작=예외(앞 타일 막힘만)".

---

## 유닛 생산 실패 피드백 시스템 (2026-05-16) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-16/15_09_production-fail-feedback/`

**신규 4개**:
- `Infrastructure/Config/ToastMessageConfig.cs` — ToastEntry(key/message/duration), TryGet()
- `Presentation/UI/Common/ToastKey.cs` — GoldInsufficient=0/PopulationFull=1/ProductionQueueFull=2
- `Presentation/UI/Common/ToastUI.cs` — 싱글턴, IPointerClickHandler, `ToastUI.Show(ToastKey)`, Queue 방식, DontDestroyOnLoad 독립 Canvas, CanvasGroup DOTween, OnGameStarted/OnGameEnd 자동정리
- `Editor/SetupToastUI.cs` — 씬 루트 생성, 자체 Canvas(Overlay sortingOrder=100)

**주의**: DontDestroyOnLoad=루트 전용(자식 배치 시 씬 전환 파괴). SetActive(false) 금지(Awake 미호출→미등록), CanvasGroup.alpha=0. 골드 텍스트(`_goldText`) 변경 안 함, `_unitCostTexts[i]`만 빨강.

**수정**: ProductionPanelUI(ProductionFailReason enum, OnUnitTap 사전검증), GameHudUI(used>=max 색상), UnitProductionUseCase(TryStartNext 자원부족 시 IsCharged=false만 취소).

## ToastUI SetActive 버그 (2026-05-16) ✅ 완료
ClearAll/FinishCurrent의 `SetActive(false)` → OnGameStarted ClearAll 시 루트 비활성→Update 정지. 3곳 `SetActive` 제거, `blocksRaycasts/interactable`만 제어. Toast 루트는 항상 활성.

## 건물 비용 텍스트 'G' 제거 (2026-05-16) ✅ 완료
`BuildingPlacementUI.cs` `$"{cost}G"` → `$"{cost}"`. 생산 패널과 통일.

## 건물 배치 패널 실패 피드백 (2026-05-16) ✅ 완료
`BuildingPlacementUI`: UpdateCostTextColors()(골드부족 빨강), Show()에서 OnResourceChanged 구독(`_resourceSubscription`), Close()에서 Dispose+초기화, PlaceAndClose 싱글 분기 골드부족 시 Toast+return(팝업 유지). 멀티 분기 미수정.

---

## 랠리포인트 깃발 팀별 표시 분리 (2026-05-16) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-16/15_30_rally-point-flag-visibility/`

**버그**: 클라이언트 랠리포인트 설정 시 호스트 화면에도 깃발 표시.
**원인**: RallyPointChangedEvent에 팀 정보 없음 → ProductionTicker가 상대 팀도 처리.
**수정(3개)**: GameEvents.cs(Event에 TeamId Team 추가), UnitProductionUseCase.cs(Set/ClearRallyPoint 발행 시 state.Team 전달), ProductionTicker.cs(OnRallyPointChanged 진입부 팀 필터 IsServer→Blue, 싱글 시 스킵). 원칙: 이벤트 자기완결, 필터링은 Presentation.

---

## 혼잡도 기반 유닛 분산 시스템 (2026-05-15) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-15/17_29_congestion-based-spread/`

**문제**: 모든 유닛 성 방향 세로 줄 이동. v1(CastleApproachManager)은 경로 거의 동일.

**신규**: `Application/Services/CongestionMap.cs`(타일별 혼잡도 Increment/Decay/Clear, 순수 C#), `CongestionAwarePathfinder.cs`(혼잡도 가중 A*, 비용=1+혼잡도×Weight, non-walkable 목적지 인접 자동 대체)
**삭제**: CastleApproachManager.cs(v1)
**수정**: GameConfig(CongestionDecayInterval=5f/CongestionWeight=3f), GameEvents(OnUnitEnteredTile), UnitView(`_isAStarMoving` bool, 타일 전환 완료 시 발행), ProductionTicker(주입+감쇠타이머, A*우선 BFS폴백), GameBootstrapper(생성+구독 서버가드+ClearAll)
**설계**: CongestionConfig SO 미생성(GameConfig 통합). reactive congestion(실제 진입 시점 증가).

## 로비 캐릭터 잘못 표시 버그 (2026-05-15) ✅ 완료
**원인 확정**: CharPreview가 실제 유닛 프리팹 인스턴스 → NetworkTransform이 Host 캐러셀 위치를 Red 클라로 동기화하여 DOTween 덮어씀. **수정(에디터)**: CharPreview 3종 Unpack Completely → UnitView/AnimationEventRelay/NetworkUnit/NetworkTransform/NetworkObject 제거. (`RaceSelectionView.cs`에 진단 로그 추가)

---

## 유닛 회전 시스템 수정 + MovementLogger 삭제 (2026-05-14) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-14/14_30_unit-rotation-system-fix/`

- `UnitView.cs`: `[SerializeField] _rotationSpeed=270f`(const 교체). A*/정렬 방향 계산 `CalculateAttackAngle(toPos)`(Atan2). Lerp 루프 매 프레임 `Quaternion.RotateTowards(현재, targetRot, _rotationSpeed*dt)`. ApplyDirection 호출부 제거(메서드 유지). MovementLogger.Log 29개 제거.
- `MovementLogger.cs` 삭제, GameBootstrapper/AttackPositionManager 호출 제거.
- CalculateAttackAngle 재사용으로 A*/정렬 회전 통일. _rotationSpeed 단일 필드로 모든 회전 통일.

---

## 유닛 이동/전투 시스템 재설계 (2026-05-11) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-11/23_19_unit-movement-redesign/`

슬롯 기반 분산 전면 폐기 → 겹침 허용 단순 구조. 근접/원거리 동일 상태 머신.

**비활성화→완전 제거(2026-05-16 dead-code-cleanup)**:
- `TileMoveSlotManager.cs` 파일 삭제, `TileOccupancyManager.cs` 비활성 메서드 5개 제거(클래스 유지)
- UnitMovementUseCase: RegisterOccupancyMove/ReleaseOccupancy/GetOccupancySize 제거
- UnitData.ClaimedTile, UnitStats.OccupancySize/GetOccupancySize 제거
- UnitView ClaimedTile 참조 7곳 제거
- HexPathfinder.FindPathToNeighbor 제거(호출처 없음)
- GameEvents OnGamePaused/OnGameResumed 제거, GameUIManager 구독 제거, IGameUI default 메서드 제거

**신규 상태 머신** (`UnitView.MoveAlongPathV3()`, 근접/원거리 동일):
- Phase 0(A* Lerp) → HasEnemyInDetectRange → Phase 1(월드 직선 추격) → HasEnemyInRange → 공격 → FindForwardClosestTile → Phase 0 재개
- `UnitCombatUseCase.FindFirstEnemyInDetectRange()` isMelee 분기 제거, 모든 유닛 `DetectRange×TileHeight` 통일
- **BUG-001(2026-05-12)**: 전투 추격 중 건물 생성/파괴 시 멈춤 → `_isInCombatPursuit` bool, `IsInCombat() → _combatTargetTransform!=null || _isInCombatPursuit`
- **BUG-002(2026-05-13)**: 전투 종료 후 ~1타일 순간이동 → 즉시 스냅 제거, 정렬 Lerp 추가(매 프레임 적 감지)

## 이동 슬롯 오프셋 Inspector 조정 (2026-05-11) ✅ 확인 완료
`TileMoveSlotManager` const→readonly+생성자 파라미터(기본 0.30f). GameBootstrapper SerializeField → 생성자 주입. 순수 C# 클래스라 SerializeField 직접 불가 → MonoBehaviour에 배치 후 주입. 런타임 수정 미반영(생성 시 1회).

---

## Lobby UI 규칙 작업 모음 (2026-05~06)

### Lobby.unity 규칙 전수 점검 (2026-06-15) ✅ 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-15/18_12_lobby-rule-violations/`
- Rule5: LobbyUI `_lobbyPanel` GameObject→CanvasGroup. Rule6: LoadingScreen StatusText 폰트 LiberationSans→Maplestory Light SDF. BattleRootView 미사용 using 제거.
- YAML 점검: Rule1(1080×1920 ScaleWithScreenSize)✅ Rule2(sizeDelta 위반없음)✅ Rule4(SafeAreaFitter 3곳)✅

### Lobby UI 규칙 준수 — 에디터 스크립트 4종 (2026-05-30) ✅ 완료 (PASS)
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-30/12_24_lobby-ui-rule-compliance/` / `FixLobbyUiGroupA~D.cs`
- 25건 위반 일괄 수정. GroupA(Toast Canvas Scaler/앵커/CanvasGroup), B(LoadingScreen/CodeInput 앵커), C(TabBar/ContentArea 앵커), D(5개 VLG 16자식)
- **VLG 자식 고정픽셀→앵커 패턴(중요)**: VLG `childControlHeight=true, childForceExpandHeight=false`. 각 자식 LayoutElement `preferredHeight=원래SizeDelta.y, flexibleHeight=0`. 자식 sizeDelta=0. ⚠️childForceExpandHeight=true면 버튼 비정상 커짐, flexibleHeight>0면 크기 변동
- **앵커 계산값 검증 필수**: Plan.md 값과 스크립트 코드값 일치(GroupA Toast Background y 0.04 vs 0.5 불일치로 위치 버그)

---

## 패널/버튼 레이아웃 작업 모음 (2026-05)

### 패널 버튼 크기 불일치 (2026-05-31) ✅ 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-31/13_18_panel-button-size-inconsistency/` / `FixPanelRowLayout.cs`
**원인**: 슬롯 아이콘 native size가 VLG preferredHeight 배분(Phase2)에서 Row 불균등. childForceExpandHeight는 Phase3만 작동. **해결**: 9 Row에 LayoutElement(preferredHeight=0, flexibleHeight=1), 27 Slot에 (preferredWidth=0, flexibleWidth=1). 결과 Row 218.45px / Slot 283.73px 균일.

### Production 잠금 유닛 Lock Icon + 디밍 (2026-05-31) ✅ 완료 (PASS)
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-31/19_48_production-lock-icon/`
- **슬롯 구조**: 유닛 버튼 3개=1/2/3단계. 슬롯0 항상 해금(인디케이터 불필요), 슬롯1/2만. `_unitLockIndicators[0]→슬롯1, [1]→슬롯2`
- `ProductionPanelUI.UpdateLockIndicators()`: `int slotIndex=i+1` 매핑 보정. 잠금 시 portrait color (0.35,0.35,0.35,1), 해금 white. UpdateButtonPortraits는 .sprite만 변경(충돌 없음)
- `AddLockIcons.cs` 재작성: `_unitButtons`에서 Slot GO 찾아 LockIndicator 생성 후 `_unitLockIndicators` 연결. 대상 슬롯 {1,2}. **LayoutElement.ignoreLayout=true 필수**(HLG 무시). RectTransform 우하단 40%. 초기 SetActive(false)
- **패턴**: 다른 컴포넌트 private [SerializeField] 리스트 접근/쓰기 — `new SerializedObject(comp).FindProperty(name)` → arraySize 설정 후 GetArrayElementAtIndex(i).objectReferenceValue 할당 → ApplyModifiedProperties

### 유닛 초상화 자동 할당 (2026-05-31) ✅ 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-31/09_32_production-panel-portrait-auto-assign/` / `AssignUnitPortraits.cs`
**원인**: UpdateButtonPortraits가 `_buildingUnitMappings[x].blueUnits[i].portrait`(null)로 덮어씀. **해결**: blueUnits/redUnits 순회 → `{name}_portrait_{blue|red}` 패턴 → FindAssets → portrait 할당(null 슬롯만). 142개 전부 할당.
**파일명**: `{UnitType.ToString().ToLower()}_portrait_{blue|red}.png` @ `Sprites/Units/{종족}/{유닛}/`

### BuildingActionPanelUI 씬 재설계 (2026-05-29) ✅ 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-29/building-action-panel-rebuild/`
`_allSlotButtons`(9)+`_activeSlotButtons`. BuildSlotCanvasGroups()에서 캐시·초기 alpha=0. OnShow() 전체 숨김 후 활성만 alpha=1(BuildingPlacementUI 패턴). 3x3 VLG+HLG. 빈 슬롯 숨김은 런타임 OnShow().

### BuildingPlacementUI 씬 재설계 (2026-05-29) ✅ 에디터 완료 / 재검증 필요
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-29/building-placement-ui-rebuild/`
패널 높이/테두리/골드 정렬 FAIL → 전면 재구성. BuildingPanel anchor(0,0)~(1,0.4). VLG→Row HLG 중첩(3×3). 버튼 내부 HLG: IconImage(flexW=6)+CostContainer(flexW=4 VLG→GoldIcon+CostText). `_buildingGoldIcons` 필드. **셋업 패턴**: 앵커 변경 후 `anchoredPosition=Vector2.zero` 명시, 기존 자식 전체 삭제, GoldIcon AspectRatioFitter 제거.

### 건물 배치 팝업 3행 버튼 레이아웃 (2026-05-19) ✅ 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-19/14_00_building-slot-layout-fix/`
`BuildingPlacementUI`: `List<CanvasGroup> _buttonCanvasGroups`, Awake()에서 캐시/AddComponent. Show() SetActive→CanvasGroup. UpdateCostTextColors `!activeSelf`→`alpha<0.5f`. **원인**: HLG(ChildForceExpandWidth=1)가 SetActive(false) 자식 제외 → 7건물 시 1슬롯이 전체 가로폭. CanvasGroup.alpha=0이면 공간 보존.

---

## 인게임 설정/액션 패널 (2026-05-18~19)

### 인게임 설정 메뉴 + 게임 포기 (2026-05-18~19) ✅ 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-18/23_36_ingame-settings-forfeit/`
- `InGameSettingsUI.cs` 신규: IGameUI. Show()싱글 timeScale=0+SharedBackground 등록. 포기: ConfirmPopup→NetworkContext 분기(멀티 RequestForfeit/싱글 GameEndUseCase.Forfeit)
- `ConfirmPopup.cs` 신규: 범용 확인 팝업. Show(message, confirm/cancelLabel, onConfirm/onCancel). BlockingOverlay(CanvasGroup)
- `GameEndUseCase.Forfeit()`: IsGameOver=true → OnGameEnd(Red)
- `NetworkGameEndController.RequestForfeit()`+ForfeitServerRpc: RequireOwnership=false. Host=Blue/Client=Red. `_announced` 재사용
- 일시정지: timeScale=0 + UIAnimator SetUpdate(true)로 DOTween 동작

### 비생산 건물 공용 액션 패널 — BuildingActionPanelUI (2026-05-18~19) ✅ 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-18/17_00_building-action-panel-ui/`
- `BuildingPanelBase` 추상 베이스 신규: protected SerializeField 6개, InitializeBase/Show/Close virtual, OnDemolishButtonClick 공통 철거(싱글/멀티). Template Method(OnShow/OnBeforeClose/BeforeDemolish 훅)
- `BuildingActionPanelUI` 신규: 베이스 상속, Initialize만. 비생산 건물 클릭 시
- `ProductionPanelUI` 리팩토링: 베이스 상속, 공통 제거, 훅으로 분해
- `BuildingTypeHelper.CanShowActionPanel`: `!IsProductionBuilding && type != Castle`
- **주의**: SharedBackgroundButton이 비활성 GO에 부착 → `FindFirstObjectByType(FindObjectsInactive.Include)` 필수. 비생산 환불 캐시 루프 누락 시 GetTotalInvestedCost→0

---

## OnEntityDied 이벤트 분리 리팩토링 (2026-05-18) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-18/15_00_entity-died-event-split/`

- EntityDiedEvent+OnEntityDied 완전 삭제 → UnitDiedEvent/BuildingDiedEvent 강타입 + OnUnitDied/OnBuildingDied Subject
- 발행 4곳, 구독 9곳 교체. **RPC 시그니처 유지** `EntityDiedClientRpc(int, bool)` (와이어 호환)
- NetworkCombatController `_diedSubscription` 단일 → unit/building 2개(OnNetworkDespawn 둘 다 Dispose)
- 발행 순서: RemoveUnit/RemoveBuilding **직전**(구독자 도메인 Dict 접근)

---

## AnimatedPanel/CanvasGroup 리팩토링 모음 (2026-05~06)

### AnimatedPanel/UIAnimator/ConfirmPopup SetActive→CanvasGroup (2026-06-13~15) ✅ 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-12/09_13_animatedpanel-canvasgroup-refactor/`
- UIAnimator 이미 완료 상태. AnimatedPanel EnsureInitialized 초기값 명시+`_backgroundOverlay`(CanvasGroup) Show/Hide 제어. ConfirmPopup `_blockingOverlay` GameObject→CanvasGroup. RematchRequestPopup SetActive 제거+Awake 초기값. ProductionPanelUI `_unitLockIndicators` List<GameObject>→List<CanvasGroup>+`_unitBorderOverlays` 캐시
- **씬**: `FixRule5Violations.cs`(메뉴 `Hexiege/Setup/규칙5 위반 수정`) 실행. AnimatedPanel GUID `b97e76d0453d56e4b961752cd52c6eb6`

### Lobby 패널 CanvasGroup 에디터 사전 부착 (2026-06-22) ✅ 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-22/05_59_lobby-canvasgroup-preattach/`
런타임 AddComponent → 에디터 사전 부착. `SetupLobbyPanelCanvasGroups.cs`(4패널 SetActive(true)+CanvasGroup, BattlePanel alpha=1 나머지 0). LobbyRootView.Awake EnsureCanvasGroup→GetComponent. 원칙: 컴포넌트 부착은 에디터에서 미리(GameSystemRules_UI Rule5).

### ProfileView 로그아웃 버튼 (2026-06-22) ✅ 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-22/04_34_lobby-profile-logout-button/`
`AddLogoutButtonToProfileView.cs`(ProfileView 부착+LogoutButton GO 생성+SerializedObject 연결). 코드 변경 없음(이미 구현).

### 로비 SetActive→CanvasGroup 전환 (2026-05-25) ✅ 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-25/lobby-canvasgroup-refactor/`
로비 7개 뷰. SetActive(false)→alpha=0/blocksRaycasts=false/interactable=false. **이유**(Rule5): SetActive(false)는 LayoutGroup 제외+DontDestroyOnLoad Awake 미호출. **신규 뷰 체크리스트**: CanvasGroup 부착, `_canvasGroup` 연결, Show/Hide CanvasGroup 패턴.

### 로비 배경 Safe Area 수정 (2026-05-26) ✅ 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-26/safe-area-lobby-bg/` / `FixLobbyBackground.cs`
**문제**: LobbyRoot Image가 SafeAreaContainer 안 → Safe Area 크기만 그림(Rule4 위반). **수정**: LobbyBackground 신규(전체화면 stretch, SafeAreaContainer보다 앞, raycastTarget=false), LobbyRoot Image enabled=false. **원칙**: 전체화면 배경은 SafeAreaContainer 밖(Canvas 직속), anchor(0,0)~(1,1), raycastTarget=false, 순서는 SafeAreaContainer보다 위.

---

## 멀티플레이 포기 시 호스트 GameEndUI 미표시 (2026-05-27) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-27/16_52_game-end-ui-bugfix/`

**원인**: ForfeitServerRpc는 GameEndUseCase를 안 거치고 AnnounceWinnerClientRpc 직접 호출 → 서버에서 OnGameEnd 미발행 → GameEndUI 미표시.
**수정**(`NetworkGameEndController.ForfeitServerRpc`): `_announced=true` 후 AnnounceWinnerClientRpc 직전 `GameEvents.OnGameEnd.OnNext(new GameEndEvent(winnerTeam))` 1줄. `_announced=true`라 OnGameEndServer 가드(`if(_announced)return`)에 막혀 중복 없음.
**Canvas BUG-002 동시 수정**: RematchRequestPopup이 SafeAreaContainer보다 앞 인덱스 → 순서 교환(Inspector).

---

## UI 종족/팀 초상화 및 생산 연동 정비 (2026-04-30) ✅ 완료
ProductionPanelUI/BuildingPlacementUI/GameBootstrapper. UI Skinning 제거, 데이터 기반 바인딩(UnitPortraitEntry/BuildingPortraitEntry), 생산 타입 1:1 동기화, 비용 텍스트 동적, Initialize GameConfig 파라미터 제거. 데이터 우선 원칙.

---

## Phase 2 후방 스냅 수정 — 7차 Step 4 (2026-04-29) ✅ 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-27/01_17_phase2-backward-snap-fix/` / `UnitView.cs` Phase2(1438~1545)
- Step 4-A: nearestTile==T0이면 6방향 인접 중 forward neighbor(거리 감소) 최근접으로 교체. `HexDirectionExtensions.Count`+`((HexDirection)i).Neighbor(origin)`. walkability 생략(A* 재계산)
- Step 4-B: Phase2 Lerp 중 적 감지 블록. `HasEnemyInDetectRange && !HasEnemyInRange && snapEnemyIsForward`. forward `<=`(동거리 앞쪽). break→강제 스냅→Phase1 재진입
- **HexCoord 인접 탐색**: GetNeighbors 부재 → `HexDirectionExtensions.Count`+`Neighbor` 표준. `<=` 동거리 forward 포함. forward filter 일관성(Phase0/1/2 모두 동일)

## Mesh Y Offset 제거 + DirectionAngles 수정 (2026-04-29) ✅ 확인 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-28/14_30_mesh-offset-cleanup/` / `UnitView.cs`
- DirectionAngles `{0,60,120,180,240,300}`→`{60,120,180,240,300,0}` (FlatTop atan2 실제값. NW(5)=0°). `_meshYOffset` 제거, CalculateAttackAngle `-_meshYOffset` 제거
- **주의**: 메시 자식 Y(30°) 제거 시 DirectionAngles +30° 조정(기존{30..330}+30). -30° 적용하면 60° 어긋남. CalculateAttackAngle은 Atan2 직접이라 독립.

---

## 패스파인딩/뭉침 개선 시리즈 (2026-04-25~27)

### 근접 유닛 뭉침 — 18슬롯 + 슬롯도달후 직진 (2026-04-27) ✅ 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-26/18_30_melee-spread/`
`AttackPositionManager` 6→18슬롯. 인접 타일당 (중심+좌경계+우경계). `Dictionary<HexCoord, Dictionary<int, Vector3>>`(도메인 좌표). 점유 Vector3.Distance<0.01f. `UnitView` Phase1 moveTarget `reachedSlot` 분기(0.15f 이내 enemyViewPos 전환).
**버그**: 슬롯위치(0.866/0.75) > 사거리(0.3/0.5) → 슬롯 도달 시 dist<0.01f로 멈춤 → 전투 안 됨. **설계**: 도메인 좌표 점유 추적(뷰는 ViewConverter 회전), AddCandidateUnique 중복방지, 단방향 전환(진동방지), Y무시 거리판정, MaxUnitsPerSlot=2 fallback.

### 타일 소유권 실시간 감지 (2026-04-26) ✅ 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-26/17_00_tile-ownership-detection/`
**신규** `Application/Services/TileOwnershipService.cs`: Pull 모델. 매 프레임 유닛 viewPos→ViewConverter.FromView→WorldToHex 역산 → `Dictionary<HexCoord, HashSet<TeamId>>`. 한 팀만 있고 GetOwner!=claimingTeam일 때만 SetOwner+OnTileOwnerChanged. HashSet 풀.
**수정**: HexGrid.GetOwner(HexCoord) 신규. GameBootstrapper `_tileOwnership` 생성+Update 서버가드 Tick.
**설계**: IsInvalid 부재→HasTile로 경계검증. 점령규칙(한 팀만 갱신/양팀 유지/빈 타일 유지). 서버가드(순수 Client 차단). 이벤트 중복 방지. Application/Services 경로.

### 근접 유닛 뒷무빙 5차 (2026-04-26) ✅ 확인 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-26/15_00_phase1-target-reselect/` / `UnitView.cs` 3곳
- Step1 Phase1 타겟 사망 시 즉시 재선택(continue/break), Step2 전투 종료 후 다음 타겟, Step3 Phase2 후방 스냅 방지(`HexCoord.Distance` 비교)
- **근본 원인**: Phase1 타겟 사망 → 무조건 Phase2 → 후방 타일 스냅. **거리 기준**: 월드 거리 대신 `HexCoord.Distance`(도메인 정수).

### 패스파인딩 4차 — FROM 점유 해제 타이밍 (2026-04-26) ✅ 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-26/11_00_occupancy-from-fix/`
`TileOccupancyManager.ReserveOccupancy` public. UnitMovementUseCase: RegisterOccupancyMove TO+1만, ProcessStep 첫줄 `from!=to`이면 OnUnitRemoved(FROM)(Lerp 완료 후). **설계**: FROM 해제 분리(물리적 위치 동안 점유 유지). death-during-Lerp 이중 해제 동시 해결. **실기**: 권총병 분산 PASS, 근접 뭉침 별도, 뒷무빙 발견.

### 패스파인딩 3차 — 뭉침/팅김 (2026-04-25) ✅ 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-25/10_05_pathfinding-improvement/`
`TileOccupancyManager.FindAvailableTile(preferred, size, grid, destination)` forward 필터 BFS(`Distance(c,dest)<=Distance(pref,dest)+1`, fallback 무필터). UnitMovementUseCase RegisterOccupancyMove/ReleaseOccupancy. UnitView `_pendingOccupancyTile`+prevActualTile 추적+우회 시 즉시 re-path. **설계**: 점유 갱신 Lerp 시작 직전(Race Condition 해결), prevActualTile 추적, 우회 즉시 re-path(팅김 방지), forward 필터 +1 여유.

---

## 유닛/건물 스탯 ScriptableObject 전환 (2026-04-25) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-25/01_35_unit-stats-scriptableobject/`

**신규**: UnitStatsConfig.cs/BuildingStatsConfig.cs(B방식: 건물타입별 3종족 묶음), SetupUnitStatsConfig.cs/SetupBuildingStatsConfig.cs
**수정**: UnitStats/UnitProductionStats/BuildingStats switch→Dictionary+Initialize(), GameBootstrapper InitializeXxxStatsFromConfig, BuildingPlacementUI GetGoldCost
**경로**: `Resources/Config/UnitStatsConfig.asset`, `BuildingStatsConfig.asset`
**설계**: Domain 순수성(C# struct 직접 정의), GameBootstrapper가 SO→Domain 변환, Play Mode 수정은 다음 진입까지 미반영.

---

## 다중 히트 데미지 (2026-04-24) ✅ 실기 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-24/16_31_multi-hit-damage/`
- UnitStats `GetHitFrameTime()`→`GetHitFrameTimes()`(float[]), LionKnight Cooldown 2.33→3.0. UnitData HitFrameTime→HitFrameTimes(float[]). UnitCombatUseCase PendingHit struct+`_pendingHits`+TickPendingHits. NetworkCombatController HitFrameTimes foreach 코루틴 N개. GameBootstrapper Update TickPendingHits
- **설계**: 쿨다운 사이클 시작 시 1회만. 싱글=타이머 리스트, 멀티=코루틴 병렬. 타겟 사망 시 IsAlive 체크로 취소
- **타이밍(30fps)**: FlameSpirit 6히트(쿨3.0) 0.667/1.167/1.433/1.667/1.933/2.100, LionKnight 2히트 0.733/1.267

## 근접유닛 추적 중 회전 (2026-04-24) ✅ 실기 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-24/15_45_melee-pursuit-rotation/`
UnitView Phase1 직선이동(850~866) `if(dist>0.01f)` 내 `CalculateAttackAngle(enemyViewPos)`+`Quaternion.RotateTowards`. 멀티: MoveAlongPath 코루틴 가드(서버만) → NetworkTransform 보간.

## 싱글플레이 AI 종족 랜덤 (2026-04-24) ✅ 실기 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-24/23_06_random-opponent-race/`
GameBootstrapper `GameRaceContext.Set(..., RaceId.Human)` → `Enum.GetValues`+`Random.Range`. BattleViewModel 중복 Set 제거. `(RaceId[])Enum.GetValues(typeof(RaceId))` 패턴.

---

## 유닛 공격 거리/회전 시리즈 (2026-04-10~12)

### 원거리 유닛 공격 중 회전 추적 (2026-04-11~12) ✅ 실기 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-11/ranged-unit-rotation-tracking/` (MULTI-001~007 PASS)
UnitView `_combatTargetTransform`+Update RotateTowards(270°/s)+백업 ID. UnitCombatUseCase `IsCurrentTargetStillValid(attacker, targetId, targetIsUnit)`. NetworkCombatController TickCombat 타겟교체 2곳 가드. Transform 직접 저장, 서버만 rotation(클라 가드), 타겟 고착성.

### 근접 공격 거리 다듬기 (2026-04-11) ✅ 실기 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-11/melee-attack-distance/`
UnitCombatUseCase `MeleeContactDist=0.3f`/`BuildingDetectionRadius=0.2f`. 근접 vs유닛 0.35f/vs건물 0.55f, 원거리 기존 유지. `isMelee = AttackRange<1.0f`.

### UnitType 개편 + 근접 사거리 (2026-04-10~11) ✅ 실기 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-10/16_09_melee-unit-attack-range/`
UnitType Pistoleer=0~LionKnight=8 (9종 독립 enum). HexPathfinder `FindPathToNeighbor()`. UnitFactory `List<UnitPrefabEntry>(type, blue, red)`. 근접(range=0.5): maxDist 0.483f+경로에 Castle 타일 추가. **ClaimedTile non-walkable 예외**(설정 시 Castle blocked 유지로 후속 차단). `FindPathToNeighbor` count=1 반환→`>=1` 조건.

### 다중 히트/회전 관련 유닛/건물 스탯 적용 + UI 골드 (2026-04-12~13) ✅ 실기 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-12/06_42_stats-apply/`
UnitStats(Pistoleer MoveSpeed 1.0→0.5, Spirit/Trans 6종 HP/ATK), BuildingStats `GetMaxHp(type, RaceId)` 오버로드(Trans Castle200/Barracks50/Mining40). BuildingPlacementUseCase에 `RaceId race=Human` 파라미터(GameRaceContext 직접 참조 없음). UI 골드 숫자만.

---

## 초상화/종족 적용 시리즈 (2026-04-07~12)

### 건물/유닛 초상화 종족+팀 (2026-04-12) ✅ 실기 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-11/building-portrait-race-support/`
BuildingPlacementUI `BuildingRacePortraitSet`(barracks+miningPost), 팀×종족 6세트, GetBuildingPortraitSet(). ProductionPanelUI 슬롯 순서. GameRaceContext Presentation 참조 허용.
- Spirit: EmberSpirit/FlameSpirit/InfernoSpirit. Trans: FoxMagician/BearGuard/LionKnight

### 종족 인게임 적용 (2026-04-07) ✅ 실기 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-07/09_00_faction-ingame-apply/`
UnitFactory/BuildingFactory 종족별 6세트 프리팹, GameRaceContext switch. GameBootstrapper 싱글 Start `GameRaceContext.Set`. SetupUnitFactoryPrefabs.cs.
- **건물 매핑**: Castle(Castle/SpiritNexus/ElderTree), Barracks(Barracks/SummoningAltar/HunterPlant), MiningPost(MiningPost/ManaRift/FungalNode)

### 중립 광산 표시 제어 (2026-04-08) ✅ 싱글 실기 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-07/23_45_goldmine-hide/`
HexGridRenderer `_goldMineObjects` List→Dictionary, Hide/ShowGoldMine, SubscribeGoldMineEvents. BuildingPlacementUseCase RemoveBuilding MiningPost 파괴 시 Owner Neutral 복원. 초기 숨김 `tile.Owner!=Neutral`. OnBuildingPlaced→Hide / OnEntityDied(MiningPost)→Show.

---

## 피격 시 부유 HP 텍스트 (2026-04-12~17 World Space 전환) ✅ 싱글/멀티 실기 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-12/18_03_floating-hp-text/`, `2026-04-13/17_50_floating-text-worldspace/`

**신규**: FloatingHpText.cs(TMP 3D World Space, DOTween LocalMoveY+DOFade), FloatingHpTextSpawner.cs(OnEntityDamaged 구독, Queue 풀 10개, 팀별 색상), FloatingHpText.prefab, SetupFloatingHpText.cs
**수정**: GameBootstrapper(SerializeField+LoadMap Initialize), NetworkHealthSync(TakeDamage 후 OnEntityDamaged 재발행)
**설계**: World Space TMP(좌표변환 없음), scale=1f 고정(줌 비례), 빌보드 `LookRotation(-Camera.forward, up)`, 좌우반전 `localScale(-s,s,s)`, 클라 재발행 diff>0만. 팀 색상 Blue 연두/Red 노랑. Material Preset은 독립 .mat 필수(폰트 sub-asset 지정 시 .asset 오염).

---

## 타겟 고정/생산 슬롯 버그 (2026-04-18~19)

### 타겟 고정 데미지 불일치 (2026-04-18) ✅ 멀티 실기 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-17/22_29_target-lock-damage-bug/`
NetworkCombatController TickCombat(253~297). **버그**: A가 B 공격 중 C 접근 시 애니=B, 데미지=C. **원인**: IsCurrentTargetStillValid(B)=true로 애니 유지하나 ExecuteAttack은 TryFindTarget(C) 사용. **수정**: `damageTargetId/IsUnit` 변수, valid=true→prev.targetId 유지. **교훈**: 애니 타겟과 데미지 타겟 항상 일치.

### 랠리포인트 Client 무시 (2026-04-19) ✅ 실기 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-19/18_54_rally-point-ignored/`
NetworkProductionController `SetRallyPointServerRpc` 신규. **원인**: CompleteRallyPointSetting이 로컬만 갱신, 서버 state.RallyPoint=null. **수정**: 네트워크 분기(ServerRpc 호출+로컬 마커). ClientRpc 불필요(서버 state로 SpawnUnitClientRpc 전달).

### 생산 슬롯 깜빡임 (2026-04-19) ✅ 싱글 실기 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-19/17_49_production-slot-flicker/`
UnitProductionUseCase ToggleAutoProduction(284~288). **버그**: 큐 빌 때 자동 등록 시 슬롯1→슬롯0 1프레임 깜빡. **수정**: Add 이후 `!CurrentProducing.HasValue`이면 즉시 TryStartNext+Early Return.

### 유닛 생산 패널 전면 재작성 (2026-04-19) ✅ 실기 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-19/production-panel-rewrite/`
- ProductionState QueueSlot struct, PendingQueue/AutoTypes/AutoCycleIndex/CurrentIsAuto, IsAutoMode→`AutoTypes.Count>0` 읽기전용
- UnitProductionUseCase EnqueueUnit/Toggle/Cancel/TryStartNext/Complete/ChargeVisibleSlots 재작성, CancelAutoTypeIfNeeded
- **구조(PendingQueue 단일 큐)**: QueueSlot{Type,IsAuto,IsCharged}. PendingQueue[0]=슬롯1/[1]=슬롯2 불변식. AutoTypes 목록. IsAutoMode 계산값
- **전역 규칙**: R1 취소 전액환불, R2 자동취소 시 IsCharged=true 수동이관, R2-1 자동등록=마지막수동이면 IsAuto전환, R3 수동추가 시 자동전체해제, R4 합산≤3, R5 골드차감(수동 등록시/자동 슬롯진입시)
- 슬롯클릭=취소+AutoTypes 제거. slotIndex==0: `wasAuto=CurrentIsAuto` 초기화 전 캡처. 미해결: 큐 빌 때 깜빡임(별도 점검)

---

## 전투 애니메이션/NGO 동기화 시리즈 (2026-03~04)

### 전투 애니메이션 시스템 전면 재정비 (2026-04-03~04) ✅ 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-03/10_00_combat-animation-overhaul/`
NetworkCombatController(3-신호 RPC, TickCombat elapsed, `_combatAnimationSent`, ExecuteAttack 동시), UnitView(Walk CrossFade 1회, `_attackToWalkBlend`, StopCombatAnimation 빈 메서드), NetworkUnit(WaitForUnitId 폴링→OnValueChanged), UnitCombatUseCase(TryAttack 네트워크 차단), UnitStats(GetAttackCooldown 클립 길이: Assault0.2/Pistoleer2.0/Sniper3.0).
**교훈**: TickCombat(Update) > 코루틴 먼저 실행, RPC 전송 추적은 타겟 Dict와 분리, ExecuteAttack 핸들러 즉시 호출(T=0 동기화).

### 재경기 초기화 버그 (2026-04-04) ✅ 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-03/20_00_rematch-initialization-bug/`
NetworkGameEndController.StartRematch에서 LoadScene 직전 SpawnedObjects 순회 명시 Despawn. `SpawnedObjects.Values` List 복사본 순회, `IsSceneObject==false`만, NGO 2.9.x bool? 비교. 교훈: DestroyWithScene/Active Scene Sync는 같은 씬 재로드 불보장.

### 유닛 NGO NetworkObject 전환 (2026-03-26~29) ✅ 완료
**설계(최종)**: 위치=NetworkTransform, 회전=NetworkTransform SyncRotAngleY=true(서버 즉시 스냅→클라 보간), Walk/공격/사망=ClientRpc, Red 클라 보정=NetworkUnit.LateUpdate(위치반전+Y+180). NGO NetworkObject는 씬 루트 생성. WaitForUnitId 폴링+ApplyStartWalkWithRetry.
**폐기**: 클라 LateUpdate 델타 회전, TurnToFaceClientRpc+DORotate, _isPreRotating, _isWalkPending, ResetMovementTracking, OnUnitFacingChanged 등 전면 제거.
**이중 보간 교훈**: 서버 DORotate(0.3)+NetworkTransform(0.1)=~1초 딜레이. 서버 즉시 스냅하면 NetworkTransform 보간만.

### 공격 타이밍 정밀화 (2026-03-27) ✅ 실기 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-03-27/11_00_attack-timing-precision/`
타격 프레임 데미지(서버 RPC 즉시→HitFrameTime 후), 타겟 고정(ApplyAttackDamage IsInRange 제거), 쿨다운 통일(GameBootstrapper Update→TickCooldowns).
신규(UnitCombatUseCase): TryFindTarget/ApplyAttackDamage/TickCooldowns/FindTargetById. HitFrameTime: Assault 0.133/Pistoleer 0.833/Sniper 2.0. DelayedAttackDamage 코루틴.

### 이동 전 회전 타이밍 (2026-03-27) ✅ 실기 완료
**문제**: DOTween(Update) vs NetworkUnit.LateUpdate 충돌. **해결**: `_isPreRotating` 플래그로 DORotate 중 LateUpdate 차단. NetworkUnit(`_isPreRotating`/SetPreRotating/ResetMovementTracking 안전망), NetworkCombatController(TurnToFaceClientRpc SetPreRotating(true)+OnComplete false).

---

## UI Lifecycle / 배경 / 카메라 (2026-03)

### Game UI Lifecycle Framework (2026-03-24) ✅ 실기 완료
신규: IGameUI.cs(OnGameStarted/Ended/Paused/Resumed default), GameUIManager.cs(등록/디스패치). 수정: GameEvents(Subject 추가), GameHudUI/ProductionPanelUI/BuildingPlacementUI/GameEndUI(IGameUI), GameBootstrapper(_uiManager Register/Initialize+OnGameStarted), NetworkGameEndController(NotifyGameEnded).
**패턴**: Register 중복방지, Initialize CompositeDisposable, GameEndUI는 OnGameEnded 제외. **BUG-1**: 클라 OnGameEnd 미발행 설계 → AnnounceWinnerClientRpc 직접 NotifyGameEnded.
**새 UI 체크리스트**: IGameUI 구현, LoadMap 앞 Register, Inspector 연결.

### 반투명 배경 오버레이 구조 개선 (2026-03-23) ✅ 실기 완료
AnimatedPanel Hide 내 SetActive(false) 타이밍 즉시. SharedBackgroundButton.cs 신규(Register/Unregister/OnClick). BuildingPlacementUI/ProductionPanelUI `_backgroundButton`→`_sharedBackground`. Game.unity `[UI]/Background` 공유.

### 코드 정리 (2026-03-20) ✅ 완료
TeamAssigner.cs 삭제(NetworkGameFlow 대체), LocalPlayerTeam/NetworkGameFlow 주석 정리, GameBootstrapper IsNetworkMode() 헬퍼 추출.

### 싱글플레이 ViewConverter 초기화 버그 (2026-03-20) ✅ 완료
**증상**: Red팀 싱글에서 내 진영 상단 표시. **원인**: ViewConverter.Reset()이 항상 Blue 고정. **수정**: GameBootstrapper.LoadMap에서 Reset 제거, ApplyConfig 직후 LocalPlayerTeam 기반 ViewConverter.Setup(isRed, mapCenter). 카메라 맵 중앙 유지.

### 카메라 줌 DOTween 보간 (2026-03-19) ✅ 완료
CameraController HandleZoom 즉시→DOTween. `_targetZoom`/`_zoomTween`(Kill 후 새 Tween)/`_zoomDuration`(0.25f). Awake 초기화, OnDestroy Kill.

### 건물 인근 이동/공격 불가 (2026-03-18) ✅ 완료
HexPathfinder FindPath goal blocked 체크 제거(목표 ClaimedTile 선점돼도 탐색). UnitCombatUseCase maxDist에 Epsilon=0.05f(Pistoleer 0.866 경계 부동소수점).

---

## 재경기/로비복귀/로딩 (2026-03-16~18)

### 랜덤매칭 재경기 지원 (2026-03-18) ✅
GameEndUI.SetupRematchButton isRandomMatch 숨김 분기 제거. 랜덤매칭도 양측 동의 재경기+LoadScene("Game").

### 커스텀게임 재경기 시스템 (2026-03-17) ✅ 완료
NetworkGameManager `_isRandomMatchmaking`/IsRandomMatchmaking. NetworkGameEndController 재경기 RPC 전면 교체(`_rematchRequesterId`, RequestRematch/Accept/Decline, NotifyRematch targeted, StartRematch LoadScene). GameEndUI SetupRematchButton/RestoreRematchButton. RematchRequestPopup.cs 신규.
**교훈**: FindFirstObjectByType 비활성 포함 시 `FindObjectsInactive.Include`. 루트 Active 유지.

### 멀티플레이 로비 복귀 버그 (2026-03-17)
**원인**: `_lobbySceneName` Inspector="Game". GameEndUI ReturnToLobby(NGM.Shutdown+LoadScene("Lobby")), CountdownCoroutine(WaitForSecondsRealtime 30초).

### 전역 로딩 스크린 (2026-03-17)
LoadingScreen.cs(싱글턴, DontDestroyOnLoad, CanvasGroup DOFade). BattleViewModel async void+Task.Delay(2000). sceneLoaded 이벤트로 자동 Hide.

### 랜덤 매칭 버그 (2026-03-16) — [random-matching-bugfix.md]
string.GetHashCode() 비결정성→GetStableHash(). OnClientConnectedCallback 등록을 StartNetworkHost 이전.

---

## 로비 종족 선택 UI — 캐러셀 (2026-04-04~06) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-04/21_00_race-selection-ui/`

**신규/수정**: RaceId.cs(Human=0/Spirit=1/Transcendence=2), LocalPlayerRace.cs, GameRaceContext.cs(Blue/RedRace), RaceSelectionViewModel.cs(UniRx, CmdPrev/Next), RaceSelectionView.cs(캐러셀 DOTween, Animator CrossFade), BattleMainView.cs(BindRace), RaceSelectionPreviewSetup.cs, Pistoleer.controller(Idle m_Speed 0→1)
**설계**: RaceSelectionView BattlePanel 직속 anchor(0,0)~(1,0.5). 항상 표시. CharacterPreview 레이어 격리→RT→RawImage. AnimBlendTime=1.0f.
**캐러셀 위치**: Center(1000,0.35,2), Left(999.7,0.1,5), Right(1000.3,0.1,5). 카메라(1000,1.5,-2) Euler(12,0,0) FOV=10.
**Pistoleer Idle 교훈**: Animator 상태 m_Speed=0이면 첫 프레임 동결. 새 상태 m_Speed=1 확인 필수.
**Android URP RT 잔상 교훈(2026-04-06)**: RT 에셋(m_AntiAliasing:2)과 카메라(allowMSAA=false) sample 불일치 → clear 실패 → 잔상. 체크리스트: RT m_AntiAliasing:1(YAML 직접), Camera allowMSAA/allowHDR=false, backgroundColor.alpha=1, URP antialiasing=None/renderType=Base/renderShadows=false.

---

## 자동/수동 생산 하이브리드 (2026-03-23) [2026-04-19 재작성으로 무효화]

ProductionState AutoEntry(UnitType+IsCharged) 기반. **무효화됨** — 현재 구조는 PendingQueue 단일 큐(2026-04-19 production-panel-rewrite 참조). 이전 BUG-12/13/20 패턴은 현 구조에서 미적용.

---

## 오래된 토픽 참조 (2026-03-14 이전)
- Animation Event 타격 반응 — [rendering-and-animation.md]
- 유닛 확정 스탯 — [unit-stats-and-combat.md] (Pistoleer/Assault/Sniper, AttackRange int→float)
