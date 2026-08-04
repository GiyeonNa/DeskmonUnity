using UnityEngine;
using Deskmon.Native;
using Deskmon.Creatures;

namespace Deskmon
{
    /// <summary>
    /// S0 스파이크 검증 HUD.
    /// 포팅계획서 §4 S0의 DoD를 화면에서 직접 확인하기 위한 것 — 본 게임에는 들어가지 않는다.
    ///
    /// 확인 항목:
    ///   1. 투명·항상위 창이 적용됐는가        → "네이티브 적용" 줄
    ///   2. 클릭통과가 커서 근접에 따라 토글되는가 → "클릭통과" 줄이 실시간으로 바뀜
    ///   3. 통과 상태에서 뒤쪽 창이 클릭되는가   → 직접 바탕화면 아이콘을 눌러 확인
    ///   4. 해제 상태에서 크리처가 클릭되는가   → 크리처를 누르면 "클릭 수신" 카운터 증가
    /// </summary>
    public class SpikeHUD : MonoBehaviour
    {
        public bool show = true;
        public KeyCode toggleKey = KeyCode.F1;
        public KeyCode quitKey = KeyCode.Escape;

        int _clicks;
        float _fps;
        GUIStyle _style, _boxStyle;

        void Update()
        {
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.1f);

            if (Input.GetKeyDown(toggleKey)) show = !show;
            if (Input.GetKeyDown(quitKey)) Quit();

            // 클릭통과가 해제된 상태에서만 Unity가 마우스를 받는다 — 그 사실 자체가 검증 항목.
            if (Input.GetMouseButtonDown(0)) _clicks++;
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnGUI()
        {
            if (!show) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
                _boxStyle = new GUIStyle(GUI.skin.box);
                var bg = new Texture2D(1, 1);
                bg.SetPixel(0, 0, new Color(0.05f, 0.07f, 0.06f, 0.82f));
                bg.Apply();
                _boxStyle.normal.background = bg;
            }

            const int W = 340, H = 186;
            GUI.Box(new Rect(12, 12, W, H), GUIContent.none, _boxStyle);
            GUILayout.BeginArea(new Rect(24, 22, W - 24, H - 20));

            GUILayout.Label("<b>데스크몬 S0 스파이크</b>", _style);
            GUILayout.Space(4);

            Row("네이티브 적용", WindowController.IsApplied ? "<color=#8fe08f>OK</color>"
                : "<color=#ffcf6b>에디터 (빌드에서 확인)</color>");
            Row("클릭통과", WindowController.IsClickThrough
                ? "<color=#8fd6ff>ON — 뒤쪽 창이 눌립니다</color>"
                : "<color=#ffd35c>OFF — 이 창이 입력을 받습니다</color>");
            Row("커서(창 좌표)", NativeCursor.GetPositionInWindow().ToString("F0"));
            Row("유휴", $"{IdleTime.Seconds():F0}s  ({(IdleTime.IsWorking() ? "작업중" : "휴식")})");
            Row("클릭 수신", _clicks.ToString());
            Row("FPS", _fps.ToString("F0"));

            GUILayout.Space(4);
            // ESC는 창이 포커스를 가진 경우에만 먹는다. 이 창은 대개 포커스를 못 받으므로
            // 실제로 믿을 수 있는 종료 수단은 전역 핫키뿐이다 — 그걸 먼저 안내한다.
            GUILayout.Label("<size=11>크리처에 커서를 대면 클릭통과가 해제됩니다.\n" +
                            "<b>종료: Ctrl+Alt+Q</b> (전역) · F1 HUD 토글</size>", _style);

            GUILayout.EndArea();
        }

        void Row(string k, string v)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color=#9fb0a4>{k}</color>", _style, GUILayout.Width(104));
            GUILayout.Label(v, _style);
            GUILayout.EndHorizontal();
        }
    }
}
