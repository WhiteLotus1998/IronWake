using Ironwake.Core;

namespace Ironwake.Content;

/// <summary>
/// The player's roster until issue 13 writes the cast: five cadets with iron swords and a
/// field dressing each, the first the captain. The Sim and the CLI share it so a transcript and a gate describe
/// the same party, and every output that uses it says so.
/// </summary>
public static class SyntheticRoster
{
    public const string Notice = "roster is 5 synthetic cadets until issue 13";

    public static ValueList<Unit> Cadets { get; } = ValueList<Unit>.Of(
        Cadet("captain", new Stats(22, 8, 0, 7, 8, 6, 5, 2, 9)),
        Cadet("wren", new Stats(20, 7, 0, 6, 8, 5, 4, 2, 3)),
        Cadet("recruit-2", new Stats(19, 6, 0, 5, 7, 4, 4, 3, 4)),
        Cadet("recruit-3", new Stats(21, 7, 0, 5, 6, 3, 5, 2, 2)),
        Cadet("recruit-4", new Stats(18, 5, 0, 8, 9, 5, 3, 3, 5)));

    private static Unit Cadet(string id, Stats stats) =>
        new(id, id, "cadet", 1, 0, stats, Stats.Zero, new Inventory(ValueList<ItemStack>.Of(new ItemStack("iron_sword", 40), new ItemStack("field_dressing", 3))), ValueList<string>.Empty);
}
