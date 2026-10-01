using Ironwake.Cli;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Names, not ids, on screen (issue 609): narration prints each unit's display name, an enemy
/// numbered only when another on the map shares its name, numbered in file order so a spawn
/// never renumbers anyone mid-battle; every narration line is in sentence case.
/// </summary>
public class UnitNamesTests
{
    /// <summary>One brigand, two archers on the board and a third archer by spawn event.</summary>
    private const string Field = """
        name: Field
        size: 8x3
        win: rout
        turn_limit: 5
        recall: 3
        enemy_level: 1

        ........
        ........
        ........

        units:
        P captain 0,1
        P recruit:wren 0,2
        E brigand 7,0 group:a behavior:hold
        E archer 7,1 group:a behavior:hold
        E archer 7,2 group:a behavior:hold

        events:
        late turn 3 enemy spawn archer 6,0 group:b behavior:hold

        """;

    [Fact]
    public void AnEnemyWithAUniqueNameReadsWithoutANumber()
    {
        var names = UnitNames.Of(Start(map: Field), Starter);

        Assert.Equal("Brigand", names["brigand-1"]);
    }

    [Fact]
    public void EnemiesSharingANameAreNumberedInFileOrderSpawnsIncluded()
    {
        var names = UnitNames.Of(Start(map: Field), Starter);

        Assert.Equal("Archer 1", names["archer-1"]);
        Assert.Equal("Archer 2", names["archer-2"]);
        Assert.Equal("Archer 3", names["archer-3"]);
    }

    [Fact]
    public void APlayerUnitReadsAsItsOwnName()
    {
        var state = Start(map: Field);
        var names = UnitNames.Of(state, Starter);

        Assert.All(state.UnitsOf(Side.Player), u => Assert.Equal(u.Unit.Name, names[u.Id]));
    }

    [Fact]
    public void AFallenPlayerUnitStillReadsAsItsName()
    {
        var state = Start(map: Field).Do(new Wait("wren"));
        var wren = state.Find("wren")!;
        var after = state.WithoutUnit("wren");

        Assert.Equal(wren.Unit.Name, UnitNames.Of(after, Starter)["wren"]);
    }

    [Fact]
    public void AnIdTheBattleNeverHeldReadsAsItself()
    {
        Assert.Equal("stranger-9", UnitNames.Of(Start(map: Field), Starter)["stranger-9"]);
        Assert.Equal("archer-1", UnitNames.None["archer-1"]);
    }

    [Fact]
    public void NarrationPrintsNamesNeverIds()
    {
        var state = Start(map: Field);
        var line = PlaySession.Describe(new UnitMoved("archer-2", new Coord(7, 2), new Coord(6, 2), ValueList<Coord>.From(new[] { new Coord(6, 2) })), Starter, UnitNames.Of(state, Starter));

        Assert.Equal("Archer 2 moves 7,2 -> 6,2", line);
        Assert.DoesNotContain("archer-2", line, StringComparison.Ordinal);
    }

    [Fact]
    public void ARefusalNamesEveryUnitTheBattleKnowsInSentenceCase()
    {
        var state = Start(map: Field);
        var names = UnitNames.Of(state, Starter);
        var unit = state.UnitsOf(Side.Player).First();
        var refusal = names.Message(state.Refused(new Attack(unit.Id, "archer-2")).Message);

        Assert.Equal(UnitNames.Sentence($"{unit.Unit.Name} cannot attack Archer 2 from 0,1"), names.Message($"{unit.Id} cannot attack archer-2 from 0,1"));
        Assert.StartsWith("Archer 2 at 7,2 is ", refusal, StringComparison.Ordinal);
        Assert.Contains($" from {unit.Unit.Name} at {unit.At}", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void ARefusalKeepsAnIdQuotedAsItWasTyped()
    {
        var names = UnitNames.Of(Start(map: Field), Starter);

        Assert.Equal("No living unit 'archer-1' to attack", names.Message("no living unit 'archer-1' to attack"));
    }

    [Fact]
    public void ARefusalNamesAnIdBeforeAPossessive()
    {
        var names = UnitNames.Of(Start(map: Field), Starter);

        Assert.Equal($"Brigand is not on {names["wren"]}'s side", names.Message("brigand-1 is not on wren's side"));
    }

    [Fact]
    public void ARefusalLeavesWordsThatAreNoUnitsId()
    {
        var names = UnitNames.Of(Start(map: Field), Starter);

        Assert.Equal("Usage: item <unit> <slot> [ally]; slots run 1-2; field_dressing; archer", names.Message("usage: item <unit> <slot> [ally]; slots run 1-2; field_dressing; archer"));
        Assert.Equal("Stranger-9 is gone", names.Message("stranger-9 is gone"));
    }

    [Fact]
    public void AWordAfterAnArticleIsARoleNotAUnit()
    {
        var names = UnitNames.Of(Start(map: Field), Starter);

        Assert.Equal("The captain is dead", names.Message("the captain is dead"));
        Assert.Equal(UnitNames.Sentence($"{names["captain"]} is the captain and leads every map"), names.Message("captain is the captain and leads every map"));
    }

    [Fact]
    public void NamedReadsIdsAsNamesWithoutSentenceCase()
    {
        var names = UnitNames.Of(Start(map: Field), Starter);

        Assert.Equal($"seize; {names["wren"]} is left behind", names.Named("seize; wren is left behind"));
    }

    [Fact]
    public void TheCampaignScreenNamesTheRosterAndTheFallen()
    {
        var content = Ironwake.Core.Tests.Maps.MapFixture.Content;
        var record = CampaignRecord.Start(content, 5);
        record = record with { Roster = ValueList<Unit>.From(record.Roster.Where(u => u.Id != "wren")), Fallen = ValueList<string>.From(new[] { "wren" }) };
        var names = UnitNames.Of(record, content);

        Assert.Equal("Alder Fenn", names["captain"]);
        Assert.Equal("Wren", names["wren"]);
        Assert.Equal("Teodor buys Iron Sword for 400; the purse holds 100", names.Message("teodor buys Iron Sword for 400; the purse holds 100"));
        Assert.Equal("No unit 'wren' on the roster", names.Message("no unit 'wren' on the roster"));
        Assert.Equal("him", names.Refer("teodor").Object);
    }

    [Theory]
    [InlineData("wren waits", "Wren waits")]
    [InlineData("-- enemy phase, turn 1 --", "-- Enemy phase, turn 1 --")]
    [InlineData("  4,1 becomes Road", "  4,1 becomes Road")]
    [InlineData("a attacks b\n  b misses a", "A attacks b\n  B misses a")]
    [InlineData("Already upper", "Already upper")]
    [InlineData("", "")]
    public void EveryNarrationLineStartsUpperCaseUnlessItOpensWithANumber(string text, string expected)
    {
        Assert.Equal(expected, UnitNames.Sentence(text));
    }

    /// <summary>Field with a flood on turn 2 over 3,0.</summary>
    private static string Flooded => Field.Replace("late turn 3 enemy spawn archer 6,0 group:b behavior:hold", "late turn 3 enemy spawn archer 6,0 group:b behavior:hold\nflood turn 2 enemy terrain 3,0 ~");

    private static GameContent With(Pronoun pronoun) =>
        Starter with { Pronouns = Starter.Pronouns.SetItem("wren", pronoun) };

    [Fact]
    public void AShippedCastMemberIsReferredToByThePronounTheCastFileGives()
    {
        var names = UnitNames.Of(Start(map: Field), Starter);

        Assert.Equal(("she", "her", "her"), (names.Refer("wren").Subject, names.Refer("wren").Object, names.Refer("wren").Possessive));
    }

    [Fact]
    public void AUnitWithoutAPronounIsReferredToByItsNameAgainNeverIt()
    {
        var names = UnitNames.Of(Start(map: Field), Starter);

        Assert.Equal("Brigand", names.Refer("brigand-1").Subject);
        Assert.Equal("Brigand's", names.Refer("brigand-1").Possessive);
    }

    [Fact]
    public void TheyTakesThePluralVerb()
    {
        var names = UnitNames.Of(Start(map: Field), With(Pronoun.They));

        Assert.EndsWith("; they still watch", PlaySession.Describe(new WatchHeld("wren", "brigand-1", new Coord(1, 1), 40), Starter, names));
    }

    [Fact]
    public void TheLedgersHeldWatchReadsTheWatchersOwnPronoun()
    {
        var she = UnitNames.Of(Start(map: Field), Starter);
        var he = UnitNames.Of(Start(map: Field), With(Pronoun.He));

        Assert.EndsWith("; she still watches", PlaySession.Describe(new WatchHeld("wren", "brigand-1", new Coord(1, 1), 40), Starter, she));
        Assert.EndsWith("; he still watches", PlaySession.Describe(new WatchHeld("wren", "brigand-1", new Coord(1, 1), 40), Starter, he));
    }

    [Fact]
    public void AWakingGroupReadsAsTheGroupWithItsCauseInParentheses()
    {
        var names = UnitNames.Of(Start(map: Field), Starter);

        Assert.Equal("The a group wakes (proximity)", PlaySession.Describe(new GroupWoke("a", WakeCause.Proximity), Starter, names));
        Assert.Equal("The b group wakes (called by the a group)", PlaySession.Describe(new GroupWoke("b", WakeCause.Proximity, CalledBy: "a"), Starter, names));
    }

    [Fact]
    public void ASpawnedUnitArrivesWithItsGroup()
    {
        var names = UnitNames.Of(Start(map: Field), Starter);

        Assert.Equal("  Archer 3 arrives at 6,0 with the b group, hold", PlaySession.Describe(new UnitSpawned("archer-3", new Coord(6, 0), "b", Behavior.Hold), Starter, names));
    }

    [Fact]
    public void AFiredSpawnEventReadsAsReinforcementsNotItsMapId()
    {
        var names = UnitNames.Of(Start(map: Field), Starter);

        Assert.Equal("Reinforcements arrive", PlaySession.Describe(new MapEventFired("late", false), Starter, names));
        Assert.Equal("Reinforcements are blocked: a unit holds 6,0", PlaySession.Describe(new MapEventFired("late", true), Starter, names));
    }

    [Fact]
    public void ASpawnBarredByTerrainNamesTheTerrainNotAUnit()
    {
        var names = UnitNames.Of(Start(map: Field), Starter);

        Assert.Equal("Reinforcements are blocked: 6,0 is wall", PlaySession.Describe(new MapEventFired("late", true, "wall"), Starter, names));
    }

    [Fact]
    public void AFiredTerrainEventReadsAsTheGroundChanging()
    {
        var names = UnitNames.Of(Start(map: Flooded), Starter);

        Assert.Equal("The ground changes", PlaySession.Describe(new MapEventFired("flood", false), Starter, names));
        Assert.Equal("3,0 does not change: a unit holds it", PlaySession.Describe(new MapEventFired("flood", true), Starter, names));
    }

    [Fact]
    public void AnEventTheMapDoesNotListKeepsItsName()
    {
        Assert.Equal("Event drill", PlaySession.Describe(new MapEventFired("drill", false), Starter, UnitNames.None));
    }

    [Fact]
    public void ARejectionReferringBackToTheUnitUsesItsPronounAndVerb()
    {
        var state = Start(map: Field);

        Assert.Equal("wren cannot Canto: she has no Canto", Resolver.Apply(state, Starter, new Canto("wren", new Coord(1, 2))).Rejection!.Message);
        Assert.Equal("wren cannot Canto: they have no Canto", Resolver.Apply(state, With(Pronoun.They), new Canto("wren", new Coord(1, 2))).Rejection!.Message);
    }

    [Fact]
    public void ARewindNamesTheUnitsItGivesBackAndReturnsByTheirNames()
    {
        var state = Start(map: Field);
        var names = UnitNames.Of(state, Starter);
        var cost = new RecallCost(
            3, 1, ValueList<string>.Of("archer-2"), 0, 0, 0, ValueList<string>.Of("archer-3"),
            ValueList<string>.Of("wren"), ValueList<HpReturn>.Of(new HpReturn("wren", 9), new HpReturn("captain", 4)));

        var text = PlaySession.UndoText(cost, names);

        Assert.Equal($"gives back 1 kill (Archer 2); returns {names["wren"]} alive at 9 hp, 4 hp to {names["captain"]}, Archer 3 not yet arrived", text);
        Assert.DoesNotContain("archer-", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRecallBrowserIsInSentenceCase()
    {
        var rows = PlaySession.RecallRows(Start(map: Field), Starter, Array.Empty<string>());

        Assert.StartsWith("Recall: 3 of 3 charges left", rows[0].Text, StringComparison.Ordinal);
        Assert.Equal("  No state to return to yet", rows[1].Text);
        Assert.All(rows, row => Assert.Equal(UnitNames.Sentence(row.Text), row.Text));
    }

    [Fact]
    public void ShowNamesTheUnitAndItsTargetsAsAReaderSeesThem()
    {
        var state = Start(map: Field);
        var archer = state.Find("archer-2")!;

        var lines = PlaySession.ShowLines(state, Starter, archer);

        Assert.StartsWith("Archer 2, ", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("  HP ", lines[1], StringComparison.Ordinal);
        Assert.All(lines, line => Assert.DoesNotContain("archer-2", line, StringComparison.Ordinal));
        Assert.All(lines, line => Assert.Equal(UnitNames.Sentence(line), line));
    }

    [Fact]
    public void TheConsolesShowAddsTheIdACommandTypesWhereItDiffersFromTheName()
    {
        var state = Start(map: Field);

        Assert.StartsWith("Archer 2 (archer-2), ", PlaySession.ShowLines(state, Starter, state.Find("archer-2")!, typed: true)[0], StringComparison.Ordinal);
        Assert.StartsWith(state.Find("wren")!.Unit.Name + ", ", PlaySession.ShowLines(state, Starter, state.Find("wren")!, typed: true)[0], StringComparison.Ordinal);
    }
}
