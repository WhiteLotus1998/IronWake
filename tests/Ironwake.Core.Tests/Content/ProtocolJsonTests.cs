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
        { new UnitExited("wren", new Coord(19, 3)), """{"type":"unitExited","unit":"wren","at":{"x":19,"y":3}}""" },
        { new UnitLeftBehind("dunstan", new Coord(12, 3)), """{"type":"unitLeftBehind","unit":"dunstan","at":{"x":12,"y":3}}""" },
        { new KeepsakeLeft("dunstan", "iron_axe", new Coord(12, 3)), """{"type":"keepsakeLeft","fallen":"dunstan","item":"iron_axe","at":{"x":12,"y":3}}""" },
        { new TomeDropped("hexer-1", ValueList<string>.Of("cinder")), """{"type":"tomeDropped","unit":"hexer-1","items":["cinder"]}""" },
        { new GrudgeSworn("soldier-1", "wren"), """{"type":"grudgeSworn","unit":"soldier-1","against":"wren"}""" },
        { new KeepsakeRecovered("wren", "dunstan", "iron_axe"), """{"type":"keepsakeRecovered","unit":"wren","fallen":"dunstan","item":"iron_axe"}""" },
        { new ChestOpened("wren", new Coord(2, 1), ValueList<string>.Of("steel_sword", "field_dressing"), ValueList<string>.Empty), """{"type":"chestOpened","unit":"wren","at":{"x":2,"y":1},"items":["steel_sword","field_dressing"]}""" },
        { new ChestOpened("wren", new Coord(2, 1), ValueList<string>.Of("steel_sword"), ValueList<string>.Of("iron_bow")), """{"type":"chestOpened","unit":"wren","at":{"x":2,"y":1},"items":["steel_sword"],"wagon":["iron_bow"]}""" },
        { new ChestOpened("pell", new Coord(2, 1), ValueList<string>.Of("cinder"), ValueList<string>.Empty, ValueList<WieldShort>.Of(new WieldShort("cinder", "fire school"))), """{"type":"chestOpened","unit":"pell","at":{"x":2,"y":1},"items":["cinder"],"cannotWield":[{"item":"cinder","why":"fire school"}]}""" },
        { new KeepsakeTaken("brigand-2", "teodor", "iron_lance"), """{"type":"keepsakeTaken","unit":"brigand-2","fallen":"teodor","item":"iron_lance"}""" },
        { new KeepsakeLost("teodor", "iron_lance", new Coord(12, 3), null), """{"type":"keepsakeLost","fallen":"teodor","item":"iron_lance","at":{"x":12,"y":3}}""" },
        { new KeepsakeLost("teodor", "iron_lance", new Coord(12, 3), "brigand-2"), """{"type":"keepsakeLost","fallen":"teodor","item":"iron_lance","at":{"x":12,"y":3},"carrier":"brigand-2"}""" },
        { new OrderCalled("alder", OrderKind.FallBack, 4, ValueList<string>.Of("teodor", "wren"), 3, 4, 12), """{"type":"orderCalled","unit":"alder","kind":"fallBack","radius":4,"reached":["teodor","wren"],"inRadius":3,"alive":4,"exposure":12}""" },
        { new FellBack("wren", A, B, ValueList<Coord>.Of(new Coord(2, 2), B)), """{"type":"fellBack","unit":"wren","from":{"x":1,"y":2},"to":{"x":3,"y":4},"path":[{"x":2,"y":2},{"x":3,"y":4}]}""" },
        { new Cantoed("ansgar", A, B, ValueList<Coord>.Of(new Coord(2, 2), B)), """{"type":"cantoed","unit":"ansgar","from":{"x":1,"y":2},"to":{"x":3,"y":4},"path":[{"x":2,"y":2},{"x":3,"y":4}]}""" },
        { new UnitRetreated("brigand-1", A, B), """{"type":"unitRetreated","unit":"brigand-1","from":{"x":1,"y":2},"to":{"x":3,"y":4}}""" },
        { new MessengerEscaped("rider-1", A), """{"type":"messengerEscaped","unit":"rider-1","at":{"x":1,"y":2}}""" },
        { new UnitBroke("soldier-1", A, 4), """{"type":"unitBroke","unit":"soldier-1","at":{"x":1,"y":2},"hp":4}""" },
        { new UnitFreed("brigand-1", A, 4), """{"type":"unitFreed","unit":"brigand-1","at":{"x":1,"y":2},"hp":4}""" },
        { new RouteDrifted("south", A, ValueList<string>.Of("rider-1")), """{"type":"routeDrifted","group":"south","to":{"x":1,"y":2},"units":["rider-1"]}""" },
        { new UnitTalked("rook", "keziah", A, 9, ReturnFate.Turned), """{"type":"unitTalked","unit":"rook","target":"keziah","at":{"x":1,"y":2},"hp":9,"fate":"turned"}""" },
        { new Carried("rook", "wren", A, B, new Coord(1, 3), new Coord(3, 5)), """{"type":"carried","unit":"rook","ally":"wren","from":{"x":1,"y":2},"to":{"x":3,"y":4},"allyFrom":{"x":1,"y":3},"setDown":{"x":3,"y":5}}""" },
        { new Breathed("rook", A, ValueList<Coord>.Of(B), ValueList<string>.Of("brigand-1"), ValueList<Coord>.Of(B)), """{"type":"breathed","unit":"rook","from":{"x":1,"y":2},"line":[{"x":3,"y":4}],"chilled":["brigand-1"],"frozen":[{"x":3,"y":4}]}""" },
        { new Shoved("dunstan", "rider-1", A, B), """{"type":"shoved","unit":"dunstan","target":"rider-1","from":{"x":1,"y":2},"to":{"x":3,"y":4}}""" },
        { new RapportGained("ottilie", "wren", 4, 8, 16), """{"type":"rapportGained","a":"ottilie","b":"wren","amount":4,"total":8,"outOf":16}""" },
        { new RapportGained("teodor", "wren", 4, 8), """{"type":"rapportGained","a":"teodor","b":"wren","amount":4,"total":8,"outOf":null}""" },
        { new RivalryEnded("ottilie", "wren"), """{"type":"rivalryEnded","a":"ottilie","b":"wren"}""" },
        { new SupportReached("ivo", "wren", "C"), """{"type":"supportReached","a":"ivo","b":"wren","tier":"C"}""" },
        { new PhaseEnded(Side.Player, 2), """{"type":"phaseEnded","side":"player","turn":2}""" },
        { new PhaseBegan(Side.Enemy, 2), """{"type":"phaseBegan","side":"enemy","turn":2}""" },
        { new UnitHealed("wren", 3, 17), """{"type":"unitHealed","unit":"wren","amount":3,"hpAfter":17}""" },
        { new UnitBurned("wren", 4, 13), """{"type":"unitBurned","unit":"wren","amount":4,"hpAfter":13}""" },
        { new RockfallStruck("rider-1", new Coord(6, 4), 10, 8), """{"type":"rockfallStruck","unit":"rider-1","at":{"x":6,"y":4},"amount":10,"hpAfter":8}""" },
        { new UnitRested("captain"), """{"type":"unitRested","unit":"captain"}""" },
        { new HungerDrained("keziah", "kinsbane", 5, 1, true), """{"type":"hungerDrained","unit":"keziah","item":"kinsbane","amount":5,"hpAfter":1,"starved":true}""" },
        { new HungerFed("keziah", "kinsbane", 3, 10, 16, 1, false), """{"type":"hungerFed","unit":"keziah","item":"kinsbane","fed":3,"healed":10,"hpAfter":16,"mtBonus":1,"woke":false}""" },
        { new HuntRanOn("keziah", 5), """{"type":"huntRanOn","unit":"keziah","mov":5}""" },
        { new KinsbaneSpoke("keziah", "kinsbane", "kb_starved_eat", "Eat."), """{"type":"kinsbaneSpoke","unit":"keziah","item":"kinsbane","line":"kb_starved_eat","text":"Eat."}""" },
        { new HungerEased("keziah", "kinsbane", 5, 6), """{"type":"hungerEased","unit":"keziah","item":"kinsbane","healed":5,"hpAfter":6}""" },
        { new UnitIgnited("brigand-1", "pell", 2, 2, 1), """{"type":"unitIgnited","unit":"brigand-1","by":"pell","amount":2,"phases":2,"stacks":1}""" },
        { new UnitIgnited("brigand-1", "pell", 4, 2, 2, 2), """{"type":"unitIgnited","unit":"brigand-1","by":"pell","amount":4,"phases":2,"stacks":2,"laid":2}""" },
        { new BurnCashed("brigand-1", "pell", 6, 3), """{"type":"burnCashed","unit":"brigand-1","by":"pell","amount":6,"hpAfter":3}""" },
        { new UnitDrained("pell", "brigand-1", 6, 11), """{"type":"unitDrained","unit":"pell","from":"brigand-1","amount":6,"hpAfter":11}""" },
        { new UnitCursed("brigand-1", "pell", 2, 2, 30), """{"type":"unitCursed","unit":"brigand-1","by":"pell","amount":2,"phases":2,"blind":30}""" },
        { new CurseTicked("brigand-1", 2, 9, "pell", 2, 7), """{"type":"curseTicked","unit":"brigand-1","amount":2,"hpAfter":9,"caster":"pell","healed":2,"casterHpAfter":7}""" },
        { new CurseTicked("brigand-1", 2, 9, null, 0, 0), """{"type":"curseTicked","unit":"brigand-1","amount":2,"hpAfter":9}""" },
        { new UnitMarked("brigand-1", "pell", MagicSchool.Lightning), """{"type":"unitMarked","unit":"brigand-1","by":"pell","school":"lightning"}""" },
        { new MarkCashed("brigand-1", "pell", MagicSchool.Lightning), """{"type":"markCashed","unit":"brigand-1","by":"pell","school":"lightning"}""" },
        { new LineStruck("hask", new Coord(4, 2), ValueList<Coord>.Of(new Coord(3, 2)), ValueList<string>.Of("hale")), """{"type":"lineStruck","unit":"hask","from":{"x":4,"y":2},"line":[{"x":3,"y":2}],"struck":["hale"]}""" },
        { new AreaCastAt("pell", "test_storm", new Coord(6, 5), ValueList<string>.From(["brigand-1", "brigand-2"]), 5), """{"type":"areaCastAt","unit":"pell","spell":"test_storm","at":{"x":6,"y":5},"struck":["brigand-1","brigand-2"],"usesLeft":5}""" },
        { new UnitCleansed("wren", "mira", false, false, false, false, Curse: true), """{"type":"unitCleansed","unit":"wren","by":"mira","burn":false,"chill":false,"stun":false,"curse":true}""" },
        { new HollowRaised("pell", "hollow-brigand-1", "brigand-1", A, 12, 3), """{"type":"hollowRaised","unit":"pell","hollow":"hollow-brigand-1","fallen":"brigand-1","at":{"x":1,"y":2},"hp":12,"phases":3}""" },
        { new HollowCrumbled("hollow-brigand-1", true), """{"type":"hollowCrumbled","unit":"hollow-brigand-1","raiserFell":true}""" },
        { new GroundRaised("pell", "teodor", A, "earthwork"), """{"type":"groundRaised","unit":"pell","target":"teodor","at":{"x":1,"y":2},"terrain":"earthwork"}""" },
        { new UnitStunned("brigand-1", "pell", Side.Enemy), """{"type":"unitStunned","unit":"brigand-1","by":"pell","side":"enemy"}""" },
        { new UnitStunned("pell", "brigand-1", Side.Player, Next: true), """{"type":"unitStunned","unit":"pell","by":"brigand-1","side":"player","next":true}""" },
        { new GroundSundered("pell", A, "earthwork", "wren"), """{"type":"groundSundered","unit":"pell","at":{"x":1,"y":2},"terrain":"earthwork","owner":"wren"}""" },
        { new ArmorDonned("pell", "test_earth_armor", 10, 2, 2), """{"type":"armorDonned","unit":"pell","item":"test_earth_armor","def":10,"mov":2,"phases":2}""" },
        { new ArmorFell("pell", "test_earth_armor"), """{"type":"armorFell","unit":"pell","item":"test_earth_armor"}""" },
        { new RodCaught("wren", "pell", "mage-1"), """{"type":"rodCaught","unit":"wren","aimed":"pell","by":"mage-1"}""" },
        { new RodCharged("wren", MagicSchool.Lightning), """{"type":"rodCharged","unit":"wren","school":"lightning"}""" },
        { new RodChargeSpent("wren", MagicSchool.Lightning), """{"type":"rodChargeSpent","unit":"wren","school":"lightning"}""" },
        { new StunSkipped("brigand-1"), """{"type":"stunSkipped","unit":"brigand-1"}""" },
        { new UnitCleansed("wren", "mira", true, true, false, false), """{"type":"unitCleansed","unit":"wren","by":"mira","burn":true,"chill":true,"stun":false}""" },
        { new UnitFrozen("brigand-1", "pell", Side.Enemy, false), """{"type":"unitFrozen","unit":"brigand-1","by":"pell","side":"enemy"}""" },
        { new UnitFrozen("leader", "pell", Side.Enemy, true, true), """{"type":"unitFrozen","unit":"leader","by":"pell","side":"enemy","boss":true,"next":true}""" },
        { new UnitCleansed("wren", "mira", false, false, false, false, Frozen: true), """{"type":"unitCleansed","unit":"wren","by":"mira","burn":false,"chill":false,"stun":false,"frozen":true}""" },
        { new UnitChilled("brigand-1", "teodor", Side.Enemy), """{"type":"unitChilled","unit":"brigand-1","by":"teodor","side":"enemy"}""" },
        { new UnitFrosted("brigand-1", "rook", 1, 7, true, false, Side.Enemy), """{"type":"unitFrosted","unit":"brigand-1","by":"rook","damage":1,"hpAfter":7,"held":true,"side":"enemy"}""" },
        { new UnitFrosted("hask", "rook", 1, 30, false, true, Side.Enemy), """{"type":"unitFrosted","unit":"hask","by":"rook","damage":1,"hpAfter":30,"held":false,"boss":true,"side":"enemy"}""" },
        { new UnitChilled("teodor", "rider-1", Side.Player, Next: true), """{"type":"unitChilled","unit":"teodor","by":"rider-1","side":"player","next":true}""" },
        { new UnitLocked("rider-1", "teodor", Side.Enemy), """{"type":"unitLocked","unit":"rider-1","by":"teodor","side":"enemy"}""" },
        { new UnitLocked("rider-1", "teodor", Side.Enemy, Next: true), """{"type":"unitLocked","unit":"rider-1","by":"teodor","side":"enemy","next":true}""" },
        { new LockDropped("rider-1", "teodor"), """{"type":"lockDropped","unit":"rider-1","by":"teodor"}""" },
        { new UnitOpened("brigand-1", "captain", 3, 3), """{"type":"unitOpened","unit":"brigand-1","by":"captain","def":3,"res":3}""" },
        { new UnitGrounded("wingrider-1", "ottilie", Side.Enemy), """{"type":"unitGrounded","unit":"wingrider-1","by":"ottilie","side":"enemy"}""" },
        { new UnitGrounded("rook", "archer-1", Side.Player, Next: true), """{"type":"unitGrounded","unit":"rook","by":"archer-1","side":"player","next":true}""" },
        { new HeirloomTurned("teodor", "family_lance", 2, "sound"), """{"type":"heirloomTurned","unit":"teodor","item":"family_lance","stage":2,"stageId":"sound"}""" },
        { new BlowRaised("toll_mauler-1", "teodor", A), """{"type":"blowRaised","unit":"toll_mauler-1","target":"teodor","at":{"x":1,"y":2}}""" },
        { new BlowLanded("toll_mauler-1", "teodor", A, 11, 9), """{"type":"blowLanded","unit":"toll_mauler-1","target":"teodor","at":{"x":1,"y":2},"damage":11,"targetHpAfter":9}""" },
        { new BlowFell("toll_mauler-1", A), """{"type":"blowFell","unit":"toll_mauler-1","at":{"x":1,"y":2}}""" },
        { new BlowBroken("toll_mauler-1", A), """{"type":"blowBroken","unit":"toll_mauler-1","at":{"x":1,"y":2}}""" },
        { new WatchTaken("ottilie", A, "brigand-1", 70), """{"type":"watchTaken","unit":"ottilie","at":{"x":1,"y":2},"passesUp":"brigand-1","passesUpHit":70}""" },
        { new WatchTaken("archer-1", A), """{"type":"watchTaken","unit":"archer-1","at":{"x":1,"y":2}}""" },
        { new WatchFired("ottilie", "rider-1", A, new StrikeEvent(0, "ottilie", "rider-1", true, false, 7, 13)), """{"type":"watchFired","unit":"ottilie","target":"rider-1","at":{"x":1,"y":2},"hit":true,"crit":false,"damage":7,"targetHpAfter":13}""" },
        { new WatchHeld("ottilie", "rider-1", A, 38), """{"type":"watchHeld","unit":"ottilie","target":"rider-1","at":{"x":1,"y":2},"hit":38}""" },
        { new WatchEnded("ottilie"), """{"type":"watchEnded","unit":"ottilie"}""" },
        { new CoverTaken("teodor", "pell", A, "brigand-1", 70), """{"type":"coverTaken","unit":"teodor","ally":"pell","allyLandsOn":{"x":1,"y":2},"passesUp":"brigand-1","passesUpHit":70}""" },
        { new CoverTaken("teodor", "pell", A), """{"type":"coverTaken","unit":"teodor","ally":"pell","allyLandsOn":{"x":1,"y":2}}""" },
        { new CoverFired("teodor", "pell", "rider-1", A, new Coord(1, 3), true, false), """{"type":"coverFired","unit":"teodor","ally":"pell","attacker":"rider-1","at":{"x":1,"y":2},"allyTo":{"x":1,"y":3},"wouldHaveKilled":true,"counters":false}""" },
        { new Recalled(12, 2), """{"type":"recalled","toIndex":12,"chargesLeft":2}""" },
        { new MoveUndone("wren", B, A), """{"type":"moveUndone","unit":"wren","from":{"x":3,"y":4},"to":{"x":1,"y":2}}""" },
        { new ItemUsed("wren", "field_dressing", "wren", 0), """{"type":"itemUsed","unit":"wren","item":"field_dressing","target":"wren","usesLeft":0}""" },
        { new WeaponEquipped("wren", "steel_sword"), """{"type":"weaponEquipped","unit":"wren","item":"steel_sword"}""" },
        { new ArtDeclared("wren", "sunder", "iron_sword", 2), """{"type":"artDeclared","unit":"wren","art":"sunder","item":"iron_sword","cost":2}""" },
        { new WeaponBroke("wren", "iron_sword"), """{"type":"weaponBroke","unit":"wren","item":"iron_sword"}""" },
        { new SpellSpent("mira", "mend"), """{"type":"spellSpent","unit":"mira","item":"mend"}""" },
        { new GroupWoke("fort", WakeCause.Proximity), """{"type":"groupWoke","group":"fort","cause":"proximity"}""" },
        { new GroupWoke("bank", WakeCause.Proximity, ValueList<Lamp>.Of(new Lamp("soldier-1", A), new Lamp("brawler-1", B))), """{"type":"groupWoke","group":"bank","cause":"proximity","lamps":[{"unit":"soldier-1","at":{"x":1,"y":2}},{"unit":"brawler-1","at":{"x":3,"y":4}}]}""" },
        { new GroupWoke("weir", WakeCause.Call, CalledBy: "ford"), """{"type":"groupWoke","group":"weir","cause":"call","by":"ford"}""" },
        { new MapEventFired("riders", false), """{"type":"mapEventFired","name":"riders","blocked":false}""" },
        { new MapEventFired("riders", true, "wall"), """{"type":"mapEventFired","name":"riders","blocked":true,"terrain":"wall"}""" },
        { new TerrainChanged(A, "plain"), """{"type":"terrainChanged","at":{"x":1,"y":2},"terrain":"plain"}""" },
        { new UnitSpawned("rider-1", B, "flank", Behavior.Aggressive), """{"type":"unitSpawned","unit":"rider-1","at":{"x":3,"y":4},"group":"flank","behavior":"aggressive"}""" },
        { new UnitWinded("wren"), """{"type":"unitWinded","unit":"wren"}""" },
        { new FlagSet("gate_open"), """{"type":"flagSet","flag":"gate_open"}""" },
        { new BarReleased("north_bar", new Coord(8, 1), new Coord(8, 0), "plain"), """{"type":"barReleased","event":"north_bar","holder":{"x":8,"y":1},"at":{"x":8,"y":0},"terrain":"plain"}""" },
        { new ArrivalWaits("north1", "brigand", new Coord(8, 0), "wall"), """{"type":"arrivalWaits","event":"north1","template":"brigand","at":{"x":8,"y":0},"terrain":"wall"}""" },
        { new ArrivalWaits("east1", "soldier", new Coord(11, 4), null), """{"type":"arrivalWaits","event":"east1","template":"soldier","at":{"x":11,"y":4}}""" },
        { new FrontFell("north_breach"), """{"type":"frontFell","front":"north_breach"}""" },
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
        { new Move("wren", B, A), """{"type":"move","unit":"wren","to":{"x":3,"y":4},"via":{"x":1,"y":2}}""" },
        { new Attack("wren", "brigand-1"), """{"type":"attack","unit":"wren","target":"brigand-1","slot":null}""" },
        { new Attack("wren", "brigand-1", 1), """{"type":"attack","unit":"wren","target":"brigand-1","slot":1}""" },
        { new Attack("wren", "brigand-1", null, "sunder"), """{"type":"attack","unit":"wren","target":"brigand-1","slot":null,"art":"sunder"}""" },
        { new UseItem("wren", 1), """{"type":"item","unit":"wren","slot":1,"target":null}""" },
        { new UseItem("mira", 0, "wren"), """{"type":"item","unit":"mira","slot":0,"target":"wren"}""" },
        { new UseItem("maud", 0, "wren", "unasked"), """{"type":"item","unit":"maud","slot":0,"target":"wren","art":"unasked"}""" },
        { new Retreat("brigand-1", A), """{"type":"retreat","unit":"brigand-1","to":{"x":1,"y":2}}""" },
        { new Wait("wren"), """{"type":"wait","unit":"wren"}""" },
        { new Cover("teodor", "pell"), """{"type":"cover","unit":"teodor","ally":"pell"}""" },
        { new Exit("wren"), """{"type":"exit","unit":"wren"}""" },
        { new Recover("wren"), """{"type":"recover","unit":"wren"}""" },
        { new Open("wren", B), """{"type":"open","unit":"wren","at":{"x":3,"y":4}}""" },
        { new Drop("wren"), """{"type":"drop","unit":"wren"}""" },
        { new Carry("rook", "wren", new Coord(5, 2), new Coord(5, 3)), """{"type":"carry","unit":"rook","ally":"wren","to":{"x":5,"y":2},"setDown":{"x":5,"y":3}}""" },
        { new StrikeLine("hask", new Coord(3, 2)), """{"type":"strikeLine","unit":"hask","toward":{"x":3,"y":2}}""" },
        { new Breathe("rook", new Coord(3, 2)), """{"type":"breathe","unit":"rook","toward":{"x":3,"y":2}}""" },
        { new Dash("wren", B), """{"type":"dash","unit":"wren","to":{"x":3,"y":4}}""" },
        { new Canto("ansgar", B), """{"type":"canto","unit":"ansgar","to":{"x":3,"y":4}}""" },
        { new EndPhase(), """{"type":"end"}""" },
        { new Recall(4), """{"type":"recall","toIndex":4}""" },
        { new Undo("wren"), """{"type":"undo","unit":"wren"}""" },
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
            LitGroups = ValueList<string>.Of("woods"),
            Flags = ValueList<string>.Of("gate_open"),
            Rapport = ValueList<Rapport>.Of(new Rapport("ottilie", "wren", 8)),
            History = ValueList<BattleState>.Empty,
        };

        Assert.Equal(marked, ProtocolJson.ReadState(ProtocolJson.State(marked, content), content));
    }

    /// <summary>Issue 269: the units that have left through an exit travel in <c>escaped</c>, and a state written before the field existed reads as none.</summary>
    [Fact]
    public void AStateReadsBackEqualWithItsEscapedUnits()
    {
        var (content, state) = PlayedTollgate();
        var leaver = state.UnitsOf(Side.Player).First(u => !u.IsCaptain);
        var marked = state.WithoutUnit(leaver.Id) with { Escaped = ValueList<BattleUnit>.Of(leaver with { Acted = true, Moved = true }), History = ValueList<BattleState>.Empty };
        var json = ProtocolJson.State(marked, content);

        Assert.Equal(marked, ProtocolJson.ReadState(json, content));
        var older = System.Text.Json.Nodes.JsonNode.Parse(ProtocolJson.State(state with { History = ValueList<BattleState>.Empty }, content))!.AsObject();
        older.Remove("escaped");
        Assert.Empty(ProtocolJson.ReadState(older.ToJsonString(), content).Escaped);
    }

    /// <summary>Issue 1094: a campaign side map travels with <c>sideMap</c>, written only when true, and a state without it reads as outside the campaign.</summary>
    [Fact]
    public void AStateReadsBackEqualOnASideMap()
    {
        var (content, state) = PlayedTollgate();
        var plain = state with { History = ValueList<BattleState>.Empty };
        var side = plain with { SideMap = true };

        var json = ProtocolJson.State(side, content);
        Assert.Contains("\"sideMap\":true", json);
        Assert.True(ProtocolJson.ReadState(json, content).InCampaign);
        Assert.Equal(side, ProtocolJson.ReadState(json, content));
        Assert.DoesNotContain("\"sideMap\"", ProtocolJson.State(plain, content));
        Assert.False(ProtocolJson.ReadState(ProtocolJson.State(plain, content), content).SideMap);
    }

    /// <summary>Issue 396: a unit shoved this phase travels with <c>shoved</c>, written only when true, and a state without it reads as not shoved.</summary>
    [Fact]
    public void AStateReadsBackEqualWithAShovedUnit()
    {
        var (content, state) = PlayedTollgate();
        var plain = state with { History = ValueList<BattleState>.Empty };
        var captain = plain.UnitsOf(Side.Player).First();
        var shoved = plain.WithUnit(captain with { Shoved = true });

        var json = ProtocolJson.State(shoved, content);
        Assert.Equal(1, json.Split("\"shoved\":true").Length - 1);
        Assert.Equal(shoved, ProtocolJson.ReadState(json, content));
        Assert.DoesNotContain("\"shoved\"", ProtocolJson.State(plain, content));
        Assert.Equal(plain, ProtocolJson.ReadState(ProtocolJson.State(plain, content), content));
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
        var map = MapFiles.Load(ProtocolSessionTests.TollgateRowFive, content);
        var session = new ProtocolSession(content, BattleState.From(map, content, content.Cast, 163), TextWriter.Null);
        foreach (var line in ProtocolSessionTests.JsonCommands(ProtocolSessionTests.TollgateScript()))
        {
            Assert.StartsWith("{\"ok\":true", session.Answer(line));
        }

        return (content, session.State);
    }
}
