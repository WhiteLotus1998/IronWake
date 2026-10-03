using Ironwake.Content;
using Ironwake.Content.Protocol;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The ending a won campaign leaves for a sequel (issue 807, docs/FUTURE.md): a versioned block on
/// the save, derived from the finished record, <c>ending: pending</c> until the endings are built (#634).
/// </summary>
public class CampaignEndingTests
{
    private static GameContent Content => ContentLoader.Load(Fixture.RealContentDirectory());

    private static string TempDir() => Path.Combine(Path.GetTempPath(), "ironwake-ending-" + Guid.NewGuid().ToString("N"));

    /// <summary>A record past its last map, picked at the branch, with an origin and a chosen pronoun.</summary>
    private static CampaignRecord Finished(GameContent content, string pick) =>
        CampaignRecord.StartAt(content, 41, "ironwake_keep", origin: "kestrow", captain: Pronoun.She, pick: pick)
            with { MapIndex = content.Campaign.Maps.Count };

    [Fact]
    public void AnEndingIsWrittenOnlyWhenTheCampaignEnds()
    {
        var content = Content;
        var marching = CampaignRecord.Start(content, 41);

        Assert.Throws<InvalidOperationException>(() => CampaignEnding.Of(marching, content));
        Assert.Null(ProtocolJson.ReadEnding(ProtocolJson.Campaign(marching)));
    }

    [Fact]
    public void TheEndingRecordsTheRunsKeyChoices()
    {
        var content = Content;
        var record = Finished(content, "keziah") with
        {
            Returned = ClaimantFate.Spared,
            FreedUnitFell = true,
            Rooms = ValueList<string>.From(new[] { "bunk", "forge" }),
        };
        var fallen = record.Roster[^1].Id;
        record = record with
        {
            Roster = ValueList<Unit>.From(record.Roster.Where(u => u.Id != fallen)),
            Fallen = record.Fallen.Add(fallen),
        };

        var ending = CampaignEnding.Of(record, content);

        Assert.Equal(CampaignEnding.CurrentVersion, ending.Version);
        Assert.Equal(CampaignEnding.Pending, ending.Ending);
        Assert.Equal(record.Seed, ending.Seed);
        Assert.Equal("kestrow", ending.CaptainOrigin);
        Assert.Equal(Pronoun.She, ending.CaptainPronoun);
        Assert.Equal(record.Roster[0].ClassId, ending.CaptainClass);
        Assert.Equal("keziah", ending.Pick);
        Assert.Equal("rook", ending.Passed);
        Assert.Equal(ClaimantFate.Spared, ending.PassedFate);
        Assert.Equal(record.Roster.Select(u => u.Id), ending.Lived);
        Assert.Contains(fallen, ending.Fallen);
        Assert.DoesNotContain(fallen, ending.Lived);
        Assert.True(ending.FreedUnitFell);
        Assert.Equal(new[] { "bunk", "forge" }, ending.Rooms);
        Assert.Null(ending.Drake);
    }

    [Fact]
    public void TheEndingCarriesKinsbanesFedCountAndTeethWhenAMemberCarriesIt()
    {
        var content = Content;
        var record = Finished(content, "keziah");
        var keziah = record.Find("keziah")!;
        var slot = keziah.Inventory.Items.ToList().FindIndex(s => s.ItemId == Kinsbane.ItemId);
        var fed = keziah.Inventory.Items[slot] with { Fed = 9 };
        var items = keziah.Inventory.Items.Select((s, i) => i == slot ? fed : s);
        record = record with { Roster = ValueList<Unit>.From(record.Roster.Select(u => u.Id == "keziah" ? u with { Inventory = new Inventory(ValueList<ItemStack>.From(items)) } : u)) };

        var kinsbane = CampaignEnding.Of(record, content).Kinsbane;

        Assert.Equal(new EndingKinsbane("keziah", 9, 4, false), kinsbane);
        Assert.Null(CampaignEnding.Of(Finished(content, "rook"), content).Kinsbane);
    }

    [Fact]
    public void TheEndingCarriesTheDrakesStageAndWhetherItsRiderLived()
    {
        var content = Content;
        var record = Finished(content, "rook");

        Assert.Equal(new EndingDrake(record.Find("rook")!.Drake!.Stage, true), CampaignEnding.Of(record, content).Drake);

        var flown = record with
        {
            Roster = ValueList<Unit>.From(record.Roster.Where(u => u.Id != "rook")),
            Fallen = record.Fallen.Add("rook"),
            DrakeFlew = DrakeStage.Grown,
        };

        Assert.Equal(new EndingDrake(DrakeStage.Grown, false), CampaignEnding.Of(flown, content).Drake);
    }

    [Fact]
    public void TheEndingBlockRoundTripsAndLeavesTheRecordReadable()
    {
        var content = Content;
        var record = Finished(content, "keziah") with { Returned = ClaimantFate.Turned, Rooms = ValueList<string>.From(new[] { "bunk" }) };
        var ending = CampaignEnding.Of(record, content);
        var json = ProtocolJson.Campaign(record, ending);

        var read = ProtocolJson.ReadEnding(json)!;

        Assert.Equal(ending, read);
        Assert.Equal(ProtocolJson.Campaign(record), ProtocolJson.Campaign(ProtocolJson.ReadCampaign(json, content)));
        Assert.Contains("\"ending\":\"pending\"", json.Replace(" ", ""));
    }

    [Fact]
    public void AnEndingOfAnotherVersionIsRefused()
    {
        var content = Content;
        var record = Finished(content, "rook");
        var json = ProtocolJson.Campaign(record, CampaignEnding.Of(record, content) with { Version = CampaignEnding.CurrentVersion + 1 });

        var refused = Assert.Throws<ProtocolException>(() => ProtocolJson.ReadEnding(json));
        Assert.Contains("ending version", refused.Message);
    }

    [Fact]
    public void AWonCampaignLeavesTheEndingSaveUnderItsReservedName()
    {
        var content = Content;
        var store = new SaveStore(TempDir());
        try
        {
            Assert.Null(store.ReadEnding());
            var record = Finished(content, "rook");

            store.WriteEnding(record, content);

            Assert.Equal(CampaignEnding.Pending, store.ReadEnding()!.Ending);
            Assert.Empty(store.Names());
            Assert.NotNull(store.Save(SaveStore.EndingName, record));
        }
        finally
        {
            Directory.Delete(store.Directory, true);
        }
    }
}
