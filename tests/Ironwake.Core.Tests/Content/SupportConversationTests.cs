using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Support conversations (issue 77 slice 8): a scene script that plays for a support pair at a tier,
/// heard at a camp with <c>support &lt;a&gt; &lt;b&gt;</c> once the pair has reached it, once each, lowest
/// tier first, recorded on the campaign record.
/// </summary>
public class SupportConversationTests
{
    private static readonly GameContent Real = ContentLoader.Load(Fixture.RealContentDirectory());

    private const string WrenPellC = "scene: wren_pell_c\nplays: support pell wren C\n\nc1 narration: A ledger on a drum.\nc2 wren: Wrong column.\nc3 pell (if fallen maud): Never shown.\n";

    private const string WrenPellB = "scene: wren_pell_b\nplays: support wren pell B\n\nb1 pell: Again.\n";

    private static Scene Parse(string text, string id) => SceneFormat.Parse(new ContentFile($"scenes/{id}.txt", text), Real);

    private static GameContent With(params (string Id, string Text)[] scenes) =>
        ContentLoader.Parse(ContentSerializer.Write(Real) with { Scenes = scenes.Select(s => new ContentFile($"scenes/{s.Id}.txt", s.Text)).ToList() });

    private static CampaignRecord Company(GameContent content, int rapport) =>
        CampaignRecord.Start(content, 1) is var start
            ? start with
            {
                Roster = start.Roster.Add(content.Unit("wren")).Add(content.Unit("pell")),
                Rapport = ValueList<Rapport>.Of(new Rapport("pell", "wren", rapport)),
            }
            : start;

    [Fact]
    public void ASupportScriptNamesItsPairInTheCampaignsOrderAndItsTier()
    {
        var scene = Parse(WrenPellC, "wren_pell_c");

        Assert.Equal(ScenePoint.Support, scene.Point);
        Assert.Equal(new SceneSupport("wren", "pell", "C"), scene.Support);
        Assert.Equal("", scene.MapId);
        Assert.StartsWith("scene: wren_pell_c\nplays: support wren pell C\n", SceneFormat.Write(scene));
        Assert.Equal(scene, Parse(SceneFormat.Write(scene), "wren_pell_c"));
    }

    [Theory]
    [InlineData("support wren maud C", "not a support pair")]
    [InlineData("support wren pell S", "not a support tier")]
    [InlineData("support wren pell", "two unit ids and a tier")]
    [InlineData("support wren nobody C", "not a support pair")]
    public void ASupportPlaysHeaderThatNamesNoPairOrNoTierFailsLoad(string plays, string problem)
    {
        var e = Assert.Throws<ContentException>(() => Parse($"scene: x\nplays: {plays}\n\na1 wren: Hm.\n", "x"));

        Assert.Equal("plays", e.Field);
        Assert.Contains(problem, e.Message);
    }

    [Fact]
    public void ASupportConversationOverTwentyLinesFailsLoadEvenWithABeatSheet()
    {
        string Lines(int n) => string.Concat(Enumerable.Range(1, n).Select(i => $"l{i} wren: Hm.\n"));
        var header = "scene: x\nplays: support wren pell C\nbeat: round 400\n\n";

        Assert.Equal(20, Parse(header + Lines(20) + "r1 rules: (a rules line.)\n", "x").Lines.Count(l => l.Speaker == "wren"));
        var e = Assert.Throws<ContentException>(() => Parse(header + Lines(21), "x"));
        Assert.Equal("plays", e.Field);
        Assert.Contains("21 lines, over a support conversation's 20", e.Message);
    }

    [Fact]
    public void TwoConversationsForOnePairAtOneTierFailLoad()
    {
        var e = Assert.Throws<ContentException>(() => With(("wren_pell_c", WrenPellC), ("wren_pell_c2", WrenPellC.Replace("wren_pell_c", "wren_pell_c2"))));

        Assert.Equal("plays", e.Field);
        Assert.Contains("a second conversation for wren and pell at C", e.Message);
    }

    [Fact]
    public void TheLowestReachedUnseenTierPlaysFirstAndEachPlaysOnce()
    {
        var content = With(("wren_pell_b", WrenPellB), ("wren_pell_c", WrenPellC));
        var record = Company(content, 28);

        var first = record.SeeSupport("pell", "Wren", content);
        Assert.True(first.Accepted, first.Text);
        Assert.Equal("Wren and Pell talk (support C)", first.Text);
        Assert.Equal(new[] { "wren_pell_c" }, first.Record.SupportsSeen);

        var second = first.Record.SeeSupport("wren", "pell", content);
        Assert.True(second.Accepted, second.Text);
        Assert.Equal(new[] { "wren_pell_c", "wren_pell_b" }, second.Record.SupportsSeen);

        var third = second.Record.SeeSupport("wren", "pell", content);
        Assert.False(third.Accepted);
        Assert.Equal("Wren and Pell have no conversation waiting", third.Text);
    }

    [Fact]
    public void ATierNotYetReachedWaits()
    {
        var content = With(("wren_pell_b", WrenPellB), ("wren_pell_c", WrenPellC));
        var record = Company(content, 16).SeeSupport("wren", "pell", content).Record;

        Assert.Equal("Wren and Pell have no conversation waiting", record.SeeSupport("wren", "pell", content).Text);
        Assert.Empty(SceneScripts.Waiting(content, record));
        Assert.Equal("wren_pell_b", SceneScripts.Waiting(content, record with { Rapport = ValueList<Rapport>.Of(new Rapport("pell", "wren", 28)) }).Single().Id);
    }

    [Fact]
    public void ASupportIsRefusedWithItsReason()
    {
        var content = With(("wren_pell_c", WrenPellC));
        var record = Company(content, 16);

        Assert.Equal("no unit 'nobody' on the roster", record.SeeSupport("nobody", "pell", content).Text);
        Assert.Equal("no unit 'maud' on the roster", record.SeeSupport("wren", "maud", content).Text);
        Assert.Equal("Wren and Wren are no support pair", record.SeeSupport("wren", "wren", content).Text);
        Assert.Equal("Wren and Pell have not reached support C", Company(content, 15).SeeSupport("wren", "pell", content).Text);
        Assert.Equal("Wren and Pell have no conversation waiting", Company(With(), 16).SeeSupport("wren", "pell", With()).Text);
        var fallen = record with { Roster = ValueList<Unit>.From(record.Roster.Where(u => u.Id != "pell")), Fallen = ValueList<string>.Of("pell") };
        Assert.Equal("no unit 'pell' on the roster", fallen.SeeSupport("wren", "pell", content).Text);
        Assert.Empty(SceneScripts.Waiting(content, fallen));
        Assert.All(new[] { "nobody", "maud" }, id => Assert.Equal(record, record.SeeSupport(id, "pell", content).Record));
    }

    [Fact]
    public void TheShippedWrenAndPellCPlaysAtCAndEndsOnTheQuire()
    {
        var scene = Real.Scenes.Single(s => s.Id == "wren_pell_c");
        var lines = CampaignSession.ConversationLines(Company(Real, 16), Real, scene);

        Assert.Equal(new SceneSupport("wren", "pell", "C"), scene.Support);
        Assert.InRange(scene.Lines.Count, 1, 20);
        Assert.Equal("-- Wren and Pell, support C --", lines[0]);
        Assert.Equal("Wren and Pell talk (support C)", Company(Real, 16).SeeSupport("pell", "wren", Real).Text);
        Assert.Contains("quire", scene.Lines[^1].Text);
    }

    [Fact]
    public void TheRosterNamesTheWaitingConversationAndItsCommand()
    {
        var content = With(("wren_pell_c", WrenPellC));
        var record = Company(content, 16);

        Assert.Equal(new[] { "Conversations: Wren and Pell C (support wren pell)" }, CampaignSession.ConversationWaitingLines(record, content));
        Assert.Empty(CampaignSession.ConversationWaitingLines(Company(content, 15), content));
        Assert.Empty(CampaignSession.ConversationWaitingLines(record.SeeSupport("wren", "pell", content).Record, content));
    }

    [Fact]
    public void AConversationPrintsUnderThePairAndTierEachShownLineUnderItsSpeaker()
    {
        var content = With(("wren_pell_c", WrenPellC));
        var record = Company(content, 16);

        Assert.Equal(
            new[] { "-- Wren and Pell, support C --", "A ledger on a drum.", "Wren: Wrong column.", "" },
            CampaignSession.ConversationLines(record, content, content.Scenes.Single()));
    }

    [Fact]
    public void TheRecordCarriesTheConversationsSeenAndRefusesOneTheContentLacks()
    {
        var content = With(("wren_pell_c", WrenPellC));
        var record = Company(content, 16).SeeSupport("wren", "pell", content).Record;
        var json = ProtocolJson.Campaign(record);

        Assert.Contains("\"supportsSeen\":[\"wren_pell_c\"]", json);
        Assert.Equal(record.SupportsSeen, ProtocolJson.ReadCampaign(json, content).SupportsSeen);
        Assert.DoesNotContain("supportsSeen", ProtocolJson.Campaign(Company(content, 16)));
        Assert.Empty(ProtocolJson.ReadCampaign(ProtocolJson.Campaign(Company(content, 16)), content).SupportsSeen);
        var e = Assert.Throws<ProtocolException>(() => ProtocolJson.ReadCampaign(json, With()));
        Assert.Contains("supportsSeen 'wren_pell_c' is not a support conversation", e.Message);
    }

    [Fact]
    public void TheProtocolCarriesAConversationsPairAndTierInPlaceOfAMap()
    {
        var scene = Parse(WrenPellC, "wren_pell_c");

        Assert.StartsWith("{\"scene\":\"wren_pell_c\",\"point\":\"support\",\"a\":\"wren\",\"b\":\"pell\",\"tier\":\"C\",\"lines\":[", ProtocolJson.Scene(scene, scene.Lines));
    }
}

/// <summary>The <c>support</c> command at a camp (issue 77 slice 8), driven through the console from a save.</summary>
[Collection("console")]
public class SupportCommandTests
{
    private const string WrenPellC = "scene: wren_pell_c\nplays: support wren pell C\n\nc1 narration: A ledger on a drum.\nc2 wren: Wrong column.\n";

    [Fact]
    public void TheCampListsTheConversationAndSupportPlaysItOnceOutsideTheLog()
    {
        var source = Fixture.RealContentDirectory();
        var dir = Path.Combine(Path.GetTempPath(), "ironwake-support-" + Guid.NewGuid().ToString("N"));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var to = Path.Combine(dir, "content", Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to);
        }

        Directory.CreateDirectory(Path.Combine(dir, "content", SceneFormat.Directory));
        File.WriteAllText(Path.Combine(dir, "content", SceneFormat.Directory, "wren_pell_c.txt"), WrenPellC);
        var content = ContentLoader.Load(Path.Combine(dir, "content"));
        var start = CampaignRecord.Start(content, 1);
        var record = start with
        {
            Roster = start.Roster.Add(content.Unit("wren")).Add(content.Unit("pell")),
            Rapport = ValueList<Rapport>.Of(new Rapport("pell", "wren", 16)),
        };
        Assert.Null(new SaveStore(Path.Combine(dir, "saves")).Save("pair", record));
        var script = Path.Combine(dir, "run.script");
        var log = Path.Combine(dir, "events.log");
        File.WriteAllText(script, "support wren pell\nsupport pell wren\nsupport wren\n");
        try
        {
            var output = ConsoleCapture.Run(() => Ironwake.Cli.Program.Main(new[]
            {
                "campaign", "--script", script, "--content", Path.Combine(dir, "content"), "--saves", Path.Combine(dir, "saves"), "--load", "pair", "--log", log,
            }));

            Assert.Contains("Conversations: Wren and Pell C (support wren pell)\n", output);
            Assert.Contains("Wren and Pell talk (support C)\n-- Wren and Pell, support C --\nA ledger on a drum.\nWren: Wrong column.\n\n", output);
            Assert.Contains("ERROR: Pell and Wren have no conversation waiting\n", output);
            Assert.Contains("ERROR: Usage: support <unit> <unit>\n", output);
            Assert.Contains("Wren and Pell talk (support C)", File.ReadAllText(log));
            Assert.DoesNotContain("Wrong column", File.ReadAllText(log));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
