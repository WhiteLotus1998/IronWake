using System.Text;
using System.Text.Json;
using Ironwake.Core;

namespace Ironwake.Content.Protocol;

/// <summary>
/// The presentation protocol's JSON (issue 25; the shapes are documented field by field in
/// docs/PROTOCOL.md): every <see cref="GameEvent"/>, every <see cref="Command"/>, a forecast,
/// a reach, and a <see cref="BattleState"/>. Field names are written by hand and never
/// reflected from the records, so renaming a record parameter cannot change the protocol;
/// a golden test per event type holds each shape. Output is compact and canonical: every
/// field is written, in a fixed order, with null where a value is absent. Enum values are
/// the enum's name in camel case (<c>player</c>, <c>twoRollAverage</c>). Consumers ignore
/// fields they do not know (<see cref="ProtocolVersion"/>), and so do the readers here.
/// </summary>
public static class ProtocolJson
{
    /// <summary>One event as a JSON object: a <c>type</c> field naming the record in camel case, its fields, then <paramref name="text"/> when given (the console's own line for the event).</summary>
    public static string Event(GameEvent e, string? text = null) => Write(w => WriteEvent(w, e, text));

    /// <summary>One command as a JSON object, the shape <see cref="ReadCommand(string)"/> reads back.</summary>
    public static string Command(Command command) => Write(w => WriteCommand(w, command));

    /// <summary>A forecast as a JSON object, the shape <see cref="ReadForecast(string)"/> reads back.</summary>
    public static string Forecast(CombatForecast forecast) => Write(w => WriteForecast(w, forecast));

    /// <summary>
    /// The whole state: the map as its canonical map text, every unit in full, the history
    /// with each prior state in the same shape (their own history empty), and the outcome.
    /// <see cref="ReadState(string, GameContent)"/> reads it back to an equal state.
    /// </summary>
    public static string State(BattleState state, GameContent content) => Write(w => WriteState(w, state, content, full: true));

    /// <summary>
    /// The state a command's answer carries: the full shape without <c>map</c> and
    /// <c>history</c>, which would make every answer grow with the battle. <c>historyCount</c>
    /// and <c>mapName</c> are written either way; a renderer asks for the full state once and
    /// follows the map's changes through <see cref="TerrainChanged"/> events.
    /// </summary>
    public static string BoardState(BattleState state, GameContent content) => Write(w => WriteState(w, state, content, full: false));

    public static void WriteEvent(Utf8JsonWriter w, GameEvent e, string? text = null)
    {
        w.WriteStartObject();
        w.WriteString("type", CamelCase(e.GetType().Name));
        switch (e)
        {
            case MoveUndone u:
                w.WriteString("unit", u.UnitId);
                WriteCoord(w, "from", u.From);
                WriteCoord(w, "to", u.To);
                break;
            case UnitMoved m:
                w.WriteString("unit", m.UnitId);
                WriteCoord(w, "from", m.From);
                WriteCoord(w, "to", m.To);
                WriteCoords(w, "path", m.Path);
                break;
            case CombatFought f:
                w.WriteString("attacker", f.AttackerId);
                w.WriteString("target", f.TargetId);
                w.WriteNumber("turn", f.Turn);
                w.WriteString("phase", Name(f.Phase));
                w.WriteStartArray("strikes");
                foreach (var s in f.Strikes)
                {
                    w.WriteStartObject();
                    w.WriteNumber("index", s.Index);
                    w.WriteString("attacker", s.AttackerId);
                    w.WriteString("target", s.TargetId);
                    w.WriteBoolean("hit", s.Hit);
                    w.WriteBoolean("crit", s.Crit);
                    w.WriteNumber("damage", s.Damage);
                    w.WriteNumber("targetHpAfter", s.TargetHpAfter);
                    w.WriteEndObject();
                }

                w.WriteEndArray();
                if (f.Bite is { } bite)
                {
                    w.WriteStartObject("bite");
                    w.WriteString("rider", bite.RiderId);
                    w.WriteString("target", bite.TargetId);
                    w.WriteNumber("damage", bite.Damage);
                    w.WriteNumber("targetHpAfter", bite.TargetHpAfter);
                    w.WriteEndObject();
                }

                w.WriteNumber("attackerHpAfter", f.AttackerHpAfter);
                w.WriteNumber("targetHpAfter", f.TargetHpAfter);
                break;
            case UnitDied d:
                w.WriteString("unit", d.UnitId);
                w.WriteString("side", Name(d.Side));
                WriteCoord(w, "at", d.At);
                break;
            case ExpGained x:
                w.WriteString("unit", x.UnitId);
                w.WriteNumber("amount", x.Amount);
                w.WriteNumber("expAfter", x.ExpAfter);
                break;
            case LeveledUp l:
                w.WriteString("unit", l.UnitId);
                w.WriteNumber("newLevel", l.NewLevel);
                WriteStats(w, "gains", l.Gains);
                break;
            case RankRaised k:
                w.WriteString("unit", k.UnitId);
                w.WriteString("weaponType", Name(k.Type));
                w.WriteString("rank", Name(k.Rank));
                break;
            case MasteryEarned m:
                w.WriteString("unit", m.UnitId);
                w.WriteString("class", m.ClassId);
                w.WriteString("ability", m.AbilityId);
                break;
            case UnitWaited u:
                w.WriteString("unit", u.UnitId);
                if (u.Braced)
                {
                    w.WriteBoolean("braced", true);
                }

                break;
            case UnitExited x:
                w.WriteString("unit", x.UnitId);
                WriteCoord(w, "at", x.At);
                break;
            case UnitLeftBehind b:
                w.WriteString("unit", b.UnitId);
                WriteCoord(w, "at", b.At);
                break;
            case KeepsakeLeft k:
                w.WriteString("fallen", k.FallenId);
                w.WriteString("item", k.ItemId);
                WriteCoord(w, "at", k.At);
                break;
            case TomeDropped t:
                w.WriteString("unit", t.UnitId);
                WriteStrings(w, "items", t.ItemIds);
                break;
            case KeepsakeRecovered k:
                w.WriteString("unit", k.UnitId);
                w.WriteString("fallen", k.FallenId);
                w.WriteString("item", k.ItemId);
                break;
            case ChestOpened c:
                w.WriteString("unit", c.UnitId);
                WriteCoord(w, "at", c.At);
                WriteStrings(w, "items", c.ItemIds);
                if (c.Wagon.Count > 0)
                {
                    WriteStrings(w, "wagon", c.Wagon);
                }

                break;
            case KeepsakeTaken k:
                w.WriteString("unit", k.UnitId);
                w.WriteString("fallen", k.FallenId);
                w.WriteString("item", k.ItemId);
                break;
            case KeepsakeLost k:
                w.WriteString("fallen", k.FallenId);
                w.WriteString("item", k.ItemId);
                WriteCoord(w, "at", k.At);
                if (k.CarrierId is { } carrier)
                {
                    w.WriteString("carrier", carrier);
                }

                break;
            case Cantoed c:
                w.WriteString("unit", c.UnitId);
                WriteCoord(w, "from", c.From);
                WriteCoord(w, "to", c.To);
                WriteCoords(w, "path", c.Path);
                break;
            case OrderCalled o:
                w.WriteString("unit", o.CaptainId);
                w.WriteString("kind", OrderName(o.Kind));
                w.WriteNumber("radius", o.Radius);
                WriteStrings(w, "reached", o.Reached);
                w.WriteNumber("inRadius", o.InRadius);
                w.WriteNumber("alive", o.Alive);
                w.WriteNumber("exposure", o.Exposure);
                break;
            case FellBack f:
                w.WriteString("unit", f.UnitId);
                WriteCoord(w, "from", f.From);
                WriteCoord(w, "to", f.To);
                WriteCoords(w, "path", f.Path);
                break;
            case UnitWinded wd:
                w.WriteString("unit", wd.UnitId);
                break;
            case Shoved s:
                w.WriteString("unit", s.UnitId);
                w.WriteString("target", s.TargetId);
                WriteCoord(w, "from", s.From);
                WriteCoord(w, "to", s.To);
                break;
            case Breathed b:
                w.WriteString("unit", b.UnitId);
                WriteCoord(w, "from", b.From);
                WriteCoords(w, "line", b.Line);
                WriteStrings(w, "chilled", b.Chilled);
                WriteCoords(w, "frozen", b.Frozen);
                break;
            case Carried c:
                w.WriteString("unit", c.UnitId);
                w.WriteString("ally", c.AllyId);
                WriteCoord(w, "from", c.From);
                WriteCoord(w, "to", c.To);
                WriteCoord(w, "allyFrom", c.AllyFrom);
                WriteCoord(w, "setDown", c.SetDown);
                break;
            case UnitRetreated r:
                w.WriteString("unit", r.UnitId);
                WriteCoord(w, "from", r.From);
                WriteCoord(w, "to", r.To);
                break;
            case UnitBroke b:
                w.WriteString("unit", b.UnitId);
                WriteCoord(w, "at", b.At);
                w.WriteNumber("hp", b.Hp);
                break;
            case UnitFreed f:
                w.WriteString("unit", f.UnitId);
                WriteCoord(w, "at", f.At);
                w.WriteNumber("hp", f.Hp);
                break;
            case RouteDrifted d:
                w.WriteString("group", d.Group);
                WriteCoord(w, "to", d.To);
                WriteStrings(w, "units", d.Units);
                break;
            case UnitTalked t:
                w.WriteString("unit", t.UnitId);
                w.WriteString("target", t.TargetId);
                WriteCoord(w, "at", t.At);
                w.WriteNumber("hp", t.Hp);
                w.WriteString("fate", t.Fate == ReturnFate.Turned ? "turned" : "spared");
                break;
            case MessengerEscaped m:
                w.WriteString("unit", m.UnitId);
                WriteCoord(w, "at", m.At);
                break;
            case FrontFell f:
                w.WriteString("front", f.Front);
                break;
            case GrudgeSworn g:
                w.WriteString("unit", g.UnitId);
                w.WriteString("against", g.AgainstId);
                break;
            case RapportGained g:
                w.WriteString("a", g.A);
                w.WriteString("b", g.B);
                w.WriteNumber("amount", g.Amount);
                w.WriteNumber("total", g.Total);
                WriteNullableNumber(w, "outOf", g.OutOf);
                break;
            case RivalryEnded r:
                w.WriteString("a", r.A);
                w.WriteString("b", r.B);
                break;
            case SupportReached s:
                w.WriteString("a", s.A);
                w.WriteString("b", s.B);
                w.WriteString("tier", s.Tier);
                break;
            case PhaseEnded p:
                w.WriteString("side", Name(p.Side));
                w.WriteNumber("turn", p.Turn);
                break;
            case PhaseBegan p:
                w.WriteString("side", Name(p.Side));
                w.WriteNumber("turn", p.Turn);
                break;
            case UnitHealed h:
                w.WriteString("unit", h.UnitId);
                w.WriteNumber("amount", h.Amount);
                w.WriteNumber("hpAfter", h.HpAfter);
                break;
            case UnitBurned b:
                w.WriteString("unit", b.UnitId);
                w.WriteNumber("amount", b.Amount);
                w.WriteNumber("hpAfter", b.HpAfter);
                break;
            case RockfallStruck r:
                w.WriteString("unit", r.UnitId);
                WriteCoord(w, "at", r.At);
                w.WriteNumber("amount", r.Amount);
                w.WriteNumber("hpAfter", r.HpAfter);
                break;
            case UnitRested r:
                w.WriteString("unit", r.UnitId);
                break;
            case HungerDrained h:
                w.WriteString("unit", h.UnitId);
                w.WriteString("item", h.ItemId);
                w.WriteNumber("amount", h.Amount);
                w.WriteNumber("hpAfter", h.HpAfter);
                w.WriteBoolean("starved", h.Starved);
                break;
            case HungerFed h:
                w.WriteString("unit", h.UnitId);
                w.WriteString("item", h.ItemId);
                w.WriteNumber("fed", h.Fed);
                w.WriteNumber("healed", h.Healed);
                w.WriteNumber("hpAfter", h.HpAfter);
                w.WriteNumber("mtBonus", h.MtBonus);
                w.WriteBoolean("woke", h.Woke);
                break;
            case HuntRanOn h:
                w.WriteString("unit", h.UnitId);
                w.WriteNumber("mov", h.Mov);
                break;
            case KinsbaneSpoke k:
                w.WriteString("unit", k.UnitId);
                w.WriteString("item", k.ItemId);
                w.WriteString("line", k.LineId);
                w.WriteString("text", k.Text);
                break;
            case HungerEased h:
                w.WriteString("unit", h.UnitId);
                w.WriteString("item", h.ItemId);
                w.WriteNumber("healed", h.Healed);
                w.WriteNumber("hpAfter", h.HpAfter);
                break;
            case UnitOpened o:
                w.WriteString("unit", o.UnitId);
                w.WriteString("by", o.ByUnitId);
                w.WriteNumber("def", o.Def);
                w.WriteNumber("res", o.Res);
                break;
            case UnitIgnited i:
                w.WriteString("unit", i.UnitId);
                w.WriteString("by", i.ByUnitId);
                w.WriteNumber("amount", i.Amount);
                w.WriteNumber("phases", i.Phases);
                w.WriteNumber("stacks", i.Stacks);
                break;
            case BurnCashed c:
                w.WriteString("unit", c.UnitId);
                w.WriteString("by", c.ByUnitId);
                w.WriteNumber("amount", c.Amount);
                w.WriteNumber("hpAfter", c.HpAfter);
                break;
            case GroundRaised g:
                w.WriteString("unit", g.UnitId);
                w.WriteString("target", g.TargetId);
                WriteCoord(w, "at", g.At);
                w.WriteString("terrain", g.TerrainId);
                break;
            case UnitStunned st:
                w.WriteString("unit", st.UnitId);
                w.WriteString("by", st.ByUnitId);
                w.WriteString("side", Name(st.Side));
                if (st.Next)
                {
                    w.WriteBoolean("next", true);
                }

                break;
            case StunSkipped sk:
                w.WriteString("unit", sk.UnitId);
                break;
            case UnitChilled c:
                w.WriteString("unit", c.UnitId);
                w.WriteString("by", c.ByUnitId);
                w.WriteString("side", Name(c.Side));
                if (c.Next)
                {
                    w.WriteBoolean("next", true);
                }

                break;
            case UnitFrosted f:
                w.WriteString("unit", f.UnitId);
                w.WriteString("by", f.ByUnitId);
                w.WriteNumber("damage", f.Damage);
                w.WriteNumber("hpAfter", f.HpAfter);
                w.WriteBoolean("held", f.Held);
                if (f.Boss)
                {
                    w.WriteBoolean("boss", true);
                }

                w.WriteString("side", Name(f.Side));
                if (f.Next)
                {
                    w.WriteBoolean("next", true);
                }

                break;
            case UnitLocked l:
                w.WriteString("unit", l.UnitId);
                w.WriteString("by", l.ByUnitId);
                w.WriteString("side", Name(l.Side));
                if (l.Next)
                {
                    w.WriteBoolean("next", true);
                }

                break;
            case LockDropped d:
                w.WriteString("unit", d.UnitId);
                w.WriteString("by", d.ByUnitId);
                break;
            case UnitGrounded g:
                w.WriteString("unit", g.UnitId);
                w.WriteString("by", g.ByUnitId);
                w.WriteString("side", Name(g.Side));
                if (g.Next)
                {
                    w.WriteBoolean("next", true);
                }

                break;
            case HeirloomTurned t:
                w.WriteString("unit", t.UnitId);
                w.WriteString("item", t.ItemId);
                w.WriteNumber("stage", t.Stage);
                w.WriteString("stageId", t.StageId);
                break;
            case WatchTaken t:
                w.WriteString("unit", t.UnitId);
                WriteCoord(w, "at", t.At);
                if (t.PassedUpTargetId is { } passed)
                {
                    w.WriteString("passesUp", passed);
                    w.WriteNumber("passesUpHit", t.PassedUpHit!.Value);
                }

                if (t.Holds)
                {
                    if (t.HoldsInsteadOf is { } instead)
                    {
                        WriteCoord(w, "holdsInsteadOf", instead);
                    }
                    else
                    {
                        w.WriteBoolean("noMoveCloser", true);
                    }
                }

                break;
            case WatchFired f:
                w.WriteString("unit", f.UnitId);
                w.WriteString("target", f.TargetId);
                WriteCoord(w, "at", f.At);
                w.WriteBoolean("hit", f.Strike.Hit);
                w.WriteBoolean("crit", f.Strike.Crit);
                w.WriteNumber("damage", f.Strike.Damage);
                w.WriteNumber("targetHpAfter", f.Strike.TargetHpAfter);
                break;
            case WatchHeld h:
                w.WriteString("unit", h.UnitId);
                w.WriteString("target", h.TargetId);
                WriteCoord(w, "at", h.At);
                w.WriteNumber("hit", h.Hit);
                break;
            case WatchEnded x:
                w.WriteString("unit", x.UnitId);
                break;
            case CoverTaken c:
                w.WriteString("unit", c.UnitId);
                w.WriteString("ally", c.AllyId);
                WriteCoord(w, "allyLandsOn", c.AllyLandsOn);
                if (c.PassedUpTargetId is { } coverPassed)
                {
                    w.WriteString("passesUp", coverPassed);
                    w.WriteNumber("passesUpHit", c.PassedUpHit!.Value);
                }

                break;
            case CoverFired c:
                w.WriteString("unit", c.UnitId);
                w.WriteString("ally", c.AllyId);
                w.WriteString("attacker", c.AttackerId);
                WriteCoord(w, "at", c.At);
                WriteCoord(w, "allyTo", c.AllyTo);
                w.WriteBoolean("wouldHaveKilled", c.WouldHaveKilled);
                w.WriteBoolean("counters", c.Counters);
                break;
            case BlowRaised b:
                w.WriteString("unit", b.UnitId);
                w.WriteString("target", b.TargetId);
                WriteCoord(w, "at", b.At);
                break;
            case BlowLanded b:
                w.WriteString("unit", b.UnitId);
                w.WriteString("target", b.TargetId);
                WriteCoord(w, "at", b.At);
                w.WriteNumber("damage", b.Damage);
                w.WriteNumber("targetHpAfter", b.TargetHpAfter);
                break;
            case BlowFell b:
                w.WriteString("unit", b.UnitId);
                WriteCoord(w, "at", b.At);
                break;
            case BlowBroken b:
                w.WriteString("unit", b.UnitId);
                WriteCoord(w, "at", b.At);
                break;
            case Recalled r:
                w.WriteNumber("toIndex", r.ToIndex);
                w.WriteNumber("chargesLeft", r.ChargesLeft);
                break;
            case ItemUsed i:
                w.WriteString("unit", i.UnitId);
                w.WriteString("item", i.ItemId);
                w.WriteString("target", i.TargetId);
                w.WriteNumber("usesLeft", i.UsesLeft);
                break;
            case WeaponEquipped q:
                w.WriteString("unit", q.UnitId);
                w.WriteString("item", q.ItemId);
                break;
            case ArtDeclared a:
                w.WriteString("unit", a.UnitId);
                w.WriteString("art", a.ArtId);
                w.WriteString("item", a.ItemId);
                w.WriteNumber("cost", a.Cost);
                break;
            case WeaponBroke b:
                w.WriteString("unit", b.UnitId);
                w.WriteString("item", b.ItemId);
                break;
            case SpellSpent s:
                w.WriteString("unit", s.UnitId);
                w.WriteString("item", s.ItemId);
                break;
            case GroupWoke g:
                w.WriteString("group", g.Group);
                w.WriteString("cause", Name(g.Cause));
                if (g.CalledBy is { } by)
                {
                    w.WriteString("by", by);
                }

                if (g.Lamps.Count > 0)
                {
                    w.WriteStartArray("lamps");
                    foreach (var lamp in g.Lamps)
                    {
                        w.WriteStartObject();
                        w.WriteString("unit", lamp.UnitId);
                        WriteCoord(w, "at", lamp.At);
                        w.WriteEndObject();
                    }

                    w.WriteEndArray();
                }

                break;
            case MapEventFired m:
                w.WriteString("name", m.Name);
                w.WriteBoolean("blocked", m.Blocked);
                if (m.Terrain is { } barring)
                {
                    w.WriteString("terrain", barring);
                }

                break;
            case TerrainChanged t:
                WriteCoord(w, "at", t.At);
                w.WriteString("terrain", t.TerrainId);
                break;
            case UnitSpawned u:
                w.WriteString("unit", u.UnitId);
                WriteCoord(w, "at", u.At);
                w.WriteString("group", u.Group);
                w.WriteString("behavior", Name(u.Behavior));
                break;
            case FlagSet f:
                w.WriteString("flag", f.Flag);
                break;
            case BarReleased b:
                w.WriteString("event", b.Event);
                WriteCoord(w, "holder", b.Holder);
                WriteCoord(w, "at", b.At);
                w.WriteString("terrain", b.TerrainId);
                break;
            case ArrivalWaits a:
                w.WriteString("event", a.Event);
                w.WriteString("template", a.Template);
                WriteCoord(w, "at", a.At);
                if (a.Terrain is { } walled)
                {
                    w.WriteString("terrain", walled);
                }

                break;
            default:
                throw new ArgumentException($"the protocol has no shape for event {e.GetType().Name}; add one to ProtocolJson and docs/PROTOCOL.md", nameof(e));
        }

        if (text is not null)
        {
            w.WriteString("text", text);
        }

        w.WriteEndObject();
    }

    public static void WriteCommand(Utf8JsonWriter w, Command command)
    {
        w.WriteStartObject();
        switch (command)
        {
            case Move m:
                w.WriteString("type", "move");
                w.WriteString("unit", m.UnitId);
                WriteCoord(w, "to", m.To);
                if (m.Via is { } via)
                {
                    WriteCoord(w, "via", via);
                }

                break;
            case Attack a:
                w.WriteString("type", "attack");
                w.WriteString("unit", a.UnitId);
                w.WriteString("target", a.TargetId);
                WriteNullableNumber(w, "slot", a.Slot);
                if (a.Art is not null)
                {
                    w.WriteString("art", a.Art);
                }

                break;
            case UseItem i:
                w.WriteString("type", "item");
                w.WriteString("unit", i.UnitId);
                w.WriteNumber("slot", i.Slot);
                w.WriteString("target", i.TargetId);
                if (i.Art is not null)
                {
                    w.WriteString("art", i.Art);
                }

                break;
            case Retreat r:
                w.WriteString("type", "retreat");
                w.WriteString("unit", r.UnitId);
                WriteCoord(w, "to", r.To);
                break;
            case Wait wait:
                w.WriteString("type", "wait");
                w.WriteString("unit", wait.UnitId);
                break;
            case Watch watch:
                w.WriteString("type", "watch");
                w.WriteString("unit", watch.UnitId);
                break;
            case Cover cover:
                w.WriteString("type", "cover");
                w.WriteString("unit", cover.UnitId);
                w.WriteString("ally", cover.AllyId);
                break;
            case Exit exit:
                w.WriteString("type", "exit");
                w.WriteString("unit", exit.UnitId);
                break;
            case Recover recover:
                w.WriteString("type", "recover");
                w.WriteString("unit", recover.UnitId);
                break;
            case Open open:
                w.WriteString("type", "open");
                w.WriteString("unit", open.UnitId);
                WriteCoord(w, "at", open.At);
                break;
            case Drop drop:
                w.WriteString("type", "drop");
                w.WriteString("unit", drop.UnitId);
                break;
            case Talk talk:
                w.WriteString("type", "talk");
                w.WriteString("unit", talk.UnitId);
                w.WriteString("target", talk.TargetId);
                break;
            case Shove shove:
                w.WriteString("type", "shove");
                w.WriteString("unit", shove.UnitId);
                w.WriteString("target", shove.TargetId);
                break;
            case Dash dash:
                w.WriteString("type", "dash");
                w.WriteString("unit", dash.UnitId);
                WriteCoord(w, "to", dash.To);
                break;
            case Breathe breathe:
                w.WriteString("type", "breathe");
                w.WriteString("unit", breathe.UnitId);
                WriteCoord(w, "toward", breathe.Toward);
                break;
            case Carry carry:
                w.WriteString("type", "carry");
                w.WriteString("unit", carry.UnitId);
                w.WriteString("ally", carry.AllyId);
                WriteCoord(w, "to", carry.To);
                WriteCoord(w, "setDown", carry.SetDown);
                break;
            case Canto canto:
                w.WriteString("type", "canto");
                w.WriteString("unit", canto.UnitId);
                WriteCoord(w, "to", canto.To);
                break;
            case Order order:
                w.WriteString("type", "order");
                w.WriteString("kind", OrderName(order.Kind));
                break;
            case FallBack fallBack:
                w.WriteString("type", "fallBack");
                w.WriteString("unit", fallBack.UnitId);
                WriteCoord(w, "to", fallBack.To);
                break;
            case EndPhase:
                w.WriteString("type", "end");
                break;
            case Recall r:
                w.WriteString("type", "recall");
                w.WriteNumber("toIndex", r.ToIndex);
                break;
            case Undo u:
                w.WriteString("type", "undo");
                w.WriteString("unit", u.UnitId);
                break;
            default:
                throw new ArgumentException($"the protocol has no shape for command {command.GetType().Name}", nameof(command));
        }

        w.WriteEndObject();
    }

    /// <summary>Reads a command object. Throws <see cref="ProtocolException"/> naming the field when the object is not one.</summary>
    public static Command ReadCommand(string json)
    {
        using var doc = Parse(json);
        return ReadCommand(doc.RootElement);
    }

    public static Command ReadCommand(JsonElement e)
    {
        var type = RequiredString(e, "type");
        return type switch
        {
            "move" => new Move(RequiredString(e, "unit"), ReadCoord(e, "to"), e.TryGetProperty("via", out _) ? ReadCoord(e, "via") : null),
            "attack" => new Attack(RequiredString(e, "unit"), RequiredString(e, "target"), OptionalInt(e, "slot"), OptionalString(e, "art")),
            "item" => new UseItem(RequiredString(e, "unit"), RequiredInt(e, "slot"), OptionalString(e, "target"), OptionalString(e, "art")),
            "retreat" => new Retreat(RequiredString(e, "unit"), ReadCoord(e, "to")),
            "wait" => new Wait(RequiredString(e, "unit")),
            "watch" => new Watch(RequiredString(e, "unit")),
            "cover" => new Cover(RequiredString(e, "unit"), RequiredString(e, "ally")),
            "canto" => new Canto(RequiredString(e, "unit"), ReadCoord(e, "to")),
            "exit" => new Exit(RequiredString(e, "unit")),
            "recover" => new Recover(RequiredString(e, "unit")),
            "open" => new Open(RequiredString(e, "unit"), ReadCoord(e, "at")),
            "drop" => new Drop(RequiredString(e, "unit")),
            "talk" => new Talk(RequiredString(e, "unit"), RequiredString(e, "target")),
            "shove" => new Shove(RequiredString(e, "unit"), RequiredString(e, "target")),
            "carry" => new Carry(RequiredString(e, "unit"), RequiredString(e, "ally"), ReadCoord(e, "to"), ReadCoord(e, "setDown")),
            "breathe" => new Breathe(RequiredString(e, "unit"), ReadCoord(e, "toward")),
            "dash" => new Dash(RequiredString(e, "unit"), ReadCoord(e, "to")),
            "order" => new Order(ReadOrderKind(RequiredString(e, "kind"))),
            "fallBack" => new FallBack(RequiredString(e, "unit"), ReadCoord(e, "to")),
            "end" => new EndPhase(),
            "recall" => new Recall(RequiredInt(e, "toIndex")),
            "undo" => new Undo(RequiredString(e, "unit")),
            _ => throw new ProtocolException($"type '{type}' is not a command; expected move, attack, item, retreat, wait, watch, cover, canto, exit, recover, open, drop, talk, shove, carry, breathe, dash, order, fallBack, end, recall, or undo"),
        };
    }

    /// <summary>An order's protocol name (issue 85): <c>press</c>, <c>rally</c> or <c>fallBack</c>.</summary>
    private static string OrderName(OrderKind kind) => kind switch
    {
        OrderKind.Press => "press",
        OrderKind.Rally => "rally",
        _ => "fallBack",
    };

    private static OrderKind ReadOrderKind(string name) => name switch
    {
        "press" => OrderKind.Press,
        "rally" => OrderKind.Rally,
        "fallBack" => OrderKind.FallBack,
        _ => throw new ProtocolException($"field 'kind' must be press, rally or fallBack, got '{name}'"),
    };

    public static void WriteForecast(Utf8JsonWriter w, CombatForecast forecast)
    {
        w.WriteStartObject();
        WriteSide(w, "attacker", forecast.Attacker);
        WriteSide(w, "defender", forecast.Defender);
        w.WriteString("scheme", Name(forecast.Scheme));
        if (forecast.ArtCost != 0)
        {
            w.WriteNumber("artCost", forecast.ArtCost);
        }

        if (forecast.CounterAnswered)
        {
            w.WriteBoolean("counterAnswered", true);
        }

        w.WriteEndObject();
    }

    public static CombatForecast ReadForecast(string json)
    {
        using var doc = Parse(json);
        var e = doc.RootElement;
        return new CombatForecast(ReadSide(Required(e, "attacker")), ReadSide(Required(e, "defender")), ParseEnum<RollScheme>(RequiredString(e, "scheme"), "scheme"), OptionalInt(e, "artCost") ?? 0)
        {
            CounterAnswered = e.TryGetProperty("counterAnswered", out _) && RequiredBool(e, "counterAnswered"),
        };
    }

    /// <summary>Every tile of a reach in the order the core settled them (DECISIONS/0012), each with its cost, its path from the origin, and whether the unit may end its move there.</summary>
    public static void WriteReach(Utf8JsonWriter w, Reach reach)
    {
        w.WriteStartObject();
        WriteCoord(w, "origin", reach.Origin);
        w.WriteString("movement", Name(reach.Movement));
        w.WriteNumber("mov", reach.Mov);
        w.WriteStartArray("tiles");
        foreach (var entry in reach.Entries)
        {
            w.WriteStartObject();
            w.WriteNumber("x", entry.At.X);
            w.WriteNumber("y", entry.At.Y);
            w.WriteNumber("cost", entry.Cost);
            w.WriteBoolean("canEnd", entry.CanEnd);
            WriteCoords(w, "path", entry.Path);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();
    }

    /// <summary>
    /// Writes a state in the shape above. On a dusk map (DESIGN.md 13.7, issue 302), what the
    /// player can see is a rule, so given <paramref name="playerView"/> the state is the
    /// player's view: <c>view</c> is <c>player</c>, <c>units</c> leaves out every enemy no
    /// player unit sees, and <c>unseen</c> lists their tiles in row-major order with no ids,
    /// names or numbers, the history written the same way. Such a state is a view and not a
    /// save, so <see cref="ReadState(JsonElement, GameContent)"/> refuses it. Without
    /// <paramref name="playerView"/> a dusk map's state carries <c>view</c> <c>omniscient</c>;
    /// a daylight state carries neither field.
    /// </summary>
    public static void WriteState(Utf8JsonWriter w, BattleState state, GameContent content, bool full, bool playerView = false)
    {
        var dark = state.Map.Dusk is not null;
        var hidden = dark && playerView
            ? state.Units.Where(u => !Dusk.Seen(state, u)).ToList()
            : new List<BattleUnit>();
        w.WriteStartObject();
        w.WriteNumber("protocolVersion", ProtocolVersion.Current);
        w.WriteNumber("rulesVersion", RulesVersion.Current);
        if (dark)
        {
            w.WriteString("view", playerView ? "player" : "omniscient");
        }

        w.WriteString("mapName", state.Map.Name);
        if (full)
        {
            w.WriteString("map", MapFormat.Write(state.Map, content));
        }

        if (state.Map.Win == WinCondition.Seize)
        {
            // Derived (issues 569, 1208): the seize tile by the name the objective prints, never read back.
            w.WriteString("seizeName", Objective.SeizeName(state.Map, content));
        }

        w.WriteNumber("turn", state.Turn);
        w.WriteString("phase", Name(state.Phase));
        w.WriteString("seed", state.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        w.WriteString("scheme", Name(state.Scheme));
        w.WriteNumber("recallCharges", state.RecallCharges);
        if (state.CampaignMap is { } campaignMap)
        {
            w.WriteNumber("campaignMap", campaignMap);
        }

        if (state.SideMap)
        {
            w.WriteBoolean("sideMap", true);
        }

        if (state.OrderCalled is { } order)
        {
            w.WriteString("order", OrderName(order));
        }

        w.WriteStartArray("units");
        foreach (var unit in state.Units.Where(u => !hidden.Contains(u)))
        {
            WriteUnit(w, unit, content, state.Map.TerrainIdAt(unit.At) == Wildfire.FireTerrainId, Signatures.Of(state, content, unit));
        }

        w.WriteEndArray();
        if (state.Map.WildfireEnabled)
        {
            WriteCoords(w, "nextFront", Wildfire.NextFront(state.Map));
        }

        if (dark && playerView)
        {
            WriteCoords(w, "unseen", hidden.Select(u => u.At).OrderBy(c => c.Y).ThenBy(c => c.X));
        }

        w.WriteStartArray("escaped");
        foreach (var unit in state.Escaped)
        {
            WriteUnit(w, unit, content);
        }

        w.WriteEndArray();
        if (state.Map.Chests.Count > 0)
        {
            w.WriteStartArray("chests");
            foreach (var chest in state.Map.Chests)
            {
                w.WriteStartObject();
                WriteCoord(w, "at", chest.At);
                WriteStrings(w, "items", chest.Items);
                w.WriteBoolean("open", state.Opened.Contains(chest.At));
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        if (state.Wagon.Count > 0)
        {
            WriteStrings(w, "wagon", state.Wagon);
        }

        if (state.Rime.Count > 0)
        {
            w.WriteStartArray("rime");
            foreach (var tile in state.Rime)
            {
                w.WriteStartObject();
                WriteCoord(w, "at", tile.At);
                w.WriteString("side", Name(tile.Side));
                w.WriteNumber("clock", tile.Clock);
                if (tile.Extra > 0)
                {
                    w.WriteNumber("extra", tile.Extra);
                }
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        if (state.Overlays.Count > 0)
        {
            w.WriteStartArray("overlays");
            foreach (var overlay in state.Overlays)
            {
                w.WriteStartObject();
                WriteCoord(w, "at", overlay.At);
                w.WriteString("terrain", overlay.TerrainId);
                w.WriteString("under", overlay.UnderId);
                w.WriteString("owner", overlay.OwnerId);
                w.WriteString("side", Name(overlay.Side));
                w.WriteNumber("clock", overlay.Clock);
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        if (state.Bars.Count > 0)
        {
            w.WriteStartArray("bars");
            foreach (var bar in state.Bars)
            {
                w.WriteStartObject();
                w.WriteString("event", bar.Event);
                WriteCoord(w, "holder", bar.Holder);
                WriteCoord(w, "at", bar.At);
                w.WriteString("terrain", bar.TerrainId);
                w.WriteString("under", bar.UnderId);
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        if (state.Waiting.Count > 0)
        {
            WriteStrings(w, "waiting", state.Waiting);
        }

        if (state.Map.KeepsakesEnabled)
        {
            w.WriteStartArray("keepsakes");
            foreach (var k in state.Keepsakes)
            {
                w.WriteStartObject();
                WriteCoord(w, "at", k.At);
                w.WriteString("fallen", k.FallenId);
                w.WriteString("item", k.Item.ItemId);
                w.WriteNumber("uses", k.Item.Uses);
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        WriteStrings(w, "awakeGroups", state.AwakeGroups);
        if (state.LitGroups.Count > 0)
        {
            WriteStrings(w, "litGroups", state.LitGroups);
        }

        w.WriteNumber("wakeRadius", content.WakeRadius);
        w.WriteNumber("noiseRadius", content.NoiseRadius);
        if (Dusk.Line(state, content) is { } duskLine)
        {
            // Derived (issue 765): the console's dusk line, the hearing radius with it, never read back.
            w.WriteString("duskLine", duskLine);
        }

        WriteStrings(w, "fired", state.Fired);
        WriteStrings(w, "flags", state.Flags);
        WriteRapport(w, state.Rapport);
        var outcome = state.Outcome;
        w.WriteStartObject("outcome");
        w.WriteString("result", Name(outcome.Result));
        w.WriteString("reason", Objective.Reason(state, content));
        w.WriteString("cause", Name(outcome.Cause));
        w.WriteEndObject();
        w.WriteNumber("historyCount", state.History.Count);
        if (full)
        {
            w.WriteStartArray("history");
            foreach (var prior in state.History)
            {
                WriteState(w, prior, content, full: true, playerView);
            }

            w.WriteEndArray();
        }

        w.WriteEndObject();
    }

    /// <summary>
    /// Reads a full state (<see cref="State"/>) back. The map text is parsed against
    /// <paramref name="content"/>, so a state names content the reader must have. A state
    /// written by another protocol version is refused rather than read into a different game.
    /// Derived fields (<c>outcome</c>, <c>maxHp</c>, <c>historyCount</c>, <c>wakeRadius</c>,
    /// <c>noiseRadius</c>, <c>seizeName</c>) are not read.
    /// </summary>
    public static BattleState ReadState(string json, GameContent content)
    {
        using var doc = Parse(json);
        return ReadState(doc.RootElement, content);
    }

    public static BattleState ReadState(JsonElement e, GameContent content)
    {
        var version = RequiredInt(e, "protocolVersion");
        if (version != ProtocolVersion.Current)
        {
            throw new ProtocolException($"protocolVersion {version} is not this build's {ProtocolVersion.Current}");
        }

        if (OptionalString(e, "view") == "player")
        {
            throw new ProtocolException("field 'view': a player-view state hides what the player cannot see and cannot be read back; write it with --omniscient");
        }

        if (!e.TryGetProperty("map", out var mapText) || mapText.ValueKind != JsonValueKind.String)
        {
            throw new ProtocolException("field 'map' is missing: only a full state (the state query's answer) can be read back");
        }

        MapDefinition map;
        try
        {
            map = MapFormat.Parse("protocol state", mapText.GetString()!, content);
        }
        catch (MapException ex)
        {
            throw new ProtocolException("field 'map': " + ex.Message);
        }

        if (!ulong.TryParse(RequiredString(e, "seed"), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seed))
        {
            throw new ProtocolException("field 'seed' is not an unsigned integer");
        }

        var history = new List<BattleState>();
        if (e.TryGetProperty("history", out var priors))
        {
            foreach (var prior in Array(priors, "history"))
            {
                history.Add(ReadState(prior, content));
            }
        }

        return new BattleState(
            map,
            ValueList<BattleUnit>.From(Array(Required(e, "units"), "units").Select(u => ReadUnit(u, content))),
            RequiredInt(e, "turn"),
            ParseEnum<Side>(RequiredString(e, "phase"), "phase"),
            seed,
            ParseEnum<RollScheme>(RequiredString(e, "scheme"), "scheme"),
            RequiredInt(e, "recallCharges"),
            ValueList<BattleState>.From(history),
            ReadStrings(e, "awakeGroups"),
            ReadStrings(e, "fired"),
            ReadStrings(e, "flags"),
            ReadRapport(Required(e, "rapport")),
            e.TryGetProperty("escaped", out var escaped)
                ? ValueList<BattleUnit>.From(Array(escaped, "escaped").Select(u => ReadUnit(u, content)))
                : ValueList<BattleUnit>.Empty,
            e.TryGetProperty("keepsakes", out var keepsakes)
                ? ValueList<Keepsake>.From(Array(keepsakes, "keepsakes").Select(k =>
                {
                    var fallen = RequiredString(k, "fallen");
                    return new Keepsake(ReadCoord(k, "at"), fallen, new ItemStack(RequiredString(k, "item"), RequiredInt(k, "uses")) { Keepsake = fallen });
                }))
                : ValueList<Keepsake>.Empty,
            e.TryGetProperty("litGroups", out _) ? ReadStrings(e, "litGroups") : ValueList<string>.Empty)
        {
            CampaignMap = OptionalInt(e, "campaignMap"),
            SideMap = e.TryGetProperty("sideMap", out _) && RequiredBool(e, "sideMap"),
            OrderCalled = OptionalString(e, "order") is { } order ? ReadOrderKind(order) : null,
            Opened = e.TryGetProperty("chests", out var chests)
                ? ValueList<Coord>.From(Array(chests, "chests").Where(c => RequiredBool(c, "open")).Select(c => ReadCoord(c, "at")).Order())
                : ValueList<Coord>.Empty,
            Wagon = e.TryGetProperty("wagon", out _) ? ReadItemIds(e, "wagon", content) : ValueList<string>.Empty,
            Rime = e.TryGetProperty("rime", out var rime)
                ? ValueList<RimeTile>.From(Array(rime, "rime").Select(r => new RimeTile(ReadCoord(r, "at"), ParseEnum<Side>(RequiredString(r, "side"), "side"), Math.Clamp(RequiredInt(r, "clock"), 1, 3)) { Extra = Math.Max(0, OptionalInt(r, "extra") ?? 0) }))
                : ValueList<RimeTile>.Empty,
            Overlays = e.TryGetProperty("overlays", out var overlays)
                ? ValueList<TileOverlay>.From(Array(overlays, "overlays").Select(o => new TileOverlay(
                    ReadCoord(o, "at"), RequiredString(o, "terrain"), RequiredString(o, "under"), RequiredString(o, "owner"),
                    ParseEnum<Side>(RequiredString(o, "side"), "side"), Math.Clamp(RequiredInt(o, "clock"), 1, 2))))
                : ValueList<TileOverlay>.Empty,
            Bars = e.TryGetProperty("bars", out var bars)
                ? ValueList<HeldBar>.From(Array(bars, "bars").Select(b => new HeldBar(
                    RequiredString(b, "event"), ReadCoord(b, "holder"), ReadCoord(b, "at"), RequiredString(b, "terrain"), RequiredString(b, "under"))))
                : ValueList<HeldBar>.Empty,
            Waiting = e.TryGetProperty("waiting", out _) ? ReadStrings(e, "waiting") : ValueList<string>.Empty,
        };
    }

    /// <summary>
    /// The string array <paramref name="name"/> of item ids (issue 679's wagon), each a weapon in
    /// weapons.json or an item in items.json; anything else is refused, naming the field.
    /// </summary>
    private static ValueList<string> ReadItemIds(JsonElement e, string name, GameContent content)
    {
        var ids = ReadStrings(e, name);
        foreach (var id in ids)
        {
            if (!content.Weapons.ContainsKey(id) && !content.Items.ContainsKey(id))
            {
                throw new ProtocolException($"field '{name}' names '{id}', which is not a weapon or an item");
            }
        }

        return ids;
    }

    private static void WriteUnit(Utf8JsonWriter w, BattleUnit unit, GameContent content, bool burning = false, SignatureKind? signature = null)
    {
        var u = unit.Unit;
        w.WriteStartObject();
        w.WriteString("id", u.Id);
        w.WriteString("name", u.Name);
        w.WriteString("side", Name(unit.Side));
        WriteCoord(w, "at", unit.At);
        w.WriteNumber("hp", unit.Hp);
        w.WriteNumber("maxHp", unit.MaxHp(content));
        w.WriteBoolean("moved", unit.Moved);
        w.WriteBoolean("acted", unit.Acted);
        w.WriteString("group", unit.Group);
        if (unit.Behavior is { } behavior)
        {
            w.WriteString("behavior", Name(behavior));
        }
        else
        {
            w.WriteNull("behavior");
        }

        w.WriteBoolean("isBoss", unit.IsBoss);
        w.WriteBoolean("isCaptain", unit.IsCaptain);
        w.WriteNumber("placementIndex", unit.PlacementIndex);
        w.WriteBoolean("retreated", unit.Retreated);
        WriteNullableNumber(w, "canto", unit.Canto);
        if (unit.Grudge is { } grudge)
        {
            w.WriteString("grudge", grudge);
        }

        if (unit.Shoved)
        {
            w.WriteBoolean("shoved", true);
        }

        if (signature is { } kind)
        {
            w.WriteString("signature", Name(kind));
            w.WriteString("signatureText", Signatures.Describe(kind));
        }

        if (unit.Braced)
        {
            w.WriteBoolean("braced", true);
        }

        if (unit.Breathed)
        {
            w.WriteBoolean("breathed", true);
        }

        if (unit.Winded)
        {
            w.WriteBoolean("winded", true);
        }

        if (unit.Answered)
        {
            w.WriteBoolean("answered", true);
        }

        if (unit.Pressed)
        {
            w.WriteBoolean("pressed", true);
        }

        if (unit.Chill > 0)
        {
            w.WriteNumber("chill", unit.Chill);
        }

        if (unit.BurnPhases > 0)
        {
            w.WriteNumber("burn", unit.Burn);
            w.WriteNumber("burnStacks", unit.BurnStacks);
            w.WriteNumber("burnPhases", unit.BurnPhases);
        }

        if (unit.Stun > 0)
        {
            w.WriteNumber("stun", unit.Stun);
        }

        if (unit.StunSpent)
        {
            w.WriteBoolean("stunSpent", true);
        }

        if (unit.LockedBy is { } lockedBy)
        {
            w.WriteString("lockedBy", lockedBy);
        }

        if (unit.Grounded > 0)
        {
            w.WriteNumber("grounded", unit.Grounded);
        }

        if (unit.Frosted > 0)
        {
            w.WriteNumber("frosted", unit.Frosted);
        }

        if (unit.FrostTurn is { } frostTurn)
        {
            w.WriteNumber("frostTurn", frostTurn);
        }

        if (unit.FlewFrom is { } flewFrom)
        {
            WriteCoord(w, "flewFrom", flewFrom);
        }

        if (unit.Open is { } open)
        {
            w.WriteStartObject("open");
            w.WriteString("by", open.By);
            w.WriteNumber("def", open.Def);
            w.WriteNumber("res", open.Res);
            w.WriteEndObject();
        }

        if (unit.FallingBack)
        {
            w.WriteBoolean("fallingBack", true);
        }

        if (unit.Spent != 0)
        {
            w.WriteNumber("spent", unit.Spent);
        }

        if (unit.HasFed)
        {
            w.WriteBoolean("hasFed", true);
        }

        if (unit.HuntRan)
        {
            w.WriteBoolean("huntRan", true);
        }

        if (unit.VoiceSpoken > 0)
        {
            w.WriteNumber("voiceSpoken", unit.VoiceSpoken);
        }

        if (unit.ArtsDeclared is { } declared)
        {
            w.WriteStartArray("artsDeclared");
            foreach (var art in declared)
            {
                w.WriteStringValue(art);
            }

            w.WriteEndArray();
        }

        if (burning)
        {
            w.WriteBoolean("burning", true);
        }

        if (unit.WindupAt is { } windup)
        {
            WriteCoord(w, "windupAt", windup);
        }

        if (unit.Watching)
        {
            w.WriteBoolean("watching", true);
        }

        if (unit.CoveredBy is { } coveredBy)
        {
            w.WriteString("coveredBy", coveredBy);
        }

        WriteRosterFields(w, u);
        w.WriteEndObject();
    }

    /// <summary>A <see cref="Unit"/>'s own fields, from its class on: the part of a unit a campaign carries between maps (issue 74).</summary>
    private static void WriteRosterFields(Utf8JsonWriter w, Unit u)
    {
        w.WriteString("class", u.ClassId);
        w.WriteNumber("level", u.Level);
        w.WriteNumber("exp", u.Exp);
        WriteStats(w, "stats", u.Stats);
        WriteStats(w, "growths", u.Growths);
        w.WriteStartArray("inventory");
        foreach (var stack in u.Inventory.Items)
        {
            w.WriteStartObject();
            w.WriteString("item", stack.ItemId);
            w.WriteNumber("uses", stack.Uses);
            if (stack.Keepsake is { } fallen)
            {
                w.WriteString("keepsake", fallen);
            }

            if (stack.Fed != 0)
            {
                w.WriteNumber("fed", stack.Fed);
            }

            if (stack.Starved)
            {
                w.WriteBoolean("starved", true);
            }

            if (stack.Combats != 0)
            {
                w.WriteNumber("combats", stack.Combats);
            }

            if (stack.Stage != 0)
            {
                w.WriteNumber("stage", stack.Stage);
            }

            if (stack.GateOpen)
            {
                w.WriteBoolean("gateOpen", true);
            }

            if (stack.Named)
            {
                w.WriteBoolean("named", true);
            }

            if (stack.Refines != 0)
            {
                w.WriteNumber("refines", stack.Refines);
                w.WriteNumber("refineMt", stack.RefineMt);
                w.WriteNumber("refineHit", stack.RefineHit);
            }

            w.WriteEndObject();
        }

        w.WriteEndArray();
        WriteStrings(w, "abilities", u.Abilities);
        w.WriteString("region", u.Region);
        w.WriteString("personality", u.Personality);
        WriteStrings(w, "hooks", u.Hooks);
        w.WriteStartObject("weaponPoints");
        foreach (var (type, points) in u.Skill.All)
        {
            w.WriteNumber(Name(type), points);
        }

        w.WriteEndObject();
        w.WriteStartObject("masteryPoints");
        foreach (var (classId, points) in u.Mastery.All)
        {
            w.WriteNumber(classId, points);
        }

        w.WriteEndObject();
        if (u.Pronoun is { } pronoun)
        {
            w.WriteString("pronoun", pronoun.ToString().ToLowerInvariant());
        }

        if (u.Doors.Count > 0)
        {
            w.WriteStartObject("doors");
            foreach (var (basis, form) in u.Doors)
            {
                w.WriteString(basis, form);
            }

            w.WriteEndObject();
        }

        if (u.Learned.Count > 0)
        {
            WriteStrings(w, "learned", u.Learned.Select(s => s.Label()));
        }

        if (u.Drake is { } drake)
        {
            w.WriteStartObject("drake");
            w.WriteString("stage", Drake.Word(drake.Stage));
            w.WriteNumber("flown", drake.Flown);
            w.WriteEndObject();
        }

        if (u.Wound is { } wound)
        {
            w.WriteStartObject("wound");
            WriteStats(w, "penalty", wound.Penalty);
            w.WriteNumber("mapsLeft", wound.MapsLeft);
            w.WriteEndObject();
        }
    }

    private static BattleUnit ReadUnit(JsonElement e, GameContent content)
    {
        var unit = ReadRosterUnit(e, content);
        var behavior = OptionalString(e, "behavior");
        return new BattleUnit(
            unit,
            ParseEnum<Side>(RequiredString(e, "side"), "side"),
            ReadCoord(e, "at"),
            RequiredInt(e, "hp"),
            RequiredBool(e, "moved"),
            RequiredBool(e, "acted"),
            OptionalString(e, "group"),
            behavior is null ? null : ParseEnum<Behavior>(behavior, "behavior"),
            RequiredBool(e, "isBoss"),
            RequiredBool(e, "isCaptain"),
            RequiredInt(e, "placementIndex"),
            RequiredBool(e, "retreated"),
            OptionalInt(e, "canto"),
            OptionalString(e, "grudge"),
            e.TryGetProperty("shoved", out _) && RequiredBool(e, "shoved"),
            e.TryGetProperty("braced", out _) && RequiredBool(e, "braced"),
            e.TryGetProperty("windupAt", out _) ? ReadCoord(e, "windupAt") : null)
        {
            Spent = OptionalInt(e, "spent") ?? 0,
            HasFed = e.TryGetProperty("hasFed", out _) && RequiredBool(e, "hasFed"),
            HuntRan = e.TryGetProperty("huntRan", out _) && RequiredBool(e, "huntRan"),
            VoiceSpoken = OptionalInt(e, "voiceSpoken") ?? 0,
            Pressed = e.TryGetProperty("pressed", out _) && RequiredBool(e, "pressed"),
            Chill = OptionalInt(e, "chill") ?? 0,
            Burn = OptionalInt(e, "burn") ?? 0,
            BurnStacks = OptionalInt(e, "burnStacks") ?? (OptionalInt(e, "burnPhases") > 0 ? 1 : 0),
            BurnPhases = OptionalInt(e, "burnPhases") ?? 0,
            Stun = OptionalInt(e, "stun") ?? 0,
            StunSpent = e.TryGetProperty("stunSpent", out _) && RequiredBool(e, "stunSpent"),
            LockedBy = OptionalString(e, "lockedBy"),
            Grounded = OptionalInt(e, "grounded") ?? 0,
            Frosted = OptionalInt(e, "frosted") ?? 0,
            FrostTurn = OptionalInt(e, "frostTurn"),
            FlewFrom = e.TryGetProperty("flewFrom", out _) ? ReadCoord(e, "flewFrom") : null,
            Breathed = e.TryGetProperty("breathed", out _) && RequiredBool(e, "breathed"),
            Winded = e.TryGetProperty("winded", out _) && RequiredBool(e, "winded"),
            Answered = e.TryGetProperty("answered", out _) && RequiredBool(e, "answered"),
            Open = e.TryGetProperty("open", out var open) ? new OpenMark(RequiredString(open, "by"), RequiredInt(open, "def"), RequiredInt(open, "res")) : null,
            FallingBack = e.TryGetProperty("fallingBack", out _) && RequiredBool(e, "fallingBack"),
            ArtsDeclared = e.TryGetProperty("artsDeclared", out var declared) ? ValueList<string>.From(declared.EnumerateArray().Select(a => a.GetString()!)) : null,
        };
    }

    /// <summary>
    /// The optional <c>wound</c> of a unit (issue 664): the penalty its stats carry and the main
    /// maps left, 1 or 2. A unit written before it, or without one, reads as unwounded.
    /// </summary>
    private static Wound? ReadWound(JsonElement e)
    {
        if (!e.TryGetProperty("wound", out var w) || w.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var maps = RequiredInt(w, "mapsLeft");
        if (maps is < 1 or > Wound.MainMaps)
        {
            throw new ProtocolException($"field 'wound.mapsLeft' must be 1 to {Wound.MainMaps}");
        }

        return new Wound(ReadStats(Required(w, "penalty")), maps);
    }

    /// <summary>
    /// The optional <c>doors</c> of a unit (issue 706): each base class whose second promotion it has
    /// passed, keyed to the form it took, in the order written. A unit written before it reads as having passed none.
    /// </summary>
    private static ValueList<(string Base, string Form)> ReadDoors(JsonElement e)
    {
        if (!e.TryGetProperty("doors", out var doors) || doors.ValueKind == JsonValueKind.Null)
        {
            return ValueList<(string, string)>.Empty;
        }

        return ValueList<(string, string)>.From(doors.EnumerateObject().Select(p => (p.Name, p.Value.GetString() ?? throw new ProtocolException($"field 'doors.{p.Name}' must be a class id"))));
    }

    /// <summary>
    /// The optional <c>learned</c> of a unit (issue 1246): the schools it learned from primers, in the
    /// order learned. A unit written before it, or without one, has learned none; a word that is not a
    /// school is refused.
    /// </summary>
    private static ValueList<MagicSchool> ReadLearned(JsonElement e) =>
        !e.TryGetProperty("learned", out _) ? ValueList<MagicSchool>.Empty : ValueList<MagicSchool>.From(ReadStrings(e, "learned").Select(word =>
            Enum.GetValues<MagicSchool>().Where(s => s.Label() == word).Select(s => (MagicSchool?)s).FirstOrDefault()
                ?? throw new ProtocolException($"field 'learned' must hold only {string.Join(", ", Enum.GetValues<MagicSchool>().Select(s => s.Label()))}, not '{word}'")));

    /// <summary>
    /// The optional <c>drake</c> of a unit (issue 805): its <c>stage</c> as the screen words it and the
    /// main maps <c>flown</c>. A unit written before it, or without one, rides none.
    /// </summary>
    private static DrakeState? ReadDrake(JsonElement e)
    {
        if (!e.TryGetProperty("drake", out var d) || d.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var stage = ReadStage(d, "stage", "drake.stage");
        var flown = RequiredInt(d, "flown");
        if (flown < 0)
        {
            throw new ProtocolException("field 'drake.flown' must be at least 0");
        }

        return new DrakeState(stage, flown);
    }

    /// <summary>The record's <c>keziahOath</c> (issue 635 slice 16): one of the four words, any other refused.</summary>
    private static OathSide ReadOath(JsonElement e) =>
        Oath.Parse(RequiredString(e, "keziahOath"))
            ?? throw new ProtocolException($"field 'keziahOath' must be one of {string.Join(", ", Enum.GetValues<OathSide>().Select(Oath.Word))}");

    /// <summary>A drake's stage (issue 805) as the screen words it, under <paramref name="field"/>; any other word refused, named as <paramref name="label"/>.</summary>
    private static DrakeStage ReadStage(JsonElement e, string field, string? label = null)
    {
        var word = RequiredString(e, field);
        return Enum.GetValues<DrakeStage>().Where(s => Drake.Word(s) == word).Select(s => (DrakeStage?)s).FirstOrDefault()
            ?? throw new ProtocolException($"field '{label ?? field}' must be one of {string.Join(", ", Enum.GetValues<DrakeStage>().Select(Drake.Word))}");
    }

    /// <summary>A <see cref="Unit"/> from its id, name and own fields; the battle fields around it are not read.</summary>
    private static Unit ReadRosterUnit(JsonElement e, GameContent content)
    {
        var id = RequiredString(e, "id");
        var classId = RequiredString(e, "class");
        if (!content.Classes.ContainsKey(classId))
        {
            throw new ProtocolException($"unit '{id}': class '{classId}' is not in the content");
        }

        Unit unit;
        try
        {
            unit = new Unit(
                id,
                RequiredString(e, "name"),
                classId,
                RequiredInt(e, "level"),
                RequiredInt(e, "exp"),
                ReadStats(Required(e, "stats")),
                ReadStats(Required(e, "growths")),
                new Inventory(ValueList<ItemStack>.From(Array(Required(e, "inventory"), "inventory").Select(s => new ItemStack(RequiredString(s, "item"), RequiredInt(s, "uses")) { Keepsake = OptionalString(s, "keepsake"), Fed = OptionalInt(s, "fed") ?? 0, Starved = s.TryGetProperty("starved", out _) && RequiredBool(s, "starved"), Combats = OptionalInt(s, "combats") ?? 0, Stage = OptionalInt(s, "stage") ?? 0, GateOpen = s.TryGetProperty("gateOpen", out _) && RequiredBool(s, "gateOpen"), Named = s.TryGetProperty("named", out _) && RequiredBool(s, "named"), Refines = OptionalInt(s, "refines") ?? 0, RefineMt = OptionalInt(s, "refineMt") ?? 0, RefineHit = OptionalInt(s, "refineHit") ?? 0 }))),
                ReadStrings(e, "abilities"),
                OptionalString(e, "region"),
                OptionalString(e, "personality"))
            {
                Hooks = ReadStrings(e, "hooks"),
                Skill = ReadWeaponPoints(e),
                Mastery = ReadMasteryPoints(e),
                Wound = ReadWound(e),
                Pronoun = ReadPronoun(e, id),
                Doors = ReadDoors(e),
                Drake = ReadDrake(e),
                Learned = ReadLearned(e),
            };
        }
        catch (ArgumentException ex)
        {
            throw new ProtocolException($"unit '{id}': {ex.Message}");
        }

        return unit;
    }

    /// <summary>
    /// A scene as it plays (issue 1001): <c>scene</c> (its id), <c>point</c> (<c>before</c>, <c>camp</c>,
    /// <c>after</c> or <c>support</c>), <c>map</c> (the campaign map id), or for a support conversation
    /// (issue 77 slice 8) <c>a</c>, <c>b</c> and <c>tier</c> in its place, and <c>lines</c>, the lines shown on the
    /// record it plays against, each <c>id</c>, <c>speaker</c> (a unit id, an incidental's id, <c>narration</c>
    /// or <c>rules</c>) and <c>text</c>; then, when the scene declares any, <c>incidental</c>, each <c>id</c> and <c>name</c>.
    /// </summary>
    public static string Scene(Scene scene, IEnumerable<SceneLine> shown) => Write(w =>
    {
        w.WriteStartObject();
        w.WriteString("scene", scene.Id);
        w.WriteString("point", SceneFormat.PointWord(scene.Point));
        if (scene.Support is { } support)
        {
            w.WriteString("a", support.A);
            w.WriteString("b", support.B);
            w.WriteString("tier", support.Tier);
        }
        else
        {
            w.WriteString("map", scene.MapId);
        }

        w.WriteStartArray("lines");
        foreach (var line in shown)
        {
            w.WriteStartObject();
            w.WriteString("id", line.Id);
            w.WriteString("speaker", line.Speaker);
            w.WriteString("text", line.Text);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        if (scene.Incidentals.Count > 0)
        {
            w.WriteStartArray("incidental");
            foreach (var incidental in scene.Incidentals)
            {
                w.WriteStartObject();
                w.WriteString("id", incidental.Id);
                w.WriteString("name", incidental.Name);
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        w.WriteEndObject();
    });

    /// <summary>
    /// A campaign record (issue 74) as one JSON object: the protocol version, the seed as a string
    /// (a ulong does not survive every JSON reader), the difficulty, permadeath when it is off (issue 664;
    /// a record without it reads as on), the captain's origin when one was chosen (issue 681), the branch's pick once made (issue 633), the support conversations seen when there are any (issue 77 slice 8), the difficulties it was lowered from when there are any (issue 677), the purse, the index of the next
    /// map, the roster in roster order (each unit's id, name and own fields as a state writes them,
    /// without the battle fields), the fallen and benched ids, and the certification trials tried
    /// since the last map (issue 252), the side maps won and those fought since the last map when there are any (issue 635), and the edits bought for the keep in the order they were made
    /// (issue 288). A campaign is a file.
    /// </summary>
    public static string Campaign(CampaignRecord record) => Campaign(record, null);

    /// <summary>
    /// A campaign record as <see cref="Campaign(CampaignRecord)"/> writes it, with <paramref name="ending"/>
    /// as its <c>ending</c> block when given (issue 807): the save written when a campaign is won.
    /// <see cref="ReadCampaign"/> ignores the block; <see cref="ReadEnding"/> reads it.
    /// </summary>
    public static string Campaign(CampaignRecord record, CampaignEnding? ending) => Write(w =>
    {
        w.WriteStartObject();
        w.WriteNumber("protocolVersion", ProtocolVersion.Current);
        w.WriteString("seed", record.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        w.WriteString("difficulty", record.Difficulty);
        if (!record.Permadeath)
        {
            w.WriteBoolean("permadeath", false);
        }

        if (record.FreedUnitFell)
        {
            w.WriteBoolean("freedUnitFell", true);
        }

        if (record.DrakeFlew is { } flew)
        {
            w.WriteString("drakeFlew", Drake.Word(flew));
        }

        if (record.KeziahOath is { } oath)
        {
            w.WriteString("keziahOath", Oath.Word(oath));
        }

        if (record.Rapport.Count > 0)
        {
            WriteRapport(w, record.Rapport);
        }

        if (record.Origin is { } origin)
        {
            w.WriteString("origin", origin);
        }

        if (record.Pick is { } pick)
        {
            w.WriteString("pick", pick);
        }

        if (record.WarningConfirmed is { } confirmed)
        {
            w.WriteNumber("warningConfirmed", confirmed);
        }

        if (record.Met.Count > 0)
        {
            w.WriteStartArray("met");
            foreach (var id in record.Met)
            {
                w.WriteStringValue(id);
            }

            w.WriteEndArray();
        }

        if (record.SupportsSeen.Count > 0)
        {
            w.WriteStartArray("supportsSeen");
            foreach (var id in record.SupportsSeen)
            {
                w.WriteStringValue(id);
            }

            w.WriteEndArray();
        }

        if (record.Returned is { } returned)
        {
            w.WriteString("returned", ClaimantFateName(returned));
        }

        if (record.LoweredFrom.Count > 0)
        {
            w.WriteStartArray("loweredFrom");
            foreach (var id in record.LoweredFrom)
            {
                w.WriteStringValue(id);
            }

            w.WriteEndArray();
        }

        w.WriteNumber("purse", record.Purse);
        w.WriteNumber("mapIndex", record.MapIndex);
        w.WriteStartArray("roster");
        foreach (var u in record.Roster)
        {
            w.WriteStartObject();
            w.WriteString("id", u.Id);
            w.WriteString("name", u.Name);
            WriteRosterFields(w, u);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        WriteStrings(w, "fallen", record.Fallen);
        if (record.FellOn.Count > 0)
        {
            w.WriteStartArray("fellOn");
            foreach (var fell in record.FellOn)
            {
                w.WriteStartObject();
                w.WriteString("unit", fell.UnitId);
                w.WriteString("map", fell.MapName);
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        WriteStrings(w, "benched", record.Benched);
        w.WriteStartArray("trialsTried");
        foreach (var attempt in record.TrialsTried)
        {
            w.WriteStartObject();
            w.WriteString("unit", attempt.UnitId);
            w.WriteString("class", attempt.ClassId);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        if (record.QuestsWon.Count > 0)
        {
            w.WriteStartArray("questsWon");
            foreach (var won in record.QuestsWon)
            {
                w.WriteStartObject();
                w.WriteString("quest", won.QuestId);
                w.WriteNumber("mapIndex", won.MapIndex);
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        if (record.QuestsTried.Count > 0)
        {
            WriteStrings(w, "questsTried", record.QuestsTried);
        }

        if (record.QuestsSeen.Count > 0)
        {
            WriteStrings(w, "questsSeen", record.QuestsSeen);
        }

        w.WriteStartArray("keep");
        foreach (var work in record.Keep)
        {
            w.WriteStartObject();
            w.WriteString("edit", work.EditId);
            WriteCoord(w, "at", work.At);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        if (record.Rooms.Count > 0)
        {
            WriteStrings(w, "rooms", record.Rooms);
        }

        if (record.CommonMaterial > 0)
        {
            w.WriteNumber("commonMaterial", record.CommonMaterial);
        }

        if (record.RareMaterial > 0)
        {
            w.WriteNumber("rareMaterial", record.RareMaterial);
        }

        if (record.Wagon.Count > 0)
        {
            WriteStrings(w, "wagon", record.Wagon);
        }

        if (ending is not null)
        {
            WriteEnding(w, ending);
        }

        w.WriteEndObject();
    });

    /// <summary>
    /// The <c>ending</c> block (issue 807, docs/PROTOCOL.md): every field always written, absent
    /// facts as null, so a sequel reads one fixed shape per <c>version</c>.
    /// </summary>
    private static void WriteEnding(Utf8JsonWriter w, CampaignEnding ending)
    {
        w.WriteStartObject("ending");
        w.WriteNumber("version", ending.Version);
        w.WriteString("ending", ending.Ending);
        w.WriteString("seed", ending.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        w.WriteString("difficulty", ending.Difficulty);
        w.WriteBoolean("permadeath", ending.Permadeath);
        w.WriteStartObject("captain");
        WriteStringOrNull(w, "origin", ending.CaptainOrigin);
        WriteStringOrNull(w, "pronoun", ending.CaptainPronoun is { } pronoun ? PronounName(pronoun) : null);
        w.WriteString("class", ending.CaptainClass);
        w.WriteEndObject();
        WriteStringOrNull(w, "pick", ending.Pick);
        WriteStringOrNull(w, "passed", ending.Passed);
        WriteStringOrNull(w, "passedFate", ending.PassedFate is { } fate ? ClaimantFateName(fate) : null);
        WriteStrings(w, "lived", ending.Lived);
        WriteStrings(w, "fallen", ending.Fallen);
        w.WriteBoolean("freedUnitFell", ending.FreedUnitFell);
        if (ending.Kinsbane is { } kinsbane)
        {
            w.WriteStartObject("kinsbane");
            w.WriteString("bearer", kinsbane.Bearer);
            w.WriteNumber("fed", kinsbane.Fed);
            w.WriteNumber("teeth", kinsbane.Teeth);
            w.WriteBoolean("woken", kinsbane.Woken);
            w.WriteEndObject();
        }
        else
        {
            w.WriteNull("kinsbane");
        }

        if (ending.Drake is { } drake)
        {
            w.WriteStartObject("drake");
            w.WriteString("stage", Drake.Word(drake.Stage));
            w.WriteBoolean("riderLived", drake.RiderLived);
            w.WriteEndObject();
        }
        else
        {
            w.WriteNull("drake");
        }

        WriteStrings(w, "rooms", ending.Rooms);
        w.WriteEndObject();
    }

    private static void WriteStringOrNull(Utf8JsonWriter w, string name, string? value)
    {
        if (value is null)
        {
            w.WriteNull(name);
        }
        else
        {
            w.WriteString(name, value);
        }
    }

    private static string PronounName(Pronoun pronoun) => pronoun switch
    {
        Pronoun.He => "he",
        Pronoun.She => "she",
        _ => "they",
    };

    /// <summary>
    /// The <c>ending</c> block of a campaign save written by <see cref="Campaign(CampaignRecord, CampaignEnding)"/>
    /// (issue 807), or null when the save has none (a campaign still marching). A block of another
    /// version is refused, as is a field of the wrong shape, naming the field.
    /// </summary>
    public static CampaignEnding? ReadEnding(string json)
    {
        using var doc = Parse(json);
        if (!doc.RootElement.TryGetProperty("ending", out var e) || e.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (e.ValueKind != JsonValueKind.Object)
        {
            throw new ProtocolException("field 'ending' is not an object");
        }

        var version = RequiredInt(e, "version");
        if (version != CampaignEnding.CurrentVersion)
        {
            throw new ProtocolException($"ending version {version} is not this build's {CampaignEnding.CurrentVersion}");
        }

        if (!ulong.TryParse(RequiredString(e, "seed"), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seed))
        {
            throw new ProtocolException("field 'ending.seed' is not an unsigned integer");
        }

        var captain = Required(e, "captain");
        var pronounText = OptionalString(captain, "pronoun");
        Pronoun? pronoun = pronounText is null ? null
            : Enum.GetValues<Pronoun>().Where(p => PronounName(p) == pronounText).Select(p => (Pronoun?)p).FirstOrDefault()
                ?? throw new ProtocolException($"ending.captain.pronoun '{pronounText}' is not one of he, she, they");
        var fateText = OptionalString(e, "passedFate");
        ClaimantFate? fate = fateText is null ? null
            : Enum.GetValues<ClaimantFate>().Where(f => ClaimantFateName(f) == fateText).Select(f => (ClaimantFate?)f).FirstOrDefault()
                ?? throw new ProtocolException($"ending.passedFate '{fateText}' is not one of turned, turnedAway, spared, fell, stood");
        EndingKinsbane? kinsbane = e.TryGetProperty("kinsbane", out var k) && k.ValueKind != JsonValueKind.Null
            ? new EndingKinsbane(RequiredString(k, "bearer"), RequiredInt(k, "fed"), RequiredInt(k, "teeth"), RequiredBool(k, "woken"))
            : null;
        EndingDrake? drake = e.TryGetProperty("drake", out var d) && d.ValueKind != JsonValueKind.Null
            ? new EndingDrake(ReadStage(d, "stage", "ending.drake.stage"), RequiredBool(d, "riderLived"))
            : null;
        return new CampaignEnding(
            version,
            RequiredString(e, "ending"),
            seed,
            RequiredString(e, "difficulty"),
            RequiredBool(e, "permadeath"),
            OptionalString(captain, "origin"),
            pronoun,
            RequiredString(captain, "class"),
            OptionalString(e, "pick"),
            OptionalString(e, "passed"),
            fate,
            ReadStrings(e, "lived"),
            ReadStrings(e, "fallen"),
            RequiredBool(e, "freedUnitFell"),
            kinsbane,
            drake,
            ReadStrings(e, "rooms"));
    }

    /// <summary>Reads a campaign record written by <see cref="Campaign(CampaignRecord)"/>; another protocol version is refused.</summary>
    public static CampaignRecord ReadCampaign(string json, GameContent content)
    {
        using var doc = Parse(json);
        var e = doc.RootElement;
        var version = RequiredInt(e, "protocolVersion");
        if (version != ProtocolVersion.Current)
        {
            throw new ProtocolException($"protocolVersion {version} is not this build's {ProtocolVersion.Current}");
        }

        if (!ulong.TryParse(RequiredString(e, "seed"), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seed))
        {
            throw new ProtocolException("field 'seed' is not an unsigned integer");
        }

        var roster = Array(Required(e, "roster"), "roster").Select(u => ReadRosterUnit(u, content)).ToList();
        if (roster.Count == 0)
        {
            throw new ProtocolException("field 'roster' is empty: a campaign needs its captain");
        }

        var purse = RequiredInt(e, "purse");
        var mapIndex = RequiredInt(e, "mapIndex");
        if (purse < 0 || mapIndex < 0)
        {
            throw new ProtocolException("fields 'purse' and 'mapIndex' must be at least 0");
        }

        // A save whose keep work a later menu moved loads with that work refunded (issue 1154).
        return new CampaignRecord(
            ValueList<Unit>.From(roster),
            ReadStrings(e, "fallen"),
            purse,
            mapIndex,
            seed,
            RequiredString(e, "difficulty"),
            ReadStrings(e, "benched"))
        {
            TrialsTried = ReadTrialsTried(e),
            QuestsWon = ReadQuestsWon(e, content),
            QuestsTried = e.TryGetProperty("questsTried", out _) ? ReadStrings(e, "questsTried") : ValueList<string>.Empty,
            QuestsSeen = e.TryGetProperty("questsSeen", out _) ? ReadStrings(e, "questsSeen") : ValueList<string>.Empty,
            FellOn = ReadFellOn(e),
            Keep = ReadKeep(e, content),
            Rooms = ReadRooms(e, content),
            CommonMaterial = Math.Max(0, OptionalInt(e, "commonMaterial") ?? 0),
            RareMaterial = Math.Max(0, OptionalInt(e, "rareMaterial") ?? 0),
            Wagon = e.TryGetProperty("wagon", out _) ? ReadItemIds(e, "wagon", content) : ValueList<string>.Empty,
            Permadeath = !e.TryGetProperty("permadeath", out _) || RequiredBool(e, "permadeath"),
            FreedUnitFell = e.TryGetProperty("freedUnitFell", out _) && RequiredBool(e, "freedUnitFell"),
            DrakeFlew = e.TryGetProperty("drakeFlew", out _) ? ReadStage(e, "drakeFlew") : null,
            KeziahOath = e.TryGetProperty("keziahOath", out _) ? ReadOath(e) : null,
            Rapport = e.TryGetProperty("rapport", out var rapport) ? ReadRapport(rapport) : ValueList<Rapport>.Empty,
            LoweredFrom = ReadLoweredFrom(e, content),
            Origin = ReadOrigin(e, content),
            Pick = ReadPick(e, content),
            WarningConfirmed = OptionalInt(e, "warningConfirmed"),
            Returned = ReadReturned(e),
            Met = ReadMet(e, content),
            SupportsSeen = ReadSupportsSeen(e, content),
        }.SettleKeep(content);
    }

    /// <summary>A returned claimant's fate's protocol name (issue 633).</summary>
    private static string ClaimantFateName(ClaimantFate fate) => fate switch
    {
        ClaimantFate.Turned => "turned",
        ClaimantFate.TurnedAway => "turnedAway",
        ClaimantFate.Spared => "spared",
        ClaimantFate.Fell => "fell",
        _ => "stood",
    };

    /// <summary>The optional <c>returned</c> of a campaign record (issue 633), the passed claimant's fate; a record without one has not fought the return.</summary>
    private static ClaimantFate? ReadReturned(JsonElement e)
    {
        if (OptionalString(e, "returned") is not { } text)
        {
            return null;
        }

        return Enum.GetValues<ClaimantFate>().Where(f => ClaimantFateName(f) == text).Select(f => (ClaimantFate?)f).FirstOrDefault()
            ?? throw new ProtocolException($"returned '{text}' is not one of turned, turnedAway, spared, fell, stood");
    }

    /// <summary>The <c>rapport</c> array a board or a record carries (issues 16 and 77): each pair's ids and points.</summary>
    private static void WriteRapport(Utf8JsonWriter w, ValueList<Rapport> rapport)
    {
        w.WriteStartArray("rapport");
        foreach (var r in rapport)
        {
            w.WriteStartObject();
            w.WriteString("a", r.A);
            w.WriteString("b", r.B);
            w.WriteNumber("points", r.Points);
            w.WriteEndObject();
        }

        w.WriteEndArray();
    }

    private static ValueList<Rapport> ReadRapport(JsonElement array) =>
        ValueList<Rapport>.From(Array(array, "rapport").Select(r => new Rapport(RequiredString(r, "a"), RequiredString(r, "b"), RequiredInt(r, "points"))));

    /// <summary>The optional <c>pronoun</c> of a roster unit (issue 681), chosen at the start; a unit without one reads the cast file's.</summary>
    private static Pronoun? ReadPronoun(JsonElement e, string id)
    {
        if (OptionalString(e, "pronoun") is not { } text)
        {
            return null;
        }

        return text switch
        {
            "he" => Pronoun.He,
            "she" => Pronoun.She,
            "they" => Pronoun.They,
            _ => throw new ProtocolException($"unit '{id}': pronoun '{text}' is not one of he, she, they"),
        };
    }

    /// <summary>The optional <c>origin</c> of a campaign record (issue 681), an origin the content offers; a record without one has the cast file's captain.</summary>
    private static string? ReadOrigin(JsonElement e, GameContent content)
    {
        if (OptionalString(e, "origin") is not { } origin)
        {
            return null;
        }

        return content.Campaign.Origin(origin) is null
            ? throw new ProtocolException($"origin '{origin}' is not one the campaign offers")
            : origin;
    }

    /// <summary>The optional <c>pick</c> of a campaign record (issue 633), a claimant some campaign map's branch offers; a record without one has made no pick.</summary>
    private static string? ReadPick(JsonElement e, GameContent content)
    {
        if (OptionalString(e, "pick") is not { } pick)
        {
            return null;
        }

        return content.Campaign.Maps.Any(m => m.Branch.Contains(pick))
            ? pick
            : throw new ProtocolException($"pick '{pick}' is not a claimant the campaign offers");
    }

    /// <summary>The optional <c>met</c> array of a campaign record (issue 633 slice 3), side characters some campaign map's <c>meets</c> offers; a record without one has met nobody.</summary>
    private static ValueList<string> ReadMet(JsonElement e, GameContent content)
    {
        if (!e.TryGetProperty("met", out _))
        {
            return ValueList<string>.Empty;
        }

        var met = Array(Required(e, "met"), "met").Select(m => m.ValueKind == JsonValueKind.String ? m.GetString()! : throw new ProtocolException("met entries must be strings")).ToList();
        foreach (var id in met)
        {
            if (!content.Campaign.Maps.Any(m => m.Meets.Contains(id)))
            {
                throw new ProtocolException($"met '{id}' is not a side character the campaign offers");
            }
        }

        return ValueList<string>.From(met);
    }

    /// <summary>The optional <c>supportsSeen</c> array of a campaign record (issue 77 slice 8), ids of the content's support conversations; a record without one has seen none.</summary>
    private static ValueList<string> ReadSupportsSeen(JsonElement e, GameContent content)
    {
        if (!e.TryGetProperty("supportsSeen", out _))
        {
            return ValueList<string>.Empty;
        }

        var seen = Array(Required(e, "supportsSeen"), "supportsSeen").Select(m => m.ValueKind == JsonValueKind.String ? m.GetString()! : throw new ProtocolException("supportsSeen entries must be strings")).ToList();
        foreach (var id in seen)
        {
            if (!content.Scenes.Any(s => s.Id == id && s.Support is not null))
            {
                throw new ProtocolException($"supportsSeen '{id}' is not a support conversation of the content");
            }
        }

        return ValueList<string>.From(seen);
    }

    /// <summary>The optional <c>fellOn</c> array of a campaign record (issue 678), the board each of the fallen fell on; a record written before it reads as none.</summary>
    private static ValueList<FellOn> ReadFellOn(JsonElement e) =>
        e.TryGetProperty("fellOn", out _)
            ? ValueList<FellOn>.From(Array(Required(e, "fellOn"), "fellOn").Select(f => new FellOn(RequiredString(f, "unit"), RequiredString(f, "map"))))
            : ValueList<FellOn>.Empty;

    /// <summary>The optional <c>loweredFrom</c> array of a campaign record (issue 677), difficulty ids; a record written before it reads as never lowered.</summary>
    private static ValueList<string> ReadLoweredFrom(JsonElement e, GameContent content)
    {
        if (!e.TryGetProperty("loweredFrom", out _))
        {
            return ValueList<string>.Empty;
        }

        var ids = ReadStrings(e, "loweredFrom");
        foreach (var id in ids.Where(id => !content.Difficulties.ContainsKey(id)))
        {
            throw new ProtocolException($"field 'loweredFrom': '{id}' is not a difficulty");
        }

        return ids;
    }

    /// <summary>The optional <c>rooms</c> array of a campaign record (issue 687), room ids as bought; a record written before it reads as none.</summary>
    private static ValueList<string> ReadRooms(JsonElement e, GameContent content)
    {
        if (!e.TryGetProperty("rooms", out _))
        {
            return ValueList<string>.Empty;
        }

        var rooms = ReadStrings(e, "rooms");
        foreach (var id in rooms.Where(id => content.Campaign.Keep.Room(id) is null))
        {
            throw new ProtocolException($"field 'rooms': '{id}' is not a room the keep sells");
        }

        return rooms;
    }

    /// <summary>The optional <c>keep</c> array of a campaign record (issue 288); a record written before it reads as nothing built.</summary>
    private static ValueList<KeepWork> ReadKeep(JsonElement e, GameContent content)
    {
        if (!e.TryGetProperty("keep", out _))
        {
            return ValueList<KeepWork>.Empty;
        }

        var built = Array(Required(e, "keep"), "keep").Select(k => new KeepWork(RequiredString(k, "edit"), ReadCoord(k, "at"))).ToList();
        foreach (var work in built.Where(w => content.Campaign.Keep.Edit(w.EditId) is null))
        {
            throw new ProtocolException($"field 'keep': '{work.EditId}' is not on the keep's menu");
        }

        return ValueList<KeepWork>.From(built);
    }

    /// <summary>The optional <c>questsWon</c> array of a campaign record (issue 635); a record written before it reads as none won. A quest the campaign does not list is refused.</summary>
    private static ValueList<QuestWon> ReadQuestsWon(JsonElement e, GameContent content)
    {
        if (!e.TryGetProperty("questsWon", out _))
        {
            return ValueList<QuestWon>.Empty;
        }

        var won = Array(Required(e, "questsWon"), "questsWon").Select(q => new QuestWon(RequiredString(q, "quest"), RequiredInt(q, "mapIndex"))).ToList();
        foreach (var quest in won.Where(q => content.Campaign.Quest(q.QuestId) is null))
        {
            throw new ProtocolException($"field 'questsWon': '{quest.QuestId}' is not a side map of the campaign");
        }

        return ValueList<QuestWon>.From(won);
    }

    /// <summary>The optional <c>trialsTried</c> array of a campaign record (issue 252); a record written before it reads as none tried.</summary>
    private static ValueList<TrialAttempt> ReadTrialsTried(JsonElement e) =>
        e.TryGetProperty("trialsTried", out _)
            ? ValueList<TrialAttempt>.From(Array(Required(e, "trialsTried"), "trialsTried")
                .Select(a => new TrialAttempt(RequiredString(a, "unit"), RequiredString(a, "class"))))
            : ValueList<TrialAttempt>.Empty;

    private static void WriteSide(Utf8JsonWriter w, string name, SideForecast side)
    {
        w.WriteStartObject(name);
        w.WriteBoolean("strikes", side.Strikes);
        w.WriteNumber("damage", side.Damage);
        w.WriteNumber("hitChance", side.HitChance);
        w.WriteNumber("displayedHit", side.DisplayedHit);
        w.WriteNumber("critChance", side.CritChance);
        w.WriteBoolean("doubles", side.Doubles);
        w.WriteNumber("strikesPerRound", side.StrikesPerRound);
        if (side.CritGrounds)
        {
            w.WriteBoolean("critGrounds", true);
        }

        if (side.Bite > 0)
        {
            w.WriteNumber("bite", side.Bite);
        }

        if (side.NeverDoubles)
        {
            w.WriteBoolean("neverDoubles", true);
        }

        if (side.Stoop > 0)
        {
            w.WriteNumber("stoop", side.Stoop);
        }

        w.WriteEndObject();
    }

    private static SideForecast ReadSide(JsonElement e) => new(
        RequiredBool(e, "strikes"), RequiredInt(e, "damage"), RequiredInt(e, "hitChance"), RequiredInt(e, "displayedHit"), RequiredInt(e, "critChance"), RequiredBool(e, "doubles"), OptionalInt(e, "strikesPerRound") ?? 1, e.TryGetProperty("critGrounds", out _) && RequiredBool(e, "critGrounds"), OptionalInt(e, "bite") ?? 0, e.TryGetProperty("neverDoubles", out _) && RequiredBool(e, "neverDoubles"), OptionalInt(e, "stoop") ?? 0);

    private static readonly string[] StatKeys = { "hp", "str", "mag", "dex", "spd", "lck", "def", "res", "cha" };

    private static void WriteStats(Utf8JsonWriter w, string name, Stats stats)
    {
        w.WriteStartObject(name);
        for (var i = 0; i < StatKeys.Length; i++)
        {
            w.WriteNumber(StatKeys[i], stats.Get(Stats.All[i]));
        }

        w.WriteEndObject();
    }

    private static Stats ReadStats(JsonElement e)
    {
        var stats = Stats.Zero;
        for (var i = 0; i < StatKeys.Length; i++)
        {
            stats = stats.With(Stats.All[i], RequiredInt(e, StatKeys[i]));
        }

        return stats;
    }

    private static void WriteCoord(Utf8JsonWriter w, string name, Coord at)
    {
        w.WriteStartObject(name);
        w.WriteNumber("x", at.X);
        w.WriteNumber("y", at.Y);
        w.WriteEndObject();
    }

    private static void WriteCoords(Utf8JsonWriter w, string name, IEnumerable<Coord> coords)
    {
        w.WriteStartArray(name);
        foreach (var at in coords)
        {
            w.WriteStartObject();
            w.WriteNumber("x", at.X);
            w.WriteNumber("y", at.Y);
            w.WriteEndObject();
        }

        w.WriteEndArray();
    }

    private static void WriteStrings(Utf8JsonWriter w, string name, IEnumerable<string> values)
    {
        w.WriteStartArray(name);
        foreach (var value in values)
        {
            w.WriteStringValue(value);
        }

        w.WriteEndArray();
    }

    private static void WriteNullableNumber(Utf8JsonWriter w, string name, int? value)
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

    /// <summary>An enum value's protocol name: its C# name with the first letter lower-cased.</summary>
    public static string Name<T>(T value)
        where T : struct, Enum => CamelCase(value.ToString());

    private static string CamelCase(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    private static T ParseEnum<T>(string text, string field)
        where T : struct, Enum
    {
        foreach (var value in Enum.GetValues<T>())
        {
            if (Name(value) == text)
            {
                return value;
            }
        }

        throw new ProtocolException($"field '{field}': '{text}' is not one of: {string.Join(", ", Enum.GetValues<T>().Select(v => Name(v)))}");
    }

    public static string Write(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static JsonDocument Parse(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ProtocolException("not JSON: " + ex.Message);
        }
    }

    private static JsonElement Required(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object)
        {
            throw new ProtocolException($"expected an object holding '{name}', got {e.ValueKind.ToString().ToLowerInvariant()}");
        }

        if (!e.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            throw new ProtocolException($"field '{name}' is missing");
        }

        return value;
    }

    public static string RequiredString(JsonElement e, string name)
    {
        var value = Required(e, name);
        return value.ValueKind == JsonValueKind.String ? value.GetString()! : throw new ProtocolException($"field '{name}' is not a string");
    }

    public static string? OptionalString(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : throw new ProtocolException($"field '{name}' is not a string");
    }

    public static int RequiredInt(JsonElement e, string name)
    {
        var value = Required(e, name);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n) ? n : throw new ProtocolException($"field '{name}' is not an integer");
    }

    public static int? OptionalInt(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n) ? n : throw new ProtocolException($"field '{name}' is not an integer");
    }

    private static bool RequiredBool(JsonElement e, string name)
    {
        var value = Required(e, name);
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new ProtocolException($"field '{name}' is not true or false"),
        };
    }

    public static Coord ReadCoord(JsonElement e, string name)
    {
        var value = Required(e, name);
        return new Coord(RequiredInt(value, "x"), RequiredInt(value, "y"));
    }

    public static Coord? OptionalCoord(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? ReadCoord(e, name) : null;

    private static IEnumerable<JsonElement> Array(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Array ? e.EnumerateArray().ToList() : throw new ProtocolException($"field '{name}' is not an array");

    /// <summary>A unit's <c>weaponPoints</c> (issue 67); a state written before the field existed reads as rank E in everything.</summary>
    private static WeaponSkill ReadWeaponPoints(JsonElement e)
    {
        if (!e.TryGetProperty("weaponPoints", out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return WeaponSkill.Zero;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new ProtocolException("field 'weaponPoints' is not an object");
        }

        var skill = WeaponSkill.Zero;
        foreach (var property in value.EnumerateObject())
        {
            var points = RequiredInt(value, property.Name);
            if (points < 0)
            {
                throw new ProtocolException($"field 'weaponPoints.{property.Name}' is below 0");
            }

            skill = skill.With(ParseEnum<WeaponType>(property.Name, "weaponPoints"), points);
        }

        return skill;
    }

    /// <summary>A unit's <c>masteryPoints</c> (issue 69), class id to points; a state written before the field existed reads as none.</summary>
    private static MasteryProgress ReadMasteryPoints(JsonElement e)
    {
        if (!e.TryGetProperty("masteryPoints", out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return MasteryProgress.Empty;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new ProtocolException("field 'masteryPoints' is not an object");
        }

        var progress = MasteryProgress.Empty;
        foreach (var property in value.EnumerateObject())
        {
            var points = RequiredInt(value, property.Name);
            if (points < 0)
            {
                throw new ProtocolException($"field 'masteryPoints.{property.Name}' is below 0");
            }

            progress = progress.With(property.Name, points);
        }

        return progress;
    }

    private static ValueList<string> ReadStrings(JsonElement e, string name) =>
        ValueList<string>.From(Array(Required(e, name), name).Select(v => v.ValueKind == JsonValueKind.String ? v.GetString()! : throw new ProtocolException($"field '{name}' holds a value that is not a string")));
}

/// <summary>A protocol message that could not be read: bad JSON, or a field missing or of the wrong kind, named in the message.</summary>
public sealed class ProtocolException : Exception
{
    public ProtocolException(string message)
        : base(message)
    {
    }
}
