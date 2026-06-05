using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using Unity.Netcode.Components;
using Hexiege.Presentation;
using Hexiege.Infrastructure;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// 신규 유닛 프리팹(32개)에 게임 동작에 필요한 컴포넌트를 자동 부착하는 1회성 에디터 도구.
    ///
    /// 배경:
    ///   아트 작업이 막 끝난 신규 유닛 프리팹은 Root에 Transform만, _Mesh 자식에 Animator만 들어있다.
    ///   하지만 게임에서 정상 동작하려면 Root에 UnitView/NetworkObject/NetworkTransform/NetworkUnit이,
    ///   _Mesh 자식에 AnimationEventRelay가 필요하다.
    ///   이 도구는 기존 완성 프리팹(Pistoleer 등)과 동일한 구성을 신규 프리팹에 한 번에 적용한다.
    ///
    /// 사용법:
    ///   Unity 상단 메뉴 → Hexiege/Setup/신규 유닛 컴포넌트 부착
    ///
    /// 안전성:
    ///   - 각 컴포넌트는 "없을 때만" 추가한다(GetComponent == null 확인). 이미 있으면 건드리지 않으므로
    ///     여러 번 실행해도 중복 부착되지 않는다(멱등성 보장).
    ///   - 한 프리팹에서 예외가 나도 나머지 프리팹 처리는 계속 진행한다.
    /// </summary>
    public static class SetupNewUnitPrefabs
    {
        // ─────────────────────────────────────────────────────────────────────
        // UnitView 인스펙터 값 — 기존 완성 프리팹(Unit_Pistoleer_Blue)에서 확인된 실제 값.
        // 직렬화 필드명은 UnitView.cs의 private [SerializeField] 필드명과 정확히 일치해야 한다.
        // ─────────────────────────────────────────────────────────────────────
        private const float IdleToWalkBlend = 0.1f;   // Idle → Walk 블렌드 시간
        private const float ToAttackBlend = 0.08f;     // (Idle/Walk) → Attack 블렌드 시간
        private const float AttackToWalkBlend = 0.1f;  // Attack → Walk 블렌드 시간
        private const float RotationSpeed = 270f;      // 유닛 회전 속도(도/초)

        // 자식 GameObject 이름에 이 문자열이 포함되어 있으면 "메시 오브젝트"로 간주한다.
        // 예) Unit_BattleAxe_Blue_Mesh
        private const string MeshChildNameKeyword = "_Mesh";

        /// <summary>
        /// 대상 프리팹 32개의 에셋 경로 목록.
        /// 종족별(Human / Spirit / Transcendence) 폴더에 Blue/Red 한 쌍씩 존재한다.
        /// 기존 완성 프리팹(Pistoleer, Assault, Sniper 등)은 의도적으로 제외했다.
        /// </summary>
        private static readonly string[] TargetPrefabPaths =
        {
            // ── Human ──────────────────────────────────────────────
            "Assets/_Project/Prefabs/Units/Human/Unit_BattleAxe_Blue.prefab",
            "Assets/_Project/Prefabs/Units/Human/Unit_BattleAxe_Red.prefab",
            "Assets/_Project/Prefabs/Units/Human/Unit_SpearMan_Blue.prefab",
            "Assets/_Project/Prefabs/Units/Human/Unit_SpearMan_Red.prefab",
            "Assets/_Project/Prefabs/Units/Human/Unit_LittleKnight_Blue.prefab",
            "Assets/_Project/Prefabs/Units/Human/Unit_LittleKnight_Red.prefab",
            "Assets/_Project/Prefabs/Units/Human/Unit_CannonCart_Blue.prefab",
            "Assets/_Project/Prefabs/Units/Human/Unit_CannonCart_Red.prefab",
            "Assets/_Project/Prefabs/Units/Human/Unit_Tank_Blue.prefab",
            "Assets/_Project/Prefabs/Units/Human/Unit_Tank_Red.prefab",

            // ── Spirit ─────────────────────────────────────────────
            "Assets/_Project/Prefabs/Units/Spirit/Unit_BoulderSpirit_Blue.prefab",
            "Assets/_Project/Prefabs/Units/Spirit/Unit_BoulderSpirit_Red.prefab",
            "Assets/_Project/Prefabs/Units/Spirit/Unit_DustSpirit_Blue.prefab",
            "Assets/_Project/Prefabs/Units/Spirit/Unit_DustSpirit_Red.prefab",
            "Assets/_Project/Prefabs/Units/Spirit/Unit_QuakeSpirit_Blue.prefab",
            "Assets/_Project/Prefabs/Units/Spirit/Unit_QuakeSpirit_Red.prefab",
            "Assets/_Project/Prefabs/Units/Spirit/Unit_TideSpirit_Blue.prefab",
            "Assets/_Project/Prefabs/Units/Spirit/Unit_TideSpirit_Red.prefab",
            "Assets/_Project/Prefabs/Units/Spirit/Unit_TorrentSpirit_Blue.prefab",
            "Assets/_Project/Prefabs/Units/Spirit/Unit_TorrentSpirit_Red.prefab",
            "Assets/_Project/Prefabs/Units/Spirit/Unit_StreamSpirit_Blue.prefab",
            "Assets/_Project/Prefabs/Units/Spirit/Unit_StreamSpirit_Red.prefab",

            // ── Transcendence ──────────────────────────────────────
            "Assets/_Project/Prefabs/Units/Transcendence/Unit_EagleArcher_Blue.prefab",
            "Assets/_Project/Prefabs/Units/Transcendence/Unit_EagleArcher_Red.prefab",
            "Assets/_Project/Prefabs/Units/Transcendence/Unit_RabbitTrickster_Blue.prefab",
            "Assets/_Project/Prefabs/Units/Transcendence/Unit_RabbitTrickster_Red.prefab",
            "Assets/_Project/Prefabs/Units/Transcendence/Unit_MushroomBomber_Blue.prefab",
            "Assets/_Project/Prefabs/Units/Transcendence/Unit_MushroomBomber_Red.prefab",
            "Assets/_Project/Prefabs/Units/Transcendence/Unit_RhinoBreaker_Blue.prefab",
            "Assets/_Project/Prefabs/Units/Transcendence/Unit_RhinoBreaker_Red.prefab",
            "Assets/_Project/Prefabs/Units/Transcendence/Unit_BloomFairy_Blue.prefab",
            "Assets/_Project/Prefabs/Units/Transcendence/Unit_BloomFairy_Red.prefab",
        };

        /// <summary>
        /// 메뉴 진입점. 대상 프리팹을 순회하며 컴포넌트를 부착하고, 마지막에 결과를 로그로 남긴다.
        /// </summary>
        [MenuItem("Hexiege/Setup/신규 유닛 컴포넌트 부착")]
        public static void Run()
        {
            int total = TargetPrefabPaths.Length;
            int success = 0;

            foreach (string path in TargetPrefabPaths)
            {
                // 프리팹 한 개를 처리한다. 실패해도 다음 프리팹으로 넘어가야 하므로 try-catch로 감싼다.
                try
                {
                    ProcessSinglePrefab(path);
                    success++;
                }
                catch (System.Exception ex)
                {
                    // 어떤 프리팹에서 무슨 에러가 났는지 명확히 남긴다.
                    Debug.LogError($"[SetupNewUnitPrefabs] 처리 실패: {path}\n{ex}");
                }
            }

            // 변경한 에셋을 디스크에 확실히 반영한다.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SetupNewUnitPrefabs] 완료: {success}/{total} 프리팹 처리됨");
        }

        /// <summary>
        /// 프리팹 하나를 편집 모드로 열어 컴포넌트를 부착한 뒤 저장하고 닫는다.
        /// </summary>
        /// <param name="path">처리할 프리팹의 에셋 경로</param>
        private static void ProcessSinglePrefab(string path)
        {
            // 경로에 실제 프리팹이 있는지 먼저 확인한다(오타/이동으로 인한 null 방지).
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefabAsset == null)
            {
                Debug.LogWarning($"[SetupNewUnitPrefabs] 프리팹을 찾을 수 없음(경로 확인 필요): {path}");
                return;
            }

            // LoadPrefabContents: 프리팹을 "편집용 임시 인스턴스"로 메모리에 로드한다.
            // 이 root에 가한 변경은 SaveAsPrefabAsset을 호출해야 디스크에 반영된다.
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // [Root] 게임 동작에 필요한 컴포넌트 부착
                SetupRootComponents(root);

                // [_Mesh 자식] AnimationEventRelay 부착
                SetupMeshChild(root, path);

                // 변경 사항을 원본 프리팹 에셋에 저장한다.
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                // 성공/실패와 무관하게 임시 인스턴스는 반드시 언로드한다(메모리 누수 방지).
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Root GameObject에 UnitView / NetworkObject / NetworkTransform / NetworkUnit을 부착한다.
        /// 이미 존재하는 컴포넌트는 건너뛴다(중복 방지).
        /// </summary>
        private static void SetupRootComponents(GameObject root)
        {
            // ── 1) UnitView ──────────────────────────────────────────
            // 유닛의 애니메이션 블렌드/회전 등 프레젠테이션 동작을 담당.
            var unitView = root.GetComponent<UnitView>();
            if (unitView == null)
            {
                unitView = root.AddComponent<UnitView>();
            }
            // 인스펙터 값(블렌드 시간, 회전 속도)을 설정한다.
            // private [SerializeField] 필드라 코드에서 직접 접근할 수 없으므로 SerializedObject로 설정한다.
            ApplyUnitViewValues(unitView);

            // ── 2) NetworkObject ─────────────────────────────────────
            // 네트워크 동기화의 기본 단위. AddComponent만으로 적절한 기본값이 들어간다
            // (Ownership=1, SynchronizeTransform=true, SpawnWithObservers=true 등은 컴포넌트 기본값).
            if (root.GetComponent<NetworkObject>() == null)
            {
                root.AddComponent<NetworkObject>();
            }

            // ── 3) NetworkTransform ──────────────────────────────────
            // 위치/회전/스케일을 네트워크로 동기화. 동기화 축을 명시적으로 설정한다.
            var networkTransform = root.GetComponent<NetworkTransform>();
            if (networkTransform == null)
            {
                networkTransform = root.AddComponent<NetworkTransform>();
            }
            ApplyNetworkTransformValues(networkTransform);

            // ── 4) NetworkUnit ───────────────────────────────────────
            // 유닛 고유의 네트워크 로직(Infrastructure 레이어). 별도 인스펙터 설정 없이 부착만 한다.
            if (root.GetComponent<NetworkUnit>() == null)
            {
                root.AddComponent<NetworkUnit>();
            }
        }

        /// <summary>
        /// UnitView의 private [SerializeField] 필드 값을 SerializedObject를 통해 설정한다.
        /// 필드명은 UnitView.cs의 실제 필드명과 정확히 일치해야 한다.
        /// </summary>
        private static void ApplyUnitViewValues(UnitView unitView)
        {
            var so = new SerializedObject(unitView);

            // 각 필드를 이름으로 찾아 값을 넣는다. 혹시 필드명이 바뀌면 FindProperty가 null을 반환하므로
            // null 체크 후 경고를 남겨 조용히 실패하지 않도록 한다.
            SetFloatProperty(so, "_idleToWalkBlend", IdleToWalkBlend);
            SetFloatProperty(so, "_toAttackBlend", ToAttackBlend);
            SetFloatProperty(so, "_attackToWalkBlend", AttackToWalkBlend);
            SetFloatProperty(so, "_rotationSpeed", RotationSpeed);

            // 변경한 직렬화 값을 컴포넌트에 반영한다(Undo 기록 없이 — 1회성 도구이므로).
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// NetworkTransform의 동기화 축/보간 설정을 SerializedObject로 설정한다.
        /// 필드명(SyncPositionX 등)은 NGO 2.9.2 NetworkTransform의 직렬화 필드명(Pascal Case)이다.
        /// </summary>
        private static void ApplyNetworkTransformValues(NetworkTransform nt)
        {
            var so = new SerializedObject(nt);

            // 위치: X/Y/Z 모두 동기화
            SetBoolProperty(so, "SyncPositionX", true);
            SetBoolProperty(so, "SyncPositionY", true);
            SetBoolProperty(so, "SyncPositionZ", true);

            // 회전: Y축만 동기화(유닛은 Y축 회전으로만 방향을 표현하므로 X/Z는 불필요)
            SetBoolProperty(so, "SyncRotAngleX", false);
            SetBoolProperty(so, "SyncRotAngleY", true);
            SetBoolProperty(so, "SyncRotAngleZ", false);

            // 스케일: X/Y/Z 모두 동기화
            SetBoolProperty(so, "SyncScaleX", true);
            SetBoolProperty(so, "SyncScaleY", true);
            SetBoolProperty(so, "SyncScaleZ", true);

            // 보간 사용(부드러운 이동 표현)
            SetBoolProperty(so, "Interpolate", true);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 이름에 "_Mesh"가 포함된 자식을 찾아 AnimationEventRelay를 부착한다.
        /// 자식을 찾지 못하면 경고만 남기고 넘어간다(나머지 처리는 정상 진행).
        /// </summary>
        private static void SetupMeshChild(GameObject root, string path)
        {
            Transform meshChild = FindMeshChild(root.transform);
            if (meshChild == null)
            {
                Debug.LogWarning($"[SetupNewUnitPrefabs] '_Mesh' 자식을 찾지 못함 (AnimationEventRelay 미부착): {path}");
                return;
            }

            // AnimationEventRelay: 애니메이션 클립의 Animation Event를 코드로 전달하는 중계 컴포넌트.
            // _Mesh 자식의 Animator와 같은 GameObject에 있어야 이벤트가 정상 전달된다.
            if (meshChild.GetComponent<AnimationEventRelay>() == null)
            {
                meshChild.gameObject.AddComponent<AnimationEventRelay>();
            }
        }

        /// <summary>
        /// 직속 자식들 중 이름에 "_Mesh"가 포함된 첫 번째 Transform을 반환한다. 없으면 null.
        /// (메시 오브젝트는 항상 Root의 직속 자식이므로 깊은 재귀 탐색은 하지 않는다.)
        /// </summary>
        private static Transform FindMeshChild(Transform root)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name.Contains(MeshChildNameKeyword))
                {
                    return child;
                }
            }
            return null;
        }

        /// <summary>
        /// SerializedObject에서 float 프로퍼티를 찾아 값을 설정한다. 못 찾으면 경고를 남긴다.
        /// </summary>
        private static void SetFloatProperty(SerializedObject so, string propertyName, float value)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[SetupNewUnitPrefabs] 직렬화 필드를 찾지 못함: {propertyName} (대상: {so.targetObject.GetType().Name})");
                return;
            }
            prop.floatValue = value;
        }

        /// <summary>
        /// SerializedObject에서 bool 프로퍼티를 찾아 값을 설정한다. 못 찾으면 경고를 남긴다.
        /// </summary>
        private static void SetBoolProperty(SerializedObject so, string propertyName, bool value)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[SetupNewUnitPrefabs] 직렬화 필드를 찾지 못함: {propertyName} (대상: {so.targetObject.GetType().Name})");
                return;
            }
            prop.boolValue = value;
        }
    }
}
