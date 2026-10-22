using System;
using System.Threading.Tasks;
using Hexiege.Application;
using Hexiege.Infrastructure;
using TMPro;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.UI;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 인게임 설정 안의 조회 전용 프로필. 인증 변경/저장/로그아웃 버튼을 만들지 않는다.
    /// CanvasGroup 숨김은 OnDisable을 부르지 않으므로 소유 설정 화면이 조회 회차를 닫는다.
    /// </summary>
    public sealed class InGameProfileSummaryView : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset _titleFont;
        [SerializeField] private TMP_FontAsset _bodyFont;
        [SerializeField] private Sprite _buttonSprite;

        private readonly PlayerProfileUseCase _profiles = new PlayerProfileUseCase(new PlayerProfileService());
        private readonly RankingUseCase _ranking = new RankingUseCase(new LeaderboardService());
        private readonly TextMeshProUGUI[] _values = new TextMeshProUGUI[7];
        private TextMeshProUGUI _account;
        private Button _back;
        private bool _built;
        private bool _visible;
        private int _version;

        /// <summary>설정 화면이 기존 복귀 핸들러를 연결할 버튼. UI는 최초 접근 때 한 번만 만든다.</summary>
        public Button BackButton { get { EnsureLayout(); return _back; } }

        private void OnDestroy() => Invalidate();

        /// <summary>닫힌 화면의 늦은 응답이 재진입 화면을 덮지 않도록 현재 조회 소유권을 끝낸다.</summary>
        public void Invalidate()
        {
            _visible = false;
            _version++;
        }

        /// <summary>기존 프로필/순위 조회만 수행하여 로비와 같은 정보값을 표시한다.</summary>
        public async Task RefreshAsync()
        {
            EnsureLayout();
            _visible = true;
            int version = ++_version;
            _account.text = "프로필 불러오는 중...";
            foreach (TextMeshProUGUI value in _values) value.text = "-";
            try
            {
                // 의존성 확인만 수행한다. 로그인/로그아웃/UGS 브릿지를 다시 실행하지 않는다.
                var auth = new FirebaseAuthService();
                bool ready = await auth.InitializeAsync();
                if (!IsCurrent(version)) return;
                if (!ready || !auth.IsLoggedIn || !AuthenticationService.Instance.IsSignedIn)
                {
                    _account.text = "로그인 정보를 확인할 수 없습니다.";
                    return;
                }
                string owner = AuthenticationService.Instance.PlayerId;
                bool guest = auth.IsAnonymous;
                string firebaseOwner = auth.FirebaseUID;
                _account.text = guest ? "닉네임 확인 불가" :
                    (!string.IsNullOrWhiteSpace(auth.DisplayName) ? auth.DisplayName : auth.Email);
                if (guest) _values[0].text = "닉네임 확인 불가";
                PlayerProfileData profile = guest ? await _profiles.LoadProfileAsync(true) : await _profiles.LoadProfileAsync();
                if (!IsCurrent(version, owner) || profile == null ||
                    (guest && (!auth.IsLoggedIn || !auth.IsAnonymous || auth.FirebaseUID != firebaseOwner))) return;
                // 익명 계정도 Login에서 처음 한 번 저장한 동일 Cloud Save 닉네임을 사용한다.
                string displayName = profile.HasNickname ? profile.DisplayName : (guest ? "닉네임 확인 불가" : "게스트");
                _values[0].text = displayName;
                if (guest) _account.text = displayName;
                _values[1].text = profile.TotalGames.ToString();
                _values[2].text = profile.Wins.ToString();
                _values[3].text = profile.Losses.ToString();
                _values[4].text = profile.TotalGames > 0 ? $"{profile.WinRate:F1}%" : "-";
                _values[5].text = DateTime.TryParse(profile.LastSessionEndAt, out DateTime date)
                    ? date.ToString("yyyy-MM-dd HH:mm") : "-";
                if (profile.TotalGames < RankingUseCase.MinGamesForRank)
                    _values[6].text = $"순위 없음 ({RankingUseCase.MinGamesForRank}판 이상 필요)";
                else
                {
                    int rank = await _ranking.GetMyRankAsync();
                    if (!IsCurrent(version, owner)) return;
                    _values[6].text = rank > 0 ? $"{rank}위" : "순위 없음";
                }
            }
            catch (Exception e)
            {
                // SDK 조회 실패는 기존 서비스가 운영 기록한다. 여기까지 전파된 표시 예외만
                // UI 통지와 함께 개발 경고로 남기며 계정/닉네임/예외 메시지는 기록하지 않는다.
                GameLog.Dev.Warn("UI", nameof(InGameProfileSummaryView), "인게임 프로필 조회/표시 실패",
                    $"Operation=ReadProfileSummary, ExceptionType={e.GetType().Name}");
                if (IsCurrent(version)) _account.text = "프로필 정보를 불러올 수 없습니다.";
            }
        }

        private bool IsCurrent(int version, string owner = null)
        {
            return this != null && _visible && version == _version &&
                (owner == null || (AuthenticationService.Instance.IsSignedIn &&
                                  AuthenticationService.Instance.PlayerId == owner));
        }

        // 기존 설정 패널의 밝은 배경을 사용한다. 전체 배경 Graphic을 덮지 않아 기존 X 입력도 유지한다.
        private void EnsureLayout()
        {
            if (_built) return;
            _built = true;
            Text("Title", transform, "프로필", true, 48,
                new Vector2(.08f, .86f), new Vector2(.84f, .96f));
            _account = Text("Account", transform, "", false, 36,
                new Vector2(.07f, .72f), new Vector2(.93f, .85f));

            RectTransform viewport = Rect("ProfileViewport", transform, new Vector2(.07f, .18f), new Vector2(.93f, .70f));
            Image surface = viewport.gameObject.AddComponent<Image>();
            surface.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = viewport;
            RectTransform content = Rect("Content", viewport, new Vector2(0, 1), Vector2.one);
            content.pivot = new Vector2(.5f, 1);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            string[] labels = { "닉네임", "총 게임 수", "승리", "패배", "승률", "최근 접속 종료", "내 순위" };
            for (int i = 0; i < labels.Length; i++)
            {
                RectTransform row = Rect("Row" + i, content, Vector2.zero, Vector2.one);
                var height = row.gameObject.AddComponent<LayoutElement>();
                height.minHeight = height.preferredHeight = i == 0 ? 144 : (i >= 5 ? 104 : 72);
                height.flexibleHeight = 0;
                Text("Label", row, labels[i], true, 36, Vector2.zero, new Vector2(.40f, 1));
                _values[i] = Text("Value", row, "-", false, i == 0 ? 42 : 36,
                    new Vector2(.42f, 0), Vector2.one);
            }
            RectTransform back = Rect("BackButton", transform, new Vector2(.15f, .08f), new Vector2(.85f, .16f));
            Image background = back.gameObject.AddComponent<Image>();
            background.sprite = _buttonSprite;
            _back = back.gameObject.AddComponent<Button>();
            _back.targetGraphic = background;
            Text("Label", back, "뒤로", true, 36, Vector2.zero, Vector2.one);
        }

        private TextMeshProUGUI Text(string name, Transform parent, string value, bool title, int size, Vector2 min, Vector2 max)
        {
            RectTransform rect = Rect(name, parent, min, max);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            // 기본 폰트로 한글을 처리하기 전에 실제 역할 에셋을 먼저 지정한다.
            text.font = title ? _titleFont : _bodyFont;
            text.fontStyle = FontStyles.Normal;
            text.fontSize = size;
            text.color = Color.black;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
