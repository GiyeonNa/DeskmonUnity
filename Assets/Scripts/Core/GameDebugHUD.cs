using UnityEngine;
using Deskmon.Native;

namespace Deskmon.Core
{
    /// <summary>
    /// 본 게임 실행 확인용 HUD. 배포 빌드에서는 꺼둔다 (기획 S5의 개발자 테스트 패널로 대체).
    ///
    /// 왜 필요한가: 출몰 간격이 기본 120~240초라 그냥 켜두고 기다리면 아무 일도 안 일어난다.
    /// 코어 루프(출몰 -> 각인 -> 도감 -> 저장)가 실제로 도는지 확인하려면 즉시 출몰이 필요하다.
    /// 그리고 이 앱은 투명 오버레이라 화면에 아무것도 안 보이는 게 정상이라, 상태를 눈으로
    /// 볼 수단이 없으면 "안 도는 것"과 "도는데 안 보이는 것"을 구분할 수 없다.
    /// </summary>
    public class GameDebugHUD : MonoBehaviour
    {
        [Tooltip("F1로 토글. 배포 시 false로 두고 시작한다.")]
        public bool show = true;

        public GameState game;
        public SpawnScheduler scheduler;

        GUIStyle _style, _box;
        string _lastEvent = "-";
        float _eventT;

        void Start()
        {
            if (game == null) game = FindFirstObjectByType<GameState>();
            if (scheduler == null) scheduler = FindFirstObjectByType<SpawnScheduler>();

            if (game != null)
                game.OnCaught += r =>
                {
                    _lastEvent = $"포획: {r.species.displayName}"
                               + (r.shiny ? " 샤이니" : "")
                               + (r.firstCatch ? " (최초)" : "")
                               + (r.berryGained > 0 ? $" +{r.berryGained}" : "");
                    _eventT = 0f;
                };
        }

        void Update()
        {
            _eventT += Time.unscaledDeltaTime;

            if (Input.GetKeyDown(KeyCode.F1)) show = !show;

            // 즉시 출몰. 기다리지 않고 코어 루프를 확인하는 수단이다.
            if (Input.GetKeyDown(KeyCode.F2) && scheduler != null && !scheduler.SpawnActive)
            {
                scheduler.Trigger(false);
                _lastEvent = "강제 출몰 (F2)";
                _eventT = 0f;
            }

            // 저장 위치를 여는 것까지 넣어두면 세이브가 실제로 쓰이는지 바로 볼 수 있다.
            if (Input.GetKeyDown(KeyCode.F3))
            {
                Application.OpenURL("file://" + Application.persistentDataPath);
                _lastEvent = "세이브 폴더 열기 (F3)";
                _eventT = 0f;
            }
        }

        void OnGUI()
        {
            if (!show) return;
            EnsureStyles();

            const int W = 330, H = 210;
            GUI.Box(new Rect(12, 12, W, H), GUIContent.none, _box);
            GUILayout.BeginArea(new Rect(24, 22, W - 24, H - 20));

            GUILayout.Label("<b>데스크몬 (개발용 HUD)</b>", _style);
            GUILayout.Space(4);

            Row("네이티브", WindowController.IsApplied
                ? "<color=#8fe08f>투명 적용됨</color>"
                : "<color=#ffcf6b>에디터 (빌드에서 확인)</color>");

            Row("클릭통과", WindowController.IsClickThrough
                ? "<color=#8fd6ff>ON</color>" : "<color=#ffd35c>OFF</color>");

            if (game?.Save != null)
            {
                Row("베리", Mathf.FloorToInt((float)game.Save.berry).ToString());
                Row("도감", $"{CaughtCount()}종 보유");
            }
            else Row("세이브", "<color=#ff8f8f>로드 안 됨</color>");

            if (scheduler != null)
            {
                Row("출몰", scheduler.SpawnActive
                    ? "<color=#9ff0a8>진행 중</color>"
                    : $"{Mathf.Max(0f, scheduler.SpawnIn):F0}초 후");
            }

            Row("유휴", $"{IdleTime.Seconds():F0}s ({(IdleTime.IsWorking() ? "작업중" : "휴식")})");

            // 최근 이벤트는 잠깐만 강조한다 - 계속 떠 있으면 지금 일어난 일인지 알 수 없다.
            if (_eventT < 6f)
                Row("최근", $"<color=#ffe9a3>{_lastEvent}</color>");

            GUILayout.Space(4);
            GUILayout.Label("<size=11>F1 HUD · <b>F2 즉시 출몰</b> · F3 세이브 폴더\n" +
                            "<b>종료: Ctrl+Alt+Q</b> (전역)</size>", _style);

            GUILayout.EndArea();
        }

        int CaughtCount()
        {
            if (game?.Save == null) return 0;
            int n = 0;
            foreach (var d in game.Save.dex) if (d.caught > 0) n++;
            return n;
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
        }

        void Row(string k, string v)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color=#9fb0a4>{k}</color>", _style, GUILayout.Width(78));
            GUILayout.Label(v, _style);
            GUILayout.EndHorizontal();
        }
    }
}
