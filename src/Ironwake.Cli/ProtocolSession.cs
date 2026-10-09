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

            if (request.TryGetProperty("query", out _))
            {
                return Query(request);
            }

            var anyway = request.TryGetProperty("anyway", out var flag) && flag.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw new ProtocolException("field 'anyway' must be true or false"),
            };
            return Command(ProtocolJson.ReadCommand(request), anyway);
        }
        catch (ProtocolException e)
        {
            return Error("badRequest", e.Message);
        }
    }

    /// <summary>
    /// Whether an <c>end</c> is refused with <c>lethalUnconfirmed</c> while a unit is lethal if all
    /// land (issue 1120): the player's setting under the difficulty in play; <c>anyway</c> is
    /// accepted either way.
    /// </summary>
    public bool ConfirmLethal { get; init; } = true;

    private string Command(Command command, bool anyway = false)
    {
        var events = new List<GameEvent?>();
        var lethal = command is EndPhase ? Queries.Lethal(_state, _content, PlayerView) : null;
        if (lethal is { Count: > 0 } && ConfirmLethal && !anyway && _state.Phase == Side.Player && !_state.Outcome.IsOver)
        {
            return LethalEndRefusal(lethal);
        }

        var passed = command is EndPhase ? EscapeCount.PassedByEnding(_state, _content) : null;
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
                    ProtocolJson.WriteEvent(w, e, PlaySession.Describe(e, _content, UnitNames.Of(_state, _content)));
                }
            }

            w.WriteEndArray();
            if (lethal is not null)
            {
                WriteLethal(w, lethal);
            }

            if (passed is not null)
            {
                w.WriteStartArray("countPassed");
                foreach (var count in passed)
                {
                    w.WriteStringValue(count.Unit.Id);
                }

                w.WriteEndArray();
            }

            w.WritePropertyName("state");
            ProtocolJson.WriteState(w, _state, _content, full: false, PlayerView);
            w.WriteEndObject();
        });
    }

    /// <summary>
    /// The refusal of an <c>end</c> while a unit is lethal if all land (issue 1093): reason
    /// <c>lethalUnconfirmed</c>, the console's lines as the message, and the same <c>lethal</c> array an
    /// ended phase carries, so a renderer can show the warned death before it asks again with
    /// <c>"anyway": true</c>. Nothing is applied.
    /// </summary>
    private string LethalEndRefusal(IReadOnlyList<LethalThreat> lethal)
    {
        var names = UnitNames.Of(_state, _content);
        var message = string.Join("\n", lethal.Select(l => PlaySession.LethalLine(l, names)))
            + $"\nlethal if all land: {string.Join(", ", lethal.Select(l => names[l.Unit.Id]))}; send \"anyway\": true to end anyway";
        return ProtocolJson.Write(w =>
        {
            w.WriteStartObject();
            w.WriteBoolean("ok", false);
            w.WriteStartObject("error");
            w.WriteString("reason", "lethalUnconfirmed");
            w.WriteString("message", message);
            w.WriteEndObject();
            WriteLethal(w, lethal);
            w.WriteEndObject();
        });
    }

    private static void WriteLethal(Utf8JsonWriter w, IReadOnlyList<LethalThreat> lethal)
    {
        w.WriteStartArray("lethal");
        foreach (var threat in lethal)
        {
            w.WriteStartObject();
            w.WriteString("unit", threat.Unit.Id);
            w.WriteNumber("total", threat.Total);
            w.WriteNumber("hp", threat.Unit.Hp);
            w.WriteStartArray("strikers");
            foreach (var striker in threat.Strikers)
            {
                w.WriteStartObject();
                w.WriteString("enemy", striker.Enemy.Id);
                w.WriteNumber("damage", striker.Damage);
                if (striker.Line)
                {
                    w.WriteBoolean("line", true);
                }

                w.WriteEndObject();
            }

            w.WriteEndArray();
            if (threat.Freed.Count > 0)
            {
                w.WriteStartArray("freed");
                foreach (var freed in threat.Freed)
                {
                    w.WriteStartObject();
                    w.WriteString("enemy", freed.Follower.Id);
                    w.WriteNumber("damage", freed.Damage);
                    w.WriteString("tile", freed.Tile.ToString());
                    w.WriteString("ifCounterKills", freed.Freer.Id);
                    w.WriteNumber("counterHit", freed.Counter.Defender.DisplayedHit);
                    w.WriteBoolean("counterKillsOnHit", freed.Counter.Defender.Damage >= freed.Freer.Hp);
                    w.WriteEndObject();
                }

                w.WriteEndArray();
            }

            w.WriteEndObject();
        }

        w.WriteEndArray();
    }

    private static void WriteNullable(Utf8JsonWriter w, string name, int? value)
    {
        if (value is { } v)
        {
            w.WriteNumber(name, v);
        }
        else
        {
            w.WriteNull(name);
        }
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

        if (name == "terrain")
        {
            var named = ProtocolJson.RequiredString(request, "terrain");
            if (TerrainCard.Find(_content, named) is not { } terrain)
            {
                throw new ProtocolException($"no terrain '{named}'; name it by its glyph, id or name");
            }

            return Ok(name, w =>
            {
                w.WriteString("terrain", terrain.Id);
                w.WriteString("text", TerrainCard.Text(_state, _content, terrain.Id));
            });
        }

        if (name == "about")
        {
            var named = ProtocolJson.RequiredString(request, "item");
            if (ItemCard.Find(_content, named) is not { } itemId)
            {
                throw new ProtocolException($"no item '{named}'; name it by its id or name");
            }

            return Ok(name, w =>
            {
                w.WriteString("item", itemId);
                w.WriteString("text", ItemCard.Text(_content, itemId));
            });
        }

        if (name == "count")
        {
            return Ok(name, w =>
            {
                w.WriteNumber("turnLimit", _state.Map.TurnLimit);
                w.WriteStartArray("counts");
                foreach (var count in EscapeCount.Of(_state, _content))
                {
                    w.WriteStartObject();
                    w.WriteString("unit", count.Unit.Id);
                    WriteNullable(w, "phases", count.Phases);
                    WriteNullable(w, "lastStart", count.LastStart);
                    WriteNullable(w, "leavesOn", count.LeavesOn);
                    w.WriteBoolean("canLeave", count.CanLeave(_state.Map.TurnLimit));
                    w.WriteEndObject();
                }

                w.WriteEndArray();
                if (EscapeCount.Line(_state, _content, UnitNames.Of(_state, _content)) is { } line)
                {
                    w.WriteString("text", line);
                }
                else
                {
                    w.WriteNull("text");
                }
            });
        }

        if (name is not ("reachable" or "targets" or "forecast" or "threat"))
        {
            throw new ProtocolException($"query '{name}' is not one of: state, reachable, targets, forecast, threat, terrain, about, count");
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
    /// equipped one, and the form named by <c>form</c> when given (issue 68; <c>art</c> still read, issue 1461 a3). Refused
    /// with the console's reasons when there is no forecast.
    /// </summary>
    private string Forecast(JsonElement request, BattleUnit unit)
    {
        var targetId = ProtocolJson.RequiredString(request, "target");
        var slot = ProtocolJson.OptionalInt(request, "slot");
        var art = ProtocolJson.OptionalString(request, "form") ?? ProtocolJson.OptionalString(request, "art");
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
            var rejection = Queries.WeaponRefusal(_content, unit, slot, art, _state.Map.FormsEnabled);
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
            var lethal = PlaySession.LethalCounterLine(unit, target, forecast, PlaySession.RaisesWith(_state, _content, unit, slot)) is not null;
            w.WriteBoolean("counterLethal", lethal);
            if (lethal && forecast.FirstRoundMissChance(target.Hp) is { } miss)
            {
                w.WriteNumber("counterLethalIfMiss", miss);
            }
            else
            {
                w.WriteNull("counterLethalIfMiss");
            }

            WriteWakes(w, Queries.FightWakes(_state, _content, unit, target, tile));
            w.WriteString("text", PlaySession.ForecastText(_state, _content, unit, target, forecast, tile, from is not null, slot, art));
        });
    }

    /// <summary>
    /// <c>counterWeapon</c>: the item id the defender counters with, its equipped weapon, or null
    /// when it does not counter. Written on every forecast and threat line whatever the console's
    /// filter says (issue 313); a renderer may hide it.
    /// </summary>
    /// <summary>
    /// A <c>wakes</c> array: one object per sleeping group a fight's noise would wake, with <c>group</c>,
    /// <c>cause</c> (<c>noise</c> or <c>call</c>), <c>by</c> for a call, and <c>heardFrom</c>, the fight's tiles
    /// that reach it. The forecast's (issue 1106) and each <c>threat</c> line's (issue 1290).
    /// </summary>
    private static void WriteWakes(Utf8JsonWriter w, IEnumerable<FightWake> wakes)
    {
        w.WriteStartArray("wakes");
        foreach (var woke in wakes)
        {
            w.WriteStartObject();
            w.WriteString("group", woke.Group);
            w.WriteString("cause", woke.CalledBy is null ? "noise" : "call");
            if (woke.CalledBy is { } by)
            {
                w.WriteString("by", by);
            }

            w.WriteStartArray("heardFrom");
            foreach (var heard in woke.HeardFrom)
            {
                w.WriteStartObject();
                w.WriteNumber("x", heard.X);
                w.WriteNumber("y", heard.Y);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        w.WriteEndArray();
    }

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

    /// <summary>The threat query: <see cref="Queries.Threats"/> on a player unit from its tile or <c>from</c>, each line with the enemy, its tile, the tile an announced event brings it on, its 0-based slot and weapon, the forecast, and what lands if every hit does, then the sleeping groups that could strike the tile if woken (issue 248), on a <c>pincer: on</c> map the planner's anvil plans against the unit, unpriced (<see cref="Queries.Anvils"/>, issue 457), the sleeping groups the stop would wake (<see cref="Queries.StopWakes"/>, issue 458), the vetoed bosses that refuse every tile they would strike it from (<see cref="Queries.Refusals"/>, issue 565), and whether the move itself wins the map (issue 356).</summary>
    private string Threat(JsonElement request, BattleUnit unit)
    {
        if (unit.Side != Side.Player)
        {
            return Error(ProtocolJson.Name(RejectionReason.NotThisSide), $"{unit.Id} is an enemy; threat answers for a player unit");
        }

        var tile = ProtocolJson.OptionalCoord(request, "from") ?? unit.At;
        if (Queries.Threats(_state, _content, unit, tile) is not { } lines || Queries.SleepingThreats(_state, _content, unit, tile) is not { } asleep)
        {
            var owed = unit.MoveAgain is not null && unit.Acted;
            return Error(
                ProtocolJson.Name(unit.Moved && !owed ? RejectionReason.AlreadyMoved : RejectionReason.OutOfReach),
                owed ? $"{unit.Id} cannot move again to {tile}" : unit.Moved ? $"{unit.Id} has already moved this phase; threat from {unit.At}" : $"{unit.Id} cannot move to {tile}");
        }

        lines = lines.Where(line => line.Arrives is not null || !Hidden(line.Enemy)).ToList();
        asleep = asleep.Select(g => g with { Members = ValueList<BattleUnit>.From(g.Members.Where(m => !Hidden(m))) }).Where(g => g.Members.Count > 0).ToList();
        return Ok("threat", w =>
        {
            w.WriteString("unit", unit.Id);
            w.WriteStartObject("from");
            w.WriteNumber("x", tile.X);
            w.WriteNumber("y", tile.Y);
            w.WriteEndObject();
            w.WriteStartArray("threats");
            var counted = Queries.CountedFrom(lines);
            foreach (var (line, index) in lines.Select((l, i) => (l, i)))
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
                if (line.Form is { } form)
                {
                    w.WriteString("form", form);
                }

                w.WriteNumber("ifAllLand", line.IfAllLand);
                if (counted[index] is { } seat)
                {
                    w.WriteStartObject("countedFrom");
                    w.WriteNumber("x", seat.X);
                    w.WriteNumber("y", seat.Y);
                    w.WriteEndObject();
                }
                else
                {
                    w.WriteNull("countedFrom");
                }

                if (line.Raises)
                {
                    w.WriteBoolean("raises", true);
                }

                if (line.FreedBy is { } stepper)
                {
                    w.WriteString("freedBy", stepper.Id);
                }

                if (line.HeldBy is { } holder)
                {
                    w.WriteString("heldBy", holder.Id);
                }

                if (line.Casts is { } cast)
                {
                    w.WriteString("casts", cast == CastKind.Raise ? "raise" : "rampart");
                }

                if (line.LitBy is { } lighter)
                {
                    if (Hidden(_state.Find(lighter.Id) ?? lighter))
                    {
                        w.WriteNull("litBy");
                    }
                    else
                    {
                        w.WriteString("litBy", lighter.Id);
                    }
                }

                w.WritePropertyName("forecast");
                ProtocolJson.WriteForecast(w, line.Forecast);
                WriteCounterWeapon(w, unit, line.Forecast);
                WriteWakes(w, line.Wakes.Where(woke => _state.Units.Any(u => u.Group == woke.Group && !Hidden(u))));
                w.WriteEndObject();
            }

            w.WriteEndArray();
            var blow = Queries.RaisedBlowOn(_state, _content, unit, tile);
            if (blow is not null)
            {
                w.WriteStartObject("blow");
                w.WriteString("wielder", blow.Wielder.Id);
                w.WriteNumber("damage", blow.Damage);
                w.WriteEndObject();
            }

            w.WriteNumber("ifAllLand", Queries.IfAllLand(lines, blow));
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

            var anvils = Queries.Anvils(_state, _content, unit, tile)!.Where(a => !Hidden(a.Anvil) && !Hidden(a.Follower)).ToList();
            if (_state.Map.PincerEnabled)
            {
                w.WriteStartArray("anvils");
                foreach (var anvil in anvils)
                {
                    w.WriteStartObject();
                    w.WriteString("anvil", anvil.Anvil.Id);
                    w.WriteStartObject("tile");
                    w.WriteNumber("x", anvil.Tile.X);
                    w.WriteNumber("y", anvil.Tile.Y);
                    w.WriteEndObject();
                    w.WriteString("follower", anvil.Follower.Id);
                    w.WriteStartObject("from");
                    w.WriteNumber("x", anvil.From.X);
                    w.WriteNumber("y", anvil.From.Y);
                    w.WriteEndObject();
                    w.WriteEndObject();
                }

                w.WriteEndArray();
            }

            var refusals = Queries.Refusals(_state, _content, unit, tile)!.Where(r => !Hidden(r.Boss)).ToList();
            w.WriteStartArray("refusals");
            foreach (var refusal in refusals)
            {
                w.WriteStartObject();
                w.WriteString("boss", refusal.Boss.Id);
                w.WriteStartObject("tile");
                w.WriteNumber("x", refusal.Refused.X);
                w.WriteNumber("y", refusal.Refused.Y);
                w.WriteEndObject();
                w.WriteStartObject("ends");
                w.WriteNumber("x", refusal.Ends.X);
                w.WriteNumber("y", refusal.Ends.Y);
                w.WriteEndObject();
                w.WriteEndObject();
            }

            w.WriteEndArray();
            var wakes = Queries.StopWakes(_state, _content, unit, tile)!;
            w.WriteStartArray("wakes");
            foreach (var woke in wakes)
            {
                w.WriteStartObject();
                w.WriteString("group", woke.Group);
                w.WriteString("cause", woke.Cause.ToString().ToLowerInvariant());
                if (woke.CalledBy is { } by)
                {
                    w.WriteString("by", by);
                }

                w.WriteEndObject();
            }

            w.WriteEndArray();
            var wins = Queries.MoveWins(_state, _content, unit, tile);
            w.WriteBoolean("wins", wins);
            w.WriteString("text", PlaySession.ThreatText(_state, _content, unit, tile, lines, asleep, unseeing, wins, anvils, wakes, refusals));

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

    /// <summary>A refusal: its reason code, and its message as a reader sees it (issue 615), unit ids as names in sentence case.</summary>
    private string Error(string reason, string message) => ProtocolJson.Write(w =>
    {
        w.WriteStartObject();
        w.WriteBoolean("ok", false);
        w.WriteStartObject("error");
        w.WriteString("reason", reason);
        w.WriteString("message", UnitNames.Of(_state, _content).Message(message));
        w.WriteEndObject();
        w.WriteEndObject();
    });
}
