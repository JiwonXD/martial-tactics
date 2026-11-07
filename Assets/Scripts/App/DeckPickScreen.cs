using System;
using System.Linq;
using MartialTactics.Battle;
using MartialTactics.Game;
using UnityEngine.UIElements;

namespace MartialTactics.App
{
    /// <summary>무인 고르기 (덱 칸을 누름). 팀 속성 적성이 높은 순. 화면 시안 캔버스의 DeckPick.</summary>
    static class DeckPickScreen
    {
        const string AptOrder = "SABCD";

        public static VisualElement Build(GameApp app)
        {
            var game = app.Game;
            var deck = game.Data.Deck;
            int ei = Array.IndexOf(BattleRules.Elements, deck.Element);

            var screen = Ui.El("screen");
            var top = Ui.El("topbar").Add(Ui.IconBtn(Icon.Kind.Back, app.BackToDeck, 22, "back"), Ui.Lb(BattleRules.PosNames[app.DeckPos], "title", "grow"));
            if (ei >= 0) top.Add(Ui.Lb(deck.Element, "elem", "on", "small"));
            screen.Add(top);

            var sv = new ScrollView(ScrollViewMode.Vertical) { verticalScrollerVisibility = ScrollerVisibility.Hidden };
            sv.AddToClassList("pscroll");
            screen.Add(sv);

            var grid = Ui.El("grid");
            var units = game.Data.Units
                .OrderBy(u => ei >= 0 ? AptOrder.IndexOf(Catalog.Unit[u.Id].Apt[ei]) : 0)
                .ThenByDescending(u => GameState.Stars(u.Id));
            foreach (var u in units)
            {
                var cell = Ui.El("cell3");
                var card = LobbyScreen.UnitCard(u.Id, u.Promotions, 0);
                var pic = card.Q(className: "portrait");
                if (ei >= 0) pic.Add(Ui.Lb(Catalog.Unit[u.Id].Apt[ei].ToString(), "apt-badge"));
                int at = Array.IndexOf(deck.Units, u.Id);
                if (at >= 0)
                {
                    pic.Add(Ui.Lb(BattleRules.PosNames[at], "inDeck"));
                    if (at == app.DeckPos) card.AddToClassList("picked");
                }
                string id = u.Id;
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    game.SetDeckUnit(app.DeckPos, id);
                    app.Save();
                    app.BackToDeck();
                });
                cell.Add(card);
                grid.Add(cell);
            }
            sv.Add(grid);
            return screen;
        }
    }
}
