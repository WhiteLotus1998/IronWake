using System.Text;

namespace Ironwake.Core;

/// <summary>
/// A battle at one moment: the map, every living unit, whose phase it is, the turn, the
/// campaign seed, the Recall charges left, and the history of prior states (DESIGN.md
/// sections 2 and 7). Immutable; <see cref="Resolver.Apply"/> is the only way forward and
/// <see cref="Recall"/> the only way back. The state carries the seed and nothing else
/// about chance: every roll is keyed (section 5), so there is no stream position to save.
/// </summary>
/// <param name="Map">The map as authored. Starting positions are history; <see cref="Units"/> says where everyone is.</param>
/// <param name="Units">Every living unit, in ascending id order.</param>
/// <param name="Turn">The turn counter, from 1.</param>
/// <param name="Phase">Whose phase it is.</param>
/// <param name="Seed">The campaign seed every roll derives from.</param>
/// <param name="Scheme">How hit rolls are read, section 5.</param>
/// <param name="RecallCharges">Recall charges left on this map.</param>
/// <param name="History">Every prior state, oldest first, each stored with an empty history of its own so the record stays finite.</param>
/// <param name="AwakeGroups">Guard groups that have woken (section 8), sorted by name. A woken group is a fact of the board, so a Recall restores it with the rest.</param>
/// <param name="Fired">The names of the map events that have fired, blocked or not, sorted (issue 32). Each fires once; a Recall restores the list with the board.</param>
/// <param name="Flags">The flags map events have set, sorted, for a win condition to read.</param>
/// <param name="Rapport">Each recruit pair's rapport, sorted by pair (issue 16). Empty on a map without the <c>rivalry:</c> header; a Recall restores it with the board.</param>
/// <param name="Keepsakes">The weapons fallen player units left on their tiles on a <c>keepsakes: on</c> map (DESIGN.md 13.8, experiment), and those a carrier dropped where it died (issue 295), in the order they were left, until an ally recovers one or an enemy takes the tile's stack; a Recall restores the list with the board.</param>
/// <param name="Escaped">The player units that have left the board through an exit on an Escape map (issue 269), in the order they left. Nothing on the board can see them; a Recall restores the list with the board.</param>
/// <param name="LitGroups">The Guard groups whose lamps are lit on a dusk map (issue 382), sorted by name: a group a player-phase command woke, whose members the player side sees wherever they stand until the enemy phase that follows ends (<see cref="Dusk.Lit"/>). A Recall restores the list with the board.</param>
public sealed record BattleState(
    MapDefinition Map,
    ValueList<BattleUnit> Units,
    int Turn,
    Side Phase,
    ulong Seed,
    RollScheme Scheme,
    int RecallCharges,
    ValueList<BattleState> History,
    ValueList<string> AwakeGroups = default,
    ValueList<string> Fired = default,
    ValueList<string> Flags = default,
    ValueList<Rapport> Rapport = default,
    ValueList<BattleUnit> Escaped = default,
    ValueList<Keepsake> Keepsakes = default,
    ValueList<string> LitGroups = default)
{
    /// <summary>
    /// The campaign map this battle is, counted from 1 (<see cref="CampaignRecord.Begin"/>), or null
    /// for a battle played outside the campaign. An heirloom turns no stage before its ladder's
    /// first map (issue 646, <see cref="Heirloom"/>); outside the campaign nothing holds it back.
    /// </summary>
    public int? CampaignMap { get; init; }

    /// <summary>
    /// Set on a campaign side map (issue 1094, <see cref="CampaignRecord.BeginQuest(MapDefinition, string, IReadOnlyList{string}, GameContent, RollScheme)"/>),
    /// which is fought inside the campaign but is no main map, so it has no <see cref="CampaignMap"/>.
    /// </summary>
    public bool SideMap { get; init; }

    /// <summary>
    /// Whether this battle is fought inside the campaign (issue 1094): a main map (<see cref="CampaignMap"/>)
    /// or a side map (<see cref="SideMap"/>). The drake's carry and breath are open on every such battle.
    /// </summary>
    public bool InCampaign => CampaignMap is not null || SideMap;

    /// <summary>
    /// The tiles of the chests opened so far (issue 649), sorted row-major. A chest opens once and
    /// stays open; a Recall restores the list with the board.
    /// </summary>
    public ValueList<Coord> Opened { get; init; }

    /// <summary>
    /// The Rime ice the drake's breath laid (issue 805, <see cref="Rime"/>), each tile on its thaw
    /// clock, sorted row-major. A Recall restores the list with the board.
    /// </summary>
    public ValueList<RimeTile> Rime { get; init; }

    /// <summary>
    /// The timed terrain overlays on the board (issue 1245, <see cref="Earthwork"/>), each on its clock,
    /// sorted row-major. A Recall restores the list with the board.
    /// </summary>
    public ValueList<TileOverlay> Overlays { get; init; }

    /// <summary>
    /// The first strike of each side in every combat this line has fought (issue 1359, <see cref="SeenRolls"/>),
    /// oldest first. A Recall restores the list with the board; what it drops goes to <see cref="Seen"/>.
    /// </summary>
    public ValueList<SeenStrike> Struck { get; init; }

    /// <summary>
    /// The first strikes that resolved in lines a Recall discarded (issue 1359, <see cref="SeenRolls"/>), from
    /// every Recall this battle, oldest first. Knowledge, not board: a Recall keeps it and adds to it.
    /// </summary>
    public ValueList<SeenStrike> Seen { get; init; }

    /// <summary>
    /// Every unit that has died on this map, as it fell, where it fell, oldest first (issue 1284, <see cref="Hollow"/>):
    /// the bodies dark's raise reads. A raised body leaves the list, and a Hollow's death adds none. A Recall restores
    /// the list with the board.
    /// </summary>
    public ValueList<BattleUnit> Bodies { get; init; }

    /// <summary>
    /// The held bars in force (issue 1259, <see cref="HeldBars"/>), in the order they fired. A Recall
    /// restores the list with the board.
    /// </summary>
    public ValueList<HeldBar> Bars { get; init; }

    /// <summary>
    /// The spawn events waiting at their tiles under <c>arrivals: wait</c> (issue 1259), oldest first.
    /// A Recall restores the list with the board.
    /// </summary>
    public ValueList<string> Waiting { get; init; }

    /// <summary>
    /// What opened chests sent to the wagon (issue 679): the item ids that did not fit in the
    /// opener's pack, and the tomes a <c>drops:</c> enemy carried (issue 1246, <see cref="TomeDrop"/>),
    /// in the order they were taken, each at full uses. The campaign collects them
    /// only if the map is won (<see cref="CampaignRecord.AfterBattle"/>); a Recall restores the list
    /// with the board.
    /// </summary>
    public ValueList<string> Wagon { get; init; }

    /// <summary>
    /// How the map's messenger left the board (DESIGN.md 13.24, issue 675): null while it stands
    /// or on a map without one; fallen where it was removed, or gone by the road when it escaped.
    /// A Recall restores it with the board.
    /// </summary>
    public MessengerFate? MessengerGone { get; init; }

    /// <summary>
    /// How the enemy bound by the map's <c>freed:</c> header left the board (issue 750): null while
    /// it stands or on a map without one; <see cref="BondFate.Fell"/> when removed, which
    /// <see cref="Freed.After"/> marks <see cref="BondFate.Freed"/> over. A Recall restores it with the board.
    /// </summary>
    public BondFate? Bond { get; init; }

    /// <summary>
    /// Who killed the bound enemy of the map's <c>freed:</c> header, when a combat did (issue 635
    /// slice 16): null while it stands, when it was freed, or when anything but a combat removed it.
    /// A Recall restores it with the board.
    /// </summary>
    public BondKill? BondKilledBy { get; init; }

    /// <summary>
    /// The claimant passed on at the branch, fighting for the enemy on this campaign battle, and the
    /// pick whose talk turns them (issue 633, <see cref="Returned"/>); null on every other battle.
    /// Set by <see cref="WithReturned"/>.
    /// </summary>
    public ReturnBond? Return { get; init; }

    /// <summary>
    /// How the returned claimant left the board (issue 633): null while they stand or on a battle
    /// without one; <see cref="ReturnFate.Fell"/> when removed, which a talk marks
    /// <see cref="ReturnFate.Turned"/> or <see cref="ReturnFate.Spared"/> over. A Recall restores it with the board.
    /// </summary>
    public ReturnFate? ReturnGone { get; init; }

    /// <summary>
    /// The names of the map's fronts that have fallen (issue 692), sorted. A front falls once and
    /// stays fallen; a Recall restores the list with the board.
    /// </summary>
    public ValueList<string> Fallen { get; init; }

    /// <summary>
    /// The front the hunter hunts this enemy phase (issue 692, <see cref="Hunt"/>): chosen once as
    /// the enemy phase begins, so wounds dealt earlier in the phase do not move it and <c>threat</c>,
    /// read on the player phase's board, names the front that comes. Null outside an enemy phase,
    /// on a map without a hunter, and when no front stands.
    /// </summary>
    public string? Hunting { get; init; }

    /// <summary>
    /// The route taken on a <c>route_drift:</c> map (issue 81, <see cref="Routes"/>): the first of the
    /// header's two groups to wake. Null until one does, and on a map without the header. A Recall
    /// restores it with the board.
    /// </summary>
    public string? RouteTaken { get; init; }

    /// <summary>Whether the <c>route_drift:</c> header's drift has come due and been spent (issue 81). Once only; a Recall restores it.</summary>
    public bool Drifted { get; init; }

    /// <summary>
    /// The ids of the untaken route's units still making for the taken route's crossing (issue 81,
    /// <see cref="Routes"/>), in id order; a unit leaves the list as a phase begins with it within
    /// <see cref="Routes.ArriveRadius"/> of the crossing. A Recall restores it with the board.
    /// </summary>
    public ValueList<string> Drifting { get; init; }

    /// <summary>
    /// The order the captain has called this map (DESIGN.md 13.2, issue 85), or null while it is
    /// unspent. Once a map; a Recall restores it with the board.
    /// </summary>
    public OrderKind? OrderCalled { get; init; }

    /// <summary>
    /// What Frozen Iron lands for at the next phase start (issue 1385, <see cref="Swallow"/>), or 0 while no boss has
    /// swallowed: set by the swallow, it climbs after each landing.
    /// </summary>
    public int FrozenIron { get; init; }

    /// <summary>
    /// Whether Commander's Word is open on this battle (issue 85): on a map with <c>orders: on</c>,
    /// and on every campaign map from the second, where it is the captain's from Maud's arrival.
    /// </summary>
    public bool OrdersOpen => Map.OrdersEnabled || CampaignMap >= 2;

    /// <summary>The map's chests not yet opened, in file order (issue 649).</summary>
    public IEnumerable<Chest> ClosedChests => Map.Chests.Where(c => !Opened.Contains(c.At));

    /// <summary>
    /// The keepsake on top of a tile's stack (DESIGN.md 13.8, issue 295): the newest left
    /// there, which <c>recover</c> takes first; null when nothing lies there.
    /// </summary>
    public Keepsake? KeepsakeAt(Coord at) => Keepsakes.LastOrDefault(k => k.At == at);

    /// <summary>Every keepsake lying on a tile, oldest first (issue 295).</summary>
    public IReadOnlyList<Keepsake> KeepsakesAt(Coord at) => Keepsakes.Where(k => k.At == at).ToList();

    /// <summary>
    /// <paramref name="unit"/> as it would stand on <paramref name="tile"/> once it had taken
    /// the stack lying there (issue 295): an enemy on a <c>keepsakes: on</c> map that ends a
    /// move on keepsakes carries all of them, appended oldest first after its own items, so
    /// the slots it already holds keep their numbers. Any other unit, or a tile with nothing
    /// on it, is returned unchanged.
    /// </summary>
    public BattleUnit Carrying(BattleUnit unit, Coord tile)
    {
        if (!Map.KeepsakesEnabled || unit.Side != Side.Enemy || tile == unit.At)
        {
            return unit;
        }

        var stack = KeepsakesAt(tile);
        if (stack.Count == 0)
        {
            return unit;
        }

        var items = unit.Unit.Inventory.Items;
        foreach (var keepsake in stack)
        {
            items = items.Add(keepsake.Item);
        }

        return unit with { Unit = unit.Unit with { Inventory = new Inventory(items) } };
    }

    /// <summary>Whether a player unit has left the board through an exit (issue 269).</summary>
    public bool HasEscaped(string id) => Escaped.Any(u => u.Id == id);

    /// <summary>
    /// The player units still on the board once the captain has escaped (issue 269): left
    /// behind, which counts as fallen. Empty until the captain leaves.
    /// </summary>
    public IEnumerable<BattleUnit> LeftBehind() =>
        Escaped.Any(u => u.IsCaptain) ? UnitsOf(Side.Player) : Enumerable.Empty<BattleUnit>();

    /// <summary>
    /// The player units that come out of the battle alive: on every map the ones still on the
    /// board, and on an Escape map the ones that left through an exit before them. Once the
    /// captain has escaped the board's are <see cref="LeftBehind"/> and fallen (issue 269); until
    /// then nobody has been left behind, so a lost Escape map keeps its living (issue 861).
    /// </summary>
    public IEnumerable<BattleUnit> Survivors() =>
        Map.Win != WinCondition.Escape ? UnitsOf(Side.Player)
        : Escaped.Any(u => u.IsCaptain) ? Escaped
        : Escaped.Concat(UnitsOf(Side.Player));

    /// <summary>Whether a Guard group has woken. Groups of any other behavior are never asked about.</summary>
    public bool IsAwake(string group) => AwakeGroups.Contains(group);

    /// <summary>
    /// How an enemy behaves now (DESIGN.md section 8): a sleeping Guard as Hold, a woken
    /// Guard as Aggressive, a refugee still below half HP as Hold (<see cref="RetreatRule.Holds"/>,
    /// issue 215), everything else as its map says. A player unit has no behavior and gets null.
    /// </summary>
    public Behavior? EffectiveBehavior(BattleUnit unit, GameContent content) =>
        unit.Behavior switch
        {
            null => null,
            _ when RetreatRule.Holds(unit, content) => Core.Behavior.Hold,
            Core.Behavior.Guard => unit.Group is { } group && IsAwake(group) ? Core.Behavior.Aggressive : Core.Behavior.Hold,
            var other => other,
        };

    /// <summary>
    /// The history indices a Recall may target (DESIGN.md section 7): the player-phase
    /// states, oldest first. Recall is the player's action, so an enemy phase's own states
    /// are never a target (issue 190).
    /// </summary>
    public IEnumerable<int> RecallTargets() =>
        Enumerable.Range(0, History.Count).Where(i => History[i].Phase == Side.Player);

    /// <summary>This state with a group woken. Idempotent; the list stays sorted.</summary>
    public BattleState Wake(string group)
    {
        if (IsAwake(group))
        {
            return this;
        }

        var groups = AwakeGroups.Add(group).ToList();
        groups.Sort(string.CompareOrdinal);
        return this with { AwakeGroups = ValueList<string>.From(groups) };
    }

    /// <summary>Whether a group's lamps are lit (issue 382).</summary>
    public bool IsLit(string group) => LitGroups.Contains(group);

    /// <summary>This state with a group's lamps lit (issue 382). Idempotent; the list stays sorted.</summary>
    public BattleState Light(string group)
    {
        if (IsLit(group))
        {
            return this;
        }

        var groups = LitGroups.Add(group).ToList();
        groups.Sort(string.CompareOrdinal);
        return this with { LitGroups = ValueList<string>.From(groups) };
    }

    /// <summary>Whether the named map event has fired on this battle.</summary>
    public bool HasFired(string eventName) => Fired.Contains(eventName);

    /// <summary>Whether a map event has set the named flag.</summary>
    public bool HasFlag(string flag) => Flags.Contains(flag);

    /// <summary>
    /// The opening state of a map. <paramref name="roster"/> is the player's units in
    /// order: the first is the captain and fills the captain's slot, a named slot takes
    /// the roster unit with that id, and bare recruit slots are filled by the remaining
    /// roster units in roster order. Roster units past the map's slots are benched.
    /// Enemies come from <see cref="MapDefinition.EnemyUnit"/> and are named
    /// <c>template-n</c>, counting placements of that template from 1. A roster that
    /// cannot fill the map, or a unit that cannot stand where the map puts it, is a
    /// content or programming error and throws with the slot named. <paramref name="benched"/>
    /// names roster units held back for gate 4's ablation (DESIGN.md section 11): the
    /// placement such a unit would have filled, named or bare, stays empty, so the bench
    /// removes a body and never shifts another recruit into the slot. A unit whose death
    /// loses the map cannot be benched: the captain, and the recruit the map's
    /// <c>protect:</c> header names (section 7's loss order; issue 141), since an empty
    /// placement for either is a loss on the opening board and never an ablation. With
    /// <paramref name="shortHanded"/>, as a campaign battle is fought (issue 795), a bare slot
    /// the roster has nobody left to fill stays empty, the map fought short-handed, where
    /// otherwise it throws; a named slot is unchanged.
    /// </summary>
    public static BattleState From(
        MapDefinition map, GameContent content, ValueList<Unit> roster, ulong seed, RollScheme scheme = RollScheme.TwoRollAverage, ValueList<string> benched = default, bool shortHanded = false)
    {
        if (roster.Count == 0)
        {
            throw new ArgumentException("the roster needs a captain", nameof(roster));
        }

        if (benched.Contains(roster[0].Id))
        {
            throw new ArgumentException($"the captain '{roster[0].Id}' cannot be benched", nameof(benched));
        }

        if (map.ProtectId is { } protectId && benched.Contains(protectId))
        {
            throw new ArgumentException($"the protected recruit '{protectId}' cannot be benched", nameof(benched));
        }

        var units = new List<BattleUnit>();
        var deployed = new HashSet<string>(StringComparer.Ordinal);
        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (var placement in map.Placements)
        {
            if (placement is PlayerPlacement { Slot: PlayerSlot.NamedRecruit, RecruitId: { } id })
            {
                named.Add(id);
            }
        }

        var nextBare = 1;
        var perTemplate = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < map.Placements.Count; index++)
        {
            var placement = map.Placements[index];
            switch (placement)
            {
                case PlayerPlacement { Slot: PlayerSlot.AnyRecruit } when (map.DeploysAll || shortHanded) && !HasBare(roster, named, deployed, nextBare):
                    break;
                case PlayerPlacement p:
                    var unit = map.Armed(map.Supplied(map.Trial(Fill(p, roster, named, deployed, ref nextBare), content), content), content);
                    if (benched.Contains(unit.Id))
                    {
                        break;
                    }

                    units.Add(Place(unit, Side.Player, p.At, map, content) with { IsCaptain = p.Slot == PlayerSlot.Captain, PlacementIndex = index });
                    break;
                case EnemyPlacement e:
                    var count = perTemplate.GetValueOrDefault(e.TemplateId) + 1;
                    perTemplate[e.TemplateId] = count;
                    var enemy = map.EnemyUnit(e, content) with { Id = e.TemplateId + "-" + count };
                    units.Add(Place(enemy, Side.Enemy, e.At, map, content) with { Group = e.Group, Behavior = e.Behavior, IsBoss = e.IsBoss, PlacementIndex = index });
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(map), placement, "unknown placement kind");
            }
        }

        units.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        for (var i = 1; i < units.Count; i++)
        {
            if (units[i].Id == units[i - 1].Id)
            {
                throw new ArgumentException($"two units share the id '{units[i].Id}'", nameof(roster));
            }
        }

        return new BattleState(map, ValueList<BattleUnit>.From(units), 1, Side.Player, seed, scheme, map.RecallCharges, ValueList<BattleState>.Empty);
    }

    /// <summary>
    /// This opening state with the claimant passed on at the branch placed for the enemy (issue 633,
    /// <see cref="CampaignMap.Return"/>): <paramref name="unit"/> on <paramref name="at"/> in
    /// <paramref name="group"/> under <paramref name="behavior"/>, bound to <paramref name="pickId"/>'s
    /// talk. Placed by no map line, so their placement index is -1. Throws when the tile is taken or
    /// they cannot stand on it, a content error the content tests hold the shipped campaign to.
    /// </summary>
    public BattleState WithReturned(Unit unit, Coord at, string group, Behavior behavior, string pickId, GameContent content)
    {
        if (Units.Any(u => u.At == at))
        {
            throw new ArgumentException($"the returned claimant '{unit.Id}' is placed on {at}, where a unit already stands", nameof(at));
        }

        var placed = Place(unit, Side.Enemy, at, Map, content) with { Group = group, Behavior = behavior, IsBoss = false, PlacementIndex = -1 };
        var units = ValueList<BattleUnit>.From(Units.Append(placed).OrderBy(u => u.Id, StringComparer.Ordinal));
        return this with { Units = units, Return = new ReturnBond(unit.Id, pickId) };
    }

    /// <summary>Whether a recruit is left for a bare slot (issue 689): on <c>deploy: all</c>, or short-handed (issue 795), the bare slots past the company stay empty.</summary>
    private static bool HasBare(ValueList<Unit> roster, HashSet<string> named, HashSet<string> deployed, int nextBare)
    {
        for (var i = nextBare; i < roster.Count; i++)
        {
            if (!deployed.Contains(roster[i].Id) && !named.Contains(roster[i].Id))
            {
                return true;
            }
        }

        return false;
    }

    private static Unit Fill(PlayerPlacement slot, ValueList<Unit> roster, HashSet<string> named, HashSet<string> deployed, ref int nextBare)
    {
        Unit? unit;
        switch (slot.Slot)
        {
            case PlayerSlot.Captain:
                unit = roster[0];
                break;
            case PlayerSlot.NamedRecruit:
                unit = null;
                for (var i = 1; i < roster.Count; i++)
                {
                    if (roster[i].Id == slot.RecruitId)
                    {
                        unit = roster[i];
                    }
                }

                if (unit is null)
                {
                    throw new ArgumentException($"the map wants recruit '{slot.RecruitId}' at {slot.At} and the roster has no recruit with that id", nameof(roster));
                }

                break;
            default:
                unit = null;
                while (nextBare < roster.Count && unit is null)
                {
                    if (!deployed.Contains(roster[nextBare].Id) && !named.Contains(roster[nextBare].Id))
                    {
                        unit = roster[nextBare];
                    }

                    nextBare++;
                }

                if (unit is null)
                {
                    throw new ArgumentException($"the map has a recruit slot at {slot.At} and the roster has no recruit left to fill it", nameof(roster));
                }

                break;
        }

        if (!deployed.Add(unit.Id))
        {
            throw new ArgumentException($"roster unit '{unit.Id}' is deployed twice; the slot at {slot.At} asks for it again", nameof(roster));
        }

        return unit;
    }

    internal static BattleUnit Place(Unit unit, Side side, Coord at, MapDefinition map, GameContent content)
    {
        var unitClass = content.Class(unit.ClassId);
        var terrain = map.TerrainAt(at, content);
        if (!terrain.IsPassable(unitClass.Movement))
        {
            throw new ArgumentException(Movement.CannotStandMessage(at, terrain, unitClass.Movement), nameof(unit));
        }

        return new BattleUnit(RefreshSpells(unit, content), side, at, content.StatsOf(unit).Hp, false, false) { Kin = unit.Swallow };
    }

    /// <summary>Section 5: spells have uses per battle, so every Reason or Faith weapon starts a map at its full durability. Physical weapons carry what they have.</summary>
    public static Unit RefreshSpells(Unit unit, GameContent content)
    {
        var items = new List<ItemStack>(unit.Inventory.Count);
        foreach (var item in unit.Inventory.Items)
        {
            items.Add(content.Weapons.TryGetValue(item.ItemId, out var weapon) && weapon.IsMagic ? item with { Uses = weapon.Durability } : item);
        }

        return unit with { Inventory = new Inventory(ValueList<ItemStack>.From(items)) };
    }

    /// <summary>
    /// Whether the battle is over and why, DESIGN.md section 7, computed from the board
    /// and never stored. Checked in order: the captain dead, the protected recruit dead or
    /// left behind by the captain's exit (issue 269), the map's win condition met (on
    /// Escape, the captain has left through an exit; on a <c>seize_hold: 1</c> map, the captain still
    /// on the seize tile when a player phase begins, issue 1274), the turn limit passed (a win for Survive, a loss for
    /// everything else), else ongoing. A finished battle refuses every command but Recall.
    /// </summary>
    public BattleOutcome Outcome
    {
        get
        {
            var captain = Units.FirstOrDefault(u => u.IsCaptain);
            var captainEscaped = Escaped.Any(u => u.IsCaptain);
            if (captain is null && !captainEscaped)
            {
                return new BattleOutcome(BattleResult.Lost, "the captain is dead", LossCause.Captain);
            }

            if (Map.ProtectId is { } protectId)
            {
                if (Find(protectId) is null && !HasEscaped(protectId))
                {
                    return new BattleOutcome(BattleResult.Lost, $"{protectId} is dead", LossCause.Protected);
                }

                if (captainEscaped && Find(protectId) is not null)
                {
                    return new BattleOutcome(BattleResult.Lost, $"{protectId} is left behind", LossCause.Protected);
                }
            }

            var won = Map.Win switch
            {
                WinCondition.Rout => !UnitsOf(Side.Enemy).Any(),
                WinCondition.Seize => Map.IsThrone(captain!.At) && (!Map.SeizeHold || AtPlayerPhaseStart),
                WinCondition.DefeatBoss => !UnitsOf(Side.Enemy).Any(u => u.IsBoss) && Map.BossSpawns().All(e => HasFired(e.Name)),
                WinCondition.Escape => captainEscaped,
                WinCondition.Survive => Turn > Map.TurnLimit,
                _ => throw new ArgumentOutOfRangeException(nameof(Map), Map.Win, "unknown win condition"),
            };
            if (won)
            {
                return new BattleOutcome(BattleResult.Won, MapRenderer.WinName(Map.Win));
            }

            if (Turn > Map.TurnLimit)
            {
                return new BattleOutcome(BattleResult.Lost, $"turn {Map.TurnLimit} passed", LossCause.Timeout);
            }

            return BattleOutcome.Ongoing;
        }
    }

    /// <summary>
    /// Whether this state is the start of a player phase, the one an enemy phase just ended into
    /// (or the turn past the limit it decides): what a <c>seize_hold: 1</c> map reads (issue 1274), since
    /// the captain on the seize tile now stood on it through that whole enemy phase. False on turn 1,
    /// which no enemy phase came before.
    /// </summary>
    public bool AtPlayerPhaseStart => Phase == Side.Player && History.Count > 0 && History[^1].Phase == Side.Enemy;

    /// <summary>The living unit with an id, or null.</summary>
    public BattleUnit? Find(string id)
    {
        foreach (var unit in Units)
        {
            if (unit.Id == id)
            {
                return unit;
            }
        }

        return null;
    }

    /// <summary>The living unit standing on a tile, or null.</summary>
    public BattleUnit? UnitAt(Coord at)
    {
        foreach (var unit in Units)
        {
            if (unit.At == at)
            {
                return unit;
            }
        }

        return null;
    }

    /// <summary>Who stands on a tile, seen from a mover's side: the occupancy answer <see cref="Movement.Reach"/> asks for.</summary>
    public Occupant OccupantAt(Coord at, Side moverSide) =>
        UnitAt(at) switch
        {
            null => Occupant.None,
            var unit when unit.Side == moverSide => Occupant.Ally,
            _ => Occupant.Enemy,
        };

    public IEnumerable<BattleUnit> UnitsOf(Side side)
    {
        foreach (var unit in Units)
        {
            if (unit.Side == side)
            {
                yield return unit;
            }
        }
    }

    /// <summary>
    /// Where a unit may move, by the section 4 rule on the board as it stands: its class's Mov, one
    /// more when Pressed (issue 85), one less when chilled, never below 1 (issue 702, <see cref="Frost.Mov"/>), less under armor, never below 1 (issue 1282, <see cref="Armor.Mov"/>),
    /// and 0 while locked (issue 635, <see cref="Lock.Holds"/>) or frozen, a boss 1 (issue 1330, <see cref="Freeze.Mov"/>);
    /// on foot while grounded, and no move at all when stranded (issue 703, <see cref="Grounding"/>).
    /// </summary>
    public Reach ReachOf(BattleUnit unit, GameContent content)
    {
        var unitClass = content.Class(unit.Unit.ClassId);
        var mov = Lock.Holds(this, unit) ? 0 : Freeze.Mov(DrakeFrost.Mov(Armor.Mov(Frost.Mov(unitClass.Mov + (unit.Pressed ? 1 : 0), unit), unit), unit), unit);
        return ReachOn(unit, content, mov);
    }

    /// <summary>
    /// Where a dash may take <paramref name="unit"/> (DESIGN.md 13.27, <see cref="Winded"/>): the
    /// section 4 reach on its Move plus <see cref="Winded.ExtraMov"/>, chill and pressing read as
    /// for a Move. A unit held by a lock goes nowhere.
    /// </summary>
    public Reach DashReachOf(BattleUnit unit, GameContent content)
    {
        var unitClass = content.Class(unit.Unit.ClassId);
        var mov = Lock.Holds(this, unit) ? 0 : Freeze.Mov(DrakeFrost.Mov(Armor.Mov(Frost.Mov(unitClass.Mov + (unit.Pressed ? 1 : 0), unit), unit) + Winded.ExtraMov, unit), unit);
        return ReachOn(unit, content, mov);
    }

    /// <summary>
    /// The route of a <c>move ... via</c> (13.25, issue 782): the reach's path to <paramref name="via"/>,
    /// then the cheapest path from there to <paramref name="to"/> on the Mov left, the unit's own
    /// tile read as empty on the second leg. The entry carries the joined path and the total cost,
    /// and <see cref="ReachEntry.CanEnd"/> as the second leg reads <paramref name="to"/>. Null, with
    /// <paramref name="refused"/> naming the leg, when either leg is out of reach.
    /// </summary>
    public ReachEntry? RouteVia(BattleUnit unit, GameContent content, Coord via, Coord to, out string refused)
    {
        var reach = ReachOf(unit, content);
        if (!Map.Contains(via) || !Map.Contains(to))
        {
            refused = "outside the map";
            return null;
        }

        if (reach.EntryAt(via) is not { } first)
        {
            refused = $"{via} is not within {reach.Mov} movement from {unit.At}";
            return null;
        }

        var left = reach.Mov - first.Cost;
        var second = Movement.Reach(Map, content, via, reach.Movement, left, at => at == unit.At ? Occupant.None : OccupantAt(at, unit.Side), content.AbilitiesOf(unit.Unit)).EntryAt(to);
        if (second is null)
        {
            refused = $"{to} is not within the {left} movement left at {via}";
            return null;
        }

        refused = "";
        return new ReachEntry(to, first.Cost + second.Cost, ValueList<Coord>.From(first.Path.Concat(second.Path)), second.CanEnd && to != via || to == via && first.CanEnd);
    }

    /// <summary>
    /// The section 4 reach of <paramref name="unit"/> on <paramref name="mov"/> points by how it moves now
    /// (<see cref="Grounding.MovementOf"/>), its footing read (issue 705); its own tile alone when grounded where it cannot walk.
    /// </summary>
    private Reach ReachOn(BattleUnit unit, GameContent content, int mov) =>
        Grounding.Stranded(this, unit, content)
            ? Movement.Reach(Map, content, unit.At, content.Class(unit.Unit.ClassId).Movement, 0, at => OccupantAt(at, unit.Side))
            : Movement.Reach(Map, content, unit.At, Grounding.MovementOf(unit, content), mov, at => OccupantAt(at, unit.Side), content.AbilitiesOf(unit.Unit));

    /// <summary>
    /// Where a Fall back order's move may take a unit (issue 85): the section 4 reach from where it
    /// stands on <see cref="Orders.FallBackMov"/>, or null when no such move is owed.
    /// </summary>
    public Reach? FallBackReachOf(BattleUnit unit, GameContent content)
    {
        if (!unit.FallingBack)
        {
            return null;
        }

        return ReachOn(unit, content, Orders.FallBackMov);
    }

    /// <summary>
    /// Where a unit's Canto may take it (issue 71): the same section 4 reach from where it
    /// stands on the budget its first move left, or null when no Canto is owed.
    /// </summary>
    public Reach? CantoReachOf(BattleUnit unit, GameContent content)
    {
        if (!unit.Acted || unit.Canto is not { } budget)
        {
            return null;
        }

        return ReachOn(unit, content, budget);
    }

    /// <summary>This state with a Hollow risen on the board (issue 1284, <see cref="Hollow.Raise"/>), the units kept in id order. Its id must be free.</summary>
    public BattleState WithRisen(BattleUnit risen) =>
        this with { Units = ValueList<BattleUnit>.From(Units.Append(risen).OrderBy(u => u.Id, StringComparer.Ordinal)) };

    /// <summary>This state with one unit replaced by id. The unit must exist.</summary>
    public BattleState WithUnit(BattleUnit unit)
    {
        for (var i = 0; i < Units.Count; i++)
        {
            if (Units[i].Id == unit.Id)
            {
                return this with { Units = Units.SetItem(i, unit) };
            }
        }

        throw new ArgumentException($"no living unit with id '{unit.Id}'", nameof(unit));
    }

    /// <summary>
    /// This state with a unit removed by id. The unit must exist. Removing the messenger records
    /// it as fallen on its tile (<see cref="MessengerGone"/>); <see cref="Messenger.AfterMove"/>
    /// marks an escape over that. Removing the bound enemy of a <c>freed:</c> header records it
    /// as fallen (<see cref="Bond"/>); <see cref="Freed.After"/> marks a freeing over that. Removing
    /// the returned claimant records them as fallen (<see cref="ReturnGone"/>); a talk marks its fate over that.
    /// </summary>
    public BattleState WithoutUnit(string id)
    {
        for (var i = 0; i < Units.Count; i++)
        {
            if (Units[i].Id == id)
            {
                var gone = Messenger.Is(this, Units[i]) ? new MessengerFate(Units[i].At, Escaped: false) : MessengerGone;
                var bond = Freed.IsBound(this, Units[i]) ? BondFate.Fell : Bond;
                var returned = Returned.Is(this, Units[i]) ? ReturnFate.Fell : ReturnGone;
                return this with { Units = Units.RemoveAt(i), MessengerGone = gone, Bond = bond, ReturnGone = returned };
            }
        }

        throw new ArgumentException($"no living unit with id '{id}'", nameof(id));
    }

    /// <summary>
    /// The state as text, one line per fact, the same bytes for the same state on every
    /// machine. Gate 6 compares replays through this; it is the seed of issue 25's protocol.
    /// History is summarised by its length; each prior state is canonical on its own.
    /// </summary>
    public string Canonical()
    {
        var sb = new StringBuilder();
        sb.Append("map ").Append(Map.Name).Append(' ').Append(Map.Width).Append('x').Append(Map.Height).Append('\n');
        sb.Append("turn ").Append(Turn).Append(" phase ").Append(Phase)
            .Append(" seed ").Append(Seed).Append(" scheme ").Append(Scheme)
            .Append(" recall ").Append(RecallCharges).Append(" history ").Append(History.Count)
            .Append(" outcome ").Append(Outcome.Result).Append('\n');
        sb.Append("awake");
        foreach (var group in AwakeGroups)
        {
            sb.Append(' ').Append(group);
        }

        sb.Append('\n');
        if (LitGroups.Count > 0)
        {
            sb.Append("lit");
            foreach (var group in LitGroups)
            {
                sb.Append(' ').Append(group);
            }

            sb.Append('\n');
        }

        if (Map.Events.Count > 0)
        {
            sb.Append("fired");
            foreach (var name in Fired)
            {
                sb.Append(' ').Append(name);
            }

            sb.Append("\nflags");
            foreach (var flag in Flags)
            {
                sb.Append(' ').Append(flag);
            }

            sb.Append('\n');
            for (var i = 0; i < Map.TerrainIds.Count; i++)
            {
                if (Map.Events.Any(e => e.Action is ChangeTerrain c && c.At.Y * Map.Width + c.At.X == i))
                {
                    sb.Append("tile ").Append(new Coord(i % Map.Width, i / Map.Width)).Append(' ').Append(Map.TerrainIds[i]).Append('\n');
                }
            }
        }

        if (Map.RivalryArm is not null)
        {
            sb.Append("rapport");
            foreach (var entry in Rapport)
            {
                sb.Append(' ').Append(entry.A).Append('+').Append(entry.B).Append('=').Append(entry.Points);
            }

            sb.Append('\n');
        }

        if (Map.Win == WinCondition.Escape)
        {
            sb.Append("escaped");
            foreach (var unit in Escaped)
            {
                sb.Append(' ').Append(unit.Id);
            }

            sb.Append('\n');
        }

        if (Map.KeepsakesEnabled)
        {
            sb.Append("keepsakes");
            foreach (var keepsake in Keepsakes)
            {
                sb.Append(' ').Append(keepsake.Item.ItemId).Append('x').Append(keepsake.Item.Uses).Append('@').Append(keepsake.At).Append('/').Append(keepsake.FallenId);
            }

            sb.Append('\n');
        }

        if (Map.Chests.Count > 0)
        {
            sb.Append("opened");
            foreach (var at in Opened)
            {
                sb.Append(' ').Append(at);
            }

            sb.Append('\n');
        }

        if (Rime.Count > 0)
        {
            sb.Append("rime");
            foreach (var tile in Rime)
            {
                sb.Append(' ').Append(tile.At).Append('/').Append(tile.Side).Append('/').Append(tile.Clock);
            }

            sb.Append('\n');
        }

        if (Overlays.Count > 0)
        {
            sb.Append("overlays");
            foreach (var overlay in Overlays)
            {
                sb.Append(' ').Append(overlay.At).Append('/').Append(overlay.TerrainId).Append('/').Append(overlay.UnderId).Append('/').Append(overlay.OwnerId).Append('/').Append(overlay.Side).Append('/').Append(overlay.Clock);
            }

            sb.Append('\n');
        }

        if (Bodies.Count > 0)
        {
            sb.Append("bodies");
            foreach (var body in Bodies)
            {
                sb.Append(' ').Append(body.Id).Append('@').Append(body.At);
            }

            sb.Append('\n');
        }

        if (Bars.Count > 0)
        {
            sb.Append("bars");
            foreach (var bar in Bars)
            {
                sb.Append(' ').Append(bar.Event).Append('/').Append(bar.Holder).Append('/').Append(bar.At).Append('/').Append(bar.TerrainId).Append('/').Append(bar.UnderId);
            }

            sb.Append('\n');
        }

        if (Waiting.Count > 0)
        {
            sb.Append("waiting ").Append(string.Join(' ', Waiting)).Append('\n');
        }

        if (Wagon.Count > 0)
        {
            sb.Append("wagon ").Append(string.Join(' ', Wagon)).Append('\n');
        }

        if (OrderCalled is { } order)
        {
            sb.Append("order ").Append(Orders.Word(order)).Append('\n');
        }

        if (FrozenIron > 0)
        {
            sb.Append("frozeniron ").Append(FrozenIron).Append('\n');
        }

        if (Map.Fronts.Count > 0)
        {
            sb.Append("fallen");
            foreach (var front in Fallen)
            {
                sb.Append(' ').Append(front);
            }

            sb.Append('\n');
        }

        if (Hunting is { } hunting)
        {
            sb.Append("hunting ").Append(hunting).Append('\n');
        }

        if (Map.RouteDrift is not null)
        {
            sb.Append("route ").Append(RouteTaken ?? "-").Append(Drifted ? " drifted" : " pending");
            foreach (var id in Drifting)
            {
                sb.Append(' ').Append(id);
            }

            sb.Append('\n');
        }

        if (MessengerGone is { } fate)
        {
            sb.Append("messenger ").Append(fate.Escaped ? "escaped" : "fallen").Append(' ').Append(fate.At).Append('\n');
        }

        if (Bond is { } bondFate)
        {
            sb.Append("bond ").Append(bondFate == BondFate.Freed ? "freed" : "fell").Append('\n');
        }

        if (BondKilledBy is { } bondKill)
        {
            sb.Append("bond killed by ").Append(bondKill.KillerId).Append(bondKill.Fed ? " fed" : " unfed").Append('\n');
        }

        if (Return is { } returnBond)
        {
            sb.Append("return ").Append(returnBond.UnitId).Append(" pick ").Append(returnBond.PickId).Append('\n');
        }

        if (ReturnGone is { } returnFate)
        {
            sb.Append("returned ").Append(returnFate.ToString().ToLowerInvariant()).Append('\n');
        }

        foreach (var unit in Units)
        {
            sb.Append("unit ").Append(unit.Id).Append(' ').Append(unit.Side).Append(' ').Append(unit.At)
                .Append(" hp ").Append(unit.Hp)
                .Append(unit.Moved ? " moved" : " unmoved").Append(unit.Acted ? " acted" : " ready")
                .Append(" class ").Append(unit.Unit.ClassId)
                .Append(" level ").Append(unit.Unit.Level).Append(" exp ").Append(unit.Unit.Exp)
                .Append(" stats ").Append(unit.Unit.Stats)
                .Append(" items");
            foreach (var item in unit.Unit.Inventory.Items)
            {
                sb.Append(' ').Append(item.ItemId).Append('x').Append(item.Uses);
                if (item.Keepsake is { } fallen)
                {
                    sb.Append('/').Append(fallen);
                }
            }

            if (unit.IsCaptain)
            {
                sb.Append(" captain");
            }

            if (unit.Canto is { } canto)
            {
                sb.Append(" canto ").Append(canto);
            }

            if (unit.Pressed)
            {
                sb.Append(" pressed");
            }

            if (unit.FallingBack)
            {
                sb.Append(" fallingback");
            }

            if (unit.Hollow is { } hollow)
            {
                sb.Append(" hollow ").Append(hollow.RaiserId).Append('/').Append(hollow.FallenId).Append('/').Append(hollow.Phases);
            }

            if (unit.RaiseSpent)
            {
                sb.Append(" raisespent");
            }

            if (unit.Swallowed)
            {
                sb.Append(" swallowed");
            }

            if (unit.Side == Side.Enemy)
            {
                sb.Append(" group ").Append(unit.Group).Append(' ').Append(unit.Behavior).Append(unit.IsBoss ? " boss" : "");
            }

            sb.Append('\n');
        }

        return sb.ToString();
    }
}
