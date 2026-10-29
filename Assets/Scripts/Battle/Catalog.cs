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
                Setup = (b, u) => u.Damaged.Add((src, normal, kind, dmg) =>
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
            // ---- 출시 무인 50명 무공 (수치는 임시, 밸런스 때 시뮬레이터로 정함) ----
            new SkillDef { Id = "sorimgon", Name = "소림곤법", Type = SkillType.Chase, Rate = 40,
                Desc = "평타 후 같은 적에게 무기 피해 130%, 2합 동안 자신이 받는 무기 피해 -15%",
                Cast = (b, u, t) => { b.Damage(u, t, DamageKind.Ext, 1.3, "소림곤법"); b.AddMod(u, ModKey.TakenExt, -0.15, 2, "소림곤법"); } },
            new SkillDef { Id = "yeonhwarak", Name = "연화락", Type = SkillType.Active, Rate = 35,
                Desc = "적 2명에게 무기 피해 80%, 2합 동안 그 적들이 받는 피해 +15%",
                Cast = (b, u, _) => { foreach (var t in b.PickEnemies(u, 2)) { b.Damage(u, t, DamageKind.Ext, 0.8, "연화락"); b.AddMod(t, ModKey.Taken, 0.15, 2, "연화락"); } } },
            new SkillDef { Id = "chwigwon", Name = "취권", Type = SkillType.Passive,
                Desc = "회피 +20%, 회피하면 50% 확률로 반격 (무기 피해 80%)",
                Setup = (b, u) =>
                {
                    b.AddMod(u, ModKey.Evade, 0.2, Inf, "취권");
                    u.Evaded.Add(src => { if (!src.Dead && b.Rand() < 0.5) b.Damage(u, src, DamageKind.Ext, 0.8, "취권", counter: true); });
                } },
            new SkillDef { Id = "dapseol", Name = "답설무흔", Type = SkillType.Aura,
                Desc = "1~3합 동안 자신 경공 +20%, 회피 +15%",
                Setup = (b, u) => { b.AddMod(u, ModKey.Spd, 0.2, 3, "답설무흔"); b.AddMod(u, ModKey.Evade, 0.15, 3, "답설무흔"); } },
            new SkillDef { Id = "tongbae", Name = "통배권", Type = SkillType.Chase, Rate = 40,
                Desc = "평타 후 같은 적에게 무기 피해 120%, 2합 동안 대상 호신 -20%",
                Cast = (b, u, t) => { b.Damage(u, t, DamageKind.Ext, 1.2, "통배권"); b.AddMod(t, ModKey.Def, -0.2, 2, "통배권"); } },
            new SkillDef { Id = "sipgubong", Name = "십구봉검", Type = SkillType.Chase, Rate = 35,
                Desc = "평타 후 같은 적에게 무기 피해 80% 2회, 이 공격의 치명타 확률 +20%",
                Cast = (b, u, t) =>
                {
                    b.AddMod(u, ModKey.Crit, 0.2, Inf, "십구봉검");
                    b.Damage(u, t, DamageKind.Ext, 0.8, "십구봉검");
                    b.Damage(u, t, DamageKind.Ext, 0.8, "십구봉검");
                    u.Mods.RemoveAll(m => m.Label == "십구봉검");
                } },
            new SkillDef { Id = "cheonsa", Name = "천사검", Type = SkillType.Active, Rate = 35,
                Desc = "적 1명에게 내공 피해 140%, 자신에게 걸린 상태이상 1개 해제",
                Cast = (b, u, _) => { foreach (var t in b.PickEnemies(u, 1)) b.Damage(u, t, DamageKind.Int, 1.4, "천사검"); b.Cleanse(u, u, "천사검"); } },
            new SkillDef { Id = "nodo", Name = "노도검", Type = SkillType.Active, Rate = 30,
                Desc = "적 전체에게 무기 피해 70%",
                Cast = (b, u, _) => { foreach (var t in b.PickEnemies(u, 3)) b.Damage(u, t, DamageKind.Ext, 0.7, "노도검"); } },
            new SkillDef { Id = "dogeom", Name = "도검쌍행", Type = SkillType.Active, Rate = 35,
                Desc = "적 2명에게 무기 피해 90%, 자신 외공 +8% (3번까지 쌓임)",
                Cast = (b, u, _) =>
                {
                    foreach (var t in b.PickEnemies(u, 2)) b.Damage(u, t, DamageKind.Ext, 0.9, "도검쌍행");
                    b.AddMod(u, ModKey.Ext, 0.08 * b.Stack(u, "도검쌍행", 3), Inf, "도검쌍행");
                } },
            new SkillDef { Id = "yukhap", Name = "육합대창", Type = SkillType.Chase, Rate = 40,
                Desc = "평타 후 같은 적에게 무기 피해 130%, 대상이 주장이면 180%",
                Cast = (b, u, t) => b.Damage(u, t, DamageKind.Ext, t.Pos == 0 ? 1.8 : 1.3, "육합대창") },
            new SkillDef { Id = "bunggwon", Name = "붕권", Type = SkillType.Chase, Rate = 40,
                Desc = "평타 후 같은 적에게 무기 피해 120%, 대상이 내상 상태면 180%",
                Cast = (b, u, t) => b.Damage(u, t, DamageKind.Ext, t.Has(StatusKind.Injury) ? 1.8 : 1.2, "붕권") },
            new SkillDef { Id = "chilseong", Name = "칠성검", Type = SkillType.Aura,
                Desc = "1~3합 동안 아군 전체 받는 피해 -12%",
                Setup = (b, u) => { foreach (var a in b.Allies(u)) b.AddMod(a, ModKey.Taken, -0.12, 3, "칠성검"); } },
            new SkillDef { Id = "namak", Name = "남악검", Type = SkillType.Active, Rate = 35,
                Desc = "적 1명에게 무기 피해 130% + 출혈(2합)",
                Cast = (b, u, _) =>
                {
                    foreach (var t in b.PickEnemies(u, 1))
                    {
                        b.Damage(u, t, DamageKind.Ext, 1.3, "남악검");
                        b.AddStatus(u, t, StatusKind.Bleed, 2, new Dot(DamageKind.Ext, 0.4));
                    }
                } },
            new SkillDef { Id = "jungak", Name = "중악검", Type = SkillType.Active, Rate = 35, Prep = true,
                Desc = "1합 기를 모은 뒤 적 2명에게 무기 피해 160%",
                Cast = (b, u, _) => { foreach (var t in b.PickEnemies(u, 2)) b.Damage(u, t, DamageKind.Ext, 1.6, "중악검"); } },
            new SkillDef { Id = "bugak", Name = "북악검", Type = SkillType.Chase, Rate = 40,
                Desc = "평타 후 같은 적에게 무기 피해 110%, 자신 호신 +6% (4번까지 쌓임)",
                Cast = (b, u, t) => { b.Damage(u, t, DamageKind.Ext, 1.1, "북악검"); b.AddMod(u, ModKey.Def, 0.06 * b.Stack(u, "북악검", 4), Inf, "북악검"); } },
            new SkillDef { Id = "cheolsa", Name = "철사장", Type = SkillType.Chase, Rate = 40,
                Desc = "평타 후 같은 적에게 무기 피해 120% + 내상(2합)",
                Cast = (b, u, t) => { b.Damage(u, t, DamageKind.Ext, 1.2, "철사장"); b.AddStatus(u, t, StatusKind.Injury, 2); } },
            new SkillDef { Id = "mosan", Name = "모산부록", Type = SkillType.Active, Rate = 30,
                Desc = "적 1명에게 내공 피해 100% + 혼란(1합)",
                Cast = (b, u, _) => { foreach (var t in b.PickEnemies(u, 1)) { b.Damage(u, t, DamageKind.Int, 1.0, "모산부록"); b.AddStatus(u, t, StatusKind.Confuse, 1); } } },
            new SkillDef { Id = "ilbu", Name = "일부당관", Type = SkillType.Aura,
                Desc = "1~2합 동안 자신에게 도발, 도발 중 평타에 맞으면 30% 확률로 반격 (무기 피해 70%)",
                Setup = (b, u) =>
                {
                    b.AddStatus(u, u, StatusKind.Taunt, 2);
                    u.Damaged.Add((src, normal, kind, dmg) =>
                    {
                        if (normal && u.Has(StatusKind.Taunt) && !src.Dead && b.Rand() < 0.3) b.Damage(u, src, DamageKind.Ext, 0.7, "일부당관", counter: true);
                    });
                } },
            new SkillDef { Id = "sujeon", Name = "수전", Type = SkillType.Active, Rate = 35,
                Desc = "적 1명에게 무기 피해 100% + 봉인(1합)",
                Cast = (b, u, _) => { foreach (var t in b.PickEnemies(u, 1)) { b.Damage(u, t, DamageKind.Ext, 1.0, "수전"); b.AddStatus(u, t, StatusKind.Silence, 1); } } },
            new SkillDef { Id = "mihon", Name = "미혼향", Type = SkillType.Active, Rate = 35,
                Desc = "적 2명에게 중독(3합, 매 합 내공 피해 40%), 이미 중독된 적은 혼란(1합)",
                Cast = (b, u, _) =>
                {
                    foreach (var t in b.PickEnemies(u, 2))
                    {
                        if (t.Has(StatusKind.Poison)) b.AddStatus(u, t, StatusKind.Confuse, 1);
                        else b.AddStatus(u, t, StatusKind.Poison, 3, new Dot(DamageKind.Int, 0.4));
                    }
                } },
            new SkillDef { Id = "juwi", Name = "주위상", Type = SkillType.Passive,
                Desc = "체력이 40% 아래로 처음 떨어지면 2합 동안 회피 +40%",
                Setup = (b, u) => u.Damaged.Add((src, normal, kind, dmg) =>
                {
                    if (u.Stacks.ContainsKey("주위상") || (double)u.Hp / u.MaxHp >= 0.4) return;
                    b.Stack(u, "주위상", 1);
                    b.AddMod(u, ModKey.Evade, 0.4, 2, "주위상");
                    b.Log($"{b.Tag(u)}의 주위상: 2합 동안 회피 +40%");
                }) },
            new SkillDef { Id = "sampan", Name = "삼판부", Type = SkillType.Active, Rate = 35,
                Desc = "적 1명에게 무기 피해, 1~3번째 발동은 180%, 그 뒤엔 80%",
                Cast = (b, u, _) => { foreach (var t in b.PickEnemies(u, 1)) b.Damage(u, t, DamageKind.Ext, u.Rec.Procs["sampan"] <= 3 ? 1.8 : 0.8, "삼판부"); } },
            new SkillDef { Id = "yeonju", Name = "연주전", Type = SkillType.Active, Rate = 35,
                Desc = "무작위 적에게 3회 무기 피해 60%",
                Cast = (b, u, _) => { for (int i = 0; i < 3; i++) foreach (var t in b.PickEnemies(u, 1)) b.Damage(u, t, DamageKind.Ext, 0.6, "연주전"); } },
            new SkillDef { Id = "nangri", Name = "낭리백조", Type = SkillType.Chase, Rate = 40,
                Desc = "평타 후 같은 적에게 무기 피해 110%, 2합 동안 대상 경공 -20%",
                Cast = (b, u, t) => { b.Damage(u, t, DamageKind.Ext, 1.1, "낭리백조"); b.AddMod(t, ModKey.Spd, -0.2, 2, "낭리백조"); } },
            new SkillDef { Id = "biyeon", Name = "비연도", Type = SkillType.Aura,
                Desc = "1합 시작 때 무작위 적 2명에게 무기 피해 60%, 1~3합 동안 자신 경공 +20%",
                Setup = (b, u) =>
                {
                    b.AddMod(u, ModKey.Spd, 0.2, 3, "비연도");
                    u.RoundStart.Add(() => { if (b.Round == 1) foreach (var t in b.PickEnemies(u, 2)) b.Damage(u, t, DamageKind.Ext, 0.6, "비연도"); });
                } },
            new SkillDef { Id = "jegalno", Name = "제갈노", Type = SkillType.Active, Rate = 35,
                Desc = "적 1명에게 3회 무기 피해 50%",
                Cast = (b, u, _) => { foreach (var t in b.PickEnemies(u, 1)) for (int i = 0; i < 3; i++) b.Damage(u, t, DamageKind.Ext, 0.5, "제갈노"); } },
            new SkillDef { Id = "dogung", Name = "도궁비현", Type = SkillType.Active, Rate = 35,
                Desc = "체력 비율이 가장 낮은 적 1명에게 무기 피해 160%",
                Cast = (b, u, _) => { var t = b.LowestHpFoe(u); if (t != null) b.Damage(u, t, DamageKind.Ext, 1.6, "도궁비현"); } },
            new SkillDef { Id = "maehwa", Name = "매화침", Type = SkillType.Chase, Rate = 40,
                Desc = "평타 후 같은 적에게 중독(3합, 매 합 내공 피해 40%), 이미 중독이면 중독 피해가 바로 한 번 터짐",
                Cast = (b, u, t) =>
                {
                    if (t.Has(StatusKind.Poison)) b.BurstDot(t, StatusKind.Poison);
                    else b.AddStatus(u, t, StatusKind.Poison, 3, new Dot(DamageKind.Int, 0.4));
                } },
            new SkillDef { Id = "jamyeong", Name = "잠영보", Type = SkillType.Aura,
                Desc = "1~2합 동안 은신 (적이 노리지 못함), 처음 주는 피해 +30%",
                Setup = (b, u) =>
                {
                    b.AddStatus(u, u, StatusKind.Stealth, 2);
                    b.AddMod(u, ModKey.Dealt, 0.3, Inf, "잠영보");
                    u.Dealt.Add((dmg, kind) => u.Mods.RemoveAll(m => m.Label == "잠영보"));
                } },
            new SkillDef { Id = "wolha", Name = "월하독작", Type = SkillType.Passive,
                Desc = "매 합 시작 시 쓰러진 아군 1명마다 자신 주는 피해 +15%",
                Setup = (b, u) => u.RoundStart.Add(() =>
                {
                    int down = b.Units.Count(a => a.Side == u.Side && a.Dead);
                    if (down > 0) b.AddMod(u, ModKey.Dealt, 0.15 * down, Inf, "월하독작");
                }) },
            new SkillDef { Id = "poseung", Name = "포승술", Type = SkillType.Chase, Rate = 35,
                Desc = "평타 후 같은 적 무장해제(1합) + 2합 동안 경공 -20%",
                Cast = (b, u, t) => { b.AddStatus(u, t, StatusKind.Disarm, 1); b.AddMod(t, ModKey.Spd, -0.2, 2, "포승술"); } },
            new SkillDef { Id = "changnyong", Name = "창룡출해", Type = SkillType.Chase, Rate = 40,
                Desc = "평타 후 같은 적에게 무기 피해 110%, 다른 적 1명에게도 60%",
                Cast = (b, u, t) =>
                {
                    b.Damage(u, t, DamageKind.Ext, 1.1, "창룡출해");
                    var others = b.Targets(u).Where(x => x != t).ToList();
                    if (others.Count > 0) b.Damage(u, b.WeightedPick(others), DamageKind.Ext, 0.6, "창룡출해");
                } },
            new SkillDef { Id = "wonang", Name = "원앙검", Type = SkillType.Active, Rate = 35,
                Desc = "적 2명에게 무기 피해 100%",
                Cast = (b, u, _) => { foreach (var t in b.PickEnemies(u, 2)) b.Damage(u, t, DamageKind.Ext, 1.0, "원앙검"); } },
            new SkillDef { Id = "maengho", Name = "맹호하산", Type = SkillType.Chase, Rate = 40,
                Desc = "평타 후 같은 적에게 무기 피해 110%, 대상 체력 비율이 70% 이상이면 170%",
                Cast = (b, u, t) => b.Damage(u, t, DamageKind.Ext, (double)t.Hp / t.MaxHp >= 0.7 ? 1.7 : 1.1, "맹호하산") },
            new SkillDef { Id = "hojo", Name = "호조수", Type = SkillType.Chase, Rate = 40,
                Desc = "평타 후 같은 적에게 무기 피해 100% + 출혈(2합)",
                Cast = (b, u, t) => { b.Damage(u, t, DamageKind.Ext, 1.0, "호조수"); b.AddStatus(u, t, StatusKind.Bleed, 2, new Dot(DamageKind.Ext, 0.4)); } },
            new SkillDef { Id = "deungpae", Name = "등패술", Type = SkillType.Passive,
                Desc = "받는 무기 피해 -15%, 평타에 맞으면 25% 확률로 공격자 무장해제(1합)",
                Setup = (b, u) =>
                {
                    b.AddMod(u, ModKey.TakenExt, -0.15, Inf, "등패술");
                    u.Damaged.Add((src, normal, kind, dmg) => { if (normal && !src.Dead && b.Rand() < 0.25) b.AddStatus(u, src, StatusKind.Disarm, 1); });
                } },
            new SkillDef { Id = "yuseong", Name = "유성추", Type = SkillType.Active, Rate = 35,
                Desc = "적 1명에게 무기 피해 130%, 40% 확률로 점혈(1합)",
                Cast = (b, u, _) => { foreach (var t in b.PickEnemies(u, 1)) { b.Damage(u, t, DamageKind.Ext, 1.3, "유성추"); if (b.Rand() < 0.4) b.AddStatus(u, t, StatusKind.Stun, 1); } } },
            new SkillDef { Id = "juhwa", Name = "주화입마", Type = SkillType.Passive,
                Desc = "받는 피해 +10%, 매 합 시작 시 잃은 체력 비율만큼 주는 피해 증가 (최대 +40%)",
                Setup = (b, u) =>
                {
                    b.AddMod(u, ModKey.Taken, 0.1, Inf, "주화입마 대가");
                    u.RoundStart.Add(() => b.AddMod(u, ModKey.Dealt, 0.4 * (1 - (double)u.Hp / u.MaxHp), Inf, "주화입마"));
                } },
            new SkillDef { Id = "idok", Name = "이독공독", Type = SkillType.Active, Rate = 40,
                Desc = "체력 비율이 가장 낮은 아군 회복 (계수 80%) + 적 1명 중독(2합, 매 합 내공 피해 40%)",
                Cast = (b, u, _) =>
                {
                    b.Heal(u, b.LowestHpAlly(u), 0.8, "이독공독");
                    foreach (var t in b.PickEnemies(u, 1)) b.AddStatus(u, t, StatusKind.Poison, 2, new Dot(DamageKind.Int, 0.4));
                } },
            new SkillDef { Id = "gujeol", Name = "구절편", Type = SkillType.Active, Rate = 35,
                Desc = "적 2명에게 무기 피해 80%, 그중 1명 무장해제(1합)",
                Cast = (b, u, _) =>
                {
                    var ts = b.PickEnemies(u, 2);
                    foreach (var t in ts) b.Damage(u, t, DamageKind.Ext, 0.8, "구절편");
                    if (ts.Count > 0) b.AddStatus(u, ts[0], StatusKind.Disarm, 1);
                } },
            new SkillDef { Id = "cheonmaryeong", Name = "천마령", Type = SkillType.Aura,
                Desc = "1~3합 동안 아군 전체 경공 +15%",
                Setup = (b, u) => { foreach (var a in b.Allies(u)) b.AddMod(a, ModKey.Spd, 0.15, 3, "천마령"); } },
            new SkillDef { Id = "hopyo", Name = "호표", Type = SkillType.Passive,
                Desc = "아군 주장이 받는 피해의 25%를 대신 받고, 대신 받을 때 30% 확률로 반격 (무기 피해 80%). 자신이 주장이면 효과 없음",
                Setup = (b, u) =>
                {
                    var leader = b.Allies(u).FirstOrDefault(a => a.Pos == 0);
                    if (leader == null || leader == u) return;
                    b.AddStatus(u, leader, StatusKind.Guard, b.Config.Rounds);
                    if (leader.Status.TryGetValue(StatusKind.Guard, out var g) && g.By == null) { g.By = u; g.Ratio = 0.25; }
                    u.Guarded.Add(src => { if (!src.Dead && b.Rand() < 0.3) b.Damage(u, src, DamageKind.Ext, 0.8, "호표", counter: true); });
                } },
            new SkillDef { Id = "jinwon", Name = "진원창", Type = SkillType.Chase, Rate = 40,
                Desc = "평타 후 같은 적에게 무기 피해 110%, 1합 동안 체력 비율이 가장 낮은 아군 받는 피해 -15%",
                Cast = (b, u, t) =>
                {
                    b.Damage(u, t, DamageKind.Ext, 1.1, "진원창");
                    var a = b.LowestHpAlly(u);
                    if (a != null) b.AddMod(a, ModKey.Taken, -0.15, 1, "진원창");
                } },
            new SkillDef { Id = "sain", Name = "사인선사마", Type = SkillType.Active, Rate = 35,
                Desc = "적 주장에게 무기 피해 120% + 2합 동안 주는 피해 -15%",
                Cast = (b, u, _) =>
                {
                    var t = b.LeaderTarget(u);
                    if (t != null) { b.Damage(u, t, DamageKind.Ext, 1.2, "사인선사마"); b.AddMod(t, ModKey.Dealt, -0.15, 2, "사인선사마"); }
                } },
            new SkillDef { Id = "mumyeong", Name = "무명검", Type = SkillType.Aura,
                Desc = "준비 합에 적 1명을 표적으로 정해, 그 적에게 주는 피해 +25%",
                Setup = (b, u) =>
                {
                    var t = b.PickEnemies(u, 1).FirstOrDefault();
                    if (t == null) return;
                    u.Mark = t; u.MarkBonus = 0.25;
                    b.Log($"{b.Tag(u)}의 무명검: {b.Tag(t)}를 표적으로 정함");
                } },
            new SkillDef { Id = "gyeokgong", Name = "격공점혈", Type = SkillType.Active, Rate = 35,
                Desc = "적 2명에게 각각 50% 확률로 점혈(1합)",
                Cast = (b, u, _) => { foreach (var t in b.PickEnemies(u, 2)) if (b.Rand() < 0.5) b.AddStatus(u, t, StatusKind.Stun, 1); } },
            new SkillDef { Id = "sipmyeon", Name = "십면매복", Type = SkillType.Active, Rate = 30,
                Desc = "적 전체에게 내공 피해 60%, 각각 25% 확률로 혼란(1합)",
                Cast = (b, u, _) => { foreach (var t in b.PickEnemies(u, 3)) { b.Damage(u, t, DamageKind.Int, 0.6, "십면매복"); if (b.Rand() < 0.25) b.AddStatus(u, t, StatusKind.Confuse, 1); } } },
            new SkillDef { Id = "pojeong", Name = "포정해우", Type = SkillType.Chase, Rate = 40,
                Desc = "평타 후 같은 적에게 무기 피해 120%, 대상 호신의 40%를 무시",
                Cast = (b, u, t) => b.Damage(u, t, DamageKind.Ext, 1.2, "포정해우", pierce: 0.4) },
            new SkillDef { Id = "sunsu", Name = "순수추주", Type = SkillType.Passive,
                Desc = "받은 무기 피해의 25%를 공격자에게 되돌림",
                Setup = (b, u) => u.Damaged.Add((src, normal, kind, dmg) =>
                {
                    if (kind == DamageKind.Ext && src.Side != u.Side && !src.Dead) b.FlatDamage(u, src, dmg * 0.25, "순수추주");
                }) },
            new SkillDef { Id = "sinnong", Name = "신농백초", Type = SkillType.Active, Rate = 40,
                Desc = "체력 비율이 가장 낮은 아군의 상태이상 1개 해제 + 회복 (계수 100%)",
                Cast = (b, u, _) =>
                {
                    var a = b.LowestHpAlly(u);
                    b.Cleanse(u, a, "신농백초");
                    b.Heal(u, a, 1.0, "신농백초");
                } },
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
            U("namgung", "남궁휘", "창궁검룡", "정파", 3000, 195, 130, 140, 170, "SABBA", "changgung"),
            U("dang", "당소소", "암기천후", "정파", 2700, 185, 140, 100, 185, "ASBAB", "mancheon"),
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
            U("moyong", "모용상", "모용세가주", "정파", 3200, 130, 180, 160, 130, "BACSB", "hyeonmu", 6),
            U("jeokyeom", "적염후", "주작당주", "마교", 3000, 170, 160, 130, 140, "BCBAS", "jujak", 5),
            U("paeng", "팽무혁", "하북도왕", "정파", 3100, 215, 90, 140, 160, "SBABC", "jikdo"),
            U("sahyeol", "사혈귀", "혈수당주", "마교", 2900, 200, 100, 120, 175, "BSACA", "talmyeong", 5),
            U("jangsam", "장삼", "금나귀수", "사파", 3000, 180, 120, 140, 165, "ABSBA", "geumna"),
            U("cheolwi", "철위강", "호위대장", "정파", 3800, 140, 100, 210, 90, "BBSAA", "howi"),
            U("moyongyeon", "모용연", "두전성이 전인", "정파", 3000, 120, 195, 140, 150, "ABBSA", "dujeon"),
            U("dokgo", "독고패", "낭인검귀", "사파", 3100, 205, 110, 130, 155, "SABCB", "sajung"),
            U("baengni", "백리청", "의협", "정파", 3100, 190, 130, 140, 145, "ASBBA", "hyeolmaek"),
            U("jegal", "제갈운", "신의", "정파", 2800, 80, 190, 120, 140, "ABASB", "hoechun"),
            // 출시 무인 50명 (스탯은 역할별 임시값, 밸런스 때 시뮬레이터로 정함)
            U("jimyeong", "지명", "소림 지자배 승려", "정파", 3400, 145, 105, 195, 100, "BCSBA", "sorimgon"),
            U("wonsosam", "원소삼", "삼결 걸개", "정파", 3000, 170, 140, 135, 150, "CSBAB", "yeonhwarak"),
            U("danorang", "단오랑", "취권 여개", "정파", 2900, 190, 105, 115, 180, "ABCSB", "chwigwon"),
            U("mokunbi", "목운비", "곤륜신행", "정파", 2700, 180, 115, 105, 190, "CSBAB", "dapseol"),
            U("dojungak", "도중악", "공동철권", "정파", 3100, 195, 110, 145, 140, "BCSBA", "tongbae"),
            U("yeopcheong", "엽청", "점창쾌검", "정파", 2900, 190, 105, 115, 180, "SBABC", "sipgubong"),
            U("haunhak", "하운학", "청성검객", "정파", 2700, 85, 200, 105, 160, "ABCSB", "cheonsa"),
            U("seokhaerang", "석해랑", "해남검수", "정파", 3100, 195, 110, 145, 140, "ABCSB", "nodo"),
            U("hwangboyeol", "황보열", "황보세가 소가주", "정파", 3360, 210, 110, 160, 140, "SBABC", "dogeom", 4),
            U("akjinmyeong", "악진명", "악가창수", "정파", 3000, 190, 115, 140, 145, "SBABC", "yukhap"),
            U("eonseoyeon", "언서연", "언가권녀", "정파", 3100, 195, 110, 145, 140, "BCSBA", "bunggwon"),
            U("cheongyangja", "청양자", "전진도사", "정파", 3000, 105, 180, 130, 140, "SBABC", "chilseong"),
            U("yusocheong", "유소청", "형산검녀", "정파", 2900, 165, 145, 130, 155, "BABCS", "namak"),
            U("gwakjin", "곽진", "숭산검수", "정파", 3100, 195, 110, 145, 140, "BCSBA", "jungak"),
            U("wijagyeong", "위자경", "항산제자", "정파", 3600, 155, 95, 205, 90, "BCSBA", "bugak"),
            U("notaegyeong", "노태경", "태산권사", "정파", 3000, 190, 115, 140, 145, "BCSBA", "cheolsa"),
            U("jangbuun", "장부운", "모산도사", "정파", 2800, 90, 195, 110, 155, "ABCSB", "mosan"),
            U("maenggwan", "맹관", "검각수문검", "정파", 3600, 155, 95, 205, 90, "SBABC", "ilbu"),
            U("soyuk", "소육", "하오문 정보꾼", "사파", 2700, 180, 115, 105, 190, "CSBAB", "sujeon"),
            U("hongmae", "홍매", "기루 주인", "사파", 2800, 90, 195, 110, 155, "ABCSB", "mihon"),
            U("noesam", "뇌삼", "객잔 점소이", "사파", 2900, 190, 105, 115, 180, "CSBAB", "juwi"),
            U("udaeho", "우대호", "녹림 도부수", "사파", 3000, 190, 115, 140, 145, "BABCS", "sampan"),
            U("jinryeong", "진령", "녹림 궁수", "사파", 2800, 185, 110, 110, 185, "CSBAB", "yeonju"),
            U("isam", "이삼", "장강수귀", "사파", 3100, 175, 135, 140, 145, "ABCSB", "nangri"),
            U("suyeon", "수연", "장강비연", "사파", 2700, 180, 115, 105, 190, "SBABC", "biyeon"),
            U("gangil", "강일", "철노 수적", "사파", 3000, 170, 140, 135, 150, "CSBAB", "jegalno"),
            U("chilho", "칠호", "살막 칠호", "사파", 2900, 190, 105, 115, 180, "SBABC", "dogung"),
            U("heukjeop", "흑접", "살막 흑접", "사파", 2700, 180, 115, 105, 190, "ABCSB", "maehwa"),
            U("muyeong", "무영", "살막 무영", "사파", 2940, 195, 115, 115, 195, "SBABC", "jamyeong", 4),
            U("gowol", "고월", "월하낭인", "사파", 3200, 200, 105, 150, 135, "BABCS", "wolha"),
            U("jobaek", "조백", "현상 사냥꾼", "사파", 2900, 165, 145, 130, 155, "BCSBA", "poseung"),
            U("yongcheonho", "용천호", "청룡당 창수", "마교", 3100, 195, 110, 145, 140, "CSBAB", "changnyong"),
            U("cheongrin", "청린", "청룡당 쌍검녀", "마교", 2900, 190, 105, 115, 180, "CSBAB", "wonang"),
            U("baekcheolsan", "백철산", "백호당 도객", "마교", 3000, 190, 115, 140, 145, "SBABC", "maengho"),
            U("baeksogyo", "백소교", "백호당 조법녀", "마교", 2800, 185, 110, 110, 185, "SBABC", "hojo"),
            U("gugap", "구갑", "현무당 방패수", "마교", 3600, 155, 95, 205, 90, "BCSBA", "deungpae"),
            U("dunsan", "둔산", "현무당 철퇴수", "마교", 3400, 145, 105, 195, 100, "BCSBA", "yuseong"),
            U("gwaksam", "곽삼", "천마 광도", "마교", 3100, 195, 110, 145, 140, "BABCS", "juhwa"),
            U("yuhwa", "유화", "마교 약녀", "마교", 3000, 105, 180, 130, 140, "ABCSB", "idok"),
            U("yeonbi", "연비", "소마녀", "마교", 2900, 165, 145, 130, 155, "BABCS", "gujeol"),
            U("heukui", "흑의사자", "천마령 전령", "마교", 2900, 100, 185, 125, 145, "ABCSB", "cheonmaryeong"),
            U("jangwiwon", "장위원", "진원표국 표두", "신비", 3780, 165, 100, 215, 95, "BCSBA", "hopyo", 4),
            U("jeonsoho", "전소호", "표사", "신비", 2900, 165, 145, 130, 155, "CSBAB", "jinwon"),
            U("haryeong", "하령", "여표사", "신비", 2800, 185, 110, 110, 185, "CSBAB", "sain"),
            U("bokmyeon", "복면객", "정체불명의 복면객", "신비", 3200, 200, 105, 150, 135, "SBABC", "mumyeong"),
            U("hoeui", "회의집사", "회의 집사", "신비", 2700, 85, 200, 105, 160, "ABCSB", "gyeokgong"),
            U("eunmyeon", "은면여인", "은가면 여인", "신비", 2800, 90, 195, 110, 155, "BABCS", "sipmyeon"),
            U("wangdaega", "왕대가", "객잔 숙수", "신비", 3200, 200, 105, 150, 135, "BABCS", "pojeong"),
            U("nocho", "노초", "나루터 뱃사공", "신비", 3400, 145, 105, 195, 100, "ABCSB", "sunsu"),
            U("sana", "산아", "약초꾼 소녀", "신비", 2900, 100, 185, 125, 145, "CSBAB", "sinnong"),
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
