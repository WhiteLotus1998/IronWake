using System.Text.Json;
using Ironwake.Content.Protocol;
using Ironwake.Core;

namespace Ironwake.Cli;

/// <summary>
/// <c>play --protocol</c> (issue 25): the battle as JSON lines, one request per input line
/// and one answer per output line, for a renderer in another process and for bug reports
/// that replay from a JSON command list. The shapes are docs/PROTOCOL.md's. The first line
/// out is the opening state in full. A command answers with its events, each carrying the
/// console's own line as <c>text</c>, and the board after it; <c>end</c> plays the enemy
/// phase through <see cref="EnemyAi.Plan"/> and the resolver exactly as <c>--script</c>
/// does, so its events are the player's end of phase and the whole enemy phase. A query
/// asks the core and answers with the data and the console's text; it changes nothing.
/// A refused command or a malformed line answers <c>ok: false</c> with a reason and a
/// message, and the session goes on. Like <see cref="PlaySession"/>, it computes nothing
/// about the rules. On a dusk map (DESIGN.md 13.7, issue 302) the session hides what the
/// console hides: every state is the player's view (<see cref="ProtocolJson.WriteState"/>),
/// an enemy's Move or Wait in the dark answers as one <c>unseenActs</c> event, and a query
/// naming an enemy no player unit sees is refused as no such unit. <c>--omniscient</c>
/// turns the view off for bug reports, and the first line then carries <c>omniscient: true</c>.
/// </summary>
public sealed class ProtocolSession
{
    private readonly GameContent _content;
    private readonly TextWriter _out;
    private readonly bool _omniscient;
    private BattleState _state;

    public ProtocolSession(GameContent content, BattleState state, TextWriter output, bool omniscient = false)
    {
        _content = content;
        _state = state;
        _out = output;
        _omniscient = omniscient;
    }

    /// <summary>The line the console prints, and the protocol's text, for an enemy command no player unit sees.</summary>
    public const string DarkLine = "enemy: something in the dark acts";

    private bool PlayerView => !_omniscient;

    public BattleState State => _state;

    /// <summary>Reads requests until the input ends and returns the exit code <c>play</c> returns: 0 on a win, 1 otherwise.</summary>
    public int Run(TextReader input)
    {
        _out.WriteLine(ProtocolJson.Write(w =>
        {
            w.WriteStartObject();
            w.WriteBoolean("ok", true);
            w.WriteNumber("protocolVersion", ProtocolVersion.Current);
            w.WriteNumber("rulesVersion", RulesVersion.Current);
            if (_omniscient)
            {
                w.WriteBoolean("omniscient", true);
            }

            w.WritePropertyName("state");
            ProtocolJson.WriteState(w, _state, _content, full: true, PlayerView);
            w.WriteEndObject();
        }));
        while (input.ReadLine() is { } line)
        {
            var text = line.Trim();
            if (text.Length == 0)
            {
                continue;
            }

            _out.WriteLine(Answer(text));
        }

        return _state.Outcome.Result == BattleResult.Won ? 0 : 1;
    }

    /// <summary>The one-line answer to one request line.</summary>
    public string Answer(string line)
    {
        try
        {
            using var doc = ProtocolJson.Parse(line);
            var request = doc.RootElement;
            if (request.ValueKind != JsonValueKind.Object)
            {
                throw new ProtocolException("a request is a JSON object");
            }

            return request.TryGetProperty("query", out _) ? Query(request) : Command(ProtocolJson.ReadCommand(request));
        }
        catch (ProtocolException e)
        {
            return Error("badRequest", e.Message);
        }
    }

    private string Command(Command command)
    {
        var events = new List<GameEvent?>();
        var result = Resolver.Apply(_state, _content, command);
        if (!result.Accepted)
        {
            return Error(ProtocolJson.Name(result.Rejection!.Reason), result.Rejection.Message);
        }

        _state = result.Next;
        events.AddRange(result.Events);
        if (command is EndPhase)
        {
            foreach (var enemy in EnemyAi.Plan(_state, _content))
            {
                var dark = PlayerView && Dusk.InTheDark(_state, _content, enemy);
                var step = Resolver.Apply(_state, _content, enemy);
                if (!step.Accepted)
                {
                    throw new InvalidOperationException($"the enemy AI's {enemy} was rejected: {step.Rejection!.Message}");
                }

                _state = step.Next;
                if (dark)
                {
                    events.Add(null);
                }
                else
                {
                    events.AddRange(step.Events);
                }
            }
        }

        return ProtocolJson.Write(w =>
        {
            w.WriteStartObject();
            w.WriteBoolean("ok", true);
            w.WriteStartArray("events");
            foreach (var e in events)
            {
                if (e is null)
                {
                    w.WriteStartObject();
                    w.WriteString("type", "unseenActs");
                    w.WriteString("text", DarkLine);
                    w.WriteEndObject();
                }
                else
                {
                    ProtocolJson.WriteEvent(w, e, PlaySession.Describe(e, _content));
                }
            }

            w.WriteEndArray();
            w.WritePropertyName("state");
            ProtocolJson.WriteState(w, _state, _content, full: false, PlayerView);
            w.WriteEndObject();
        });
    }

    private string Query(JsonElement request)
    {
        var name = ProtocolJson.RequiredString(request, "query");
        if (name == "state")
        {
            return Ok(name, w =>
            {
                w.WritePropertyName("state");
                ProtocolJson.WriteState(w, _state, _content, full: true, PlayerView);
            });
        }

        if (name is not ("reachable" or "targets" or "forecast" or "threat"))
        {
            throw new ProtocolException($"query '{name}' is not one of: state, reachable, targets, forecast, threat");
        }

        var unitId = ProtocolJson.RequiredString(request, "unit");
        if (_state.Find(unitId) is not { } unit || Hidden(unit))
        {
            return Error(ProtocolJson.Name(RejectionReason.NoSuchUnit), $"no living unit '{unitId}'");
        }

        switch (name)
        {
            case "reachable":
                return Ok(name, w =>
                {
                    w.WriteString("unit", unit.Id);
                    w.WritePropertyName("reach");
                    ProtocolJson.WriteReach(w, Queries.Reachable(_state, _content, unit));
                });
            case "targets":
                return Ok(name, w =>
                {
                    w.WriteString("unit", unit.Id);
                    w.WriteStartArray("targets");
                    foreach (var target in Queries.Targets(_state, _content, unit).Where(t => !Hidden(t)))
                    {
                        w.WriteStringValue(target.Id);
                    }

                    w.WriteEndArray();
                });
            case "forecast":
                return Forecast(request, unit);
            default:
                return Threat(request, unit);
        }
    }

    /// <summary>
    /// The forecast query: <see cref="Queries.Forecast(BattleState, GameContent, BattleUnit, BattleUnit, Coord, int?, string?)"/>
    /// from the unit's tile or <c>from</c>, with the weapon in <c>slot</c> (0-based) or the
    /// equipped one, and the combat art named by <c>art</c> when given (issue 68). Refused
    /// with the console's reasons when there is no forecast.
    /// </summary>
    private string Forecast(JsonElement request, BattleUnit unit)
    {
        var targetId = ProtocolJson.RequiredString(request, "target");
        var slot = ProtocolJson.OptionalInt(request, "slot");
        var art = ProtocolJson.OptionalString(request, "art");
        var from = ProtocolJson.OptionalCoord(request, "from");
        var tile = from ?? unit.At;
        if (tile != unit.At && !Queries.CanStandOn(_state, _content, unit, tile))
        {
            return Error(ProtocolJson.Name(unit.Moved ? RejectionReason.AlreadyMoved : RejectionReason.OutOfReach), unit.Moved ? $"{unit.Id} has already moved this phase; forecast from {unit.At}" : $"{unit.Id} cannot move to {tile}");
        }

        if (_state.Find(targetId) is not { } target || (PlayerView && !Dusk.Seen(_state, target, unit.Id, tile)))
        {
            return Error(ProtocolJson.Name(RejectionReason.NoSuchTarget), $"no living unit '{targetId}'");
        }

        if (Queries.Forecast(_state, _content, unit, target, tile, slot, art) is not { } forecast)
        {
            var rejection = Queries.WeaponRefusal(_content, unit, slot, art);
            return rejection is not null
                ? Error(ProtocolJson.Name(rejection.Reason), rejection.Message)
                : Error(ProtocolJson.Name(RejectionReason.OutOfRange), $"{unit.Id} cannot attack {target.Id} from {tile}");
        }

        return Ok("forecast", w =>
        {
            w.WriteString("unit", unit.Id);
            w.WriteString("target", target.Id);
            w.WriteStartObject("from");
            w.WriteNumber("x", tile.X);
            w.WriteNumber("y", tile.Y);
            w.WriteEndObject();
            w.WritePropertyName("forecast");
            ProtocolJson.WriteForecast(w, forecast);
            w.WriteString("weapon", unit.Unit.Inventory.Items[slot ?? unit.EquippedSlot(_content)].ItemId);
            WriteCounterWeapon(w, target, forecast);
            w.WriteString("text", PlaySession.ForecastText(_state, _content, unit, target, forecast, tile, from is not null, slot, art));
        });
    }

    /// <summary>
    /// <c>counterWeapon</c>: the item id the defender counters with, its equipped weapon, or null
    /// when it does not counter. Written on every forecast and threat line whatever the console's
    /// filter says (issue 313); a renderer may hide it.
    /// </summary>
    private void WriteCounterWeapon(Utf8JsonWriter w, BattleUnit defender, CombatForecast forecast)
    {
        var slot = defender.EquippedSlot(_content);
        if (forecast.Defender.Strikes && slot >= 0)
        {
            w.WriteString("counterWeapon", defender.Unit.Inventory.Items[slot].ItemId);
        }
        else
        {
            w.WriteNull("counterWeapon");
        }
    }

    /// <summary>The threat query: <see cref="Queries.Threats"/> on a player unit from its tile or <c>from</c>, each line with the enemy, its tile, the tile an announced event brings it on, its 0-based slot and weapon, the forecast, and what lands if every hit does, then the sleeping groups that could strike the tile if woken (issue 248).</summary>
    private string Threat(JsonElement request, BattleUnit unit)
    {
        if (unit.Side != Side.Player)
        {
            return Error(ProtocolJson.Name(RejectionReason.NotThisSide), $"{unit.Id} is an enemy; threat answers for a player unit");
        }

        var tile = ProtocolJson.OptionalCoord(request, "from") ?? unit.At;
        if (Queries.Threats(_state, _content, unit, tile) is not { } lines || Queries.SleepingThreats(_state, _content, unit, tile) is not { } asleep)
        {
            var owed = unit.Canto is not null && unit.Acted;
            return Error(
                ProtocolJson.Name(unit.Moved && !owed ? RejectionReason.AlreadyMoved : RejectionReason.OutOfReach),
                owed ? $"{unit.Id} cannot canto to {tile}" : unit.Moved ? $"{unit.Id} has already moved this phase; threat from {unit.At}" : $"{unit.Id} cannot move to {tile}");
        }

        lines = lines.Where(line => line.Arrives is not null || !Hidden(line.Enemy)).ToList();
        asleep = asleep.Select(g => g with { Members = g.Members.Where(m => !Hidden(m)).ToList() }).Where(g => g.Members.Count > 0).ToList();
        return Ok("threat", w =>
        {
            w.WriteString("unit", unit.Id);
            w.WriteStartObject("from");
            w.WriteNumber("x", tile.X);
            w.WriteNumber("y", tile.Y);
            w.WriteEndObject();
            w.WriteStartArray("threats");
            foreach (var line in lines)
            {
                w.WriteStartObject();
                w.WriteString("enemy", line.Enemy.Id);
                w.WriteStartObject("from");
                w.WriteNumber("x", line.From.X);
                w.WriteNumber("y", line.From.Y);
                w.WriteEndObject();
                if (line.Arrives is { } arrives)
                {
                    w.WriteStartObject("arrives");
                    w.WriteNumber("x", arrives.X);
                    w.WriteNumber("y", arrives.Y);
                    w.WriteEndObject();
                }

                w.WriteNumber("slot", line.Slot);
                w.WriteString("weapon", line.Weapon.Id);
                w.WriteNumber("ifAllLand", line.IfAllLand);
                w.WritePropertyName("forecast");
                ProtocolJson.WriteForecast(w, line.Forecast);
                WriteCounterWeapon(w, unit, line.Forecast);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteNumber("ifAllLand", Queries.IfAllLand(lines));
            w.WriteStartArray("asleep");
            foreach (var group in asleep)
            {
                w.WriteStartObject();
                w.WriteString("group", group.Group);
                w.WriteStartArray("members");
                foreach (var member in group.Members)
                {
                    w.WriteStringValue(member.Id);
                }

                w.WriteEndArray();
                w.WriteEndObject();
            }

            w.WriteEndArray();
            var unseeing = Queries.Unseeing(_state, _content, unit, tile)!;
            if (_state.Map.Dusk is not null)
            {
                WriteIds("cannotSee", unseeing.Where(e => !Hidden(e)));
            }

            w.WriteString("text", PlaySession.ThreatText(_state, _content, unit, tile, lines, asleep, unseeing));

            void WriteIds(string name, IEnumerable<BattleUnit> units)
            {
                w.WriteStartArray(name);
                foreach (var u in units)
                {
                    w.WriteStringValue(u.Id);
                }

                w.WriteEndArray();
            }
        });
    }

    /// <summary>Whether the player view hides <paramref name="unit"/>: an enemy no player unit sees at dusk.</summary>
    private bool Hidden(BattleUnit unit) => PlayerView && !Dusk.Seen(_state, unit);

    private static string Ok(string query, Action<Utf8JsonWriter> body) => ProtocolJson.Write(w =>
    {
        w.WriteStartObject();
        w.WriteBoolean("ok", true);
        w.WriteString("query", query);
        body(w);
        w.WriteEndObject();
    });

    private static string Error(string reason, string message) => ProtocolJson.Write(w =>
    {
        w.WriteStartObject();
        w.WriteBoolean("ok", false);
        w.WriteStartObject("error");
        w.WriteString("reason", reason);
        w.WriteString("message", message);
        w.WriteEndObject();
        w.WriteEndObject();
    });
}
