namespace Ironwake.Core;

/// <summary>
/// A recruit's signature (DESIGN.md 13.18, experiment): the personality line made one board
/// fact, flaw included. The cast file names each holder's kind; the rules below read it only
/// on a map with the <c>signatures: on</c> header and only for a player unit.
/// </summary>
public enum SignatureKind
{
    /// <summary>Wren: counts tiles (Move Again after any action) and talks (a combat she is in wakes at <see cref="Signatures.TalkRadius"/>).</summary>
    Counting,

    /// <summary>Teodor: an ally within 2 acting after him strikes at +5; his own strike with an ally within 2 is at -10.</summary>
    Orders,

    /// <summary>Ottilie: a strike or watch shot at a displayed hit under 65 is refused.</summary>
    Ledger,
}

/// <summary>
/// The three cadets' signatures (DESIGN.md 13.18, rounds 117 and 118, issue 486), behind a
/// map's <c>signatures: on</c> header. Each is read where the rule it bends is read, so the
/// forecast, the resolver and the planners cannot drift: Teodor's two hit modifiers sit in the
/// striker's hit slot through <see cref="StrikeHit"/>, Wren's Move Again in <see cref="HasMoveAgain"/>,
/// her noise in <see cref="NoiseRadius"/>, and Ottilie's refusal in <see cref="Refuses"/>, on the
/// displayed hit after rounding and every modifier.
/// </summary>
public static class Signatures
{
    /// <summary>The noise radius of a combat Wren is in (round 117), in place of <see cref="GameContent.NoiseRadius"/>.</summary>
    public const int TalkRadius = 8;

    /// <summary>The one radius both halves of Teodor's signature read (round 118).</summary>
    public const int OrdersRadius = 2;

    /// <summary>The hit an ally ordered by Teodor strikes with.</summary>
    public const int OrdersHit = 5;

    /// <summary>The hit Teodor loses on his own strike while an ally is within <see cref="OrdersRadius"/>.</summary>
    public const int WatchedHit = 10;

    /// <summary>The displayed hit below which Ottilie refuses a strike or a watch shot.</summary>
    public const int LedgerFloor = 65;

    /// <summary>The signature <paramref name="unit"/> plays with on this board: its cast kind on a <c>signatures: on</c> map for a player unit, else null.</summary>
    public static SignatureKind? Of(BattleState state, GameContent content, BattleUnit unit) =>
        state.Map.SignaturesEnabled && unit.Side == Side.Player && content.Signatures.TryGetValue(unit.Id, out var kind) ? kind : null;

    /// <summary>Whether <paramref name="unit"/> has Move Again: an ability that grants it, or Wren's counting on a <c>signatures: on</c> map.</summary>
    public static bool HasMoveAgain(BattleState state, GameContent content, BattleUnit unit) =>
        AbilityRules.HasMoveAgain(content.AbilitiesOf(unit.Unit)) || Of(state, content, unit) == SignatureKind.Counting;

    /// <summary>
    /// The noise radius of a combat between <paramref name="a"/> and <paramref name="b"/>, read from
    /// both combat tiles: <see cref="TalkRadius"/> when either is Wren playing her signature, either
    /// phase, else the content's.
    /// </summary>
    public static int NoiseRadius(BattleState state, GameContent content, BattleUnit? a, BattleUnit? b) =>
        (a is not null && Of(state, content, a) == SignatureKind.Counting) || (b is not null && Of(state, content, b) == SignatureKind.Counting)
            ? TalkRadius
            : content.NoiseRadius;

    /// <summary>
    /// The Teodor whose orders <paramref name="striker"/> strikes under, or null: a living unit
    /// of its side with <see cref="SignatureKind.Orders"/>, not the striker, that has acted this
    /// phase (its side's phase), within <see cref="OrdersRadius"/> of the striker's tile.
    /// </summary>
    public static BattleUnit? OrderedBy(BattleState state, GameContent content, BattleUnit striker) =>
        state.Phase != striker.Side
            ? null
            : state.UnitsOf(striker.Side).FirstOrDefault(u =>
                u.Id != striker.Id && u.Acted && u.At.DistanceTo(striker.At) <= OrdersRadius && Of(state, content, u) == SignatureKind.Orders);

    /// <summary>
    /// Whether <paramref name="striker"/> is Teodor watched: playing <see cref="SignatureKind.Orders"/>
    /// on his side's phase with any ally within <see cref="OrdersRadius"/> of his tile.
    /// </summary>
    public static bool Watched(BattleState state, GameContent content, BattleUnit striker) =>
        state.Phase == striker.Side
        && Of(state, content, striker) == SignatureKind.Orders
        && state.UnitsOf(striker.Side).Any(u => u.Id != striker.Id && u.At.DistanceTo(striker.At) <= OrdersRadius);

    /// <summary>
    /// The hit modifier the signatures give <paramref name="striker"/>'s own strike: +<see cref="OrdersHit"/>
    /// under Teodor's orders, -<see cref="WatchedHit"/> when it is Teodor watched. A counter is never
    /// changed (counters stand), so <paramref name="countering"/> gives 0.
    /// </summary>
    public static int StrikeHit(BattleState state, GameContent content, BattleUnit striker, bool countering) =>
        countering ? 0
        : (OrderedBy(state, content, striker) is not null ? OrdersHit : 0) - (Watched(state, content, striker) ? WatchedHit : 0);

    /// <summary>Whether <paramref name="unit"/> refuses a strike or watch shot at <paramref name="displayedHit"/>: Ottilie's ledger under <see cref="LedgerFloor"/>.</summary>
    public static bool Refuses(BattleState state, GameContent content, BattleUnit unit, int displayedHit) =>
        displayedHit < LedgerFloor && Of(state, content, unit) == SignatureKind.Ledger;

    /// <summary>The reason an attack is refused, naming the displayed hit and the ledger (issue 486): both reasons.</summary>
    public static string LedgerRefusal(BattleUnit unit, string targetId, int displayedHit) =>
        $"{unit.Id} will not shoot {targetId} at {displayedHit}: she bills the crown for every arrow and looses none under {LedgerFloor} (signature)";

    /// <summary>The one line <c>show</c> and the legend print for a signature, in player words, flaw included.</summary>
    public static string Describe(SignatureKind kind) => kind switch
    {
        SignatureKind.Counting => $"counts tiles: moves again after any action with the Mov she has left; talks: any combat she is in wakes a sleeping group within {TalkRadius}",
        SignatureKind.Orders => $"orders: an ally within {OrdersRadius} who acts after him strikes at +{OrdersHit} hit; watched: his own strike with an ally within {OrdersRadius} is at -{WatchedHit} hit",
        SignatureKind.Ledger => $"ledger: refuses a strike or a watch shot under {LedgerFloor} hit; her counters stand",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown signature"),
    };
}
