using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The camp as one view (issue 678): the console's <c>camp</c> prints the heading, then Roster,
/// Keep, Quests and Shop in that order, then the march line; the fallen are listed with the board
/// each fell on and never deploy; the Keep panel opens on the purse the record holds; the Quests
/// panel prints each open side map's price and each won one's pay.
/// </summary>
[Collection("console")]
public class CampScreenTests
{
    private static readonly GameContent Content = ContentLoader.Load(Fixture.RealContentDirectory());

    private static string Dir => Fixture.RealContentDirectory();

    private static CampaignRecord Tollgate() => CampaignRecord.StartAt(Content, 701, "the_tollgate");

    private static MapDefinition Map(CampaignRecord record) => CampaignSession.MapFor(Dir, Content, record, record.NextMap(Content).MapId);

    private static IReadOnlyList<string> Camp(CampaignRecord record) => CampaignSession.CampLines(Dir, Content, record, Map(record));

    private static List<string> Panel(IReadOnlyList<string> lines, int panel)
    {
        var start = lines.ToList().IndexOf(CampaignSession.CampPanels[panel]) + 1;
        var end = panel + 1 < CampaignSession.CampPanels.Count ? lines.ToList().IndexOf(CampaignSession.CampPanels[panel + 1]) : lines.Count - 1;
        return lines.Skip(start).Take(end - start).ToList();
    }

    [Fact]
    public void TheConsoleCampPrintsTheFourPanelsInOrderThenTheMarchLine()
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-camp-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, "camp\n");
        string output;
        try
        {
            output = ConsoleCapture.Run(() => Ironwake.Cli.Program.Main(new[] { "campaign", "--from", "the_tollgate", "--seed", "701", "--script", path, "--content", Dir }));
        }
        finally
        {
            File.Delete(path);
        }

        var camp = output[(output.IndexOf("> camp\n", StringComparison.Ordinal) + "> camp\n".Length)..];
        var at = CampaignSession.CampPanels.Select(p => camp.IndexOf(p + "\n", StringComparison.Ordinal)).ToList();
        var march = camp.IndexOf("March (march): map ", StringComparison.Ordinal);

        Assert.StartsWith(CampaignSession.ScreenHeading(Tollgate(), Content, Map(Tollgate())) + "\n", camp);
        Assert.All(at, i => Assert.True(i > 0));
        Assert.Equal(at.OrderBy(i => i), at);
        Assert.True(march > at[^1]);
        Assert.Contains(string.Join("\n", CampaignSession.CampLines(Dir, Content, Tollgate(), Map(Tollgate()), typed: true)) + "\n", camp);
    }

    [Fact]
    public void TheMarchLineNamesTheNextMapAndItsObjective()
    {
        var record = Tollgate();
        var map = Map(record);

        Assert.Equal(
            $"March (march): map {record.MapIndex + 1}, {map.Name}. {Objective.Line(record.Begin(map, Content), Content)}",
            Camp(record)[^1]);
    }

    [Fact]
    public void TheMarchLineNamesTheMapAloneWhenTheBoardCannotBeFought()
    {
        var record = Tollgate();
        var named = Map(record).Placements.OfType<PlayerPlacement>().First(p => p.Slot == PlayerSlot.NamedRecruit).RecruitId!;
        var broken = record with { Roster = ValueList<Unit>.From(record.Roster.Where(u => u.Id != named)) };

        var lines = Camp(broken);

        Assert.Equal($"March (march): map {record.MapIndex + 1}, {Map(record).Name}.", lines[^1]);
        Assert.Contains(Panel(lines, 0), l => l.StartsWith("ERROR: ", StringComparison.Ordinal));
    }

    [Fact]
    public void AFallenMemberIsListedWithTheBoardTheyFellOnAndIsNotDeployable()
    {
        var record = Tollgate();
        var deployed = record.Deployment(Map(record), Content);
        var fallen = deployed.First(id => id != record.Roster[0].Id);
        var name = UnitNames.Of(record, Content)[fallen];
        var after = record with
        {
            Roster = ValueList<Unit>.From(record.Roster.Where(u => u.Id != fallen)),
            Fallen = ValueList<string>.Of(fallen),
            FellOn = ValueList<FellOn>.Of(new FellOn(fallen, "The Mill")),
        };

        var roster = Panel(Camp(after), 0);

        Assert.Contains($"  Fallen: {name} (fell on The Mill)", roster);
        Assert.DoesNotContain(fallen, after.Deployment(Map(after), Content));
        Assert.DoesNotContain(name, roster.Single(l => l.StartsWith("Deploys to ", StringComparison.Ordinal)));
    }

    [Fact]
    public void AFallenMemberFromARecordWithoutBoardsIsListedByNameAlone()
    {
        var record = Tollgate();
        var fallen = record.Roster[^1];
        var after = record with { Roster = ValueList<Unit>.From(record.Roster.Take(record.Roster.Count - 1)), Fallen = ValueList<string>.Of(fallen.Id) };

        Assert.Contains($"  Fallen: {UnitNames.Of(after, Content)[fallen.Id]}", Panel(Camp(after), 0));
    }

    [Fact]
    public void TheKeepPanelOpensOnThePurseTheRecordHolds()
    {
        var record = Tollgate() with { Purse = 1234 };

        Assert.Equal("Purse: 1234", Panel(Camp(record), 1)[0]);
    }

    [Fact]
    public void BeforeTheRaidTheKeepPanelSaysWhyWallsAreClosed()
    {
        var record = Tollgate();

        Assert.Contains("Walls and ditches: " + UnitNames.Sentence(record.KeepMenuRefusal(Content)!), Panel(Camp(record), 1));
    }

    [Fact]
    public void AnOpenSideMapPrintsThatPermadeathApplies()
    {
        var record = Tollgate();
        var quest = record.QuestsOffered(Content)[0];

        Assert.Contains(Panel(Camp(record), 2), l => l.StartsWith($"  {quest.Id}: ", StringComparison.Ordinal) && l.EndsWith("; permadeath applies", StringComparison.Ordinal));
    }

    [Fact]
    public void WithPermadeathOffAnOpenSideMapSaysWhoFallsComesBackWounded()
    {
        var record = Tollgate() with { Permadeath = false };
        var quest = record.QuestsOffered(Content)[0];

        Assert.Contains(Panel(Camp(record), 2), l => l.StartsWith($"  {quest.Id}: ", StringComparison.Ordinal) && l.EndsWith("; permadeath is off: who falls comes back wounded", StringComparison.Ordinal));
    }

    [Fact]
    public void ASideMapOpensSayingWhoFallsIsGoneForGoodWithPermadeathOn()
    {
        var record = Tollgate();
        var quest = record.QuestsOffered(Content)[0];

        var line = CampaignSession.QuestOpening(record, Content, Map(record), quest.Id, quest.MemberId)[1];

        Assert.EndsWith("; who falls here is gone for good.", line, StringComparison.Ordinal);
    }

    [Fact]
    public void ASideMapOpensSayingWhoFallsComesBackWoundedWithPermadeathOff()
    {
        var record = Tollgate() with { Permadeath = false };
        var quest = record.QuestsOffered(Content)[0];

        var line = CampaignSession.QuestOpening(record, Content, Map(record), quest.Id, quest.MemberId)[1];

        Assert.EndsWith("; who falls here comes back wounded.", line, StringComparison.Ordinal);
        Assert.DoesNotContain("gone for good", line, StringComparison.Ordinal);
    }

    [Fact]
    public void AWonSideMapIsListedWithWhatItPaid()
    {
        var record = Tollgate();
        var quest = record.QuestsOffered(Content)[0];
        var won = record with { QuestsWon = ValueList<QuestWon>.Of(new QuestWon(quest.Id, record.MapIndex)) };
        var pay = new List<string>();
        if (quest.Pays is { } item)
        {
            pay.Add(Content.ItemName(item));
        }

        if (quest.Common > 0)
        {
            pay.Add($"{quest.Common} common material");
        }

        if (quest.Rare > 0)
        {
            pay.Add($"{quest.Rare} rare material");
        }

        var panel = Panel(Camp(won), 2);

        Assert.Contains("Side maps won:", panel);
        Assert.Contains(panel, l => l.StartsWith($"  {quest.Id}: ", StringComparison.Ordinal) && l.EndsWith("; paid " + (pay.Count == 0 ? "nothing" : string.Join(" and ", pay)), StringComparison.Ordinal));
    }

    [Fact]
    public void WithNoSideMapOpenOrWonTheQuestsPanelSaysSo()
    {
        var record = CampaignRecord.Start(Content, 3);

        Assert.Equal(new[] { "No side map is open." }, Panel(Camp(record), 2));
    }

    [Fact]
    public void TheBoardEachOfTheFallenFellOnSurvivesTheProtocol()
    {
        var record = Tollgate() with { Fallen = ValueList<string>.Of("wren"), FellOn = ValueList<FellOn>.Of(new FellOn("wren", "The Mill")) };

        var read = ProtocolJson.ReadCampaign(ProtocolJson.Campaign(record), Content);

        Assert.Equal(record.FellOn, read.FellOn);
        Assert.DoesNotContain("fellOn", ProtocolJson.Campaign(Tollgate()));
        Assert.Empty(ProtocolJson.ReadCampaign(ProtocolJson.Campaign(Tollgate()), Content).FellOn);
    }
}
