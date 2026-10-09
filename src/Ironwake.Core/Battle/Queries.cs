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
    /// Where the unit may move, section 4, on the board as it stands: its Move Again's reach
    /// when it has acted and is owed one (issue 71), else its full reach.
    /// </summary>
    public static Reach Reachable(BattleState state, GameContent content, BattleUnit unit) =>
        state.MoveAgainReachOf(unit, content) ?? state.ReachOf(unit, content);

    /// <summary>
    /// What a move would walk and wear without making it (13.25, issue 782): the move applied to
    /// the board as it stands and read back, so the preview and the walk cannot disagree. The
    /// route as <see cref="UnitMoved"/> carries it, and each tile the walk wears with the terrain
    /// it ends as, in walk order, one per tile (a map event's terrain change is not wear and is
    /// left out). Null with the resolver's refusal when the move
    /// would be refused.
    /// </summary>
    public static WalkPreview? PreviewMove(BattleState state, GameContent content, Move move, out Rejection? rejection)
    {
        var result = Resolver.Apply(state, content, move);
        rejection = result.Rejection;
        if (!result.Accepted)
        {
            return null;
        }

        var moved = result.Events.OfType<UnitMoved>().First();
        var left = moved.Path.Take(moved.Path.Count - 1).Prepend(moved.From).ToHashSet();
        var worn = new List<(Coord At, string TerrainId)>();
        foreach (var changed in result.Events.OfType<TerrainChanged>())
        {
            if (!left.Contains(changed.At) || state.Map.TerrainAt(changed.At, content).WearsTo is null)
            {
                continue;
            }

            var index = worn.FindIndex(w => w.At == changed.At);
            if (index >= 0)
            {
                worn[index] = (changed.At, changed.TerrainId);
            }
            else
            {
                worn.Add((changed.At, changed.TerrainId));
            }
        }

        return new WalkPreview(moved, ValueList<(Coord At, string TerrainId)>.From(worn));
    }

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
            (declared, rejection) = Resolver.ChooseArt(armed, content, weapon!, art, state.Map.FormsEnabled);
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

        var holder = LightningRod.Catcher(state, content, from, weapon, target);
        var struck = holder ?? target;
        var forecast = Combat.Forecast(Stoop.Poised(content, armed, from).ToCombatant(state, content, art: declared, against: struck), struck.Answering(state, content, from, armed) with { Catching = holder is not null }, from.DistanceTo(struck.At), state.Scheme) with { CaughtBy = holder?.Id };
        return declared is null || state.Map.FormsEnabled ? forecast : forecast with { ArtCost = declared.Cost };
    }

    /// <summary>
    /// Every way the unit could strike the target from where it stands (issue 611), the attack
    /// menu's rows in order: the plain attack with each weapon it carries (healing spells, area
    /// tomes, which are cast with the Item action (issue 1329), and items left out), then each art it knows under each carried weapon of the art's type, or
    /// once under no weapon when it carries none of that type; a signature art (issue 635) is
    /// listed only under its own item, and not at all while the unit does not carry it. Each row is the
    /// <see cref="Attack"/> it would submit, with the forecast <see cref="Forecast(BattleState, GameContent, BattleUnit, BattleUnit, int?, string?)"/>
    /// gives it when the resolver would accept it, or the resolver's own refusal, so a renderer
    /// greys a row with the rule's words and never judges legality itself. The equipped
    /// weapon's rows carry no slot, so a plain strike is the same command a click made before
    /// the menu.
    /// </summary>
    public static IReadOnlyList<AttackOption> AttackOptions(BattleState state, GameContent content, BattleUnit unit, BattleUnit target)
    {
        var equipped = unit.EquippedSlot(content);
        var slots = Enumerable.Range(0, unit.Unit.Inventory.Count)
            .Where(slot => content.Weapons.TryGetValue(unit.Unit.Inventory.Items[slot].ItemId, out var weapon) && !weapon.Heals && weapon.Area == 0)
            .ToList();
        var rows = new List<AttackOption>();
        void Add(int? slot, Ability? art)
        {
            var command = new Attack(unit.Id, target.Id, slot == equipped ? null : slot, art?.Id);
            var weaponId = slot is { } s ? unit.Unit.Inventory.Items[s].ItemId : null;
            var refusal = Resolver.Apply(state, content, command).Rejection;
            var forecast = refusal is null ? Forecast(state, content, unit, target, command.Slot, command.Art) : null;
            rows.Add(new AttackOption(command, weaponId, art, forecast, refusal));
        }

        foreach (var slot in slots)
        {
            Add(slot, null);
        }

        foreach (var (ability, art) in content.ArtsOf(unit.Unit))
        {
            var matching = slots.Where(slot => content.Weapon(unit.Unit.Inventory.Items[slot].ItemId).Type == art.Weapon
                && (art.Item is null || unit.Unit.Inventory.Items[slot].ItemId == art.Item)).ToList();
            if (matching.Count == 0 && art.Item is not null)
            {
                continue;
            }

            if (matching.Count == 0)
            {
                Add(slots.Contains(equipped) ? equipped : slots.Count > 0 ? slots[0] : null, ability);
                continue;
            }

            foreach (var slot in matching)
            {
                Add(slot, ability);
            }
        }

        return rows;
    }

    /// <summary>
    /// Why the unit's weapon choice gives no forecast: the refusal of the slot
    /// (<see cref="Resolver.ChooseWeapon"/>) or of the art (<see cref="Resolver.ChooseArt"/>),
    /// the same the resolver would give; null when both would be accepted.
    /// </summary>
    public static Rejection? WeaponRefusal(GameContent content, BattleUnit unit, int? slot, string? art, bool forms = false)
    {
        var (armed, weapon, rejection) = Resolver.ChooseWeapon(unit, content, slot);
        if (rejection is not null || art is null)
        {
            return rejection;
        }

        return Resolver.ChooseArt(armed, content, weapon!, art, forms).Rejection;
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
    /// <see cref="ThreatBoard"/>'s. A member of a group still asleep behaves as Hold (DESIGN.md 8):
    /// it is listed only for a strike from its own tile, and what it could do woken is
    /// <see cref="SleepingThreats"/>' business; a unit that holds strikes only from its own tile.
    /// An enemy an announced event spawns at the start of that enemy phase is priced like
    /// any other and carries the tile it arrives on (issue 248); an unannounced one is not
    /// on the board the query reads (DECISIONS/0045). Each line reads that phase-start
    /// board; an earlier enemy's move or kill in the phase is not played out. A unit owed a
    /// Move Again (issue 71) is asked from any tile its Move Again can end on, since that is where it
    /// still chooses to stand. A strike a cover would swap onto the coverer (DESIGN.md 13.19)
    /// is priced against the coverer on the unit's tile and carries it as <see cref="ThreatLine.CoveredBy"/>.
    /// On a <c>dash: on</c> map a tile only a dash reaches (DESIGN.md 13.27) is priced with the unit
    /// winded, as it would stand there.
    /// On a dusk map (DESIGN.md 13.7) an enemy that does not know where the unit is, or whose side
    /// cannot see it, strikes anyway once a side-mate acting before it in the phase stands within
    /// sight of the unit (issue 987): such an enemy is priced on the board with that side-mate on
    /// its strike tile and carries it as <see cref="ThreatLine.LitBy"/>, after the other lines
    /// (<see cref="LitStrikes"/>).
    /// Null when the unit cannot stand on the tile this phase, or
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

        var direct = new Dictionary<string, ThreatLine>(StringComparer.Ordinal);
        var blocked = new List<BattleUnit>();
        foreach (var enemy in board.UnitsOf(Side.Enemy))
        {
            if (board.EffectiveBehavior(enemy, content) is null)
            {
                continue;
            }

            if (EnemyAi.StrikeOn(board, content, enemy, moved) is not { } strike)
            {
                blocked.Add(enemy);
                continue;
            }

            direct[enemy.Id] = Line(board, enemy, strike);
        }

        var held = new Dictionary<string, ThreatLine>(StringComparer.Ordinal);
        foreach (var enemy in blocked)
        {
            if (HeldStrike(board, content, moved, enemy, direct.Values) is var (onto, strike, freedBy, heldBy))
            {
                held[enemy.Id] = Line(onto, enemy, strike) with { FreedBy = freedBy, HeldBy = heldBy };
            }
        }

        foreach (var enemy in board.UnitsOf(Side.Enemy))
        {
            if (direct.TryGetValue(enemy.Id, out var line) || held.TryGetValue(enemy.Id, out line))
            {
                lines.Add(line);
            }
        }

        ThreatLine Line(BattleState on, BattleUnit enemy, EnemyStrike strike)
        {
            var carrier = on.Carrying(enemy, strike.From);
            var weapon = carrier.UsableWeaponAt(content, strike.Slot)!;
            var covered = Windup.Raises(on, weapon) ? null : CoverRule.Swapped(on, moved);
            var forecast = covered is ({ } swapped, { } coverer, _)
                ? StrikeForecast(swapped, content, carrier, coverer, strike)
                : StrikeForecast(on, content, carrier, moved, strike);
            Coord? arrives = arrivals.TryGetValue(enemy.Id, out var at) ? at : null;
            return new ThreatLine(carrier, strike.From, strike.Slot, weapon, forecast, arrives, StrikeTiles(board, content, enemy, moved), Windup.Raises(on, weapon)) { CoveredBy = covered?.Struck };
        }

        foreach (var (enemy, lighter, lit, strike) in LitStrikes(board, content, moved, lines.Where(l => l.HeldBy is null).ToList()))
        {
            var carrier = lit.Carrying(enemy, strike.From);
            var weapon = carrier.UsableWeaponAt(content, strike.Slot)!;
            Coord? arrives = arrivals.TryGetValue(enemy.Id, out var at) ? at : null;
            lines.Add(new ThreatLine(carrier, strike.From, strike.Slot, weapon, StrikeForecast(lit, content, carrier, moved, strike), arrives, StrikeTiles(lit, content, enemy, moved), Windup.Raises(lit, weapon)) { LitBy = lighter });
        }

        return lines
            .Select(l => StrikeWakes(board, content, moved, l) is { Count: > 0 } wakes ? l with { Wakes = ValueList<FightWake>.From(wakes) } : l)
            .Select(l => l with { Casts = CastsInstead(content, moved, l) })
            .ToList();
    }

    /// <summary>
    /// The cast a line's enemy may make in place of its strike (issue 1286, DECISIONS/0316 amended):
    /// <see cref="EnemyAi.CastsInstead"/> when the strike would not kill the unit it lands on if every hit lands
    /// (the planner's own kill flag), since the planner never casts over a kill. Null for a kill, a windup raise or a
    /// held line. The line stays priced and counted: the total is the worst case.
    /// </summary>
    private static CastKind? CastsInstead(GameContent content, BattleUnit moved, ThreatLine line)
    {
        if (line.Raises || line.HeldBy is not null)
        {
            return null;
        }

        var struck = line.CoveredBy ?? moved;
        return line.Forecast.AttackerDamageLivedFor(line.Enemy.Hp) >= struck.Hp ? null : EnemyAi.CastsInstead(content, line.Enemy);
    }

    /// <summary>
    /// The sleeping groups the noise of one priced enemy strike would wake (issue 1290), in
    /// <see cref="FightWakes"/>' shape: <see cref="WakeCheck.Run"/> on <see cref="Threats"/>' board with the
    /// enemy moved to its strike tile, once with the fight's two tiles (the strike tile and the tile struck:
    /// the unit's, or a Lightning Rod holder's) as noise at <see cref="Signatures.NoiseRadius"/> and once
    /// without, keeping the groups only the noisy run wakes. A group the stop already wakes is awake on that
    /// board and never named. Unpriced, so the one-wave total of DECISIONS/0281 is untouched. Empty for a
    /// raise, which fights no combat, and for a held line, which is not counted.
    /// </summary>
    private static IReadOnlyList<FightWake> StrikeWakes(BattleState board, GameContent content, BattleUnit moved, ThreatLine line)
    {
        if (line.Raises || line.HeldBy is not null || !board.Units.Any(u => u is { Behavior: Behavior.Guard, Group: { } g } && !board.IsAwake(g)))
        {
            return Array.Empty<FightWake>();
        }

        var rod = line.Forecast.CaughtBy is { } rodId ? board.Find(rodId) : null;
        var struck = rod ?? line.CoveredBy ?? moved;
        var after = board.WithUnit(line.Enemy with { At = line.From });
        var radius = Signatures.NoiseRadius(board, content, line.Enemy, struck);
        var noisy = new[] { new Noise(line.From, radius), new Noise(rod?.At ?? moved.At, radius) };
        var quiet = WakeCheck.Run(board, after, content, Array.Empty<Noise>(), Array.Empty<string>()).Select(w => w.Group).ToHashSet(StringComparer.Ordinal);
        return WakeCheck.Run(board, after, content, noisy, Array.Empty<string>())
            .Where(w => !quiet.Contains(w.Group))
            .Select(w => new FightWake(w.Group, w.CalledBy, w.Cause == WakeCause.Noise ? ValueList<Coord>.From(WakeCheck.HeardFrom(after, w.Group, noisy)) : ValueList<Coord>.Empty))
            .ToList();
    }

    /// <summary>
    /// The strike of an <paramref name="enemy"/> that <see cref="EnemyAi.StrikeOn"/> refuses on the
    /// phase-start <paramref name="board"/> only because a side-mate stands on a tile it would
    /// strike from (issue 1256). A holder that has a line of its own from another tile
    /// (<paramref name="direct"/>) steps off first in the worst case the total prices, so the
    /// enemy is asked again on the board with the holder on its strike tile, one wave deep as a
    /// tile a counter frees is (issue 1191): the strike comes with <c>FreedBy</c> set and is counted.
    /// Otherwise a holder that stays (no line, or a line from where it stands) keeps the tile: the
    /// enemy is asked with the holder lifted off the board, and a strike from that very tile comes
    /// with <c>HeldBy</c> set, printed but not counted, so a line is never silently absent. Holders
    /// are tried in unit order, steppers first; null when none lets the enemy strike.
    /// </summary>
    private static (BattleState Board, EnemyStrike Strike, BattleUnit? FreedBy, BattleUnit? HeldBy)? HeldStrike(BattleState board, GameContent content, BattleUnit moved, BattleUnit enemy, IEnumerable<ThreatLine> direct)
    {
        var steps = direct.ToDictionary(l => l.Enemy.Id, l => l.From, StringComparer.Ordinal);
        var weapons = Enumerable.Range(0, enemy.Unit.Inventory.Count)
            .Select(slot => enemy.UsableWeaponAt(content, slot))
            .OfType<Weapon>()
            .ToList();
        var mayMove = !enemy.Moved && board.EffectiveBehavior(enemy, content) == Behavior.Aggressive;
        var reach = mayMove ? board.ReachOf(enemy, content).Entries.Select(e => e.At).ToHashSet() : new HashSet<Coord>();
        var holders = board.UnitsOf(Side.Enemy)
            .Where(h => h.Id != enemy.Id && reach.Contains(h.At) && weapons.Any(w => w.InRange(h.At.DistanceTo(moved.At))))
            .ToList();
        foreach (var holder in holders.Where(h => steps.TryGetValue(h.Id, out var to) && to != h.At))
        {
            var stepped = board.WithUnit(holder with { At = steps[holder.Id], Moved = true });
            if (EnemyAi.StrikeOn(stepped, content, enemy, moved) is { } strike)
            {
                return (stepped, strike, holder, null);
            }
        }

        foreach (var holder in holders.Where(h => !steps.TryGetValue(h.Id, out var to) || to == h.At))
        {
            var lifted = board.WithoutUnit(holder.Id);
            if (EnemyAi.StrikeOn(lifted, content, enemy, moved) is { } strike && strike.From == holder.At)
            {
                return (lifted, strike, null, holder);
            }
        }

        return null;
    }

    /// <summary>
    /// The enemies that skip the coming enemy phase stunned (issue 1244, <see cref="Stun"/>) on the
    /// board <see cref="Threats"/> reads for <paramref name="unit"/> on <paramref name="from"/>, so
    /// <c>threat</c> says why they have no line; null where <see cref="Threats"/> is.
    /// </summary>
    public static IReadOnlyList<BattleUnit>? Stunned(BattleState state, GameContent content, BattleUnit unit, Coord from) =>
        ThreatBoard(state, content, unit, from) is (var board, _, _) ? board.UnitsOf(Side.Enemy).Where(Stun.Skipping).ToList() : null;

    /// <summary>
    /// The enemies that strike <paramref name="moved"/> on a dusk map only once a side-mate lights
    /// it (issue 987): <see cref="Dusk.Knows"/> and the strike's own sight read the live board, so
    /// an enemy that does not know where the unit is, or cannot see it from where it would strike,
    /// does both once a side-mate that acted before it stands within sight of the unit. Each
    /// enemy <see cref="Unseeing"/> would list is asked again on the board with a lighter moved to
    /// one of its strike tiles within sight of the unit (an empty tile, or its own), the lighter
    /// marked moved so the tile stays its own; the first lighter and tile in line order that lets
    /// the enemy strike decide its line. A lighter is any enemy with a line in
    /// <paramref name="lines"/> or found here, so a lit enemy lights the next, or one whose own plan
    /// walks it within sight of the unit without striking it (<see cref="Walkers"/>, issue 1104). It must come before
    /// the enemy in the phase's order (<see cref="EnemyAi.Plan"/> acts in unit order), except on a
    /// <c>pincer: on</c> map, whose anvils reorder the phase. Like every line of
    /// <see cref="Threats"/> it prices what the enemy would do were it to choose the unit. Empty in
    /// daylight.
    /// </summary>
    private static List<(BattleUnit Enemy, BattleUnit Lighter, BattleState Lit, EnemyStrike Strike)> LitStrikes(BattleState board, GameContent content, BattleUnit moved, IReadOnlyList<ThreatLine> lines)
    {
        var found = new List<(BattleUnit, BattleUnit, BattleState, EnemyStrike)>();
        if (Dusk.Sight(board) is not { } sight)
        {
            return found;
        }

        var order = board.UnitsOf(Side.Enemy).Select(u => u.Id).ToList();
        var lighters = lines.Select(l => (Unit: board.Find(l.Enemy.Id) ?? l.Enemy, Tiles: l.Tiles ?? ValueList<Coord>.Of(l.From))).ToList();
        var pending = board.UnitsOf(Side.Enemy)
            .Where(e => board.EffectiveBehavior(e, content) is not null
                && EnemyAi.StrikeOn(board, content, e, moved) is null
                && EnemyAi.StrikeOn(board, content, e, moved, inDaylight: true) is not null)
            .ToList();
        if (pending.Count == 0)
        {
            return found;
        }

        lighters.AddRange(Walkers(board, content, moved, lines, sight));
        var progress = true;
        while (progress)
        {
            progress = false;
            foreach (var enemy in pending.ToList())
            {
                if (LitBy(board, content, moved, enemy, lighters, order, sight) is not var (lighter, lit, strike))
                {
                    continue;
                }

                found.Add((enemy, lighter, lit, strike));
                lighters.Add((enemy, StrikeTiles(lit, content, enemy, moved)));
                pending.Remove(enemy);
                progress = true;
            }
        }

        return found;
    }

    /// <summary>
    /// The enemies with no line on <paramref name="moved"/> whose own plan on the phase-start
    /// <paramref name="board"/> (<see cref="EnemyAi.PlanUnit"/>) ends its move within sight of it
    /// (issue 1104): a side-mate walking toward someone else still lights the unit for the enemies
    /// that act after it. Each comes with the one tile its plan ends on; an enemy whose plan does
    /// not move it is left out, since where it stands already counts for its side's sight.
    /// </summary>
    private static IEnumerable<(BattleUnit Unit, ValueList<Coord> Tiles)> Walkers(BattleState board, GameContent content, BattleUnit moved, IReadOnlyList<ThreatLine> lines, int sight)
    {
        var striking = lines.Select(l => l.Enemy.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var enemy in board.UnitsOf(Side.Enemy))
        {
            if (striking.Contains(enemy.Id) || board.EffectiveBehavior(enemy, content) is null)
            {
                continue;
            }

            if (EnemyAi.PlanUnit(board, content, enemy).OfType<Move>().LastOrDefault() is { } walk
                && walk.To != enemy.At && walk.To.DistanceTo(moved.At) <= sight)
            {
                yield return (enemy, ValueList<Coord>.Of(walk.To));
            }
        }
    }

    private static (BattleUnit Lighter, BattleState Lit, EnemyStrike Strike)? LitBy(BattleState board, GameContent content, BattleUnit moved, BattleUnit enemy,
        IReadOnlyList<(BattleUnit Unit, ValueList<Coord> Tiles)> lighters, List<string> order, int sight)
    {
        foreach (var (lighter, tiles) in lighters)
        {
            if (lighter.Id == enemy.Id || (!board.Map.PincerEnabled && order.IndexOf(lighter.Id) > order.IndexOf(enemy.Id)))
            {
                continue;
            }

            foreach (var tile in tiles)
            {
                if (tile.DistanceTo(moved.At) > sight || (board.UnitAt(tile) is { } there && there.Id != lighter.Id))
                {
                    continue;
                }

                var lit = board.WithUnit(lighter with { At = tile, Moved = true });
                if (EnemyAi.StrikeOn(lit, content, enemy, moved) is { } strike)
                {
                    return (lighter, lit, strike);
                }
            }
        }

        return null;
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
    /// <c>threat</c> prices them at 0 and says why. An enemy a side-mate would light the unit for
    /// is a line of <see cref="Threats"/> instead (<see cref="ThreatLine.LitBy"/>, issue 987), so
    /// one listed here strikes the unit in no order of the phase. Empty in daylight. Null exactly when
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

        var lit = Threats(state, content, unit, from)!.Where(l => l.LitBy is not null).Select(l => l.Enemy.Id).ToHashSet(StringComparer.Ordinal);
        return board.UnitsOf(Side.Enemy)
            .Where(e => !lit.Contains(e.Id)
                && board.EffectiveBehavior(e, content) is not null
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
        Exposure.SeatedSum(lines.Where(l => !l.Raises && l.HeldBy is null).Select(l => (l.IfAllLand, (IReadOnlyList<Coord>)(l.Tiles ?? ValueList<Coord>.Of(l.From)))).ToList());

    /// <summary>
    /// Where <see cref="IfAllLand(IReadOnlyList{ThreatLine})"/> seats each of the
    /// <paramref name="lines"/>, by index (issue 1237, round 415): the strike tile the total
    /// counts that enemy from, or null for a line the total leaves out, a raise or one the
    /// seating drops for want of a free tile. The lines and weights are the total's, so the
    /// seated lines sum to it. Each line tries its own <see cref="ThreatLine.From"/> first, and a
    /// line seated elsewhere goes back to it when that tile is free or when its holder, itself
    /// off its own tile, can take the line's seat, so a line reads moved only when the total needs the move. It is the worst
    /// case's assignment, not the planner's move, and an equal-weight seating is not unique, so it
    /// explains the number and never predicts where an enemy goes. Read-only.
    /// </summary>
    public static IReadOnlyList<Coord?> CountedFrom(IReadOnlyList<ThreatLine> lines)
    {
        var priced = Enumerable.Range(0, lines.Count).Where(i => !lines[i].Raises && lines[i].HeldBy is null).ToList();
        var tiles = priced.Select(i => (IReadOnlyList<Coord>)new[] { lines[i].From }.Concat((lines[i].Tiles ?? ValueList<Coord>.Of(lines[i].From)).Where(t => t != lines[i].From)).ToList()).ToList();
        var seats = new Dictionary<int, Coord>(Exposure.SeatedOn(priced.Select((i, k) => (lines[i].IfAllLand, tiles[k])).ToList()));
        var settled = false;
        while (!settled)
        {
            settled = true;
            foreach (var (k, seat) in seats.ToList())
            {
                var own = lines[priced[k]].From;
                if (seat == own)
                {
                    continue;
                }

                var holders = seats.Where(pair => pair.Value == own).Select(pair => pair.Key).ToList();
                if (holders.Count == 0)
                {
                    seats[k] = own;
                    settled = false;
                }
                else if (lines[priced[holders[0]]].From != own && tiles[holders[0]].Contains(seat))
                {
                    seats[k] = own;
                    seats[holders[0]] = seat;
                    settled = false;
                }
            }
        }

        var counted = new Coord?[lines.Count];
        foreach (var (k, tile) in seats)
        {
            counted[priced[k]] = tile;
        }

        return counted;
    }

    /// <summary>
    /// Every player unit that the coming enemy phase kills if every strike <c>threat</c> prices
    /// on it lands (issue 558, rounds 158 and 159): the unit's <see cref="Threats"/> from where it
    /// stands, summed as <see cref="IfAllLand(IReadOnlyList{ThreatLine}, RaisedBlow?)"/> sums them,
    /// at least its HP. When <paramref name="playerView"/> is set an enemy the player cannot see
    /// at dusk is left out unless an announced event spawns it, as <c>threat</c> leaves it out; a
    /// sleeping group counts for nothing. A unit the board's seating does not kill is still named
    /// when the strikes its own counter-kills would seat (<see cref="FreedStrikes"/>, issue 1191,
    /// round 404) bring the total to its HP, at any chance of that counter, with those strikes
    /// as <see cref="LethalThreat.Freed"/>. A strike a cover would swap onto the coverer (DESIGN.md
    /// 13.19) is not in the unit's own total. A line strike the planner would strike through the unit
    /// (<see cref="LineStrike.Through"/>, issue 1448) counts its one strike as that striker's whole share,
    /// in place of any plain strike <see cref="Threats"/> prices for the same enemy, marked
    /// <see cref="LethalStriker.Line"/>. In deployment order; empty off the player phase or
    /// when the map is over. Read-only.
    /// </summary>
    public static IReadOnlyList<LethalThreat> Lethal(BattleState state, GameContent content, bool playerView = true)
    {
        var found = new List<LethalThreat>();
        if (state.Phase != Side.Player || state.Outcome.IsOver)
        {
            return found;
        }

        foreach (var unit in state.UnitsOf(Side.Player))
        {
            if (Threats(state, content, unit, unit.At) is not { } all)
            {
                continue;
            }

            var through = LineStrike.Through(state, content, unit, unit.At) is { } strike && (!playerView || Dusk.Seen(state, strike.Striker)) ? strike : ((BattleUnit Striker, LineStrike.Choice Line)?)null;
            var lines = all.Where(l => !l.Raises && l.HeldBy is null && l.CoveredBy is null && (!playerView || l.Arrives is not null || Dusk.Seen(state, l.Enemy)) && l.Enemy.Id != through?.Striker.Id).ToList();
            var seated = Exposure.Seated(lines.Select(l => (l.IfAllLand, (IReadOnlyList<Coord>)(l.Tiles ?? ValueList<Coord>.Of(l.From)))).ToList());
            var strikers = new List<LethalStriker>();
            if (RaisedBlowOn(state, content, unit, unit.At) is { } blow)
            {
                strikers.Add(new LethalStriker(blow.Wielder, blow.Damage));
            }

            strikers.AddRange(seated.Select(i => new LethalStriker(lines[i].Enemy, lines[i].IfAllLand)).Where(s => s.Damage > 0));
            if (through is { } line)
            {
                var damage = LineStrike.Forecast(state.WithUnit(line.Striker), content, line.Striker, unit).Attacker.Damage;
                if (damage > 0)
                {
                    strikers.Add(new LethalStriker(line.Striker, damage) { Line = true });
                }
            }

            var total = strikers.Sum(s => s.Damage);
            if (strikers.Count > 0 && total >= unit.Hp)
            {
                found.Add(new LethalThreat(unit, total, ValueList<LethalStriker>.From(strikers)));
                continue;
            }

            var freed = FreedStrikes(lines, unit);
            if (strikers.Count > 0 && freed.Count > 0 && total + freed.Sum(f => f.Damage) >= unit.Hp)
            {
                found.Add(new LethalThreat(unit, total + freed.Sum(f => f.Damage), ValueList<LethalStriker>.From(strikers)) { Freed = ValueList<FreedStrike>.From(freed) });
            }
        }

        return found;
    }

    /// <summary>
    /// The strikes a tile freed by the unit's own counter would let in, one wave deep (issue 1191,
    /// round 404): the lines are seated as <see cref="IfAllLand(IReadOnlyList{ThreatLine})"/> seats
    /// them, a seated line whose counter is lethal if every counter strike lands
    /// (<see cref="CombatForecast.CounterIsLethal"/>) frees its tile, and the lines the seating
    /// dropped are seated a second time onto the freed tiles alone, heaviest first. A strike a
    /// cover would swap frees nothing, a line that only raises a blow is left out, and a strike
    /// seated on a freed tile frees nothing further. In line order of the dropped strikes; empty
    /// when no counter frees a tile a dropped line could take. Read-only.
    /// </summary>
    public static IReadOnlyList<FreedStrike> FreedStrikes(IReadOnlyList<ThreatLine> lines, BattleUnit unit)
    {
        var priced = lines.Where(l => !l.Raises && l.HeldBy is null).ToList();
        var seats = Exposure.SeatedOn(priced.Select(l => (l.IfAllLand, (IReadOnlyList<Coord>)(l.Tiles ?? ValueList<Coord>.Of(l.From)))).ToList());
        var freedBy = new Dictionary<Coord, ThreatLine>();
        foreach (var (index, tile) in seats.OrderBy(pair => pair.Key))
        {
            var line = priced[index];
            if (line.CoveredBy is null && line.Forecast.CounterIsLethal(line.Enemy.Hp, unit.Hp))
            {
                freedBy[tile] = line;
            }
        }

        if (freedBy.Count == 0)
        {
            return Array.Empty<FreedStrike>();
        }

        var dropped = Enumerable.Range(0, priced.Count).Where(i => !seats.ContainsKey(i)).Select(i => priced[i]).ToList();
        var second = Exposure.SeatedOn(dropped.Select(l => (l.IfAllLand, (IReadOnlyList<Coord>)(l.Tiles ?? ValueList<Coord>.Of(l.From)).Where(freedBy.ContainsKey).ToList())).ToList());
        return second.OrderBy(pair => pair.Key)
            .Where(pair => dropped[pair.Key].IfAllLand > 0)
            .Select(pair => new FreedStrike(freedBy[pair.Value].Enemy, freedBy[pair.Value].Forecast, dropped[pair.Key].Enemy, pair.Value, dropped[pair.Key].IfAllLand))
            .ToList();
    }

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
    /// can play in either order; a <c>goes_home:</c> member off its post, its home tile alone
    /// (<see cref="EnemyAi.HomeTile"/>, issue 1372). In row-major order.
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

        if (EnemyAi.HomeTile(board, enemy, board.ReachOf(enemy, content)) is { } home)
        {
            return Strikes(home) ? ValueList<Coord>.Of(home) : ValueList<Coord>.Empty;
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
    /// awake (issue 248, the thirty-sixth round's shape one): each group with the members that
    /// could strike the tile were it awake, never one that could not (issue 454), in group order, and no numbers, so the player learns a sleeping group is a
    /// question without being handed its answer. A sleeping member already priced by <see cref="Threats"/>,
    /// as a Hold strike from its own tile, is left out, so a strike is never both priced and only a
    /// question (issue 1351); a group with no member left is not named. A group the tile itself certainly wakes is
    /// already awake on that board and priced by <see cref="Threats"/> instead. Null exactly when <see cref="Threats"/> is. Read-only.
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

        var priced = (Threats(state, content, unit, from) ?? Array.Empty<ThreatLine>()).Select(l => l.Enemy.Id).ToHashSet(StringComparer.Ordinal);
        var sleeping = board.UnitsOf(Side.Enemy)
            .Where(u => u is { Behavior: Behavior.Guard, Group: not null } && !board.IsAwake(u.Group))
            .Select(u => u.Group!)
            .Distinct()
            .OrderBy(g => g, StringComparer.Ordinal);
        foreach (var group in sleeping)
        {
            var woken = board.Wake(group);
            var members = ValueList<BattleUnit>.From(woken.UnitsOf(Side.Enemy).Where(u => u.Group == group && !priced.Contains(u.Id) && EnemyAi.StrikeOn(woken, content, u, moved) is not null));
            if (members.Count == 0)
            {
                continue;
            }

            groups.Add(new SleepingThreat(group, members));
        }

        return groups;
    }

    /// <summary>
    /// The planner's anvil plans against <paramref name="unit"/> on <paramref name="from"/> (DESIGN.md
    /// 13.13, issue 457), on a <c>pincer: on</c> map only: on <see cref="Threats"/>' board, the
    /// enemies in ascending id each asked for <see cref="EnemyAi.Anvil"/> as the phase's
    /// planner asks, with a claimed set that grows by each plan's follower and anvil so neither is
    /// used twice, and the plans whose pinned unit is this one kept, in that order. Each is a
    /// forecast of intent, never binding and never priced: nothing here enters
    /// <see cref="IfAllLand(IReadOnlyList{ThreatLine})"/>. Like every line of <see cref="Threats"/>
    /// it reads the phase-start board, so a plan that exists only after another enemy's move is
    /// not listed. Empty on any other map. Null exactly when <see cref="Threats"/> is. Read-only.
    /// </summary>
    public static IReadOnlyList<AnvilLine>? Anvils(BattleState state, GameContent content, BattleUnit unit, Coord from)
    {
        if (ThreatBoard(state, content, unit, from) is not (var board, var moved, _))
        {
            return null;
        }

        var lines = new List<AnvilLine>();
        if (moved is null || !board.Map.PincerEnabled)
        {
            return lines;
        }

        var claimed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var enemy in board.UnitsOf(Side.Enemy).OrderBy(u => u.Id, StringComparer.Ordinal))
        {
            if (claimed.Contains(enemy.Id) || EnemyAi.Anvil(board, content, enemy, claimed) is not { } plan)
            {
                continue;
            }

            claimed.Add(plan.FollowerId);
            claimed.Add(enemy.Id);
            if (plan.PinnedId == moved.Id)
            {
                var far = new Coord(2 * moved.At.X - plan.Tile.X, 2 * moved.At.Y - plan.Tile.Y);
                lines.Add(new AnvilLine(enemy, plan.Tile, board.Find(plan.FollowerId)!, far));
            }
        }

        return lines;
    }

    /// <summary>
    /// The bosses under the veto that could strike <paramref name="unit"/> on <paramref name="from"/>
    /// but refuse every tile they would strike it from (issue 565, DESIGN.md section 8), in unit
    /// order, each with <see cref="EnemyAi.Refusal"/>'s nearest refused tile and the tile its plan
    /// ends on. Read on <see cref="Threats"/>' board, so a boss the tile reads clear of for this
    /// reason is named, and a boss that swings at the unit anyway is a line of <see cref="Threats"/>
    /// instead. A sleeping boss and one that cannot reach are not listed. Unpriced: nothing here
    /// enters <see cref="IfAllLand(IReadOnlyList{ThreatLine})"/>. Null exactly when
    /// <see cref="Threats"/> is. Read-only.
    /// </summary>
    public static IReadOnlyList<RefusalLine>? Refusals(BattleState state, GameContent content, BattleUnit unit, Coord from)
    {
        if (ThreatBoard(state, content, unit, from) is not (var board, var moved, _))
        {
            return null;
        }

        var lines = new List<RefusalLine>();
        if (moved is null)
        {
            return lines;
        }

        foreach (var enemy in board.UnitsOf(Side.Enemy))
        {
            if (board.EffectiveBehavior(enemy, content) is not null && EnemyAi.Refusal(board, content, enemy, moved) is { } refusal)
            {
                lines.Add(new RefusalLine(enemy, refusal.Refused, refusal.Ends));
            }
        }

        return lines;
    }

    /// <summary>
    /// The sleeping groups <paramref name="unit"/> would wake by stopping on <paramref name="from"/>
    /// (issue 458): <see cref="WakeCheck.Run"/> on the board as it stands and the same board with
    /// the unit moved there, no combat and no death, so the causes are proximity and the
    /// <c>wake_links:</c> calls it sets off, in the check's own order. The unit's own tile wakes
    /// nothing new, since the check already ran on where it stands. Unpriced: who could then strike
    /// is <see cref="Threats"/>' and <see cref="SleepingThreats"/>' business. Null when the unit
    /// cannot stand on the tile this phase, or when the state is not a player phase. Read-only.
    /// </summary>
    public static IReadOnlyList<GroupWoke>? StopWakes(BattleState state, GameContent content, BattleUnit unit, Coord from)
    {
        var standable = CanStandOn(state, content, unit, from) || state.MoveAgainReachOf(unit, content)?.CanEnd(from) == true;
        if (state.Phase != Side.Player || unit.Side != Side.Player || !standable)
        {
            return null;
        }

        return WakeCheck.Run(state, state.WithUnit(unit with { At = from }), content, Array.Empty<Noise>(), Array.Empty<string>());
    }

    /// <summary>
    /// The sleeping groups the noise of <paramref name="unit"/> fighting <paramref name="target"/> from
    /// <paramref name="from"/> would wake (issue 1106): <see cref="WakeCheck.Run"/> on the board with the
    /// unit moved there, once with the fight's two tiles as noise at <see cref="Signatures.NoiseRadius"/>
    /// (the resolver's own tiles and radius) and once without, keeping the groups only the noisy run
    /// wakes, so what <see cref="StopWakes"/> already names for the stop is not repeated. Each noise
    /// wake names the fight's tiles that reach a living member at the radius the wind bends on that
    /// board; a <c>wake_links:</c> call names its caller and no tile. No death is counted. Empty in an
    /// enemy phase and for an enemy unit. Read-only.
    /// </summary>
    public static IReadOnlyList<FightWake> FightWakes(BattleState state, GameContent content, BattleUnit unit, BattleUnit target, Coord from)
    {
        if (state.Phase != Side.Player || unit.Side != Side.Player)
        {
            return Array.Empty<FightWake>();
        }

        var board = state.WithUnit(unit with { At = from });
        var radius = Signatures.NoiseRadius(state, content, unit, target);
        var noisy = new[] { new Noise(from, radius), new Noise(target.At, radius) };
        var quiet = WakeCheck.Run(state, board, content, Array.Empty<Noise>(), Array.Empty<string>()).Select(w => w.Group).ToHashSet(StringComparer.Ordinal);
        return WakeCheck.Run(state, board, content, noisy, Array.Empty<string>())
            .Where(w => !quiet.Contains(w.Group))
            .Select(w => new FightWake(w.Group, w.CalledBy, w.Cause == WakeCause.Noise ? ValueList<Coord>.From(WakeCheck.HeardFrom(board, w.Group, noisy)) : ValueList<Coord>.Empty))
            .ToList();
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
        var standable = CanStandOn(state, content, unit, from) || state.MoveAgainReachOf(unit, content)?.CanEnd(from) == true;
        if (state.Phase != Side.Player || unit.Side != Side.Player || !standable || state.Outcome.IsOver)
        {
            return false;
        }

        return state.WithUnit(unit with { At = from }).Outcome.Result == BattleResult.Won;
    }

    /// <summary>
    /// The board <see cref="Threats"/> and <see cref="SleepingThreats"/> read: the exposure
    /// sum's, <see cref="Exposure.Board"/> (the unit on the tile, every group its standing
    /// there certainly wakes awake), with the enter events a stop on that tile fires applied
    /// as the move would apply them (issue 1258: a bar's wall closes its lane in the read), a held
    /// bar its holder leaves given back as the resolver would after the move (issue 1259), an
    /// unannounced spawn among them taken off again, and the player phase then ended through the resolver,
    /// so the phase-start healing and events the enemy phase would see are applied. An enemy
    /// an unannounced event spawned in that phase start is taken off the board again, and
    /// an announced one is kept and named in <c>Arrivals</c> with its tile. <c>Moved</c> is
    /// null when ending the phase ends the battle or removes the unit. Null when the unit
    /// cannot stand on the tile this phase, or when the state is not a player phase.
    /// </summary>
    private static (BattleState Board, BattleUnit? Moved, IReadOnlyDictionary<string, Coord> Arrivals)? ThreatBoard(BattleState state, GameContent content, BattleUnit unit, Coord from)
    {
        var standable = CanStandOn(state, content, unit, from) || state.MoveAgainReachOf(unit, content)?.CanEnd(from) == true;
        var dashed = !standable && Winded.CanDashTo(state, content, unit, from);
        if (state.Phase != Side.Player || unit.Side != Side.Player || !(standable || dashed))
        {
            return null;
        }

        var asked = dashed ? unit with { Winded = true } : unit;
        var arrivals = new Dictionary<string, Coord>(StringComparer.Ordinal);
        var stood = Exposure.Board(state.WithUnit(asked), content, asked, from);
        if (from != unit.At && stood.Find(unit.Id) is { } stopped)
        {
            var entered = new List<GameEvent>();
            stood = MapEvents.AfterMove(stood, content, stopped, entered);
            foreach (var spawned in entered.OfType<UnitSpawned>().Where(_ => !state.Map.Announced))
            {
                stood = stood.WithoutUnit(spawned.UnitId);
            }
        }

        stood = HeldBars.After(stood, new List<GameEvent>());

        var ended = Resolver.Apply(stood, content, new EndPhase());
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

/// <summary>A sleeping group a fight would wake (issue 1106): by noise heard from <paramref name="HeardFrom"/>, or called by <paramref name="CalledBy"/> through <c>wake_links:</c>.</summary>
public sealed record FightWake(string Group, string? CalledBy, ValueList<Coord> HeardFrom);

/// <summary>
/// One anvil plan <see cref="Queries.Anvils"/> lists (issue 457): <paramref name="Anvil"/> could
/// end on <paramref name="Tile"/> beside the unit so <paramref name="Follower"/> strikes it pinned
/// from <paramref name="From"/>, the tile across the unit. Unpriced and not binding.
/// </summary>
public sealed record AnvilLine(BattleUnit Anvil, Coord Tile, BattleUnit Follower, Coord From);

/// <summary>
/// One refusal <see cref="Queries.Refusals"/> lists (issue 565): <paramref name="Boss"/> could strike
/// the unit from <paramref name="Refused"/> but the veto refuses it there, and it ends on
/// <paramref name="Ends"/> instead. Unpriced and read from the planner's own call.
/// </summary>
public sealed record RefusalLine(BattleUnit Boss, Coord Refused, Coord Ends);

/// <summary>A Guard group <see cref="Queries.SleepingThreats"/> names: asleep, and <see cref="Members"/> the ones able to strike the unit were it awake.</summary>
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
    /// phase (issue 444), and for a strike a Lightning Rod catches (<see cref="CombatForecast.CaughtBy"/>,
    /// issue 1280), which lands on the holder.
    /// </summary>
    public int IfAllLand => Raises || Forecast.CaughtBy is not null ? 0 : Forecast.AttackerDamageLivedFor(Enemy.Hp);

    /// <summary>
    /// The coverer this strike lands on instead of the unit (DESIGN.md 13.19), standing on the
    /// unit's tile, when a cover would swap them; <see cref="Forecast"/> is then against it. Null otherwise.
    /// </summary>
    public BattleUnit? CoveredBy { get; init; }

    /// <summary>
    /// On a dusk map (DESIGN.md 13.7), the side-mate that lights the unit for this strike (issue
    /// 987): the enemy neither knows where the unit is nor could see it on the phase-start board,
    /// and strikes once that side-mate, acting before it, stands within sight of the unit.
    /// <see cref="Forecast"/> is read with the side-mate on its strike tile. Null otherwise.
    /// </summary>
    public BattleUnit? LitBy { get; init; }

    /// <summary>
    /// The side-mate standing on <see cref="From"/> at phase start that strikes from another tile
    /// of its own, so it steps off before this enemy takes the tile (issue 1256); the line is
    /// counted, one wave deep, and <see cref="Forecast"/> is read with the side-mate moved. Null otherwise.
    /// </summary>
    public BattleUnit? FreedBy { get; init; }

    /// <summary>
    /// The side-mate standing on <see cref="From"/> at phase start that does not step off it
    /// (issue 1256): the enemy has no other tile to strike from, so the line is printed and left
    /// out of every total, and <see cref="Forecast"/> is read as if the tile were free. Null otherwise.
    /// </summary>
    public BattleUnit? HeldBy { get; init; }

    /// <summary>
    /// The sleeping groups this strike's noise would wake (issue 1290), each naming the fight's tiles
    /// that reach it, or its <c>wake_links:</c> caller. Unpriced: a woken group's strikes are not in any
    /// total (DECISIONS/0281). Empty when the strike wakes nothing.
    /// </summary>
    public ValueList<FightWake> Wakes { get; init; } = ValueList<FightWake>.Empty;

    /// <summary>
    /// The cast the enemy may make in place of this strike (issue 1286, DECISIONS/0316 amended): a raiser or an
    /// earth-shaper casts in place of any strike that is not a kill. The line stays counted, since whether the cast is
    /// taken depends on the bodies and allies the phase leaves. Null when the strike kills or the enemy casts nothing.
    /// </summary>
    public CastKind? Casts { get; init; }
}

/// <summary>A cast an enemy makes on the board in place of a strike that is not a kill (issue 1286): a raise dead, or a Rampart under an ally.</summary>
public enum CastKind
{
    /// <summary>Raises a fallen unit of its side as a Hollow (<see cref="EnemyAi.Raise"/>).</summary>
    Raise,

    /// <summary>Lays the earth rider's ground under an ally (<see cref="EnemyAi.Rampart"/>).</summary>
    Rampart,
}

/// <summary>
/// A blow already raised over <paramref name="Over"/> by <paramref name="Wielder"/> (DESIGN.md
/// 13.16), as <see cref="Queries.RaisedBlowOn"/> reads it: <paramref name="Damage"/> lands for
/// certain at the wielder's next phase start on the unit standing there, unless a hit from within the wielder's reach breaks it.
/// </summary>
public sealed record RaisedBlow(BattleUnit Wielder, Coord Over, int Damage);

/// <summary>
/// A player unit <see cref="Queries.Lethal"/> names (issue 558): <paramref name="Total"/> is the
/// coming enemy phase's damage on it if every strike lands, at least its HP, and
/// <paramref name="Strikers"/> the enemies that total sums, in <c>threat</c>'s order.
/// </summary>
public sealed record LethalThreat(BattleUnit Unit, int Total, ValueList<LethalStriker> Strikers)
{
    /// <summary>
    /// The strikes a tile freed by the unit's own counter-kill lets in (<see cref="Queries.FreedStrikes"/>,
    /// issue 1191), set only when <see cref="Strikers"/> alone fall short of the unit's HP; <see cref="Total"/> then counts them.
    /// </summary>
    public ValueList<FreedStrike> Freed { get; init; } = ValueList<FreedStrike>.Empty;
}

/// <summary>
/// One strike <see cref="Queries.FreedStrikes"/> seats (issue 1191): <paramref name="Follower"/> takes
/// <paramref name="Tile"/> if the unit's counter kills <paramref name="Freer"/>, whose strike
/// <paramref name="Counter"/> forecasts (its <see cref="CombatForecast.Defender"/> is the unit's
/// counter), and deals <paramref name="Damage"/> if every strike lands.
/// </summary>
public sealed record FreedStrike(BattleUnit Freer, CombatForecast Counter, BattleUnit Follower, Coord Tile, int Damage);

/// <summary>One enemy in a <see cref="LethalThreat"/>'s total, and the damage it deals if every strike lands.</summary>
public sealed record LethalStriker(BattleUnit Enemy, int Damage)
{
    /// <summary>Whether <see cref="Damage"/> is the enemy's line strike through the unit (<see cref="LineStrike.Through"/>, issue 1448) rather than a plain strike.</summary>
    public bool Line { get; init; }
}

/// <summary>
/// One row of the attack menu (issue 611, <see cref="Queries.AttackOptions"/>): the attack it
/// submits, the id of the weapon it strikes with (null only for an art the unit has no weapon
/// for), the art it declares or null for the plain attack, and either the forecast or the
/// resolver's refusal, never both.
/// </summary>
public sealed record AttackOption(Attack Command, string? WeaponId, Ability? Art, CombatForecast? Forecast, Rejection? Refusal)
{
    /// <summary>True when the resolver would accept the row's attack.</summary>
    public bool Legal => Refusal is null;
}

/// <summary>
/// A move's preview (issue 782): the walk as the resolver would record it, and the tiles it would
/// wear, each with the terrain it would become.
/// </summary>
public sealed record WalkPreview(UnitMoved Walk, ValueList<(Coord At, string TerrainId)> Worn);
