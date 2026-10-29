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
            screen.Add(NavBar(app));
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

            // 머리: 왼쪽 프로필(주장 얼굴 · Lv · 전투력), 오른쪽 재화와 설정
            var deck = app.Game.Data.Deck.Units;
            var top = Ui.El("ltop");
            var prof = Ui.El("profile");
            prof.Add(Ui.Portrait(app.Game.Find(deck[0]) != null ? deck[0] : null, true, "avatar"));
            var pinfo = Ui.El("pinfo");
            pinfo.Add(Ui.Lb($"Lv {Ui.N}", "plv"), Ui.Lb($"전투력 {Ui.N}", "ppow"));
            prof.Add(pinfo);
            top.Add(prof, Ui.El("grow"), Currencies(), Ui.IconBtn(Icon.Kind.Gear, () => app.OpenSheet(LobbySheet.Settings), 20, "sq"));
            stage.Add(top);

            // 가운데 위: 단계 도전 (크럼블 보스 소환 자리). 상대는 아직 임시(무작위)라 대전 준비로 이어 둔다
            var mid = Ui.El("challenge-box");
            var challenge = new Button(app.OpenBattleReady);
            challenge.AddToClassList("challenge");
            challenge.Add(Ui.Lb($"{Ui.N}단계 도전", "ch-t"));
            mid.Add(challenge);
            var idle = Ui.El("idle-tag");
            idle.Add(Ui.Item(Icon.Kind.Clock, 16), Ui.Lb($"{Ui.N}단계 반복 중", "it"));
            mid.Add(idle);
            stage.Add(mid);

            // 왼쪽 세로 줄: 방치 보상
            var side = Ui.El("side-col");
            var reward = new Button(() => app.OpenSheet(LobbySheet.IdleReward));
            reward.AddToClassList("round");
            reward.Add(Ui.Item(Icon.Kind.Chest, 56));
            reward.Add(Ui.El("dot"));
            side.Add(reward);
            stage.Add(side);
            stage.Add(Ui.Lb("[방치 전투]", "stage-ph"));
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
            var forge = new Button();
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

        static VisualElement NavBar(GameApp app)
        {
            var nav = Ui.El("nav");
            foreach (var t in Tabs)
            {
                // 다섯 칸의 너비·아이콘 크기·위치는 어느 화면에서나 같다. 고른 칸은 테두리 색과 아래 이름만 달라진다
                if (t.tab == LobbyTab.None)
                {
                    // 가운데 칸은 늘 같은 너비: 로비에서는 대장간 그림이 이 칸에 걸쳐 앉고, 탭을 열면 닫기
                    var center = Ui.El("tb", "center");
                    if (app.Tab != LobbyTab.None) center.Add(Ui.IconBtn(Icon.Kind.Close, app.CloseTab, 26, "x"));

                    nav.Add(center);
                    continue;
                }
                var tab = t.tab;
                var b = new Button(() => app.OpenTab(tab));
                b.AddToClassList("tb");
                bool on = app.Tab == tab;
                // 그림 아이콘은 한지 원판 위에 (먹색 바에서도 검은 그림이 보이게)
                var medal = Ui.El("medal");
                medal.Add(Ui.Item(t.icon, 50));
                b.Add(medal);
                var name = Ui.Lb(t.name, "tbn");
                if (on) b.AddToClassList("on");
                else name.style.visibility = Visibility.Hidden;
                b.Add(name);
                nav.Add(b);
            }
            return nav;
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
            if (!app.ConfirmReset)
            {
                body.Add(Ui.Btn("저장 초기화", () => { app.ConfirmReset = true; app.Refresh(); }, "btn"));
            }
            else
            {
                body.Add(Ui.Lb("보유 무인 · 무공 · 덱 삭제", "warn"));
                var row = Ui.El("row", "btnrow");
                row.Add(Ui.Btn("취소", () => { app.ConfirmReset = false; app.Refresh(); }, "btn"), Ui.Btn("초기화", app.ResetSave, "btn", "danger"));
                body.Add(row);
            }
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
                var box = Ui.El("rw-icon");
                box.Add(Ui.Item(icon, 46, art));
                cell.Add(box, Ui.Lb(Ui.N, "rw-n"), Ui.Lb(name, "rw-name"));
                grid.Add(cell);
            }
            body.Add(grid);
            var get = Ui.Btn("받기", () => app.OpenSheet(LobbySheet.None), "btn", "pri");
            get.style.marginTop = 14;
            body.Add(get);
            return dim;
        }

        // --- 탭 패널 ---

        static void Panel(GameApp app, VisualElement screen)
        {
            var head = Ui.El("phead");
            string title = Tabs.First(t => t.tab == app.Tab).name;
            if (app.Tab == LobbyTab.Growth) title = "폐관수련";
            head.Add(Ui.Lb(title, "ptitle"), Ui.El("grow"), Currencies());
            screen.Add(head);

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
    }
}
