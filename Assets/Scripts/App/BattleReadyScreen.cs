using MartialTactics.Battle;
using UnityEngine.UIElements;

namespace MartialTactics.App
{
    /// <summary>대전 준비: 위 상대, 아래 아군 (왼쪽부터 주장 · 부장 · 부장). 상대는 아직 임시(무작위 3명 + 무작위 속성).</summary>
    static class BattleReadyScreen
    {
        public static VisualElement Build(GameApp app)
        {
            var game = app.Game;
            var screen = Ui.El("screen", "ready");
            screen.Add(Ui.El("topbar").Add(Ui.IconBtn(Icon.Kind.Back, app.CloseTab, 22, "back"), Ui.Lb("대전", "title")));

            var errors = game.ValidateDeck();
            var field = Ui.El("ready-field");
            field.Add(TeamRow(app.Opponent, true));
            field.Add(Ui.Lb("對", "vs"));
            if (errors.Count == 0) field.Add(TeamRow(game.BuildTeam(), false));
            else
            {
                var box = Ui.El("ready-err");
                foreach (var e in errors) box.Add(Ui.Lb(e, "err"));
                box.Add(Ui.Btn("덱 편성", app.BackToDeck, "btn"));
                field.Add(box);
            }
            screen.Add(field);

            var footer = Ui.El("footer");
            var start = Ui.Btn("전투 시작", app.StartBattle, "btn", "pri", "second");
            start.SetEnabled(errors.Count == 0);
            footer.Add(Ui.Btn("새 상대", app.NewOpponent, "btn"), start);
            screen.Add(footer);
            return screen;
        }

        static VisualElement TeamRow(Team team, bool foe)
        {
            var box = Ui.El("rteam");
            if (foe) box.AddToClassList("foe");
            var head = Ui.El("row");
            head.Add(Ui.Lb(foe ? "상대" : "아군", "b", "grow"), Ui.Lb(team.Element, "elem", "on", "small"));
            box.Add(head);
            var row = Ui.El("rrow");
            for (int i = 0; i < team.Slots.Length; i++)
            {
                var s = team.Slots[i];
                var col = Ui.El("rcol");
                if (i == 0) col.AddToClassList("leader");
                var pic = Ui.Portrait(s.Unit, false, "rpic");
                pic.Add(Ui.Lb(BattleRules.PosNames[i], "pos-seal"));
                col.Add(pic, Ui.Lb(Catalog.Unit[s.Unit].Name, "un"));
                row.Add(col);
            }
            box.Add(row);
            return box;
        }
    }
}
