// ============================================================================
// TowerBoundsMeasureTool.cs  (에디터 전용 측정 메뉴 — Assets/Editor/Tools/)
//
// 무엇을 왜 재는가 (2026-11-02 tower-bounds-measure-tool):
//   인간 지원 스킬 EMP 는 적 방어탑을 잠시 멈추게 하고, 멈춘 탑 위에 「무력화됨」 상태 VFX 를 붙인다.
//   세 종족 방어탑(인간 CannonTower · 정령 RuneSpire · 초월 VineTower)은 키와 굵기가 서로 달라서,
//   VFX 를 종족별 크기 배율 · 위치 보정 프리셋으로 맞추기로 했다. 그 보정값의 기준이 되는
//   "탑의 실제 높이 · 바닥/꼭대기 위치 · 가로 중심 · 반경" 을 이 메뉴가 재서 Console 에 보여 준다.
//   이 메뉴 자체는 VFX 를 맞추지 않는다 — 숫자는 사람이 읽고 다음 단계(상위 Task 2-B)에서 쓴다.
//
// 아무것도 저장하지 않는다:
//   - 프리팹은 유닛 측정 도구의 Measure() 가 격리 로드(LoadPrefabContents)로 열고 저장 없이 닫는다.
//   - Game 씬은 "미리보기(preview)" 로만 열어 BuildingFactory 등록을 읽고 바로 닫는다
//     → 지금 편집 중인 씬과 그 저장 상태는 바뀌지 않는다.
//   - 설정 에셋(UnitEffectConfig · BuildingEffectConfig 등)에도 쓰지 않는다. 결과는 로그 한 줄씩뿐이다.
//   - 에디터에서는 GameLog 가 Console 과 함께 런타임 로그 파일에도 같은 줄을 남긴다(LogRules 1.10 — 규칙상 정상).
//
// 어떤 프리팹을 재는가 (이름 추측이 아니라 실제 게임 생성 연결을 따라간다 — Plan 2-2 P1):
//   Game 씬 BuildingFactory 의 종족별 목록(_humanPrefabs · _spiritPrefabs · _transcendencePrefabs)에서
//   type == AutoTower(2) 인 줄의 blue · red 프리팹. 런타임 BuildingFactory.GetPrefab 처럼
//   "목록에서 처음 만나는 AutoTower 줄" 을 쓴다. 빈 칸(blue 또는 red 없음)은 「없음」 한 줄을 남기고 건너뛴다.
//
// 측정 기준과 값의 의미 (단위 월드 미터, 기준점 = 프리팹 루트를 원점 · 무회전에 둔 상태):
//   탑 프리팹에는 VisualRoot 가 없어서 Measure() 가 자동으로 "프리팹 루트" 기준으로 잰다(Pivot=PrefabRoot).
//   게임도 탑을 생성할 때 프리팹 루트를 타일 위에 두므로(BuildingFactory 생성 위치) 같은 기준이다.
//     Height  = 탑 최저점 Y ~ 최고점 Y 사이 높이
//     BottomY = 루트 기준 최저점 Y,  TopY = BottomY + Height (최고점 Y)
//     CenterX · CenterZ = 루트 기준 가로(XZ) 중심. 0 에서 벗어나 있으면, 상대 팀 CannonTower 가
//                        Y축 180° 로 돌아 놓일 때(Buildings 규칙 11) 그 치우침 방향도 뒤집힌다.
//     Radius  = 가로 중심에서 가장 먼 점까지의 수평 거리.
//
// 🔴 반경은 "상한값" 일 수 있다:
//   세 탑의 메시는 Read/Write 가 꺼져 있어 정점을 읽을 수 없다. 그래서 Measure() 는 정점 대신
//   메시 경계 상자(bounds)의 꼭짓점 8개로 잰다(Method=BoundsFallback). 그러면 Radius 는
//   "상자 수평 대각선의 절반" 이라 둥근 탑의 실제 몸통 반경보다 크게 나온다(정사각 단면이면 최대 약 √2배).
//   높이는 탑의 세로축이 상자 축과 나란히 서 있어 정확하다. 그래서 출력에 RadiusKind=UpperBound 를 함께 남긴다.
//
// 유닛 이상값 기준(UnitRecipientFitMeasureTool.IsImplausible)은 쓰지 않는다:
//   그 기준(키 1.2m · 반경 0.43m)은 "타일 위에 서는 유닛" 용이라, 정상 탑도 경고될 수 있다.
//
// 언제 다시 실행하나:
//   탑 모델(FBX) · 프리팹 배치(자식 위치 · 회전 · 스케일)를 바꿨거나 BuildingFactory 의 탑 등록을 바꿨을 때.
//
// 유지 기간 (사용자 결정 2026-11-02, Plan 6-1 U-1):
//   지원 스킬 VFX 작업이 모두 끝날 때까지 유지(삭제 보류). 그 뒤 삭제 여부를 사용자에게 다시 확인한다.
//
// 측정 코드 공유 방식 (Plan 3절 S1):
//   유닛 측정 도구의 Measure() 를 "부르기만" 한다(같은 에디터 어셈블리라 internal 접근 가능).
//   Measure() 는 회귀 스위트(RunUnitActionSelfValidation.VfxConnections.cs)도 쓰므로 여기서 고치지 않는다.
//
// 상위 Task: Assets/_Project/Docs/_Tasks/2026-11-02/20_45_tower-bounds-measure-tool/{Research,Plan}.md
//           (상위 기획: Assets/_Project/Docs/_Tasks/2026-11-02/14_25_race-support-skills-design/Plan.md 5-1 결정 19)
// ============================================================================

using System.Collections.Generic;
using System.Globalization;
using Hexiege.Application;
using Hexiege.Domain;
using Hexiege.Infrastructure;
using Hexiege.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// Game 씬 BuildingFactory 에 등록된 종족별 방어탑(AutoTower) 프리팹 크기를 재서 Console 에 출력만 하는 메뉴 도구.
    /// 프리팹 · 씬 · 설정 에셋은 하나도 바꾸지 않는다.
    /// </summary>
    public static class TowerBoundsMeasureTool
    {
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string LogClass = nameof(TowerBoundsMeasureTool);

        // 로그 System 은 「기능」 기준(LogRules 카테고리 규칙)이다. 이 값은 전투 상태 VFX 크기 맞춤용이고,
        // 같은 목적의 유닛 측정 도구(UnitRecipientFitMeasureTool)도 "Combat" 을 쓰므로 집계가 갈라지지 않게 맞춘다.
        private const string LogSystem = "Combat";
        private const string DialogTitle = "Tower Bounds";

        /// <summary>Blue/Red 높이 차이를 경고로 남기는 비율(5%) — 유닛 도구 WarnIfTeamsDiffer 와 같은 기준.</summary>
        private const float TeamMismatchRatio = 0.05f;

        /// <summary>
        /// 무엇을 기준으로 쟀는지 알려 주는 고정 값(Plan 2-3). 탑에는 VisualRoot 가 없어 Measure() 가 루트 기준으로 잰다.
        /// </summary>
        private const string PivotPrefabRoot = "PrefabRoot";

        // Measure() 가 돌려주는 측정 방식 이름 중 "경계 상자로 잼" 을 뜻하는 토큰(유닛 도구 CollectBodyPoints 의 값).
        private const string BoundsFallbackMethod = "BoundsFallback";

        // Radius 의 성격 표시 값. 경계 상자가 하나라도 섞이면 상한값, 전부 정점으로 쟀으면 정점 기준 값.
        private const string RadiusKindUpperBound = "UpperBound";
        private const string RadiusKindVertices = "Vertices";

        // 런타임 BuildingFactory 의 직렬화 필드 이름(BuildingFactory.cs 의 종족별 목록) — 종족과 짝지어 둔다.
        //   필드 이름은 private 이라 nameof 로 참조할 수 없어 문자열로 둔다. 이름이 바뀌면 FindProperty 가 null 이 되고,
        //   그 경우 아래 ReadTowerPrefabs 가 「목록 없음」 경고를 남긴다(조용히 0개로 끝나지 않게).
        private static readonly (RaceId Race, string ListName)[] RaceLists =
        {
            (RaceId.Human, "_humanPrefabs"),
            (RaceId.Spirit, "_spiritPrefabs"),
            (RaceId.Transcendence, "_transcendencePrefabs"),
        };

        /// <summary>종족 하나의 방어탑 등록 줄(Blue/Red 프리팹). Found=false 면 그 종족 목록에 AutoTower 줄이 없다.</summary>
        private struct TowerRow
        {
            public RaceId Race;
            public bool Found;
            public GameObject Blue, Red;
        }

        /// <summary>실행 결과 집계(마지막 요약 한 줄과 완료 창에 쓴다).</summary>
        private struct Tally
        {
            public int Targets;  // 잴 프리팹 칸 수(종족 3 × 팀 2 = 6 이 정상)
            public int Measured; // 측정 성공
            public int Failed;   // 측정 실패(Measure 의 Ok == false)
            public int Missing;  // 등록이 비어 있어 건너뛴 칸
        }

        /// <summary>
        /// 메뉴: 종족별 방어탑 프리팹(3종 × Blue/Red)의 높이 · 바닥/꼭대기 Y · 가로 중심 · 반경을 재서 Console 에 출력한다.
        /// 아무것도 저장하지 않으므로 실행 전 확인 창은 없다.
        /// </summary>
        [MenuItem("Hexiege/Effects/Measure Tower Bounds")]
        public static void MeasureAndLog()
        {
            // Play 중에는 프리팹 격리 로드 · 미리보기 씬 열기를 피한다(유닛 도구와 같은 안전장치).
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(DialogTitle, "Play 모드에서는 실행할 수 없습니다. Play를 멈춘 뒤 다시 실행하세요.", "확인");
                return;
            }

            List<TowerRow> rows = ReadTowerPrefabs();
            if (rows == null) return; // 사유는 ReadTowerPrefabs 가 이미 로그 · 창으로 알렸다.

            var tally = new Tally();
            foreach (TowerRow row in rows)
                MeasureRace(row, ref tally);

            GameLog.Dev.Info(LogSystem, LogClass, "방어탑 크기 측정 완료 — 출력만 했고 저장한 에셋은 없다",
                string.Format(CultureInfo.InvariantCulture, "Targets={0}, Measured={1}, Failed={2}, Missing={3}",
                    tally.Targets, tally.Measured, tally.Failed, tally.Missing));
            EditorUtility.DisplayDialog(DialogTitle,
                "완료: 대상 " + tally.Targets + "칸 / 성공 " + tally.Measured + " / 실패 " + tally.Failed
                + " / 등록 없음 " + tally.Missing + "\n\n"
                + "자세한 값은 Console(및 런타임 로그 파일)의 [" + LogSystem + "/" + LogClass + "] 줄을 확인하세요.\n"
                + "저장한 에셋은 없습니다.", "확인");
        }

        /// <summary>
        /// 종족 하나의 Blue · Red 탑을 재고 한 줄씩 출력한다. 둘 다 성공하면 두 팀 높이 차이도 확인한다.
        /// </summary>
        /// <param name="row">그 종족의 방어탑 등록 줄.</param>
        /// <param name="tally">집계(이 함수가 늘린다).</param>
        private static void MeasureRace(TowerRow row, ref Tally tally)
        {
            // 종족 목록에 AutoTower 줄 자체가 없으면 두 팀 칸이 모두 비어 있는 것과 같다 → 두 칸 다 「없음」.
            bool blueOk = MeasureOne(row.Race, TeamId.Blue, row.Found ? row.Blue : null, ref tally,
                out UnitRecipientFitMeasureTool.Measurement blue);
            bool redOk = MeasureOne(row.Race, TeamId.Red, row.Found ? row.Red : null, ref tally,
                out UnitRecipientFitMeasureTool.Measurement red);
            if (blueOk && redOk) WarnIfTeamsDiffer(row.Race, blue, red);
        }

        /// <summary>
        /// 탑 프리팹 하나를 재서 결과 한 줄(성공 Info · 실패 Warn · 빈 칸 Info)을 남긴다.
        /// </summary>
        /// <param name="race">종족(로그용).</param>
        /// <param name="team">팀(로그용).</param>
        /// <param name="prefab">잴 프리팹. null 이면 등록이 비어 있는 칸이다.</param>
        /// <param name="tally">집계(이 함수가 늘린다).</param>
        /// <param name="m">측정 결과(성공했을 때만 의미가 있다).</param>
        /// <returns>측정에 성공했으면 true.</returns>
        private static bool MeasureOne(RaceId race, TeamId team, GameObject prefab, ref Tally tally,
            out UnitRecipientFitMeasureTool.Measurement m)
        {
            m = default;
            tally.Targets++;
            if (prefab == null)
            {
                // 빈 칸은 실패가 아니라 "잴 대상이 없음" 이다. 지금은 6칸이 모두 차 있어야 정상(Research 2-1).
                tally.Missing++;
                GameLog.Dev.Info(LogSystem, LogClass, "방어탑 프리팹 등록이 비어 있어 건너뛴다(없음)",
                    $"Race={race}, Team={team}, Prefab=None");
                return false;
            }

            WarnIfPivotIsNotRoot(race, team, prefab);
            // 측정 자체는 유닛 도구 코드를 그대로 쓴다(Plan 3절 S1 — Measure 는 고치지 않는다).
            m = UnitRecipientFitMeasureTool.Measure(prefab);
            if (!m.Ok)
            {
                tally.Failed++;
                GameLog.Dev.Warn(LogSystem, LogClass, "방어탑 크기 측정 실패",
                    $"Race={race}, Team={team}, Prefab={prefab.name}, Reason={m.Failure}");
                return false;
            }

            tally.Measured++;
            LogMeasurement(race, team, prefab.name, m);
            return true;
        }

        /// <summary>
        /// 측정 성공 한 줄을 남긴다. 키 순서 · 이름은 Plan 2-3 표를 따른다.
        /// </summary>
        /// <param name="race">종족.</param>
        /// <param name="team">팀.</param>
        /// <param name="prefabName">프리팹 이름.</param>
        /// <param name="m">측정 결과(Ok == true).</param>
        private static void LogMeasurement(RaceId race, TeamId team, string prefabName,
            UnitRecipientFitMeasureTool.Measurement m)
        {
            // Measure() 의 FootOffset = (가로 중심 X, 최저점 Y, 가로 중심 Z) — 기준점(여기서는 프리팹 루트) 기준이다.
            float bottomY = m.FootOffset.y;
            float topY = bottomY + m.Height;
            // 숫자는 CultureInfo.InvariantCulture + 소수 셋째 자리(유닛 도구와 같은 형식).
            //   지역 설정이 소수점을 쉼표로 쓰는 PC 에서도 "0,405" 가 아니라 "0.405" 로 찍혀야 값 안에 ", " 가 섞이지 않는다.
            GameLog.Dev.Info(LogSystem, LogClass, "방어탑 크기 측정값",
                string.Format(CultureInfo.InvariantCulture,
                    "Race={0}, Team={1}, Prefab={2}, Height={3:0.###}, BottomY={4:0.###}, TopY={5:0.###}, "
                    + "CenterX={6:0.###}, CenterZ={7:0.###}, Radius={8:0.###}, RadiusKind={9}, "
                    + "Renderers={10}, Method={11}, Pivot={12}",
                    race, team, prefabName, m.Height, bottomY, topY,
                    m.FootOffset.x, m.FootOffset.z, m.Radius, ResolveRadiusKind(m.Method),
                    m.RendererCount, m.Method, PivotPrefabRoot));
        }

        /// <summary>
        /// 측정 방식 이름으로 Radius 의 성격을 정한다. 경계 상자가 하나라도 섞였으면 상한값(UpperBound)이다.
        /// </summary>
        /// <param name="method">Measure() 가 돌려준 측정 방식(예: BoundsFallback, MeshVertices, 둘을 '+' 로 이은 값).</param>
        /// <returns>UpperBound 또는 Vertices.</returns>
        private static string ResolveRadiusKind(string method) =>
            method != null && method.Contains(BoundsFallbackMethod) ? RadiusKindUpperBound : RadiusKindVertices;

        /// <summary>
        /// 출력의 Pivot=PrefabRoot 가 사실인지 확인한다. Measure() 는 VisualRootProjector 나 "VisualRoot" 자식이 있으면
        /// 그쪽을 기준으로 재므로, 탑 프리팹에 그런 것이 생기면 값의 기준점이 루트가 아니게 된다 → 경고로 알린다.
        /// (프리팹 에셋을 읽기만 한다 — 수정 없음.)
        /// </summary>
        /// <param name="race">종족(로그용).</param>
        /// <param name="team">팀(로그용).</param>
        /// <param name="prefab">확인할 프리팹 에셋.</param>
        private static void WarnIfPivotIsNotRoot(RaceId race, TeamId team, GameObject prefab)
        {
            bool hasProjector = prefab.GetComponent<VisualRootProjector>() != null;
            bool hasVisualRootChild = prefab.transform.Find("VisualRoot") != null;
            if (!hasProjector && !hasVisualRootChild) return;
            GameLog.Dev.Warn(LogSystem, LogClass,
                "방어탑 프리팹에 VisualRoot 가 있어 측정 기준이 프리팹 루트가 아닐 수 있다 — Pivot 값을 믿지 말고 프리팹 구조를 확인",
                $"Race={race}, Team={team}, Prefab={prefab.name}, HasProjector={hasProjector}, HasVisualRootChild={hasVisualRootChild}");
        }

        /// <summary>
        /// 같은 종족 Blue · Red 탑 높이가 5% 넘게 다르면 경고를 남긴다(지금은 같은 메시 · 배치라 같아야 한다).
        /// </summary>
        /// <param name="race">종족.</param>
        /// <param name="blue">Blue 측정 결과(Ok).</param>
        /// <param name="red">Red 측정 결과(Ok).</param>
        private static void WarnIfTeamsDiffer(RaceId race, UnitRecipientFitMeasureTool.Measurement blue,
            UnitRecipientFitMeasureTool.Measurement red)
        {
            if (Mathf.Abs(red.Height - blue.Height) <= blue.Height * TeamMismatchRatio) return;
            GameLog.Dev.Warn(LogSystem, LogClass, "같은 종족 Blue/Red 방어탑 높이가 5% 넘게 다르다 — 프리팹 배치 확인",
                string.Format(CultureInfo.InvariantCulture, "Race={0}, BlueHeight={1:0.###}, RedHeight={2:0.###}",
                    race, blue.Height, red.Height));
        }

        /// <summary>
        /// Game 씬을 미리보기로 열어 BuildingFactory 의 종족별 목록에서 AutoTower 줄(Blue/Red)을 읽는다.
        /// 씬은 바로 닫으며 현재 편집 중인 씬에는 영향이 없다.
        /// </summary>
        /// <returns>종족 3개 순서(Human · Spirit · Transcendence)의 등록 줄. BuildingFactory 가 정확히 1개가 아니면 null.</returns>
        private static List<TowerRow> ReadTowerPrefabs()
        {
            var scene = EditorSceneManager.OpenPreviewScene(GameScenePath);
            try
            {
                var factories = new List<BuildingFactory>();
                foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                    factories.AddRange(sceneRoot.GetComponentsInChildren<BuildingFactory>(true));

                // 0개면 씬 구성이 바뀐 것이고, 2개 이상이면 어느 쪽이 게임에서 쓰이는지 알 수 없다 → 추측하지 않고 멈춘다.
                if (factories.Count != 1)
                {
                    GameLog.Dev.Warn(LogSystem, LogClass, "Game 씬의 BuildingFactory 가 정확히 1개가 아니라 측정을 중단했다",
                        "Factories=" + factories.Count);
                    EditorUtility.DisplayDialog(DialogTitle,
                        "Game 씬의 BuildingFactory 가 1개가 아닙니다(" + factories.Count + "개). 측정을 중단합니다.", "확인");
                    return null;
                }

                var serialized = new SerializedObject(factories[0]);
                var rows = new List<TowerRow>(RaceLists.Length);
                foreach (var (race, listName) in RaceLists)
                    rows.Add(ReadTowerRow(serialized, race, listName));
                return rows;
            }
            finally
            {
                // 미리보기 씬은 반드시 닫는다(예외가 나도). 닫지 않으면 에디터 메모리에 씬이 남는다.
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        /// <summary>
        /// 종족 목록 하나에서 처음 만나는 AutoTower 줄을 찾는다(런타임 BuildingFactory.GetPrefab 과 같은 규칙).
        /// </summary>
        /// <param name="serialized">BuildingFactory 의 SerializedObject.</param>
        /// <param name="race">종족.</param>
        /// <param name="listName">그 종족 목록의 직렬화 필드 이름.</param>
        /// <returns>등록 줄. 목록이 없거나 AutoTower 줄이 없으면 Found=false.</returns>
        private static TowerRow ReadTowerRow(SerializedObject serialized, RaceId race, string listName)
        {
            var row = new TowerRow { Race = race };
            SerializedProperty list = serialized.FindProperty(listName);
            if (list == null || !list.isArray)
            {
                // 필드 이름이 바뀐 경우 — 조용히 「없음」 으로만 보이지 않게 원인을 따로 남긴다.
                GameLog.Dev.Warn(LogSystem, LogClass, "BuildingFactory 에서 종족 프리팹 목록 필드를 찾지 못했다",
                    $"Race={race}, Field={listName}");
                return row;
            }
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                // enum 프로퍼티의 intValue 는 enum 의 실제 숫자 값이다(AutoTower = 2).
                if ((BuildingType)entry.FindPropertyRelative("type").intValue != BuildingType.AutoTower) continue;
                row.Found = true;
                row.Blue = entry.FindPropertyRelative("blue").objectReferenceValue as GameObject;
                row.Red = entry.FindPropertyRelative("red").objectReferenceValue as GameObject;
                break; // 런타임도 첫 줄만 쓴다 — 뒤에 같은 타입 줄이 더 있어도 게임에서는 쓰이지 않는다.
            }
            return row;
        }
    }
}
