using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 344's constructed position on <c>docs/samples/sallow_grange_shove.map</c> (the shipped
/// Sallow Grange with <c>shove: on</c>): the Reeve has left the throne, the captain stands unmoved
/// at 12,5 with Mov 4, five tiles from 16,6, and Pell can reach 11,5. An ally consents, so Pell
/// throws the captain one tile east whatever the heft, the captain keeps his unmoved flag, and
/// his own move seizes the throne. The same push on an enemy as heavy as the captain is refused.
/// </summary>
public class ShoveSallowTests
{
    private static readonly Coord Throne = new(16, 6);

    private static BattleState Position()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var map = MapFiles.Load(Path.Combine(repo, "docs", "samples", "sallow_grange_shove.map"), MapFixture.Content);
        var state = BattleState.From(map, MapFixture.Content, MapFixture.Content.Cast, 41);
        var reeve = state.UnitsOf(Side.Enemy).Single(u => u.IsBoss);
        state = state.WithUnit(reeve with { At = new Coord(13, 8) });
        state = state.WithUnit(state.Find("captain")! with { At = new Coord(12, 5) });
        return state.WithUnit(state.Find("pell")! with { At = new Coord(9, 5) });
    }

    [Fact]
    public void PellThrowsTheUnmovedCaptainEastAndHisOwnMoveSeizesTheThrone()
    {
        var state = Position();
        Assert.True(state.Map.ShoveEnabled);
        Assert.True(state.Map.IsThrone(Throne));
        Assert.Null(state.UnitAt(Throne));
        Assert.True(Resolver.Heft(MapFixture.Content, state.Find("pell")!) < Resolver.Heft(MapFixture.Content, state.Find("captain")!));

        state = Apply(state, new Move("pell", new Coord(11, 5)));
        state = Apply(state, new Shove("pell", "captain"));

        var captain = state.Find("captain")!;
        Assert.Equal(new Coord(13, 5), captain.At);
        Assert.False(captain.Moved);
        Assert.False(captain.Acted);

        state = Apply(state, new Move("captain", Throne));

        Assert.Equal(BattleResult.Won, state.Outcome.Result);
    }

    [Fact]
    public void TheSamePushOnAnEnemyAsHeavyAsTheCaptainIsRefusedForHeft()
    {
        var state = Position();
        var captain = state.Find("captain")!;
        var reeve = state.UnitsOf(Side.Enemy).Single(u => u.IsBoss);
        state = state.WithUnit(captain with { At = new Coord(0, 0) });
        state = state.WithUnit(reeve with { At = new Coord(12, 5) });
        state = Apply(state, new Move("pell", new Coord(11, 5)));
        reeve = state.Find(reeve.Id)!;
        Assert.True(Resolver.Heft(MapFixture.Content, reeve) >= Resolver.Heft(MapFixture.Content, captain));

        var result = Resolver.Apply(state, MapFixture.Content, new Shove("pell", reeve.Id));

        Assert.False(result.Accepted);
        Assert.Equal(RejectionReason.CannotShove, result.Rejection!.Reason);
        Assert.Contains($"heft {Resolver.Heft(MapFixture.Content, state.Find("pell")!)} is under its {Resolver.Heft(MapFixture.Content, reeve)}", result.Rejection.Message);
    }

    private static BattleState Apply(BattleState state, Command command)
    {
        var result = Resolver.Apply(state, MapFixture.Content, command);
        Assert.True(result.Accepted, result.Rejection?.Message);
        return result.Next;
    }
}
