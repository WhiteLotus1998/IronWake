using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The Vanguard's Opening (issue 772, round 236): a Vanguard's hit that leaves an enemy standing opens it until
/// the phase ends, and every strike an ally makes on it reads its Def and Res 3 lower, never below 0. The
/// Vanguard's own strikes never read it, a counter never opens, a miss opens nothing, and it never stacks.
/// Played on the Tollgate with the cast captain certified Vanguard, below the woods brigand at 6,5, Teodor beside it at 7,5.
/// </summary>
public class OpeningTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static BattleState Placed(ulong seed = 772)
    {
        var state = BattleState.From(MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), Shipped), Shipped, Shipped.Cast, seed);
        var captain = state.Find("captain")!;
        var teodor = state.Find("teodor")!;
        state = state.WithUnit(captain with { At = new Coord(6, 6), Unit = captain.Unit with { ClassId = "vanguard" } });
        return state.WithUnit(teodor with { At = new Coord(7, 5) });
    }

    private static BattleUnit Brigand(BattleState state) => state.Units.Single(u => u.Side == Side.Enemy && u.Group == "woods" && u.Unit.Name == "Toll Brigand");

    private static BattleState Opened(BattleState state) =>
        state.WithUnit(Brigand(state) with { Open = new OpenMark("captain", 3, 3) });

    [Fact]
    public void TheVanguardHoldsOpeningAndTheChampionDoesNot()
    {
        Assert.Equal(new OpeningEffect(3, 3), AbilityRules.Opening(Shipped.AbilitiesOf(Shipped.Cast[0] with { ClassId = "vanguard" })));
        Assert.Null(AbilityRules.Opening(Shipped.AbilitiesOf(Shipped.Cast[0] with { ClassId = "champion" })));
        Assert.Null(AbilityRules.Opening(Shipped.AbilitiesOf(Shipped.Cast[0])));
    }

    [Fact]
    public void AVanguardsHitThatLeavesTheEnemyStandingOpensItAndAMissDoesNot()
    {
        var (hit, miss) = (false, false);
        for (ulong seed = 1; seed < 400 && !(hit && miss); seed++)
        {
            var state = Placed(seed);
            var brigand = Brigand(state);
            var result = Resolver.Apply(state, Shipped, new Attack("captain", brigand.Id));
            Assert.True(result.Accepted, result.Rejection?.Message);
            if (result.Next.Find(brigand.Id) is not { } after)
            {
                Assert.DoesNotContain(result.Events, e => e is UnitOpened);
                continue;
            }

            var landed = result.Events.OfType<CombatFought>().Single().Strikes.Any(s => s.AttackerId == "captain" && s.Hit);
            Assert.Equal(landed ? new OpenMark("captain", 3, 3) : null, after.Open);
            Assert.Equal(landed, result.Events.Contains(new UnitOpened(brigand.Id, "captain", 3, 3)));
            hit |= landed;
            miss |= !landed;
        }

        Assert.True(hit && miss, "no seed under 400 gave both a surviving hit and a full miss");
    }

    [Fact]
    public void AnAllyWithoutOpeningOpensNothing()
    {
        for (ulong seed = 1; seed < 40; seed++)
        {
            var state = Placed(seed);
            var result = Resolver.Apply(state, Shipped, new Attack("teodor", Brigand(state).Id));

            Assert.DoesNotContain(result.Events, e => e is UnitOpened);
        }
    }

    [Fact]
    public void ACounterNeverOpens()
    {
        for (ulong seed = 1; seed < 60; seed++)
        {
            var state = Placed(seed) with { Phase = Side.Enemy };
            var brigand = Brigand(state);
            var result = Resolver.Apply(state, Shipped, new Attack(brigand.Id, "captain"));
            Assert.True(result.Accepted, result.Rejection?.Message);

            Assert.DoesNotContain(result.Events, e => e is UnitOpened);
            Assert.Null(result.Next.Find(brigand.Id)?.Open);
        }
    }

    [Fact]
    public void AnAllysStrikeOnAnOpenEnemyReadsItsDefAndResThreeLower()
    {
        var state = Placed();
        var open = Opened(state);
        var teodor = state.Find("teodor")!;

        var plain = Brigand(state).Answering(state, Shipped, teodor.At, teodor).Stats;
        var lowered = Brigand(open).Answering(open, Shipped, teodor.At, teodor).Stats;

        Assert.Equal(Math.Max(0, plain.Def - 3), lowered.Def);
        Assert.True(plain.Def > 0, "the brigand has Def to lose");
        Assert.Equal(Math.Max(0, plain.Res - 3), lowered.Res);
        Assert.Equal(
            Queries.Forecast(state, Shipped, teodor, Brigand(state))!.Attacker.Damage + Math.Min(3, plain.Def),
            Queries.Forecast(open, Shipped, teodor, Brigand(open))!.Attacker.Damage);
    }

    [Fact]
    public void TheOpenersOwnStrikesNeverReadIt()
    {
        var state = Placed();
        var open = Opened(state);
        var captain = state.Find("captain")!;

        Assert.False(Opening.Reads(Brigand(open), captain));
        Assert.Equal(Queries.Forecast(state, Shipped, captain, Brigand(state)), Queries.Forecast(open, Shipped, captain, Brigand(open)));
    }

    [Fact]
    public void TheOpenEnemysOwnSideNeverReadsIt()
    {
        var open = Opened(Placed());
        var other = open.Units.First(u => u.Side == Side.Enemy && u.Id != Brigand(open).Id);

        Assert.False(Opening.Reads(Brigand(open), other));
        Assert.False(Opening.Reads(Brigand(open), null));
        Assert.True(Opening.Reads(Brigand(open), open.Find("teodor")));
    }

    [Theory]
    [InlineData(7, 2, -3, -2)]
    [InlineData(1, 0, -1, 0)]
    [InlineData(0, 5, 0, -3)]
    [InlineData(3, 3, -3, -3)]
    public void OpeningLowersDefAndResByThreeNeverBelowZero(int def, int res, int defDelta, int resDelta)
    {
        var delta = Opening.Lowered(new OpenMark("captain", 3, 3), Stats.Zero with { Def = def, Res = res });

        Assert.Equal(Stats.Zero with { Def = defDelta, Res = resDelta }, delta);
    }

    [Fact]
    public void ASecondOpeningRefreshesTheMarkAndDoesNotStack()
    {
        for (ulong seed = 1; seed < 400; seed++)
        {
            var state = Opened(Placed(seed));
            var brigand = Brigand(state);
            var result = Resolver.Apply(state, Shipped, new Attack("captain", brigand.Id));
            if (result.Next.Find(brigand.Id) is not { } after || !result.Events.Any(e => e is UnitOpened))
            {
                continue;
            }

            Assert.Equal(new OpenMark("captain", 3, 3), after.Open);
            var teodor = result.Next.Find("teodor")!;
            Assert.Equal(brigand.Answering(state, Shipped, teodor.At, teodor).Stats.Def, after.Answering(result.Next, Shipped, teodor.At, teodor).Stats.Def);
            return;
        }

        Assert.Fail("no seed under 400 opened the brigand twice");
    }

    [Fact]
    public void TheMarkClearsWhenThePlayerPhaseEnds()
    {
        var open = Opened(Placed());

        var enemyPhase = Resolver.Apply(open, Shipped, new EndPhase()).Next;

        Assert.Equal(Side.Enemy, enemyPhase.Phase);
        Assert.All(enemyPhase.Units, u => Assert.Null(u.Open));
    }

    [Fact]
    public void TheCardTheBoardTheForecastAndTheEventNameTheMark()
    {
        var open = Opened(Placed());
        var brigand = Brigand(open);
        var names = UnitNames.Of(open, Shipped);
        var teodor = open.Find("teodor")!;
        var captain = open.Find("captain")!;

        Assert.Contains(PlaySession.ShowLines(open, Shipped, brigand), l => l == "  open: allies of Alder Fenn strike at Def -3, Res -3 until player phase ends");
        Assert.Contains(PlaySession.ShowLines(open, Shipped, captain), l => l.Contains("Opening (A hit that leaves a foe standing opens it"));
        Assert.DoesNotContain(PlaySession.ShowLines(open, Shipped, teodor), l => l.Contains("Opening"));
        Assert.Contains(", open: Def -3 Res -3 to allies of captain", MapRenderer.Render(open, Shipped));
        Assert.DoesNotContain("open:", MapRenderer.Render(Placed(), Shipped));
        Assert.Contains("  open: Toll Brigand opened by Alder Fenn: Def -3, Res -3 in this forecast", PlaySession.OpenLines(teodor, brigand, names));
        Assert.Contains("  open: Toll Brigand is open to Alder Fenn's allies, not to Alder Fenn", PlaySession.OpenLines(captain, brigand, names));
        Assert.Empty(PlaySession.OpenLines(teodor, Brigand(Placed()), names));
        Assert.Equal("Toll Brigand is open: allies of Alder Fenn strike it at Def -3, Res -3 until the phase ends", PlaySession.Describe(new UnitOpened(brigand.Id, "captain", 3, 3), Shipped, names));
    }

    [Fact]
    public void TheMarkRoundTripsThroughTheProtocol()
    {
        var open = Opened(Placed());

        var json = ProtocolJson.State(open, Shipped);
        var back = ProtocolJson.ReadState(json, Shipped);

        Assert.Contains("\"open\":{\"by\":\"captain\",\"def\":3,\"res\":3}", json);
        Assert.Equal(new OpenMark("captain", 3, 3), Brigand(back).Open);
        Assert.Equal(json, ProtocolJson.State(back, Shipped));
    }

    [Theory]
    [InlineData("\"def\": 0, \"res\": 0", "effect", "an opening must lower def or res")]
    [InlineData("\"def\": -1, \"res\": 3", "effect.def", "must be 0 or more")]
    [InlineData("\"def\": 3, \"res\": -2", "effect.res", "must be 0 or more")]
    [InlineData("\"def\": 3, \"hit\": 5", "effect.hit", "is not read here")]
    public void AnOpeningThatLowersNothingOrRaisesAStatIsRefusedAtLoad(string fields, string field, string why)
    {
        var abilities = Fixture.Abilities.Replace("\n] }", ",\n{ \"id\": \"gap\", \"name\": \"Gap\", \"text\": \"A test line.\", \"effect\": { \"kind\": \"opening\", " + fields + " } }\n] }");
        Assert.Contains("\"gap\"", abilities);

        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(abilities: abilities)));
        Assert.Contains("gap", error.Message);
        Assert.Contains(field, error.Message);
        Assert.Contains(why, error.Message);
    }

    [Fact]
    public void AnOpeningRoundTripsThroughTheSerializer()
    {
        var abilities = Fixture.Abilities.Replace("\n] }", ",\n{ \"id\": \"gap\", \"name\": \"Gap\", \"text\": \"A test line.\", \"effect\": { \"kind\": \"opening\", \"def\": 2 } }\n] }");
        var content = ContentLoader.Parse(Fixture.Files(abilities: abilities));

        Assert.Equal(new OpeningEffect(2, 0), content.Ability("gap").Effect);
        Assert.Contains("\"kind\": \"opening\"", ContentSerializer.Write(content).Abilities.Text);
    }

    [Fact]
    public void TheSimCountsOpenedStrikesAndTheKillsAnAllyConvertedFromThem()
    {
        var mix = new ActionMix(1, 5, 0, 0, Opened: 2, Converted: 1, Decided: 1);

        Assert.Equal("atk 1 dmg 5 heal 0 abs 0 opened 2 converted 1 decided 1", mix.ToString());
        Assert.Equal("atk 1 dmg 5 heal 0 abs 0", (mix with { Opened = 0, Converted = 0, Decided = 0 }).ToString());
        Assert.Equal(new ActionMix(2, 10, 0, 0, 4, 2, 2), mix.Plus(mix));

        var vanguard = LadderRun.WithCaptain(Shipped, LadderRun.Promote(Shipped, Shipped.Cast[0], Shipped.Class("vanguard")));
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), Shipped);
        var games = Enumerable.Range(1, 12).Select(s => Runner.Play(vanguard, map, (ulong)s, new HeuristicPlayer())).ToList();
        var captain = games.Aggregate(ActionMix.Zero, (sum, g) => sum.Plus(g.Mix["captain"]));

        Assert.True(captain.Opened > 0, "the Vanguard captain opened nothing in 12 games");
        Assert.InRange(captain.Converted, 1, captain.Opened);
        Assert.InRange(captain.Decided, 0, captain.Converted);
    }

    [Theory]
    [InlineData(9, false, 7, false)]
    [InlineData(9, false, 8, true)]
    [InlineData(9, true, 3, false)]
    [InlineData(9, true, 4, true)]
    public void TheMarkDecidesAKillOnlyWhenTheHitsWithoutItFallShort(int damage, bool crit, int hpBefore, bool decided)
    {
        var state = Placed();
        var brigand = Brigand(state) with { Hp = hpBefore };
        var open = new OpenMark("captain", 3, 3);
        state = state.WithUnit(brigand with { Open = open });
        var cut = -Opening.Lowered(open, brigand.ToCombatant(state, Shipped, countering: true).Stats).Def;
        Assert.Equal(2, cut);
        var strikes = ValueList<StrikeEvent>.Empty.Add(new StrikeEvent(0, "teodor", brigand.Id, true, crit, damage, 0));
        var fought = new CombatFought("teodor", brigand.Id, 1, Side.Player, strikes, 21, 0);

        Assert.Equal(decided, Runner.MarkDecided(state, state.WithoutUnit(brigand.Id), Shipped, fought, open));
    }
}
