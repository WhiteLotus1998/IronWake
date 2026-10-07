using System.Collections.Immutable;
using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// A school's rider and fire's burn (issue 1243, DECISIONS/0297): the rider is data on the school in
/// <c>rules.json</c>, a tome carries its school's, and a hit (not a miss, not a kill) from a burn
/// rider's tome leaves the target losing its amount at each of its side's next phase starts, never
/// below 1, refreshed and never stacked, and never stacked with tile fire. The shipped fire school
/// carries no rider until the Table turns it on, so these tests give it one: 2 for two phases.
/// Played on the sample <c>the_tollgate_frost.map</c>, Pell with Cinder against the woods brigand.
/// </summary>
public class BurningTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly SchoolRider FireBurn = new(RiderKind.Burn, 2, 2);

    private static readonly GameContent Burns = Shipped with
    {
        Riders = ImmutableSortedDictionary<MagicSchool, SchoolRider>.Empty.Add(MagicSchool.Fire, FireBurn),
    };

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_tollgate_frost.map");

    private const string RiderRules = """
        { "wakeRadius": 4, "schools": { "fire": { "rider": { "kind": "burn", "amount": 2, "phases": 2 } }, "ice": {} } }
        """;

    /// <summary>Pell two tiles below the woods brigand at 6,5, out of its axe's reach.</summary>
    private static BattleState Facing(ulong seed = 1243)
    {
        var state = BattleState.From(MapFiles.Load(SamplePath, Burns), Burns, Burns.Cast, seed);
        return state.WithUnit(state.Find("pell")! with { At = new Coord(6, 7) });
    }

    private static BattleUnit Brigand(BattleState state) => state.Units.Single(u => u.Side == Side.Enemy && u.Group == "woods" && u.Unit.ClassId == Shipped.Unit("toll_brigand").ClassId);

    /// <summary>A board where Pell's Cinder hit the brigand and left it standing, and the result.</summary>
    private static (BattleState Before, ApplyResult Result) Struck()
    {
        for (ulong seed = 1; seed < 400; seed++)
        {
            var state = Facing(seed);
            var result = Resolver.Apply(state, Burns, new Attack("pell", Brigand(state).Id));
            if (result.Next.Find(Brigand(state).Id) is { BurnPhases: > 0 })
            {
                return (state, result);
            }
        }

        throw new InvalidOperationException("no seed under 400 gave a surviving hit");
    }

    [Fact]
    public void ASchoolsRiderLoadsFromRulesAndEveryTomeOfTheSchoolCarriesIt()
    {
        var content = ContentLoader.Parse(Fixture.Files(rules: RiderRules));

        Assert.Equal(FireBurn, content.Riders[MagicSchool.Fire]);
        Assert.False(content.Riders.ContainsKey(MagicSchool.Ice));
        Assert.Equal(FireBurn, Burns.RiderOf(Burns.Weapon("cinder")));
        Assert.Null(Burns.RiderOf(Burns.Weapon("bolt")));
        Assert.Null(Burns.RiderOf(Burns.Weapon("iron_lance")));
    }

    [Fact]
    public void TheShippedFireSchoolCarriesNoRiderUntilTheTableTurnsItOn()
    {
        Assert.Empty(Shipped.Riders);
        Assert.Null(Shipped.RiderOf(Shipped.Weapon("cinder")));
    }

    [Theory]
    [InlineData("\"kind\": \"burn\"", "\"kind\": \"smoulder\"", "rider.kind", "unknown rider kind 'smoulder'")]
    [InlineData("\"amount\": 2", "\"amount\": 0", "rider.amount", "at least 1")]
    [InlineData("\"phases\": 2", "\"phases\": 0", "rider.phases", "at least 1")]
    [InlineData("\"phases\": 2", "\"phases\": 2, \"gate\": 3", "rider.gate", "is not a rider field")]
    public void ABadRiderIsRefusedAtLoadNamingFileEntryAndField(string from, string to, string field, string why)
    {
        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(rules: RiderRules.Replace(from, to))));

        Assert.Contains(ContentFiles.RulesName, error.Message);
        Assert.Contains("schools.fire", error.Message);
        Assert.Contains(field, error.Message);
        Assert.Contains(why, error.Message);
    }

    [Fact]
    public void AnUnknownSchoolInTheBlockIsRefusedAtLoad()
    {
        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(rules: RiderRules.Replace("\"ice\"", "\"sand\""))));

        Assert.Contains("schools", error.Message);
        Assert.Contains("'sand' is not one of", error.Message);
    }

    [Fact]
    public void TheSchoolsBlockRoundTripsThroughTheSerializer()
    {
        var content = ContentLoader.Parse(Fixture.Files(rules: RiderRules));
        var again = ContentLoader.Parse(ContentSerializer.Write(content));

        Assert.Equal(content.Riders, again.Riders);
    }

    [Fact]
    public void AHitFromABurningSchoolSetsTheTargetBurningAndAMissDoesNot()
    {
        var (hit, miss) = (false, false);
        for (ulong seed = 1; seed < 400 && !(hit && miss); seed++)
        {
            var state = Facing(seed);
            var brigand = Brigand(state);
            var result = Resolver.Apply(state, Burns, new Attack("pell", brigand.Id));
            Assert.True(result.Accepted, result.Rejection?.Message);
            if (result.Next.Find(brigand.Id) is not { } after)
            {
                Assert.DoesNotContain(result.Events, e => e is UnitIgnited);
                continue;
            }

            var landed = result.Events.OfType<CombatFought>().Single().Strikes.Any(s => s.AttackerId == "pell" && s.Hit);
            Assert.Equal(landed ? (2, 2) : (0, 0), (after.Burn, after.BurnPhases));
            Assert.Equal(landed, result.Events.Contains(new UnitIgnited(brigand.Id, "pell", 2, 2)));
            hit |= landed;
            miss |= !landed;
        }

        Assert.True(hit && miss, "no seed under 400 gave both a surviving hit and a full miss");
    }

    [Fact]
    public void ATomeWithoutARiderNeverBurns()
    {
        for (ulong seed = 1; seed < 40; seed++)
        {
            var state = BattleState.From(MapFiles.Load(SamplePath, Shipped), Shipped, Shipped.Cast, seed);
            state = state.WithUnit(state.Find("pell")! with { At = new Coord(6, 7) });
            var result = Resolver.Apply(state, Shipped, new Attack("pell", Brigand(state).Id));

            Assert.DoesNotContain(result.Events, e => e is UnitIgnited);
        }
    }

    [Fact]
    public void TheBurnTicksAtEachOfItsSidesNextTwoPhaseStartsThenClears()
    {
        var (before, result) = Struck();
        var id = Brigand(before).Id;
        var hp = result.Next.Find(id)!.Hp;

        var enemy = Resolver.Apply(result.Next, Burns, new EndPhase());
        Assert.Contains(new UnitBurned(id, 2, hp - 2), enemy.Events);
        Assert.Equal((2, 1), (enemy.Next.Find(id)!.Burn, enemy.Next.Find(id)!.BurnPhases));

        var player = Resolver.Apply(enemy.Next, Burns, new EndPhase());
        Assert.DoesNotContain(player.Events, e => e is UnitBurned);
        Assert.Equal(hp - 2, player.Next.Find(id)!.Hp);

        var second = Resolver.Apply(player.Next, Burns, new EndPhase());
        Assert.Contains(new UnitBurned(id, 2, hp - 4), second.Events);
        Assert.Equal((0, 0), (second.Next.Find(id)!.Burn, second.Next.Find(id)!.BurnPhases));

        var third = Resolver.Apply(Resolver.Apply(second.Next, Burns, new EndPhase()).Next, Burns, new EndPhase());
        Assert.DoesNotContain(third.Events, e => e is UnitBurned);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 1)]
    public void ABurnNeverTakesAUnitBelowOneHp(int hp, int after)
    {
        var state = Facing();
        var brigand = Brigand(state) with { Hp = hp, Burn = 2, BurnPhases = 2 };
        var result = Resolver.Apply(state.WithUnit(brigand), Burns, new EndPhase());

        Assert.Equal(after, result.Next.Find(brigand.Id)!.Hp);
        Assert.Equal(hp > 1, result.Events.Any(e => e is UnitBurned));
    }

    [Fact]
    public void ASecondBurnRefreshesTheCountAndNeverStacks()
    {
        for (ulong seed = 1; seed < 400; seed++)
        {
            var state = Facing(seed);
            var brigand = Brigand(state) with { Burn = 2, BurnPhases = 1 };
            var result = Resolver.Apply(state.WithUnit(brigand), Burns, new Attack("pell", brigand.Id));
            if (result.Next.Find(brigand.Id) is { } after && result.Events.Any(e => e is UnitIgnited))
            {
                Assert.Equal((2, 2), (after.Burn, after.BurnPhases));
                return;
            }
        }

        Assert.Fail("no seed under 400 gave a surviving hit");
    }

    [Fact]
    public void ABurningUnitOnAFireTileLosesTheLargerOfTheTwoOnce()
    {
        var state = Facing();
        var brigand = Brigand(state) with { Burn = 2, BurnPhases = 2 };
        state = state.WithUnit(brigand) with { Map = state.Map.WithTerrain(brigand.At, Wildfire.FireTerrainId) };
        var max = brigand.MaxHp(Burns);
        var tile = state.Map.TerrainAt(brigand.At, Burns).BurnFor(max);
        Assert.True(tile > 0);

        var result = Resolver.Apply(state, Burns, new EndPhase());

        var burnt = Assert.Single(result.Events.OfType<UnitBurned>());
        Assert.Equal(Math.Min(brigand.Hp - 1, Math.Max(tile, 2)), burnt.Amount);
    }

    [Fact]
    public void TheForecastNamesTheRiderAndTheCardTheBurnLeft()
    {
        var state = Facing();
        var (pell, target) = (state.Find("pell")!, Brigand(state));
        var (riders, counterRiders) = PlaySession.Riders(Burns, pell, target, null);
        Assert.Equal((" burns 2 for two phases", ""), (riders, counterRiders));
        var forecast = Core.Combat.Forecast(
            Burns.CombatantOf(pell.Unit, pell.EquippedWeapon(Burns), state.Map.TerrainAt(pell.At, Burns), pell.Hp),
            Burns.CombatantOf(target.Unit, target.EquippedWeapon(Burns), state.Map.TerrainAt(target.At, Burns), target.Hp),
            2,
            state.Scheme);
        Assert.Contains("burns 2 for two phases; counter", PlaySession.ForecastLine(pell, target, forecast, riders: riders));
        Assert.Equal(("", ""), PlaySession.Riders(Shipped, pell, target, null));

        var (_, result) = Struck();
        var brigand = Brigand(result.Next);
        Assert.Equal("burning: 2 for two more phases", Burning.CardLine(brigand));
        Assert.Equal("burning: 2 for one more phase", Burning.CardLine(brigand with { BurnPhases = 1 }));
        Assert.Null(Burning.CardLine(brigand with { Burn = 0, BurnPhases = 0 }));
    }

    [Fact]
    public void ThePlannerPricesTheExpectedBurnOnAStrikeThatDoesNotKill()
    {
        var state = Facing();
        var pell = state.Find("pell")!;
        var brigand = Brigand(state);

        var with = EnemyAi.Score(state, Burns, pell, pell.At, brigand);
        var without = EnemyAi.Score(state, Shipped, pell, pell.At, brigand);
        var already = EnemyAi.Score(state.WithUnit(brigand with { Burn = 2, BurnPhases = 2 }), Burns, pell, pell.At, brigand with { Burn = 2, BurnPhases = 2 });

        Assert.True(with > without, $"{with} against {without}");
        Assert.Equal(without, already, 6);
    }

    [Fact]
    public void ABurnRoundTripsThroughTheProtocolState()
    {
        var (_, result) = Struck();
        var again = ProtocolJson.ReadState(ProtocolJson.State(result.Next, Burns), Burns);
        var id = Brigand(result.Next).Id;

        Assert.Equal((2, 2), (again.Find(id)!.Burn, again.Find(id)!.BurnPhases));
    }
}
