using MartialTactics.Battle;
using MartialTactics.Game;
using UnityEngine.UIElements;

namespace MartialTactics.App
{
    /// <summary>중복 카드 쓰기 (승급 / 무공 추출) + 자유 스탯 배분. 화면 시안 캔버스의 CardUse · StatAlloc.</summary>
    static class CardUseScreen
    {
        public static VisualElement Build(GameApp app, OwnedUnit u)
        {
            var game = app.Game;
            var d = Catalog.Unit[u.Id];
            int stars = GameState.Stars(u.Id);

            var screen = Ui.El("screen");
            screen.Add(Ui.El("topbar").Add(Ui.IconBtn(Icon.Kind.Back, app.BackToDetail, 22, "back"), Ui.Lb("승급 · 추출", "title")));

            var sv = new ScrollView(ScrollViewMode.Vertical) { verticalScrollerVisibility = ScrollerVisibility.Hidden };
            sv.AddToClassList("pscroll");
            screen.Add(sv);

            var head = Ui.El("cu-head");
            head.Add(Ui.Portrait(u.Id, true, "cu-face"));
            var info = Ui.El("grow");
            info.Add(Ui.Lb(Ui.Stars(u.Id, u.Promotions), "sr"), Ui.Lb(d.Name, "uname"), Ui.Lb($"승급 {u.Promotions} / {stars}", "lb"));
            head.Add(info);
            var spare = Ui.El("spare-box");
            spare.Add(Ui.Lb("중복 카드", "lb"), Ui.Lb($"{u.Spare}", "spare-n"));
            head.Add(spare);
            sv.Add(head);

            // 두 쓰임새: 같은 카드 1장
            var uses = Ui.El("uses");
            var promote = Ui.El("use");
            promote.Add(Ui.Lb("승급", "use-name"), Ui.Lb($"자유 스탯 +{GameState.PointsPerPromotion}", "lb"));
            var pb = Ui.Btn(u.Promotions >= stars ? "최대" : "승급", () => { game.Promote(u); app.Save(); app.Refresh(); }, "btn", "pri");
            pb.SetEnabled(game.CanPromote(u));
            promote.Add(pb);
            var extract = Ui.El("use");
            var skill = Catalog.Skill[d.Skill];
            extract.Add(Ui.Lb("추출", "use-name"), Ui.Lb(skill.Name, "lb"));
            var eb = Ui.Btn("추출", () => { game.Extract(u); app.Save(); app.Refresh(); }, "btn");
            eb.SetEnabled(u.Spare > 0);
            extract.Add(eb);
            uses.Add(promote, extract);
            sv.Add(uses);

            // 자유 스탯 배분
            var alloc = Ui.El("box", "section");
            alloc.Add(Ui.El("row").Add(Ui.Lb("자유 스탯", "b", "grow"), Ui.Lb($"{u.FreePoints}", "free-n")));
            int[] baseStats = { d.Ext, d.Int, d.Def, d.Spd };
            for (int k = 0; k < 4; k++)
            {
                var stat = (StatKind)k;
                var row = Ui.El("srow");
                row.Add(Ui.Lb(GameState.StatNames[k], "sname"), Ui.Lb($"{baseStats[k] + u.Added[k]}", "sval"));
                row.Add(Ui.Lb(u.Added[k] > 0 ? $"+{u.Added[k]}" : "", "plus", "grow"));
                var add = Ui.Btn("+1", () => { game.AddStat(u, stat); app.Save(); app.Refresh(); }, "btn", "sm");
                add.SetEnabled(u.FreePoints > 0);
                row.Add(add);
                alloc.Add(row);
            }
            sv.Add(alloc);
            return screen;
        }
    }
}
