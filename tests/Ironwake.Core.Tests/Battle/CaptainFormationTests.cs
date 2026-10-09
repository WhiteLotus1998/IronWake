using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The captain's ladder's verbs (issue 705, slice 2; DESIGN section 3): the Vanguard's Shoulder to
/// Shoulder (+1 Def and +1 Res with an ally orthogonally beside), the Marshal's Command Presence
/// (allies within 2 tiles fight at +10 Acc) and the Commander's Field Command (+10 Evade, the same
/// shape), and the Pathfinder's Trail Sense (forest and hill cost 1). Played on the shipped Saltmarsh
/// Ford's open south rows and the forest at 2,6.
/// </summary>
public class CaptainFormationTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly string MapPath = Path.Combine(Fixture.RealContentDirectory(), "maps", "saltmarsh_ford.map");

    private static BattleState Placed() =>
        BattleState.From(MapFiles.Load(MapPath, Shipped), Shipped, Shipped.Cast, 705);

    private static BattleUnit Player(BattleState state, int index) => state.UnitsOf(Side.Player).ElementAt(index);

    /// <summary>The <paramref name="index"/>th player unit in <paramref name="classId"/> at <paramref name="at"/>, holding <paramref name="abilities"/> as its own, at full HP.</summary>
    private static BattleState As(BattleState state, int index, string classId, Coord at, params string[] abilities)
    {
        var unit = Player(state, index);
        var changed = unit.Unit with { ClassId = classId, Abilities = ValueList<string>.From(abilities) };
        return state.WithUnit(unit with { Unit = changed, At = at, Hp = Shipped.StatsOf(changed).Hp });
    }

    /// <summary>An enemy that strikes at one tile, not a boss.</summary>
    private static BattleUnit Melee(BattleState state) =>
        state.UnitsOf(Side.Enemy).First(u => !u.IsBoss && u.EquippedWeapon(Shipped)?.MinRange == 1);

    private static BattleState At(BattleState state, int index, Coord at) => state.WithUnit(Player(state, index) with { At = at });

    /// <summary>The board with every player unit but the first <paramref name="keep"/> sent to the bottom-right corner rows, out of every radius here.</summary>
    private static BattleState Clear(BattleState state, int keep)
    {
        var far = new[] { new Coord(13, 9), new Coord(12, 9), new Coord(13, 8), new Coord(12, 8) };
        var i = 0;
        foreach (var unit in state.UnitsOf(Side.Player).Skip(keep).ToList())
        {
            state = state.WithUnit(unit with { At = far[i++] });
        }

        return state;
    }

    [Fact]
    public void ShoulderToShoulderAddsDefAndResWithAnAllyOrthogonallyBeside()
    {
        var state = Clear(As(Placed(), 0, "vanguard", new Coord(2, 8), "shoulder_to_shoulder"), 2);
        var alone = At(state, 1, new Coord(5, 8));
        var beside = At(state, 1, new Coord(3, 8));
        var diagonal = At(state, 1, new Coord(3, 7));
        var id = Player(state, 0).Id;

        var lone = alone.Find(id)!.ToCombatant(alone, Shipped).Stats;
        var paired = beside.Find(id)!.ToCombatant(beside, Shipped).Stats;

        Assert.Equal(lone.Def + 1, paired.Def);
        Assert.Equal(lone.Res + 1, paired.Res);
        Assert.Equal(lone with { Def = lone.Def + 1, Res = lone.Res + 1 }, paired);
        Assert.Equal(lone, diagonal.Find(id)!.ToCombatant(diagonal, Shipped).Stats);
    }

    [Fact]
    public void ShoulderToShoulderIsNotLentToTheAllyAndAnEnemyBesideDoesNotCount()
    {
        var state = Clear(As(Placed(), 0, "vanguard", new Coord(2, 8), "shoulder_to_shoulder"), 2);
        state = At(state, 1, new Coord(3, 8));
        var ally = Player(state, 1);
        var lone = At(state, 0, new Coord(0, 9));
        Assert.Equal(lone.Find(ally.Id)!.ToCombatant(lone, Shipped).Stats, state.Find(ally.Id)!.ToCombatant(state, Shipped).Stats);

        var withEnemy = At(state, 1, new Coord(5, 8));
        var soldier = Melee(withEnemy);
        var flanked = withEnemy.WithUnit(soldier with { At = new Coord(2, 7) });
        var id = Player(state, 0).Id;
        Assert.Equal(withEnemy.Find(id)!.ToCombatant(withEnemy, Shipped).Stats, flanked.Find(id)!.ToCombatant(flanked, Shipped).Stats);
    }

    [Fact]
    public void ShoulderToShoulderReachesTheForecastsNumbers()
    {
        var state = Clear(As(Placed(), 0, "vanguard", new Coord(2, 8), "shoulder_to_shoulder"), 2);
        var soldier = Melee(state) with { At = new Coord(2, 7) };
        state = state.WithUnit(soldier);
        var id = Player(state, 0).Id;
        var alone = At(state, 1, new Coord(5, 8));
        var beside = At(state, 1, new Coord(3, 8));

        int Damage(BattleState board) =>
            Core.Combat.Forecast(board.Find(soldier.Id)!.ToCombatant(board, Shipped), board.Find(id)!.Answering(board, Shipped, soldier.At), 1, board.Scheme).Attacker.Damage;

        Assert.Equal(Damage(alone) - 1, Damage(beside));
    }

    [Theory]
    [InlineData(4, 8, 10)]
    [InlineData(3, 9, 10)]
    [InlineData(5, 8, 0)]
    [InlineData(4, 9, 0)]
    public void CommandPresenceGivesAlliesWithinTwoTilesTenAcc(int x, int y, int hit)
    {
        var state = Clear(As(Placed(), 0, "marshal", new Coord(2, 8), "command_presence"), 2);
        state = At(state, 1, new Coord(x, y));
        var ally = Player(state, 1);

        Assert.Equal(new CombatBonus(hit, 0, 0, 0), state.Find(ally.Id)!.ToCombatant(state, Shipped).Aura);
    }

    [Fact]
    public void CommandPresenceNeverReachesTheHolderOrTheEnemy()
    {
        var state = Clear(As(Placed(), 0, "marshal", new Coord(2, 8), "command_presence"), 1);
        var soldier = Melee(state) with { At = new Coord(2, 7) };
        state = state.WithUnit(soldier);

        Assert.Equal(CombatBonus.None, Player(state, 0).ToCombatant(state, Shipped).Aura);
        Assert.Equal(CombatBonus.None, state.Find(soldier.Id)!.ToCombatant(state, Shipped).Aura);
    }

    [Fact]
    public void CommandPresenceRaisesTheAllysHitChanceByTen()
    {
        var state = Clear(As(Placed(), 0, "marshal", new Coord(0, 9), "command_presence"), 2);
        var soldier = Melee(state) with { At = new Coord(8, 7) };
        state = At(state.WithUnit(soldier), 1, new Coord(8, 8));
        var ally = Player(state, 1).Id;
        var near = At(state, 0, new Coord(7, 9));

        int Hit(BattleState board) =>
            Core.Combat.HitChance(board.Find(ally)!.ToCombatant(board, Shipped), board.Find(soldier.Id)!.Answering(board, Shipped, board.Find(ally)!.At));

        Assert.InRange(Hit(state), 0, 89);
        Assert.Equal(Hit(state) + 10, Hit(near));
    }

    [Fact]
    public void FieldCommandLowersTheEnemysHitOnAnAllyByTen()
    {
        var state = Clear(As(Placed(), 0, "commander", new Coord(0, 9), "field_command"), 2);
        var soldier = Melee(state) with { At = new Coord(8, 7) };
        state = At(state.WithUnit(soldier), 1, new Coord(8, 8));
        var ally = Player(state, 1).Id;
        var near = At(state, 0, new Coord(7, 9));

        int Hit(BattleState board) =>
            Core.Combat.HitChance(board.Find(soldier.Id)!.ToCombatant(board, Shipped), board.Find(ally)!.Answering(board, Shipped, soldier.At));

        Assert.InRange(Hit(state), 11, 100);
        Assert.Equal(Hit(state) - 10, Hit(near));
    }

    [Fact]
    public void TwoHoldersOfOneAuraDoNotStackAndTwoAurasDo()
    {
        var state = Clear(As(As(Placed(), 0, "marshal", new Coord(2, 8), "command_presence"), 1, "marshal", new Coord(4, 8), "command_presence"), 3);
        state = At(state, 2, new Coord(3, 8));
        var ally = Player(state, 2).Id;
        Assert.Equal(new CombatBonus(10, 0, 0, 0), state.Find(ally)!.ToCombatant(state, Shipped).Aura);

        var both = As(state, 0, "commander", new Coord(2, 8), "command_presence", "field_command");
        Assert.Equal(new CombatBonus(10, 10, 0, 0), both.Find(ally)!.ToCombatant(both, Shipped).Aura);
    }

    [Theory]
    [InlineData("pathfinder", 2)]
    [InlineData("ranger", 3)]
    public void TrailSenseMakesForestCostOneToThePathfinder(string classId, int cost)
    {
        var state = Clear(As(Placed(), 0, classId, new Coord(2, 8)), 1);
        var unit = Player(state, 0);

        Assert.Equal(cost, state.ReachOf(unit, Shipped).CostTo(new Coord(2, 6)));
    }

    [Fact]
    public void TrailSenseLowersOnlyItsTerrainAndNeverOpensImpassableGround()
    {
        var abilities = ValueList<Ability>.Of(Shipped.Ability("trail_sense"));

        Assert.Equal(1, AbilityRules.StepCost(Shipped.TerrainById("forest"), MovementType.Infantry, abilities));
        Assert.Equal(1, AbilityRules.StepCost(Shipped.TerrainById("hill"), MovementType.Armored, abilities));
        Assert.Equal(Shipped.TerrainById("fort").MoveCost(MovementType.Infantry), AbilityRules.StepCost(Shipped.TerrainById("fort"), MovementType.Infantry, abilities));
        Assert.Null(AbilityRules.StepCost(Shipped.TerrainById("water"), MovementType.Infantry, abilities));
        Assert.Equal(2, AbilityRules.StepCost(Shipped.TerrainById("forest"), MovementType.Infantry, ValueList<Ability>.Empty));
    }

    [Fact]
    public void EachCaptainsClassNamesAMasteryAndThePathfinderAddsTrailSense()
    {
        Assert.Equal("shoulder_to_shoulder", Shipped.Class("vanguard").Mastery);
        Assert.Equal("command_presence", Shipped.Class("marshal").Mastery);
        Assert.Equal("move_again", Shipped.Class("ranger").Mastery);
        Assert.Equal("breakthrough", Shipped.Class("champion").Mastery);
        Assert.Equal("field_command", Shipped.Class("commander").Mastery);
        Assert.Equal("light_step", Shipped.Class("pathfinder").Mastery);
        Assert.Equal(ValueList<string>.Of("trail_sense"), Shipped.Class("pathfinder").Abilities);
        Assert.All(Shipped.Classes.Values.Where(c => c.Captain), c => Assert.Equal(12, c.MasteryPoints));
    }
}
