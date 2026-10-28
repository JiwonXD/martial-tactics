using System;
using System.Linq;
using MartialTactics.Battle;
using UnityEngine.UIElements;

namespace MartialTactics.App
{
    /// <summary>전투 결과: 승패와 이유, 무인별 남은 체력, 아군 무공 통계, 전보(전체 기록). 화면 시안 캔버스의 Result.</summary>
    static class ResultScreen
    {
        public static VisualElement Build(BattleResult r, string allyElement, string foeElement, Action replay, Action confirm)
        {
            var screen = Ui.El("screen");

            var head = Ui.El();
            head.style.backgroundColor = new StyleColor(UnityEngine.Color.white);
            head.style.paddingTop = 32;
            head.style.paddingBottom = 16;
            head.style.alignItems = Align.Center;
            string verdict = r.Winner == Side.A ? "승리" : r.Winner == Side.B ? "패배" : "무승부";
            head.Add(Ui.Lb(verdict, "verdict"), Ui.Lb(Reason(r), "lb"));
            screen.Add(head);

            var summary = new ScrollView(ScrollViewMode.Vertical);
            var log = new ScrollView(ScrollViewMode.Vertical);
            foreach (var sv in new[] { summary, log })
            {
                sv.verticalScrollerVisibility = ScrollerVisibility.Hidden;
                sv.style.flexGrow = 1;
                sv.style.paddingLeft = 16;
                sv.style.paddingRight = 16;
                sv.style.paddingTop = 14;
                sv.style.marginBottom = 88;
            }

            var tabs = Ui.El("tabs");
            Button sumTab = null, logTab = null;
            void Select(bool showLog)
            {
                summary.style.display = showLog ? DisplayStyle.None : DisplayStyle.Flex;
                log.style.display = showLog ? DisplayStyle.Flex : DisplayStyle.None;
                sumTab.EnableInClassList("on", !showLog);
                logTab.EnableInClassList("on", showLog);
            }
            sumTab = Ui.Btn("요약 · 통계", () => Select(false), "tabbtn");
            logTab = Ui.Btn("전보 (전체 기록)", () => Select(true), "tabbtn");
            tabs.Add(sumTab, logTab);
            screen.Add(tabs, summary, log);

            summary.Add(Team(r, Side.A, $"아군 · 속성 {allyElement}"));
            summary.Add(Team(r, Side.B, $"상대 · 속성 {foeElement}"));
            summary.Add(Stats(r));

            foreach (var l in r.Log)
            {
                bool header = l.Text == "준비 합" || l.Text == $"{l.Round}합";
                log.Add(Ui.Lb(l.Text, header ? new[] { "ln", "head" } : new[] { "ln" }));
            }
            Select(false);

            var footer = Ui.El("footer");
            footer.Add(Ui.Btn("다시 보기", replay, "btn"), Ui.Btn("확인", confirm, "btn", "pri", "second"));
            screen.Add(footer);
            return screen;
        }

        static string Reason(BattleResult r)
        {
            if (r.Reason == "leader") return r.Winner == Side.A ? "적 주장 격파" : "아군 주장이 쓰러짐";
            double a = Ratio(r, Side.A), b = Ratio(r, Side.B);
            return $"{r.Rounds}합 종료 · 남은 체력 비율 {a:P0} : {b:P0}";
        }

        static double Ratio(BattleResult r, Side side)
        {
            var us = r.Units.Where(u => u.Side == side).ToList();
            return (double)us.Sum(u => u.Hp) / us.Sum(u => u.MaxHp);
        }

        static VisualElement Team(BattleResult r, Side side, string title)
        {
            var box = Ui.El();
            box.style.marginBottom = 14;
            box.Add(Ui.Lb(title, "b"));
            foreach (var u in r.Units.Where(x => x.Side == side).OrderBy(x => x.Pos))
            {
                var row = Ui.El("ur");
                var nm = Ui.Lb(u.Name, "nm");
                if (u.Pos == 0) nm.AddToClassList("b");
                var bar = Ui.Bar((float)u.Hp / u.MaxHp, side == Side.B);
                bar.style.flexGrow = 1;
                bar.style.marginLeft = 6;
                bar.style.marginRight = 6;
                row.Add(Ui.Lb(BattleRules.PosNames[u.Pos], "lb", "pos"), nm, bar, Ui.Lb(u.Dead ? "쓰러짐" : $"{u.Hp} / {u.MaxHp}", "lb", "hp"));
                box.Add(row);
            }
            return box;
        }

        // 전투 엔진이 이미 내는 집계(발동 횟수, 무공별 피해·회복)를 아군 무인별로
        static VisualElement Stats(BattleResult r)
        {
            var box = Ui.El("box");
            box.Add(Ui.Lb("무공 통계 · 아군", "b"));
            box.Add(Row("무공", "발동", "피해", "회복", true));
            foreach (var u in r.Units.Where(x => x.Side == Side.A).OrderBy(x => x.Pos))
                foreach (var s in u.Skills)
                {
                    u.Rec.Procs.TryGetValue(s.Id, out int procs);
                    u.Rec.BySkill.TryGetValue(s.Name, out var t);
                    int dealt = t?.Dealt ?? 0, healed = t?.Healed ?? 0;
                    if (procs == 0 && dealt == 0 && healed == 0) continue;
                    box.Add(Row($"{s.Name} · {u.Name}", procs > 0 ? $"{procs}" : "–", dealt > 0 ? $"{dealt}" : "–", healed > 0 ? $"{healed}" : "–", false));
                }
            return box;
        }

        static VisualElement Row(string name, string procs, string dealt, string healed, bool header)
        {
            var row = Ui.El("tr");
            string[] cls = header ? new[] { "lb" } : new string[0];
            var c1 = Ui.Lb(name, cls);
            c1.AddToClassList("c1");
            row.Add(c1);
            foreach (var v in new[] { procs, dealt, healed })
            {
                var c = Ui.Lb(v, cls);
                c.AddToClassList("cn");
                row.Add(c);
            }
            return row;
        }
    }
}
