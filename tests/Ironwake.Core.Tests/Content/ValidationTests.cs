using Ironwake.Content;

namespace Ironwake.Core.Tests.Content;

/// <summary>One case per validation rule in <see cref="ContentLoader"/>, each showing the rule firing.</summary>
public class ValidationTests
{
    private static ContentException Fails(ContentFiles files) =>
        Assert.Throws<ContentException>(() => ContentLoader.Parse(files));

    private static void AssertNames(ContentException e, string file, string? entry, string? field)
    {
        Assert.Equal(file, e.File);
        Assert.Equal(entry, e.Entry);
        Assert.Equal(field, e.Field);
        Assert.Contains(file, e.Message);
        if (entry is not null)
        {
            Assert.Contains(entry, e.Message);
        }

        if (field is not null)
        {
            Assert.Contains(field, e.Message);
        }
    }

    [Fact]
    public void TheFixtureItselfIsValid()
    {
        var content = ContentLoader.Parse(Fixture.Files());

        Assert.Single(content.Units);
    }

    [Fact]
    public void MissingDirectoryNamesTheDirectory()
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Load("/no/such/dir"));

        Assert.Contains("/no/such/dir", e.Message);
    }

    [Fact]
    public void MissingFileNamesTheFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ironwake-missing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, ContentFiles.ClassesName), Fixture.Classes);
            var e = Assert.Throws<ContentException>(() => ContentLoader.Load(dir));
            AssertNames(e, ContentFiles.WeaponsName, null, null);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void InvalidJsonNamesTheFile()
    {
        var e = Fails(Fixture.Files(weapons: "{ not json"));

        AssertNames(e, "weapons.json", null, null);
        Assert.Contains("invalid JSON", e.Message);
    }

    [Fact]
    public void RootMustBeAnObject()
    {
        var e = Fails(Fixture.Files(terrain: "[]"));

        AssertNames(e, "terrain.json", null, null);
    }

    [Fact]
    public void RootMustHaveTheListKey()
    {
        var e = Fails(Fixture.Files(classes: "{ \"units\": [] }"));

        AssertNames(e, "classes.json", null, "classes");
    }

    [Fact]
    public void EntriesMustBeObjects()
    {
        var e = Fails(Fixture.Files(classes: "{ \"classes\": [ 3 ] }"));

        AssertNames(e, "classes.json", "classes[0]", null);
    }

    [Fact]
    public void EntriesNeedAnId()
    {
        var e = Fails(Fixture.Files(classes: "{ \"classes\": [ { \"name\": \"X\" } ] }"));

        AssertNames(e, "classes.json", "classes[0]", "id");
    }

    [Fact]
    public void DuplicateIdsAreRejected()
    {
        var e = Fails(Fixture.Files(classes: """
            { "classes": [
              { "id": "cadet", "name": "Cadet", "movement": "infantry", "mov": 4, "weapons": ["sword"] },
              { "id": "cadet", "name": "Cadet", "movement": "infantry", "mov": 4, "weapons": ["sword"] }
            ] }
            """));

        AssertNames(e, "classes.json", "cadet", "id");
    }

    [Fact]
    public void EmptyListsAreRejected()
    {
        AssertNames(Fails(Fixture.Files(terrain: "{ \"terrain\": [] }")), "terrain.json", null, "terrain");
        AssertNames(Fails(Fixture.Files(classes: "{ \"classes\": [] }")), "classes.json", null, "classes");
        AssertNames(Fails(Fixture.Files(weapons: "{ \"weapons\": [] }")), "weapons.json", null, "weapons");
    }

    private static string ClassWith(string extra) =>
        "{ \"classes\": [ { \"id\": \"cadet\", \"name\": \"Cadet\", \"movement\": \"infantry\", \"mov\": 4, \"weapons\": [\"sword\"]" + extra + " } ] }";

    private static string ClassReplacing(string field, string value) =>
        ClassWith("").Replace("\"" + field + "\": " + Original(field), "\"" + field + "\": " + value);

    private static string Original(string field) => field switch
    {
        "mov" => "4",
        "movement" => "\"infantry\"",
        "weapons" => "[\"sword\"]",
        "name" => "\"Cadet\"",
        _ => throw new ArgumentException(field),
    };

    [Theory]
    [InlineData("mov", "\"four\"", "mov")]
    [InlineData("mov", "0", "mov")]
    [InlineData("movement", "\"swimming\"", "movement")]
    [InlineData("weapons", "[]", "weapons")]
    [InlineData("weapons", "[\"sword\", \"sword\"]", "weapons")]
    [InlineData("weapons", "[\"whip\"]", "weapons")]
    [InlineData("weapons", "\"sword\"", "weapons")]
    [InlineData("name", "\"\"", "name")]
    public void ClassFieldRulesFire(string field, string value, string expectedField)
    {
        var e = Fails(Fixture.Files(classes: ClassReplacing(field, value)));

        AssertNames(e, "classes.json", "cadet", expectedField);
    }

    [Fact]
    public void ClassMissingARequiredFieldNamesIt()
    {
        var e = Fails(Fixture.Files(classes: "{ \"classes\": [ { \"id\": \"cadet\", \"name\": \"Cadet\", \"movement\": \"infantry\", \"weapons\": [\"sword\"] } ] }"));

        AssertNames(e, "classes.json", "cadet", "mov");
    }

    [Fact]
    public void UnknownStatKeyInModifiersIsRejected()
    {
        var e = Fails(Fixture.Files(classes: ClassWith(", \"modifiers\": { \"strength\": 1 }")));

        AssertNames(e, "classes.json", "cadet", "strength");
    }

    [Fact]
    public void ModifiersMayOmitStatsAndDefaultToZero()
    {
        var content = ContentLoader.Parse(Fixture.Files(classes: ClassWith(", \"modifiers\": { \"str\": 2 }, \"growthModifiers\": { \"def\": -5 }")));
        var cadet = content.Class("cadet");

        Assert.Equal(2, cadet.Modifiers.Str);
        Assert.Equal(0, cadet.Modifiers.Hp);
        Assert.Equal(-5, cadet.GrowthModifiers.Def);
    }

    private static string TerrainWith(string cost, string extra = "") =>
        "{ \"terrain\": [ { \"id\": \"plain\", \"name\": \"Plain\", \"glyph\": \".\", \"cost\": " + cost + extra + " } ] }";

    private const string AllOne = "{ \"infantry\": 1, \"cavalry\": 1, \"flying\": 1, \"armored\": 1 }";

    [Theory]
    [InlineData("{ \"infantry\": 0, \"cavalry\": 1, \"flying\": 1, \"armored\": 1 }", "", "cost.infantry")]
    [InlineData("{ \"infantry\": 1, \"cavalry\": 1, \"flying\": 1 }", "", "armored")]
    [InlineData("{ \"infantry\": \"x\", \"cavalry\": 1, \"flying\": 1, \"armored\": 1 }", "", "infantry")]
    [InlineData("3", "", "cost")]
    [InlineData(AllOne, ", \"heal\": 101", "heal")]
    [InlineData(AllOne, ", \"avoid\": -1", "avoid")]
    [InlineData(AllOne, ", \"appliesToFlyers\": \"yes\"", "appliesToFlyers")]
    public void TerrainFieldRulesFire(string cost, string extra, string expectedField)
    {
        var e = Fails(Fixture.Files(terrain: TerrainWith(cost, extra)));

        AssertNames(e, "terrain.json", "plain", expectedField);
    }

    [Fact]
    public void GlyphMustBeOneCharacter()
    {
        var e = Fails(Fixture.Files(terrain: TerrainWith(AllOne).Replace("\".\"", "\"..\"")));

        AssertNames(e, "terrain.json", "plain", "glyph");
    }

    [Fact]
    public void GlyphsMustBeUnique()
    {
        var e = Fails(Fixture.Files(terrain: """
            { "terrain": [
              { "id": "plain", "name": "Plain", "glyph": ".", "cost": { "infantry": 1, "cavalry": 1, "flying": 1, "armored": 1 } },
              { "id": "road", "name": "Road", "glyph": ".", "cost": { "infantry": 1, "cavalry": 1, "flying": 1, "armored": 1 } }
            ] }
            """));

        AssertNames(e, "terrain.json", "road", "glyph");
        Assert.Contains("plain", e.Message);
    }

    private static string WeaponWith(string body) =>
        "{ \"weapons\": [ { \"id\": \"w\", \"name\": \"W\", " + body + " } ] }";

    private const string Sword = "\"type\": \"sword\", \"mt\": 5, \"hit\": 90, \"crit\": 0, \"wt\": 5, \"minRange\": 1, \"maxRange\": 1, \"durability\": 40";

    [Theory]
    [InlineData("\"type\": \"sword\", \"mt\": -1, \"hit\": 90, \"crit\": 0, \"wt\": 5, \"minRange\": 1, \"maxRange\": 1, \"durability\": 40", "mt")]
    [InlineData("\"type\": \"sword\", \"mt\": 5, \"hit\": 201, \"crit\": 0, \"wt\": 5, \"minRange\": 1, \"maxRange\": 1, \"durability\": 40", "hit")]
    [InlineData("\"type\": \"sword\", \"mt\": 5, \"hit\": 90, \"crit\": 101, \"wt\": 5, \"minRange\": 1, \"maxRange\": 1, \"durability\": 40", "crit")]
    [InlineData("\"type\": \"sword\", \"mt\": 5, \"hit\": 90, \"crit\": 0, \"wt\": -1, \"minRange\": 1, \"maxRange\": 1, \"durability\": 40", "wt")]
    [InlineData("\"type\": \"sword\", \"mt\": 5, \"hit\": 90, \"crit\": 0, \"wt\": 5, \"minRange\": 0, \"maxRange\": 1, \"durability\": 40", "minRange")]
    [InlineData("\"type\": \"sword\", \"mt\": 5, \"hit\": 90, \"crit\": 0, \"wt\": 5, \"minRange\": 2, \"maxRange\": 1, \"durability\": 40", "maxRange")]
    [InlineData("\"type\": \"sword\", \"mt\": 5, \"hit\": 90, \"crit\": 0, \"wt\": 5, \"minRange\": 1, \"maxRange\": 1, \"durability\": 0", "durability")]
    [InlineData("\"type\": \"sword\", \"mt\": 5, \"hit\": 90, \"crit\": 0, \"wt\": 5, \"minRange\": 1, \"maxRange\": 1", "durability")]
    [InlineData("\"type\": \"club\", \"mt\": 5, \"hit\": 90, \"crit\": 0, \"wt\": 5, \"minRange\": 1, \"maxRange\": 1, \"durability\": 40", "type")]
    [InlineData(Sword + ", \"heals\": true", "heals")]
    [InlineData(Sword + ", \"healBase\": 3", "healBase")]
    [InlineData(Sword + ", \"effective\": [\"boats\"]", "effective")]
    [InlineData(Sword + ", \"effective\": [\"flying\", \"flying\"]", "effective")]
    [InlineData("\"type\": \"faith\", \"mt\": 0, \"hit\": 100, \"crit\": 0, \"wt\": 2, \"minRange\": 1, \"maxRange\": 1, \"durability\": 8, \"heals\": true, \"healBase\": -1", "healBase")]
    public void WeaponFieldRulesFire(string body, string expectedField)
    {
        var e = Fails(Fixture.Files(weapons: WeaponWith(body)));

        AssertNames(e, "weapons.json", "w", expectedField);
    }

    private const string Nine = "{ \"hp\": 20, \"str\": 6, \"mag\": 1, \"dex\": 5, \"spd\": 6, \"lck\": 3, \"def\": 4, \"res\": 2, \"cha\": 5 }";

    private static string UnitWith(string body) =>
        "{ \"units\": [ { \"id\": \"u\", \"name\": \"U\", " + body + " } ] }";

    private static string Recruit(string extra = "", string stats = Nine, string growths = Nine, string classId = "cadet") =>
        "\"class\": \"" + classId + "\", \"stats\": " + stats + ", \"growths\": " + growths + extra;

    [Fact]
    public void UnknownClassIsRejected()
    {
        var e = Fails(Fixture.Files(units: UnitWith(Recruit(classId: "paladin"))));

        AssertNames(e, "units/units.json", "u", "class");
        Assert.Contains("paladin", e.Message);
    }

    [Theory]
    [InlineData(", \"level\": 0", "level")]
    [InlineData(", \"level\": 31", "level")]
    [InlineData(", \"exp\": -1", "exp")]
    [InlineData(", \"exp\": 100", "exp")]
    [InlineData(", \"inventory\": [ { \"item\": \"stick\" } ]", "inventory[0].item")]
    [InlineData(", \"inventory\": [ { \"item\": \"iron_sword\", \"uses\": 41 } ]", "inventory[0].uses")]
    [InlineData(", \"inventory\": [ { \"item\": \"iron_sword\", \"uses\": 0 } ]", "inventory[0].uses")]
    [InlineData(", \"inventory\": [ \"iron_sword\" ]", "inventory[0]")]
    [InlineData(", \"inventory\": [ { \"item\": \"iron_sword\" }, { \"item\": \"iron_sword\" }, { \"item\": \"iron_sword\" }, { \"item\": \"iron_sword\" }, { \"item\": \"iron_sword\" }, { \"item\": \"iron_sword\" } ]", "inventory")]
    [InlineData(", \"abilities\": [1]", "abilities")]
    public void UnitFieldRulesFire(string extra, string expectedField)
    {
        var e = Fails(Fixture.Files(units: UnitWith(Recruit(extra))));

        AssertNames(e, "units/units.json", "u", expectedField);
    }

    [Fact]
    public void UnitStatsMustListAllNine()
    {
        var e = Fails(Fixture.Files(units: UnitWith(Recruit(stats: "{ \"hp\": 20 }"))));

        AssertNames(e, "units/units.json", "u", "str");
    }

    [Fact]
    public void UnitHpMustBeAtLeastOne()
    {
        var e = Fails(Fixture.Files(units: UnitWith(Recruit(stats: Nine.Replace("\"hp\": 20", "\"hp\": 0")))));

        AssertNames(e, "units/units.json", "u", "stats.hp");
    }

    [Fact]
    public void UnitStatsAndGrowthsMustNotBeNegative()
    {
        var stats = Fails(Fixture.Files(units: UnitWith(Recruit(stats: Nine.Replace("\"def\": 4", "\"def\": -1")))));
        var growths = Fails(Fixture.Files(units: UnitWith(Recruit(growths: Nine.Replace("\"res\": 2", "\"res\": -1")))));

        AssertNames(stats, "units/units.json", "u", "stats.def");
        AssertNames(growths, "units/units.json", "u", "growths.res");
    }

    [Fact]
    public void UnitIdsAreUniqueAcrossFiles()
    {
        var e = Fails(Fixture.Files(secondUnitsFile: Fixture.Units));

        AssertNames(e, "units/more.json", "recruit", "id");
        Assert.Contains("units/units.json", e.Message);
    }

    [Fact]
    public void InventoryUsesDefaultToTheWeaponsDurabilityAndOptionalFieldsLoad()
    {
        var content = ContentLoader.Parse(Fixture.Files(units: UnitWith(Recruit(
            ", \"inventory\": [ { \"item\": \"iron_sword\" }, { \"item\": \"iron_sword\", \"uses\": 7 } ], \"abilities\": [\"vigilance\"], \"region\": \"north\", \"personality\": \"Counts everything twice.\""))));
        var unit = content.Unit("u");

        Assert.Equal(40, unit.Inventory.Items[0].Uses);
        Assert.Equal(7, unit.Inventory.Items[1].Uses);
        Assert.Equal(Core.ValueList<string>.Of("vigilance"), unit.Abilities);
        Assert.Equal("north", unit.Region);
        Assert.Equal("Counts everything twice.", unit.Personality);
    }
}
