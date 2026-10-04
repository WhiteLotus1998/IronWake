namespace Ironwake.Core;

/// <summary>
/// Which side of the hunger the bearer's oath fell on (issue 635 slice 16, Design Table rounds 303
/// to 305): the four answers the Oath Stone records, for the endings (#634) to read.
/// </summary>
public enum OathSide
{
    /// <summary>The bound man's death fed the hungering weapon.</summary>
    Fed,

    /// <summary>He was freed when his boss fell.</summary>
    Spared,

    /// <summary>The bearer killed him with anything but the hungering weapon, and the god got nothing.</summary>
    Refused,

    /// <summary>Anyone else killed him, or something other than a combat did.</summary>
    Other,
}

/// <summary>Reads <see cref="OathSide"/> off a decided board, and words it for the record.</summary>
public static class Oath
{
    /// <summary>
    /// The side <paramref name="end"/> shows for the member <paramref name="memberId"/>: spared when the bond
    /// freed him, fed when the kill fed, refused when the member killed him unfed, other for any other end,
    /// null while he stands (<see cref="BattleState.Bond"/> unset).
    /// </summary>
    public static OathSide? Of(BattleState end, string memberId) => end.Bond switch
    {
        null => null,
        BondFate.Freed => OathSide.Spared,
        _ => end.BondKilledBy switch
        {
            { Fed: true } => OathSide.Fed,
            { } kill when kill.KillerId == memberId => OathSide.Refused,
            _ => OathSide.Other,
        },
    };

    /// <summary>The record's word for <paramref name="side"/>: <c>fed</c>, <c>spared</c>, <c>refused</c> or <c>other</c>.</summary>
    public static string Word(OathSide side) => side switch
    {
        OathSide.Fed => "fed",
        OathSide.Spared => "spared",
        OathSide.Refused => "refused",
        _ => "other",
    };

    /// <summary>The side a record word names, or null for any other word.</summary>
    public static OathSide? Parse(string word) =>
        Enum.GetValues<OathSide>().Where(s => Word(s) == word).Select(s => (OathSide?)s).FirstOrDefault();
}
