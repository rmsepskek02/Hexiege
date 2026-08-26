# Game Programmer — 아키텍처

레이어 구조, 레이어 간 제약, 정적 홀더 패턴, 어셈블리 구조.

---

## 레이어 구조 (의존 방향)

Domain → Application → Core → Infrastructure → Presentation → Bootstrap

### 레이어 제약 (CRITICAL)
- **Domain**: `using Hexiege.Core` 절대 금지 → HexOrientationContext 등 정적 홀더 패턴. UnityEngine 참조 금지(예: `Domain/AI/BuildOrderStep.cs`에서 [Tooltip]/[Header] 제거, System [Serializable]만)
- **Application**: Unity.Netcode 직접 참조 금지 → NetworkContext 정적 홀더 패턴
- **Infrastructure**: NetworkBehaviour 및 Unity.Netcode는 이 레이어 전용. `using Hexiege.Presentation` 금지(2026-05-20 검증 0건)
- **Presentation**: `using Unity.Netcode` 절대 금지(2026-05-20 검증 0건). NetworkContext 홀더 패턴만 사용
- **GameBootstrapper** = 유일한 의존성 조합 루트
- **Assembly Definition 없음** — 네임스페이스 규약만으로 레이어 분리

### 레이어 위반 회피 패턴
- Infrastructure→Presentation 직접 호출 불가 → `GameEvents`(Subject) 이벤트 경유. 예: NetworkGameManager가 SceneLoader 직접 호출 대신 `GameEvents.OnNetworkBackToLobby` 발행 → GameEndUI(Presentation) 구독해 SceneLoader.Load
- Domain→Infrastructure 직접 참조 불가 → `Func<TeamId, RaceId>` 델리게이트를 GameBootstrapper에서 주입 (예: TowerCombatUseCase 팀→종족 변환)
- Application에 GameRaceContext 참조 없음 → 호출자에서 `RaceId race` 파라미터로 전달

---

## 정적 홀더 패턴 (레이어 간 의존성 우회)

- `HexOrientationContext` — Domain에서 Core의 Orientation 접근
- `NetworkContext` — Application에서 NetworkManager 상태 접근 (`IsNetworkActive`, `IsNetworkServer`)
- `LocalPlayerTeam` — 현재 플레이어 팀 (싱글=Blue, 네트워크 시 갱신)
- `LocalPlayerRace` — 로컬 플레이어 종족 (Set/Current/Reset)
- `GameRaceContext` — BlueRace/RedRace (멀티플레이 수신용)
- `LocalPlayerDifficulty` — DifficultyLevel(Easy/Normal/Hard) 정적 홀더
- `ViewConverter` — Red팀 좌표/방향 반전

**GameRaceContext는 Presentation에서 참조 허용** — Infrastructure 정적 홀더이지만 레이어 위반 아님 (UnitFactory/BuildingFactory/UI에서 직접 조회).

---

## GameBootstrapper

### Start() 분기
- NetworkManager null 또는 IsHost/IsClient=false → 싱글플레이 (LoadMap 즉시)
- 네트워크 → 맵 로드 건너뜀, NetworkGameFlow가 StartNetworkGame() 대기
- C# LangVersion 9.0 (switch expression 사용 가능)

### 책임
- SO → Domain 구조체 변환 단일 책임 (InitializeUnitStatsFromConfig / InitializeBuildingStatsFromConfig 등)
- ViewConverter.Setup()은 LoadMap() 이전 호출 필수 (ApplyConfig 이후 — HexMetrics 준비 후 GridCenter 계산)
- 파일 분할: GameBootstrapper.cs / .Setup.cs / .Map.cs

### 주의 — 네임스페이스 충돌
- `Hexiege.Application` 네임스페이스가 `UnityEngine.Application`을 가림 → `Application.dataPath` 등은 반드시 `UnityEngine.Application.xxx` 명시

---

## 팀 매핑

- TeamId: Neutral=0, Blue=1, Red=2
- Host→Blue, Client→Red
- TeamAssigner는 삭제됨 (2026-03-20) — NetworkGameFlow.WaitForTeamAndSendReady()에서 `IsHost ? Blue : Red`로 직접 할당

---

## ScriptableObject Config 패턴

- List<Entry> + Initialize()에서 Dictionary 캐싱 (UnitStatsConfig/BuildingStatsConfig/SoundConfig/UnitEffectConfig 공통 패턴)
- Domain은 C# struct(StatValues/ProductionValues) 직접 정의 → Infrastructure→Domain 의존 없음
- Play Mode 중 SO 수정 → Dictionary는 Start() 복사본이므로 다음 Play Mode 진입까지 미반영 (의도된 동작)
- enum 값이 비연속(0,1..7,10..)이면 `enumValueIndex`에 정수 직접 대입 금지 → enumNames 순회하며 intValue 일치 인덱스 탐색

---

## 에디터 스크립트 패턴 (1회성/셋업)

- 프리팹: `PrefabUtility.LoadPrefabContents` → 처리 → `SaveAsPrefabAsset` → `UnloadPrefabContents`
- Inspector private 필드: `SerializedObject.FindProperty` → `ApplyModifiedPropertiesWithoutUndo()` (LoadPrefabContents 환경에서 직접 대입은 직렬화 미반영 가능)
- 다른 컴포넌트 private [SerializeField] 리스트 접근/쓰기: `new SerializedObject(comp).FindProperty(name)` → arraySize 설정 후 GetArrayElementAtIndex(i).objectReferenceValue 할당 → ApplyModifiedProperties (리플렉션보다 안전)
- GUID 기반 에셋 로드: `AssetDatabase.GUIDToAssetPath(guid)` → `LoadAssetAtPath<T>()` (파일 이동/이름변경에 안정적)
- 멱등성: `GetComponent == null` 후 AddComponent
- 씬 오브젝트 검색(비활성 포함): `FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)`, `FindFirstObjectByType<T>(FindObjectsInactive.Include)`
- 씬 NGM 제거: Additive 임시 로드 → `Undo.DestroyObjectImmediate` → `EditorSceneManager.SaveScene` → CloseScene
- Undo 지원: RegisterCreatedObjectUndo / SetTransformParent / RecordObject / RegisterCompleteObjectUndo

### 배치 관례와 저장 반영 (2026-08-10 확인 / 2026-08-21 복구 — 유일본)

> 2026-08-17 `675203ae` 로 `MEMORY.md` 에서 소실됐던 내용. 아래 4개 항목은
> `Assets/_Project/Docs/**` · `.claude/**` 어디에도 남아 있지 않아 여기로 복구한다.

- **에디터 1회성 셋업 스크립트 위치 = `Assets/Editor/Setup/`, 네임스페이스 `Hexiege.EditorTools`**
  (asmdef 없음 → Assembly-CSharp-Editor). 메뉴 접두사 관례는 `Hexiege/Setup/`.
  예외: 스킬 셋업 2종만 코디네이터 지시로 `Assets/_Project/Scripts/Editor/` + `Hexiege/Skill/` 에 있다.
- **저장 반영 필수**: `EditorUtility.SetDirty(obj)` **+** `EditorSceneManager.MarkSceneDirty(scene)`.
  **둘 중 하나라도 빠지면 씬에 저장되지 않는다** — 스크립트는 성공 로그를 찍고 변경은 사라진다.
- **멱등 헬퍼 `FindOrCreateChild(parent, name)`** 로 이름/컴포넌트를 찾아 재사용한다.
  단 **패널처럼 통째로 복제하는 대상은 "기존 제거 후 재복제"** 로 항상 1개를 보장한다.
- 이름 기반 탐색은 **타입 탐색 우선 + 이름은 폴백**. 못 찾으면 조용히 넘어가지 말고 경고를 남긴다
  (과거 사고: `_backButton` 이 `OffButton` 에 오연결됐다).
- 에디터 스크립트의 `Debug.Log` 진행 출력은 **허용**된다 — `LogRules.md` 는 **런타임 파일 로그** 규칙이다.

---

## DontDestroyOnLoad 규칙

- **루트 GameObject에만 작동** — 자식 배치 시 씬 전환마다 재생성+즉시파괴 반복
- DontDestroyOnLoad 오브젝트는 생성 씬 하나(예: Lobby)에만 배치. 다른 씬 중복 배치 금지
- SetActive(false) 상태에서 Awake() 미호출 → DontDestroyOnLoad 미등록. 숨김은 CanvasGroup.alpha=0으로
