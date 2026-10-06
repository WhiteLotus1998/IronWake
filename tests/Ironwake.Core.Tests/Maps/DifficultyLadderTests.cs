using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Maps;

/// <summary>
/// The difficulty ladder (issue 664, DESIGN section 9): Recruit, Captain and Tactician as data in
/// <c>rules.json</c>. Captain is <c>normal</c> by another name and stays the identity; Recruit adds
/// a Recall charge to each map's own and weakens every enemy; Tactician takes a charge and raises
/// the enemy level, and opens only once a campaign is won on Captain, a win the profile beside
/// the saves keeps outside every record.
/// </summary>
[Collection("console")]
public class DifficultyLadderTests
{
    private static GameContent Content => MapFixture.Content;

    private static string TempDir() => Path.Combine(Path.GetTempPath(), "ironwake-ladder-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void TheShippedLadderIsRecruitCaptainTactician()
    {
        Assert.Equal("Captain", Content.Difficulty("normal").DisplayName);
        Assert.Equal("Recruit", Content.Difficulty("recruit").DisplayName);
        Assert.Equal("Tactician", Content.Difficulty("tactician").DisplayName);
        Assert.Equal(1, Content.Difficulty("recruit").RecallOffset);
        Assert.Equal(-1, Content.Difficulty("tactician").RecallOffset);
        Assert.True(Content.Difficulty("tactician").EnemyLevelOffset > 0);
        Assert.All(Stats.All, s => Assert.True(Content.Difficulty("recruit").StatPercent.Get(s) < 100));
        Assert.All(Stats.All, s => Assert.True(Content.Difficulty("tactician").StatPercent.Get(s) > 100));
        Assert.Null(Content.Difficulty("recruit").UnlockedBy);
        Assert.Equal("normal", Content.Difficulty("tactician").UnlockedBy);
        Assert.True(Content.Difficulty("normal").IsIdentity);
    }

    [Fact]
    public void ARecallOffsetMovesEachMapsOwnChargesHeldToZeroAndNinetyNine()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad);
        var more = new Difficulty("more", Difficulty.FullPercent, 0, null) { RecallOffset = 1 };
        var fewer = new Difficulty("fewer", Difficulty.FullPercent, 0, null) { RecallOffset = -1 };
        var none = new Difficulty("none", Difficulty.FullPercent, 0, null) { RecallOffset = -99 };

        Assert.Equal(map.RecallCharges + 1, map.Under(more).RecallCharges);
        Assert.Equal(map.RecallCharges - 1, map.Under(fewer).RecallCharges);
        Assert.Equal(0, map.Under(none).RecallCharges);
        Assert.Equal(99, (map with { RecallCharges = 99 }).Under(more).RecallCharges);
    }

    [Fact]
    public void RecruitWeakensEveryEnemyAndTacticianRaisesTheirLevel()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad);
        var template = map.Placements.OfType<EnemyPlacement>().First();

        var captain = map.EnemyUnit(template, Content);
        var recruit = map.Under(Content.Difficulty("recruit")).EnemyUnit(template, Content);
        var tactician = map.Under(Content.Difficulty("tactician"));

        Assert.True(recruit.Stats.Hp < captain.Stats.Hp);
        Assert.Equal(Difficulty.Scale(captain.Stats.Str, Content.Difficulty("recruit").StatPercent.Str), recruit.Stats.Str);
        Assert.Equal(map.EnemyLevel + Content.Difficulty("tactician").EnemyLevelOffset, tactician.EnemyLevel);
    }

    [Fact]
    public void ADifficultyWithNoUnlockIsOpenAndTacticianOpensOnACaptainWin()
    {
        var tactician = Content.Difficulty("tactician");

        Assert.True(Content.Difficulty("recruit").IsUnlocked(Array.Empty<string>()));
        Assert.False(tactician.IsUnlocked(Array.Empty<string>()));
        Assert.False(tactician.IsUnlocked(new[] { "recruit" }));
        Assert.True(tactician.IsUnlocked(new[] { "normal" }));
    }

    [Fact]
    public void TheProfileRecordsEachDifficultyWonOnceOutsideTheSaves()
    {
        var store = new SaveStore(TempDir());
        try
        {
            Assert.Empty(store.Won());
            Assert.True(store.RecordWin("normal"));
            Assert.False(store.RecordWin("normal"));
            Assert.True(store.RecordWin("recruit"));

            Assert.Equal(new[] { "normal", "recruit" }, store.Won());
            Assert.Empty(store.Names());
        }
        finally
        {
            Directory.Delete(store.Directory, true);
        }
    }

    [Fact]
    public void TacticianIsRefusedWhileTheProfileHoldsNoCaptainWin()
    {
        var saves = TempDir();
        try
        {
            var output = Run(out var exit, "--difficulty", "tactician", "--saves", saves);

            Assert.Equal(2, exit);
            Assert.Contains("ERROR: Tactician unlocks when a campaign is won on Captain", output);

            new SaveStore(saves).RecordWin("normal");
            var opened = Run(out _, "--difficulty", "tactician", "--saves", saves);

            Assert.Contains("Campaign, seed 3, difficulty Tactician, permadeath on", opened);
        }
        finally
        {
            if (Directory.Exists(saves))
            {
                Directory.Delete(saves, true);
            }
        }
    }

    [Fact]
    public void AScriptedRunWithoutSavesKeepsNoProfileAndPlaysEveryDifficulty()
    {
        var output = Run(out _, "--difficulty", "Tactician", "--permadeath", "off");

        Assert.Contains("Campaign, seed 3, difficulty Tactician, permadeath off", output);
    }

    [Fact]
    public void AnUnknownDifficultyListsTheLadderByIdAndName()
    {
        var output = Run(out var exit, "--difficulty", "hard");

        Assert.Equal(2, exit);
        Assert.Contains("ERROR: no difficulty 'hard'; the content declares normal (Captain), recruit (Recruit), tactician (Tactician)", output);
    }

    /// <summary>
    /// Code's journaled play (issue 664): the Lazar House with permadeath off brings Wren back
    /// Wounded (2), she fights the Tollgate wounded, and its win counts her down to Wounded (1), and the camp drills her to the levy floor (issue 1164).
    /// </summary>
    [Fact]
    public void TheJournaledWoundedTollgateReplaysToItsTranscript()
    {
        var transcripts = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "transcripts");
        var script = Path.Combine(transcripts, "2026-10-01-the_tollgate-664-wounded.script");
        var args = new[] { "campaign", "--from", "the_tollgate", "--seed", "701", "--permadeath", "off", "--script", script, "--strict", "--content", Fixture.RoomlessContentDirectory() };

        var output = ConsoleCapture.Run(() => Ironwake.Cli.Program.Main(args));

        Assert.Contains("Maud wins maud_1; fell and came back wounded: Wren\n", output);
        Assert.Contains("    Wounded (2): Str -2, Spd -2 for 2 more main maps\n", output);
        Assert.Contains("The Tollgate won: seize; reward 1000, the purse holds 1500; nobody fell\n", output);
        Assert.Contains("  Wren: Cadet L3, EXP 0, Wounded (1); ", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    private static string Run(out int exit, params string[] extra)
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-ladder-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, "quit\n");
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
