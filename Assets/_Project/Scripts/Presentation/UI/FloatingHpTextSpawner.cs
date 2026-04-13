// ============================================================================
// FloatingHpTextSpawner.cs
// 피격 이벤트 수신 시 부유 HP 텍스트를 생성하고 관리하는 컴포넌트.
//
// 역할:
//   1. GameEvents.OnEntityDamaged 이벤트를 구독하여 피격 발생 감지.
//   2. 피격 오브젝트의 월드 좌표를 IEntityPositionProvider로 조회.
//   3. 월드 좌표 → 스크린 좌표 → Canvas 로컬 좌표 변환.
//   4. 오브젝트 풀에서 FloatingHpText를 꺼내 애니메이션 재생.
//   5. 애니메이션 완료 후 풀에 반환하여 재사용.
//
// 오브젝트 풀링 설명:
//   매번 Instantiate/Destroy를 하면 GC(가비지 컬렉션) 부하가 발생.
//   미리 10개를 만들어 두고 재사용하면 런타임 할당이 최소화됨.
//   풀이 비면 새로 Instantiate하되, 사용 후 파괴하지 않고 풀에 반환.
//
// 부착 위치: 씬의 아무 GameObject (GameBootstrapper에서 Initialize 호출)
//
// Presentation 레이어 — UniRx, Application 이벤트 의존.
// ============================================================================

using System.Collections.Generic;
using UniRx;
using UnityEngine;
using Hexiege.Application;

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
        /// 유닛/건물의 월드 좌표를 반환하는 프로바이더.
        /// UnitWorldPositionProvider가 구현체이며, GameBootstrapper에서 생성.
        /// </summary>
        private IEntityPositionProvider _positionProvider;

        /// <summary>
        /// 부유 텍스트의 부모가 될 Canvas.
        /// Screen Space - Overlay Canvas의 RectTransform 좌표계에서 위치 계산.
        /// </summary>
        private Canvas _canvas;

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
        /// 피격 오브젝트 위쪽으로의 추가 오프셋 (Canvas 로컬 좌표 기준, 픽셀).
        /// 실제 적용 시 줌 스케일이 곱해지므로 줌 배율에 따라 유동적으로 변함.
        /// </summary>
        private const float YOffset = 80f;

        [Header("줌 스케일링")]

        [Tooltip("텍스트 크기·오프셋 계산의 기준이 되는 카메라 orthographicSize.\n" +
                 "이 값일 때 텍스트가 기본 크기로 표시됨.\n" +
                 "게임에서 기본(기준) 줌 상태의 orthographicSize를 입력하세요.")]
        [SerializeField] private float _referenceOrthographicSize = 5f;

        /// <summary>
        /// Camera.main 캐시.
        /// Camera.main은 호출마다 씬 탐색 비용이 발생하므로 Initialize()에서 한 번만 조회하여 보관.
        /// </summary>
        private Camera _mainCamera;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// 스포너 초기화. GameBootstrapper에서 호출.
        /// 의존성 저장, 풀 사전 생성, 이벤트 구독을 순서대로 수행.
        /// </summary>
        /// <param name="positionProvider">유닛/건물 월드 좌표 제공자.</param>
        /// <param name="canvas">부유 텍스트가 배치될 UI Canvas.</param>
        /// <param name="prefab">FloatingHpText 프리팹.</param>
        public void Initialize(
            IEntityPositionProvider positionProvider,
            Canvas canvas,
            FloatingHpText prefab)
        {
            // 필수 의존성 null 체크 — Inspector 미연결 시 CreateInstance()에서 크래시 방지
            if (positionProvider == null || canvas == null || prefab == null)
            {
                Debug.LogError("[FloatingHpTextSpawner] Initialize() 실패: 필수 의존성이 null입니다. " +
                               "GameBootstrapper Inspector에서 모든 슬롯이 연결되었는지 확인하세요.");
                return;
            }

            _positionProvider = positionProvider;
            _canvas = canvas;
            _prefab = prefab;

            // Camera.main 캐싱 — 매 피격 이벤트마다 씬 탐색 비용 발생을 방지
            _mainCamera = Camera.main;

            // 풀 사전 생성: InitialPoolSize개를 미리 만들어 비활성 상태로 대기
            for (int i = 0; i < InitialPoolSize; i++)
            {
                FloatingHpText instance = CreateInstance();
                instance.gameObject.SetActive(false);
                _pool.Enqueue(instance);
            }

            // 피격 이벤트 구독.
            // AddTo(this): 이 MonoBehaviour가 파괴될 때 자동으로 구독 해제.
            // 구독 해제하지 않으면 파괴된 오브젝트의 메서드가 호출되어 NullReferenceException 발생.
            GameEvents.OnEntityDamaged
                .Subscribe(OnEntityDamaged)
                .AddTo(this);
        }

        // ====================================================================
        // 이벤트 핸들러
        // ====================================================================

        /// <summary>
        /// 피격 이벤트 핸들러.
        /// 피격 오브젝트의 월드 좌표를 Canvas 로컬 좌표로 변환하여 부유 텍스트 표시.
        ///
        /// 좌표 변환 과정:
        ///   1. 월드 좌표 (3D 공간) → WorldToScreenPoint → 스크린 좌표 (2D 픽셀)
        ///   2. 스크린 좌표 → ScreenPointToLocalPointInRectangle → Canvas 로컬 좌표
        ///   Canvas가 Screen Space - Overlay이므로 camera 파라미터는 null.
        /// </summary>
        /// <param name="evt">피격 이벤트 데이터. Entity(피격 대상), CurrentHp, IsUnit 포함.</param>
        private void OnEntityDamaged(EntityDamagedEvent evt)
        {
            if (_positionProvider == null || _canvas == null) return;

            // 피격 엔티티의 월드 좌표 조회
            // IsUnit이면 유닛 좌표, 아니면 건물 좌표
            Vector3 worldPos = evt.IsUnit
                ? _positionProvider.GetUnitWorldPosition(evt.Entity.Id)
                : _positionProvider.GetBuildingWorldPosition(evt.Entity.Id);

            // Vector3.zero = GameObject가 이미 파괴된 경우 (소멸 후 이벤트 도달)
            if (worldPos == Vector3.zero) return;

            // 캐싱된 카메라가 없으면 좌표 변환 불가
            if (_mainCamera == null) return;

            // 월드 좌표 → 스크린 좌표 변환
            // WorldToScreenPoint: 3D 공간의 점을 화면 픽셀 좌표로 변환
            Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

            // 스크린 좌표 → Canvas 로컬 좌표 변환
            // Screen Space - Overlay Canvas이므로 camera = null 전달
            // out localPoint: 변환 결과 Canvas RectTransform 기준 좌표
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform,
                screenPos,
                null,
                out Vector2 localPoint);

            // 현재 줌 배율에 따른 스케일 계산
            // 줌 아웃(orthographicSize 증가) → scale 감소 → 텍스트 작아지고 오프셋도 줄어듦
            float scale = GetZoomScale();

            // 풀에서 텍스트 오브젝트 가져오기
            FloatingHpText hpText = GetFromPool();

            // Canvas의 자식으로 설정 (worldPositionStays=false: 부모 변경 시 로컬 좌표 유지)
            hpText.transform.SetParent(_canvas.transform, false);

            // 남은 HP를 텍스트로 표시 + 줌 스케일 적용 오프셋으로 애니메이션 시작
            hpText.Play(
                $"{evt.CurrentHp}",
                localPoint + new Vector2(0f, YOffset * scale),
                scale);
        }

        // ====================================================================
        // 줌 스케일 계산
        // ====================================================================

        /// <summary>
        /// 현재 카메라 줌 상태를 기준값과 비교하여 UI 스케일 비율을 반환합니다.
        ///
        /// Orthographic 카메라 기준:
        ///   - orthographicSize가 작을수록(줌 인) → scale 커짐 → 텍스트 크고 오프셋 큼
        ///   - orthographicSize가 클수록(줌 아웃) → scale 작아짐 → 텍스트 작고 오프셋 작음
        ///
        /// Perspective 카메라의 경우 orthographic 방식이 맞지 않으므로
        /// 카메라가 Orthographic이 아닌 경우 1f(기본값)를 반환합니다.
        /// </summary>
        private float GetZoomScale()
        {
            if (_mainCamera == null) return 1f;

            if (_mainCamera.orthographic)
            {
                // orthographicSize가 0이면 나누기 오류 방지
                if (_mainCamera.orthographicSize <= 0f) return 1f;
                return _referenceOrthographicSize / _mainCamera.orthographicSize;
            }

            // Perspective 카메라: 줌 스케일링 미지원 → 기본값 반환
            // Perspective 카메라를 사용한다면 별도 거리 기반 계산으로 교체 필요
            return 1f;
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
            FloatingHpText instance = Instantiate(_prefab, _canvas.transform);
            instance.SetReturnCallback(ReturnToPool);
            return instance;
        }
    }
}
