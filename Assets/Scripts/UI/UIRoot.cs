using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Deskmon.Core;
using Deskmon.Capture;
using Deskmon.Native;

namespace Deskmon.UI
{
    /// <summary>
    /// 베이스캠프 코너 카드 + 패널 전환. index.html의 카드/배지/패널 구조 이식 (기획 v4 §3.2).
    ///
    /// 형태: 우하단 배지(접힘) <-> 카드(펼침). 카드에서 가방/도감/설정 패널을 연다.
    ///
    /// 클릭통과와의 계약:
    ///   이 UI가 차지하는 사각 영역을 DesktopOverlay에 등록한다(IInteractiveRect).
    ///   커서가 그 안에 있을 때만 창이 입력을 받고, 밖에서는 뒤쪽 창이 눌린다.
    ///   같은 영역을 SigilInput에도 등록해 각인 획이 UI 위에서 시작되지 않게 한다.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class UIRoot : MonoBehaviour, IInteractiveRect
    {
        [Header("배치")]
        public Vector2 margin = new Vector2(12f, 12f);

        /// <summary>접힘 상태 - 배지만 보인다.</summary>
        public bool Collapsed { get; private set; } = true;

        GameState _game;
        RoamManager _roam;

        Canvas _canvas;
        GameObject _badge, _card, _panelHost;
        Text _badgeBerry, _cardBerry, _cardProd, _cardRoam;

        GameObject _openPanel;
        string _openPanelName;
        BagPanel _bag;
        DexPanel _dex;
        SettingsPanel _settings;

        Rect _screenRect;
        Rect _sigilBlocked;

        // ── IInteractiveRect ──
        public Rect ScreenRect => _screenRect;
        public bool IsActive => isActiveAndEnabled;

        void Start()
        {
            _game = GameState.Instance;
            _roam = FindFirstObjectByType<RoamManager>();

            BuildCanvas();
            BuildBadge();
            BuildCard();
            SetCollapsed(true);

            if (DesktopOverlay.Instance != null) DesktopOverlay.Instance.Register(this);
            if (_game != null) _game.OnCaught += _ => RefreshOpenPanel();
        }

        void OnDestroy()
        {
            if (DesktopOverlay.Instance != null) DesktopOverlay.Instance.Unregister(this);
            if (_sigilBlocked.width > 0f) SigilInput.UnblockArea(_sigilBlocked);
        }

        void Update()
        {
            // 카드 상단 요약은 매 프레임 갱신해도 싸다 - 텍스트 몇 개다.
            if (_game?.Save != null && _game.db?.balance != null)
            {
                string berry = UIKit.Fmt(_game.Save.berry);
                if (_badgeBerry != null) _badgeBerry.text = berry;
                if (_cardBerry != null) _cardBerry.text = "베리 " + berry;

                if (_cardProd != null)
                {
                    bool working = IdleTime.IsWorking(_game.db.balance.workingIdleSec);
                    float p = CreatureRegistry.ProductionPerSecond(_game.Save, _game.db, working);
                    _cardProd.text = $"+{p:F1}/초" + (working ? " (부스트)" : "");
                }

                if (_cardRoam != null && _roam != null)
                    _cardRoam.text = $"방목 {_roam.Count}/{RoamSystem.Slots(_game.Save, _game.db.balance)}";
            }

            UpdateInteractiveRect();
        }

        /// <summary>
        /// 현재 보이는 UI(배지 또는 카드+패널)의 화면 영역을 계산해
        /// 클릭통과 판정과 각인 차단에 반영한다.
        /// </summary>
        void UpdateInteractiveRect()
        {
            Rect r = RectOf(Collapsed ? _badge : _card);
            if (!Collapsed && _openPanel != null && _openPanel.activeSelf)
                r = Union(r, RectOf(_openPanel));

            _screenRect = r;

            if (r != _sigilBlocked)
            {
                if (_sigilBlocked.width > 0f) SigilInput.UnblockArea(_sigilBlocked);
                _sigilBlocked = r;
                SigilInput.BlockArea(_sigilBlocked);
            }
        }

        static Rect RectOf(GameObject go)
        {
            if (go == null || !go.activeSelf) return default;
            var rt = (RectTransform)go.transform;
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            // ScreenSpaceOverlay 캔버스의 월드 좌표는 곧 스크린 픽셀(좌하단 원점)이다.
            return new Rect(c[0].x, c[0].y, c[2].x - c[0].x, c[2].y - c[0].y);
        }

        static Rect Union(Rect a, Rect b)
        {
            if (a.width <= 0f) return b;
            if (b.width <= 0f) return a;
            float xMin = Mathf.Min(a.xMin, b.xMin), yMin = Mathf.Min(a.yMin, b.yMin);
            float xMax = Mathf.Max(a.xMax, b.xMax), yMax = Mathf.Max(a.yMax, b.yMax);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        // ── 조립 ──

        void BuildCanvas()
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // 클릭이 UI에 닿으려면 EventSystem이 필요하다. 입력 핸들러가 Both라
            // 레거시 StandaloneInputModule로 충분하다.
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem",
                    typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }
        }

        /// <summary>접힌 상태 - 우하단 작은 배지. 베리 수만 보여준다.</summary>
        void BuildBadge()
        {
            _badge = UIKit.Panel(_canvas.transform, "Badge", new Vector2(86f, 40f),
                                 new Vector2(1f, 0f), new Vector2(-margin.x, margin.y));

            var btn = _badge.AddComponent<Button>();
            btn.targetGraphic = _badge.GetComponent<Image>();
            btn.onClick.AddListener(() => SetCollapsed(false));

            var row = UIKit.HRow(_badge.transform, 40f);
            UIKit.Stretch(row);
            ((HorizontalLayoutGroup)row.GetComponent<HorizontalLayoutGroup>()).padding
                = new RectOffset(10, 10, 0, 0);

            var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(row, false);
            dot.GetComponent<Image>().color = UIKit.Accent;
            dot.GetComponent<Image>().raycastTarget = false;
            UIKit.Fixed(dot, 10f, 10f);

            _badgeBerry = UIKit.Label(row, "0", 14, UIKit.TextGold);
            _badgeBerry.gameObject.AddComponent<LayoutElement>().preferredWidth = 52f;
        }

        /// <summary>펼친 상태 - 베이스캠프 카드. 요약 + 패널 버튼.</summary>
        void BuildCard()
        {
            _card = UIKit.Panel(_canvas.transform, "CampCard", new Vector2(260f, 120f),
                                new Vector2(1f, 0f), new Vector2(-margin.x, margin.y));

            var list = UIKit.VList(_card.transform, 4f);
            UIKit.Stretch(list);

            // 헤더: 제목 + 접기
            var head = UIKit.HRow(list, 22f);
            var title = UIKit.Label(head, "베이스캠프", 15, UIKit.TextMain);
            title.fontStyle = FontStyle.Bold;
            title.gameObject.AddComponent<LayoutElement>().preferredWidth = 150f;
            var collapse = UIKit.Button(head, "접기", 12, new Vector2(46f, 20f),
                                        () => SetCollapsed(true));
            UIKit.Fixed(collapse.gameObject, 46f, 20f);

            // 요약
            _cardBerry = UIKit.Label(list, "베리 0", 14, UIKit.TextGold);
            UIKit.Fixed(_cardBerry.gameObject, 0f, 18f);
            _cardProd = UIKit.Label(list, "+0.0/초", 12, UIKit.TextSub);
            UIKit.Fixed(_cardProd.gameObject, 0f, 16f);
            _cardRoam = UIKit.Label(list, "방목 0/2", 12, UIKit.TextSub);
            UIKit.Fixed(_cardRoam.gameObject, 0f, 16f);

            // 패널 버튼
            var btns = UIKit.HRow(list, 26f);
            foreach (var name in new[] { "가방", "도감", "설정" })
            {
                string captured = name;
                var b = UIKit.Button(btns, name, 13, new Vector2(72f, 24f),
                                     () => TogglePanel(captured));
                UIKit.Fixed(b.gameObject, 72f, 24f);
            }

            // 패널 호스트 - 카드 위쪽에 붙는다
            _panelHost = new GameObject("PanelHost", typeof(RectTransform));
            _panelHost.transform.SetParent(_canvas.transform, false);
            var prt = (RectTransform)_panelHost.transform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(1f, 0f);
            prt.anchoredPosition = new Vector2(-margin.x, margin.y + 128f);
            prt.sizeDelta = new Vector2(320f, 380f);
        }

        void SetCollapsed(bool collapsed)
        {
            Collapsed = collapsed;
            _badge.SetActive(collapsed);
            _card.SetActive(!collapsed);
            if (collapsed) ClosePanel();
        }

        // ── 패널 ──

        void TogglePanel(string name)
        {
            if (_openPanelName == name) { ClosePanel(); return; }
            ClosePanel();

            switch (name)
            {
                case "가방":
                    if (_bag == null) _bag = new BagPanel(_panelHost.transform, this);
                    _openPanel = _bag.Root; _bag.Refresh(); break;
                case "도감":
                    if (_dex == null) _dex = new DexPanel(_panelHost.transform, this);
                    _openPanel = _dex.Root; _dex.Refresh(); break;
                case "설정":
                    if (_settings == null) _settings = new SettingsPanel(_panelHost.transform, this);
                    _openPanel = _settings.Root; _settings.Refresh(); break;
            }

            if (_openPanel != null) { _openPanel.SetActive(true); _openPanelName = name; }
        }

        void ClosePanel()
        {
            if (_openPanel != null) _openPanel.SetActive(false);
            _openPanel = null;
            _openPanelName = null;
        }

        /// <summary>열린 패널을 다시 그린다. 포획/진화/해금 뒤에 부른다.</summary>
        public void RefreshOpenPanel()
        {
            if (_openPanelName == "가방") _bag?.Refresh();
            else if (_openPanelName == "도감") _dex?.Refresh();
            else if (_openPanelName == "설정") _settings?.Refresh();
        }

        // 패널들이 쓰는 공용 접근자
        public GameState Game => _game;
        public RoamManager Roam => _roam;
    }
}
