using System.Collections.Generic;
using UnityEngine;
using Deskmon.Core;

namespace Deskmon.Capture
{
    /// <summary>
    /// 각인 UI를 손으로 확인하기 위한 하네스. 본 게임에는 들어가지 않는다.
    ///
    /// 각인은 인식률과 연출이 전부인 기능이라 코드만 봐서는 판단할 수 없다.
    ///
    /// 이전/다음 버튼이 있는 이유: 정상 흐름에서는 앞 문양을 맞춰야 다음으로 넘어가는데,
    /// 나선이나 번개처럼 어려운 문양에서 막히면 뒤쪽 문양을 볼 수가 없다. 검증이 목적일 때는
    /// 판정과 무관하게 8종을 다 그려봐야 하므로 순회 수단을 따로 둔다.
    /// </summary>
    public class SigilTestHarness : MonoBehaviour
    {
        public DeskmonDatabase db;
        public SigilCapture capture;

        [Header("현황 (읽기 전용)")]
        [SerializeField] int _captured;
        [SerializeField] int _failed;
        [SerializeField] string _lastResult = "-";

        /// <summary>문양 8종 전수 확인 모드. 희귀도 추첨 대신 전부를 순서대로 넣는다.</summary>
        bool _allGlyphMode = true;

        Rarity _rarity = Rarity.Epic;
        GUIStyle _style, _box, _btn;

        /// <summary>패널이 차지하는 화면 영역. 여기 클릭은 각인 획으로 새지 않게 막는다.</summary>
        Rect _blocked;

        const int PANEL_X = 12, PANEL_Y = 12, PANEL_W = 350, PANEL_H = 256;

        /// <summary>
        /// 인식기가 아는 문양 전부. 전수 모드에서 쓴다.
        ///
        /// 목록을 여기 적어두지 않고 인식기에서 가져온다 - 문양을 추가했을 때
        /// 하네스만 옛 목록을 들고 있으면 새 문양을 확인할 방법이 없어진다.
        /// </summary>
        static string[] AllGlyphs
        {
            get
            {
                var list = new List<string>(SigilRecognizer.Names);
                return list.ToArray();
            }
        }

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

        /// <summary>
        /// 패널 영역을 입력 차단 목록에 등록한다.
        ///
        /// GUI는 좌상단 원점이고 커서 좌표는 좌하단 원점이라 Y를 뒤집어야 한다.
        /// 해상도가 바뀔 수 있으므로 매 프레임 다시 계산한다.
        /// </summary>
        void UpdateBlockedRect()
        {
            var next = new Rect(PANEL_X, Screen.height - PANEL_Y - PANEL_H, PANEL_W, PANEL_H);
            if (next == _blocked) return;

            if (_blocked.width > 0f) SigilInput.UnblockArea(_blocked);
            _blocked = next;
            SigilInput.BlockArea(_blocked);
        }

        void OnDisable()
        {
            if (_blocked.width > 0f) SigilInput.UnblockArea(_blocked);
            _blocked = default;
        }

        void Update()
        {
            UpdateBlockedRect();

            if (Input.GetKeyDown(KeyCode.Alpha1)) SetRarity(Rarity.Common);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetRarity(Rarity.Rare);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetRarity(Rarity.Epic);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetRarity(Rarity.Legendary);
            if (Input.GetKeyDown(KeyCode.R)) Restart();
            if (Input.GetKeyDown(KeyCode.B)) capture.ApplyBait();
            if (Input.GetKeyDown(KeyCode.A)) ToggleAllGlyphMode();

            // 좌우 방향키로도 순회한다 - 그리다가 마우스를 떼지 않고 넘길 수 있게.
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Step(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) Step(1);
        }

        /// <summary>판정과 무관하게 문양을 넘긴다.</summary>
        void Step(int delta)
        {
            capture.DebugStepGlyph(delta);
            _lastResult = delta > 0 ? "다음 문양 (건너뜀)" : "이전 문양";
        }

        void ToggleAllGlyphMode()
        {
            _allGlyphMode = !_allGlyphMode;
            Restart();
        }

        void SetRarity(Rarity r)
        {
            _rarity = r;
            _allGlyphMode = false;   // 희귀도를 고르면 추첨 모드로 돌아간다
            Restart();
        }

        /// <summary>새 각인을 시작한다.</summary>
        void Restart()
        {
            if (capture == null) return;

            // 희귀도만 바꾼 임시 종을 만들어 넘긴다 - 에셋을 건드리지 않기 위해서다.
            var probe = ScriptableObject.CreateInstance<SpeciesData>();
            probe.id = "test";
            probe.displayName = "테스트";
            probe.rarity = _rarity;
            probe.forms = 1;

            capture.Begin(probe);

            // 전수 모드에서는 추첨 결과를 8종 전부로 갈아끼운다.
            if (_allGlyphMode) capture.DebugSetGlyphs(AllGlyphs);

            capture.Engage();   // 바로 각인 상태로 - 클릭 없이 그리기부터 확인
        }

        void OnGUI()
        {
            EnsureStyles();

            // 차단 영역과 같은 값을 써야 한다 - 어긋나면 버튼 옆을 눌렀을 때 획이 그어진다.
            GUI.Box(new Rect(PANEL_X, PANEL_Y, PANEL_W, PANEL_H), GUIContent.none, _box);
            GUILayout.BeginArea(new Rect(PANEL_X + 12, PANEL_Y + 10, PANEL_W - 24, PANEL_H - 20));

            GUILayout.Label("<b>각인 UI 테스트</b>", _style);
            GUILayout.Space(4);

            Row("모드", _allGlyphMode
                ? "<color=#9ff0a8>문양 8종 전수</color>"
                : $"희귀도 추첨 ({_rarity})");

            Row("문양", capture != null
                ? $"{capture.Index + 1} / {capture.TotalGlyphs}   <b>{Label(capture.CurrentGlyph)}</b>"
                : "-");

            Row("포획 / 실패", $"{_captured} / {_failed}");
            Row("최근", _lastResult);

            // 실패 원인 진단. 목표 점수는 높은데 분류가 다른 문양으로 갔다면 임계값을
            // 낮춰도 소용없다 - 판정이 분류 일치를 요구하기 때문이다.
            if (capture != null && capture.LastPointCount > 0)
            {
                bool classMismatch = capture.LastRecognized != null
                                     && capture.LastRecognized != capture.CurrentGlyph;

                string diag = $"{Label(capture.LastRecognized)} {capture.LastScore:F2}"
                            + $" · 목표 {capture.LastTargetScore:F2}"
                            + $" · {capture.LastPointCount}점";

                Row("판정", classMismatch ? $"<color=#ff8f8f>{diag}</color>" : diag);
            }

            GUILayout.Space(6);

            // ── 이전 / 다음 ──
            // 판정을 거치지 않으므로 실패해도 계속 넘어간다.
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("< 이전", _btn, GUILayout.Height(28))) Step(-1);
            if (GUILayout.Button("다음 >", _btn, GUILayout.Height(28))) Step(1);
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_allGlyphMode ? "추첨 모드" : "8종 전수", _btn, GUILayout.Height(24)))
                ToggleAllGlyphMode();
            if (GUILayout.Button("새 각인", _btn, GUILayout.Height(24))) Restart();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("<size=11>점선 문양을 마우스로 따라 그리세요. 판정에 실패해도\n" +
                            "이전/다음(또는 방향키)으로 넘겨 전부 확인할 수 있습니다.\n" +
                            "A 전수/추첨 · 1~4 희귀도 · B 미끼 · R 새 각인</size>", _style);

            GUILayout.EndArea();
        }

        void EnsureStyles()
        {
            if (_style != null) return;

            _style = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };

            _box = new GUIStyle(GUI.skin.box);
            var bg = new Texture2D(1, 1);
            bg.SetPixel(0, 0, new Color(0.05f, 0.07f, 0.06f, 0.85f));
            bg.Apply();
            _box.normal.background = bg;

            _btn = new GUIStyle(GUI.skin.button) { fontSize = 12, richText = true };
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
