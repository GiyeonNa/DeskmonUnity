using System.Collections.Generic;
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
        readonly GameObject _scrollView;

        // 상세 보기 - 폼(도감 번호) 단위로 순환한다. 001 몽글이 -> 002 잎몽이 -> 003 꽃몽이
        // -> 004 ... 종 단위로 돌리면 진화체는 자기 페이지가 없어 도트를 볼 방법이 없다.
        GameObject _detail;
        int _detailIndex = -1;
        readonly List<(SpeciesData sp, int stage)> _formIndex = new List<(SpeciesData, int)>();
        bool DetailOpen => _detail != null && _detail.activeSelf;

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
            _scrollView = _list.parent.gameObject;   // ScrollList의 뷰포트 - 상세와 교대한다
        }

        public void Refresh()
        {
            // 상세가 열려 있으면 상세를 다시 그린다 (포획 등으로 상태가 바뀌었을 수 있다)
            if (DetailOpen) { ShowDetail(_detailIndex); return; }

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

                    var btn = UIKit.Button(row, $"해금 (베리 {UIKit.Fmt(field.unlockCost)})", 12,
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

            // 줄 전체 클릭 = 상세 보기. 투명하지만 레이캐스트는 받는 배경 -
            // 위에 얹힌 개별 버튼(카드)이 먼저 클릭을 가져가므로 충돌하지 않는다.
            var rowBg = row.gameObject.AddComponent<Image>();
            rowBg.color = Color.clear;
            var rowBtn = row.gameObject.AddComponent<Button>();
            rowBtn.targetGraphic = rowBg;
            rowBtn.onClick.AddListener(() => ShowDetailOf(sp));

            var icon = UIKit.SpriteIcon(row, sp.SpriteAt(0), 26f);
            if (!seen) icon.color = new Color(0f, 0f, 0f, 0.55f);   // 실루엣
            UIKit.Fixed(icon.gameObject, 26f, 26f);

            var name = UIKit.Label(row, seen ? sp.displayName : "???", 12,
                                   seen ? UIKit.TextMain : UIKit.TextSub);
            UIKit.Fixed(name.gameObject, 92f, 26f);

            BuildFormCells(row, sp, dx, 14f);

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

        /// <summary>
        /// 폼 수집 칸. 수집한 폼은 초록, 샤이니까지 있으면 금색.
        /// 홈 프레임(테마)이 있으면 미수집이어도 칸 자체는 보인다 -
        /// "여기 채울 자리가 있다"는 정보다. 목록과 상세가 공유한다.
        /// </summary>
        static void BuildFormCells(Transform parent, SpeciesData sp, SaveData.DexEntry dx, float size)
        {
            var theme = UIKit.Theme;
            for (int f = 0; f < sp.forms; f++)
            {
                var cell = new GameObject("Form", typeof(RectTransform), typeof(Image));
                cell.transform.SetParent(parent, false);
                var img = cell.GetComponent<Image>();
                img.raycastTarget = false;
                UIKit.Fixed(cell, size, size);

                Color state = dx.shinyForms[f] ? UIKit.TextGold
                            : dx.forms[f] ? UIKit.Accent
                            : Color.clear;

                if (theme != null && theme.frameCell != null)
                {
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
        }

        // ── 상세 보기 ──

        /// <summary>도감 번호 순서의 (종, 폼) 평탄 목록. 종 구성이 바뀌면 다시 만든다.</summary>
        void EnsureFormIndex(DeskmonDatabase db)
        {
            int total = 0;
            foreach (var sp in db.species) if (sp != null) total += sp.forms;
            if (_formIndex.Count == total) return;

            _formIndex.Clear();
            foreach (var sp in db.species)
            {
                if (sp == null) continue;
                for (int st = 0; st < sp.forms; st++) _formIndex.Add((sp, st));
            }
        }

        /// <summary>이 종의 기본형 페이지를 연다 - 목록 줄 클릭.</summary>
        void ShowDetailOf(SpeciesData sp)
        {
            var db = _ui.Game?.db;
            if (db == null) return;
            EnsureFormIndex(db);

            for (int i = 0; i < _formIndex.Count; i++)
                if (_formIndex[i].sp == sp && _formIndex[i].stage == 0) { ShowDetail(i); return; }
        }

        /// <summary>
        /// 폼 하나를 크게 보여준다. index는 평탄 목록 위치이고 좌우로 순환한다.
        /// 그 폼을 기록하지 못했으면 실루엣 + ??? - 진화체는 진화시켜야 보인다.
        /// 설명/포획 횟수는 라인 단위 정보라 기본형을 잡았으면 보여준다.
        /// </summary>
        void ShowDetail(int index)
        {
            var game = _ui.Game;
            var db = game?.db;
            if (db == null || game.Save == null) return;

            EnsureFormIndex(db);
            if (_formIndex.Count == 0) return;

            int n = _formIndex.Count;
            _detailIndex = (index % n + n) % n;   // 음수 안전 순환

            var (sp, stage) = _formIndex[_detailIndex];
            var save = game.Save;
            var dx = save.Dex(sp.id);
            bool seen = dx.caught > 0;
            bool formSeen = dx.forms[stage] || dx.shinyForms[stage];

            _scrollView.SetActive(false);
            if (_detail == null)
            {
                _detail = new GameObject("Detail", typeof(RectTransform));
                _detail.transform.SetParent(_scrollView.transform.parent, false);
                UIKit.Fixed(_detail, 0f, 330f);
            }
            _detail.SetActive(true);
            foreach (Transform c in _detail.transform) Object.Destroy(c.gameObject);

            var v = UIKit.VList(_detail.transform, 6f, new RectOffset(4, 4, 4, 4));
            UIKit.Stretch(v);

            // 내비게이션: 이전/번호/다음/목록
            var nav = UIKit.HRow(v, 26f);
            UIKit.Fixed(nav.gameObject, 0f, 26f);

            var prev = UIKit.Button(nav, "<", 14, new Vector2(34f, 24f),
                                    () => ShowDetail(_detailIndex - 1));
            UIKit.Fixed(prev.gameObject, 34f, 24f);

            // 정본 도감 번호(151 원장, 폼 단위). 원장 미연결 데이터면 목록 순서로 대체.
            int dexNo = sp.DexNoAt(stage);
            var no = UIKit.Label(nav, $"No.{(dexNo > 0 ? dexNo : _detailIndex + 1):000}", 13, UIKit.TextSub,
                                 TextAnchor.MiddleCenter);
            UIKit.Fixed(no.gameObject, 70f, 24f);

            var next = UIKit.Button(nav, ">", 14, new Vector2(34f, 24f),
                                    () => ShowDetail(_detailIndex + 1));
            UIKit.Fixed(next.gameObject, 34f, 24f);

            var back = UIKit.Button(nav, "목록", 12, new Vector2(50f, 24f), CloseDetail);
            UIKit.Fixed(back.gameObject, 50f, 24f);

            // 본체: 큰 아이콘 + 정보
            var body = UIKit.HRow(v, 100f, 10f);
            UIKit.Fixed(body.gameObject, 0f, 100f);

            var icon = UIKit.SpriteIcon(body, sp.SpriteAt(stage), 96f);
            if (!formSeen) icon.color = new Color(0f, 0f, 0f, 0.55f);   // 실루엣
            UIKit.Fixed(icon.gameObject, 96f, 96f);

            var info = UIKit.VList(body, 3f, new RectOffset(0, 0, 0, 0));
            UIKit.Fixed(info.gameObject, 176f, 100f);

            var nameLabel = UIKit.Label(info, formSeen ? sp.NameAt(stage) : "???", 17,
                                        formSeen ? UIKit.TextMain : UIKit.TextSub);
            nameLabel.fontStyle = FontStyle.Bold;
            UIKit.Fixed(nameLabel.gameObject, 0f, 22f);

            // 필드는 목록에서도 보이는 정보라 미포획이어도 표시한다. 희귀도는 숨긴다.
            var field = db.GetField(sp.field);
            string meta = (field != null ? field.displayName : sp.field.ToString())
                        + " · " + (seen ? RarityName(sp.rarity) : "???");
            if (sp.field == Field.Lake && !string.IsNullOrEmpty(save.faction))
                meta += save.faction == "dew" ? " · 이슬 팀" : " · 이끼 팀";
            UIKit.Fixed(UIKit.Label(info, meta, 12, UIKit.Accent).gameObject, 0f, 16f);

            // 진화 라인 - 만난 폼만 이름을 보여주고, 지금 보는 폼을 굵게 표시한다
            if (sp.forms > 1)
            {
                var names = new string[sp.forms];
                for (int i = 0; i < sp.forms; i++)
                {
                    string nm = (dx.forms[i] || dx.shinyForms[i]) ? sp.NameAt(i) : "???";
                    names[i] = i == stage ? $"<b>{nm}</b>" : nm;
                }
                UIKit.Fixed(UIKit.Label(info, string.Join(" → ", names), 11, UIKit.TextSub)
                    .gameObject, 0f, 16f);
            }

            var cellRow = UIKit.HRow(info, 18f, 4f);
            UIKit.Fixed(cellRow.gameObject, 0f, 18f);
            BuildFormCells(cellRow, sp, dx, 16f);
            if (dx.milestone)
            {
                var done = UIKit.Label(cellRow, "라인 완성", 11, UIKit.TextGold);
                UIKit.Fixed(done.gameObject, 60f, 16f);
            }

            UIKit.Fixed(UIKit.Label(info, seen ? $"포획 {dx.caught}회" : "", 11, UIKit.TextSub)
                .gameObject, 0f, 14f);

            // 설명 - 그 폼을 기록하지 못했으면 감춘다. 도감은 만나야 채워지는 책이다.
            var desc = UIKit.Label(v, formSeen ? sp.description : "아직 만나지 못한 몬스터입니다.",
                                   13, formSeen ? UIKit.TextMain : UIKit.TextSub);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.alignment = TextAnchor.UpperLeft;
            UIKit.Fixed(desc.gameObject, 0f, 80f);

            if (seen)
            {
                var actions = UIKit.HRow(v, 28f);
                UIKit.Fixed(actions.gameObject, 0f, 28f);
                var card = UIKit.Button(actions, "카드 저장", 12, new Vector2(90f, 26f), () =>
                {
                    string path = DexCardExporter.Export(sp, save, db);
                    if (path != null)
                        Application.OpenURL("file://" + System.IO.Path.GetDirectoryName(path));
                });
                UIKit.Fixed(card.gameObject, 90f, 26f);
            }
        }

        static string RarityName(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return "희귀";
                case Rarity.Epic: return "에픽";
                case Rarity.Legendary: return "전설";
                default: return "일반";
            }
        }

        void CloseDetail()
        {
            if (_detail != null) _detail.SetActive(false);
            _scrollView.SetActive(true);
            _detailIndex = -1;
            Refresh();   // 상세에서 바뀐 상태(포획 등)가 목록에도 반영되게
        }

        void Unlock(FieldData field)
        {
            var game = _ui.Game;
            if (game?.Save == null) return;
            if (game.Save.berry < field.unlockCost) return;

            game.Save.berry -= field.unlockCost;
            game.Save.habitats.Add(SpawnScheduler.FieldId(field.id));
            Sfx.Unlock();

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
