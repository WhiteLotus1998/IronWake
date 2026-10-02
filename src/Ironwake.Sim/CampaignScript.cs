using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>
/// The full-campaign parity script (issue 786, slice 5): the heuristic player through the whole
/// campaign from map 1, driven through the client's own campaign presenter so every line written
/// is one the client took, with every camp action and every order taken once where the campaign
/// offers it. Each camp tries, in a fixed order, the kinds not yet touched and keeps the first
/// concrete line the record accepts; a refused try writes nothing. A trial or a side map is
/// fought by the same player and left. On a map where orders are open, the captain calls the
/// first order not yet called in place of a plain Wait when it reaches someone; a Fall back is
/// answered by each ally it reached holding its tile. The script is what <c>campaign --script</c>
/// and <c>--campaign-parity</c> both read.
/// </summary>
public static class CampaignScript
{
    /// <summary>Every camp action and battle action the script is meant to take once, in the order a camp tries them.</summary>
    public static readonly IReadOnlyList<string> Kinds = new[]
    {
        "buy", "drop", "bench", "unbench", "repair", "room", "hire", "edit", "certify", "advance", "refine", "trial", "quest",
        "press", "rally", "fall back", "fallback",
    };

    /// <summary>The script, the kinds it took, and the map number the campaign was lost on, or null when it was won.</summary>
    public sealed record Result(string Text, IReadOnlySet<string> Touched, int? LostOn, int Maps);

    /// <summary>
    /// The script for <paramref name="seed"/>. A map named in <paramref name="handPlays"/> opens
    /// with that hand-played script's board lines (the heuristic cannot win Starting Alone alone),
    /// taken while the client accepts them, the heuristic finishing anything left; the camp before
    /// such a map takes no action, so the hand play's slots stand as it was played. <paramref name="variant"/>
    /// varies the camps for a run the heuristic can win: the class list a certify tries is rotated by it,
    /// and when <c>variant / 3</c> is odd no side map is taken.
    /// </summary>
    public static Result Write(GameContent content, string contentDir, ulong seed, IReadOnlyDictionary<string, string>? handPlays = null, string difficulty = CampaignRecord.NormalDifficulty, bool permadeath = true, int variant = 0)
    {
        var client = new CampaignClient(content, contentDir, CampaignRecord.Start(content, seed, difficulty, permadeath));
        var lines = new List<string>
        {
            $"# Issue 786 slice 5: the full campaign from map 1, written by `ironwake-sim --campaign-script {seed} --difficulty {difficulty} --permadeath {(permadeath ? "on" : "off")} --variant {variant}`.",
            $"# Played by `campaign --seed {seed} --difficulty {difficulty} --permadeath {(permadeath ? "on" : "off")}`. Starting Alone and The Mill open with Code's hand plays (seeds 631, 632);",
            "# the heuristic player fights the rest; each camp takes the actions not yet taken that the record accepts.",
        };
        var touched = new HashSet<string>(StringComparer.Ordinal);
        var maps = 0;
        while (!client.Over && !client.Record.IsFinished(content))
        {
            var mapId = client.Record.NextMap(content).MapId;
            var hand = handPlays is not null && handPlays.TryGetValue(mapId, out var played) ? played : null;
            if (hand is null)
            {
                Camp(client, content, contentDir, lines, touched, variant);
            }

            bool marched;
            try
            {
                marched = client.March();
            }
            catch (ArgumentException e)
            {
                // Issue 795: a company thinned below a map's slots throws at march; the run ends there.
                lines.Add("march");
                lines.Add("# march threw: " + e.Message);
                return new Result(string.Concat(lines.Select(l => l + "\n")), touched, maps + 1, maps + 1);
            }

            if (!marched)
            {
                throw new InvalidOperationException($"march refused: {client.Status}");
            }

            lines.Add("march");
            maps++;
            Fight(client, content, lines, touched, hand);
            Leave(client, lines);
        }

        int? lostOn = client.Record.IsFinished(content) ? null : maps;
        return new Result(string.Concat(lines.Select(l => l + "\n")), touched, lostOn, maps);
    }

    private static void Camp(CampaignClient client, GameContent content, string contentDir, List<string> lines, HashSet<string> touched, int variant)
    {
        bool Take(string kind, string line, Func<bool> act)
        {
            if (touched.Contains(kind) || !act())
            {
                return false;
            }

            lines.Add(line);
            touched.Add(kind);
            return true;
        }

        var record = client.Record;
        var units = record.Present(content).ToList();
        var others = units.Where(u => !CampaignRecord.IsCaptain(u, content)).ToList();
        var captain = record.Captain(content);

        // A cheap item bought and dropped; take needs a wagon, which only a chest's overflow fills.
        if (captain is not null && !touched.Contains("buy"))
        {
            var cheapest = client.Stock.Where(id => content.Items.ContainsKey(id)).OrderBy(id => content.Items[id].Price ?? int.MaxValue).ThenBy(id => id, StringComparer.Ordinal).FirstOrDefault();
            if (cheapest is not null && Take("buy", $"buy {cheapest} {captain.Id}", () => client.Buy(cheapest, captain.Id)))
            {
                var slot = client.Record.Find(captain.Id)!.Inventory.Items.Count;
                Take("drop", $"drop {captain.Id} {slot}", () => client.Drop(captain.Id, slot - 1));
            }
        }

        if (others.LastOrDefault() is { } benched && Take("bench", $"bench {benched.Id}", () => client.Bench(benched.Id)))
        {
            Take("unbench", $"unbench {benched.Id}", () => client.Unbench(benched.Id));
        }

        foreach (var unit in units)
        {
            for (var slot = 0; slot < unit.Inventory.Items.Count; slot++)
            {
                var at = slot;
                Take("repair", $"repair {unit.Id} {slot + 1}", () => client.Repair(unit.Id, at));
            }
        }

        foreach (var room in content.Campaign.Keep.Rooms.Where(r => r.Forge).Concat(content.Campaign.Keep.Rooms.Where(r => r.Hires.Count > 0)))
        {
            if (!client.Record.Rooms.Contains(room.Id) && client.BuildRoom(room.Id))
            {
                lines.Add($"build {room.Id}");
                touched.Add("room");
            }
        }

        foreach (var hire in client.Record.HiresOffered(content))
        {
            Take("hire", $"hire {hire.Id}", () => client.Hire(hire.Id));
        }

        foreach (var edit in content.Campaign.Keep.Edits)
        {
            foreach (var at in edit.At)
            {
                Take("edit", $"build {edit.Id} {at}", () => client.Build(edit.Id, at));
            }
        }

        // The captain's ladder is the promotion every run meets; a recruit's certify waits for its advanced form.
        foreach (var unit in client.Record.Present(content))
        {
            var forms = content.Classes.Values.OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
            foreach (var form in forms.Skip(variant % forms.Count).Concat(forms.Take(variant % forms.Count)))
            {
                var kind = form.Advances is null ? "certify" : "advance";
                if (kind == "advance" || CampaignRecord.IsCaptain(unit, content))
                {
                    Take(kind, $"certify {unit.Id} {form.Id}", () => client.Certify(unit.Id, form.Id));
                }
            }
        }

        foreach (var unit in client.Record.Present(content))
        {
            for (var slot = 0; slot < unit.Inventory.Items.Count; slot++)
            {
                var at = slot;
                Take("refine", $"refine {unit.Id} {slot + 1} mt", () => client.Refine(unit.Id, at, "mt"));
            }
        }

        foreach (var trial in content.Campaign.Trials)
        {
            foreach (var unit in client.Record.Present(content))
            {
                if (!touched.Contains("trial") && client.Trial(unit.Id, trial.ClassId))
                {
                    lines.Add($"trial {unit.Id} {trial.ClassId}");
                    touched.Add("trial");
                    Fight(client, content, lines, touched);
                    Leave(client, lines);
                }
            }
        }

        foreach (var quest in client.Record.QuestsOffered(content))
        {
            if (touched.Contains("quest") || (variant / 3) % 2 == 1)
            {
                break;
            }

            var map = QuestMap(client, contentDir, quest);
            var needed = CampaignRecord.QuestAllies(map);
            var allies = client.Record.Present(content)
                .Where(u => u.Id != quest.MemberId && !CampaignRecord.IsCaptain(u, content))
                .OrderByDescending(u => u.Level).ThenBy(u => u.Id, StringComparer.Ordinal)
                .Take(needed).Select(u => u.Id).ToList();
            if (allies.Count == needed && client.Quest(quest.Id, allies))
            {
                lines.Add($"quest {quest.Id} {string.Join(' ', allies)}");
                touched.Add("quest");
                Fight(client, content, lines, touched);
                Leave(client, lines);
            }
        }
    }

    /// <summary>The side map a quest is fought on, read to count the allies it takes.</summary>
    private static MapDefinition QuestMap(CampaignClient client, string contentDir, CampaignQuest quest) =>
        MapFiles.Load(Path.Combine(contentDir, MapFiles.QuestsDirectory, quest.MapId + MapFiles.Extension), client.Content);

    private static void Leave(CampaignClient client, List<string> lines)
    {
        if (!client.Leave())
        {
            throw new InvalidOperationException($"leave refused: {client.Status}");
        }

        lines.Add("leave");
    }

    private static void Fight(CampaignClient client, GameContent content, List<string> lines, HashSet<string> touched, string? hand = null)
    {
        var battle = client.Battle!;
        foreach (var line in (hand ?? "").Split('\n'))
        {
            if (Script.Parse(line, battle.State) is not { } played)
            {
                continue;
            }

            if (!battle.Submit(played))
            {
                break;
            }

            lines.Add(line.Trim());
            battle.Continue();
        }

        var player = new HeuristicPlayer();
        var steps = 0;
        while (!battle.State.Outcome.IsOver)
        {
            if (++steps > 5000)
            {
                throw new InvalidOperationException("the battle did not end in 5000 player steps");
            }

            var commands = player.Next(battle.State, content);
            if (commands.Count == 0)
            {
                throw new InvalidOperationException("the player had nothing to do in its own phase");
            }

            for (var i = 0; i < commands.Count; i++)
            {
                var command = commands[i];
                if (command is Wait wait && TryOrder(battle, content, lines, touched, wait.UnitId))
                {
                    break;
                }

                Submit(battle, lines, command);
                if (battle.State.Outcome.IsOver)
                {
                    return;
                }
            }
        }
    }

    /// <summary>The captain's Wait replaced by the first order not yet called that reaches anyone, answered at once.</summary>
    private static bool TryOrder(ClientSession battle, GameContent content, List<string> lines, HashSet<string> touched, string unitId)
    {
        var state = battle.State;
        if (state.Find(unitId) is not { IsCaptain: true } captain || Orders.Refusal(state) is not null)
        {
            return false;
        }

        foreach (var kind in new[] { OrderKind.Press, OrderKind.Rally, OrderKind.FallBack })
        {
            var word = Orders.Word(kind);
            if (touched.Contains(word) || Orders.Reached(state, content, captain, captain.At, kind).Count == 0)
            {
                continue;
            }

            Submit(battle, lines, new Order(kind));
            touched.Add(word);
            if (kind == OrderKind.FallBack)
            {
                foreach (var ally in battle.State.UnitsOf(Side.Player).Where(u => u.FallingBack).ToList())
                {
                    Submit(battle, lines, new FallBack(ally.Id, ally.At));
                    touched.Add("fallback");
                }
            }

            return true;
        }

        return false;
    }

    private static void Submit(ClientSession battle, List<string> lines, Command command)
    {
        if (!battle.Submit(command))
        {
            throw new InvalidOperationException($"{Program.Script(command)} was refused: {battle.Status}");
        }

        lines.Add(command is FallBack f && battle.State.Find(f.UnitId) is { } u && u.At == f.To ? $"fallback {f.UnitId} stay" : Program.Script(command));
        battle.Continue();
    }
}
