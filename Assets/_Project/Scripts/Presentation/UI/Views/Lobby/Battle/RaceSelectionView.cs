// ============================================================================
// RaceSelectionView.cs
// 로비 전투 탭의 종족 선택 UI View — 캐러셀 방식.
//
// 역할:
//   - RenderTexture를 통해 3D 캐릭터 미리보기 표시 (RawImage)
//   - 종족명 텍스트 표시 (TMP_Text)
//   - 좌우 화살표 버튼으로 종족 전환
//   - 캐러셀: 3종족 캐릭터를 동시에 표시하고, 선택된 종족은 중앙(가까이),
//     나머지는 좌/우(멀리)에 배치하여 원근감으로 크기 차이 표현
//   - DOTween을 사용한 부드러운 포지션 이동 애니메이션
//
// Inspector 연결:
//   - _rawImage: RenderTexture를 출력할 RawImage (캐릭터 미리보기)
//   - _raceNameText: 종족명을 표시할 TMP_Text
//   - _prevButton: 왼쪽 화살표 Button
//   - _nextButton: 오른쪽 화살표 Button
//   - _characterRoots: 종족별 3D 캐릭터 루트 오브젝트 배열
//       [0] = Human(인간), [1] = Spirit(정령), [2] = Nature(자연)
//   - _centerPos: 선택된 종족이 위치할 중앙 좌표 (카메라에 가까움 → 크게 보임)
//   - _leftPos: 비선택 종족의 왼쪽 좌표 (카메라에서 멀어 → 작게 보임)
//   - _rightPos: 비선택 종족의 오른쪽 좌표 (카메라에서 멀어 → 작게 보임)
//   - _moveDuration: 캐러셀 이동 애니메이션 시간(초)
//
// Presentation 레이어 — MonoBehaviour.
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;
using DG.Tweening;
using Hexiege.Domain;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 종족 선택 UI View. RaceSelectionViewModel에 바인딩하여 캐러셀 방식으로 종족 전환 표시.
    /// </summary>
    public class RaceSelectionView : MonoBehaviour, IView<RaceSelectionViewModel>
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("UI")]
        [Tooltip("RenderTexture를 출력할 RawImage (3D 캐릭터 미리보기)")]
        [SerializeField] private RawImage _rawImage;

        [Tooltip("현재 선택된 종족명을 표시할 텍스트")]
        [SerializeField] private TMP_Text _raceNameText;

        [Tooltip("이전 종족으로 전환하는 왼쪽 화살표 버튼")]
        [SerializeField] private Button _prevButton;

        [Tooltip("다음 종족으로 전환하는 오른쪽 화살표 버튼")]
        [SerializeField] private Button _nextButton;

        [Header("캐릭터 오브젝트")]
        [Tooltip("종족별 3D 캐릭터 루트. [0]인간 [1]정령 [2]자연")]
        [SerializeField] private GameObject[] _characterRoots;

        [Header("캐러셀 슬롯 위치 (World Space)")]
        [Tooltip("선택된 종족이 위치할 중앙 좌표 — 카메라에 가까워서 크게 보임")]
        [SerializeField] private Vector3 _centerPos = new Vector3(1000f, 0f, 4f);

        [Tooltip("비선택 종족의 왼쪽 좌표 — 카메라에서 멀어서 작게 보임")]
        [SerializeField] private Vector3 _leftPos = new Vector3(997.5f, 0f, 7f);

        [Tooltip("비선택 종족의 오른쪽 좌표 — 카메라에서 멀어서 작게 보임")]
        [SerializeField] private Vector3 _rightPos = new Vector3(1002.5f, 0f, 7f);

        [Tooltip("캐러셀 이동 애니메이션 시간(초)")]
        [SerializeField] private float _moveDuration = 0.3f;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>UniRx 구독 일괄 해제용.</summary>
        private readonly CompositeDisposable _disposables = new();

        // ====================================================================
        // IView 구현
        // ====================================================================

        /// <summary>
        /// RaceSelectionViewModel에 바인딩.
        /// 종족 변경 시 캐러셀 포지션 이동 + 종족명 텍스트 갱신 + 버튼 커맨드 연결.
        /// </summary>
        public void Bind(RaceSelectionViewModel vm)
        {
            // 기존 구독이 남아있으면 먼저 정리
            Unbind();

            // 첫 번째 구독(초기값)은 애니메이션 없이 즉시 배치,
            // 이후 변경부터는 DOTween 애니메이션으로 부드럽게 이동
            bool isFirst = true;
            vm.SelectedRace
                .Subscribe(race =>
                {
                    ApplyCarouselPositions(race, animate: !isFirst);
                    isFirst = false;
                })
                .AddTo(_disposables);

            // 종족명 텍스트 갱신
            if (_raceNameText != null)
            {
                vm.SelectedRaceName
                    .Subscribe(name => _raceNameText.text = name)
                    .AddTo(_disposables);
            }

            // 왼쪽 화살표 → 이전 종족
            if (_prevButton != null)
                _prevButton.onClick.AddListener(() => vm.CmdPrev.OnNext(Unit.Default));

            // 오른쪽 화살표 → 다음 종족
            if (_nextButton != null)
                _nextButton.onClick.AddListener(() => vm.CmdNext.OnNext(Unit.Default));
        }

        /// <summary>
        /// 구독 해제 + 버튼 리스너 제거 + 진행 중인 DOTween 정리.
        /// 재바인딩이나 씬 전환 전에 호출.
        /// </summary>
        public void Unbind()
        {
            _disposables.Clear();

            if (_prevButton != null) _prevButton.onClick.RemoveAllListeners();
            if (_nextButton != null) _nextButton.onClick.RemoveAllListeners();

            // 진행 중인 DOTween 애니메이션 정리 — 씬 전환 시 트윈이 남아있으면 오류 발생 가능
            KillAllCharacterTweens();
        }

        // ====================================================================
        // 캐러셀 로직
        // ====================================================================

        /// <summary>
        /// 선택된 종족에 따라 3개 캐릭터를 중앙/좌/우 위치에 배치한다.
        ///
        /// offset 계산:
        ///   offset = (캐릭터인덱스 - 선택된종족인덱스 + 3) % 3
        ///   offset 0 → 중앙 (_centerPos) — 선택된 종족
        ///   offset 1 → 오른쪽 (_rightPos) — 다음 종족
        ///   offset 2 → 왼쪽 (_leftPos) — 이전 종족
        ///
        /// animate=false이면 position을 즉시 설정 (초기 배치용),
        /// animate=true이면 DOTween.DOMove로 부드럽게 이동.
        /// </summary>
        private void ApplyCarouselPositions(RaceId selected, bool animate)
        {
            // _characterRoots가 비어있거나 null이면 안전하게 종료
            if (_characterRoots == null || _characterRoots.Length == 0) return;

            int selectedIndex = (int)selected;

            for (int i = 0; i < _characterRoots.Length; i++)
            {
                // 배열 범위 초과 또는 null 오브젝트 방어
                if (i >= _characterRoots.Length || _characterRoots[i] == null) continue;

                // 모든 캐릭터는 항상 활성 상태 — 캐러셀에서 3개 동시 표시
                _characterRoots[i].SetActive(true);

                // offset 계산: 선택된 종족 기준으로 상대 위치 결정
                // offset 0 = 중앙(선택됨), 1 = 오른쪽(다음), 2 = 왼쪽(이전)
                int offset = (i - selectedIndex + 3) % 3;

                Vector3 targetPos = offset switch
                {
                    0 => _centerPos,  // 선택된 종족 → 중앙 (카메라에 가까움 → 크게 보임)
                    1 => _rightPos,   // 다음 종족 → 오른쪽 (카메라에서 멀어 → 작게 보임)
                    _ => _leftPos,    // 이전 종족 → 왼쪽 (카메라에서 멀어 → 작게 보임)
                };

                Transform charTransform = _characterRoots[i].transform;

                if (!animate)
                {
                    // 초기 배치: 애니메이션 없이 즉시 위치 설정
                    charTransform.position = targetPos;
                }
                else
                {
                    // 캐러셀 이동: 기존 트윈을 중단하고 새 목표 위치로 부드럽게 이동
                    DOTween.Kill(charTransform);
                    charTransform.DOMove(targetPos, _moveDuration).SetEase(Ease.OutCubic);
                }
            }
        }

        /// <summary>
        /// _characterRoots의 모든 오브젝트에 걸려있는 DOTween 트윈을 중단한다.
        /// Unbind() 시 호출하여 씬 전환 시 트윈 잔여물 방지.
        /// </summary>
        private void KillAllCharacterTweens()
        {
            if (_characterRoots == null) return;

            for (int i = 0; i < _characterRoots.Length; i++)
            {
                if (_characterRoots[i] != null)
                    DOTween.Kill(_characterRoots[i].transform);
            }
        }
    }
}
