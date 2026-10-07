using Ironwake.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The second promotion tier (issue 704, DESIGN section 3): each of the eight first-tier classes
/// has one advanced form, named in <c>classes.json</c> by <c>advances</c>. Only a unit in the base
/// class may take the step, at level 10 and rank C in the base's main weapon; the form keeps every
/// weapon type of its base and grows as its base does; the step's seal is the advanced price.
/// </summary>
public class AdvancedClassTests
{
    private static GameContent Content => MapFixture.Content;

    private static Unit Recruit(string id) => Content.Cast.Single(u => u.Id == id);

    private static Unit Ready(string id, string classId, WeaponType type) =>
        Recruit(id) with { ClassId = classId, Level = 7, Skill = WeaponSkill.Zero.With(type, WeaponRanks.Threshold(WeaponRank.C)) };

    [Theory]
    [InlineData("halberdier", "pikeman", "lance")]
    [InlineData("berserker", "reaver", "axe")]
    [InlineData("marksman", "bowman", "bow")]
    [InlineData("scholar", "adept", "lore")]
    [InlineData("warden", "chaplain", "faith")]
    [InlineData("lancer", "outrider", "lance")]
    [InlineData("skycaptain", "skyrider", "lance")]
    [InlineData("sentinel", "bulwark", "lance")]
    public void EachFirstTierClassHasOneAdvancedFormAtLevelSevenAndThePointsGate(string formId, string baseId, string weapon)
    {
        var form = Content.Class(formId);

        Assert.Equal(baseId, form.Advances?.Id);
        Assert.Equal(baseId, form.BaseId);
        Assert.Equal(7, form.Certification.Level);
        Assert.Equal($"level 7, {weapon} 50 (between D and C)", form.Certification.Describe());
        Assert.Single(Content.Classes.Values, c => c.Advances?.Id == baseId && c.Unique is null);
    }

    [Fact]
    public void EveryVisibleFirstTierClassButTheCadetHasAnAdvancedForm()
    {
        var bases = Content.Classes.Values.Where(c => c.Advances is null && !c.Hidden && !c.Enemy && c.Id != "cadet").Select(c => c.Id);

        Assert.All(bases, id => Assert.Contains(Content.Classes.Values, c => c.Advances?.Id == id));
    }

    [Fact]
    public void AnAdvancedFormKeepsItsBasesWeaponsAndGrowths()
    {
        foreach (var form in Content.Classes.Values.Where(c => c.Advances is not null && c.Unique is null))
        {
            Assert.All(form.Advances!.Weapons, w => Assert.True(form.CanUse(w), $"{form.Id} lacks {w}"));
            Assert.Equal(form.Advances.GrowthModifiers, form.GrowthModifiers);
        }
    }

    [Fact]
    public void AnAdvancedFormChangesAWeaponOrMovOverItsBaseWhereDataCanSayIt()
    {
        Assert.True(Content.Class("halberdier").CanUse(WeaponType.Axe));
        Assert.True(Content.Class("lancer").CanUse(WeaponType.Axe));
        Assert.True(Content.Class("warden").CanUse(WeaponType.Sword));
        Assert.True(Content.Class("sentinel").CanUse(WeaponType.Bow));
        Assert.Contains("canto", Content.Class("lancer").Abilities);
    }

    [Fact]
    public void AUnitNotInTheBaseClassIsRefusedTheAdvancedFormNamingTheBase()
    {
        var cadet = Ready("teodor", "cadet", WeaponType.Lance);

        var refusals = Certifications.Check(cadet, Content.Class("halberdier"));

        Assert.Equal(new[] { "needs to be a Pikeman first" }, refusals.Select(r => r.Text));
        Assert.Equal("advances", refusals[0].Requirement);
    }

    [Fact]
    public void AnotherAdvancedFormDoesNotStandInForTheBase()
    {
        var lancer = Ready("teodor", "lancer", WeaponType.Lance);

        Assert.Contains(Certifications.Check(lancer, Content.Class("halberdier")), r => r.Text == "needs to be a Pikeman first");
    }

    [Fact]
    public void APikemanAtLevelTenWithALanceAtCIsPromotedToHalberdier()
    {
        var pikeman = Ready("teodor", "pikeman", WeaponType.Lance);

        Assert.Empty(Certifications.Check(pikeman, Content.Class("halberdier")));
        Assert.Equal("halberdier", Certifications.Certify(pikeman, Content.Class("halberdier")).ClassId);
    }

    [Fact]
    public void APikemanShortOfTheLevelAndTheGateIsRefusedNamingBoth()
    {
        var pikeman = Recruit("teodor") with { ClassId = "pikeman", Level = 6, Skill = WeaponSkill.Zero.With(WeaponType.Lance, WeaponRanks.Threshold(WeaponRank.D)) };

        Assert.Equal(new[] { "needs level 7, has 6", "needs lance 50, has 30" }, Certifications.Check(pikeman, Content.Class("halberdier")).Select(r => r.Text));
    }

    [Fact]
    public void AGateRefusalNamesTheWeaponTypeByItsScreenLabel()
    {
        var adept = Recruit("pell") with { Level = 10 };

        Assert.Equal(new[] { "needs lore 50, has 0" }, Certifications.Check(adept, Content.Class("scholar")).Select(r => r.Text));
    }

    [Fact]
    public void MasteryPointsAndMasteredAbilitiesCarryThroughBothSteps()
    {
        var cadet = Recruit("brannock") with
        {
            Level = 10,
            Skill = WeaponSkill.Zero.With(WeaponType.Lance, WeaponRanks.Threshold(WeaponRank.C)),
            Mastery = MasteryProgress.Empty.With("cadet", 12),
            Abilities = ValueList<string>.Of("fundamentals"),
        };

        var pikeman = Certifications.Certify(cadet, Content.Class("pikeman")) with { Mastery = cadet.Mastery.With("pikeman", 5) };
        var halberdier = Certifications.Certify(pikeman, Content.Class("halberdier"));

        Assert.Equal("halberdier", halberdier.ClassId);
        Assert.Equal(12, halberdier.Mastery.Points("cadet"));
        Assert.Equal(5, halberdier.Mastery.Points("pikeman"));
        Assert.Contains("fundamentals", halberdier.Abilities);
    }

    [Fact]
    public void TheStepIntoAnAdvancedFormPaysTheAdvancedSeal()
    {
        var pikeman = Ready("teodor", "pikeman", WeaponType.Lance);
        var record = CampaignRecord.Start(Content, 5) with { Purse = 1200, Roster = ValueList<Unit>.Of(pikeman) };

        var result = record.Certify("teodor", "halberdier", Content);

        Assert.Equal(1000, Content.Campaign.SealFor(Content.Class("halberdier")));
        Assert.Equal(500, Content.Campaign.SealFor(Content.Class("pikeman")));
        Assert.True(result.Accepted, result.Text);
        Assert.Equal(200, result.Record.Purse);
        Assert.Equal("a seal costs 1000 and the purse holds 999", (record with { Purse = 999 }).Certify("teodor", "halberdier", Content).Text);
    }

    private const string TwoTier = """
        { "classes": [
          { "id": "cadet", "name": "Cadet", "movement": "infantry", "mov": 4, "weapons": ["sword"] },
          { "id": "fencer", "name": "Fencer", "movement": "infantry", "mov": 4, "weapons": ["sword"], "growthModifiers": { "spd": 10 } },
          { "id": "duelist", "name": "Duelist", "movement": "infantry", "mov": 5, "weapons": ["sword", "axe"], "advances": "fencer", "certification": { "level": 10 } }
        ] }
        """;

    private static ContentException Fails(string classes) =>
        Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(classes: classes)));

    [Fact]
    public void AnAdvancedFormLoadsWithItsBasesGrowthsAndRoundTrips()
    {
        var content = ContentLoader.Parse(Fixture.Files(classes: TwoTier));

        Assert.Equal("fencer", content.Class("duelist").Advances?.Id);
        Assert.Equal(Stats.Zero.With(Stat.Spd, 10), content.Class("duelist").GrowthModifiers);

        var written = ContentSerializer.Write(content);
        var reloaded = ContentLoader.Parse(written);
        Assert.Equal(content, reloaded);
        Assert.Equal(written.Classes.Text, ContentSerializer.Write(reloaded).Classes.Text);
    }

    [Theory]
    [InlineData("\"advances\": \"fencer\"", "\"advances\": \"nobody\"", "advances", "unknown class 'nobody'")]
    [InlineData("\"advances\": \"fencer\"", "\"advances\": \"duelist\"", "advances", "'duelist' must be another class that is not hidden")]
    [InlineData("\"weapons\": [\"sword\"], \"growthModifiers\"", "\"weapons\": [\"sword\"], \"hidden\": true, \"growthModifiers\"", "advances", "'fencer' must be another class that is not hidden")]
    [InlineData("\"weapons\": [\"sword\", \"axe\"]", "\"weapons\": [\"axe\"]", "weapons", "must keep every weapon type of 'fencer'; missing sword")]
    [InlineData("\"advances\": \"fencer\",", "\"advances\": \"fencer\", \"growthModifiers\": { \"str\": 5 },", "growthModifiers", "an advanced form grows as its base, 'fencer', does; name none")]
    public void ABadAdvancedFormIsRefusedNamingTheClassAndTheField(string find, string replace, string field, string message)
    {
        var e = Fails(TwoTier.Replace(find, replace));

        Assert.Equal(ContentFiles.ClassesName, e.File);
        Assert.Equal("duelist", e.Entry);
        Assert.Equal(field, e.Field);
        Assert.Contains(message, e.Message);
    }

    [Fact]
    public void AFormAboveAnAdvancedFormIsRefused()
    {
        var three = TwoTier.Replace("\n] }", ",\n  { \"id\": \"master\", \"name\": \"Master\", \"movement\": \"infantry\", \"mov\": 5, \"weapons\": [\"sword\", \"axe\"], \"advances\": \"duelist\" }\n] }");

        var e = Fails(three);

        Assert.Equal("master", e.Entry);
        Assert.Equal("advances", e.Field);
        Assert.Contains("'duelist' is itself an advanced form; a class has one step above it", e.Message);
    }

    private const string Verbs = """
        { "abilities": [
          { "id": "reach", "name": "Reach", "text": "Bows reach further.", "effect": { "kind": "range", "weapon": "bow", "range": 1 } },
          { "id": "mend", "name": "Mend", "text": "Heals reach further.", "effect": { "kind": "range", "heals": true, "range": 1 } },
          { "id": "feast", "name": "Feast", "text": "A kill heals.", "effect": { "kind": "killheal", "heal": 5, "wielding": "axe" } }
        ] }
        """;

    private const string StrikeOnlyClasses = """
        { "classes": [
          { "id": "cadet", "name": "Cadet", "movement": "infantry", "mov": 4, "weapons": ["sword"] },
          { "id": "seer", "name": "Seer", "movement": "infantry", "mov": 4, "weapons": ["reason", "faith"], "strikeOnly": ["faith"], "grants": { "faith": "D" }, "abilities": ["reach", "mend", "feast"] }
        ] }
        """;

    [Fact]
    public void TheVerbEffectsAndAStrikeOnlyClassLoadAndRoundTrip()
    {
        var content = ContentLoader.Parse(Fixture.Files(classes: StrikeOnlyClasses, abilities: Verbs));

        Assert.Equal(new RangeEffect(WeaponType.Bow, false, 1), content.Ability("reach").Effect);
        Assert.Equal(new RangeEffect(null, true, 1), content.Ability("mend").Effect);
        Assert.Equal(new KillHealEffect(5, WeaponType.Axe), content.Ability("feast").Effect);
        Assert.Equal(ValueList<WeaponType>.Of(WeaponType.Faith), content.Class("seer").StrikeOnly);
        Assert.Equal(ValueList<(WeaponType, WeaponRank)>.Of((WeaponType.Faith, WeaponRank.D)), content.Class("seer").Grants);

        var written = ContentSerializer.Write(content);
        var reloaded = ContentLoader.Parse(written);
        Assert.Equal(content, reloaded);
        Assert.Equal(written.Abilities.Text, ContentSerializer.Write(reloaded).Abilities.Text);
        Assert.Equal(written.Classes.Text, ContentSerializer.Write(reloaded).Classes.Text);
    }

    [Theory]
    [InlineData("\"weapon\": \"bow\", \"range\": 1", "\"weapon\": \"bow\", \"heals\": true, \"range\": 1", "reach", "effect", "names a weapon type or heals: true, not both")]
    [InlineData("\"weapon\": \"bow\", \"range\": 1", "\"range\": 1", "reach", "effect", "names a weapon type or heals: true, not both")]
    [InlineData("\"weapon\": \"bow\", \"range\": 1", "\"weapon\": \"bow\", \"range\": 0", "reach", "effect.range", "must be at least 1")]
    [InlineData("\"heal\": 5", "\"heal\": 0", "feast", "effect.heal", "must be at least 1")]
    public void ABadVerbEffectIsRefusedNamingTheAbilityAndTheField(string find, string replace, string entry, string field, string message)
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(classes: StrikeOnlyClasses, abilities: Verbs.Replace(find, replace))));

        Assert.Equal(ContentFiles.AbilitiesName, e.File);
        Assert.Equal(entry, e.Entry);
        Assert.Equal(field, e.Field);
        Assert.Contains(message, e.Message);
    }

    [Theory]
    [InlineData("\"strikeOnly\": [\"faith\"]", "\"strikeOnly\": [\"bow\"]", "strikeOnly", "bow is not in the class's weapons")]
    [InlineData("\"strikeOnly\": [\"faith\"]", "\"strikeOnly\": [\"faith\", \"faith\"]", "strikeOnly", "must not repeat a weapon type")]
    [InlineData("\"grants\": { \"faith\": \"D\" }", "\"grants\": { \"bow\": \"D\" }", "grants.bow", "is not in the class's weapons")]
    public void ABadStrikeOnlyOrGrantIsRefusedNamingTheClassAndTheField(string find, string replace, string field, string message)
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(classes: StrikeOnlyClasses.Replace(find, replace), abilities: Verbs)));

        Assert.Equal(ContentFiles.ClassesName, e.File);
        Assert.Equal("seer", e.Entry);
        Assert.Equal(field, e.Field);
        Assert.Contains(message, e.Message);
    }
}
