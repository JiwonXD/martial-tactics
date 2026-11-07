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

            // 머리 (삼국지 전략판 무장 화면 참고): 큰 초상(아래로 어두워짐), 왼쪽 세력 세로 깃발, 초상 둘레에 Lv · 승급 배지,
            // 아래에 이름 · 별 · 전투력. 뒤로 가기는 하단 바 가운데 ↩
            var head = Ui.El("dh");
            head.Add(Ui.Portrait(u.Id, false, "illust"));
            head.Add(Ui.El("illust-shade"));
            var flag = Ui.El("sect-flag");
            foreach (char ch in d.Faction) flag.Add(Ui.Lb(ch.ToString(), "sf-ch"));
            head.Add(flag);
            head.Add(Ui.Lb($"Lv {Ui.N}", "dh-lv"));
            head.Add(Ui.Lb($"승급 {u.Promotions}/{stars}", "dh-promo"));
            var card = Ui.El("dh-name");
            card.Add(Ui.Lb(Ui.Stars(u.Id, u.Promotions), "stars"), Ui.Lb(d.Name, "uname"), Ui.Lb($"{d.Title} · {GameState.Rank(stars)}", "lb"));
            head.Add(card);
            var pow = Ui.El("dh-pow");
            pow.Add(Ui.Lb("전투력", "lb"), Ui.Lb(Ui.N, "dh-pow-n"));
            head.Add(pow);
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

            // 무공 3칸: 칸마다 둥근 표식(종류 첫 글자) + 이름 한 줄. 빈 칸은 + 장착 가능
            body.Add(Ui.Lb("무공", "lb", "section"));
            body.Add(SkillLine(Catalog.Skill[d.Skill], true, null));
            for (int i = 0; i < 2; i++)
            {
                int slot = i;
                var s = game.FindSkill(u.Equipped[i]);
                body.Add(SkillLine(s != null ? Catalog.Skill[s.Skill] : null, false, () => pickSkill(slot)));
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

        static VisualElement SkillLine(SkillDef def, bool own, Action tap)
        {
            var row = Ui.El("sk-line");
            if (own) row.AddToClassList("own");
            if (def == null)
            {
                row.AddToClassList("empty");
                var plus = Ui.El("sk-dot", "plus");
                plus.Add(new Icon(Icon.Kind.Plus, 16));
                row.Add(plus, Ui.Lb("장착 가능", "sk-empty"));
            }
            else
            {
                var dot = Ui.El("sk-dot");
                dot.Add(Ui.Lb(BattleRules.TypeNames[def.Type].Substring(0, 1), "sk-dot-t"));
                row.Add(dot, Ui.Lb(def.Name, "sk-name"), SkillTag(def), Ui.El("grow"));
                if (own) row.Add(Ui.Lb("자기 무공", "lb"));
            }
            if (tap != null) row.RegisterCallback<ClickEvent>(_ => tap());
            return row;
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
