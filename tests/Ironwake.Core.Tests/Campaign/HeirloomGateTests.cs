using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Teodor's quest 1 gates the Family Lance (issue 635 slice 7, rounds 266 and 267): the campaign
/// fields him with the lance behind his iron from the start; the counter runs on, but the last
/// stage is held until his quest 1, The Old Watch, is won; a win wakes it at once when the count
/// has passed it, and otherwise lets it wake when the count gets there; the card says the stage is
/// held once the count has passed it.
/// </summary>
public class HeirloomGateTests
{
    private const string LanceId = "family_lance";

    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly HeirloomLadder Ladder = Shipped.Weapon(LanceId).Heirloom!;

    private static readonly int Woken = Ladder.Turns[^1].At;

    private static BattleState Placed() =>
        BattleState.From(MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), Shipped), Shipped, Shipped.Cast, 646);

    private static BattleUnit Teodor(BattleState state) => state.Find("teodor")!;

    private static ItemStack Lance(BattleState state) => Teodor(state).Unit.Inventory.Items[0];

    /// <summary>Teodor with the lance in his first slot at the last stage but one, its counter and gate set, below the brigand at 6,5, on campaign map 6.</summary>
    private static BattleState Armed(int combats, bool open = false)
    {
        var state = Placed();
        var teodor = Teodor(state);
        var stack = new ItemStack(LanceId, 40) { Combats = combats, Stage = Ladder.Turns.Count - 1, GateOpen = open };
        return state.WithUnit(teodor with { At = new Coord(6, 6), Unit = teodor.Unit with { Inventory = teodor.Unit.Inventory.Replace(0, stack) } }) with { CampaignMap = 6 };
    }

    private static ApplyResult Strike(BattleState state) =>
        Resolver.Apply(state, Shipped, new Attack("teodor", state.Units.Single(u => u.At == new Coord(6, 5)).Id));

    /// <summary>A record after map 5 with Teodor's lance stack set, and his quest 1 decided as won with Wren beside him.</summary>
    private static (CampaignRecord Record, BattleState Won) AtTheWatch(int combats, int stage)
    {
        var record = CampaignRecord.StartAt(Shipped, 701, "ironwake_raid");
        var teodor = record.Find("teodor")!;
        var slot = teodor.Inventory.Items.ToList().FindIndex(s => s.ItemId == LanceId);
        teodor = teodor with { Inventory = teodor.Inventory.Replace(slot, new ItemStack(LanceId, 40) { Combats = combats, Stage = stage }) };
        record = record with { Roster = ValueList<Unit>.From(record.Roster.Select(u => u.Id == "teodor" ? teodor : u)) };
        var board = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), MapFiles.QuestsDirectory, "the_old_watch.map"), Shipped);
        var opening = record.BeginQuest(board, "teodor_1", "wren", Shipped);
        var won = opening with { Units = ValueList<BattleUnit>.From(opening.UnitsOf(Side.Player)), History = ValueList<BattleState>.Of(opening) };
        return (record, won);
    }

    [Fact]
    public void TeodorsQuestOneIsTheOldWatchAfterMapFiveAndWakesTheFamilyLance()
    {
        var quest = Shipped.Campaign.Quest("teodor_1")!;
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), MapFiles.QuestsDirectory, "the_old_watch.map"), Shipped);

        Assert.Equal(("teodor", 1, "the_old_watch", "harrow_weir", LanceId), (quest.MemberId, quest.Part, quest.MapId, quest.OpensAfter, quest.Wakes));
        Assert.Null(quest.Pays);
        Assert.Null(CampaignRecord.QuestMapRefusal(map));
        Assert.Equal(WinCondition.DefeatBoss, map.Win);
        Assert.NotNull(Ladder.Held);
    }

    [Fact]
    public void EveryGatedHeirloomShippedHasAQuestThatWakesIt()
    {
        var gated = Shipped.Weapons.Values.Where(w => w.Heirloom is { Held: not null }).Select(w => w.Id).ToList();

        Assert.NotEmpty(gated);
        Assert.All(gated, id => Assert.Contains(Shipped.Campaign.Quests, q => q.Wakes == id));
    }

    [Fact]
    public void TheCampaignFieldsTeodorWithTheLanceBehindHisIronAndTheCastFileStaysAsItIs()
    {
        var teodor = CampaignRecord.Start(Shipped, 1).Find("teodor")!;

        Assert.Equal(new[] { "iron_lance", "field_dressing", LanceId }, teodor.Inventory.Items.Select(s => s.ItemId));
        Assert.Equal(new ItemStack(LanceId, Shipped.Weapon(LanceId).Durability), teodor.Inventory.Items[^1]);
        Assert.DoesNotContain(Shipped.Unit("teodor").Inventory.Items, s => s.ItemId == LanceId);
    }

    [Fact]
    public void KittedNeverIssuesTheHeirloomTwice()
    {
        var once = CampaignRecord.Kitted(Shipped.Unit("teodor"), Shipped);

        Assert.Equal(once, CampaignRecord.Kitted(once, Shipped));
    }

    [Fact]
    public void AHeldLanceAtTheWokenCountStaysAtTheStageBeforeWhileTheCounterRunsOn()
    {
        var result = Strike(Armed(Woken - 1));

        Assert.True(result.Accepted);
        Assert.Equal((Woken, Ladder.Turns.Count - 1), (Lance(result.Next).Combats, Lance(result.Next).Stage));
        Assert.DoesNotContain(result.Events, e => e is HeirloomTurned);
    }

    [Fact]
    public void AnOpenGateLetsTheLastStageTurnAtItsCount()
    {
        var result = Strike(Armed(Woken - 1, open: true));

        Assert.Equal(Ladder.Turns.Count, Lance(result.Next).Stage);
        Assert.Contains(new HeirloomTurned("teodor", LanceId, Ladder.Turns.Count, "woken"), result.Events);
    }

    [Fact]
    public void TheGateHoldsOnlyTheLastStage()
    {
        Assert.False(Heirloom.Held(Ladder, new ItemStack(LanceId, 40), Ladder.Turns.Count - 1));
        Assert.True(Heirloom.Held(Ladder, new ItemStack(LanceId, 40), Ladder.Turns.Count));
        Assert.False(Heirloom.Held(Ladder, new ItemStack(LanceId, 40) { GateOpen = true }, Ladder.Turns.Count));
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(3, true)]
    public void TheCardSaysTheRustHoldsOnlyOnceTheCountHasPassedTheHeldStage(int past, bool held)
    {
        var card = Heirloom.Card(Teodor(Armed(Woken + past)), Shipped)!;

        Assert.StartsWith("Family Lance, sound: ", card);
        Assert.Equal(held, card.EndsWith(" " + Ladder.Held, StringComparison.Ordinal));
    }

    [Fact]
    public void TheCardSaysNothingOfTheGateOnceItIsOpen()
    {
        var card = Heirloom.Card(Teodor(Armed(Woken + 2, open: true)), Shipped)!;

        Assert.DoesNotContain(Ladder.Held!, card);
    }

    [Theory]
    [InlineData(10, 2, 3)]
    [InlineData(9, 2, 2)]
    [InlineData(12, 1, 1)]
    [InlineData(0, 0, 0)]
    public void OpeningTheGateWakesTheLanceAtOnceOnlyWhenTheCountHasPassedTheStageBefore(int combats, int stage, int after)
    {
        var opened = Heirloom.OpenGate(Shipped.Weapon(LanceId), new ItemStack(LanceId, 40) { Combats = combats, Stage = stage });

        Assert.True(opened.GateOpen);
        Assert.Equal(after, opened.Stage);
    }

    [Fact]
    public void AWonQuestOneWakesALanceAlreadyPastItsCount()
    {
        var (record, won) = AtTheWatch(Woken + 1, Ladder.Turns.Count - 1);

        var after = record.AfterQuest(won, "teodor_1", Shipped);

        Assert.Equal("teodor wins teodor_1; Family Lance wakes in Teodor's hands; nobody fell", after.Text);
        var lance = after.Record.Find("teodor")!.Inventory.Items.Single(s => s.ItemId == LanceId);
        Assert.Equal((true, Ladder.Turns.Count), (lance.GateOpen, lance.Stage));
    }

    [Fact]
    public void AWonQuestOneBelowTheCountOpensTheGateAndLeavesTheStage()
    {
        var (record, won) = AtTheWatch(4, 1);

        var after = record.AfterQuest(won, "teodor_1", Shipped);

        Assert.Equal("teodor wins teodor_1; Family Lance will wake in Teodor's hands; nobody fell", after.Text);
        var lance = after.Record.Find("teodor")!.Inventory.Items.Single(s => s.ItemId == LanceId);
        Assert.Equal((true, 1), (lance.GateOpen, lance.Stage));
    }

    [Fact]
    public void ALostQuestOneLeavesTheGateShut()
    {
        var (record, won) = AtTheWatch(Woken + 1, Ladder.Turns.Count - 1);
        var lost = won with { Units = ValueList<BattleUnit>.From(won.History[0].Units), Turn = won.Map.TurnLimit + 1 };

        var after = record.AfterQuest(lost, "teodor_1", Shipped);

        Assert.False(after.Record.Find("teodor")!.Inventory.Items.Single(s => s.ItemId == LanceId).GateOpen);
    }

    [Fact]
    public void TheProtocolCarriesTheOpenGate()
    {
        var state = Armed(Woken, open: true);

        var back = ProtocolJson.ReadState(ProtocolJson.State(state, Shipped), Shipped);

        Assert.True(Lance(back).GateOpen);
        Assert.Contains("\"gateOpen\":true", ProtocolJson.State(state, Shipped));
    }

    [Fact]
    public void TheSerializerKeepsTheHeldLineAndTheWaking()
    {
        var back = ContentLoader.Parse(ContentSerializer.Write(Shipped));

        Assert.Equal(Ladder.Held, back.Weapon(LanceId).Heirloom!.Held);
        Assert.Equal(LanceId, back.Campaign.Quest("teodor_1")!.Wakes);
    }

    [Theory]
    [InlineData(2, "iron_lance", "only a quest 1 wakes an heirloom")]
    [InlineData(1, "iron_lance", "must be a gated heirloom (heirloom.held) bound to 'teodor'")]
    public void AWakingOnAQuestTwoOrOnAnUngatedWeaponIsRefused(int part, string item, string problem)
    {
        var quests = part == 2
            ? ValueList<CampaignQuest>.Of(new CampaignQuest("teodor_1", "teodor", 1, "m"), new CampaignQuest("teodor_2", "teodor", 2, "n") { Wakes = LanceId })
            : ValueList<CampaignQuest>.Of(new CampaignQuest("teodor_1", "teodor", 1, "m") { Wakes = item });
        var content = Shipped with { Campaign = Shipped.Campaign with { Quests = quests } };

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(ContentSerializer.Write(content)));

        Assert.Equal("wakes", e.Field);
        Assert.Contains(problem, e.Message);
    }
}
