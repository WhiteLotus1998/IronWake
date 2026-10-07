using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Frozen iron and chill (issue 702 slice 2, rounds 216 and 217): a weapon is frozen iron when its
/// content marks it, when it is an heirloom at its last stage, or when it is a signature that has
/// taken its last frozen-iron Refine step; Kinsbane never is. A hit from it takes 1 Mov from a
/// surviving target until its side's next phase ends, no stack, never below 1; a miss does nothing.
/// The forecast prints <c>chills</c>, the card and the event name the clock, and every reach reads it.
/// Played on the sample <c>the_tollgate_frost.map</c>, Teodor carrying the woken Family Lance.
/// </summary>
public class FrostTests
{
    private const string LanceId = "family_lance";

    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_tollgate_frost.map");

    private static BattleState Placed(ulong seed = 702) =>
        BattleState.From(MapFiles.Load(SamplePath, Shipped), Shipped, Shipped.Cast, seed);

    private static BattleUnit Teodor(BattleState state) => state.Find("teodor")!;

    private static BattleUnit Brigand(BattleState state) => state.Units.Single(u => u.Unit.ClassId == Shipped.Unit("toll_brigand").ClassId && u.Side == Side.Enemy && u.Group == "woods");

    /// <summary>Teodor below the woods brigand at 6,5, the lance at <paramref name="stage"/>.</summary>
    private static BattleState Facing(BattleState state, int stage = 3)
    {
        var teodor = Teodor(state);
        var items = teodor.Unit.Inventory.Replace(0, new ItemStack(LanceId, 40) { Stage = stage });
        return state.WithUnit(teodor with { At = new Coord(6, 6), Unit = teodor.Unit with { Inventory = items } });
    }

    private static Weapon Signature(string boundTo) =>
        Shipped.Weapon("iron_lance") with { Id = "test_signature", BoundTo = boundTo, Price = null };

    [Fact]
    public void TheWokenHeaderRoundTripsAndTheSampleIsCanonical()
    {
        var sample = MapFiles.Load(SamplePath, Shipped);

        Assert.Equal("teodor", sample.WokenBearer);
        Assert.Equal(File.ReadAllText(SamplePath).Replace("\r\n", "\n"), MapFormat.Write(sample, Shipped));
    }

    [Fact]
    public void AWokenHeaderNamingNoPlacedRecruitIsRefused()
    {
        var text = File.ReadAllText(SamplePath).Replace("woken: teodor", "woken: rook");

        var error = Assert.Throws<MapException>(() => MapFormat.Parse("frost.map", text, Shipped));
        Assert.Contains("woken names 'rook'", error.Message);
    }

    [Fact]
    public void TheWokenHeaderIssuesTheBoundHeirloomAtItsLastStageInFront()
    {
        var stack = Teodor(Placed()).Unit.Inventory.Items[0];

        Assert.Equal(LanceId, stack.ItemId);
        Assert.Equal(Shipped.Weapon(LanceId).Heirloom!.Turns.Count, stack.Stage);
        Assert.True(Teodor(Placed()).EquippedWeapon(Shipped)!.FrozenIron);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void AnHeirloomIsFrozenIronOnlyAtItsLastStage(int stage, bool frozen)
    {
        Assert.Equal(frozen, Teodor(Facing(Placed(), stage)).EquippedWeapon(Shipped)!.FrozenIron);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void AMainLineSignatureIsFrozenIronOnlyAtItsLastRareStep(int refines, bool frozen)
    {
        Assert.Equal(3, Shipped.Campaign.Forge.RareSteps);
        var weapon = Signature("maud");

        Assert.Equal(Material.Rare, Forge.MaterialFor(weapon, Shipped).Material);
        Assert.Equal(frozen, Frost.Shape(weapon, new ItemStack(weapon.Id, 40) { Refines = refines }, Shipped).FrozenIron);
    }

    [Fact]
    public void AWeaponThatRefinesOnCommonMaterialIsNeverFrozenIron()
    {
        var shop = Shipped.Weapon("iron_lance");

        Assert.Equal(Material.Common, Forge.MaterialFor(shop, Shipped).Material);
        Assert.False(Frost.Shape(shop, new ItemStack(shop.Id, 40) { Refines = 5 }, Shipped).FrozenIron);
    }

    [Fact]
    public void KinsbaneIsNeverFrozenIronWhateverItsStackOrMarkSays()
    {
        var kinsbane = Shipped.Weapon(Kinsbane.ItemId) with { FrozenIron = true };

        Assert.False(Frost.Shape(kinsbane, new ItemStack(kinsbane.Id, 20) { Refines = 9, Stage = 9 }, Shipped).FrozenIron);
    }

    [Theory]
    [InlineData("\"hungers\": true, ", "a hungering weapon is never frozen iron")]
    [InlineData("\"type\": \"reason\", ", "frozen iron is a physical weapon")]
    public void AFrozenIronMarkOnAHungeringWeaponOrASpellIsRefusedAtLoad(string extra, string why)
    {
        var line = """{ "id": "rimefang", "name": "Rimefang", "type": "axe", "mt": 9, "hit": 70, "crit": 0, "wt": 10, "minRange": 1, "maxRange": 1, "durability": 20, "rank": "E", "frozenIron": true, "description": "A test line." }""";
        line = extra.StartsWith("\"type\"") ? line.Replace("\"type\": \"axe\", ", extra) : line.Replace("\"rank\"", extra + "\"rank\"");
        var weapons = Fixture.Weapons.Replace("\n] }", ",\n" + line + "\n] }");

        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(weapons: weapons)));
        Assert.Contains("rimefang", error.Message);
        Assert.Contains("frozenIron", error.Message);
        Assert.Contains(why, error.Message);
    }

    [Fact]
    public void AFrozenIronMarkRoundTripsThroughTheSerializer()
    {
        var line = """{ "id": "rimefang", "name": "Rimefang", "type": "axe", "mt": 9, "hit": 70, "crit": 0, "wt": 10, "minRange": 1, "maxRange": 1, "durability": 20, "rank": "E", "frozenIron": true, "description": "A test line." }""";
        var content = ContentLoader.Parse(Fixture.Files(weapons: Fixture.Weapons.Replace("\n] }", ",\n" + line + "\n] }")));

        Assert.True(content.Weapon("rimefang").FrozenIron);
        Assert.Contains("\"frozenIron\": true", ContentSerializer.Write(content).Weapons.Text);
    }

    [Fact]
    public void AHitFromFrozenIronChillsAndAMissDoesNot()
    {
        var (hit, miss) = (false, false);
        for (ulong seed = 1; seed < 400 && !(hit && miss); seed++)
        {
            var state = Facing(Placed(seed));
            var brigand = Brigand(state);
            var result = Resolver.Apply(state, Shipped, new Attack("teodor", brigand.Id));
            Assert.True(result.Accepted);
            var fought = result.Events.OfType<CombatFought>().Single();
            if (result.Next.Find(brigand.Id) is not { } after || result.Next.Find("teodor") is null)
            {
                continue;
            }

            var landed = fought.Strikes.Any(s => s.AttackerId == "teodor" && s.Hit);
            Assert.Equal(landed ? 1 : 0, after.Chill);
            Assert.Equal(landed, result.Events.Contains(new UnitChilled(brigand.Id, "teodor", Side.Enemy)));
            hit |= landed;
            miss |= !landed;
        }

        Assert.True(hit && miss, "no seed under 400 gave both a surviving hit and a full miss");
    }

    [Fact]
    public void ACounterOnTheStruckSidesOwnPhaseChillsThroughItsNextPhase()
    {
        for (ulong seed = 1; seed < 400; seed++)
        {
            var state = Facing(Placed(seed)) with { Phase = Side.Enemy };
            var brigand = Brigand(state);
            var result = Resolver.Apply(state, Shipped, new Attack(brigand.Id, "teodor"));
            Assert.True(result.Accepted, result.Rejection?.Message);
            if (result.Next.Find(brigand.Id) is not { Chill: 1 } chilled)
            {
                continue;
            }

            Assert.Contains(new UnitChilled(brigand.Id, "teodor", Side.Enemy, Next: true), result.Events);
            var player = Resolver.Apply(result.Next, Shipped, new EndPhase()).Next;
            Assert.Equal(1, player.Find(brigand.Id)!.Chill);
            Assert.Equal(Shipped.Class(chilled.Unit.ClassId).Mov - 1, player.ReachOf(player.Find(brigand.Id)!, Shipped).Mov);
            return;
        }

        Assert.Fail("no seed under 400 gave a surviving counter hit");
    }

    [Fact]
    public void ARustedLanceNeverChills()
    {
        for (ulong seed = 1; seed < 40; seed++)
        {
            var state = Facing(Placed(seed), stage: 0);
            var result = Resolver.Apply(state, Shipped, new Attack("teodor", Brigand(state).Id));

            Assert.DoesNotContain(result.Events, e => e is UnitChilled);
        }
    }

    [Fact]
    public void ChillDoesNotStackAndASecondHitRefreshesTheClock()
    {
        var state = Facing(Placed());
        var brigand = Brigand(state) with { Chill = 2 };
        state = state.WithUnit(brigand);
        var strikes = ValueList<StrikeEvent>.Empty.Add(new StrikeEvent(0, "teodor", brigand.Id, true, false, 3, brigand.Hp - 3));
        var events = new List<GameEvent>();

        var next = Frost.AfterCombat(state, Shipped, "teodor", Teodor(state).EquippedWeapon(Shipped), brigand.Id, null, strikes, events);

        Assert.Equal(1, next.Find(brigand.Id)!.Chill);
        Assert.Equal(Shipped.Class(brigand.Unit.ClassId).Mov - 1, next.ReachOf(next.Find(brigand.Id)!, Shipped).Mov);
    }

    [Theory]
    [InlineData(5, 4)]
    [InlineData(2, 1)]
    [InlineData(1, 1)]
    public void ChillTakesOneMovAndNeverGoesBelowOne(int mov, int chilled)
    {
        var unit = Brigand(Placed());

        Assert.Equal(mov, Frost.Mov(mov, unit));
        Assert.Equal(chilled, Frost.Mov(mov, unit with { Chill = 1 }));
        Assert.Equal(chilled, Frost.Mov(mov, unit with { Chill = 2 }));
    }

    [Theory]
    [InlineData(1, Side.Enemy, Side.Player, Side.Enemy, 2)]
    [InlineData(2, Side.Enemy, Side.Enemy, Side.Player, 0)]
    [InlineData(1, Side.Player, Side.Player, Side.Enemy, 1)]
    [InlineData(1, Side.Player, Side.Enemy, Side.Player, 2)]
    [InlineData(2, Side.Player, Side.Player, Side.Enemy, 0)]
    [InlineData(2, Side.Enemy, Side.Player, Side.Enemy, 2)]
    [InlineData(0, Side.Enemy, Side.Player, Side.Enemy, 0)]
    public void TheChillClockTurnsAtItsSidesPhaseStartAndClearsAtThatPhasesEnd(int chill, Side side, Side ended, Side begins, int after)
    {
        Assert.Equal(after, Frost.AtPhaseChange(chill, side, ended, begins));
    }

    [Fact]
    public void AnEnemyChilledOnThePlayerPhaseMovesShortThroughTheEnemyPhaseAndNoLonger()
    {
        var state = Placed();
        var brigand = Brigand(state);
        var full = Shipped.Class(brigand.Unit.ClassId).Mov;
        state = state.WithUnit(brigand with { Chill = 1 });

        var enemyPhase = Resolver.Apply(state, Shipped, new EndPhase()).Next;
        Assert.Equal(Side.Enemy, enemyPhase.Phase);
        Assert.Equal(full - 1, enemyPhase.ReachOf(enemyPhase.Find(brigand.Id)!, Shipped).Mov);

        var playerPhase = Resolver.Apply(enemyPhase, Shipped, new EndPhase()).Next;
        Assert.Equal(Side.Player, playerPhase.Phase);
        Assert.Equal(0, playerPhase.Find(brigand.Id)!.Chill);
        Assert.Equal(full, playerPhase.ReachOf(playerPhase.Find(brigand.Id)!, Shipped).Mov);
    }

    [Fact]
    public void APlayerChilledOnItsOwnPhaseStaysChilledThroughItsNextPhase()
    {
        var state = Placed();
        var wren = state.Find("wren")!;
        state = state.WithUnit(wren with { Chill = 1 });

        var enemyPhase = Resolver.Apply(state, Shipped, new EndPhase()).Next;
        Assert.Equal(1, enemyPhase.Find("wren")!.Chill);
        var nextPlayer = Resolver.Apply(enemyPhase, Shipped, new EndPhase()).Next;
        Assert.Equal(2, nextPlayer.Find("wren")!.Chill);
        Assert.Equal(Shipped.Class(wren.Unit.ClassId).Mov - 1, nextPlayer.ReachOf(nextPlayer.Find("wren")!, Shipped).Mov);
        var after = Resolver.Apply(Resolver.Apply(nextPlayer, Shipped, new EndPhase()).Next, Shipped, new EndPhase()).Next;
        Assert.Equal(0, after.Find("wren")!.Chill);
    }

    [Fact]
    public void ThreatPlansAChilledEnemysShorterReach()
    {
        var state = Placed();
        var brigand = Brigand(state) with { Behavior = Behavior.Aggressive };
        state = state.WithUnit(brigand);
        var chilled = state.WithUnit(brigand with { Chill = 1 });

        var full = Threat.StruckByUnit(state, Shipped, state.Find(brigand.Id)!);
        var short_ = Threat.StruckByUnit(chilled, Shipped, chilled.Find(brigand.Id)!);

        Assert.True(short_.IsProperSubsetOf(full), "a chilled enemy's threatened tiles are a proper subset of its unchilled ones");
    }

    [Fact]
    public void TheForecastPrintsChillsExactlyWhenTheWeaponIsFrozenIron()
    {
        var woken = Facing(Placed());
        var rusted = Facing(Placed(), stage: 0);

        string Text(BattleState state)
        {
            var target = Brigand(state);
            var forecast = Queries.Forecast(state, Shipped, Teodor(state), target)!;
            return PlaySession.ForecastText(state, Shipped, Teodor(state), target, forecast, Teodor(state).At, fromTile: false);
        }

        var line = Text(woken).Split('\n')[0];
        Assert.Contains("% chills; counter", line);
        Assert.DoesNotContain("chills", Text(rusted));
    }

    [Fact]
    public void TheCounterPrintsChillsWhenTheDefendersWeaponIsFrozenIron()
    {
        var state = Facing(Placed());
        var brigand = Brigand(state);
        var forecast = Queries.Forecast(state, Shipped, brigand, Teodor(state))!;
        Assert.True(forecast.Defender.Strikes);

        var line = PlaySession.ForecastText(state, Shipped, brigand, Teodor(state), forecast, brigand.At, fromTile: false).Split('\n')[0];

        Assert.EndsWith(" chills", line);
        Assert.DoesNotContain("% chills;", line);
    }

    [Fact]
    public void TheCardAndTheEventNameTheChillAndItsSide()
    {
        var state = Placed();
        var brigand = Brigand(state) with { Chill = 1 };
        state = state.WithUnit(brigand);
        var names = UnitNames.Of(state, Shipped);

        Assert.Contains(PlaySession.ShowLines(state, Shipped, brigand), l => l == "  chilled: Mov -1 until enemy phase ends");
        Assert.EndsWith("is chilled: Mov -1 until enemy phase ends", PlaySession.Describe(new UnitChilled(brigand.Id, "teodor", Side.Enemy), Shipped, names));
        Assert.Null(Frost.CardLine(state, brigand with { Chill = 0 }));
        Assert.Equal("chilled: Mov -1 until the next enemy phase ends", Frost.CardLine(state with { Phase = Side.Enemy }, brigand));
        Assert.EndsWith("is chilled: Mov -1 until the next enemy phase ends", PlaySession.Describe(new UnitChilled(brigand.Id, "teodor", Side.Enemy, Next: true), Shipped, names));
        Assert.Contains(PlaySession.ShowLines(state, Shipped, Teodor(state)), l => l.StartsWith("  Frozen iron: a hit chills"));
        Assert.DoesNotContain(PlaySession.ShowLines(Facing(state, stage: 2), Shipped, Teodor(Facing(state, stage: 2))), l => l.Contains("Frozen iron"));
    }

    [Fact]
    public void TheChillClockRoundTripsThroughTheProtocol()
    {
        var state = Placed();
        state = state.WithUnit(Brigand(state) with { Chill = 2 });

        var json = ProtocolJson.State(state, Shipped);
        var back = ProtocolJson.ReadState(json, Shipped);

        Assert.Contains("\"chill\":2", json);
        Assert.Equal(2, Brigand(back).Chill);
        Assert.Equal(json, ProtocolJson.State(back, Shipped));
    }
}
