using System;
using System.Collections.Generic;
using System.Linq;
using MartialTactics.Battle;
using UnityEngine.UIElements;

namespace MartialTactics.App
{
    /// <summary>
    /// 전투 A안 (2026-10-28 확정): 위 연출 + 아래 고정 기록 5줄, 누르면 전체 기록. 화면 시안 캔버스의 BattleA.
    /// 전투는 미리 끝까지 계산해 두고, 기록 한 줄씩 그 시점의 체력·상태를 보여 준다.
    /// </summary>
    sealed class BattleScreen
    {
        const float LineDelay = 0.45f; // ×1에서 기록 한 줄 간격(초)
        const int LogLines = 5;

        public readonly VisualElement Root;

        readonly BattleResult result;
        readonly Action finished;
        readonly int maxRounds = new BattleConfig().Rounds;
        readonly int[] maxHp;

        int shown = -1;     // 지금까지 보여 준 기록 줄 번호
        float wait;
        int speed = 1;
        bool full;          // 전체 기록 펼침
        bool done;

        // 화면 요소
        readonly VisualElement allyBar, foeBar, logPanel, banner;
        readonly ScrollView logBody;
        readonly Label allyPct, foePct, round, logRound, bannerSide, bannerText;
        readonly Button speedBtn, fullToggle;
        readonly UnitView[] units;

        sealed class UnitView
        {
            public VisualElement Root, Bar, Chips;
            public Label Pop;
            public float PopTime;
        }

        public BattleScreen(BattleResult result, Action finished)
        {
            this.result = result;
            this.finished = finished;
            maxHp = result.Units.Select(u => u.MaxHp).ToArray();

            Root = Ui.El("screen");

            // 머리줄: 아군 체력 비율 · 합 · 상대 체력 비율
            var hud = Ui.El("hud");
            var left = Ui.El("hud-side");
            allyPct = Ui.Lb("", "b");
            allyBar = Ui.Bar(1, false);
            allyBar.style.flexGrow = 1;
            allyBar.style.marginRight = 6;
            left.Add(Ui.Lb("아군 체력", "lb"), Ui.El("row").Add(allyBar, allyPct));
            var mid = Ui.El("hud-mid");
            round = Ui.Lb("", "round");
            mid.Add(round, Ui.Lb("합", "lb"));
            var right = Ui.El("hud-side");
            foePct = Ui.Lb("", "b");
            foeBar = Ui.Bar(1, true);
            foeBar.style.flexGrow = 1;
            foeBar.style.marginLeft = 6;
            var foeLabel = Ui.Lb("상대 체력", "lb");
            foeLabel.style.unityTextAlign = UnityEngine.TextAnchor.MiddleRight;
            right.Add(foeLabel, Ui.El("row").Add(foePct, foeBar));
            hud.Add(left, mid, right);
            Root.Add(hud);

            // 연출: 위 상대, 가운데 발동 배너, 아래 아군. 주장이 가운데에 크게
            var arena = Ui.El("arena");
            units = new UnitView[result.Units.Count];
            var foeRow = TeamRow(Side.B, "team");
            banner = Ui.El("banner");
            bannerSide = Ui.Lb("", "lb");
            bannerSide.style.color = new StyleColor(new UnityEngine.Color(1, 1, 1, 0.8f));
            bannerSide.style.marginRight = 8;
            bannerText = Ui.Lb("", "b");
            bannerText.style.fontSize = 17;
            banner.Add(bannerSide, bannerText);
            banner.style.visibility = Visibility.Hidden;
            var allyRow = TeamRow(Side.A, "team", "ally");
            var speedBox = Ui.El("speed");
            speedBtn = Ui.Btn("×1", ToggleSpeed, "btn", "sm");
            speedBox.Add(speedBtn, Ui.Btn("건너뛰기", Skip, "btn", "sm"));
            arena.Add(foeRow, banner, allyRow, speedBox);
            Root.Add(arena);

            // 아래 고정 기록
            logPanel = Ui.El("logpanel");
            var head = Ui.El("loghead");
            logRound = Ui.Lb("", "lb");
            logRound.style.marginLeft = 8;
            fullToggle = Ui.Btn("전체 보기 ▴", ToggleFull, "btn", "sm", "flat");
            head.Add(Ui.Lb("전투 기록", "b"), logRound, Ui.El("grow"), fullToggle);
            logBody = new ScrollView(ScrollViewMode.Vertical) { verticalScrollerVisibility = ScrollerVisibility.Hidden };
            logBody.style.flexGrow = 1;
            logBody.style.paddingLeft = 16;
            logBody.style.paddingRight = 16;
            logBody.style.paddingTop = 10;
            logPanel.Add(head, logBody);
            Root.Add(logPanel);

            Step();
        }

        VisualElement TeamRow(Side side, params string[] classes)
        {
            var row = Ui.El(classes);
            foreach (int pos in new[] { 1, 0, 2 })
            {
                int i = result.Units.FindIndex(u => u.Side == side && u.Pos == pos);
                if (i < 0) continue;
                var u = result.Units[i];
                bool leader = pos == 0;
                var v = new UnitView { Root = Ui.El("u") };
                var face = Ui.Placeholder("[무인]", -1, -1, "face");
                if (leader) face.AddToClassList("leader");
                v.Pop = Ui.Lb("", "pop");
                face.Add(v.Pop);
                v.Bar = Ui.Bar(1, side == Side.B, leader ? 92 : 76);
                v.Bar.style.marginTop = 3;
                v.Chips = Ui.El("chips");
                var name = Ui.Lb(leader ? $"{u.Name} · 주장" : u.Name);
                if (leader) name.AddToClassList("b");
                name.style.marginTop = 3;
                // 상대 줄은 시안대로 얼굴 아래 끝을 주장에 맞춘다
                v.Root.Add(side == Side.B ? Ui.Add(Ui.El("facebox"), face) : face, name, v.Bar, v.Chips);
                units[i] = v;
                row.Add(v.Root);
            }
            return row;
        }

        /// <summary>GameApp.Update에서 매 프레임 부른다.</summary>
        public void Tick(float dt)
        {
            foreach (var v in units)
            {
                if (v == null || v.PopTime <= 0) continue;
                v.PopTime -= dt;
                v.Pop.style.opacity = Math.Max(0, Math.Min(1, v.PopTime / 0.3f));
            }
            if (done) return;
            wait -= dt * speed;
            if (wait > 0) return;
            if (shown >= result.Log.Count - 1)
            {
                done = true;
                finished();
                return;
            }
            Step();
        }

        void Step()
        {
            shown++;
            var line = result.Log[shown];
            wait = Header(line) ? LineDelay * 2 : LineDelay;

            var prev = shown > 0 ? result.Log[shown - 1].Hp : maxHp;
            for (int i = 0; i < units.Length; i++)
            {
                var v = units[i];
                if (v == null) continue;
                int hp = line.Hp[i];
                Ui.SetRatio(v.Bar, (float)hp / maxHp[i]);
                v.Root.EnableInClassList("dead", hp <= 0);
                v.Chips.Clear();
                foreach (var k in line.Status[i]) v.Chips.Add(Ui.Lb(BattleRules.StatusNames[k], "sta"));
                int delta = hp - prev[i];
                if (delta != 0)
                {
                    v.Pop.text = delta > 0 ? $"+{delta}" : $"−{-delta}";
                    v.Pop.EnableInClassList("heal", delta > 0);
                    v.Pop.style.opacity = 1;
                    v.PopTime = 0.9f;
                }
            }

            float a = Ratio(line, Side.A), b = Ratio(line, Side.B);
            Ui.SetRatio(allyBar, a);
            Ui.SetRatio(foeBar, b);
            allyPct.text = $"{Math.Round(a * 100)}%";
            foePct.text = $"{Math.Round(b * 100)}%";
            round.text = line.Round == 0 ? "준비" : $"{line.Round} / {maxRounds}";
            logRound.text = line.Round == 0 ? "준비 합" : $"{line.Round}합";

            // 발동·연계 무공이 나오면 배너. 새 합이 시작되면 내린다
            if (Header(line)) banner.style.visibility = Visibility.Hidden;
            var cast = ParseCast(line.Text);
            if (cast != null)
            {
                bannerSide.text = cast.Value.side;
                bannerText.text = cast.Value.text;
                banner.style.visibility = Visibility.Visible;
            }

            RenderLog();
        }

        void RenderLog()
        {
            logBody.Clear();
            IEnumerable<LogLine> lines = result.Log.Take(shown + 1);
            if (!full) lines = lines.Where(l => !Header(l)).Reverse().Take(LogLines).Reverse();
            foreach (var l in lines)
            {
                var lb = Ui.Lb(l.Text, "ln");
                if (Header(l)) lb.AddToClassList("head");
                else if (ParseCast(l.Text) != null) lb.AddToClassList("hl");
                logBody.Add(lb);
            }
            if (full) logBody.schedule.Execute(() => logBody.scrollOffset = new UnityEngine.Vector2(0, float.MaxValue));
        }

        void ToggleFull()
        {
            full = !full;
            logPanel.EnableInClassList("full", full);
            fullToggle.text = full ? "접기 ▾" : "전체 보기 ▴";
            RenderLog();
        }

        void ToggleSpeed()
        {
            speed = speed == 1 ? 2 : 1;
            speedBtn.text = $"×{speed}";
        }

        void Skip()
        {
            if (done) return;
            done = true;
            finished();
        }

        float Ratio(LogLine line, Side side)
        {
            int hp = 0, max = 0;
            for (int i = 0; i < result.Units.Count; i++)
                if (result.Units[i].Side == side) { hp += line.Hp[i]; max += maxHp[i]; }
            return max == 0 ? 0 : (float)hp / max;
        }

        static bool Header(LogLine l) => l.Text == "준비 합" || l.Text == $"{l.Round}합";

        // "[적] 독수귀녀: 발동 무공 산공독" → ("상대 발동", "독수귀녀 · 산공독")
        static (string side, string text)? ParseCast(string text)
        {
            foreach (var kind in new[] { "발동", "연계" })
            {
                string mark = $": {kind} 무공 ";
                int at = text.IndexOf(mark, StringComparison.Ordinal);
                if (at < 0 || text.Length < 4) continue;
                string who = text.Substring(4, at - 4);
                string skill = text.Substring(at + mark.Length);
                return ((text.StartsWith("[아]") ? "아군 " : "상대 ") + kind, $"{who} · {skill}");
            }
            return null;
        }
    }
}
