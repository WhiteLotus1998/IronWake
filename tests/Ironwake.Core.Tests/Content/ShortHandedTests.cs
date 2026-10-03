using Ironwake.Cli;
using Ironwake.Client;
using Ironwake.Content;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// A company thinned below a map's bare slots (issue 795): the campaign fights the map short-handed,
/// those slots empty, and the deployment line says how many stand empty; <c>march</c> never throws,
/// any refusal left is a camp line in the console and a status in the client. Outside the campaign a
/// roster too short for the map is still a content error and throws.
/// </summary>
[Collection("console")]
public class ShortHandedTests
{
    private static readonly GameContent Content = ContentLoader.Load(Fixture.RealContentDirectory());

    private static string Dir => Fixture.RealContentDirectory();

    private static MapDefinition Map(CampaignRecord record) => CampaignSession.MapFor(Dir, Content, record, record.NextMap(Content).MapId);

    /// <summary>The raid with only the captain and the recruits it names left alive, as permadeath leaves a company.</summary>
    private static CampaignRecord Thinned()
    {
        var record = CampaignRecord.StartAt(Content, 795, "ironwake_raid");
        var named = Map(record).Placements.OfType<PlayerPlacement>().Where(p => p.RecruitId is not null).Select(p => p.RecruitId!).ToHashSet();
        var kept = record.Roster.Where((u, i) => i == 0 || named.Contains(u.Id)).ToList();
        var fallen = record.Roster.Where(u => !kept.Contains(u)).Select(u => u.Id);
        return record with { Roster = ValueList<Unit>.From(kept), Fallen = ValueList<string>.From(record.Fallen.Concat(fallen)), Pick = "rook" };
    }

    private static int Slots(MapDefinition map) => map.Placements.OfType<PlayerPlacement>().Count();

    [Fact]
    public void ACompanyThinnerThanTheMapsBareSlotsMarchesShortHanded()
    {
        var record = Thinned();
        var map = Map(record);

        var battle = record.Begin(map, Content);

        Assert.Null(record.MarchRefusal(map, Content));
        Assert.True(battle.UnitsOf(Side.Player).Count() < Slots(map));
        Assert.Equal(record.Present(Content).Select(u => u.Id).OrderBy(id => id, StringComparer.Ordinal), battle.UnitsOf(Side.Player).Select(u => u.Id).OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public void TheDeploymentLineCountsTheSlotsThatStandEmpty()
    {
        var record = Thinned();
        var map = Map(record);
        var deployed = record.Deployment(map, Content).Count;
        var empty = Slots(map) - deployed;

        Assert.EndsWith($"(deploy {deployed} of {record.Present(Content).Count}; {empty} slots stand empty)", CampaignSession.DeploymentLine(record, Content, map));
        Assert.DoesNotContain(CampaignSession.RosterPanelLines(record, Content, map), l => l.StartsWith("ERROR: ", StringComparison.Ordinal));
    }

    [Fact]
    public void AFullCompanyPrintsNoEmptySlots()
    {
        var record = CampaignRecord.StartAt(Content, 795, "the_tollgate");

        Assert.DoesNotContain("stand empty", CampaignSession.DeploymentLine(record, Content, Map(record)));
    }

    [Fact]
    public void TheClientMarchesAThinnedCompanyWithoutAThrow()
    {
        var client = new CampaignClient(Content, Dir, Thinned());

        Assert.True(client.March());
        Assert.NotNull(client.Battle);
    }

    [Fact]
    public void OutsideTheCampaignARosterTooShortForTheMapStillThrows()
    {
        var record = Thinned();
        var map = Map(record);

        var e = Assert.Throws<ArgumentException>(() => BattleState.From(map, Content, record.Roster, 1));

        Assert.Contains("no recruit left to fill it", e.Message);
    }

    [Fact]
    public void AMarchTheBoardRefusesIsAStatusInTheClientNotAThrow()
    {
        var record = CampaignRecord.StartAt(Content, 701, "the_tollgate");
        var named = Map(record).Placements.OfType<PlayerPlacement>().First(p => p.Slot == PlayerSlot.NamedRecruit).RecruitId!;
        var broken = record with { Roster = ValueList<Unit>.From(record.Roster.Where(u => u.Id != named)) };
        var client = new CampaignClient(Content, Dir, broken);

        Assert.NotNull(broken.MarchRefusal(Map(broken), Content));
        Assert.False(client.March());
        Assert.Null(client.Battle);
        Assert.NotNull(client.Status);
    }

    /// <summary>The console's output for <paramref name="script"/> played from <paramref name="record"/>, loaded from a save.</summary>
    private static string Console(CampaignRecord record, string script)
    {
        var dir = Path.Combine(Path.GetTempPath(), "ironwake-shorthanded-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "play.script");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, script);
            Assert.Null(new SaveStore(dir).Save("thin", record));
            return ConsoleCapture.Run(() => Ironwake.Cli.Program.Main(new[] { "campaign", "--script", path, "--content", Dir, "--saves", dir, "--load", "thin" }));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void TheConsoleMarchesAThinnedCompanyWithoutAThrow()
    {
        var record = Thinned();

        var output = Console(record, "roster\nmarch\n");

        Assert.Contains(CampaignSession.DeploymentLine(record, Content, Map(record)) + "\n", output);
        Assert.Contains("> march\n", output);
        Assert.Contains("Objective: ", output[output.IndexOf("> march\n", StringComparison.Ordinal)..]);
        Assert.DoesNotContain("ERROR: ", output);
    }

    [Fact]
    public void AMarchTheBoardRefusesIsAnErrorLineInTheConsoleNotAThrow()
    {
        var record = CampaignRecord.StartAt(Content, 701, "the_tollgate");
        var named = Map(record).Placements.OfType<PlayerPlacement>().First(p => p.Slot == PlayerSlot.NamedRecruit).RecruitId!;
        var broken = record with { Roster = ValueList<Unit>.From(record.Roster.Where(u => u.Id != named)) };

        var output = Console(broken, "march\n");

        Assert.Contains("> march\nERROR: " + CampaignSession.Text(broken, Content, broken.MarchRefusal(Map(broken), Content)!) + "\n", output);
        Assert.DoesNotContain("Objective: ", output);
    }
}
