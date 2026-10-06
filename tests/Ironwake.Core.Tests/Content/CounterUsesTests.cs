namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Issue 1200 (round 406): a counter thrown with a spell spends one of the spell's uses for the
/// battle, so <c>threat</c>, <c>forecast</c> and the enemy-phase forecast lines print the uses left
/// after the counter's strike columns, and <c>threat</c> adds a row when the counters it lists could
/// spend the last one. Screen only: no rule and no ask changes. Played on the Mill's pre-<c>holds:</c>
/// copy at Code's seed 1480, where Maud holds the fort with Radiance.
/// </summary>
[Collection("console")]
public class CounterUsesTests
{
    private const string Opening = "move captain 5,8\nwait captain\nwait maud\nend\n";
    private const string ToTurnThree = Opening + "move captain 7,8\nattack captain archer-2\nattack maud brigand-1\nend !\nmove captain 6,8\nattack captain archer-2\nattack maud soldier-1\n";

    [Fact]
    public void ThreatPrintsTheSpellUsesLeftOnEveryCounterThatWouldSpendOne()
    {
        var output = Play(Opening + "threat maud\n");

        Assert.Contains("  Archer 2 from 7,6 with Iron Bow (slot 1): acc 65% dmg 6 crit 0%; counter: acc 97% dmg 10 crit 2% (Radiance 5 of 5 left)\n", output);
        Assert.Contains("  Brigand from 9,5 with Iron Axe (slot 1): acc 45% dmg 11 crit 0%; counter: acc 98% dmg 11 crit 3% (Radiance 5 of 5 left)\n", output);
    }

    [Fact]
    public void ThreatSaysNothingOfTheLastUseWhileTheCountersCannotReachIt()
    {
        var output = Play(Opening + "threat maud\n");

        Assert.Contains("  If all land: 17 against 17 hp\n", output);
        Assert.DoesNotContain("last use", output);
    }

    [Fact]
    public void ThreatSaysWhenTheCountersCouldSpendTheLastUse()
    {
        var output = Play(ToTurnThree + "threat maud\n");

        Assert.Contains("  Soldier from 8,4 with Iron Lance (slot 1): acc 58% dmg 9 crit 0%; counter: acc 97% dmg 10 crit 2% (Radiance 2 of 5 left)\n  If all land: 15 against 17 hp\n  Counters could spend Radiance's last use\n", output);
    }

    [Fact]
    public void TheEnemyPhaseForecastPrintsTheUsesLeftBeforeTheCounterSpendsOne()
    {
        var output = Play(ToTurnThree + "end\n");

        Assert.Contains("Forecast Archer 1 -> Maud: acc 65% dmg 6 crit 0%; counter: acc 80% dmg 10 crit 2% (Radiance 2 of 5 left)\n", output);
        Assert.Contains("Forecast Soldier -> Maud: acc 58% dmg 9 crit 0%; counter: acc 97% dmg 10 crit 2% (Radiance 1 of 5 left)\n", output);
        Assert.Contains("Maud's Radiance is spent for this battle\n", output);
    }

    [Fact]
    public void AForecastWithNoCounterPrintsNoUses()
    {
        var output = Play(ToTurnThree);

        Assert.Contains("Forecast Maud -> Soldier: acc 97% dmg 10 crit 2%; counter: none\n", output);
    }

    [Fact]
    public void APhysicalWeaponsCounterPrintsNoUses()
    {
        var output = Play(Opening + "threat captain from 7,7\n");

        Assert.Contains("  Brigand from 8,7 with Iron Axe (slot 1): acc 49% dmg 10 crit 0%; counter: acc 90% dmg 11 x2 crit 5%\n", output);
    }

    private static string Play(string script)
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var path = Path.Combine(Path.GetTempPath(), "ironwake-uses-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, script);
        try
        {
            return ConsoleCapture.Run(() => Ironwake.Cli.Program.Main(["play", Path.Combine(repo, "docs", "samples", "the_mill_0278.map"), "--seed", "1480", "--script", path, "--content", Fixture.RealContentDirectory()]));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
