using Ironwake.Content;

namespace Ironwake.Core.Tests.Content;

/// <summary>Issue 66's content half: abilities.json loads, validates each field, round-trips, and every ability id elsewhere must name an entry of it.</summary>
public class AbilityContentTests
{
    private const string Breakers = """
        { "abilities": [
          { "id": "axebreaker", "name": "Axebreaker", "text": "+20 hit and avoid against axes.",
            "effect": { "kind": "combat", "against": { "weapon": "axe" }, "hit": 20, "avoid": 20 } },
          { "id": "skyward", "name": "Skyward", "text": "+10 crit against fliers on foot.",
            "effect": { "kind": "combat", "against": { "weapon": "lance", "movement": "flying" }, "crit": 10 } },
          { "id": "steady", "name": "Steady", "text": "-5 crit taken from anyone.",
            "effect": { "kind": "combat", "critAvoid": 5 } },
          { "id": "vigilance", "name": "Vigilance", "text": "Def +2, HP +1.", "effect": { "kind": "stats", "stats": { "def": 2, "hp": 1 } } }
        ] }
        """;

    private static ContentException Fails(ContentFiles files) =>
        Assert.Throws<ContentException>(() => ContentLoader.Parse(files));

    private static void AssertNames(ContentException e, string file, string? entry, string? field)
    {
        Assert.Equal(file, e.File);
        Assert.Equal(entry, e.Entry);
        Assert.Equal(field, e.Field);
    }

    private static string One(string effect) =>
        "{ \"abilities\": [ { \"id\": \"a\", \"name\": \"A\", \"text\": \"One line.\", \"effect\": " + effect + " } ] }";

    private static string RecruitWithAbilities(string list) => Fixture.Units.Replace(
        "\"inventory\": [ { \"item\": \"iron_sword\" } ] }",
        "\"inventory\": [ { \"item\": \"iron_sword\" } ], \"abilities\": " + list + " }");

    [Fact]
    public void AbilitiesLoadWithTheirEffects()
    {
        var content = ContentLoader.Parse(Fixture.Files(abilities: Breakers));

        Assert.Equal(
            new CombatModifierEffect(new OpponentCondition(WeaponType.Axe, null), 20, 20, 0, 0),
            content.Ability("axebreaker").Effect);
        Assert.Equal(new OpponentCondition(WeaponType.Lance, MovementType.Flying), ((CombatModifierEffect)content.Ability("skyward").Effect).Against);
        Assert.Equal(OpponentCondition.Any, ((CombatModifierEffect)content.Ability("steady").Effect).Against);
        Assert.Equal(new StatDeltaEffect(Stats.Zero with { Def = 2, Hp = 1 }), content.Ability("vigilance").Effect);
        Assert.Equal("+20 hit and avoid against axes.", content.Ability("axebreaker").Text);
    }

    [Fact]
    public void AUnitsAbilitiesAreResolvedAndItsPassiveDeltaIsItsStats()
    {
        var content = ContentLoader.Parse(Fixture.Files(abilities: Breakers, units: RecruitWithAbilities("[\"vigilance\", \"axebreaker\"]")));
        var recruit = content.Unit("recruit");

        Assert.Equal(new[] { "vigilance", "axebreaker" }, content.AbilitiesOf(recruit).Select(a => a.Id));
        Assert.Equal(21, content.StatsOf(recruit).Hp);
        Assert.Equal(6, content.StatsOf(recruit).Def);
    }

    [Fact]
    public void AnUnknownAbilityOnAUnitNamesFileEntryAndField()
    {
        var e = Fails(Fixture.Files(units: RecruitWithAbilities("[\"vigilance\", \"lancebreaker\"]")));

        AssertNames(e, "units/units.json", "recruit", "abilities[1]");
        Assert.Contains("lancebreaker", e.Message);
    }

    [Fact]
    public void AUnitMayNotListAnAbilityTwice()
    {
        var e = Fails(Fixture.Files(units: RecruitWithAbilities("[\"vigilance\", \"vigilance\"]")));

        AssertNames(e, "units/units.json", "recruit", "abilities[1]");
    }

    [Fact]
    public void AClassMasteryLoadsAndMustNameAnAbility()
    {
        var classes = Fixture.Classes.Replace("\"weapons\": [\"sword\"]", "\"weapons\": [\"sword\"], \"mastery\": \"vigilance\"");
        Assert.Equal("vigilance", ContentLoader.Parse(Fixture.Files(classes: classes)).Class("cadet").Mastery);
        Assert.Null(ContentLoader.Parse(Fixture.Files()).Class("cadet").Mastery);

        var e = Fails(Fixture.Files(classes: classes.Replace("\"mastery\": \"vigilance\"", "\"mastery\": \"swordfaire\"")));

        AssertNames(e, ContentFiles.ClassesName, "cadet", "mastery");
    }

    [Fact]
    public void ANamedMasteryGrantsNothing()
    {
        var classes = Fixture.Classes.Replace("\"weapons\": [\"sword\"]", "\"weapons\": [\"sword\"], \"mastery\": \"vigilance\"");
        var content = ContentLoader.Parse(Fixture.Files(classes: classes));

        Assert.Equal(4, content.StatsOf(content.Unit("recruit")).Def);
    }

    [Fact]
    public void AnEffectKindMustBeKnown()
    {
        AssertNames(Fails(Fixture.Files(abilities: One("{ \"kind\": \"canto\" }"))), ContentFiles.AbilitiesName, "a", "effect.kind");
    }

    [Fact]
    public void AStatsEffectMustChangeAStat()
    {
        AssertNames(Fails(Fixture.Files(abilities: One("{ \"kind\": \"stats\", \"stats\": { \"def\": 0 } }"))), ContentFiles.AbilitiesName, "a", "effect.stats");
    }

    [Fact]
    public void ACombatEffectMustChangeSomething()
    {
        AssertNames(Fails(Fixture.Files(abilities: One("{ \"kind\": \"combat\", \"against\": { \"weapon\": \"axe\" } }"))), ContentFiles.AbilitiesName, "a", "effect");
    }

    [Fact]
    public void AKeyTheKindDoesNotReadIsRefused()
    {
        AssertNames(Fails(Fixture.Files(abilities: One("{ \"kind\": \"combat\", \"hti\": 20 }"))), ContentFiles.AbilitiesName, "a", "effect.hti");
        AssertNames(Fails(Fixture.Files(abilities: One("{ \"kind\": \"stats\", \"stats\": { \"def\": 1 }, \"hit\": 5 }"))), ContentFiles.AbilitiesName, "a", "effect.hit");
        AssertNames(Fails(Fixture.Files(abilities: One("{ \"kind\": \"combat\", \"hit\": 5, \"against\": { \"weapons\": \"axe\" } }"))), ContentFiles.AbilitiesName, "a", "effect.against.weapons");
    }

    [Fact]
    public void AnAgainstConditionMustNameSomethingReal()
    {
        AssertNames(Fails(Fixture.Files(abilities: One("{ \"kind\": \"combat\", \"hit\": 5, \"against\": { } }"))), ContentFiles.AbilitiesName, "a", "effect.against");
        AssertNames(Fails(Fixture.Files(abilities: One("{ \"kind\": \"combat\", \"hit\": 5, \"against\": { \"weapon\": \"whip\" } }"))), ContentFiles.AbilitiesName, "a", "effect.against.weapon");
        AssertNames(Fails(Fixture.Files(abilities: One("{ \"kind\": \"combat\", \"hit\": 5, \"against\": { \"movement\": \"swimming\" } }"))), ContentFiles.AbilitiesName, "a", "effect.against.movement");
    }

    [Fact]
    public void AnAbilityCarriesText()
    {
        var e = Fails(Fixture.Files(abilities: "{ \"abilities\": [ { \"id\": \"a\", \"name\": \"A\", \"text\": \" \", \"effect\": { \"kind\": \"combat\", \"hit\": 1 } } ] }"));

        AssertNames(e, ContentFiles.AbilitiesName, "a", "text");
    }

    [Fact]
    public void AbilityIdsAreUnique()
    {
        var twice = Breakers.Replace("\"id\": \"steady\"", "\"id\": \"axebreaker\"");

        AssertNames(Fails(Fixture.Files(abilities: twice)), ContentFiles.AbilitiesName, "axebreaker", "id");
    }

    [Fact]
    public void AbilitiesAndMasteryRoundTrip()
    {
        var classes = Fixture.Classes.Replace("\"weapons\": [\"sword\"]", "\"weapons\": [\"sword\"], \"mastery\": \"axebreaker\"");
        var content = ContentLoader.Parse(Fixture.Files(abilities: Breakers, classes: classes, units: RecruitWithAbilities("[\"steady\"]")));

        var written = ContentSerializer.Write(content);
        var reloaded = ContentLoader.Parse(written);

        Assert.Equal(content, reloaded);
        Assert.Equal(written.Abilities.Text, ContentSerializer.Write(reloaded).Abilities.Text);
    }

    [Fact]
    public void ContentWithDifferentAbilitiesIsNotEqual()
    {
        Assert.NotEqual(ContentLoader.Parse(Fixture.Files(abilities: Breakers)), ContentLoader.Parse(Fixture.Files()));
    }
}
