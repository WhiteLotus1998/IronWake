namespace Ironwake.Core;

/// <summary>
/// An area cast (issue 1329, Lotus's round-3 spell rulings, DECISIONS/0322: Spark Storm). A tome that names <c>area</c>
/// (<see cref="Weapon.Area"/>, the radius) is cast through the Item action at a unit or a tile in its range,
/// <c>item &lt;unit&gt; &lt;slot&gt; &lt;unit|x,y&gt;</c>, checked as a strike's tome is (the caster may wield it, a use left, no art):
/// <list type="bullet">
/// <item>Every enemy of the caster within the radius of that tile is struck once, in board order: one forecast, one hit
/// roll keyed by caster and target, no double, and no counter. The caster's side is never struck.</item>
/// <item>It is refused when no enemy is in the area, when the tile is out of range, and when the caster's side cannot
/// see the tile at dusk. No cover swaps.</item>
/// <item>A Lightning Rod catches the whole storm (issue 1400, Lotus, DECISIONS/0351; <see cref="Catcher"/>): one strike on the
/// holder alone, x0.5, and no one else in the area is struck; a holder standing after it is charged. A catch marks no one.</item>
/// <item>One use and the action are spent. The caster earns one combat's EXP, from the struck unit that pays most (a kill
/// pays its kill), and one combat's rank and mastery: an area is not an EXP farm.</item>
/// <item>A marking tome marks every unit it hits that survives, cashing any mark already on it first (<see cref="Mark"/>).</item>
/// </list>
/// An area tome is never equipped (<see cref="BattleUnit.UsableWeaponAt"/>), so it never attacks and never counters.
/// Spark Storm (Gust's id, DECISIONS/0348) is the one shipped area tome. <see cref="Resolver.Legal"/> offers each cast by
/// the set it strikes, and the Sim's player and the enemy planner price one with <see cref="Best"/> (issue 1391).
/// </summary>
public static class AreaCast
{
    /// <summary>The tile a cast names: a living unit's, or <c>x,y</c> on the map; null when it names neither.</summary>
    public static Coord? TileOf(BattleState state, string target) =>
        state.Find(target) is { } unit ? unit.At
        : Sunder.TileOf(target) is { } tile && state.Map.Contains(tile) ? tile
        : null;

    /// <summary>Every enemy of <paramref name="caster"/> within <paramref name="spell"/>'s radius of <paramref name="at"/>, board order.</summary>
    public static IReadOnlyList<BattleUnit> Struck(BattleState state, BattleUnit caster, Weapon spell, Coord at) =>
        state.Units.Where(u => u.Side != caster.Side && u.At.DistanceTo(at) <= spell.Area).ToList();

    /// <summary>
    /// The Lightning Rod holder that catches <paramref name="caster"/>'s cast of <paramref name="spell"/> at <paramref name="at"/>
    /// (issue 1400, Lotus's ruling, DECISIONS/0351), with the struck unit of its side that draws the catch; null when no rod
    /// catches it and the storm lands as cast. A holder catches when it is not stunned, holds a rod of the tome's school, the
    /// tome reaches it from the caster's tile, and the area takes in a unit of its side, itself aside, within the rod's
    /// radius of it; the holder may stand in the area or beside it. Of two, the nearer to the aimed tile, then unit order.
    /// The drawn unit is the nearest of the holder's side in the area to the aimed tile, then board order.
    /// </summary>
    public static (BattleUnit Holder, BattleUnit Drawn)? Catcher(BattleState state, GameContent content, BattleUnit caster, Weapon spell, Coord at)
    {
        if (spell.School is not { } school)
        {
            return null;
        }

        var struck = Struck(state, caster, spell, at);
        foreach (var holder in state.Units
            .Where(u => u.Side != caster.Side && u.Stun == 0 && spell.InRange(caster.At.DistanceTo(u.At)))
            .OrderBy(u => u.At.DistanceTo(at)))
        {
            if (AbilityRules.Rod(content.AbilitiesOf(holder.Unit), school) is not { } rod)
            {
                continue;
            }

            var drawn = struck
                .Where(s => s.Id != holder.Id && s.Side == holder.Side && s.At.DistanceTo(holder.At) <= rod.Radius)
                .OrderBy(s => s.At.DistanceTo(at))
                .FirstOrDefault();
            if (drawn is not null)
            {
                return (holder, drawn);
            }
        }

        return null;
    }

    /// <summary>
    /// Why <paramref name="caster"/> cannot cast <paramref name="spell"/> at <paramref name="at"/>: the tile out of range, or
    /// unseen by the caster's side at dusk; null when it can be cast there. The cast and its preview both ask it.
    /// </summary>
    public static Rejection? Unreachable(BattleState state, BattleUnit caster, Weapon spell, Coord at)
    {
        var distance = caster.At.DistanceTo(at);
        if (!spell.InRange(distance))
        {
            return new Rejection(
                RejectionReason.OutOfRange,
                $"{at} is {distance} tiles from {caster.Id} at {caster.At}; {spell.Name} reaches {spell.MinRange}-{spell.MaxRange}");
        }

        return Dusk.Sees(state, caster.Side, at)
            ? null
            : new Rejection(RejectionReason.Unseen, $"no unit on {caster.Id}'s side can see {at} at dusk (sight {Dusk.Sight(state)})");
    }

    /// <summary>
    /// The forecast of <paramref name="caster"/>'s cast at <paramref name="at"/> on <paramref name="target"/>: one strike, no
    /// counter, read at the cast's distance (the tile's, in range), since a unit at the area's edge may stand beyond it.
    /// </summary>
    public static CombatForecast Forecast(BattleState state, GameContent content, BattleUnit caster, Weapon spell, BattleUnit target, Coord at) =>
        Combat.Forecast(
            caster.ToCombatant(state, content, against: target, casting: spell),
            target.ToCombatant(state, content, countering: true, against: caster) with { Blind = true },
            caster.At.DistanceTo(at),
            state.Scheme);

    /// <summary>
    /// The forecast of a caught cast's one strike on <paramref name="holder"/> (issue 1400, <see cref="Catcher"/>): no counter,
    /// read at the caster's distance to the holder, the rod's x0.5 applied.
    /// </summary>
    public static CombatForecast CaughtForecast(BattleState state, GameContent content, BattleUnit caster, Weapon spell, BattleUnit holder) =>
        Combat.Forecast(
            caster.ToCombatant(state, content, against: holder, casting: spell),
            holder.ToCombatant(state, content, countering: true, against: caster) with { Blind = true, Catching = true },
            caster.At.DistanceTo(holder.At),
            state.Scheme);

    /// <summary>
    /// The preview's line for the cast: <c>Test Storm at 6,5 strikes brigand-1 acc 80% dmg 3 (hp 18), marks; ...</c>,
    /// or what refuses it: the tile out of range or unseen (<see cref="Unreachable"/>), or no enemy in the area.
    /// </summary>
    public static string Preview(BattleState state, GameContent content, BattleUnit caster, Weapon spell, Coord at)
    {
        if (Unreachable(state, caster, spell, at) is { } refusal)
        {
            return refusal.Message;
        }

        var struck = Struck(state, caster, spell, at);
        if (struck.Count == 0)
        {
            return $"{spell.Name} at {at} would strike no one: no enemy within {spell.Area} of it";
        }

        if (Catcher(state, content, caster, spell, at) is ({ } holder, _))
        {
            var caught = CaughtForecast(state, content, caster, spell, holder).Attacker;
            var dealt = caught.FirstHit(crit: false);
            return $"{spell.Name} at {at}{LightningRod.ForecastText(content, holder, holder.Id, spell.School!.Value)}: {holder.Id} acc {caught.DisplayedHit}% dmg {dealt} (hp {holder.Hp}){LightningRod.ForecastText(caught)}{Mark.ForecastText(caught)}{Armor.ForecastText(caught)}, no counter";
        }

        return $"{spell.Name} at {at} strikes " + string.Join("; ", struck.Select(target =>
        {
            var side = Forecast(state, content, caster, spell, target, at).Attacker;
            var damage = side.FirstHit(crit: false);
            return $"{target.Id} acc {side.DisplayedHit}% dmg {damage} (hp {target.Hp}){Mark.ForecastText(side)}{Armor.ForecastText(side)}{(spell.Marks ? ", marks" : "")}";
        })) + ", no counter";
    }

    /// <summary>A cast a planner may make: the tile cast from, the tome's slot, the tile cast at, the units struck, and its score.</summary>
    public sealed record Choice(Coord From, int Slot, Coord At, IReadOnlyList<BattleUnit> Struck, double Score);

    /// <summary>
    /// The area cast <paramref name="unit"/> makes from one of <paramref name="tiles"/>, or null (issue 1391). Each tome it
    /// can cast (<see cref="Tomes"/>) is tried at every tile in range of every tile it may stand on, and priced on
    /// <see cref="EnemyAi.Score"/>'s scale (<see cref="Price"/>). A cast qualifies only when it strikes two or more of
    /// <paramref name="known"/> or its forecast kills one, so a single sting never stands in for an attack. The best
    /// score wins, then the first tile in <paramref name="tiles"/>' order, then the first tile cast at in row order. A
    /// tile <paramref name="refused"/> names is no option, which is how a veto applies; a tile another unit stands on is
    /// never one but the unit's own.
    /// </summary>
    public static Choice? Best(
        BattleState state,
        GameContent content,
        BattleUnit unit,
        IEnumerable<Coord> tiles,
        IReadOnlyCollection<BattleUnit> known,
        Func<Coord, bool>? refused = null)
    {
        var tomes = Tomes(content, unit);
        if (tomes.Count == 0)
        {
            return null;
        }

        var knownIds = known.Select(k => k.Id).ToHashSet();
        Choice? best = null;
        foreach (var tile in tiles)
        {
            if ((tile != unit.At && state.UnitAt(tile) is not null) || refused?.Invoke(tile) == true)
            {
                continue;
            }

            var there = unit with { At = tile };
            var board = state.WithUnit(there);
            foreach (var (slot, spell) in tomes)
            {
                for (var y = tile.Y - spell.MaxRange; y <= tile.Y + spell.MaxRange; y++)
                {
                    for (var x = tile.X - spell.MaxRange; x <= tile.X + spell.MaxRange; x++)
                    {
                        var at = new Coord(x, y);
                        if (!board.Map.Contains(at) || Unreachable(board, there, spell, at) is not null)
                        {
                            continue;
                        }

                        var struck = Struck(board, there, spell, at).Where(u => knownIds.Contains(u.Id)).ToList();
                        if (struck.Count == 0)
                        {
                            continue;
                        }

                        var catcher = Catcher(board, content, there, spell, at)?.Holder;
                        if (catcher is not null)
                        {
                            struck = [catcher];
                        }

                        var (score, kills) = Price(board, content, there, spell, at, struck, catcher);
                        if ((struck.Count >= 2 || kills) && (best is null || score > best.Score))
                        {
                            best = new Choice(tile, slot, at, struck, score);
                        }
                    }
                }
            }
        }

        return best;
    }

    /// <summary>
    /// The area tomes <paramref name="unit"/> can cast now, by slot: those naming <c>area</c> that it may wield with a use
    /// left. <see cref="Resolver.Legal"/> and <see cref="Best"/> both read it, so neither offers a cast the Item action refuses.
    /// </summary>
    public static IReadOnlyList<(int Slot, Weapon Spell)> Tomes(GameContent content, BattleUnit unit)
    {
        var unitClass = content.Class(unit.Unit.ClassId);
        var tomes = new List<(int, Weapon)>();
        for (var slot = 0; slot < unit.Unit.Inventory.Count; slot++)
        {
            var stack = unit.Unit.Inventory.Items[slot];
            if (stack.Uses == 0 || content.Items.ContainsKey(stack.ItemId))
            {
                continue;
            }

            var spell = content.WeaponOf(unit.Unit, content.Weapon(stack.ItemId));
            if (spell.Area > 0 && unit.Unit.CanWield(spell, unitClass))
            {
                tomes.Add((slot, spell));
            }
        }

        return tomes;
    }

    /// <summary>
    /// A cast's score on <see cref="EnemyAi.Score"/>'s scale (issue 1391), and whether its forecast kills one struck: for
    /// each struck unit the kill bonus when the forecast's damage (a cashed mark's, when it cashes one) takes its HP,
    /// plus the expected damage capped at its HP; for a marking tome, each one that survives adds the expected x1.5 on
    /// that damage, the next lightning hit's share; then <see cref="EnemyAi.NoCounterBonus"/> once, since no one counters.
    /// A cast a rod catches (<paramref name="catcher"/>, issue 1400) is priced as its one caught strike on the holder, at
    /// x0.5 and with no mark's share, since a catch marks no one.
    /// </summary>
    public static (double Score, bool Kills) Price(BattleState state, GameContent content, BattleUnit caster, Weapon spell, Coord at, IReadOnlyList<BattleUnit> struck, BattleUnit? catcher = null)
    {
        var score = EnemyAi.NoCounterBonus;
        var kills = false;
        if (catcher is not null)
        {
            struck = [catcher];
        }

        foreach (var target in struck)
        {
            var side = (catcher is null ? Forecast(state, content, caster, spell, target, at) : CaughtForecast(state, content, caster, spell, target)).Attacker;
            var damage = side.FirstHit(crit: false);
            var hit = Combat.HitProbability(side.HitChance, state.Scheme);
            var kill = damage >= target.Hp;
            kills |= kill;
            score += (kill ? EnemyAi.KillBonus : 0) + Math.Min(target.Hp, damage) * hit;
            if (!kill && spell.Marks && catcher is null)
            {
                score += (Mark.Of(side.Damage) - side.Damage) * hit;
            }
        }

        return (score, kills);
    }
}
