using Ironwake.Content;
using Ironwake.Cli;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 1191 (rounds 404 and 405): when two strikers share the one tile they can strike from,
/// <c>threat</c> seats one of them; if the unit's own counter can kill the seated one, the tile is
/// freed and the other takes it. <see cref="Queries.FreedStrikes"/> prices that one wave deep,
/// <c>threat</c> prints it under its total, and <see cref="Queries.Lethal"/> asks on it at any
/// counter chance, printing the chance. The board is issue 253's: the Bulwark trial's corridor,
/// the captain on 5,3, both brigands striking only from 4,3, at 1 HP so a counter kills, or at full HP so none does.
/// </summary>
public sealed class FreedTileTests
{
    private static readonly Coord Door = new(4, 3);

    private static (BattleState State, IReadOnlyList<ThreatLine> Lines) Corridor(int? brigandHp, Func<IReadOnlyList<ThreatLine>, int> captainHp)
    {
        var map = MapFiles.Load(Path.Combine(Ironwake.Core.Tests.Content.Fixture.RealContentDirectory(), "trials", "bulwark_trial.map"), Starter);
        var state = BattleState.From(map, Starter, Starter.Cast, 12);
        state = state.WithUnit(state.Find("captain")! with { At = new Coord(5, 3) });
        if (brigandHp is { } hp)
        {
            foreach (var line in Queries.Threats(state, Starter, state.Find("captain")!, new Coord(5, 3))!)
            {
                state = state.WithUnit(state.Find(line.Enemy.Id)! with { Hp = hp });
            }
        }

        var lines = Queries.Threats(state, Starter, state.Find("captain")!, new Coord(5, 3))!;
        state = state.WithUnit(state.Find("captain")! with { Hp = captainHp(lines) });
        return (state, Queries.Threats(state, Starter, state.Find("captain")!, new Coord(5, 3))!);
    }

    [Fact]
    public void ACounterKillThatFreesTheOneWayInLetsTheNextStrikerIn()
    {
        var (state, lines) = Corridor(1, ls => ls.Max(l => l.IfAllLand) + 1);
        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.True(l.Forecast.CounterIsLethal(l.Enemy.Hp, state.Find("captain")!.Hp)));

        var freed = Assert.Single(Queries.FreedStrikes(lines, state.Find("captain")!));

        Assert.Equal(Door, freed.Tile);
        Assert.NotEqual(freed.Freer.Id, freed.Follower.Id);
        Assert.Equal(lines.Single(l => l.Enemy.Id == freed.Follower.Id).IfAllLand, freed.Damage);
    }

    [Fact]
    public void ACounterThatCannotKillFreesNoTile()
    {
        var (state, lines) = Corridor(null, ls => 25);
        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.False(l.Forecast.CounterIsLethal(l.Enemy.Hp, state.Find("captain")!.Hp)));

        Assert.Empty(Queries.FreedStrikes(lines, state.Find("captain")!));
    }

    [Fact]
    public void EndAsksWhenTheFreedTileMakesTheTotalLethal()
    {
        var (state, lines) = Corridor(1, ls => ls.Max(l => l.IfAllLand) + 1);
        var seated = Queries.IfAllLand(lines);
        Assert.True(seated < state.Find("captain")!.Hp);

        var lethal = Queries.Lethal(state, Starter).Single(l => l.Unit.Id == "captain");

        Assert.Single(lethal.Strikers);
        var freed = Assert.Single(lethal.Freed);
        Assert.Equal(seated + freed.Damage, lethal.Total);
        var names = UnitNames.Of(state, Starter);
        Assert.Equal(
            $"Lethal if all land: {names["captain"]} ({names[lethal.Strikers[0].Enemy.Id]} for {lethal.Strikers[0].Damage}, {names[freed.Follower.Id]} for {freed.Damage} on 4,3 if {names["captain"]}'s counter kills {names[freed.Freer.Id]} ({freed.Counter.Defender.DisplayedHit} hit), against {lethal.Unit.Hp} hp)",
            PlaySession.LethalLine(lethal, names));
    }

    [Fact]
    public void AUnitTheFreedTileLeavesStandingIsNotLethal()
    {
        var (state, _) = Corridor(1, ls => ls.Sum(l => l.IfAllLand) + 1);

        Assert.DoesNotContain(Queries.Lethal(state, Starter), l => l.Unit.Id == "captain");
    }

    [Fact]
    public void AUnitTheSeatedStrikesAlreadyKillCarriesNoFreedStrike()
    {
        var (state, lines) = Corridor(1, ls => ls.Max(l => l.IfAllLand));

        var lethal = Queries.Lethal(state, Starter).Single(l => l.Unit.Id == "captain");

        Assert.Empty(lethal.Freed);
        Assert.Equal(Queries.IfAllLand(lines), lethal.Total);
    }

    [Fact]
    public void ThreatPrintsTheFreedTileUnderItsTotal()
    {
        var (state, lines) = Corridor(1, ls => ls.Max(l => l.IfAllLand) + 1);
        var captain = state.Find("captain")!;
        var freed = Assert.Single(Queries.FreedStrikes(lines, captain));
        var names = UnitNames.Of(state, Starter);

        var text = PlaySession.ThreatText(state, Starter, captain, new Coord(5, 3), lines, Array.Empty<SleepingThreat>());

        Assert.Contains(
            $"  If all land: {Queries.IfAllLand(lines)} against {captain.Hp} hp\n  If {names["captain"]}'s counter kills {names[freed.Freer.Id]} ({freed.Counter.Defender.DisplayedHit} hit), {names[freed.Follower.Id]} takes 4,3: {Queries.IfAllLand(lines) + freed.Damage} against {captain.Hp} hp",
            text.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void ThreatPrintsNoFreedRowWhenNoCounterKills()
    {
        var (state, lines) = Corridor(null, ls => 25);

        var text = PlaySession.ThreatText(state, Starter, state.Find("captain")!, new Coord(5, 3), lines, Array.Empty<SleepingThreat>());

        Assert.DoesNotContain("counter kills", text);
    }

    [Fact]
    public void TheChanceReadsAllCountersLandingWhenOneCounterStrikeCannotKill()
    {
        var (state, lines) = Corridor(1, ls => 25);
        var freed = Assert.Single(Queries.FreedStrikes(lines, state.Find("captain")!));
        var oneStrike = freed with { Freer = freed.Freer with { Hp = freed.Counter.Defender.Damage } };
        var twoStrikes = freed with { Freer = freed.Freer with { Hp = freed.Counter.Defender.Damage + 1 } };

        Assert.Equal($"({freed.Counter.Defender.DisplayedHit} hit)", PlaySession.FreedChance(oneStrike));
        Assert.Equal($"({freed.Counter.Defender.DisplayedHit} hit, all counters landing)", PlaySession.FreedChance(twoStrikes));
    }

    [Fact]
    public void TheProtocolsLethalEntryCarriesTheFreedStrike()
    {
        var (state, _) = Corridor(1, ls => ls.Max(l => l.IfAllLand) + 1);

        var answer = new ProtocolSession(Starter, state, new StringWriter()).Answer("""{"type":"end","anyway":true}""");

        Assert.Contains("\"freed\":[{\"enemy\":\"brigand-", answer);
        Assert.Contains("\"tile\":\"4,3\",\"ifCounterKills\":\"brigand-", answer);
        Assert.Contains("\"counterKillsOnHit\":true", answer);
    }
}
