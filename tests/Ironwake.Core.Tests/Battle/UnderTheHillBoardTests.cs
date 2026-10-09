using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The hill's board at the campaign's level (issue 1386 slice 3c, DECISIONS/0391; Chat's round 548): the company
/// fielded whole, the Kin begun swallowed, and a route to the shard's bearer by turn 2. The route is read on the
/// terrain alone, the slowest infantry Move walked twice from the nearest company slot to a tile beside the bearer.
/// </summary>
public class UnderTheHillBoardTests
{
    /// <summary>The slowest infantry Move a story member can field (the base classes' 4).</summary>
    private const int SlowestMove = 4;

    private static string Sample => Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "under_the_hill_campaign.map");

    private static MapDefinition Board => MapFiles.Load(Sample, MapFixture.Content);

    [Fact]
    public void TheHillFieldsTheWholeCompanyAtTheKeepsLevelWithTheKinSwallowedAndASwornBearer()
    {
        var map = Board;
        Assert.True(map.DeploysAll);
        Assert.Equal(8, map.EnemyLevel);
        var kin = Assert.Single(map.Placements.OfType<EnemyPlacement>(), p => p.IsBoss);
        Assert.Equal(kin.At, map.Swallowed);
        var bearer = Assert.Single(map.Placements.OfType<EnemyPlacement>(), p => p.At == map.KinShard);
        Assert.False(bearer.IsBoss);
    }

    [Fact]
    public void TheHillGivesTheCompanyARouteToTheBearerByTurnTwo()
    {
        var map = Board;
        var bearer = map.KinShard!.Value;
        var beside = new[] { new Coord(bearer.X + 1, bearer.Y), new Coord(bearer.X - 1, bearer.Y), new Coord(bearer.X, bearer.Y + 1), new Coord(bearer.X, bearer.Y - 1) }.ToHashSet();
        var slots = map.Placements.OfType<PlayerPlacement>().Select(p => p.At);
        Assert.InRange(beside.Min(b => slots.Min(s => Walk(map, s, b))), 0, 2 * SlowestMove);
    }

    [Fact]
    public void TheRouteGuardFiresOnABearerWalledOff()
    {
        var walled = MapFixture.Parse("""
            name: Walled
            size: 12x3
            win: defeat_boss
            kin_shard: 1,1
            turn_limit: 10
            recall: 3
            enemy_level: 1

            ............
            ...#........
            ............

            units:
            P captain 11,1
            B hask 0,0 group:kin behavior:guard
            E soldier 1,1 group:sworn behavior:guard
            """, "walled.map");
        Assert.True(Walk(walled, new Coord(11, 1), new Coord(2, 1)) > 2 * SlowestMove);
    }

    /// <summary>The cheapest infantry walk from <paramref name="from"/> to <paramref name="to"/> on the terrain alone, units ignored; int.MaxValue when none.</summary>
    private static int Walk(MapDefinition map, Coord from, Coord to)
    {
        var cost = new Dictionary<Coord, int> { [from] = 0 };
        var open = new PriorityQueue<Coord, int>();
        open.Enqueue(from, 0);
        while (open.TryDequeue(out var at, out var spent))
        {
            if (at == to)
            {
                return spent;
            }

            if (spent > cost[at])
            {
                continue;
            }

            foreach (var next in new[] { new Coord(at.X + 1, at.Y), new Coord(at.X - 1, at.Y), new Coord(at.X, at.Y + 1), new Coord(at.X, at.Y - 1) })
            {
                if (next.X < 0 || next.Y < 0 || next.X >= map.Width || next.Y >= map.Height || map.TerrainAt(next, MapFixture.Content).MoveCost(MovementType.Infantry) is not { } step)
                {
                    continue;
                }

                if (!cost.TryGetValue(next, out var known) || spent + step < known)
                {
                    cost[next] = spent + step;
                    open.Enqueue(next, spent + step);
                }
            }
        }

        return int.MaxValue;
    }
}
