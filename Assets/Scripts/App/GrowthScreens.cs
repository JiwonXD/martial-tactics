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
            var sv = LobbyScreen.Scroll();
            var hero = Ui.ArtBox("research_hero", "[폐관수련 그림]", "hero", "short");
            var cap = Ui.El("hero-cap");
            cap.Add(Ui.Cur(Icon.Kind.Pill, Ui.N), Ui.Cur(Icon.Kind.Pill, Ui.N, "rare"));
            hero.Add(cap);
            sv.Add(hero);

            // 진행 중인 수련 하나
            var now = Ui.El("research-now", "plate-row");
            now.Add(Ui.Item(Icon.Kind.Clock, 24), Ui.Lb($"외공 Lv {Ui.N}", "b", "grow"), Ui.Lb($"{Ui.N}:{Ui.N}:{Ui.N}", "b"));
            sv.Add(now);

            var gwan = Ui.El("row", "gwan");
            gwan.Add(Ui.IconBtn(Icon.Kind.Prev, null, 18, "sq", "small"), Ui.Lb("제 1관", "gwan-t"), Ui.IconBtn(Icon.Kind.Next, null, 18, "sq", "small"));
            sv.Add(gwan);

            // 사다리(가운데 줄) + 곁가지(오른쪽)
            var tree = Ui.El("tree");
            var ladder = Ui.El("ladder");
            ladder.Add(Ui.El("ladder-line"));
            for (int i = 0; i < BaseNodes.Length; i++)
            {
                var row = Ui.El("tnode-row");
                row.Add(Node(BaseNodes[i], $"+{Ui.N}%", i == 0, false));
                if (i == 1 || i == 3)
                {
                    row.Add(Ui.El("branch-line"));
                    row.Add(Node(i == 1 ? "방치 시간" : "방치 보상", i == 1 ? $"+{Ui.N}시간" : $"+{Ui.N}%", false, true));
                }
                ladder.Add(row);
            }
            tree.Add(ladder);
            sv.Add(tree);

            // 속성 갈래
            sv.Add(Ui.Band("속성 갈래", "sub-band"));
            var el = Ui.El("elements", "tight");
            for (int i = 0; i < BattleRules.Elements.Length; i++)
            {
                int k = i;
                var b = Ui.Btn(BattleRules.Elements[i], () => { app.ResearchElement = k; app.Refresh(); }, "elem", "mid");
                if (app.ResearchElement == i) b.AddToClassList("on");
                el.Add(b);
            }
            sv.Add(el);
            var branch = Ui.El("chips-wrap", "nodes");
            foreach (var n in new[] { "외공", "내공", "호신", "경공" }) branch.Add(Node(n, $"+{Ui.N}%", false, false, true));
            branch.Add(Node("상극 우위", $"+{Ui.N}%p", false, false, true));
            sv.Add(branch);
            body.Add(sv);
        }

        static VisualElement Node(string name, string value, bool active, bool special, bool small = false)
        {
            var n = Ui.El("tnode");
            if (active) n.AddToClassList("active");
            if (special) n.AddToClassList("special");
            if (small) n.AddToClassList("small");
            n.Add(Ui.Lb(name, "tn-name"), Ui.Lb(value, "tn-v"), Ui.Lb($"Lv {Ui.N}/{Ui.N}", "tn-lv"));
            if (!special && !small) n.AddToClassList("plate-row");
            if (special) Frame.On(n, Frame.Vermilion, Frame.Vermilion, 3, 6);
            return n;
        }
    }
}
