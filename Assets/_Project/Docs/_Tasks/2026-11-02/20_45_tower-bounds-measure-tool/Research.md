# Research — 방어탑 3종 크기 측정 메뉴

## 이 문서는 무엇인가

인간 지원 스킬 **EMP**는 적 방어탑을 잠시 멈추게 한다. 멈춘 탑 위에는 「지금 무력화됐다」는 것을 보여 주는 **상태 VFX**(지지직거리는 전기 같은 효과)가 붙어야 한다. 그런데 세 종족의 방어탑(인간 CannonTower · 정령 RuneSpire · 초월 VineTower)은 모양도 키도 서로 다르다. 같은 크기의 VFX를 그대로 붙이면 어떤 탑에서는 VFX가 탑 허리에 걸리고, 어떤 탑에서는 탑을 다 덮어 버린다.

그래서 EMP 무력화 VFX는 **종족마다 크기 배율과 위치 보정을 따로 갖는 프리셋**으로 맞추기로 했다. 그 보정값을 정하려면 먼저 **탑이 실제로 몇 미터 높이이고 얼마나 넓은지**를 알아야 한다. 지금 그 값은 코드에도 문서에도 없다.

사용자는 2026-11-02 「ABC 모두 추천대로 진행하자」로 이 측정 방법을 정했다(상위 Task 결정 19, B): **에디터 메뉴를 작게 하나 추가해서, 탑 크기를 재 Console에 출력만 한다.** 측정 결과를 에셋에 쓰지 않고, 게임 실행 코드도 바꾸지 않는다. 사용자가 메뉴를 한 번 실행해 Console 결과를 알려 주면, 그 숫자를 EMP 스킬 코드 단계(상위 Task 2-B)에서 프리셋 값의 기준으로 쓴다.

이 문서는 그 메뉴를 만들기 전에 **지금 탑 프리팹이 어떻게 생겼는지, 유닛용 측정 도구를 어디까지 다시 쓸 수 있는지, 무엇을 주의해야 하는지**를 파일·행 단위로 확인한 기록이다. 구현 방법은 같은 폴더의 [Plan.md](Plan.md)에 있다. 이번 단계는 문서만 만든다 — 코드 · 에셋 · 다른 문서는 바꾸지 않았다.

**상위 Task:** [종족별 지원 스킬 기획](../14_25_race-support-skills-design/Research.md) — Research 2-13 ④(탑 크기와 프리셋 구조), Plan 5-1 결정 19(이 측정 방법), Plan 2-1 V6(탑 무력화 상태 VFX), Plan 4절 15(EMP 코드 단계 물음).

**표기:** `[확인]` = 문서 담당이 2026-11-02 HEAD `1bee2c9f` 작업 트리에서 직접 읽어 확인한 것 · `[전달 — 프로그래머]` = game-programmer의 읽기 확인을 메인 세션이 전달한 것 · `[계산]` = 확인한 값으로 계산한 것 · `[추정]` = 확인하지 않은 판단 · `[미확인]` = 이 단계에서 확인할 수 없었던 것 · `[제안]` = 결정 아님.

---

## 1. 결정과 범위 `[전달]`

| 항목 | 내용 | 출처 |
|---|---|---|
| 측정 방법 | 작은 에디터 메뉴를 추가해 **Console 출력**으로 잰다 | 사용자 「ABC 모두 추천대로 진행하자」(B) — 상위 Plan 5-1 결정 19 |
| 측정 함수 | 유닛 측정 도구의 `Measure(prefab)` 재사용. 공유 방식(직접 호출 · 분리 · 복사)은 프로그래머 판단 | 상위 Research 2-13 ④, 메인 세션 지시 |
| 이번 범위 밖 | EMP 무력화 VFX 종족별 프리셋 칸 추가(`BuildingEffectConfig`), 건물에 계속 붙는 VFX 경로 | 상위 Plan 4절 15 덧붙임 — 스킬 코드 단계 2-B |
| 이번 범위 밖 | 측정값을 에셋에 쓰기, 런타임 코드 변경 | 메인 세션 지시 |

---

## 2. 현재 상태

### 2-1. 방어탑 = `AutoTower` 하나, 종족별 프리팹 3쌍 `[확인]`

- 세 종족 방어탑은 모두 `BuildingType.AutoTower = 2`다 — `Assets/_Project/Scripts/Domain/Building/BuildingType.cs` 34행. 종족별 이름은 `GameSystemRules_Buildings.md` 「방어 타워 시스템」 머리 안내(99행): Human = CannonTower, Spirit = RuneSpire, Transcendence = VineTower.
- 실제 생성에 쓰는 프리팹은 Game 씬 `BuildingFactory`의 종족별 목록 `_humanPrefabs` · `_spiritPrefabs` · `_transcendencePrefabs`(`Assets/_Project/Scripts/Infrastructure/Factories/BuildingFactory.cs` 56 · 59 · 62행)에서 `type: 2` 줄의 `blue` · `red`다. Game.unity의 그 줄을 GUID로 풀면:

| 종족 | Blue 프리팹 | Red 프리팹 |
|---|---|---|
| Human | `Assets/_Project/Prefabs/Buildings/Human/Building_CannonTower_Blue.prefab` (GUID `51da62dc…`) | `…/Human/Building_CannonTower_Red.prefab` (`56409693…`) |
| Spirit | `Assets/_Project/Prefabs/Buildings/Spirit/Building_RuneSpire_Blue.prefab` (`48a391cc…`) | `…/Spirit/Building_RuneSpire_Red.prefab` (`50de052e…`) |
| Transcendence | `Assets/_Project/Prefabs/Buildings/Transcendence/Building_VineTower_Blue.prefab` (`c00d242c…`) | `…/Transcendence/Building_VineTower_Red.prefab` (`e637761d…`) |

- 6개 모두 존재한다. 같은 폴더의 `Building_FireSpire_*.prefab`은 정령 **생산 건물**(`FireSpire = 15`, 불 1단계 — `BuildingType.cs` 53행)이라 대상이 아니다.
- 프리팹 선택 코드: `BuildingFactory.cs` 187~206행 `GetPrefab` — 종족 목록에서 `type`이 같은 줄을 찾아 팀에 따라 `blue`/`red`를 돌려준다.

### 2-2. 프리팹 구조 — 루트 + 메시 자식 하나 `[확인]`

6개 프리팹은 모두 **GameObject 2개 · Transform 2개 · MeshFilter 1개 · MeshRenderer 1개**뿐이다(YAML 객체 종류 집계). 루트에는 Transform 외 컴포넌트가 없다 — `VisualRootProjector`도, `VisualRoot`라는 자식도 없다. 파티클 · 스프라이트 렌더러도 없다.

| 탑 | 메시 자식 이름 | 자식 위치 y | 자식 회전 | 자식 스케일 | 메시 (FBX) |
|---|---|---|---|---|---|
| CannonTower (Blue/Red 같음) | `Building_CannonTower_{Blue,Red}_Mesh` | 0.28 | X −90° (`{-0.7071068, 0, 0, 0.7071068}`) | 35 | `Assets/_Project/Models/Buildings/Human/CannonTower.fbx` |
| RuneSpire (Blue/Red 같음) | `Building_RuneSpire_{Blue,Red}_Mesh` | 0.32 | `{-0.27059808, 0.6532815, 0.6532815, 0.27059808}` | 40 | `…/Spirit/RuneSpire.fbx` |
| VineTower (Blue/Red 같음) | `Building_VineTower_{Blue,Red}_Mesh` | 0.06 | RuneSpire와 같은 값 | 30 | `…/Transcendence/VineTower.fbx` |

- 루트는 6개 모두 위치 0 · 회전 없음 · 스케일 1이다.
- 같은 종족의 Blue와 Red는 **같은 메시 · 같은 배치**를 쓴다(메시 GUID · 위치 · 회전 · 스케일 동일). 다른 것은 머티리얼로 보인다 `[추정 — 머티리얼 GUID만 보고 내용은 읽지 않았다]`.
- RuneSpire · VineTower 자식 회전을 계산하면 메시의 로컬 Z축이 월드 위(+Y)로 서고, 로컬 X · Y축은 수평면에서 **45° 비스듬히** 놓인다 `[계산]` (로컬 X → (−0.707, 0, −0.707), 로컬 Y → (−0.707, 0, 0.707), 로컬 Z → (0, 1, 0)). CannonTower는 X축 −90°라 로컬 축이 수평면과 나란하다.
- 위 스케일 · y값은 **모델을 화면에 맞추려는 배치 보정값**이지 탑의 실제 높이가 아니다 `[전달 — 프로그래머]`. 실제 높이는 메시 크기 × 이 배치로 정해진다.

### 2-3. 메시 Read/Write가 꺼져 있다 `[확인]`

세 FBX의 임포트 설정(`.fbx.meta`)은 모두 `isReadable: 0`(Read/Write 꺼짐) · `globalScale: 1` · `useFileScale: 1`이다(각 `.meta` 38 · 41 · 76행).

→ 유닛 측정 도구는 메시가 읽기 불가면 **정점 대신 메시 경계 상자의 꼭짓점 8개**로 잰다(`UnitRecipientFitMeasureTool.cs` 296~306행, 아래 2-4). 이 경우 결과의 의미는 2-5에 정리했다.

### 2-4. 유닛 측정 도구 `Measure(prefab)` — 재사용할 수 있는 부분 `[확인]`

파일: `Assets/Editor/Tools/UnitRecipientFitMeasureTool.cs` (네임스페이스 `Hexiege.EditorTools`, 클래스 `UnitRecipientFitMeasureTool`). 메뉴 `Hexiege/Effects/Measure Unit Recipient Fit`(100행).

| 부분 | 행 | 탑에 쓸 수 있나 |
|---|---|---|
| `Measure(GameObject prefab)` — `internal static`. 프리팹을 `LoadPrefabContents`로 격리해 열고, 루트를 원점 · 무회전에 두고, 기준 Transform 기준으로 점을 모아 높이 · 반경 · 발밑 위치를 낸 뒤 **저장하지 않고 닫는다**(250행 `UnloadPrefabContents`) | 206~252 | **그대로 쓸 수 있다.** 프리팹을 바꾸지 않는다 |
| `ResolveVisualRoot` — `VisualRootProjector`의 `_visualRoot` → 없으면 `VisualRoot` 자식 → 없으면 **루트** | 258~268 | 탑에는 둘 다 없으므로 **루트 기준**으로 잰다(2-2) |
| `CollectBodyPoints` — SkinnedMeshRenderer가 있으면 그것만, 없으면 MeshRenderer 전체 | 274~309 | 탑은 SkinnedMeshRenderer가 없어 **MeshRenderer 경로**(289~307행)를 탄다 |
| MeshRenderer 경로 — 읽기 가능하면 정점 전부, 아니면 `mesh.bounds` 꼭짓점 8개를 `localToWorldMatrix`로 옮김 | 296~306 | 탑 메시는 읽기 불가(2-3) → **경계 상자 경로**(`Method=BoundsFallback`) |
| 결과 구조 `Measurement` — `Ok` · `Height` · `Radius` · `FootOffset` · `RendererCount` · `Method` · `Failure` | 87~95 | 높이 · 반경 · 바닥 위치는 나온다. **최고점 y는 `FootOffset.y + Height`로 계산**해야 하고, X · Z 방향 반폭은 따로 없다 |
| 이상값 기준 `SuspiciousMaxHeight 1.2` · `SuspiciousMaxRadius 0.43` · `SuspiciousMinHeight 0.1`, `IsImplausible` | 80~84, 371~378 | **유닛 기준**이다(주석 76~78행: 반경 0.43 = 헥스 타일 폭의 절반). 탑에 그대로 적용하면 정상 탑도 경고될 수 있다 `[추정]` |
| `ReadProductionPrefabs` — Game 씬을 미리보기로 열어 `UnitFactory` 목록을 읽고 닫음 | 156~198 | `UnitFactory` 전용이다. 같은 방식으로 `BuildingFactory`를 읽는 함수는 새로 써야 한다 |
| `WriteTable` — `UnitEffectConfig.asset`에 저장 | 411~453 | **쓰지 않는다**(이번 범위는 출력만) |

- 어셈블리: `Assets/Editor` 아래에 `.asmdef`가 없다(프로젝트 안 `.asmdef`는 `Assets/Packages/ai.meshy/…/MeshyAssembly.asmdef` 하나뿐). 그래서 `Assets/Editor/Tools/`에 새 파일을 두면 같은 에디터 어셈블리라 `internal`인 `Measure`를 그대로 부를 수 있다 `[추정 — Unity 기본 어셈블리 규칙에 근거, 컴파일로 확인하지 않았다]`. 실제로 회귀 검사 파일이 이미 그렇게 부른다(아래).
- **회귀 스위트가 이 함수를 쓴다:** `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.VfxConnections.cs` 593~603행이 `Measure`로 Pistoleer · Tank를 재서 「Pistoleer 키 0.2~0.6」, 「유닛 이상값 아님」, 「Tank가 Pistoleer보다 큼」을 검사한다. 564~566행은 이상값 상수 셋을 직접 읽는다. → `Measure`나 그 상수를 **고치면** 회귀 스위트 결과가 바뀔 수 있다. 새 파일에서 **부르기만** 하면 영향이 없다 `[판단]`.

### 2-5. 경계 상자로 잴 때 결과가 뜻하는 것 `[판단 — Unity `Mesh.bounds` 정의(메시 로컬 공간의 정점 경계 상자)에 근거, 실행으로 확인하지 않았다]`

- **높이:** 세 탑 모두 메시의 로컬 한 축(CannonTower는 X −90° 회전으로, RuneSpire · VineTower는 로컬 Z가)이 정확히 월드 위로 선다(2-2 계산). 그래서 경계 상자의 그 축 길이가 그대로 높이가 되고, **정점으로 잰 값과 같다**.
- **반경:** 도구는 「XZ 가운데에서 가장 먼 점까지 거리」를 낸다(`UnitRecipientFitMeasureTool.cs` 234~242행). 점이 상자 꼭짓점이면 이 값은 **상자 수평 대각선의 절반**이다. 둥근 탑이면 실제 몸통 반경보다 **크게** 나온다(정사각 단면이면 약 √2배까지). RuneSpire · VineTower는 상자가 45° 돌아가 있어도 같은 값이다(중심에서 꼭짓점까지 거리는 회전해도 변하지 않는다).
- → 출력에는 「반경은 상자 기준 상한값」이라고 측정 방식을 함께 적어야 오해가 없다. 정확한 몸통 반경이 필요하면 정점을 읽어야 한다(Plan 3절 선택지).
- 참고 선례: 2026-10-21 CannonTower 포구 위치를 정할 때는 FBX 정점(1,483개)을 직접 읽어 프리팹 배치(y 0.28 · X −90° · 크기 35)를 적용해 계산했다 — `Assets/_Project/Scripts/Application/Combat/ProjectileDeliveryContracts.cs` 398~406행 주석 `[확인]`. 그 결과 포구 = 루트 기준 (0, 0.405, 0.17), 포신 반지름 ≈ 0.026. → CannonTower 측정 높이(바닥 기준)는 최소한 포구 높이 근처 이상이어야 맞다 — **결과 점검용 기준**으로 쓸 수 있다 `[추정]`.

### 2-6. 탑이 게임에 놓이는 기준점 `[확인]`

- 생성 위치: 헥스 좌표 → 월드 좌표 → 뷰 좌표에 `_buildingYOffset`을 더한 곳에 **프리팹 루트**를 둔다 — `BuildingFactory.cs` 159~174행. 그 값은 `GameConfig.BuildingYOffset = 0.1`(`Assets/_Project/Scripts/Infrastructure/Config/GameConfig.cs` 200행, `Assets/_Project/Resources/Config/GameConfig.asset` 36행).
- 회전: Human CannonTower만 상대 팀 탑이면 Y축 180°(`BuildingFactory.cs` 231~248행 `GetInitialRotation`, `GameSystemRules_Buildings.md` 「Human CannonTower 초기 방향」 규칙 11). RuneSpire · VineTower는 회전 없음.
- → 측정은 **프리팹 루트 기준**(원점 · 무회전)이 맞다. 무력화 VFX의 위치 보정도 탑 루트 기준으로 쓰게 될 것이기 때문이다 `[추정 — 건물에 붙는 VFX 경로는 아직 없다(상위 Research 2-13 ③ · ④), 붙이는 기준점은 2-B 단계에서 정한다]`. CannonTower가 180° 돌아도 높이는 그대로다. 수평 중심이 루트에서 벗어나 있으면 돌린 탑에서 그 방향이 뒤집힌다 — 출력에 수평 중심(X · Z)을 함께 내면 2-B에서 판단할 수 있다.

### 2-7. 업그레이드로 탑 모양이 바뀌는가 — 바뀌지 않는다 `[확인]`

- 건물 업그레이드는 **다른 BuildingType의 프리팹으로 갈아 끼우는** 방식이다 — `BuildingFactory.cs` 257~299행 `UpgradeBuildingObject`(새 타입으로 `GetPrefab` 후 새로 생성, 옛 것 파괴).
- 업그레이드 가능 여부는 `BuildingTypeHelper.CanUpgrade` = 단계 표에 다음 단계가 있는가다(`Assets/_Project/Scripts/Domain/Building/BuildingTypeHelper.cs` 132~141행). 그 표(57~94행)에는 **생산 건물만** 있고 `AutoTower`는 없다 → `CanUpgrade(AutoTower)`는 false다.
- → 탑은 업그레이드 단계가 없어 **종족당 메시 1종**이다. 단계별 측정은 필요 없다. (연구소 강화는 유닛 스탯 강화다 — `GameSystemRules.md` 인덱스 「GameSystemRules_Upgrade.md」 행.)

### 2-8. 로그 출력 규칙 `[확인]`

- 진단용 출력은 raw `Debug.Log`를 직접 부르지 않고 `GameLog`를 거친다 — `Assets/_Project/Docs/LogRules.md` 「1.14 금지 사항」 1. 유닛 측정 도구도 `GameLog.Dev.Info` / `GameLog.Dev.Warn`을 쓴다(`UnitRecipientFitMeasureTool.cs` 111 · 145 · 442행).
- 에디터에서는 `GameLog`가 Console과 함께 **런타임 로그 파일에도** 남긴다 — LogRules 「1.10 파일 기록 — 에디터 상시 활성」. 유닛 도구의 완료 창도 「Console(및 런타임 로그 파일)」이라고 안내한다(148행). → 「Console 출력만」은 **에셋에 쓰지 않는다**는 뜻으로 읽고, 로그 파일 한 줄은 규칙상 따라오는 것이다 `[판단]`.
- 한 줄 형식은 `key=value`이고 값 안에 `", "`를 넣지 않는다 — LogRules 「1.4 형식」, 유닛 도구 369행 주석.

### 2-9. 에디터 도구 유지 기준 `[확인]`

- `WORKFLOW.md` 「[5-2] 에디터 스크립트 최소 유지 기준」: 현재 필요한 도구만 유지하고, 「미래 재사용 가능성」만으로 영구 보존하지 않으며, 필요할 때 다시 작성한다. 1회성 스크립트는 실행 완료 · 재사용 없음을 확인한 뒤 [4]의 비활성화 · 사용자 테스트 · 최종 삭제 절차를 따른다.
- 같은 절의 「현재 진입점」 표는 5개(로그 저장 · 로그 ZIP · 강제 실패 · 회귀 스위트 · Root Pose)다. `UnitRecipientFitMeasureTool.cs`는 머리 주석에 「영구 보존 도구 … 삭제 금지」(2행)라고 적혀 있지만 이 표에는 없다 — 이번 범위 밖이라 기록만 한다.
- → 새 측정 메뉴를 **1회성으로 둘지, 계속 둘지**는 사용자 결정이 필요하다(Plan 6절 U-1). **[2026-11-02 결정 → Plan 6-1]** 지원 스킬 VFX 작업이 모두 끝날 때까지 유지(삭제 보류), 그 뒤 삭제 여부 재확인 — [5-2]와의 관계는 사용자 명시 결정에 의한 기간 한정 유지.

---

## 3. 영향 범위

| 대상 | 영향 | 근거 |
|---|---|---|
| 게임 실행 코드(Runtime) | **없음** — 에디터 폴더에 파일 1개 추가 | Plan 2절 |
| 에셋 · 프리팹 · 씬 | **없음** — 프리팹은 격리 로드 후 저장 없이 닫고, Game 씬은 미리보기로 열고 닫는다 | 2-4 (`Measure` 250행), 유닛 도구 156~198행 방식 |
| 회귀 스위트 | 기존 `Measure`를 **고치지 않으면** 없음 | 2-4 마지막 항목 |
| 멀티플레이 · 동기화 | 없음 — 에디터 전용 | — |
| 규칙 문서 | 바꿀 것 없음. 1회성으로 남길지에 따라 `WORKFLOW.md` [5-2] 표 갱신 필요 여부가 갈린다(U-1). **[2026-11-02]** U-1은 기간 한정 유지로 정해졌고, [5-2] 표 갱신은 이번에 하지 않았다(필요하면 별도 승인 — Plan 6-1) | 2-9 |

---

## 4. 발견한 부가 사항

- **이상값 기준이 유닛용이다** — 탑에 `IsImplausible`을 그대로 쓰면 반경 0.43 초과(상자 기준 반경은 커지기 쉬움, 2-5)로 정상 탑도 경고가 날 수 있다 `[추정]`. Plan은 탑에 유닛 기준을 적용하지 않는다.
- **반경 상한 문제** — 경계 상자 측정이라 반경이 실제보다 크게 나온다(2-5). EMP VFX의 가로 크기를 이 값으로 정하면 넓게 잡힐 수 있다. Plan 3절에 정점 측정 선택지를 기록했다.
- **유닛 도구 머리 주석 「영구 보존 · 삭제 금지」와 WORKFLOW [5-2] 표의 불일치** — 2-9. 이번 범위 밖, 사용자에게 보고만 한다.

---

## 5. 이 단계에서 하지 않은 것

- 탑 높이 · 반경의 실제 수치 — Unity 없이 FBX를 읽을 수 없어 재지 않았다 `[미확인]`. 메뉴 실행 결과로 채운다.
- 머티리얼 · 텍스처 확인, 탑 파괴 연출 확인 — 측정과 무관.
- EMP 프리셋 칸 설계, 건물에 붙는 VFX 경로 — 상위 Task 2-B.
- 코드 · 에셋 · 규칙 문서 · 상위 Task 문서 · 메모리 변경 — 없음. `.meta`도 만들지 않았다(Unity가 만든다).
