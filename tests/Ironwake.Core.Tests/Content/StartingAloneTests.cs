using Ironwake.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Starting Alone, the campaign's first map (issue 631, DESIGN section 14): the captain alone
/// against three, losable, with a journaled play whose one Recall changes the outcome; and the
/// text cards a <c>campaign.json</c> map carries before and after it, loaded, refused and printed.
/// </summary>
[Collection("console")]
public class StartingAloneTests
{
    private static string Repo => Directory.GetParent(Fixture.RealContentDirectory())!.FullName;

    private static string Transcript(string name) => Path.Combine(Repo, "docs", "transcripts", name);

    private static ContentFiles With(string campaign) =>
        Fixture.Files() with { Campaign = new ContentFile(ContentFiles.CampaignName, campaign) };

    private static string Campaign(string card) =>
        $$"""{ "startingPurse": 0, "certificationPrice": 0, "maps": [ { "map": "one", "reward": 0, "stock": [] {{card}} } ] }""";

    private static ContentException Fails(string card) => Assert.Throws<ContentException>(() => ContentLoader.Parse(With(Campaign(card))));

    [Fact]
    public void StartingAloneIsTheCampaignsFirstMapAndDeploysTheCaptainAlone()
    {
        var content = MapFixture.Content;
        var record = CampaignRecord.Start(content, 1);
        var map = MapFiles.Load(Path.Combine(MapFixture.MapsDirectory, "starting_alone.map"), content);

        Assert.Equal("starting_alone", content.Campaign.Maps[0].MapId);
        Assert.Equal(new[] { "captain" }, record.Deployment(map, content));
        Assert.Equal(WinCondition.Rout, map.Win);
        Assert.Equal(3, map.RecallCharges);
        Assert.Equal(3, map.Placements.Count(p => p.Side == Side.Enemy));
        Assert.Empty(content.Campaign.Maps[0].Before);
        Assert.Empty(content.Campaign.Maps[0].After);
    }

    /// <summary>
    /// The after scene (issue 1005, round 374) opens "Three dead on the waystation road": a rout
    /// leaves every enemy dead, so the line holds only while the board fields three, and it fails
    /// here before it can come untrue on the screen.
    /// </summary>
    [Fact]
    public void TheAfterScenesThreeDeadMatchTheBoardsEnemies()
    {
        var content = MapFixture.Content;
        var map = MapFiles.Load(Path.Combine(MapFixture.MapsDirectory, "starting_alone.map"), content);
        var scene = content.Scenes.Single(s => s.Id == "starting_alone_after");

        Assert.Equal(ScenePoint.After, scene.Point);
        Assert.StartsWith("Three dead on the waystation road.", scene.Lines[0].Text, StringComparison.Ordinal);
        Assert.Equal(WinCondition.Rout, map.Win);
        Assert.Equal(3, map.Placements.Count(p => p.Side == Side.Enemy));
    }

    /// <summary>
    /// Hask's line in the before scene (issue 1005, round 374), "The bowman will keep to the high
    /// ground and let you come to him", is a prediction the board keeps: the map's one archer is a
    /// guard on a hill, so it fails here before it can come untrue on the screen.
    /// </summary>
    [Fact]
    public void TheBeforeScenesBowmanLineHoldsOnTheBoard()
    {
        var content = MapFixture.Content;
        var map = MapFiles.Load(Path.Combine(MapFixture.MapsDirectory, "starting_alone.map"), content);
        var scene = content.Scenes.Single(s => s.Id == "starting_alone_before");

        Assert.Equal(ScenePoint.Before, scene.Point);
        Assert.Contains(scene.Lines, l => l.Speaker == "hask" && l.Text.Contains("The bowman will keep to the high ground", StringComparison.Ordinal));
        var archer = Assert.Single(map.Placements.OfType<EnemyPlacement>(), e => e.TemplateId == "archer");
        Assert.Equal(Behavior.Guard, archer.Behavior);
        Assert.Equal("hill", map.TerrainIdAt(archer.At));
    }

    /// <summary>
    /// Issue 631's acceptance: the captain can lose the map. Waiting on the fort while the hill
    /// group's hexer strikes from range he cannot answer kills him on seed 3 by turn 5.
    /// </summary>
    [Fact]
    public void HoldingTheFortWithoutStrikingBackLosesTheCaptain()
    {
        var output = Play(out var exit, "move captain 5,4\nwait captain\nend !\nwait captain\nend !\nend !\nend !\nend !\n", "3");

        Assert.Equal(1, exit);
        Assert.Contains("Hexer attacks Alder Fenn", output);
        Assert.Contains("Alder Fenn falls at 5,4\n", output);
        Assert.Contains("Battle lost: the captain is dead", output);
    }

    /// <summary>
    /// Code's journaled play (seed 631): the fort, the brigand killed on its counter, the hexer
    /// struck from beside the fort, then a swing at the archer at 3 hp that misses; the archer's
    /// arrow kills the captain, the one Recall returns to turn 4's start, a dressing on the fort
    /// replaces the swing, and the archer falls on turn 6.
    /// </summary>
    [Fact]
    public void TheJournaledPlayWinsOnTurnSixWithOneRecallThatChangesTheOutcome()
    {
        var script = Transcript("2026-10-01-starting_alone-631.script");

        var output = Run(out var exit, "play", "starting_alone", "--seed", "631", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.Contains("Lethal if all land: Alder Fenn (Archer for 5, against 3 hp)\n", output);
        Assert.Contains("Alder Fenn falls at 5,2\n", output);
        Assert.Contains("Recalled to state 22; 2 charges left\n", output);
        Assert.Contains("Alder Fenn uses Field Dressing (2 left)\n", output);
        Assert.Contains("Archer falls at 5,1\n", output);
        Assert.EndsWith("Battle won: rout\n", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Starting Alone's cards are replaced by its scenes (issue 1005): the campaign opens on the
    /// screen's heading, prints the before scene after <c>march</c> and the map line, rules line last,
    /// and the after scene once the map is won, below the won line; none of it is an event, so none is in the log.
    /// </summary>
    [Fact]
    public void TheCampaignPrintsTheBeforeSceneAfterMarchAndTheAfterSceneBelowTheWonLine()
    {
        var battle = File.ReadAllText(Transcript("2026-10-01-starting_alone-631.script"));
        var path = Path.Combine(Path.GetTempPath(), "ironwake-alone-" + Guid.NewGuid().ToString("N") + ".script");
        var log = Path.ChangeExtension(path, ".log");
        File.WriteAllText(path, "march\n" + battle + "leave\n");
        try
        {
            var output = Run(out _, "campaign", "--seed", "631", "--script", path, "--content", Fixture.RealContentDirectory(), "--log", log);

            Assert.StartsWith("Campaign, seed 631, difficulty Captain, permadeath on, scheme TwoRollAverage, 10 maps\n-- Before map 1 of 10: Starting Alone; the purse holds 500 --\n", output);
            Assert.Contains("Map 1 of 10: Starting Alone, seed 631\n-- Starting Alone --\nIronwake, before sunrise.", output);
            Assert.Contains("Hask: Don't write, Alder. Bring them home.\n", output);
            Assert.Contains("rewinds to a player turn you name; you have three.)\n\nObjective:", output);
            Assert.Contains("Starting Alone won: rout; reward 300, the purse holds 800; nobody fell\n-- After Starting Alone --\nThree dead on the waystation road.", output);
            Assert.Contains("into Hask's purse and draws the\nstring.\n\n-- Before map 2 of 10: The Mill; the purse holds 800 --\n", output);
            Assert.DoesNotContain("Three dead on a road nobody will remember", output);
            var scene = output[output.IndexOf("-- Starting Alone --", StringComparison.Ordinal)..output.IndexOf("Objective:", StringComparison.Ordinal)];
            Assert.All(scene.Split('\n'), line => Assert.True(line.Length <= Ironwake.Cli.CampaignSession.CardWidth, line));
            Assert.DoesNotContain("Bring them home", File.ReadAllText(log));
            Assert.DoesNotContain("draws the string", File.ReadAllText(log));
        }
        finally
        {
            File.Delete(path);
            File.Delete(log);
        }
    }

    [Fact]
    public void ACardWrapsItsParagraphsToTheCardWidthWithABlankLineAfterEach()
    {
        var content = ContentLoader.Parse(With(Campaign(""", "after": ["one two three", "four"] """)));

        Assert.Equal(ValueList<string>.Of("one two three", "four"), content.Campaign.Maps[0].After);
        Assert.Empty(content.Campaign.Maps[0].Before);
        Assert.Equal(new[] { "one two", "three" }, Ironwake.Cli.CampaignSession.Wrap("one two three", 7));
        Assert.Equal(new[] { "unbroken", "a" }, Ironwake.Cli.CampaignSession.Wrap("unbroken a", 3));
    }

    [Fact]
    public void ACardRoundTripsThroughTheSerializer()
    {
        var content = ContentLoader.Parse(With(Campaign(""", "before": ["first", "second"], "after": ["last"] """)));

        var reloaded = ContentLoader.Parse(ContentSerializer.Write(content));

        Assert.Equal(content.Campaign, reloaded.Campaign);
        Assert.Equal(ValueList<string>.Of("first", "second"), reloaded.Campaign.Maps[0].Before);
    }

    [Theory]
    [InlineData(""", "before": [] """, "before", "must hold at least one paragraph, or be left out")]
    [InlineData(""", "before": [" padded"] """, "before[0]", "must be non-blank text with no leading or trailing space")]
    [InlineData(""", "after": ["fine", "café"] """, "after[1]", "must be printable ASCII on one line")]
    [InlineData(""", "after": ["two\nlines"] """, "after[0]", "must be printable ASCII on one line")]
    [InlineData(""", "before": ["a", "b", "c", "d", "e", "f", "g"] """, "before", "holds 7 paragraphs; a card holds at most 6")]
    public void ACardIsRefusedNamingTheMapAndTheParagraph(string card, string field, string message)
    {
        var e = Fails(card);

        Assert.Equal((ContentFiles.CampaignName, "one", field), (e.File, e.Entry, e.Field));
        Assert.Contains(message, e.Message);
    }

    [Fact]
    public void ACardParagraphLongerThanTheCapIsRefused()
    {
        var e = Fails($$""", "after": ["{{new string('a', ContentLoader.CardParagraphLength + 1)}}"] """);

        Assert.Equal("after[0]", e.Field);
        Assert.Contains($"is {ContentLoader.CardParagraphLength + 1} characters; a paragraph holds at most {ContentLoader.CardParagraphLength}", e.Message);
    }

    private static string Play(out int exit, string script, string seed)
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-alone-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, script);
        try
        {
            return Run(out exit, "play", "starting_alone", "--seed", seed, "--script", path, "--content", Fixture.RealContentDirectory());
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string Run(out int exit, params string[] args)
    {
        var code = 0;
        var output = ConsoleCapture.Run(() => code = Ironwake.Cli.Program.Main(args));
        exit = code;
        return output;
    }
}
