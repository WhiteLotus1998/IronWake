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

    /// <summary>Every enemy a shipped map places, with whether it is placed as a boss, from the maps and the keep.</summary>
    internal static IEnumerable<(string TemplateId, bool IsBoss)> ShippedEnemies(GameContent content)
    {
        var root = Fixture.RealContentDirectory();
        var files = Directory.GetFiles(Path.Combine(root, "maps"), "*.map").Concat(Directory.GetFiles(Path.Combine(root, "keep"), "*.map"));
        return files.Select(f => MapFiles.Load(f, content))
            .SelectMany(m => m.Placements.OfType<EnemyPlacement>().Concat(m.Spawns()))
            .Select(p => (p.TemplateId, p.IsBoss));
    }

    /// <summary>Every unit a shipped map places as a boss.</summary>
    internal static IEnumerable<string> ShippedBosses(GameContent content) =>
        ShippedEnemies(content).Where(e => e.IsBoss).Select(e => e.TemplateId);

    [Fact]
    public void TheSpecListsExactlyTheNamesTheContentDerives()
    {
        var content = Content();
        var expected = ArtSpec.Names(content, ShippedEnemies(content)).ToList();

        Assert.Equal(expected, SpecNames(File.ReadAllText(SpecPath())));
    }

    [Fact]
    public void AMissingRowFailsTheSpec()
    {
        var content = Content();
        var expected = ArtSpec.Names(content, ShippedEnemies(content)).ToList();
        var text = File.ReadAllText(SpecPath()).Replace("\ncadet_sword_strike\n", "\n", StringComparison.Ordinal);

        Assert.NotEqual(expected, SpecNames(text));
    }

    [Fact]
    public void EveryClassGetsATokenPerSideAndEveryClipPerWeaponKind()
    {
        var content = Content();
        var names = ArtSpec.Names(content, Array.Empty<(string, bool)>()).ToHashSet();

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
    public void AReachTwoPikemanAndABossReaverGetAVariantTokenEach()
    {
        var content = Content();
        var names = ArtSpec.TokenVariants(content, new[] { ("toll_warden", false), ("grange_reeve", true), ("bandit_leader", true), ("weir_foreman", true) }).ToList();

        Assert.Equal(new[] { "token_pikeman_enemy_hooked", "token_reaver_enemy_double" }, names);
    }

    [Fact]
    public void TheCaptainLooksOnlyForHisOwnToken()
    {
        var content = Content();

        Assert.Equal(new[] { "token_captain_player" }, ArtSpec.TokenFiles(content, content.Units["toll_brigand"], Side.Player, isBoss: false, isCaptain: true));
    }

    [Fact]
    public void AnEnemyWithATellTriesItsVariantThenItsClassToken()
    {
        var content = Content();

        Assert.Equal(new[] { "token_pikeman_enemy_hooked", "token_pikeman_enemy" }, ArtSpec.TokenFiles(content, content.Units["toll_warden"], Side.Enemy, isBoss: false, isCaptain: false));
        Assert.Equal(new[] { "token_reaver_enemy_double", "token_reaver_enemy" }, ArtSpec.TokenFiles(content, content.Units["toll_brigand"], Side.Enemy, isBoss: true, isCaptain: false));
        Assert.Equal(new[] { "token_reaver_enemy" }, ArtSpec.TokenFiles(content, content.Units["toll_brigand"], Side.Enemy, isBoss: false, isCaptain: false));
    }

    [Fact]
    public void APlayerUnitTriesItsClassTokenForItsSideAndNoTell()
    {
        var content = Content();

        Assert.Equal(new[] { "token_pikeman_player" }, ArtSpec.TokenFiles(content, content.Units["toll_warden"], Side.Player, isBoss: false, isCaptain: false));
    }

    [Fact]
    public void EveryTokenFileARendererTriesIsARowOfTheSpec()
    {
        var content = Content();
        var rows = ArtSpec.Tokens(content).Concat(ArtSpec.TokenVariants(content, content.Units.Keys.SelectMany(id => new[] { (id, false), (id, true) }))).ToHashSet();

        var tried = content.Units.Values.SelectMany(u => new[] { Side.Player, Side.Enemy }.SelectMany(side =>
            new[] { false, true }.SelectMany(boss => ArtSpec.TokenFiles(content, u, side, boss, isCaptain: false))))
            .Append("token_captain_player").Distinct().ToList();

        Assert.All(tried, name => Assert.Contains(name, rows));
    }

    [Fact]
    public void APlainPikemanOrAReaverNotPlacedAsABossGetsNoVariant()
    {
        var content = Content();

        Assert.Null(ArtSpec.TokenTell(content, content.Units["soldier"], isBoss: false));
        Assert.Null(ArtSpec.TokenTell(content, content.Units["toll_brigand"], isBoss: false));
        Assert.Equal("double", ArtSpec.TokenTell(content, content.Units["toll_brigand"], isBoss: true));
        Assert.Empty(ArtSpec.TokenVariants(content, new[] { ("soldier", false), ("toll_brigand", false), ("archer", true) }));
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
