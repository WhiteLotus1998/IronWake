using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using Ironwake.Sim;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The supports' ceiling (issue 77, slice 5): the Sim's pairing player is the heuristic committed to
/// one support pair. A member that would end its turn away from its partner ends beside it where the
/// tile is no worse, the striker of the pair plans before the idle one, the veto still holds, and the
/// bench is set so both members deploy.
/// </summary>
[Collection("console")]
public class PairingPlayerTests
{
    /// <summary>An 8x5 field: Hale at 0,1, Wren at 0,3, a soldier holding at 3,2, open ground.</summary>
    private const string Field = """
        name: Field
        size: 8x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ........
        ........
        ........
        ........
        ........

        units:
        P captain 0,1
        P recruit:wren 0,3
        E soldier 3,2 group:f behavior:hold

        """;

    private static BattleState Start() => BattleFixture.Start(map: Field);

    private static Coord EndOf(IReadOnlyList<Command> plan, BattleState state, string id) =>
        plan[0] is Move move ? move.To : state.Find(id)!.At;

    [Fact]
    public void APairMemberWhoseWaitEndsAwayFromItsPartnerEndsBesideIt()
    {
        var state = Start();
        var plan = new Command[] { new Move("wren", new Coord(1, 4)), new Wait("wren") };

        var adjusted = PairingPlayer.Adjust(state, Starter, plan, "hale", "wren");

        Assert.IsType<Wait>(adjusted[^1]);
        Assert.Equal(1, EndOf(adjusted, state, "wren").DistanceTo(state.Find("hale")!.At));
    }

    [Fact]
    public void AWaitAlreadyBesideThePartnerIsLeftAlone()
    {
        var state = Start();
        var plan = new Command[] { new Move("wren", new Coord(1, 1)), new Wait("wren") };

        Assert.Equal(plan, PairingPlayer.Adjust(state, Starter, plan, "hale", "wren"));
    }

    [Fact]
    public void AUnitOutsideThePairIsLeftAlone()
    {
        var state = Start();
        var plan = new Command[] { new Move("wren", new Coord(1, 4)), new Wait("wren") };

        Assert.Equal(plan, PairingPlayer.Adjust(state, Starter, plan, "hale", "ivo"));
    }

    [Fact]
    public void AnAttackMovesBesideThePartnerWhenTheSameStrikeScoresNoLowerThere()
    {
        var state = Start().Stood("hale", new Coord(2, 1));
        var plan = new Command[] { new Move("wren", new Coord(3, 3)), new Attack("wren", state.UnitAt(new Coord(3, 2))!.Id) };

        var adjusted = PairingPlayer.Adjust(state, Starter, plan, "hale", "wren");

        Assert.Equal(new Command[] { new Move("wren", new Coord(2, 2)), plan[1] }, adjusted);
    }

    [Fact]
    public void AnAttackNoTileBesideThePartnerCanMakeIsLeftAlone()
    {
        var state = Start();
        var plan = new Command[] { new Move("wren", new Coord(3, 3)), new Attack("wren", state.UnitAt(new Coord(3, 2))!.Id) };

        Assert.Equal(plan, PairingPlayer.Adjust(state, Starter, plan, "hale", "wren"));
    }

    [Fact]
    public void TheCaptainNeverWaitsBesideItsPartnerOnATileTheVetoRefuses()
    {
        var start = BattleFixture.Start(map: Field.Replace("behavior:hold", "behavior:aggressive")).Stood("wren", new Coord(2, 3));
        var state = start.WithUnit(start.Find("hale")! with { Hp = 1 });
        var plan = new Command[] { new Wait("hale") };

        Assert.Equal(plan, PairingPlayer.Adjust(state, Starter, plan, "hale", "wren"));
    }

    [Fact]
    public void TheCaptainWaitsBesideItsPartnerWhenTheVetoPassesThere()
    {
        var state = Start().Stood("wren", new Coord(0, 4));
        var plan = new Command[] { new Wait("hale") };

        var adjusted = PairingPlayer.Adjust(state, Starter, plan, "hale", "wren");

        Assert.Equal(1, EndOf(adjusted, state, "hale").DistanceTo(new Coord(0, 4)));
    }

    [Fact]
    public void TheStrikerOfThePairPlansBeforeTheIdleOne()
    {
        var start = Start();
        var state = start.Stood(start.UnitAt(new Coord(3, 2))!.Id, new Coord(7, 4)).Stood("wren", new Coord(6, 4));
        var heuristic = new HeuristicPlayer().Next(state, Starter);
        Assert.Equal("hale", heuristic[^1] switch { Wait w => w.UnitId, Attack a => a.UnitId, _ => null });
        Assert.IsType<Wait>(heuristic[^1]);

        var plan = new PairingPlayer("hale", "wren").Next(state, Starter);

        Assert.Equal("wren", Assert.IsType<Attack>(plan[^1]).UnitId);
    }

    [Fact]
    public void DeployBenchesSoBothMembersOfThePairFight()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var record = CampaignRecord.Start(content, 7);
        var map = MapFiles.Load(MapFiles.CampaignPath(Fixture.RealContentDirectory(), content, "saltmarsh_ford"), content);
        var deployed = record.Deployment(map, content);
        var left = record.Present(content).Select(u => u.Id).FirstOrDefault(id => !deployed.Contains(id) && content.Campaign.Supports.Any(p => p.Involves(id)));
        Assert.NotNull(left);
        var pair = content.Campaign.Supports.First(p => p.Involves(left!));

        var benched = PairingPlayer.Deploy(record, map, content, pair.A, pair.B);

        Assert.Contains(pair.A, benched.Deployment(map, content));
        Assert.Contains(pair.B, benched.Deployment(map, content));
    }

    [Fact]
    public void TheSimRefusesAPairCampaignJsonDoesNotList()
    {
        var code = 0;
        var output = ConsoleCapture.Run(() => code = Ironwake.Sim.Program.Main(new[] { "--supports", "--seeds", "1", "--pair", "wren", "brannock" }));

        Assert.Equal(1, code);
        Assert.Contains("supports: wren and brannock are not a support pair in campaign.json", output, StringComparison.Ordinal);
    }
}
