using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// The captain's origin and gender (issue 681, DESIGN section 14): an origin moves the cast
/// file's captain by deltas that sum to 0, the loader refuses a set that grows or breaks the
/// card, the chosen pronoun is what text uses for the captain, both survive a save, no rapport or
/// rivalry number depends on the origin, and a campaign started without either is the cast file's.
/// </summary>
[Collection("console")]
public class CaptainOriginTests
{
    private static readonly GameContent Content = ContentLoader.Load(Fixture.RealContentDirectory());

    private static Unit CastCaptain => Content.Cast[0];

    private static ContentException Fails(string origins)
    {
        var campaign = System.Text.Json.Nodes.JsonNode.Parse(ContentSerializer.Write(Content).Campaign!.Text)!;
        campaign.AsObject()["origins"] = System.Text.Json.Nodes.JsonNode.Parse(origins);
        var files = ContentSerializer.Write(Content) with { Campaign = new ContentFile(ContentFiles.CampaignName, campaign.ToJsonString()) };
        return Assert.Throws<ContentException>(() => ContentLoader.Parse(files));
    }

    [Fact]
    public void TheCampaignOffersFourOriginsEachMovingTheCardBySetsThatSumToZero()
    {
        Assert.Equal(new[] { "aldmere", "sallow", "kestrow", "outlands" }, Content.Campaign.Origins.Select(o => o.Id));
        foreach (var origin in Content.Campaign.Origins)
        {
            Assert.Equal(0, CaptainOrigin.Sum(origin.Stats));
            Assert.Equal(0, CaptainOrigin.Sum(origin.Growths));
            Assert.NotEqual(Stats.Zero, origin.Stats);
        }
    }

    [Fact]
    public void AnOriginsCardIsTheCastCaptainPlusItsDeltasWithItsIdAsTheRegion()
    {
        var origin = Content.Campaign.Origin("kestrow")!;

        var captain = origin.Apply(CastCaptain);

        Assert.Equal(CastCaptain.Stats + origin.Stats, captain.Stats);
        Assert.Equal(CastCaptain.Growths + origin.Growths, captain.Growths);
        Assert.Equal("kestrow", captain.Region);
        Assert.Equal(CastCaptain with { Stats = captain.Stats, Growths = captain.Growths, Region = "kestrow" }, captain);
    }

    [Fact]
    public void AnOriginWhoseStatsDoNotSumToZeroIsRefused()
    {
        var e = Fails("""[ { "id": "big", "name": "Big", "stats": { "str": 2 }, "growths": {} } ]""");

        Assert.Equal((ContentFiles.CampaignName, "big", "stats"), (e.File, e.Entry, e.Field));
        Assert.Contains("sums to 2", e.Message);
    }

    [Fact]
    public void AnOriginWhoseGrowthsDoNotSumToZeroIsRefused()
    {
        var e = Fails("""[ { "id": "big", "name": "Big", "stats": {}, "growths": { "spd": -5 } } ]""");

        Assert.Equal((ContentFiles.CampaignName, "big", "growths"), (e.File, e.Entry, e.Field));
    }

    [Fact]
    public void AnOriginThatTakesAStatBelowZeroIsRefused()
    {
        var e = Fails("""[ { "id": "thin", "name": "Thin", "stats": { "mag": -1, "str": 1 }, "growths": {} } ]""");

        Assert.Equal((ContentFiles.CampaignName, "thin", "stats.mag"), (e.File, e.Entry, e.Field));
    }

    [Fact]
    public void AnOriginThatTakesAGrowthPastOneHundredIsRefused()
    {
        var e = Fails("""[ { "id": "fast", "name": "Fast", "stats": {}, "growths": { "hp": 50, "cha": -50 } } ]""");

        Assert.Equal((ContentFiles.CampaignName, "fast", "growths.hp"), (e.File, e.Entry, e.Field));
    }

    [Fact]
    public void AnOriginListedTwiceIsRefused()
    {
        var e = Fails("""[ { "id": "same", "name": "A", "stats": {}, "growths": {} }, { "id": "same", "name": "B", "stats": {}, "growths": {} } ]""");

        Assert.Equal((ContentFiles.CampaignName, "same", "id"), (e.File, e.Entry, e.Field));
    }

    [Fact]
    public void TheOriginsRoundTripThroughTheSerializer()
    {
        var reloaded = ContentLoader.Parse(ContentSerializer.Write(Content));

        Assert.Equal(Content.Campaign.Origins, reloaded.Campaign.Origins);
    }

    [Fact]
    public void ACampaignStartedOnAnOriginPlaysTheCaptainOnItsCardAndRecordsIt()
    {
        var record = CampaignRecord.Start(Content, 5, origin: "sallow");

        Assert.Equal("sallow", record.Origin);
        Assert.Equal(Content.Campaign.Origin("sallow")!.Apply(CastCaptain), record.Captain(Content));
    }

    [Fact]
    public void ACampaignStartedOnALaterMapKeepsTheChosenCaptain()
    {
        var record = CampaignRecord.StartAt(Content, 5, "the_tollgate", origin: "aldmere", captain: Pronoun.She);

        Assert.Equal(Content.Campaign.Origin("aldmere")!.Apply(CastCaptain) with { Pronoun = Pronoun.She }, record.Captain(Content));
    }

    [Fact]
    public void ACampaignStartedWithoutAnOriginOrPronounIsTheCastFilesCaptain()
    {
        var record = CampaignRecord.Start(Content, 5);

        Assert.Null(record.Origin);
        Assert.Equal(CastCaptain, record.Captain(Content));
        Assert.DoesNotContain("origin", ProtocolJson.Campaign(record));
        Assert.DoesNotContain("pronoun", ProtocolJson.Campaign(record));
    }

    [Fact]
    public void AnOriginTheCampaignDoesNotOfferIsRefusedAtTheStart()
    {
        var e = Assert.Throws<ArgumentException>(() => CampaignRecord.Start(Content, 5, origin: "nowhere"));

        Assert.Contains("aldmere, sallow, kestrow, outlands", e.Message);
    }

    [Fact]
    public void NoRapportOrRivalryNumberDependsOnTheOrigin()
    {
        var plain = CampaignRecord.Start(Content, 5);
        foreach (var origin in Content.Campaign.Origins)
        {
            var record = CampaignRecord.Start(Content, 5, origin: origin.Id);
            var recruits = record.Roster.Where(u => u.Id != CastCaptain.Id);

            Assert.Equal(plain.Roster.Where(u => u.Id != CastCaptain.Id), recruits);
            Assert.Equal(CastCaptain.Stats.Cha, record.Captain(Content)!.Stats.Cha);
            Assert.Equal(CastCaptain.Growths.Cha, record.Captain(Content)!.Growths.Cha);
            Assert.False(Rivalry.IsRecruit(new BattleUnit(record.Captain(Content)!, Side.Player, default, 1, false, false, IsCaptain: true)));
        }
    }

    [Fact]
    public void TheChosenPronounIsWhatTextUsesForTheCaptain()
    {
        var record = CampaignRecord.Start(Content, 5, captain: Pronoun.She);
        var names = UnitNames.Of(record, Content);

        Assert.Equal(("she", "her", "her"), (names.Refer(CastCaptain.Id).Subject, names.Refer(CastCaptain.Id).Object, names.Refer(CastCaptain.Id).Possessive));
        Assert.Equal("she", Referent.For(Content, record.Captain(Content)!).Subject);
        Assert.Equal("he", Referent.For(Content, CastCaptain).Subject);
    }

    [Fact]
    public void ACampaignRefusalUsesTheChosenPronoun()
    {
        var record = CampaignRecord.Start(Content, 5, captain: Pronoun.She);

        var refused = record.Repair(CastCaptain.Id, 4, Content);

        Assert.False(refused.Accepted);
        Assert.Contains("; she carries 2", refused.Text);
    }

    [Fact]
    public void TheOriginAndPronounSurviveASave()
    {
        var record = CampaignRecord.Start(Content, 5, origin: "outlands", captain: Pronoun.She);

        var json = ProtocolJson.Campaign(record);
        var loaded = ProtocolJson.ReadCampaign(json, Content);

        Assert.Equal(json, ProtocolJson.Campaign(loaded));
        Assert.Equal("outlands", loaded.Origin);
        Assert.Equal(Pronoun.She, loaded.Captain(Content)!.Pronoun);
    }

    [Fact]
    public void ARecordNamingAnOriginTheCampaignDoesNotOfferIsRefused()
    {
        var json = ProtocolJson.Campaign(CampaignRecord.Start(Content, 5, origin: "outlands")).Replace("\"origin\":\"outlands\"", "\"origin\":\"nowhere\"");

        var e = Assert.Throws<ProtocolException>(() => ProtocolJson.ReadCampaign(json, Content));

        Assert.Contains("origin 'nowhere'", e.Message);
    }

    [Fact]
    public void TheConsoleCampaignTakesTheOriginAndCaptainAndPrintsThem()
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-origin-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, "quit\n");
        try
        {
            var output = ConsoleCapture.Run(() => Ironwake.Cli.Program.Main(new[] { "campaign", "--seed", "5", "--origin", "kestrow", "--captain", "she", "--script", path, "--content", Fixture.RealContentDirectory() }));

            Assert.Contains($"Captain: {CastCaptain.Name}, from Kestrow (she/her)", output);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TheConsoleCampaignRefusesAnOriginItDoesNotOffer()
    {
        var output = ConsoleCapture.Run(() => Ironwake.Cli.Program.Main(new[] { "campaign", "--origin", "nowhere", "--content", Fixture.RealContentDirectory() }));

        Assert.Contains("ERROR: the campaign has no origin 'nowhere'; it offers aldmere, sallow, kestrow, outlands", output);
    }

    [Fact]
    public void ACampaignWithoutAChoicePrintsNoCaptainLine()
    {
        Assert.Null(Ironwake.Cli.CampaignSession.CaptainLine(CampaignRecord.Start(Content, 5), Content));
    }

    [Fact]
    public void NewGamesCaptainLinesNameTheOriginItsMovesAndThePronoun()
    {
        var lines = Screens.CaptainLines(Content, 2, Pronoun.She);
        var kestrow = Content.Campaign.Origins[2];
        var moves = Stats.All.Where(s => kestrow.Stats.Get(s) != 0).Select(s => $"{s} {kestrow.Stats.Get(s):+0;-0}");

        Assert.Equal(new[] { $"Origin: Kestrow, {string.Join(" ", moves)}", $"Captain: {CastCaptain.Name}, she/her" }, lines);
    }
}
