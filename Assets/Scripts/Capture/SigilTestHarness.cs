using UnityEngine;
using Deskmon.Core;

namespace Deskmon.Capture
{
    /// <summary>
    /// 각인 UI를 손으로 확인하기 위한 하네스. 본 게임에는 들어가지 않는다.
    ///
    /// 각인은 인식률과 연출이 전부인 기능이라 코드만 봐서는 판단할 수 없다.
    /// 희귀도를 바꿔가며 문양 개수·연속 판정·실패 흔들림을 눈으로 확인한다.
    /// </summary>
    public class SigilTestHarness : MonoBehaviour
    {
        public DeskmonDatabase db;
        public SigilCapture capture;

        [Header("현황 (읽기 전용)")]
        [SerializeField] int _captured;
        [SerializeField] int _failed;
        [SerializeField] string _lastResult = "-";

        Rarity _rarity = Rarity.Epic;
        GUIStyle _style, _box;

        void Start()
        {
            if (capture == null) capture = GetComponent<SigilCapture>();
            Restart();

            capture.OnGlyphSuccess += () => _lastResult = "문양 성공";
            capture.OnGlyphFail += () => { _failed++; _lastResult = "실패 - 다시"; };
            capture.OnCaptured += () =>
            {
                _captured++;
                _lastResult = "포획!";
                Invoke(nameof(Restart), 0.8f);
            };
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetRarity(Rarity.Common);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetRarity(Rarity.Rare);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetRarity(Rarity.Epic);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetRarity(Rarity.Legendary);
            if (Input.GetKeyDown(KeyCode.R)) Restart();
            if (Input.GetKeyDown(KeyCode.B)) capture.ApplyBait();
        }

        void SetRarity(Rarity r) { _rarity = r; Restart(); }

        /// <summary>지정 희귀도로 새 각인을 시작한다.</summary>
        void Restart()
        {
            if (db == null || capture == null) return;

            // 희귀도만 바꾼 임시 종을 만들어 넘긴다 - 에셋을 건드리지 않기 위해서다.
            var probe = ScriptableObject.CreateInstance<SpeciesData>();
            probe.id = "test";
            probe.displayName = "테스트";
            probe.rarity = _rarity;
            probe.forms = 1;

            capture.Begin(probe);
            capture.Engage();   // 바로 각인 상태로 - 클릭 없이 그리기부터 확인
        }

        void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
                _box = new GUIStyle(GUI.skin.box);
                var bg = new Texture2D(1, 1);
                bg.SetPixel(0, 0, new Color(0.05f, 0.07f, 0.06f, 0.85f));
                bg.Apply();
                _box.normal.background = bg;
            }

            const int W = 330, H = 168;
            GUI.Box(new Rect(12, 12, W, H), GUIContent.none, _box);
            GUILayout.BeginArea(new Rect(24, 22, W - 24, H - 20));

            GUILayout.Label("<b>각인 UI 테스트</b>", _style);
            GUILayout.Space(4);

            Row("희귀도", $"{_rarity}  (1~4 키로 변경)");
            Row("문양", capture != null
                ? $"{capture.Index + 1} / {capture.TotalGlyphs}  현재: {Label(capture.CurrentGlyph)}"
                : "-");
            Row("포획 / 실패", $"{_captured} / {_failed}");
            Row("최근", _lastResult);

            GUILayout.Space(4);
            GUILayout.Label("<size=11>점선 문양을 마우스로 따라 그리세요.\n" +
                            "R 새 각인 · B 미끼(문양 -1) · 1~4 희귀도</size>", _style);

            GUILayout.EndArea();
        }

        static string Label(string glyph)
            => string.IsNullOrEmpty(glyph) ? "-" : SigilRecognizer.Label(glyph);

        void Row(string k, string v)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color=#9fb0a4>{k}</color>", _style, GUILayout.Width(84));
            GUILayout.Label(v, _style);
            GUILayout.EndHorizontal();
        }
    }
}
