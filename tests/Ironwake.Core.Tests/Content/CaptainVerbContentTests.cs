using Ironwake.Content;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The captain's ladder's effect kinds load, round-trip and refuse bad data naming the ability and
/// the field (issue 705, slices 2 and 5): <c>beside</c>, <c>aura</c>, <c>footing</c> and <c>ground</c>.
/// </summary>
public class CaptainVerbContentTests
{
    private const string Terrain = """
        { "terrain": [
          { "id": "plain", "name": "Plain", "glyph": ".", "cost": { "infantry": 1, "cavalry": 1, "flying": 1, "armored": 1 } },
          { "id": "forest", "name": "Forest", "glyph": "^", "cost": { "infantry": 2, "cavalry": 3, "flying": 1, "armored": 2 } }
        ] }
        """;

    private const string Verbs = """
        { "abilities": [
          { "id": "shoulder", "name": "Shoulder", "text": "Def with a friend.", "effect": { "kind": "beside", "stats": { "def": 1, "res": 1 } } },
          { "id": "presence", "name": "Presence", "text": "Allies hit.", "effect": { "kind": "aura", "radius": 2, "hit": 10 } },
          { "id": "trail", "name": "Trail", "text": "Forest is easy.", "effect": { "kind": "footing", "terrain": ["forest"], "cost": 1 } },
          { "id": "ground", "name": "Ground", "text": "Forest is home.", "effect": { "kind": "ground", "terrain": ["plain", "forest"], "stats": { "def": 2, "spd": 2 } } }
        ] }
        """;

    private const string Classes = """
        { "classes": [
          { "id": "cadet", "name": "Cadet", "movement": "infantry", "mov": 4, "weapons": ["sword"], "abilities": ["shoulder", "presence", "trail", "ground"] }
        ] }
        """;

    [Fact]
    public void TheCaptainsVerbEffectsLoadAndRoundTrip()
    {
        var content = ContentLoader.Parse(Fixture.Files(terrain: Terrain, classes: Classes, abilities: Verbs));

        Assert.Equal(new BesideStatsEffect(Stats.Zero with { Def = 1, Res = 1 }), content.Ability("shoulder").Effect);
        Assert.Equal(new AuraEffect(2, 10, 0), content.Ability("presence").Effect);
        Assert.Equal(new FootingEffect(ValueList<string>.Of("forest"), 1), content.Ability("trail").Effect);
        Assert.Equal(new GroundStatsEffect(ValueList<string>.Of("plain", "forest"), Stats.Zero with { Def = 2, Spd = 2 }), content.Ability("ground").Effect);

        var written = ContentSerializer.Write(content);
        var reloaded = ContentLoader.Parse(written);
        Assert.Equal(content, reloaded);
        Assert.Equal(written.Abilities.Text, ContentSerializer.Write(reloaded).Abilities.Text);
    }

    [Theory]
    [InlineData("\"stats\": { \"def\": 1, \"res\": 1 }", "\"stats\": { }", "shoulder", "effect.stats", "must change at least one stat")]
    [InlineData("\"stats\": { \"def\": 1, \"res\": 1 }", "\"stats\": { \"hp\": 2 }", "shoulder", "effect.stats.hp", "max HP cannot move with a neighbour")]
    [InlineData("\"radius\": 2, \"hit\": 10", "\"radius\": 0, \"hit\": 10", "presence", "effect.radius", "must be at least 1")]
    [InlineData("\"radius\": 2, \"hit\": 10", "\"radius\": 2", "presence", "effect", "an aura must change hit or avoid")]
    [InlineData("\"radius\": 2, \"hit\": 10", "\"radius\": 2, \"crit\": 10", "presence", "effect.crit", "is not read here")]
    [InlineData("\"terrain\": [\"forest\"]", "\"terrain\": []", "trail", "effect.terrain", "must name at least one terrain")]
    [InlineData("\"terrain\": [\"forest\"]", "\"terrain\": [\"swamp\"]", "trail", "effect.terrain", "unknown terrain 'swamp'")]
    [InlineData("\"cost\": 1", "\"cost\": 0", "trail", "effect.cost", "must be at least 1")]
    [InlineData("[\"plain\", \"forest\"]", "[]", "ground", "effect.terrain", "must name at least one terrain")]
    [InlineData("[\"plain\", \"forest\"]", "[\"plain\", \"swamp\"]", "ground", "effect.terrain", "unknown terrain 'swamp'")]
    [InlineData("\"stats\": { \"def\": 2, \"spd\": 2 }", "\"stats\": { }", "ground", "effect.stats", "must change at least one stat")]
    [InlineData("\"stats\": { \"def\": 2, \"spd\": 2 }", "\"stats\": { \"hp\": 2 }", "ground", "effect.stats.hp", "max HP cannot move with the ground")]
    [InlineData("\"stats\": { \"def\": 2, \"spd\": 2 }", "\"stats\": { \"def\": 2 }, \"cost\": 1", "ground", "effect.cost", "is not read here")]
    public void ABadCaptainsVerbIsRefusedNamingTheAbilityAndTheField(string find, string replace, string entry, string field, string message)
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(terrain: Terrain, classes: Classes, abilities: Verbs.Replace(find, replace))));

        Assert.Equal(ContentFiles.AbilitiesName, e.File);
        Assert.Equal(entry, e.Entry);
        Assert.Equal(field, e.Field);
        Assert.Contains(message, e.Message);
    }
}
