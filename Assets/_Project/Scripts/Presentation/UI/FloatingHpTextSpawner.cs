// ============================================================================
// FloatingHpTextSpawner.cs
// 피격 시 부유 HP 텍스트를 생성하고 관리하는 컴포넌트.
//
// 역할:
//   1. public ShowDamage(evt) 호출 시 피격 발생 처리 (표시의 유일한 진입점).
//   2. 피격 오브젝트의 월드 좌표를 IEntityPositionProvider로 조회.
//   3. 오브젝트 풀에서 FloatingHpText를 꺼내 월드 좌표에 배치 후 애니메이션 재생.
//   4. 애니메이션 완료 후 풀에 반환하여 재사용.
//
// [Phase 2 변경] 예전에는 GameEvents.OnEntityDamaged를 직접 구독해 "데미지 도착 즉시" 표시했다.
//   지금은 HitPresentationQueue가 공격자의 로컬 타격 프레임(OnAttackHit)에 맞춰 방출 시점에
//   ShowDamage()를 호출한다. 직접 구독을 제거해 "즉시 표시"와 "방출 시점 표시"의 이중 표시를 없앴다.
//
// 오브젝트 풀링 설명:
//   매번 Instantiate/Destroy를 하면 GC(가비지 컬렉션) 부하가 발생.
//   미리 10개를 만들어 두고 재사용하면 런타임 할당이 최소화됨.
//   풀이 비면 새로 Instantiate하되, 사용 후 파괴하지 않고 풀에 반환.
//
// 부착 위치: 씬의 아무 GameObject (GameBootstrapper에서 Initialize 호출)
//
// Presentation 레이어 — Application 이벤트 데이터(EntityDamagedEvent) 의존.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UniRx;
using Hexiege.Application;
using Hexiege.Domain;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 피격 이벤트에 반응하여 부유 HP 텍스트를 생성하는 스포너.
    /// GameBootstrapper에서 Initialize()를 호출하여 의존성 주입.
    /// </summary>
    public class FloatingHpTextSpawner : MonoBehaviour
    {
        // ====================================================================
        // 의존성 (Initialize()에서 주입)
        // ====================================================================

        /// <summary>
        /// 로컬 화면에 그려지는 유닛/건물 좌표를 반환하는 프로바이더.
        /// Simulation용 IEntityPositionProvider와 분리하여 전투 계산 좌표를 변경하지 않는다.
        /// </summary>
        private IPresentationPoseProvider _presentationPoseProvider;

        /// <summary>
        /// 부유 텍스트 오브젝트들의 부모 컨테이너(월드 공간의 빈 GameObject).
        /// World Space TextMeshPro이므로 월드 좌표 기준으로 배치됨.
        /// </summary>
        private Transform _container;

        /// <summary>
        /// FloatingHpText 프리팹. 풀 생성 및 부족 시 추가 Instantiate에 사용.
        /// </summary>
        private FloatingHpText _prefab;

        // ====================================================================
        // 오브젝트 풀
        // ====================================================================

        /// <summary>
        /// 비활성 상태의 FloatingHpText 오브젝트 큐.
        /// Dequeue로 꺼내 사용, Enqueue로 반환.
        /// </summary>
        private readonly Queue<FloatingHpText> _pool = new Queue<FloatingHpText>();

        /// <summary> 초기 풀 크기. 게임 시작 시 미리 생성할 텍스트 오브젝트 수. </summary>
        private const int InitialPoolSize = 10;

        /// <summary>
        /// 피격 오브젝트 위쪽으로의 월드 공간 Y 오프셋.
        /// World Space 기반이므로 월드 단위(유닛 높이) 기준.
        /// </summary>
        [Tooltip("피격 오브젝트 머리 위쪽 시작 오프셋 (월드 Y 단위). 클수록 텍스트가 더 높은 위치에서 시작됨.")]
        [SerializeField] private float _yOffset = 1.2f;

        // ====================================================================
        // 팀별 텍스트 색상 (Inspector에서 조정 가능)
        // ====================================================================

        [Header("팀별 텍스트 색상")]

        [Tooltip("Blue 팀 엔티티가 피격당할 때 표시되는 텍스트 색상.")]
        [SerializeField] private Color _blueTeamColor = new Color(120f / 255f, 230f / 255f, 80f / 255f);

        [Tooltip("Red 팀 엔티티가 피격당할 때 표시되는 텍스트 색상.")]
        [SerializeField] private Color _redTeamColor = new Color(255f / 255f, 220f / 255f, 30f / 255f);

        [Tooltip("회복(힐)될 때 표시되는 텍스트 색상. 피격과 구분되는 치유 색상(기본: 청록/시안).")]
        [SerializeField] private Color _healColor = new Color(60f / 255f, 220f / 255f, 220f / 255f);

        /// <summary>OnEntityHealed 구독 해제용 Disposable. 재초기화/파괴 시 정리.</summary>
        private System.IDisposable _healedSubscription;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// 스포너 초기화. GameBootstrapper에서 호출.
        /// 의존성 저장, 풀 사전 생성, 이벤트 구독을 순서대로 수행.
        /// </summary>
        /// <param name="presentationPoseProvider">유닛/건물 표현 좌표 제공자.</param>
        /// <param name="container">부유 텍스트가 배치될 월드 공간 부모 Transform.</param>
        /// <param name="prefab">FloatingHpText 프리팹.</param>
        public void Initialize(
            IPresentationPoseProvider presentationPoseProvider,
            Transform container,
            FloatingHpText prefab)
        {
            // 필수 의존성 null 체크 — Inspector 미연결 시 CreateInstance()에서 크래시 방지
            if (presentationPoseProvider == null || container == null || prefab == null)
            {
                Debug.LogError("[FloatingHpTextSpawner] Initialize() 실패: 필수 의존성이 null입니다. " +
                               "GameBootstrapper Inspector에서 모든 슬롯이 연결되었는지 확인하세요.");
                return;
            }

            _presentationPoseProvider = presentationPoseProvider;
            _container = container;
            _prefab = prefab;

            // 풀 사전 생성: InitialPoolSize개를 미리 만들어 비활성 상태로 대기
            for (int i = 0; i < InitialPoolSize; i++)
            {
                FloatingHpText instance = CreateInstance();
                instance.gameObject.SetActive(false);
                _pool.Enqueue(instance);
            }

            // NOTE(Phase 2 — 축 3): 과거에는 여기서 GameEvents.OnEntityDamaged를 직접 구독하여
            //   데미지가 도착한 "즉시" HP 텍스트를 띄웠다. 이제는 HitPresentationQueue가
            //   공격자의 로컬 타격 프레임(OnAttackHit)에 맞춰 방출 시점에 ShowDamage()를 호출한다.
            //   → 직접 구독을 제거하여 "즉시 표시"와 "방출 시점 표시"가 중복되지 않게 한다(이중 표시 방지).
            //   HP 텍스트 표시의 유일한 진입점은 이제 public ShowDamage() 뿐이다.

            // 힐(회복)은 피격과 달리 공격자 타격 프레임에 맞출 필요가 없다(파도가 아군에 닿는 서버 시각에 즉시 표시).
            //   따라서 HitPresentationQueue를 거치지 않고 여기서 OnEntityHealed를 직접 구독해 치유 텍스트를 띄운다.
            //   각 머신에서 정확히 1회 표시된다: 싱글=UseCase 1회 / 호스트=UseCase 1회 / 클라=NetworkHealthSync 재발행 1회.
            //   재초기화(맵 재로드)로 Initialize가 다시 불릴 수 있으므로, 기존 구독을 먼저 해제하고 재구독한다(중복 방지).
            _healedSubscription?.Dispose();
            _healedSubscription = GameEvents.OnEntityHealed.Subscribe(ShowHeal);
        }

        // ====================================================================
        // 표시 API (HitPresentationQueue가 방출 시점에 호출)
        // ====================================================================

        /// <summary>
        /// 피격 HP 텍스트를 표시한다. HitPresentationQueue가 피격 연출을 방출하는 시점에 호출한다.
        /// 피격 오브젝트의 월드 좌표에 직접 World Space 텍스트를 배치한다.
        /// 텍스트는 다른 월드 오브젝트(유닛, 건물)와 동일하게 줌에 비례해 커지고 작아진다.
        /// </summary>
        /// <param name="evt">피격 이벤트 데이터. Entity(피격 대상), CurrentHp, IsUnit 포함.</param>
        public void ShowDamage(EntityDamagedEvent evt)
        {
            if (_presentationPoseProvider == null || _container == null) return;

            // 피격 엔티티의 월드 좌표 조회 — IsUnit이면 유닛, 아니면 건물
            Vector3 worldPos = evt.IsUnit
                ? _presentationPoseProvider.GetUnitPosition(evt.Entity.Id)
                : _presentationPoseProvider.GetBuildingPosition(evt.Entity.Id);

            // Vector3.zero = GameObject가 이미 파괴된 경우 (소멸 후 이벤트 도달)
            if (worldPos == Vector3.zero) return;

            // 피격 지점 머리 위 월드 좌표 계산
            Vector3 spawnPos = worldPos + Vector3.up * _yOffset;

            // 풀에서 텍스트 오브젝트 가져오기
            FloatingHpText hpText = GetFromPool();

            // 컨테이너의 자식으로 설정 (worldPositionStays=false: 부모 변경 시 로컬 좌표 유지)
            hpText.transform.SetParent(_container, false);

            // 피격 대상 팀에 따라 텍스트 색상 결정 — Blue=연두, Red=노랑, 그 외=흰색
            TeamId team = evt.Entity.Team;
            Color textColor = team switch
            {
                TeamId.Blue => _blueTeamColor,
                TeamId.Red  => _redTeamColor,
                _           => Color.white
            };

            // 남은 HP를 텍스트로 표시 — 월드 좌표 전달
            hpText.Play(
                $"{evt.CurrentHp}",
                spawnPos,
                color: textColor);
        }

        /// <summary>
        /// 회복(힐) 텍스트를 표시한다. GameEvents.OnEntityHealed 구독으로 회복 시점에 호출된다.
        /// 피격 텍스트와 오브젝트 풀·배치 로직을 그대로 재사용하되, 치유 색상(_healColor)으로 피격과 구분한다.
        /// 표시 문자열은 즉발/파도/HoT 완료 모두 "회복 후 현재 HP"로 통일한다(사용자 결정 B).
        ///
        /// 힐 VFX: 현재 전용 힐 VFX(파티클) 프리셋이 없어 텍스트만 표시한다.
        ///   추후 힐 VFX 에셋이 확보되면 EffectManager에 PlayUnitHeal 경로를 추가해 여기서 함께 재생한다
        ///   (사운드 규칙 15 — VFX+SFX 쌍, 미확보 시 텍스트 연출만).
        /// </summary>
        /// <param name="evt">회복 이벤트 데이터. Entity(회복 대상), CurrentHp 포함.</param>
        public void ShowHeal(EntityHealedEvent evt)
        {
            if (_presentationPoseProvider == null || _container == null) return;
            if (evt.Entity == null) return;

            // [힐 텍스트 정리 — 2026-07-19] 텍스트 억제 이벤트(HoT 틱 등)는 여기서 걸러 낸다.
            //   ShowText=false인 이벤트는 오직 HP 적용/동기화(ApplyHealToUnit·NetworkHealthSync)만을 위해
            //   발행된 것이므로, 부유 텍스트는 그리지 않는다. HoT는 완료 시 ShowText=true 이벤트로 1회만 표시된다.
            if (!evt.ShowText) return;

            // 회복 엔티티의 월드 좌표 조회 — 유닛(파도/BloomFairy/자연회복)과 건물(MistShrine 물안개 힐) 둘 다 대상이다.
            //   IsUnit 분기 덕분에 건물 회복 텍스트도 신규 UI 없이 이 경로로 그대로 표시된다(MistShrine 규칙 15).
            Vector3 worldPos = evt.IsUnit
                ? _presentationPoseProvider.GetUnitPosition(evt.Entity.Id)
                : _presentationPoseProvider.GetBuildingPosition(evt.Entity.Id);

            // Vector3.zero = GameObject가 이미 파괴된 경우
            if (worldPos == Vector3.zero) return;

            Vector3 spawnPos = worldPos + Vector3.up * _yOffset;

            FloatingHpText hpText = GetFromPool();
            hpText.transform.SetParent(_container, false);

            // 표시 문자열 결정:
            //   즉발/파도/HoT 완료 모두 동일하게 "회복 후 현재 HP"를 보여 준다(사용자 결정 B — 형식 통일).
            //   HoT 완료도 여러 틱을 요약한 "+총량"이 아니라, 다른 힐과 같은 절대값(현재 HP)으로 표시한다.
            string label = $"{evt.CurrentHp}";

            // 치유 색상으로 표시(피격과 구분).
            hpText.Play(
                label,
                spawnPos,
                color: _healColor);
        }

        // ====================================================================
        // 오브젝트 풀 관리
        // ====================================================================

        /// <summary>
        /// 풀에서 FloatingHpText 하나를 꺼냄.
        /// 풀이 비어있으면 새로 Instantiate하여 반환.
        /// </summary>
        /// <returns>사용 가능한 FloatingHpText 인스턴스.</returns>
        private FloatingHpText GetFromPool()
        {
            if (_pool.Count > 0)
            {
                return _pool.Dequeue();
            }

            // 풀이 비었으면 새로 생성
            return CreateInstance();
        }

        /// <summary>
        /// 사용 완료된 FloatingHpText를 풀에 반환.
        /// FloatingHpText의 OnComplete에서 콜백으로 호출됨.
        /// </summary>
        /// <param name="text">반환할 FloatingHpText 인스턴스.</param>
        private void ReturnToPool(FloatingHpText text)
        {
            if (text == null) return;
            text.gameObject.SetActive(false);
            _pool.Enqueue(text);
        }

        /// <summary>
        /// FloatingHpText 인스턴스를 프리팹에서 생성하고 풀 반환 콜백을 설정.
        /// </summary>
        /// <returns>생성된 FloatingHpText 인스턴스.</returns>
        private FloatingHpText CreateInstance()
        {
            FloatingHpText instance = Instantiate(_prefab, _container);
            instance.SetReturnCallback(ReturnToPool);
            return instance;
        }

        /// <summary>
        /// 파괴 시 OnEntityHealed 구독을 해제하여 누수를 방지한다.
        /// </summary>
        private void OnDestroy()
        {
            _healedSubscription?.Dispose();
            _healedSubscription = null;
        }
    }
}
