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
    /// an enemy the attack would probably have killed. The one exclusion is exact and
    /// constant-free (issue 117): a target the named attack kills with certainty, the raw
    /// hit at 100 so the resolved probability is one under either scheme and the first
    /// strike's damage at least its current HP, contributes neither its counter nor its
    /// enemy-phase term, since both are branches of probability zero and a veto that
    /// counted them took a certain loss over an impossible death. Raw 99 prints as 100
    /// under two rolls and still counts. The same zero-probability branch read from the
    /// other side is excluded too (issue 126): a strike whose raw hit against the unit is
    /// 0 after the clamp contributes nothing, whether it is the counter on the named
    /// attack or an enemy-phase term; raw 1 counts in full. Reach is taken on the board
    /// as it stands with the mover treated as standing on <paramref name="tile"/>.
    /// </summary>
    public static ExposureSum Of(BattleState state, GameContent content, BattleUnit unit, Coord tile, BattleUnit? target = null, int? slot = null)
    {
        var moved = unit with { At = tile };
        var board = state.WithUnit(moved);
        var counter = 0;
        var counterCrit = 0;
        var certainKill = false;
        if (target is not null)
        {
            var (armed, weapon, rejection) = Resolver.ChooseWeapon(moved, content, slot);
            var distance = tile.DistanceTo(target.At);
            if (rejection is null && weapon!.InRange(distance))
            {
                var forecast = Combat.Forecast(armed.ToCombatant(board.Map, content), target.ToCombatant(board.Map, content), distance, state.Scheme);
                certainKill = KillsWithCertainty(forecast.Attacker, target.Hp);
                if (!certainKill)
                {
                    (counter, counterCrit) = Worst(forecast.Defender);
                }
            }
        }

        var noCrit = counter;
        var withCrit = counterCrit;
        var me = moved.ToCombatant(board.Map, content);
        foreach (var enemy in board.UnitsOf(unit.Side == Side.Player ? Side.Enemy : Side.Player))
        {
            var weapon = enemy.EquippedWeapon(content);
            if (weapon is null || (certainKill && enemy.Id == target!.Id))
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

    /// <summary>
    /// Whether the first strike of <paramref name="attacker"/> kills a target at
    /// <paramref name="hp"/> with certainty: it strikes, its raw hit is 100 (clamped, so
    /// <see cref="Combat.HitProbability"/> is exactly one under either scheme), and its
    /// plain damage reaches the HP. One strike, never the double, because the counter
    /// falls between the strikes.
    /// </summary>
    public static bool KillsWithCertainty(SideForecast attacker, int hp) =>
        attacker.Strikes && attacker.HitChance >= 100 && attacker.Damage >= hp;

    /// <summary>
    /// The worst a side's strikes can do: plain and crit damage over one or two strikes,
    /// or nothing when the side does not strike or its raw hit is 0, since a strike that
    /// cannot land is a branch of probability zero under either scheme (issue 126).
    /// </summary>
    private static (int Plain, int Crit) Worst(SideForecast side)
    {
        if (!side.Strikes || side.HitChance <= 0)
        {
            return (0, 0);
        }

        var strikes = side.Doubles ? 2 : 1;
        return (side.Damage * strikes, side.Damage * Combat.CritMultiplier * strikes);
    }
}
