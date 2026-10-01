using Ironwake.Content;
using Ironwake.Content.Protocol;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Saves and the fail menu (issue 663, DESIGN section 9): the autosave rotation keeps three, a
/// named save round-trips, a suspend resumes once and is then deleted, a scripted run keeps no
/// save unless <c>--saves</c> names a directory, and a lost map offers load, new game and quit.
/// </summary>
[Collection("console")]
public class CampaignSaveTests
{
    private static GameContent Content => ContentLoader.Load(Fixture.RealContentDirectory());

    private static string TempDir() => Path.Combine(Path.GetTempPath(), "ironwake-saves-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void TheAutosaveRotationKeepsTheLastThreeCampsNewestFirst()
    {
        var content = Content;
        var store = new SaveStore(TempDir());
        try
        {
            for (var purse = 100; purse <= 400; purse += 100)
            {
                store.Autosave(CampaignRecord.Start(content, 3) with { Purse = purse });
            }

            Assert.Equal(new[] { "auto-1", "auto-2", "auto-3" }, store.Names());
            Assert.Equal(400, store.Load("auto-1", content).Record!.Purse);
            Assert.Equal(300, store.Load("auto-2", content).Record!.Purse);
            Assert.Equal(200, store.Load("auto-3", content).Record!.Purse);
            Assert.Equal(3, Directory.GetFiles(store.Directory).Length);
        }
        finally
        {
            Directory.Delete(store.Directory, true);
        }
    }

    [Fact]
    public void ANamedSaveRoundTripsTheRecord()
    {
        var content = Content;
        var store = new SaveStore(TempDir());
        try
        {
            var record = CampaignRecord.Start(content, 77) with { Purse = 1234, MapIndex = 2 };

            Assert.Null(store.Save("before-the-weir", record));
            var (loaded, refusal) = store.Load("before-the-weir", content);

            Assert.Null(refusal);
            Assert.Equal(ProtocolJson.Campaign(record), ProtocolJson.Campaign(loaded!));
            Assert.Equal(new[] { "before-the-weir" }, store.Names());
        }
        finally
        {
            Directory.Delete(store.Directory, true);
        }
    }

    [Theory]
    [InlineData("auto-1")]
    [InlineData("")]
    [InlineData("Upper")]
    [InlineData("../escape")]
    [InlineData("a name")]
    public void ASaveNameOutsideTheRuleIsRefused(string name)
    {
        var store = new SaveStore(TempDir());

        Assert.NotNull(store.Save(name, CampaignRecord.Start(Content, 3)));
        Assert.False(Directory.Exists(store.Directory));
    }

    [Fact]
    public void AMissingSaveIsRefusedByName()
    {
        var store = new SaveStore(TempDir());

        var (record, refusal) = store.Load("nothing", Content);

        Assert.Null(record);
        Assert.Equal($"no save 'nothing' in {store.Directory}", refusal);
    }

    [Fact]
    public void ASuspendResumesOnceAndIsThenDeleted()
    {
        var content = Content;
        var store = new SaveStore(TempDir());
        try
        {
            store.Suspend(CampaignRecord.Start(content, 3), new[] { "end", "move captain 4,6" });

            var first = store.TakeSuspend(content);
            var second = store.TakeSuspend(content);

            Assert.Equal(new[] { "end", "move captain 4,6" }, first.Lines);
            Assert.NotNull(first.Record);
            Assert.False(store.HasSuspend);
            Assert.Null(second.Record);
            Assert.Equal($"no suspended battle in {store.Directory}", second.Refusal);
        }
        finally
        {
            Directory.Delete(store.Directory, true);
        }
    }

    [Fact]
    public void QuitInABattleSuspendsItAndResumeReplaysItToTheSameTurn()
    {
        var dir = TempDir();
        try
        {
            var quit = Run(out var quitExit, "march\nend\nend\nquit\n", "--saves", dir);
            var resumed = Run(out var resumedExit, "nonsense\n", "--saves", dir, "--resume", "--strict");
            var again = Run(out var againExit, "", "--saves", dir, "--resume");

            Assert.Equal(1, quitExit);
            Assert.Contains("Suspended in Starting Alone at turn 3: ironwake campaign --resume picks it up once, from " + dir + "\n", quit);
            Assert.Contains("Resumed Starting Alone at turn 3 from the suspend, 2 lines replayed; the suspend is deleted\n", resumed);
            Assert.Contains("Starting Alone  turn 3 of 10  player phase", resumed);
            Assert.DoesNotContain("-- Player phase ends, turn 1 --", resumed);
            Assert.Equal(3, resumedExit);
            Assert.DoesNotContain("Suspended", resumed);
            Assert.Equal(2, againExit);
            Assert.Contains($"ERROR: no suspended battle in {dir}\n", again);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void AResumeWithNoSuspendIsRefused()
    {
        var dir = TempDir();

        var output = Run(out var exit, "", "--saves", dir, "--resume");

        Assert.Equal(2, exit);
        Assert.Contains($"ERROR: no suspended battle in {dir}\n", output);
    }

    [Fact]
    public void EveryCampAutosavesAndASaveNamedThereLoadsWithLoad()
    {
        var dir = TempDir();
        try
        {
            var saved = Run(out _, "save first-camp\nsaves\n", "--saves", dir);
            var loaded = Run(out _, "roster\n", "--saves", dir, "--load", "first-camp");

            Assert.Contains("Autosaved as auto-1; the last 3 camps are kept\n", saved);
            Assert.Contains("> save first-camp\nSaved as first-camp: before map 1 of 9, starting_alone, the purse holds 500\n", saved);
            Assert.Contains("  auto-1: before map 1 of 9, starting_alone, the purse holds 500\n  first-camp: before map 1 of 9, starting_alone, the purse holds 500\n", saved);
            Assert.Contains("Campaign, seed 3, difficulty normal", loaded);
            Assert.Equal(new[] { "auto-1", "auto-2", "first-camp" }, new SaveStore(dir).Names());
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void AScriptedRunKeepsNoSaveUnlessSavesNamesADirectory()
    {
        var output = Run(out _, "save first-camp\nsaves\n");

        Assert.DoesNotContain("Autosaved", output);
        Assert.Contains("ERROR: Saves are off: a scripted run keeps none unless --saves names a directory\n", output);
        Assert.Contains("Saves: off; saves are off", output);
    }

    [Fact]
    public void ALostMapPlaysThePlaceholderCardAndOffersLoadNewGameAndQuit()
    {
        var dir = TempDir();
        try
        {
            var lose = "march\n" + string.Concat(Enumerable.Repeat("end\n", 10)) + "leave\n";
            var output = Run(out var exit, lose + "saves\nload nothing\nnew\nmarch\nquit\n", "--saves", dir);

            Assert.Equal(1, exit);
            Assert.Contains("Campaign lost on Starting Alone: turn 10 passed\n-- The company is lost (placeholder card) --\n", output);
            Assert.Contains("Load from save (load <name>; saves lists them), New game (new), or Quit (quit)\n", output);
            Assert.Contains("  auto-1: before map 1 of 9, starting_alone, the purse holds 500\n", output);
            Assert.Contains("ERROR: No save 'nothing' in " + dir + "\n", output);
            Assert.Contains("> new\nNew game, seed 3, difficulty normal\nAutosaved as auto-1", output);
            Assert.Contains("Suspended in Starting Alone at turn 1", output);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void LoadOnTheFailMenuGoesOnFromTheSave()
    {
        var dir = TempDir();
        try
        {
            var lose = "save start\nmarch\n" + string.Concat(Enumerable.Repeat("end\n", 10)) + "leave\n";
            var output = Run(out _, lose + "load start\nquit\n", "--saves", dir);

            Assert.Contains("> load start\nLoaded start: before map 1 of 9, starting_alone, the purse holds 500\nAutosaved as auto-1", output);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void QuitOnTheFailMenuEndsTheRunAndOtherWordsAreRefused()
    {
        var lose = "march\n" + string.Concat(Enumerable.Repeat("end\n", 10)) + "leave\n";

        var output = Run(out var exit, lose + "march\nload start\nquit\nmarch\n");

        Assert.Equal(1, exit);
        Assert.Contains("> march\nERROR: The campaign is lost; Load from save", output);
        Assert.Contains("> load start\nERROR: Saves are off", output);
        Assert.Contains("  line 13: march: The campaign is lost; Load from save (load <name>; saves lists them), New game (new), or Quit (quit)\n  line 14: load start: Saves are off: a scripted run keeps none unless --saves names a directory\n", output);
        Assert.DoesNotContain("> quit\n> march", output);
    }

    [Fact]
    public void QuitIsRefusedOnceTheBattleIsDecided()
    {
        var lose = "march\n" + string.Concat(Enumerable.Repeat("end\n", 10)) + "quit\n";

        var output = Run(out _, lose);

        Assert.Contains("> quit\nERROR: The battle is decided; leave it instead\n", output);
    }

    private static string Run(out int exit, string script, params string[] extra)
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-saves-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, script);
        try
        {
            var args = new[] { "campaign", "--seed", "3", "--script", path, "--content", Fixture.RealContentDirectory() }.Concat(extra).ToArray();
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
