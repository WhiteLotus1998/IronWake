using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The title screen and Options (issue 677): the options kept in the profile beside the saves,
/// Continue loading the newest save and shown only when one exists, and a campaign's difficulty
/// lowered at a camp, never raised and never mid-battle, the lowering printed on the record.
/// </summary>
[Collection("console")]
public class TitleAndOptionsTests
{
    private static GameContent Content => MapFixture.Content;

    private static string TempDir() => Path.Combine(Path.GetTempPath(), "ironwake-options-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("speed", "instant")]
    [InlineData("speed", "5x")]
    [InlineData("scenes", "all")]
    [InlineData("scenes", "map")]
    [InlineData("confirm-end-turn", "off")]
    [InlineData("reach-on-hover", "off")]
    [InlineData("ui-scale", "150")]
    [InlineData("sound", "off")]
    [InlineData("volume", "35")]
    public void TheProfileRoundTripsEachOption(string key, string value)
    {
        var store = new SaveStore(TempDir());
        try
        {
            var (set, refusal) = Options.Set(new Options(), key, value);
            Assert.Null(refusal);
            Assert.NotEqual(new Options(), set);

            store.WriteOptions(set);
            var (read, warnings) = new SaveStore(store.Directory).ReadOptions();

            Assert.Equal(set, read);
            Assert.Empty(warnings);
            Assert.Contains($"{key}: {value}", File.ReadAllLines(Path.Combine(store.Directory, SaveStore.ProfileFile)));
        }
        finally
        {
            if (Directory.Exists(store.Directory))
            {
                Directory.Delete(store.Directory, true);
            }
        }
    }

    [Fact]
    public void OptionsAndWinsShareTheProfileWithoutLosingEachOther()
    {
        var store = new SaveStore(TempDir());
        try
        {
            store.RecordWin("normal");
            store.WriteOptions(new Options { Speed = "2x" });
            store.RecordWin("recruit");

            Assert.Equal(new[] { "normal", "recruit" }, store.Won());
            Assert.Equal("2x", store.ReadOptions().Options.Speed);
        }
        finally
        {
            Directory.Delete(store.Directory, true);
        }
    }

    [Fact]
    public void AWinAloneWritesNoOptionLinesSoAnOldProfileStaysAsItWas()
    {
        var store = new SaveStore(TempDir());
        try
        {
            store.RecordWin("normal");

            Assert.Equal(new[] { "normal" }, File.ReadAllLines(Path.Combine(store.Directory, SaveStore.ProfileFile)));
            Assert.Equal(new Options(), store.ReadOptions().Options);
        }
        finally
        {
            Directory.Delete(store.Directory, true);
        }
    }

    [Theory]
    [InlineData("speed: 3x", "speed")]
    [InlineData("ui-scale: 110", "ui-scale")]
    [InlineData("volume: 101", "volume")]
    [InlineData("volume: -1", "volume")]
    [InlineData("sound: maybe", "sound")]
    [InlineData("colour: blue", "colour")]
    public void ABadProfileLineKeepsTheDefaultAndWarnsNamingTheLineAndField(string line, string key)
    {
        var (options, warnings) = Options.Read(new[] { "normal", line });

        Assert.Equal(new Options(), options);
        var warning = Assert.Single(warnings);
        Assert.StartsWith($"{SaveStore.ProfileFile} line 2, '{key}': ", warning);
    }

    [Fact]
    public void ContinueIsAbsentWithNoSaveAndPresentAndFirstWithOne()
    {
        var store = new SaveStore(TempDir());
        try
        {
            Assert.Null(store.Newest());
            Assert.DoesNotContain(TitleChoice.Continue, Screens.CampaignTitle(store.Newest() is not null));
            Assert.Equal(TitleChoice.NewGame, Screens.DefaultTitleChoice(false));
            Assert.Equal(new[] { "New game", "Load", "Options", "Quit" }, Screens.CampaignTitle(false).Select(Screens.Label));

            store.Autosave(CampaignRecord.Start(Content, 3));

            Assert.Equal("auto-1", store.Newest());
            var title = Screens.CampaignTitle(store.Newest() is not null);
            Assert.Equal(TitleChoice.Continue, title[0]);
            Assert.Equal(TitleChoice.Continue, Screens.DefaultTitleChoice(true));
            Assert.Equal(5, title.Count);
        }
        finally
        {
            if (Directory.Exists(store.Directory))
            {
                Directory.Delete(store.Directory, true);
            }
        }
    }

    [Fact]
    public void ContinueLoadsTheNewestSaveAutosaveOrNamed()
    {
        var store = new SaveStore(TempDir());
        try
        {
            var record = CampaignRecord.Start(Content, 3);
            store.Autosave(record);
            store.Save("before-the-mill", record with { Purse = 1 });
            File.SetLastWriteTimeUtc(Path.Combine(store.Directory, "auto-1.json"), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(Path.Combine(store.Directory, "before-the-mill.json"), new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

            Assert.Equal("before-the-mill", store.Newest());

            File.SetLastWriteTimeUtc(Path.Combine(store.Directory, "auto-1.json"), new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));

            Assert.Equal("auto-1", store.Newest());
        }
        finally
        {
            Directory.Delete(store.Directory, true);
        }
    }

    [Fact]
    public void TheTitleFooterIsTheNameAndTheRulesVersion()
    {
        Assert.Equal($"Ironwake, rules version {RulesVersion.Current}", Screens.TitleFooter);
    }

    [Fact]
    public void TheShippedLadderStandsInTierOrderRecruitCaptainTactician()
    {
        Assert.True(Content.Difficulty("recruit").Tier < Content.Difficulty("normal").Tier);
        Assert.True(Content.Difficulty("normal").Tier < Content.Difficulty("tactician").Tier);
    }

    [Fact]
    public void LoweringTheDifficultyAtACampIsTakenAndPrintedOnTheRecord()
    {
        var record = CampaignRecord.Start(Content, 3, "tactician");

        var lowered = record.LowerDifficulty("normal", Content);
        var again = lowered.Record.LowerDifficulty("recruit", Content);

        Assert.True(lowered.Accepted);
        Assert.Equal("normal", lowered.Record.Difficulty);
        Assert.Equal("difficulty lowered from Tactician to Captain for the rest of the campaign; the record says so", lowered.Text);
        Assert.True(again.Accepted);
        Assert.Equal(new[] { "tactician", "normal" }, again.Record.LoweredFrom);
        Assert.Equal("difficulty Recruit (lowered from Tactician), permadeath on", Ironwake.Cli.CampaignSession.RulesLine(again.Record, Content));
    }

    [Theory]
    [InlineData("normal", "tactician", "Tactician is not easier than Captain; a difficulty may be lowered at a camp, never raised")]
    [InlineData("recruit", "normal", "Captain is not easier than Recruit; a difficulty may be lowered at a camp, never raised")]
    [InlineData("normal", "normal", "the campaign is already on Captain")]
    [InlineData("normal", "nightmare", "no difficulty 'nightmare'; there are Recruit, Captain, Tactician")]
    public void RaisingTheDifficultyIsRefused(string from, string to, string refusal)
    {
        var record = CampaignRecord.Start(Content, 3, from);

        var result = record.LowerDifficulty(to, Content);

        Assert.False(result.Accepted);
        Assert.Equal(refusal, result.Text);
        Assert.Same(record, result.Record);
    }

    [Fact]
    public void ALoweredRecordRoundTripsThroughTheProtocolAndAnOldRecordReadsAsNeverLowered()
    {
        var record = CampaignRecord.Start(Content, 3, "normal").LowerDifficulty("recruit", Content).Record;

        var json = ProtocolJson.Campaign(record);
        var read = ProtocolJson.ReadCampaign(json, Content);

        Assert.Contains("\"loweredFrom\":[\"normal\"]", json);
        Assert.Equal(record.LoweredFrom, read.LoweredFrom);
        Assert.DoesNotContain("loweredFrom", ProtocolJson.Campaign(CampaignRecord.Start(Content, 3)));
        Assert.Empty(ProtocolJson.ReadCampaign(ProtocolJson.Campaign(CampaignRecord.Start(Content, 3)), Content).LoweredFrom);
    }

    [Fact]
    public void ARecordLoweredFromAnUnknownDifficultyIsRefused()
    {
        var json = ProtocolJson.Campaign(CampaignRecord.Start(Content, 3).LowerDifficulty("recruit", Content).Record).Replace("\"loweredFrom\":[\"normal\"]", "\"loweredFrom\":[\"nightmare\"]");

        var e = Assert.Throws<ProtocolException>(() => ProtocolJson.ReadCampaign(json, Content));

        Assert.Contains("'nightmare' is not a difficulty", e.Message);
    }

    [Fact]
    public void TheConsoleLowersTheDifficultyAtACampAndRefusesItMidBattle()
    {
        var output = Run("difficulty recruit\ndifficulty captain\nmarch\ndifficulty recruit\n", "--difficulty", "normal");

        Assert.Contains("Difficulty lowered from Captain to Recruit for the rest of the campaign; the record says so\n", output);
        Assert.Contains("ERROR: Captain is not easier than Recruit; a difficulty may be lowered at a camp, never raised\n", output);
        Assert.Contains("ERROR: The difficulty is lowered only at a camp, never mid-battle\n", output);
    }

    [Fact]
    public void TheOptionsScreenShowsEachOptionAndTheCampaignsRulesReadOnly()
    {
        var lines = Screens.OptionsLines(new Options { Scenes = "map", UiScale = 125 }, CampaignRecord.Start(Content, 3), Content);

        Assert.Equal(
            new[]
            {
                "Enemy phase speed: 1x",
                "Battle scenes: map only",
                "Confirm end turn while units are unmoved: on",
                "Show enemy reach on hover: on",
                "UI scale: 125",
                "Sound: on, volume 80",
                "This campaign: difficulty Captain, permadeath on (lowered only at a camp)",
            },
            lines);
        Assert.Equal(6, Screens.OptionsLines(new Options(), null, Content).Count);
    }

    private static string Run(string script, params string[] extra)
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-options-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, script);
        try
        {
            var args = new[] { "campaign", "--seed", "3", "--script", path, "--content", Fixture.RealContentDirectory() }.Concat(extra).ToArray();
            return ConsoleCapture.Run(() => Ironwake.Cli.Program.Main(args));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
