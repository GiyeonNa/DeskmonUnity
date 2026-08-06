using UnityEngine;
using UnityEngine.UI;
using Deskmon.Core;
using Deskmon.Native;

namespace Deskmon.UI
{
    /// <summary>
    /// 가방 - 보유 폼 목록과 조작. index.html renderPanel()의 bag 모드 이식.
    ///
    /// 폼 하나가 한 줄이다: 아이콘 · 이름 xN · 친밀 Lv · [방목] [간식] [진화] [방생]
    /// 진화가 막혀 있으면 버튼 대신 이유를 보여준다 - "안 눌리는 버튼"만큼
    /// 답답한 것이 없고, 원본도 토스트로 이유를 알려줬다.
    ///
    /// Refresh는 전부 지우고 다시 만든다. 보유 폼이 많아야 수십 줄이라
    /// 차등 갱신의 복잡함이 이득보다 크다.
    /// </summary>
    public class BagPanel
    {
        public GameObject Root { get; }

        readonly UIRoot _ui;
        readonly RectTransform _list;
        readonly Text _header;

        public BagPanel(Transform parent, UIRoot ui)
        {
            _ui = ui;
            Root = UIKit.Panel(parent, "BagPanel", new Vector2(320f, 380f),
                               new Vector2(1f, 0f), Vector2.zero);

            var v = UIKit.VList(Root.transform, 4f);
            UIKit.Stretch(v);

            _header = UIKit.Label(v, "가방", 15, UIKit.TextMain);
            _header.fontStyle = FontStyle.Bold;
            UIKit.Fixed(_header.gameObject, 0f, 20f);

            // 가방도 폼이 쌓이면 길어진다 - 도감과 같은 스크롤 목록을 쓴다
            _list = UIKit.ScrollList(v, 330f);
        }

        public void Refresh()
        {
            foreach (Transform child in _list) Object.Destroy(child.gameObject);

            var game = _ui.Game;
            if (game?.Save == null || game.db == null) return;

            var save = game.Save;
            var db = game.db;

            _header.text = $"가방 · 방목 {save.roam.Count}/{RoamSystem.Slots(save, db.balance)}";

            var w = WorldConditions.Now(IdleTime.IsWorking(db.balance.workingIdleSec));
            bool any = false;

            foreach (var sp in db.species)
            {
                if (sp == null) continue;
                var c = save.Creature(sp.id);

                for (int st = 0; st < sp.forms; st++)
                {
                    for (int track = 0; track < 2; track++)
                    {
                        bool shiny = track == 1;
                        int count = shiny ? c.shiny[st] : c.s[st];
                        if (count < 1) continue;

                        any = true;
                        BuildRow(sp, st, shiny, count, save, db, w);
                    }
                }
            }

            if (!any)
                UIKit.Label(_list, "아직 잡은 몬스터가 없습니다.", 13, UIKit.TextSub);
        }

        void BuildRow(SpeciesData sp, int stage, bool shiny, int count,
                      SaveData save, DeskmonDatabase db, in WorldConditions w)
        {
            var row = UIKit.HRow(_list, 40f, 4f);
            UIKit.Fixed(row.gameObject, 0f, 40f);

            var icon = UIKit.SpriteIcon(row, sp.SpriteAt(stage), 32f);
            UIKit.Fixed(icon.gameObject, 32f, 32f);

            // 샤이니 표시 - 아이콘이 있으면 반짝 별을 크리처 아이콘 모서리에 겹친다.
            // 텍스트 "S"보다 자리도 안 먹고 도감의 금색 칸과 같은 문법이 된다.
            bool sparkleIcon = shiny && UIKit.Theme != null && UIKit.Theme.iconSparkle != null;
            if (sparkleIcon)
            {
                var spark = UIKit.SpriteIcon(icon.transform, UIKit.Theme.iconSparkle, 12f);
                var srt = (RectTransform)spark.transform;
                srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(1f, 1f);
                srt.anchoredPosition = Vector2.zero;
            }

            // 이름 + 수량 + 친밀. 샤이니는 금색으로 구분한다.
            int lv = FriendshipSystem.Level(save, db.balance, sp.id, stage);
            var name = UIKit.Label(row,
                $"{sp.NameAt(stage)}{(shiny && !sparkleIcon ? " S" : "")} x{count}\n친밀 Lv{lv}",
                11, shiny ? UIKit.TextGold : UIKit.TextMain);
            UIKit.Fixed(name.gameObject, 86f, 40f);

            // 방목 토글
            bool roaming = RoamSystem.IsRoaming(save, sp.id, stage, shiny);
            var roamBtn = UIKit.Button(row, roaming ? "회수" : "방목", 11,
                new Vector2(40f, 34f), () =>
                {
                    RoamSystem.Toggle(save, db.balance, sp.id, stage, shiny);
                    AfterChange();
                });
            if (roaming) UIKit.SetButtonOn(roamBtn, true);
            UIKit.Fixed(roamBtn.gameObject, 40f, 34f);

            // 간식 - 방목 여부와 무관하게 폼에 준다 (원본 가방 패널과 같다)
            var snackBtn = UIKit.Button(row, "간식", 11, new Vector2(40f, 34f), () =>
            {
                var r = FriendshipSystem.Snack(save, db.balance, sp.id, stage);
                if (r.ok) AfterChange();
            });
            if (save.berry < db.balance.snackCost)
                snackBtn.interactable = false;
            UIKit.Fixed(snackBtn.gameObject, 40f, 34f);

            // 진화 - 가능하면 버튼, 막혀 있으면 이유
            var reason = EvolutionSystem.Check(save, db, sp.id, stage, shiny, w);
            if (reason == EvolutionSystem.BlockReason.None)
            {
                var evoBtn = UIKit.Button(row, "진화", 11, new Vector2(40f, 34f), () =>
                {
                    var world = WorldConditions.Now(
                        IdleTime.IsWorking(db.balance.workingIdleSec));
                    EvolutionSystem.TryEvolve(save, db, sp.id, stage, shiny, world);
                    AfterChange();
                });
                UIKit.SetButtonOn(evoBtn, true);
                UIKit.Fixed(evoBtn.gameObject, 40f, 34f);
            }
            else if (reason != EvolutionSystem.BlockReason.FinalStage)
            {
                var why = UIKit.Label(row, ReasonShort(reason, stage, db), 10, UIKit.TextSub,
                                      TextAnchor.MiddleCenter);
                UIKit.Fixed(why.gameObject, 40f, 34f);
            }
            else
            {
                // 최종형은 진화 칸을 비워둔다 - 자리까지 없애면 줄마다 폭이 들쭉난다
                var pad = new GameObject("Pad", typeof(RectTransform));
                pad.transform.SetParent(row, false);
                UIKit.Fixed(pad, 40f, 34f);
            }

            // 방생 (돌려보내기)
            var relBtn = UIKit.Button(row, "방생", 11, new Vector2(40f, 34f), () =>
            {
                EvolutionSystem.ReleaseOne(save, db, sp.id, stage, shiny);
                AfterChange();
            });
            UIKit.Fixed(relBtn.gameObject, 40f, 34f);
        }

        static string ReasonShort(EvolutionSystem.BlockReason r, int stage, DeskmonDatabase db)
        {
            switch (r)
            {
                case EvolutionSystem.BlockReason.Friendship:
                    return $"친밀\nLv{db.balance.EvolveLevelFor(stage)}";
                case EvolutionSystem.BlockReason.NeedNight: return "밤에만";
                case EvolutionSystem.BlockReason.NeedFed:   return "간식\n필요";
                default: return "";
            }
        }

        /// <summary>조작 뒤 공통 처리 - 저장, 방목 동기화, 다시 그리기.</summary>
        void AfterChange()
        {
            var game = _ui.Game;
            if (game?.Save != null) SaveSystem.Save(game.Save);
            if (_ui.Roam != null) _ui.Roam.Refresh();
            Refresh();
        }
    }
}
