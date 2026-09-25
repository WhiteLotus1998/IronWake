namespace Ironwake.Core;

/// <summary>
/// The questions a renderer asks the core instead of counting for itself (DESIGN.md
/// section 2; the presentation protocol of issue 25 exposes these by name): where a unit
/// can move, whom it can attack from where it stands, and what a combat would look like.
/// The CLI computes none of this.
/// </summary>
public static class Queries
{
    /// <summary>Where the unit may move, section 4, on the board as it stands.</summary>
    public static Reach Reachable(BattleState state, GameContent content, BattleUnit unit) => state.ReachOf(unit, content);

    /// <summary>The enemy units the unit's equipped weapon reaches from where it stands, in id order. Empty when it has no weapon.</summary>
    public static IEnumerable<BattleUnit> Targets(BattleState state, GameContent content, BattleUnit unit)
    {
        var weapon = unit.EquippedWeapon(content);
        if (weapon is null)
        {
            yield break;
        }

        foreach (var other in state.UnitsOf(unit.Side == Side.Player ? Side.Enemy : Side.Player))
        {
            if (weapon.InRange(unit.At.DistanceTo(other.At)))
            {
                yield return other;
            }
        }
    }

    /// <summary>
    /// The section 5 forecast of the unit attacking the target from where it stands with
    /// its equipped weapon, or with the weapon in <paramref name="slot"/>; null when it
    /// cannot (no weapon, a slot that holds no usable weapon, the target out of range or
    /// on its own side), the same combatants the resolver would build.
    /// </summary>
    public static CombatForecast? Forecast(BattleState state, GameContent content, BattleUnit unit, BattleUnit target, int? slot = null) =>
        Forecast(state, content, unit, target, unit.At, slot);

    /// <summary>
    /// The forecast of the unit attacking the target from <paramref name="from"/>, a tile it
    /// could move to this phase, on that tile's terrain and at that tile's distance (issue
    /// 151): the number the player needs while choosing where to stand, before the move is
    /// made and final. Null when the tile is not one the unit can end a move on now (out of
    /// reach, an occupied tile, or any tile but its own once it has moved this phase), or
    /// for the reasons the standing forecast is null. From the unit's own tile it is the
    /// standing forecast. Read-only: nothing moves.
    /// </summary>
    public static CombatForecast? Forecast(BattleState state, GameContent content, BattleUnit unit, BattleUnit target, Coord from, int? slot = null)
    {
        if (!CanStandOn(state, content, unit, from))
        {
            return null;
        }

        var (armed, weapon, rejection) = Resolver.ChooseWeapon(unit, content, slot);
        var distance = from.DistanceTo(target.At);
        if (rejection is not null || !weapon!.InRange(distance) || target.Side == unit.Side)
        {
            return null;
        }

        return Combat.Forecast((armed with { At = from }).ToCombatant(state, content), target.ToCombatant(state, content, countering: true), distance, state.Scheme);
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
    /// forecast the enemy phase prints before that strike. The board is the exposure sum's,
    /// <see cref="Exposure.Board"/> (the unit on the tile, every group its standing there
    /// certainly wakes awake), with the player phase then ended through the resolver, so
    /// the phase-start healing and events the enemy phase would see are applied. A group
    /// still asleep is not listed and a unit that holds strikes only from its own tile.
    /// Each line reads that phase-start board; an earlier enemy's move or kill in the phase
    /// is not played out. Null when the unit cannot stand on the tile this phase, or when
    /// the state is not a player phase. Read-only.
    /// </summary>
    public static IReadOnlyList<ThreatLine>? Threats(BattleState state, GameContent content, BattleUnit unit, Coord from)
    {
        if (state.Phase != Side.Player || unit.Side != Side.Player || !CanStandOn(state, content, unit, from))
        {
            return null;
        }

        var lines = new List<ThreatLine>();
        var ended = Resolver.Apply(Exposure.Board(state, content, unit, from), content, new EndPhase());
        if (!ended.Accepted || ended.Next.Outcome.IsOver || ended.Next.Find(unit.Id) is not { } moved)
        {
            return lines;
        }

        var board = ended.Next;
        foreach (var enemy in board.UnitsOf(Side.Enemy))
        {
            if (board.EffectiveBehavior(enemy, content) is null || EnemyAi.StrikeOn(board, content, enemy, moved) is not { } strike)
            {
                continue;
            }

            var forecast = Forecast(board, content, enemy, moved, strike.From, strike.Slot)
                ?? throw new InvalidOperationException($"the planner's strike of {enemy.Id} on {unit.Id} from {strike.From} has no forecast");
            lines.Add(new ThreatLine(enemy, strike.From, strike.Slot, enemy.UsableWeaponAt(content, strike.Slot)!, forecast));
        }

        return lines;
    }
}

/// <summary>One enemy's strike on a unit as <see cref="Queries.Threats"/> prices it: who, from where, with which slot's weapon, and the forecast of that combat.</summary>
public sealed record ThreatLine(BattleUnit Enemy, Coord From, int Slot, Weapon Weapon, CombatForecast Forecast)
{
    /// <summary>The damage the strike deals if every hit lands, doubles included, no crit.</summary>
    public int IfAllLand => Forecast.Attacker.Strikes ? Forecast.Attacker.Damage * (Forecast.Attacker.Doubles ? 2 : 1) : 0;
}
