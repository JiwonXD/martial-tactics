// 게임 진행 로직 테스트.
//   dotnet run --project Tests/GameLogic
using System;
using System.Linq;
using MartialTactics.Battle;
using MartialTactics.Game;

static class Program
{
    static int failures;

    static int Main()
    {
        var g = new GameState(new PlayerData(), 7);

        // 뽑기: 새 무인은 보유 목록에, 이미 있는 무인은 중복 카드로
        var draws = g.Draw(200);
        Check(g.Data.Draws == 200, "뽑기 횟수");
        Check(g.Data.Units.Select(u => u.Id).Distinct().Count() == g.Data.Units.Count, "보유 무인 중복 없음");
        Check(draws.Count(r => r.New) == g.Data.Units.Count, "새 무인 수 = 보유 무인 수");
        Check(g.Data.Units.Sum(u => u.Spare) == draws.Count(r => !r.New), "중복 카드 수");

        // 승급: 카드 1장, 별 수만큼, 승급당 자유 스탯 10
        var u = g.Data.Units.OrderByDescending(x => x.Spare).First();
        int spare = u.Spare, stars = GameState.Stars(u.Id);
        Check(spare > stars + 1, "테스트용 중복 카드 충분");
        for (int i = 0; i < stars; i++) g.Promote(u);
        Check(u.Promotions == stars && u.Spare == spare - stars && u.FreePoints == stars * 10, "승급");
        Check(!g.CanPromote(u), "별 수보다 더 승급 불가");
        Throws(() => g.Promote(u), "최대 승급 후 승급 거부");

        g.AddStat(u, StatKind.Ext);
        g.AddStat(u, StatKind.Spd);
        var e = GameState.EffectiveDef(u);
        var d = Catalog.Unit[u.Id];
        Check(e.Ext == d.Ext + 1 && e.Spd == d.Spd + 1 && e.Int == d.Int && e.Hp == d.Hp && u.FreePoints == stars * 10 - 2, "자유 스탯 반영");

        // 추출: 카드 1장 소모, 그 무인의 무공
        var a = g.Data.Units.First(x => x != u && x.Spare > 0);
        int aSpare = a.Spare;
        var sk = g.Extract(a);
        Check(a.Spare == aSpare - 1 && sk.Skill == Catalog.Unit[a.Id].Skill, "추출");
        var empty = g.Data.Units.FirstOrDefault(x => x.Spare == 0);
        if (empty != null) Throws(() => g.Extract(empty), "중복 카드 없으면 추출 거부");

        // 장착: 자기 무공과 같으면 안 됨, 한 무인만 장착 (옮기면 원래 무인에서 빠짐)
        Check(g.EquipError(a, 0, sk) != null, "자기 무공은 장착 불가");
        g.Equip(u, 0, sk);
        Check(u.Equipped[0] == sk.Uid && g.EquippedBy(sk.Uid) == u, "장착");
        var b = g.Data.Units.First(x => x != u && x != a && Catalog.Unit[x.Id].Skill != sk.Skill);
        g.Equip(b, 1, sk);
        Check(u.Equipped[0] == -1 && b.Equipped[1] == sk.Uid, "다른 무인에게 옮기면 원래 무인에서 빠짐");
        g.Equip(b, 0, sk);
        Check(b.Equipped[0] == sk.Uid && b.Equipped[1] == -1, "같은 무인 안에서 칸 옮기기");
        var sk2 = g.Extract(a);
        Check(g.EquipError(b, 1, sk2) != null, "같은 무공 두 칸 장착 불가");

        // 덱: 속성·빈 자리 검증, 자리 맞바꾸기, 팀 안 같은 무공 금지
        Check(g.ValidateDeck().Count > 0, "빈 덱은 검증 실패");
        g.SetDeckUnit(0, u.Id); g.SetDeckUnit(1, b.Id); g.SetDeckUnit(2, a.Id);
        Check(g.ValidateDeck().Count > 0, "속성 없으면 검증 실패");
        g.Data.Deck.Element = "금";
        Check(g.ValidateDeck().Count > 0, "a의 고유 무공과 b의 추출 무공이 겹치면 실패");
        g.Unequip(b, 0);
        Check(g.ValidateDeck().Count == 0, "편성 완료: " + string.Join(" ", g.ValidateDeck()));
        g.SetDeckUnit(2, u.Id);
        Check(g.Data.Deck.Units[0] == a.Id && g.Data.Deck.Units[2] == u.Id, "덱 안에서 자리 맞바꾸기");

        // 전투: 승급 스탯이 전투에 들어감
        var opp = g.RandomOpponent();
        Check(Catalog.ValidateTeam(opp).Count == 0, "무작위 상대 편성 검증");
        var r = g.Fight(opp);
        Check(r.Log.Count > 0 && r.Rounds >= 1 && r.Rounds <= 8, "전투 실행");
        var mine = r.Units.First(x => x.Side == Side.A && x.Source.Id == u.Id);
        Check(mine.Source.Ext == d.Ext + 1, "전투에 승급 스탯 반영");

        Console.WriteLine(failures == 0 ? "게임 로직 테스트 통과" : $"실패 {failures}건");
        return failures == 0 ? 0 : 1;
    }

    static void Check(bool ok, string what)
    {
        if (!ok) { failures++; Console.WriteLine("실패: " + what); }
    }

    static void Throws(Action act, string what)
    {
        try { act(); Check(false, what); }
        catch (InvalidOperationException) { }
    }
}
