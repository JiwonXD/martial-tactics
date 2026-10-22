// 웹 시뮬레이터(tools/web-sim/make-golden.js)가 만든 golden.json과 C# 전투 엔진 결과를 비교한다.
//   dotnet run --project Tests/BattleParity
// 어긋난 판은 C# 쪽 전투 기록을 mismatch-<대전>-<시드>.txt로 남긴다.
// 웹 쪽 기록: node tools/web-sim/make-golden.js --log <대전> <시드>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MartialTactics.Battle;

static class Program
{
    static int failures;

    static int Main()
    {
        var root = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden.json"))).RootElement;
        int simN = root.GetProperty("simN").GetInt32(), simSeed = root.GetProperty("simSeed").GetInt32();
        int battles = 0, matchups = 0;

        foreach (var m in root.GetProperty("matchups").EnumerateArray())
        {
            string name = m.GetProperty("name").GetString();
            var a = ReadTeam(m.GetProperty("a")); var b = ReadTeam(m.GetProperty("b"));
            Check(Catalog.ValidateTeam(a).Count == 0 && Catalog.ValidateTeam(b).Count == 0, $"{name}: 편성 검증");
            matchups++;

            foreach (var g in m.GetProperty("battles").EnumerateArray())
            {
                int seed = g.GetProperty("seed").GetInt32();
                var r = new BattleEngine(a, b, seed, log: true).Run();
                string text = string.Join("\n", r.Log.Select(l => $"{l.Round}\t{l.Text}"));
                string winner = g.GetProperty("winner").ValueKind == JsonValueKind.Null ? null : g.GetProperty("winner").GetString();
                var hp = g.GetProperty("hp").EnumerateArray().Select(x => x.GetInt32()).ToArray();
                bool ok = Check(Equals(winner, r.Winner?.ToString()), $"{name}/{seed}: 승자 {winner} ≠ {r.Winner}")
                    & Check(g.GetProperty("reason").GetString() == r.Reason, $"{name}/{seed}: 결과 사유")
                    & Check(g.GetProperty("rounds").GetInt32() == r.Rounds, $"{name}/{seed}: 합 수")
                    & Check(hp.SequenceEqual(r.Units.Select(u => u.Hp)), $"{name}/{seed}: 남은 체력 [{string.Join(",", hp)}] ≠ [{string.Join(",", r.Units.Select(u => u.Hp))}]")
                    & Check(g.GetProperty("log").GetString() == Sha(text), $"{name}/{seed}: 전투 기록");
                if (!ok) File.WriteAllText($"mismatch-{name}-{seed}.txt", text + "\n");
                battles++;
            }

            var s = m.GetProperty("sim");
            var sim = Simulator.Simulate(a, b, simN, simSeed);
            Check(s.GetProperty("A").GetInt32() == sim.A && s.GetProperty("B").GetInt32() == sim.B
                && s.GetProperty("draw").GetInt32() == sim.Draw && s.GetProperty("byHp").GetInt32() == sim.ByHp
                && s.GetProperty("rounds").GetInt32() == sim.Rounds, $"{name}: 집계 승패");
            int j = 0;
            foreach (var gu in s.GetProperty("units").EnumerateArray())
            {
                var u = sim.Units[j++];
                Check(gu.GetProperty("dealt").GetInt32() == u.Dealt && gu.GetProperty("healed").GetInt32() == u.Healed
                    && gu.GetProperty("taken").GetInt32() == u.Taken && gu.GetProperty("deaths").GetInt32() == u.Deaths, $"{name}: {u.Name} 집계");
                Check(SameMap(gu.GetProperty("procs"), u.Procs.ToDictionary(kv => kv.Key, kv => $"{kv.Value}")), $"{name}: {u.Name} 발동 횟수");
                Check(SameMap(gu.GetProperty("bySkill"), u.BySkill.ToDictionary(kv => kv.Key, kv => $"{kv.Value.Dealt}/{kv.Value.Healed}")), $"{name}: {u.Name} 무공별 집계");
            }
        }

        Console.WriteLine(failures == 0
            ? $"일치: {matchups}개 대전, 기록 비교 {battles}판, 집계 비교 {matchups * simN}판"
            : $"불일치 {failures}건");
        return failures == 0 ? 0 : 1;
    }

    static bool Check(bool ok, string what)
    {
        if (!ok && failures++ < 30) Console.WriteLine("불일치: " + what);
        return ok;
    }

    static bool SameMap(JsonElement golden, Dictionary<string, string> mine)
    {
        var g = golden.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.Object
            ? $"{p.Value.GetProperty("dealt").GetInt32()}/{p.Value.GetProperty("healed").GetInt32()}"
            : $"{p.Value.GetInt32()}");
        return g.Count == mine.Count && g.All(kv => mine.TryGetValue(kv.Key, out var v) && v == kv.Value);
    }

    static Team ReadTeam(JsonElement t) => new Team
    {
        Element = t.GetProperty("element").GetString(),
        Slots = t.GetProperty("slots").EnumerateArray().Select(s => new TeamSlot
        {
            Unit = s.GetProperty("unit").GetString(),
            Skills = s.GetProperty("skills").EnumerateArray().Select(x => x.ValueKind == JsonValueKind.Null ? null : x.GetString()).ToArray(),
        }).ToArray(),
    };

    static string Sha(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
}
