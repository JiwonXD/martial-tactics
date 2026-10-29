using System;
using System.Collections.Generic;
using System.Linq;
using MartialTactics.Battle;

namespace MartialTactics.Game
{
    public sealed class DrawResult
    {
        public string Unit;
        public bool New; // false = 중복 카드
    }

    /// <summary>뽑기, 승급, 무공 추출·장착, 덱 편성, 전투. 화면은 이 클래스만 거쳐 PlayerData를 바꾼다.</summary>
    public sealed class GameState
    {
        public const int PointsPerPromotion = 10;
        public static readonly string[] StatNames = { "외공", "내공", "호신", "경공" };

        public readonly PlayerData Data;
        readonly Random rng;

        public GameState(PlayerData data, int seed)
        {
            Data = data;
            rng = new Random(seed);
        }

        public OwnedUnit Find(string id) => Data.Units.FirstOrDefault(u => u.Id == id);
        public ExtractedSkill FindSkill(int uid) => Data.Skills.FirstOrDefault(s => s.Uid == uid);

        public static int Stars(string unitId) => Catalog.Unit[unitId].Stars;

        public static string Rank(int stars) => stars == 3 ? "삼류" : stars == 4 ? "이류" : stars == 5 ? "일류" : stars == 6 ? "절정" : "";

        /// <summary>뽑기에 나오는 무인. 6성 절정은 확장 패치 전까지 뺀다.</summary>
        public static readonly List<UnitDef> DrawPool = Catalog.Units.Where(u => u.Stars <= 5).ToList();

        // --- 뽑기 ---

        // 임시: 등급별 확률이 아직 없어서 뽑기 대상 무인 균등 확률
        public List<DrawResult> Draw(int count)
        {
            var results = new List<DrawResult>();
            for (int i = 0; i < count; i++)
            {
                var def = DrawPool[rng.Next(DrawPool.Count)];
                var own = Find(def.Id);
                if (own == null) Data.Units.Add(new OwnedUnit { Id = def.Id });
                else own.Spare++;
                results.Add(new DrawResult { Unit = def.Id, New = own == null });
                Data.Draws++;
            }
            return results;
        }

        // --- 승급·추출 ---

        public bool CanPromote(OwnedUnit u) => u.Spare > 0 && u.Promotions < Stars(u.Id);

        public void Promote(OwnedUnit u)
        {
            if (!CanPromote(u)) throw new InvalidOperationException("승급할 수 없습니다.");
            u.Spare--;
            u.Promotions++;
            u.FreePoints += PointsPerPromotion;
        }

        public void AddStat(OwnedUnit u, StatKind stat)
        {
            if (u.FreePoints <= 0) throw new InvalidOperationException("자유 스탯이 없습니다.");
            u.FreePoints--;
            u.Added[(int)stat]++;
        }

        /// <summary>중복 카드 1장을 소모해 그 무인의 무공을 얻는다.</summary>
        public ExtractedSkill Extract(OwnedUnit u)
        {
            if (u.Spare <= 0) throw new InvalidOperationException("중복 카드가 없습니다.");
            u.Spare--;
            var s = new ExtractedSkill { Uid = Data.NextUid++, Skill = Catalog.Unit[u.Id].Skill };
            Data.Skills.Add(s);
            return s;
        }

        // --- 무공 장착 ---

        public OwnedUnit EquippedBy(int uid) => Data.Units.FirstOrDefault(u => u.Equipped.Contains(uid));

        /// <summary>장착할 수 없으면 이유, 할 수 있으면 null. 다른 무인이 장착 중이면 옮겨 온다.</summary>
        public string EquipError(OwnedUnit u, int slot, ExtractedSkill s)
        {
            if (s.Skill == Catalog.Unit[u.Id].Skill) return "자기 무공과 같습니다.";
            var other = FindSkill(u.Equipped[1 - slot]);
            if (other != null && other.Uid != s.Uid && other.Skill == s.Skill) return "다른 칸에 같은 무공이 있습니다.";
            return null;
        }

        public void Equip(OwnedUnit u, int slot, ExtractedSkill s)
        {
            var err = EquipError(u, slot, s);
            if (err != null) throw new InvalidOperationException(err);
            var holder = EquippedBy(s.Uid);
            if (holder != null) holder.Equipped[Array.IndexOf(holder.Equipped, s.Uid)] = -1;
            u.Equipped[slot] = s.Uid;
        }

        public void Unequip(OwnedUnit u, int slot) => u.Equipped[slot] = -1;

        /// <summary>자유 스탯을 더한 무인 정보 (전투에 쓰인다).</summary>
        public static UnitDef EffectiveDef(OwnedUnit u)
        {
            var d = Catalog.Unit[u.Id];
            return new UnitDef
            {
                Id = d.Id, Name = d.Name, Title = d.Title, Faction = d.Faction, Apt = d.Apt, Skill = d.Skill, Hp = d.Hp, Stars = d.Stars,
                Ext = d.Ext + u.Added[(int)StatKind.Ext], Int = d.Int + u.Added[(int)StatKind.Int],
                Def = d.Def + u.Added[(int)StatKind.Def], Spd = d.Spd + u.Added[(int)StatKind.Spd],
            };
        }

        // --- 덱 편성 ---

        /// <summary>pos 자리에 무인을 넣는다. 이미 다른 자리에 있으면 두 자리를 맞바꾼다.</summary>
        public void SetDeckUnit(int pos, string unitId)
        {
            var units = Data.Deck.Units;
            int prev = Array.IndexOf(units, unitId);
            if (prev >= 0) units[prev] = units[pos];
            units[pos] = unitId;
        }

        public Team BuildTeam()
        {
            return new Team
            {
                Element = Data.Deck.Element,
                Slots = Data.Deck.Units.Select(id =>
                {
                    var u = Find(id);
                    if (u == null) return new TeamSlot { Unit = null, Skills = new string[0] };
                    return new TeamSlot
                    {
                        Unit = id,
                        Skills = u.Equipped.Select(uid => FindSkill(uid)?.Skill).ToArray(),
                        Def = EffectiveDef(u),
                    };
                }).ToArray(),
            };
        }

        /// <summary>문제가 없으면 빈 목록.</summary>
        public List<string> ValidateDeck() => Catalog.ValidateTeam(BuildTeam());

        // --- 전투 ---

        // 임시 상대: 무작위 무인 3명 + 무작위 속성, 추가 무공 없음
        public Team RandomOpponent()
        {
            var picks = Catalog.Units.OrderBy(_ => rng.Next()).Take(3).ToList();
            return new Team
            {
                Element = BattleRules.Elements[rng.Next(BattleRules.Elements.Length)],
                Slots = picks.Select(d => new TeamSlot { Unit = d.Id, Skills = new string[0] }).ToArray(),
            };
        }

        public BattleResult Fight(Team opponent)
        {
            var errors = ValidateDeck();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));
            return new BattleEngine(BuildTeam(), opponent, rng.Next(), log: true).Run();
        }
    }
}
