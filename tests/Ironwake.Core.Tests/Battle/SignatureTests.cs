using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The cadets' signatures (DESIGN.md 13.18, experiment; issue 486 as rounds 117, 118 and 124
/// specify it), behind a map's <c>signatures: on</c> header: Wren counts tiles (Canto after any
/// action) and talks (a combat she is in wakes a group at 8); Teodor orders (an ally within 2
/// acting after him strikes at +5) and is watched (his own strike with an ally within 2 is at
/// -10); Ottilie keeps a ledger (a strike or watch shot under 65 displayed is refused). Every
/// rule is falsified by the same board without the header.
/// </summary>
public class SignatureTests
{
    private static Unit Cast(string id) => Starter.Cast.Single(u => u.Id == id);

    private static readonly ValueList<Unit> Cadets = ValueList<Unit>.Of(Hale, Cast("wren"), Cast("teodor"), Cast("ottilie"));

    private static string Field(bool signatures, string rows, string units, bool overwatch = false)
    {
        var headers = (overwatch ? "overwatch: on\n" : "") + (signatures ? "signatures: on\n" : "");
        return $"name: Field\nsize: {rows.Split('\n')[0].Trim().Length}x{rows.Split('\n').Length}\nwin: rout\nturn_limit: 10\nrecall: 3\nenemy_level: 1\n"
            + headers + "\n" + rows + "\n\nunits:\n" + units + "\n";
    }

    private static BattleState Start(bool signatures, string rows, string units, bool overwatch = false) =>
        BattleFixture.Start(7, Cadets, Field(signatures, rows, units, overwatch));

    private static ApplyResult Step(BattleState state, Command command)
    {
        var result = state.Try(command);
        Assert.True(result.Accepted, result.Rejection?.Message);
        return result;
    }

    private static int Hit(BattleState state, string unit, string target) =>
        Queries.Forecast(state, Starter, state.Find(unit)!, state.Find(target)!)!.Attacker.HitChance;

    [Fact]
    public void TheCastFileGivesTheThreeCadetsTheirSignaturesAndNobodyElse()
    {
        Assert.Equal(SignatureKind.Counting, Starter.Signatures["wren"]);
        Assert.Equal(SignatureKind.Orders, Starter.Signatures["teodor"]);
        Assert.Equal(SignatureKind.Ledger, Starter.Signatures["ottilie"]);
        Assert.Equal(3, Starter.Signatures.Count);
    }

    [Fact]
    public void TheSignaturesHeaderIsOffByDefaultAndRoundTrips()
    {
        var off = MapFixture.Parse(Field(false, ".....", "P captain 0,0\nE brigand 4,0 group:a behavior:hold"));
        var on = MapFixture.Parse(Field(true, ".....", "P captain 0,0\nE brigand 4,0 group:a behavior:hold"));

        Assert.False(off.SignaturesEnabled);
        Assert.True(on.SignaturesEnabled);
        Assert.Equal(on, MapFormat.Parse("again.map", MapFormat.Write(on, Starter), Starter) with { Name = on.Name });
    }

    [Fact]
    public void ASignatureIsReadOnlyForAPlayerUnitOnASignaturesMap()
    {
        const string rows = ".....";
        const string units = "P captain 0,0\nP recruit:wren 1,0\nE brigand 4,0 group:a behavior:hold";

        Assert.Equal(SignatureKind.Counting, Signatures.Of(Start(true, rows, units), Starter, Start(true, rows, units).Find("wren")!));
        Assert.Null(Signatures.Of(Start(false, rows, units), Starter, Start(false, rows, units).Find("wren")!));
        Assert.Null(Signatures.Of(Start(true, rows, units), Starter, Start(true, rows, units).Find("hale")!));
    }

    // Wren: counts tiles.

    private const string Road = """
        ............
        ............
        ............
        """;

    private const string WrenLine = """
        P captain 0,0
        P recruit:wren 1,1
        E brigand 3,1 group:road behavior:hold
        """;

    [Fact]
    public void WrenCantosAfterAnActionOnTheMovHerMoveLeft()
    {
        var state = Start(true, Road, WrenLine);
        state = Step(state, new Move("wren", new Coord(2, 1))).Next;
        state = Step(state, new Attack("wren", "brigand-1")).Next;

        Assert.Equal(3, state.Find("wren")!.Canto);
        var cantoed = Step(state, new Canto("wren", new Coord(2, 0)));
        Assert.Equal(new Coord(2, 0), cantoed.Next.Find("wren")!.At);
    }

    [Fact]
    public void WrenHasNoCantoWithoutTheHeader()
    {
        var state = Start(false, Road, WrenLine);
        state = Step(state, new Move("wren", new Coord(2, 1))).Next;
        state = Step(state, new Attack("wren", "brigand-1")).Next;

        Assert.Null(state.Find("wren")!.Canto);
        Assert.Equal(RejectionReason.NoCanto, state.Refused(new Canto("wren", new Coord(2, 0))).Reason);
    }

    // Wren: talks. The watch group sleeps 7 from the brigand's tile and 8 from Wren's, past the
    // content's noise radius (6) and inside hers (8); no player unit comes within the wake radius.

    private const string TalkLine = """
        P captain 0,0
        P recruit:wren 1,1
        P recruit:teodor 0,2
        E brigand 2,1 group:road behavior:hold
        E soldier 9,1 group:watch behavior:guard
        """;

    [Fact]
    public void ACombatWrenFightsWakesASleepingGroupAtEight()
    {
        var state = Start(true, Road, TalkLine);
        Assert.Equal(6, Starter.NoiseRadius);
        Assert.Equal(7, new Coord(9, 1).DistanceTo(new Coord(2, 1)));

        var fought = Step(state, new Attack("wren", "brigand-1"));

        Assert.Contains(new GroupWoke("watch", WakeCause.Noise), fought.Events);
        Assert.True(fought.Next.IsAwake("watch"));
    }

    [Fact]
    public void TheSameCombatWithoutTheHeaderLeavesTheGroupAsleep()
    {
        var fought = Step(Start(false, Road, TalkLine), new Attack("wren", "brigand-1"));

        Assert.DoesNotContain(fought.Events, e => e is GroupWoke { Group: "watch" });
        Assert.False(fought.Next.IsAwake("watch"));
    }

    [Fact]
    public void ACombatWithoutWrenMakesNoiseAtTheContentsRadius()
    {
        var line = TalkLine.Replace("P recruit:wren 1,1", "P recruit:wren 0,1").Replace("P recruit:teodor 0,2", "P recruit:teodor 1,1");
        var fought = Step(Start(true, Road, line), new Attack("teodor", "brigand-1"));

        Assert.False(fought.Next.IsAwake("watch"));
    }

    [Fact]
    public void WrenTalksOnEitherSideOfTheCombat()
    {
        var state = Start(true, Road, TalkLine);
        var wren = state.Find("wren")!;
        var brigand = state.Find("brigand-1")!;

        Assert.Equal(Signatures.TalkRadius, Signatures.NoiseRadius(state, Starter, wren, brigand));
        Assert.Equal(Signatures.TalkRadius, Signatures.NoiseRadius(state, Starter, brigand, wren));
        Assert.Equal(Starter.NoiseRadius, Signatures.NoiseRadius(state, Starter, state.Find("teodor")!, brigand));
    }

    // Teodor: orders and watched.

    private const string Yard = """
        ........
        ........
        ........
        ........
        """;

    /// <summary>Teodor at 1,1, Wren at 2,2 (within 2), the brigand at 3,2 beside Wren, the captain far off.</summary>
    private const string OrdersLine = """
        P captain 0,3
        P recruit:teodor 1,1
        P recruit:wren 2,2
        E brigand 3,2 group:yard behavior:hold
        """;

    [Fact]
    public void AnAllyWithinTwoActingAfterTeodorStrikesAtPlusFive()
    {
        var on = Step(Start(true, Yard, OrdersLine), new Wait("teodor")).Next;
        var off = Step(Start(false, Yard, OrdersLine), new Wait("teodor")).Next;

        Assert.True(Hit(off, "wren", "brigand-1") <= 95);
        Assert.Equal(Hit(off, "wren", "brigand-1") + Signatures.OrdersHit, Hit(on, "wren", "brigand-1"));
        Assert.Equal("teodor", Signatures.OrderedBy(on, Starter, on.Find("wren")!)!.Id);
    }

    [Fact]
    public void AnAllyActingBeforeTeodorHasNoOrders()
    {
        var on = Start(true, Yard, OrdersLine);
        var off = Start(false, Yard, OrdersLine);

        Assert.Equal(Hit(off, "wren", "brigand-1"), Hit(on, "wren", "brigand-1"));
    }

    [Fact]
    public void AnAllyBeyondTwoOfTeodorHasNoOrders()
    {
        var line = OrdersLine.Replace("P recruit:teodor 1,1", "P recruit:teodor 0,0");
        var on = Step(Start(true, Yard, line), new Wait("teodor")).Next;
        var off = Step(Start(false, Yard, line), new Wait("teodor")).Next;

        Assert.True(new Coord(0, 0).DistanceTo(new Coord(2, 2)) > Signatures.OrdersRadius);
        Assert.Equal(Hit(off, "wren", "brigand-1"), Hit(on, "wren", "brigand-1"));
    }

    [Fact]
    public void TeodorsOrdersNeverTouchACounter()
    {
        var state = Step(Start(true, Yard, OrdersLine), new Wait("teodor")).Next;

        Assert.Equal(0, Signatures.StrikeHit(state, Starter, state.Find("wren")!, countering: true));
    }

    /// <summary>Teodor beside the brigand at 3,1 with Wren at 2,2, within 2 of him.</summary>
    private const string WatchedLine = """
        P captain 0,3
        P recruit:teodor 2,1
        P recruit:wren 2,2
        E brigand 3,1 group:yard behavior:hold
        """;

    [Fact]
    public void TeodorsOwnStrikeWithAnAllyWithinTwoIsAtMinusTen()
    {
        var on = Start(true, Yard, WatchedLine);
        var off = Start(false, Yard, WatchedLine);

        Assert.True(Hit(off, "teodor", "brigand-1") >= Signatures.WatchedHit);
        Assert.Equal(Hit(off, "teodor", "brigand-1") - Signatures.WatchedHit, Hit(on, "teodor", "brigand-1"));
        Assert.True(Signatures.Watched(on, Starter, on.Find("teodor")!));
    }

    [Fact]
    public void TeodorAloneStrikesAtHisOwnHit()
    {
        var line = WatchedLine.Replace("P recruit:wren 2,2", "P recruit:wren 6,3");
        var on = Start(true, Yard, line);
        var off = Start(false, Yard, line);

        Assert.False(Signatures.Watched(on, Starter, on.Find("teodor")!));
        Assert.Equal(Hit(off, "teodor", "brigand-1"), Hit(on, "teodor", "brigand-1"));
    }

    [Fact]
    public void TeodorsCounterStandsWhileWatched()
    {
        var on = Step(Step(Start(true, Yard, WatchedLine), new Wait("teodor")).Next, new Wait("wren")).Next;
        on = Step(Step(on, new Wait("hale")).Next, new EndPhase()).Next;
        var off = Step(Step(Start(false, Yard, WatchedLine), new Wait("teodor")).Next, new Wait("wren")).Next;
        off = Step(Step(off, new Wait("hale")).Next, new EndPhase()).Next;

        var counterOn = Queries.Forecast(on, Starter, on.Find("brigand-1")!, on.Find("teodor")!)!.Defender;
        var counterOff = Queries.Forecast(off, Starter, off.Find("brigand-1")!, off.Find("teodor")!)!.Defender;

        Assert.True(counterOn.Strikes);
        Assert.Equal(counterOff.HitChance, counterOn.HitChance);
    }

    // Ottilie: the ledger. The brigand stands on the mountain two tiles off; the soldier on plain.

    private const string Hills = """
        ......
        ......
        ....M.
        ......
        ......
        """;

    private const string LedgerLine = """
        P captain 0,0
        P recruit:ottilie 2,2
        E brigand 4,2 group:hill behavior:hold
        E soldier 2,4 group:hill behavior:hold
        """;

    [Fact]
    public void OttilieRefusesAStrikeUnderFiftyDisplayedWithBothReasonsNamed()
    {
        var state = Start(true, Hills, LedgerLine);
        var shown = Queries.Forecast(state, Starter, state.Find("ottilie")!, state.Find("brigand-1")!)!.Attacker.DisplayedHit;
        Assert.True(shown < Signatures.LedgerFloor, $"displayed {shown}");

        var refused = state.Refused(new Attack("ottilie", "brigand-1"));

        Assert.Equal(RejectionReason.SignatureRefused, refused.Reason);
        Assert.Contains(shown.ToString(System.Globalization.CultureInfo.InvariantCulture), refused.Message);
        Assert.Contains("under 65", refused.Message);
    }

    [Fact]
    public void OttilieStrikesAtFiftyOrMore()
    {
        var state = Start(true, Hills, LedgerLine);
        Assert.True(Queries.Forecast(state, Starter, state.Find("ottilie")!, state.Find("soldier-1")!)!.Attacker.DisplayedHit >= Signatures.LedgerFloor);

        Step(state, new Attack("ottilie", "soldier-1"));
    }

    [Fact]
    public void WithoutTheHeaderOttilieTakesTheShotUnderFifty()
    {
        Step(Start(false, Hills, LedgerLine), new Attack("ottilie", "brigand-1"));
    }

    /// <summary>Issue 611: Aimed Shot's +20 hit is a way past the ledger, so the art's strike on the target the plain shot is refused is legal.</summary>
    [Fact]
    public void AnAimedShotThatLiftsTheShownHitPastTheLedgerIsLegal()
    {
        var state = Start(true, Hills, LedgerLine);

        Assert.Equal(RejectionReason.SignatureRefused, state.Refused(new Attack("ottilie", "brigand-1")).Reason);
        Assert.Contains(new Attack("ottilie", "brigand-1", null, "aimed_shot"), Resolver.Legal(state, Starter).OfType<Attack>());
    }

    [Fact]
    public void TheLegalListLeavesOutTheRefusedStrikeAndKeepsTheOther()
    {
        var state = Start(true, Hills, LedgerLine);
        var attacks = Resolver.Legal(state, Starter).OfType<Attack>().Where(a => a.UnitId == "ottilie" && a.Art is null).Select(a => a.TargetId).ToList();

        Assert.Equal(new[] { "soldier-1" }, attacks);
        Assert.Contains("brigand-1", Resolver.Legal(Start(false, Hills, LedgerLine), Starter).OfType<Attack>().Where(a => a.UnitId == "ottilie").Select(a => a.TargetId));
    }

    [Fact]
    public void OttiliesForecastStillAnswersAndNamesTheRefusal()
    {
        var state = Start(true, Hills, LedgerLine);
        var ottilie = state.Find("ottilie")!;
        var brigand = state.Find("brigand-1")!;
        var forecast = Queries.Forecast(state, Starter, ottilie, brigand)!;

        var text = PlaySession.ForecastText(state, Starter, ottilie, brigand, forecast, ottilie.At, fromTile: false);

        Assert.Contains($"  Signature: Ottilie refuses this strike: {forecast.Attacker.DisplayedHit} is under 65", text);
    }

    // The ledger and the orders interlock (issue 540): a soldier on a fort reads 60 alone and 68
    // once Teodor has acted within 2 of her, so the hard shot is his to unlock.

    private const string FortRows = """
        ......
        ......
        ....F.
        ......
        ......
        """;

    private const string FortLine = """
        P captain 0,0
        P recruit:teodor 2,1
        P recruit:ottilie 2,2
        E soldier 4,2 group:fort behavior:hold
        """;

    [Fact]
    public void TeodorsOrdersLiftOttiliesRefusedShotOverTheLedger()
    {
        var alone = Start(true, FortRows, FortLine);
        var shownAlone = Queries.Forecast(alone, Starter, alone.Find("ottilie")!, alone.Find("soldier-1")!)!.Attacker.DisplayedHit;
        Assert.InRange(shownAlone, Signatures.LedgerFloor - Signatures.OrdersHit, Signatures.LedgerFloor - 1);
        Assert.Equal(RejectionReason.SignatureRefused, alone.Refused(new Attack("ottilie", "soldier-1")).Reason);

        var ordered = Step(alone, new Wait("teodor")).Next;
        Assert.True(Queries.Forecast(ordered, Starter, ordered.Find("ottilie")!, ordered.Find("soldier-1")!)!.Attacker.DisplayedHit >= Signatures.LedgerFloor);

        Step(ordered, new Attack("ottilie", "soldier-1"));
    }

    private const string WatchLine = """
        P captain 0,0
        P recruit:ottilie 2,2
        E brigand 5,2 group:hill behavior:hold
        E soldier 2,5 group:hill behavior:hold
        """;

    private const string WatchRows = """
        ......
        ......
        ....M.
        ......
        ......
        ......
        """;

    [Fact]
    public void OttiliesWatchHoldsAShotUnderFiftyAndStaysForTheNextArrival()
    {
        var state = Start(true, WatchRows, WatchLine, overwatch: true);
        state = Step(Step(Step(state, new Watch("ottilie")).Next, new Wait("hale")).Next, new EndPhase()).Next;

        var held = Step(state, new Move("brigand-1", new Coord(4, 2)));

        var hold = Assert.Single(held.Events.OfType<WatchHeld>());
        Assert.Equal(("ottilie", "brigand-1"), (hold.UnitId, hold.TargetId));
        Assert.True(hold.Hit < Signatures.LedgerFloor);
        Assert.Empty(held.Events.OfType<WatchFired>());
        Assert.True(held.Next.Find("ottilie")!.Watching);

        var shot = Step(held.Next, new Move("soldier-1", new Coord(2, 4)));
        Assert.Single(shot.Events.OfType<WatchFired>());
    }

    [Fact]
    public void WithoutTheHeaderOttiliesWatchFiresUnderFifty()
    {
        var state = Start(false, WatchRows, WatchLine, overwatch: true);
        state = Step(Step(Step(state, new Watch("ottilie")).Next, new Wait("hale")).Next, new EndPhase()).Next;

        var moved = Step(state, new Move("brigand-1", new Coord(4, 2)));

        Assert.Single(moved.Events.OfType<WatchFired>());
    }

    // On screen.

    [Fact]
    public void TheForecastNamesTeodorsOrdersAndHisWatchedPenalty()
    {
        var ordered = Step(Start(true, Yard, OrdersLine), new Wait("teodor")).Next;
        var wren = ordered.Find("wren")!;
        var brigand = ordered.Find("brigand-1")!;
        var text = PlaySession.ForecastText(ordered, Starter, wren, brigand, Queries.Forecast(ordered, Starter, wren, brigand)!, wren.At, fromTile: false);
        Assert.Contains("  Signature: Teodor's orders: Wren acc +5", text);

        var watched = Start(true, Yard, WatchedLine);
        var teodor = watched.Find("teodor")!;
        var target = watched.Find("brigand-1")!;
        var own = PlaySession.ForecastText(watched, Starter, teodor, target, Queries.Forecast(watched, Starter, teodor, target)!, teodor.At, fromTile: false);
        Assert.Contains("  Signature: Teodor acc -10 (ally within 2)", own);

        var plain = Start(false, Yard, WatchedLine);
        Assert.DoesNotContain("signature", PlaySession.ForecastText(plain, Starter, plain.Find("teodor")!, plain.Find("brigand-1")!, Queries.Forecast(plain, Starter, plain.Find("teodor")!, plain.Find("brigand-1")!)!, plain.Find("teodor")!.At, fromTile: false));
    }

    [Fact]
    public void ShowAndTheLegendPrintTheSignatureAndWrensRadius()
    {
        var state = Start(true, Yard, OrdersLine);

        Assert.Contains($"  Signature: {Signatures.Describe(SignatureKind.Orders)}", PlaySession.ShowLines(state, Starter, state.Find("teodor")!));
        Assert.DoesNotContain(PlaySession.ShowLines(state, Starter, state.Find("hale")!), l => l.Contains("signature"));
        Assert.DoesNotContain(PlaySession.ShowLines(Start(false, Yard, OrdersLine), Starter, state.Find("teodor")!), l => l.Contains("signature"));

        var board = MapRenderer.Render(state, Starter);
        Assert.Contains(MapRenderer.SignaturesLegend, board);
        Assert.Contains("  wren: counts tiles", board);
        Assert.Contains("wakes a sleeping group within 8", board);
    }

    /// <summary>
    /// Issue 486: the signature sample is the brace sample (<c>docs/samples/saltmarsh_ford_brace.map</c>,
    /// the 0030 board with its stationary boss) with only <c>signatures: on</c> added, and canonical.
    /// </summary>
    [Fact]
    public void TheSignatureSampleIsTheBraceSampleWithOnlyTheSignaturesHeaderAdded()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var bracePath = Path.Combine(repo, "docs", "samples", "saltmarsh_ford_brace.map");
        var samplePath = Path.Combine(repo, "docs", "samples", "saltmarsh_ford_brace_signatures.map");
        var brace = File.ReadAllText(bracePath).Replace("\r\n", "\n").Split('\n').ToList();
        var sampleText = File.ReadAllText(samplePath).Replace("\r\n", "\n");
        var sampleLines = sampleText.Split('\n').ToList();

        var at = sampleLines.IndexOf("signatures: on");
        Assert.True(at >= 0);
        sampleLines.RemoveAt(at);
        Assert.Equal(brace, sampleLines);

        var sample = MapFiles.Load(samplePath, MapFixture.Content);
        Assert.Equal(MapFiles.Load(bracePath, MapFixture.Content) with { SignaturesEnabled = true }, sample);
        Assert.Equal(sampleText, MapFormat.Write(sample, Starter));
    }
}
