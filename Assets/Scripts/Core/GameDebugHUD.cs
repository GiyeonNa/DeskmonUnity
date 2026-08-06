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
        public RoamManager roam;

        GUIStyle _style, _box;
        string _lastEvent = "-";
        float _eventT;

        /// <summary>내용 실측 높이. 첫 프레임은 대략값으로 시작해 다음 프레임부터 맞는다.</summary>
        float _measuredHeight = 250f;

        void Start()
        {
            if (game == null) game = FindFirstObjectByType<GameState>();
            if (scheduler == null) scheduler = FindFirstObjectByType<SpawnScheduler>();
            if (roam == null) roam = FindFirstObjectByType<RoamManager>();

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

            if (GlobalKey.Down(KeyCode.F1)) show = !show;

            // 즉시 출몰. 기다리지 않고 코어 루프를 확인하는 수단이다.
            if (GlobalKey.Down(KeyCode.F2))
            {
                _eventT = 0f;

                // 눌렀는데 아무 일도 안 일어나면 원인을 알 수 없다. 왜 안 됐는지 남긴다.
                if (scheduler == null) _lastEvent = "출몰 실패: SpawnScheduler 없음 (Main 씬인가?)";
                else if (scheduler.SpawnActive) _lastEvent = "이미 출몰 중";
                else if (game?.Save == null) _lastEvent = "출몰 실패: 세이브 로드 안 됨";
                else
                {
                    scheduler.Trigger(false);
                    _lastEvent = scheduler.SpawnActive ? "강제 출몰 (F2)"
                        : "출몰 실패: 조건에 맞는 종이 없음 (해금/시간 게이트 확인)";
                }
            }

            // 저장 위치를 여는 것까지 넣어두면 세이브가 실제로 쓰이는지 바로 볼 수 있다.
            if (GlobalKey.Down(KeyCode.F3))
            {
                Application.OpenURL("file://" + Application.persistentDataPath);
                _lastEvent = "세이브 폴더 열기 (F3)";
                _eventT = 0f;
            }

            // ── S3 검증 키 - 가방 UI가 나오기 전까지의 임시 조작 수단 ──

            // F4: 첫 보유 폼 방목 토글
            if (GlobalKey.Down(KeyCode.F4)) { _eventT = 0f; _lastEvent = ToggleFirstRoam(); }

            // F5: 첫 방목 개체에게 간식 (베리 3 소모, 친밀 +6, 만복 기록)
            if (GlobalKey.Down(KeyCode.F5)) { _eventT = 0f; _lastEvent = SnackFirstRoamer(); }

            // F6: 일괄 진화
            if (GlobalKey.Down(KeyCode.F6)) { _eventT = 0f; _lastEvent = EvolveAll(); }
        }

        string ToggleFirstRoam()
        {
            if (game?.Save == null || game.db == null) return "방목 실패: 세이브 없음";

            // 보유한 첫 폼을 찾는다 - 이미 방목 중이면 회수가 되니 토글 확인에도 쓴다
            foreach (var sp in game.db.species)
            {
                if (sp == null) continue;
                var c = game.Save.Creature(sp.id);
                for (int st = 0; st < sp.forms; st++)
                {
                    for (int track = 0; track < 2; track++)
                    {
                        bool shiny = track == 1;
                        if ((shiny ? c.shiny[st] : c.s[st]) < 1) continue;

                        var r = RoamSystem.Toggle(game.Save, game.db.balance, sp.id, st, shiny);
                        SaveSystem.Save(game.Save);
                        if (roam != null) roam.Refresh();

                        switch (r)
                        {
                            case RoamSystem.ToggleResult.Added:   return $"방목: {sp.NameAt(st)}";
                            case RoamSystem.ToggleResult.Removed: return $"회수: {sp.NameAt(st)}";
                            case RoamSystem.ToggleResult.Full:    return "방목 슬롯 가득";
                            default:                              return "방목 실패";
                        }
                    }
                }
            }
            return "방목 실패: 보유 개체 없음 (먼저 잡으세요)";
        }

        string SnackFirstRoamer()
        {
            if (game?.Save == null || game.db?.balance == null) return "간식 실패: 세이브 없음";

            var target = roam != null ? roam.First : null;
            if (target == null) return "간식 실패: 방목 개체 없음 (F4)";

            var r = FriendshipSystem.Snack(game.Save, game.db.balance, target.speciesId, target.stage);
            if (r.notEnoughBerry) return $"간식 실패: 베리 부족 (필요 {game.db.balance.snackCost})";
            if (!r.ok) return "간식 실패";

            SaveSystem.Save(game.Save);
            return $"간식 - 친밀 Lv{r.newLevel}" + (r.leveledUp ? " (레벨 업)" : "");
        }

        string EvolveAll()
        {
            if (game?.Save == null || game.db == null) return "진화 실패: 세이브 없음";

            var w = WorldConditions.Now(IdleTime.IsWorking(game.db.balance.workingIdleSec));
            int n = EvolutionSystem.EvolveAll(game.Save, game.db, w);

            if (n > 0)
            {
                SaveSystem.Save(game.Save);
                if (roam != null) roam.Refresh();   // 진화로 방목 폼이 사라졌을 수 있다
                return $"진화 {n}회";
            }

            // 왜 안 되는지 첫 이유를 보여준다 - "안 됨"만으로는 친밀도 부족인지 알 수 없다
            bool anyFinal = false;
            foreach (var sp in game.db.species)
            {
                if (sp == null) continue;

                var c = game.Save.Creature(sp.id);
                if (c.s[sp.forms - 1] > 0 || c.shiny[sp.forms - 1] > 0) anyFinal = true;

                for (int st = 0; st < sp.forms - 1; st++)
                {
                    var reason = EvolutionSystem.Check(game.Save, game.db, sp.id, st, false, w);
                    if (reason != EvolutionSystem.BlockReason.NoCreature
                        && reason != EvolutionSystem.BlockReason.FinalStage)
                        return $"진화 불가: {sp.NameAt(st)} - {ReasonText(reason, st)}";
                }
            }

            // "대상 없음"만으로는 이미 다 진화한 것인지 아무것도 없는 것인지 알 수 없다
            return anyFinal ? "진화 대상 없음 - 보유 개체가 전부 최종형" : "진화 대상 없음";
        }

        string ReasonText(EvolutionSystem.BlockReason r, int stage)
        {
            switch (r)
            {
                case EvolutionSystem.BlockReason.Friendship:
                    return $"친밀 Lv{game.db.balance.EvolveLevelFor(stage)} 필요 (쓰다듬기/간식)";
                case EvolutionSystem.BlockReason.NeedNight: return "밤(18~06시)에만 진화";
                case EvolutionSystem.BlockReason.NeedFed:   return "간식을 먹여야 진화 (F5)";
                default: return r.ToString();
            }
        }

        void OnGUI()
        {
            if (!show) return;
            EnsureStyles();

            // 높이를 고정하지 않는다. 줄이 조건부(숨김 경고/방목/최근)라 고정값은
            // 반드시 어긋난다 - 실제로 방목 줄이 생기면서 아래가 잘렸다.
            // 내용을 그리면서 실측하고, 배경은 지난 프레임 측정값으로 그린다
            // (IMGUI는 그린 순서대로 쌓여서 배경을 나중에 그리면 글자를 덮는다).
            // 한 프레임 늦는 것은 개발용 HUD에서 문제가 되지 않는다.
            const int W = 330;
            GUI.Box(new Rect(12, 12, W, _measuredHeight + 20f), GUIContent.none, _box);
            GUILayout.BeginArea(new Rect(24, 22, W - 24, 2000f));
            GUILayout.BeginVertical();

            GUILayout.Label("<b>데스크몬 (개발용 HUD)</b>", _style);
            GUILayout.Space(4);

            Row("네이티브", WindowController.IsApplied
                ? "<color=#8fe08f>투명 적용됨</color>"
                : "<color=#ffcf6b>에디터 (빌드에서 확인)</color>");

            Row("클릭통과", WindowController.IsClickThrough
                ? "<color=#8fd6ff>ON</color>" : "<color=#ffd35c>OFF</color>");

            // 숨김 상태는 반드시 보여준다. 카메라가 꺼져 크리처가 전멸하는 상태인데
            // HUD(IMGUI)는 카메라 없이도 그려지므로, 이 줄이 없으면 "출몰 중인데
            // 아무것도 없음"으로만 보인다.
            var overlay = DesktopOverlay.Instance;
            if (overlay != null && overlay.HiddenByFullscreen)
                Row("표시", "<color=#ff8f8f>전체화면 감지로 숨김 - 크리처 안 보임</color>");

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

            // 방목 + 첫 개체의 친밀도. 친밀도가 진화 엔진이라 진행이 보여야 한다.
            if (game?.Save != null && game.db?.balance != null)
            {
                string roamText = $"{(roam != null ? roam.Count : 0)} / {RoamSystem.Slots(game.Save, game.db.balance)}";
                var first = roam != null ? roam.First : null;
                if (first != null)
                {
                    int lv = FriendshipSystem.Level(game.Save, game.db.balance, first.speciesId, first.stage);
                    int pts = FriendshipSystem.Points(game.Save, first.speciesId, first.stage);
                    roamText += $"   친밀 Lv{lv} ({pts}pt)";
                }
                Row("방목", roamText);
            }

            // 최근 이벤트는 잠깐만 강조한다 - 계속 떠 있으면 지금 일어난 일인지 알 수 없다.
            if (_eventT < 6f)
                Row("최근", $"<color=#ffe9a3>{_lastEvent}</color>");

            GUILayout.Space(4);
            GUILayout.Label("<size=11>F1 HUD · <b>F2 출몰</b> · F3 세이브 · <b>F4 방목</b> · F5 간식 · F6 진화\n" +
                            "방목 개체 클릭 = 쓰다듬기 · <b>종료: Ctrl+Alt+Q</b> (전역)</size>", _style);

            GUILayout.EndVertical();

            // 실측은 Repaint에서만 유효하다 - Layout 이벤트의 rect는 0이다.
            if (Event.current.type == EventType.Repaint)
                _measuredHeight = GUILayoutUtility.GetLastRect().height;

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
