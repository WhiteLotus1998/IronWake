using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Ice's chill and lightning's stun as school riders (issue 1244, DECISIONS/0299). Ice's rider is
/// frozen iron's chill (0155, 0264), shipped as data with no tome naming it. The stun is a rider kind
/// gated on the caster's class, once a map per caster, bosses spared, counters kept; it ships in no
/// content until the storm-warden's class exists, so these tests use fixture tomes, <c>test_frost</c>
/// and <c>test_knell</c>, in Pell's hands on the sample <c>the_tollgate_frost.map</c>.
/// </summary>
public class SchoolRiderTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly Weapon Cinder = Shipped.Weapon("cinder");

    private static readonly GameContent Riders = Shipped with
    {
        Weapons = Shipped.Weapons
            .SetItem("test_frost", Cinder with { Id = "test_frost", Name = "Test Frost", School = MagicSchool.Ice, Rider = RiderKind.Chill, Ignites = false })
            .SetItem("test_knell", Cinder with { Id = "test_knell", Name = "Test Knell", School = MagicSchool.Lightning, Rider = RiderKind.Stun, Ignites = false }),
        Riders = Shipped.Riders.SetItem(MagicSchool.Lightning, new SchoolRider(RiderKind.Stun, 0, 0) { Classes = ValueList<string>.Of("adept") }),
    };

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_tollgate_frost.map");

    private const string RiderRules = """
        { "wakeRadius": 4, "schools": { "ice": { "rider": { "kind": "chill" } } } }
        """;

    /// <summary>The fixture content files with <see cref="RiderRules"/>, <paramref name="edit"/> applied, as rules.json.</summary>
    private static ContentFiles RulesFiles(Func<string, string> edit) => Fixture.Files(rules: edit(RiderRules));

    /// <summary>Pell holding <paramref name="tome"/> two tiles below the woods brigand at 6,5, out of its axe's reach.</summary>
    private static BattleState Facing(string tome, ulong seed = 1244)
    {
        var state = BattleState.From(MapFiles.Load(SamplePath, Riders), Riders, Riders.Cast, seed);
        var pell = state.Find("pell")!;
        var unit = pell.Unit with { Inventory = pell.Unit.Inventory.Replace(0, new ItemStack(tome, Cinder.Durability)) };
        return state.WithUnit(pell with { Unit = unit, At = new Coord(6, 7) });
    }

    private static BattleUnit Brigand(BattleState state) => state.Units.Single(u => u.Side == Side.Enemy && u.Group == "woods" && u.Unit.ClassId == Shipped.Unit("toll_brigand").ClassId);

    /// <summary>The first seed under 400 whose attack by Pell with <paramref name="tome"/> hits the brigand and leaves it standing, and its result.</summary>
    private static (BattleState Before, ApplyResult Result) Struck(string tome, Func<BattleState, BattleState>? edit = null)
    {
        for (ulong seed = 1; seed < 400; seed++)
        {
            var state = Facing(tome, seed);
            state = edit?.Invoke(state) ?? state;
            var result = Resolver.Apply(state, Riders, new Attack("pell", Brigand(state).Id));
            Assert.True(result.Accepted, result.Rejection?.Message);
            if (result.Next.Find(Brigand(state).Id) is not null && result.Events.OfType<CombatFought>().Single().Strikes.Any(s => s.AttackerId == "pell" && s.Hit))
            {
                return (state, result);
            }
        }

        throw new InvalidOperationException("no seed under 400 gave a surviving hit");
    }

    [Fact]
    public void IcesRiderShipsAsFrozenIronsChillAndNoShippedTomeNamesIt()
    {
        Assert.Equal(new SchoolRider(RiderKind.Chill, 0, 0), Shipped.Riders[MagicSchool.Ice]);
        Assert.False(Shipped.Riders.ContainsKey(MagicSchool.Lightning));
        Assert.All(Shipped.Weapons.Values, w => Assert.Null(w.Rider));
    }

    [Fact]
    public void ChillAndStunRidersRoundTripThroughTheSerializer()
    {
        var content = ContentLoader.Parse(RulesFiles(r => r.Replace("\"ice\": {", "\"lightning\": { \"rider\": { \"kind\": \"stun\", \"classes\": [ \"cadet\" ] } }, \"ice\": {")));
        var again = ContentLoader.Parse(ContentSerializer.Write(content));

        Assert.Equal(ValueList<string>.Of("cadet"), content.Riders[MagicSchool.Lightning].Classes);
        Assert.Equal(content.Riders, again.Riders);
    }

    [Theory]
    [InlineData("\"ice\": { \"rider\": { \"kind\": \"chill\" } }", "\"ice\": { \"rider\": { \"kind\": \"chill\", \"amount\": 2 } }", "schools.ice", "rider.amount", "is not a field of a chill rider")]
    [InlineData("\"ice\": { \"rider\": { \"kind\": \"chill\" } }", "\"ice\": { \"rider\": { \"kind\": \"stun\" } }", "schools.ice", "classes", "is required")]
    [InlineData("\"ice\": { \"rider\": { \"kind\": \"chill\" } }", "\"ice\": { \"rider\": { \"kind\": \"stun\", \"classes\": [] } }", "schools.ice", "rider.classes", "at least one class")]
    [InlineData("\"ice\": { \"rider\": { \"kind\": \"chill\" } }", "\"ice\": { \"rider\": { \"kind\": \"stun\", \"classes\": [ \"stormwarden\" ] } }", "schools.ice", "rider.classes", "unknown class 'stormwarden'")]
    [InlineData("\"ice\": { \"rider\": { \"kind\": \"chill\" } }", "\"ice\": { \"rider\": { \"kind\": \"stun\", \"classes\": [ \"cadet\" ], \"amount\": 1 } }", "schools.ice", "rider.amount", "is not a field of a stun rider")]
    public void ABadChillOrStunRiderIsRefusedAtLoadNamingFileEntryAndField(string from, string to, string entry, string field, string why)
    {
        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(RulesFiles(r => r.Replace(from, to))));

        Assert.Contains(ContentFiles.RulesName, error.Message);
        Assert.Contains(entry, error.Message);
        Assert.Contains(field, error.Message);
        Assert.Contains(why, error.Message);
    }

    [Fact]
    public void AnIceTomeThatNamesItsRiderChillsAsFrozenIronDoes()
    {
        var (before, result) = Struck("test_frost");
        var brigand = Brigand(before);
        var after = result.Next.Find(brigand.Id)!;

        Assert.Equal(1, after.Chill);
        Assert.Contains(new UnitChilled(brigand.Id, "pell", Side.Enemy), result.Events);
        var enemy = Resolver.Apply(result.Next, Riders, new EndPhase()).Next;
        Assert.Equal(Shipped.Class(brigand.Unit.ClassId).Mov - Frost.MovLost, enemy.ReachOf(enemy.Find(brigand.Id)!, Riders).Mov);
        Assert.True(Frost.Chills(Riders, Riders.Weapon("test_frost")));
        Assert.False(Frost.Chills(Shipped, Shipped.Weapon("cinder")));
    }

    [Fact]
    public void AnIceTomeWithoutItsRiderNeverChills()
    {
        var plain = Riders with { Weapons = Riders.Weapons.SetItem("test_frost", Riders.Weapon("test_frost") with { Rider = null }) };
        for (ulong seed = 1; seed < 40; seed++)
        {
            var state = Facing("test_frost", seed);
            var result = Resolver.Apply(state, plain, new Attack("pell", Brigand(state).Id));

            Assert.DoesNotContain(result.Events, e => e is UnitChilled);
        }
    }

    [Fact]
    public void RooksHoldWinsOverAnIceChillAndALockedUnitStaysLocked()
    {
        var (_, result) = Struck("test_frost", s => s.WithUnit(s.Find("teodor")! with { At = new Coord(7, 5) }).WithUnit(Brigand(s) with { Frosted = 1, LockedBy = "teodor", Chill = 1 }));
        var after = Brigand(result.Next);

        Assert.Equal("teodor", after.LockedBy);
        Assert.Equal((1, 1), (after.Chill, after.Frosted));
        Assert.Equal(DrakeFrost.HoldMov, DrakeFrost.Mov(Frost.Mov(Shipped.Class(after.Unit.ClassId).Mov, after), after));
    }

    [Fact]
    public void AStunHitMakesTheTargetSkipItsSidesNextPhaseThenClears()
    {
        var (before, result) = Struck("test_knell");
        var id = Brigand(before).Id;

        Assert.Contains(new UnitStunned(id, "pell", Side.Enemy), result.Events);
        Assert.Equal(1, result.Next.Find(id)!.Stun);
        Assert.True(result.Next.Find("pell")!.StunSpent);

        var enemy = Resolver.Apply(result.Next, Riders, new EndPhase());
        Assert.Contains(new StunSkipped(id), enemy.Events);
        var skipping = enemy.Next.Find(id)!;
        Assert.True(Stun.Skipping(skipping));
        Assert.True(skipping.Moved && skipping.Acted);
        var refused = Resolver.Apply(enemy.Next, Riders, new Wait(id));
        Assert.False(refused.Accepted);
        Assert.Contains("stunned and skips this phase", refused.Rejection!.Message);
        Assert.Null(EnemyAi.StrikeOn(enemy.Next, Riders, skipping, enemy.Next.Find("pell")!));

        var player = Resolver.Apply(enemy.Next, Riders, new EndPhase());
        Assert.Equal(0, player.Next.Find(id)!.Stun);
        var next = Resolver.Apply(player.Next, Riders, new EndPhase());
        Assert.DoesNotContain(next.Events, e => e is StunSkipped);
        Assert.False(next.Next.Find(id)!.Acted);
    }

    [Fact]
    public void AStunnedUnitStillCounters()
    {
        var (_, result) = Struck("test_knell");
        var stunned = Brigand(result.Next);
        var board = result.Next.WithUnit(result.Next.Find("teodor")! with { At = new Coord(6, 6) });
        var forecast = Queries.Forecast(board, Riders, board.Find("teodor")!, stunned)!;

        Assert.Equal(1, stunned.Stun);
        Assert.True(forecast.Defender.Strikes);
    }

    [Fact]
    public void AStunFiresOnlyForACasterOfAClassTheRiderNames()
    {
        var ungated = Riders with { Riders = Riders.Riders.SetItem(MagicSchool.Lightning, new SchoolRider(RiderKind.Stun, 0, 0) { Classes = ValueList<string>.Of("scholar") }) };
        for (ulong seed = 1; seed < 40; seed++)
        {
            var state = Facing("test_knell", seed);
            var result = Resolver.Apply(state, ungated, new Attack("pell", Brigand(state).Id));

            Assert.DoesNotContain(result.Events, e => e is UnitStunned);
            Assert.False(result.Next.Find("pell")!.StunSpent);
        }

        var board = Facing("test_knell");
        Assert.Equal(("", ""), PlaySession.Riders(ungated, board.Find("pell")!, Brigand(board), null));
    }

    [Fact]
    public void BossesAreSparedAndAMissOrASparedBossSpendsNothing()
    {
        var (spared, missed) = (false, false);
        for (ulong seed = 1; seed < 400 && !(spared && missed); seed++)
        {
            var state = Facing("test_knell", seed);
            state = state.WithUnit(Brigand(state) with { IsBoss = true });
            var result = Resolver.Apply(state, Riders, new Attack("pell", Brigand(state).Id));
            var landed = result.Events.OfType<CombatFought>().Single().Strikes.Any(s => s.AttackerId == "pell" && s.Hit);

            Assert.DoesNotContain(result.Events, e => e is UnitStunned);
            Assert.False(result.Next.Find("pell")!.StunSpent);
            spared |= landed && result.Next.Find(Brigand(state).Id) is not null;
            missed |= !landed;
        }

        Assert.True(spared && missed, "no seed under 400 gave both a surviving hit on the boss and a full miss");
        var board = Facing("test_knell");
        Assert.Equal(" stun: bosses spared", PlaySession.Riders(Riders, board.Find("pell")!, Brigand(board) with { IsBoss = true }, null).Riders);
    }

    [Fact]
    public void TheStunIsOnceAMapPerCaster()
    {
        var (_, result) = Struck("test_knell", s => s.WithUnit(s.Find("pell")! with { StunSpent = true }));

        Assert.DoesNotContain(result.Events, e => e is UnitStunned);
        Assert.Equal(0, Brigand(result.Next).Stun);
        var board = Facing("test_knell");
        Assert.Equal(" stun spent", PlaySession.Riders(Riders, board.Find("pell")! with { StunSpent = true }, Brigand(board), null).Riders);
    }

    [Fact]
    public void TheForecastNamesEachRiderAndTheCardTheStun()
    {
        var frost = Facing("test_frost");
        Assert.Equal(" chills", PlaySession.Riders(Riders, frost.Find("pell")!, Brigand(frost), null).Riders);
        var knell = Facing("test_knell");
        Assert.Equal(" stuns", PlaySession.Riders(Riders, knell.Find("pell")!, Brigand(knell), null).Riders);

        var brigand = Brigand(knell);
        Assert.Equal("stunned: skips its next phase", Stun.CardLine(brigand with { Stun = 1 }));
        Assert.Equal("stunned: skips this phase", Stun.CardLine(brigand with { Stun = 2 }));
        Assert.Null(Stun.CardLine(brigand));
    }

    [Fact]
    public void ThreatDropsAStunnedEnemysLineAndSaysWhy()
    {
        var state = Facing("test_knell");
        var pell = state.Find("pell")!;
        var tile = new Coord(6, 6);
        var brigand = Brigand(state);

        Assert.Contains(Queries.Threats(state, Riders, pell, tile)!, l => l.Enemy.Id == brigand.Id);
        Assert.Empty(Queries.Stunned(state, Riders, pell, tile)!);

        var stunned = state.WithUnit(brigand with { Stun = 1 });
        Assert.DoesNotContain(Queries.Threats(stunned, Riders, pell, tile)!, l => l.Enemy.Id == brigand.Id);
        Assert.Equal(brigand.Id, Assert.Single(Queries.Stunned(stunned, Riders, pell, tile)!).Id);
    }

    [Fact]
    public void TheStunIsBoardStateTheProtocolCarries()
    {
        var (_, result) = Struck("test_knell");
        var again = ProtocolJson.ReadState(ProtocolJson.State(result.Next, Riders), Riders);

        Assert.Equal(1, Brigand(again).Stun);
        Assert.True(again.Find("pell")!.StunSpent);
    }
}
