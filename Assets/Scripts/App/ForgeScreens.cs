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
        // 자동 뽑기 설정: 톱니를 누르면 아래에서 올라온다. 남길 등급 아래는 바로 분해 → 불씨,
        // 고른 특수효과가 없는 장비도 바로 분해(영웅 이상에만 적용). 한 번에 뽑는 개수 상한은 뽑기 레벨 따라 풀림
        public static VisualElement AutoSheet(GameApp app)
        {
            var dim = LobbyScreen.Sheet(app, "자동 뽑기", out var body);
            dim.AddToClassList("bottom");

            // 크럼블 자동 열기와 같은 짜임: 장비 등급 드롭다운, 한 번에 뽑는 개수 드롭다운, 펼치는 특수효과 체크
            var grades = Enumerable.Range(0, Ui.GearGrades.Length).Reverse().Select(i => (i, $"{Ui.GearGrades[i]} 이상")).ToArray();
            body.Add(Dropdown("장비 등급", $"{Ui.GearGrades[app.KeepGrade]} 이상", app.GradeOpen, grades, app.KeepGrade,
                () => { app.GradeOpen = !app.GradeOpen; app.CountOpen = false; app.Refresh(); },
                g => { app.KeepGrade = g; app.GradeOpen = false; app.Refresh(); }));
            // 한 번에 뽑는 개수 상한은 뽑기 레벨 따라 풀리고 수치는 미정이라 칸 하나만
            body.Add(Dropdown("한 번에", $"{Ui.N}개", app.CountOpen, new[] { (0, $"{Ui.N}개") }, 0,
                () => { app.CountOpen = !app.CountOpen; app.GradeOpen = false; app.Refresh(); },
                _ => { app.CountOpen = false; app.Refresh(); }));

            var fx = new Button(() => { app.EffectsOpen = !app.EffectsOpen; app.Refresh(); });
            fx.AddToClassList("fold");
            fx.Add(Ui.Lb("특수효과 설정", "b", "grow"), Ui.Lb($"{app.Effects.Count}", "lb"), new Icon(app.EffectsOpen ? Icon.Kind.Up : Icon.Kind.Down, 14));
            body.Add(fx);
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

            var row = Ui.El("row", "btnrow");
            row.Add(Ui.Btn("취소", () => app.OpenSheet(LobbySheet.None), "btn"), Ui.Btn("시작", app.OpenForgeResult, "btn", "pri"));
            body.Add(row);
            return dim;
        }

        // 결과 창: 대장간은 분해만 (모두 분해 + 골라보기). 장착은 무인 상세 > 장비 칸 > 바꾸기
        public static VisualElement Result(GameApp app)
        {
            var screen = Ui.El("screen");
            screen.Add(Top(app.BackToLobby, "대장간"));
            var hero = Ui.ArtBox("forge_hero", "[대장간 그림]", "hero");
            var res = Ui.El("hero-cap");
            res.Add(Ui.Cur(Icon.Kind.Ore, Ui.N), Ui.Cur(Icon.Kind.Flame, Ui.N));
            hero.Add(res);
            screen.Add(hero);

            var flt = Ui.El("flt");
            flt.Add(Ui.Lb($"쌓인 장비 {Ui.N}개", "b", "grow"));
            flt.Add(Ui.Btn("골라보기", null, "chip"));
            screen.Add(flt);

            var sv = LobbyScreen.Scroll();
            var grid = Ui.El("grid");
            for (int i = 0; i < 12; i++)
            {
                var cell = Ui.El("cell4");
                var slot = Ui.El("gear-cell", "empty");
                slot.Add(Ui.Lb(Ui.GearSlots[i % 4], "lb"));
                cell.Add(slot);
                grid.Add(cell);
            }
            sv.Add(grid);
            screen.Add(sv);

            var footer = Ui.El("footer", "static");
            footer.Add(Ui.Btn("닫기", app.BackToLobby, "btn"), Ui.Btn($"모두 분해  +{Ui.N}", null, "btn", "pri", "second"));
            screen.Add(footer);
            return screen;
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
            var grow = new Button();
            grow.AddToClassList("btn");
            grow.AddToClassList("pri");
            grow.AddToClassList("iconbtn");
            grow.Add(Ui.Lb("대장간 성장", "bt"), Ui.Item(Icon.Kind.Flame, 22), Ui.Lb(Ui.N, "bt"));
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
                line.Add(Ui.Add(Ui.El("gear-cell", "mini"), Ui.Lb(Ui.GearSlots[slot], "lb")));
                var t = Ui.El("grow");
                t.Add(Ui.Lb($"[장비] · {Ui.N}", "b"), Ui.Lb($"{Ui.GearStats[slot]} +{Ui.N}", "desc"));
                line.Add(t, Ui.Lb($"+{Ui.N}", "delta"));
                row.Add(line);
                sv.Add(row);
            }
            screen.Add(sv);
            return screen;
        }

        /// <summary>이름 + 드롭다운 한 줄. 펼치면 바로 아래에 고를 것 목록.</summary>
        static VisualElement Dropdown(string label, string value, bool open, (int key, string text)[] options, int selected, Action toggle, Action<int> pick)
        {
            var box = Ui.El("dd");
            var row = Ui.El("row", "sh-label");
            row.Add(Ui.Lb(label, "b", "grow"));
            var drop = new Button(toggle);
            drop.AddToClassList("dropdown");
            drop.Add(Ui.Lb(value, "dd-t"), new Icon(open ? Icon.Kind.Up : Icon.Kind.Down, 14));
            row.Add(drop);
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
