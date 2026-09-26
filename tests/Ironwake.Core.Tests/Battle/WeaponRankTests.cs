using Ironwake.Core.Tests.Maps;
using Ironwake.Sim;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 67: weapon skill ranks E to S, gained by use. The thresholds and awards are the
/// issue's provisional numbers: 3 points a combat, 5 for a kill; E 0, D 30, C 80, B 160,
/// A 280, S 450.
/// </summary>
public class WeaponRankTests
{
    [Theory]
    [InlineData(0, WeaponRank.E)]
    [InlineData(29, WeaponRank.E)]
    [InlineData(30, WeaponRank.D)]
    [InlineData(79, WeaponRank.D)]
    [InlineData(80, WeaponRank.C)]
    [InlineData(159, WeaponRank.C)]
    [InlineData(160, WeaponRank.B)]
    [InlineData(279, WeaponRank.B)]
    [InlineData(280, WeaponRank.A)]
    [InlineData(449, WeaponRank.A)]
    [InlineData(450, WeaponRank.S)]
    [InlineData(9000, WeaponRank.S)]
    public void TheRankIsTheHighestThresholdThePointsReach(int points, WeaponRank rank)
    {
        Assert.Equal(rank, WeaponRanks.RankAt(points));
    }

    [Theory]
    [InlineData(WeaponRank.E, 0)]
    [InlineData(WeaponRank.D, 30)]
    [InlineData(WeaponRank.C, 80)]
    [InlineData(WeaponRank.B, 160)]
    [InlineData(WeaponRank.A, 280)]
    [InlineData(WeaponRank.S, 450)]
    public void EachRankBeginsAtItsThreshold(WeaponRank rank, int threshold)
    {
        Assert.Equal(threshold, WeaponRanks.Threshold(rank));
    }

    [Fact]
    public void ACombatEarnsThreeAndAKillEarnsFive()
    {
        Assert.Equal(3, WeaponRanks.ForCombat(killed: false));
        Assert.Equal(5, WeaponRanks.ForCombat(killed: true));
    }

    [Fact]
    public void NegativeRankPointsAreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WeaponRanks.RankAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => WeaponSkill.Zero.With(WeaponType.Sword, -1));
    }

    [Fact]
    public void APlayerUnitEarnsRankPointsOncePerCombatInItsWeaponsTypeAndAnEnemyEarnsNothing()
    {
        var state = Start().Do(new Move("hale", new Coord(2, 1)));
        var brigand = state.Find("brigand-1")!;
        var captain = state.Find("hale")!;
        var sword = captain.EquippedWeapon(Starter)!.Type;

        var result = state.Try(new Attack("hale", "brigand-1"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        var fought = result.Events.OfType<CombatFought>().Single();
        Assert.True(fought.Strikes.Count(s => s.AttackerId == "hale") >= 1);
        var after = result.Next.Find("hale")!.Unit.Skill;
        Assert.Equal(fought.TargetHpAfter == 0 ? 5 : 3, after.Points(sword));
        Assert.Equal(0, after.All.Where(t => t.Type != sword).Sum(t => t.Points));
        if (result.Next.Find("brigand-1") is { } survivor)
        {
            Assert.Equal(brigand.Unit.Skill, survivor.Unit.Skill);
        }
    }

    [Fact]
    public void CrossingAThresholdRaisesTheRankWithAnEvent()
    {
        var state = Start().Do(new Move("hale", new Coord(2, 1)));
        var captain = state.Find("hale")!;
        var near = state.WithUnit(captain with { Unit = captain.Unit with { Skill = WeaponSkill.Zero.With(WeaponType.Sword, 27) } });

        var result = near.Try(new Attack("hale", "brigand-1"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Contains(new RankRaised("hale", WeaponType.Sword, WeaponRank.D), result.Events);
        Assert.Equal(WeaponRank.D, result.Next.Find("hale")!.Unit.Skill.Rank(WeaponType.Sword));

        var far = state.Try(new Attack("hale", "brigand-1"));
        Assert.DoesNotContain(far.Events, e => e is RankRaised);
    }

    [Fact]
    public void AUnitThatMadeNoStrikeEarnsNothing()
    {
        var bow = Recruit("wren", "bowman", new Stats(20, 7, 0, 6, 8, 5, 4, 2, 3), "iron_bow");
        var state = Start(roster: ValueList<Unit>.Of(Hale, bow));
        var wren = state.Find("wren")!;
        var beside = state.WithUnit(wren with { At = new Coord(2, 1) }) with { Phase = Side.Enemy };

        var result = beside.Try(new Attack("brigand-1", "wren"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.DoesNotContain(result.Events.OfType<CombatFought>().Single().Strikes, s => s.AttackerId == "wren");
        Assert.Equal(0, result.Next.Find("wren")?.Unit.Skill.Points(WeaponType.Bow) ?? 0);
    }

    [Fact]
    public void AWeaponAboveTheUnitsRankIsRefusedNamingUnitWeaponAndRank()
    {
        var content = WithRank("steel_sword", WeaponRank.D);
        var twoSwords = Recruit("hale", new Stats(22, 8, 0, 7, 8, 6, 5, 2, 9), "iron_sword", "steel_sword");
        var state = BattleState.From(YardMap, content, ValueList<Unit>.Of(twoSwords, Wren), 7);
        state = Resolver.Apply(state, content, new Move("hale", new Coord(2, 1))).Next;

        var refused = Resolver.Apply(state, content, new Attack("hale", "brigand-1", 1));

        Assert.False(refused.Accepted);
        Assert.Equal(RejectionReason.NotUsable, refused.Rejection!.Reason);
        Assert.Equal("hale cannot attack with steel_sword: rank E in sword, and Steel Sword needs D", refused.Rejection.Message);

        var hale = state.Find("hale")!;
        var ranked = state.WithUnit(hale with { Unit = hale.Unit with { Skill = WeaponSkill.Zero.With(WeaponType.Sword, 30) } });
        Assert.True(Resolver.Apply(ranked, content, new Attack("hale", "brigand-1", 1)).Accepted);
    }

    [Fact]
    public void AWeaponAboveTheUnitsRankIsNeverEquipped()
    {
        var content = WithRank("steel_sword", WeaponRank.D);
        var steelOnly = Recruit("hale", new Stats(22, 8, 0, 7, 8, 6, 5, 2, 9), "steel_sword");
        var state = BattleState.From(YardMap, content, ValueList<Unit>.Of(steelOnly, Wren), 7);
        var hale = state.Find("hale")!;

        Assert.Null(hale.EquippedWeapon(content));
        Assert.False(steelOnly.CanWield(content.Weapon("steel_sword"), content.Class("cadet")));
        Assert.True((steelOnly with { Skill = WeaponSkill.Zero.With(WeaponType.Sword, 30) }).CanWield(content.Weapon("steel_sword"), content.Class("cadet")));
    }

    [Fact]
    public void AHealEarnsFaithPoints()
    {
        var mira = Recruit("mira", "chaplain", new Stats(16, 1, 4, 4, 4, 3, 1, 5, 3), "radiance", "salve");
        var state = Start(roster: ValueList<Unit>.Of(Hale, mira), map: Yard.Replace("recruit:wren", "recruit"));
        var hale = state.Find("hale") ?? state.UnitsOf(Side.Player).First(u => u.Id != "mira");
        var hurt = state.WithUnit(hale with { Hp = 5 });
        Assert.Equal(1, hurt.Find("mira")!.At.DistanceTo(hale.At));

        var result = hurt.Try(new UseItem("mira", 1, hale.Id));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(3, result.Next.Find("mira")!.Unit.Skill.Points(WeaponType.Faith));
    }

    [Fact]
    public void TwoSimRunsOfTheSameSeedEndWithIdenticalRankTotals()
    {
        var map = MapFixture.Parse(File.ReadAllText(Path.Combine(MapFixture.MapsDirectory, "old_mill_road.map")), "old_mill_road.map");

        var first = Runner.Play(Starter, map, 11, new HeuristicPlayer());
        var second = Runner.Play(Starter, map, 11, new HeuristicPlayer());

        Assert.Equal(first.Skills.Keys, second.Skills.Keys);
        Assert.All(first.Skills, pair => Assert.Equal(pair.Value, second.Skills[pair.Key]));
        Assert.True(first.Skills.Values.Any(skill => skill.All.Any(t => t.Points > 0)), $"{first.Result} {first.Turns} {string.Join(";", first.Skills.Select(p => p.Key + ":" + string.Join(",", p.Value.All.Select(t => t.Points))))}");
    }

    [Fact]
    public void TheConsoleNamesARankRaised()
    {
        Assert.Equal("wren reaches rank D in sword", Ironwake.Cli.PlaySession.Describe(new RankRaised("wren", WeaponType.Sword, WeaponRank.D), Starter));
    }

    private static GameContent WithRank(string weaponId, WeaponRank rank) =>
        Starter with { Weapons = Starter.Weapons.SetItem(weaponId, Starter.Weapon(weaponId) with { Rank = rank }) };
}
