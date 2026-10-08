using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Earth's armor (issue 1282, Lotus's #1247 rulings, DECISIONS/0307 and 0312): a tome naming <c>armor</c> on a school
/// whose rider is raise carries its own numbers and is cast through the Item action on its caster alone: Def up in
/// every combat, Mov down, never below 1, through the caster's next phases, falling as the last ends. No shipped tome
/// names it, so these tests use fixture tomes, <c>test_earth_armor</c> (+10 Def, Mov -2, two phases) and
/// <c>test_obsidian_armor</c> (issue 1403, Lotus: a one-hit shell, +20 Def, no Mov cost, three phases, cast on Pell or an
/// adjacent ally, a grimoire gated on Mag), in Pell's hands on the sample <c>the_tollgate_frost.map</c>, her class given earth.
/// </summary>
public class ArmorTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly Weapon Cinder = Shipped.Weapon("cinder");

    private static readonly ArmorSpell EarthArmor = new(10, 2, 2);

    private static readonly ArmorSpell ObsidianArmor = new(20, 0, 3) { Shell = true, Range = 1 };

    private const int Uses = 2;

    private static readonly GameContent Armoring = Shipped with
    {
        Weapons = Shipped.Weapons
            .SetItem("test_earth_armor", Cinder with { Id = "test_earth_armor", Name = "Test Earth Armor", School = MagicSchool.Earth, Rider = RiderKind.Armor, Armor = EarthArmor, Ignites = false })
            .SetItem("test_obsidian_armor", Cinder with { Id = "test_obsidian_armor", Name = "Test Obsidian Armor", School = MagicSchool.Earth, Rider = RiderKind.Armor, Armor = ObsidianArmor, Ignites = false, MinMag = 1 }),
        Classes = Shipped.Classes.SetItem("adept", Shipped.Class("adept") with { Schools = ValueList<MagicSchool>.Of(MagicSchool.Fire, MagicSchool.Ice, MagicSchool.Lightning, MagicSchool.Earth) }),
    };

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_tollgate_frost.map");

    /// <summary>The frost sample with Pell (6,10) holding <paramref name="tome"/> in slot 0.</summary>
    private static BattleState Board(string tome = "test_earth_armor", int uses = Uses)
    {
        var state = BattleState.From(MapFiles.Load(SamplePath, Armoring), Armoring, Armoring.Cast, 1282);
        var pell = state.Find("pell")!;
        return state.WithUnit(pell with { Unit = pell.Unit with { Inventory = pell.Unit.Inventory.Replace(0, new ItemStack(tome, uses)) } });
    }

    private static ApplyResult Don(BattleState state, string? target = null) =>
        Resolver.Apply(state, Armoring, new UseItem("pell", 0, target));

    private static ApplyResult End(BattleState state) => Resolver.Apply(state, Armoring, new EndPhase());

    [Fact]
    public void ArmorIsATomesRiderBorrowedFromEarthsRaiseAndCarriesItsOwnNumbers()
    {
        Assert.Equal(RiderKind.Raise, Shipped.Riders[MagicSchool.Earth].Kind);
        Assert.Equal(RiderKind.Armor, Armoring.RiderOf(Armoring.Weapon("test_earth_armor"))!.Kind);
        Assert.True(Armor.Armors(Armoring, Armoring.Weapon("test_earth_armor")));
        Assert.False(Armor.Armors(Armoring, Cinder));
        Assert.All(Shipped.Weapons.Values, w => Assert.Null(w.Armor));
    }

    [Fact]
    public void AnArmorTomeLoadsWithItsNumbersAndRoundTrips()
    {
        var content = ContentLoader.Parse(ShippedWithWeapons(w => w.Replace("\"school\": \"fire\"", "\"school\": \"earth\", \"rider\": \"armor\", \"armor\": { \"def\": 10, \"mov\": 2, \"phases\": 2 }")));
        var again = ContentLoader.Parse(ContentSerializer.Write(content));

        Assert.Equal(EarthArmor, content.Weapon("cinder").Armor);
        Assert.Equal(EarthArmor, again.Weapon("cinder").Armor);
    }

    [Theory]
    [InlineData("\"school\": \"earth\", \"rider\": \"armor\"", "armor", "is required on a tome whose rider is armor")]
    [InlineData("\"school\": \"fire\", \"armor\": { \"def\": 10, \"mov\": 2, \"phases\": 2 }", "armor", "only a tome whose rider is armor")]
    [InlineData("\"school\": \"earth\", \"rider\": \"armor\", \"armor\": { \"def\": 0, \"mov\": 2, \"phases\": 2 }", "armor.def", "at least 1")]
    [InlineData("\"school\": \"earth\", \"rider\": \"armor\", \"armor\": { \"def\": 10, \"mov\": -1, \"phases\": 2 }", "armor.mov", "at least 0")]
    [InlineData("\"school\": \"earth\", \"rider\": \"armor\", \"armor\": { \"def\": 10, \"mov\": 2, \"phases\": 0 }", "armor.phases", "at least 1")]
    [InlineData("\"school\": \"earth\", \"rider\": \"armor\", \"armor\": { \"def\": 10, \"mov\": 2, \"phases\": 2, \"res\": 3 }", "armor.res", "is not an armor field")]
    public void ABadArmorTomeIsRefusedAtLoadNamingFileEntryAndField(string to, string field, string why)
    {
        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(ShippedWithWeapons(w => w.Replace("\"school\": \"fire\"", to))));

        Assert.Equal(ContentFiles.WeaponsName, error.File);
        Assert.Equal("cinder", error.Entry);
        Assert.Equal(field, error.Field);
        Assert.Contains(why, error.Message);
    }

    [Fact]
    public void ASchoolsOwnRiderIsNeverArmor()
    {
        var rules = """{ "wakeRadius": 4, "schools": { "earth": { "rider": { "kind": "armor" } } } }""";

        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(rules: rules)));

        Assert.Contains("schools.earth", error.Message);
        Assert.Contains("never a school's own rider", error.Message);
    }

    [Fact]
    public void ArmorIsWornByItsCasterAndSpendsAUseAndTheAction()
    {
        var result = Don(Board());

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(
            [new ItemUsed("pell", "test_earth_armor", "pell", Uses - 1), new ArmorDonned("pell", "test_earth_armor", 10, 2, 2)],
            result.Events.Where(e => e is ItemUsed or ArmorDonned).ToList());
        var pell = result.Next.Find("pell")!;
        Assert.True(pell.Moved && pell.Acted);
        Assert.Equal(new ArmorMark("test_earth_armor", 10, 2, 2), pell.Armor);
        Assert.Equal(Uses - 1, pell.Unit.Inventory.Items[0].Uses);
        Assert.Equal(Board().Find("pell")!.Unit.Exp, pell.Unit.Exp);
    }

    [Fact]
    public void ArmorNamingItsCasterIsWornToo()
    {
        Assert.True(Don(Board(), "pell").Accepted);
    }

    [Fact]
    public void ArmorCastOnAnotherUnitIsRefused()
    {
        var result = Don(Board(), "teodor");

        Assert.False(result.Accepted);
        Assert.Contains("worn by its caster alone", result.Rejection!.Message);
    }

    [Fact]
    public void ArmorWithNoUsesLeftIsRefused()
    {
        var result = Don(Board(uses: 0));

        Assert.False(result.Accepted);
        Assert.Contains("no uses left", result.Rejection!.Message);
    }

    [Fact]
    public void ObsidianArmorIsAGrimoireGatedOnMag()
    {
        var mag = Armoring.StatsOf(Board().Find("pell")!.Unit).Mag;
        var gated = Armoring with { Weapons = Armoring.Weapons.SetItem("test_obsidian_armor", Armoring.Weapon("test_obsidian_armor") with { MinMag = mag + 1 }) };

        var refused = Resolver.Apply(Board("test_obsidian_armor"), gated, new UseItem("pell", 0, null));
        var worn = Don(Board("test_obsidian_armor"));

        Assert.False(refused.Accepted);
        Assert.Contains("Mag", refused.Rejection!.Message);
        Assert.Equal(new ArmorMark("test_obsidian_armor", 20, 0, 3) { Shell = true }, worn.Next.Find("pell")!.Armor);
    }

    /// <summary>The issue's numbers: the armor's Def is added to the wearer's in every combat, and nothing else moves.</summary>
    [Theory]
    [InlineData("test_earth_armor", 10)]
    public void ArmorAddsItsDefToTheWearerInCombat(string tome, int def)
    {
        var board = Board(tome);
        var worn = Don(board).Next;

        var before = board.Find("pell")!.ToCombatant(board, Armoring).Stats;
        var after = worn.Find("pell")!.ToCombatant(worn, Armoring).Stats;

        Assert.Equal(before with { Def = before.Def + def }, after);
    }

    [Theory]
    [InlineData(4, 2, 2)]
    [InlineData(5, 2, 3)]
    [InlineData(3, 2, 1)]
    [InlineData(2, 2, 1)]
    [InlineData(1, 2, 1)]
    [InlineData(0, 2, 0)]
    [InlineData(4, 0, 4)]
    public void ArmorTakesItsMovNeverBelowOne(int mov, int cost, int expected)
    {
        var unit = Board().Find("pell")! with { Armor = new ArmorMark("test_earth_armor", 10, cost, 2) };

        Assert.Equal(expected, Armor.Mov(mov, unit));
        Assert.Equal(mov, Armor.Mov(mov, unit with { Armor = null }));
    }

    [Fact]
    public void ArmorsMovCostShowsAsAMoveRefused()
    {
        var board = Board();
        var pell = board.Find("pell")!;
        var armored = board.WithUnit(pell with { Armor = new ArmorMark("test_earth_armor", 10, 2, 2) });

        var free = board.ReachOf(pell, Armoring);
        var heavy = armored.ReachOf(armored.Find("pell")!, Armoring);
        var lost = free.Entries.First(e => e.CanEnd && !heavy.CanEnd(e.At) && board.UnitAt(e.At) is null).At;

        Assert.True(Resolver.Apply(board, Armoring, new Move("pell", lost)).Accepted);
        Assert.False(Resolver.Apply(armored, Armoring, new Move("pell", lost)).Accepted);
        Assert.Equal(Armoring.Class(pell.Unit.ClassId).Mov - 2, heavy.Mov);
    }

    [Fact]
    public void ArmorHoldsThroughTheCastersNextPhasesAndFallsAsTheLastEnds()
    {
        var worn = Don(Board()).Next;

        var enemy1 = End(worn).Next;
        Assert.Equal(2, enemy1.Find("pell")!.Armor!.Phases);
        var player2 = End(enemy1).Next;
        Assert.Equal(1, player2.Find("pell")!.Armor!.Phases);
        var enemy2 = End(player2).Next;
        Assert.NotNull(enemy2.Find("pell")!.Armor);
        var player3 = End(enemy2).Next;
        Assert.Equal(0, player3.Find("pell")!.Armor!.Phases);

        var fallen = End(player3);
        Assert.Contains(new ArmorFell("pell", "test_earth_armor"), fallen.Events);
        Assert.Null(fallen.Next.Find("pell")!.Armor);
    }

    [Fact]
    public void ARecastReplacesTheArmorAndRefreshesItsClock()
    {
        var worn = Don(Board()).Next;
        var player2 = End(End(worn).Next).Next;

        var recast = Don(player2).Next;

        Assert.Equal(new ArmorMark("test_earth_armor", 10, 2, 2), recast.Find("pell")!.Armor);
    }

    [Fact]
    public void RecallTakesTheArmorOff()
    {
        var worn = Don(Board());
        var back = Resolver.Apply(worn.Next, Armoring, new Recall(0));

        Assert.True(back.Accepted, back.Rejection?.Message);
        Assert.Null(back.Next.Find("pell")!.Armor);
        Assert.Equal(Uses, back.Next.Find("pell")!.Unit.Inventory.Items[0].Uses);
    }

    [Fact]
    public void TheCardSaysWhatTheArmorGivesAndWhenItFalls()
    {
        var worn = Don(Board()).Next;
        var pell = worn.Find("pell")!;

        Assert.Equal("armor: Def +10, Mov -2 (Test Earth Armor), falls after 2 more player phases", Armor.CardLine(Armoring, pell));
        Assert.Equal("armor: Def +10, Mov -2 (Test Earth Armor), falls as the next player phase ends", Armor.CardLine(Armoring, pell with { Armor = pell.Armor! with { Phases = 1 } }));
        Assert.Equal("armor: Def +10, Mov -2 (Test Earth Armor), falls as this player phase ends", Armor.CardLine(Armoring, pell with { Armor = pell.Armor! with { Phases = 0 } }));
        Assert.Null(Armor.CardLine(Armoring, Board().Find("pell")!));
    }

    [Fact]
    public void TheEventsPrintAndSerializeAndTheArmorRoundTripsThroughTheProtocolState()
    {
        var worn = Don(Board());
        var names = UnitNames.Of(worn.Next, Armoring);

        Assert.Contains("Pell wears Test Earth Armor: Def +10, Mov -2 through its side's next two phases", worn.Events.Select(e => PlaySession.Describe(e, Armoring, names)));
        Assert.Equal("Pell's Test Earth Armor falls away", PlaySession.Describe(new ArmorFell("pell", "test_earth_armor"), Armoring, names));
        Assert.Equal(
            """{"type":"armorDonned","unit":"pell","item":"test_earth_armor","def":10,"mov":2,"phases":2}""",
            ProtocolJson.Event(new ArmorDonned("pell", "test_earth_armor", 10, 2, 2)));
        Assert.Equal(
            """{"type":"armorFell","unit":"pell","item":"test_earth_armor"}""",
            ProtocolJson.Event(new ArmorFell("pell", "test_earth_armor")));

        var back = ProtocolJson.ReadState(ProtocolJson.State(worn.Next, Armoring), Armoring);
        Assert.Equal(worn.Next.Find("pell")!.Armor, back.Find("pell")!.Armor);
    }

    [Fact]
    public void AShellTomeLoadsWithShellAndRangeAndRoundTrips()
    {
        var content = ContentLoader.Parse(ShippedWithWeapons(w => w.Replace("\"school\": \"fire\"", "\"school\": \"earth\", \"rider\": \"armor\", \"armor\": { \"def\": 20, \"mov\": 0, \"phases\": 3, \"shell\": true, \"range\": 1 }")));
        var again = ContentLoader.Parse(ContentSerializer.Write(content));

        Assert.Equal(ObsidianArmor, content.Weapon("cinder").Armor);
        Assert.Equal(ObsidianArmor, again.Weapon("cinder").Armor);
    }

    [Fact]
    public void AnArmorRangeBeyondOneIsRefusedAtLoad()
    {
        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(ShippedWithWeapons(w => w.Replace("\"school\": \"fire\"", "\"school\": \"earth\", \"rider\": \"armor\", \"armor\": { \"def\": 20, \"mov\": 0, \"phases\": 3, \"range\": 2 }"))));

        Assert.Equal("cinder", error.Entry);
        Assert.Equal("armor.range", error.Field);
        Assert.Contains("must be 0", error.Message);
    }

    private static BattleUnit Captain(BattleState state) => state.Units.Single(u => u.Side == Side.Player && u.IsCaptain);

    [Fact]
    public void AShellIsLaidOnAnAdjacentAllyAndTheCasterSpendsTheAction()
    {
        var board = Board("test_obsidian_armor");
        var captain = Captain(board);
        Assert.Equal(1, board.Find("pell")!.At.DistanceTo(captain.At));

        var result = Don(board, captain.Id);

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Contains(new ArmorDonned("pell", "test_obsidian_armor", 20, 0, 3) { WearerId = captain.Id, Shell = true }, result.Events);
        Assert.Contains(new ItemUsed("pell", "test_obsidian_armor", captain.Id, Uses - 1), result.Events);
        Assert.Equal(new ArmorMark("test_obsidian_armor", 20, 0, 3) { Shell = true }, result.Next.Find(captain.Id)!.Armor);
        Assert.Null(result.Next.Find("pell")!.Armor);
        Assert.True(result.Next.Find("pell")!.Acted);
        Assert.False(result.Next.Find(captain.Id)!.Acted);
    }

    [Fact]
    public void AShellOnAnAllyTwoAwayOrOnAnEnemyIsRefused()
    {
        var board = Board("test_obsidian_armor");
        Assert.Equal(2, board.Find("pell")!.At.DistanceTo(board.Find("wren")!.At));
        var enemy = board.Units.First(u => u.Side == Side.Enemy);
        var beside = board.WithUnit(enemy with { At = new Coord(7, 10) });

        var far = Don(board, "wren");
        var foe = Don(beside, enemy.Id);

        Assert.False(far.Accepted);
        Assert.Contains("an ally within 1", far.Rejection!.Message);
        Assert.False(foe.Accepted);
        Assert.Contains("an ally within 1", foe.Rejection!.Message);
    }

    [Fact]
    public void AShellAddsNothingToTheWearersStatsAndRidesTheCombatantInstead()
    {
        var board = Board("test_obsidian_armor");
        var worn = Don(board).Next;

        var before = board.Find("pell")!.ToCombatant(board, Armoring);
        var after = worn.Find("pell")!.ToCombatant(worn, Armoring);

        Assert.Equal(before.Stats, after.Stats);
        Assert.Equal(0, before.Shell);
        Assert.Equal(20, after.Shell);
        Assert.Equal(20, worn.Find("pell")!.Answering(worn, Armoring, new Coord(6, 9)).Shell);
        Assert.Equal(5, Armor.Mov(5, worn.Find("pell")!));
    }

    /// <summary>The frost sample with Wren at 6,6 beside the woods brigand at 6,5, <paramref name="shelled"/> wearing the shell.</summary>
    private static BattleState Beside(ulong seed, Func<BattleState, BattleUnit> shelled)
    {
        var state = BattleState.From(MapFiles.Load(SamplePath, Armoring), Armoring, Armoring.Cast, seed);
        state = state.WithUnit(state.Find("wren")! with { At = new Coord(6, 6) });
        return state.WithUnit(shelled(state) with { Armor = new ArmorMark("test_obsidian_armor", 20, 0, 3) { Shell = true } });
    }

    private static BattleUnit WoodsBrigand(BattleState state) => state.Units.Single(u => u.Side == Side.Enemy && u.At == new Coord(6, 5));

    /// <summary>The first seed under 400 whose attack by Wren on the woods brigand passes <paramref name="keep"/>.</summary>
    private static (BattleState Before, ApplyResult Result) Seeded(Func<BattleState, BattleUnit> shelled, Func<BattleState, ApplyResult, bool> keep)
    {
        for (ulong seed = 1; seed < 400; seed++)
        {
            var state = Beside(seed, shelled);
            var result = Resolver.Apply(state, Armoring, new Attack("wren", WoodsBrigand(state).Id));
            Assert.True(result.Accepted, result.Rejection?.Message);
            if (keep(state, result))
            {
                return (state, result);
            }
        }

        throw new InvalidOperationException("no seed under 400 gives the combat asked for");
    }

    private static CombatFought Fought(ApplyResult result) => result.Events.OfType<CombatFought>().Single();

    [Fact]
    public void TheFirstHitThatLandsMeetsTheShellAndBreaksItAndTheRestArePlain()
    {
        var (before, result) = Seeded(WoodsBrigand, (s, r) => Fought(r).Strikes.Count(x => x.AttackerId == "wren" && x.Hit) >= 2 && !Fought(r).Strikes.Any(x => x.Crit) && r.Next.Find(WoodsBrigand(s).Id) is not null);
        var brigand = WoodsBrigand(before);
        var forecast = Ironwake.Core.Combat.Forecast(before.Find("wren")!.ToCombatant(before, Armoring, against: brigand), brigand.Answering(before, Armoring, new Coord(6, 6)), 1, before.Scheme).Attacker;
        var hits = Fought(result).Strikes.Where(x => x.AttackerId == "wren" && x.Hit).Select(x => x.Damage).ToList();

        Assert.Equal(20, forecast.Shell);
        Assert.True(forecast.FirstHit(crit: false) < forecast.Damage);
        Assert.Equal([forecast.FirstHit(crit: false), forecast.Damage], hits.Take(2));
        Assert.Contains(new ArmorShattered(brigand.Id, "test_obsidian_armor", "wren"), result.Events);
        Assert.Null(result.Next.Find(brigand.Id)!.Armor);
    }

    [Fact]
    public void AMissLeavesTheShellAndTheWearersOwnHitsNeverBreakIt()
    {
        var (_, result) = Seeded(s => s.Find("wren")!, (s, r) => Fought(r).Strikes.Any(x => x.AttackerId == "wren" && x.Hit) && !Fought(r).Strikes.Any(x => x.TargetId == "wren" && x.Hit) && r.Next.Find("wren") is not null);

        Assert.DoesNotContain(result.Events, e => e is ArmorShattered);
        Assert.NotNull(result.Next.Find("wren")!.Armor);
    }

    [Fact]
    public void ACounterThatLandsBreaksTheAttackersShell()
    {
        var (before, result) = Seeded(s => s.Find("wren")!, (s, r) => Fought(r).Strikes.Any(x => x.TargetId == "wren" && x.Hit) && r.Next.Find("wren") is not null);
        var brigand = WoodsBrigand(before);

        Assert.Contains(new ArmorShattered("wren", "test_obsidian_armor", brigand.Id), result.Events);
        Assert.Null(result.Next.Find("wren")!.Armor);
    }

    [Fact]
    public void TheShellsCardForecastAndEventsSayItBreaksOnTheFirstHit()
    {
        var board = Board("test_obsidian_armor");
        var captain = Captain(board);
        var worn = Don(board, captain.Id);
        var names = UnitNames.Of(worn.Next, Armoring);
        var shelled = worn.Next.Find(captain.Id)!;

        Assert.Equal("shell: Def +20 against the first hit (Test Obsidian Armor), falls after 3 more player phases", Armor.CardLine(Armoring, shelled));
        Assert.Contains($"Pell lays Test Obsidian Armor on {names[captain.Id]}: a shell, Def +20 against the first hit, through its side's next three phases", worn.Events.Select(e => PlaySession.Describe(e, Armoring, names)));
        Assert.Equal($"Wren's hit shatters the Test Obsidian Armor on {names[captain.Id]}", PlaySession.Describe(new ArmorShattered(captain.Id, "test_obsidian_armor", "wren"), Armoring, names));
        Assert.Equal(" meets the shell (Def +20): first hit 0", Armor.ForecastText(new SideForecast(true, 9, 90, 90, 0, false) { Shell = 20, Shelled = 0 }));
        Assert.Equal("", Armor.ForecastText(new SideForecast(true, 9, 90, 90, 0, false)));

        var back = ProtocolJson.ReadState(ProtocolJson.State(worn.Next, Armoring), Armoring);
        Assert.Equal(shelled.Armor, back.Find(captain.Id)!.Armor);
    }

    /// <summary>The shipped content written out, the Adept given earth so Cinder may join the school, with <paramref name="edit"/> applied to weapons.json.</summary>
    private static ContentFiles ShippedWithWeapons(Func<string, string> edit)
    {
        var files = ContentSerializer.Write(Shipped with { Classes = Armoring.Classes });
        return files with { Weapons = new ContentFile(files.Weapons.Name, edit(files.Weapons.Text)) };
    }
}
