namespace Ironwake.Core;

/// <summary>
/// A unit as it stands in a battle: the roster or template <see cref="Unit"/> it is, which
/// side it fights for, where it stands, its current HP, and whether it has moved or acted
/// this phase (DESIGN.md section 7: Move is optional and comes first, then one of Attack,
/// Item, Wait). Enemies also carry their group and behavior from the map (section 8).
/// The unit's id is its <see cref="Unit"/>'s id: for a player unit the roster id, for an
/// enemy the template id with a per-template counter (<c>brigand-1</c>), so every roll key
/// and every event names one body on the map. <see cref="PlacementIndex"/> is the index of
/// the map placement the unit filled, so a renderer draws it with the letter section 10
/// gives that slot for the whole battle, wherever it has moved. <see cref="Retreated"/>
/// is set by a <see cref="Retreat"/> and never cleared, so no unit retreats twice.
/// <see cref="Canto"/> is what a Canto unit's Move left of its Mov this phase (issue 71),
/// its full Mov when it acts without moving, and null once the Canto is taken or declined,
/// at the end of the phase, and for a unit without Canto; a <see cref="Canto"/> command is
/// legal only while the unit has acted and this is not null.
/// <see cref="Grudge"/> is the id of the player unit an enemy is sworn against on a
/// <c>grudges: on</c> map (DESIGN.md 13.4, experiment): set when that unit kills one of the
/// enemy's group, replaced by a newer such kill, and null otherwise.
/// <see cref="Shoved"/> is set when an ally's <see cref="Shove"/> moved the unit this phase
/// (DESIGN.md 13.12) and cleared with <see cref="Moved"/> when a phase begins: the shove leaves
/// the unit its own Move, but it did not begin its turn where it stands, so it may not exit
/// there this phase (section 7, issue 396).
/// <see cref="Braced"/> is set when the unit waits on the tile it began its phase on, on a <c>brace: on</c> map
/// (DESIGN.md 13.14, experiment) and cleared when its side's next phase begins or an ally
/// shoves it; while it is set, strikes against the unit lose <see cref="Brace.Hit"/> hit.
/// <see cref="WindupAt"/> is the tile a raised blow will land on, on a <c>windup: on</c> map
/// (DESIGN.md 13.16, experiment): set by an attack with a windup weapon, cleared when the blow
/// lands at the unit's side's next phase start or when a hit on the unit breaks it.
/// <see cref="Watching"/> is set by a <see cref="Watch"/> on an <c>overwatch: on</c> map
/// (DESIGN.md 13.17, experiment) and cleared when the watch fires, when the unit is struck, or
/// when its side's next phase begins.
/// <see cref="CoveredBy"/> is the id of the ally covering this unit on a <c>cover: on</c> map
/// (DESIGN.md 13.19, experiment): set by that ally's <see cref="Cover"/>, cleared when the cover
/// fires and when this unit's side's next phase begins.
/// <see cref="ArtsDeclared"/> lists, once per declaration, the arts with a per-map cap the unit
/// has declared this battle (issue 636), so <see cref="Resolver.ChooseArt"/> refuses one spent;
/// it is board state, so Recall restores the charge with the board.
/// <see cref="Spent"/> is the price of an art that costs the next phase (issue 636): 1 from the
/// attack to the end of that phase, 2 through the unit's side's next phase, which it begins moved
/// and acted, so it neither moves, acts, waits nor braces, and 0 otherwise.
/// <see cref="HasFed"/> is set when a hungering weapon the unit carries feeds on a kill (DESIGN.md
/// 13.23, experiment) and cleared at each of the unit's side's phase starts, after the drain reads it.
/// <see cref="Pressed"/> is set by a Press order (DESIGN.md 13.2, issue 85) on an ally that has not
/// moved: +1 Mov until the phase ends. <see cref="FallingBack"/> is set by a Fall back order on an
/// ally that has acted: one <see cref="FallBack"/> move is owed until it is taken or the phase ends.
/// </summary>
public sealed record BattleUnit(
    Unit Unit,
    Side Side,
    Coord At,
    int Hp,
    bool Moved,
    bool Acted,
    string? Group = null,
    Behavior? Behavior = null,
    bool IsBoss = false,
    bool IsCaptain = false,
    int PlacementIndex = -1,
    bool Retreated = false,
    int? Canto = null,
    string? Grudge = null,
    bool Shoved = false,
    bool Braced = false,
    Coord? WindupAt = null,
    bool Watching = false,
    string? CoveredBy = null,
    ValueList<string>? ArtsDeclared = null,
    int Spent = 0,
    bool HasFed = false,
    bool Pressed = false,
    bool FallingBack = false)
{
    /// <summary>
    /// The chill clock (issue 702 slice 2, <see cref="Frost"/>): 0 unchilled; 1 struck by frozen iron,
    /// its side's next phase not yet begun; 2 that phase under way, cleared when it ends. While it is
    /// not 0 the unit's Mov is one less, never below 1 (<see cref="Frost.Mov"/>).
    /// </summary>
    public int Chill { get; init; }

    /// <summary>
    /// Set when the unit's woken hungering weapon ran the hunt on (issue 804, <see cref="Kinsbane.RunsOn"/>):
    /// a Canto of its full Move after a kill, once a battle. Never cleared; board state, so Recall restores it.
    /// </summary>
    public bool HuntRan { get; init; }

    /// <summary>
    /// The unit whose Turn the Key locked this one (issue 635, <see cref="Lock"/>), or null. While the
    /// chill clock runs and that unit stands orthogonally beside it, its Mov is 0 (<see cref="Lock.Holds"/>).
    /// </summary>
    public string? LockedBy { get; init; }

    /// <summary>
    /// The open mark (issue 772, <see cref="Opening"/>): set when a holder of Opening hits the unit and leaves it
    /// alive, cleared when the phase ends; null when the unit is not open. While it is set, a strike on the unit
    /// by any unit of another side than its own, other than <see cref="OpenMark.By"/>, reads its Def and Res lower.
    /// </summary>
    public OpenMark? Open { get; init; }

    /// <summary>
    /// How many tiles further than its side's dusk sight the unit sees (issue 706, the Scout's
    /// <see cref="SightEffect"/>), read from its abilities when it is placed (<see cref="BattleUnit.Sighted"/>).
    /// 0 for nearly every unit; nothing in daylight (<see cref="Dusk.Sees"/>).
    /// </summary>
    public int ExtraSight { get; init; }

    /// <summary>This unit with <see cref="ExtraSight"/> read from its abilities in <paramref name="content"/> (issue 706).</summary>
    public BattleUnit Sighted(GameContent content) => this with { ExtraSight = AbilityRules.ExtraSight(content.AbilitiesOf(Unit)) };

    /// <summary>
    /// The grounding clock (issue 703, <see cref="Grounding"/>): 0 not grounded; 1 a flier landed by a
    /// bow's crit, its side's next phase not yet begun; 2 that phase under way, cleared when it ends.
    /// While it is not 0 the unit moves on foot (<see cref="Grounding.MovementOf"/>).
    /// </summary>
    public int Grounded { get; init; }

    /// <summary>How many times the unit has declared <paramref name="artId"/> this battle, counted only for an art with a per-map cap.</summary>
    public int TimesDeclared(string artId) => ArtsDeclared is { } declared ? declared.Count(id => id == artId) : 0;

    /// <summary>Whether the unit is paying an art's next-phase cost now: its side's phase, begun moved and acted (issue 636).</summary>
    public bool Resting => Spent == 2;

    public string Id => Unit.Id;

    public int MaxHp(GameContent content) => content.StatsOf(Unit).Hp;

    /// <summary>
    /// The inventory slot of the weapon the unit strikes with: the first slot that
    /// <see cref="UsableWeaponAt"/> accepts. -1 when there is none. An <see cref="Attack"/>
    /// naming another usable slot moves that weapon to the front first.
    /// </summary>
    public int EquippedSlot(GameContent content)
    {
        for (var slot = 0; slot < Unit.Inventory.Count; slot++)
        {
            if (UsableWeaponAt(content, slot) is not null)
            {
                return slot;
            }
        }

        return -1;
    }

    /// <summary>
    /// The weapon in a slot if the unit can strike with it: a weapon its class can use at a
    /// rank the unit has reached (issue 67) that is not a healing spell, skipping a spell with no uses left this battle
    /// (section 5: a physical weapon at zero uses still fights, broken; a spent spell
    /// does not). Null for an empty slot, an item, a healing spell, or a spent spell.
    /// </summary>
    public Weapon? UsableWeaponAt(GameContent content, int slot)
    {
        if (slot < 0 || slot >= Unit.Inventory.Count)
        {
            return null;
        }

        var item = Unit.Inventory.Items[slot];
        var unitClass = content.Class(Unit.ClassId);
        return content.Weapons.TryGetValue(item.ItemId, out var weapon) && Unit.CanWield(weapon, unitClass) && !weapon.Heals
            && (item.Uses > 0 || !weapon.IsMagic)
            ? content.WeaponOf(Unit, Frost.Shape(Forge.Shape(Heirloom.Shape(Kinsbane.Shape(weapon, item), item), item), item, content))
            : null;
    }

    /// <summary>
    /// How many different weapons this unit could strike with at <paramref name="distance"/>:
    /// distinct weapon ids among the slots <see cref="UsableWeaponAt"/> accepts whose range covers
    /// it. More than one is when a forecast or <c>threat</c> line names the weapon (DESIGN.md
    /// 13.11, issue 313); two copies of one weapon count once.
    /// </summary>
    public int WeaponChoicesAt(GameContent content, int distance) =>
        Enumerable.Range(0, Unit.Inventory.Count)
            .Select(slot => UsableWeaponAt(content, slot))
            .Where(weapon => weapon is not null && weapon.InRange(distance))
            .Select(weapon => weapon!.Id)
            .Distinct()
            .Count();

    /// <summary>This unit with the item in <paramref name="slot"/> moved to the front of its inventory, the other slots keeping their order.</summary>
    public BattleUnit WithSlotInFront(int slot)
    {
        if (slot == 0)
        {
            return this;
        }

        var items = new List<ItemStack>(Unit.Inventory.Count) { Unit.Inventory.Items[slot] };
        for (var i = 0; i < Unit.Inventory.Count; i++)
        {
            if (i != slot)
            {
                items.Add(Unit.Inventory.Items[i]);
            }
        }

        return this with { Unit = Unit with { Inventory = new Inventory(ValueList<ItemStack>.From(items)) } };
    }

    /// <summary>
    /// The weapon in <see cref="EquippedSlot"/>, or null when the unit has none, in which case it can neither attack nor counter.
    /// A hungering weapon comes back as its stack has grown or starved it (<see cref="Kinsbane.Shape"/>),
    /// an heirloom at the stage its stack has reached (<see cref="Heirloom.Shape"/>), either with the forge's steps on it (<see cref="Forge.Shape"/>), marked frozen iron when its stack has made it so (<see cref="Frost.Shape"/>), and at the reach the unit's abilities give it (<see cref="GameContent.WeaponOf"/>, issue 704).
    /// </summary>
    public Weapon? EquippedWeapon(GameContent content)
    {
        var slot = EquippedSlot(content);
        if (slot < 0)
        {
            return null;
        }

        var stack = Unit.Inventory.Items[slot];
        return content.WeaponOf(Unit, Frost.Shape(Forge.Shape(Heirloom.Shape(Kinsbane.Shape(content.Weapon(stack.ItemId), stack), stack), stack), stack, content));
    }

    /// <summary>Whether the equipped weapon is at zero uses and fights at the broken fallback.</summary>
    public bool WeaponBroken(GameContent content)
    {
        var slot = EquippedSlot(content);
        return slot >= 0 && Unit.Inventory.Items[slot].Uses == 0;
    }

    /// <summary>This unit as the section 5 formulas see it, on the terrain it stands on, its weapon broken or whole.</summary>
    public Combatant ToCombatant(MapDefinition map, GameContent content) =>
        content.CombatantOf(Unit, Grounding.ForMap(map, EquippedWeapon(content)), map.TerrainAt(At, content), Hp, 0, WeaponBroken(content)) with { Oathbound = map.IsOathbound(this) };

    /// <summary>
    /// This unit as the formulas see it on a board, rivalry's modifiers included (issue 16):
    /// the neighbours are read from <paramref name="state"/> at this unit's <see cref="At"/>,
    /// so a forecast may pass the unit at a tile it has not moved to yet.
    /// <paramref name="countering"/> is true for the side that is struck first and answers.
    /// <paramref name="art"/> is a combat art the attacker declared (issue 68): the equipped
    /// weapon strikes as the art makes it. A counter never carries one.
    /// <paramref name="against"/> is the unit it fights, when known: a unit sworn against by
    /// that enemy loses <see cref="Grudges.SwornCritAvoid"/> crit avoid (issue 331), and on a
    /// <c>pincer: on</c> map this unit strikes it at <see cref="Pincer.Hit"/> more when it is
    /// pinned (DESIGN.md 13.13), read at both units' <see cref="At"/>, and at
    /// <see cref="Brace.Hit"/> less when it is braced (DESIGN.md 13.14). On a <c>signatures: on</c>
    /// map a strike, never a counter, carries Teodor's orders or his watched penalty (DESIGN.md 13.18).
    /// A unit the pair rule binds cannot crit it when it has an ally beside it (<see cref="PairRule"/>, issue 692).
    /// The captain's formation effects are read here too (<see cref="Formation"/>, issue 705), and a support
    /// partner's tier beside it joins the aura (<see cref="Supports.Bonus"/>, issue 77).
    /// An art that strikes once makes the side <see cref="Combatant.SingleStrike"/> (issue 739).
    /// An open unit answering a strike by an ally of its opener reads its Def and Res lower (<see cref="Opening.Lowered"/>, issue 772).
    /// </summary>
    public Combatant ToCombatant(BattleState state, GameContent content, bool countering = false, CombatArtEffect? art = null, BattleUnit? against = null)
    {
        if (countering && art is not null)
        {
            throw new ArgumentException($"{Id} is countering and cannot declare an art", nameof(art));
        }

        var (hit, crit, critAvoid) = Rivalry.Modifiers(state, content, this, countering);
        critAvoid += Grudges.CritAvoidAgainst(this, against);
        hit += Brace.StrikeHit(state, this, against) + Signatures.StrikeHit(state, content, this, countering);
        var weapon = EquippedWeapon(content);
        if (art is not null && weapon is not null)
        {
            weapon = art.Apply(weapon);
        }

        var terrain = state.Map.TerrainAt(At, content);
        var beside = Formation.Beside(state, content, this);
        var combatant = content.CombatantOf(Unit, Grounding.ForMap(state.Map, weapon), terrain, Hp, critAvoid, WeaponBroken(content), hit, crit, beside);
        if (countering && Opening.Reads(this, against))
        {
            combatant = content.CombatantOf(Unit, Grounding.ForMap(state.Map, weapon), terrain, Hp, critAvoid, WeaponBroken(content), hit, crit, beside + Opening.Lowered(Open!, combatant.Stats));
        }

        return combatant with { Oathbound = state.Map.IsOathbound(this), PairHeld = PairRule.Holds(state, this, against), SingleStrike = art is { Single: true }, Aura = Formation.Aura(state, content, this) + Supports.Bonus(state, content, this) };
    }

    /// <summary>
    /// This unit as it answers a strike from <paramref name="attackerAt"/>: countering, and
    /// <see cref="Combatant.Blind"/> when its side cannot see that tile at dusk, since a counter
    /// needs sight the same as a strike (DESIGN.md 13.7, issue 308). Every forecast, the
    /// resolver and the enemy planner build the answering side here, so all read one number.
    /// <paramref name="attacker"/> is the unit striking, for the sworn crit avoid (issue 331).
    /// </summary>
    public Combatant Answering(BattleState state, GameContent content, Coord attackerAt, BattleUnit? attacker = null) =>
        ToCombatant(state, content, countering: true, against: attacker) with { Blind = !Dusk.Sees(state, Side, attackerAt) };
}
