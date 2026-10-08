using Ironwake.Core;

namespace Ironwake.Client;

/// <summary>
/// Reads a <c>play --script</c> file into the commands the client submits (issue 347), so the
/// parity gate drives the client and the console from the same file. Only lines that change
/// the board become commands, <c>open</c>, <c>order</c> and <c>fallback</c> among them (issue 786);
/// a query (<c>forecast</c>, <c>order ... preview</c>, <c>threat</c>, <c>show</c>, <c>reach</c>,
/// <c>map</c>, <c>help</c>, a bare <c>recall</c> or <c>recall list</c>), a blank line or a
/// <c>#</c> comment is skipped, as none of them prints an event. Slots count from 1, as the
/// console reads them, or name an item by id.
/// </summary>
public static class Script
{
    /// <summary>The command a script line names on the board as it stands, or null for a line that prints no event.</summary>
    public static Command? Parse(string line, BattleState state)
    {
        var words = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || words[0].StartsWith('#'))
        {
            return null;
        }

        if (words[0] is "attack" or "end" && words.Length > 1 && words[^1] == "!")
        {
            // The console's confirm on a swing into a lethal counter (issue 975) or an end with a unit
            // lethal if all land (issue 1093); the client's command is the same attack or end.
            words = words[..^1];
        }

        string? art = null;
        if (words[0] is "attack" or "item" && words.Length > 3)
        {
            var at = Array.IndexOf(words, "art", 3);
            if (at >= 0 && at + 1 < words.Length)
            {
                art = words[at + 1];
                words = words.Take(at).Concat(words.Skip(at + 2)).ToArray();
            }
        }

        return (words[0], words.Length) switch
        {
            ("move", 3) when TryCoord(words[2], out var to) => new Move(words[1], to),
            ("move", 5) when words[3] == "via" && TryCoord(words[2], out var to) && TryCoord(words[4], out var via) => new Move(words[1], to, via),
            ("attack", 3) => new Attack(words[1], words[2], null, art),
            ("attack", 4) when SlotOf(state, words[1], words[3]) is { } slot => new Attack(words[1], words[2], slot, art),
            ("item", 3 or 4) when SlotOf(state, words[1], words[2]) is { } slot => new UseItem(words[1], slot, words.Length == 4 ? words[3] : null, art),
            ("wait", 2) => new Wait(words[1]),
            ("canto", 3) when words[2] == "stay" && state.Find(words[1]) is { } stayer => new Canto(stayer.Id, stayer.At),
            ("canto", 3) when TryCoord(words[2], out var to) => new Canto(words[1], to),
            ("exit", 2) => new Exit(words[1]),
            ("recover", 2) => new Recover(words[1]),
            ("drop", 2) => new Drop(words[1]),
            ("talk", 3) => new Talk(words[1], words[2]),
            ("shove", 3) => new Shove(words[1], words[2]),
            ("dash", 3) when TryCoord(words[2], out var dashTo) => new Dash(words[1], dashTo),
            ("breathe", 3) when TryCoord(words[2], out var toward) => new Breathe(words[1], toward),
            ("carry", 5) when TryCoord(words[3], out var carryTo) && TryCoord(words[4], out var setDown) => new Carry(words[1], words[2], carryTo, setDown),
            ("open", 3) when TryCoord(words[2], out var chest) => new Open(words[1], chest),
            ("order", 2 or 3) when OrderOf(words) is { } kind => new Order(kind),
            ("fallback", 3) when words[2] == "stay" && state.Find(words[1]) is { } holder => new FallBack(holder.Id, holder.At),
            ("fallback", 3) when TryCoord(words[2], out var to) => new FallBack(words[1], to),
            ("end", 1) => new EndPhase(),
            ("recall", 2) when int.TryParse(words[1], out var index) => new Recall(index),
            ("undo", 2) => new Undo(words[1]),
            _ => null,
        };
    }

    /// <summary>
    /// The zero-based slot a script word names for <paramref name="unitId"/>: a one-based number,
    /// or an item id the unit carries in exactly one slot, ignoring case (issue 1114, as the
    /// Sim's trace writes a weapon whose slot a swing may have moved). Null when it names none.
    /// </summary>
    private static int? SlotOf(BattleState state, string unitId, string word)
    {
        if (int.TryParse(word, out var typed))
        {
            return typed - 1;
        }

        var named = state.Find(unitId)?.Unit.Inventory.Items
            .Select((item, at) => (item, at))
            .Where(entry => string.Equals(entry.item.ItemId, word, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.at)
            .ToList();
        return named is [var only] ? only : null;
    }

    /// <summary>
    /// Plays a script through a fresh client and returns its event log: each command is
    /// submitted as the player would click it, <c>item</c>, <c>exit</c>, <c>carry</c> and <c>breathe</c> through the action
    /// list and its target pick (<see cref="ApplyByClicks"/>, issue 1308), and each enemy phase
    /// is stepped to its end.
    /// </summary>
    public static string Play(GameContent content, BattleState state, string script)
    {
        var client = new ClientSession(content, state);
        ApplyByClicks(client, script);
        return client.LogText;
    }

    /// <summary>
    /// Submits each command a script names to a client that already exists, stepping each enemy
    /// phase to its end, so the renderer can open a battle part of the way through (the
    /// screenshot mode) on exactly the board the parity gate reaches.
    /// </summary>
    public static void Apply(ClientSession client, string script)
    {
        foreach (var line in script.Split('\n'))
        {
            if (Parse(line, client.State) is { } command)
            {
                client.Submit(command);
                client.Continue();
            }
        }
    }

    /// <summary>
    /// Submits each command a script names as <see cref="Apply"/> does, but takes every
    /// <c>item</c>, <c>exit</c>, <c>carry</c> and <c>breathe</c> line the way a mouse would (issue
    /// 1308): the unit is selected by a click on its tile, the matching row of
    /// <see cref="ClientSession.Actions"/> is taken, an item that needs a target is aimed by a
    /// click on the target's tile, and the drake's verbs by a click on each tile of their pick
    /// (the ally, where to fly, where to set it down; the breath's first tile). A line the core
    /// refuses is submitted as it stands, to print its refusal as the console does; a line it
    /// accepts with no row to take it throws, naming it, so a verb the action list cannot reach
    /// fails the parity gate.
    /// </summary>
    public static void ApplyByClicks(ClientSession client, string script)
    {
        foreach (var line in script.Split('\n'))
        {
            ApplyLineByClicks(client, line);
        }
    }

    /// <summary>
    /// One line of <see cref="ApplyByClicks"/>: <c>item</c>, <c>exit</c>, <c>carry</c> and
    /// <c>breathe</c> the core accepts are taken through the action list and its pick, every other
    /// command is submitted, and the enemy phase it starts is stepped to its end. Returns whether
    /// the line named a command.
    /// </summary>
    public static bool ApplyLineByClicks(ClientSession client, string line)
    {
        var command = Parse(line, client.State);
        if (command is null)
        {
            return false;
        }

        if (command is UseItem or Exit or Carry or Breathe && client.State.Find(UnitOf(command)) is { } unit && Resolver.Apply(client.State, client.Content, command).Accepted)
        {
            TakeByClicks(client, command, unit, line.Trim());
        }
        else
        {
            client.Submit(command);
        }

        client.Continue();
        return true;
    }

    private static string UnitOf(Command command) => command switch
    {
        UseItem use => use.UnitId,
        Exit exit => exit.UnitId,
        Carry carry => carry.UnitId,
        Breathe breathe => breathe.UnitId,
        _ => "",
    };

    private static void TakeByClicks(ClientSession client, Command command, BattleUnit unit, string line)
    {
        client.Select(unit.At);
        var rows = client.Actions();
        if (command is Carry or Breathe)
        {
            TakeStepsByClicks(client, command, rows, line);
            return;
        }

        var index = command is UseItem { TargetId: { } targetId } aimed
            ? rows.ToList().FindIndex(row => row.Pick is ItemPick pick && pick.UnitId == aimed.UnitId && pick.Slot == aimed.Slot && pick.Art == aimed.Art)
            : rows.ToList().FindIndex(row => row.Pick is null && row.Command == command);
        if (index < 0)
        {
            throw new InvalidOperationException($"no action row takes '{line}'");
        }

        if (command is UseItem { TargetId: { } target })
        {
            var at = client.State.Find(target)?.At ?? (TryCoord(target, out var tile) ? tile : (Coord?)null);
            client.TakeAction(index);
            if (client.Pick is null || at is null || client.Click(at.Value) is null)
            {
                throw new InvalidOperationException($"the item pick did not take '{line}': {client.Status}");
            }

            return;
        }

        if (client.TakeAction(index) is null)
        {
            throw new InvalidOperationException($"the action row did not take '{line}': {client.Status}");
        }
    }

    /// <summary>
    /// Takes a carry or a breath (issue 1308, slice 2) through its drake row: the row is taken, and
    /// each tile of the path that names the command is clicked in turn, the last click submitting.
    /// </summary>
    private static void TakeStepsByClicks(ClientSession client, Command command, IReadOnlyList<ActionRow> rows, string line)
    {
        var index = rows.ToList().FindIndex(row => row.Pick is StepPick pick && pick.Paths.Any(path => path.Command == command));
        if (index < 0)
        {
            throw new InvalidOperationException($"no action row takes '{line}'");
        }

        var path = ((StepPick)rows[index].Pick!).Paths.First(p => p.Command == command);
        client.TakeAction(index);
        Command? taken = null;
        foreach (var at in path.Clicks)
        {
            if (client.Pick is null)
            {
                break;
            }

            taken = client.Click(at);
        }

        if (taken != command)
        {
            throw new InvalidOperationException($"the drake pick did not take '{line}': {client.Status}");
        }
    }

    /// <summary>
    /// Plays a whole <c>campaign --script</c> through a fresh campaign presenter (issue 360) and
    /// returns its event log. On the between-map screen a line that changes the record is taken as
    /// the screen's action (buy, repair, drop, take, refine, certify, trial, quest, duty, yard, hire, pick, build,
    /// bench, unbench, march); a listing
    /// prints no event and is skipped. In a battle, <c>leave</c> leaves it and every other line is
    /// read as <see cref="Parse"/> reads a <c>play</c> line and taken as <see cref="ApplyLineByClicks"/>
    /// takes it, items, exits and the drake's verbs through the action list (issue 1308), each
    /// enemy phase stepped to its end.
    /// </summary>
    public static string PlayCampaign(CampaignClient campaign, string script)
    {
        foreach (var line in script.Split('\n'))
        {
            var words = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (campaign.Battle is { } battle)
            {
                if (words is ["leave"])
                {
                    campaign.Leave();
                }
                else
                {
                    ApplyLineByClicks(battle, line);
                }

                continue;
            }

            _ = words switch
            {
                ["buy", var item, var unit] => campaign.Buy(item, unit),
                ["repair", var unit, var slot] when int.TryParse(slot, out var at) => campaign.Repair(unit, at - 1),
                ["drop", var unit, var slot] when int.TryParse(slot, out var at) => campaign.Drop(unit, at - 1),
                ["take", var unit, var index] when int.TryParse(index, out var at) => campaign.Take(unit, at - 1),
                ["refine", var unit, var slot, var stat] when int.TryParse(slot, out var at) => campaign.Refine(unit, at - 1, stat),
                ["certify", var unit, var classId] => campaign.Certify(unit, classId),
                ["trial", var unit, var classId] => campaign.Trial(unit, classId),
                ["quest", var quest, _, ..] => campaign.Quest(quest, words[2..]),
                ["duty", var unit, "forge", var slot, var stat] when int.TryParse(slot, out var at) => campaign.ForgeDuty(unit, at - 1, stat),
                ["duty", var unit, var duty] => campaign.Duty(unit, duty),
                ["yard", var teacher, var student, var weapon] => campaign.Yard(teacher, student, weapon),
                ["hire", var hire] => campaign.Hire(hire),
                ["pick", var claimant] => campaign.Pick(claimant),
                ["meet", var side] => campaign.Meet(side),
                ["build", var room] when campaign.Content.Campaign.Keep.Edit(room) is null => campaign.BuildRoom(room),
                ["build", var edit, var at] when TryCoord(at, out var tile) => campaign.Build(edit, tile),
                ["bench", var unit] => campaign.Bench(unit),
                ["unbench", var unit] => campaign.Unbench(unit),
                ["march"] => campaign.March(),
                ["march", "sure"] => campaign.March(sure: true),
                _ => false,
            };
        }

        return campaign.LogText;
    }

    /// <summary>
    /// The order an <c>order</c> line calls (<c>press</c>, <c>rally</c>, <c>fall back</c> as one
    /// word or two), or null for anything else, a <c>preview</c> query included.
    /// </summary>
    private static OrderKind? OrderOf(string[] words) =>
        Orders.Parse(string.Join(' ', words.Skip(1)));

    private static bool TryCoord(string text, out Coord at)
    {
        at = default;
        var parts = text.Split(',');
        if (parts.Length == 2 && int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y))
        {
            at = new Coord(x, y);
            return true;
        }

        return false;
    }
}
