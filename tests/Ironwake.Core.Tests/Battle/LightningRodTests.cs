using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Lightning Rod (issue 1280, Lotus's #1247 rulings; <see cref="LightningRod"/>): a lightning spell aimed at an
/// ally within 2 of the holder strikes the holder instead, when the spell reaches it. No class carries the rod
/// until the storm-warden's kit is signed, so these tests hand it to the woods archer on the sample
/// <c>the_tollgate_frost.map</c> and put Pell, holding a fixture lightning tome (<c>test_bolt</c>, Cinder's numbers) or Cinder, in front of the woods brigand at 6,5.
/// </summary>
public class LightningRodTests
{
    private static readonly GameContent Real = ContentLoader.Load(Fixture.RealContentDirectory());

    /// <summary>The shipped content with <c>test_bolt</c>, Cinder's numbers as a lightning tome Pell can wield.</summary>
    private static readonly GameContent Shipped = Real with
    {
        Weapons = Real.Weapons.SetItem("test_bolt", Real.Weapon("cinder") with { Id = "test_bolt", Name = "Test Bolt", School = MagicSchool.Lightning, Ignites = false }),
    };

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_tollgate_frost.map");

    private static readonly Coord Brigand = new(6, 5);

    /// <summary>Pell on 6,6 holding <paramref name="tome"/>, beside the brigand; the woods archer holding the rod on <paramref name="holderAt"/> (5,5, its own tile, by default).</summary>
    private static BattleState Board(string tome = "test_bolt", Coord? holderAt = null, ulong seed = 1280, bool rod = true)
    {
        var state = BattleState.From(MapFiles.Load(SamplePath, Shipped), Shipped, Shipped.Cast, seed);
        var pell = state.Find("pell")!;
        var tomeStack = new ItemStack(tome, Shipped.Weapon(tome).Durability);
        state = state.WithUnit(pell with { Unit = pell.Unit with { Inventory = pell.Unit.Inventory.Replace(0, tomeStack) }, At = new Coord(6, 6) });
        var archer = Archer(state);
        var abilities = rod ? archer.Unit.Abilities.Add("lightning_rod") : archer.Unit.Abilities;
        return state.WithUnit(archer with { Unit = archer.Unit with { Abilities = abilities }, At = holderAt ?? archer.At });
    }

    private static BattleUnit Archer(BattleState state) => state.Units.Single(u => u.Side == Side.Enemy && u.Group == "woods" && u.Unit.ClassId == Shipped.Unit("archer").ClassId);

    private static BattleUnit Struck(BattleState state) => state.Units.Single(u => u.At == Brigand);

    [Fact]
    public void ALightningSpellAimedAtAnAllyWithinTwoStrikesTheHolder()
    {
        var state = Board();
        var result = Resolver.Apply(state, Shipped, new Attack("pell", Struck(state).Id));

        Assert.True(result.Accepted, result.Rejection?.Message);
        var archer = Archer(state);
        Assert.Contains(new RodCaught(archer.Id, Struck(state).Id, "pell"), result.Events);
        var fought = result.Events.OfType<CombatFought>().Single();
        Assert.Equal(archer.Id, fought.TargetId);
        Assert.Equal(Struck(state).Hp, result.Next.Find(Struck(state).Id)!.Hp);
    }

    [Fact]
    public void TheRodCatchesNothingAtThreeTiles()
    {
        var state = Board(holderAt: new Coord(5, 7));
        Assert.Equal(3, new Coord(5, 7).DistanceTo(Brigand));

        var result = Resolver.Apply(state, Shipped, new Attack("pell", Struck(state).Id));

        Assert.DoesNotContain(result.Events, e => e is RodCaught);
        Assert.Equal(Struck(state).Id, result.Events.OfType<CombatFought>().Single().TargetId);
    }

    [Fact]
    public void TheRodCatchesNothingTheSpellCannotReach()
    {
        var state = Board(holderAt: new Coord(4, 5));
        Assert.Equal(2, new Coord(4, 5).DistanceTo(Brigand));
        Assert.Equal(3, new Coord(4, 5).DistanceTo(state.Find("pell")!.At));

        var result = Resolver.Apply(state, Shipped, new Attack("pell", Struck(state).Id));

        Assert.DoesNotContain(result.Events, e => e is RodCaught);
        Assert.Null(LightningRod.Catcher(state, Shipped, state.Find("pell")!.At, Shipped.Weapon("test_bolt"), Struck(state)));
    }

    [Fact]
    public void AFireSpellPassesTheRod()
    {
        var state = Board(tome: "cinder");

        var result = Resolver.Apply(state, Shipped, new Attack("pell", Struck(state).Id));

        Assert.DoesNotContain(result.Events, e => e is RodCaught);
        Assert.Equal(Struck(state).Id, result.Events.OfType<CombatFought>().Single().TargetId);
    }

    [Fact]
    public void AStunnedHolderCatchesNothingAndAFallenOneIsGone()
    {
        var stunned = Board();
        stunned = stunned.WithUnit(Archer(stunned) with { Stun = 1 });
        var fallen = Board();
        fallen = fallen.WithoutUnit(Archer(fallen).Id);

        Assert.Null(LightningRod.Catcher(stunned, Shipped, new Coord(6, 6), Shipped.Weapon("test_bolt"), Struck(stunned)));
        Assert.Null(LightningRod.Catcher(fallen, Shipped, new Coord(6, 6), Shipped.Weapon("test_bolt"), Struck(fallen)));
        Assert.DoesNotContain(Resolver.Apply(stunned, Shipped, new Attack("pell", Struck(stunned).Id)).Events, e => e is RodCaught);
    }

    [Fact]
    public void ASpellAimedAtTheHolderItselfIsAPlainAttack()
    {
        var state = Board();

        Assert.Null(LightningRod.Catcher(state, Shipped, new Coord(6, 6), Shipped.Weapon("test_bolt"), Archer(state)));
    }

    [Fact]
    public void WithoutTheRodTheSpellStrikesWhereItIsAimed()
    {
        var state = Board(rod: false);

        Assert.Null(LightningRod.Catcher(state, Shipped, new Coord(6, 6), Shipped.Weapon("test_bolt"), Struck(state)));
    }

    [Fact]
    public void TheForecastIsAgainstTheHolderAndTheHolderCounters()
    {
        var state = Board();
        var pell = state.Find("pell")!;
        var archer = Archer(state);

        var caught = Queries.Forecast(state, Shipped, pell, Struck(state))!;
        var direct = Queries.Forecast(state, Shipped, pell, archer)!;

        Assert.Equal(archer.Id, caught.CaughtBy);
        Assert.Null(direct.CaughtBy);
        Assert.Equal(direct with { CaughtBy = archer.Id }, caught);
        Assert.True(caught.Defender.Strikes);
        var text = PlaySession.ForecastText(state, Shipped, pell, Struck(state), caught, pell.At, false);
        Assert.Contains("(Lightning Rod: strikes ", text);
    }

    [Fact]
    public void ThePlannerScoresACaughtSpellAgainstTheHolder()
    {
        var state = Board();
        var pell = state.Find("pell")!;

        Assert.Equal(EnemyAi.Score(state, Shipped, pell, pell.At, Archer(state)), EnemyAi.Score(state, Shipped, pell, pell.At, Struck(state)));
        Assert.NotEqual(EnemyAi.Score(Board(rod: false), Shipped, pell, pell.At, Struck(state)), EnemyAi.Score(state, Shipped, pell, pell.At, Struck(state)));
    }

    [Fact]
    public void ACaughtThreatLineStaysOutOfTheTotal()
    {
        var state = Board();
        var pell = state.Find("pell")!;
        var forecast = Queries.Forecast(state, Shipped, pell, Struck(state))!;
        var line = new ThreatLine(pell, pell.At, 0, Shipped.Weapon("test_bolt"), forecast);

        Assert.Equal(0, line.IfAllLand);
        Assert.True((line with { Forecast = forecast with { CaughtBy = null } }).IfAllLand > 0);
    }

    [Fact]
    public void TheRodShipsAsDataAndNoClassCarriesIt()
    {
        Assert.Equal(new RodEffect(MagicSchool.Lightning, 2), Real.Ability("lightning_rod").Effect);
        Assert.All(Real.Classes.Values, c => Assert.DoesNotContain("lightning_rod", c.Abilities));
        Assert.Equal(Real.Ability("lightning_rod"), ContentLoader.Parse(ContentSerializer.Write(Real)).Ability("lightning_rod"));
    }

    [Theory]
    [InlineData("\"kind\": \"rod\", \"school\": \"lightning\", \"radius\": 0", "effect.radius", "must be at least 1")]
    [InlineData("\"kind\": \"rod\", \"school\": \"thunder\", \"radius\": 2", "effect.school", "thunder")]
    [InlineData("\"kind\": \"rod\", \"school\": \"lightning\", \"radius\": 2, \"amount\": 1", "amount", "")]
    public void AMalformedRodIsRefusedAtLoadNamingFileEntryAndField(string fields, string field, string why)
    {
        var abilities = Fixture.Abilities.Replace("\n] }", ",\n{ \"id\": \"gap\", \"name\": \"Gap\", \"text\": \"A test line.\", \"effect\": { " + fields + " } }\n] }");

        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(abilities: abilities)));
        Assert.Contains("gap", error.Message);
        Assert.Contains(field, error.Message);
        Assert.Contains(why, error.Message);
    }
}
