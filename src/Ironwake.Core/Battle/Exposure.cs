namespace Ironwake.Core;

/// <summary>
/// The worst a unit could take over one cycle, the question behind the captain veto of
/// issue 12 and the exposure counters of the two Cha spikes (issue 85, issue 16), one
/// function for all three (Design Table, seventh round). <see cref="NoCrit"/> is every
/// strike landing at its plain damage, doubles included; <see cref="WithCrit"/> is the
/// same strikes at the crit multiplier. The hard veto reads the no-crit sum against
/// current HP; the crit sum orders among tiles that pass and never forbids.
/// </summary>
/// <param name="Counter">The worst-case counter on the attack the unit is about to make; zero when the plan has no attack.</param>
/// <param name="NoCrit">Counter plus every enemy that can reach the tile, no crit landing.</param>
/// <param name="WithCrit">Counter plus every enemy that can reach the tile, every strike a crit.</param>
public sealed record ExposureSum(int Counter, int NoCrit, int WithCrit);

public static class Exposure
{
    /// <summary>
    /// The exposure of <paramref name="unit"/> ending its cycle on <paramref name="tile"/>,
    /// after attacking <paramref name="target"/> from there when one is named. Every unit
    /// of the other side whose reach-plus-range covers the tile is counted at the
    /// forecast's own numbers from the best attack tile it can end on, conservative on
    /// purpose: a sleeping Guard is counted by its reach from where it stands, and so is
    /// an enemy the attack would have killed. Reach is taken on the board as it stands
    /// with the mover treated as standing on <paramref name="tile"/>.
    /// </summary>
    public static ExposureSum Of(BattleState state, GameContent content, BattleUnit unit, Coord tile, BattleUnit? target = null, int? slot = null)
    {
        var moved = unit with { At = tile };
        var board = state.WithUnit(moved);
        var counter = 0;
        var counterCrit = 0;
        if (target is not null)
        {
            var (armed, weapon, rejection) = Resolver.ChooseWeapon(moved, content, slot);
            var distance = tile.DistanceTo(target.At);
            if (rejection is null && weapon!.InRange(distance))
            {
                var forecast = Combat.Forecast(armed.ToCombatant(board.Map, content), target.ToCombatant(board.Map, content), distance, state.Scheme);
                (counter, counterCrit) = Worst(forecast.Defender);
            }
        }

        var noCrit = counter;
        var withCrit = counterCrit;
        var me = moved.ToCombatant(board.Map, content);
        foreach (var enemy in board.UnitsOf(unit.Side == Side.Player ? Side.Enemy : Side.Player))
        {
            var weapon = enemy.EquippedWeapon(content);
            if (weapon is null)
            {
                continue;
            }

            var movement = content.Class(enemy.Unit.ClassId).Movement;
            var mayMove = board.EffectiveBehavior(enemy) == Behavior.Aggressive;
            var reach = board.ReachOf(enemy, content);
            var worst = (Plain: 0, Crit: 0);
            var found = false;
            foreach (var from in EnemyAi.AttackTiles(board, content, enemy, weapon, moved, movement))
            {
                if (from != enemy.At && (!mayMove || !reach.CanEnd(from)))
                {
                    continue;
                }

                var striker = new Combatant(enemy.Unit, content.Class(enemy.Unit.ClassId), weapon, board.Map.TerrainAt(from, content), enemy.Hp, 0, enemy.WeaponBroken(content));
                var forecast = Combat.Forecast(striker, me, from.DistanceTo(tile), state.Scheme);
                var here = Worst(forecast.Attacker);
                if (!found || here.Plain > worst.Plain || (here.Plain == worst.Plain && here.Crit > worst.Crit))
                {
                    worst = here;
                    found = true;
                }
            }

            noCrit += worst.Plain;
            withCrit += worst.Crit;
        }

        return new ExposureSum(counter, noCrit, withCrit);
    }

    private static (int Plain, int Crit) Worst(SideForecast side)
    {
        if (!side.Strikes)
        {
            return (0, 0);
        }

        var strikes = side.Doubles ? 2 : 1;
        return (side.Damage * strikes, side.Damage * Combat.CritMultiplier * strikes);
    }
}
