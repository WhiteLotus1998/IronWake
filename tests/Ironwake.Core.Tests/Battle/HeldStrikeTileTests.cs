using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 1256: a striker whose only strike tile a side-mate holds at phase start is never
/// silently absent from <c>threat</c>. A holder that strikes from another tile of its own steps
/// off first, so the striker is priced from the freed tile, one wave deep, and counted
/// (<see cref="ThreatLine.FreedBy"/>); a holder that stays keeps the tile, so the striker prints
/// <c>(not counted: 4,3 held by ...)</c> and stays out of every total (<see cref="ThreatLine.HeldBy"/>).
/// The held board is the Bulwark trial's corridor (issue 253) with a brigand already on the door;
/// the freed board is Code's Lazar House play, seed 1590, turn 3.
/// </summary>
[Collection("console")]
public sealed class HeldStrikeTileTests
{
    private static readonly Coord Door = new(4, 3);

    private static (BattleState State, BattleUnit Captain, IReadOnlyList<ThreatLine> Lines) DoorHeld()
    {
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "trials", "bulwark_trial.map"), Starter);
        var state = BattleState.From(map, Starter, Starter.Cast, 12);
        state = state.WithUnit(state.Find("captain")! with { At = new Coord(5, 3), Hp = 25 });
        var holder = state.UnitsOf(Side.Enemy).First(u => u.Id.StartsWith("brigand", StringComparison.Ordinal));
        state = state.WithUnit(holder with { At = Door });
        var captain = state.Find("captain")!;
        return (state, captain, Queries.Threats(state, Starter, captain, captain.At)!);
    }

    [Fact]
    public void AStrikerWhoseOnlyTileASideMateKeepsIsListedHeldByIt()
    {
        var (state, _, lines) = DoorHeld();
        var holder = state.UnitAt(Door)!;

        var held = lines.Where(l => l.HeldBy is not null).ToList();

        Assert.NotEmpty(held);
        Assert.All(held, l => Assert.Equal(holder.Id, l.HeldBy!.Id));
        Assert.All(held, l => Assert.Equal(Door, l.From));
        Assert.Single(lines, l => l.HeldBy is null && l.Enemy.Id == holder.Id && l.From == Door);
    }

    [Fact]
    public void AHeldLineIsLeftOutOfTheTotalTheSeatAndTheLethalAsk()
    {
        var (state, captain, lines) = DoorHeld();
        var holderLine = lines.Single(l => l.HeldBy is null);

        Assert.Equal(holderLine.IfAllLand, Queries.IfAllLand(lines));
        Assert.All(lines.Select((l, i) => (l, i)).Where(p => p.l.HeldBy is not null), p => Assert.Null(Queries.CountedFrom(lines)[p.i]));

        var frail = state.WithUnit(captain with { Hp = holderLine.IfAllLand + 1 });
        Assert.Empty(Queries.Lethal(frail, Starter));
    }

    [Fact]
    public void ThreatPrintsAHeldLineAsNotCountedHeldByItsHolder()
    {
        var (state, captain, lines) = DoorHeld();
        var names = UnitNames.Of(state, Starter);
        var holder = state.UnitAt(Door)!;

        var text = PlaySession.ThreatText(state, Starter, captain, captain.At, lines, Array.Empty<SleepingThreat>());

        Assert.Contains($" from 4,3 (not counted: 4,3 held by {names[holder.Id]}) with ", text);
        Assert.Contains($"  If all land: {lines.Single(l => l.HeldBy is null).IfAllLand} against 25 hp", text);
    }

    [Fact]
    public void TheProtocolNamesTheHolder()
    {
        var (state, _, _) = DoorHeld();
        var holder = state.UnitAt(Door)!;
        var session = new ProtocolSession(Starter, state, new StringWriter());

        var answer = session.Answer("""{"query":"threat","unit":"captain"}""");

        Assert.Contains($"\"heldBy\":\"{holder.Id}\"", answer);
        Assert.DoesNotContain("\"freedBy\"", answer);
    }

    [Fact]
    public void AStrikerWhoseTileASideMateStepsOffIsPricedFromItAndCounted()
    {
        var script = Path.Combine(Path.GetTempPath(), "ironwake-1256-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllLines(script, new[]
        {
            "quest maud_1 dunstan", "move dunstan 4,1", "wait dunstan", "wait maud", "end",
            "move dunstan 5,1", "attack dunstan brigand-1", "move maud 4,1", "attack maud brigand-1", "end",
            "recall 9", "move dunstan 3,1", "wait dunstan", "wait maud", "end",
            "attack dunstan brigand-1", "attack maud archer-1", "threat dunstan",
        });
        string output;
        try
        {
            output = ConsoleCapture.Run(() => Program.Main(new[] { "campaign", "--from", "the_tollgate", "--seed", "1590", "--script", script, "--content", Fixture.RealContentDirectory() }));
        }
        finally
        {
            File.Delete(script);
        }

        Assert.Contains("  Brigand 2 from 4,1 with Iron Axe (slot 1): acc 73% dmg 8 crit 0%;", output);
        Assert.Contains("  Hexer (arrives this enemy phase at 8,0) (once Brigand 2 steps off 5,1) from 5,1 with Cinder (slot 1): acc 99% dmg 11 crit 1%; counter: none\n", output);
        Assert.Contains("  If all land: 19 against 23 hp\n", output);
    }
}
