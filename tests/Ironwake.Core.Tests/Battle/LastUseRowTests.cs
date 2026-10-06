using Ironwake.Content;
using Ironwake.Cli;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 1200: <c>threat</c>'s last-use row counts every strike the listed counters could throw
/// with the unit's spell, a double as two, and on a <c>one_answer: on</c> map only the first line's.
/// The board is the Mill's pre-<c>holds:</c> copy with Maud on her fort at 8,5, struck by the archer
/// and the brigand once the captain has gone south (Code's 1480 opening).
/// </summary>
public sealed class LastUseRowTests
{
    private static (BattleState State, BattleUnit Maud, IReadOnlyList<ThreatLine> Lines) Fort(int radianceUses, bool oneAnswer = false)
    {
        var repo = Directory.GetParent(Ironwake.Core.Tests.Content.Fixture.RealContentDirectory())!.FullName;
        var map = MapFiles.Load(Path.Combine(repo, "docs", "samples", "the_mill_0278.map"), Starter);
        var state = BattleState.From(map with { OneAnswerEnabled = oneAnswer }, Starter, Starter.Cast, 1480);
        var maud = state.Find("maud")!;
        var slot = maud.EquippedSlot(Starter);
        maud = maud with { Unit = maud.Unit with { Inventory = maud.Unit.Inventory.Replace(slot, maud.Unit.Inventory.Items[slot] with { Uses = radianceUses }) } };
        state = state.WithUnit(maud);
        foreach (var enemy in new[] { ("archer-2", new Coord(7, 6)), ("brigand-1", new Coord(9, 5)) })
        {
            state = state.WithUnit(state.Find(enemy.Item1)! with { At = enemy.Item2 });
        }

        return (state, maud, Queries.Threats(state, Starter, maud, maud.At)!);
    }

    [Fact]
    public void TheRowAppearsWhenTheListedCountersCouldSpendEveryUseLeft()
    {
        var (state, maud, lines) = Fort(2);
        Assert.Equal(2, lines.Count(l => l.Forecast.Defender.Strikes));
        Assert.Equal(2, lines.Sum(l => l.Forecast.Defender.StrikeCount));

        Assert.Equal("  counters could spend Radiance's last use", PlaySession.LastUseRow(state, maud, lines, Starter));
    }

    [Fact]
    public void TheRowStaysQuietWhileAUseWouldBeLeftOver()
    {
        var (state, maud, lines) = Fort(3);

        Assert.Null(PlaySession.LastUseRow(state, maud, lines, Starter));
    }

    [Fact]
    public void OnAOneAnswerMapOnlyTheFirstCounterSpends()
    {
        var (state, maud, lines) = Fort(2, oneAnswer: true);
        Assert.Equal(2, lines.Count(l => l.Forecast.Defender.Strikes));

        Assert.Null(PlaySession.LastUseRow(state, maud, lines, Starter));
        var (lastState, lastMaud, lastLines) = Fort(1, oneAnswer: true);
        Assert.Equal("  counters could spend Radiance's last use", PlaySession.LastUseRow(lastState, lastMaud, lastLines, Starter));
    }

    [Fact]
    public void AUnitWithNoSpellInFrontGetsNoRow()
    {
        var (state, _, _) = Fort(2);
        var captain = state.Find("captain")! with { At = new Coord(8, 6) };
        state = state.WithUnit(captain);
        var lines = Queries.Threats(state, Starter, captain, captain.At)!;
        Assert.Contains(lines, l => l.Forecast.Defender.Strikes);

        Assert.Null(PlaySession.LastUseRow(state, captain, lines, Starter));
    }
}
