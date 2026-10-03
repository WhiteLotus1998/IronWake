using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Round 266 (issue 635 slice 8): Teodor carries the Family Lance from arrival, the iron lance
/// first; in a campaign its ladder stops at sound until his quest 1, The Barrow Field, is won, the
/// counter still counting; a win wakes it on the after card when the count is already past the
/// woken threshold; the card prints the hold once only the quest keeps it from turning.
/// </summary>
public class HeirloomHoldTests
{
    private const string LanceId = "family_lance";

    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly HeirloomLadder Ladder = Shipped.Weapon(LanceId).Heirloom!;

    private static readonly string[] Held = { LanceId };

    private static BattleState Placed() =>
        BattleState.From(MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), Shipped), Shipped, Shipped.Cast, 266);

    private static ItemStack Lance(BattleState state) => state.Find("teodor")!.Unit.Inventory.Items.First(s => s.ItemId == LanceId);

    /// <summary>Teodor with the lance in front of his pack, its counter and stage set, standing below the brigand at 6,5, on campaign map 6, the hold applying when <paramref name="held"/>.</summary>
    private static BattleState Armed(int combats, int stage, bool held)
    {
        var state = Placed();
        var teodor = state.Find("teodor")!;
        var stack = new ItemStack(LanceId, 40) { Combats = combats, Stage = stage };
        return state.WithUnit(teodor with { At = new Coord(6, 6), Unit = teodor.Unit with { Inventory = teodor.Unit.Inventory.Replace(0, stack) } })
            with { CampaignMap = 6, Held = held ? ValueList<string>.From(Held) : ValueList<string>.Empty };
    }

    private static ApplyResult StrikeTheBrigand(BattleState state) =>
        Resolver.Apply(state, Shipped, new Attack("teodor", state.Units.Single(u => u.At == new Coord(6, 5)).Id));

    private static MapDefinition Barrow() =>
        MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), MapFiles.QuestsDirectory, "the_barrow_field.map"), Shipped);

    /// <summary>The camp after Harrow Weir with Teodor's lance at <paramref name="combats"/> and sound.</summary>
    private static CampaignRecord AtTheRaid(int combats)
    {
        var record = CampaignRecord.StartAt(Shipped, 266, "ironwake_raid");
        var roster = record.Roster.Select(u => u.Id != "teodor" ? u : u with
        {
            Inventory = new Inventory(ValueList<ItemStack>.From(u.Inventory.Items.Select(s => s.ItemId == LanceId ? s with { Combats = combats, Stage = 2 } : s))),
        });
        return record with { Roster = ValueList<Unit>.From(roster) };
    }

    /// <summary>The Barrow Field's opening with the dig group gone, decided as a win.</summary>
    private static BattleState Won(CampaignRecord record)
    {
        var opening = record.BeginQuest(Barrow(), "teodor_1", "wren", Shipped);
        var won = opening with { Units = ValueList<BattleUnit>.From(opening.UnitsOf(Side.Player)), History = ValueList<BattleState>.Of(opening) };
        Assert.Equal(BattleResult.Won, won.Outcome.Result);
        return won;
    }

    [Fact]
    public void TheShippedLanceHoldsAtSoundUntilTeodorsFirstQuest()
    {
        var quest = Shipped.Campaign.Quest("teodor_1")!;

        Assert.Equal("sound", Ladder.Turns[Ladder.HoldsAt!.Value - 1].Id);
        Assert.Equal(("teodor", 1, "the_barrow_field", LanceId, "harrow_weir", (string?)null), (quest.MemberId, quest.Part, quest.MapId, quest.Wakes, quest.OpensAfter, quest.Pays));
        Assert.Single(Shipped.Campaign.Quests, q => q.Wakes == LanceId);
    }

    [Fact]
    public void TeodorCarriesTheLanceFromArrivalBehindTheIron()
    {
        Assert.Equal(new[] { "iron_lance", "field_dressing", LanceId }, Shipped.Unit("teodor").Inventory.Items.Select(s => s.ItemId));
        Assert.Equal("iron_lance", Placed().Find("teodor")!.EquippedWeapon(Shipped)!.Id);
    }

    [Fact]
    public void AHeldLadderStopsAtItsHoldAndTheCounterStillCounts()
    {
        var result = StrikeTheBrigand(Armed(Ladder.Turns[^1].At + 2, Ladder.HoldsAt!.Value, held: true));

        Assert.Equal((Ladder.HoldsAt!.Value, Ladder.Turns[^1].At + 3), (Lance(result.Next).Stage, Lance(result.Next).Combats));
        Assert.Empty(result.Events.OfType<HeirloomTurned>());
    }

    [Fact]
    public void WithoutTheHoldTheSameCombatWakesIt()
    {
        var result = StrikeTheBrigand(Armed(Ladder.Turns[^1].At + 2, Ladder.HoldsAt!.Value, held: false));

        Assert.Equal(Ladder.Turns.Count, Lance(result.Next).Stage);
        Assert.Contains(new HeirloomTurned("teodor", LanceId, Ladder.Turns.Count, "woken"), result.Events);
    }

    [Fact]
    public void TheHoldStopsNoStageBeforeItsOwn()
    {
        var result = StrikeTheBrigand(Armed(Ladder.Turns[1].At, 1, held: true));

        Assert.Equal(2, Lance(result.Next).Stage);
    }

    [Theory]
    [InlineData(10, true, true)]
    [InlineData(9, true, false)]
    [InlineData(10, false, false)]
    public void TheCardPrintsTheHoldOnlyOnceTheCountHasPassedIt(int combats, bool held, bool printed)
    {
        var state = Armed(combats, 2, held);

        var card = Heirloom.Card(state.Find("teodor")!, Shipped, state.Held)!;

        Assert.StartsWith("Family Lance, sound: ", card);
        Assert.Equal(printed, card.EndsWith(" (the rust holds; it waits on Teodor)", StringComparison.Ordinal));
        Assert.DoesNotContain(combats.ToString(System.Globalization.CultureInfo.InvariantCulture), card);
    }

    [Fact]
    public void TheCampaignHoldsTheLanceOnEveryBoardUntilTheQuestIsWon()
    {
        var record = AtTheRaid(0);
        var raid = MapFiles.Load(MapFiles.CampaignPath(Fixture.RealContentDirectory(), Shipped, "ironwake_raid"), Shipped);
        var won = record with { QuestsWon = ValueList<QuestWon>.Of(new QuestWon("teodor_1", 5)) };

        Assert.Equal(Held, record.Held(Shipped));
        Assert.Equal(Held, record.Begin(raid, Shipped).Held);
        Assert.Equal(Held, record.BeginQuest(Barrow(), "teodor_1", "wren", Shipped).Held);
        Assert.Empty(won.Held(Shipped));
        Assert.Empty(won.Begin(raid, Shipped).Held);
        Assert.Empty(Placed().Held);
    }

    [Fact]
    public void TheQuestBoardCountsTheLance()
    {
        var opening = AtTheRaid(9).BeginQuest(Barrow(), "teodor_1", "wren", Shipped);
        var teodor = opening.Find("teodor")!;
        var jory = opening.Units.Single(u => u.At == new Coord(9, 4));
        var state = opening.WithUnit(teodor with { At = new Coord(8, 4) });

        var result = Resolver.Apply(state, Shipped, new Attack("teodor", jory.Id, Slot: 2));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal((2, 10), (Lance(result.Next).Stage, Lance(result.Next).Combats));
    }

    [Fact]
    public void AWonQuestWakesALanceAlreadyPastItsCount()
    {
        var record = AtTheRaid(12);

        var after = record.AfterQuest(Won(record), "teodor_1", Shipped);

        Assert.Contains("; Family Lance wakes in Teodor's hands;", after.Text);
        Assert.Equal(Ladder.Turns.Count, after.Record.Find("teodor")!.Inventory.Items.First(s => s.ItemId == LanceId).Stage);
        Assert.Empty(after.Record.Held(Shipped));
    }

    [Fact]
    public void AWonQuestBelowTheCountLeavesTheLanceToTurnOnALaterCombat()
    {
        var record = AtTheRaid(7);

        var after = record.AfterQuest(Won(record), "teodor_1", Shipped);

        Assert.DoesNotContain("wakes", after.Text);
        Assert.Equal(2, after.Record.Find("teodor")!.Inventory.Items.First(s => s.ItemId == LanceId).Stage);
        Assert.Empty(after.Record.Held(Shipped));
    }

    [Fact]
    public void ALostQuestKeepsTheHold()
    {
        var record = AtTheRaid(12);
        var opening = record.BeginQuest(Barrow(), "teodor_1", "wren", Shipped);
        var lost = opening with { Turn = opening.Map.TurnLimit + 1, History = ValueList<BattleState>.Of(opening) };
        Assert.Equal(BattleResult.Lost, lost.Outcome.Result);

        var after = record.AfterQuest(lost, "teodor_1", Shipped);

        Assert.Equal(2, after.Record.Find("teodor")!.Inventory.Items.First(s => s.ItemId == LanceId).Stage);
        Assert.Equal(Held, after.Record.Held(Shipped));
    }

    [Fact]
    public void TheProtocolCarriesTheHold()
    {
        var state = Armed(10, 2, held: true);

        Assert.Equal(Held, ProtocolJson.ReadState(ProtocolJson.State(state, Shipped), Shipped).Held);
        Assert.Empty(ProtocolJson.ReadState(ProtocolJson.State(Placed(), Shipped), Shipped).Held);
    }

    [Fact]
    public void TheHoldAndTheWakingRoundTripThroughTheSerializer()
    {
        var reloaded = ContentLoader.Parse(ContentSerializer.Write(Shipped));

        Assert.Equal(Ladder.HoldsAt, reloaded.Weapon(LanceId).Heirloom!.HoldsAt);
        Assert.Equal(LanceId, reloaded.Campaign.Quest("teodor_1")!.Wakes);
    }

    [Theory]
    [InlineData("woken", "must name a stage before the last")]
    [InlineData("gleaming", "must name a stage before the last")]
    public void ALadderThatHoldsAtItsLastStageOrNoStageIsRefused(string holdsAt, string problem)
    {
        var files = ContentSerializer.Write(Shipped);
        var weapons = System.Text.RegularExpressions.Regex.Replace(files.Weapons.Text, "\"holdsAt\":\\s*\"sound\"", $"\"holdsAt\": \"{holdsAt}\"");
        Assert.NotEqual(files.Weapons.Text, weapons);

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(files with { Weapons = files.Weapons with { Text = weapons } }));

        Assert.Equal((ContentFiles.WeaponsName, LanceId, "heirloom.holdsAt"), (e.File, e.Entry, e.Field));
        Assert.Contains(problem, e.Message);
    }

    [Theory]
    [InlineData("iron_lance", "'iron_lance' must be an heirloom bound to 'teodor' whose ladder holds")]
    [InlineData("twice", "'family_lance' is woken by another quest already")]
    public void AQuestThatWakesWhatItCannotIsRefused(string change, string problem)
    {
        var quests = change == "twice"
            ? Shipped.Campaign.Quests.Add(new CampaignQuest("teodor_2", "teodor", 2, "the_barrow_field") { Wakes = LanceId })
            : ValueList<CampaignQuest>.From(Shipped.Campaign.Quests.Select(q => q.Id == "teodor_1" ? q with { Wakes = change } : q));
        var content = Shipped with { Campaign = Shipped.Campaign with { Quests = quests } };

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(ContentSerializer.Write(content)));

        Assert.Equal((ContentFiles.CampaignName, change == "twice" ? "teodor_2" : "teodor_1", "wakes"), (e.File, e.Entry, e.Field));
        Assert.Contains(problem, e.Message);
    }

    [Fact]
    public void TheBarrowFieldIsADefeatBossSideMapWithJoryBoundToTheCaptain()
    {
        var map = Barrow();
        var path = Path.Combine(Fixture.RealContentDirectory(), MapFiles.QuestsDirectory, "the_barrow_field.map");

        Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n"), MapFormat.Write(MapFormat.Parse(path, File.ReadAllText(path).ReplaceLineEndings("\n"), Shipped), Shipped));
        Assert.Null(CampaignRecord.QuestMapRefusal(map));
        Assert.Equal((WinCondition.DefeatBoss, 8), (map.Win, map.TurnLimit));
        Assert.Equal(new FreedBond(new Coord(9, 4), "dig"), map.Bond);
    }
}
