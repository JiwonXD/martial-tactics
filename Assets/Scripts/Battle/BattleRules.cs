using System.Collections.Generic;

namespace MartialTactics.Battle
{
    /// <summary>밸런싱 수치. 기본값은 웹 시뮬레이터(tools/web-sim/engine.js)의 CFG와 같다.</summary>
    public sealed class BattleConfig
    {
        public int Rounds = 8;
        public double BaseDmg = 300;      // 피해식의 B
        public double StatK = 2;          // 피해식의 k (공격 - 방어 1당)
        public double CounterBonus = 0.15; // 오행 상극 시 주는 피해 ±15%
        public double FormationCounter = 0.10; // 진법: 상극 우위 피해 추가 +10%p
        public double FactionBonus = 0.10; // 같은 세력 3명 시 스탯 +10%
        public double CritMult = 1.5;
        public double HealBase = 150;     // 회복식 기본값
    }

    public enum SkillType { Formation, Aura, Active, Chase, Passive }

    public enum StatusKind { Stun, Confuse, Silence, Disarm, Taunt, Poison, Bleed, Injury, Guard, Stealth }

    /// <summary>고정 규칙과 표시 이름.</summary>
    public static class BattleRules
    {
        public static readonly string[] PosNames = { "주장", "부장", "부장" };
        public static readonly int[] TargetWeight = { 20, 40, 40 };
        public static readonly string[] Elements = { "금", "목", "토", "수", "화" };

        // 앞이 뒤를 이긴다
        public static readonly Dictionary<string, string> Beats = new Dictionary<string, string>
        {
            { "금", "목" }, { "목", "토" }, { "토", "수" }, { "수", "화" }, { "화", "금" },
        };

        public static readonly Dictionary<char, double> AptMult = new Dictionary<char, double>
        {
            { 'S', 1.2 }, { 'A', 1.0 }, { 'B', 0.85 }, { 'C', 0.7 },
        };

        public static readonly Dictionary<SkillType, string> TypeNames = new Dictionary<SkillType, string>
        {
            { SkillType.Formation, "진법" }, { SkillType.Aura, "개전" }, { SkillType.Active, "발동" },
            { SkillType.Chase, "연계" }, { SkillType.Passive, "상시" },
        };

        public static readonly Dictionary<StatusKind, string> StatusNames = new Dictionary<StatusKind, string>
        {
            { StatusKind.Stun, "점혈" }, { StatusKind.Confuse, "혼란" }, { StatusKind.Silence, "봉인" },
            { StatusKind.Disarm, "무장해제" }, { StatusKind.Taunt, "도발" }, { StatusKind.Poison, "중독" },
            { StatusKind.Bleed, "출혈" }, { StatusKind.Injury, "내상" }, { StatusKind.Guard, "호위" },
            { StatusKind.Stealth, "은신" },
        };

        // 청심결이 해제하는 순서
        public static readonly StatusKind[] Debuffs =
        {
            StatusKind.Stun, StatusKind.Confuse, StatusKind.Silence, StatusKind.Disarm,
            StatusKind.Poison, StatusKind.Bleed, StatusKind.Injury,
        };
    }
}
