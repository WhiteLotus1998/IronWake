using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 1097: <c>docs/samples/saltmarsh_ford_talk.map</c>, the board that makes Wren's talk
/// (DESIGN.md 13.18) decidable. It is the signature sample widened four columns west, with a
/// sleeping guard pair of riders (group <c>marsh</c>) placed 7 or 8 tiles from where the ford fight
/// lands, so a combat there wakes the pair only when Wren is in it.
/// </summary>
public class TalkSampleTests
{
    private static string Repo => Directory.GetParent(Fixture.RealContentDirectory())!.FullName;

    private static string SamplePath => Path.Combine(Repo, "docs", "samples", "saltmarsh_ford_talk.map");

    private static MapDefinition Sample => MapFiles.Load(SamplePath, MapFixture.Content);

    /// <summary>The ford fight's tiles the issue's 200-seed sweep puts most at (the bridge foot) and the tiles Chat's 4611 ford pair landed on, in this board's columns.</summary>
    private static readonly Coord[] FordFight =
    [
        new(7, 4), new(7, 5), new(8, 5), new(8, 6), new(9, 6), new(8, 7),
    ];

    private static IReadOnlyList<Coord> Pair => Sample.Placements.OfType<EnemyPlacement>().Where(p => p.Group == "marsh").Select(p => p.At).ToList();

    private static int ToPair(Coord tile) => Pair.Min(m => m.DistanceTo(tile));

    [Fact]
    public void TheTalkSampleIsTheSignatureSampleFourColumnsWiderWithTheMarshPairAdded()
    {
        var signatures = MapFiles.Load(Path.Combine(Repo, "docs", "samples", "saltmarsh_ford_brace_signatures.map"), MapFixture.Content);
        var talk = Sample;

        Assert.Equal(signatures.Width + 4, talk.Width);
        Assert.Equal(signatures.Height, talk.Height);
        Assert.True(talk.SignaturesEnabled);
        Assert.Equal(
            signatures.Placements.Select(p => p.At with { X = p.At.X + 4 }),
            talk.Placements.Where(p => p is not EnemyPlacement { Group: "marsh" }).Select(p => p.At));
        var pair = talk.Placements.OfType<EnemyPlacement>().Where(p => p.Group == "marsh").ToList();
        Assert.Equal(2, pair.Count);
        Assert.All(pair, p => Assert.Equal(Behavior.Guard, p.Behavior));
        Assert.Equal(File.ReadAllText(SamplePath).Replace("\r\n", "\n"), MapFormat.Write(talk, MapFixture.Content));
    }

    [Fact]
    public void EveryFordFightTileIsInsideTheTalkAndOutsideTheNoise()
    {
        Assert.All(FordFight, tile => Assert.InRange(ToPair(tile), MapFixture.Content.NoiseRadius + 1, Signatures.TalkRadius));
    }

    [Fact]
    public void TheMarshPairIsOutOfReachOfTheDeployAndTheFortsRadii()
    {
        var map = Sample;
        Assert.All(map.Placements.OfType<PlayerPlacement>(), p => Assert.True(ToPair(p.At) > MapFixture.Content.WakeRadius));
        Assert.All(map.Placements.OfType<EnemyPlacement>().Where(p => p.Group == "fort"), p => Assert.True(ToPair(p.At) > Signatures.TalkRadius));
    }

    [Fact]
    public void AFordFightWakesTheMarshPairOnlyWhenWrenIsInIt()
    {
        var content = MapFixture.Content;
        var state = BattleState.From(Sample, content, content.Cast, 1097, RollScheme.TwoRollAverage);
        var none = new List<string>();

        var quiet = WakeCheck.Run(state, state, content, WakeCheck.At(content, new Coord(7, 4), new Coord(7, 5)), none);
        var talk = WakeCheck.Run(state, state, content, [new Noise(new Coord(7, 4), Signatures.TalkRadius), new Noise(new Coord(7, 5), Signatures.TalkRadius)], none);

        Assert.DoesNotContain(quiet, w => w.Group == "marsh");
        Assert.Contains(talk, w => w.Group == "marsh" && w.Cause == WakeCause.Noise);
    }
}
