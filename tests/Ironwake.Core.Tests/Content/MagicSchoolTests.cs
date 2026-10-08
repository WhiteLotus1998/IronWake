using Ironwake.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The schools of Lore magic, step one (issue 1242, DECISIONS/0296): a <c>school</c> tag on a Lore
/// tome, a class's reach as <c>schools</c> in <c>classes.json</c>, and the rule that a schooled tome
/// is wielded only in a class that reaches its school. Cinder is fire; Bolt and Gust are lightning
/// (Gust folded into lightning as storm, issue 1319, DECISIONS/0317).
/// </summary>
public class MagicSchoolTests
{
    private static GameContent Content => MapFixture.Content;

    [Theory]
    [InlineData("cinder", MagicSchool.Fire)]
    [InlineData("bolt", MagicSchool.Lightning)]
    [InlineData("gust", MagicSchool.Lightning)]
    [InlineData("pell_commonplace", null)]
    public void ShippedTomesCarryTheirSchoolAndTheCommonplaceNone(string id, MagicSchool? school) =>
        Assert.Equal(school, Content.Weapon(id).School);

    [Fact]
    public void OnlyCinderBoltAndGustAreSchooledInContent() =>
        Assert.Equal(new[] { "bolt", "cinder", "gust" }, Content.Weapons.Values.Where(w => w.School is not null).Select(w => w.Id).Order(StringComparer.Ordinal));

    [Theory]
    [InlineData("adept", "fire,ice,lightning")]
    [InlineData("scholar", "fire,ice,lightning,earth")]
    [InlineData("marshal", "fire,lightning")]
    [InlineData("commander", "fire,lightning")]
    public void EachLoreClassReachesTheSchoolsOfTheLean(string classId, string schools) =>
        Assert.Equal(schools, string.Join(",", Content.Class(classId).Schools.Select(s => s.Label())));

    [Fact]
    public void OnlyALoreClassReachesASchool() =>
        Assert.All(Content.Classes.Values.Where(c => c.Schools.Count > 0), c => Assert.True(c.CanUse(WeaponType.Reason), c.Id));

    [Fact]
    public void AnAdvancedFormKeepsEverySchoolOfItsBase()
    {
        foreach (var form in Content.Classes.Values.Where(c => c.Advances is not null))
        {
            Assert.All(form.Advances!.Schools, s => Assert.True(form.Reaches(s), $"{form.Id} lacks {s}"));
        }
    }

    [Fact]
    public void NoShippedUnitOrHireCarriesATomeItsClassDoesNotReach()
    {
        var carriers = Content.Units.Values.Select(u => (u.Id, u.ClassId, Items: u.Inventory.Items.Select(i => i.ItemId)))
            .Concat(Content.Campaign.Keep.Hires.Select(h => (h.Id, h.ClassId, Items: h.Items.AsEnumerable())));
        foreach (var (id, classId, items) in carriers)
        {
            foreach (var item in items.Where(Content.Weapons.ContainsKey))
            {
                Assert.True(MagicSchoolExtensions.SchoolShort(Content.Cast[0], Content.Class(classId), Content.Weapon(item)) is null, $"{id} carries {item}");
            }
        }
    }

    [Fact]
    public void GustIsLightningAsStormAndCarriesNoRiderSoItStunsNoOne()
    {
        var gust = Content.Weapon("gust");

        Assert.Equal(MagicSchool.Lightning, gust.School);
        Assert.Null(gust.Rider);
        Assert.Null(Content.RiderOf(gust));
    }

    [Fact]
    public void EveryPlayerSideLoreClassReachesGustSoNoBuyerLosesIt()
    {
        var gust = Content.Weapon("gust");
        var lore = Content.Classes.Values.Where(c => !c.Enemy && c.CanUse(WeaponType.Reason)).ToList();

        Assert.NotEmpty(lore);
        Assert.All(lore, c => Assert.True(MagicSchoolExtensions.SchoolShort(Content.Cast[0], c, gust) is null, c.Id));
    }

    [Fact]
    public void PellStillWieldsHerGustAsAnAdept()
    {
        var pell = Content.Units["pell"];

        Assert.True(pell.CanWield(Content.Weapon("gust"), Content.Class(pell.ClassId)));
    }

    [Fact]
    public void SparkStormsCardNamesTheLightningSchool() =>
        Assert.StartsWith("Spark Storm, lore E, lightning school. Acc 90", ItemCard.Text(Content, "gust"), StringComparison.Ordinal);

    [Fact]
    public void TheAdeptHireStaysPlainWithCinderAlone()
    {
        var mattias = Content.Campaign.Keep.Hire("mattias")!;

        Assert.Equal("adept", mattias.ClassId);
        Assert.Equal(new[] { "cinder" }, mattias.Items);
    }

    private const string SchoolClasses = """
        { "classes": [
          { "id": "cadet", "name": "Cadet", "movement": "infantry", "mov": 4, "weapons": ["sword"] },
          { "id": "pyre", "name": "Pyre", "movement": "infantry", "mov": 4, "weapons": ["reason"], "schools": ["fire"] },
          { "id": "kiln", "name": "Kiln", "movement": "infantry", "mov": 4, "weapons": ["reason"], "schools": ["fire", "earth"], "advances": "pyre" }
        ] }
        """;

    private const string SchoolWeapons = """
        { "weapons": [
          { "id": "iron_sword", "name": "Iron Sword", "type": "sword", "mt": 5, "hit": 90, "crit": 0, "wt": 5, "minRange": 1, "maxRange": 1, "durability": 40, "description": "A test line.", "rank": "E" },
          { "id": "spark", "name": "Spark", "type": "reason", "mt": 3, "hit": 90, "crit": 0, "wt": 1, "minRange": 1, "maxRange": 2, "durability": 8, "description": "A test line.", "rank": "E", "school": "fire" },
          { "id": "zap", "name": "Zap", "type": "reason", "mt": 3, "hit": 90, "crit": 0, "wt": 1, "minRange": 1, "maxRange": 2, "durability": 8, "description": "A test line.", "rank": "E", "school": "lightning" },
          { "id": "shove", "name": "Shove", "type": "reason", "mt": 3, "hit": 90, "crit": 0, "wt": 1, "minRange": 1, "maxRange": 2, "durability": 8, "description": "A test line.", "rank": "E" }
        ] }
        """;

    private static GameContent Schooled() => ContentLoader.Parse(Fixture.Files(classes: SchoolClasses, weapons: SchoolWeapons));

    [Fact]
    public void ASchooledTomeIsWieldedOnlyInAClassThatReachesItsSchool()
    {
        var content = Schooled();
        var pyre = content.Class("pyre");
        var mage = content.Units["recruit"] with { ClassId = "pyre" };

        Assert.True(mage.CanWield(content.Weapon("spark"), pyre));
        Assert.False(mage.CanWield(content.Weapon("zap"), pyre));
        Assert.True(mage.CanWield(content.Weapon("shove"), pyre));
    }

    [Fact]
    public void TheRefusalNamesTheSchoolAndWhatTheClassReaches()
    {
        var content = Schooled();
        var mage = content.Units["recruit"] with { ClassId = "pyre" };

        Assert.Equal("needs the lightning school; a Pyre reaches fire", MagicSchoolExtensions.SchoolShort(mage, content.Class("pyre"), content.Weapon("zap")));
        Assert.Null(MagicSchoolExtensions.SchoolShort(mage, content.Class("pyre"), content.Weapon("spark")));
        Assert.Null(MagicSchoolExtensions.SchoolShort(mage, content.Class("pyre"), content.Weapon("shove")));
    }

    [Fact]
    public void TheCardNamesATomesSchool()
    {
        Assert.StartsWith("Spark, lore E, fire school. Acc 90", ItemCard.Text(Schooled(), "spark"), StringComparison.Ordinal);
        Assert.StartsWith("Shove, lore E. Acc 90", ItemCard.Text(Schooled(), "shove"), StringComparison.Ordinal);
    }

    [Fact]
    public void SchoolsLoadAndRoundTrip()
    {
        var content = Schooled();

        Assert.Equal(ValueList<MagicSchool>.Of(MagicSchool.Fire, MagicSchool.Earth), content.Class("kiln").Schools);

        var written = ContentSerializer.Write(content);
        var reloaded = ContentLoader.Parse(written);
        Assert.Equal(content, reloaded);
        Assert.Equal(written.Classes.Text, ContentSerializer.Write(reloaded).Classes.Text);
        Assert.Equal(written.Weapons.Text, ContentSerializer.Write(reloaded).Weapons.Text);
    }

    [Theory]
    [InlineData("\"schools\": [\"fire\"] }", "\"schools\": [\"fire\", \"fire\"] }", "pyre", "schools", "must not repeat a school")]
    [InlineData("\"schools\": [\"fire\"] }", "\"schools\": [\"frost\"] }", "pyre", "schools", "frost")]
    [InlineData("\"weapons\": [\"sword\"] }", "\"weapons\": [\"sword\"], \"schools\": [\"fire\"] }", "cadet", "schools", "only a class that wields Lore (reason) reaches a school")]
    [InlineData("\"schools\": [\"fire\", \"earth\"]", "\"schools\": [\"earth\"]", "kiln", "schools", "must keep every school of 'pyre'; missing fire")]
    public void ABadSchoolsListIsRefusedNamingTheClassAndTheField(string find, string replace, string entry, string field, string message)
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(classes: SchoolClasses.Replace(find, replace), weapons: SchoolWeapons)));

        Assert.Equal(ContentFiles.ClassesName, e.File);
        Assert.Equal(entry, e.Entry);
        Assert.Equal(field, e.Field);
        Assert.Contains(message, e.Message);
    }

    [Theory]
    [InlineData("\"rank\": \"E\" },\n", "\"rank\": \"E\", \"school\": \"fire\" },\n", "iron_sword", "only a Lore (reason) tome belongs to a school")]
    [InlineData("\"school\": \"fire\"", "\"school\": \"wind\"", "spark", "wind")]
    public void ABadSchoolTagIsRefusedNamingTheWeaponAndTheField(string find, string replace, string entry, string message)
    {
        var weapons = SchoolWeapons.Replace(find, replace, StringComparison.Ordinal);
        Assert.NotEqual(SchoolWeapons, weapons);

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(classes: SchoolClasses, weapons: weapons)));

        Assert.Equal(ContentFiles.WeaponsName, e.File);
        Assert.Equal(entry, e.Entry);
        Assert.Equal("school", e.Field);
        Assert.Contains(message, e.Message);
    }

    [Fact]
    public void ATemplateCarryingATomeItsClassDoesNotReachIsRefused()
    {
        var units = Fixture.Units.Replace("\"class\": \"cadet\"", "\"class\": \"pyre\"").Replace("\"item\": \"iron_sword\"", "\"item\": \"zap\"");

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(classes: SchoolClasses, weapons: SchoolWeapons, units: units)));

        Assert.Equal("recruit", e.Entry);
        Assert.Equal("inventory[0].item", e.Field);
        Assert.Contains("recruit carries zap, which needs the lightning school; a Pyre reaches fire", e.Message);
    }
}
