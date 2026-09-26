using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 72's class certification: a class names what it asks (a level, weapon ranks, stat
/// minimums), a unit that meets it certifies with no roll, and a unit that does not is
/// refused with the requirement named. The requirements here are test numbers; the shipped
/// ladder's are in ClassLadderTests.
/// </summary>
public class CertificationTests
{
    private static readonly CertificationRequirements Pike = new(
        5,
        ValueList<(WeaponType, WeaponRank)>.Of((WeaponType.Lance, WeaponRank.D)),
        Stats.Zero.With(Stat.Str, 8).With(Stat.Def, 5));

    private static GameContent WithPikemanRequirements(CertificationRequirements requirements) =>
        Starter with { Classes = Starter.Classes.SetItem("pikeman", Starter.Class("pikeman") with { Certification = requirements }) };

    /// <summary>A shipped class with its certification requirements removed.</summary>
    private static UnitClass Free(string id) => Starter.Class(id) with { Certification = CertificationRequirements.None };

    private static Unit Ready => Hale with
    {
        Level = 5,
        Skill = WeaponSkill.Zero.With(WeaponType.Lance, WeaponRanks.Threshold(WeaponRank.D)),
    };

    [Theory]
    [InlineData(5, "D", 8, 5, null)]
    [InlineData(4, "D", 8, 5, "needs level 5, has 4")]
    [InlineData(5, "E", 8, 5, "needs lance D, has E")]
    [InlineData(5, "C", 8, 5, null)]
    [InlineData(5, "D", 7, 5, "needs str 8, has 7")]
    [InlineData(5, "D", 8, 4, "needs def 5, has 4")]
    [InlineData(30, "S", 20, 20, null)]
    public void RequirementsAreMetAtTheirNumbersAndFailedBelowThem(int level, string rank, int str, int def, string? refusal)
    {
        var pikeman = WithPikemanRequirements(Pike).Class("pikeman");
        var unit = Hale with
        {
            Level = level,
            Stats = Hale.Stats with { Str = str, Def = def },
            Skill = WeaponSkill.Zero.With(WeaponType.Lance, WeaponRanks.Threshold(Enum.Parse<WeaponRank>(rank))),
        };

        var refusals = Certifications.Check(unit, pikeman);

        if (refusal is null)
        {
            Assert.Empty(refusals);
        }
        else
        {
            Assert.Equal(refusal, Assert.Single(refusals).Text);
        }
    }

    [Fact]
    public void EveryFailedRequirementIsListedInOrderWithItsField()
    {
        var pikeman = WithPikemanRequirements(Pike).Class("pikeman");
        var unit = Hale with { Stats = Hale.Stats with { Str = 6, Def = 2 } };

        var refusals = Certifications.Check(unit, pikeman);

        Assert.Equal(new[] { "level", "ranks.lance", "stats.str", "stats.def" }, refusals.Select(r => r.Requirement));
        Assert.Equal(
            new[] { "needs level 5, has 1", "needs lance D, has E", "needs str 8, has 6", "needs def 5, has 2" },
            refusals.Select(r => r.Text));
    }

    [Fact]
    public void ARefusedCertificationNamesTheFirstFailedRequirement()
    {
        var pikeman = WithPikemanRequirements(Pike).Class("pikeman");

        var e = Assert.Throws<InvalidOperationException>(() => Certifications.Certify(Ready with { Level = 3 }, pikeman));

        Assert.Equal("hale cannot certify as Pikeman: needs level 5, has 3", e.Message);
    }

    [Fact]
    public void AStatMinimumReadsTheUnitsOwnStatsNotItsClassModifiers()
    {
        var reaver = Starter.Class("reaver");
        var content = WithPikemanRequirements(Pike);
        var unit = Ready with { ClassId = "reaver", Stats = Hale.Stats with { Str = 7 } };

        Assert.Equal(9, unit.EffectiveStats(reaver).Str);
        Assert.Equal("needs str 8, has 7", Assert.Single(Certifications.Check(unit, content.Class("pikeman"))).Text);
    }

    [Fact]
    public void AClassThatNamesNoRequirementsTakesAnyUnit()
    {
        Assert.Equal(CertificationRequirements.None, Starter.Class("cadet").Certification);
        Assert.Empty(Certifications.Check(Hale, Free("outrider")));
    }

    [Fact]
    public void CertifyingIntoTheUnitsOwnClassIsRefused()
    {
        var refusal = Assert.Single(Certifications.Check(Hale, Starter.Class("cadet")));

        Assert.Equal("class", refusal.Requirement);
        Assert.Equal("hale is already a Cadet", refusal.Text);
    }

    [Fact]
    public void CertifyingChangesTheClassAndNothingElse()
    {
        var pikeman = WithPikemanRequirements(Pike).Class("pikeman");

        var certified = Certifications.Certify(Ready, pikeman);

        Assert.Equal(Ready with { ClassId = "pikeman" }, certified);
        Assert.Equal(Ready.Stats + pikeman.Modifiers, certified.EffectiveStats(pikeman));
    }

    [Fact]
    public void MasteryIsRetainedAcrossCertification()
    {
        var content = Starter with { Classes = Starter.Classes.SetItem("cadet", Starter.Class("cadet") with { Mastery = "axebreaker", MasteryPoints = 2 }) };
        var unit = Ready;
        (unit, _) = Masteries.ForCombat(unit, content.Class("cadet"));
        (unit, _) = Masteries.ForCombat(unit, content.Class("cadet"));

        var certified = Certifications.Certify(unit, Free("outrider"));

        Assert.Contains("axebreaker", certified.Abilities);
        Assert.Equal(2, certified.Mastery.Points("cadet"));
        Assert.Equal(unit.Mastery, certified.Mastery);
    }

    [Fact]
    public void TheNextMapReadsTheNewClassesMovAndWeapons()
    {
        var wren = Certifications.Certify(Wren, Free("outrider"));
        var state = BattleState.From(YardMap, Starter, ValueList<Unit>.Of(Hale, wren), 7);

        var unit = state.Find("wren")!;
        var reach = Queries.Reachable(state, Starter, unit);

        Assert.Equal(MovementType.Cavalry, reach.Movement);
        Assert.Equal(6, reach.Mov);
        Assert.True(Wren.CanWield(Starter.Weapon("hatchet"), Starter.Class("cadet")));
        Assert.False(wren.CanWield(Starter.Weapon("hatchet"), Starter.Class(wren.ClassId)));
        Assert.True(wren.CanWield(Starter.Weapon("iron_lance"), Starter.Class(wren.ClassId)));
    }
}
