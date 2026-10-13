using UnityEngine;

namespace Hexiege.Presentation
{
    /// <summary>실측으로 승인된 좌우 배율과 선두 보정만 담는다. Y는 gameplay volume이 아니다.</summary>
    public readonly struct WaveVisualCalibration
    {
        public readonly float LateralScale;
        public readonly float FrontAnchor;
        public readonly bool HierarchySizeScaling;
        public WaveVisualCalibration(float lateralScale, float frontAnchor, bool hierarchySizeScaling)
        { LateralScale = lateralScale; FrontAnchor = frontAnchor; HierarchySizeScaling = hierarchySizeScaling; }

        // 2026-10-01 승인된 공통 runtime helper v2 실측: runtime-helper-20261001-044444-281.csv.
        // Hierarchy X=.782384 후보의 fit maxAbsX=1.500076은 폭3 경계를 미세 초과했다.
        // 메인 승인 X=.78로 낮춘 값은 최종 재측정 대상이며, holdout으로 fit 값을 조정하지 않았다.
        // 후보 holdout maxAbsX=1.470601; Y 높이는 fit 1.005499/holdout 1.029019로 원본과 동일했다.
        // anchor=.928406의 crest 잔차는 fit ±.257130, holdout [-.245697,.229983]이다.
        // preset Z=.4에서 anchor를 빼므로 root Z=-.528406. 정확한 접촉 동시성/임의 방향 PASS는 별도 gate다.
        // Y는 원본 미학을 유지할 뿐 서버 gameplay 높이 또는 신규 피해 volume을 뜻하지 않는다.
        public static WaveVisualCalibration Production => new WaveVisualCalibration(.78f, .928406f, true);

        /// <summary>원본 높이 배율을 유지하며 좌우만 보정한다.</summary>
        public Vector3 Scale(Vector3 authored) => new Vector3(authored.x * LateralScale, authored.y, authored.z);

        /// <summary>preset 전방 offset과 입자 내부 전방 offset의 합에 대한 실측 anchor를 뺀다.</summary>
        public Vector3 Offset(Vector3 authored) => new Vector3(authored.x, authored.y, authored.z - FrontAnchor);
    }
}
