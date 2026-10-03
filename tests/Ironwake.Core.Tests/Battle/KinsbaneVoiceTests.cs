using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The hungering weapon's voice (issue 804 item 3, DESIGN.md 13.23): it speaks to its carrier when a
/// drain starves it, when a kill grows a tooth (the tooth's number choosing the line), and on the kill
/// that wakes it, in place of that kill's tooth line, with the carrier's name in; at most three lines a
/// battle; silent otherwise. The lines are content, validated on load as barks. Played on the spike's
/// sample, <c>docs/samples/the_gleaning_kinsbane.map</c>.
/// </summary>
public class KinsbaneVoiceTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_gleaning_kinsbane.map");

    private static BattleState Placed() =>
        BattleState.From(MapFiles.Load(SamplePath, Shipped), Shipped, Shipped.Cast, 645);

    private static BattleUnit Keziah(BattleState state) => state.Find("keziah")!;

    private static BattleState WithScythe(int hp, int fed = 0, bool starved = false, int spoken = 0)
    {
        var state = Placed();
        var keziah = Keziah(state);
        var stack = keziah.Unit.Inventory.Items[0] with { Fed = fed, Starved = starved, Uses = starved ? 1 : 20 };
        return state.WithUnit(keziah with { Hp = hp, VoiceSpoken = spoken, Unit = keziah.Unit with { Inventory = keziah.Unit.Inventory.Replace(0, stack) } });
    }

    private static (BattleState State, List<GameEvent> Events) PhaseStart(BattleState state, int turn = 2)
    {
        var events = new List<GameEvent>();
        return (Kinsbane.AtPhaseStart(state with { Turn = turn }, Shipped, Side.Player, events), events);
    }

    private static (BattleUnit Unit, List<GameEvent> Events) Kill(BattleState state)
    {
        var events = new List<GameEvent>();
        return (Kinsbane.AfterCombat(Keziah(state), Shipped, ValueList<StrikeEvent>.Empty, killed: true, events), events);
    }

    private static HungerVoice Voice => Shipped.Weapon(Kinsbane.ItemId).Voice!;

    [Fact]
    public void TheShippedScytheCarriesAVoiceInAllThreeRegisters()
    {
        Assert.NotEmpty(Voice.Starved);
        Assert.Equal(Kinsbane.MtCap - 1, Voice.Tooth.Count);
        Assert.Single(Voice.Woken);
    }

    [Fact]
    public void ADrainThatStarvesTheBladeSpeaksAStarvedLineAfterTheDrain()
    {
        var (next, events) = PhaseStart(WithScythe(6), turn: 2);

        Assert.IsType<HungerDrained>(events[0]);
        var line = Voice.Starved[2 % Voice.Starved.Count];
        Assert.Equal(new KinsbaneSpoke("keziah", Kinsbane.ItemId, line.Id, line.Text), events[1]);
        Assert.Equal(1, Keziah(next).VoiceSpoken);
    }

    [Theory]
    [InlineData(23, false)]
    [InlineData(1, true)]
    public void ADrainThatDoesNotStarveTheBladeIsSilent(int hp, bool alreadyStarved)
    {
        var (next, events) = PhaseStart(WithScythe(hp, starved: alreadyStarved));

        Assert.DoesNotContain(events, e => e is KinsbaneSpoke);
        Assert.Equal(0, Keziah(next).VoiceSpoken);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(7, 4)]
    public void AKillThatGrowsAToothSpeaksThatToothsLine(int fedBefore, int tooth)
    {
        var (unit, events) = Kill(WithScythe(10, fed: fedBefore));

        var line = Voice.Tooth[tooth - 1];
        Assert.Equal(new KinsbaneSpoke("keziah", Kinsbane.ItemId, line.Id, line.Text), events[^1]);
        Assert.IsType<HungerFed>(events[0]);
        Assert.Equal(1, unit.VoiceSpoken);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(12)]
    public void AKillThatGrowsNoToothIsSilent(int fedBefore)
    {
        var (unit, events) = Kill(WithScythe(10, fed: fedBefore));

        Assert.DoesNotContain(events, e => e is KinsbaneSpoke);
        Assert.Equal(0, unit.VoiceSpoken);
    }

    [Fact]
    public void TheWakingKillSpeaksTheWokenLineWithTheCarriersNameAndNoToothLine()
    {
        var (_, events) = Kill(WithScythe(10, fed: Kinsbane.WakeKills - 1));

        var spoke = Assert.Single(events.OfType<KinsbaneSpoke>());
        Assert.Equal(Voice.Woken[0].Id, spoke.LineId);
        Assert.Equal("Keziah. I remember what I'm for.", spoke.Text);
    }

    [Fact]
    public void TheVoiceSaysAtMostThreeLinesABattle()
    {
        var (unit, events) = Kill(WithScythe(10, fed: 1, spoken: Kinsbane.VoiceCap));
        var (next, drained) = PhaseStart(WithScythe(6, spoken: Kinsbane.VoiceCap));

        Assert.DoesNotContain(events, e => e is KinsbaneSpoke);
        Assert.DoesNotContain(drained, e => e is KinsbaneSpoke);
        Assert.Equal(Kinsbane.VoiceCap, unit.VoiceSpoken);
        Assert.Equal(Kinsbane.VoiceCap, Keziah(next).VoiceSpoken);
    }

    [Fact]
    public void AWeaponWithNoVoiceIsSilent()
    {
        var events = new List<GameEvent>();
        var keziah = Keziah(Placed());

        Assert.Equal(keziah, Kinsbane.Speak(keziah, Kinsbane.ItemId, null, 0, events));
        Assert.Equal(keziah, Kinsbane.Speak(keziah, Kinsbane.ItemId, ValueList<VoiceLine>.Empty, 0, events));
        Assert.Empty(events);
    }

    [Fact]
    public void TheVoiceCountRidesTheBoardThroughTheProtocol()
    {
        var state = WithScythe(10, spoken: 2);

        Assert.Equal(2, Keziah(ProtocolJson.ReadState(ProtocolJson.State(state, Shipped), Shipped)).VoiceSpoken);
    }

    [Fact]
    public void TheConsoleQuotesTheLineToItsCarrier()
    {
        var spoke = new KinsbaneSpoke("keziah", Kinsbane.ItemId, "kb_starved_eat", "Eat.");

        Assert.EndsWith("Kinsbane, to keziah: \"Eat.\"", PlaySession.Describe(spoke, Shipped, UnitNames.None), StringComparison.Ordinal);
    }

    private const string Scythe = """{ "id": "scythe", "name": "Scythe", "type": "axe", "mt": 9, "hit": 70, "crit": 0, "wt": 10, "minRange": 1, "maxRange": 1, "durability": 20, "rank": "E", "hungers": true, "description": "A test line.", "voice": VOICE }""";

    private static ContentException Refused(string voice, bool hungers = true)
    {
        var line = Scythe.Replace("VOICE", voice);
        line = hungers ? line : line.Replace("\"hungers\": true, ", "");
        var weapons = Fixture.Weapons.Replace("\n] }", ",\n" + line + "\n] }");
        Assert.Contains("scythe", weapons);
        return Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(weapons: weapons)));
    }

    [Fact]
    public void AVoiceOnAWeaponThatDoesNotHungerIsRefused()
    {
        Assert.Contains("only a hungering weapon speaks", Refused("""{ "starved": [ { "id": "a", "text": "Eat." } ] }""", hungers: false).Message);
    }

    [Theory]
    [InlineData("""{ "growl": [ { "id": "a", "text": "Eat." } ] }""", "is not a register")]
    [InlineData("""{ "starved": [ { "id": "a", "text": "Eat." } ], "tooth": [ { "id": "a", "text": "More." } ] }""", "names a line twice")]
    [InlineData("""{ "starved": [ { "id": "a", "text": "one two three four five six seven eight nine ten eleven twelve thirteen" } ] }""", "at most 12 words, not 13")]
    [InlineData("""{ "woken": [ { "id": "a", "text": "{bearer}. I remember." } ] }""", "only the {name} token")]
    [InlineData("""{ "starved": [ "Eat." ] }""", "must be an object with id and text")]
    public void ABadVoiceLineIsRefusedNamingItsField(string voice, string problem)
    {
        var error = Refused(voice);

        Assert.Contains("voice", error.Message);
        Assert.Contains(problem, error.Message);
    }

    [Fact]
    public void AVoiceLineAtTwelveWordsLoads()
    {
        var line = Scythe.Replace("VOICE", """{ "starved": [ { "id": "a", "text": "one two three four five six seven eight nine ten eleven {name}" } ] }""");
        var content = ContentLoader.Parse(Fixture.Files(weapons: Fixture.Weapons.Replace("\n] }", ",\n" + line + "\n] }")));

        Assert.Equal("a", content.Weapon("scythe").Voice!.Starved[0].Id);
    }
}
