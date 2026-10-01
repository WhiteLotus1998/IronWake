using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The title screen and Options, slice 2 (issue 677): the Options screen's rows cycling through
/// what the profile accepts, instant as a fourth speed, the scenes option read and written as
/// the B cycle's settings, the end-turn confirm, reach on hover, New game's difficulties, the
/// pause menu's way to Options, and the campaign client autosaving at each camp so Continue
/// finds it.
/// </summary>
[Collection("console")]
public class ClientOptionsScreenTests
{
    private static readonly GameContent Content = ContentLoader.Load(Fixture.RealContentDirectory());

    private static ClientSession Brackwater()
    {
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "brackwater_cut.map"), Content);
        return new ClientSession(Content, BattleState.From(map, Content, Content.Cast, 53));
    }

    private static string TempDir() => Path.Combine(Path.GetTempPath(), "ironwake-options-screen-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void TheOptionsRowsAreTheProfilesKeysInScreenOrderAndUiScaleWaits()
    {
        Assert.Equal(
            new[] { "speed", "scenes", "confirm-end-turn", "reach-on-hover", "sound", "volume" },
            OptionsMenu.Rows.Select(r => r.Key));
        Assert.DoesNotContain(OptionsMenu.Rows, r => r.Key == "ui-scale");
    }

    [Theory]
    [InlineData("speed", new[] { "2x", "5x", "instant", "1x" })]
    [InlineData("scenes", new[] { "all", "map", "key" })]
    [InlineData("confirm-end-turn", new[] { "off", "on" })]
    [InlineData("reach-on-hover", new[] { "off", "on" })]
    [InlineData("sound", new[] { "off", "on" })]
    [InlineData("volume", new[] { "100", "0", "20", "40", "60", "80" })]
    public void AClickOnARowStepsItsValueAndWraps(string key, string[] steps)
    {
        var options = new Options();
        foreach (var step in steps)
        {
            options = OptionsMenu.Cycle(options, key);
            Assert.Equal(step, OptionsMenu.Value(options, key));
        }

        Assert.Equal(new Options(), options);
    }

    [Fact]
    public void AVolumeBetweenTheStepsMovesToTheNextStepAbove()
    {
        var options = Options.Set(new Options(), "volume", "35").Options;

        Assert.Equal(40, OptionsMenu.Cycle(options, "volume").Volume);
    }

    [Fact]
    public void ACycledOptionRoundTripsThroughTheProfile()
    {
        var store = new SaveStore(TempDir());
        try
        {
            var options = OptionsMenu.Cycle(OptionsMenu.Cycle(new Options(), "speed"), "reach-on-hover");
            store.WriteOptions(options);

            Assert.Equal(options, new SaveStore(store.Directory).ReadOptions().Options);
        }
        finally
        {
            Directory.Delete(store.Directory, recursive: true);
        }
    }

    [Fact]
    public void TheScenesRowSaysTheSettingAsTheFooterDoes()
    {
        Assert.Equal("key moments", OptionsMenu.Words("scenes", "key"));
        Assert.Equal("map only", OptionsMenu.Words("scenes", "map"));
        Assert.Equal("instant", OptionsMenu.Words("speed", "instant"));
    }

    [Theory]
    [InlineData("key", SceneSetting.KeyMoments)]
    [InlineData("all", SceneSetting.All)]
    [InlineData("map", SceneSetting.MapOnly)]
    public void TheScenesOptionIsTheBCyclesSetting(string value, SceneSetting setting)
    {
        Assert.Equal(setting, Scenes.FromOption(value));
        Assert.Equal(value, Scenes.ToOption(setting));
    }

    [Fact]
    public void EverySpeedTheProfileAcceptsIsAButtonAndInstantIsTheFourth()
    {
        Assert.Equal(Options.Speeds, GameSpeed.Speeds.Select(s => s.Name));
        Assert.Equal(3, GameSpeed.IndexOf("instant"));
        Assert.True(GameSpeed.Speeds[3].Bar);
        Assert.Equal(GameSpeed.Default, GameSpeed.IndexOf("10x"));
    }

    [Fact]
    public void InstantStillPlaysEveryBeatOnlyFasterThanFiveTimes()
    {
        var instant = GameSpeed.Factor(GameSpeed.IndexOf("instant"));

        Assert.True(instant > 0);
        Assert.True(instant < GameSpeed.Factor(GameSpeed.IndexOf("5x")));
    }

    [Fact]
    public void TheEndTurnConfirmNamesWhoHasNotMovedThenTheLethalLinesThenTheAnswer()
    {
        var client = Brackwater();
        var lines = EndTurnConfirm.Lines(client.State, Content, confirmOn: true);

        Assert.NotNull(lines);
        var unmoved = client.State.UnitsOf(Side.Player).Count();
        Assert.StartsWith($"{unmoved} units have not moved: ", lines![0]);
        var lethal = Queries.Lethal(client.State, Content).Select(l => Ironwake.Cli.PlaySession.LethalLine(l, UnitNames.Of(client.State, Content)));
        Assert.Equal(lethal, lines.Skip(1).Take(lines.Count - 2));
        Assert.Equal(EndTurnConfirm.Answer, lines[^1]);
    }

    [Fact]
    public void TheEndTurnConfirmIsSilentWithTheOptionOff()
    {
        Assert.Null(EndTurnConfirm.Lines(Brackwater().State, Content, confirmOn: false));
    }

    [Fact]
    public void TheEndTurnConfirmIsSilentOnceEveryUnitHasMovedOrActed()
    {
        var client = Brackwater();
        var state = client.State;
        foreach (var unit in state.UnitsOf(Side.Player).ToList())
        {
            state = Resolver.Apply(state, Content, new Wait(unit.Id)).Next;
        }

        Assert.Equal(Side.Player, state.Phase);
        Assert.Null(EndTurnConfirm.Lines(state, Content, confirmOn: true));
    }

    [Fact]
    public void TheEndTurnConfirmCountsOneUnitInTheSingular()
    {
        var state = Brackwater().State;
        var units = state.UnitsOf(Side.Player).ToList();
        foreach (var unit in units.Skip(1))
        {
            state = Resolver.Apply(state, Content, new Wait(unit.Id)).Next;
        }

        Assert.StartsWith("1 unit has not moved: ", EndTurnConfirm.Lines(state, Content, confirmOn: true)![0]);
    }

    [Fact]
    public void HoveringAnEnemyShowsItsReachOnlyWithTheOptionOnAndNoUnitSelected()
    {
        var client = Brackwater();
        var enemy = client.State.UnitsOf(Side.Enemy).First(e => client.EnemyReachAt(e.At) is not null);

        var shown = client.ReachShown(enemy.At, onHover: true);
        Assert.Equal(enemy.Id, shown?.Id);
        Assert.Equal(client.EnemyReachAt(enemy.At)!.Tiles.Order(), shown!.Tiles.Order());
        Assert.Null(client.ReachShown(enemy.At, onHover: false));

        client.Select(client.State.UnitsOf(Side.Player).First().At);
        Assert.Null(client.ReachShown(enemy.At, onHover: true));
    }

    [Fact]
    public void NewGameOffersOnlyTheUnlockedDifficultiesLowestTierFirst()
    {
        var fresh = Screens.NewGameDifficulties(Content, Array.Empty<string>());
        var all = Screens.NewGameDifficulties(Content, Content.Difficulties.Keys);

        Assert.All(fresh, d => Assert.True(d.IsUnlocked(Array.Empty<string>())));
        Assert.Equal(fresh.OrderBy(d => d.Tier).Select(d => d.Id), fresh.Select(d => d.Id));
        Assert.Contains(fresh, d => d.Id == CampaignRecord.NormalDifficulty);
        Assert.Contains(Content.Difficulties.Values, d => !d.IsUnlocked(Array.Empty<string>()) && !fresh.Contains(d));
        Assert.Equal(Content.Difficulties.Count, all.Count);
    }

    [Fact]
    public void NewGameSaysWhatPermadeathOffMeans()
    {
        var lines = Screens.NewGameLines(Content.Difficulties[CampaignRecord.NormalDifficulty], permadeath: false);

        Assert.StartsWith("Difficulty: ", lines[0]);
        Assert.Equal($"Permadeath: off, the fallen come back wounded for {Wound.MainMaps} maps", lines[1]);
    }

    [Fact]
    public void ThePauseMenuOpensOptionsOnO()
    {
        Assert.Equal(PauseChoice.Options, Screens.PauseKey("O"));
    }

    [Fact]
    public void EachCampaignTitleLineHasItsOwnKey()
    {
        var keys = Screens.CampaignTitle(hasSave: true).Select(Screens.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void TheCampaignClientAutosavesAtTheCampSoContinueFindsIt()
    {
        var store = new SaveStore(TempDir());
        try
        {
            Assert.Null(store.Newest());

            var campaign = new CampaignClient(Content, Fixture.RealContentDirectory(), CampaignRecord.Start(Content, 7), saves: store);

            Assert.Equal(SaveStore.AutoPrefix + "1", store.Newest());
            Assert.Equal(campaign.Record, store.Load(store.Newest()!, Content).Record);
        }
        finally
        {
            if (Directory.Exists(store.Directory))
            {
                Directory.Delete(store.Directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ACampaignClientWithoutASaveStoreWritesNothing()
    {
        var dir = TempDir();
        _ = new CampaignClient(Content, Fixture.RealContentDirectory(), CampaignRecord.Start(Content, 7));

        Assert.False(Directory.Exists(dir));
        Assert.Null(new SaveStore(dir).Newest());
    }
}
