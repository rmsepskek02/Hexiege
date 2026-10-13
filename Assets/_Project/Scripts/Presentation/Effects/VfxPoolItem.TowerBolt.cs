// ============================================================================
// VfxPoolItem.TowerBolt.cs
// 정령 방어 타워(RuneSpire) 번개 이펙트의 "길이 맞춤 + 원래 맞은 유닛 끝점 추적" 부분.
//
// 왜 필요한가 (2026-10-13 vfx-code-followups B):
//   예전에는 번개의 "방향"만 정하고 길이는 프리팹에 저장된 고정값(Bolt Z 스케일 3.5)을 썼다.
//   그래서 적이 가깝거나 멀면 번개 끝이 적에게 닿지 않았고, 번개가 보이는 약 0.35초 동안
//   적이 걸어가도 끝은 처음 자리에 그대로 있었다.
//
// 프리팹 구조 (vfx_runespire_attack.prefab, D-4 — 프리팹은 수정하지 않는다):
//   루트
//   ├─ Source_Flash        (타워 쪽 섬광)
//   └─ Bolt                (번개 메시. 로컬 Z 0→1 길이로 제작, 저장된 localScale = (1,1,3.5))
//      └─ Impact_Sparks    (Bolt의 "자식", 로컬 Z = 1 → Bolt 끝에 붙어 있다)
//   ⇒ Bolt의 로컬 Z 스케일만 "거리"로 바꾸면 자식인 Impact_Sparks도 자동으로 끝점에 간다.
//     Impact_Sparks 위치를 따로 또 바꾸면 거리가 두 번 곱해지므로(거리 × 거리) 건드리지 않는다.
//
// 추적 규칙 (D-5 — 화면 표현 전용):
//   - 끝점은 "원래 맞은 그 유닛"(재생 때 1번 고정한 UnitView + UnitData 객체)만 따라간다.
//     매 프레임 ID로 다시 찾지 않는다 → 같은 ID가 다른 유닛에 재사용돼도 옮겨 가지 않는다.
//   - 그 유닛이 죽거나 사라지거나 다른 데이터로 바뀌면 마지막으로 알던 끝점에 "영구 고정"한다.
//   - 피해·타겟 선택·HP·서버 경로는 전혀 건드리지 않는다. 오직 이 이펙트 오브젝트의 Transform만 바꾼다.
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour, Transform).
// ============================================================================

using Hexiege.Domain;
using UnityEngine;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 타워 번개가 따라갈 "원래 맞은 유닛"의 참조 묶음. 재생 1회 시점에 HitPresentationQueue가 만든다.
    /// 화면 표현에만 쓰며, 피해·타겟 판정의 입력이 될 수 없다(NET-AUTH-001).
    /// </summary>
    public readonly struct TowerBoltTarget
    {
        /// <summary>대상 유닛 화면 위치를 담은 Transform(VisualRoot). 투영기가 없을 때 사용.</summary>
        public readonly Transform Pose;
        /// <summary>대상 유닛의 화면 컴포넌트. "아직 같은 유닛인가" 확인에 쓴다.</summary>
        public readonly UnitView View;
        /// <summary>재생 시점에 고정한 대상 Domain 객체. ID가 아니라 이 객체 참조로 동일성을 확인한다.</summary>
        public readonly UnitData Data;
        /// <summary>있으면 매 프레임 같은 프레임의 최신 화면 위치를 즉시 계산해 준다(VisualRootProjector).</summary>
        public readonly VisualRootProjector Projector;

        /// <summary>재생 시점에 해석한 대상 참조로 추적 대상을 만든다.</summary>
        public TowerBoltTarget(Transform pose, UnitView view, UnitData data, VisualRootProjector projector)
        {
            Pose = pose; View = view; Data = data; Projector = projector;
        }

        /// <summary>추적에 필요한 참조가 모두 있는지(= 해석에 성공했는지).</summary>
        public bool HasReference => Pose != null && View != null && Data != null;

        /// <summary>
        /// 지금 이 순간 대상이 "원래 그 유닛이고 살아 있는지" 확인하고, 맞으면 화면 위치를 돌려준다.
        /// 하나라도 어긋나면 false + 사유(로그용 짧은 영문 토큰)를 돌려준다.
        /// </summary>
        public bool TryReadEndpoint(out Vector3 endpoint, out string reason)
        {
            endpoint = default;
            // Unity 오브젝트는 파괴되면 == null 이 참이 된다(C# 참조는 남아 있어도).
            if (View == null) { reason = "ViewDestroyed"; return false; }
            // 같은 View에 다른 UnitData가 들어왔다 = 다른 유닛으로 재사용됐다 → 따라가면 안 된다.
            if (!ReferenceEquals(View.Data, Data)) { reason = "TargetReplaced"; return false; }
            if (Data == null || !Data.IsAlive) { reason = "TargetDead"; return false; }
            if (!View.gameObject.activeInHierarchy) { reason = "TargetInactive"; return false; }
            if (Projector != null) endpoint = Projector.PresentationPosition;
            else if (Pose != null && Pose.gameObject.activeInHierarchy) endpoint = Pose.position;
            else { reason = "PoseUnavailable"; return false; }
            if (float.IsNaN(endpoint.x) || float.IsInfinity(endpoint.x)
                || float.IsNaN(endpoint.z) || float.IsInfinity(endpoint.z))
            { reason = "PoseNotFinite"; return false; }
            reason = null;
            return true;
        }
    }

    public partial class VfxPoolItem
    {
        /// <summary>번개 길이가 0이 되어 메시가 사라지거나 0으로 나누는 일을 막는 최소 길이(로컬 단위).</summary>
        private const float MinBoltLength = 0.01f;
        /// <summary>방향 계산에 쓰기엔 너무 짧은 XZ 거리(제곱). EffectManager의 기존 기준과 같은 값.</summary>
        private const float MinDirectionSqr = 0.0001f;

        // ── 프리팹 구조 캐시 (최초 1회) ──
        private bool _boltSearched;              // Bolt 탐색을 이미 했는가(못 찾았어도 다시 찾지 않는다)
        private Transform _boltTransform;        // 루트 직속 자식 "Bolt"
        private Vector3 _boltAuthoredLocalScale; // 프리팹에 저장된 Bolt 원래 스케일 (현재 (1,1,3.5))
        private Transform _boltSiblingSparks;    // Impact_Sparks가 Bolt의 "형제"인 구조일 때만 값이 있다
        private Vector3 _boltSiblingSparksAuthoredPosition;

        // ── 재생별 상태 (ResetTowerBolt에서 전부 비운다) ──
        private bool _boltTracking;   // 매 프레임 대상 유닛을 따라가는 중인가
        private TowerBoltTarget _boltTarget;
        private Vector3 _boltSource, _boltEndpoint, _boltOffset, _boltEuler;
        private Quaternion _boltRotation;
        private Hexiege.Application.AttackEffectPresentation _boltSnapshot; // 고정(Frozen) 진단 기록용

        /// <summary>읽기 전용 진단/검증용: 지금 대상 유닛을 따라가는 중인지.</summary>
        public bool TowerBoltTracking => _boltTracking;

        /// <summary>
        /// 이미 Play()로 재생을 시작한 정령 타워 번개에 "거리 맞춤 + 끝점 추적"을 켠다.
        /// EffectManager.PlayAttackEffect가 Spirit 타워일 때만 호출한다(Human 타워·버섯 착탄은 호출하지 않음).
        /// </summary>
        /// <param name="snapshot">서버가 확정한 공격 표현 스냅샷(진단 기록에만 사용).</param>
        /// <param name="source">번개 시작점(타워 위치, 화면 좌표 — Client 반전 변환 적용 후 값).</param>
        /// <param name="snapshotImpact">공격 순간의 대상 위치(화면 좌표). 추적 실패 시 끝점.</param>
        /// <param name="localPositionOffset">프리셋 위치 보정(Play와 같은 값).</param>
        /// <param name="eulerRotationOffset">프리셋 회전 보정(Play와 같은 값).</param>
        /// <param name="target">원래 맞은 유닛 참조. 해석 실패면 default.</param>
        /// <param name="untrackedReason">target이 default일 때 그 사유(로그용). null 허용.</param>
        /// <returns>진단용 끝점 상태 문자열: "Tracked" / "Untracked(사유)".</returns>
        public string BeginTowerBolt(Hexiege.Application.AttackEffectPresentation snapshot, Vector3 source, Vector3 snapshotImpact,
            Vector3 localPositionOffset, Vector3 eulerRotationOffset, TowerBoltTarget target, string untrackedReason)
        {
            ResetTowerBolt();
            if (!CacheBoltStructure())
                return "Untracked(BoltMissing)"; // Bolt가 없으면 아무것도 바꾸지 않고 기존 재생 그대로 둔다.

            _boltSnapshot = snapshot;
            _boltSource = source;
            _boltOffset = localPositionOffset;
            _boltEuler = eulerRotationOffset;
            // 방향을 못 정하는 0거리 대비 — Play가 정한 "방향" 회전을 기본값으로 둔다(회전 보정은 빼 둔다:
            //   ApplyTowerBoltPose가 보정을 다시 곱하므로 두 번 곱해지지 않게).
            _boltRotation = transform.rotation * Quaternion.Inverse(Quaternion.Euler(eulerRotationOffset));

            string state;
            string reason = null;
            Vector3 live = default;
            if (target.HasReference && target.TryReadEndpoint(out live, out reason))
            {
                _boltTarget = target;
                _boltTracking = true;
                _boltEndpoint = live;
                state = "Tracked";
            }
            else
            {
                // 대상 해석 실패/이미 사망 → 추적 없이 "공격 순간 위치"를 끝점으로 길이만 맞춘다.
                _boltEndpoint = snapshotImpact;
                state = "Untracked(" + (target.HasReference ? reason : untrackedReason ?? "NoTarget") + ")";
            }
            ApplyTowerBoltPose();
            return state;
        }

        /// <summary>
        /// 매 프레임 렌더 직전에 끝점을 갱신한다. 유닛 이동(Update)이 끝난 뒤라 이번 프레임 위치를 쓴다.
        /// 추적 중이 아니면(고정·Human·다른 이펙트) 즉시 반환하므로 비용이 거의 없다.
        /// </summary>
        private void LateUpdate()
        {
            if (!_active || !_boltTracking) return;
            if (_boltTarget.TryReadEndpoint(out Vector3 live, out string reason))
            {
                _boltEndpoint = live;
                ApplyTowerBoltPose();
                return;
            }
            // 대상이 죽음/제거/교체 → 마지막으로 알던 끝점에서 영구 고정. 다시는 대상을 찾지 않는다.
            _boltTracking = false;
            _boltTarget = default; // 옛 유닛 참조를 즉시 놓아 준다.
            Hexiege.Application.AttackEffectIdentity.Record("EndpointFrozen", _boltSnapshot, "Endpoint=Frozen(" + reason + ")");
        }

        /// <summary>
        /// 시작점(고정)과 현재 끝점으로 번개의 위치·회전·길이를 맞춘다.
        /// 높이 차는 기존 규칙(EffectManager.PlayAttackEffect)처럼 무시하고 XZ 평면만 쓴다.
        /// </summary>
        private void ApplyTowerBoltPose()
        {
            Vector3 direction = _boltEndpoint - _boltSource; direction.y = 0f;
            if (direction.sqrMagnitude > MinDirectionSqr) _boltRotation = Quaternion.LookRotation(direction);
            transform.SetPositionAndRotation(_boltSource + _boltRotation * _boltOffset,
                _boltRotation * Quaternion.Euler(_boltEuler));

            // 실제 루트 위치(오프셋 반영)에서 끝점까지의 XZ 거리 = 번개가 보여야 할 월드 길이.
            Vector3 span = _boltEndpoint - transform.position; span.y = 0f;
            float worldLength = span.magnitude;
            // Bolt 스케일은 루트 기준 "로컬" 값이다. 루트가 프리셋 배율로 커져 있으면 그만큼 나눠야
            // 화면 길이가 정확히 worldLength가 된다(현재 RuneSpire 프리셋 배율은 1).
            float rootScaleZ = transform.lossyScale.z;
            float localLength = rootScaleZ > 0.0001f ? worldLength / rootScaleZ : worldLength;
            localLength = Mathf.Max(MinBoltLength, localLength);

            // Bolt 메시는 로컬 Z 0→1로 만들어졌으므로 Z 스케일 = 길이. X·Y(굵기)는 프리팹 원래 값 유지.
            Vector3 scale = _boltAuthoredLocalScale; scale.z = localLength;
            _boltTransform.localScale = scale;
            // Impact_Sparks가 Bolt의 자식(현재 구조)이면 위 한 줄로 끝점에 자동으로 붙는다.
            // 형제 구조로 바뀐 프리팹일 때만 위치를 직접 끝점(로컬 Z = 길이)으로 옮긴다.
            if (_boltSiblingSparks != null)
            {
                Vector3 p = _boltSiblingSparksAuthoredPosition; p.z = localLength;
                _boltSiblingSparks.localPosition = p;
            }
        }

        /// <summary>
        /// 프리팹 계층에서 Bolt / Impact_Sparks를 이름으로 1번만 찾고, 프리팹 원래 값을 기억한다.
        /// 최초 탐색 시점에는 아직 아무도 Bolt를 바꾸지 않았으므로 그 값이 authored 값이다.
        /// </summary>
        private bool CacheBoltStructure()
        {
            if (_boltSearched) return _boltTransform != null;
            _boltSearched = true;
            _boltTransform = transform.Find("Bolt"); // 루트 "직속" 자식만 찾는다
            if (_boltTransform == null) return false;
            _boltAuthoredLocalScale = _boltTransform.localScale;
            // Bolt 아래에 있으면(현재 구조) 따로 옮길 필요가 없으므로 형제 참조를 비워 둔다.
            if (_boltTransform.Find("Impact_Sparks") == null)
            {
                _boltSiblingSparks = transform.Find("Impact_Sparks");
                if (_boltSiblingSparks != null) _boltSiblingSparksAuthoredPosition = _boltSiblingSparks.localPosition;
            }
            return true;
        }

        /// <summary>
        /// 번개 재생별 상태를 비우고 Bolt/Impact_Sparks를 프리팹 원래 값으로 되돌린다.
        /// 풀 반환(Return)과 모든 새 재생 시작에서 ResetTravelState를 통해 호출된다.
        /// </summary>
        private void ResetTowerBolt()
        {
            if (_boltTransform != null) _boltTransform.localScale = _boltAuthoredLocalScale;
            if (_boltSiblingSparks != null) _boltSiblingSparks.localPosition = _boltSiblingSparksAuthoredPosition;
            _boltTracking = false;
            _boltTarget = default;
            _boltSnapshot = default;
            _boltSource = _boltEndpoint = _boltOffset = _boltEuler = Vector3.zero;
            _boltRotation = Quaternion.identity;
        }
    }
}
