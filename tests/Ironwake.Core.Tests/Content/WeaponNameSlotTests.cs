using Ironwake.Cli;
using Ironwake.Content;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Issue 477: the slot argument of <c>attack</c> and <c>forecast</c> also takes a weapon's id
/// or display name, ignoring case, since an equip reorders the slots. A number reads as before;
/// a name that matches nothing, or two slots, is refused with the unit's slots listed. Played on Gust
/// (DECISIONS/0348), where Pell strikes with either of two tomes; Spark Storm is never equipped.
/// </summary>
[Collection("console")]
public class WeaponNameSlotTests
{
    private static string Play(string script)
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-477-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, script);
        try
        {
            return ConsoleCapture.Run(() => Program.Main(["play", "harrow_weir", "--seed", "487", "--script", path, "--content", Fixture.GustContentDirectory()]));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("1", "cinder")]
    [InlineData("1", "Cinder")]
    [InlineData("1", "CINDER")]
    [InlineData("2", "gust")]
    [InlineData("2", "Gust")]
    public void ANamedWeaponForecastsTheSameCombatAsItsSlot(string slot, string name)
    {
        var output = Play($"forecast pell brigand-1 {slot} from 2,4\nforecast pell brigand-1 {name} from 2,4\n");

        var lines = output.Split('\n').Where(line => line.StartsWith("Forecast Pell -> ", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal(lines[0], lines[1]);
        Assert.DoesNotContain("ERROR", output);
    }

    [Fact]
    public void AttackByNameStrikesWithThatWeaponAfterAnEquipReordersTheSlots()
    {
        var output = Play("move pell 2,4\nattack pell brigand-1 gust\nforecast pell brigand-1 cinder\nshow pell\nforecast pell brigand-1 1\n");

        Assert.Contains("> attack pell brigand-1 gust\nForecast Pell -> Brigand 1 with Gust:", output);
        Assert.Contains("> forecast pell brigand-1 cinder\nForecast Pell -> Brigand 1 with Cinder:", output);
        Assert.Contains("> forecast pell brigand-1 1\nForecast Pell -> Brigand 1 with Gust:", output);
    }

    [Fact]
    public void AStaleSlotNumberIsRefusedNamingBothWeapons()
    {
        var output = Play("move pell 2,4\nattack pell brigand-1 gust\nforecast pell brigand-1 1\nattack pell brigand-1 1\n");

        const string refusal = "ERROR: Slot 1 is Gust now; it was Cinder when Pell's pack was last listed; name the weapon, or show pell\n";
        Assert.Contains("> forecast pell brigand-1 1\n" + refusal, output);
        Assert.Contains("> attack pell brigand-1 1\n" + refusal, output);
    }

    [Fact]
    public void AStaleSlotNumberIsRefusedEvenWithTheBang()
    {
        var output = Play("move pell 2,4\nattack pell brigand-1 gust\nattack pell brigand-1 1 !\n");

        Assert.Contains("> attack pell brigand-1 1 !\nERROR: Slot 1 is Gust now; it was Cinder when Pell's pack was last listed; name the weapon, or show pell\n", output);
    }

    [Fact]
    public void ASlotNumberReReadAfterShowIsAccepted()
    {
        var output = Play("move pell 2,4\nattack pell brigand-1 gust\nshow pell\nforecast pell brigand-1 2\n");

        Assert.Contains("  Items: 1: Gust", output);
        Assert.Contains("> forecast pell brigand-1 2\nForecast Pell -> Brigand 1 with Cinder:", output);
        Assert.DoesNotContain("ERROR", output);
    }

    [Fact]
    public void ASlotNumberUnmovedBySwingsIsAccepted()
    {
        var output = Play("move pell 2,4\nattack pell brigand-1 1\nforecast pell brigand-1 1\n");

        Assert.Contains("> forecast pell brigand-1 1\nForecast Pell -> Brigand 1 with Cinder:", output);
        Assert.DoesNotContain("ERROR", output);
    }

    [Fact]
    public void TheSlotsARefusedNamePrintsCountAsAListing()
    {
        var output = Play("move pell 2,4\nattack pell brigand-1 gust\nforecast pell brigand-1 bolt\nforecast pell brigand-1 1\n");

        Assert.Contains("ERROR: Pell carries no 'bolt'; slots: 1 Gust, 2 Cinder\n", output);
        Assert.Contains("> forecast pell brigand-1 1\nForecast Pell -> Brigand 1 with Gust:", output);
    }

    [Fact]
    public void AMultiWordNameAndTheIdNameTheSameWeapon()
    {
        var output = Play("forecast ottilie brigand-1 iron bow from 3,5\nforecast ottilie brigand-1 iron_bow from 3,5\nforecast ottilie brigand-1 1 from 3,5\n");

        var lines = output.Split('\n').Where(line => line.StartsWith("Forecast Ottilie -> ", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, lines.Count);
        Assert.All(lines, line => Assert.Equal(lines[2], line));
        Assert.DoesNotContain("ERROR", output);
    }

    [Fact]
    public void AnUnknownNameIsRejectedWithTheSlotsListed()
    {
        var output = Play("forecast pell brigand-1 iron sword from 2,4\nattack pell brigand-1 bolt\n");

        Assert.Contains("> forecast pell brigand-1 iron sword from 2,4\nERROR: Pell carries no 'iron sword'; slots: 1 Cinder, 2 Gust\n", output);
        Assert.Contains("> attack pell brigand-1 bolt\nERROR: Pell carries no 'bolt'; slots: 1 Cinder, 2 Gust\n", output);
    }

    [Theory]
    [InlineData("forecast pell brigand-1 at 2,4")]
    [InlineData("forecast pell brigand-1 gust 2,4")]
    [InlineData("attack pell brigand-1 gust 2")]
    public void ANameWordThatIsNotANameStaysAUsageError(string command)
    {
        var output = Play(command + "\n");

        Assert.Contains("> " + command + "\nERROR: Usage: ", output);
    }

    [Fact]
    public void AnAmbiguousNameIsRejectedAndAsksForTheSlotNumber()
    {
        var content = Path.Combine(Path.GetTempPath(), "ironwake-477-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(Fixture.RealContentDirectory(), content);
        var cast = Path.Combine(content, "units", "cast.json");
        var text = File.ReadAllText(cast);
        var first = text.IndexOf("\"field_dressing\"", StringComparison.Ordinal);
        File.WriteAllText(cast, text[..first] + "\"iron_sword\"" + text[(first + "\"field_dressing\"".Length)..]);
        var script = Path.Combine(content, "ambiguous.script");
        File.WriteAllText(script, "forecast captain brigand-1 iron sword\nforecast captain brigand-1 2\n");
        try
        {
            var output = ConsoleCapture.Run(() => Program.Main(["play", "old_mill_road", "--seed", "7", "--script", script, "--content", content]));

            Assert.Contains("> forecast captain brigand-1 iron sword\nERROR: Alder Fenn carries 'iron sword' in slots 1 and 2; give the slot number; slots: 1 Iron Sword, 2 Iron Sword\n", output);
            Assert.DoesNotContain("> forecast captain brigand-1 2\nERROR: Alder Fenn carries", output);
        }
        finally
        {
            Directory.Delete(content, recursive: true);
        }
    }

    [Fact]
    public void AnAmbiguousNameMatchesEverySlotThatCarriesIt()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var inventory = new Inventory(ValueList<ItemStack>.From([new ItemStack("iron_sword", 40), new ItemStack("field_dressing", 1), new ItemStack("iron_sword", 12)]));

        Assert.Equal(new[] { 0, 2 }, PlaySession.SlotsNamed(inventory, content, "Iron Sword"));
        Assert.Equal(new[] { 1 }, PlaySession.SlotsNamed(inventory, content, "FIELD_DRESSING"));
        Assert.Empty(PlaySession.SlotsNamed(inventory, content, "sword"));
    }

    private static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.GetDirectories(from))
        {
            CopyDirectory(directory, Path.Combine(to, Path.GetFileName(directory)));
        }
    }
}
