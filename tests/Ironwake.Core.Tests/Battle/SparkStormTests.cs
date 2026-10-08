using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Combat;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Spark Storm's area cast and its mark (issue 1329 slice 1, DECISIONS/0322, 0326). A tome naming <c>area</c> is cast
/// through the Item action at a unit or a tile and strikes every enemy within its radius once with no counter; a tome
/// naming <c>marks</c> marks every unit it hits that survives, and the first hit on a marked unit from any tome of that
/// school deals x1.5 on final damage and spends the mark. No shipped tome carries either until Lotus signs Spark Storm's
/// numbers, so these tests use fixtures cut from Gust: <c>test_storm</c> (area 1, marks) and <c>test_bolt</c> (a plain
/// lightning tome), with the flier rider dropped, Pell holding them on the sample <c>the_tollgate_frost.map</c>, two
/// tiles below the woods brigand at 6,5 and its archer at 5,5.
/// </summary>
public class SparkStormTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly Weapon Gust = Shipped.Weapon("gust") with { EffectiveAgainst = ValueList<MovementType>.Empty };

    private static readonly GameContent Storms = Shipped with
    {
        Weapons = Shipped.Weapons
            .SetItem("test_storm", Gust with { Id = "test_storm", Name = "Test Storm", Area = 1, Marks = true })
            .SetItem("test_bolt", Gust with { Id = "test_bolt", Name = "Test Bolt" }),
    };

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_tollgate_frost.map");

    /// <summary>Pell at 6,7 holding the storm in slot 0 and the bolt in slot 1, Wren beside the brigand at 6,6.</summary>
    private static BattleState Facing(ulong seed = 1329, int stormUses = 6)
    {
        var state = BattleState.From(MapFiles.Load(SamplePath, Storms), Storms, Storms.Cast, seed);
        var pell = state.Find("pell")!;
        var inventory = new Inventory(ValueList<ItemStack>.From([new ItemStack("test_storm", stormUses), new ItemStack("test_bolt", Gust.Durability), new ItemStack("cinder", 6)]));
        state = state.WithUnit(pell with { Unit = pell.Unit with { Inventory = inventory }, At = new Coord(6, 7) });
        return state.WithUnit(state.Find("wren")! with { At = new Coord(6, 6) });
    }

    private static BattleUnit Brigand(BattleState state) => state.Units.Single(u => u.Side == Side.Enemy && u.Group == "woods" && u.Unit.ClassId == Shipped.Unit("toll_brigand").ClassId);

    private static BattleUnit Archer(BattleState state) => state.Units.Single(u => u.Side == Side.Enemy && u.Group == "woods" && u.Id != Brigand(state).Id);

    private static BattleState Marked(BattleState state) => state.WithUnit(Brigand(state) with { Mark = MagicSchool.Lightning });

    /// <summary>The first seed under 400 whose command passes <paramref name="keep"/>, the board before it and its result.</summary>
    private static (BattleState Before, ApplyResult Result) Seeded(Func<BattleState, Command> command, Func<BattleState, ApplyResult, bool> keep, Func<BattleState, BattleState>? edit = null)
    {
        for (ulong seed = 1; seed < 400; seed++)
        {
            var state = Facing(seed);
            state = edit?.Invoke(state) ?? state;
            var result = Resolver.Apply(state, Storms, command(state));
            Assert.True(result.Accepted, result.Rejection?.Message);
            if (keep(state, result))
            {
                return (state, result);
            }
        }

        throw new InvalidOperationException("no seed under 400 gave the result asked for");
    }

    private static Command Storm(BattleState state) => new UseItem("pell", 0, "6,5");

    private static Command Bolt(BattleState state) => new Attack("pell", Brigand(state).Id, 1);

    private static ValueList<StrikeEvent> StrikesOn(ApplyResult result, string targetId) =>
        result.Events.OfType<CombatFought>().Single(f => f.TargetId == targetId).Strikes;

    private static bool HitOn(ApplyResult result, string targetId) =>
        StrikesOn(result, targetId).Any(s => s.AttackerId == "pell" && s.Hit);

    private static bool Survives(BattleState before, ApplyResult result) =>
        result.Next.Find(Brigand(before).Id) is not null;

    private static int Plain(BattleState state) =>
        AreaCast.Forecast(state, Storms, state.Find("pell")!, Storms.Weapon("test_bolt"), Brigand(state), new Coord(6, 5)).Attacker.Damage;

    [Fact]
    public void AnAreaCastStrikesEveryEnemyWithinItsRadiusOnceAndNoAlly()
    {
        var state = Facing();
        var result = Resolver.Apply(state, Storms, Storm(state));

        Assert.True(result.Accepted, result.Rejection?.Message);
        var cast = result.Events.OfType<AreaCastAt>().Single();
        Assert.Equal(new Coord(6, 5), cast.At);
        Assert.Equal(new[] { Brigand(state).Id, Archer(state).Id }.OrderBy(id => state.Units.ToList().FindIndex(u => u.Id == id)), cast.Struck);
        var fights = result.Events.OfType<CombatFought>().ToList();
        Assert.Equal(cast.Struck, fights.Select(f => f.TargetId));
        Assert.All(fights, f => Assert.Equal("pell", Assert.Single(f.Strikes).AttackerId));
        Assert.Equal(state.Find("wren")!.Hp, result.Next.Find("wren")!.Hp);
        Assert.True(result.Next.Find("pell")!.Acted);
    }

    [Fact]
    public void AnAreaCastDrawsNoCounterAndNeverDoubles()
    {
        var state = Facing();
        var forecast = AreaCast.Forecast(state, Storms, state.Find("pell")!, Storms.Weapon("test_storm"), Brigand(state), new Coord(6, 5));

        Assert.False(forecast.Defender.Strikes);
        Assert.False(forecast.Attacker.Doubles);
        Assert.Equal(state.Find("pell")!.Hp, Resolver.Apply(state, Storms, Storm(state)).Next.Find("pell")!.Hp);
    }

    [Fact]
    public void AnAreaCastSpendsOneUseAndEarnsOneCombatsExp()
    {
        var state = Facing(stormUses: 1);
        var result = Resolver.Apply(state, Storms, Storm(state));

        Assert.Equal(0, result.Events.OfType<AreaCastAt>().Single().UsesLeft);
        Assert.Equal(0, result.Next.Find("pell")!.Unit.Inventory.Items[0].Uses);
        Assert.Single(result.Events.OfType<SpellSpent>());
        Assert.Single(result.Events.OfType<ExpGained>());
    }

    [Fact]
    public void AnAreaCastIsRefusedWithNoEnemyInTheAreaOutOfRangeOrWithNoUseLeft()
    {
        var state = Facing();

        Assert.Equal(RejectionReason.NoSuchTarget, Resolver.Apply(state, Storms, new UseItem("pell", 0, "6,8")).Rejection?.Reason);
        Assert.Equal(RejectionReason.OutOfRange, Resolver.Apply(state, Storms, new UseItem("pell", 0, "6,4")).Rejection?.Reason);
        Assert.Equal(RejectionReason.NoTarget, Resolver.Apply(state, Storms, new UseItem("pell", 0)).Rejection?.Reason);
        Assert.Equal(RejectionReason.NotUsable, Resolver.Apply(Facing(stormUses: 0), Storms, Storm(state)).Rejection?.Reason);
        Assert.Equal(RejectionReason.ArtRefused, Resolver.Apply(state, Storms, new UseItem("pell", 0, "6,5", "anything")).Rejection?.Reason);
    }

    [Fact]
    public void AnAreaCastNamedByAUnitStrikesAroundItsTile()
    {
        var state = Facing();
        var result = Resolver.Apply(state, Storms, new UseItem("pell", 0, Brigand(state).Id));

        Assert.Equal(new Coord(6, 5), result.Events.OfType<AreaCastAt>().Single().At);
    }

    [Fact]
    public void AnAreaTomeIsNeverEquippedSoItNeverAttacksOrCounters()
    {
        var state = Facing();
        var pell = state.Find("pell")!;

        Assert.Equal("test_bolt", pell.EquippedWeapon(Storms)!.Id);
        var refused = Resolver.Apply(state, Storms, new Attack("pell", Brigand(state).Id, 0)).Rejection;
        Assert.Equal(RejectionReason.NotUsable, refused?.Reason);
        Assert.Contains("an area cast", refused!.Message);
    }

    [Fact]
    public void AMarkingHitOnAUnitThatSurvivesMarksIt()
    {
        var (before, result) = Seeded(Storm, (s, r) => HitOn(r, Brigand(s).Id) && Survives(s, r));

        Assert.Equal(MagicSchool.Lightning, result.Next.Find(Brigand(before).Id)!.Mark);
        Assert.Contains(new UnitMarked(Brigand(before).Id, "pell", MagicSchool.Lightning), result.Events);
    }

    [Fact]
    public void AMissMarksNothing()
    {
        var (before, result) = Seeded(Storm, (s, r) => !HitOn(r, Brigand(s).Id));

        Assert.Null(result.Next.Find(Brigand(before).Id)!.Mark);
    }

    [Fact]
    public void AnotherCastersLightningHitCashesTheMarkForHalfAgainOnTheFirstHitAlone()
    {
        var (before, result) = Seeded(Bolt, (s, r) => HitOn(r, Brigand(s).Id) && Survives(s, r), Marked);
        var plain = Plain(before);
        var hits = StrikesOn(result, Brigand(before).Id).Where(s => s.AttackerId == "pell" && s.Hit && !s.Crit).ToList();

        Assert.Equal(Mark.Of(plain), hits[0].Damage);
        Assert.All(hits.Skip(1), h => Assert.Equal(plain, h.Damage));
        Assert.Null(result.Next.Find(Brigand(before).Id)!.Mark);
        Assert.Contains(new MarkCashed(Brigand(before).Id, "pell", MagicSchool.Lightning), result.Events);
    }

    [Fact]
    public void AMissKeepsTheMark()
    {
        var (before, result) = Seeded(Bolt, (s, r) => !HitOn(r, Brigand(s).Id), Marked);

        Assert.Equal(MagicSchool.Lightning, result.Next.Find(Brigand(before).Id)!.Mark);
        Assert.Empty(result.Events.OfType<MarkCashed>());
    }

    [Fact]
    public void AStormOnAMarkedUnitCashesTheOldMarkThenLaysItsOwn()
    {
        var (before, result) = Seeded(Storm, (s, r) => HitOn(r, Brigand(s).Id) && Survives(s, r) && !StrikesOn(r, Brigand(s).Id)[0].Crit, Marked);
        var brigand = Brigand(before).Id;
        var events = result.Events.Where(e => e is MarkCashed c && c.UnitId == brigand || e is UnitMarked m && m.UnitId == brigand).ToList();

        Assert.Equal<GameEvent>([new MarkCashed(brigand, "pell", MagicSchool.Lightning), new UnitMarked(brigand, "pell", MagicSchool.Lightning)], events);
        Assert.Equal(MagicSchool.Lightning, result.Next.Find(brigand)!.Mark);
        Assert.Equal(Mark.Of(AreaCast.Forecast(before, Storms, before.Find("pell")!, Storms.Weapon("test_storm"), Brigand(before), new Coord(6, 5)).Attacker.Damage), StrikesOn(result, brigand)[0].Damage);
    }

    [Fact]
    public void AHitFromAnotherSchoolNeitherCashesNorMultiplies()
    {
        var state = Marked(Facing());

        Assert.False(Queries.Forecast(state, Storms, state.Find("pell")!, Brigand(state), slot: 2)!.Attacker.CashesMark);
        Assert.True(Queries.Forecast(state, Storms, state.Find("pell")!, Brigand(state), slot: 1)!.Attacker.CashesMark);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(4, 6)]
    [InlineData(5, 7)]
    [InlineData(15, 22)]
    public void TheMarkMultipliesFinalDamageByHalfAgainRoundedDownOnce(int damage, int marked)
    {
        Assert.Equal(marked, Mark.Of(damage));
    }

    [Fact]
    public void AMarkedCritMultipliesTheCritOnce()
    {
        var side = new SideForecast(true, 5, 100, 100, 0, true, CashesMark: true);

        Assert.Equal(7, side.MarkedDamage);
        Assert.Equal(22, side.MarkedCritDamage);
    }

    /// <summary>A fast lightning caster that doubles the forest brigand, marked, at range 2 where its axe cannot answer.</summary>
    private static (Combatant Caster, Combatant Brigand) Doubling()
    {
        var unit = new Unit("caster", "caster", "adept", 1, 0, new Stats(20, 1, 8, 6, 12, 5, 4, 2, 3), Stats.Zero, Inventory.Empty, ValueList<string>.Empty);
        var caster = new Combatant(unit, CombatFixture.Adept, CombatFixture.Spark with { School = MagicSchool.Lightning }, CombatFixture.Plain, 20);
        return (caster, CombatFixture.BrigandInForest() with { Marked = MagicSchool.Lightning });
    }

    private static ScriptedRng NoCrits(params int[] missed)
    {
        var rng = new ScriptedRng(0);
        for (var i = 0; i < 2; i++)
        {
            rng.Set(RollKey.Combat(1, Side.Player, "caster", "brigand", i, CombatRoll.Crit), 99);
            rng.Set(RollKey.Combat(1, Side.Player, "caster", "brigand", i, CombatRoll.HitA), missed.Contains(i) ? 99 : 0);
        }

        return rng;
    }

    [Fact]
    public void TheMarkIsCashedByTheFirstHitAloneAndADoubledSecondHitIsPlain()
    {
        var (caster, brigand) = Doubling();
        var forecast = Ironwake.Core.Combat.Forecast(caster, brigand, 2, RollScheme.OneRoll).Attacker;
        Assert.True(forecast.Doubles);
        Assert.True(forecast.CashesMark);

        var result = CombatResolver.Resolve(caster, brigand, 2, new CombatContext(1, Side.Player), NoCrits(), RollScheme.OneRoll);

        Assert.Equal([forecast.MarkedDamage, forecast.Damage], result.Strikes.Select(s => s.Damage));
    }

    [Fact]
    public void AMissedFirstStrikeLeavesTheMarkForTheSecond()
    {
        var (caster, brigand) = Doubling();
        var forecast = Ironwake.Core.Combat.Forecast(caster, brigand, 2, RollScheme.OneRoll).Attacker;

        var result = CombatResolver.Resolve(caster, brigand, 2, new CombatContext(1, Side.Player), NoCrits(0), RollScheme.OneRoll);

        Assert.Equal([0, forecast.MarkedDamage], result.Strikes.Select(s => s.Damage));
    }

    [Fact]
    public void AnUnmarkedUnitTakesPlainDamage()
    {
        var (caster, brigand) = Doubling();

        Assert.False(Ironwake.Core.Combat.Forecast(caster, brigand with { Marked = null }, 2, RollScheme.OneRoll).Attacker.CashesMark);
        Assert.False(Ironwake.Core.Combat.Forecast(caster, brigand with { Marked = MagicSchool.Fire }, 2, RollScheme.OneRoll).Attacker.CashesMark);
    }

    [Fact]
    public void TheForecastAndTheCardShowTheMark()
    {
        var state = Marked(Facing());

        Assert.Equal("marked: next lightning x1.5", Mark.CardLine(Brigand(state)));
        Assert.Null(Mark.CardLine(state.Find("pell")!));
        Assert.Equal(" cashes the mark (x1.5)", Mark.ForecastText(new SideForecast(true, 5, 100, 100, 0, false, CashesMark: true)));
        Assert.Contains("cashes the mark (x1.5)", AreaCast.Preview(state, Storms, state.Find("pell")!, Storms.Weapon("test_storm"), new Coord(6, 5)));
        Assert.EndsWith(", no counter", AreaCast.Preview(state, Storms, state.Find("pell")!, Storms.Weapon("test_storm"), new Coord(6, 5)));
        Assert.Contains("strikes every enemy within 1 of it once, no counter", ItemCard.Text(Storms, "test_storm"));
    }

    [Fact]
    public void TheKillFlagReadsTheMarkedFirstHit()
    {
        var side = new SideForecast(true, 5, 100, 100, 0, false, CashesMark: true);
        var forecast = new CombatForecast(side, SideForecast.None, RollScheme.OneRoll);

        Assert.Equal(7, forecast.AttackerDamageLivedFor(20));
    }

    [Fact]
    public void AMarkReadsBackEqualFromTheProtocol()
    {
        var state = Marked(Facing()) with { History = ValueList<BattleState>.Empty };
        var json = ProtocolJson.State(state, Storms);

        Assert.Contains("\"mark\":\"lightning\"", json);
        Assert.Equal(state, ProtocolJson.ReadState(json, Storms));
        Assert.Throws<ProtocolException>(() => ProtocolJson.ReadState(json.Replace("\"mark\":\"lightning\"", "\"mark\":\"thunder\""), Storms));
    }

    [Theory]
    [InlineData("gust", "\"area\": 0")]
    [InlineData("salve", "\"area\": 1")]
    [InlineData("iron_sword", "\"area\": 1")]
    [InlineData("gust", "\"area\": 1, \"rider\": \"stun\"")]
    public void AnAreaOnAHealOrAPhysicalWeaponOrARiderOrWithNoRadiusIsRefusedAtLoad(string id, string extra)
    {
        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(With(id, extra)));

        Assert.Equal(id, error.Entry);
        Assert.Equal("area", error.Field);
    }

    [Fact]
    public void AMarkOnATomeWithNoSchoolIsRefusedAtLoad()
    {
        var files = ContentSerializer.Write(Shipped);
        var text = files.Weapons.Text;
        var at = text.IndexOf("\"id\": \"cinder\"", StringComparison.Ordinal);
        var edited = text[..at] + "\"marks\": true, " + text[at..];
        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(files with { Weapons = new ContentFile(files.Weapons.Name, RemoveSchool(edited, at)) }));

        Assert.Equal("marks", error.Field);
    }

    [Fact]
    public void AnAreaAndAMarkLoadAndRoundTrip()
    {
        var content = ContentLoader.Parse(With("gust", "\"area\": 1, \"marks\": true"));
        var again = ContentLoader.Parse(ContentSerializer.Write(content));

        Assert.Equal(1, again.Weapon("gust").Area);
        Assert.True(again.Weapon("gust").Marks);
        Assert.All(Shipped.Weapons.Values, w => Assert.Equal(0, w.Area));
        Assert.All(Shipped.Weapons.Values, w => Assert.False(w.Marks));
    }

    /// <summary>The shipped content written out with <paramref name="extra"/> added to the weapon <paramref name="id"/>'s entry.</summary>
    private static ContentFiles With(string id, string extra)
    {
        var files = ContentSerializer.Write(Shipped);
        var text = files.Weapons.Text;
        var at = text.IndexOf($"\"id\": \"{id}\"", StringComparison.Ordinal);
        return files with { Weapons = new ContentFile(files.Weapons.Name, text[..at] + extra + ", " + text[at..]) };
    }

    /// <summary><paramref name="text"/> with the <c>school</c> field of the entry beginning near <paramref name="at"/> removed.</summary>
    private static string RemoveSchool(string text, int at)
    {
        var end = text.IndexOf('}', at);
        var entry = System.Text.RegularExpressions.Regex.Replace(text[at..end], ",\\s*\"school\": \"[a-z]+\"", "");
        return text[..at] + entry + text[end..];
    }
}
