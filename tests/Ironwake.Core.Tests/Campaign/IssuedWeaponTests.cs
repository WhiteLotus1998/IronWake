using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Issue 804 slice 2 (round 263, DESIGN 13.23): the campaign issues Kinsbane on the pick.
/// <c>campaign.json</c>'s <c>issues</c> names a weapon for a cast member, and
/// <see cref="CampaignRecord.Kitted"/> puts it in front of their pack beside their cast weapons (issue 851,
/// round 276), so Keziah joins with the scythe ahead of her iron axe. A passed Keziah comes back on the field
/// fed to the fourth tooth's count. The cast file is unchanged, so a battle outside the campaign carries none.
/// </summary>
public class IssuedWeaponTests
{
    private static readonly GameContent Content = MapFixture.Content;

    private static string Dir => Fixture.RealContentDirectory();

    private static ItemStack Fresh(string id) => new(id, Content.Weapon(id).Durability);

    [Fact]
    public void TheShippedCampaignIssuesKinsbaneToKeziah()
    {
        Assert.Equal(new CampaignIssue("keziah", "kinsbane"), Assert.Single(Content.Campaign.Issues));
    }

    [Fact]
    public void KittedPutsTheIssuedWeaponInFrontBesideHerIronAxe()
    {
        var keziah = CampaignRecord.Kitted(Content.Unit("keziah"), Content);

        Assert.Equal(new[] { Fresh(Kinsbane.ItemId), Fresh("iron_axe"), Fresh("iron_gauntlets") }, keziah.Inventory.Items);
    }

    [Fact]
    public void AFullPackDropsItsLastStackForTheIssuedWeapon()
    {
        var cast = Content.Unit("keziah");
        var full = cast with { Inventory = new Inventory(ValueList<ItemStack>.From(Enumerable.Range(0, Inventory.Capacity).Select(i => i == 0 ? Fresh("iron_axe") : Fresh("iron_gauntlets")))) };

        var keziah = CampaignRecord.Kitted(full, Content);

        Assert.Equal(Inventory.Capacity, keziah.Inventory.Count);
        Assert.Equal(new[] { Fresh(Kinsbane.ItemId), Fresh("iron_axe") }, keziah.Inventory.Items.Take(2));
    }

    [Fact]
    public void TheIssuedScytheIsBoundToKeziah()
    {
        Assert.Equal("keziah", Content.Weapon(Kinsbane.ItemId).BoundTo);
    }

    [Fact]
    public void TheCampRefusesToDropTheIssuedScythe()
    {
        var record = CampaignRecord.StartAt(Content, 804, "sallow_grange", pick: "keziah");

        var drop = record.Drop("keziah", 0, Content);

        Assert.False(drop.Accepted);
        Assert.Equal("Kinsbane is bound to keziah and is never dropped", drop.Text);
        Assert.True(record.Drop("keziah", 1, Content).Accepted);
    }

    [Fact]
    public void TheBoundScytheStaysOutOfTheSignatureCeiling()
    {
        Assert.DoesNotContain(SignatureCeiling.Items(Content), w => w.Id == Kinsbane.ItemId);
        Assert.Contains(SignatureCeiling.Items(Content), w => w.Id == "family_lance");
    }

    [Fact]
    public void KittedNeverIssuesTheWeaponTwice()
    {
        var once = CampaignRecord.Kitted(Content.Unit("keziah"), Content);

        Assert.Equal(once, CampaignRecord.Kitted(once, Content));
    }

    [Fact]
    public void AnyoneTheCampaignIssuesNothingIsKittedAsBefore()
    {
        Assert.Equal(Content.Unit("wren"), CampaignRecord.Kitted(Content.Unit("wren"), Content));
        Assert.Equal(Content.Unit("rook").Inventory, CampaignRecord.Kitted(Content.Unit("rook"), Content).Inventory);
    }

    [Fact]
    public void PickedAtTheRaidKeziahJoinsCarryingTheScythe()
    {
        var record = CampaignRecord.StartAt(Content, 804, "ironwake_raid").PickClaimant("keziah", Content).Record;

        var keziah = record.Present(Content).Single(u => u.Id == "keziah");
        Assert.Equal(Fresh(Kinsbane.ItemId), keziah.Inventory.Items[0]);
        Assert.Equal(Fresh("iron_axe"), keziah.Inventory.Items[1]);
    }

    [Fact]
    public void APassedKeziahReturnsOnTheFieldOneToothShortOfWaking()
    {
        var field = MapFiles.Load(MapFiles.CampaignPath(Dir, Content, "the_field"), Content);
        var opening = CampaignRecord.StartAt(Content, 804, "the_field", pick: "rook").Begin(field, Content);

        var scythe = opening.Find("keziah")!.Unit.Inventory.Items[0];
        Assert.Equal(Kinsbane.ItemId, scythe.ItemId);
        Assert.Equal(Kinsbane.FedFor(4), scythe.Fed);
        Assert.Equal(8, scythe.Fed);
        Assert.Equal(4, Kinsbane.Teeth(scythe.Fed));
        Assert.False(Kinsbane.Woken(scythe.Fed));
    }

    [Fact]
    public void AStarvedKinsbaneCampsAsStarvedAndNotAsLow()
    {
        var record = CampaignRecord.StartAt(Content, 804, "the_field", pick: "keziah");
        var starved = record with
        {
            Roster = ValueList<Unit>.From(record.Roster.Select(u => u.Id == "keziah"
                ? u with { Inventory = u.Inventory.Replace(0, u.Inventory.Items[0] with { Starved = true, Uses = 1 }) }
                : u)),
        };
        Assert.Equal(Kinsbane.ItemId, starved.Find("keziah")!.Inventory.Items[0].ItemId);

        var lines = CampaignSession.LowLines(starved, Content).Where(l => l.Contains("Kinsbane", StringComparison.Ordinal)).ToList();

        Assert.Equal(new[] { "starved: Keziah's Kinsbane at half power until it lands a hit" }, lines);
        Assert.DoesNotContain(CampaignSession.LowLines(record, Content), l => l.Contains("Kinsbane", StringComparison.Ordinal));
        Assert.DoesNotContain(CampaignSession.LowLines(starved with { Benched = ValueList<string>.Of("keziah") }, Content), l => l.Contains("Kinsbane", StringComparison.Ordinal));
    }

    [Fact]
    public void KinsbanesDescriptionNamesTheReachClause()
    {
        Assert.Contains("if an enemy is in reach", Content.Weapon(Kinsbane.ItemId).Description, StringComparison.Ordinal);
    }

    [Fact]
    public void APassedRookReturnsWithHerOwnPack()
    {
        var field = MapFiles.Load(MapFiles.CampaignPath(Dir, Content, "the_field"), Content);
        var opening = CampaignRecord.StartAt(Content, 804, "the_field", pick: "keziah").Begin(field, Content);

        Assert.Equal(Content.Unit("rook").Inventory.Items, opening.Find("rook")!.Unit.Inventory.Items);
    }

    private static ContentFiles With(string issues) =>
        ContentSerializer.Write(Content) with { Campaign = new ContentFile(ContentFiles.CampaignName, $$"""{ "startingPurse": 0, "certificationPrice": 0, "maps": [ { "map": "one", "reward": 0, "stock": [] } ], "issues": {{issues}} }""") };

    [Fact]
    public void IssuesRoundTripThroughTheSerializer()
    {
        var content = ContentLoader.Parse(With("""{ "keziah": "kinsbane" }"""));

        Assert.Equal(content.Campaign.Issues, ContentLoader.Parse(ContentSerializer.Write(content)).Campaign.Issues);
        Assert.Equal("kinsbane", content.Campaign.IssuedTo("keziah"));
    }

    [Theory]
    [InlineData("""{ "nobody": "kinsbane" }""", "issues.nobody", "is not a cast member")]
    [InlineData("""{ "keziah": "no_such_item" }""", "issues.keziah", "must be a weapon id in weapons.json")]
    [InlineData("""{ "keziah": 3 }""", "issues.keziah", "must be a weapon id in weapons.json")]
    [InlineData("""{ "keziah": "maud_psalter" }""", "issues.keziah", "is bound to maud")]
    public void ABadIssueIsRefusedNamingTheField(string issues, string field, string message)
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(With(issues)));

        Assert.Equal((ContentFiles.CampaignName, (string?)null, field), (e.File, e.Entry, e.Field));
        Assert.Contains(message, e.Message);
    }
}
