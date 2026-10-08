using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Grounding (issue 703, Lotus's batch item 5, round 216): a bow's crit on a flier that survives lands
/// it until the end of its side's next phase, so it moves on foot, and over a tile infantry cannot
/// enter it cannot move at all but still acts. Bows trade the effective tag for +20 crit against
/// fliers; Gust kept its tag, and Spark Storm, which it became, drops it (DECISIONS/0348). Since issue 723 (round 220) that crit deals plain damage, not triple,
/// for either side. The forecast prints <c>grounds N%</c> in the crit's place, the card and the event name the
/// clock, and every reach reads it. Played on the shipped Saltmarsh Ford, whose wingrider holds the
/// north bank above the river row.
/// </summary>
public class GroundingTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly string MapPath = Path.Combine(Fixture.RealContentDirectory(), "maps", "saltmarsh_ford.map");

    private static readonly Coord River = new(12, 3);

    private static BattleState Placed(ulong seed = 703) =>
        BattleState.From(MapFiles.Load(MapPath, Shipped), Shipped, Shipped.Cast, seed);

    private static BattleUnit Wingrider(BattleState state) => state.Units.Single(u => u.Side == Side.Enemy && u.Unit.ClassId == "skyrider");

    private static BattleUnit Archer(BattleState state) => state.Units.Single(u => u.Side == Side.Enemy && u.Unit.ClassId == "bowman");

    /// <summary>The wingrider made too sturdy for a crit to kill, so a landed crit leaves it standing.</summary>
    private static BattleUnit Sturdy(BattleUnit unit) =>
        unit with { Unit = unit.Unit with { Stats = unit.Unit.Stats with { Hp = 80 } }, Hp = 80 };

    /// <summary>A player unit stood at <paramref name="at"/> as a bowman with an Iron Bow in front.</summary>
    private static BattleState WithPlayerBow(BattleState state, Coord at, out string id)
    {
        var unit = state.UnitsOf(Side.Player).First();
        id = unit.Id;
        var bow = unit.Unit with { ClassId = "bowman", Inventory = unit.Unit.Inventory.Replace(0, new ItemStack("iron_bow", 40)) };
        return state.WithUnit(unit with { Unit = bow, At = at, Hp = Math.Min(unit.Hp, Shipped.StatsOf(bow).Hp) });
    }

    [Theory]
    [InlineData("iron_bow")]
    [InlineData("steel_bow")]
    [InlineData("obsidian_bow")]
    public void BowsCarryNoEffectiveTagAndTwentyCritAgainstFlyingOnly(string id)
    {
        var bow = Shipped.Weapon(id);

        Assert.Empty(bow.EffectiveAgainst);
        Assert.Equal(20, bow.CritBonusAgainst(MovementType.Flying));
        Assert.Equal(0, bow.CritBonusAgainst(MovementType.Infantry));
        Assert.Equal(0, bow.CritBonusAgainst(MovementType.Cavalry));
        Assert.Equal(0, bow.CritBonusAgainst(MovementType.Armored));
    }

    [Fact]
    public void SparkStormDropsGustsEffectiveTag()
    {
        Assert.False(Shipped.Weapon("gust").IsEffectiveAgainst(MovementType.Flying));
        Assert.Empty(Shipped.Weapon("gust").CritAgainst);
    }

    [Theory]
    [InlineData("skyrider", 20)]
    [InlineData("pikeman", 0)]
    [InlineData("outrider", 0)]
    [InlineData("bulwark", 0)]
    public void ABowsCritChanceRisesByItsBonusAgainstAFlierOnly(string targetClass, int bonus)
    {
        var state = Placed();
        var archer = Archer(state);
        var wing = Wingrider(state);
        var target = wing with { Unit = wing.Unit with { ClassId = targetClass, Inventory = Inventory.Empty } };
        target = target with { Hp = Shipped.StatsOf(target.Unit).Hp };
        var defender = target.ToCombatant(state, Shipped);
        var bow = Shipped.Weapon("iron_bow");
        var with = Shipped.CombatantOf(archer.Unit, bow, defender.Terrain, archer.Hp);
        var without = Shipped.CombatantOf(archer.Unit, bow with { CritBonus = 0 }, defender.Terrain, archer.Hp);

        Assert.Equal(bonus, Ironwake.Core.Combat.CritChance(with, defender) - Ironwake.Core.Combat.CritChance(without, defender));
    }

    [Fact]
    public void ABowCritOnAFlierGroundsItAndAHitThatDoesNotCritDoesNot()
    {
        var (crit, plain) = (false, false);
        for (ulong seed = 1; seed < 600 && !(crit && plain); seed++)
        {
            var state = WithPlayerBow(Placed(seed), new Coord(12, 4), out var id);
            state = state.WithUnit(Sturdy(Wingrider(state)) with { At = new Coord(12, 2) });
            var wing = Wingrider(state);
            var result = Resolver.Apply(state, Shipped, new Attack(id, wing.Id));
            Assert.True(result.Accepted, result.Rejection?.Message);
            var strikes = result.Events.OfType<CombatFought>().Single().Strikes.Where(s => s.AttackerId == id).ToList();
            var critted = strikes.Any(s => s.Hit && s.Crit);
            var landed = strikes.Any(s => s.Hit);
            Assert.Equal(critted ? 1 : 0, result.Next.Find(wing.Id)!.Grounded);
            Assert.Equal(critted, result.Events.Contains(new UnitGrounded(wing.Id, id, Side.Enemy)));
            crit |= critted;
            plain |= landed && !critted;
        }

        Assert.True(crit && plain, "no seed under 600 gave both a crit and a plain hit");
    }

    [Theory]
    [InlineData("iron_lance", "skyrider", false)]
    [InlineData("iron_bow", "pikeman", false)]
    [InlineData("iron_bow", "skyrider", true)]
    [InlineData("gust", "skyrider", false)]
    public void OnlyABowsCritOnAFlierGrounds(string weapon, string targetClass, bool grounds)
    {
        var state = Placed();
        var wing = Wingrider(state);
        var target = wing with { Unit = wing.Unit with { ClassId = targetClass } };
        state = state.WithUnit(target);
        var strikes = ValueList<StrikeEvent>.Empty.Add(new StrikeEvent(0, "striker", wing.Id, true, true, 3, wing.Hp - 3));

        var next = Grounding.AfterCombat(state, Shipped, "striker", Shipped.Weapon(weapon), wing.Id, null, strikes, new List<GameEvent>());

        Assert.Equal(grounds ? 1 : 0, next.Find(wing.Id)!.Grounded);
    }

    [Fact]
    public void AnEffectiveBowsMapKeepsTheOldBowAndNeverGrounds()
    {
        var state = Placed();
        var old = state with { Map = state.Map with { EffectiveBows = true } };
        var wing = Wingrider(old);
        var strikes = ValueList<StrikeEvent>.Empty.Add(new StrikeEvent(0, "striker", wing.Id, true, true, 3, wing.Hp - 3));

        var next = Grounding.AfterCombat(old, Shipped, "striker", Shipped.Weapon("iron_bow"), wing.Id, null, strikes, new List<GameEvent>());

        Assert.Equal(0, next.Find(wing.Id)!.Grounded);
        var bow = Grounding.ForMap(old.Map, Shipped.Weapon("iron_bow"))!;
        Assert.True(bow.IsEffectiveAgainst(MovementType.Flying));
        Assert.Equal(0, bow.CritBonusAgainst(MovementType.Flying));
        Assert.Same(Shipped.Weapon("iron_bow"), Grounding.ForMap(state.Map, Shipped.Weapon("iron_bow")));
    }

    [Fact]
    public void TheEffectiveBowsHeaderRoundTrips()
    {
        var sample = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "saltmarsh_ford_0093.map");
        var map = MapFiles.Load(sample, Shipped);

        Assert.True(map.EffectiveBows);
        Assert.False(MapFiles.Load(MapPath, Shipped).EffectiveBows);
        Assert.Equal(File.ReadAllText(sample).Replace("\r\n", "\n"), MapFormat.Write(map, Shipped));
    }

    [Fact]
    public void AGroundedFlierMovesOnFootAndAStrandedOneHasNoMove()
    {
        var state = Placed();
        var wing = Wingrider(state);
        var bank = wing with { At = new Coord(12, 1), Grounded = 1 };
        var river = wing with { At = River, Grounded = 1 };

        Assert.Equal(MovementType.Flying, Grounding.MovementOf(wing, Shipped));
        Assert.Equal(MovementType.Infantry, Grounding.MovementOf(bank, Shipped));
        Assert.DoesNotContain(state.WithUnit(bank).ReachOf(bank, Shipped).Destinations, t => state.Map.TerrainAt(t, Shipped).Id == "water");
        Assert.Contains(state.WithUnit(wing with { At = new Coord(12, 1) }).ReachOf(wing with { At = new Coord(12, 1) }, Shipped).Destinations, t => state.Map.TerrainAt(t, Shipped).Id == "water");

        var stranded = state.WithUnit(river);
        Assert.True(Grounding.Stranded(stranded, river, Shipped));
        Assert.Equal(new[] { River }, stranded.ReachOf(river, Shipped).Destinations.ToArray());
    }

    [Fact]
    public void AStrandedFlierStillStrikesFromWhereItFell()
    {
        var state = WithPlayerBow(Placed(), new Coord(12, 4), out var id) with { Phase = Side.Enemy };
        var river = Wingrider(state) with { At = River, Grounded = 2, Behavior = Behavior.Aggressive };
        state = state.WithUnit(river);

        Assert.Contains(River, EnemyAi.AttackTiles(state, Shipped, river, river.EquippedWeapon(Shipped)!, state.Find(id)!, Grounding.MovementOf(river, Shipped)));
        var result = Resolver.Apply(state, Shipped, new Attack(river.Id, id));
        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(River, result.Next.Find(river.Id)!.At);
    }

    [Fact]
    public void GroundingLastsThroughTheGroundedSidesNextPhaseAndNoLonger()
    {
        var state = Placed();
        var wing = Wingrider(state);
        state = state.WithUnit(wing with { At = new Coord(12, 1), Grounded = 1 });

        var enemyPhase = Resolver.Apply(state, Shipped, new EndPhase()).Next;
        Assert.Equal(2, enemyPhase.Find(wing.Id)!.Grounded);
        Assert.Equal(MovementType.Infantry, enemyPhase.ReachOf(enemyPhase.Find(wing.Id)!, Shipped).Movement);

        var playerPhase = Resolver.Apply(enemyPhase, Shipped, new EndPhase()).Next;
        Assert.Equal(0, playerPhase.Find(wing.Id)!.Grounded);
        Assert.Equal(MovementType.Flying, playerPhase.ReachOf(playerPhase.Find(wing.Id)!, Shipped).Movement);
    }

    [Fact]
    public void ThreatPlansAGroundedEnemysReachOnFoot()
    {
        var state = Placed();
        var wing = Wingrider(state) with { Behavior = Behavior.Aggressive, At = new Coord(12, 1) };
        state = state.WithUnit(wing);
        var grounded = state.WithUnit(wing with { Grounded = 1 });

        var flying = Threat.StruckByUnit(state, Shipped, state.Find(wing.Id)!);
        var walking = Threat.StruckByUnit(grounded, Shipped, grounded.Find(wing.Id)!);

        Assert.True(walking.IsProperSubsetOf(flying), "a grounded flier's threatened tiles are a proper subset of its flying ones");
    }

    [Fact]
    public void TheForecastPrintsGroundsExactlyWhenABowCanCritAFlier()
    {
        var state = WithPlayerBow(Placed(), new Coord(12, 4), out var id);
        state = state.WithUnit(Wingrider(state) with { At = new Coord(12, 2) });
        var bowman = state.Find(id)!;
        var wing = Wingrider(state);

        var forecast = Queries.Forecast(state, Shipped, bowman, wing)!;
        var line = PlaySession.ForecastText(state, Shipped, bowman, wing, forecast, bowman.At, fromTile: false).Split('\n')[0];
        Assert.True(forecast.Attacker.CritGrounds);
        Assert.Contains($"dmg {forecast.Attacker.Damage} grounds {forecast.Attacker.CritChance}%; counter", line);
        Assert.DoesNotContain($"crit {forecast.Attacker.CritChance}%; counter", line);

        var old = state with { Map = state.Map with { EffectiveBows = true } };
        Assert.DoesNotContain("grounds", PlaySession.ForecastText(old, Shipped, bowman, wing, Queries.Forecast(old, Shipped, bowman, wing)!, bowman.At, fromTile: false));

        var archer = Archer(state);
        var onFoot = state.Find(id)! with { At = new Coord(10, 3) };
        var walker = state.WithUnit(onFoot);
        Assert.DoesNotContain("grounds", PlaySession.ForecastText(walker, Shipped, onFoot, archer, Queries.Forecast(walker, Shipped, onFoot, archer, new Coord(10, 3))!, new Coord(10, 3), fromTile: true));
    }

    [Theory]
    [InlineData("iron_bow", MovementType.Flying, false, true)]
    [InlineData("steel_bow", MovementType.Flying, false, true)]
    [InlineData("iron_bow", MovementType.Infantry, false, false)]
    [InlineData("iron_bow", MovementType.Cavalry, false, false)]
    [InlineData("iron_lance", MovementType.Flying, false, false)]
    [InlineData("gust", MovementType.Flying, false, false)]
    [InlineData("iron_bow", MovementType.Flying, true, false)]
    public void ACritGroundsInsteadOfTriplingOnlyForABowOnAFlier(string weapon, MovementType movement, bool effectiveBows, bool grounds)
    {
        var map = Placed().Map with { EffectiveBows = effectiveBows };
        var armed = Grounding.ForMap(map, Shipped.Weapon(weapon))!;

        Assert.Equal(grounds, armed.GroundsAgainst(movement));
        Assert.Equal(grounds, Grounding.Grounds(map, Shipped.Weapon(weapon), movement));
    }

    [Theory]
    [InlineData("iron_bow", true)]
    [InlineData("iron_lance", false)]
    public void ABowCritOnAFlierDealsPlainDamageAndAnyOtherCritOnItTriples(string weapon, bool grounds)
    {
        var state = Placed();
        var wing = Sturdy(Wingrider(state));
        var defender = wing.ToCombatant(state, Shipped);
        var striker = state.UnitsOf(Side.Player).First();
        var unit = striker.Unit with { ClassId = grounds ? "bowman" : "pikeman", Inventory = Inventory.Empty };
        var attacker = Shipped.CombatantOf(unit, Shipped.Weapon(weapon), defender.Terrain, Shipped.StatsOf(unit).Hp);
        var distance = Shipped.Weapon(weapon).MinRange;

        var forecast = Ironwake.Core.Combat.Forecast(attacker, defender, distance, RollScheme.TwoRollAverage);
        var result = CombatResolver.Resolve(attacker, defender, distance, new CombatContext(1, Side.Player), new Ironwake.Core.Tests.Combat.ScriptedRng(0), RollScheme.TwoRollAverage);
        var first = result.Strikes.First(s => s.AttackerId == attacker.Id);

        Assert.True(forecast.Attacker.Damage > 0 && forecast.Attacker.CritChance > 0, "the striker must land a crit for damage");
        Assert.Equal(grounds, forecast.Attacker.CritGrounds);
        Assert.True(first.Hit && first.Crit);
        Assert.Equal(grounds ? forecast.Attacker.Damage : forecast.Attacker.Damage * Ironwake.Core.Combat.CritMultiplier, first.Damage);
        Assert.Equal(first.Damage, forecast.Attacker.CritDamage);
    }

    [Fact]
    public void APlayerBowsCritOnTheShippedWingriderGroundsItInsteadOfKillingIt()
    {
        for (ulong seed = 1; seed < 600; seed++)
        {
            var state = WithPlayerBow(Placed(seed), new Coord(12, 4), out var id);
            state = state.WithUnit(Wingrider(state) with { At = new Coord(12, 2) });
            var wing = Wingrider(state);
            var forecast = Queries.Forecast(state, Shipped, state.Find(id)!, wing)!;
            var result = Resolver.Apply(state, Shipped, new Attack(id, wing.Id));
            var strikes = result.Events.OfType<CombatFought>().Single().Strikes.Where(s => s.AttackerId == id).ToList();
            if (!strikes.Any(s => s.Hit && s.Crit))
            {
                continue;
            }

            Assert.All(strikes.Where(s => s.Hit), s => Assert.Equal(forecast.Attacker.Damage, s.Damage));
            Assert.True(forecast.Attacker.Damage * forecast.Attacker.StrikeCount < wing.Hp, "the shipped numbers leave the wingrider standing on plain damage");
            Assert.Equal(1, result.Next.Find(wing.Id)!.Grounded);
            return;
        }

        Assert.Fail("no seed under 600 gave a bow crit");
    }

    [Fact]
    public void AnEnemyBowsCritOnAPlayerFlierDealsPlainDamageAndGroundsIt()
    {
        for (ulong seed = 1; seed < 600; seed++)
        {
            var state = Placed(seed) with { Phase = Side.Enemy };
            var archer = Archer(state);
            var flier = state.UnitsOf(Side.Player).First();
            var winged = flier.Unit with { ClassId = "skyrider", Inventory = flier.Unit.Inventory.Replace(0, new ItemStack("iron_lance", 40)) };
            var hp = Shipped.StatsOf(winged).Hp + 40;
            flier = flier with { Unit = winged with { Stats = winged.Stats with { Hp = winged.Stats.Hp + 40 } }, Hp = hp, At = Free(state, archer.At) };
            state = state.WithUnit(flier);
            var forecast = Queries.Forecast(state, Shipped, archer, state.Find(flier.Id)!)!;
            var result = Resolver.Apply(state, Shipped, new Attack(archer.Id, flier.Id));
            Assert.True(result.Accepted, result.Rejection?.Message);
            var strikes = result.Events.OfType<CombatFought>().Single().Strikes.Where(s => s.AttackerId == archer.Id).ToList();
            if (!strikes.Any(s => s.Hit && s.Crit))
            {
                continue;
            }

            Assert.True(forecast.Attacker.CritGrounds);
            Assert.All(strikes.Where(s => s.Hit), s => Assert.Equal(forecast.Attacker.Damage, s.Damage));
            Assert.Equal(1, result.Next.Find(flier.Id)!.Grounded);
            Assert.Contains(new UnitGrounded(flier.Id, archer.Id, Side.Player, false), result.Events);
            return;
        }

        Assert.Fail("no seed under 600 gave the archer a crit");

        static Coord Free(BattleState state, Coord from)
        {
            foreach (var at in new[] { new Coord(from.X, from.Y + 2), new Coord(from.X, from.Y - 2), new Coord(from.X + 2, from.Y), new Coord(from.X - 2, from.Y), new Coord(from.X + 1, from.Y + 1), new Coord(from.X - 1, from.Y + 1), new Coord(from.X + 1, from.Y - 1), new Coord(from.X - 1, from.Y - 1) })
            {
                if (at.X >= 0 && at.Y >= 0 && at.X < state.Map.Width && at.Y < state.Map.Height && state.Units.All(u => u.At != at))
                {
                    return at;
                }
            }

            throw new InvalidOperationException("no free tile two from the archer");
        }
    }

    [Fact]
    public void AGroundingSideRoundTripsThroughTheProtocolAndAPlainSideWritesNoField()
    {
        var grounding = new SideForecast(true, 9, 70, 80, 23, false, CritGrounds: true);
        var plain = grounding with { CritGrounds = false };
        var forecast = new CombatForecast(grounding, plain, RollScheme.TwoRollAverage);

        var json = ProtocolJson.Forecast(forecast);
        Assert.Equal(forecast, ProtocolJson.ReadForecast(json));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(json, "critGrounds"));
        Assert.DoesNotContain("critGrounds", ProtocolJson.Forecast(forecast with { Attacker = plain }));
        Assert.Equal(9, grounding.CritDamage);
        Assert.Equal(27, plain.CritDamage);
    }

    [Fact]
    public void TheCardAndTheEventNameTheGroundingAndItsSide()
    {
        var state = Placed();
        var wing = Wingrider(state) with { At = new Coord(12, 1), Grounded = 1 };
        state = state.WithUnit(wing);
        var names = UnitNames.Of(state, Shipped);

        Assert.Contains(PlaySession.ShowLines(state, Shipped, wing), l => l == "  grounded until enemy phase ends, moves on foot");
        var river = wing with { At = River };
        Assert.Contains(PlaySession.ShowLines(state.WithUnit(river), Shipped, river), l => l == "  grounded until enemy phase ends, stranded: no move, may still act");
        Assert.Null(Grounding.CardLine(state, wing with { Grounded = 0 }));
        Assert.Equal("grounded until the next enemy phase ends", Grounding.CardLine(state with { Phase = Side.Enemy }, wing));
        Assert.EndsWith("is grounded: moves on foot until enemy phase ends", PlaySession.Describe(new UnitGrounded(wing.Id, "ottilie", Side.Enemy), Shipped, names));
        Assert.EndsWith("is grounded: moves on foot until the next enemy phase ends", PlaySession.Describe(new UnitGrounded(wing.Id, "ottilie", Side.Enemy, Next: true), Shipped, names));
    }

    [Fact]
    public void TheGroundingClockRoundTripsThroughTheProtocol()
    {
        var state = Placed();
        state = state.WithUnit(Wingrider(state) with { Grounded = 2 });

        var json = ProtocolJson.State(state, Shipped);
        var back = ProtocolJson.ReadState(json, Shipped);

        Assert.Contains("\"grounded\":2", json);
        Assert.Equal(2, Wingrider(back).Grounded);
        Assert.Equal(json, ProtocolJson.State(back, Shipped));
    }

    [Theory]
    [InlineData("\"critAgainst\": [\"flying\"], ", "critBonus", "must be 1..100")]
    [InlineData("\"critBonus\": 20, ", "critBonus", "only valid when critAgainst")]
    [InlineData("\"critAgainst\": [\"flying\", \"flying\"], \"critBonus\": 20, ", "critAgainst", "must not repeat")]
    public void ACritBonusWithoutItsMovementOrTheOtherWayIsRefusedAtLoad(string extra, string field, string why)
    {
        var line = """{ "id": "fowler", "name": "Fowler", "type": "bow", "mt": 5, "hit": 70, "crit": 0, "wt": 5, "minRange": 2, "maxRange": 2, "durability": 20, "rank": "E", "description": "A test line." }""";
        var weapons = Fixture.Weapons.Replace("\n] }", ",\n" + line.Replace("\"rank\"", extra + "\"rank\"") + "\n] }");

        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(weapons: weapons)));
        Assert.Contains("fowler", error.Message);
        Assert.Contains(field, error.Message);
        Assert.Contains(why, error.Message);
    }

    [Fact]
    public void ACritBonusRoundTripsThroughTheSerializer()
    {
        var again = ContentLoader.Parse(ContentSerializer.Write(Shipped));

        Assert.Equal(20, again.Weapon("iron_bow").CritBonusAgainst(MovementType.Flying));
        Assert.Contains("\"critBonus\": 20", ContentSerializer.Write(Shipped).Weapons.Text);
    }
}
