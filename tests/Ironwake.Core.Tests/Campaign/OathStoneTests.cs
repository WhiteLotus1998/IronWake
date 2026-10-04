using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// The Oath Stone's pins (issue 635 slice 16, Design Table rounds 303 to 305): the bound man is
/// Joab, held off the spare route's reach; the envoy keeps his fort; <c>threat</c> prints a counter
/// that would feed the scythe; and the board and the record say which side of the hunger the oath fell on.
/// </summary>
public class OathStoneTests
{
    private static readonly GameContent Content = MapFixture.Content;

    private const string Bound = "brigand-1";
    private const string Boss = "bandit_leader-1";

    private static string Field(string captainAt = "0,4") =>
        $"""
        name: Oath Field
        size: 7x5
        win: defeat_boss
        turn_limit: 10
        recall: 3
        enemy_level: 1
        kinsbane: keziah
        freed: 2,2 by keep

        .......
        .......
        .......
        .......
        .......

        units:
        P captain {captainAt}
        P recruit:keziah 1,2
        B bandit_leader 6,0 group:keep behavior:boss
        E brigand 2,2 group:oath behavior:hold

        """;

    private static BattleState Start(ulong seed, string captainAt = "0,4")
    {
        var state = BattleState.From(MapFormat.Parse("oath.map", Field(captainAt), Content), Content, Content.Cast, seed);
        return state.WithUnit(state.Find(Bound)! with { Hp = 1 });
    }

    private static string CaptainId(BattleState state) => state.UnitsOf(Side.Player).Single(u => u.IsCaptain).Id;

    private static int Slot(BattleState state, string itemId) =>
        state.Find("keziah")!.Unit.Inventory.Items.ToList().FindIndex(s => s.ItemId == itemId);

    /// <summary>The first seed from 1 on which <paramref name="attack"/> takes the bound brigand off the board.</summary>
    private static ApplyResult Kill(Func<BattleState, Attack> attack, string captainAt = "0,4", Func<BattleState, BattleState>? setup = null)
    {
        for (ulong seed = 1; seed < 200; seed++)
        {
            var state = Start(seed, captainAt);
            state = setup is null ? state : setup(state);
            var result = Resolver.Apply(state, Content, attack(state));
            Assert.True(result.Accepted, result.Rejection?.Message);
            if (result.Next.Find(Bound) is null)
            {
                return result;
            }
        }

        throw new InvalidOperationException("no seed under 200 kills the bound brigand");
    }

    [Fact]
    public void AScytheKillOfTheBoundManFeedsAndTheBoardRecordsItFed()
    {
        var result = Kill(s => new Attack("keziah", Bound, Slot(s, Kinsbane.ItemId)));

        Assert.Contains(result.Events, e => e is HungerFed { UnitId: "keziah" });
        Assert.Equal(BondFate.Fell, result.Next.Bond);
        Assert.Equal(new BondKill("keziah", true), result.Next.BondKilledBy);
        Assert.Equal(OathSide.Fed, Oath.Of(result.Next, "keziah"));
    }

    [Fact]
    public void AnAxeKillByTheBearerFeedsNothingAndIsRefused()
    {
        var result = Kill(s => new Attack("keziah", Bound, Slot(s, "iron_axe")));

        Assert.DoesNotContain(result.Events, e => e is HungerFed);
        Assert.Equal(new BondKill("keziah", false), result.Next.BondKilledBy);
        Assert.Equal(OathSide.Refused, Oath.Of(result.Next, "keziah"));
    }

    [Fact]
    public void AnAllysKillIsOther()
    {
        var result = Kill(s => new Attack(CaptainId(s), Bound), captainAt: "3,2");

        Assert.Equal(CaptainId(result.Next), result.Next.BondKilledBy!.KillerId);
        Assert.Equal(OathSide.Other, Oath.Of(result.Next, "keziah"));
    }

    [Fact]
    public void ACounterKillByTheScytheOnTheEnemyPhaseIsRecordedFed()
    {
        for (ulong seed = 1; seed < 200; seed++)
        {
            var state = Start(seed) with { Phase = Side.Enemy };
            var result = Resolver.Apply(state, Content, new Attack(Bound, "keziah"));
            Assert.True(result.Accepted, result.Rejection?.Message);
            if (result.Next.Find(Bound) is null)
            {
                Assert.Equal(new BondKill("keziah", true), result.Next.BondKilledBy);
                return;
            }
        }

        Assert.Fail("no seed under 200 counter-kills the bound brigand");
    }

    [Fact]
    public void AFreedManIsSparedAndOneStandingIsNoAnswerYet()
    {
        var standing = Start(1);
        Assert.Null(Oath.Of(standing, "keziah"));
        Assert.Equal(OathSide.Spared, Oath.Of(standing with { Bond = BondFate.Freed }, "keziah"));
        Assert.Equal(OathSide.Other, Oath.Of(standing with { Bond = BondFate.Fell }, "keziah"));
    }

    [Theory]
    [InlineData(OathSide.Fed, "fed")]
    [InlineData(OathSide.Spared, "spared")]
    [InlineData(OathSide.Refused, "refused")]
    [InlineData(OathSide.Other, "other")]
    public void TheOathReadsAsItsWord(OathSide side, string word)
    {
        Assert.Equal(word, Oath.Word(side));
        Assert.Equal(side, Oath.Parse(word));
    }

    [Fact]
    public void ThreatPrintsACounterThatWouldFeedTheScythe()
    {
        var state = Start(1);
        var keziah = state.Find("keziah")!;

        var text = PlaySession.ThreatText(state, Content, keziah, keziah.At, Queries.Threats(state, Content, keziah, keziah.At)!, Queries.SleepingThreats(state, Content, keziah, keziah.At)!);

        Assert.Contains($"    Counter kills on hit: Keziah +{Kinsbane.FeedHeal} HP, to max {keziah.MaxHp(Content)} (Kinsbane feeds, fed 1)", text);
    }

    [Fact]
    public void ACounterThatKillsOnlyIfEveryStrikeLandsSaysSo()
    {
        var state = Start(1);
        var keziah = state.Find("keziah")!;
        var striker = state.Find(Bound)! with { Hp = 14 };
        var doubled = new CombatForecast(new SideForecast(true, 6, 70, 70, 0, false), new SideForecast(true, 8, 60, 60, 0, true), RollScheme.OneRoll);
        var single = doubled with { Defender = doubled.Defender with { Damage = 14, Doubles = false } };

        Assert.StartsWith("counter kills if all land: Keziah", Kinsbane.CounterFeedLine(keziah, striker, doubled, Content, "Keziah"));
        Assert.StartsWith("counter kills on hit: Keziah", Kinsbane.CounterFeedLine(keziah, striker, single, Content, "Keziah"));
        Assert.Null(Kinsbane.CounterFeedLine(keziah, striker, doubled with { Defender = doubled.Defender with { Doubles = false } }, Content, "Keziah"));
    }

    [Fact]
    public void ThreatPrintsNoFeedForAnAxeInFrontOrAWokenScytheOrACounterThatCannotKill()
    {
        var state = Start(1);
        var keziah = state.Find("keziah")!;
        string Text(BattleState s) => PlaySession.ThreatText(s, Content, s.Find("keziah")!, keziah.At, Queries.Threats(s, Content, s.Find("keziah")!, keziah.At)!, Queries.SleepingThreats(s, Content, s.Find("keziah")!, keziah.At)!);
        var scythe = keziah.Unit.Inventory.Items[Slot(state, Kinsbane.ItemId)];

        var axe = state.WithUnit(keziah.WithSlotInFront(Slot(state, "iron_axe")));
        var woken = state.WithUnit(keziah with { Unit = keziah.Unit with { Inventory = keziah.Unit.Inventory.Replace(Slot(state, Kinsbane.ItemId), scythe with { Fed = Kinsbane.WakeKills }) } });
        var sturdy = state.WithUnit(state.Find(Bound)! with { Hp = 99 });

        Assert.Contains("Brigand", Text(axe));
        Assert.DoesNotContain("Counter kill", Text(axe));
        Assert.DoesNotContain("Counter kill", Text(woken));
        Assert.DoesNotContain("Counter kill", Text(sturdy));
    }

    private static MapDefinition OathStone() =>
        MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), MapFiles.QuestsDirectory, "the_oath_stone.map"), Content);

    [Fact]
    public void TheBoundManIsJoabANamedManWhoHoldsTheDoor()
    {
        var map = OathStone();
        var joab = map.Placements.OfType<EnemyPlacement>().Single(p => p.At == map.Bond!.Bound);

        Assert.Equal(("joab", Behavior.Hold), (joab.TemplateId, joab.Behavior));
        Assert.Equal("Joab", Content.Unit("joab").Name);
        Assert.StartsWith("Keziah knows the man on the door. Joab kept the lamps at her mother's shrine", Content.Campaign.Quest("keziah_2")!.Before[0]);
    }

    /// <summary>
    /// Issue 940: the rear rider comes with the rear brigand on turn 5, so the camp's two are the only
    /// meals before the road and the rider is fought on it, not in the camp.
    /// </summary>
    [Fact]
    public void TheRearRiderArrivesWithTheBrigandOnTurnFive()
    {
        var turns = OathStone().Events.ToDictionary(e => e.Name, e => ((TurnTrigger)e.Trigger).Turn);

        Assert.Equal(5, turns["rear1"]);
        Assert.Equal(5, turns["rear2"]);
        Assert.Contains("More come up behind you from turn 5, announced.", string.Join(" ", Content.Campaign.Quest("keziah_2")!.Before));
    }

    [Fact]
    public void TheEnvoyKeepsHisFort()
    {
        var map = OathStone();
        var envoy = map.Placements.OfType<EnemyPlacement>().Single(p => p.IsBoss);

        Assert.Equal((new Coord(12, 3), Behavior.Boss), (envoy.At, envoy.Behavior));
        Assert.Equal("fort", map.TerrainIdAt(envoy.At));
    }

    /// <summary>
    /// Round 304's layout rule: Keziah can walk from her slot to a tile that strikes the envoy without
    /// ever entering the bound man's reach for any weapon he carries, so a counter never feeds the
    /// scythe on the spare route unchosen.
    /// </summary>
    [Fact]
    public void TheSpareRouteNeverEntersTheBoundMansReach()
    {
        var map = OathStone();
        var door = map.Bond!.Bound;
        var envoy = map.Placements.OfType<EnemyPlacement>().Single(p => p.IsBoss).At;
        var weapons = Content.Unit("joab").Inventory.Items.Where(s => Content.Weapons.ContainsKey(s.ItemId)).Select(s => Content.Weapon(s.ItemId)).ToList();
        Assert.NotEmpty(weapons);
        bool InReach(Coord c) => c == door || weapons.Any(w => w.InRange(c.DistanceTo(door)));
        var movement = Content.Class(Content.Unit("keziah").ClassId).Movement;
        var start = map.Placements.OfType<PlayerPlacement>().Single(p => p.Slot == PlayerSlot.Captain).At;
        var strikes = new[] { 1 }.Concat(Content.Unit("keziah").Inventory.Items.Where(s => Content.Weapons.ContainsKey(s.ItemId)).Select(s => Content.Weapon(s.ItemId).MaxRange)).Max();

        var seen = new HashSet<Coord> { start };
        var queue = new Queue<Coord>(seen);
        var reached = false;
        while (queue.Count > 0 && !reached)
        {
            var at = queue.Dequeue();
            reached = at.DistanceTo(envoy) <= strikes && at.DistanceTo(envoy) >= 1;
            foreach (var next in new[] { at with { X = at.X + 1 }, at with { X = at.X - 1 }, at with { Y = at.Y + 1 }, at with { Y = at.Y - 1 } })
            {
                if (map.Contains(next) && next != envoy && !InReach(next) && map.TerrainAt(next, Content).IsPassable(movement) && seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        Assert.True(reached, "no route to the envoy stays out of the bound man's reach");
    }

    [Fact]
    public void AWonOathStoneRecordsTheSideAndALostOneWritesNothing()
    {
        var earlier = new[] { new QuestWon("keziah_1", 7) };
        var record = CampaignRecord.StartAt(Content, 701, "ironwake_keep", pick: "keziah") with { QuestsWon = ValueList<QuestWon>.Of(earlier) };
        var opening = record.BeginQuest(OathStone(), "keziah_2", "ottilie", Content);
        var players = opening.UnitsOf(Side.Player).ToList();
        var won = opening with { Units = ValueList<BattleUnit>.From(players), Bond = BondFate.Fell, BondKilledBy = new BondKill("keziah", false), History = ValueList<BattleState>.Of(opening) };
        Assert.Equal(BattleResult.Won, won.Outcome.Result);

        Assert.Equal(OathSide.Refused, record.AfterQuest(won, "keziah_2", Content).Record.KeziahOath);
        Assert.Equal(OathSide.Spared, record.AfterQuest(won with { Bond = BondFate.Freed, BondKilledBy = null }, "keziah_2", Content).Record.KeziahOath);

        var lost = won with { Units = ValueList<BattleUnit>.From(opening.Units), Turn = opening.Map.TurnLimit + 1 };
        Assert.Equal(BattleResult.Lost, lost.Outcome.Result);
        Assert.Null(record.AfterQuest(lost, "keziah_2", Content).Record.KeziahOath);
    }

    [Fact]
    public void TheOathRoundTripsOnTheRecordAndABadWordIsRefused()
    {
        var record = CampaignRecord.StartAt(Content, 701, "ironwake_keep", pick: "keziah") with { KeziahOath = OathSide.Refused };

        var json = ProtocolJson.Campaign(record);
        Assert.Contains("\"keziahOath\":\"refused\"", json);
        Assert.Equal(OathSide.Refused, ProtocolJson.ReadCampaign(json, Content).KeziahOath);
        Assert.DoesNotContain("keziahOath", ProtocolJson.Campaign(record with { KeziahOath = null }));
        var error = Assert.Throws<ProtocolException>(() => ProtocolJson.ReadCampaign(json.Replace("\"refused\"", "\"eaten\""), Content));
        Assert.Contains("keziahOath", error.Message);
    }
}
