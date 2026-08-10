using UnityEngine;
using UnityEngine.UI;
using Deskmon.Core;

namespace Deskmon.UI
{
    /// <summary>
    /// 설정. index.html 설정 패널의 최소 이식 - 출몰 빈도만.
    /// 야간 모드는 배경 그라디언트가 붙는 S4에서 의미가 생기므로 그때 넣는다.
    /// </summary>
    public class SettingsPanel
    {
        public GameObject Root { get; }

        readonly UIRoot _ui;
        readonly RectTransform _list;

        static readonly (string key, string label)[] Rates =
        {
            ("fast", "자주"), ("normal", "보통"), ("slow", "가끔"), ("off", "끔"),
        };

        public SettingsPanel(Transform parent, UIRoot ui)
        {
            _ui = ui;
            Root = UIKit.Panel(parent, "SettingsPanel", new Vector2(320f, 200f),
                               new Vector2(1f, 0f), Vector2.zero);

            var v = UIKit.VList(Root.transform, 6f);
            UIKit.Stretch(v);

            var header = UIKit.Label(v, "설정", 15, UIKit.TextMain);
            header.fontStyle = FontStyle.Bold;
            UIKit.Fixed(header.gameObject, 0f, 20f);

            UIKit.Fixed(UIKit.Label(v, "야생 출몰 빈도", 12, UIKit.TextSub).gameObject, 0f, 16f);
            _list = UIKit.HRow(v, 28f);
            UIKit.Fixed(_list.gameObject, 0f, 28f);

            UIKit.Fixed(UIKit.Label(v,
                "끄면 야생이 나오지 않습니다. 방목과 생산은 계속됩니다.",
                11, UIKit.TextSub).gameObject, 0f, 30f);

            // ── 종료 ──
            // 이 앱은 Alt+Tab에도 작업표시줄에도 안 뜬다. 전역 핫키(Ctrl+Alt+Q)는
            // 개발자용 안전장치이지 일반 사용자가 알 수 있는 것이 아니므로,
            // 눈에 보이는 종료 수단은 이 버튼이 유일하다. 배포 필수 요소다.
            var quitRow = UIKit.HRow(v, 30f);
            UIKit.Fixed(quitRow.gameObject, 0f, 30f);

            var quit = UIKit.Button(quitRow, "데스크몬 종료", 12, new Vector2(120f, 28f), Quit);
            quit.GetComponent<UnityEngine.UI.Image>().color = new Color(1f, 0.72f, 0.72f, 1f);
            UIKit.Fixed(quit.gameObject, 120f, 28f);

            UIKit.Fixed(UIKit.Label(v,
                "저장 후 종료합니다. 다시 켤 때까지 몬스터도 쉽니다.",
                11, UIKit.TextSub).gameObject, 0f, 16f);
        }

        static void Quit()
        {
            // 저장은 GameState.OnApplicationQuit이 담당한다 - 여기서 또 하면 이중 저장.
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void Refresh()
        {
            foreach (Transform child in _list) Object.Destroy(child.gameObject);

            var game = _ui.Game;
            if (game?.Save == null) return;
            string current = game.Save.settings.spawnRate;

            foreach (var (key, label) in Rates)
            {
                string captured = key;
                var b = UIKit.Button(_list, label, 12, new Vector2(64f, 26f), () =>
                {
                    game.Save.settings.spawnRate = captured;
                    SaveSystem.Save(game.Save);
                    Refresh();
                });
                if (current == key) UIKit.SetButtonOn(b, true);
                UIKit.Fixed(b.gameObject, 64f, 26f);
            }
        }
    }
}
