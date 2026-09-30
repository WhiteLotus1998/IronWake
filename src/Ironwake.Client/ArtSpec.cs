using Ironwake.Core;

namespace Ironwake.Client;

/// <summary>One battle-scene clip (issue 532): its name, its frame count, and the frame where the blow lands (hit-stop and the number fire there), or null for a clip with no contact.</summary>
public sealed record Clip(string Name, int Frames, int? Contact);

/// <summary>One battle-scene effect (issue 564): its name without the <c>fx_</c> prefix and its frame count. An effect has no contact frame; the scene lays it on its pivot when the beat that calls it starts.</summary>
public sealed record Effect(string Name, int Frames);

/// <summary>
/// The art spec's names (issue 532, <c>docs/ART_SPEC.md</c>): every asset an outside artist
/// delivers, derived from the content so a new class, weapon, boss or cast member adds its rows
/// here and the spec's test fails until the document lists them. Tokens are one file per class per
/// side, plus a variant per weapon tell a shipped enemy carries; battle clips are one sheet per (class, weapon kind, clip) and per (boss, weapon, clip);
/// effects are one sheet each, the spells' from the weapons; level-up poses and portraits are one
/// per cast member and optional. A renderer looks each name
/// up and draws its own vector placeholder where no file is delivered, so real art drops in
/// without code changes.
/// </summary>
public static class ArtSpec
{
    /// <summary>The token frame in pixels at 1280x720: a 48 px tile, the disc about two thirds of it.</summary>
    public const int TokenFrame = 48;

    /// <summary>The battle-scene frame in pixels, one combatant, pivot at the feet centre.</summary>
    public const int ClipFrame = 256;

    /// <summary>The clips every (class, weapon kind) pair and every boss weapon delivers, in the spec's order.</summary>
    public static IReadOnlyList<Clip> Clips { get; } = new[]
    {
        new Clip("idle", 8, null),
        new Clip("advance", 6, null),
        new Clip("strike", 8, 5),
        new Clip("strike_crit", 12, 8),
        new Clip("miss_recover", 8, 5),
        new Clip("dodge", 6, null),
        new Clip("hit_react", 4, 1),
        new Clip("fall", 10, null),
    };

    /// <summary>The effect sheet's pivot: the point the scene lays it on, the struck body's centre (or, for dust, the ground under the feet).</summary>
    public static readonly (int X, int Y) EffectPivot = (128, 128);

    /// <summary>The frames of every spell's burst.</summary>
    public const int SpellFrames = 8;

    /// <summary>The effects every battle needs whatever its weapons, in the spec's order; the spells' bursts follow from the content.</summary>
    public static IReadOnlyList<Effect> FixedEffects { get; } = new[]
    {
        new Effect("hit_spark", 5),
        new Effect("slash_arc", 5),
        new Effect("crit_flash", 6),
        new Effect("heal", 10),
        new Effect("dust", 6),
        new Effect("embers", 12),
    };

    /// <summary>A weapon kind as the file names spell it.</summary>
    public static string Kind(WeaponType type) => type.ToString().ToLowerInvariant();

    /// <summary>The map tokens: <c>token_&lt;class&gt;_&lt;side&gt;</c> for every class and both sides, then the captain's own.</summary>
    public static IEnumerable<string> Tokens(GameContent content) =>
        content.Classes.Keys.SelectMany(id => new[] { $"token_{id}_player", $"token_{id}_enemy" })
            .Append("token_captain_player");

    /// <summary>
    /// A token's tell (issue 564, ART_SPEC's silhouettes): <c>hooked</c> for a pikeman whose weapons
    /// reach 2 (the toll warden's and the reeve's Toll Spear), <c>double</c> for a boss reaver (the
    /// bandit leader's and the foreman's double bit), else null. The same rule
    /// <c>Main.Look.cs</c>'s silhouette draws, so a variant token file and the placeholder it
    /// replaces carry one shape.
    /// </summary>
    public static string? TokenTell(GameContent content, Unit unit, bool isBoss) => unit.ClassId switch
    {
        "pikeman" when unit.Inventory.Items.Select(i => i.ItemId).Where(content.Weapons.ContainsKey).Any(id => content.Weapons[id].MaxRange >= 2) => "hooked",
        "reaver" when isBoss => "double",
        _ => null,
    };

    /// <summary>
    /// The token files a renderer tries for one unit on the board, first found wins (issue 564):
    /// the captain's own token and nothing else, since the class's disc is not him; else the
    /// variant for the unit's tell, then its class's token for its side. When none is on disk the
    /// renderer draws its vector placeholder.
    /// </summary>
    public static IReadOnlyList<string> TokenFiles(GameContent content, Unit unit, Side side, bool isBoss, bool isCaptain)
    {
        if (isCaptain)
        {
            return new[] { "token_captain_player" };
        }

        var own = $"token_{unit.ClassId}_{(side == Side.Player ? "player" : "enemy")}";
        return side == Side.Enemy && TokenTell(content, unit, isBoss) is { } tell
            ? new[] { $"{own}_{tell}", own }
            : new[] { own };
    }

    /// <summary>
    /// The variant tokens: <c>token_&lt;class&gt;_enemy_&lt;tell&gt;</c> for every tell an enemy placed
    /// on a shipped map carries, one row per tell, not per unit, sorted. A renderer without the
    /// variant's file falls back to the class's own token.
    /// </summary>
    public static IEnumerable<string> TokenVariants(GameContent content, IEnumerable<(string TemplateId, bool IsBoss)> enemies) =>
        enemies.Select(e => (Unit: content.Units[e.TemplateId], e.IsBoss))
            .Select(e => TokenTell(content, e.Unit, e.IsBoss) is { } tell ? $"token_{e.Unit.ClassId}_enemy_{tell}" : null)
            .OfType<string>().Distinct().OrderBy(n => n, StringComparer.Ordinal);

    /// <summary>
    /// The map tiles (issue 564): <c>tile_&lt;terrain&gt;</c> for every terrain, a 48 px square at
    /// 2x with its detail laid over its own colour; fire is an ember hatch on clear ground, laid
    /// over whatever burns. The grid and the north light's shadows stay the renderer's, since they
    /// depend on the neighbours.
    /// </summary>
    public static IEnumerable<string> Tiles(GameContent content) =>
        content.Terrain.Keys.Select(id => $"tile_{id}");

    /// <summary>The battle clips: <c>&lt;class&gt;_&lt;kind&gt;_&lt;clip&gt;</c> for every weapon kind a class can use, class by class.</summary>
    public static IEnumerable<string> ClassClips(GameContent content) =>
        from unitClass in content.Classes.Values
        from type in unitClass.Weapons
        from clip in Clips
        select $"{unitClass.Id}_{Kind(type)}_{clip.Name}";

    /// <summary>
    /// The boss clips: <c>boss_&lt;unit&gt;_&lt;weapon&gt;_&lt;clip&gt;</c> for every weapon a named boss
    /// carries, by weapon id, so a two-weapon boss gets a row per weapon.
    /// </summary>
    public static IEnumerable<string> BossClips(GameContent content, IEnumerable<string> bossIds) =>
        from id in bossIds.Distinct().OrderBy(id => id, StringComparer.Ordinal)
        from weapon in content.Units[id].Inventory.Items.Select(i => i.ItemId).Where(content.Weapons.ContainsKey).Distinct()
        from clip in Clips
        select $"boss_{id}_{weapon}_{clip.Name}";

    /// <summary>
    /// The effects (issue 564): the fixed ones, then a burst <c>spell_&lt;weapon&gt;</c> for every
    /// Reason or Faith weapon that strikes, by weapon id, so a new spell adds a row. The healing
    /// spells share <c>heal</c>.
    /// </summary>
    public static IEnumerable<Effect> Effects(GameContent content) =>
        FixedEffects.Concat(content.Weapons.Values.Where(w => w.IsMagic && !w.Heals).Select(w => new Effect($"spell_{w.Id}", SpellFrames)));

    /// <summary>The effect sheets: <c>fx_&lt;effect&gt;</c> for every effect.</summary>
    public static IEnumerable<string> EffectNames(GameContent content) => Effects(content).Select(e => $"fx_{e.Name}");

    /// <summary>The optional rows: a level-up pose and a portrait for each cast member, in roster order.</summary>
    public static IEnumerable<string> Optional(GameContent content) =>
        content.Cast.Select(u => $"levelup_{u.Id}").Concat(content.Cast.Select(u => $"portrait_{u.Id}"));

    /// <summary>
    /// Every name the spec lists, in its order: tokens, the variant tokens, tiles, class clips, boss
    /// clips, effects, then the optional rows. <paramref name="enemies"/> is every enemy the shipped
    /// maps place, with whether it is placed as a boss.
    /// </summary>
    public static IEnumerable<string> Names(GameContent content, IEnumerable<(string TemplateId, bool IsBoss)> enemies)
    {
        var placed = enemies.ToList();
        return Tokens(content).Concat(TokenVariants(content, placed)).Concat(Tiles(content)).Concat(ClassClips(content))
            .Concat(BossClips(content, placed.Where(e => e.IsBoss).Select(e => e.TemplateId)))
            .Concat(EffectNames(content)).Concat(Optional(content));
    }
}
