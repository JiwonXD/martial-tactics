using System;
using System.Linq;
using MartialTactics.Battle;
using MartialTactics.Game;
using UnityEngine.UIElements;

namespace MartialTactics.App
{
    /// <summary>
    /// 성장 화면: 무인 레벨업 · 장비 (무인 탭 하위 탭), 폐관수련 (성장 탭). 규칙은 기획서 2026-10-28~29, 수치는 [n].
    /// </summary>
    static class GrowthScreens
    {
        // 무인 레벨업: 레벨업 재화로 올리고 기본 스탯이 자동으로 오른다. 덱 무인을 위에
        public static void LevelUp(GameApp app, VisualElement body)
        {
            var sv = LobbyScreen.Scroll();
            var deck = app.Game.Data.Deck.Units;
            var inDeck = deck.Select(app.Game.Find).Where(u => u != null).ToList();
            if (inDeck.Count > 0)
            {
                sv.Add(Ui.Band("덱", "sub-band"));
                foreach (var u in inDeck) sv.Add(LevelRow(app, u, Array.IndexOf(deck, u.Id)));
            }
            sv.Add(Ui.Band("다른 무인", "sub-band"));
            foreach (var u in app.Game.Data.Units.Where(x => !inDeck.Contains(x)).OrderByDescending(x => GameState.Stars(x.Id)))
                sv.Add(LevelRow(app, u, -1));
            body.Add(sv);
        }

        static VisualElement LevelRow(GameApp app, OwnedUnit u, int pos)
        {
            var row = Ui.El("urow", "plate-row");
            var face = Ui.Portrait(u.Id, true, "uface");
            if (pos >= 0) face.Add(Ui.Lb(BattleRules.PosNames[pos], pos == 0 ? "pos-seal" : "pos-seal"));
            row.Add(face);
            var t = Ui.El("grow");
            t.Add(Ui.Lb(Ui.Stars(u.Id, u.Promotions), "sr", "left"), Ui.Lb(Catalog.Unit[u.Id].Name, "b"), Ui.Lb($"Lv {Ui.N}", "lb"));
            row.Add(t);
            var b = new Button();
            b.AddToClassList("btn");
            b.AddToClassList("iconbtn");
            b.Add(Ui.Lb("레벨업", "bt"), Ui.Item(Icon.Kind.Book, 20), Ui.Lb(Ui.N, "bt"));
            row.Add(b);
            string id = u.Id;
            face.RegisterCallback<ClickEvent>(_ => app.OpenUnit(id));
            return row;
        }

        // 장비: 무인마다 4칸. 칸을 누르면 장비 바꾸기
        public static void GearList(GameApp app, VisualElement body)
        {
            var sv = LobbyScreen.Scroll();
            var deck = app.Game.Data.Deck.Units;
            var units = app.Game.Data.Units
                .OrderBy(u => Array.IndexOf(deck, u.Id) is int i && i >= 0 ? i : 9)
                .ThenByDescending(u => GameState.Stars(u.Id));
            foreach (var u in units)
            {
                var box = Ui.El("gear-box");
                var head = Ui.El("row");
                int pos = Array.IndexOf(deck, u.Id);
                head.Add(Ui.Portrait(u.Id, true, "holder"), Ui.Lb(Catalog.Unit[u.Id].Name, "b", "grow"));
                if (pos >= 0) head.Add(Ui.Lb(BattleRules.PosNames[pos], "tag"));
                head.Add(Ui.Lb($"전투력 {Ui.N}", "lb"));
                box.Add(head);
                string id = u.Id;
                box.Add(UnitDetailScreen.GearRow(s => app.OpenEquipSwap(id, s, false)));
                sv.Add(box);
            }
            body.Add(sv);
        }

        static readonly string[] BaseNodes = { "외공", "내공", "호신", "경공", "체력" };

        // 폐관수련 (크럼블 노움 연구소 참고): 기본 수련 = 관마다 외공·내공·호신·경공·체력 % 사다리 (단약),
        // 옆 곁가지 특별 수련 = 방치 보상 최대 시간·양 (영단), 속성 갈래 = 그 속성 덱일 때만 4스탯 % · 상극 우위 피해 %p.
        // 한 번에 하나, 시간이 걸린다
        public static void Research(GameApp app, VisualElement body)
        {
            // 크럼블 노움 연구소 짜임: 위에 노드 나무(누르면 고름), 아래 고른 노드 상세 패널
            var sv = LobbyScreen.Scroll();
            var gwan = Ui.El("row", "gwan");
            gwan.Add(Ui.IconBtn(Icon.Kind.Prev, null, 18, "sq", "small"), Ui.Lb("제 1관", "gwan-t"), Ui.IconBtn(Icon.Kind.Next, null, 18, "sq", "small"));
            sv.Add(gwan);

            // 노드 번호: 0~4 기본 수련 사다리, 5~6 특별 수련 곁가지, 7~11 속성 갈래
            var tree = Ui.El("tree");
            for (int i = 0; i < BaseNodes.Length; i++)
            {
                var row = Ui.El("t-row");
                row.Add(Ui.El("t-side"));
                row.Add(Tile(app, i, BaseNodes[i], false));
                var side = Ui.El("t-side");
                if (i == 1 || i == 3)
                {
                    side.Add(Ui.El("t-hline"));
                    side.Add(Tile(app, i == 1 ? 5 : 6, i == 1 ? "방치 시간" : "방치 보상", true));
                }
                row.Add(side);
                tree.Add(row);
                tree.Add(Ui.El("t-vline"));
            }
            var els = Ui.El("t-els");
            for (int i = 0; i < BattleRules.Elements.Length; i++) els.Add(Tile(app, 7 + i, BattleRules.Elements[i], false, true));
            tree.Add(els);
            sv.Add(tree);
            body.Add(sv);

            body.Add(Detail(app));
        }

        static VisualElement Tile(GameApp app, int index, string name, bool special, bool small = false)
        {
            var t = new Button(() => { app.ResearchNode = index; app.Refresh(); });
            t.AddToClassList("tile");
            if (special) t.AddToClassList("special");
            if (small) t.AddToClassList("small");
            if (app.ResearchNode == index) t.AddToClassList("on");
            t.Add(Ui.Lb(name, "tile-name"));
            var prog = Ui.El("tile-prog");
            prog.Add(Ui.Lb($"{Ui.N}/{Ui.N}", "tile-n"));
            t.Add(prog);
            return t;
        }

        // 고른 노드 상세: 이름, 효과 지금 > 다음, 수련 시간, 수련 시작(비용). 특별 수련은 영단, 나머지는 단약
        static VisualElement Detail(GameApp app)
        {
            int n = app.ResearchNode;
            string name, effect;
            if (n < 5) { name = $"{BaseNodes[n]} 수련"; effect = $"{BaseNodes[n]} 증가"; }
            else if (n < 7) { name = n == 5 ? "방치 시간" : "방치 보상"; effect = n == 5 ? "최대 시간" : "보상 양"; }
            else { name = $"{BattleRules.Elements[n - 7]} 갈래"; effect = $"{BattleRules.Elements[n - 7]} 덱 4스탯 · 상극 우위"; }
            bool special = n == 5 || n == 6;

            var panel = Ui.El("detail-panel");
            var top = Ui.El("row");
            var icon = Ui.El("dp-icon");
            icon.Add(Ui.Lb(name.Substring(0, 1), "dp-glyph"));
            top.Add(icon);
            var info = Ui.El("grow");
            info.Add(Ui.Lb(name, "dp-name"));
            var eff = Ui.El("row", "dp-eff");
            eff.Add(Ui.Lb(effect, "dp-eff-t", "grow"), Ui.Lb($"{Ui.N}%", "dp-now"), new Icon(Icon.Kind.Next, 14), Ui.Lb($"{Ui.N}%", "dp-next"));
            info.Add(eff);
            top.Add(info);
            panel.Add(top);
            var bottom = Ui.El("row", "dp-bottom");
            var time = Ui.El("dp-time");
            time.Add(Ui.Lb("수련 시간", "dp-time-l"));
            var tr = Ui.El("row");
            tr.Add(Ui.Item(Icon.Kind.Clock, 18), Ui.Lb($"{Ui.N}:{Ui.N}:{Ui.N}", "dp-time-n"));
            time.Add(tr);
            bottom.Add(time);
            var start = new Button();
            start.AddToClassList("btn");
            start.AddToClassList("pri");
            start.AddToClassList("dp-start");
            start.Add(Ui.Lb("수련 시작", "draw-name"));
            var cost = Ui.El("row", "draw-cost");
            cost.Add(Ui.Item(Icon.Kind.Pill, 18, special ? "pill2" : "pill"), Ui.Lb(Ui.N, "dc-t"));
            start.Add(cost);
            bottom.Add(start);
            panel.Add(bottom);
            return panel;
        }
    }
}
