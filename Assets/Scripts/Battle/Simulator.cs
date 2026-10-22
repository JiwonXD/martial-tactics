using System.Collections.Generic;

namespace MartialTactics.Battle
{
    public sealed class UnitStats
    {
        public Side Side;
        public int Pos;
        public string Name;
        public int Dealt, Healed, Taken, Deaths;
        public readonly Dictionary<string, int> Procs = new Dictionary<string, int>();
        public readonly Dictionary<string, SkillTally> BySkill = new Dictionary<string, SkillTally>();
    }

    public sealed class SimResult
    {
        public int N, A, B, Draw, ByHp, Rounds;
        public readonly List<UnitStats> Units = new List<UnitStats>();
        public double AvgRounds => (double)Rounds / N;
    }

    /// <summary>같은 편성으로 n판을 돌려 집계. i번째 판의 시드는 seed + i (웹 시뮬레이터의 simulate와 같다).</summary>
    public static class Simulator
    {
        public static SimResult Simulate(Team teamA, Team teamB, int n, int seed = 1, BattleConfig config = null)
        {
            var agg = new SimResult { N = n };
            for (int i = 0; i < n; i++)
            {
                var r = new BattleEngine(teamA, teamB, seed + i, false, config).Run();
                if (r.Winner == Side.A) agg.A++;
                else if (r.Winner == Side.B) agg.B++;
                else agg.Draw++;
                if (r.Reason == "hp") agg.ByHp++;
                agg.Rounds += r.Rounds;
                for (int j = 0; j < r.Units.Count; j++)
                {
                    var u = r.Units[j];
                    if (agg.Units.Count <= j) agg.Units.Add(new UnitStats { Side = u.Side, Pos = u.Pos, Name = u.Name });
                    var a = agg.Units[j];
                    a.Dealt += u.Rec.Dealt; a.Healed += u.Rec.Healed; a.Taken += u.Rec.Taken; a.Deaths += u.Dead ? 1 : 0;
                    foreach (var kv in u.Rec.Procs)
                    {
                        a.Procs.TryGetValue(kv.Key, out int c);
                        a.Procs[kv.Key] = c + kv.Value;
                    }
                    foreach (var kv in u.Rec.BySkill)
                    {
                        if (!a.BySkill.TryGetValue(kv.Key, out var t)) a.BySkill[kv.Key] = t = new SkillTally();
                        t.Dealt += kv.Value.Dealt; t.Healed += kv.Value.Healed;
                    }
                }
            }
            return agg;
        }
    }
}
