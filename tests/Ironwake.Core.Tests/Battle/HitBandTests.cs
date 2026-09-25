using Ironwake.Sim;

namespace Ironwake.Core.Tests.Battle;

using static BattleFixture;

/// <summary>
/// Issue 158's instrument: the content arm cuts the iron tier and nothing else, and the
/// tally reads both sides of every attack from the forecast the resolver is about to use.
/// </summary>
public class HitBandTests
{
    [Fact]
    public void TheIronArmCutsEveryIronWeaponsHitByFifteenAndNothingElse()
    {
        var content = HitBand.Content(Starter, HitBandArm.IronHit15);

        foreach (var (id, weapon) in Starter.Weapons)
        {
            var expected = id.StartsWith("iron_", StringComparison.Ordinal) ? weapon.Hit - HitBand.IronHitCut : weapon.Hit;
            Assert.Equal(expected, content.Weapons[id].Hit);
        }

        Assert.Contains(Starter.Weapons.Keys, id => id.StartsWith("iron_", StringComparison.Ordinal));
        Assert.Same(Starter, HitBand.Content(Starter, HitBandArm.Main));
    }

    [Fact]
    public void TheTallyCountsOnlyAttacksAndBothSidesOfEach()
    {
        var empty = new HitTally();
        var start = BattleState.From(YardMap, Starter, Roster, 7);
        empty.Record(start, Starter, new Wait(start.UnitsOf(Side.Player).First().Id));
        Assert.Contains("player raw hit: 0-9 0 10-19 0 20-29 0 30-39 0 40-49 0 50-59 0 60-69 0 70-79 0 80-89 0 90-99 0 100 0, strikes 0, doubles 0 %", empty.Lines("t"));

        var hits = new HitTally();
        var game = Runner.Play(Starter, YardMap, 7, new HeuristicPlayer(), hits: hits);
        var attacks = game.Mix.Values.Sum(m => m.Attacks);
        Assert.True(attacks > 0);

        var lines = hits.Lines("t").Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("t player raw hit:", lines[0]);
        Assert.StartsWith("t enemy raw hit:", lines[1]);
        foreach (var line in lines)
        {
            var buckets = line[(line.IndexOf(':') + 1)..line.IndexOf(',')].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var counted = 0;
            for (var i = 1; i < buckets.Length; i += 2)
            {
                counted += int.Parse(buckets[i], System.Globalization.CultureInfo.InvariantCulture);
            }

            var strikes = int.Parse(line.Split("strikes ")[1].Split(',')[0], System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(strikes, counted);
            Assert.True(strikes > 0, line);
        }
    }

    [Fact]
    public void TheUsageLineNamesTheHitBandCommand()
    {
        Assert.Contains("--hitband <map>|--all [--seeds N]", Program.Usage);
    }
}
