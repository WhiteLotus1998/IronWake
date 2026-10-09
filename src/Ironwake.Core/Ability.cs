namespace Ironwake.Core;

/// <summary>
/// An ability from <c>abilities.json</c> (issue 66): an id, a name, one line of text, and
/// one typed effect. Abilities are data, never a handler per ability, so the forecast, the
/// resolver and both planners price them through the same section 5 functions without
/// knowing any ability by name. Adding a Sense is a content entry; adding a new kind of
/// effect is a new <see cref="AbilityEffect"/> record and a case in <see cref="AbilityRules"/>.
/// </summary>
public sealed record Ability(string Id, string Name, string Text, AbilityEffect Effect)
{
    public AbilityTrigger Trigger => Effect.Trigger;
}

/// <summary>
/// When an ability's effect applies. The set is closed and small on purpose: a bow's range
/// extension joins it with the issue that needs it.
/// </summary>
public enum AbilityTrigger
{
    /// <summary>Always on: a flat delta to the unit's stats, max HP included.</summary>
    Passive,

    /// <summary>In a fight, read by the hit and crit chances, so the forecast shows it.</summary>
    OnCombat,

    /// <summary>Declared with an attack command, before the roll: a combat art (issue 68).</summary>
    Declared,

    /// <summary>After the unit's Attack, Item or Wait: Move Again's second move (issue 71).</summary>
    AfterAction,

    /// <summary>When the unit takes Wait: a brace on any map (issue 691).</summary>
    OnWait,

    /// <summary>Whenever the unit's weapon is read, to strike or to heal: a longer reach (issue 704).</summary>
    OnWeapon,

    /// <summary>After a combat in which the unit killed and lived: a kill's heal (issue 704).</summary>
    OnKill,

    /// <summary>In a fight, while a living ally stands orthogonally beside the holder: the Vanguard's stats (issue 705).</summary>
    Beside,

    /// <summary>In a fight, on every ally within the holder's radius, never the holder: the Marshal's aura (issue 705).</summary>
    Aura,

    /// <summary>Whenever the holder's reach is walked: a cheaper step on named terrain (issue 705).</summary>
    OnMove,

    /// <summary>After the holder's own attack lands a hit on an enemy it leaves alive: the Vanguard's Opening (issue 772).</summary>
    OnHit,

    /// <summary>When the holder's drake breathes: the Drake Warden's Deep Rime (issue 872).</summary>
    OnBreath,

    /// <summary>When the holder ends a Move it flew beside an enemy: the Sky Captain's drake frost (issue 1127).</summary>
    OnLanding,

    /// <summary>On the holder's first strike of an attack after a long flight: the Sky Captain's Stoop (issue 1127).</summary>
    OnDive,

    /// <summary>When an enemy spell is aimed at an ally near the holder: Lightning Rod (issue 1280).</summary>
    OnAimed,

    /// <summary>Taken as the holder's action in place of an attack: Hask's line strike (issue 1384).</summary>
    Action,
}

/// <summary>The closed set of ability effects. Each record names its own trigger.</summary>
public abstract record AbilityEffect
{
    private protected AbilityEffect()
    {
    }

    public abstract AbilityTrigger Trigger { get; }
}

/// <summary>A passive flat delta added to the unit's stats after its class modifiers.</summary>
public sealed record StatDeltaEffect(Stats Delta) : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.Passive;
}

/// <summary>
/// An on-combat modifier: hit and crit added to this side's strikes, avoid and crit avoid
/// added against the opponent's, whenever the opponent matches <see cref="Against"/> and,
/// when <see cref="Wielding"/> is set, the holder strikes or counters with a weapon of that
/// type (issue 245). Lance Sense (section 5) is <c>+20 hit, +20 avoid against lances</c>;
/// Bloodrush is <c>+15 crit, -10 avoid while wielding an axe</c>.
/// </summary>
public sealed record CombatModifierEffect(OpponentCondition Against, int Hit, int Avoid, int Crit, int CritAvoid) : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.OnCombat;

    /// <summary>The weapon type the holder must fight with for the modifier to apply, or null for any; an unarmed holder never matches a type.</summary>
    public WeaponType? Wielding { get; init; }

    /// <summary>Whether the modifier applies to <paramref name="self"/> fighting <paramref name="opponent"/>.</summary>
    public bool AppliesTo(Combatant self, Combatant opponent) =>
        (Wielding is null || self.Weapon?.Type == Wielding) && Against.Matches(opponent);
}

/// <summary>
/// A combat art (issue 68): declared with the attack command, before the roll, on a
/// weapon of <see cref="Weapon"/>'s type at rank <see cref="Rank"/> or above. The art is
/// the weapon with these deltas added (<see cref="Apply"/>), so the forecast, the resolver,
/// burden and doubling all read it through the unchanged section 5 functions, and a Wt
/// delta can cost the unit its double. <see cref="Cost"/> is the extra uses the attack
/// spends on top of one per strike, paid whether the art hits or misses.
/// <see cref="PerMap"/> caps how many times a unit declares the art in one battle (null: no
/// cap), and <see cref="CostsNextPhase"/> makes the attack cost the unit its side's next phase,
/// in which it can neither move nor act (the captain's strike, issue 636). <see cref="Single"/>
/// makes the attack strike once whatever the speed gap (issue 739). <see cref="Item"/>
/// makes it a signature art (issue 635): declared only with that one weapon.
/// </summary>
public sealed record CombatArtEffect(WeaponType Weapon, WeaponRank Rank, int Cost, int Mt, int Hit, int Crit, int Wt, int Range) : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.Declared;

    /// <summary>The weapon as the art strikes with it: the deltas added, Mt and Wt floored at zero, the far end of its range extended.</summary>
    public Weapon Apply(Weapon weapon) => weapon with
    {
        Mt = Math.Max(0, weapon.Mt + Mt),
        Hit = weapon.Hit + Hit,
        Crit = weapon.Crit + Crit,
        Wt = Math.Max(0, weapon.Wt + Wt),
        MaxRange = weapon.MaxRange + Range,
    };

    /// <summary>The fewest uses a weapon must have left to pay for the art: its cost and the first strike.</summary>
    public int UsesNeeded => Cost + 1;

    /// <summary>How many times a unit may declare the art in one battle; null when there is no cap.</summary>
    public int? PerMap { get; init; }

    /// <summary>Whether the attack costs the unit its side's next phase: it begins that phase moved and acted (issue 636).</summary>
    public bool CostsNextPhase { get; init; }

    /// <summary>
    /// What the form costs in Grit on a <c>forms: on</c> map (issue 1461, <see cref="Ironwake.Core.Grit"/>): 1 for a
    /// reposition, 2 for a payoff, 3 for Defend, 0 for a form whose own cap is its price. Off the header <see cref="Cost"/> is paid in uses instead.
    /// </summary>
    public int Grit { get; init; }

    /// <summary>Whether the attack never doubles, whatever the speed gap (issue 739); the counter is unchanged.</summary>
    public bool Single { get; init; }

    /// <summary>The one weapon this signature art is declared with (issue 635), or null when any weapon of its type will do.</summary>
    public string? Item { get; init; }

    /// <summary>
    /// Whether the art is declared only once its heirloom <see cref="Item"/> is woken and named
    /// (issue 635, rounds 268 to 270; <see cref="Heirloom.ArtOpen"/>): the First Warden's Lance's Turn the Key.
    /// </summary>
    public bool Woken { get; init; }

    /// <summary>
    /// Whether a hit with the art on a unit that survives locks it (issue 635, rounds 269 and 270;
    /// <see cref="Lock"/>): Mov 0 on the chill's clock while the striker stands orthogonally beside it.
    /// </summary>
    public bool Locks { get; init; }
}

/// <summary>
/// A heal art (issue 635, round 261; Maud's Unasked): declared with the Item action on a healing
/// spell of type <see cref="Weapon"/>, at rank <see cref="Rank"/> or above, on an ally who has
/// neither moved, acted nor been shoved this phase. It restores <see cref="Factor"/> times the
/// plain cast, never above the ally's max HP, and spends one use as the plain cast does; the ally's
/// phase then ends where it stands, a Wait in place (it braces where <see cref="Brace"/> says so).
/// The ally's turn is the price, so the art costs no extra use. <see cref="Item"/> makes it a
/// signature art, declared only with that one spell.
/// </summary>
public sealed record HealArtEffect(WeaponType Weapon, WeaponRank Rank, int Factor) : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.Declared;

    /// <summary>The one healing spell this signature art is declared with, or null when any of its type will do.</summary>
    public string? Item { get; init; }
}

/// <summary>
/// Move Again (issue 71, DESIGN.md section 7): after an Attack, Item or Wait the unit may move
/// again on what its first move left of its Mov, through <see cref="Movement.Reach"/> from
/// where it stands. The effect has no numbers; content decides which classes carry it.
/// </summary>
public sealed record MoveAgainEffect : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.AfterAction;

    /// <summary>Whether the second move is owed only after the holder heals an ally with a spell (issue 706, the Field Surgeon), not after every action.</summary>
    public bool AfterHeal { get; init; }
}

/// <summary>
/// Banked (issue 691, renamed by issue 1042): the holder braces on a Wait in place on every map, as a
/// <c>brace: on</c> map lets every unit do (<see cref="Brace"/>). The effect has no numbers;
/// the brace's own are the rule's.
/// </summary>
public sealed record BraceEffect : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.OnWait;
}

/// <summary>
/// A longer reach (issue 704, the advanced forms): every weapon the holder strikes or heals with
/// that matches reaches <see cref="Range"/> tiles further at its far end. A weapon matches when it
/// is of type <see cref="Weapon"/>, or, with <see cref="Heals"/>, when it is a healing spell.
/// The Marksman's Long Draw is <c>+1 range with bows</c>; the Warden's Far Mending <c>+1 range on heals</c>.
/// </summary>
public sealed record RangeEffect(WeaponType? Weapon, bool Heals, int Range) : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.OnWeapon;

    /// <summary>Whether <paramref name="weapon"/> takes the longer reach.</summary>
    public bool Matches(Weapon weapon) => Heals ? weapon.Heals : weapon.Type == Weapon && !weapon.Heals;
}

/// <summary>
/// The Field Surgeon's hands (issue 706): every healing spell the holder casts restores
/// <see cref="Factor"/> times its heal and reaches no further than <see cref="Reach"/> tiles,
/// whatever its own range or any <see cref="RangeEffect"/> says.
/// </summary>
public sealed record MendingEffect(int Factor, int Reach) : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.OnWeapon;
}

/// <summary>
/// A kill's heal (issue 704, the Berserker's Blood Price): when the holder kills in a combat and
/// lives, it heals <see cref="Heal"/>, never above its max HP, if it fought with a weapon of type
/// <see cref="Wielding"/> (any weapon when null).
/// </summary>
public sealed record KillHealEffect(int Heal, WeaponType? Wielding) : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.OnKill;
}

/// <summary>
/// The Vanguard's Shoulder to Shoulder (issue 705): <see cref="Delta"/> added to the holder's stats in a
/// fight while a living ally stands orthogonally beside the tile it fights from, read where both stand
/// (<see cref="Formation.Beside"/>), so the forecast, the resolver and both planners read one number.
/// </summary>
public sealed record BesideStatsEffect(Stats Delta) : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.Beside;
}

/// <summary>
/// The Marshal's aura (issue 705): every ally of the holder within <see cref="Radius"/> tiles
/// (Manhattan) of it, never the holder itself, fights at <see cref="Hit"/> more hit and
/// <see cref="Avoid"/> more avoid (<see cref="Formation.Aura"/>). Two holders of one aura do not
/// stack; two different auras do.
/// </summary>
public sealed record AuraEffect(int Radius, int Hit, int Avoid) : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.Aura;
}

/// <summary>
/// The Pathfinder's footing (issue 705): a step into a tile of any of <see cref="Terrain"/> costs the
/// holder at most <see cref="Cost"/>, when its movement type can enter it at all; every other tile
/// costs what the terrain says (<see cref="AbilityRules.StepCost"/>).
/// </summary>
public sealed record FootingEffect(ValueList<string> Terrain, int Cost) : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.OnMove;
}

/// <summary>
/// The Vanguard's Opening (issue 772, DESIGN section 3): when the holder attacks and any of its strikes hits
/// an enemy that lives through the combat, that enemy is open (<see cref="BattleUnit.Open"/>) until the
/// phase ends, and every strike an ally of the holder makes against it reads its Def <see cref="Def"/> and
/// its Res <see cref="Res"/> lower, never below 0 (<see cref="Opening"/>). The holder's own strikes never
/// read it, a counter never opens, and a second opening refreshes the mark without stacking.
/// </summary>
public sealed record OpeningEffect(int Def, int Res) : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.OnHit;
}

/// <summary>
/// The Drake Warden's drake bite (issue 872, DESIGN section 3): in a combat at distance 1 in which one of the
/// holder's lance strikes hits and both units still stand after the exchange, the drake bites the
/// opponent once for a fixed <see cref="HalfGrown"/> while the drake is Half-grown and <see cref="Grown"/>
/// from Grown on, through no Def. It is added damage, not a strike: it never rolls, never crits, and
/// nothing that reads a strike reads it (<see cref="CombatResult.Bite"/>). It fires on counters too,
/// and while the holder is grounded. Read on the rider's <see cref="Unit.Drake"/>: no drake, no bite.
/// </summary>
public sealed record BiteEffect(int HalfGrown, int Grown) : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.OnCombat;
}

/// <summary>
/// The Drake Warden's long carry (issue 872): from Grown, after the holder sets a carried ally down
/// (<see cref="DrakeCarry"/>) it may move again on the Move the carry left, a Move Again with no strike.
/// </summary>
public sealed record LongCarryEffect : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.AfterAction;
}

/// <summary>
/// The Drake Warden's deep rime (issue 872): at Unbroken, the Rime ice the holder's breath makes (<see cref="Rime"/>)
/// holds <see cref="Rounds"/> more full rounds, an enemy phase and a player phase each, before it thaws.
/// </summary>
public sealed record DeepRimeEffect(int Rounds) : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.OnBreath;
}

/// <summary>
/// The Sky Captain's drake frost (issue 1127, DECISIONS/0262; <see cref="DrakeFrost"/>): when a rider with a drake
/// ends a Move it flew with an enemy orthogonally beside it, the drake's frost strikes every such enemy for
/// <see cref="Damage"/>, never below 1, and holds each but a boss to Mov 1 for its next phase. It then rests
/// <see cref="Rest"/> turns: fired on turn N, it is ready again on turn N + Rest + 1. Read on the rider's
/// <see cref="Unit.Drake"/>: no drake, no frost.
/// </summary>
public sealed record DrakeFrostEffect(int Damage, int Rest) : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.OnLanding;
}

/// <summary>
/// The Sky Captain's Stoop (issue 1127, DECISIONS/0262; <see cref="Stoop"/>): when the holder attacks after
/// flying at least <see cref="Flight"/> tiles this phase, counted from the tile it took off from to the tile
/// it strikes from, its first strike of the combat deals <see cref="Damage"/> more when it hits. A rider with a
/// drake never stoops: the drake's frost is hers instead.
/// </summary>
public sealed record StoopEffect(int Flight, int Damage) : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.OnDive;
}

/// <summary>
/// The Iron Warden's line strike (issue 1384, round 487; <see cref="LineStrike"/>): as the holder's action, its equipped
/// lance strikes every unit of another side on up to <see cref="Reach"/> tiles in one cardinal line out from it, once
/// each, with no counter.
/// </summary>
public sealed record LineStrikeEffect(int Reach) : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.Action;
}

/// <summary>
/// Lightning Rod (issue 1280, Lotus's #1247 rulings; <see cref="LightningRod"/>): always on, an attack by a
/// unit of another side with a tome of <see cref="School"/>, aimed at an ally of the holder within
/// <see cref="Radius"/> tiles of it, strikes the holder instead, when the tome reaches the holder from where
/// the caster stands. Spells of other schools pass.
/// </summary>
public sealed record RodEffect(MagicSchool School, int Radius) : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.OnAimed;
}

/// <summary>
/// Which opponents a combat modifier answers to: a weapon type, a movement type, both
/// (both must match), or neither (every opponent). An opponent with no weapon never
/// matches a weapon condition. <see cref="Oathbound"/> also asks that the opponent be
/// oath-bound (issue 691, <see cref="Combatant.Oathbound"/>).
/// </summary>
public sealed record OpponentCondition(WeaponType? Weapon, MovementType? Movement)
{
    public static OpponentCondition Any { get; } = new(null, null);

    /// <summary>Whether the opponent must be oath-bound (issue 691): an enemy in a group the map's <c>oathbound:</c> header names.</summary>
    public bool Oathbound { get; init; }

    public bool Matches(Combatant opponent) =>
        (Weapon is null || opponent.Weapon?.Type == Weapon)
        && (Movement is null || opponent.Movement == Movement)
        && (!Oathbound || opponent.Oathbound);
}

/// <summary>The summed on-combat modifiers one side carries against one opponent.</summary>
public readonly record struct CombatBonus(int Hit, int Avoid, int Crit, int CritAvoid)
{
    public static CombatBonus None => default;

    public static CombatBonus operator +(CombatBonus a, CombatBonus b) =>
        new(a.Hit + b.Hit, a.Avoid + b.Avoid, a.Crit + b.Crit, a.CritAvoid + b.CritAvoid);
}

/// <summary>
/// The one place abilities are applied. <see cref="Combatant.Stats"/> reads
/// <see cref="Passive"/> and <see cref="Combat"/>'s hit and crit chances read
/// <see cref="Against"/>; nothing else reads an ability's effect.
/// </summary>
public static class AbilityRules
{
    /// <summary>The sum of every passive stat delta in <paramref name="abilities"/>.</summary>
    public static Stats Passive(ValueList<Ability> abilities)
    {
        var total = Stats.Zero;
        foreach (var ability in abilities)
        {
            if (ability.Effect is StatDeltaEffect delta)
            {
                total += delta.Delta;
            }
        }

        return total;
    }

    /// <summary>
    /// <paramref name="weapon"/> as <paramref name="abilities"/> reach with it (issue 704): the far end of
    /// its range extended by every <see cref="RangeEffect"/> that matches it, then a healing spell's held to a
    /// <see cref="MendingEffect"/>'s reach (issue 706, never under its own near end); the same record when nothing changes it.
    /// </summary>
    public static Weapon Shape(Weapon weapon, ValueList<Ability> abilities)
    {
        var range = 0;
        foreach (var ability in abilities)
        {
            if (ability.Effect is RangeEffect reach && reach.Matches(weapon))
            {
                range += reach.Range;
            }
        }

        var max = weapon.MaxRange + range;
        if (weapon.Heals)
        {
            foreach (var ability in abilities)
            {
                if (ability.Effect is MendingEffect mending)
                {
                    max = Math.Min(max, Math.Max(weapon.MinRange, mending.Reach));
                }
            }
        }

        return max == weapon.MaxRange ? weapon : weapon with { MaxRange = max };
    }

    /// <summary>What <paramref name="abilities"/> multiply a healing spell's heal by (issue 706): the product of every <see cref="MendingEffect"/>'s factor, 1 when none.</summary>
    public static int HealFactor(ValueList<Ability> abilities)
    {
        var factor = 1;
        foreach (var ability in abilities)
        {
            if (ability.Effect is MendingEffect mending)
            {
                factor *= mending.Factor;
            }
        }

        return factor;
    }

    /// <summary>What <paramref name="abilities"/> heal on a kill made with <paramref name="weapon"/> (issue 704); 0 when none answers.</summary>
    public static int KillHeal(ValueList<Ability> abilities, Weapon? weapon)
    {
        var heal = 0;
        foreach (var ability in abilities)
        {
            if (ability.Effect is KillHealEffect effect && weapon is not null && (effect.Wielding is null || effect.Wielding == weapon.Type))
            {
                heal += effect.Heal;
            }
        }

        return heal;
    }

    /// <summary>The sum of every <see cref="BesideStatsEffect"/> in <paramref name="abilities"/>: what the holder gains with an ally beside it (issue 705).</summary>
    public static Stats Beside(ValueList<Ability> abilities)
    {
        var total = Stats.Zero;
        foreach (var ability in abilities)
        {
            if (ability.Effect is BesideStatsEffect beside)
            {
                total += beside.Delta;
            }
        }

        return total;
    }

    /// <summary>
    /// What a step into <paramref name="terrain"/> costs a unit moving as <paramref name="movement"/> with
    /// <paramref name="abilities"/> (issue 705): the terrain's cost, lowered to a matching
    /// <see cref="FootingEffect"/>'s; null where the movement type cannot enter, footing or not.
    /// </summary>
    public static int? StepCost(Terrain terrain, MovementType movement, ValueList<Ability> abilities)
    {
        if (terrain.MoveCost(movement) is not { } cost)
        {
            return null;
        }

        foreach (var ability in abilities)
        {
            if (ability.Effect is FootingEffect footing && footing.Terrain.Contains(terrain.Id))
            {
                cost = Math.Min(cost, footing.Cost);
            }
        }

        return cost;
    }

    /// <summary>The first <see cref="OpeningEffect"/> among <paramref name="abilities"/> (issue 772), or null when none opens.</summary>
    public static OpeningEffect? Opening(ValueList<Ability> abilities) =>
        abilities.Select(a => a.Effect).OfType<OpeningEffect>().FirstOrDefault();

    /// <summary>
    /// What the drake bites for in <paramref name="unit"/>'s combats (issue 872, <see cref="BiteEffect"/>):
    /// the best bite among <paramref name="abilities"/> at the drake's stage, 0 with no bite or no drake.
    /// </summary>
    public static int Bite(ValueList<Ability> abilities, Unit unit) =>
        unit.Drake is not { } drake ? 0
        : abilities.Select(a => a.Effect).OfType<BiteEffect>().Select(b => drake.Stage == DrakeStage.HalfGrown ? b.HalfGrown : b.Grown).DefaultIfEmpty(0).Max();

    /// <summary>Whether <paramref name="unit"/> may move again after a carry (issue 872, <see cref="LongCarryEffect"/>): it holds the long carry and its drake is Grown or more.</summary>
    public static bool LongCarry(ValueList<Ability> abilities, Unit unit) =>
        unit.Drake is { Stage: >= DrakeStage.Grown } && abilities.Any(a => a.Effect is LongCarryEffect);

    /// <summary>The extra rounds <paramref name="unit"/>'s breath's ice holds (issue 872, <see cref="DeepRimeEffect"/>): the most among <paramref name="abilities"/> at Unbroken, else 0.</summary>
    public static int DeepRime(ValueList<Ability> abilities, Unit unit) =>
        unit.Drake is not { Stage: DrakeStage.Unbroken } ? 0
        : abilities.Select(a => a.Effect).OfType<DeepRimeEffect>().Select(d => d.Rounds).DefaultIfEmpty(0).Max();

    /// <summary>The drake frost <paramref name="unit"/> carries (issue 1127, <see cref="DrakeFrostEffect"/>): the first among <paramref name="abilities"/> while it rides a drake, else null.</summary>
    public static DrakeFrostEffect? DrakeFrost(ValueList<Ability> abilities, Unit unit) =>
        unit.Drake is null ? null : abilities.Select(a => a.Effect).OfType<DrakeFrostEffect>().FirstOrDefault();

    /// <summary>The Stoop <paramref name="unit"/> carries (issue 1127, <see cref="StoopEffect"/>): the first among <paramref name="abilities"/> while it rides no drake, else null.</summary>
    public static StoopEffect? Stoop(ValueList<Ability> abilities, Unit unit) =>
        unit.Drake is not null ? null : abilities.Select(a => a.Effect).OfType<StoopEffect>().FirstOrDefault();

    /// <summary>The rod among <paramref name="abilities"/> that catches spells of <paramref name="school"/> (issue 1280, <see cref="RodEffect"/>), the widest first; null when none does.</summary>
    public static RodEffect? Rod(ValueList<Ability> abilities, MagicSchool school) =>
        abilities.Select(a => a.Effect).OfType<RodEffect>().Where(r => r.School == school).OrderByDescending(r => r.Radius).FirstOrDefault();

    /// <summary>The longest line strike among <paramref name="abilities"/> (issue 1384, <see cref="LineStrikeEffect"/>), null when none strikes a line.</summary>
    public static LineStrikeEffect? LineStrike(ValueList<Ability> abilities) =>
        abilities.Select(a => a.Effect).OfType<LineStrikeEffect>().OrderByDescending(l => l.Reach).FirstOrDefault();

    /// <summary>Whether any of <paramref name="abilities"/> braces on every map (issue 691).</summary>
    public static bool Braces(ValueList<Ability> abilities) => abilities.Any(a => a.Effect is BraceEffect);

    /// <summary>Whether any of <paramref name="abilities"/> is Move Again after every action.</summary>
    public static bool HasMoveAgain(ValueList<Ability> abilities) => abilities.Any(a => a.Effect is MoveAgainEffect { AfterHeal: false });

    /// <summary>Whether any of <paramref name="abilities"/> owes a Move Again after a heal (issue 706): Move Again of either kind.</summary>
    public static bool HasMoveAgainAfterHeal(ValueList<Ability> abilities) => abilities.Any(a => a.Effect is MoveAgainEffect);

    /// <summary>The aura <paramref name="self"/> fights under (<see cref="Combatant.Aura"/>, issue 705) plus the sum of its combat modifiers whose conditions hold: <paramref name="opponent"/> meets the opponent condition, and <paramref name="self"/>'s weapon the wielding one.</summary>
    public static CombatBonus Against(Combatant self, Combatant opponent)
    {
        var bonus = self.Aura;
        foreach (var ability in self.Abilities)
        {
            if (ability.Effect is CombatModifierEffect modifier && modifier.AppliesTo(self, opponent))
            {
                bonus = new CombatBonus(
                    bonus.Hit + modifier.Hit,
                    bonus.Avoid + modifier.Avoid,
                    bonus.Crit + modifier.Crit,
                    bonus.CritAvoid + modifier.CritAvoid);
            }
        }

        return bonus;
    }
}
