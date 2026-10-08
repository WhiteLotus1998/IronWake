using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Gust becomes Spark Storm in the shipped content (issue 1329 slice 3, Lotus's signature on #1247, DECISIONS/0348):
/// the tome keeps the id <c>gust</c>, so every journaled script still names it, and takes the signed numbers, an area
/// of 1 and the lightning mark, with the flier rider dropped. Pell carries it and the shops stock it; it is cast with
/// the Item action and never attacks.
/// </summary>
[Collection("console")]
public class SparkStormShippedTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static string Play(string script)
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-1329-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, script);
        try
        {
            return ConsoleCapture.Run(() => Program.Main(["play", "the_tollgate", "--seed", "1329", "--script", path, "--content", Fixture.RealContentDirectory()]));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SparkStormCarriesLotussSignedNumbers()
    {
        var storm = Shipped.Weapon("gust");

        Assert.Equal("Spark Storm", storm.Name);
        Assert.Equal((3, 90, 1, 2, 4, WeaponRank.E), (storm.Mt, storm.Hit, storm.MinRange, storm.MaxRange, storm.Durability, storm.Rank));
        Assert.Equal(1, storm.Area);
        Assert.True(storm.Marks);
        Assert.Equal(MagicSchool.Lightning, storm.School);
        Assert.False(storm.IsEffectiveAgainst(MovementType.Flying));
    }

    [Fact]
    public void SparkStormIsTheOneShippedAreaTomeAndTheOneMarkingTome()
    {
        Assert.Equal(new[] { "gust" }, Shipped.Weapons.Values.Where(w => w.Area > 0).Select(w => w.Id));
        Assert.Equal(new[] { "gust" }, Shipped.Weapons.Values.Where(w => w.Marks).Select(w => w.Id));
    }

    [Fact]
    public void PellCarriesSparkStormBesideCinder()
    {
        var pell = Shipped.Cast.Single(u => u.Id == "pell");

        Assert.Equal(new[] { "cinder", "gust" }, pell.Inventory.Items.Select(i => i.ItemId));
        Assert.Equal(4, pell.Inventory.Items[1].Uses);
    }

    [Fact]
    public void AnAttackWithSparkStormIsRefusedNamingTheItemCommandWithTheConsolesSlot()
    {
        var output = Play("attack pell archer-1 2\n");

        Assert.Contains("cannot attack with gust: an area cast; use it with item: item Pell 2 <unit|x,y>\n", output);
    }

    [Fact]
    public void AnAreaCastWithNoTargetNamesTheConsolesSlot()
    {
        var output = Play("item pell 2\n");

        Assert.Contains("ERROR: Spark Storm strikes an area: item Pell 2 <unit|x,y>\n", output);
    }

    [Fact]
    public void APreviewAtATileOutOfRangeIsRefusedWithTheRangeRatherThanForecast()
    {
        var output = Play("item pell 2 6,5 preview\n");

        Assert.Contains("> item pell 2 6,5 preview\n6,5 is 5 tiles from pell at 6,10; Spark Storm reaches 1-2\n", output);
        Assert.DoesNotContain("Unhandled", output);
    }
}
