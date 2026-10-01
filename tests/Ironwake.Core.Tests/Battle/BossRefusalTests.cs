using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// <c>threat</c> names a vetoed boss's refusal (issue 565, DESIGN.md section 8): when the boss
/// veto is why a tile inside a boss's move and reach reads clear, one unpriced row says the boss
/// could reach a striking tile, that it refuses it, and where its plan ends instead.
/// </summary>
public class BossRefusalTests
{
    /// <summary>A field of <paramref name="width"/>x3: the party at the west end, a guard boss in group hall at <paramref name="bossX"/>,1.</summary>
    private static string Field(string win, int players, int width = 9, int bossX = 6) => $"""
        name: Field
        size: {width}x3
        win: {win}
        turn_limit: 10
        recall: 3
        enemy_level: 1

        {new string('.', width)}
        {new string('.', width)}
        {new string('.', width)}

        units:
        P captain 1,1
        {string.Join("\n", new[] { "P recruit 1,0", "P recruit 1,2" }.Take(players - 1))}
        B grange_reeve {bossX},1 group:hall behavior:guard

        """;

    private static readonly ValueList<Unit> Party = ValueList<Unit>.Of(Hale, Wren, Ivo);

    private static BattleState Awake(string win, int width = 9, int bossX = 6) =>
        Start(roster: Party, map: Field(win, Party.Count, width, bossX)).Wake("hall");

    [Fact]
    public void AVetoedBossThatCouldStrikeTheUnitIsNamedAsRefusing()
    {
        var state = Awake("defeat_boss");
        var hale = state.Find("hale")!;

        var line = Assert.Single(Queries.Refusals(state, Starter, hale, hale.At)!);
        Assert.Equal("grange_reeve-1", line.Boss.Id);
        Assert.DoesNotContain(Queries.Threats(state, Starter, hale, hale.At)!, l => l.Enemy.Id == line.Boss.Id);
    }

    [Fact]
    public void TheNamedTileIsOneTheVetoRefusedAndTheEndIsThePlans()
    {
        var board = Awake("defeat_boss").Do(new EndPhase());
        var reeve = board.Find("grange_reeve-1")!;
        var hale = board.Find("hale")!;

        var refusal = EnemyAi.Refusal(board, Starter, reeve, hale);
        Assert.NotNull(refusal);
        Assert.Contains(Enumerable.Range(0, reeve.Unit.Inventory.Count), slot =>
            reeve.UsableWeaponAt(Starter, slot) is { } weapon
            && weapon.InRange(refusal!.Refused.DistanceTo(hale.At))
            && EnemyAi.BossVetoRefuses(board, Starter, reeve, refusal.Refused, hale, slot));
        var end = EnemyAi.PlanUnit(board, Starter, reeve).OfType<Move>().Select(m => m.To).DefaultIfEmpty(reeve.At).Single();
        Assert.Equal(end, refusal!.Ends);
    }

    [Fact]
    public void ABossThatCannotReachTheUnitIsNotNamed()
    {
        var state = Awake("defeat_boss", width: 19, bossX: 18);
        var hale = state.Find("hale")!;

        Assert.Empty(Queries.Refusals(state, Starter, hale, hale.At)!);
        var board = state.Do(new EndPhase());
        Assert.Null(EnemyAi.Refusal(board, Starter, board.Find("grange_reeve-1")!, board.Find("hale")!));
    }

    [Fact]
    public void ASleepingBossIsNotNamed()
    {
        var state = Start(roster: Party, map: Field("defeat_boss", Party.Count));
        var hale = state.Find("hale")!;

        Assert.Empty(Queries.Refusals(state, Starter, hale, hale.At)!);
    }

    [Fact]
    public void ABossOffADefeatBossMapIsNeverNamed()
    {
        var board = Awake("rout").Do(new EndPhase());

        Assert.Null(EnemyAi.Refusal(board, Starter, board.Find("grange_reeve-1")!, board.Find("hale")!));
    }

    [Fact]
    public void ABossWhoseStrikeOnTheUnitPassesIsNotNamed()
    {
        var map = Field("defeat_boss", 1);
        var state = Start(roster: ValueList<Unit>.Of(Unarmed), map: map).Wake("hall");
        var pell = state.Find("pell")!;

        Assert.Empty(Queries.Refusals(state, Starter, pell, pell.At)!);
        Assert.NotEmpty(Queries.Threats(state, Starter, pell, pell.At)!);
    }

    [Fact]
    public void ThreatPrintsTheRefusalAsOneUnpricedRow()
    {
        var state = Awake("defeat_boss");
        var hale = state.Find("hale")!;
        var line = Assert.Single(Queries.Refusals(state, Starter, hale, hale.At)!);

        var text = Ironwake.Cli.PlaySession.ThreatText(state, Starter, hale, hale.At, Queries.Threats(state, Starter, hale, hale.At)!,
            Queries.SleepingThreats(state, Starter, hale, hale.At)!, refusals: Queries.Refusals(state, Starter, hale, hale.At));
        var ends = line.Ends == line.Boss.At ? $"holds {line.Ends}" : $"ends on {line.Ends}";
        Assert.Contains($"  grange_reeve-1 could reach {line.Refused} but refuses it: too exposed there; {ends}", text);
        Assert.Contains("no enemy can strike hale next phase", text);
    }
}
