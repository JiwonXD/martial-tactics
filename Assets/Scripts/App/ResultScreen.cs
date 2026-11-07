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
            head.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.078f, 0.2f, 0.204f)); // 패널 #143334
            head.style.paddingTop = 32;
            head.style.paddingBottom = 16;
            head.style.alignItems = Align.Center;
            string verdict = r.Winner == Side.A ? "승리" : r.Winner == Side.B ? "패배" : "무승부";
            // 승패 연출: 기울어진 큰 붓 글씨 도장, 이기면 뒤에 빛줄기
            var v = Ui.Lb(r.Winner == Side.A ? "勝" : r.Winner == Side.B ? "敗" : "和", "verdict", "stamp");
            if (r.Winner == Side.A)
            {
                v.AddToClassList("win");
                head.Add(new Popup.Rays());
            }
            head.Add(v, Ui.Lb(verdict, "verdict-sub"), Ui.Lb(Reason(r), "lb"));
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

        // 전보식 팀 카드 (삼국지 전략판 전보 참고): 무인마다 초상 · 남은 체력 · 발동 · 피해 · 회복. 아군은 경험치 ▲
        static VisualElement Team(BattleResult r, Side side, string title)
        {
            var box = Ui.El("rep-team");
            if (side == Side.B) box.AddToClassList("foe");
            box.Add(Ui.Lb(title, "rep-title"));
            var row = Ui.El("row", "rep-row");
            foreach (var u in r.Units.Where(x => x.Side == side).OrderBy(x => x.Pos))
            {
                var card = Ui.El("rep-card");
                if (u.Pos == 0) card.AddToClassList("leader");
                if (u.Dead) card.AddToClassList("dead");
                var face = Ui.Portrait(u.Source.Id, true, "rep-face");
                face.Add(Ui.Lb(BattleRules.PosNames[u.Pos], "pos-seal"));
                face.Add(Skin.CardFrame(u.Pos == 0));
                card.Add(face, Ui.Lb(u.Name, "un"));
                card.Add(Ui.Bar((float)u.Hp / u.MaxHp, side == Side.B));
                int procs = u.Rec.Procs.Values.Sum();
                var st = Ui.El("rep-stats");
                st.Add(Stat("발동", procs), Stat("피해", u.Rec.Dealt), Stat("회복", u.Rec.Healed));
                card.Add(st);
                if (side == Side.A) card.Add(Ui.Lb($"경험치 ▲{Ui.N}", "rep-exp"));
                row.Add(card);
            }
            box.Add(row);
            return box;
        }

        static VisualElement Stat(string name, int v)
        {
            var e = Ui.El("row", "rep-stat");
            e.Add(Ui.Lb(name, "lb", "grow"), Ui.Lb(v > 0 ? $"{v}" : "–", "rep-v"));
            return e;
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
