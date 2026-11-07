using System;
using System.Linq;
using MartialTactics.Battle;
using MartialTactics.Game;
using UnityEngine.UIElements;

namespace MartialTactics.App
{
    /// <summary>
    /// 로비 F안 (2026-10-29, 쿠키런: 크럼블 구조) + 화풍 A. 화면 시안 캔버스의 LobbyFIdle / StyleA.
    /// 기본은 방치 스테이지 + 덱 3명, 하단 바 가운데 대장간. 탭을 열면 패널이 화면 전체를 쓰고 가운데가 닫기(✕)로 바뀐다.
    /// 하단 바는 아이콘만, 고른 칸에만 이름. 수치가 정해지지 않은 곳은 [n].
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
            if (app.Tab == LobbyTab.None)
            {
                Idle(app, screen);
                screen.Add(ForgePanel(app));
            }
            else Panel(app, screen);
            screen.Add(NavBar(app, app.Tab == LobbyTab.None ? NavCenter.Forge : NavCenter.Close, null));
            if (app.Tab == LobbyTab.None) screen.Add(ForgeObject(app));
            switch (app.Sheet)
            {
                case LobbySheet.Settings: screen.Add(Settings(app)); break;
                case LobbySheet.IdleReward: screen.Add(IdleReward(app)); break;
                case LobbySheet.ForgeAuto: screen.Add(ForgeScreens.AutoSheet(app)); break;
                case LobbySheet.ForgeResult: screen.Add(ForgeScreens.ResultSheet(app)); break;
            }
            return screen;
        }

        /// <summary>머리줄 재화: 레벨업 재화 · 골드 · 뽑기권 (방치 보상으로 얻는 것 중 로비에 늘 보이는 셋).</summary>
        static VisualElement Currencies()
        {
            var row = Ui.El("curs");
            row.Add(Ui.Cur(Icon.Kind.Book, Ui.N), Ui.Cur(Icon.Kind.Coin, Ui.N), Ui.Cur(Icon.Kind.Ticket, Ui.N));
            return row;
        }

        // --- 기본 화면 ---

        static void Idle(GameApp app, VisualElement screen)
        {
            var stage = Ui.El("stage");
            var bg = Art.Load("Art/lobby_bg");
            if (bg != null) stage.style.backgroundImage = new StyleBackground(bg);

            stage.Add(TopBand(app));

            // 무대 위 HUD 띠: 단계 진행 + 단계 도전 (하늘에 따로 띄우지 않는다). 상대는 아직 임시(무작위)라 대전 준비로 이어 둔다
            var hud = Ui.El("stage-hud");
            var prog = Ui.El("sh-prog");
            prog.Add(Ui.Lb($"{Ui.N}단계", "sh-stage"));
            var bar = Ui.Bar(0.6f, false);
            bar.AddToClassList("sh-bar");
            prog.Add(bar);
            hud.Add(prog);
            var challenge = new Button(app.OpenBattleReady);
            challenge.AddToClassList("challenge");
            challenge.Add(Ui.Lb("단계 도전", "ch-t"));
            hud.Add(challenge);
            stage.Add(hud);

            // 덱 3명이 쉬지 않고 싸우는 무대
            var live = new LobbyStage(app.Game);
            app.Stage = live;
            stage.Add(live.Root);

            // 왼쪽 세로 줄: 방치 보상
            var side = Ui.El("side-col");
            var reward = new Button(() => app.OpenSheet(LobbySheet.IdleReward));
            reward.AddToClassList("round");
            reward.Add(Ui.Item(Icon.Kind.Chest, 56));
            reward.Add(Ui.El("dot"));
            side.Add(reward);
            stage.Add(side);
            screen.Add(stage);
        }

        // 대장간 패널 (크럼블 오븐 패널 자리): 가운데 대장간, 왼쪽 주장, 오른쪽 부장 둘 (왼쪽부터 주장 · 부장 · 부장).
        // 대장간: 누르면 한 번 뽑기, 위 Lv = 레벨 창, 오른쪽 위 = 남은 철광석, 왼쪽 = 쌓인 장비(결과 창), 왼쪽 아래 톱니 = 자동 뽑기.
        // 장비 데이터가 아직 없어 뽑기는 아무것도 하지 않는다
        static VisualElement ForgePanel(GameApp app)
        {
            var panel = Ui.El("forge-panel");
            var deck = app.Game.Data.Deck.Units;
            var left = Ui.El("fp-side");
            left.Add(DeckCard(app, 0));
            var right = Ui.El("fp-side");
            right.Add(DeckCard(app, 1), DeckCard(app, 2));

            var mid = Ui.El("fp-mid"); // 대장간 그림 자리 (ForgeObject가 겹쳐 그린다)

            panel.Add(left, mid, right);
            return panel;
        }

        // 대장간 (크럼블 오븐처럼 그림 한 덩어리): 패널 가운데에 앉고 아랫부분이 하단 바 가운데 칸에 걸친다.
        // 누르면 한 번 뽑기, 위 Lv = 레벨 창, 오른쪽 위 = 남은 철광석, 왼쪽 = 쌓인 장비(결과 창), 왼쪽 아래 Auto = 자동 뽑기.
        // 장비 데이터가 아직 없어 뽑기는 아무것도 하지 않는다
        static VisualElement ForgeObject(GameApp app)
        {
            var wrap = Ui.El("forge-wrap");
            wrap.pickingMode = PickingMode.Ignore;
            var box = Ui.El("forge-box");
            var forge = new Button(() => UiRoot.Current?.Toast("장비 데이터 준비 중"));
            forge.AddToClassList("forge-art");
            var tex = Art.Load("UI/Art/forge_obj");
            if (tex != null) forge.style.backgroundImage = new StyleBackground(tex);
            else forge.Add(Ui.Item(Icon.Kind.Forge, 72));
            box.Add(forge);
            var lv = new Button(app.OpenForgeLevel) { text = $"Lv {Ui.N}" };
            lv.AddToClassList("forge-lv");
            box.Add(lv);
            var ore = Ui.El("forge-ore");
            ore.Add(Ui.Item(Icon.Kind.Ore, 16), Ui.Lb(Ui.N, "fo-n"));
            box.Add(ore);
            var stack = new Button(app.OpenForgeResult) { text = $"{Ui.N}개" };
            stack.AddToClassList("forge-stack");
            box.Add(stack);
            var auto = new Button(() => app.OpenSheet(LobbySheet.ForgeAuto));
            auto.AddToClassList("forge-auto");
            auto.Add(Ui.Lb("Auto", "fa-t"));
            box.Add(auto);
            wrap.Add(box);
            return wrap;
        }

        static VisualElement DeckCard(GameApp app, int pos)
        {
            var u = app.Game.Find(app.Game.Data.Deck.Units[pos]);
            var col = Ui.El("pt");
            if (pos == 0) col.AddToClassList("leader");
            VisualElement card;
            if (u != null)
            {
                card = Ui.Portrait(u.Id, true, "pcard");
                card.Add(Ui.Lb($"Lv {Ui.N}", "lv"));
                card.Add(Skin.CardFrame(pos == 0));
                col.Add(card, Ui.Bar(1, false));
            }
            else
            {
                card = Ui.El("pcard", "empty");
                card.Add(new Icon(Icon.Kind.Plus, 24));
                col.Add(card);
            }
            card.Add(Ui.Lb(BattleRules.PosNames[pos], "pos-seal"));
            col.RegisterCallback<ClickEvent>(_ =>
            {
                if (u != null) app.OpenUnit(u.Id);
                else app.OpenDeckPick(pos);
            });
            return col;
        }

        /// <summary>하단 바 가운데 칸 상태 (크럼블): 로비 = 대장간, 메뉴 1단 = 닫기 X, 그 안으로 더 들어가면 = 한 단계 뒤로 ↩.</summary>
        public enum NavCenter { Forge, Close, Back }

        public static VisualElement NavBar(GameApp app, NavCenter center, Action back)
        {
            var nav = Ui.El("nav");
            foreach (var t in Tabs)
            {
                // 다섯 칸의 너비·아이콘 크기·위치는 어느 화면에서나 같다. 고른 칸은 아이콘이 올라가고 아래 이름만 붙는다
                if (t.tab == LobbyTab.None)
                {
                    var c = Ui.El("tb", "center");
                    if (center == NavCenter.Close) c.Add(Ui.IconBtn(Icon.Kind.Close, app.CloseTab, 26, "x"));
                    else if (center == NavCenter.Back) c.Add(Ui.IconBtn(Icon.Kind.Back, back, 26, "x", "back-x"));
                    nav.Add(c);
                    continue;
                }
                var tab = t.tab;
                var b = new Button(() => app.OpenTab(tab));
                b.AddToClassList("tb");
                bool on = app.Tab == tab;
                var medal = Ui.El("medal");
                medal.Add(Ui.Item(t.icon, 48));
                b.Add(medal);
                var name = Ui.Lb(t.name, "tbn");
                if (on) b.AddToClassList("on");
                else name.style.visibility = Visibility.Hidden;
                b.Add(name);
                nav.Add(b);
            }
            return nav;
        }

        /// <summary>위 띠 (모든 화면 같음): 왼쪽 프로필 묶음(주장 얼굴 · Lv · 전투력), 오른쪽 재화와 설정.</summary>
        public static VisualElement TopBand(GameApp app)
        {
            var deck = app.Game.Data.Deck.Units;
            var top = Ui.El("ltop");
            var prof = Ui.El("profile");
            prof.Add(Ui.Portrait(app.Game.Find(deck[0]) != null ? deck[0] : null, true, "avatar"));
            var pinfo = Ui.El("pinfo");
            pinfo.Add(Ui.Lb($"Lv {Ui.N}", "plv"), Ui.Lb($"전투력 {Ui.N}", "ppow"));
            prof.Add(pinfo);
            top.Add(prof, Ui.El("grow"), Currencies(), Ui.IconBtn(Icon.Kind.Gear, () => app.OpenSheet(LobbySheet.Settings), 20, "sq"));
            return top;
        }

        /// <summary>패널 제목 띠: 화면 이름은 위 띠가 아니라 여기에.</summary>
        static VisualElement TitleBand(string title)
        {
            var band = Ui.El("title-band");
            band.Add(Ui.Lb(title, "tb-title"));
            return band;
        }

        /// <summary>
        /// 메뉴 안쪽 화면(무인 상세, 승급, 대전 준비 등)을 위 띠 + 제목 띠 + 내용 + 하단 바(가운데 ↩)로 감싼다.
        /// 왼쪽 위 뒤로 버튼은 쓰지 않는다.
        /// </summary>
        public static VisualElement Shell(GameApp app, string title, VisualElement content, Action back)
        {
            var screen = Ui.El("screen", "shell");
            screen.Add(TopBand(app));
            if (title != null) screen.Add(TitleBand(title));
            content.AddToClassList("shell-body");
            // 내용 화면이 따로 그리던 머리줄·뒤로 버튼은 걷어낸다
            content.Query(className: "topbar").ForEach(e => e.RemoveFromHierarchy());
            content.Query(className: "float").ForEach(e => { if (e.ClassListContains("back")) e.RemoveFromHierarchy(); });
            screen.Add(content);
            screen.Add(NavBar(app, NavCenter.Back, back));
            if (app.Sheet == LobbySheet.Settings) screen.Add(Settings(app));
            return screen;
        }

        public static VisualElement Sheet(GameApp app, string title, out VisualElement body)
        {
            var dim = Ui.El("modal");
            dim.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target != dim) return;
                app.OpenSheet(LobbySheet.None);
            });
            var sheet = Ui.El("sheet");
            // 크럼블 창 머리: 가운데 제목, 오른쪽 닫기
            var head = Ui.El("sheet-head");
            head.Add(Ui.Lb(title, "sheet-title"), Ui.IconBtn(Icon.Kind.Close, () => app.OpenSheet(LobbySheet.None), 18, "sheet-x"));
            sheet.Add(head);
            body = Ui.El("sheet-body");
            sheet.Add(body);
            dim.Add(sheet);
            return dim;
        }

        static VisualElement Settings(GameApp app)
        {
            var dim = Sheet(app, "설정", out var body);
            body.Add(Ui.Btn("저장 초기화", () => Popup.Confirm("저장 초기화", "보유 무인 · 무공 · 덱 삭제", "초기화", app.ResetSave), "btn"));
            return dim;
        }

        // 방치 보상 (기획서 2026-10-28~29): 레벨업 재화 · 골드 · 뽑기권 · 장비 강화 재료 · 철광석 · 단약.
        // 쌓이는 최대 시간과 양은 폐관수련 특별 수련으로 늘어난다
        static VisualElement IdleReward(GameApp app)
        {
            var dim = Sheet(app, "방치 보상", out var body);
            var time = Ui.El("idle-time");
            time.Add(Ui.Item(Icon.Kind.Clock, 26), Ui.Lb($"{Ui.N}:{Ui.N}", "it-n"), Ui.El("grow"), Ui.Lb($"최대 {Ui.N}시간", "lb"));
            body.Add(time);
            var grid = Ui.El("rewards");
            foreach (var (icon, art, name) in new[]
                     {
                         (Icon.Kind.Book, "book", "레벨업 재화"), (Icon.Kind.Coin, "coin", "골드"), (Icon.Kind.Ticket, "ticket", "뽑기권"),
                         (Icon.Kind.Gear, "whet", "강화 재료"), (Icon.Kind.Ore, "ore", "철광석"), (Icon.Kind.Pill, "pill", "단약"),
                     })
            {
                var cell = Ui.El("rw");
                cell.Add(Ui.Tile(art, icon, Ui.N), Ui.Lb(name, "rw-name"));
                grid.Add(cell);
            }
            body.Add(grid);
            var get = Ui.Btn("받기", () =>
            {
                app.OpenSheet(LobbySheet.None);
                Popup.Reward("보상 획득!", new[]
                {
                    ("book", Icon.Kind.Book, Ui.N), ("coin", Icon.Kind.Coin, Ui.N), ("ticket", Icon.Kind.Ticket, Ui.N),
                    ("whet", Icon.Kind.Gear, Ui.N), ("ore", Icon.Kind.Ore, Ui.N), ("pill", Icon.Kind.Pill, Ui.N),
                });
            }, "btn", "pri");
            get.style.marginTop = 14;
            body.Add(get);
            return dim;
        }

        // --- 탭 패널 ---

        static void Panel(GameApp app, VisualElement screen)
        {
            string title = Tabs.First(t => t.tab == app.Tab).name;
            if (app.Tab == LobbyTab.Growth) title = "폐관수련";
            screen.Add(TopBand(app));
            screen.Add(TitleBand(title));

            var body = Ui.El("pbody");
            switch (app.Tab)
            {
                case LobbyTab.Units:
                    switch (app.Sub)
                    {
                        case UnitsSub.List: UnitList(app, body); break;
                        case UnitsSub.Deck: DeckEdit(app, body); break;
                        case UnitsSub.LevelUp: GrowthScreens.LevelUp(app, body); break;
                        case UnitsSub.Gear: GrowthScreens.GearList(app, body); break;
                    }
                    break;
                case LobbyTab.Growth: GrowthScreens.Research(app, body); break;
                case LobbyTab.Content:
                    if (app.Content == ContentSub.Match) ContentScreens.Match(app, body);
                    else ContentScreens.Dungeons(app, body);
                    break;
                case LobbyTab.Gacha: ContentScreens.Gacha(app, body); break;
            }
            screen.Add(body);

            // 하위 탭은 하단 바 바로 위 줄 (크럼블). 성장·뽑기는 아직 하나뿐이지만 같은 자리에 둔다
            if (app.Tab == LobbyTab.Growth)
                screen.Add(SubBar(new[] { ("폐관수련", true, (Action)(() => { })) }));
            if (app.Tab == LobbyTab.Gacha)
                screen.Add(SubBar(new[] { ("무인 뽑기", true, (Action)(() => { })) }));

            if (app.Tab == LobbyTab.Units)
                screen.Add(SubBar(Subs.Select(s => (s.name, app.Sub == s.sub, (Action)(() => app.OpenSub(s.sub))))));
            if (app.Tab == LobbyTab.Content)
                screen.Add(SubBar(new[]
                {
                    ("대전", app.Content == ContentSub.Match, (Action)(() => app.OpenContent(ContentSub.Match))),
                    ("일일 던전", app.Content == ContentSub.Dungeon, (Action)(() => app.OpenContent(ContentSub.Dungeon))),
                }));
        }

        static VisualElement SubBar(System.Collections.Generic.IEnumerable<(string name, bool on, Action open)> items)
        {
            var subs = Ui.El("subs");
            foreach (var s in items)
            {
                var b = Ui.Btn(s.on ? $"▸ {s.name} ◂" : s.name, s.open, "sub");
                if (s.on) b.AddToClassList("on");
                subs.Add(b);
            }
            return subs;
        }

        public static ScrollView Scroll()
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
            flt.Add(Ui.El("grow"), Ui.Lb($"{units.Count} / {Catalog.Units.Count}", "count"));
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

        /// <summary>무인 카드: 초상 + 장식 틀 + 별·이름. 등급이 높을수록 틀이 굵다 (등급 색은 미정).</summary>
        public static VisualElement UnitCard(string id, int promotions, int spare)
        {
            var d = Catalog.Unit[id];
            var card = Ui.El("ucard");
            int stars = GameState.Stars(id);
            card.AddToClassList("s" + stars);
            // 크럼블 카드: 둥근 초상(테두리, 왼쪽 아래 Lv, 오른쪽 위 중복 수) 아래에 별과 이름
            var pic = Ui.Portrait(id, false, "upic");
            pic.Add(Ui.Lb($"Lv {Ui.N}", "lv"));
            if (spare > 0) pic.Add(Ui.Lb($"+{spare}", "spare"));
            pic.Add(Skin.CardFrame(stars >= 5));
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

            // 엔트리(덱 여러 개) 수는 미정이라 덱 탭은 자리만
            var decks = Ui.El("decktabs");
            for (int i = 0; i < 3; i++)
            {
                var b = Ui.Btn($"덱 {i + 1}", null, "chip");
                if (i == 0) b.AddToClassList("on");
                else b.SetEnabled(false);
                decks.Add(b);
            }
            sv.Add(decks);

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
                if (pos == app.DeckPos) col.AddToClassList("sel");
                col.Add(Ui.Lb(BattleRules.PosNames[pos], "dpos"));
                if (u != null)
                {
                    var d = Catalog.Unit[u.Id];
                    var pic = Ui.Portrait(u.Id, false, "dpic");
                    pic.Add(Skin.CardFrame(pos == 0, 0.32f));
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
                    app.DeckPos = p; // 고른 칸에 아래 보유 무인을 눌러 넣는다
                    app.Refresh();
                });
                slots.Add(col);
            }
            sv.Add(slots);

            // 아래에 보유 무인 격자: 누르면 고른 칸에 들어간다 (크럼블 편성). 팀 속성 적성 높은 순
            const string aptOrder = "SABCD";
            var owned = Ui.El("grid", "own-grid");
            foreach (var o in app.Game.Data.Units
                         .OrderBy(x => ei >= 0 ? aptOrder.IndexOf(Catalog.Unit[x.Id].Apt[ei]) : 0)
                         .ThenByDescending(x => GameState.Stars(x.Id)))
            {
                var cell = Ui.El("cell5");
                var face = Ui.Portrait(o.Id, true, "own-face");
                if (ei >= 0) face.Add(Ui.Lb(Catalog.Unit[o.Id].Apt[ei].ToString(), "apt-badge"));
                int at = Array.IndexOf(deck.Units, o.Id);
                if (at >= 0) face.Add(Ui.Lb(BattleRules.PosNames[at], "inDeck"));
                face.Add(Skin.CardFrame(at == 0));
                string oid = o.Id;
                face.RegisterCallback<ClickEvent>(_ => { app.Game.SetDeckUnit(app.DeckPos, oid); app.Save(); app.Refresh(); });
                cell.Add(face, Ui.Lb(Catalog.Unit[o.Id].Name, "dn"));
                owned.Add(cell);
            }

            var errors = app.Game.ValidateDeck();
            var foot = Ui.El("deckfoot");
            foreach (var e in errors) foot.Add(Ui.Lb(e, "err"));
            var go = Ui.Btn("대전", app.OpenBattleReady, "btn", "pri");
            go.SetEnabled(errors.Count == 0);
            foot.Add(go);
            sv.Add(owned);
            sv.Add(foot);
            body.Add(sv);
        }
    }
}
