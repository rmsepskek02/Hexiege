// ============================================================================
// ResearchMatrixView.cs
// 연구 패널 "매트릭스 레이어"의 드라이버. 행=현재 종족의 그룹, 열=공격력/방어력/이동속도로
// 셀(ResearchCellView) 격자를 동적 생성하고, RefreshRequested 마다 모든 셀을 갱신한다.
//
// 하는 일:
//   1) 패널이 열리면 소유 팀 종족에 맞는 격자를 구성한다.
//        · 인간/정령: 3그룹 × (공/방/속) = 3행 × 3열.
//        · 초월: 2그룹 × (공/방/속) = 2행 × 3열 + 자연회복 전체폭 셀 1개(격자 아래, 그룹 무관).
//   2) RefreshRequested(팀 상태·골드·연구 착수 변화)마다 모든 셀의 Refresh()를 호출한다.
//   3) 트랙 식별은 이름 매칭이 아니라 (group, stat) 데이터로 한다(.claude 교훈: 이름 기반 오연결 위험).
//
// 열 헤더(공/방/속)는 씬에 정적으로 배치(Wire가 생성)하고, 행(그룹)은 팀 종족에 따라 여기서 동적 생성한다.
// 행이 바뀌는 경우는 팀(=종족)이 바뀔 때뿐이라, 게임 중 사실상 1회만 생성된다(모바일 친화 — 매 프레임 할당 없음).
//
// Presentation 레이어 — MonoBehaviour. Domain(UpgradeGroup/UnitUpgradeStat) 만 참조.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Hexiege.Application;      // GameLog — 런타임 로그 파사드 (LogRules.md 1.4)
using Hexiege.Domain;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 연구 매트릭스 레이어의 셀 격자를 동적으로 생성/갱신하는 드라이버.
    /// ResearchPanelUI의 조회 API와 RefreshRequested 이벤트만으로 동작한다.
    /// </summary>
    public class ResearchMatrixView : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("연구 로직 코어. 상태 조회/연구 착수 라우팅/갱신 알림(RefreshRequested)의 원천.")]
        [SerializeField] private ResearchPanelUI _panel;

        [Tooltip("셀 하나의 템플릿(ResearchCellView). 비활성 상태로 두고 복제해서 사용한다.")]
        [SerializeField] private ResearchCellView _cellTemplate;

        [Tooltip("행 앞머리 그룹 라벨 텍스트 템플릿(예: '근접'). 비활성 상태로 두고 복제한다.")]
        [SerializeField] private TextMeshProUGUI _rowLabelTemplate;

        [Tooltip("생성된 행이 배치될 컨테이너(보통 VerticalLayoutGroup).")]
        [SerializeField] private Transform _rowContainer;

        [Header("Layout")]
        [Tooltip("각 그룹 행의 높이(px). 모바일 터치 대상 크기. 스크린샷 후 튜닝 대상.")]
        [SerializeField] private float _rowHeight = 92f;

        [Tooltip("행 앞머리 그룹 라벨의 가로 비중(flexibleWidth). 셀 비중 대비 상대값.")]
        [SerializeField] private float _labelWeight = 1.0f;

        [Tooltip("각 셀의 가로 비중(flexibleWidth).")]
        [SerializeField] private float _cellWeight = 1.4f;

        [Tooltip("행 내부 요소 간격(px).")]
        [SerializeField] private float _rowSpacing = 6f;

        // 열 순서(공/방/속). 자연회복은 초월 전용 별도 전체폭 셀로 추가한다.
        private static readonly UnitUpgradeStat[] Columns =
        {
            UnitUpgradeStat.Attack,
            UnitUpgradeStat.Defense,
            UnitUpgradeStat.MoveSpeed
        };

        // 생성된 셀 목록(갱신 대상).
        private readonly List<ResearchCellView> _cells = new List<ResearchCellView>();
        // 생성된 행 GameObject 목록(재생성 시 파괴 대상).
        private readonly List<GameObject> _rows = new List<GameObject>();

        private bool _built;
        private TeamId _builtTeam;
        private bool _subscribed;

        // ====================================================================
        // 생명주기
        // ====================================================================

        private void Awake()
        {
            // 템플릿은 프로토타입일 뿐이라 항상 숨겨 둔다(복제본만 활성).
            if (_cellTemplate != null) _cellTemplate.gameObject.SetActive(false);
            if (_rowLabelTemplate != null) _rowLabelTemplate.gameObject.SetActive(false);
        }

        private void OnEnable() => TrySubscribe();
        private void Start() => TrySubscribe(); // 패널 Initialize 시점과 무관하게 확실히 구독.

        private void OnDisable()
        {
            if (_subscribed && _panel != null)
            {
                _panel.RefreshRequested -= OnRefreshRequested;
                _subscribed = false;
            }
        }

        // RefreshRequested는 순수 C# 이벤트라 중복 구독 방지를 위해 플래그로 가드한다.
        private void TrySubscribe()
        {
            if (_subscribed || _panel == null) return;
            _panel.RefreshRequested += OnRefreshRequested;
            _subscribed = true;
        }

        // ====================================================================
        // 갱신 진입점
        // ====================================================================

        // 팀 상태/골드/연구 착수가 바뀔 때마다(그리고 패널 Open 시) 호출된다.
        private void OnRefreshRequested()
        {
            // 패널이 닫혀 있으면 굳이 갱신하지 않는다.
            if (_panel == null || !_panel.IsPanelOpen) return;

            EnsureBuilt();
            for (int i = 0; i < _cells.Count; i++)
            {
                if (_cells[i] != null) _cells[i].Refresh();
            }
        }

        // 팀(=종족 격자 구성)이 바뀌었을 때만 격자를 재생성한다. 그 외엔 재사용.
        private void EnsureBuilt()
        {
            if (_built && _builtTeam == _panel.CurrentTeam) return;
            BuildMatrix();
        }

        // ====================================================================
        // 격자 생성
        // ====================================================================

        // 현재 팀 종족에 맞는 그룹 행(공/방/속) + 초월이면 자연회복 전체폭 셀을 만든다.
        private void BuildMatrix()
        {
            Clear();

            if (_cellTemplate == null || _rowContainer == null)
            {
                // [개발] Warn + 개발 — Inspector 배선 누락(1.3 원칙 3 단서).
                GameLog.Dev.Warn("UI", nameof(ResearchMatrixView),
                                 "셀 템플릿/컨테이너 미배선 — 강화 격자를 생성할 수 없다",
                                 "Field=_cellTemplate|_rowContainer");
                _built = false;
                return;
            }

            // ── 그룹 행(공/방/속) ──
            UpgradeGroup[] groups = _panel.GetGroupsForCurrentTeam();
            for (int g = 0; g < groups.Length; g++)
            {
                UpgradeGroup group = groups[g];
                GameObject row = CreateRow($"Row_{group}");
                CreateRowLabel(row.transform, ResearchPanelUI.GroupDisplayName(group));
                for (int c = 0; c < Columns.Length; c++)
                    CreateCell(row.transform, group, Columns[c], _cellWeight);
            }

            // ── 초월 종족 전용 자연회복 전체폭 셀(그룹 무관 — 정규화 그룹 키) ──
            if (_panel.CurrentTeamIsTranscendence())
            {
                GameObject row = CreateRow("Row_Regen");
                CreateRowLabel(row.transform, ResearchPanelUI.StatDisplayName(UnitUpgradeStat.Regen));
                // 전체폭: 3열 자리를 한 셀이 차지하도록 비중을 열 3칸 합으로 준다.
                CreateCell(row.transform, UpgradeGroupHelper.RegenCanonicalGroup, UnitUpgradeStat.Regen,
                    _cellWeight * Columns.Length);
            }

            _built = true;
            _builtTeam = _panel.CurrentTeam;
        }

        // 한 행(HorizontalLayoutGroup)을 컨테이너 아래에 만든다.
        private GameObject CreateRow(string name)
        {
            var rowGo = new GameObject(name, typeof(RectTransform));
            rowGo.transform.SetParent(_rowContainer, false);

            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = _rowSpacing;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; // 각 자식은 자신의 flexibleWidth 비중으로 폭 분배.
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleCenter;

            var le = rowGo.AddComponent<LayoutElement>();
            le.preferredHeight = _rowHeight;
            le.flexibleHeight = 0f;   // 행은 고정 높이(MEMORY 교훈: flexibleHeight>0 이면 크기 변동).
            le.flexibleWidth = 1f;

            _rows.Add(rowGo);
            return rowGo;
        }

        // 행 앞머리 그룹 라벨을 복제해 배치한다.
        private void CreateRowLabel(Transform rowParent, string text)
        {
            if (_rowLabelTemplate == null) return;
            TextMeshProUGUI label = Instantiate(_rowLabelTemplate, rowParent);
            label.gameObject.SetActive(true);
            label.text = text;

            // 라벨 가로 비중 지정(템플릿에 LayoutElement 없으면 부착).
            var le = label.GetComponent<LayoutElement>();
            if (le == null) le = label.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = _labelWeight;
        }

        // 셀 템플릿을 복제해 트랙에 바인딩하고 가로 비중을 지정한다.
        private void CreateCell(Transform rowParent, UpgradeGroup group, UnitUpgradeStat stat, float weight)
        {
            ResearchCellView cell = Instantiate(_cellTemplate, rowParent);
            cell.gameObject.SetActive(true);

            var le = cell.GetComponent<LayoutElement>();
            if (le == null) le = cell.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = weight;

            cell.Bind(_panel, group, stat);
            _cells.Add(cell);
        }

        // 기존 행/셀을 모두 파괴하고 목록을 비운다.
        private void Clear()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null) Destroy(_rows[i]);
            }
            _rows.Clear();
            _cells.Clear();
            _built = false;
        }
    }
}
