using System;
using MartialTactics.Battle;
using MartialTactics.Game;
using UnityEngine.UIElements;

namespace MartialTactics.App
{
    /// <summary>무인 상세 A안 (2026-10-28 확정): 일러스트 위, 정보 아래. 화면 시안 캔버스의 UnitDetailA.</summary>
    static class UnitDetailScreen
    {
        public static VisualElement Build(GameState game, OwnedUnit u, Action back, Action<int> pickSkill, Action cardUse, Action<int> equip)
        {
            var d = Catalog.Unit[u.Id];
            var e = GameState.EffectiveDef(u);
            int stars = GameState.Stars(u.Id);

            var screen = Ui.El("screen");
            var scroll = new ScrollView(ScrollViewMode.Vertical) { verticalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.style.flexGrow = 1;
            screen.Add(scroll);

            // 일러스트와 겹쳐 놓인 이름 카드. 뒤로 버튼은 일러스트 위에 띄운다
            var head = Ui.El();
            head.Add(Ui.Portrait(u.Id, false, "illust"));
            var card = Ui.El("namecard");
            card.Add(Ui.Lb($"{Ui.Stars(u.Id, u.Promotions)}  <color=#5c564d>{GameState.Rank(stars)} · 승급 {u.Promotions}/{stars}</color>", "stars"));
            card.Add(Ui.El("row").Add(Ui.Lb(d.Name, "uname"), Ui.Lb("Lv [n]", "lvtag")));
            card.Add(Ui.Lb($"{d.Title} · {d.Faction}", "lb"));
            Frame.On(card, Frame.Bronze, Frame.Vermilion, 3, 8);
            head.Add(card);
            scroll.Add(head);
            screen.Add(Ui.IconBtn(Icon.Kind.Back, back, 22, "back", "float"));

            var body = Ui.El("detail-body");
            scroll.Add(body);

            var stats = Ui.El("grid5");
            stats.Add(Stat("체력", d.Hp, 0, true));
            int[] baseStats = { d.Ext, d.Int, d.Def, d.Spd };
            for (int k = 0; k < 4; k++) stats.Add(Stat(GameState.StatNames[k], baseStats[k] + u.Added[k], u.Added[k], false));
            body.Add(stats);
            if (u.FreePoints > 0)
                body.Add(Ui.El("row", "section").Add(Ui.Lb($"남은 자유 스탯 <b>{u.FreePoints}</b>", "grow"), Ui.Btn("배분 ›", cardUse, "btn", "sm")));

            body.Add(Ui.Lb("속성 적성", "lb", "section"));
            var apt = Ui.El("grid5");
            apt.style.marginTop = 6;
            for (int i = 0; i < BattleRules.Elements.Length; i++)
            {
                var c = Ui.El("cell", "apt");
                if (i == 0) c.AddToClassList("first");
                c.Add(Ui.Lb(BattleRules.Elements[i], "lb"), Ui.Lb(d.Apt[i].ToString(), "b"));
                apt.Add(c);
            }
            body.Add(apt);

            body.Add(Ui.Lb("무공 3칸", "lb", "section"));
            var own = Ui.El("slot", "locked");
            own.Add(Ui.Lb(Catalog.Skill[d.Skill].Name, "b"), SkillTag(Catalog.Skill[d.Skill]), Ui.El("grow"), Ui.Lb("자기 무공", "lb"));
            body.Add(own);
            for (int i = 0; i < 2; i++)
            {
                int slot = i;
                var s = game.FindSkill(u.Equipped[i]);
                var row = Ui.El("slot");
                row.Add(Ui.Lb($"{i + 2}", "lb"));
                row[0].style.width = 16;
                if (s != null)
                {
                    var def = Catalog.Skill[s.Skill];
                    row.Add(Ui.Lb(def.Name, "b"), SkillTag(def), Ui.El("grow"), Ui.Lb("바꾸기 ›", "lb"));
                }
                else
                {
                    row.AddToClassList("empty");
                    row.Add(Ui.Lb("비어 있음", "lb"), Ui.El("grow"), Ui.Lb("장착 ›", "b"));
                }
                row.RegisterCallback<ClickEvent>(_ => pickSkill(slot));
                body.Add(row);
            }

            // 장비 4칸 (2026-10-29): 칸마다 주 스탯 하나. 장비 데이터가 아직 없어 빈 칸, 누르면 장비 바꾸기
            body.Add(Ui.Lb("장비", "lb", "section"));
            body.Add(GearRow(i => equip(i)));

            var bottom = Ui.El("bottombar");
            bottom.Add(Ui.El().Add(Ui.Lb("중복 카드", "lb"), Ui.Lb($"{u.Spare}장", "b")), Ui.El("grow"));
            var use = Ui.Btn("승급 / 무공 추출", cardUse, "btn", "pri");
            use.style.width = 200;
            use.SetEnabled(u.Spare > 0); // 승급·추출 둘 다 중복 카드가 있어야 한다
            bottom.Add(use);
            screen.Add(bottom);
            return screen;
        }

        /// <summary>장비 4칸 한 줄. 빈 칸은 칸 이름과 그 칸의 주 스탯.</summary>
        public static VisualElement GearRow(Action<int> tap)
        {
            var gear = Ui.El("grid5");
            gear.style.marginTop = 6;
            for (int i = 0; i < Ui.GearSlots.Length; i++)
            {
                int slot = i;
                var c = Ui.El("cell", "eq");
                if (i == 0) c.AddToClassList("first");
                c.Add(Ui.Lb(Ui.GearSlots[i], "b"), Ui.Lb(Ui.GearStats[i], "lb"));
                c.RegisterCallback<ClickEvent>(_ => tap(slot));
                gear.Add(c);
            }
            return gear;
        }

        static VisualElement Stat(string name, int value, int added, bool first)
        {
            var c = Ui.El("cell");
            if (first) c.AddToClassList("first");
            c.Add(Ui.Lb(name, "lb"), Ui.Lb(value.ToString(), "v"));
            if (added > 0) c.Add(Ui.Lb($"+{added}", "plus"));
            return c;
        }

        public static Label SkillTag(SkillDef s)
        {
            string type = BattleRules.TypeNames[s.Type];
            return Ui.Lb(s.Type == SkillType.Formation ? $"{type} · {s.Element}" : type, "tag");
        }
    }
}
