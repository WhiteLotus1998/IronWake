using Ironwake.Cli;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// Issue 1308: the Item and Exit verbs reached with the mouse. Item rows in the action list,
/// a target pick a board click aims, the exit row with the console's warning, and the parity
/// gate driving those lines through the rows and clicks rather than straight into the resolver.
/// </summary>
[Collection("console")]
public class ClientItemExitTests
{
    private static string Repo => Directory.GetParent(Fixture.RealContentDirectory())!.FullName;

    private static GameContent Content => ContentLoader.Load(Fixture.RealContentDirectory());

    private static string SamplePath(string name) => Path.Combine(Repo, "docs", "samples", name + ".map");

    private static string MapPath(string name) => Path.Combine(Fixture.RealContentDirectory(), "maps", name + ".map");

    private static ClientSession Open(string path, ulong seed, Func<BattleState, BattleState>? edit = null)
    {
        var content = Content;
        var state = BattleState.From(MapFiles.Load(path, content), content, content.Cast, seed);
        return new ClientSession(content, edit is null ? state : edit(state));
    }

    private static string ConsoleLog(string path, ulong seed, string script)
    {
        var log = Path.Combine(Path.GetTempPath(), $"ironwake-parity-{Guid.NewGuid():N}.log");
        try
        {
            ConsoleCapture.Run(() => PlaySession.Run(new[]
            {
                path, "--seed", seed.ToString(), "--script", script,
                "--content", Fixture.RealContentDirectory(), "--log", log,
            }));
            return File.ReadAllText(log);
        }
        finally
        {
            File.Delete(log);
        }
    }

    [Theory]
    [InlineData("sample", "harrow_weir_orders", 85UL, "docs/transcripts/2026-10-01-harrow_weir_orders-85.script", " uses ")]
    [InlineData("map", "the_mill", 635UL, "tests/parity/clicks/the_mill-635.script", " uses Salve on captain")]
    [InlineData("map", "brackwater_cut", 53UL, "tests/parity/brackwater_cut-53.script", " leaves through the exit")]
    public void ClientEventLogMatchesTheConsoleWithItemsAndExitsTakenByClicks(string kind, string name, ulong seed, string script, string mark)
    {
        var path = kind == "map" ? MapPath(name) : SamplePath(name);
        var file = Path.Combine(Repo, script);
        var console = ConsoleLog(path, seed, file);
        var client = Open(path, seed);
        Script.ApplyByClicks(client, File.ReadAllText(file));

        Assert.Contains(mark, console);
        Assert.Null(Parity.FirstDifference(console, client.LogText));
    }

    private static BattleState Wound(BattleState state, string id, Coord at, int hp) => state.WithUnit(state.Find(id)! with { At = at, Hp = hp });

    private static ClientSession Mill(Func<BattleState, BattleState>? edit = null) => Open(MapPath("the_mill"), 635, edit);

    [Fact]
    public void AHealRowArmsAPickOfTheTilesTheCoreTakesAndAClickThereHeals()
    {
        var client = Mill(s => Wound(s, "captain", new Coord(7, 8), 10));
        client.Select(new Coord(7, 9));

        var row = Assert.Single(client.Actions(), r => r.Label == "Item: Salve");
        Assert.Equal("2: Salve x8", row.Line);
        Assert.True(row.Legal);
        var pick = Assert.IsType<ItemPick>(row.Pick);
        Assert.Equal(new Dictionary<Coord, string> { [new Coord(7, 8)] = "captain" }, pick.Targets);

        Assert.Null(client.TakeAction(client.Actions().ToList().IndexOf(row)));
        Assert.Same(pick, client.Pick);
        Assert.Contains("Maud uses Salve on captain (7 left)", client.PickPreview(new Coord(7, 8)));
        Assert.DoesNotContain(client.PickPreview(new Coord(7, 8)), line => line.Contains("exp", StringComparison.Ordinal));
        Assert.Empty(client.PickPreview(new Coord(3, 3)));

        Assert.Equal(new UseItem("maud", 1, "captain"), client.Click(new Coord(7, 8)));
        Assert.Null(client.Pick);
        Assert.True(client.State.Find("captain")!.Hp > 10);
    }

    [Fact]
    public void AClickOffThePickClosesItAndUsesNothing()
    {
        var client = Mill(s => Wound(s, "captain", new Coord(7, 8), 10));
        client.Select(new Coord(7, 9));
        client.TakeAction(client.Actions().ToList().FindIndex(r => r.Label == "Item: Salve"));

        Assert.Null(client.Click(new Coord(3, 3)));

        Assert.Null(client.Pick);
        Assert.Contains("no target there", client.Status);
        Assert.Equal(10, client.State.Find("captain")!.Hp);
        Assert.Equal("maud", client.Selected);
    }

    [Fact]
    public void EscapeClosesAnArmedPick()
    {
        var client = Mill(s => Wound(s, "captain", new Coord(7, 8), 10));
        client.Select(new Coord(7, 9));
        client.TakeAction(client.Actions().ToList().FindIndex(r => r.Label == "Item: Salve"));

        // The renderer passes an armed pick to Esc as it passes an open attack menu.
        Assert.Equal(EscapeAction.CloseAttackMenu, Screens.Escape(client.Menu is not null || client.Pick is not null, true, false, false));
        client.CloseMenu();

        Assert.Null(client.Pick);
        Assert.Equal("maud", client.Selected);
    }

    [Fact]
    public void AHealRowWithNobodyWoundedInReachIsGreyed()
    {
        var client = Mill();
        client.Select(new Coord(7, 9));

        var row = Assert.Single(client.Actions(), r => r.Label == "Item: Salve");

        Assert.Equal("no target in reach", row.Refusal);
        Assert.Null(client.TakeAction(client.Actions().ToList().IndexOf(row)));
        Assert.Null(client.Pick);
    }

    [Fact]
    public void AnArtRowIsListedOnlyWhenTheCoreTakesTheArt()
    {
        // Unasked needs Maud's Psalter carried; without it the core refuses it, so no row offers it.
        var client = Mill(s => Wound(s, "captain", new Coord(7, 8), 10));
        client.Select(new Coord(7, 9));

        Assert.DoesNotContain(client.Actions(), r => r.Label.Contains("Unasked", StringComparison.Ordinal));
    }

    [Fact]
    public void ASelfHealItemAppliesFromItsRowAndIsGreyedAtFullHp()
    {
        var full = Mill();
        full.Select(new Coord(1, 8));
        var greyed = Assert.Single(full.Actions(), r => r.Label == "Item: Field Dressing");
        Assert.Null(greyed.Pick);
        Assert.Contains("full HP", greyed.Refusal);

        var hurt = Mill(s => Wound(s, "captain", new Coord(1, 8), 10));
        hurt.Select(new Coord(1, 8));
        var index = hurt.Actions().ToList().FindIndex(r => r.Label == "Item: Field Dressing");

        Assert.Equal(new UseItem("captain", 1), hurt.TakeAction(index));
        Assert.True(hurt.State.Find("captain")!.Hp > 10);
    }

    [Fact]
    public void AWeaponHasNoItemRow()
    {
        var client = Mill();
        client.Select(new Coord(1, 8));

        Assert.DoesNotContain(client.Actions(), r => r.Label == "Item: Iron Sword");
    }

    [Fact]
    public void AUnitThatHasActedHasNoItemRows()
    {
        // A unit owed a Fall back move is still selectable after acting; its item rows are gone.
        var client = Mill(s => s.WithUnit(s.Find("maud")! with { Moved = true, Acted = true, FallingBack = true }));
        client.Select(new Coord(7, 9));
        Assert.Equal("maud", client.Selected);

        Assert.DoesNotContain(client.Actions(), r => r.Label.StartsWith("Item:", StringComparison.Ordinal));
    }

    [Fact]
    public void TheCaptainsExitRowNamesWhoIsLeftBehindInTheConsolesWords()
    {
        var path = MapPath("brackwater_cut");
        var onExit = Open(path, 53, s => s.WithUnit(s.Find("captain")! with { At = new Coord(19, 4) }));
        onExit.Select(new Coord(19, 4));

        var row = Assert.Single(onExit.Actions(), r => r.Label == "Exit");
        Assert.Equal(PlaySession.ExitLine(onExit.State, onExit.Content, "captain", campaign: false), row.Line);
        Assert.StartsWith("Exit: leaves ", row.Line);
        Assert.EndsWith(" behind", row.Line);
        Assert.True(row.Legal);
        Assert.Equal(new Exit("captain"), onExit.TakeAction(onExit.Actions().ToList().IndexOf(row)));
    }

    [Fact]
    public void InACampaignTheExitRowSaysLeftBehindCountsAsFallen()
    {
        var content = Content;
        var state = BattleState.From(MapFiles.Load(MapPath("brackwater_cut"), content), content, content.Cast, 53);
        var client = new ClientSession(content, state.WithUnit(state.Find("captain")! with { At = new Coord(19, 4) })) { Campaign = true };
        client.Select(new Coord(19, 4));

        Assert.EndsWith(" (left behind counts as fallen)", Assert.Single(client.Actions(), r => r.Label == "Exit").Line);
    }

    [Fact]
    public void NoExitRowOffAnExitTile()
    {
        var client = Open(MapPath("brackwater_cut"), 53);
        client.Select(client.State.Find("captain")!.At);

        Assert.DoesNotContain(client.Actions(), r => r.Label == "Exit");
    }

    [Fact]
    public void AnExitRowAfterAMoveIsGreyedWithTheCoresRefusal()
    {
        var client = Open(MapPath("brackwater_cut"), 53, s => s.WithUnit(s.Find("captain")! with { At = new Coord(19, 4), Moved = true }));
        client.Select(new Coord(19, 4));

        var row = Assert.Single(client.Actions(), r => r.Label == "Exit");

        Assert.False(row.Legal);
        Assert.Contains("moved this turn", row.Refusal);
    }
}
