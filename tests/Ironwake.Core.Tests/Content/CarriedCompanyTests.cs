using Ironwake.Content;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The curve's <c>carried</c> and <c>carried, spread</c> lines (issue 764): the file's party at the
/// levels the heuristic's own campaign brings to a map's camp, slot by slot, and the same total
/// spread evenly; 0161's bend reads <c>carried</c>.
/// </summary>
public class CarriedCompanyTests
{
    private static readonly string Root = Fixture.RealContentDirectory();
    private static readonly GameContent Shipped = ContentLoader.Load(Root);

    private static LevelRun.Camp Camp(params int[] deployed) => new(1, deployed, 0, 0);

    [Fact]
    public void CarriedIsTheMedianOfEachPlaceWithTheDeployedSortedHighestFirst()
    {
        var camps = new[] { Camp(1, 6, 2), Camp(7, 1, 1), Camp(1, 1, 5) };

        Assert.Equal([6, 1, 1], LevelRun.SlotLevels(camps, 3));
    }

    [Fact]
    public void APlaceOnlyCampsThatFilledItVoteOnAndAnEmptyPlaceReadsZero()
    {
        var camps = new[] { Camp(4), Camp(5, 3), Camp(6, 3) };

        Assert.Equal([5, 3, 0], LevelRun.SlotLevels(camps, 3));
    }

    [Theory]
    [InlineData(new[] { 7, 2, 1, 1, 1 }, new[] { 3, 3, 2, 2, 2 })]
    [InlineData(new[] { 6, 1, 1, 1, 1, 1 }, new[] { 2, 2, 2, 2, 2, 1 })]
    [InlineData(new[] { 2, 2 }, new[] { 2, 2 })]
    [InlineData(new int[0], new int[0])]
    public void SpreadKeepsTheTotalEvenWithTheRemainderToTheTopPlaces(int[] carried, int[] spread)
    {
        var result = LevelRun.Spread(carried);

        Assert.Equal(spread, result);
        Assert.Equal(carried.Sum(), result.Sum());
    }

    private static MapDefinition Tollgate() => MapFiles.Load(MapFiles.CampaignPath(Root, Shipped, "the_tollgate"), Shipped);

    [Fact]
    public void TheCaptainTakesTheTopPlaceAndTheRestFollowInPlacementOrder()
    {
        var map = Tollgate();
        var deployed = BattleState.From(map, Shipped, Shipped.Cast, 1).UnitsOf(Side.Player)
            .OrderByDescending(u => u.IsCaptain).ThenBy(u => u.PlacementIndex).Select(u => u.Id).ToList();
        var levels = Enumerable.Range(0, deployed.Count).Select(k => deployed.Count + 1 - k).ToList();

        var (company, applied) = Program.AtSlotLevels(Shipped, map, levels);

        Assert.Equal(levels, applied);
        var raised = BattleState.From(map, company, company.Cast, 1).UnitsOf(Side.Player).ToDictionary(u => u.Id, u => u.Unit.Level);
        for (var k = 0; k < deployed.Count; k++)
        {
            Assert.Equal(levels[k], raised[deployed[k]]);
        }

        Assert.True(raised[deployed[0]] > Shipped.Cast[0].Level);
        Assert.Equal(Shipped.Cast[0].Id, deployed[0]);
    }

    [Fact]
    public void APlaceBelowAUnitsOwnLevelOrReadingZeroKeepsTheUnitsLevel()
    {
        var map = Tollgate();
        var captain = Shipped.Cast[0];
        var lifted = captain.AtLevel(4, Shipped.Class(captain.ClassId));
        var content = Shipped with { Cast = Shipped.Cast.SetItem(0, lifted), Units = Shipped.Units.ContainsKey(lifted.Id) ? Shipped.Units.SetItem(lifted.Id, lifted) : Shipped.Units };

        var (_, applied) = Program.AtSlotLevels(content, map, [2, 0]);

        Assert.Equal(4, applied[0]);
        Assert.Equal(1, applied[1]);
    }
}
