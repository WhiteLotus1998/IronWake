using Ironwake.Core;

namespace Ironwake.Client;

/// <summary>
/// Which combats play as a battle scene (issue 535's pacing rule): every one, the key moments
/// (the default), or none, every strike then playing on the map as issue 513 draws it.
/// </summary>
public enum SceneSetting
{
    KeyMoments,
    All,
    MapOnly,
}

/// <summary>The pacing rule (issue 535): which combats get a scene under each setting, and the setting's words on screen.</summary>
public static class Scenes
{
    /// <summary>The next setting in the cycle a key steps through: key moments, all, map only, then back.</summary>
    public static SceneSetting Next(SceneSetting setting) => setting switch
    {
        SceneSetting.KeyMoments => SceneSetting.All,
        SceneSetting.All => SceneSetting.MapOnly,
        _ => SceneSetting.KeyMoments,
    };

    /// <summary>The setting as the footer names it.</summary>
    public static string Label(SceneSetting setting) => setting switch
    {
        SceneSetting.All => "scenes: all",
        SceneSetting.MapOnly => "scenes: map only",
        _ => "scenes: key moments",
    };

    /// <summary>
    /// True when <paramref name="combat"/> plays as a scene: under <see cref="SceneSetting.All"/>
    /// always, under <see cref="SceneSetting.MapOnly"/> never, and under the key moments when a
    /// player unit strikes first, a strike crits, the combat kills, a boss strikes first, or a
    /// unit levels from it (a <see cref="LeveledUp"/> among <paramref name="events"/>, the events
    /// of the same command). Any other enemy strike plays on the map.
    /// </summary>
    public static bool Plays(SceneSetting setting, CombatFought combat, BattleState before, IEnumerable<GameEvent> events) => setting switch
    {
        SceneSetting.All => true,
        SceneSetting.MapOnly => false,
        _ => before.Find(combat.AttackerId) is { } attacker && (attacker.Side == Side.Player || attacker.IsBoss)
            || combat.Strikes.Any(s => s.Hit && s.Crit)
            || combat.AttackerHpAfter == 0 || combat.TargetHpAfter == 0
            || events.Any(e => e is LeveledUp),
    };
}

/// <summary>
/// One combatant in a battle scene: who it is, which side and half of the frame it stands on,
/// the ground under it (the backdrop's band on its half), its HP before the first strike out of
/// its max, and the sheet names its clips are looked up under, first found wins.
/// </summary>
public sealed record SceneSide(string Id, string Name, Side Side, bool IsBoss, bool Left, string Terrain, int HpBefore, int MaxHp, IReadOnlyList<string> Prefixes)
{
    /// <summary>The files a renderer tries for <paramref name="clip"/>, in order: the boss's own sheet for the weapon swung, then the class's for its weapon kind.</summary>
    public IReadOnlyList<string> ClipFiles(string clip) => Prefixes.Select(p => $"{p}_{clip}").ToList();
}

/// <summary>
/// One strike in a scene: the striker's clip and the struck unit's (<c>hit_react</c>, <c>dodge</c>,
/// or <c>fall</c> on the killing blow), when the striker's clip starts, its contact time (hit-stop
/// holds there and the number fires), when the strike ends, the number, the effects laid on the
/// struck body at contact (by <see cref="ArtSpec.Effects"/>' names), and the shake, from 0 to 1,
/// scaled to the damage against the struck unit's max HP and louder on a crit.
/// </summary>
public sealed record SceneStrike(
    string AttackerId,
    string TargetId,
    string AttackerClip,
    string TargetClip,
    float Start,
    float Contact,
    float End,
    PopKind Kind,
    string Text,
    int TargetHpAfter,
    IReadOnlyList<string> Effects,
    float Shake);

/// <summary>
/// A combat played as a scene (issue 535): both combatants, the attacker's advance, each strike
/// on its clip's timeline, and the fall, timed at the art's 12 frames a second with hit-stop at
/// each contact. The scene is read from the event and the states around it, and adds nothing
/// they do not say; its <see cref="Strikes"/> run in the event's order, one per strike, so the
/// board's numbers, HP and death beat keep time with it (<see cref="Rhythm.PopTimes"/>).
/// </summary>
public sealed record BattleScene(SceneSide Attacker, SceneSide Defender, IReadOnlyList<SceneStrike> Strikes, string? FallenId, float FallStart, float Length)
{
    /// <summary>The procedural backdrop behind both combatants (slice 2), read from the board the combat was fought on.</summary>
    public SceneBackdrop Backdrop { get; init; } = SceneBackdrop.Day(Attacker.Terrain, Defender.Terrain);

    /// <summary>The art's frame rate (ART_SPEC: 12 frames a second).</summary>
    public const float Fps = 12f;

    /// <summary>The hold on a hit's contact frame.</summary>
    public const float HitStop = 0.1f;

    /// <summary>The hold on a crit's contact frame: longer, so the louder blow lands louder.</summary>
    public const float CritStop = 0.22f;

    /// <summary>The breath between one strike's end and the next striker's clip.</summary>
    public const float Between = 0.12f;

    /// <summary>The stillness after the last clip (or the fall) before the scene gives the board back.</summary>
    public const float Tail = 0.35f;

    private static float Frames(string clip) => (ArtSpec.Clips.First(c => c.Name == clip).Frames) / Fps;

    private static float ContactAt(string clip) => (ArtSpec.Clips.First(c => c.Name == clip).Contact ?? 0) / Fps;

    /// <summary>
    /// The scene for <paramref name="combat"/>, fought between <paramref name="before"/> and
    /// <paramref name="after"/>; null when either side is not on the board before it. The player
    /// side stands on the left, the sheets' own facing; an enemy on the right is mirrored.
    /// </summary>
    public static BattleScene? Of(CombatFought combat, BattleState before, BattleState after, GameContent content)
    {
        if (before.Find(combat.AttackerId) is not { } a || before.Find(combat.TargetId) is not { } t)
        {
            return null;
        }

        var names = UnitNames.Of(after, content);
        SceneSide Side(BattleUnit u) => new(
            u.Id, names[u.Id], u.Side, u.IsBoss, u.Side == Ironwake.Core.Side.Player,
            before.Map.TerrainIdAt(u.At), u.Hp, u.MaxHp(content), Prefixes(u, after.Find(u.Id) ?? u, content));
        var attacker = Side(a);
        var defender = Side(t);
        var maxHp = new Dictionary<string, int> { [a.Id] = attacker.MaxHp, [t.Id] = defender.MaxHp };
        var weapons = new Dictionary<string, Weapon?> { [a.Id] = Swung(after.Find(a.Id) ?? a, content), [t.Id] = Swung(after.Find(t.Id) ?? t, content) };

        var clock = Frames("advance");
        var strikes = new List<SceneStrike>();
        foreach (var s in combat.Strikes)
        {
            var clip = !s.Hit ? "miss_recover" : s.Crit ? "strike_crit" : "strike";
            var kind = !s.Hit ? PopKind.Miss : s.Crit ? PopKind.Crit : PopKind.Damage;
            var struck = !s.Hit ? "dodge" : s.TargetHpAfter == 0 ? "fall" : "hit_react";
            var contact = clock + ContactAt(clip);
            var stop = !s.Hit ? 0 : s.Crit ? CritStop : HitStop;
            var end = clock + Frames(clip) + stop;
            var text = s.Hit ? s.Damage.ToString(System.Globalization.CultureInfo.InvariantCulture) : "miss";
            var shake = s.Hit ? Math.Min(1f, (float)s.Damage / Math.Max(1, maxHp[s.TargetId]) * (s.Crit ? 1.5f : 1f)) : 0f;
            strikes.Add(new SceneStrike(s.AttackerId, s.TargetId, clip, struck, clock, contact, end, kind, text, s.TargetHpAfter, EffectsOf(s, weapons[s.AttackerId]), shake));
            clock = end + Between;
        }

        var fallen = combat.TargetHpAfter == 0 ? t.Id : combat.AttackerHpAfter == 0 ? a.Id : null;
        var fallStart = strikes.Count > 0 ? strikes[^1].Contact : clock;
        var length = fallen is null ? clock - Between + Tail : Math.Max(clock - Between, fallStart + Frames("fall")) + Tail;
        var (leftUnit, rightUnit) = attacker.Left ? (a, t) : (t, a);
        return new BattleScene(attacker, defender, strikes, fallen, fallStart, length) { Backdrop = SceneBackdrop.Of(before, leftUnit, rightUnit) };
    }

    /// <summary>
    /// The effects a strike lays on the struck body at contact: dust under a dodge; on a hit the
    /// spark, the spell's own burst for a striking spell or the slash arc for a blade, axe or
    /// lance, and the crit flash on a crit. A bow's or a fist's hit is the spark alone.
    /// </summary>
    public static IReadOnlyList<string> EffectsOf(StrikeEvent strike, Weapon? weapon)
    {
        if (!strike.Hit)
        {
            return new[] { "dust" };
        }

        var effects = new List<string>();
        if (weapon is { IsMagic: true, Heals: false })
        {
            effects.Add($"spell_{weapon.Id}");
        }
        else
        {
            effects.Add("hit_spark");
            if (weapon?.Type is WeaponType.Sword or WeaponType.Axe or WeaponType.Lance)
            {
                effects.Add("slash_arc");
            }
        }

        if (strike.Crit)
        {
            effects.Add("crit_flash");
        }

        return effects;
    }

    /// <summary>The weapon a unit struck with: the one at its front slot once the command has moved it there.</summary>
    private static Weapon? Swung(BattleUnit unit, GameContent content) => unit.EquippedWeapon(content);

    /// <summary>
    /// A combatant's sheet prefixes (issue 532's names): a boss's own <c>boss_&lt;template&gt;_&lt;weapon&gt;</c>,
    /// then <c>&lt;class&gt;_&lt;kind&gt;</c> for the weapon it swung, or for its class's first
    /// weapon kind when it holds none it can swing.
    /// </summary>
    private static IReadOnlyList<string> Prefixes(BattleUnit before, BattleUnit after, GameContent content)
    {
        var weapon = Swung(after, content) ?? Swung(before, content);
        var unitClass = content.Class(before.Unit.ClassId);
        var kind = weapon?.Type ?? unitClass.Weapons.FirstOrDefault();
        var prefixes = new List<string>();
        if (before.IsBoss && weapon is not null)
        {
            prefixes.Add($"boss_{Template(before.Id)}_{weapon.Id}");
        }

        prefixes.Add($"{unitClass.Id}_{ArtSpec.Kind(kind)}");
        return prefixes;
    }

    /// <summary>An enemy's template id: its unit id less the per-template counter (<c>bandit_leader-1</c> is <c>bandit_leader</c>).</summary>
    public static string Template(string unitId)
    {
        var dash = unitId.LastIndexOf('-');
        return dash > 0 && dash < unitId.Length - 1 && unitId[(dash + 1)..].All(char.IsAsciiDigit) ? unitId[..dash] : unitId;
    }
}

/// <summary>
/// The level-up screen (issue 535): after a combat that levels a unit, the unit, the level it
/// left and the level it reached, and each stat that rose by name and amount. Two levels from
/// one combat show once, with both levels' gains summed. A level where nothing rose carries the
/// unit's one dry line instead, never a blank.
/// </summary>
public sealed record LevelUpCard(string UnitId, string Name, int FromLevel, int ToLevel, IReadOnlyList<(Stat Stat, int Amount)> Rose, string? Flat)
{
    /// <summary>How long the card holds at normal speed before the next act.</summary>
    public const float Hold = 1.8f;

    /// <summary>A stat as the card names it.</summary>
    public static string StatName(Stat stat) => stat switch
    {
        Stat.Hp => "HP",
        Stat.Str => "Strength",
        Stat.Mag => "Magic",
        Stat.Dex => "Dexterity",
        Stat.Spd => "Speed",
        Stat.Lck => "Luck",
        Stat.Def => "Defence",
        Stat.Res => "Resistance",
        _ => "Charisma",
    };

    /// <summary>
    /// What each cast member says when a level gives them nothing (issue 535), by roster id; a
    /// unit not listed says <see cref="FlatDefault"/>.
    /// </summary>
    public static IReadOnlyDictionary<string, string> FlatLines { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["captain"] = "Fenn adds nothing to the list but his own name, again.",
        ["wren"] = "Wren counts it twice. Still nothing.",
        ["teodor"] = "Teodor orders himself to do better next time.",
        ["ottilie"] = "Ottilie writes it down: one level, no change, bill pending.",
        ["pell"] = "Pell marks the page and keeps reading.",
        ["dunstan"] = "Dunstan shrugs. He was not going to step back anyway.",
        ["maud"] = "Maud says a short prayer over it, just in case.",
        ["ansgar"] = "Ansgar insists the horse got the benefit.",
        ["rook"] = "Rook looks for higher ground to feel better about it.",
        ["keziah"] = "Keziah laughs. Nobody else does.",
        ["brannock"] = "Brannock calls it a draw and asks for a rematch.",
    };

    /// <summary>The dry line of a unit with no line of its own.</summary>
    public const string FlatDefault = "Nothing rose. Tomorrow, then.";

    /// <summary>
    /// The card for the first unit that levels among <paramref name="events"/> (one command's
    /// events), with every level it gained from them; null when nobody levels.
    /// </summary>
    public static LevelUpCard? Of(IEnumerable<GameEvent> events, BattleState after, GameContent content)
    {
        var levels = events.OfType<LeveledUp>().ToList();
        if (levels.Count == 0)
        {
            return null;
        }

        var id = levels[0].UnitId;
        var own = levels.Where(l => l.UnitId == id).ToList();
        var gains = own.Aggregate(Stats.Zero, (sum, l) => sum + l.Gains);
        var rose = Stats.All.Where(s => gains.Get(s) > 0).Select(s => (s, gains.Get(s))).ToList();
        var name = UnitNames.Of(after, content)[id];
        var flat = rose.Count == 0 ? FlatLines.GetValueOrDefault(id, FlatDefault) : null;
        return new LevelUpCard(id, name, own[0].NewLevel - 1, own[^1].NewLevel, rose, flat);
    }

    /// <summary>The card's lines: the headline, then one line per stat that rose, or the dry line.</summary>
    public IReadOnlyList<string> Lines()
    {
        var lines = new List<string> { $"{Name} reaches level {ToLevel}" };
        lines.AddRange(Rose.Count > 0 ? Rose.Select(r => $"{StatName(r.Stat)} +{r.Amount}") : new[] { Flat ?? FlatDefault });
        return lines;
    }
}

/// <summary>
/// One ember over a burning half of a scene's backdrop: where it rises across the half (0 at the
/// half's left edge, 1 at its right), how far through its rise it starts (0 to 1), and how fast
/// it rises in fractions of the frame's height a second.
/// </summary>
public sealed record Ember(float X, float Phase, float Speed);

/// <summary>
/// One half of a scene's backdrop (issue 535, slice 2): the ground the combatant stands on, the
/// fog over it (0 to 1), the embers rising when fire burns near, and whether its group's lamps
/// are lit.
/// </summary>
public sealed record BackdropHalf(string Terrain, float Fog, IReadOnlyList<Ember> Embers, bool Lamps);

/// <summary>
/// A scene's procedural backdrop (issue 535, slice 2), read from the board the combat was fought
/// on and adding nothing it does not say: each half's ground in three parallax layers, fog on
/// forest and water, embers where a burning tile lies within <see cref="EmberReach"/> of the
/// combatant, an enemy half's lamps when its group is lit (DESIGN 13.7), and the dusk over both,
/// darker as the turns take the light. Daylight is <see cref="Dark"/> 0.
/// </summary>
public sealed record SceneBackdrop(BackdropHalf Left, BackdropHalf Right, float Dark)
{
    /// <summary>The parallax layers far to near, with each one's drift in pixels a second: the far ridge, the ground's own detail, the near band.</summary>
    public static IReadOnlyList<(string Name, float Speed)> Layers { get; } = new[] { ("ridge", 6f), ("detail", 14f), ("near", 30f) };

    /// <summary>The dusk on a dusk map's first turn.</summary>
    public const float DuskFirst = 0.15f;

    /// <summary>The dusk once sight is down to one tile.</summary>
    public const float DuskLast = 0.55f;

    /// <summary>How near, Manhattan, a burning tile raises embers over a combatant's half.</summary>
    public const int EmberReach = 2;

    /// <summary>Embers raised per burning tile within reach.</summary>
    public const int EmbersPerFire = 4;

    /// <summary>The most embers over one half.</summary>
    public const int MaxEmbers = 12;

    /// <summary>The fog each ground carries; a ground not listed has none.</summary>
    public static IReadOnlyDictionary<string, float> GroundFog { get; } = new Dictionary<string, float>(StringComparer.Ordinal)
    {
        ["forest"] = 0.22f,
        ["water"] = 0.3f,
    };

    /// <summary>The backdrop with no fog, fire, lamps or dusk, for scenes made without a board.</summary>
    public static SceneBackdrop Day(string left, string right) =>
        new(new BackdropHalf(left, 0, Array.Empty<Ember>(), false), new BackdropHalf(right, 0, Array.Empty<Ember>(), false), 0);

    /// <summary>
    /// The dusk over <paramref name="map"/> on <paramref name="turn"/>: 0 in daylight;
    /// <see cref="DuskFirst"/> on a dusk map's first turn rising to <see cref="DuskLast"/> on
    /// the turn sight reaches one tile (<see cref="Dusk.Sight(MapDefinition, int)"/>).
    /// </summary>
    public static float DarkOf(MapDefinition map, int turn)
    {
        if (map.Dusk is not { } start || Dusk.Sight(map, turn) is not { } sight)
        {
            return 0;
        }

        return start <= 1 ? DuskLast : DuskFirst + (DuskLast - DuskFirst) * (start - sight) / (start - 1);
    }

    /// <summary>The backdrop for a combat between <paramref name="left"/> and <paramref name="right"/> on <paramref name="state"/>, the board before it.</summary>
    public static SceneBackdrop Of(BattleState state, BattleUnit left, BattleUnit right)
    {
        var dark = DarkOf(state.Map, state.Turn);
        var burning = Wildfire.Burning(state.Map);
        BackdropHalf Half(BattleUnit unit)
        {
            var terrain = state.Map.TerrainIdAt(unit.At);
            var fires = burning.Count(c => c.DistanceTo(unit.At) <= EmberReach);
            var embers = Enumerable.Range(0, Math.Min(MaxEmbers, fires * EmbersPerFire))
                .Select(i => new Ember(Unit01(unit.Id, i, 1), Unit01(unit.Id, i, 2), 0.25f + 0.35f * Unit01(unit.Id, i, 3)))
                .ToList();
            var fog = Math.Min(1f, GroundFog.GetValueOrDefault(terrain) + dark * 0.3f);
            var lamps = unit.Side == Side.Enemy && unit.Group is { } group && state.IsLit(group);
            return new BackdropHalf(terrain, fog, embers, lamps);
        }

        return new SceneBackdrop(Half(left), Half(right), dark);
    }

    /// <summary>A fixed number in 0 to 1 from a unit's id and two indices (FNV-1a), so the same combat raises the same embers on every run and every Recall.</summary>
    public static float Unit01(string id, int index, int salt)
    {
        var hash = 2166136261u;
        foreach (var c in $"{id}/{index}/{salt}")
        {
            hash = (hash ^ c) * 16777619u;
        }

        return (hash % 10007) / 10007f;
    }
}
