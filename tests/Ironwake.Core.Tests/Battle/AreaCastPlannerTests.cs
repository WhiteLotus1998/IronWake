using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The planners cast an area tome (issue 1391): <see cref="Resolver.Legal"/> offers Spark Storm once per set of enemies
/// it strikes, and <see cref="AreaCast.Best"/> prices a cast on the attack score's scale, qualifying only when it strikes
/// two or more or kills one. The Sim's player and the enemy planner take it over an attack it outscores. The board is
/// the shipped content on the sample <c>the_tollgate_frost.map</c>: Pell at 6,7 with Cinder in slot 0 and Spark Storm
/// in slot 1, two tiles below the woods brigand at 6,5 and its archer at 5,5.
/// </summary>
public class AreaCastPlannerTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_tollgate_frost.map");

    private const int StormSlot = 1;

    private static BattleState Facing(int stormUses = 4)
    {
        var state = BattleState.From(MapFiles.Load(SamplePath, Shipped), Shipped, Shipped.Cast, 1391);
        var pell = state.Find("pell")!;
        var inventory = pell.Unit.Inventory.Replace(StormSlot, pell.Unit.Inventory.Items[StormSlot] with { Uses = stormUses });
        return state.WithUnit(pell with { At = new Coord(6, 7), Unit = pell.Unit with { Inventory = inventory } });
    }

    private static BattleUnit Brigand(BattleState state) => state.Units.Single(u => u.Side == Side.Enemy && u.Group == "woods" && u.At == new Coord(6, 5));

    private static BattleUnit Archer(BattleState state) => state.Units.Single(u => u.Side == Side.Enemy && u.Group == "woods" && u.At == new Coord(5, 5));

    private static List<UseItem> Casts(BattleState state) =>
        Resolver.Legal(state, Shipped).OfType<UseItem>().Where(u => u.UnitId == "pell" && u.Slot == StormSlot).ToList();

    private static AreaCast.Choice? PellsBest(BattleState state, Func<Coord, bool>? refused = null)
    {
        var pell = state.Find("pell")!;
        return AreaCast.Best(state, Shipped, pell, new[] { pell.At }, state.UnitsOf(Side.Enemy).ToList(), refused);
    }

    [Fact]
    public void LegalOffersAnAreaCastOncePerSetOfEnemiesStruck()
    {
        var state = Facing();
        var casts = Casts(state);
        var pell = state.Find("pell")!;
        var sets = casts.Select(c => string.Join(" ", AreaCast.Struck(state, pell, Shipped.Weapon("gust"), AreaCast.TileOf(state, c.TargetId!)!.Value).Select(u => u.Id))).ToList();

        Assert.Contains(new UseItem("pell", StormSlot, "6,5"), casts);
        Assert.Equal(sets.Count, sets.Distinct().Count());
        Assert.Contains(sets, set => set.Split(' ').ToHashSet().SetEquals(new[] { Brigand(state).Id, Archer(state).Id }));
    }

    [Fact]
    public void EveryAreaCastLegalOffersIsAccepted()
    {
        var state = Facing();

        Assert.NotEmpty(Casts(state));
        Assert.All(Casts(state), cast => Assert.True(Resolver.Apply(state, Shipped, cast).Accepted));
    }

    [Fact]
    public void LegalOffersNoAreaCastWithNoUseLeft()
    {
        Assert.Empty(Casts(Facing(stormUses: 0)));
    }

    [Fact]
    public void LegalOffersNoAreaCastWithNoEnemyInTheArea()
    {
        var state = Facing();
        state = state.WithUnit(state.Find("pell")! with { At = new Coord(6, 11) });

        Assert.Empty(Casts(state));
    }

    [Fact]
    public void AnAreaCastStrikingTwoQualifiesAndIsPricedAboveEitherSting()
    {
        var state = Facing();
        var best = PellsBest(state);

        Assert.NotNull(best);
        Assert.Equal(new Coord(6, 5), best.At);
        Assert.Equal(2, best.Struck.Count);
        var pell = state.Find("pell")!;
        var alone = AreaCast.Price(state, Shipped, pell, Shipped.Weapon("gust"), best.At, new[] { Brigand(state) }).Score;
        Assert.True(best.Score > alone);
    }

    [Fact]
    public void ASingleStingThatDoesNotKillIsNoCast()
    {
        var state = Facing();
        state = state.WithoutUnit(Archer(state).Id);

        Assert.Null(PellsBest(state));
    }

    [Fact]
    public void ASingleStingThatKillsIsACast()
    {
        var state = Facing();
        state = state.WithoutUnit(Archer(state).Id);
        state = state.WithUnit(Brigand(state) with { Hp = 1 });

        var best = PellsBest(state);
        Assert.NotNull(best);
        Assert.True(AreaCast.Price(state, Shipped, state.Find("pell")!, Shipped.Weapon("gust"), best.At, best.Struck).Kills);
    }

    [Fact]
    public void ARefusedTileIsNoCast()
    {
        Assert.Null(PellsBest(Facing(), refused: _ => true));
    }

    [Fact]
    public void AMarkingTomePricesTheMarkOnASurvivor()
    {
        var state = Facing();
        var pell = state.Find("pell")!;
        var storm = Shipped.Weapon("gust");
        var at = new Coord(6, 5);
        var struck = new[] { Brigand(state), Archer(state) };

        Assert.True(AreaCast.Price(state, Shipped, pell, storm, at, struck).Score > AreaCast.Price(state, Shipped, pell, storm with { Marks = false }, at, struck).Score);
    }

    [Fact]
    public void TheSimsPlayerCastsWhenTheStormOutscoresItsBestAttack()
    {
        var state = Facing();
        state = state.WithUnit(state.Find("pell")! with { Moved = true });
        var best = PellsBest(state)!;

        var plan = HeuristicPlayer.PlanUnit(state, Shipped, state.Find("pell")!);
        var attackScores = new[] { Brigand(state), Archer(state) }
            .Where(t => t.At.DistanceTo(new Coord(6, 7)) <= 2)
            .Select(t => EnemyAi.Score(state, Shipped, state.Find("pell")!, new Coord(6, 7), t));

        Assert.True(best.Score > attackScores.Max());
        Assert.Equal(new UseItem("pell", StormSlot, "6,5"), Assert.Single(plan));
    }

    [Fact]
    public void TheSimsPlayerWithCastsOffAttacksWhereItWouldCast()
    {
        var state = Facing();
        state = state.WithUnit(state.Find("pell")! with { Moved = true });

        var plan = HeuristicPlayer.PlanUnit(state, Shipped, state.Find("pell")!, out _, casts: false);

        Assert.IsType<Attack>(Assert.Single(plan));
    }

    [Fact]
    public void TheCampaignScriptsWriterCastsUnlessToldNotTo()
    {
        Assert.True(CampaignScript.WriterPlayer().Casts);
        Assert.False(CampaignScript.WriterPlayer(casts: false).Casts);
        Assert.True(new HeuristicPlayer().Casts);
    }

    [Fact]
    public void TheSimsPlayerAttacksWhenItsBestAttackOutscoresTheStorm()
    {
        var state = Facing();
        state = state.WithUnit(state.Find("pell")! with { Moved = true });
        var pell = state.Find("pell")!;
        var sting = AreaCast.Forecast(state, Shipped, pell, Shipped.Weapon("gust"), Brigand(state), new Coord(6, 5)).Attacker.Damage;
        state = state.WithUnit(Brigand(state) with { Hp = sting + 1 });

        var plan = HeuristicPlayer.PlanUnit(state, Shipped, pell);

        Assert.True(EnemyAi.Kills(state, Shipped, pell, pell.At, Brigand(state)));
        Assert.NotNull(PellsBest(state));
        Assert.Equal(new Attack("pell", Brigand(state).Id), Assert.Single(plan));
    }

    [Fact]
    public void TheSimsPlayerCastsNoStormFromATileWhoseExposureWouldKillAUnitWhoseDeathLosesTheMap()
    {
        var state = Facing();
        state = state with { Map = state.Map with { ProtectId = "pell" } };
        state = state.WithUnit(state.Find("pell")! with { Moved = true, Hp = 1 });

        var plan = HeuristicPlayer.PlanUnit(state, Shipped, state.Find("pell")!);

        Assert.DoesNotContain(plan, c => c is UseItem { Slot: StormSlot });
    }

    [Fact]
    public void TheSimsPlayerCastsNoStormFromATileWhoseExposureWouldKillTheCaster()
    {
        var state = Facing();
        state = state.WithUnit(state.Find("pell")! with { Moved = true });
        var pell = state.Find("pell")!;
        var exposure = Exposure.Of(state, Shipped, pell, pell.At).NoCrit;
        Assert.InRange(exposure, 1, pell.Hp - 1);
        Assert.Equal(new UseItem("pell", StormSlot, "6,5"), Assert.Single(HeuristicPlayer.PlanUnit(state, Shipped, pell)));

        state = state.WithUnit(pell with { Hp = exposure });
        var plan = HeuristicPlayer.PlanUnit(state, Shipped, state.Find("pell")!);

        Assert.Null(state.Map.ProtectId);
        Assert.DoesNotContain(plan, c => c is UseItem { Slot: StormSlot });
    }

    [Fact]
    public void TheEnemyPlannerCastsAStormOnTwoPlayerUnits()
    {
        var state = Facing();
        var archer = Archer(state);
        var caster = archer with
        {
            Unit = archer.Unit with { ClassId = "storm_caster", Inventory = new Inventory(ValueList<ItemStack>.From([new ItemStack("gust", 4)])) },
        };
        state = state.WithUnit(caster);
        state = state.WithUnit(state.Find("wren")! with { At = new Coord(5, 7) });
        state = state.WithUnit(state.Find("teodor")! with { At = new Coord(4, 7) });
        state = state.WithUnit(state.Find("pell")! with { At = new Coord(6, 11) });
        state = state with { Phase = Side.Enemy };

        var plan = EnemyAi.PlanUnit(state, Shipped, state.Find(caster.Id)!);

        var cast = Assert.IsType<UseItem>(plan[^1]);
        Assert.Equal(0, cast.Slot);
        var struck = AreaCast.Struck(state, state.Find(caster.Id)!, Shipped.Weapon("gust"), AreaCast.TileOf(state, cast.TargetId!)!.Value);
        Assert.Equal(2, struck.Count);
        Assert.True(Resolver.Apply(state, Shipped, cast).Accepted);
    }
}
