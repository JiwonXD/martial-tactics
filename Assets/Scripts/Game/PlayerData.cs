using System;
using System.Collections.Generic;

namespace MartialTactics.Game
{
    public enum StatKind { Ext, Int, Def, Spd } // 외공, 내공, 호신, 경공

    /// <summary>보유 무인. 같은 무인을 또 뽑으면 Spare(중복 카드)가 늘고, 승급이나 무공 추출에 쓴다.</summary>
    [Serializable]
    public sealed class OwnedUnit
    {
        public string Id;
        public int Spare;
        public int Promotions;                  // 승급 횟수 = 붉은 별 수
        public int FreePoints;                  // 아직 안 찍은 자유 스탯
        public int[] Added = new int[4];        // StatKind 순서로 찍은 자유 스탯
        public int[] Equipped = { -1, -1 };     // 무공창 2·3칸에 장착한 추출 무공 Uid, -1 = 비움
    }

    /// <summary>무공 추출로 얻은 무공 한 개. 한 번에 한 무인만 장착할 수 있다.</summary>
    [Serializable]
    public sealed class ExtractedSkill
    {
        public int Uid;
        public string Skill;
    }

    /// <summary>Units[0] = 주장. 빈 문자열 = 비움.</summary>
    [Serializable]
    public sealed class Deck
    {
        public string Element = "";
        public string[] Units = { "", "", "" };
    }

    /// <summary>저장되는 플레이어 진행 상황 전부.</summary>
    [Serializable]
    public sealed class PlayerData
    {
        public List<OwnedUnit> Units = new List<OwnedUnit>();
        public List<ExtractedSkill> Skills = new List<ExtractedSkill>();
        public Deck Deck = new Deck();
        public int NextUid = 1;
        public int Draws;
    }
}
