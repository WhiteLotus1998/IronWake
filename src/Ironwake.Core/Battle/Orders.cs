namespace Ironwake.Core;

/// <summary>The three orders of Commander's Word (DESIGN.md 13.2, issue 85; rounds 206 to 209).</summary>
public enum OrderKind
{
    /// <summary>+1 Mov this phase to the allies reached who have not moved.</summary>
    Press,

    /// <summary>Heals the allies reached <see cref="Orders.RallyPercent"/> percent of max HP, at least 1.</summary>
    Rally,

    /// <summary>The allies reached who have already acted may each move up to <see cref="Orders.FallBackMov"/>, waking no one.</summary>
    FallBack,
}

/// <summary>
/// Commander's Word, arm B (DESIGN.md 13.2, issue 85): fixed magnitudes, a reach set by the
/// captain's Cha. The radius is <c>2 + Cha / 4</c>, Manhattan, walls not considered, the
/// distance the wake rule uses. The captain is never among the reached; the allies counted are
/// the living player units on the board other than him.
/// </summary>
public static class Orders
{
    public const int BaseRadius = 2;
    public const int ChaPerTile = 4;
    public const int RallyPercent = 15;
    public const int FallBackMov = 2;

    /// <summary>The order's reach in tiles from the captain: <c>2 + Cha / 4</c> on his effective Cha.</summary>
    public static int Radius(BattleUnit captain, GameContent content) =>
        BaseRadius + content.StatsOf(captain.Unit).Cha / ChaPerTile;

    /// <summary>The word a script types for an order, and the console prints.</summary>
    public static string Word(OrderKind kind) => kind switch
    {
        OrderKind.Press => "press",
        OrderKind.Rally => "rally",
        _ => "fall back",
    };

    /// <summary>The order a script word names (<c>press</c>, <c>rally</c>, <c>fall back</c> as one or two words), or null.</summary>
    public static OrderKind? Parse(string word) => word switch
    {
        "press" => OrderKind.Press,
        "rally" => OrderKind.Rally,
        "fall back" or "fallback" or "fall-back" => OrderKind.FallBack,
        _ => null,
    };

    /// <summary>The allies the order is for: every living player unit on the board but the captain, in id order.</summary>
    public static IReadOnlyList<BattleUnit> Allies(BattleState state) =>
        state.UnitsOf(Side.Player).Where(u => !u.IsCaptain).ToList();

    /// <summary>The allies within the radius of <paramref name="from"/>, in id order.</summary>
    public static IReadOnlyList<BattleUnit> InRadius(BattleState state, GameContent content, BattleUnit captain, Coord from)
    {
        var radius = Radius(captain, content);
        return Allies(state).Where(u => u.At.DistanceTo(from) <= radius).ToList();
    }

    /// <summary>
    /// The allies an order called from <paramref name="from"/> would act on, in id order: for
    /// Press those in the radius who have not moved, for Rally everyone in it, for Fall back
    /// those in it who have already acted. The preview and the call read the same list.
    /// </summary>
    public static IReadOnlyList<BattleUnit> Reached(BattleState state, GameContent content, BattleUnit captain, Coord from, OrderKind kind) =>
        InRadius(state, content, captain, from).Where(u => kind switch
        {
            OrderKind.Press => !u.Moved && !u.Acted,
            OrderKind.Rally => true,
            _ => u.Acted,
        }).ToList();

    /// <summary>What Rally heals a unit: 15 percent of max HP, at least 1, never past max.</summary>
    public static int RallyHeal(BattleUnit unit, GameContent content)
    {
        var max = unit.MaxHp(content);
        return Math.Min(max - unit.Hp, Math.Max(1, max * RallyPercent / 100));
    }

    /// <summary>
    /// Why the captain cannot call an order now, or null when he can: orders must be open on
    /// the map, unspent, the captain on the board in the player phase and not yet acted.
    /// </summary>
    public static string? Refusal(BattleState state)
    {
        if (!state.OrdersOpen)
        {
            return "there are no orders on this map";
        }

        if (state.OrderCalled is { } called)
        {
            return $"the order is spent this map ({Word(called)})";
        }

        if (state.Phase != Side.Player)
        {
            return "orders are called in the player phase";
        }

        if (state.UnitsOf(Side.Player).FirstOrDefault(u => u.IsCaptain) is not { } captain)
        {
            return "there is no captain on the board to call it";
        }

        return captain.Acted ? $"{captain.Id} has already acted this phase" : null;
    }
}
