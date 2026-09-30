using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The art spec (issue 532): <c>docs/ART_SPEC.md</c>'s name list is the one
/// <see cref="ArtSpec"/> derives from the content, row for row, so the brief an artist holds
/// and the names the client looks up never drift apart.
/// </summary>
public class ArtSpecTests
{
    private static GameContent Content() => ContentLoader.Load(Fixture.RealContentDirectory());

    private static string SpecPath() => Path.Combine(Fixture.RealContentDirectory(), "..", "docs", "ART_SPEC.md");

    /// <summary>The rows of the spec's fenced <c>names</c> block.</summary>
    private static List<string> SpecNames(string text)
    {
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var start = lines.IndexOf("```names");
        Assert.True(start >= 0, "ART_SPEC.md has no ```names block");
        return lines.Skip(start + 1).TakeWhile(l => l != "```").ToList();
    }

    /// <summary>Every unit a shipped map places as a boss, from the maps and the keep.</summary>
    internal static IEnumerable<string> ShippedBosses(GameContent content)
    {
        var root = Fixture.RealContentDirectory();
        var files = Directory.GetFiles(Path.Combine(root, "maps"), "*.map").Concat(Directory.GetFiles(Path.Combine(root, "keep"), "*.map"));
        return files.Select(f => MapFiles.Load(f, content))
            .SelectMany(m => m.Placements.OfType<EnemyPlacement>().Concat(m.Spawns()))
            .Where(p => p.IsBoss).Select(p => p.TemplateId);
    }

    [Fact]
    public void TheSpecListsExactlyTheNamesTheContentDerives()
    {
        var content = Content();
        var expected = ArtSpec.Names(content, ShippedBosses(content)).ToList();

        Assert.Equal(expected, SpecNames(File.ReadAllText(SpecPath())));
    }

    [Fact]
    public void AMissingRowFailsTheSpec()
    {
        var content = Content();
        var expected = ArtSpec.Names(content, ShippedBosses(content)).ToList();
        var text = File.ReadAllText(SpecPath()).Replace("\ncadet_sword_strike\n", "\n", StringComparison.Ordinal);

        Assert.NotEqual(expected, SpecNames(text));
    }

    [Fact]
    public void EveryClassGetsATokenPerSideAndEveryClipPerWeaponKind()
    {
        var content = Content();
        var names = ArtSpec.Names(content, Array.Empty<string>()).ToHashSet();

        foreach (var unitClass in content.Classes.Values)
        {
            Assert.Contains($"token_{unitClass.Id}_player", names);
            Assert.Contains($"token_{unitClass.Id}_enemy", names);
            foreach (var type in unitClass.Weapons)
            {
                Assert.All(ArtSpec.Clips, clip => Assert.Contains($"{unitClass.Id}_{ArtSpec.Kind(type)}_{clip.Name}", names));
            }
        }
    }

    [Fact]
    public void ATwoWeaponBossGetsASetPerWeapon()
    {
        var content = Content();
        var names = ArtSpec.BossClips(content, new[] { "bandit_leader" }).ToList();

        Assert.Contains("boss_bandit_leader_steel_axe_strike", names);
        Assert.Contains("boss_bandit_leader_toll_axe_strike", names);
        Assert.Equal(2 * ArtSpec.Clips.Count, names.Count);
    }

    [Fact]
    public void EveryContactFrameIsInsideItsClip()
    {
        Assert.All(ArtSpec.Clips, clip => Assert.True(clip.Contact is null || (clip.Contact >= 0 && clip.Contact < clip.Frames), clip.Name));
        Assert.Equal(ArtSpec.Clips.Count, ArtSpec.Clips.Select(c => c.Name).Distinct().Count());
    }

    [Fact]
    public void TheSpecsClipTableMatchesTheClipsFramesAndContacts()
    {
        var text = File.ReadAllText(SpecPath());
        foreach (var clip in ArtSpec.Clips)
        {
            Assert.Contains($"| `{clip.Name}` | {clip.Frames} | {(clip.Contact is { } c ? c.ToString(System.Globalization.CultureInfo.InvariantCulture) : "none")} |", text);
        }
    }

    [Fact]
    public void EveryStrikingSpellGetsABurstAndTheHealsShareOne()
    {
        var content = Content();
        var names = ArtSpec.EffectNames(content).ToList();

        Assert.All(content.Weapons.Values.Where(w => w.IsMagic && !w.Heals), w => Assert.Contains($"fx_spell_{w.Id}", names));
        Assert.All(content.Weapons.Values.Where(w => w.Heals), w => Assert.DoesNotContain($"fx_spell_{w.Id}", names));
        Assert.All(content.Weapons.Values.Where(w => !w.IsMagic), w => Assert.DoesNotContain($"fx_spell_{w.Id}", names));
        Assert.Contains("fx_heal", names);
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void TheSpecsEffectTableMatchesTheEffectsFrames()
    {
        var text = File.ReadAllText(SpecPath());
        foreach (var effect in ArtSpec.FixedEffects)
        {
            Assert.Contains($"| `{effect.Name}` | {effect.Frames} |", text);
        }

        Assert.Contains($"| `spell_<weapon>` | {ArtSpec.SpellFrames} |", text);
    }
}
