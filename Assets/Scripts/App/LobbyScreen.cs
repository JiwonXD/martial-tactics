using System;
using System.Collections.Generic;
using System.Linq;
using MartialTactics.Battle;
using MartialTactics.Game;
using UnityEngine.UIElements;

namespace MartialTactics.App
{
    /// <summary>
    /// 로비 F안 (2026-10-29, 쿠키런: 크럼블 구조) + 화풍 A. 화면 시안 캔버스의 LobbyFIdle / StyleA.
    /// 기본은 방치 전투 자리 + 덱 3명, 하단 바 가운데 대장간. 탭을 열면 패널이 화면 전체를 쓰고 가운데가 닫기(✕)로 바뀐다.
    /// 하단 바는 아이콘만, 고른 칸에만 이름.
    /// </summary>
    static class LobbyScreen
    {
        static readonly (LobbyTab tab, Icon.Kind icon, string name)[] Tabs =
        {
            (LobbyTab.Units, Icon.Kind.Units, "무인"),
            (LobbyTab.Growth, Icon.Kind.Growth, "성장"),
            (LobbyTab.None, Icon.Kind.Close, ""),
            (LobbyTab.Content, Icon.Kind.Content, "컨텐츠"),
            (LobbyTab.Gacha, Icon.Kind.Gacha, "뽑기"),
        };

        static readonly (UnitsSub sub, string name)[] Subs =
        {
            (UnitsSub.List, "목록"), (UnitsSub.LevelUp, "레벨업"), (UnitsSub.Gear, "장비"), (UnitsSub.Deck, "덱 편성"),
        };

        public static VisualElement Build(GameApp app)
        {
            var screen = Ui.El("screen", "lobby");
            if (app.Tab == LobbyTab.None) Idle(app, screen);
            else Panel(app, screen);
            screen.Add(NavBar(app));
            if (app.Tab == LobbyTab.None) screen.Add(Forge());
            if (app.SettingsOpen) screen.Add(Settings(app));
            return screen;
        }

        // --- 기본 화면 ---

        static void Idle(GameApp app, VisualElement screen)
        {
            var stage = Ui.El("stage");
            var bg = Art.Load("Art/lobby_bg");
            if (bg != null) stage.style.backgroundImage = new StyleBackground(bg);

            var deck = app.Game.Data.Deck.Units;
            var top = Ui.El("ltop");
            var avatar = Ui.Portrait(app.Game.Find(deck[0]) != null ? deck[0] : null, true, "avatar");
            top.Add(avatar, Ui.El("grow"), Ui.IconBtn(Icon.Kind.Gear, () => { app.SettingsOpen = true; app.Refresh(); }, 22, "sq"));
            stage.Add(top);

            // 방치 보상·단계는 규칙이 아직 없어 자리만. 도전은 지금 있는 대전으로 이어 둔다
            var act = Ui.El("act");
            var reward = Ui.IconBtn(Icon.Kind.Chest, null, 26, "reward");
            reward.Add(Ui.El("dot"));
            var challenge = Ui.Btn("도전", app.OpenBattleReady, "challenge");
            act.Add(reward, challenge);
            stage.Add(act);
            stage.Add(Ui.Lb("[방치 전투]", "idle-tag"));

            // 덱 3명: 왼쪽부터 주장 · 부장 · 부장
            var party = Ui.El("party");
            for (int pos = 0; pos < 3; pos++)
            {
                int p = pos;
                var u = app.Game.Find(deck[pos]);
                var col = Ui.El("pt");
                if (pos == 0) col.AddToClassList("leader");
                VisualElement card;
                if (u != null)
                {
                    card = Ui.Portrait(u.Id, false, "pcard");
                    col.Add(card, Ui.Lb(Catalog.Unit[u.Id].Name, "pname"));
                }
                else
                {
                    card = Ui.El("pcard", "empty");
                    card.Add(new Icon(Icon.Kind.Plus, 28));
                    col.Add(card, Ui.Lb(" ", "pname"));
                }
                card.Add(Ui.Lb(BattleRules.PosNames[pos], "pos-seal"));
                col.RegisterCallback<ClickEvent>(_ =>
                {
                    if (u != null) app.OpenUnit(u.Id);
                    else app.OpenDeckPick(p);
                });
                party.Add(col);
            }
            stage.Add(party);
            screen.Add(stage);
        }

        static VisualElement Forge()
        {
            // 장비 뽑기 규칙(철광석·뽑기 레벨)이 아직 게임에 없어 버튼 모양만
            var wrap = Ui.El("forge-wrap");
            wrap.pickingMode = PickingMode.Ignore;
            var ring = Ui.El("forge-ring");
            var forge = new Button { };
            forge.AddToClassList("forge");
            forge.Add(new Icon(Icon.Kind.Forge, 44));
            forge.Add(Ui.Lb("대장간", "forge-name"));
            ring.Add(forge);
            var gear = Ui.IconBtn(Icon.Kind.Gear, null, 16, "forge-gear");
            ring.Add(gear);
            wrap.Add(ring);
            return wrap;
        }

        static VisualElement NavBar(GameApp app)
        {
            var nav = Ui.El("nav");
            foreach (var t in Tabs)
            {
                if (t.tab == LobbyTab.None)
                {
                    if (app.Tab == LobbyTab.None) nav.Add(Ui.El("tb", "spacer"));
                    else nav.Add(Ui.IconBtn(Icon.Kind.Close, app.CloseTab, 26, "tb", "x"));
                    continue;
                }
                var tab = t.tab;
                var b = new Button(() => app.OpenTab(tab));
                b.AddToClassList("tb");
                bool on = app.Tab == tab;
                b.Add(new Icon(t.icon, on ? 22 : 26));
                if (on)
                {
                    b.AddToClassList("on");
                    b.Add(Ui.Lb(t.name, "tbn"));
                }
                nav.Add(b);
            }
            return nav;
        }

        static VisualElement Settings(GameApp app)
        {
            var dim = Ui.El("modal");
            dim.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target != dim) return;
                app.SettingsOpen = app.ConfirmReset = false;
                app.Refresh();
            });
            var sheet = Ui.El("sheet");
            sheet.Add(Ui.Lb("설정", "sheet-title"));
            if (!app.ConfirmReset)
            {
                sheet.Add(Ui.Btn("저장 초기화", () => { app.ConfirmReset = true; app.Refresh(); }, "btn"));
            }
            else
            {
                sheet.Add(Ui.Lb("보유 무인 · 무공 · 덱 삭제", "warn"));
                var row = Ui.El("row");
                var no = Ui.Btn("취소", () => { app.ConfirmReset = false; app.Refresh(); }, "btn");
                var yes = Ui.Btn("초기화", app.ResetSave, "btn", "danger");
                no.style.flexGrow = 1;
                yes.style.flexGrow = 1;
                yes.style.marginLeft = 8;
                row.Add(no, yes);
                sheet.Add(row);
            }
            sheet.Add(Ui.Btn("닫기", () => { app.SettingsOpen = app.ConfirmReset = false; app.Refresh(); }, "btn", "flat"));
            dim.Add(sheet);
            return dim;
        }

        // --- 탭 패널 ---

        static void Panel(GameApp app, VisualElement screen)
        {
            var head = Ui.El("phead");
            string title = Tabs.First(t => t.tab == app.Tab).name;
            if (app.Tab == LobbyTab.Growth) title = "폐관수련";
            head.Add(Ui.Lb(title, "ptitle"));
            screen.Add(head);

            var body = Ui.El("pbody");
            switch (app.Tab)
            {
                case LobbyTab.Units:
                    switch (app.Sub)
                    {
                        case UnitsSub.List: UnitList(app, body); break;
                        case UnitsSub.Deck: DeckEdit(app, body); break;
                        case UnitsSub.LevelUp: body.Add(Fill("[무인 레벨업]")); break;
                        case UnitsSub.Gear: body.Add(Fill("[장비]")); break;
                    }
                    break;
                case LobbyTab.Growth: body.Add(Fill("[폐관수련]")); break;
                case LobbyTab.Content: Content(app, body); break;
                case LobbyTab.Gacha: Gacha(app, body); break;
            }
            screen.Add(body);

            if (app.Tab == LobbyTab.Units)
            {
                var subs = Ui.El("subs");
                foreach (var s in Subs)
                {
                    var sub = s.sub;
                    var b = Ui.Btn(s.name, () => app.OpenSub(sub), "sub");
                    if (app.Sub == sub) b.AddToClassList("on");
                    subs.Add(b);
                }
                screen.Add(subs);
            }
        }

        static VisualElement Fill(string text)
        {
            var e = Ui.Placeholder(text, -1, -1, "fill-ph");
            return e;
        }

        static ScrollView Scroll()
        {
            var sv = new ScrollView(ScrollViewMode.Vertical) { verticalScrollerVisibility = ScrollerVisibility.Hidden };
            sv.AddToClassList("pscroll");
            return sv;
        }

        // 무인 목록: 3열 카드. 카드 구석 숫자 = 중복 카드 수
        static void UnitList(GameApp app, VisualElement body)
        {
            var units = app.Game.Data.Units;
            var flt = Ui.El("flt");
            foreach (int stars in new[] { 0, 5, 4, 3 })
            {
                int s = stars;
                var chip = Ui.Btn(s == 0 ? "전체" : $"★{s}", () => { app.StarFilter = s; app.Refresh(); }, "chip");
                if (app.StarFilter == s) chip.AddToClassList("on");
                flt.Add(chip);
            }
            flt.Add(Ui.El("grow"), Ui.Lb($"{units.Count}", "count"));
            body.Add(flt);

            var sv = Scroll();
            if (units.Count == 0)
            {
                var empty = Ui.El("empty-box");
                empty.Add(Ui.Btn("뽑기", () => app.OpenTab(LobbyTab.Gacha), "btn", "pri"));
                sv.Add(empty);
            }
            var deck = app.Game.Data.Deck.Units;
            var grid = Ui.El("grid");
            foreach (var u in units
                         .Where(x => app.StarFilter == 0 || GameState.Stars(x.Id) == app.StarFilter)
                         .OrderByDescending(x => GameState.Stars(x.Id))
                         .ThenByDescending(x => x.Promotions))
            {
                var cell = Ui.El("cell3");
                var card = UnitCard(u.Id, u.Promotions, u.Spare);
                int at = Array.IndexOf(deck, u.Id);
                if (at >= 0) card.Q(className: "portrait").Add(Ui.Lb(BattleRules.PosNames[at], "inDeck"));
                string id = u.Id;
                card.RegisterCallback<ClickEvent>(_ => app.OpenUnit(id));
                cell.Add(card);
                grid.Add(cell);
            }
            sv.Add(grid);
            body.Add(sv);
        }

        public static VisualElement UnitCard(string id, int promotions, int spare)
        {
            var d = Catalog.Unit[id];
            var card = Ui.El("ucard");
            int stars = GameState.Stars(id);
            card.AddToClassList("s" + stars);
            var pic = Ui.Portrait(id, false, "upic");
            if (spare > 0) pic.Add(Ui.Lb($"+{spare}", "spare"));
            card.Add(pic);
            var cap = Ui.El("ucap");
            cap.Add(Ui.Lb(Ui.Stars(id, promotions), "sr"), Ui.Lb(d.Name, "un"));
            card.Add(cap);
            return card;
        }

        // 덱 편성: 속성 + 세 칸 (왼쪽부터 주장 · 부장 · 부장)
        static void DeckEdit(GameApp app, VisualElement body)
        {
            var deck = app.Game.Data.Deck;
            var sv = Scroll();

            var el = Ui.El("elements");
            foreach (var e in BattleRules.Elements)
            {
                string elem = e;
                var b = Ui.Btn(e, () => { deck.Element = elem; app.Save(); app.Refresh(); }, "elem");
                if (deck.Element == e) b.AddToClassList("on");
                el.Add(b);
            }
            sv.Add(el);

            int ei = Array.IndexOf(BattleRules.Elements, deck.Element);
            var slots = Ui.El("slots");
            for (int pos = 0; pos < 3; pos++)
            {
                int p = pos;
                var u = app.Game.Find(deck.Units[pos]);
                var col = Ui.El("dslot");
                if (pos == 0) col.AddToClassList("leader");
                col.Add(Ui.Lb(BattleRules.PosNames[pos], "dpos"));
                if (u != null)
                {
                    var d = Catalog.Unit[u.Id];
                    var pic = Ui.Portrait(u.Id, false, "dpic");
                    if (ei >= 0) pic.Add(Ui.Lb(d.Apt[ei].ToString(), "apt-badge"));
                    pic.Add(Ui.IconBtn(Icon.Kind.Close, () => { deck.Units[p] = ""; app.Save(); app.Refresh(); }, 14, "dremove"));
                    col.Add(pic, Ui.Lb(Ui.Stars(u.Id, u.Promotions), "sr"), Ui.Lb(d.Name, "un"));
                    var skills = new[] { d.Skill }.Concat(u.Equipped.Select(uid => app.Game.FindSkill(uid)?.Skill).Where(id => id != null));
                    foreach (var s in skills) col.Add(Ui.Lb(Catalog.Skill[s].Name, "dskill"));
                }
                else
                {
                    var pic = Ui.El("dpic", "empty");
                    pic.Add(new Icon(Icon.Kind.Plus, 30));
                    col.Add(pic);
                }
                col.RegisterCallback<ClickEvent>(e =>
                {
                    if (e.target is VisualElement t && t.ClassListContains("dremove")) return;
                    app.OpenDeckPick(p);
                });
                slots.Add(col);
            }
            sv.Add(slots);

            var errors = app.Game.ValidateDeck();
            var foot = Ui.El("deckfoot");
            foreach (var e in errors) foot.Add(Ui.Lb(e, "err"));
            var go = Ui.Btn("대전", app.OpenBattleReady, "btn", "pri");
            go.SetEnabled(errors.Count == 0);
            foot.Add(go);
            sv.Add(foot);
            body.Add(sv);
        }

        // 컨텐츠: 대전 묶음 배너 목록. 지금 돌아가는 건 대전(임시 상대)뿐
        static void Content(GameApp app, VisualElement body)
        {
            var sv = Scroll();
            sv.Add(Banner("대전", "對戰", app.OpenBattleReady));
            sv.Add(Banner("순위전", "順位", null));
            sv.Add(Banner("위너스", "連戰", null));
            sv.Add(Banner("단약 던전", "丹藥", null));
            body.Add(sv);
        }

        static VisualElement Banner(string name, string seal, Action open)
        {
            var b = Ui.El("banner-card");
            if (open == null) b.AddToClassList("locked");
            else b.RegisterCallback<ClickEvent>(_ => open());
            var art = Ui.Placeholder("[배너 그림]", -1, -1, "banner-art");
            b.Add(art);
            var text = Ui.El("banner-text");
            text.Add(Ui.Lb(name, "banner-name"));
            b.Add(text);
            b.Add(Ui.Lb(seal, "banner-seal"));
            return b;
        }

        // 뽑기: 테스트 기간이라 무료. 결과는 카드로
        static void Gacha(GameApp app, VisualElement body)
        {
            var sv = Scroll();
            var hero = Ui.Placeholder("[무인 뽑기 그림]", -1, 220, "gacha-art");
            sv.Add(hero);
            var row = Ui.El("gacha-btns");
            var one = Ui.Btn("1회", () => { app.LastDraw = app.Game.Draw(1); app.Save(); app.Refresh(); }, "btn");
            var ten = Ui.Btn("10회", () => { app.LastDraw = app.Game.Draw(10); app.Save(); app.Refresh(); }, "btn", "pri");
            row.Add(one, ten);
            sv.Add(row);

            // 1회는 한 장을 크게
            if (app.LastDraw.Count == 1)
            {
                var r = app.LastDraw[0];
                var big = Ui.El("draw-one");
                var card = LobbyScreen.UnitCard(r.Unit, 0, 0);
                card.AddToClassList("big");
                card.Q(className: "portrait").Add(Ui.Lb(r.New ? "NEW" : "+1", r.New ? "new" : "dup"));
                big.Add(card);
                sv.Add(big);
                body.Add(sv);
                return;
            }
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
            body.Add(sv);
        }
    }
}
