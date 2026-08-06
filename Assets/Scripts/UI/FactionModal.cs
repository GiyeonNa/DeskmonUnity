using UnityEngine;
using Deskmon.Core;

namespace Deskmon.UI
{
    /// <summary>
    /// 진영 선택 모달. index.html showFactionModal() 이식 (기획 v4 §7.1).
    ///
    /// 호수의 두 종은 진영 배타다 - 이슬 팀은 이슬방울만, 이끼 팀은 이끼돌만 출몰한다.
    /// "혼자 다 모을 수 없다"가 요점이고, 상대 팀 종은 Phase 2 교환으로만 얻는다.
    ///
    /// 선택은 한 번뿐이고 되돌릴 수 없다 - 그래서 닫기 버튼이 없다. 원본도 같다.
    /// 고르지 않고 지나칠 수는 있다(모달 밖 클릭 무시). 그동안 호수에는 아무것도
    /// 안 나오며, 도감의 [진영 선택] 버튼으로 다시 열 수 있다.
    /// </summary>
    public class FactionModal
    {
        public GameObject Root { get; }

        readonly UIRoot _ui;

        public FactionModal(Transform parent, UIRoot ui)
        {
            _ui = ui;

            // 화면 중앙 - 카드 구석이 아니라 "결정하라"는 자리다
            Root = UIKit.Panel(parent, "FactionModal", new Vector2(320f, 170f),
                               new Vector2(0.5f, 0.5f), Vector2.zero);

            var v = UIKit.VList(Root.transform, 6f);
            UIKit.Stretch(v);

            var title = UIKit.Label(v, "진영을 선택하세요", 15, UIKit.TextMain);
            title.fontStyle = FontStyle.Bold;
            UIKit.Fixed(title.gameObject, 0f, 20f);

            var desc = UIKit.Label(v,
                "호수에는 두 팀의 몬스터가 살아요.\n" +
                "상대 팀 몬스터는 나중에 교환으로만 얻을 수 있어요.\n" +
                "한 번 고르면 바꿀 수 없습니다.",
                12, UIKit.TextSub);
            UIKit.Fixed(desc.gameObject, 0f, 54f);

            var row = UIKit.HRow(v, 34f, 8f);
            UIKit.Fixed(row.gameObject, 0f, 34f);

            var dew = UIKit.Button(row, "이슬 팀 (이슬방울)", 12, new Vector2(144f, 32f),
                                   () => Choose("dew"));
            UIKit.Fixed(dew.gameObject, 144f, 32f);

            var moss = UIKit.Button(row, "이끼 팀 (이끼돌)", 12, new Vector2(144f, 32f),
                                    () => Choose("moss"));
            UIKit.Fixed(moss.gameObject, 144f, 32f);
        }

        void Choose(string faction)
        {
            var game = _ui.Game;
            if (game?.Save == null) return;

            // 이미 골랐으면 덮어쓰지 않는다 - 되돌릴 수 없다는 규칙의 방어선
            if (string.IsNullOrEmpty(game.Save.faction))
            {
                game.Save.faction = faction;
                SaveSystem.Save(game.Save);
            }

            Root.SetActive(false);
            _ui.RefreshOpenPanel();
        }
    }
}
