using UnityEngine;
using UnityEngine.UI;
using Deskmon.Core;

namespace Deskmon.UI
{
    /// <summary>
    /// 도감 - 종별 폼 수집 현황 + 필드 해금. index.html renderPanel()의 dex 모드 이식.
    ///
    /// 종 하나가 한 줄: 아이콘 · 이름(미포획이면 ???) · 폼 칸(수집 여부 색) · 샤이니 표시.
    /// 필드 해금 버튼을 여기 두는 이유는 원본과 같다 - 도감이 "다음에 뭘 모을지"를
    /// 보는 화면이므로, 새 필드를 여는 결정도 여기서 내려진다.
    /// </summary>
    public class DexPanel
    {
        public GameObject Root { get; }

        readonly UIRoot _ui;
        readonly RectTransform _list;

        public DexPanel(Transform parent, UIRoot ui)
        {
            _ui = ui;
            Root = UIKit.Panel(parent, "DexPanel", new Vector2(320f, 380f),
                               new Vector2(1f, 0f), Vector2.zero);

            var v = UIKit.VList(Root.transform, 4f);
            UIKit.Stretch(v);

            var header = UIKit.Label(v, "도감", 15, UIKit.TextMain);
            header.fontStyle = FontStyle.Bold;
            UIKit.Fixed(header.gameObject, 0f, 20f);

            _list = UIKit.ScrollList(v, 330f);
        }

        public void Refresh()
        {
            foreach (Transform child in _list) Object.Destroy(child.gameObject);

            var game = _ui.Game;
            if (game?.Save == null || game.db == null) return;
            var save = game.Save;
            var db = game.db;

            // ── 필드별 종 목록 ──
            foreach (var field in db.fields)
            {
                if (field == null) continue;

                bool unlocked = save.FieldUnlocked(SpawnScheduler.FieldId(field.id));
                var (got, total) = CreatureRegistry.DexProgress(save, db, field.id);

                var head = UIKit.Label(_list,
                    $"{field.displayName}  {got}/{total}" + (unlocked ? "" : "  (잠김)"),
                    13, unlocked ? UIKit.Accent : UIKit.TextSub);
                head.fontStyle = FontStyle.Bold;
                UIKit.Fixed(head.gameObject, 0f, 18f);

                if (!unlocked)
                {
                    // 해금 버튼. 베리가 모자라면 비활성 - 원본은 버튼을 두고 클릭 시 거절했지만
                    // 여기서는 살 수 있는지 한눈에 보이는 쪽이 낫다.
                    var row = UIKit.HRow(_list, 26f);
                    UIKit.Fixed(row.gameObject, 0f, 26f);

                    var btn = UIKit.Button(row, $"해금 (베리 {field.unlockCost})", 12,
                        new Vector2(150f, 24f), () => Unlock(field));
                    btn.interactable = save.berry >= field.unlockCost;
                    UIKit.Fixed(btn.gameObject, 150f, 24f);
                    continue;
                }

                foreach (var sp in db.species)
                {
                    if (sp == null || sp.field != field.id) continue;
                    BuildRow(sp, save);
                }
            }

            // special/event 종 (루미·크로노)은 필드에 안 속한다 - 잡은 적 있으면만 보여준다
            foreach (var sp in db.species)
            {
                if (sp == null) continue;
                if (sp.field != Field.Special && sp.field != Field.Event) continue;
                if (save.Dex(sp.id).caught > 0) BuildRow(sp, save);
            }
        }

        void BuildRow(SpeciesData sp, SaveData save)
        {
            var dx = save.Dex(sp.id);
            bool seen = dx.caught > 0;

            var row = UIKit.HRow(_list, 30f, 6f);
            UIKit.Fixed(row.gameObject, 0f, 30f);

            var icon = UIKit.SpriteIcon(row, sp.SpriteAt(0), 26f);
            if (!seen) icon.color = new Color(0f, 0f, 0f, 0.55f);   // 실루엣
            UIKit.Fixed(icon.gameObject, 26f, 26f);

            var name = UIKit.Label(row, seen ? sp.displayName : "???", 12,
                                   seen ? UIKit.TextMain : UIKit.TextSub);
            UIKit.Fixed(name.gameObject, 92f, 26f);

            // 폼 칸 - 수집한 폼은 초록, 샤이니까지 있으면 금색 테두리 대신 금색 칸
            for (int f = 0; f < sp.forms; f++)
            {
                var cell = new GameObject("Form", typeof(RectTransform), typeof(Image));
                cell.transform.SetParent(row, false);
                var img = cell.GetComponent<Image>();
                img.raycastTarget = false;
                img.color = dx.shinyForms[f] ? UIKit.TextGold
                          : dx.forms[f] ? UIKit.Accent
                          : new Color(1f, 1f, 1f, 0.12f);
                UIKit.Fixed(cell, 14f, 14f);
            }

            if (dx.milestone)
            {
                var done = UIKit.Label(row, "완성", 11, UIKit.TextGold);
                UIKit.Fixed(done.gameObject, 30f, 26f);
            }
        }

        void Unlock(FieldData field)
        {
            var game = _ui.Game;
            if (game?.Save == null) return;
            if (game.Save.berry < field.unlockCost) return;

            game.Save.berry -= field.unlockCost;
            game.Save.habitats.Add(SpawnScheduler.FieldId(field.id));

            // 해금은 방목 슬롯(+1)과 출몰 풀에 즉시 반영된다
            SaveSystem.Save(game.Save);
            Refresh();
        }
    }
}
