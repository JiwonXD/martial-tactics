using MartialTactics.Battle;
using MartialTactics.Game;
using UnityEngine.UIElements;

namespace MartialTactics.App
{
    /// <summary>무공 고르기 (장착). 추출해 둔 무공 중에서 무공창 2·3칸에 낄 것을 고른다. 화면 시안 캔버스의 SkillPick.</summary>
    static class SkillPickScreen
    {
        public static VisualElement Build(GameApp app, OwnedUnit u, int slot)
        {
            var game = app.Game;
            var screen = Ui.El("screen");
            var top = Ui.El("topbar").Add(Ui.IconBtn(Icon.Kind.Back, app.BackToDetail, 22, "back"), Ui.Lb($"무공 {slot + 2}칸", "title", "grow"));
            if (game.FindSkill(u.Equipped[slot]) != null)
                top.Add(Ui.Btn("비우기", () => { game.Unequip(u, slot); app.Save(); app.BackToDetail(); }, "btn", "sm"));
            screen.Add(top);

            var sv = new ScrollView(ScrollViewMode.Vertical) { verticalScrollerVisibility = ScrollerVisibility.Hidden };
            sv.AddToClassList("pscroll");
            screen.Add(sv);

            if (game.Data.Skills.Count == 0) sv.Add(Ui.Placeholder("[추출한 무공 없음]", -1, 120));
            foreach (var s in game.Data.Skills)
            {
                var def = Catalog.Skill[s.Skill];
                var holder = game.EquippedBy(s.Uid);
                string err = game.EquipError(u, slot, s);
                var row = Ui.El("skrow", "plate-row");
                if (err != null) row.AddToClassList("off");
                var line = Ui.El("row");
                line.Add(Ui.Lb(def.Name, "b"), UnitDetailScreen.SkillTag(def), Ui.El("grow"));
                if (holder != null)
                {
                    line.Add(Ui.Portrait(holder.Id, true, "holder"));
                    line.Add(Ui.Lb(Catalog.Unit[holder.Id].Name, "lb"));
                }
                row.Add(line);
                row.Add(Ui.Lb(err ?? def.Desc, err != null ? "err" : "desc"));
                if (err == null)
                    row.RegisterCallback<ClickEvent>(_ => { game.Equip(u, slot, s); app.Save(); app.BackToDetail(); });
                sv.Add(row);
            }
            return screen;
        }
    }
}
