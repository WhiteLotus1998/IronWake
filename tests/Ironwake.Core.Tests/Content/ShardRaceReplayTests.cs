using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Code's warm play of the shard race (issue 1386, slice 1) on <c>docs/samples/ironwake_keep_shard_race.map</c>, seed
/// 1424 at L8 with the full company: the 1424 chair's script to the turn-10 fall, then the race by hand. Hask goes down
/// on 0,5 beside his hold; nobody unspent can reach a tile beside him that phase, so the race runs one enemy phase,
/// and on turn 11 the captain takes the shard from beside him and the map is won with him in the coma.
/// </summary>
[Collection("console")]
public class ShardRaceReplayTests
{
    [Fact]
    public void TheShardRaceReplayOnSeed1424IsWonByTheCaptainsTakeOnTurn11()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-09-ironwake_keep_shard_race-1424.script");
        var map = Path.Combine(root, "docs", "samples", "ironwake_keep_shard_race.map");
        var args = new[] { "play", map, "--seed", "1424", "--level", "8", "--strict", "--script", script, "--content", Fixture.RealContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("Hask does not swallow: he stays on his knees at 0,5 with the shard in his fist. Swallows in 5 (take it from beside him)\n", output);
        Assert.Contains("Hask holds the shard: swallows in 4\n", output);
        Assert.Contains("Alder Fenn wrenches the shard from Hask and breaks it on the stone.", output);
        Assert.Contains("Battle won: defeat_boss\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(root, "docs", "transcripts", "2026-10-09-ironwake_keep_shard_race-1424.txt")).ReplaceLineEndings("\n"), output);
    }
}
