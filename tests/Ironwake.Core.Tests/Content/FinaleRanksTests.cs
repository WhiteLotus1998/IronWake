using Ironwake.Content;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The finale's earned ranks (issue 1395, layer 2): <c>--levels --finale-ranks</c> reads each story member's
/// median rank points per type its class uses from the company standing after the map before the keep.
/// </summary>
public class FinaleRanksTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static LevelRun.Run RunWith(int map, params (string Id, WeaponSkill Skill)[] members) =>
        new([(map, members.Select(_ => 1).ToList(), 0, new LevelRun.Camp(1, [1], 0, 0))], null)
        {
            Companies = [members.Select(m => new LevelRun.Member(1, 0, false, false) { Id = m.Id, Skill = m.Skill }).ToList()],
        };

    [Fact]
    public void TheFinaleRanksAreTheLowerMedianOfTheCompaniesBeforeTheKeep()
    {
        var before = Shipped.Campaign.Maps.ToList().FindIndex(m => m.MapId == Shipped.Campaign.Keep.MapId);
        var runs = new[]
        {
            RunWith(before, ("pell", WeaponSkill.Zero.With(WeaponType.Reason, 40))),
            RunWith(before, ("pell", WeaponSkill.Zero.With(WeaponType.Reason, 90))),
            RunWith(before, ("pell", WeaponSkill.Zero.With(WeaponType.Reason, 60)), ("wren", WeaponSkill.Zero.With(WeaponType.Sword, 12))),
            RunWith(before - 1, ("pell", WeaponSkill.Zero.With(WeaponType.Reason, 400))),
        };

        var lines = LevelRun.FinaleRankLines(Shipped, runs).ToList();

        Assert.Contains("  pell: in 3; reason 60 (D)", lines);
        Assert.Contains("  wren: in 1; sword 12 (E)", lines);
        Assert.Contains("  dunstan: in 0; none", lines);
        Assert.Equal("  finaleRanks: { \"wren\": { \"sword\": 12 }, \"pell\": { \"reason\": 60 } }", lines[^1]);
    }
}
