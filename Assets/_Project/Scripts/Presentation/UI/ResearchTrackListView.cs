// ============================================================================
// ResearchTrackListView.cs
// 연구 패널의 "트랙 행 드라이버". ResearchPanelUI(로직 코어)가 위임하는 행 렌더링/구동 담당.
//
// 하는 일:
//   1) 패널이 열리면(panel.IsOpen) 소유 팀 종족에 맞는 트랙 목록을 데이터로 산출해
//      행 템플릿을 동적 인스턴스화한다(종족별 9/9/7 행).
//        · 인간/정령: 3그룹 × (공/방/속) = 9행
//        · 초월: 2그룹 × (공/방/속) = 6행 + 자연회복 1행 = 7행
//   2) RefreshRequested(팀 상태·골드·연구 착수 변화) 마다 모든 행의 Refresh() 를 호출한다.
//   3) 트랙 식별은 이름 매칭이 아니라 (group, stat) 데이터로 한다(.claude 교훈: 이름 기반 오연결 위험).
//
// 설계 메모:
//   - ResearchPanelUI 는 "로직 코어"(상태 조회 + 라우팅)로 그대로 두고, 이 컴포넌트가 시각 행을 담당한다.
//     (역할 분리 — 로직 코어를 건드리지 않고 완성.)
//   - 실전에서 팀은 게임 내내 고정(자기 연구소만 클릭)이라 행은 사실상 1회만 생성된다.
//     그래도 팀이 바뀌면(방어적) 재생성하도록 서명 비교를 둔다.
//   - 행 생성은 패널이 열릴 때 1회뿐이라 Update 매 프레임 할당이 없다(모바일 친화).
//
// Presentation 레이어 — MonoBehaviour. Domain(UpgradeGroup/UnitUpgradeStat) 만 참조.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 연구 패널의 트랙 행을 동적으로 생성/갱신하는 드라이버.
    /// ResearchPanelUI 의 조회 API 와 RefreshRequested 이벤트만으로 동작한다.
    /// </summary>
    public class ResearchTrackListView : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("연구 로직 코어. 상태 조회/연구 착수 라우팅/갱신 알림(RefreshRequested)의 원천.")]
        [SerializeField] private ResearchPanelUI _panel;

        [Tooltip("행 하나의 템플릿(ResearchTrackRowView). 비활성 상태로 두고 복제해서 사용한다.")]
        [SerializeField] private ResearchTrackRowView _rowTemplate;

        [Tooltip("생성된 행이 배치될 컨테이너(보통 VerticalLayoutGroup). 비우면 템플릿의 부모를 사용.")]
        [SerializeField] private Transform _rowContainer;

        // 공/방/속 3스탯(그룹 단위 트랙). 자연회복은 초월 전용 별도 트랙으로 추가한다.
        private static readonly UnitUpgradeStat[] GroupStats =
        {
            UnitUpgradeStat.Attack,
            UnitUpgradeStat.Defense,
            UnitUpgradeStat.MoveSpeed
        };

        // 생성된 행 목록(템플릿 제외).
        private readonly List<ResearchTrackRowView> _rows = new List<ResearchTrackRowView>();

        private bool _built;
        private TeamId _builtTeam;
        private bool _subscribed;

        // ====================================================================
        // 생명주기
        // ====================================================================

        private void Awake()
        {
            // 템플릿은 프로토타입일 뿐이라 항상 숨겨 둔다(복제본만 활성).
            if (_rowTemplate != null) _rowTemplate.gameObject.SetActive(false);

            // 컨테이너 미지정 시 템플릿의 부모를 사용(에디터 배선 누락 방어).
            if (_rowContainer == null && _rowTemplate != null)
                _rowContainer = _rowTemplate.transform.parent;
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

        // RefreshRequested 는 순수 C# 이벤트라 중복 구독 방지를 위해 플래그로 가드한다.
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
            // 패널이 닫혀 있으면(CanvasGroup alpha=0) 굳이 갱신하지 않는다.
            if (_panel == null || !_panel.IsOpen) return;

            EnsureRowsBuilt();
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null) _rows[i].Refresh();
            }
        }

        // 팀(=종족 트랙 구성)이 바뀌었을 때만 행을 재생성한다. 그 외엔 재사용.
        private void EnsureRowsBuilt()
        {
            if (_built && _builtTeam == _panel.CurrentTeam) return;
            BuildRows();
        }

        // ====================================================================
        // 행 생성
        // ====================================================================

        // 현재 팀 종족에 맞는 트랙(그룹×스탯 + 초월이면 자연회복)마다 행을 만든다.
        private void BuildRows()
        {
            ClearRows();

            if (_rowTemplate == null || _rowContainer == null)
            {
                Debug.LogWarning("[ResearchTrackListView] 행 템플릿/컨테이너가 배선되지 않아 행을 생성할 수 없습니다.");
                _built = false;
                return;
            }

            // 그룹 트랙(공/방/속).
            UpgradeGroup[] groups = _panel.GetGroupsForCurrentTeam();
            for (int g = 0; g < groups.Length; g++)
            {
                for (int s = 0; s < GroupStats.Length; s++)
                {
                    UpgradeGroup group = groups[g];
                    UnitUpgradeStat stat = GroupStats[s];
                    CreateRow(group, stat, BuildTrackName(group, stat));
                }
            }

            // 초월 종족 전용 자연회복 트랙(그룹 무관 — 정규화 그룹 키 사용).
            if (_panel.CurrentTeamIsTranscendence())
            {
                CreateRow(UpgradeGroupHelper.RegenCanonicalGroup, UnitUpgradeStat.Regen,
                    StatName(UnitUpgradeStat.Regen));
            }

            _built = true;
            _builtTeam = _panel.CurrentTeam;
        }

        // 템플릿을 복제해 한 행을 만들고 바인딩한다.
        private void CreateRow(UpgradeGroup group, UnitUpgradeStat stat, string displayName)
        {
            ResearchTrackRowView row = Instantiate(_rowTemplate, _rowContainer);
            row.gameObject.SetActive(true);
            row.Bind(_panel, group, stat, displayName);
            _rows.Add(row);
        }

        // 기존 행을 모두 파괴하고 목록을 비운다.
        private void ClearRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null) Destroy(_rows[i].gameObject);
            }
            _rows.Clear();
            _built = false;
        }

        // ====================================================================
        // 트랙 표시 이름(한국어)
        // ====================================================================

        // "그룹 + 스탯"(예: "근접 공격력"). 자연회복은 그룹 무관이라 스탯명만.
        private static string BuildTrackName(UpgradeGroup group, UnitUpgradeStat stat)
        {
            if (stat == UnitUpgradeStat.Regen) return StatName(stat);
            return $"{GroupName(group)} {StatName(stat)}";
        }

        private static string GroupName(UpgradeGroup group)
        {
            switch (group)
            {
                case UpgradeGroup.HumanMelee: return "근접";
                case UpgradeGroup.HumanRanged: return "원거리";
                case UpgradeGroup.HumanVehicle: return "탈것";
                case UpgradeGroup.SpiritFire: return "불";
                case UpgradeGroup.SpiritWater: return "물";
                case UpgradeGroup.SpiritEarth: return "땅";
                case UpgradeGroup.TransAnimal: return "동물";
                case UpgradeGroup.TransPlant: return "식물";
                default: return group.ToString();
            }
        }

        private static string StatName(UnitUpgradeStat stat)
        {
            switch (stat)
            {
                case UnitUpgradeStat.Attack: return "공격력";
                case UnitUpgradeStat.Defense: return "방어력";
                case UnitUpgradeStat.MoveSpeed: return "이동속도";
                case UnitUpgradeStat.Regen: return "자연회복";
                default: return stat.ToString();
            }
        }
    }
}
