using Ironwake.Cli;
using Ironwake.Content;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// <c>campaign --from &lt;map&gt; --pick keziah --fed N</c> (issue 865, round 286): a chair opening on a
/// later map plays the pick's hungering weapon at a given feed, at full HP and not starved, so a cold
/// field reads Kinsbane at 0211's median without a five-map run. Refused without <c>--from</c>, without
/// <c>--pick</c>, out of range, and for a pick with no hungering weapon; <c>--fed 0</c> is no flag.
/// </summary>
[Collection("console")]
public class FedFlagTests
{
    private static readonly GameContent Content = ContentLoader.Load(Fixture.RealContentDirectory());

    private static string Run(string script, params string[] args)
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-fed-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, script);
        try
        {
            return ConsoleCapture.Run(() => CampaignSession.Run(args.Concat(new[] { "--script", path, "--content", Fixture.RealContentDirectory() }).ToArray()));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string Refusal(params string[] args) =>
        ConsoleCapture.Run(() => Assert.Equal(2, CampaignSession.Run(args.Concat(new[] { "--content", Fixture.RealContentDirectory() }).ToArray())));

    [Fact]
    public void FedTenOnTheFieldReadsFourTeethAtFullHp()
    {
        var output = Run("march\nshow keziah\n", "--from", "the_field", "--pick", "keziah", "--level", "6", "--fed", "10", "--seed", "820");

        Assert.Contains("  Kinsbane: fed 10, teeth 4/5. Power +4. Hungry:", output);
        Assert.Contains("  HP 26/26 ", output);
        Assert.DoesNotContain("ERROR", output);
    }

    [Fact]
    public void FedTwelveReadsWoken()
    {
        var output = Run("march\nshow keziah\n", "--from", "the_field", "--pick", "keziah", "--level", "6", "--fed", "12", "--seed", "820");

        Assert.Contains("  Kinsbane: fed 12, teeth 5/5. Power +5. Woken: no drain. A kill: move again (once a map).", output);
    }

    [Fact]
    public void FedZeroIsTheSameRunAsNoFlag()
    {
        var script = "march\nshow keziah\nend\nend\n";
        var flagged = Run(script, "--from", "the_field", "--pick", "keziah", "--fed", "0", "--seed", "820");
        var bare = Run(script, "--from", "the_field", "--pick", "keziah", "--seed", "820");

        Assert.Equal(bare, flagged);
    }

    [Fact]
    public void FedStartsTheWeaponNotStarved()
    {
        var record = CampaignRecord.StartAt(Content, 820, "the_field", pick: "keziah");
        var keziah = record.Roster.Single(u => u.Id == "keziah");
        var starved = keziah with { Inventory = new Inventory(ValueList<ItemStack>.From(keziah.Inventory.Items.Select(s => s.ItemId == Kinsbane.ItemId ? s with { Starved = true, Uses = 1 } : s))) };
        record = record with { Roster = ValueList<Unit>.From(record.Roster.Select(u => u.Id == "keziah" ? starved : u)) };

        var stack = CampaignSession.Fed(record, Content, "keziah", 10)!.Roster.Single(u => u.Id == "keziah").Inventory.Items.Single(s => s.ItemId == Kinsbane.ItemId);

        Assert.Equal(10, stack.Fed);
        Assert.False(stack.Starved);
    }

    [Fact]
    public void FedWithoutFromIsRefused()
    {
        var output = Refusal("--fed", "10");

        Assert.Contains("ERROR: --fed feeds the pick's hungering weapon for a campaign opening on a later map; give --from", output);
    }

    [Fact]
    public void FedWithoutPickIsRefused()
    {
        var output = Refusal("--from", "the_field", "--fed", "10");

        Assert.Contains("ERROR: --fed feeds the pick's hungering weapon; give --pick", output);
    }

    [Theory]
    [InlineData("13")]
    [InlineData("-1")]
    public void FedOutOfRangeIsRefused(string count)
    {
        var output = Refusal("--from", "the_field", "--pick", "keziah", "--fed", count);

        Assert.Contains($"ERROR: --fed is 0 to 12, the count at which the weapon wakes; got {count}", output);
    }

    [Fact]
    public void FedForAPickWithNoHungeringWeaponIsRefused()
    {
        var output = Refusal("--from", "the_field", "--pick", "rook", "--fed", "10");

        Assert.Contains("ERROR: rook carries no hungering weapon, so --fed has nothing to feed", output);
    }
}
