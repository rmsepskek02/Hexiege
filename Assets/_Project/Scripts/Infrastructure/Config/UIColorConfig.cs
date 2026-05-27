// ============================================================================
// UIColorConfig.cs
// 프로젝트 전체 UI에서 공통으로 사용하는 색상 설정 ScriptableObject.
//
// 왜 이런 설정 에셋을 만드는가?
//   기존에는 각 UI 컴포넌트가 Color.red, Color.white, new Color(0.3f, 0.5f, 0.9f) 등
//   하드코딩된 색상을 직접 사용했다. 그 결과:
//     - 색상을 한 곳에서 일관되게 관리할 수 없다 (UX 톤 변경 시 여러 파일 수정 필요).
//     - 디자이너/기획자가 색상만 바꾸려 해도 코드 수정 + 재컴파일이 강제된다.
//     - 같은 의미(골드 부족)인데 파일마다 색상이 미세하게 다를 위험이 있다.
//
//   이 ScriptableObject를 도입하면:
//     - 색상을 에셋 1개에 모아 단일 진실 공급원(SSOT)으로 관리.
//     - 디자이너가 인스펙터에서 즉시 수정 가능, 코드 수정 불필요.
//     - 모든 UI가 같은 색상 토큰을 참조하므로 자동으로 톤이 일치한다.
//
// 사용 방법:
//   1. Project 창에서 Create > Hexiege > Config > UIColorConfig 로 에셋 생성.
//      권장 경로: Assets/Resources/Config/UIColorConfig.asset
//   2. 각 UI 컴포넌트에 [SerializeField] private UIColorConfig _colorConfig; 추가.
//   3. Inspector에서 해당 에셋을 드래그하여 연결.
//   4. 코드에서는 _colorConfig.goldInsufficientColor 처럼 직접 참조.
//
// 안전 가드 패턴 (UI 컴포넌트에서):
//   _text.color = _colorConfig?.goldInsufficientColor ?? Color.red;
//   - _colorConfig가 Inspector에서 연결되지 않은 경우에도 합리적인 폴백 색상을 사용.
//   - null 전파 연산자(?.)와 ?? 조합으로 NullReferenceException 방지.
//
// Infrastructure 레이어 — Unity ScriptableObject 의존.
// 프로젝트 컨벤션에 따라 Hexiege.Infrastructure 네임스페이스에 위치.
// ============================================================================

using UnityEngine;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 프로젝트 전체 UI에서 공통으로 사용하는 색상 토큰 모음.
    /// 각 UI 컴포넌트는 이 ScriptableObject를 SerializeField로 참조하여
    /// 일관된 색상 체계를 유지한다. 색상 변경 시 이 에셋만 수정하면 된다.
    /// </summary>
    [CreateAssetMenu(fileName = "UIColorConfig", menuName = "Hexiege/Config/UIColorConfig")]
    public class UIColorConfig : ScriptableObject
    {
        // ====================================================================
        // 기본 텍스트 색상
        // ====================================================================

        [Header("텍스트 — 기본/경고")]
        [Tooltip("일반 상황에서 사용하는 기본 텍스트 색상. " +
                 "빨간색/노란색 등 강조 색상에서 원래 색으로 되돌릴 때 리셋용으로도 사용.")]
        public Color normalTextColor = Color.white;

        [Tooltip("골드가 부족할 때 비용 텍스트를 강조하는 색상. " +
                 "건물 배치/유닛 생산 팝업에서 해당 항목의 비용 표시에 적용된다.")]
        public Color goldInsufficientColor = Color.red;

        [Tooltip("인구가 가득 찼을 때 HUD의 인구 텍스트를 강조하는 색상. " +
                 "사용 인구 >= 최대 인구일 때 적용.")]
        public Color populationFullColor = Color.red;

        // ====================================================================
        // 게임 종료 결과
        // ====================================================================

        [Header("게임 종료")]
        [Tooltip("승리 결과 텍스트 색상. 로컬 팀이 승리한 경우 적용된다.")]
        public Color winColor = new Color(0.3f, 0.5f, 0.9f);

        [Tooltip("패배 결과 텍스트 색상. 로컬 팀이 패배한 경우 적용된다.")]
        public Color loseColor = new Color(0.9f, 0.3f, 0.3f);

        // ====================================================================
        // 건물 철거
        // ====================================================================

        [Header("건물 철거")]
        [Tooltip("건물 철거 시 환불받을 골드 금액을 표시하는 텍스트 색상. " +
                 "환불은 자원 회수(양의 변화)이므로 초록색 톤을 사용.")]
        public Color demolishRefundColor = Color.green;

        // ====================================================================
        // 확인 팝업 버튼
        // ====================================================================

        [Header("확인 팝업 버튼")]
        [Tooltip("확인(또는 위험 행동 수행) 버튼의 배경 색상. " +
                 "예: 포기 확인 팝업의 '포기' 버튼.")]
        public Color confirmButtonColor = Color.green;

        [Tooltip("취소 버튼의 배경 색상. " +
                 "예: 포기 확인 팝업의 '취소' 버튼.")]
        public Color cancelButtonColor = Color.red;
    }
}
