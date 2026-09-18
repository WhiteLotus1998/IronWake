using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// A six-by-four yard on the starter content: the captain Hale and the recruit Wren, both
/// cadets with iron swords, two tiles of open ground from a brigand and a soldier. Cadet
/// Mov is 4, so either player unit reaches the tile beside its enemy on turn 1.
/// </summary>
internal static class BattleFixture
{
    public static GameContent Starter => MapFixture.Content;

    public const string Yard = """
        name: Yard
        size: 6x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ......
        ......
        ......
        ......

        units:
        P captain 0,1
        P recruit:wren 0,2
        E brigand 3,1 group:yard behavior:aggressive
        E soldier 3,2 group:yard behavior:aggressive

        """;

    public static readonly Unit Hale = Recruit("hale", new Stats(22, 8, 0, 7, 8, 6, 5, 2, 9), "iron_sword");
    public static readonly Unit Wren = Recruit("wren", new Stats(20, 7, 0, 6, 8, 5, 4, 2, 3), "iron_sword");
    public static readonly Unit Ivo = Recruit("ivo", new Stats(19, 6, 0, 5, 7, 4, 4, 3, 4), "iron_sword");
    public static readonly Unit Unarmed = Recruit("pell", new Stats(18, 5, 0, 5, 6, 3, 3, 2, 2));

    public static ValueList<Unit> Roster => ValueList<Unit>.Of(Hale, Wren);

    public static MapDefinition YardMap => MapFixture.Parse(Yard, "yard.map");

    public static BattleState Start(ulong seed = 7, ValueList<Unit>? roster = null, string? map = null) =>
        BattleState.From(map is null ? YardMap : MapFixture.Parse(map, "test.map"), Starter, roster ?? Roster, seed);

    public static Unit Recruit(string id, Stats stats, params string[] items) => Recruit(id, "cadet", stats, items);

    /// <summary>A level 1 unit of a starter class carrying each named weapon or item at full uses.</summary>
    public static Unit Recruit(string id, string classId, Stats stats, params string[] items)
    {
        var inventory = Inventory.Empty;
        foreach (var item in items)
        {
            inventory = inventory.Add(new ItemStack(item, Starter.Weapons.TryGetValue(item, out var weapon) ? weapon.Durability : Starter.Item(item).Uses));
        }

        return new Unit(id, id, classId, 1, 0, stats, Stats.Zero, inventory, ValueList<string>.Empty);
    }

    /// <summary>The unit with the uses of its first inventory slot set.</summary>
    public static Unit WithUses(this Unit unit, int uses) =>
        unit with { Inventory = unit.Inventory.Replace(0, unit.Inventory.Items[0] with { Uses = uses }) };

    /// <summary>Applies a command that must be accepted and returns the next state.</summary>
    public static BattleState Do(this BattleState state, Command command)
    {
        var result = Resolver.Apply(state, Starter, command);
        Assert.True(result.Accepted, result.Rejection?.Message);
        return result.Next;
    }

    public static ApplyResult Try(this BattleState state, Command command) => Resolver.Apply(state, Starter, command);

    public static Rejection Refused(this BattleState state, Command command)
    {
        var result = state.Try(command);
        Assert.False(result.Accepted, "the command was accepted");
        Assert.Same(state, result.Next);
        Assert.Empty(result.Events);
        return result.Rejection!;
    }
}
