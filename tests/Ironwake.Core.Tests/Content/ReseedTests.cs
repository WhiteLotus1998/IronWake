using Ironwake.Content;
using Ironwake.Content.Protocol;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// <c>campaign --load save --reseed N</c> (issue 963, DECISIONS/0238): a save replayed on a new
/// seed, said on the first card. Rolls are keyed by the seed, so two chairs on one pinned save
/// read one set of dice; the reseed is the loud way to take a read of one's own.
/// </summary>
[Collection("console")]
public class ReseedTests
{
    private const ulong Reseed = 2;

    private static string Repo => Directory.GetParent(Fixture.RealContentDirectory())!.FullName;

    private static string OathSave => Path.Combine(Repo, "docs", "transcripts", "2026-10-04-the_oath_stone-1133.saves", "oath.json");

    private static GameContent Content => ContentLoader.Load(Fixture.RealContentDirectory());

    private static string TempDir() => Path.Combine(Path.GetTempPath(), "ironwake-reseed-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The pinned case of round 332: on the oath save (campaign seed 984, the Oath Stone's side-map
    /// seed 1133) Keziah's turn-8 swing at the envoy draws the same two rolls for every chair; the
    /// reseeded save's side map is on another seed and draws other rolls for the same strike.
    /// </summary>
    [Fact]
    public void AReseededSaveFightsItsNextBattleOnTheNewSeedsRolls()
    {
        var content = Content;
        var store = new SaveStore(Path.GetDirectoryName(OathSave)!);
        var saved = store.Load("oath", content).Record!;
        var reseeded = saved with { Seed = Reseed };

        var pinned = saved.QuestSeed("keziah_2", content);
        var fresh = reseeded.QuestSeed("keziah_2", content);

        Assert.Equal(984UL, saved.Seed);
        Assert.Equal(1133UL, pinned);
        Assert.Equal(Reseed + (pinned - saved.Seed), fresh);
        Assert.NotEqual(Rolls(pinned), Rolls(fresh));
    }

    [Fact]
    public void ReseedLoadsTheSaveUnchangedButItsSeedAndSaysSoOnTheFirstCardAndInTheLog()
    {
        var content = Content;
        var dir = SavesWithOath();
        var log = Path.Combine(dir, "events.log");
        try
        {
            var output = Run(out var exit, "record\nsave reseeded\n", "--saves", dir, "--load", "oath", "--reseed", Reseed.ToString(), "--log", log);
            var store = new SaveStore(dir);
            var saved = store.Load("oath", content).Record!;
            var written = store.Load("reseeded", content).Record!;

            Assert.Equal(1, exit);
            Assert.Contains("Campaign, seed 2, ", output);
            Assert.Contains("\nreseeded from 984 to 2: not the save's battle\n", output);
            Assert.Contains("reseeded from 984 to 2: not the save's battle\n", File.ReadAllText(log));
            Assert.Contains(ProtocolJson.Campaign(saved with { Seed = Reseed }) + "\n", output);
            Assert.Equal(ProtocolJson.Campaign(saved with { Seed = Reseed }), ProtocolJson.Campaign(written));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ASaveWrittenAfterAReseedCarriesTheNewSeedAndIsNotReseededAgain()
    {
        var dir = SavesWithOath();
        try
        {
            Run(out _, "save reseeded\n", "--saves", dir, "--load", "oath", "--reseed", Reseed.ToString());
            var again = Run(out _, "", "--saves", dir, "--load", "reseeded");

            Assert.Equal(Reseed, new SaveStore(dir).Load("auto-1", Content).Record!.Seed);
            Assert.Contains("Campaign, seed 2, ", again);
            Assert.DoesNotContain("reseeded from", again);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ReseedWithoutALoadOrResumeIsRefused()
    {
        var output = Run(out var exit, "", "--seed", "3", "--reseed", "9");

        Assert.Equal(2, exit);
        Assert.Contains("ERROR: --reseed 9 replays a save on a new seed; give --load or --resume\n", output);
        Assert.DoesNotContain("Campaign, seed", output);
    }

    [Fact]
    public void ReseedToTheSeedTheSaveAlreadyPinsIsRefused()
    {
        var dir = SavesWithOath();
        try
        {
            var output = Run(out var exit, "", "--saves", dir, "--load", "oath", "--reseed", "984");

            Assert.Equal(2, exit);
            Assert.Contains("ERROR: --reseed 984 is the seed the save already pins; give another\n", output);
            Assert.DoesNotContain("Campaign, seed", output);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ReseedOnAResumeWithBattleLinesIsRefusedAndTheSuspendIsKept()
    {
        var dir = TempDir();
        try
        {
            Run(out _, "march\nend\nquit\n", "--seed", "3", "--saves", dir);

            var refused = Run(out var exit, "", "--saves", dir, "--resume", "--reseed", "9");
            var resumed = Run(out _, "", "--saves", dir, "--resume");

            Assert.Equal(2, exit);
            Assert.Contains("ERROR: --reseed 9 would replay the suspended battle on other rolls; --load the camp's autosave and reseed that\n", refused);
            Assert.DoesNotContain("Campaign, seed", refused);
            Assert.Contains("Resumed Starting Alone at turn 2 from the suspend, 1 lines replayed; the suspend is deleted\n", resumed);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ReseedOnAResumeQuitBeforeAnyCommandStartsTheBattleOnTheNewSeed()
    {
        var dir = TempDir();
        try
        {
            Run(out _, "march\nquit\n", "--seed", "3", "--saves", dir);

            var output = Run(out _, "", "--saves", dir, "--resume", "--reseed", "9");

            Assert.Contains("Campaign, seed 9, ", output);
            Assert.Contains("\nreseeded from 3 to 9: not the save's battle\n", output);
            Assert.Contains("Resumed Starting Alone at turn 1 from the suspend, 0 lines replayed; the suspend is deleted\n", output);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SeedWithALoadNamesReseedAsTheWayToANewSeed()
    {
        var dir = SavesWithOath();
        try
        {
            var output = Run(out var exit, "", "--seed", "9", "--saves", dir, "--load", "oath");

            Assert.Equal(2, exit);
            Assert.Contains("ERROR: --seed 9 starts a new campaign; a loaded or resumed campaign keeps the seed its save pins; --reseed N replays it on a new seed\n", output);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static (int, int) Rolls(ulong seed)
    {
        var rng = new KeyedRng(seed);
        return (rng.Roll(RollKey.Combat(8, Side.Player, "keziah", "sworn_captain-1", 0, CombatRoll.HitA)),
            rng.Roll(RollKey.Combat(8, Side.Player, "keziah", "sworn_captain-1", 0, CombatRoll.HitB)));
    }

    private static string SavesWithOath()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        File.Copy(OathSave, Path.Combine(dir, "oath.json"));
        return dir;
    }

    private static string Run(out int exit, string script, params string[] extra)
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-reseed-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, script);
        try
        {
            var args = new[] { "campaign", "--script", path, "--content", Fixture.RealContentDirectory() }.Concat(extra).ToArray();
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
