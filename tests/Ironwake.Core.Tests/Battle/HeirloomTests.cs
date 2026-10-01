using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The cursed heirloom (issue 646): a hidden counter of the combats its carrier strikes in with it
/// equipped; a stage turns at each threshold, one a combat, never before the ladder's first
/// campaign map; the current stage's numbers are what the forecast reads; it never breaks; the
/// smith refuses it at its first stage; and Recall rewinds it with the board. Played on the
/// Tollgate with Teodor carrying the shipped Family Lance.
/// </summary>
public class HeirloomTests
{
    private const string LanceId = "family_lance";

    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly HeirloomLadder Ladder = Shipped.Weapon(LanceId).Heirloom!;

    private static BattleState Placed(ulong seed = 646) =>
        BattleState.From(MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), Shipped), Shipped, Shipped.Cast, seed);

    private static BattleUnit Teodor(BattleState state) => state.Find("teodor")!;

    private static ItemStack Lance(BattleState state) => Teodor(state).Unit.Inventory.Items[0];

    /// <summary>Teodor with the lance in place of his first slot, its counter and stage set, standing below the brigand at 6,5.</summary>
    private static BattleState Armed(BattleState state, int combats = 0, int stage = 0, int uses = 40, int? campaignMap = null)
    {
        var teodor = Teodor(state);
        var stack = new ItemStack(LanceId, uses) { Combats = combats, Stage = stage };
        return state.WithUnit(teodor with { At = new Coord(6, 6), Unit = teodor.Unit with { Inventory = teodor.Unit.Inventory.Replace(0, stack) } }) with { CampaignMap = campaignMap };
    }

    [Fact]
    public void TheShippedLanceIsBoundToTeodorWithThreeTurnsFromMapFive()
    {
        var lance = Shipped.Weapon(LanceId);

        Assert.Equal("teodor", lance.BoundTo);
        Assert.Null(lance.Price);
        Assert.Equal(5, Ladder.FromMap);
        Assert.Equal("rusted", Ladder.First);
        Assert.Equal(new[] { "pitted", "sound", "woken" }, Ladder.Turns.Select(t => t.Id));
        Assert.True(Ladder.Turns.Zip(Ladder.Turns.Skip(1)).All(p => p.First.At < p.Second.At));
    }

    [Fact]
    public void RustedStartsBelowTheIronLanceAndWokenPassesTheSignatureCeiling()
    {
        var lance = Shipped.Weapon(LanceId);
        var iron = Shipped.Weapon("iron_lance");

        Assert.True(lance.Mt < iron.Mt && lance.Hit < iron.Hit);
        var reading = SignatureCeiling.Read(Shipped, lance, RollScheme.TwoRollAverage);
        Assert.True(reading.Passed, reading.Failure);
        Assert.Equal(Ladder.Turns[^1].Mt, reading.Item.Mt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TheEquippedWeaponCarriesTheCurrentStagesNumbersAndLine(int stage)
    {
        var weapon = Teodor(Armed(Placed(), stage: stage)).EquippedWeapon(Shipped)!;
        var base_ = Shipped.Weapon(LanceId);
        var expected = stage == 0 ? (base_.Mt, base_.Hit, base_.Crit, base_.Wt, base_.Description)
            : (Ladder.Turns[stage - 1].Mt, Ladder.Turns[stage - 1].Hit, Ladder.Turns[stage - 1].Crit, Ladder.Turns[stage - 1].Wt, Ladder.Turns[stage - 1].Description);

        Assert.Equal(expected, (weapon.Mt, weapon.Hit, weapon.Crit, weapon.Wt, weapon.Description));
    }

    [Fact]
    public void TheForecastPrintsTheCurrentStagesNumbers()
    {
        var rusted = Armed(Placed());
        var woken = Armed(Placed(), stage: 3);
        var target = rusted.Units.Single(u => u.At == new Coord(6, 5));

        var low = Queries.Forecast(rusted, Shipped, Teodor(rusted), target)!;
        var high = Queries.Forecast(woken, Shipped, Teodor(woken), target)!;

        Assert.True(high.Attacker.Damage > low.Attacker.Damage);
        Assert.True(high.Attacker.HitChance > low.Attacker.HitChance);
    }

    [Fact]
    public void ACombatTeodorStrikesInCountsOne()
    {
        var state = Armed(Placed());

        var result = Resolver.Apply(state, Shipped, new Attack("teodor", state.Units.Single(u => u.At == new Coord(6, 5)).Id));

        Assert.True(result.Accepted);
        Assert.Equal(1, Lance(result.Next).Combats);
        Assert.Equal(0, Lance(result.Next).Stage);
        Assert.DoesNotContain(result.Events, e => e is HeirloomTurned);
    }

    [Fact]
    public void ACombatWithNoStrikeFromTheCarrierCountsNothing()
    {
        var state = Armed(Placed());
        var events = new List<GameEvent>();

        var after = Heirloom.AfterCombat(Teodor(state), state, Shipped, ValueList<StrikeEvent>.Empty, events);

        Assert.Equal(0, after.Unit.Inventory.Items[0].Combats);
        Assert.Empty(events);
    }

    [Fact]
    public void TheCombatThatReachesTheThresholdTurnsTheNextStage()
    {
        var state = Armed(Placed(), combats: Ladder.Turns[0].At - 1);

        var result = Resolver.Apply(state, Shipped, new Attack("teodor", state.Units.Single(u => u.At == new Coord(6, 5)).Id));

        Assert.Equal(1, Lance(result.Next).Stage);
        Assert.Contains(new HeirloomTurned("teodor", LanceId, 1, "pitted"), result.Events);
    }

    [Fact]
    public void ACombatBelowTheThresholdTurnsNothing()
    {
        var state = Armed(Placed(), combats: Ladder.Turns[0].At - 2);

        var result = Resolver.Apply(state, Shipped, new Attack("teodor", state.Units.Single(u => u.At == new Coord(6, 5)).Id));

        Assert.Equal(0, Lance(result.Next).Stage);
    }

    [Fact]
    public void OneStageTurnsPerCombatEvenWhenTheCountIsPastTwoThresholds()
    {
        var state = Armed(Placed(), combats: Ladder.Turns[1].At + 5);

        var result = Resolver.Apply(state, Shipped, new Attack("teodor", state.Units.Single(u => u.At == new Coord(6, 5)).Id));

        Assert.Equal(1, Lance(result.Next).Stage);
        Assert.Single(result.Events.OfType<HeirloomTurned>());
    }

    [Theory]
    [InlineData(4, 0)]
    [InlineData(5, 1)]
    [InlineData(6, 1)]
    public void NoStageTurnsBeforeTheLaddersFirstCampaignMap(int campaignMap, int stage)
    {
        var state = Armed(Placed(), combats: Ladder.Turns[0].At + 3, campaignMap: campaignMap);

        var result = Resolver.Apply(state, Shipped, new Attack("teodor", state.Units.Single(u => u.At == new Coord(6, 5)).Id));

        Assert.Equal(stage, Lance(result.Next).Stage);
        Assert.Equal(Ladder.Turns[0].At + 4, Lance(result.Next).Combats);
    }

    [Fact]
    public void TheCampaignTellsTheBattleWhichMapItIs()
    {
        var content = Shipped;
        var record = CampaignRecord.StartAt(content, 7, "harrow_weir");
        var map = MapFiles.Load(MapFiles.CampaignPath(Fixture.RealContentDirectory(), content, "harrow_weir"), content);

        Assert.Equal(5, record.Begin(map, content).CampaignMap);
        Assert.Null(Placed().CampaignMap);
    }

    [Fact]
    public void AStrikeNeverSpendsTheLanceBelowOne()
    {
        var state = Armed(Placed(), uses: 1);

        var result = Resolver.Apply(state, Shipped, new Attack("teodor", state.Units.Single(u => u.At == new Coord(6, 5)).Id));

        Assert.Equal(1, Lance(result.Next).Uses);
        Assert.DoesNotContain(result.Events, e => e is WeaponBroke);
    }

    [Fact]
    public void RecallRewindsTheCounterAndTheStage()
    {
        var state = Armed(Placed(), combats: Ladder.Turns[0].At - 1);
        var struck = Resolver.Apply(state, Shipped, new Attack("teodor", state.Units.Single(u => u.At == new Coord(6, 5)).Id)).Next;
        Assert.Equal(1, Lance(struck).Stage);

        var back = Resolver.Apply(struck, Shipped, new Recall(0)).Next;

        Assert.Equal(Lance(state), Lance(back));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(3, false)]
    public void TheSmithRefusesTheLanceOnlyAtItsFirstStage(int stage, bool refused)
    {
        Assert.Equal(refused, Heirloom.SmithRefuses(Shipped.Weapon(LanceId), new ItemStack(LanceId, 40) { Stage = stage }));
        Assert.False(Heirloom.SmithRefuses(Shipped.Weapon("iron_lance"), new ItemStack("iron_lance", 40)));
        Assert.Equal("Nothing here to work with. It's all rust.", Heirloom.SmithRefusal);
    }

    [Fact]
    public void TheCardNamesTheStageAndNeverTheCounter()
    {
        var card = Heirloom.Card(Teodor(Armed(Placed(), combats: 13, stage: 1)), Shipped)!;

        Assert.StartsWith("Family Lance, pitted: ", card);
        Assert.DoesNotContain("13", card);
        Assert.Null(Heirloom.Card(Placed().Find("wren")!, Shipped));
    }

    [Fact]
    public void TheTurnPrintsTheStageAndItsLine()
    {
        var line = PlaySession.Describe(new HeirloomTurned("teodor", LanceId, 2, "sound"), Shipped, UnitNames.Of(Placed(), Shipped));

        Assert.Equal($"Family Lance turns in Teodor's hands: sound. {Ladder.Turns[1].Description}", line);
    }

    [Fact]
    public void TheProtocolCarriesTheCounterTheStageAndTheCampaignMap()
    {
        var state = Armed(Placed(), combats: 9, stage: 1, campaignMap: 5);

        var back = ProtocolJson.ReadState(ProtocolJson.State(state, Shipped), Shipped);

        Assert.Equal(Lance(state), Lance(back));
        Assert.Equal(5, back.CampaignMap);
    }

    [Theory]
    [InlineData("\"boundTo\": \"recruit\", \"price\": 100, ", "heirloom")]
    [InlineData("", "heirloom")]
    public void AnHeirloomWithAPriceOrNoOwnerIsRefused(string extra, string field)
    {
        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(weapons: WithLance(extra, 3, 6))));

        Assert.Contains(field, error.Message);
    }

    [Fact]
    public void AnHeirloomWhoseThresholdsDoNotRiseIsRefused()
    {
        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(weapons: WithLance("\"boundTo\": \"recruit\", ", 6, 6))));

        Assert.Contains("stages[1]", error.Message);
        Assert.Contains("above the stage before it", error.Message);
    }

    private static string WithLance(string extra, int firstAt, int secondAt)
    {
        var line = """{ "id": "old_lance", "name": "Old Lance", "type": "sword", "mt": 4, "hit": 60, "crit": 0, "wt": 9, "minRange": 1, "maxRange": 1, "durability": 40, "rank": "E", EXTRA"description": "A test line.", "heirloom": { "fromMap": 5, "first": "rusted", "stages": [ { "id": "pitted", "at": FIRST, "mt": 6, "hit": 70, "crit": 0, "wt": 8, "description": "A test line." }, { "id": "sound", "at": SECOND, "mt": 8, "hit": 80, "crit": 0, "wt": 7, "description": "A test line." } ] } }"""
            .Replace("EXTRA", extra).Replace("FIRST", firstAt.ToString(System.Globalization.CultureInfo.InvariantCulture)).Replace("SECOND", secondAt.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Fixture.Weapons.Replace("\n] }", ",\n" + line + "\n] }");
    }
}
