using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Obsidian Armor offered and laid (issue 1403 slice 3): <see cref="Resolver.Legal"/> lists an armor tome on its caster and
/// on each ally its range reaches, an armor tome is never struck with, and the Sim's heuristic lays a one-hit shell on an
/// exposed ally that has acted, planning after the rest so the allies stand where they end (<see cref="ShellAim"/>). The
/// fixture tomes are <see cref="ArmorTests"/>' in Pell's slot 0 on <c>the_tollgate_frost.map</c>: Pell at 6,10, the
/// captain beside her at 6,11, Wren and Teodor two away.
/// </summary>
public class ShellPlanTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly Weapon Cinder = Shipped.Weapon("cinder");

    private static readonly GameContent Armoring = Shipped with
    {
        Weapons = Shipped.Weapons
            .SetItem("test_earth_armor", Cinder with { Id = "test_earth_armor", Name = "Test Earth Armor", School = MagicSchool.Earth, Rider = RiderKind.Armor, Armor = new ArmorSpell(10, 2, 2), Ignites = false })
            .SetItem("test_obsidian_armor", Cinder with { Id = "test_obsidian_armor", Name = "Test Obsidian Armor", School = MagicSchool.Earth, Rider = RiderKind.Armor, Armor = new ArmorSpell(20, 0, 3) { Shell = true, Range = 1 }, Ignites = false }),
        Classes = Shipped.Classes.SetItem("adept", Shipped.Class("adept") with { Schools = ValueList<MagicSchool>.Of(MagicSchool.Fire, MagicSchool.Ice, MagicSchool.Lightning, MagicSchool.Earth) }),
    };

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_tollgate_frost.map");

    private static BattleState Board(string tome = "test_obsidian_armor", int uses = 1)
    {
        var state = BattleState.From(MapFiles.Load(SamplePath, Armoring), Armoring, Armoring.Cast, 1403);
        var pell = state.Find("pell")!;
        return state.WithUnit(pell with { Unit = pell.Unit with { Inventory = pell.Unit.Inventory.Replace(0, new ItemStack(tome, uses)) } });
    }

    /// <summary>
    /// The board with every player unit but Pell done for the phase, Wren stepped off to 3,11 so the captain's west side is
    /// open, and the woods brigand awake at 4,9, three from that open tile.
    /// </summary>
    private static BattleState Threatened()
    {
        var state = Board();
        foreach (var unit in state.UnitsOf(Side.Player).Where(u => u.Id != "pell").ToList())
        {
            state = state.WithUnit(unit with { Moved = true, Acted = true, At = unit.Id == "wren" ? new Coord(3, 11) : unit.At });
        }

        var brigand = state.UnitsOf(Side.Enemy).First(u => u.At == new Coord(6, 5));
        return state.WithUnit(brigand with { At = new Coord(4, 9), Behavior = Behavior.Aggressive });
    }

    private static List<UseItem> ArmorCasts(BattleState state) =>
        Resolver.Legal(state, Armoring).OfType<UseItem>().Where(u => u.UnitId == "pell" && u.Slot == 0).ToList();

    [Fact]
    public void LegalListsAShellOnItsCasterAndEachAllyItsRangeReaches()
    {
        var casts = ArmorCasts(Board());

        Assert.Equal(new[] { new UseItem("pell", 0, null), new UseItem("pell", 0, "captain") }, casts);
        Assert.All(casts, cast => Assert.True(Resolver.Apply(Board(), Armoring, cast).Accepted));
    }

    [Fact]
    public void LegalListsEarthArmorOnItsCasterAlone() =>
        Assert.Equal(new[] { new UseItem("pell", 0, null) }, ArmorCasts(Board("test_earth_armor")));

    [Fact]
    public void LegalListsNoArmorWithNoUseLeft() =>
        Assert.Empty(ArmorCasts(Board(uses: 0)));

    [Fact]
    public void AnArmorTomeIsNeverStruckWith()
    {
        var pell = Board().Find("pell")!;

        Assert.Null(pell.UsableWeaponAt(Armoring, 0));
        Assert.DoesNotContain(Resolver.Legal(Board(), Armoring).OfType<Attack>(), a => a.UnitId == "pell" && (a.Slot ?? pell.EquippedSlot(Armoring)) == 0);
    }

    [Fact]
    public void AUnitHoldingAShellPlansAfterTheRest()
    {
        var order = HeuristicPlayer.PlanOrder(Board(), Armoring).Select(u => u.Id).ToList();

        Assert.Equal(new[] { "captain", "teodor", "wren", "pell" }, order);
        Assert.Equal(new[] { "captain", "pell", "teodor", "wren" }, HeuristicPlayer.PlanOrder(Board("test_earth_armor"), Armoring).Select(u => u.Id));
    }

    [Fact]
    public void TheHeuristicLaysTheShellWhenAWearerIsExposed()
    {
        var state = Threatened();
        Assert.True(Exposure.Of(state, Armoring, state.Find("captain")!, new Coord(6, 11)).NoCrit > 0);

        var plan = HeuristicPlayer.PlanUnit(state, Armoring, state.Find("pell")!, out _);

        var lay = Assert.IsType<UseItem>(plan[^1]);
        Assert.Equal(0, lay.Slot);
        var after = plan.Aggregate(state, (s, c) => Resolver.Apply(s, Armoring, c).Next);
        Assert.Contains(after.Units, u => u.Armor is { Shell: true });
    }

    [Theory]
    [InlineData(ShellAim.Never)]
    [InlineData(ShellAim.Drake)]
    [InlineData(ShellAim.Healer)]
    public void AnAimThatAdmitsNoOneLaysNoShell(ShellAim aim)
    {
        var state = Threatened();

        var plan = HeuristicPlayer.PlanUnit(state, Armoring, state.Find("pell")!, out _, shell: aim);

        Assert.DoesNotContain(plan, c => c is UseItem { Slot: 0 });
    }

    [Fact]
    public void TheFrontAimLaysTheShellOnlyOnTheAllyOfTheHighestDef()
    {
        var state = Threatened();
        var front = state.UnitsOf(Side.Player).Where(u => u.Id != "pell").OrderByDescending(u => Armoring.StatsOf(u.Unit).Def).ThenBy(u => u.Id, StringComparer.Ordinal).First();

        var plan = HeuristicPlayer.PlanUnit(state, Armoring, state.Find("pell")!, out _, shell: ShellAim.Front);

        Assert.Equal(new UseItem("pell", 0, front.Id), plan[^1]);
    }

    [Fact]
    public void AnAllyThatHasNotActedIsNotShelled()
    {
        var state = Threatened();
        state = state.WithUnit(state.Find("captain")! with { Moved = false, Acted = false });

        var plan = HeuristicPlayer.PlanUnit(state, Armoring, state.Find("pell")!, out _);

        Assert.DoesNotContain(plan, c => c is UseItem { Slot: 0, TargetId: "captain" });
    }

    [Fact]
    public void AnAllyAlreadyWearingArmorIsNotShelledAgain()
    {
        var state = Threatened();
        state = state.WithUnit(state.Find("captain")! with { Armor = new ArmorMark("test_earth_armor", 10, 2, 1) });

        var plan = HeuristicPlayer.PlanUnit(state, Armoring, state.Find("pell")!, out _);

        Assert.DoesNotContain(plan, c => c is UseItem { Slot: 0, TargetId: "captain" });
    }
}
