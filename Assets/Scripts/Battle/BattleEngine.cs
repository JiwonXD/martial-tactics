using System;
using System.Collections.Generic;
using System.Linq;

namespace MartialTactics.Battle
{
    public enum Side { A, B }

    /// <summary>지속 피해(중독·출혈) 계수. 걸 때 피해량을 한 번 계산해 둔다.</summary>
    public sealed class Dot
    {
        public readonly DamageKind Kind;
        public readonly double Coef;
        public Dot(DamageKind kind, double coef) { Kind = kind; Coef = coef; }
    }

    public sealed class StatusEntry
    {
        public int Rounds, Since;
        public bool HasDot;
        public int Tick;      // 지속 피해량
        public Unit Src;
        public string Label;  // 지속 피해를 건 무공
        public Unit By;       // 호위하는 무인
        public double Ratio;  // 호위가 대신 받는 비율
    }

    public sealed class Mod
    {
        public ModKey Key;
        public double Value;
        public double Rounds; // PositiveInfinity = 전투 내내
        public string Label;
        public int Since;
    }

    public sealed class SkillTally { public int Dealt, Healed; }

    public sealed class UnitRecord
    {
        public int Dealt, Healed, Taken;
        public readonly Dictionary<string, int> Procs = new Dictionary<string, int>();          // 무공 id별 발동 횟수
        public readonly Dictionary<string, SkillTally> BySkill = new Dictionary<string, SkillTally>(); // 무공 이름별 피해·회복
    }

    /// <summary>전투 중인 무인.</summary>
    public sealed class Unit
    {
        public Side Side;
        public int Pos;
        public UnitDef Source;
        public string Name;
        public int MaxHp, Hp;
        public bool Dead;
        public double BaseExt, BaseInt, BaseDef, BaseSpd;
        public char Apt;
        public List<SkillDef> Skills;
        public readonly Dictionary<StatusKind, StatusEntry> Status = new Dictionary<StatusKind, StatusEntry>();
        public readonly List<Mod> Mods = new List<Mod>();
        public readonly HashSet<StatusKind> Immune = new HashSet<StatusKind>();
        public SkillDef Prep;
        public bool Reflect; // 두전성이
        public readonly List<Action> RoundStart = new List<Action>();
        public readonly List<Action<Unit, bool, DamageKind>> Damaged = new List<Action<Unit, bool, DamageKind>>(); // (공격자, 평타 여부, 피해 종류)
        public readonly List<Action<int, DamageKind>> Dealt = new List<Action<int, DamageKind>>();
        public readonly UnitRecord Rec = new UnitRecord();

        public bool Has(StatusKind k) => Status.ContainsKey(k);

        public double Base(ModKey key)
        {
            switch (key)
            {
                case ModKey.Ext: return BaseExt;
                case ModKey.Int: return BaseInt;
                case ModKey.Def: return BaseDef;
                case ModKey.Spd: return BaseSpd;
                default: throw new ArgumentException(key.ToString());
            }
        }
    }

    public struct LogLine
    {
        public int Round;
        public string Text;
    }

    public sealed class BattleResult
    {
        public Side? Winner;  // null = 무승부
        public string Reason; // leader(주장 격파), hp(8합 후 체력 비율), draw
        public int Rounds;
        public List<Unit> Units;
        public List<LogLine> Log;
    }

    /// <summary>
    /// 전투 1판. 웹 시뮬레이터(tools/web-sim/engine.js)의 Battle과 같은 시드에서 같은 결과를 낸다.
    /// 난수를 뽑는 순서와 부동소수 계산 순서를 바꾸면 결과가 달라지므로 두 쪽을 함께 고친다.
    /// </summary>
    public sealed class BattleEngine
    {
        static readonly StatusKind[] Controls = { StatusKind.Stun, StatusKind.Confuse, StatusKind.Silence, StatusKind.Disarm }; // 두전성이가 되돌리는 제어
        static readonly StatusKind[] DotKinds = { StatusKind.Poison, StatusKind.Bleed };
        static readonly SkillType[] SetupOrder = { SkillType.Formation, SkillType.Aura, SkillType.Passive };

        public readonly BattleConfig Config;
        public readonly List<Unit> Units;
        public readonly List<LogLine> Lines = new List<LogLine>();
        public int Round;
        public bool Over;

        readonly Rng rng;
        readonly bool logging;
        readonly Team[] teams;
        int turnSeq;
        string acting;

        public BattleEngine(Team teamA, Team teamB, int seed = 1, bool log = false, BattleConfig config = null)
        {
            Config = config ?? new BattleConfig();
            rng = new Rng(seed);
            logging = log;
            teams = new[] { teamA, teamB };
            Units = BuildSide(Side.A, teamA).Concat(BuildSide(Side.B, teamB)).ToList();
        }

        List<Unit> BuildSide(Side side, Team team)
        {
            var defs = team.Slots.Select(s => s.Def ?? Catalog.Unit[s.Unit]).ToList();
            bool sameFaction = defs.All(d => d.Faction == defs[0].Faction);
            int ei = Array.IndexOf(BattleRules.Elements, team.Element);
            var list = new List<Unit>();
            for (int pos = 0; pos < team.Slots.Length; pos++)
            {
                var s = team.Slots[pos];
                var d = defs[pos];
                double aptMult = BattleRules.AptMult[d.Apt[ei]];
                double mult = aptMult * (sameFaction ? 1 + Config.FactionBonus : 1);
                var skills = new List<SkillDef> { Catalog.Skill[d.Skill] };
                skills.AddRange(s.Skills.Where(id => !string.IsNullOrEmpty(id)).Select(id => Catalog.Skill[id]));
                list.Add(new Unit
                {
                    Side = side, Pos = pos, Source = d, Name = d.Name, MaxHp = d.Hp, Hp = d.Hp,
                    BaseExt = d.Ext * mult, BaseInt = d.Int * mult, BaseDef = d.Def * mult, BaseSpd = d.Spd * mult,
                    Apt = d.Apt[ei], Skills = skills,
                });
            }
            return list;
        }

        // --- 조회 ---
        public double Rand() => rng.Next();
        public string Tag(Unit u) => $"[{(u.Side == Side.A ? "아" : "적")}] {u.Name}";
        public void Log(string text) { if (logging) Lines.Add(new LogLine { Round = Round, Text = text }); }
        public List<Unit> Alive(Side side) => Units.Where(u => u.Side == side && !u.Dead).ToList();
        public List<Unit> Allies(Unit u) => Alive(u.Side);
        public List<Unit> Foes(Unit u) => Alive(u.Side == Side.A ? Side.B : Side.A);

        public double Mod(Unit u, ModKey key)
        {
            double s = 0;
            foreach (var m in u.Mods) if (m.Key == key) s += m.Value;
            return s;
        }

        public double Stat(Unit u, ModKey key) => u.Base(key) * Math.Max(0.1, 1 + Mod(u, key));

        // JS Math.round와 같게: .5는 +무한대 쪽으로 올림
        static int RoundJs(double x)
        {
            double f = Math.Floor(x);
            return (int)(x - f >= 0.5 ? f + 1 : f);
        }

        static double HpRatio(Unit u) => (double)u.Hp / u.MaxHp;

        Unit WeightedPick(List<Unit> pool)
        {
            int total = 0;
            foreach (var u in pool) total += BattleRules.TargetWeight[u.Pos];
            double r = Rand() * total;
            foreach (var u in pool) { r -= BattleRules.TargetWeight[u.Pos]; if (r < 0) return u; }
            return pool.Count > 0 ? pool[pool.Count - 1] : null;
        }

        /// <summary>n명 고르기. 3 이상이면 적 전체. 혼란 상태면 단일 대상이 적·아군 중 무작위.</summary>
        public List<Unit> PickEnemies(Unit u, int n)
        {
            var foes = Foes(u);
            if (n >= 3) return foes;
            var pool = u.Has(StatusKind.Confuse) ? Units.Where(x => !x.Dead && x != u).ToList() : foes;
            var output = new List<Unit>();
            while (output.Count < n && pool.Count > 0)
            {
                var t = WeightedPick(pool);
                output.Add(t);
                pool = pool.Where(x => x != t).ToList();
            }
            return output;
        }

        /// <summary>적 주장을 노림. 혼란 상태면 무작위.</summary>
        public Unit LeaderTarget(Unit u)
        {
            if (u.Has(StatusKind.Confuse)) return PickEnemies(u, 1).FirstOrDefault();
            var foes = Foes(u);
            return foes.FirstOrDefault(f => f.Pos == 0) ?? WeightedPick(foes);
        }

        Unit AttackTarget(Unit u)
        {
            if (u.Has(StatusKind.Confuse)) return PickEnemies(u, 1).FirstOrDefault();
            var foes = Foes(u);
            return foes.FirstOrDefault(f => f.Has(StatusKind.Taunt)) ?? WeightedPick(foes);
        }

        /// <summary>체력 비율이 가장 낮은 아군. 같으면 앞자리.</summary>
        public Unit LowestHpAlly(Unit u)
        {
            Unit best = null;
            foreach (var a in Allies(u)) if (best == null || HpRatio(a) < HpRatio(best)) best = a;
            return best;
        }

        // --- 효과 ---
        public void AddMod(Unit u, ModKey key, double value, double rounds, string label)
        {
            u.Mods.RemoveAll(m => m.Label == label && m.Key == key); // 같은 출처는 갱신
            u.Mods.Add(new Mod { Key = key, Value = value, Rounds = rounds, Label = label, Since = turnSeq });
        }

        public void AddStatus(Unit src, Unit dst, StatusKind key, int rounds, Dot dot = null, bool reflected = false)
        {
            if (dst.Dead) return;
            if (dst.Immune.Contains(key)) { Log($"{Tag(dst)}: {BattleRules.StatusNames[key]} 면역"); return; }
            if (dst.Has(key)) return; // 같은 종류는 중첩되지 않음
            var entry = new StatusEntry { Rounds = rounds, Since = turnSeq };
            if (dot != null)
            {
                entry.HasDot = true;
                entry.Tick = RoundJs(RawDamage(src, dst, dot.Kind, dot.Coef));
                entry.Src = src;
                entry.Label = acting;
            }
            dst.Status[key] = entry;
            Log($"{Tag(dst)}: {BattleRules.StatusNames[key]} ({rounds}합)");
            if (!reflected && Array.IndexOf(Controls, key) >= 0 && src.Side != dst.Side && !src.Dead)
            {
                var r = Allies(dst).FirstOrDefault(a => a.Reflect);
                if (r != null && !src.Has(key))
                {
                    Log($"{Tag(r)}의 두전성이: {BattleRules.StatusNames[key]}를 되돌림");
                    AddStatus(r, src, key, rounds, null, true);
                }
            }
        }

        double RawDamage(Unit src, Unit dst, DamageKind kind, double coef)
        {
            double atk = Stat(src, kind == DamageKind.Ext ? ModKey.Ext : ModKey.Int);
            double dfn = Stat(dst, kind == DamageKind.Ext ? ModKey.Def : ModKey.Int);
            double dmg = (Config.BaseDmg + Math.Max(0, atk - dfn) * Config.StatK) * coef;
            string eA = teams[(int)src.Side].Element, eB = teams[(int)dst.Side].Element;
            if (src.Side != dst.Side)
            {
                if (BattleRules.Beats[eA] == eB) dmg *= 1 + Config.CounterBonus + Mod(src, ModKey.Counter);
                else if (BattleRules.Beats[eB] == eA) dmg *= 1 - Config.CounterBonus;
            }
            dmg *= 0.6 + 0.4 * src.Hp / src.MaxHp; // 체력 보정
            dmg *= Math.Max(0, 1 + Mod(src, ModKey.Dealt));
            dmg *= Math.Max(0.1, 1 + Mod(dst, ModKey.Taken) + Mod(dst, kind == DamageKind.Ext ? ModKey.TakenExt : ModKey.TakenInt));
            return dmg;
        }

        public int Damage(Unit src, Unit dst, DamageKind kind, double coef, string label, bool normal = false, bool counter = false)
        {
            if (dst.Dead || Over) return 0;
            double raw = RawDamage(src, dst, kind, coef) * (0.9 + 0.2 * Rand());
            bool crit = false;
            if (kind == DamageKind.Ext && Rand() < Mod(src, ModKey.Crit)) { raw *= Config.CritMult; crit = true; }
            int dmg = Math.Max(1, RoundJs(raw));
            // 호위: 피해 일부를 호위하는 무인이 대신 받음
            if (dst.Status.TryGetValue(StatusKind.Guard, out var g) && g.By != null && src.Side != dst.Side && !g.By.Dead)
            {
                int share = RoundJs(dmg * g.Ratio);
                if (share > 0)
                {
                    dmg -= share;
                    ApplyLoss(g.By, share, src, label);
                    Log($"{Tag(g.By)}의 호위: {Tag(dst)} 대신 {share} 피해 (남은 체력 {g.By.Hp})");
                }
            }
            ApplyLoss(dst, dmg, src, label);
            Log($"{Tag(src)}의 {label}{(crit ? " (치명타)" : "")} → {Tag(dst)} {dmg} 피해 ({(kind == DamageKind.Ext ? "무기" : "내공")}, 남은 체력 {dst.Hp})");
            if (!dst.Dead && !counter) foreach (var h in dst.Damaged) h(src, normal, kind);
            if (src.Side != dst.Side && !src.Dead) foreach (var h in src.Dealt) h(dmg, kind);
            return dmg;
        }

        // 무공별 집계. '탈명연환 추가타'처럼 꾸밈말이 붙은 이름은 무공 이름으로 묶는다.
        void Record(Unit u, string label, bool healed, int amt)
        {
            string name = label.Split(' ')[0];
            if (!u.Rec.BySkill.TryGetValue(name, out var r)) u.Rec.BySkill[name] = r = new SkillTally();
            if (healed) r.Healed += amt; else r.Dealt += amt;
        }

        void ApplyLoss(Unit dst, int dmg, Unit src, string label)
        {
            dmg = Math.Min(dmg, dst.Hp);
            dst.Hp -= dmg;
            dst.Rec.Taken += dmg;
            if (src != null && src.Side != dst.Side)
            {
                src.Rec.Dealt += dmg;
                if (label != null) Record(src, label, false, dmg);
            }
            if (dst.Hp <= 0)
            {
                dst.Dead = true;
                Log($"{Tag(dst)} 쓰러짐");
                if (dst.Pos == 0) Over = true;
            }
        }

        public void Heal(Unit src, Unit dst, double coef, string label)
        {
            if (dst.Dead) return;
            double amt = (Config.HealBase + Stat(src, ModKey.Int)) * coef * (0.9 + 0.2 * Rand());
            GiveHeal(src, dst, amt, label);
        }

        /// <summary>계산된 양을 그대로 회복 (난수 없음). 내상이면 반감.</summary>
        public void HealFlat(Unit src, Unit dst, double amt, string label)
        {
            if (dst.Dead || Over) return;
            GiveHeal(src, dst, amt, label);
        }

        void GiveHeal(Unit src, Unit dst, double amt, string label)
        {
            bool injured = dst.Has(StatusKind.Injury);
            if (injured) amt *= 0.5;
            int healed = Math.Min(RoundJs(amt), dst.MaxHp - dst.Hp);
            if (healed <= 0) return;
            dst.Hp += healed;
            src.Rec.Healed += healed;
            Record(src, label, true, healed);
            Log($"{Tag(src)}의 {label} → {Tag(dst)} {healed} 회복{(injured ? " (내상으로 반감)" : "")}");
        }

        public double TeamRatio(Side side)
        {
            int hp = 0, max = 0;
            foreach (var u in Units) if (u.Side == side) { hp += u.Hp; max += u.MaxHp; }
            return (double)hp / max;
        }

        void Proc(Unit u, SkillDef s)
        {
            u.Rec.Procs.TryGetValue(s.Id, out int n);
            u.Rec.Procs[s.Id] = n + 1;
            acting = s.Name;
        }

        // --- 진행 ---
        public BattleResult Run()
        {
            Log("준비 합");
            var order = SpeedOrder();
            foreach (var type in SetupOrder)
                foreach (var u in order)
                    foreach (var s in u.Skills.Where(s => s.Type == type).ToList())
                    {
                        if (type == SkillType.Formation && (u.Pos != 0 || s.Element != teams[(int)u.Side].Element)) continue; // 진법은 주장만, 같은 속성만
                        Log($"{Tag(u)}: {BattleRules.TypeNames[type]} {s.Name} 적용");
                        s.Setup(this, u);
                    }
            for (Round = 1; Round <= Config.Rounds && !Over; Round++)
            {
                Log($"{Round}합");
                foreach (var u in SpeedOrder())
                    foreach (var h in u.RoundStart) if (!u.Dead) h();
                foreach (var u in SpeedOrder())
                {
                    if (Over) break;
                    if (!u.Dead) TakeTurn(u);
                }
            }
            return Result();
        }

        // 경공 내림차순. 동률은 난수로 가른다. (OrderByDescending은 안정 정렬이라 웹과 순서가 같다)
        List<Unit> SpeedOrder()
        {
            var list = new List<KeyValuePair<Unit, double>>();
            foreach (var u in Units) if (!u.Dead) list.Add(new KeyValuePair<Unit, double>(u, Stat(u, ModKey.Spd) + Rand() * 0.01));
            return list.OrderByDescending(x => x.Value).Select(x => x.Key).ToList();
        }

        void TakeTurn(Unit u)
        {
            int turn = ++turnSeq;
            foreach (var k in DotKinds)
            {
                if (u.Status.TryGetValue(k, out var st) && !u.Dead)
                {
                    ApplyLoss(u, st.Tick, st.Src, st.Label); // 지속 피해는 건 무공의 피해로 집계
                    Log($"{Tag(u)}: {BattleRules.StatusNames[k]} {st.Tick} 피해 (남은 체력 {u.Hp})");
                }
            }
            if (u.Dead || Over) return;

            if (u.Has(StatusKind.Stun))
            {
                Log($"{Tag(u)}: 점혈로 움직이지 못함");
                if (u.Prep != null) { Log($"{Tag(u)}: {u.Prep.Name} 준비가 끊김"); u.Prep = null; }
            }
            else
            {
                // 발동 무공
                SkillDef released = null;
                if (u.Prep != null)
                {
                    var s = released = u.Prep;
                    u.Prep = null;
                    if (u.Has(StatusKind.Silence)) Log($"{Tag(u)}: 봉인으로 {s.Name} 준비가 끊김");
                    else { Log($"{Tag(u)}: 발동 무공 {s.Name}"); Proc(u, s); s.Cast(this, u, null); }
                }
                foreach (var s in u.Skills.Where(s => s.Type == SkillType.Active).ToList())
                {
                    if (Over || u.Has(StatusKind.Silence)) break;
                    if (s == released || Rand() * 100 >= s.Rate) continue;
                    if (s.Prep) { u.Prep = s; Log($"{Tag(u)}: {s.Name} 기를 모음"); continue; }
                    Log($"{Tag(u)}: 발동 무공 {s.Name}");
                    Proc(u, s);
                    s.Cast(this, u, null);
                }
                // 평타 + 연계 무공
                if (!Over && !u.Dead && !u.Has(StatusKind.Disarm))
                {
                    var t = AttackTarget(u);
                    if (t != null)
                    {
                        Damage(u, t, DamageKind.Ext, 1.0, "평타", normal: true);
                        foreach (var s in u.Skills.Where(s => s.Type == SkillType.Chase).ToList())
                        {
                            if (Over || u.Dead || Rand() * 100 >= s.Rate) continue;
                            var tt = t.Dead ? AttackTarget(u) : t;
                            if (tt == null) break;
                            Log($"{Tag(u)}: 연계 무공 {s.Name}");
                            Proc(u, s);
                            s.Cast(this, u, tt);
                        }
                    }
                }
                else if (!Over && u.Has(StatusKind.Disarm))
                {
                    Log($"{Tag(u)}: 무장해제로 평타 불가");
                }
            }
            // 자기 차례가 끝날 때 지속 시간 1 감소 (이번 차례에 걸린 것은 제외)
            foreach (var k in u.Status.Keys.ToList())
            {
                var st = u.Status[k];
                if (st.Since < turn && --st.Rounds <= 0) u.Status.Remove(k);
            }
            for (int i = 0; i < u.Mods.Count;)
            {
                var m = u.Mods[i];
                if (!double.IsPositiveInfinity(m.Rounds) && m.Since < turn && --m.Rounds <= 0) u.Mods.RemoveAt(i);
                else i++;
            }
        }

        BattleResult Result()
        {
            int rounds = Math.Min(Round - 1, Config.Rounds);
            bool deadA = Units[0].Dead, deadB = Units[3].Dead;
            Side? winner = null;
            string reason = "draw";
            if (deadA != deadB) { winner = deadA ? Side.B : Side.A; reason = "leader"; }
            else
            {
                // 8합 종료: 팀의 남은 체력 비율(남은 체력 합 ÷ 최대 체력 합)이 높은 쪽 승리, 같으면 무승부
                double a = TeamRatio(Side.A), b = TeamRatio(Side.B);
                if (a != b) { winner = a > b ? Side.A : Side.B; reason = "hp"; }
            }
            return new BattleResult { Winner = winner, Reason = reason, Rounds = rounds, Units = Units, Log = Lines };
        }
    }
}
