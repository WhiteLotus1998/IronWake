namespace Ironwake.Core;

/// <summary>
/// The questions a renderer asks the core instead of counting for itself (DESIGN.md
/// section 2; the presentation protocol of issue 25 exposes these by name): where a unit
/// can move, whom it can attack from where it stands, and what a combat would look like.
/// The CLI computes none of this.
/// </summary>
public static class Queries
{
    /// <summary>
    /// Where the unit may move, section 4, on the board as it stands: its Canto's reach
    /// when it has acted and is owed one (issue 71), else its full reach.
    /// </summary>
    public static Reach Reachable(BattleState state, GameContent content, BattleUnit unit) =>
        state.CantoReachOf(unit, content) ?? state.ReachOf(unit, content);

    /// <summary>The enemy units the unit's equipped weapon reaches from where it stands and its side sees (DESIGN.md 13.7), in id order. Empty when it has no weapon.</summary>
    public static IEnumerable<BattleUnit> Targets(BattleState state, GameContent content, BattleUnit unit)
    {
        var weapon = unit.EquippedWeapon(content);
        if (weapon is null)
        {
            yield break;
        }

        foreach (var other in state.UnitsOf(unit.Side == Side.Player ? Side.Enemy : Side.Player))
        {
            if (weapon.InRange(unit.At.DistanceTo(other.At)) && Dusk.Sees(state, unit.Side, other.At))
            {
                yield return other;
            }
        }
    }

    /// <summary>
    /// The section 5 forecast of the unit attacking the target from where it stands with
    /// its equipped weapon, or with the weapon in <paramref name="slot"/>; null when it
    /// cannot (no weapon, a slot that holds no usable weapon, the target out of range or
    /// on its own side, or a target the unit's side cannot see at dusk), the same combatants the resolver would build. With
    /// <paramref name="art"/> it is the forecast of that combat art (issue 68): the art's
    /// numbers, its cost in <see cref="CombatForecast.ArtCost"/>, and null when the resolver
    /// would refuse the art.
    /// </summary>
    public static CombatForecast? Forecast(BattleState state, GameContent content, BattleUnit unit, BattleUnit target, int? slot = null, string? art = null) =>
        Forecast(state, content, unit, target, unit.At, slot, art);

    /// <summary>
    /// The forecast of the unit attacking the target from <paramref name="from"/>, a tile it
    /// could move to this phase, on that tile's terrain and at that tile's distance (issue
    /// 151): the number the player needs while choosing where to stand, before the move is
    /// made and final. Null when the tile is not one the unit can end a move on now (out of
    /// reach, an occupied tile, or any tile but its own once it has moved this phase), or
    /// for the reasons the standing forecast is null. From the unit's own tile it is the
    /// standing forecast. Read-only: nothing moves.
    /// </summary>
    public static CombatForecast? Forecast(BattleState state, GameContent content, BattleUnit unit, BattleUnit target, Coord from, int? slot = null, string? art = null)
    {
        if (!CanStandOn(state, content, unit, from))
        {
            return null;
        }

        var (armed, weapon, rejection) = Resolver.ChooseWeapon(unit, content, slot);
        if (rejection is not null)
        {
            return null;
        }

        CombatArtEffect? declared = null;
        if (art is not null)
        {
            (declared, rejection) = Resolver.ChooseArt(armed, content, weapon!, art);
            if (rejection is not null)
            {
                return null;
            }

            weapon = declared!.Apply(weapon!);
        }

        var distance = from.DistanceTo(target.At);
        if (!weapon!.InRange(distance) || target.Side == unit.Side || !Dusk.Sees(state, unit.Side, target.At, unit.Id, from))
        {
            return null;
        }

        var forecast = Combat.Forecast((armed with { At = from }).ToCombatant(state, content, art: declared, against: target), target.Answering(state, content, from, armed), distance, state.Scheme);
        return declared is null ? forecast : forecast with { ArtCost = declared.Cost };
    }

    /// <summary>
    /// Why the unit's weapon choice gives no forecast: the refusal of the slot
    /// (<see cref="Resolver.ChooseWeapon"/>) or of the art (<see cref="Resolver.ChooseArt"/>),
    /// the same the resolver would give; null when both would be accepted.
    /// </summary>
    public static Rejection? WeaponRefusal(GameContent content, BattleUnit unit, int? slot, string? art)
    {
        var (armed, weapon, rejection) = Resolver.ChooseWeapon(unit, content, slot);
        if (rejection is not null || art is null)
        {
            return rejection;
        }

        return Resolver.ChooseArt(armed, content, weapon!, art).Rejection;
    }

    /// <summary>
    /// Whether the unit could stand on the tile this phase: its own tile always, otherwise a
    /// tile its reach lets it end on, and only while it has not moved (section 7: Move is
    /// optional, comes first, and is final).
    /// </summary>
    public static bool CanStandOn(BattleState state, GameContent content, BattleUnit unit, Coord at) =>
        at == unit.At || (!unit.Moved && Reachable(state, content, unit).CanEnd(at));

    /// <summary>
    /// What the coming enemy phase could do to <paramref name="unit"/> if it ended its move
    /// on <paramref name="from"/> (issue 217): one line per enemy with an attack on it, in
    /// the phase's own order, each the strike the planner would make
    /// were it to choose this unit, through <see cref="EnemyAi.StrikeOn"/>, with the
    /// forecast the enemy phase prints before that strike. The board is
    /// <see cref="ThreatBoard"/>'s. A group still asleep is not listed (see
    /// <see cref="SleepingThreats"/>) and a unit that holds strikes only from its own tile.
    /// An enemy an announced event spawns at the start of that enemy phase is priced like
    /// any other and carries the tile it arrives on (issue 248); an unannounced one is not
    /// on the board the query reads (DECISIONS/0045). Each line reads that phase-start
    /// board; an earlier enemy's move or kill in the phase is not played out. A unit owed a
    /// Canto (issue 71) is asked from any tile its Canto can end on, since that is where it
    /// still chooses to stand. Null when the unit cannot stand on the tile this phase, or
    /// when the state is not a player phase. Read-only.
    /// </summary>
    public static IReadOnlyList<ThreatLine>? Threats(BattleState state, GameContent content, BattleUnit unit, Coord from)
    {
        if (ThreatBoard(state, content, unit, from) is not (var board, var moved, var arrivals))
        {
            return null;
        }

        var lines = new List<ThreatLine>();
        if (moved is null)
        {
            return lines;
        }

        foreach (var enemy in board.UnitsOf(Side.Enemy))
        {
            if (board.EffectiveBehavior(enemy, content) is null || EnemyAi.StrikeOn(board, content, enemy, moved) is not { } strike)
            {
                continue;
            }

            var carrier = board.Carrying(enemy, strike.From);
            var forecast = StrikeForecast(board, content, carrier, moved, strike);
            Coord? arrives = arrivals.TryGetValue(enemy.Id, out var at) ? at : null;
            var weapon = carrier.UsableWeaponAt(content, strike.Slot)!;
            lines.Add(new ThreatLine(carrier, strike.From, strike.Slot, weapon, forecast, arrives, StrikeTiles(board, content, enemy, moved), Windup.Raises(board, weapon)));
        }

        return lines;
    }

    /// <summary>
    /// The forecast of <paramref name="enemy"/>'s planned <paramref name="strike"/> on
    /// <paramref name="target"/>, which <see cref="Threats"/> prices. The planner only plans a
    /// strike the forecast can price, so a strike with no forecast is a broken invariant between
    /// <see cref="EnemyAi.StrikeOn"/> and <see cref="Forecast(BattleState, GameContent, BattleUnit, BattleUnit, Coord, int?, string?)"/>
    /// and throws <see cref="InvalidOperationException"/> naming both units and the tile.
    /// </summary>
    public static CombatForecast StrikeForecast(BattleState board, GameContent content, BattleUnit enemy, BattleUnit target, EnemyStrike strike) =>
        Forecast(board, content, enemy, target, strike.From, strike.Slot)
            ?? throw new InvalidOperationException($"the planner's strike of {enemy.Id} on {target.Id} from {strike.From} has no forecast");

    /// <summary>
    /// The enemies on <see cref="Threats"/>' board that would strike <paramref name="unit"/> on
    /// <paramref name="from"/> in daylight but cannot this enemy phase only because they do not
    /// know where it is or their side cannot see it (DESIGN.md 13.7, issue 302), in unit order;
    /// <c>threat</c> prices them at 0 and says why. Empty in daylight. Null exactly when
    /// <see cref="Threats"/> is. Read-only.
    /// </summary>
    public static IReadOnlyList<BattleUnit>? Unseeing(BattleState state, GameContent content, BattleUnit unit, Coord from)
    {
        if (ThreatBoard(state, content, unit, from) is not (var board, var moved, _))
        {
            return null;
        }

        if (moved is null || Dusk.Sight(board) is null)
        {
            return Array.Empty<BattleUnit>();
        }

        return board.UnitsOf(Side.Enemy)
            .Where(e => board.EffectiveBehavior(e, content) is not null
                && EnemyAi.StrikeOn(board, content, e, moved) is null
                && EnemyAi.StrikeOn(board, content, e, moved, inDaylight: true) is not null)
            .ToList();
    }

    /// <summary>
    /// What the lines of <see cref="Threats"/> deal together if every strike lands (issue
    /// 253): the worst case over assignments of enemies to distinct strike tiles, each enemy
    /// on one tile of its <see cref="ThreatLine.Tiles"/>, at most one enemy per tile, an
    /// enemy left without a free tile dropped. Two enemies whose only tile is the same one
    /// count once, the harder hitter. Each enemy is weighed at its line's
    /// <see cref="ThreatLine.IfAllLand"/>. The lines are a transversal matroid over the
    /// tiles, so taking them heaviest first (ties in line order) and keeping each one an
    /// augmenting path can still seat is the maximum.
    /// A line that only raises a blow (<see cref="ThreatLine.Raises"/>) deals nothing that
    /// phase and is left out, so it holds no tile (issue 444).
    /// </summary>
    public static int IfAllLand(IReadOnlyList<ThreatLine> lines) =>
        Exposure.SeatedSum(lines.Where(l => !l.Raises).Select(l => (l.IfAllLand, (IReadOnlyList<Coord>)(l.Tiles ?? ValueList<Coord>.Of(l.From)))).ToList());

    /// <summary>
    /// <see cref="IfAllLand(IReadOnlyList{ThreatLine})"/> with a blow already raised over the tile
    /// (<see cref="RaisedBlow"/>) added: it lands for certain at the coming enemy phase start,
    /// before any strike, so it is in the coming phase's total (DESIGN.md 13.16, issue 444).
    /// </summary>
    public static int IfAllLand(IReadOnlyList<ThreatLine> lines, RaisedBlow? blow) =>
        IfAllLand(lines) + (blow?.Damage ?? 0);

    /// <summary>
    /// The blow already raised over <paramref name="tile"/> on a <c>windup: on</c> map (DESIGN.md
    /// 13.16) by a unit other than <paramref name="unit"/>: its wielder, and the damage it lands
    /// on the unit standing there at the wielder's next phase start, certain
    /// (<see cref="Windup.Damage"/>). Null on a map without the header or with no blow over the
    /// tile. Read-only.
    /// </summary>
    public static RaisedBlow? RaisedBlowOn(BattleState state, GameContent content, BattleUnit unit, Coord tile)
    {
        if (!state.Map.WindupEnabled || Windup.Over(state, tile) is not { } wielder || wielder.Id == unit.Id)
        {
            return null;
        }

        return new RaisedBlow(wielder, tile, Windup.Damage(state, content, wielder, unit with { At = tile }));
    }

    /// <summary>
    /// Every tile <paramref name="enemy"/> could strike <paramref name="target"/> from on the
    /// board, with any usable weapon: its own tile only when it holds or has moved, as
    /// <see cref="EnemyAi.StrikeOn"/> reads it, otherwise every tile of its reach it may end
    /// on, and a tile another enemy stands on who may move off it first, since the phase
    /// can play in either order. In row-major order.
    /// </summary>
    private static ValueList<Coord> StrikeTiles(BattleState board, GameContent content, BattleUnit enemy, BattleUnit target)
    {
        var weapons = Enumerable.Range(0, enemy.Unit.Inventory.Count)
            .Select(slot => enemy.UsableWeaponAt(content, slot))
            .OfType<Weapon>()
            .ToList();
        bool Strikes(Coord tile) => weapons.Any(w => w.InRange(tile.DistanceTo(target.At)));
        bool MayMove(BattleUnit unit) => !unit.Moved && board.EffectiveBehavior(unit, content) == Behavior.Aggressive;

        if (!MayMove(enemy))
        {
            return ValueList<Coord>.Of(enemy.At);
        }

        var tiles = board.ReachOf(enemy, content).Entries
            .Where(e => e.CanEnd || board.UnitsOf(Side.Enemy).Any(u => u.At == e.At && MayMove(u)))
            .Select(e => e.At)
            .Where(Strikes);
        return ValueList<Coord>.From(tiles);
    }

    /// <summary>
    /// The Guard groups still asleep on <see cref="ThreatBoard"/>'s board of which some
    /// member could strike <paramref name="unit"/> on <paramref name="from"/> were the group
    /// awake (issue 248, the thirty-sixth round's shape one): each group with every living
    /// member, in group order, and no numbers, so the player learns a sleeping group is a
    /// question without being handed its answer. A group the tile itself certainly wakes is
    /// already awake on that board and priced by <see cref="Threats"/> instead. Null exactly
    /// when <see cref="Threats"/> is. Read-only.
    /// </summary>
    public static IReadOnlyList<SleepingThreat>? SleepingThreats(BattleState state, GameContent content, BattleUnit unit, Coord from)
    {
        if (ThreatBoard(state, content, unit, from) is not (var board, var moved, _))
        {
            return null;
        }

        var groups = new List<SleepingThreat>();
        if (moved is null)
        {
            return groups;
        }

        var sleeping = board.UnitsOf(Side.Enemy)
            .Where(u => u is { Behavior: Behavior.Guard, Group: not null } && !board.IsAwake(u.Group))
            .Select(u => u.Group!)
            .Distinct()
            .OrderBy(g => g, StringComparer.Ordinal);
        foreach (var group in sleeping)
        {
            var woken = board.Wake(group);
            var members = ValueList<BattleUnit>.From(woken.UnitsOf(Side.Enemy).Where(u => u.Group == group));
            if (members.Any(m => EnemyAi.StrikeOn(woken, content, m, moved) is not null))
            {
                groups.Add(new SleepingThreat(group, members));
            }
        }

        return groups;
    }

    /// <summary>
    /// Whether <paramref name="unit"/> ending its move on <paramref name="from"/> wins the
    /// battle there and then (issue 356): the board with the unit on the tile and nothing else
    /// changed reads <see cref="BattleResult.Won"/>, as the captain on a Seize throne does. No
    /// enemy phase follows such a move, so <c>threat</c> says it wins instead of reading as an
    /// all-clear. False when <see cref="Threats"/> would be null. Read-only.
    /// </summary>
    public static bool MoveWins(BattleState state, GameContent content, BattleUnit unit, Coord from)
    {
        var standable = CanStandOn(state, content, unit, from) || state.CantoReachOf(unit, content)?.CanEnd(from) == true;
        if (state.Phase != Side.Player || unit.Side != Side.Player || !standable || state.Outcome.IsOver)
        {
            return false;
        }

        return state.WithUnit(unit with { At = from }).Outcome.Result == BattleResult.Won;
    }

    /// <summary>
    /// The board <see cref="Threats"/> and <see cref="SleepingThreats"/> read: the exposure
    /// sum's, <see cref="Exposure.Board"/> (the unit on the tile, every group its standing
    /// there certainly wakes awake), with the player phase then ended through the resolver,
    /// so the phase-start healing and events the enemy phase would see are applied. An enemy
    /// an unannounced event spawned in that phase start is taken off the board again, and
    /// an announced one is kept and named in <c>Arrivals</c> with its tile. <c>Moved</c> is
    /// null when ending the phase ends the battle or removes the unit. Null when the unit
    /// cannot stand on the tile this phase, or when the state is not a player phase.
    /// </summary>
    private static (BattleState Board, BattleUnit? Moved, IReadOnlyDictionary<string, Coord> Arrivals)? ThreatBoard(BattleState state, GameContent content, BattleUnit unit, Coord from)
    {
        var standable = CanStandOn(state, content, unit, from) || state.CantoReachOf(unit, content)?.CanEnd(from) == true;
        if (state.Phase != Side.Player || unit.Side != Side.Player || !standable)
        {
            return null;
        }

        var arrivals = new Dictionary<string, Coord>(StringComparer.Ordinal);
        var ended = Resolver.Apply(Exposure.Board(state, content, unit, from), content, new EndPhase());
        if (!ended.Accepted || ended.Next.Outcome.IsOver || ended.Next.Find(unit.Id) is not { } moved)
        {
            return (state, null, arrivals);
        }

        var board = ended.Next;
        foreach (var spawned in ended.Events.OfType<UnitSpawned>())
        {
            if (state.Map.Announced)
            {
                arrivals[spawned.UnitId] = spawned.At;
            }
            else
            {
                board = board.WithoutUnit(spawned.UnitId);
            }
        }

        return (board, moved, arrivals);
    }
}

/// <summary>A Guard group <see cref="Queries.SleepingThreats"/> names: asleep, and able to strike the unit were it awake.</summary>
public sealed record SleepingThreat(string Group, ValueList<BattleUnit> Members);

/// <summary>
/// One enemy's strike on a unit as <see cref="Queries.Threats"/> prices it: who, from
/// where, with which slot's weapon, and the forecast of that combat. <paramref name="Arrives"/>
/// is the tile an announced event spawns the enemy on at the start of that enemy phase
/// (issue 248), null for an enemy already on the board. <paramref name="Tiles"/> is every
/// tile the enemy could strike the unit from, which <see cref="Queries.IfAllLand(IReadOnlyList{ThreatLine})"/> reads
/// so two enemies are never counted on one tile (issue 253); null reads as
/// <paramref name="From"/> alone. <paramref name="Raises"/> is true when the strike is a
/// windup weapon's raise (<see cref="Windup.Raises"/>), which deals nothing that phase and
/// lands the phase after (issue 444).
/// </summary>
public sealed record ThreatLine(BattleUnit Enemy, Coord From, int Slot, Weapon Weapon, CombatForecast Forecast, Coord? Arrives = null, ValueList<Coord>? Tiles = null, bool Raises = false)
{
    /// <summary>
    /// The damage the strike deals if every hit lands, no crit, over the strikes the enemy
    /// lives to make: a double stops at the first round when the unit's plain counter
    /// between them kills the enemy (<see cref="CombatForecast.AttackerDamageLivedFor"/>, issue 315).
    /// 0 for a line that only raises a blow (<see cref="Raises"/>), which deals nothing that
    /// phase (issue 444).
    /// </summary>
    public int IfAllLand => Raises ? 0 : Forecast.AttackerDamageLivedFor(Enemy.Hp);
}

/// <summary>
/// A blow already raised over <paramref name="Over"/> by <paramref name="Wielder"/> (DESIGN.md
/// 13.16), as <see cref="Queries.RaisedBlowOn"/> reads it: <paramref name="Damage"/> lands for
/// certain at the wielder's next phase start on the unit standing there, unless a hit breaks it.
/// </summary>
public sealed record RaisedBlow(BattleUnit Wielder, Coord Over, int Damage);
