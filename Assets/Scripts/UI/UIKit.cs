using UnityEngine;
using UnityEngine.UI;

namespace Deskmon.UI
{
    /// <summary>
    /// 코드로 UGUI를 조립하기 위한 공통 도구. 씬/프리팹 수작업 대신 코드로 만드는 이유는
    /// 씬 생성기와 같다 - 재현되고, 리뷰되고, 원본(index.html의 DOM 구성)과 대조할 수 있다.
    ///
    /// 지금은 단색 사각형 플레이스홀더다. 실제 UI 이미지의 사양은
    /// Docs/UI_이미지_기획서.md 에 있고, 생성되면 여기 팔레트/배경만 교체한다.
    /// </summary>
    public static class UIKit
    {
        /// <summary>
        /// 적용할 이미지 테마. UIRoot가 시작할 때 넣는다. null이거나 칸이 비면
        /// 아래 단색 플레이스홀더로 폴백한다 - 이미지가 하나씩 승인되는 파이프라인이라
        /// 일부만 있어도 화면이 깨지면 안 된다.
        /// </summary>
        public static UITheme Theme;

        // ── 팔레트 - index.html의 카드 톤(밝은 종이 + 초록 포인트)을 어둡게 뒤집은 것.
        //    바탕화면 위에 뜨는 반투명 카드라 밝은 배경은 아이콘과 뒤섞인다.
        public static readonly Color PanelBg = new Color(0.07f, 0.10f, 0.08f, 0.93f);
        public static readonly Color PanelLine = new Color(0.55f, 0.69f, 0.62f, 0.35f);
        public static readonly Color BtnBg = new Color(1f, 1f, 1f, 0.10f);
        public static readonly Color BtnBgOn = new Color(0.50f, 0.79f, 0.48f, 0.30f);
        public static readonly Color TextMain = new Color(0.92f, 0.96f, 0.93f, 1f);
        public static readonly Color TextSub = new Color(0.62f, 0.69f, 0.64f, 1f);
        public static readonly Color TextGold = new Color(1f, 0.91f, 0.64f, 1f);
        public static readonly Color TextWarn = new Color(1f, 0.56f, 0.56f, 1f);
        public static readonly Color Accent = new Color(0.56f, 0.85f, 0.47f, 1f);

        static Font _font;

        /// <summary>
        /// 한국어가 되는 동적 폰트. Unity 기본 폰트는 한글 글리프가 없어 OS 폰트를 쓴다.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = UnityEngine.Font.CreateDynamicFontFromOSFont(
                    new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI" }, 14);
                if (_font == null)
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        /// <summary>수치 표기. index.html fmt() - 1.2k / 3.4M.</summary>
        public static string Fmt(double n)
        {
            if (n >= 1e6) return (n / 1e6).ToString("F1") + "M";
            if (n >= 1e3) return (n / 1e3).ToString("F1") + "k";
            return Mathf.FloorToInt((float)n).ToString();
        }

        // ── 조립 ──

        public static GameObject Panel(Transform parent, string name, Vector2 size,
                                       Vector2 anchor, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            var img = go.GetComponent<Image>();
            if (Theme != null && Theme.frameCard != null)
            {
                // 9슬라이스 프레임. 테두리는 이미지에 있으므로 코드 테두리를 만들지 않는다.
                img.sprite = Theme.frameCard;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                return go;
            }

            img.color = PanelBg;

            // 테두리 1px - 이미지가 나오기 전까지 카드 윤곽을 잡아준다
            var line = new GameObject("Line", typeof(RectTransform), typeof(Image), typeof(Outline));
            line.transform.SetParent(go.transform, false);
            var lrt = (RectTransform)line.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            line.GetComponent<Image>().color = Color.clear;
            var outline = line.GetComponent<Outline>();
            outline.effectColor = PanelLine;
            outline.effectDistance = new Vector2(1f, -1f);

            return go;
        }

        public static Text Label(Transform parent, string text, int size, Color color,
                                 TextAnchor align = TextAnchor.MiddleLeft)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var t = go.GetComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.color = color;
            t.text = text;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;   // 글자가 버튼 클릭을 가로채지 않게
            return t;
        }

        public static Button Button(Transform parent, string caption, int fontSize,
                                    Vector2 size, System.Action onClick)
        {
            var go = new GameObject("Btn_" + caption, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = size;

            var img = go.GetComponent<Image>();
            if (Theme != null && Theme.frameButton != null)
            {
                img.sprite = Theme.frameButton;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else img.color = BtnBg;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var label = Label(go.transform, caption, fontSize, TextMain, TextAnchor.MiddleCenter);
            Stretch((RectTransform)label.transform);
            return btn;
        }

        /// <summary>
        /// 버튼 활성(선택) 표시. 테마가 있으면 켜짐 프레임으로 스왑하고,
        /// 없으면 색으로 표현한다. 호출부가 테마 유무를 몰라도 되게 한 지점.
        /// </summary>
        public static void SetButtonOn(Button btn, bool on)
        {
            var img = btn.GetComponent<Image>();
            if (img == null) return;

            if (Theme != null && Theme.frameButton != null && Theme.frameButtonOn != null)
            {
                img.sprite = on ? Theme.frameButtonOn : Theme.frameButton;
                img.color = Color.white;
            }
            else img.color = on ? BtnBgOn : BtnBg;
        }

        /// <summary>부모를 꽉 채운다.</summary>
        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 가로줄 컨테이너 - 자식을 왼쪽부터 나란히.
        ///
        /// childControl은 반드시 켠다. 꺼두면 레이아웃 그룹이 LayoutElement의
        /// 크기 지정(Fixed)을 무시하고 RectTransform 기본값(100x100)을 그대로 쓴다 -
        /// 실제로 배지의 10px 점이 100px 녹색 사각형으로 화면을 덮었다.
        /// </summary>
        public static RectTransform HRow(Transform parent, float height, float spacing = 6f)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);

            var lg = go.GetComponent<HorizontalLayoutGroup>();
            lg.spacing = spacing;
            lg.childAlignment = TextAnchor.MiddleLeft;
            lg.childControlWidth = true;
            lg.childControlHeight = true;
            lg.childForceExpandWidth = false;
            lg.childForceExpandHeight = false;

            // 줄 자신도 부모 목록의 제어를 받으므로 높이를 LayoutElement로 준다
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(0f, height);
            return rt;
        }

        /// <summary>세로 목록 컨테이너.</summary>
        public static RectTransform VList(Transform parent, float spacing = 4f,
                                          RectOffset padding = null)
        {
            var go = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);

            var lg = go.GetComponent<VerticalLayoutGroup>();
            lg.spacing = spacing;
            lg.padding = padding ?? new RectOffset(10, 10, 8, 8);
            lg.childAlignment = TextAnchor.UpperLeft;
            lg.childControlWidth = true;
            lg.childControlHeight = true;   // HRow와 같은 이유 - Fixed가 동작하려면 켜야 한다
            lg.childForceExpandWidth = true;
            lg.childForceExpandHeight = false;

            return (RectTransform)go.transform;
        }

        /// <summary>
        /// 스크롤되는 세로 목록. 반환값은 내용 컨테이너 - 여기에 줄을 붙인다.
        ///
        /// 목록이 영역보다 길어지면 마스크로 잘리기만 해서는 아래 요소를 볼 방법이
        /// 없다 - 도감에서 필드를 해금하자마자 그렇게 됐다. 휠/드래그 둘 다 된다.
        /// </summary>
        public static RectTransform ScrollList(Transform parent, float height, float spacing = 3f)
        {
            var view = new GameObject("Scroll", typeof(RectTransform), typeof(Image),
                                      typeof(RectMask2D), typeof(ScrollRect));
            view.transform.SetParent(parent, false);
            Fixed(view, 0f, height);

            // 투명하지만 레이캐스트는 받는다 - 휠 이벤트가 닿으려면 Graphic이 필요하다
            var img = view.GetComponent<Image>();
            img.color = Color.clear;

            var content = VList(view.transform, spacing, new RectOffset(0, 0, 0, 0));
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            // 내용 높이는 목록이 계산한 선호 크기를 따른다 - 이게 있어야 스크롤 범위가 생긴다
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = view.GetComponent<ScrollRect>();
            sr.content = content;
            sr.viewport = (RectTransform)view.transform;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 24f;

            return content;
        }

        /// <summary>고정 크기 요소 (레이아웃 그룹 안에서).</summary>
        public static LayoutElement Fixed(GameObject go, float w, float h)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (w > 0) { le.minWidth = w; le.preferredWidth = w; }
            if (h > 0) { le.minHeight = h; le.preferredHeight = h; }
            return le;
        }

        /// <summary>크리처 스프라이트 아이콘 (Point 필터 도트 그대로).</summary>
        public static Image SpriteIcon(Transform parent, Sprite sprite, float size)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(size, size);

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            if (sprite == null) img.color = new Color(1f, 1f, 1f, 0.08f);
            return img;
        }
    }
}
