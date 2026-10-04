using Ironwake.Content;
using Ironwake.Content.Protocol;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Scene scripts (issue 1001, WRITING.md process step 9): the plain-text format, its validation on
/// load, conditions over record facts, Lotus's line locks, and the campaign points a scene plays at.
/// </summary>
[Collection("console")]
public class SceneScriptTests
{
    private static readonly GameContent Real = ContentLoader.Load(Fixture.RealContentDirectory());

    private static string Repo => Directory.GetParent(Fixture.RealContentDirectory())!.FullName;

    private static string FixturePath => Path.Combine(Repo, "tests", "Ironwake.Core.Tests", "Content", "Scenes", "fixture_alone.txt");

    private static Scene FixtureScene() => SceneFormat.Parse(new ContentFile("scenes/fixture_alone.txt", File.ReadAllText(FixturePath)), Real);

    private static Scene Parse(string body, string id = "x") =>
        SceneFormat.Parse(new ContentFile($"scenes/{id}.txt", $"scene: {id}\nplays: after starting_alone\n\n{body}"), Real);

    private static ContentException Fails(string body) => Assert.Throws<ContentException>(() => Parse(body));

    [Fact]
    public void TheFixtureSceneLoadsItsSpeakersConditionsAndLock()
    {
        var scene = FixtureScene();

        Assert.Equal("fixture_alone", scene.Id);
        Assert.Equal(ScenePoint.Camp, scene.Point);
        Assert.Equal("starting_alone", scene.MapId);
        Assert.Equal(new[] { "f9" }, scene.Retired);
        Assert.Equal(new[] { "f1", "f2", "f3", "f4", "f5", "f6", "f7", "f8" }, scene.Lines.Select(l => l.Id));
        Assert.Equal(new[] { SceneScripts.Narration, "captain", "captain", "captain", "maud", "brigand", "keeper", SceneScripts.Rules }, scene.Lines.Select(l => l.Speaker));
        Assert.Equal(new SceneIncidental("keeper", "The keeper"), scene.Incidentals.Single());
        Assert.Null(scene.Lines[0].Condition);
        Assert.Equal(new SceneFact(SceneFactKind.Fallen, ValueList<string>.Of("maud"), false), scene.Lines[2].Condition!.Facts.Single());
        Assert.Equal(new SceneFact(SceneFactKind.Fallen, ValueList<string>.Of("maud"), true), scene.Lines[3].Condition!.Facts.Single());
        Assert.Equal(2, scene.Lines[4].Condition!.Facts.Count);
        Assert.Equal("9485", scene.Lines[3].Lock);
        Assert.Null(scene.Lines[5].Condition);
    }

    [Fact]
    public void ALineShowsOnlyWhileItsConditionAndItsBlocksHold()
    {
        var scene = FixtureScene();
        var start = CampaignRecord.Start(Real, 1);

        Assert.Equal(new[] { "f1", "f2", "f4", "f6", "f7", "f8" }, SceneScripts.Shown(scene, start, Real).Select(l => l.Id));
        Assert.Equal(new[] { "f1", "f2", "f3", "f6", "f7", "f8" }, SceneScripts.Shown(scene, start with { Fallen = ValueList<string>.Of("maud") }, Real).Select(l => l.Id));
        var supported = start with { Rapport = ValueList<Rapport>.Of(new Rapport("captain", "maud", 16)) };
        Assert.Equal(new[] { "f1", "f2", "f4", "f5", "f6", "f7", "f8" }, SceneScripts.Shown(scene, supported, Real).Select(l => l.Id));
    }

    [Fact]
    public void EachFactReadsItsOwnRecordField()
    {
        var start = CampaignRecord.Start(Real, 1);
        bool Holds(string condition, CampaignRecord record) =>
            SceneScripts.Shown(Parse($"a narration (if {condition}): x"), record, Real).Count == 1;

        Assert.True(Holds("pick keziah", start with { Pick = "keziah" }));
        Assert.False(Holds("pick keziah", start with { Pick = "rook" }));
        Assert.True(Holds("met ansgar", start with { Met = ValueList<string>.Of("ansgar") }));
        Assert.True(Holds("returned spared", start with { Returned = ClaimantFate.Spared }));
        Assert.False(Holds("returned spared", start));
        Assert.True(Holds("drake", start with { DrakeFlew = DrakeStage.Grown }));
        Assert.False(Holds("drake unbroken", start with { DrakeFlew = DrakeStage.Grown }));
        Assert.True(Holds("oath fed", start with { KeziahOath = OathSide.Fed }));
        Assert.True(Holds("quest maud_1", start with { QuestsWon = ValueList<QuestWon>.Of(new QuestWon("maud_1", 3)) }));
        Assert.True(Holds("freed-fell", start with { FreedUnitFell = true }));
        Assert.True(Holds("not freed-fell and not fallen maud", start));
        Assert.False(Holds("support captain maud B", start with { Rapport = ValueList<Rapport>.Of(new Rapport("captain", "maud", 16)) }));
        Assert.True(Holds("support captain maud B", start with { Rapport = ValueList<Rapport>.Of(new Rapport("captain", "maud", 28)) }));
    }

    [Fact]
    public void TheLockIsFourHexOfTheTrimmedText()
    {
        Assert.Equal("9485", SceneScripts.LockHash("The chaplain's name is still on it."));
        Assert.Equal(SceneScripts.LockHash("x"), SceneScripts.LockHash("  x  "));
        Assert.NotEqual(SceneScripts.LockHash("x"), SceneScripts.LockHash("y"));
        Assert.Matches("^[0-9a-f]{4}$", SceneScripts.LockHash("anything at all"));
    }

    [Fact]
    public void ALockedLineWhoseTextChangedFailsLoad()
    {
        var e = Fails("a captain (human:9485): The chaplain's name is still on it, mostly.");

        Assert.Equal("scenes/x.txt", e.File);
        Assert.Equal("a", e.Entry);
        Assert.Equal("lock", e.Field);
    }

    [Theory]
    [InlineData("a captain: Café again.", null, "plain ASCII")]
    [InlineData("a captain: one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen seventeen eighteen nineteen twenty twentyone twentytwo twentythree twentyfour twentyfive twentysix.", "text", "at most 25")]
    [InlineData("a captain: x\na maud: y", "id", "earlier line")]
    [InlineData("a nobody: x", "speaker", "not a unit id")]
    [InlineData("a captain (if raining): x", "if", "names no fact")]
    [InlineData("a captain (if fallen soldier): x", "if", "not in the cast")]
    [InlineData("a captain (if fallen): x", "if", "takes 1")]
    [InlineData("a captain (if support wren maud C): x", "if", "not a support pair")]
    [InlineData("a captain (if support wren pell S): x", "if", "not a support tier")]
    [InlineData("a captain (if returned forgiven): x", "if", "not one of")]
    [InlineData("a captain (if quest nowhere): x", "if", "not a quest")]
    [InlineData("a captain (if pick maud): x", "if", "no branch")]
    [InlineData("a captain (human:ZZZZ): x", "lock", "4 lowercase hex")]
    [InlineData("if fallen maud\na captain: x", "if", "never closed")]
    [InlineData("if fallen maud\nif fallen pell\nend", "if", "do not nest")]
    [InlineData("end", "end", "no block")]
    [InlineData("a captain: x\nbeat: late", "beat", "before the first line")]
    [InlineData("a captain (if fallen maud) (if fallen pell): x", "if", "one condition")]
    [InlineData("a captain (human:ad8b) (human:ad8b): x", "lock", "one lock")]
    [InlineData("a captain (if drake grown big): x", "if", "takes 0 or 1")]
    [InlineData("a captain (if freed-fell maud): x", "if", "takes 0")]
    [InlineData("a keeper: x", "speaker", "not a unit id")]
    [InlineData("just words", null, "not a header")]
    [InlineData("a captain:", null, "not a header")]
    public void ABadLineFailsLoadNamingTheFileTheEntryAndTheField(string body, string? field, string problem)
    {
        var e = Fails(body);

        Assert.Equal("scenes/x.txt", e.File);
        Assert.NotNull(e.Entry);
        Assert.Equal(field, e.Field);
        Assert.Contains(problem, e.Problem);
    }

    [Fact]
    public void ARetiredIdIsNeverReused()
    {
        var e = Assert.Throws<ContentException>(() =>
            SceneFormat.Parse(new ContentFile("scenes/x.txt", "scene: x\nplays: after starting_alone\nretired: a, b\n\na captain: x"), Real));

        Assert.Equal("a", e.Entry);
        Assert.Contains("retired", e.Problem);
    }

    [Fact]
    public void TheHeaderNamesTheFileAndACampaignMap()
    {
        Assert.Equal("scene", Assert.Throws<ContentException>(() => SceneFormat.Parse(new ContentFile("scenes/x.txt", "scene: y\nplays: after starting_alone\na captain: x"), Real)).Field);
        Assert.Equal("scene", Assert.Throws<ContentException>(() => SceneFormat.Parse(new ContentFile("scenes/x.txt", "plays: after starting_alone\na captain: x"), Real)).Field);
        Assert.Equal("plays", Assert.Throws<ContentException>(() => SceneFormat.Parse(new ContentFile("scenes/x.txt", "scene: x\na captain: x"), Real)).Field);
        Assert.Equal("plays", Assert.Throws<ContentException>(() => SceneFormat.Parse(new ContentFile("scenes/x.txt", "scene: x\nplays: after nowhere\na captain: x"), Real)).Field);
        Assert.Equal("plays", Assert.Throws<ContentException>(() => SceneFormat.Parse(new ContentFile("scenes/x.txt", "scene: x\nplays: during starting_alone\na captain: x"), Real)).Field);
        Assert.Equal("beat", Assert.Throws<ContentException>(() => SceneFormat.Parse(new ContentFile("scenes/x.txt", "scene: x\nbeat:\na captain: x"), Real)).Field);
        Assert.Equal("retired", Assert.Throws<ContentException>(() => SceneFormat.Parse(new ContentFile("scenes/x.txt", "scene: x\nretired: A-1\na captain: x"), Real)).Field);
        Assert.Contains("no lines", Assert.Throws<ContentException>(() => SceneFormat.Parse(new ContentFile("scenes/x.txt", "scene: x\nplays: after starting_alone\n"), Real)).Problem);
    }

    [Fact]
    public void ASceneOverFortyLinesNeedsTheBeatSheetThatArguedForIt()
    {
        var body = string.Concat(Enumerable.Range(1, SceneScripts.LinesMax + 1).Select(i => $"l{i} narration: Line {i}.\n"));

        Assert.Equal("beat", Fails(body).Field);
        var scene = SceneFormat.Parse(new ContentFile("scenes/x.txt", "scene: x\nplays: after starting_alone\nbeat: round 999\n" + body), Real);
        Assert.Equal(SceneScripts.LinesMax + 1, scene.Lines.Count);
        Assert.Equal("round 999", scene.Beat);
        Assert.Equal(SceneScripts.LinesMax, Parse(string.Concat(Enumerable.Range(1, SceneScripts.LinesMax).Select(i => $"l{i} narration: Line {i}.\n"))).Lines.Count);
    }

    [Fact]
    public void AnIncidentalSpeakerIsDeclaredOnceAndSpeaksAtMostTwice()
    {
        static Scene With(string header, string body) =>
            SceneFormat.Parse(new ContentFile("scenes/x.txt", $"scene: x\nplays: after starting_alone\n{header}\n{body}"), Real);

        Assert.Equal(2, With("incidental: girl = A girl at the well", "a girl: We're well.\nb girl: Thank you.").Lines.Count);
        var third = Assert.Throws<ContentException>(() => With("incidental: girl = A girl at the well", "a girl: One.\nb girl: Two.\nc girl: Three."));
        Assert.Equal(("c", "speaker"), (third.Entry, third.Field));
        Assert.Equal("incidental", Assert.Throws<ContentException>(() => With("incidental: maud = Not her", "a maud: x")).Field);
        Assert.Equal("incidental", Assert.Throws<ContentException>(() => With("incidental: rules = Nobody", "a narration: x")).Field);
        Assert.Equal("incidental", Assert.Throws<ContentException>(() => With("incidental: girl = One\nincidental: girl = Two", "a narration: x")).Field);
        Assert.Equal("incidental", Assert.Throws<ContentException>(() => With("incidental: girl", "a narration: x")).Field);
    }

    [Fact]
    public void RulesLinesSitOutsideTheBudgetAndTheWordCap()
    {
        var body = string.Concat(Enumerable.Range(1, SceneScripts.LinesMax).Select(i => $"l{i} narration: Line {i}.\n"))
            + "r1 rules: " + string.Join(' ', Enumerable.Repeat("word", 40)) + "\n";

        var scene = Parse(body);

        Assert.Equal(SceneScripts.LinesMax + 1, scene.Lines.Count);
        Assert.Equal("beat", Fails(body + "l41 narration: One more.\n").Field);
    }

    [Fact]
    public void NarrationTakesNoWordCap()
    {
        var long_ = string.Join(' ', Enumerable.Repeat("word", 40));

        Assert.Single(Parse($"a narration: {long_}").Lines);
    }

    [Fact]
    public void ASceneWritesToTextThatReadsBackEqual()
    {
        var scene = FixtureScene();
        var again = SceneFormat.Parse(new ContentFile("scenes/fixture_alone.txt", SceneFormat.Write(scene)), Real);

        Assert.Equal(scene, again);
        Assert.Contains("f5 maud (if not fallen maud and support maud captain C): You read it like a prayer.\n", SceneFormat.Write(scene));
    }

    [Fact]
    public void TheLoaderReadsTheScenesDirectoryAndTheSerializerWritesItBack()
    {
        var dir = CopyContentWith(("fixture_alone.txt", File.ReadAllText(FixturePath)));
        try
        {
            var content = ContentLoader.Load(dir);

            Assert.Equal(FixtureScene(), content.Scenes.Single());
            var files = ContentSerializer.Write(content);
            Assert.Equal("scenes/fixture_alone.txt", files.Scenes!.Single().Name);
            Assert.Equal(content.Scenes, ContentLoader.Parse(files).Scenes);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void TheShippedContentLoadsWithoutScenesAndPrintsNone()
    {
        var record = CampaignRecord.Start(Real, 1);

        Assert.Empty(Real.Scenes);
        Assert.Empty(Ironwake.Cli.CampaignSession.SceneLines(record, Real, ScenePoint.Camp, "starting_alone", "Starting Alone"));
    }

    [Fact]
    public void TheProtocolCarriesTheShownLinesWithTheirIdsAndSpeakers()
    {
        var scene = FixtureScene();
        var json = ProtocolJson.Scene(scene, SceneScripts.Shown(scene, CampaignRecord.Start(Real, 1), Real));

        Assert.StartsWith("{\"scene\":\"fixture_alone\",\"point\":\"camp\",\"map\":\"starting_alone\",\"lines\":[{\"id\":\"f1\",\"speaker\":\"narration\",\"text\":\"The road east is empty in both directions.\"}", json);
        Assert.Contains("{\"id\":\"f6\",\"speaker\":\"brigand\",\"text\":\"Keep walking.\"}", json);
        Assert.Contains("{\"id\":\"f8\",\"speaker\":\"rules\",", json);
        Assert.EndsWith("],\"incidental\":[{\"id\":\"keeper\",\"name\":\"The keeper\"}]}", json);
        Assert.DoesNotContain("\"f3\"", json);
    }

    /// <summary>
    /// The console prints a camp scene after the map's before card, a before scene after <c>march</c>
    /// and the map line, and an after scene after the after card; each line under its speaker's name,
    /// a narration line bare, and none of it in the event log.
    /// </summary>
    [Fact]
    public void TheCampaignPrintsEachSceneAtItsPoint()
    {
        var dir = CopyContentWith(
            ("fixture_alone.txt", File.ReadAllText(FixturePath)),
            ("fixture_march.txt", "scene: fixture_march\nplays: before starting_alone\n\nm1 captain: East, then.\n"),
            ("fixture_won.txt", "scene: fixture_won\nplays: after starting_alone\n\nw1 narration: The road is quiet again.\nw2 captain (if fallen captain): Never shown.\n"));
        var battle = File.ReadAllText(Path.Combine(Repo, "docs", "transcripts", "2026-10-01-starting_alone-631.script"));
        var path = Path.Combine(Path.GetTempPath(), "ironwake-scenes-" + Guid.NewGuid().ToString("N") + ".script");
        var log = Path.ChangeExtension(path, ".log");
        File.WriteAllText(path, "march\n" + battle + "leave\n");
        try
        {
            var code = 0;
            var output = ConsoleCapture.Run(() => code = Ironwake.Cli.Program.Main(new[] { "campaign", "--seed", "631", "--script", path, "--content", dir, "--log", log }));

            Assert.Contains("you have three.)\n\n-- Starting Alone --\nThe road east is empty in both directions.\nAlder Fenn: Nobody on the list has come.\nAlder Fenn: The chaplain's name is still on it.\nBrigand: Keep walking.\nThe keeper: Bodies off the road before dark.\n(forecast <unit> <target> prints a strike and its counter before you\ntake it.)\n\n-- Before map 1 of 10", output);
            Assert.Contains("\n-- Starting Alone --\nAlder Fenn: East, then.\n\n", output);
            Assert.True(output.IndexOf("Alder Fenn: East, then.", StringComparison.Ordinal) > output.IndexOf("-- Before map 1 of 10", StringComparison.Ordinal));
            Assert.Contains("by way of the mill.\n\n-- After Starting Alone --\nThe road is quiet again.\n\n-- The Mill --\n", output);
            Assert.DoesNotContain("Never shown", output);
            Assert.DoesNotContain("Keep walking", File.ReadAllText(log));
        }
        finally
        {
            File.Delete(path);
            File.Delete(log);
            Directory.Delete(dir, true);
        }
    }

    private static string CopyContentWith(params (string Name, string Text)[] scenes)
    {
        var source = Fixture.RealContentDirectory();
        var target = Path.Combine(Path.GetTempPath(), "ironwake-scenes-" + Guid.NewGuid().ToString("N"));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var to = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to);
        }

        Directory.CreateDirectory(Path.Combine(target, SceneFormat.Directory));
        foreach (var (name, text) in scenes)
        {
            File.WriteAllText(Path.Combine(target, SceneFormat.Directory, name), text);
        }

        return target;
    }
}
