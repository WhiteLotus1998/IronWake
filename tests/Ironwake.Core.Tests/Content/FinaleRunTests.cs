using Ironwake.Content;
using Ironwake.Core.Tests.Maps;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The keep finale's measurement (issue 692, slice 5; DECISIONS/0151): the three companies the Sim
/// fields on a <c>deploy: all</c> board, and the verdicts each one is held to.
/// </summary>
public class FinaleRunTests
{
    private static readonly GameContent Content = MapFixture.Content;

    private static string Sample(string name) =>
        Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", name);

    private static IReadOnlyList<GameResult> Games(int won, int lost, int turns) =>
        Enumerable.Repeat(new GameResult(BattleResult.Won, turns, new Dictionary<string, ActionMix>()), won)
            .Concat(Enumerable.Repeat(new GameResult(BattleResult.Lost, turns, new Dictionary<string, ActionMix>(), LossCause.Timeout), lost))
            .ToList();

    [Fact]
    public void TheFullCompanyIsTheWholeCastThenHiresToTheCap()
    {
        var roster = FinaleRun.Roster(Content, FinaleRun.Company.Full, FinaleRun.DefaultLevel);

        Assert.Equal(CampaignRecord.CompanyCap, roster.Count);
        Assert.Equal(Content.Cast.Select(u => u.Id), roster.Take(Content.Cast.Count).Select(u => u.Id));
        Assert.Equal(Content.Campaign.Keep.Hires.Take(CampaignRecord.CompanyCap - Content.Cast.Count).Select(h => h.Id), roster.Skip(Content.Cast.Count).Select(u => u.Id));
    }

    [Fact]
    public void TheDepletedCompanyIsTheCaptainFiveStoryMembersAndHiresToTheCap()
    {
        var roster = FinaleRun.Roster(Content, FinaleRun.Company.Depleted, FinaleRun.DefaultLevel);

        Assert.Equal(CampaignRecord.CompanyCap, roster.Count);
        Assert.Equal(Content.Cast.Take(1 + FinaleRun.DepletedStory).Select(u => u.Id), roster.Take(1 + FinaleRun.DepletedStory).Select(u => u.Id));
        Assert.All(roster.Skip(1 + FinaleRun.DepletedStory), u => Assert.True(Barracks.IsHire(u, Content), u.Id));
    }

    [Fact]
    public void TheFloorCompanyIsTheCaptainThreeStoryMembersAndEveryHire()
    {
        var roster = FinaleRun.Roster(Content, FinaleRun.Company.Floor, FinaleRun.DefaultLevel);

        Assert.Equal(1 + FinaleRun.FloorStory + Content.Campaign.Keep.Hires.Count, roster.Count);
        Assert.Equal(Content.Campaign.Keep.Hires.Select(h => h.Id), roster.Skip(1 + FinaleRun.FloorStory).Select(u => u.Id));
    }

    [Fact]
    public void StoryMembersStandAtTheLevelAndHiresJoinTheBarracksLevelsBelowIt()
    {
        var roster = FinaleRun.Roster(Content, FinaleRun.Company.Floor, FinaleRun.DefaultLevel);

        Assert.All(roster.Take(1 + FinaleRun.FloorStory), u => Assert.True(u.Level >= FinaleRun.DefaultLevel, u.Id));
        Assert.All(roster.Skip(1 + FinaleRun.FloorStory), u => Assert.Equal(FinaleRun.DefaultLevel - Barracks.LevelsBelow, u.Level));
    }

    [Fact]
    public void AMapThatDoesNotDeployAllIsRefusedAsTheFinale()
    {
        var pair = MapFiles.Load(Sample("ironwake_keep_pair.map"), Content);
        var finale = MapFiles.Load(Sample("ironwake_keep_finale.map"), Content);

        Assert.Equal("finale: pair is not 'deploy: all'; the finale fields the whole company", FinaleRun.Refusal(pair, "pair"));
        Assert.Null(FinaleRun.Refusal(finale, "finale"));
    }

    [Fact]
    public void TheSimsUsageNamesTheFinale()
    {
        Assert.Contains("--finale <map|file> [--seeds N] [--level N] [--scheme one|two] [--gates] |", Program.Usage);
    }

    [Fact]
    public void FullAndDepletedAreHeldToGateOneAndTheFloorIsData()
    {
        foreach (var company in new[] { FinaleRun.Company.Full, FinaleRun.Company.Depleted })
        {
            Assert.False(new FinaleRun.Reading(company, 12, Games(1, 9, 10), 10, 11).Passed);
            Assert.True(new FinaleRun.Reading(company, 12, Games(6, 4, 10), 10, 11).Passed);
        }

        var floor = new FinaleRun.Reading(FinaleRun.Company.Floor, 10, Games(0, 10, 10), 10, 11);
        Assert.True(floor.Passed);
        Assert.Contains("data, a cold chair's play decides (never won)", floor.Lines(8).First());
    }

    [Fact]
    public void AFinaleLongerThanSixteenTurnsAtTheMedianFailsOnLengthAndNamesTheLever()
    {
        var reading = new FinaleRun.Reading(FinaleRun.Company.Floor, 10, Games(5, 5, FinaleRun.LengthLimit + 1), 10, 20);

        Assert.False(reading.Passed);
        Assert.Contains($"  length: median {FinaleRun.LengthLimit + 1} turns over every game, limit 20: over {FinaleRun.LengthLimit}, the lever is one fewer wave", reading.Lines(8));
    }

    /// <summary>
    /// Issue 1204: on a map with fronts each reading counts, per fall event, the games its spawn
    /// arrived in, was blocked in, or never fired in, as data beside the gates.
    /// </summary>
    [Fact]
    public void AFinaleReadingCountsEachFallsSpawnArrivedBlockedOrUnfired()
    {
        var games = Games(3, 0, 10).Select((g, i) => g with
        {
            Fired = i switch
            {
                0 => new[] { new MapEventFired("gate_in1", false) },
                1 => new[] { new MapEventFired("gate_in1", true), new MapEventFired("wave1", false) },
                _ => Array.Empty<MapEventFired>(),
            },
        }).ToList();
        var reading = new FinaleRun.Reading(FinaleRun.Company.Full, 12, games, 10, 11) { Falls = new[] { "gate_in1" } };

        Assert.Contains("  falls: gate_in1 arrived 1, blocked 1, unfired 1 of 3 (data; all blocked is decoration)", reading.Lines(8));
        Assert.DoesNotContain(new FinaleRun.Reading(FinaleRun.Company.Full, 12, games, 10, 11).Lines(8), l => l.StartsWith("  falls:", StringComparison.Ordinal));
    }

    [Fact]
    public void AFinaleWhoseSlowestAiGameReachesOneSecondFailsOnTime()
    {
        var reading = new FinaleRun.Reading(FinaleRun.Company.Full, 12, Games(10, 0, 10), FinaleRun.SpeedLimitMs, 11);

        Assert.False(reading.Passed);
        Assert.Contains($"  time: {FinaleRun.SpeedSeeds} AI-vs-AI games, slowest {FinaleRun.SpeedLimitMs} ms: FAILED", reading.Lines(8));
    }

    [Fact]
    public void TheFinaleSampleFieldsEachCompanyWholeAndIsCanonical()
    {
        var path = Sample("ironwake_keep_finale.map");
        var text = File.ReadAllText(path).ReplaceLineEndings("\n");
        var map = MapFiles.Load(path, Content);

        Assert.Equal(text, Ironwake.Content.MapFormat.Write(Ironwake.Content.MapFormat.Parse("ironwake_keep_finale.map", text, Content), Content));
        Assert.Equal(12, FinaleRun.Measure(Content, map, FinaleRun.Company.Full, FinaleRun.DefaultLevel, 1, RollScheme.TwoRollAverage).Size);
        Assert.Equal(12, FinaleRun.Measure(Content, map, FinaleRun.Company.Depleted, FinaleRun.DefaultLevel, 1, RollScheme.TwoRollAverage).Size);
        Assert.Equal(1 + FinaleRun.FloorStory + Content.Campaign.Keep.Hires.Count, FinaleRun.Measure(Content, map, FinaleRun.Company.Floor, FinaleRun.DefaultLevel, 1, RollScheme.TwoRollAverage).Size);
    }

    /// <summary>Issue 1149: the fielded content's cast is the company, so a gate that fields the cast fields it at the level.</summary>
    [Fact]
    public void TheFieldedContentsCastIsTheCompany()
    {
        Assert.Equal(FinaleRun.Roster(Content, FinaleRun.Company.Depleted, 8), FinaleRun.Fielded(Content, FinaleRun.Company.Depleted, 8).Cast);
    }
}

/// <summary>The finale's console lines (issue 1149), read through the console, so in its collection.</summary>
[Collection("console")]
public class FinaleRunConsoleTests
{
    /// <summary>
    /// Issue 1149: <c>--gates</c> adds gate 2 and gate 4 under the full company, and only there;
    /// without it neither line prints.
    /// </summary>
    [Fact]
    public void TheGatesFlagAddsGatesTwoAndFourToTheFullCompany()
    {
        var with = ConsoleCapture.Run(() => Ironwake.Sim.Program.Main(new[] { "--finale", "ironwake_keep", "--seeds", "1", "--gates" }));
        var without = ConsoleCapture.Run(() => Ironwake.Sim.Program.Main(new[] { "--finale", "ironwake_keep", "--seeds", "1" }));

        var full = with.IndexOf("finale full:", StringComparison.Ordinal);
        var depleted = with.IndexOf("finale depleted:", StringComparison.Ordinal);
        var two = with.IndexOf("  gate 2 decisions matter: ironwake_keep, random wins ", StringComparison.Ordinal);
        var four = with.IndexOf("  gate 4 no dead weight: ironwake_keep, 11 recruits x 1 seeds", StringComparison.Ordinal);
        Assert.True(full < two && two < four && four < depleted, with);
        Assert.Equal(1, with.Split("gate 2 ").Length - 1);
        Assert.DoesNotContain("gate 2 ", without);
        Assert.DoesNotContain("gate 4 ", without);
    }
}
