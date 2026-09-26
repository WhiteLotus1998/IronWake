using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The presentation protocol's JSON (issue 25): one golden shape per event type, commands
/// and forecasts that read back equal, a state that reads back equal with its history, and
/// the guards that refuse what the protocol cannot carry.
/// </summary>
public class ProtocolJsonTests
{
    private static readonly Coord A = new(1, 2);
    private static readonly Coord B = new(3, 4);

    /// <summary>Every event type and the exact JSON the protocol writes for it; docs/PROTOCOL.md lists the same fields.</summary>
    public static TheoryData<GameEvent, string> Events() => new()
    {
        { new UnitMoved("wren", A, B, ValueList<Coord>.Of(new Coord(2, 2), B)), """{"type":"unitMoved","unit":"wren","from":{"x":1,"y":2},"to":{"x":3,"y":4},"path":[{"x":2,"y":2},{"x":3,"y":4}]}""" },
        {
            new CombatFought("wren", "brigand-1", 3, Side.Player, ValueList<StrikeEvent>.Of(new StrikeEvent(0, "wren", "brigand-1", true, false, 7, 13), new StrikeEvent(1, "brigand-1", "wren", false, false, 0, 20)), 20, 13),
            """{"type":"combatFought","attacker":"wren","target":"brigand-1","turn":3,"phase":"player","strikes":[{"index":0,"attacker":"wren","target":"brigand-1","hit":true,"crit":false,"damage":7,"targetHpAfter":13},{"index":1,"attacker":"brigand-1","target":"wren","hit":false,"crit":false,"damage":0,"targetHpAfter":20}],"attackerHpAfter":20,"targetHpAfter":13}"""
        },
        { new UnitDied("brigand-1", Side.Enemy, B), """{"type":"unitDied","unit":"brigand-1","side":"enemy","at":{"x":3,"y":4}}""" },
        { new ExpGained("wren", 30, 72), """{"type":"expGained","unit":"wren","amount":30,"expAfter":72}""" },
        { new LeveledUp("wren", 4, new Stats(1, 0, 0, 1, 1, 0, 0, 0, 1)), """{"type":"leveledUp","unit":"wren","newLevel":4,"gains":{"hp":1,"str":0,"mag":0,"dex":1,"spd":1,"lck":0,"def":0,"res":0,"cha":1}}""" },
        { new RankRaised("wren", WeaponType.Sword, WeaponRank.D), """{"type":"rankRaised","unit":"wren","weaponType":"sword","rank":"d"}""" },
        { new MasteryEarned("wren", "cadet", "axebreaker"), """{"type":"masteryEarned","unit":"wren","class":"cadet","ability":"axebreaker"}""" },
        { new UnitWaited("wren"), """{"type":"unitWaited","unit":"wren"}""" },
        { new Cantoed("ansgar", A, B, ValueList<Coord>.Of(new Coord(2, 2), B)), """{"type":"cantoed","unit":"ansgar","from":{"x":1,"y":2},"to":{"x":3,"y":4},"path":[{"x":2,"y":2},{"x":3,"y":4}]}""" },
        { new UnitRetreated("brigand-1", A, B), """{"type":"unitRetreated","unit":"brigand-1","from":{"x":1,"y":2},"to":{"x":3,"y":4}}""" },
        { new RapportGained("ottilie", "wren", 4, 8, 16), """{"type":"rapportGained","a":"ottilie","b":"wren","amount":4,"total":8,"outOf":16}""" },
        { new RapportGained("teodor", "wren", 4, 8), """{"type":"rapportGained","a":"teodor","b":"wren","amount":4,"total":8,"outOf":null}""" },
        { new RivalryEnded("ottilie", "wren"), """{"type":"rivalryEnded","a":"ottilie","b":"wren"}""" },
        { new PhaseEnded(Side.Player, 2), """{"type":"phaseEnded","side":"player","turn":2}""" },
        { new PhaseBegan(Side.Enemy, 2), """{"type":"phaseBegan","side":"enemy","turn":2}""" },
        { new UnitHealed("wren", 3, 17), """{"type":"unitHealed","unit":"wren","amount":3,"hpAfter":17}""" },
        { new Recalled(12, 2), """{"type":"recalled","toIndex":12,"chargesLeft":2}""" },
        { new ItemUsed("wren", "field_dressing", "wren", 0), """{"type":"itemUsed","unit":"wren","item":"field_dressing","target":"wren","usesLeft":0}""" },
        { new WeaponEquipped("wren", "steel_sword"), """{"type":"weaponEquipped","unit":"wren","item":"steel_sword"}""" },
        { new ArtDeclared("wren", "sunder", "iron_sword", 2), """{"type":"artDeclared","unit":"wren","art":"sunder","item":"iron_sword","cost":2}""" },
        { new WeaponBroke("wren", "iron_sword"), """{"type":"weaponBroke","unit":"wren","item":"iron_sword"}""" },
        { new SpellSpent("mira", "mend"), """{"type":"spellSpent","unit":"mira","item":"mend"}""" },
        { new GroupWoke("fort", WakeCause.Proximity), """{"type":"groupWoke","group":"fort","cause":"proximity"}""" },
        { new MapEventFired("riders", false), """{"type":"mapEventFired","name":"riders","blocked":false}""" },
        { new TerrainChanged(A, "plain"), """{"type":"terrainChanged","at":{"x":1,"y":2},"terrain":"plain"}""" },
        { new UnitSpawned("rider-1", B, "flank", Behavior.Aggressive), """{"type":"unitSpawned","unit":"rider-1","at":{"x":3,"y":4},"group":"flank","behavior":"aggressive"}""" },
        { new FlagSet("gate_open"), """{"type":"flagSet","flag":"gate_open"}""" },
    };

    [Theory]
    [MemberData(nameof(Events))]
    public void EveryEventTypeHasItsDocumentedShape(GameEvent e, string json)
    {
        Assert.Equal(json, ProtocolJson.Event(e));
    }

    [Fact]
    public void EveryEventRecordInTheCoreHasAGoldenShape()
    {
        var covered = Events().Select(row => row[0].GetType()).ToHashSet();
        var all = typeof(GameEvent).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(GameEvent)) && !t.IsAbstract).ToList();

        Assert.NotEmpty(all);
        Assert.All(all, t => Assert.True(covered.Contains(t), $"{t.Name} has no golden protocol shape"));
    }

    [Fact]
    public void EveryEventTypeIsDocumentedInProtocolMd()
    {
        var doc = File.ReadAllText(Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "PROTOCOL.md"));

        foreach (var row in Events())
        {
            var type = System.Text.Json.JsonDocument.Parse((string)row[1]).RootElement.GetProperty("type").GetString()!;
            Assert.Contains("`" + type + "`", doc);
        }
    }

    [Fact]
    public void AnEventCarriesTheConsoleTextLastWhenGiven()
    {
        Assert.Equal("""{"type":"unitWaited","unit":"wren","text":"wren waits"}""", ProtocolJson.Event(new UnitWaited("wren"), "wren waits"));
    }

    private sealed record UnknownEvent : GameEvent;

    [Fact]
    public void AnEventTypeWithoutAShapeIsRefusedNotGuessed()
    {
        var e = Assert.Throws<ArgumentException>(() => ProtocolJson.Event(new UnknownEvent()));
        Assert.Contains("UnknownEvent", e.Message);
    }

    public static TheoryData<Command, string> Commands() => new()
    {
        { new Move("wren", B), """{"type":"move","unit":"wren","to":{"x":3,"y":4}}""" },
        { new Attack("wren", "brigand-1"), """{"type":"attack","unit":"wren","target":"brigand-1","slot":null}""" },
        { new Attack("wren", "brigand-1", 1), """{"type":"attack","unit":"wren","target":"brigand-1","slot":1}""" },
        { new Attack("wren", "brigand-1", null, "sunder"), """{"type":"attack","unit":"wren","target":"brigand-1","slot":null,"art":"sunder"}""" },
        { new UseItem("wren", 1), """{"type":"item","unit":"wren","slot":1,"target":null}""" },
        { new UseItem("mira", 0, "wren"), """{"type":"item","unit":"mira","slot":0,"target":"wren"}""" },
        { new Retreat("brigand-1", A), """{"type":"retreat","unit":"brigand-1","to":{"x":1,"y":2}}""" },
        { new Wait("wren"), """{"type":"wait","unit":"wren"}""" },
        { new Canto("ansgar", B), """{"type":"canto","unit":"ansgar","to":{"x":3,"y":4}}""" },
        { new EndPhase(), """{"type":"end"}""" },
        { new Recall(4), """{"type":"recall","toIndex":4}""" },
    };

    [Theory]
    [MemberData(nameof(Commands))]
    public void EveryCommandHasItsShapeAndReadsBackEqual(Command command, string json)
    {
        Assert.Equal(json, ProtocolJson.Command(command));
        Assert.Equal(command, ProtocolJson.ReadCommand(json));
    }

    [Fact]
    public void ACommandReaderIgnoresFieldsItDoesNotKnow()
    {
        Assert.Equal(new Wait("wren"), ProtocolJson.ReadCommand("""{"type":"wait","unit":"wren","note":"from a newer renderer"}"""));
    }

    [Theory]
    [InlineData("""{"unit":"wren"}""", "field 'type' is missing")]
    [InlineData("""{"type":"fly","unit":"wren"}""", "type 'fly' is not a command")]
    [InlineData("""{"type":"move","unit":"wren"}""", "field 'to' is missing")]
    [InlineData("""{"type":"move","unit":"wren","to":{"x":"a","y":1}}""", "field 'x' is not an integer")]
    [InlineData("""{"type":"attack","unit":7,"target":"b"}""", "field 'unit' is not a string")]
    [InlineData("""{"type":"attack","unit":"a","target":"b","slot":"one"}""", "field 'slot' is not an integer")]
    [InlineData("""{"type":"recall"}""", "field 'toIndex' is missing")]
    [InlineData("""[1,2]""", "expected an object holding 'type'")]
    [InlineData("""{"type":""", "not JSON")]
    public void AMalformedCommandIsRefusedNamingTheField(string json, string message)
    {
        var e = Assert.Throws<ProtocolException>(() => ProtocolJson.ReadCommand(json));
        Assert.Contains(message, e.Message);
    }

    [Fact]
    public void AGauntletForecastCarriesItsStrikesPerRoundAndAnOlderForecastReadsAsOne()
    {
        var forecast = new CombatForecast(new SideForecast(true, 3, 81, 91, 4, true, StrikesPerRound: 2), SideForecast.None, RollScheme.OneRoll);

        var json = ProtocolJson.Forecast(forecast);

        Assert.Contains("\"doubles\":true,\"strikesPerRound\":2}", json);
        Assert.Equal(forecast, ProtocolJson.ReadForecast(json));
        var older = json.Replace(",\"strikesPerRound\":2", "").Replace(",\"strikesPerRound\":1", "");
        Assert.Equal(1, ProtocolJson.ReadForecast(older).Attacker.StrikesPerRound);
    }

    [Fact]
    public void AForecastReadsBackEqual()
    {
        var forecast = new CombatForecast(new SideForecast(true, 7, 81, 91, 4, true), SideForecast.None, RollScheme.TwoRollAverage);

        var json = ProtocolJson.Forecast(forecast);

        Assert.Equal("""{"attacker":{"strikes":true,"damage":7,"hitChance":81,"displayedHit":91,"critChance":4,"doubles":true,"strikesPerRound":1},"defender":{"strikes":false,"damage":0,"hitChance":0,"displayedHit":0,"critChance":0,"doubles":false,"strikesPerRound":1},"scheme":"twoRollAverage"}""", json);
        Assert.Equal(forecast, ProtocolJson.ReadForecast(json));
    }

    [Fact]
    public void AnArtsForecastCarriesItsCostAndReadsBackEqual()
    {
        var forecast = new CombatForecast(new SideForecast(true, 16, 90, 99, 5, false), SideForecast.None, RollScheme.OneRoll, 2);

        var json = ProtocolJson.Forecast(forecast);

        Assert.EndsWith("\"scheme\":\"oneRoll\",\"artCost\":2}", json);
        Assert.Equal(forecast, ProtocolJson.ReadForecast(json));
        Assert.Equal(3, forecast.AttackerSpendsAtMost);
    }

    [Fact]
    public void AnUnknownEnumNameIsRefusedListingTheNames()
    {
        var e = Assert.Throws<ProtocolException>(() => ProtocolJson.ReadForecast("""{"attacker":{"strikes":true,"damage":7,"hitChance":81,"displayedHit":91,"critChance":4,"doubles":true},"defender":{"strikes":false,"damage":0,"hitChance":0,"displayedHit":0,"critChance":0,"doubles":false},"scheme":"three"}"""));
        Assert.Contains("field 'scheme': 'three' is not one of: twoRollAverage, oneRoll", e.Message);
    }

    /// <summary>
    /// The seed-163 Tollgate line played over the protocol to its end: a state with a Recall
    /// spent, a spawned rider, a fired event, levels and spent uses, and a long history. It
    /// writes and reads back to an equal state, history and all, with the same canonical text.
    /// </summary>
    [Fact]
    public void AStateReadsBackEqualWithItsHistory()
    {
        var (content, state) = PlayedTollgate();
        Assert.True(state.History.Count > 50);
        Assert.Contains("riders", state.Fired);

        var json = ProtocolJson.State(state, content);
        var back = ProtocolJson.ReadState(json, content);

        Assert.Equal(state, back);
        Assert.Equal(state.Canonical(), back.Canonical());
        Assert.Equal(json, ProtocolJson.State(back, content));
    }

    /// <summary>
    /// Issue 260: the state carries the content's wake and noise radii beside
    /// <c>awakeGroups</c>, so a renderer can print the wake legend; like <c>maxHp</c> they
    /// are derived and not read back.
    /// </summary>
    [Fact]
    public void TheStateCarriesTheWakeAndNoiseRadii()
    {
        var content = Ironwake.Core.Tests.Maps.MapFixture.Content with { WakeRadius = 3 };
        var state = Ironwake.Core.Tests.Battle.BattleFixture.Start(map: Ironwake.Core.Tests.Maps.MapFixture.OldMillRoad);

        foreach (var json in new[] { ProtocolJson.State(state, content), ProtocolJson.BoardState(state, content) })
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            Assert.Equal(3, doc.RootElement.GetProperty("wakeRadius").GetInt32());
            Assert.Equal(5, doc.RootElement.GetProperty("noiseRadius").GetInt32());
        }
    }

    [Fact]
    public void AStateReadsBackEqualWithRapportGroupsAndFlags()
    {
        var (content, state) = PlayedTollgate();
        var marked = state with
        {
            AwakeGroups = ValueList<string>.Of("fort", "woods"),
            Flags = ValueList<string>.Of("gate_open"),
            Rapport = ValueList<Rapport>.Of(new Rapport("ottilie", "wren", 8)),
            History = ValueList<BattleState>.Empty,
        };

        Assert.Equal(marked, ProtocolJson.ReadState(ProtocolJson.State(marked, content), content));
    }

    /// <summary>Issue 71: a Canto owed travels in <c>canto</c>, null when none is, and a state written before the field existed reads as none.</summary>
    [Fact]
    public void AStateReadsBackEqualWithACantoOwed()
    {
        var (content, state) = PlayedTollgate();
        var captain = state.UnitsOf(Side.Player).First();
        var owed = (state with { History = ValueList<BattleState>.Empty }).WithUnit(captain with { Acted = true, Canto = 3 });

        var json = ProtocolJson.State(owed, content);
        Assert.Contains("\"canto\":3", json);
        Assert.Contains("\"canto\":null", json);
        Assert.Equal(owed, ProtocolJson.ReadState(json, content));

        var older = json.Replace(",\"canto\":null", string.Empty);
        Assert.DoesNotContain("\"canto\":null", older);
        Assert.Equal(owed, ProtocolJson.ReadState(older, content));
    }

    /// <summary>Issue 67: a unit's rank points travel in <c>weaponPoints</c>, and a state written before the field existed reads as rank E.</summary>
    [Fact]
    public void AStateReadsBackEqualWithWeaponPoints()
    {
        var (content, state) = PlayedTollgate();
        var captain = state.UnitsOf(Side.Player).First();
        var skilled = (state with { History = ValueList<BattleState>.Empty })
            .WithUnit(captain with { Unit = captain.Unit with { Skill = WeaponSkill.Zero.With(WeaponType.Sword, 34).With(WeaponType.Faith, 3) } });

        var json = ProtocolJson.State(skilled, content);
        Assert.Contains("\"weaponPoints\":{\"sword\":34,\"lance\":0,\"axe\":0,\"bow\":0,\"reason\":0,\"faith\":3,\"gauntlet\":0}", json);
        Assert.Equal(skilled, ProtocolJson.ReadState(json, content));

        var older = System.Text.RegularExpressions.Regex.Replace(json, ",\"weaponPoints\":\\{[^}]*\\}", string.Empty);
        Assert.DoesNotContain("weaponPoints", older);
        Assert.Equal(WeaponSkill.Zero, ProtocolJson.ReadState(older, content).Find(captain.Id)!.Unit.Skill);
    }

    /// <summary>Issue 69: a unit's mastery points travel in <c>masteryPoints</c> by class id, and a state written before the field existed reads as none.</summary>
    [Fact]
    public void AStateReadsBackEqualWithMasteryPoints()
    {
        var (content, state) = PlayedTollgate();
        var captain = state.UnitsOf(Side.Player).First();
        var trained = (state with { History = ValueList<BattleState>.Empty })
            .WithUnit(captain with { Unit = captain.Unit with { Mastery = MasteryProgress.Empty.With("pikeman", 2).With("cadet", 5) } });

        var json = ProtocolJson.State(trained, content);
        Assert.Contains("\"masteryPoints\":{\"cadet\":5,\"pikeman\":2}", json);
        Assert.Equal(trained, ProtocolJson.ReadState(json, content));

        var older = System.Text.RegularExpressions.Regex.Replace(json, ",\"masteryPoints\":\\{[^}]*\\}", string.Empty);
        Assert.DoesNotContain("masteryPoints", older);
        Assert.Equal(MasteryProgress.Empty, ProtocolJson.ReadState(older, content).Find(captain.Id)!.Unit.Mastery);
    }

    [Fact]
    public void TheBoardStateLeavesOutTheMapAndTheHistoryAndCannotBeReadBack()
    {
        var (content, state) = PlayedTollgate();

        var board = ProtocolJson.BoardState(state, content);

        Assert.DoesNotContain("\"map\":", board);
        Assert.DoesNotContain("\"history\":", board);
        Assert.Contains($"\"historyCount\":{state.History.Count}", board);
        Assert.Contains("\"mapName\":\"The Tollgate\"", board);
        var e = Assert.Throws<ProtocolException>(() => ProtocolJson.ReadState(board, content));
        Assert.Contains("field 'map' is missing", e.Message);
    }

    [Fact]
    public void AStateFromAnotherProtocolVersionIsRefused()
    {
        var (content, state) = PlayedTollgate();
        var json = ProtocolJson.State(state with { History = ValueList<BattleState>.Empty }, content)
            .Replace($"\"protocolVersion\":{ProtocolVersion.Current}", "\"protocolVersion\":999");

        var e = Assert.Throws<ProtocolException>(() => ProtocolJson.ReadState(json, content));
        Assert.Contains("protocolVersion 999", e.Message);
    }

    [Fact]
    public void AStateNamingAClassTheContentLacksIsRefusedNamingTheUnit()
    {
        var (content, state) = PlayedTollgate();
        var captain = state.Units.First(u => u.IsCaptain);
        var json = ProtocolJson.State(state with { History = ValueList<BattleState>.Empty }, content)
            .Replace($"\"class\":\"{captain.Unit.ClassId}\"", "\"class\":\"no_such_class\"");

        var e = Assert.Throws<ProtocolException>(() => ProtocolJson.ReadState(json, content));
        Assert.Contains($"unit '{captain.Id}': class 'no_such_class'", e.Message);
    }

    [Fact]
    public void TheSeedTravelsAsAStringSoNoConsumerRoundsIt()
    {
        var (content, state) = PlayedTollgate();
        var big = state with { Seed = ulong.MaxValue, History = ValueList<BattleState>.Empty };

        var json = ProtocolJson.State(big, content);

        Assert.Contains("\"seed\":\"18446744073709551615\"", json);
        Assert.Equal(ulong.MaxValue, ProtocolJson.ReadState(json, content).Seed);
    }

    private static (GameContent Content, BattleState State) PlayedTollgate()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), content);
        var session = new ProtocolSession(content, BattleState.From(map, content, content.Cast, 163), TextWriter.Null);
        foreach (var line in ProtocolSessionTests.JsonCommands(ProtocolSessionTests.TollgateScript()))
        {
            Assert.StartsWith("{\"ok\":true", session.Answer(line));
        }

        return (content, session.State);
    }
}
