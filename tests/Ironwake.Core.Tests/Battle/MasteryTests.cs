using Ironwake.Core.Tests.Maps;
using Ironwake.Sim;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 69's class mastery: a class naming a mastery ability names the points that earn
/// it, a player unit earns one point per combat fought in the class, and the ability it
/// earns is its own after it leaves the class. The requirements here are test numbers:
/// the shipped classes' masteries and requirements are content, tested in ShippedMasteryTests.
/// </summary>
public class MasteryTests
{
    private static GameContent WithCadetMastery(string ability, int points) =>
        Starter with { Classes = Starter.Classes.SetItem("cadet", Starter.Class("cadet") with { Mastery = ability, MasteryPoints = points }) };

    /// <summary>The shipped content with the cadet naming no mastery, since every shipped class names one.</summary>
    private static GameContent WithoutCadetMastery() =>
        Starter with { Classes = Starter.Classes.SetItem("cadet", Starter.Class("cadet") with { Mastery = null, MasteryPoints = 0 }) };

    private static BattleState Beside(GameContent content, Unit? captain = null, ulong seed = 7)
    {
        var state = BattleState.From(YardMap, content, ValueList<Unit>.Of(captain ?? Hale, Wren), seed);
        return Resolver.Apply(state, content, new Move("hale", new Coord(2, 1))).Next;
    }

    [Fact]
    public void ACombatEarnsOneMasteryPointInTheUnitsClass()
    {
        var content = WithCadetMastery("axebreaker", 10);
        var cadet = content.Class("cadet");

        var (unit, mastered) = Masteries.ForCombat(Hale, cadet);

        Assert.Equal(1, Masteries.PerCombat);
        Assert.Equal(1, unit.Mastery.Points("cadet"));
        Assert.Null(mastered);
        Assert.DoesNotContain("axebreaker", unit.Abilities);
    }

    [Fact]
    public void AClassWithNoMasteryEarnsNoPoints()
    {
        var (unit, mastered) = Masteries.ForCombat(Hale, WithoutCadetMastery().Class("cadet"));

        Assert.Equal(Hale, unit);
        Assert.Null(mastered);
    }

    [Fact]
    public void ReachingTheRequirementGrantsTheAbility()
    {
        var cadet = WithCadetMastery("axebreaker", 3).Class("cadet");
        var unit = Hale;
        string? mastered = null;
        for (var combat = 0; combat < 3; combat++)
        {
            Assert.Null(mastered);
            (unit, mastered) = Masteries.ForCombat(unit, cadet);
        }

        Assert.Equal("axebreaker", mastered);
        Assert.Contains("axebreaker", unit.Abilities);
        Assert.Equal(3, unit.Mastery.Points("cadet"));
    }

    [Fact]
    public void AMasteredClassEarnsNothingMore()
    {
        var cadet = WithCadetMastery("axebreaker", 1).Class("cadet");
        var (once, _) = Masteries.ForCombat(Hale, cadet);

        var (twice, again) = Masteries.ForCombat(once, cadet);

        Assert.Equal(once, twice);
        Assert.Null(again);
        Assert.Single(twice.Abilities, "axebreaker");
    }

    [Fact]
    public void MasteryPointsAreEarnedOnlyInTheUnitsOwnClass()
    {
        var content = WithCadetMastery("axebreaker", 3);

        Assert.Throws<ArgumentException>(() => Masteries.ForCombat(Hale, content.Class("pikeman")));
    }

    [Fact]
    public void AMasteryIsRetainedAfterAClassChange()
    {
        var content = WithCadetMastery("axebreaker", 1);
        var (mastered, _) = Masteries.ForCombat(Hale, content.Class("cadet"));

        var pikeman = mastered with { ClassId = "pikeman" };

        Assert.Contains("axebreaker", pikeman.Abilities);
        Assert.Contains(content.AbilitiesOf(pikeman), a => a.Id == "axebreaker");
        Assert.Equal(1, pikeman.Mastery.Points("cadet"));
        Assert.Equal(0, pikeman.Mastery.Points("pikeman"));
    }

    [Fact]
    public void BothSidesOfACombatEarnAPointAndAnEnemyEarnsNothing()
    {
        var content = WithCadetMastery("axebreaker", 10);
        var state = Beside(content) with { Phase = Side.Enemy };

        var result = Resolver.Apply(state, content, new Attack("brigand-1", "hale"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        if (result.Next.Find("hale") is { } hale)
        {
            Assert.Equal(1, hale.Unit.Mastery.Points("cadet"));
        }

        Assert.Equal(MasteryProgress.Empty, result.Next.Find("brigand-1")?.Unit.Mastery ?? MasteryProgress.Empty);
        Assert.Equal(0, result.Next.Find("wren")!.Unit.Mastery.Points("cadet"));
    }

    [Fact]
    public void TheCombatThatReachesTheRequirementEmitsMasteryEarnedAndTheBreakerIsLiveAtOnce()
    {
        var content = WithCadetMastery("axebreaker", 1);
        var state = Beside(content);
        var before = Queries.Forecast(state, content, state.Find("hale")!, state.Find("brigand-1")!)!;

        var result = Resolver.Apply(state, content, new Attack("hale", "brigand-1"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Contains(new MasteryEarned("hale", "cadet", "axebreaker"), result.Events);
        if (result.Next.Find("brigand-1") is { } brigand)
        {
            var after = Queries.Forecast(result.Next, content, result.Next.Find("hale")!, brigand)!;
            Assert.Equal(Math.Clamp(before.Attacker.HitChance + 20, 0, 100), after.Attacker.HitChance);
        }
    }

    [Fact]
    public void AHealIsNotACombatAndEarnsNoMasteryPoint()
    {
        var chaplain = Starter.Class("chaplain");
        var content = Starter with { Classes = Starter.Classes.SetItem("chaplain", chaplain with { Mastery = "faithbreaker", MasteryPoints = 5 }) };
        var mira = Recruit("mira", "chaplain", new Stats(16, 1, 4, 4, 4, 3, 1, 5, 3), "radiance", "salve");
        var state = BattleState.From(MapFixture.Parse(Yard.Replace("recruit:wren", "recruit"), "yard.map"), content, ValueList<Unit>.Of(Hale, mira), 7);
        var hale = state.Find("hale") ?? state.UnitsOf(Side.Player).First(u => u.Id != "mira");
        var hurt = state.WithUnit(hale with { Hp = 5 });
        Assert.Equal(1, hurt.Find("mira")!.At.DistanceTo(hale.At));

        var result = Resolver.Apply(hurt, content, new UseItem("mira", 1, hale.Id));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(0, result.Next.Find("mira")!.Unit.Mastery.Points("chaplain"));
    }

    [Fact]
    public void AMasteryThatRaisesMaxHpRaisesCurrentHpByAsMuch()
    {
        var tough = new Ability("tough", "Tough", "HP +3.", new StatDeltaEffect(Stats.Zero with { Hp = 3 }));
        var content = WithCadetMastery("tough", 1) with { Abilities = Starter.Abilities.Add(tough.Id, tough) };
        var state = Beside(content) with { Phase = Side.Enemy };
        var hale = state.Find("hale")!;

        var result = Resolver.Apply(state, content, new Attack("brigand-1", "hale"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        var fought = result.Events.OfType<CombatFought>().Single();
        if (result.Next.Find("hale") is { } after)
        {
            Assert.Equal(fought.TargetHpAfter + 3, after.Hp);
            Assert.Equal(hale.MaxHp(Starter) + 3, after.MaxHp(content));
        }
    }

    [Fact]
    public void AMasteryThatLowersMaxHpCapsCurrentHpAtTheNewMax()
    {
        var frail = new Ability("frail", "Frail", "HP -30.", new StatDeltaEffect(Stats.Zero with { Hp = -30 }));
        var content = WithCadetMastery("frail", 1) with { Abilities = Starter.Abilities.Add(frail.Id, frail) };
        var sturdy = Hale with { Stats = Hale.Stats with { Hp = 60 } };
        var state = Beside(content, sturdy) with { Phase = Side.Enemy };

        var result = Resolver.Apply(state, content, new Attack("brigand-1", "hale"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        var after = result.Next.Find("hale")!;
        Assert.Contains(new MasteryEarned("hale", "cadet", "frail"), result.Events);
        Assert.Equal(after.MaxHp(content), after.Hp);
        Assert.True(after.Hp < result.Events.OfType<CombatFought>().Single().TargetHpAfter);
    }

    [Fact]
    public void NegativeMasteryPointsAreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MasteryProgress.Empty.With("cadet", -1));
    }

    [Fact]
    public void MasteryProgressIsValueEqualAndOrderedByClass()
    {
        var one = MasteryProgress.Empty.With("pikeman", 2).With("cadet", 3);
        var two = MasteryProgress.Empty.With("cadet", 3).With("pikeman", 2);

        Assert.Equal(one, two);
        Assert.Equal(new[] { "cadet", "pikeman" }, one.All.Select(p => p.ClassId));
        Assert.Equal(MasteryProgress.Empty, one.With("cadet", 0).With("pikeman", 0));
    }

    [Fact]
    public void TwoSimRunsOfTheSameSeedEndWithIdenticalMasteryPoints()
    {
        var content = Starter with
        {
            Classes = Starter.Classes.Values.Aggregate(Starter.Classes, (all, c) => all.SetItem(c.Id, c with { Mastery = "axebreaker", MasteryPoints = 2 })),
        };
        var map = MapFixture.Parse(File.ReadAllText(Path.Combine(MapFixture.MapsDirectory, "old_mill_road.map")), "old_mill_road.map");

        var first = Runner.Play(content, map, 11, new HeuristicPlayer());
        var second = Runner.Play(content, map, 11, new HeuristicPlayer());

        Assert.Equal(first.Masteries.Keys, second.Masteries.Keys);
        Assert.All(first.Masteries, pair => Assert.Equal(pair.Value, second.Masteries[pair.Key]));
        Assert.Contains(first.Masteries.Values, m => m.All.Any());
    }

    [Fact]
    public void ShowPrintsTheMasteryAgainstItsRequirementThenMastered()
    {
        var content = WithCadetMastery("axebreaker", 12);
        var hale = BattleState.From(YardMap, content, ValueList<Unit>.Of(Hale, Wren), 7).Find("hale")!;
        var three = hale with { Unit = hale.Unit with { Mastery = MasteryProgress.Empty.With("cadet", 3) } };
        var done = hale with { Unit = hale.Unit with { Abilities = ValueList<string>.Of("axebreaker") } };

        Assert.Null(Ironwake.Cli.PlaySession.MasteryLine(hale, WithoutCadetMastery()));
        Assert.Equal("  mastery: Axebreaker (3 of 12 combats)", Ironwake.Cli.PlaySession.MasteryLine(three, content));
        Assert.Equal("  mastery: Axebreaker, mastered", Ironwake.Cli.PlaySession.MasteryLine(done, content));
    }

    [Fact]
    public void TheConsoleNamesAMasteryEarned()
    {
        Assert.Equal("hale masters the cadet class and keeps axebreaker", Ironwake.Cli.PlaySession.Describe(new MasteryEarned("hale", "cadet", "axebreaker")));
    }
}
