using System;
using System.Linq;
using MartialTactics.Battle;
using MartialTactics.Game;
using UnityEngine.UIElements;

namespace MartialTactics.App
{
    /// <summary>
    /// 대장간과 장비 화면 (기획서 2026-10-29). 규칙은 정해졌고 장비 데이터·수치는 아직 없어 수치는 [n], 장비 목록은 비어 있다.
    /// 화면 시안 캔버스의 ForgeAuto · ForgeAutoMore · ForgeResult · ForgeFilter · ForgeRates · EquipSwap.
    /// </summary>
    static class ForgeScreens
    {
        // 자동 뽑기 (크럼블 자동 열기): 아래에서 올라오는 창. 장비 등급 드롭다운 + 한 번에 뽑는 개수 드롭다운을 나란히,
        // 가운데 시작, 오른쪽 아래 특수효과 설정 펼치기. 남길 등급 아래와 고른 특수효과가 없는 장비(영웅 이상)는 바로 분해 → 불씨.
        // 한 번에 뽑는 개수 상한은 뽑기 레벨 따라 풀리고 수치는 미정
        public static VisualElement AutoSheet(GameApp app)
        {
            var dim = LobbyScreen.Sheet(app, "자동 뽑기", out var body);
            dim.AddToClassList("bottom");

            var pair = Ui.El("dd-pair");
            var grades = Enumerable.Range(0, Ui.GearGrades.Length).Reverse().Select(i => (i, Ui.GearGrades[i])).ToArray();
            pair.Add(Dropdown("장비 등급", Ui.GearGrades[app.KeepGrade], "이상", app.GradeOpen, grades, app.KeepGrade,
                () => { app.GradeOpen = !app.GradeOpen; app.CountOpen = false; app.Refresh(); },
                g => { app.KeepGrade = g; app.GradeOpen = false; app.Refresh(); }));
            pair.Add(Dropdown("한 번에 뽑는 개수", Ui.N, null, app.CountOpen, new[] { (0, Ui.N) }, 0,
                () => { app.CountOpen = !app.CountOpen; app.GradeOpen = false; app.Refresh(); },
                _ => { app.CountOpen = false; app.Refresh(); }));
            body.Add(pair);

            if (app.EffectsOpen)
            {
                var list = Ui.El("checks");
                for (int i = 0; i < Ui.GearEffects.Length; i++)
                {
                    int k = i;
                    bool on = app.Effects.Contains(k);
                    var c = new Button(() => { if (!app.Effects.Remove(k)) app.Effects.Add(k); app.Refresh(); });
                    c.AddToClassList("checkrow");
                    var box = Ui.El("checkbox");
                    if (on)
                    {
                        box.AddToClassList("on");
                        box.Add(new Icon(Icon.Kind.Check, 14));
                    }
                    c.Add(box, Ui.Lb(Ui.GearEffects[i], "chk-t"));
                    list.Add(c);
                }
                body.Add(list);
            }

            var foot = Ui.El("sheet-foot");
            foot.Add(Ui.El("foot-side"));
            foot.Add(Ui.Btn("시작", app.OpenForgeResult, "btn", "pri", "wide"));
            var fx = new Button(() => { app.EffectsOpen = !app.EffectsOpen; app.Refresh(); });
            fx.AddToClassList("link");
            fx.Add(Ui.Lb("특수효과 설정", "link-t"), new Icon(app.EffectsOpen ? Icon.Kind.Down : Icon.Kind.Up, 12));
            var side = Ui.El("foot-side", "right");
            side.Add(fx);
            foot.Add(side);
            body.Add(foot);
            return dim;
        }

        // 자동 뽑기 결과 (크럼블 결과 창): 뽑은 장비 격자 + 골라보기 / 모두 분해. 대장간은 분해만, 장착은 무인 상세 > 장비 칸
        public static VisualElement ResultSheet(GameApp app)
        {
            var dim = LobbyScreen.Sheet(app, "자동 뽑기 결과", out var body);
            dim.AddToClassList("bottom");
            // 아이템 타일: 그 칸 그림 + 오른쪽 아래 강화 수치, 왼쪽 위 Lv. 장비 데이터가 없어 칸 종류만 돌려 보인다
            var grid = Ui.El("gear-grid");
            string[] art = { "weapon", "robe", "boots", "jade" };
            for (int i = 0; i < 12; i++)
                grid.Add(Ui.Tile(art[i % 4], Icon.Kind.Forge, $"+{Ui.N}", -1, $"Lv {Ui.N}", 70));
            body.Add(grid);
            var foot = Ui.El("sheet-foot");
            var pick = new Button();
            pick.AddToClassList("icon-text");
            pick.Add(new Icon(Icon.Kind.Content, 22), Ui.Lb("골라보기", "it-t"));
            var l = Ui.El("foot-side");
            l.Add(pick);
            foot.Add(l);
            foot.Add(Ui.Btn("모두 분해", null, "btn", "pri", "wide"));
            var r = Ui.El("foot-side", "right");
            r.Add(Ui.Cur(Icon.Kind.Flame, $"+{Ui.N}"));
            foot.Add(r);
            body.Add(foot);
            return dim;
        }

        // 대장간 레벨: 등급마다 지금/다음 레벨 확률, 불씨로 채우는 성장 게이지 (레벨마다 한 줄, 뽑기 경험치 없음)
        public static VisualElement Level(GameApp app)
        {
            var screen = Ui.El("screen");
            screen.Add(Top(app.BackToLobby, "대장간 레벨"));
            var sv = LobbyScreen.Scroll();

            var head = Ui.El("lvhead");
            head.Add(Ui.Lb($"Lv {Ui.N}", "lv-big"), new Icon(Icon.Kind.Next, 20), Ui.Lb($"Lv {Ui.N}", "lv-big", "next"));
            sv.Add(head);

            var table = Ui.El("box", "section");
            var hr = Ui.El("tr");
            hr.Add(Ui.Lb("등급", "lb", "c1"), Ui.Lb("지금", "lb", "cn"), Ui.Lb("다음", "lb", "cn"));
            table.Add(hr);
            for (int i = Ui.GearGrades.Length - 1; i >= 0; i--)
            {
                var r = Ui.El("tr");
                var g = Ui.Lb(Ui.GearGrades[i], "c1", "b");
                if (i >= 5) g.AddToClassList("red");
                r.Add(g, Ui.Lb($"{Ui.N}%", "cn"), Ui.Lb($"{Ui.N}%", "cn", "b"));
                table.Add(r);
            }
            sv.Add(table);

            var gauge = Ui.El("box", "section");
            gauge.Add(Ui.El("row").Add(Ui.Lb("성장", "b", "grow"), Ui.Lb($"{Ui.N} / {Ui.N}", "lb")));
            var bar = Ui.Bar(0.35f, true);
            bar.AddToClassList("thick");
            gauge.Add(bar);
            sv.Add(gauge);
            screen.Add(sv);

            var footer = Ui.El("footer", "static");
            // 불씨가 모자라면 부족 팝업: 불씨는 장비 분해로 얻는다
            var grow = Ui.CostBtn("대장간 성장", Icon.Kind.Flame, Ui.N, true,
                () => Popup.Lack("불씨", Icon.Kind.Flame, Ui.N, Ui.N, new (string, Action)[] { ("장비 분해", app.OpenForgeResult) }));
            footer.Add(grow);
            screen.Add(footer);
            return screen;
        }

        // 장비 바꾸기: 그 칸에 낄 수 있는 장비(대장간에 쌓인 것)를 이 무인 기준 전투력 ±로
        public static VisualElement EquipSwap(GameApp app, OwnedUnit u)
        {
            var d = Catalog.Unit[u.Id];
            int slot = app.EquipSlot;
            var screen = Ui.El("screen");
            screen.Add(Top(app.BackFromEquip, $"{Ui.GearSlots[slot]} 바꾸기"));

            var head = Ui.El("cu-head");
            head.Add(Ui.Portrait(u.Id, true, "cu-face"));
            var info = Ui.El("grow");
            info.Add(Ui.Lb(d.Name, "uname"), Ui.Lb($"{Ui.GearSlots[slot]} · {Ui.GearStats[slot]}", "lb"));
            head.Add(info);
            var power = Ui.El("spare-box");
            power.Add(Ui.Lb("전투력", "lb"), Ui.Lb(Ui.N, "spare-n"));
            head.Add(power);
            screen.Add(head);

            var cur = Ui.El("box", "section");
            cur.Add(Ui.Lb("지금", "lb"), Ui.Lb("비어 있음", "b"));
            screen.Add(cur);

            var sv = LobbyScreen.Scroll();
            for (int i = 0; i < 3; i++)
            {
                var row = Ui.El("skrow", "plate-row", "off");
                var line = Ui.El("row");
                line.Add(Ui.Tile(new[] { "weapon", "robe", "boots", "jade" }[slot], Icon.Kind.Forge, $"+{Ui.N}", -1, null, 52));
                var t = Ui.El("grow");
                t.Add(Ui.Lb($"[장비] · {Ui.N}", "b"), Ui.Lb($"{Ui.GearStats[slot]} +{Ui.N}", "desc"));
                line.Add(t, Ui.Lb($"+{Ui.N}", "delta"));
                row.Add(line);
                sv.Add(row);
            }
            screen.Add(sv);
            return screen;
        }

        /// <summary>위에 이름, 아래 드롭다운 (크럼블 자동 열기). 펼치면 바로 아래에 목록.</summary>
        static VisualElement Dropdown(string label, string value, string suffix, bool open, (int key, string text)[] options, int selected, Action toggle, Action<int> pick)
        {
            var box = Ui.El("dd");
            box.Add(Ui.Lb(label, "dd-label"));
            var row = Ui.El("row");
            var drop = new Button(toggle);
            drop.AddToClassList("dropdown");
            drop.Add(Ui.Lb(value, "dd-t"), Ui.El("grow"), new Icon(open ? Icon.Kind.Up : Icon.Kind.Down, 14));
            row.Add(drop);
            if (suffix != null) row.Add(Ui.Lb(suffix, "dd-suffix"));
            box.Add(row);
            if (open)
            {
                var list = Ui.El("dd-list");
                foreach (var o in options)
                {
                    int key = o.key;
                    var item = Ui.Btn(o.text, () => pick(key), "dd-item");
                    if (key == selected) item.AddToClassList("on");
                    list.Add(item);
                }
                box.Add(list);
            }
            return box;
        }

        public static VisualElement Top(System.Action back, string title)
        {
            var top = Ui.El("topbar");
            top.Add(Ui.IconBtn(Icon.Kind.Back, back, 22, "back"), Ui.Band(title));
            return top;
        }
    }
}
