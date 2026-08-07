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

                // 호수는 진영 표시를 함께 - 어느 팀인지가 이 필드의 정체성이다
                string factionTag = "";
                if (field.id == Field.Lake && !string.IsNullOrEmpty(save.faction))
                    factionTag = save.faction == "dew" ? "  이슬 팀" : "  이끼 팀";

                var head = UIKit.Label(_list,
                    $"{field.displayName}  {got}/{total}{factionTag}" + (unlocked ? "" : "  (잠김)"),
                    13, unlocked ? UIKit.Accent : UIKit.TextSub);
                head.fontStyle = FontStyle.Bold;
                UIKit.Fixed(head.gameObject, 0f, 18f);

                // 호수를 해금했는데 진영을 아직 안 골랐으면 여기서 다시 열 수 있다.
                // (모달을 고르지 않고 지나친 세이브의 유일한 복구 경로다)
                if (unlocked && field.id == Field.Lake && string.IsNullOrEmpty(save.faction))
                {
                    var row = UIKit.HRow(_list, 26f);
                    UIKit.Fixed(row.gameObject, 0f, 26f);
                    var pick = UIKit.Button(row, "진영 선택 (필수)", 12, new Vector2(150f, 24f),
                                            () => _ui.ShowFactionModal());
                    UIKit.Fixed(pick.gameObject, 150f, 24f);
                }

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

            // 폼 칸 - 수집한 폼은 초록, 샤이니까지 있으면 금색
            var theme = UIKit.Theme;
            for (int f = 0; f < sp.forms; f++)
            {
                var cell = new GameObject("Form", typeof(RectTransform), typeof(Image));
                cell.transform.SetParent(row, false);
                var img = cell.GetComponent<Image>();
                img.raycastTarget = false;
                UIKit.Fixed(cell, 14f, 14f);

                Color state = dx.shinyForms[f] ? UIKit.TextGold
                            : dx.forms[f] ? UIKit.Accent
                            : Color.clear;

                if (theme != null && theme.frameCell != null)
                {
                    // 홈 프레임 + 수집 상태를 안쪽 채움으로. 프레임이 빈 칸을 표현하므로
                    // 미수집이어도 칸 자체는 보인다 - "여기 채울 자리가 있다"는 정보다.
                    img.sprite = theme.frameCell;
                    img.color = Color.white;

                    if (state != Color.clear)
                    {
                        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                        fill.transform.SetParent(cell.transform, false);
                        var frt = (RectTransform)fill.transform;
                        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
                        frt.offsetMin = new Vector2(3f, 3f); frt.offsetMax = new Vector2(-3f, -3f);
                        var fimg = fill.GetComponent<Image>();
                        fimg.color = state;
                        fimg.raycastTarget = false;
                    }
                }
                else
                {
                    img.color = state == Color.clear ? new Color(1f, 1f, 1f, 0.12f) : state;
                }
            }

            if (dx.milestone)
            {
                var done = UIKit.Label(row, "완성", 11, UIKit.TextGold);
                UIKit.Fixed(done.gameObject, 30f, 26f);
            }

            // 카드 저장 (기획 v4 §7.3 자랑 공유) - 잡은 종만
            if (seen)
            {
                var cardBtn = UIKit.Button(row, "카드", 10, new Vector2(34f, 22f), () =>
                {
                    var game = _ui.Game;
                    string path = DexCardExporter.Export(sp, game.Save, game.db);
                    if (path != null)
                        Application.OpenURL("file://" + System.IO.Path.GetDirectoryName(path));
                });
                UIKit.Fixed(cardBtn.gameObject, 34f, 22f);
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

            // 호수는 진영을 골라야 출몰이 시작된다 - 두 종 모두 진영 배타라서
            // 안 고르면 호수 출몰 풀이 빈다. 해금 직후 바로 묻는다 (원본과 같은 흐름).
            if (field.id == Field.Lake && string.IsNullOrEmpty(game.Save.faction))
                _ui.ShowFactionModal();
        }
    }
}
