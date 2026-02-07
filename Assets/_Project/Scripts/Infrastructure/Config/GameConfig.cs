// ============================================================================
// GameConfig.cs
// 게임 전역 설정을 담는 ScriptableObject.
//
// ScriptableObject란?
//   Unity의 데이터 컨테이너. MonoBehaviour와 달리 씬에 부착하지 않고,
//   에셋 파일(.asset)로 프로젝트에 저장. Inspector에서 값을 편집 가능.
//   코드 수정 없이 수치를 조정할 수 있어 밸런싱에 유용.
//
// 생성 방법 (Unity 에디터):
//   Assets 폴더에서 우클릭 → Create → Hexiege → GameConfig
//   → Resources/Config/ 폴더에 저장 (코드에서 Resources.Load로 접근)
//
// 이 설정에 포함된 항목:
//   - 그리드: 가로/세로 타일 수, 타일 크기
//   - 팀 색상: Neutral, Blue, Red, 선택 하이라이트
//   - 유닛: 이동 속도
//   - 애니메이션: 프레임 속도
//   - 카메라: 줌 범위
//
// 사용 예시:
//   var config = Resources.Load<GameConfig>("Config/GameConfig");
//   OrientationConfig oc = config.PointyTop;  // 또는 config.FlatTop
//   int width = oc.GridWidth;
//
// Infrastructure 레이어 — Unity 의존 (ScriptableObject, Color).
// ============================================================================

using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Infrastructure
{
    // ========================================================================
    // OrientationConfig
    // 각 Orientation(PointyTop/FlatTop)별 그리드 설정을 묶는 중첩 클래스.
    // GameConfig 내부에서 PointyTop / FlatTop 인스턴스로 사용.
    // ========================================================================

    [System.Serializable]
    public class OrientationConfig
    {
        [Tooltip("그리드 가로 타일 수")]
        public int GridWidth;

        [Tooltip("그리드 세로 타일 수")]
        public int GridHeight;

        [Tooltip("타일 간 가로 간격 (월드 단위)")]
        public float TileWidth;

        [Tooltip("타일 간 세로 간격 (월드 단위)")]
        public float TileHeight;
    }

    [CreateAssetMenu(fileName = "GameConfig", menuName = "Hexiege/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        // ====================================================================
        // 그리드 설정 — Orientation별 분리
        // 런타임에서 LoadMap(orientation)으로 전환 시 해당 config 사용.
        // ====================================================================

        [Header("PointyTop Grid")]
        [Tooltip("PointyTop 방향 그리드 설정 (꼭지점 12시)")]
        public OrientationConfig PointyTop = new OrientationConfig
        {
            GridWidth = 7,
            GridHeight = 17,
            TileWidth = 0.866f,
            TileHeight = 0.82f
        };

        [Header("FlatTop Grid")]
        [Tooltip("FlatTop 방향 그리드 설정 (변 12시)")]
        public OrientationConfig FlatTop = new OrientationConfig
        {
            GridWidth = 10,
            GridHeight = 29,
            TileWidth = 1.0f,
            TileHeight = 0.36f
        };

        // ====================================================================
        // 팀 색상 설정
        // PrototypePlan.md 기준:
        //   Neutral: RGB(178,178,178) 회색
        //   Blue:    RGB(77,128,230) 파랑
        //   Red:     RGB(230,77,77)  빨강
        //   Selected: 기존 색상 × RGB(255,255,128) 노란 틴트
        // ====================================================================

        [Header("Team Colors")]

        /// <summary> 중립 타일 색상. 아무도 점령하지 않은 상태. </summary>
        [Tooltip("중립 타일 색상")]
        public Color NeutralColor = new Color(178f / 255f, 178f / 255f, 178f / 255f, 1f);

        /// <summary> 블루 팀 타일 색상. </summary>
        [Tooltip("블루 팀 타일 색상")]
        public Color BlueTeamColor = new Color(77f / 255f, 128f / 255f, 230f / 255f, 1f);

        /// <summary> 레드 팀 타일 색상. </summary>
        [Tooltip("레드 팀 타일 색상")]
        public Color RedTeamColor = new Color(230f / 255f, 77f / 255f, 77f / 255f, 1f);

        /// <summary>
        /// 선택된 타일에 곱할 틴트 색상.
        /// 기존 팀 색상에 이 색을 곱하면 노란빛 하이라이트 효과.
        /// </summary>
        [Tooltip("선택 하이라이트 틴트 (기존 색상에 곱셈)")]
        public Color SelectedTint = new Color(1f, 1f, 128f / 255f, 1f);

        // ====================================================================
        // 유닛 설정
        // ====================================================================

        [Header("Unit")]

        /// <summary>
        /// 유닛이 타일 하나를 이동하는 데 걸리는 시간(초).
        /// 값이 작을수록 빠르게 이동.
        /// UnitView의 Lerp 코루틴에서 사용.
        /// </summary>
        [Tooltip("타일 1칸 이동 소요 시간(초). 작을수록 빠름")]
        public float UnitMoveSeconds = 0.3f;

        /// <summary>
        /// 유닛을 타일 중심보다 위로 올리는 Y 오프셋 (월드 단위).
        /// 양수 = 위쪽. 유닛이 타일 "위에 서 있는" 느낌을 주기 위한 값.
        /// 레퍼런스 이미지처럼 유닛이 타일 표면 위에 위치하도록 조정.
        /// </summary>
        [Tooltip("유닛 Y 오프셋 (양수=위). 타일 위에 서있는 느낌을 줌")]
        public float UnitYOffset = 0.15f;

        // ====================================================================
        // 애니메이션 설정
        // ====================================================================

        [Header("Animation")]

        /// <summary>
        /// 스프라이트 애니메이션 프레임 속도.
        /// FrameAnimator에서 1/fps 초마다 다음 프레임으로 전환.
        /// </summary>
        [Tooltip("스프라이트 애니메이션 FPS")]
        public float AnimationFps = 6f;

        // ====================================================================
        // 카메라 설정
        // ====================================================================

        [Header("Camera")]

        /// <summary>
        /// 카메라 Orthographic Size 최소값 (최대 줌인).
        /// 모바일 세로(9:16)에서 orthoSize=2이면 세로 4유닛 보임.
        /// </summary>
        [Tooltip("최대 줌인 시 Orthographic Size")]
        public float CameraZoomMin = 2f;

        /// <summary> 카메라 Orthographic Size 최대값 (최대 줌아웃). </summary>
        [Tooltip("최대 줌아웃 시 Orthographic Size")]
        public float CameraZoomMax = 7f;

        /// <summary>
        /// 카메라 기본 Orthographic Size.
        /// 모바일 세로(9:16) 기준:
        ///   orthoSize=5 → 세로 10유닛 → 그리드 높이(12.75)의 ~78% 보임.
        ///   그리드 전체를 한눈에 보기보다 적절한 줌 레벨.
        /// </summary>
        [Tooltip("기본 카메라 줌 레벨 (모바일 세로 기준 5 권장)")]
        public float CameraZoomDefault = 5f;

        /// <summary> 스크롤/핀치 줌 감도. </summary>
        [Tooltip("줌 속도 배율")]
        public float CameraZoomSpeed = 2f;

        /// <summary> 드래그 팬 감도. </summary>
        [Tooltip("드래그 팬 속도 배율")]
        public float CameraPanSpeed = 1f;

        // ====================================================================
        // 유틸리티 메서드
        // ====================================================================

        /// <summary>
        /// TeamId에 해당하는 색상을 반환.
        /// HexTileView에서 타일 색상 설정 시 사용.
        /// </summary>
        public Color GetTeamColor(Domain.TeamId team)
        {
            switch (team)
            {
                case Domain.TeamId.Blue: return BlueTeamColor;
                case Domain.TeamId.Red: return RedTeamColor;
                default: return NeutralColor;
            }
        }
    }
}
