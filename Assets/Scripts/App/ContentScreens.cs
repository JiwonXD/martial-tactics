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
            sv.Add(Banner("banner_pvp", "대전", null, app.OpenBattleReady));
            sv.Add(Banner("banner_rank", "엔트리 순위전", "준비 중", null));
            sv.Add(Banner("banner_winners", "위너스 연전", "준비 중", null));
            body.Add(sv);
        }

        // 일일 던전 (크럼블 던전 목록): 배너 카드를 세로로 쌓고, 아래에 입장권 충전 시간
        public static void Dungeons(GameApp app, VisualElement body)
        {
            var sv = LobbyScreen.Scroll();
            var b = Banner("banner_dungeon", "단약 던전", $"{Ui.N}단계", app.OpenDungeon);
            var count = Ui.El("banner-count");
            count.Add(Ui.Lb($"{Ui.N}/{Ui.N}", "bc-t"));
            b.Add(count);
            var icon = Ui.El("banner-icon");
            icon.Add(Ui.Item(Icon.Kind.Pill, 34));
            b.Add(icon);
            sv.Add(b);
            body.Add(sv);
            var charge = Ui.El("charge");
            charge.Add(Ui.Item(Icon.Kind.Clock, 16), Ui.Lb($"입장권 충전까지 {Ui.N}", "charge-t"));
            body.Add(charge);
        }

        static VisualElement Banner(string art, string name, string sub, Action open)
        {
            var b = Ui.ArtBox(art, "[배너 그림]", "banner-card");
            if (open == null) b.AddToClassList("locked");
            else b.RegisterCallback<ClickEvent>(_ => open());
            var t = Ui.El("banner-title");
            t.Add(Ui.Lb(name, "bt-name"));
            if (sub != null) t.Add(Ui.Lb(sub, "bt-sub"));
            b.Add(t);
            return b;
        }

        // 단약 던전 입장 (크럼블 연구석 던전): 위 그림 + 제목 띠 + 단계 선택(− [n] +) · 추천 전투력 · 달성 보상,
        // 아래 패널에 출전 덱 · 전투력 · 소탕 / 도전하기 / 연속 도전. 영단은 단계 첫 클리어 보상(달성 보상)에서만
        public static VisualElement DungeonEntry(GameApp app)
        {
            var screen = Ui.El("screen");
            var hero = Ui.ArtBox("banner_dungeon", "[단약 던전 그림]", "entry-art");
            hero.Add(Ui.IconBtn(Icon.Kind.Back, () => app.OpenContent(ContentSub.Dungeon), 22, "back", "float"));
            var ribbon = Ui.El("ribbon");
            ribbon.Add(Ui.Lb("단약 던전", "ribbon-t"));
            hero.Add(ribbon);
            var pick = Ui.El("stage-pick");
            pick.Add(Ui.Lb($"추천 전투력 {Ui.N}", "sp-pow"), Ui.Lb("단계 선택", "sp-label"));
            var step = Ui.El("row", "sp-step");
            step.Add(Ui.Btn("−", null, "sp-btn", "minus"), Ui.Lb(Ui.N, "sp-n"), Ui.Btn("+", null, "sp-btn"));
            pick.Add(step);
            hero.Add(pick);
            var reward = Ui.El("entry-reward");
            reward.Add(Ui.Item(Icon.Kind.Pill, 40), Ui.Lb(Ui.N, "er-n"));
            hero.Add(reward);
            var goal = new Button();
            goal.AddToClassList("goal-btn");
            goal.Add(Ui.Item(Icon.Kind.Chest, 34), Ui.Lb("달성 보상", "goal-t"));
            hero.Add(goal);
            screen.Add(hero);

            var panel = Ui.El("entry-panel");
            var head = Ui.El("row", "ep-head");
            head.Add(Ui.Lb($"전투력 {Ui.N}", "ep-pow"), Ui.El("grow"), Ui.Btn("편성하기", app.BackToDeck, "btn", "sec", "small"));
            panel.Add(head);
            var deck = Ui.El("ep-deck");
            for (int i = 0; i < 3; i++)
            {
                var id = app.Game.Data.Deck.Units[i];
                var card = app.Game.Find(id) != null ? Ui.Portrait(id, true, "ep-card") : Ui.El("ep-card", "empty");
                if (app.Game.Find(id) != null)
                {
                    card.Add(Ui.Lb($"Lv {Ui.N}", "lv"));
                    card.Add(Skin.CardFrame(i == 0));
                }
                card.Add(Ui.Lb(BattleRules.PosNames[i], "pos-seal"));
                deck.Add(card);
            }
            panel.Add(deck);
            var btns = Ui.El("row", "ep-btns");
            btns.Add(Ui.Btn("소탕", null, "btn", "sec"));
            var go = new Button();
            go.AddToClassList("btn");
            go.AddToClassList("pri");
            go.AddToClassList("go");
            go.Add(Ui.Lb("도전하기!", "go-t"), Ui.Lb($"{Ui.N}/{Ui.N}", "go-n"));
            btns.Add(go);
            var cont = new Button();
            cont.AddToClassList("check-btn");
            cont.Add(Ui.El("checkbox"), Ui.Lb("연속 도전", "chk-t"));
            btns.Add(cont);
            panel.Add(btns);
            screen.Add(panel);
            return screen;
        }

        // 무인 뽑기 (크럼블 쿠키 뽑기): 그림이 화면을 채우고, 위에 확률 정보 · 뽑기 레벨, 아래 패널에 1회 / 10회.
        // 뽑기 레벨은 뽑을수록 경험치, 높을수록 상급 무인 확률↑. 테스트 기간이라 뽑기는 무료
        public static void Gacha(GameApp app, VisualElement body)
        {
            var art = Ui.ArtBox("gacha_hero", "[무인 뽑기 그림]", "gacha-art");
            var tools = Ui.El("row", "gacha-tools");
            tools.Add(Ui.Btn("확률 정보", app.OpenGachaRates, "chip", "dark"));
            art.Add(tools);
            var lv = Ui.El("gacha-lv");
            lv.Add(Ui.Lb("무인 뽑기 레벨", "gl-label"));
            var row = Ui.El("row");
            row.Add(Ui.Lb($"Lv {Ui.N}", "gl-lv"));
            var bar = Ui.Bar(0.4f, false);
            bar.AddToClassList("gl-bar");
            row.Add(bar);
            lv.Add(row);
            art.Add(lv);
            body.Add(art);

            var panel = Ui.El("gacha-panel");
            var btns = Ui.El("row", "gacha-btns");
            // 주 버튼은 하나: 10회만 주홍, 1회는 보조
            var one = Ui.CostBtn("1회", Icon.Kind.Ticket, Ui.N, false, () => { app.LastDraw = app.Game.Draw(1); app.Save(); app.Refresh(); });
            var ten = Ui.CostBtn("10회", Icon.Kind.Ticket, Ui.N, true, () => { app.LastDraw = app.Game.Draw(10); app.Save(); app.Refresh(); });
            one.AddToClassList("drawbtn");
            ten.AddToClassList("drawbtn");
            btns.Add(one, ten);
            panel.Add(btns);
            body.Add(panel);

            if (app.LastDraw.Count > 0) body.Add(DrawResult(app));
        }

        static Button DrawBtn(string name, Action draw)
        {
            var b = new Button(draw);
            b.AddToClassList("btn");
            b.AddToClassList("pri");
            b.AddToClassList("drawbtn");
            b.Add(Ui.Lb(name, "draw-name"));
            var c = Ui.El("row", "draw-cost");
            c.Add(Ui.Item(Icon.Kind.Ticket, 18), Ui.Lb(Ui.N, "dc-t"));
            b.Add(c);
            return b;
        }

        // 뽑기 결과는 그림 위에 겹치는 창
        static VisualElement DrawResult(GameApp app)
        {
            var dim = Ui.El("modal");
            var sheet = Ui.El("sheet");
            var head = Ui.El("sheet-head");
            head.Add(Ui.Lb("뽑기 결과", "sheet-title"));
            sheet.Add(head);
            var grid = Ui.El("drawgrid");
            foreach (var r in app.LastDraw)
            {
                var cell = Ui.El(app.LastDraw.Count == 1 ? "cell-one" : "cell5");
                var card = Ui.Portrait(r.Unit, true, "dcard");
                card.Add(Ui.Lb(r.New ? "NEW" : "+1", r.New ? "new" : "dup"));
                card.Add(Skin.CardFrame(GameState.Stars(r.Unit) >= 5));
                cell.Add(card, Ui.Lb(Catalog.Unit[r.Unit].Name, "dn"));
                grid.Add(cell);
            }
            sheet.Add(grid);
            var foot = Ui.El("sheet-foot");
            foot.Add(Ui.Btn("확인", () => { app.LastDraw.Clear(); app.Refresh(); }, "btn", "pri", "wide"));
            sheet.Add(foot);
            dim.Add(sheet);
            return dim;
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
