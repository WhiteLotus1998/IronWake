namespace Ironwake.Core;

/// <summary>
/// Frozen iron and its chill (issue 702 slice 2, rounds 216 and 217; DESIGN section 5). A weapon
/// is frozen iron when its content marks it (<see cref="Weapon.FrozenIron"/>), when it is an
/// heirloom at its last stage (the woken Family Lance), or when it refines on frozen iron
/// (<see cref="Material.Rare"/>) and its stack has taken every rare step. A hungering weapon is
/// never frozen iron: Kinsbane is what the iron holds.
/// <list type="bullet">
/// <item>Chill: a hit from a frozen-iron weapon on a unit that survives the combat takes 1 Mov from
/// it until its side's next phase ends (<see cref="BattleUnit.Chill"/>). A miss does nothing.</item>
/// <item>It does not stack: a second hit refreshes the clock and takes no second point.</item>
/// <item>It never takes Mov below 1 (<see cref="Mov"/>).</item>
/// <item>Bosses are chilled like anyone.</item>
/// <item>An ice tome that names its school's <see cref="RiderKind.Chill"/> rider chills as frozen iron
/// does (issue 1244, <see cref="Chills"/>): the same clock and the same Mov, never a second point.</item>
/// </list>
/// The clock counts as <see cref="BattleUnit.Spent"/> does: a hit sets 1, the chilled side's phase
/// beginning turns 1 to 2, and a phase of that side ending clears 2 (<see cref="AtPhaseChange"/>).
/// Everything is board state, so Recall restores it with the board.
/// </summary>
public static class Frost
{
    /// <summary>The Mov a chill takes.</summary>
    public const int MovLost = 1;

    /// <summary>
    /// The weapon as frozen iron when <paramref name="stack"/> makes it so (the woken heirloom, a
    /// signature at its last rare step) or its content already does; a hungering weapon never.
    /// </summary>
    public static Weapon Shape(Weapon weapon, ItemStack stack, GameContent content)
    {
        if (weapon.Hungers)
        {
            return weapon.FrozenIron ? weapon with { FrozenIron = false } : weapon;
        }

        return weapon.FrozenIron || Becomes(weapon, stack, content) ? weapon with { FrozenIron = true } : weapon;
    }

    /// <summary>Whether <paramref name="stack"/> has made <paramref name="weapon"/> frozen iron: an heirloom at its last stage, or every rare Refine step taken.</summary>
    public static bool Becomes(Weapon weapon, ItemStack stack, GameContent content)
    {
        if (weapon.Hungers)
        {
            return false;
        }

        if (weapon.Heirloom is { } ladder)
        {
            return ladder.Turns.Count > 0 && stack.Stage >= ladder.Turns.Count;
        }

        var steps = content.Campaign.Forge.RareSteps;
        return steps > 0 && stack.Refines >= steps && Forge.MaterialFor(weapon, content).Material == Material.Rare;
    }

    /// <summary><paramref name="mov"/> as a chill leaves it for <paramref name="unit"/>: one less while chilled, never below 1.</summary>
    public static int Mov(int mov, BattleUnit unit) => unit.Chill > 0 && mov > 1 ? Math.Max(1, mov - MovLost) : mov;

    /// <summary>
    /// A chill clock across a phase change: a unit of the side whose phase begins turns 1 to 2, a
    /// unit of the side whose phase ended clears 2. Any other value carries.
    /// </summary>
    public static int AtPhaseChange(int chill, Side side, Side ended, Side begins) =>
        side == begins && chill == 1 ? 2
        : side == ended && chill == 2 ? 0
        : chill;

    /// <summary>Whether a hit from <paramref name="weapon"/> chills: frozen iron, or a tome that names its school's chill rider (issue 1244).</summary>
    public static bool Chills(GameContent content, Weapon? weapon) =>
        weapon is { FrozenIron: true } || content.RiderOf(weapon) is { Kind: RiderKind.Chill };

    /// <summary>
    /// After a combat or a strike: each unit in <paramref name="strikes"/> still standing that a
    /// hit from a weapon that chills (<see cref="Chills"/>) landed on is chilled (<see cref="UnitChilled"/>), its clock
    /// set to 1. <paramref name="aWeapon"/> is what <paramref name="aId"/> struck with and
    /// <paramref name="bWeapon"/> what <paramref name="bId"/> did; a null weapon never chills.
    /// A learned school's rider fires only past its gate (issue 1246, <see cref="LearnedGate"/>), read
    /// on <paramref name="a"/> and <paramref name="b"/>, the two as they entered the combat, else as
    /// <paramref name="state"/> finds them.
    /// </summary>
    public static BattleState AfterCombat(BattleState state, GameContent content, string aId, Weapon? aWeapon, string bId, Weapon? bWeapon, ValueList<StrikeEvent> strikes, List<GameEvent> events, BattleUnit? a = null, BattleUnit? b = null)
    {
        a ??= state.Find(aId);
        b ??= state.Find(bId);
        foreach (var (strikerId, weapon, targetId, striker, struck) in new[] { (aId, aWeapon, bId, a, b), (bId, bWeapon, aId, b, a) })
        {
            if (!LearnedGate.Fires(content, striker, weapon, struck))
            {
                continue;
            }

            if (!Chills(content, weapon) || !strikes.Any(s => s.AttackerId == strikerId && s.TargetId == targetId && s.Hit))
            {
                continue;
            }

            if (state.Find(targetId) is { } target)
            {
                events.Add(new UnitChilled(target.Id, strikerId, target.Side, target.Side == state.Phase));
                state = state.WithUnit(target with { Chill = 1 });
            }
        }

        return state;
    }

    /// <summary>
    /// The unit card's line for a chilled unit in <paramref name="state"/>: <c>chilled: Mov -1 until
    /// enemy phase ends</c>, or <c>until the next enemy phase ends</c> while its clock has not begun
    /// on its own side's phase; null when it is not chilled.
    /// </summary>
    public static string? CardLine(BattleState state, BattleUnit unit) =>
        unit.Chill > 0 ? $"chilled: Mov -{MovLost} until {Until(unit.Side, unit.Chill == 1 && unit.Side == state.Phase)}" : null;

    /// <summary>When a chill on <paramref name="side"/> ends, as a line says it: <c>enemy phase ends</c>, or <c>the next enemy phase ends</c>.</summary>
    public static string Until(Side side, bool next) => (next ? "the next " : "") + Word(side) + " phase ends";

    /// <summary>The side as a line names it: <c>player</c> or <c>enemy</c>.</summary>
    public static string Word(Side side) => side == Side.Enemy ? "enemy" : "player";
}
