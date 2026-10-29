using System;
using System.Linq;
using MartialTactics.Battle;
using MartialTactics.Game;
using UnityEngine.UIElements;

namespace MartialTactics.App
{
    /// <summary>
    /// 컨텐츠 탭(대전 묶음 · 일일 던전)과 뽑기 탭. 화면 시안 캔버스의 Match · Dungeon · DungeonEntry · Gacha · GachaRates.
    /// 지금 실제로 돌아가는 건 대전(임시 상대)과 무인 뽑기뿐이고, 나머지 수치는 [n].
    /// </summary>
    static class ContentScreens
    {
        public static void Match(GameApp app, VisualElement body)
        {
            var sv = LobbyScreen.Scroll();
            sv.Add(Banner("banner_pvp", "대전", "對戰", app.OpenBattleReady, null));
            sv.Add(Banner("banner_rank", "엔트리 순위전", "順位", null, "준비 중"));
            sv.Add(Banner("banner_winners", "위너스 연전", "連戰", null, "준비 중"));
            body.Add(sv);
        }

        public static void Dungeons(GameApp app, VisualElement body)
        {
            var sv = LobbyScreen.Scroll();
            sv.Add(Banner("banner_dungeon", "단약 던전", "丹藥", app.OpenDungeon, $"입장 {Ui.N}/{Ui.N}"));
            body.Add(sv);
        }

        static VisualElement Banner(string art, string name, string seal, Action open, string note)
        {
            var b = Ui.ArtBox(art, "[배너 그림]", "banner-card");
            if (open == null) b.AddToClassList("locked");
            else b.RegisterCallback<ClickEvent>(_ => open());
            b.Add(Ui.Band(name, "banner-band"));
            if (note != null) b.Add(Ui.Lb(note, "banner-note"));
            b.Add(Ui.Lb(seal, "banner-seal"));
            var edge = new VisualElement { pickingMode = PickingMode.Ignore };
            edge.AddToClassList("cardframe");
            Skin.Set(edge, "panel", 46, 46, 0.45f, false);
            b.Add(edge);
            return b;
        }

        // 단약 던전 입장 (크럼블 던전 입장 화면 참고): 단계 선택, 추천 전투력, 출전 덱, 소탕 · 도전 · 연속 도전.
        // 영단은 단계 첫 클리어 보상에서만
        public static VisualElement DungeonEntry(GameApp app)
        {
            var screen = Ui.El("screen");
            screen.Add(ForgeScreens.Top(() => app.OpenContent(ContentSub.Dungeon), "단약 던전"));
            var hero = Ui.ArtBox("banner_dungeon", "[단약 던전 그림]", "hero", "short");
            var cap = Ui.El("hero-cap");
            cap.Add(Ui.Cur(Icon.Kind.Ticket, $"{Ui.N}/{Ui.N}"));
            hero.Add(cap);
            screen.Add(hero);

            var sv = LobbyScreen.Scroll();
            for (int i = 1; i <= 6; i++)
            {
                var row = Ui.El("stage-row", "plate-row");
                if (i == 1) row.AddToClassList("on");
                if (i > 2) row.AddToClassList("off");
                row.Add(Ui.Lb($"{i}", "stage-n"));
                var t = Ui.El("grow");
                t.Add(Ui.Lb($"{i}단계", "b"), Ui.Lb($"추천 전투력 {Ui.N}", "lb"));
                row.Add(t);
                row.Add(Ui.Cur(Icon.Kind.Pill, Ui.N));
                var first = Ui.Cur(Icon.Kind.Pill, Ui.N, "rare");
                first.Add(Ui.Lb("첫", "first-tag"));
                row.Add(first);
                sv.Add(row);
            }
            screen.Add(sv);

            var foot = Ui.El("entry-foot");
            var deck = Ui.El("row");
            deck.Add(Ui.Lb("출전", "lb"));
            foreach (var id in app.Game.Data.Deck.Units)
                deck.Add(app.Game.Find(id) != null ? Ui.Portrait(id, true, "holder") : Ui.El("holder", "ph"));
            deck.Add(Ui.El("grow"), Ui.Lb($"전투력 {Ui.N}", "b"));
            foot.Add(deck);
            var btns = Ui.El("row", "btnrow", "three");
            btns.Add(Ui.Btn("소탕", null, "btn"), Ui.Btn("연속 도전", null, "btn"), Ui.Btn("도전", null, "btn", "pri"));
            foot.Add(btns);
            screen.Add(foot);
            return screen;
        }

        // 무인 뽑기: 뽑기 레벨(뽑을수록 경험치, 높을수록 상급 무인 확률↑), 확률 정보. 테스트 기간이라 뽑기는 무료
        public static void Gacha(GameApp app, VisualElement body)
        {
            var sv = LobbyScreen.Scroll();
            var hero = Ui.ArtBox("gacha_hero", "[무인 뽑기 그림]", "hero", "gacha");
            var lv = Ui.El("gacha-lv");
            lv.Add(Ui.Lb($"뽑기 Lv {Ui.N}", "b"));
            var bar = Ui.Bar(0.4f, true);
            bar.AddToClassList("thick");
            lv.Add(bar);
            hero.Add(lv);
            hero.Add(Ui.Btn("확률", app.OpenGachaRates, "chip", "rates"));
            sv.Add(hero);

            var row = Ui.El("gacha-btns");
            row.Add(DrawBtn("1회", "×1", false, () => { app.LastDraw = app.Game.Draw(1); app.Save(); app.Refresh(); }));
            row.Add(DrawBtn("10회", "×10", true, () => { app.LastDraw = app.Game.Draw(10); app.Save(); app.Refresh(); }));
            sv.Add(row);

            if (app.LastDraw.Count == 1)
            {
                var r = app.LastDraw[0];
                var big = Ui.El("draw-one");
                var card = LobbyScreen.UnitCard(r.Unit, 0, 0);
                card.AddToClassList("big");
                card.Q(className: "portrait").Add(Ui.Lb(r.New ? "NEW" : "+1", r.New ? "new" : "dup"));
                big.Add(card);
                sv.Add(big);
            }
            else
            {
                var grid = Ui.El("drawgrid");
                foreach (var r in app.LastDraw)
                {
                    var cell = Ui.El("cell5");
                    var card = Ui.El("dcard");
                    card.AddToClassList("s" + GameState.Stars(r.Unit));
                    var pic = Ui.Portrait(r.Unit, true, "dface");
                    pic.Add(Ui.Lb(r.New ? "NEW" : "+1", r.New ? "new" : "dup"));
                    card.Add(pic, Ui.Lb(Catalog.Unit[r.Unit].Name, "dn"));
                    cell.Add(card);
                    grid.Add(cell);
                }
                sv.Add(grid);
            }
            body.Add(sv);
        }

        static Button DrawBtn(string name, string cost, bool pri, Action draw)
        {
            var b = new Button(draw);
            b.AddToClassList("btn");
            b.AddToClassList("drawbtn");
            if (pri) b.AddToClassList("pri");
            b.Add(Ui.Lb(name, "draw-name"));
            var c = Ui.El("row");
            c.Add(Ui.Item(Icon.Kind.Ticket, 20), Ui.Lb(cost, "draw-cost"));
            b.Add(c);
            return b;
        }

        // 확률 정보 (크럼블 확률표): 위에 등급별 합계, 아래 무인별, 맨 아래에서 레벨을 넘겨 본다
        public static VisualElement GachaRates(GameApp app)
        {
            var screen = Ui.El("screen");
            screen.Add(ForgeScreens.Top(() => app.OpenTab(LobbyTab.Gacha), "확률 정보"));
            var sv = LobbyScreen.Scroll();
            var sum = Ui.El("grid5", "rate-sum");
            foreach (int s in new[] { 5, 4, 3 })
            {
                var c = Ui.El("cell", "apt");
                if (s == 5) c.AddToClassList("first");
                c.Add(Ui.Lb($"★{s}", "b"), Ui.Lb($"{Ui.N}%", "v"));
                sum.Add(c);
            }
            sv.Add(sum);
            foreach (int s in new[] { 5, 4, 3 })
            {
                sv.Add(Ui.Band($"★{s} {GameState.Rank(s)}", "sub-band"));
                var box = Ui.El("box", "rate-box");
                foreach (var d in GameState.DrawPool.Where(u => u.Stars == s))
                {
                    var r = Ui.El("tr");
                    r.Add(Ui.Lb(d.Name, "c1"), Ui.Lb($"{Ui.N}%", "cn"));
                    box.Add(r);
                }
                sv.Add(box);
            }
            screen.Add(sv);
            var foot = Ui.El("row", "lvnav");
            foot.Add(Ui.IconBtn(Icon.Kind.Prev, null, 20, "sq"), Ui.Lb($"Lv {Ui.N}", "lv-big"), Ui.IconBtn(Icon.Kind.Next, null, 20, "sq"));
            screen.Add(foot);
            return screen;
        }
    }
}
