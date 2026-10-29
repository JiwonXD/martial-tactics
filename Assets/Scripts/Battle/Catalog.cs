using System;
using System.Collections.Generic;
using System.Linq;

namespace MartialTactics.Battle
{
    public enum DamageKind { Ext, Int } // Ext = 무기 피해(외공 vs 호신), Int = 내공 피해(내공 vs 내공)

    public enum ModKey { Ext, Int, Def, Spd, Counter, Dealt, Taken, TakenExt, TakenInt, Crit, CritDmg, SkillDmg, Evade, CtrlHit, CtrlRes }

    /// <summary>
    /// 무공 정의. Setup은 준비 합에 1회, Cast는 발동 시 (연계 무공은 target에 평타 대상이 온다).
    /// Formation은 주장만, 팀 속성이 Element와 같을 때만 적용된다.
    /// </summary>
    public sealed class SkillDef
    {
        public string Id, Name, Desc, Element;
        public SkillType Type;
        public int Rate;   // 발동·연계 확률(%)
        public bool Prep;  // 1합 기를 모은 뒤 발동
        public Action<BattleEngine, Unit> Setup;
        public Action<BattleEngine, Unit, Unit> Cast;
    }

    /// <summary>무인 정의. Hp = 체력, Spd = 경공, Apt = 금목토수화 순서의 적성, Skill = 자기 무공(무공창 1칸), Stars = 등급(별 개수).</summary>
    public sealed class UnitDef
    {
        public string Id, Name, Title, Faction, Apt, Skill;
        public int Hp, Ext, Int, Def, Spd;
        public int Stars = 3;
    }

    /// <summary>
    /// 무인 한 명이 낀 장비의 합계. 스탯은 무인 스탯에 더한 뒤 적성 배수를 곱한다.
    /// 특수효과는 비율 (0.05 = 5%). 웹 시뮬레이터의 gear와 같다.
    /// </summary>
    public sealed class Gear
    {
        public int Ext, Int, Def, Spd;
        public double Crit;      // 치명타율 (무기 피해에만)
        public double CritDmg;   // 치명타 피해 (치명타 배수에 더함)
        public double SkillDmg;  // 무공 피해 증가 (평타 제외)
        public double DmgReduce; // 받는 피해 감소
        public double Evade;     // 회피 (지속 피해는 못 피함)
        public double CtrlHit;   // 제어 적중 (상대 제어 저항을 깎음)
        public double CtrlRes;   // 제어 저항 (점혈·혼란·봉인·무장해제를 막을 확률)
    }

    public sealed class TeamSlot
    {
        public string Unit;
        public string[] Skills; // 무공창 2·3칸 (null = 비움)
        public UnitDef Def;     // 승급 스탯을 더한 무인 정보 (null = Catalog 기본값)
        public Gear Gear;       // 장비 합계 (null = 없음)
    }

    /// <summary>Slots[0] = 주장.</summary>
    public sealed class Team
    {
        public string Element;
        public TeamSlot[] Slots;
    }

    /// <summary>무공과 무인 목록. 웹 시뮬레이터(tools/web-sim/engine.js)의 SKILLS, UNITS와 같다.</summary>
    public static class Catalog
    {
        const double Inf = double.PositiveInfinity;
        static readonly BattleConfig Defaults = new BattleConfig();

        // 진법: 그 속성을 강화한다. 공통으로 아군 전체의 상극 우위 피해가 커지고, 진법마다 효과가 하나 더 붙는다.
        static SkillDef Formation(string id, string name, string element, string desc, Action<BattleEngine, Unit> extra)
        {
            return new SkillDef
            {
                Id = id, Name = name, Type = SkillType.Formation, Element = element,
                Desc = $"{element} 속성 강화: 아군 전체 상극 우위 피해 +{Math.Round(Defaults.FormationCounter * 100)}%p, {desc}",
                Setup = (b, u) =>
                {
                    foreach (var a in b.Allies(u)) b.AddMod(a, ModKey.Counter, b.Config.FormationCounter, Inf, name);
                    extra(b, u);
                },
            };
        }

        public static readonly List<SkillDef> Skills = new List<SkillDef>
        {
            new SkillDef { Id = "cheongun", Name = "청운검기", Type = SkillType.Active, Rate = 35,
                Desc = "적 전체에게 내공 피해 90%",
                Cast = (b, u, _) => { foreach (var t in b.PickEnemies(u, 3)) b.Damage(u, t, DamageKind.Int, 0.9, "청운검기"); } },
            new SkillDef { Id = "saengsaeng", Name = "생생조화신공", Type = SkillType.Aura,
                Desc = "1~4합 동안 매 합 시작 시 아군 전체 회복 (계수 50%)",
                Setup = (b, u) => u.RoundStart.Add(() =>
                {
                    if (b.Round <= 4) foreach (var a in b.Allies(u)) b.Heal(u, a, 0.5, "생생조화신공");
                }) },
            new SkillDef { Id = "geumgang", Name = "금강불괴", Type = SkillType.Passive,
                Desc = "점혈 면역, 받는 무기 피해 -20%",
                Setup = (b, u) => { u.Immune.Add(StatusKind.Stun); b.AddMod(u, ModKey.TakenExt, -0.2, Inf, "금강불괴"); } },
            new SkillDef { Id = "hyeolma", Name = "혈마천강", Type = SkillType.Active, Rate = 40, Prep = true,
                Desc = "1합 기를 모은 뒤 적 1명에게 무기 피해 260% + 출혈(2합)",
                Cast = (b, u, _) =>
                {
                    foreach (var t in b.PickEnemies(u, 1))
                    {
                        b.Damage(u, t, DamageKind.Ext, 2.6, "혈마천강");
                        b.AddStatus(u, t, StatusKind.Bleed, 2, new Dot(DamageKind.Ext, 0.4));
                    }
                } },
            new SkillDef { Id = "mandok", Name = "만독귀환", Type = SkillType.Active, Rate = 40,
                Desc = "적 2명에게 중독(3합, 매 합 내공 피해 50%) + 내상(2합)",
                Cast = (b, u, _) =>
                {
                    foreach (var t in b.PickEnemies(u, 2))
                    {
                        b.AddStatus(u, t, StatusKind.Poison, 3, new Dot(DamageKind.Int, 0.5));
                        b.AddStatus(u, t, StatusKind.Injury, 2);
                    }
                } },
            new SkillDef { Id = "cheolbyeok", Name = "철벽마갑", Type = SkillType.Aura,
                Desc = "1~3합 동안 자신에게 도발, 받는 피해 -30%",
                Setup = (b, u) => { b.AddStatus(u, u, StatusKind.Taunt, 3); b.AddMod(u, ModKey.Taken, -0.3, 3, "철벽마갑"); } },
            new SkillDef { Id = "changgung", Name = "창궁검법", Type = SkillType.Chase, Rate = 45,
                Desc = "평타 후 같은 적에게 무기 피해 160%",
                Cast = (b, u, t) => b.Damage(u, t, DamageKind.Ext, 1.6, "창궁검법") },
            new SkillDef { Id = "mancheon", Name = "만천화우", Type = SkillType.Active, Rate = 30,
                Desc = "무작위 적에게 3회 무기 피해 70%, 타격마다 30% 확률로 출혈(2합)",
                Cast = (b, u, _) =>
                {
                    for (int i = 0; i < 3; i++)
                        foreach (var t in b.PickEnemies(u, 1))
                        {
                            b.Damage(u, t, DamageKind.Ext, 0.7, "만천화우");
                            if (b.Rand() < 0.3) b.AddStatus(u, t, StatusKind.Bleed, 2, new Dot(DamageKind.Ext, 0.4));
                        }
                } },
            new SkillDef { Id = "ildo", Name = "일도양단", Type = SkillType.Chase, Rate = 35,
                Desc = "평타 후 같은 적에게 무기 피해 200% + 무장해제(1합)",
                Cast = (b, u, t) => { b.Damage(u, t, DamageKind.Ext, 2.0, "일도양단"); b.AddStatus(u, t, StatusKind.Disarm, 1); } },
            Formation("bukdu", "천강북두진", "금", "아군 전체 주는 피해 +12%",
                (b, u) => { foreach (var a in b.Allies(u)) b.AddMod(a, ModKey.Dealt, 0.12, Inf, "천강북두진"); }),
            Formation("cheongryong", "청룡진", "목", "매 합 시작 시 아군 전체 회복 (계수 25%)",
                (b, u) => u.RoundStart.Add(() => { foreach (var a in b.Allies(u)) b.Heal(u, a, 0.25, "청룡진"); })),
            Formation("samjae", "삼재진", "토", "아군 전체 호신 +15%",
                (b, u) => { foreach (var a in b.Allies(u)) b.AddMod(a, ModKey.Def, 0.15, Inf, "삼재진"); }),
            Formation("hyeonmu", "현무진", "수", "아군 전체 내공 +15%",
                (b, u) => { foreach (var a in b.Allies(u)) b.AddMod(a, ModKey.Int, 0.15, Inf, "현무진"); }),
            Formation("jujak", "주작진", "화", "아군 전체 무기 피해 치명타 확률 +15%",
                (b, u) => { foreach (var a in b.Allies(u)) b.AddMod(a, ModKey.Crit, 0.15, Inf, "주작진"); }),
            new SkillDef { Id = "cheongsim", Name = "청심결", Type = SkillType.Passive,
                Desc = "매 합 시작 시 상태이상에 걸린 아군 1명의 상태이상 1개 해제",
                Setup = (b, u) => u.RoundStart.Add(() =>
                {
                    var sick = b.Allies(u).Where(a => BattleRules.Debuffs.Any(a.Has)).ToList();
                    if (sick.Count == 0) return;
                    var a = sick[(int)Math.Floor(b.Rand() * sick.Count)];
                    var k = BattleRules.Debuffs.First(a.Has);
                    a.Status.Remove(k);
                    b.Log($"{b.Tag(u)}의 청심결: {b.Tag(a)}의 {BattleRules.StatusNames[k]} 해제");
                }) },
            new SkillDef { Id = "jeomhyeol", Name = "점혈수", Type = SkillType.Active, Rate = 35,
                Desc = "적 1명 점혈(1합)",
                Cast = (b, u, _) => { foreach (var t in b.PickEnemies(u, 1)) b.AddStatus(u, t, StatusKind.Stun, 1); } },
            new SkillDef { Id = "naesang", Name = "내상장", Type = SkillType.Chase, Rate = 40,
                Desc = "평타 후 같은 적에게 내공 피해 120% + 내상(2합)",
                Cast = (b, u, t) => { b.Damage(u, t, DamageKind.Int, 1.2, "내상장"); b.AddStatus(u, t, StatusKind.Injury, 2); } },
            new SkillDef { Id = "saja", Name = "사자후", Type = SkillType.Active, Rate = 35,
                Desc = "자신에게 도발(2합), 적 전체에게 내공 피해 50%",
                Cast = (b, u, _) =>
                {
                    b.AddStatus(u, u, StatusKind.Taunt, 2);
                    foreach (var t in b.PickEnemies(u, 3)) b.Damage(u, t, DamageKind.Int, 0.5, "사자후");
                } },
            new SkillDef { Id = "bantan", Name = "반탄경", Type = SkillType.Passive,
                Desc = "평타에 맞으면 30% 확률로 반격 (무기 피해 80%)",
                Setup = (b, u) => u.Damaged.Add((src, normal, kind) =>
                {
                    if (normal && !src.Dead && b.Rand() < 0.3) b.Damage(u, src, DamageKind.Ext, 0.8, "반탄경", counter: true);
                }) },
            new SkillDef { Id = "sangong", Name = "산공독", Type = SkillType.Active, Rate = 35,
                Desc = "적 2명 봉인(1합)",
                Cast = (b, u, _) => { foreach (var t in b.PickEnemies(u, 2)) b.AddStatus(u, t, StatusKind.Silence, 1); } },
            new SkillDef { Id = "seophon", Name = "섭혼대법", Type = SkillType.Active, Rate = 30,
                Desc = "적 1명 혼란(2합)",
                Cast = (b, u, _) => { foreach (var t in b.PickEnemies(u, 1)) b.AddStatus(u, t, StatusKind.Confuse, 2); } },
            new SkillDef { Id = "hoechun", Name = "회춘공", Type = SkillType.Active, Rate = 40,
                Desc = "체력 비율이 가장 낮은 아군 1명 회복 (계수 120%)",
                Cast = (b, u, _) => b.Heal(u, b.LowestHpAlly(u), 1.2, "회춘공") },
            new SkillDef { Id = "jikdo", Name = "직도황룡", Type = SkillType.Active, Rate = 35, Prep = true,
                Desc = "1합 기를 모은 뒤 적 주장에게 무기 피해 280%",
                Cast = (b, u, _) => { var t = b.LeaderTarget(u); if (t != null) b.Damage(u, t, DamageKind.Ext, 2.8, "직도황룡"); } },
            new SkillDef { Id = "talmyeong", Name = "탈명연환", Type = SkillType.Chase, Rate = 40,
                Desc = "평타 후 같은 적에게 무기 피해 120%, 대상이 출혈 상태면 무기 피해 120% 한 번 더",
                Cast = (b, u, t) =>
                {
                    bool bleeding = t.Has(StatusKind.Bleed);
                    b.Damage(u, t, DamageKind.Ext, 1.2, "탈명연환");
                    if (bleeding) b.Damage(u, t, DamageKind.Ext, 1.2, "탈명연환 추가타");
                } },
            new SkillDef { Id = "geumna", Name = "금나수", Type = SkillType.Chase, Rate = 35,
                Desc = "평타 후 같은 적 무장해제(1합), 대상이 내상 상태면 점혈(1합)도 부여",
                Cast = (b, u, t) =>
                {
                    b.AddStatus(u, t, StatusKind.Disarm, 1);
                    if (t.Has(StatusKind.Injury)) b.AddStatus(u, t, StatusKind.Stun, 1);
                } },
            new SkillDef { Id = "howi", Name = "호위결", Type = SkillType.Aura,
                Desc = "1~4합 동안 아군 주장이 받는 피해의 30%를 대신 받음 (자신이 주장이면 효과 없음)",
                Setup = (b, u) =>
                {
                    var leader = b.Allies(u).FirstOrDefault(a => a.Pos == 0);
                    if (leader == null || leader == u) return;
                    b.AddStatus(u, leader, StatusKind.Guard, 4);
                    if (leader.Status.TryGetValue(StatusKind.Guard, out var g)) { g.By = u; g.Ratio = 0.3; }
                } },
            new SkillDef { Id = "dujeon", Name = "두전성이", Type = SkillType.Passive,
                Desc = "자신이 주장이면, 적이 아군에게 점혈·혼란·봉인·무장해제를 걸 때 건 적에게도 똑같이 건다",
                Setup = (b, u) => { if (u.Pos == 0) u.Reflect = true; } },
            new SkillDef { Id = "sajung", Name = "사중구활", Type = SkillType.Passive,
                Desc = "매 합 시작 시 아군 체력 비율이 적보다 낮으면 아군 전체 주는 피해 +15%, 높으면 아군 전체 받는 피해 -10% (그 합 동안)",
                Setup = (b, u) => u.RoundStart.Add(() =>
                {
                    double mine = b.TeamRatio(u.Side), theirs = b.TeamRatio(u.Side == Side.A ? Side.B : Side.A);
                    foreach (var a in b.Allies(u)) a.Mods.RemoveAll(m => m.Label == "사중구활"); // 지난 합 효과 해제
                    if (mine == theirs) return;
                    bool low = mine < theirs;
                    foreach (var a in b.Allies(u))
                        b.AddMod(a, low ? ModKey.Dealt : ModKey.Taken, low ? 0.15 : -0.1, Inf, "사중구활");
                    b.Log($"{b.Tag(u)}의 사중구활: 체력 비율이 {(low ? "낮아" : "높아")} 아군 전체 {(low ? "주는 피해 +15%" : "받는 피해 -10%")}");
                }) },
            new SkillDef { Id = "hyeolmaek", Name = "혈맥상통", Type = SkillType.Passive,
                Desc = "3합부터 자신이 무기 피해를 줄 때마다 체력 비율이 가장 낮은 아군이 그 피해의 20%를 회복",
                Setup = (b, u) => u.Dealt.Add((dmg, kind) =>
                {
                    if (b.Round < 3 || kind != DamageKind.Ext) return;
                    b.HealFlat(u, b.LowestHpAlly(u), dmg * 0.2, "혈맥상통");
                }) },
            new SkillDef { Id = "gwangpung", Name = "광풍쾌검", Type = SkillType.Passive,
                Desc = "무기 피해 치명타 확률 +25% (치명타 시 1.5배)",
                Setup = (b, u) => b.AddMod(u, ModKey.Crit, 0.25, Inf, "광풍쾌검") },
        };

        public static readonly Dictionary<string, SkillDef> Skill = Skills.ToDictionary(s => s.Id);

        static UnitDef U(string id, string name, string title, string faction, int hp, int ext, int @int, int def, int spd, string apt, string skill, int stars = 3)
        {
            return new UnitDef { Id = id, Name = name, Title = title, Faction = faction, Hp = hp, Ext = ext, Int = @int, Def = def, Spd = spd, Apt = apt, Skill = skill, Stars = stars };
        }

        public static readonly List<UnitDef> Units = new List<UnitDef>
        {
            U("baek", "백무진", "청운검주", "정파", 3000, 150, 205, 120, 160, "SABAB", "cheongun"),
            U("seol", "설지향", "화산의선", "정파", 2800, 90, 190, 110, 150, "ABBSA", "saengsaeng"),
            U("hye", "혜법", "금강권승", "정파", 3600, 170, 120, 200, 95, "ABSCB", "geumgang"),
            U("hyeol", "혈영마군", "혈마전인", "마교", 3000, 220, 110, 130, 140, "BASCS", "hyeolma"),
            U("dok", "독수귀녀", "만독곡주", "마교", 2700, 80, 200, 100, 175, "CBASA", "mandok", 5),
            U("cheol", "철갑마웅", "흑풍당주", "마교", 3800, 160, 90, 210, 85, "BASBA", "cheolbyeok", 5),
            U("namgung", "남궁휘", "창궁검룡", "세가", 3000, 195, 130, 140, 170, "SABBA", "changgung"),
            U("dang", "당소소", "암기천후", "세가", 2700, 185, 140, 100, 185, "ASBAB", "mancheon"),
            U("nameless", "무명도객", "낭인", "사파", 3200, 210, 80, 150, 130, "SBABA", "ildo"),
            U("cheongheo", "청허진인", "무당장문", "정파", 3200, 110, 170, 170, 120, "ABSAB", "samjae", 6),
            U("jin", "진세악", "종남검수", "정파", 3000, 175, 140, 140, 150, "SABBA", "bukdu"),
            U("jeongyeon", "정연", "아미신니", "정파", 2800, 90, 185, 120, 155, "BABSA", "cheongsim"),
            U("gongjin", "공진", "나한당주", "정파", 3500, 160, 150, 190, 100, "BASCA", "saja", 5),
            U("ma", "마천우", "귀영수", "사파", 2700, 170, 130, 100, 190, "ASBAB", "jeomhyeol"),
            U("guyang", "구양철", "철장방주", "사파", 3200, 190, 150, 150, 120, "BAABS", "naesang", 5),
            U("yeon", "연소백", "쾌검랑", "사파", 2900, 200, 100, 120, 185, "SABCB", "gwangpung"),
            U("maeng", "맹악", "흑수장군", "마교", 3600, 150, 110, 200, 90, "BBSAB", "bantan"),
            U("hahu", "하후빈", "산공노조", "마교", 2800, 100, 190, 110, 160, "CBASA", "sangong"),
            U("so", "소요령", "섭혼마녀", "마교", 2700, 90, 210, 100, 170, "CBBSA", "seophon"),
            U("songhak", "송학", "녹림군사", "사파", 3000, 120, 175, 140, 145, "ASBBC", "cheongryong"),
            U("moyong", "모용상", "모용세가주", "세가", 3200, 130, 180, 160, 130, "BACSB", "hyeonmu", 6),
            U("jeokyeom", "적염후", "주작당주", "마교", 3000, 170, 160, 130, 140, "BCBAS", "jujak", 5),
            U("paeng", "팽무혁", "하북도왕", "세가", 3100, 215, 90, 140, 160, "SBABC", "jikdo"),
            U("sahyeol", "사혈귀", "혈수당주", "마교", 2900, 200, 100, 120, 175, "BSACA", "talmyeong", 5),
            U("jangsam", "장삼", "금나귀수", "사파", 3000, 180, 120, 140, 165, "ABSBA", "geumna"),
            U("cheolwi", "철위강", "호위대장", "세가", 3800, 140, 100, 210, 90, "BBSAA", "howi"),
            U("moyongyeon", "모용연", "두전성이 전인", "세가", 3000, 120, 195, 140, 150, "ABBSA", "dujeon"),
            U("dokgo", "독고패", "낭인검귀", "사파", 3100, 205, 110, 130, 155, "SABCB", "sajung"),
            U("baengni", "백리청", "의협", "정파", 3100, 190, 130, 140, 145, "ASBBA", "hyeolmaek"),
            U("jegal", "제갈운", "신의", "세가", 2800, 80, 190, 120, 140, "ABASB", "hoechun"),
        };

        public static readonly Dictionary<string, UnitDef> Unit = Units.ToDictionary(u => u.Id);

        /// <summary>편성 검증. 문제가 없으면 빈 목록.</summary>
        public static List<string> ValidateTeam(Team team)
        {
            var errors = new List<string>();
            if (Array.IndexOf(BattleRules.Elements, team.Element) < 0) errors.Add("속성을 고르세요.");
            var units = team.Slots.Select(s => s.Unit).ToList();
            if (units.Any(id => id == null || !Unit.ContainsKey(id))) errors.Add("빈 자리가 있습니다.");
            if (units.Distinct().Count() != units.Count) errors.Add("같은 무인은 한 팀에 한 명만 넣을 수 있습니다.");
            var skills = new List<string>();
            for (int i = 0; i < team.Slots.Length; i++)
            {
                var s = team.Slots[i];
                if (s.Unit != null && Unit.TryGetValue(s.Unit, out var d)) skills.Add(d.Skill);
                foreach (var id in s.Skills.Where(x => !string.IsNullOrEmpty(x)))
                {
                    if (!Skill.ContainsKey(id)) errors.Add($"{i + 1}번 자리({BattleRules.PosNames[i]}): 없는 무공입니다.");
                    skills.Add(id);
                }
            }
            var dup = skills.Where((id, i) => skills.IndexOf(id) != i).Distinct().ToList();
            if (dup.Count > 0) errors.Add($"같은 무공은 한 팀에 하나만: {string.Join(", ", dup.Select(id => Skill[id].Name))}");
            return errors;
        }
    }
}
