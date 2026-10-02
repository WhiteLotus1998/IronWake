using Ironwake.Core;

namespace Ironwake.Client;

/// <summary>
/// Reads a <c>play --script</c> file into the commands the client submits (issue 347), so the
/// parity gate drives the client and the console from the same file. Only lines that change
/// the board become commands, <c>open</c>, <c>order</c> and <c>fallback</c> among them (issue 786);
/// a query (<c>forecast</c>, <c>order ... preview</c>, <c>threat</c>, <c>show</c>, <c>reach</c>,
/// <c>map</c>, <c>help</c>, a bare <c>recall</c> or <c>recall list</c>), a blank line or a
/// <c>#</c> comment is skipped, as none of them prints an event. Slots count from 1, as the
/// console reads them.
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

        string? art = null;
        if (words[0] == "attack" && words.Length > 3)
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
            ("attack", 4) when int.TryParse(words[3], out var slot) => new Attack(words[1], words[2], slot - 1, art),
            ("item", 3 or 4) when int.TryParse(words[2], out var slot) => new UseItem(words[1], slot - 1, words.Length == 4 ? words[3] : null),
            ("wait", 2) => new Wait(words[1]),
            ("canto", 3) when words[2] == "stay" && state.Find(words[1]) is { } stayer => new Canto(stayer.Id, stayer.At),
            ("canto", 3) when TryCoord(words[2], out var to) => new Canto(words[1], to),
            ("exit", 2) => new Exit(words[1]),
            ("recover", 2) => new Recover(words[1]),
            ("drop", 2) => new Drop(words[1]),
            ("shove", 3) => new Shove(words[1], words[2]),
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
    /// Plays a script through a fresh client and returns its event log: each command is
    /// submitted as the player would click it, and each enemy phase is stepped to its end.
    /// </summary>
    public static string Play(GameContent content, BattleState state, string script)
    {
        var client = new ClientSession(content, state);
        Apply(client, script);
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
    /// Plays a whole <c>campaign --script</c> through a fresh campaign presenter (issue 360) and
    /// returns its event log. On the between-map screen a line that changes the record is taken as
    /// the screen's action (buy, repair, drop, take, refine, certify, trial, quest, hire, build,
    /// bench, unbench, march); a listing
    /// prints no event and is skipped. In a battle, <c>leave</c> leaves it and every other line is
    /// read as <see cref="Parse"/> reads a <c>play</c> line, each enemy phase stepped to its end.
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
                else if (Parse(line, battle.State) is { } command)
                {
                    battle.Submit(command);
                    battle.Continue();
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
                ["hire", var hire] => campaign.Hire(hire),
                ["build", var room] when campaign.Content.Campaign.Keep.Edit(room) is null => campaign.BuildRoom(room),
                ["build", var edit, var at] when TryCoord(at, out var tile) => campaign.Build(edit, tile),
                ["bench", var unit] => campaign.Bench(unit),
                ["unbench", var unit] => campaign.Unbench(unit),
                ["march"] => campaign.March(),
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
