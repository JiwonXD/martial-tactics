using MartialTactics.Battle;
using UnityEngine.UIElements;

namespace MartialTactics.App
{
    /// <summary>
    /// 대전 준비 (삼국지 전략판 출정 화면 참고): 위 패널에 양 팀 전투력을 한 번만, 그 아래 상대·아군 줄.
    /// 줄마다 칸 위에 역할 띠(왼쪽부터 주장 · 부장 · 부장)와 세로 카드 3장, 빈 칸은 실루엣. 상대는 아직 임시(무작위 3명 + 무작위 속성).
    /// </summary>
    static class BattleReadyScreen
    {
        public static VisualElement Build(GameApp app)
        {
            var game = app.Game;
            var screen = Ui.El("screen", "ready");

            var errors = game.ValidateDeck();
            var sum = Ui.El("row", "ready-sum");
            sum.Add(Side("아군", game.Data.Deck.Element, false), Ui.Lb(":", "rs-colon"), Side("상대", app.Opponent.Element, true));
            screen.Add(sum);

            var field = Ui.El("ready-field");
            field.Add(TeamRow(app.Opponent, true));
            if (errors.Count == 0) field.Add(TeamRow(game.BuildTeam(), false));
            else
            {
                var box = Ui.El("ready-err");
                foreach (var e in errors) box.Add(Ui.Lb(e, "err"));
                box.Add(Ui.Btn("덱 편성", app.BackToDeck, "btn"));
                field.Add(box);
            }
            screen.Add(field);

            var footer = Ui.El("footer", "static");
            var start = Ui.Btn("전투 시작", app.StartBattle, "btn", "pri", "second");
            start.SetEnabled(errors.Count == 0);
            footer.Add(Ui.Btn("새 상대", app.NewOpponent, "btn"), start);
            screen.Add(footer);
            return screen;
        }

        static VisualElement Side(string who, string element, bool foe)
        {
            var e = Ui.El("rs-side");
            if (foe) e.AddToClassList("foe");
            var top = Ui.El("row");
            top.Add(Ui.Lb(who, "rs-who"), Ui.Lb(string.IsNullOrEmpty(element) ? "-" : element, "elem", "on", "small"));
            e.Add(top, Ui.Lb($"전투력 {Ui.N}", "rs-pow"));
            return e;
        }

        static VisualElement TeamRow(Team team, bool foe)
        {
            var box = Ui.El("rteam");
            if (foe) box.AddToClassList("foe");
            var row = Ui.El("rrow");
            for (int i = 0; i < 3; i++)
            {
                var col = Ui.El("rcol");
                if (i == 0) col.AddToClassList("leader");
                col.Add(Ui.Lb(BattleRules.PosNames[i], "role-band"));
                var s = i < team.Slots.Length ? team.Slots[i] : null;
                if (s?.Unit != null)
                {
                    var pic = Ui.Portrait(s.Unit, false, "rpic");
                    pic.Add(Ui.Lb($"Lv {Ui.N}", "lv"));
                    pic.Add(Skin.CardFrame(i == 0));
                    col.Add(pic, Ui.Lb(Catalog.Unit[s.Unit].Name, "un"));
                }
                else
                {
                    var empty = Ui.El("rpic", "silhouette");
                    empty.Add(Ui.Item(Icon.Kind.Units, 56));
                    col.Add(empty, Ui.Lb(" ", "un"));
                }
                row.Add(col);
            }
            box.Add(row);
            return box;
        }
    }
}
