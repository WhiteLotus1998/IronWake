using Ironwake.Cli;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// Issue 1308, slice 2: the drake's Carry and Breathe reached with the mouse. Each is a row of the
/// action list that arms a pick taken in steps (the ally, where to fly, where to set it down; the
/// breath's first tile), and the parity gate drives both through the rows and clicks. The carry's
/// parity script is the journaled one with turn 2's <c>end</c> confirmed as <c>end !</c>, the
/// console's lethal ask (issue 1093) having come after it; it sits under <c>tests/parity/clicks</c>,
/// outside godot-parity's glob, as <c>play</c> resolves map names and not samples.
/// </summary>
[Collection("console")]
public class ClientDrakeTests
{
    private static string Repo => Directory.GetParent(Fixture.RealContentDirectory())!.FullName;

    private static GameContent Content => ContentLoader.Load(Fixture.RealContentDirectory());

    private static string SamplePath(string name) => Path.Combine(Repo, "docs", "samples", name + ".map");

    private static ClientSession Open(string sample, Func<BattleState, BattleState>? edit = null)
    {
        var content = Content;
        var state = BattleState.From(MapFiles.Load(SamplePath(sample), content), content, content.Cast, 8051);
        return new ClientSession(content, edit is null ? state : edit(state));
    }

    private static string ConsoleLog(string path, ulong seed, string script)
    {
        var log = Path.Combine(Path.GetTempPath(), $"ironwake-parity-{Guid.NewGuid():N}.log");
        try
        {
            ConsoleCapture.Run(() => PlaySession.Run(new[]
            {
                path, "--seed", seed.ToString(), "--script", script,
                "--content", Fixture.RealContentDirectory(), "--log", log,
            }));
            return File.ReadAllText(log);
        }
        finally
        {
            File.Delete(log);
        }
    }

    [Theory]
    [InlineData("kestrow_water_carry", "tests/parity/clicks/kestrow_water_carry-8051.script", "'s drake carries ")]
    [InlineData("kestrow_water_rime", "docs/transcripts/2026-10-05-kestrow_water_rime-8051.script", " breathes ")]
    public void ClientEventLogMatchesTheConsoleWithCarryAndBreathTakenByClicks(string sample, string script, string mark)
    {
        var path = SamplePath(sample);
        var file = Path.Combine(Repo, script);
        var console = ConsoleLog(path, 8051, file);
        var client = Open(sample);
        Script.ApplyByClicks(client, File.ReadAllText(file));

        Assert.Contains(mark, console);
        Assert.Null(Parity.FirstDifference(console, client.LogText));
    }

    private static ActionRow Row(ClientSession client, string label) => Assert.Single(client.Actions(), row => row.Label == label);

    [Fact]
    public void TheCarryRowArmsThreeStepsAndTheLastClickCarries()
    {
        var client = Open("kestrow_water_carry");
        client.Select(new Coord(6, 9));
        var row = Row(client, "Carry");
        Assert.True(row.Legal);
        client.TakeAction(client.Actions().ToList().IndexOf(row));

        var lift = Assert.IsType<StepPick>(client.Pick);
        Assert.True(lift.Marks(new Coord(5, 9)));
        Assert.True(lift.Marks(new Coord(6, 10)));
        Assert.False(lift.Marks(new Coord(7, 9)));
        Assert.Equal(new[] { "lift Maud; then click where to fly" }, client.PickPreview(new Coord(6, 10)));

        Assert.Null(client.Click(new Coord(6, 10)));
        var fly = Assert.IsType<StepPick>(client.Pick);
        Assert.True(fly.Marks(new Coord(8, 8)));
        Assert.Null(client.Click(new Coord(8, 8)));

        var land = Assert.IsType<StepPick>(client.Pick);
        Assert.True(land.Last);
        Assert.True(land.Marks(new Coord(9, 8)));
        Assert.Contains(client.PickPreview(new Coord(9, 8)), line => line.Contains("'s drake carries Maud", StringComparison.Ordinal));

        Assert.Equal(new Carry("rook", "maud", new Coord(8, 8), new Coord(9, 8)), client.Click(new Coord(9, 8)));
        Assert.Null(client.Pick);
        Assert.Equal(new Coord(9, 8), client.State.Find("maud")!.At);
    }

    [Fact]
    public void AClickOffACarryStepClosesThePickAndCarriesNothing()
    {
        var client = Open("kestrow_water_carry");
        client.Select(new Coord(6, 9));
        client.TakeAction(client.Actions().ToList().IndexOf(Row(client, "Carry")));
        client.Click(new Coord(6, 10));

        Assert.Null(client.Click(new Coord(0, 11)));
        Assert.Null(client.Pick);
        Assert.Equal(new Coord(6, 10), client.State.Find("maud")!.At);
    }

    [Fact]
    public void TheCarryRowIsGreyedWithNoAllyBeside()
    {
        var client = Open("kestrow_water_carry", s => s.WithUnit(s.Find("rook")! with { At = new Coord(1, 9) }));
        client.Select(new Coord(1, 9));

        Assert.Equal("no ally beside it to carry", Row(client, "Carry").Refusal);
    }

    [Fact]
    public void TheBreathRowPicksTheFirstTileAndTheHoverMarksTheLine()
    {
        var client = Open("kestrow_water_rime");
        client.Select(new Coord(6, 9));
        client.TakeAction(client.Actions().ToList().IndexOf(Row(client, "Breathe")));

        var pick = Assert.IsType<StepPick>(client.Pick);
        Assert.True(pick.Marks(new Coord(6, 8)));
        Assert.False(pick.Marks(new Coord(6, 7)));
        Assert.Equal(new[] { new Coord(6, 8), new Coord(6, 7), new Coord(6, 6) }, client.PickArea(new Coord(6, 8)));
        Assert.Empty(client.PickArea(new Coord(6, 7)));

        Assert.Equal(new Breathe("rook", new Coord(6, 8)), client.Click(new Coord(6, 8)));
        Assert.True(client.State.Find("rook")!.Breathed);
    }

    [Fact]
    public void TheBreathRowIsGreyedWithTheCoresRefusalOnceTheDrakeHasBreathed()
    {
        var client = Open("kestrow_water_rime", s => s.WithUnit(s.Find("rook")! with { Breathed = true }));
        client.Select(new Coord(6, 9));

        Assert.Contains("once a map", Row(client, "Breathe").Refusal);
    }

    [Fact]
    public void AUnitWithNoDrakeHasNoDrakeRows()
    {
        var client = Open("kestrow_water_carry");
        client.Select(new Coord(5, 9));

        Assert.DoesNotContain(client.Actions(), row => row.Label is "Carry" or "Breathe");
    }
}
