using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// The hill after the keep (issue 1386 slice 3d, STORY's "one extra map after the keep, and the choice is on its
/// card"): a keep won with a boss left in the coma stands at the hill's card instead of finishing; <c>reseal</c> needs
/// a rite-keeper standing and ends the campaign there; <c>fight</c> marches to the hill, whose win ends it; the card
/// carries to the save and the ending.
/// </summary>
[Collection("console")]
public class HillTests
{
    private static GameContent Real => ContentLoader.Load(Fixture.RealContentDirectory());

    /// <summary>A record that has just won the keep with Hask in the coma: the secret path's race taken.</summary>
    private static CampaignRecord AtTheHill(GameContent content) =>
        CampaignRecord.StartAt(content, 41, "ironwake_keep", pick: "keziah") with
        {
            MapIndex = content.Campaign.Maps.Count,
            Coma = ValueList<string>.Of("hask"),
        };

    private static CampaignRecord WithoutKeepers(CampaignRecord record) =>
        record with { Roster = ValueList<Unit>.From(record.Roster.Where(u => u.Id is not ("maud" or "pell"))) };

    [Fact]
    public void AKeepWonWithAComaStandsAtTheHillsCard()
    {
        var content = Real;
        var record = AtTheHill(content);

        Assert.False(record.IsFinished(content));
        Assert.True(record.HillWaits(content));
        Assert.Equal("under_the_hill", record.NextMap(content).MapId);
        Assert.Equal(8, record.NextMap(content).EnemyLevel);
        Assert.Empty(record.QuestsOffered(content));
        Assert.Equal("the keep is fought; nothing built now stands in a battle", record.KeepMenuRefusal(content));
    }

    [Fact]
    public void AKeepWonWithoutAComaFinishesTheCampaign()
    {
        var content = Real;
        var record = AtTheHill(content) with { Coma = ValueList<string>.Empty };

        Assert.True(record.IsFinished(content));
        Assert.Null(record.Hill(content));
        Assert.Equal("no card waits; the hill is offered after the keep, on the secret path", record.HillRefusal(HillChoice.Fight, content));
        Assert.False(record.ChooseHill(HillChoice.Fight, content).Accepted);
    }

    [Fact]
    public void TheCardIsNotOfferedBeforeTheKeepIsWon()
    {
        var content = Real;
        var record = AtTheHill(content) with { MapIndex = content.Campaign.Maps.Count - 1 };

        Assert.Null(record.Hill(content));
        Assert.False(record.HillWaits(content));
        Assert.False(record.ChooseHill(HillChoice.Reseal, content).Accepted);
    }

    [Fact]
    public void TheMarchToTheHillIsRefusedUntilTheCardIsAnswered()
    {
        var content = Real;
        var record = AtTheHill(content);
        var hill = MapFiles.Load(MapFiles.CampaignPath(Fixture.RealContentDirectory(), content, "under_the_hill"), content);

        Assert.Equal("the card waits; answer it with reseal or fight first", record.MarchRefusal(hill, content));
        Assert.Equal("the card waits; answer it with fight first", WithoutKeepers(record).MarchRefusal(hill, content));
        Assert.Null(record.ChooseHill(HillChoice.Fight, content).Record.MarchRefusal(hill, content));
    }

    [Fact]
    public void ResealWithARiteKeeperStandingEndsTheCampaignAtTheCard()
    {
        var content = Real;
        var result = AtTheHill(content).ChooseHill(HillChoice.Reseal, content);

        Assert.True(result.Accepted);
        Assert.Equal("the door is resealed; the campaign ends at the keep", result.Text);
        Assert.True(result.Record.IsFinished(content));
        Assert.Equal(HillChoice.Reseal, CampaignEnding.Of(result.Record, content).Hill);
    }

    [Fact]
    public void ResealIsRefusedWithNoRiteKeeperStandingAndFightIsTheOnlyAnswer()
    {
        var content = Real;
        var record = WithoutKeepers(AtTheHill(content));

        var refused = record.ChooseHill(HillChoice.Reseal, content);
        Assert.False(refused.Accepted);
        Assert.Equal("nobody left can read the rite (maud or pell); the card offers only fight", refused.Text);
        Assert.True(record.ChooseHill(HillChoice.Fight, content).Accepted);

        var pellAlone = record with { Roster = record.Roster.Add(content.Unit("pell")) };
        Assert.True(pellAlone.ChooseHill(HillChoice.Reseal, content).Accepted);
    }

    [Fact]
    public void TheCardIsAnsweredOnce()
    {
        var content = Real;
        var fight = AtTheHill(content).ChooseHill(HillChoice.Fight, content).Record;

        Assert.False(fight.IsFinished(content));
        var again = fight.ChooseHill(HillChoice.Reseal, content);
        Assert.False(again.Accepted);
        Assert.Equal("the card is answered: fight", again.Text);
    }

    [Fact]
    public void TheHillIsFoughtOnItsBoardAndItsWinEndsTheCampaign()
    {
        var content = Real;
        var record = AtTheHill(content).ChooseHill(HillChoice.Fight, content).Record;
        var hill = MapFiles.Load(MapFiles.CampaignPath(Fixture.RealContentDirectory(), content, "under_the_hill"), content);

        var opening = record.Begin(hill, content);
        Assert.Equal("Under the Hill", opening.Map.Name);
        Assert.Equal(record.Seed + (ulong)content.Campaign.Maps.Count, opening.Seed);
        Assert.Contains(opening.Units, u => u.Swallowed);

        var end = opening with { Units = ValueList<BattleUnit>.From(opening.UnitsOf(Side.Player)) };
        Assert.Equal(BattleResult.Won, end.Outcome.Result);
        var won = record.Fought(end, content);
        Assert.True(won.IsFinished(content));
        Assert.Equal(HillChoice.Fight, CampaignEnding.Of(won, content).Hill);
    }

    [Fact]
    public void TheCardRoundTripsOnTheSaveAndTheEnding()
    {
        var content = Real;
        var record = AtTheHill(content).ChooseHill(HillChoice.Reseal, content).Record;
        var ending = CampaignEnding.Of(record, content);

        var json = ProtocolJson.Campaign(record, ending);
        Assert.Contains("\"hillChose\":\"reseal\"", json.Replace(" ", ""));
        Assert.Contains("\"hill\":\"reseal\"", json.Replace(" ", ""));
        Assert.Equal(HillChoice.Reseal, ProtocolJson.ReadCampaign(json, content).HillChose);
        Assert.Equal(ending, ProtocolJson.ReadEnding(json));
        Assert.DoesNotContain("hillChose", ProtocolJson.Campaign(AtTheHill(content)));

        var bad = json.Replace("\"hillChose\":\"reseal\"", "\"hillChose\":\"flee\"");
        Assert.Contains("hillChose 'flee' is not one of reseal, fight", Assert.Throws<ProtocolException>(() => ProtocolJson.ReadCampaign(bad, content)).Message);
    }

    [Fact]
    public void TheHillsKeepersMustBeInTheCast()
    {
        var dir = Fixture.CopyRealContent();
        var path = Path.Combine(dir, "campaign.json");
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"keepers\": [\"maud\", \"pell\"]", "\"keepers\": [\"nobody\"]"));

        var e = Assert.Throws<ContentException>(() => ContentLoader.Load(dir));
        Assert.Contains("secret.hill.keepers", e.Message);
        Assert.Contains("'nobody' is not in the cast", e.Message);
    }

    [Fact]
    public void TheHillMayNotBeOneOfTheMainMaps()
    {
        var dir = Fixture.CopyRealContent();
        var path = Path.Combine(dir, "campaign.json");
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"map\": \"under_the_hill\"", "\"map\": \"the_mill\""));

        var e = Assert.Throws<ContentException>(() => ContentLoader.Load(dir));
        Assert.Contains("secret.hill.map", e.Message);
    }

    [Fact]
    public void TheConsoleCampaignEndsOnResealAndPrintsTheCard()
    {
        var content = Real;
        var dir = Path.Combine(Path.GetTempPath(), "ironwake-hill-" + Guid.NewGuid().ToString("N"));
        try
        {
            new SaveStore(dir).Save("at-the-hill", AtTheHill(content));

            var output = Run(out var exit, "march\n", dir);
            Assert.Contains("The card: reseal (shut the door here; the campaign ends) or fight (go under the hill).\n", output);
            Assert.Contains("ERROR: The card waits; answer it with reseal or fight first\n", output);
            Assert.Contains("-- After the keep: Under the Hill; the purse holds 500 --\n", output);
            Assert.Contains("March (march): after the keep, Under the Hill.", output);

            var resealed = Run(out var resealExit, "reseal\n", dir);
            Assert.Equal(0, resealExit);
            Assert.Contains("Campaign won: all 10 maps, the door resealed, the purse holds", resealed);
            Assert.Contains("\"hill\":\"reseal\"", File.ReadAllText(Path.Combine(dir, "ending.json")).Replace(" ", ""));

            var fought = Run(out _, "fight\nmarch\n", dir);
            Assert.Contains($"After the keep: Under the Hill, seed {AtTheHill(content).Seed + (ulong)content.Campaign.Maps.Count}\n", fought);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    private static string Run(out int exit, string script, string saves)
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-hill-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, script);
        try
        {
            var args = new[] { "campaign", "--script", path, "--content", Fixture.RealContentDirectory(), "--saves", saves, "--load", "at-the-hill" };
            var code = 0;
            var output = ConsoleCapture.Run(() => code = Ironwake.Cli.Program.Main(args));
            exit = code;
            return output;
        }
        finally
        {
            File.Delete(path);
        }
    }
}
