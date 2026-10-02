using System.Text.Json;
using Ironwake.Cli;
using Ironwake.Content;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Item descriptions and the item card (issue 650): every weapon, spell and consumable carries one
/// line, the validator refuses an item without it, and the console's <c>about</c> command and the
/// protocol's <c>about</c> query print one card, the core's.
/// </summary>
[Collection("console")]
public class ItemCardTests
{
    private static string Ebb => Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "ebb_ford_tide.map");

    private static GameContent Real => ContentLoader.Load(Fixture.RealContentDirectory());

    private static string WeaponWith(string tail) =>
        "{ \"weapons\": [ { \"id\": \"w\", \"name\": \"W\", \"type\": \"sword\", \"mt\": 5, \"hit\": 90, \"crit\": 0, \"wt\": 5, \"minRange\": 1, \"maxRange\": 1, \"durability\": 40, \"rank\": \"E\"" + tail + " } ] }";

    private static string ItemWith(string tail) =>
        "{ \"items\": [ { \"id\": \"x\", \"name\": \"X\", \"heals\": 1, \"uses\": 1" + tail + " } ] }";

    [Theory]
    [InlineData("", "description")]
    [InlineData(", \"description\": \"\"", "description")]
    [InlineData(", \"description\": \"One line.\\nAnd another.\"", "description")]
    [InlineData(", \"description\": 3", "description")]
    public void AWeaponWithoutAOneLineDescriptionIsRefusedNamingFileEntryAndField(string tail, string field)
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(weapons: WeaponWith(tail))));

        Assert.Equal(ContentFiles.WeaponsName, e.File);
        Assert.Equal("w", e.Entry);
        Assert.Equal(field, e.Field);
    }

    [Theory]
    [InlineData("")]
    [InlineData(", \"description\": \"\"")]
    [InlineData(", \"description\": \"One line.\\rAnd another.\"")]
    public void AnItemWithoutAOneLineDescriptionIsRefusedNamingFileEntryAndField(string tail)
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(items: ItemWith(tail))));

        Assert.Equal(ContentFiles.ItemsName, e.File);
        Assert.Equal("x", e.Entry);
        Assert.Equal("description", e.Field);
    }

    [Fact]
    public void ADescriptionLongerThanAConsoleLineIsRefused()
    {
        var tail = ", \"description\": \"" + new string('a', ContentLoader.DescriptionMax + 1) + "\"";

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(weapons: WeaponWith(tail))));

        Assert.Equal("description", e.Field);
        Assert.Contains($"at most {ContentLoader.DescriptionMax}", e.Message, StringComparison.Ordinal);

        var atMost = WeaponWith(", \"description\": \"" + new string('a', ContentLoader.DescriptionMax) + "\"").Replace("\"id\": \"w\"", "\"id\": \"iron_sword\"", StringComparison.Ordinal);
        var fits = ContentLoader.Parse(Fixture.Files(weapons: atMost));
        Assert.Equal(ContentLoader.DescriptionMax, fits.Weapon("iron_sword").Description.Length);
    }

    [Fact]
    public void EveryShippedWeaponSpellAndItemCarriesADistinctDescription()
    {
        var content = Real;
        var lines = content.Weapons.Values.Select(w => w.Description).Concat(content.Items.Values.Select(i => i.Description)).ToList();

        Assert.All(lines, line => Assert.False(string.IsNullOrWhiteSpace(line)));
        Assert.Equal(lines.Count, lines.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void DescriptionsRoundTripThroughTheSerializer()
    {
        var content = Real;
        var again = ContentLoader.Parse(ContentSerializer.Write(content));

        Assert.Equal(content.Weapon("iron_sword").Description, again.Weapon("iron_sword").Description);
        Assert.Equal(content.Item("field_dressing").Description, again.Item("field_dressing").Description);
    }

    [Fact]
    public void TheCardReadsItsNumbersFromTheRecordAndEndsOnTheDescription()
    {
        var content = Real;
        var bow = content.Weapon("iron_bow");

        Assert.Equal(
            $"Iron Bow, bow E. Acc {bow.Hit}, Power {bow.Mt}, Crit {bow.Crit}, Wt {bow.Wt}, range 2, {bow.Durability} uses. Crit +20 against flying. A crit on a flier grounds it for plain damage. {bow.Description}",
            ItemCard.Text(content, "iron_bow"));
        Assert.StartsWith("Cinder, lore E. Acc 90, Power 5, Crit 0, Wt 3, range 1-2, 8 uses a battle. ", ItemCard.Text(content, "cinder"), StringComparison.Ordinal);
        Assert.StartsWith("Salve, faith E. Acc ", ItemCard.Text(content, "salve"), StringComparison.Ordinal);
        var dressing = content.Item("field_dressing");
        Assert.Equal($"Field Dressing. Heals {dressing.Heals} HP, {dressing.Uses} uses. {dressing.Description}", ItemCard.Text(content, "field_dressing"));
    }

    [Theory]
    [InlineData("iron_sword", "iron_sword")]
    [InlineData("Iron Sword", "iron_sword")]
    [InlineData("field dressing", "field_dressing")]
    [InlineData("pitchfork", null)]
    public void TheCardFindsAnItemByIdOrNameInAnyCase(string named, string? expected)
    {
        Assert.Equal(expected, ItemCard.Find(Real, named));
    }

    [Fact]
    public void TheConsoleAboutCommandPrintsTheCard()
    {
        var content = Real;
        var script = Path.Combine(Path.GetTempPath(), "ironwake-about-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(script, "about iron sword\nabout pitchfork\n");
        try
        {
            var output = ConsoleCapture.Run(() => Ironwake.Cli.Program.Main(new[] { "play", Ebb, "--seed", "1", "--script", script, "--content", Fixture.RealContentDirectory() }));

            Assert.Contains("> about iron sword\n" + ItemCard.Text(content, "iron_sword") + "\n", output, StringComparison.Ordinal);
            Assert.Contains("ERROR: No item 'pitchfork'; name it by its id or name", output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(script);
        }
    }

    [Fact]
    public void TheProtocolAboutQueryCarriesTheConsolesCard()
    {
        var content = Real;
        var state = BattleState.From(MapFiles.Load(Ebb, content), content, content.Cast, 1);
        var session = new ProtocolSession(content, state, new StringWriter());

        using var answer = JsonDocument.Parse(session.Answer("""{"query":"about","item":"Field Dressing"}"""));
        Assert.Equal("field_dressing", answer.RootElement.GetProperty("item").GetString());
        Assert.Equal(ItemCard.Text(content, "field_dressing"), answer.RootElement.GetProperty("text").GetString());

        using var refused = JsonDocument.Parse(session.Answer("""{"query":"about","item":"pitchfork"}"""));
        Assert.Equal("badRequest", refused.RootElement.GetProperty("error").GetProperty("reason").GetString());
    }
}
