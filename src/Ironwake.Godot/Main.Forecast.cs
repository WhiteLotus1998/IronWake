using Godot;
using Ironwake.Client;
using Ironwake.Core;
using CoreSide = Ironwake.Core.Side;

namespace Ironwake.Godot;

/// <summary>
/// Showcase slice 2 (issue 512, <c>docs/look/forecast.svg</c>): the forecast drawn as the screen's
/// centrepiece and the unit card. Both sides sit at the same weight, each with the source of its
/// avoid beside its tile; each HP bar shows three states, what is left, what this strike can cost
/// (hatched), and what was already lost; hit, damage and crit in big numerals, a strike as a pip
/// and a missing counter as an empty slot. Every number is a field of <see cref="ForecastCard"/>,
/// the <see cref="CombatForecast"/> the console's line prints; nothing here computes one.
/// </summary>
public partial class Main
{
    /// <summary>The drawn forecast's height, from the card's top edge to its bottom.</summary>
    private const float ForecastHeight = 236;

    /// <summary>The drawn unit card's height.</summary>
    private const float UnitCardHeight = 128;

    /// <summary>
    /// Draws the card for <paramref name="card"/> with its top edge at <paramref name="top"/>, and
    /// returns the card's bottom edge. Positions follow <c>forecast.svg</c>, whose content runs
    /// 500 pixels across, the column's width.
    /// </summary>
    private float DrawForecastCard(float top, ForecastCard card)
    {
        var x0 = PanelOrigin.X;
        var right = x0 + PanelWidth;
        Vector2 P(float x, float y) => new(x0 + x, top + y);
        Card(new Rect2(x0 - 12, top, PanelWidth + 24, ForecastHeight), Box, 10);
        DrawString(_caps, P(8, 24), "FORECAST", fontSize: 11, modulate: Muted);
        if (card.LevelUpLine is { } levelUp)
        {
            // A strike that would carry the unit across a level says so beside the title (issue 533).
            UiText(P(20 + _caps.GetStringSize("FORECAST", fontSize: 11).X, 24), levelUp, Look(LookPalette.Player), 12, bold: true);
        }

        UiText(P(PanelWidth - 8 - UiWidth(card.Heading, 12), 24), card.Heading, Muted, 12);

        // Who, with what, from where: the source of avoid beside each tile.
        DrawDisc(P(34, 62), 20, card.Attacker.Id);
        UiText(P(66, 56), card.Attacker.Name, Ink, 18, bold: true);
        UiText(P(66, 76), $"{card.Attacker.Weapon}  |  {card.Attacker.Ground}", Muted, 12);
        DrawDisc(P(PanelWidth - 34, 62), 20, card.Defender.Id);
        RightText(P(PanelWidth - 66, 56), card.Defender.Name, Ink, 18, bold: true);
        RightText(P(PanelWidth - 66, 76), $"{card.Defender.Weapon}  |  {card.Defender.Ground}", Muted, 12);

        // Both outcomes at one weight (round 131): the counter is what this game prices.
        var barWidth = (PanelWidth - 36) / 2f;
        HpBar(new Rect2(P(8, 94), new Vector2(barWidth, 14)), card.Attacker, Look(LookPalette.Player));
        HpBar(new Rect2(P(PanelWidth - 8 - barWidth, 94), new Vector2(barWidth, 14)), card.Defender, Look(LookPalette.EnemyBone));
        var ours = $"{card.Attacker.Hp} → {card.Attacker.After}";
        UiText(P(8, 128), ours, Ink, 15, bold: true);
        UiText(P(8 + UiWidth(ours, 15, bold: true) + 8, 128), card.Defender.Strike.Strikes ? "if countered" : "no counter", Muted, 12);
        var theirs = $"{card.Defender.Hp} → {card.Defender.After}";
        RightText(P(PanelWidth - 8, 128), theirs, Ink, 15, bold: true);
        RightText(P(PanelWidth - 16 - UiWidth(theirs, 15, bold: true), 128), "if all land", Muted, 12);

        // The numerals: ours in amber left of each rule, theirs in bone right of it.
        var a = card.Attacker.Strike;
        var d = card.Defender.Strike;
        var hit = card.Raises ? "--" : a.DisplayedHit.ToString();
        var crit = card.Raises ? "--" : a.CritChance.ToString();
        Numerals(P(PanelWidth * 0.15f, 0), top, "HIT", hit, d.Strikes ? d.DisplayedHit.ToString() : null);
        Numerals(P(PanelWidth * 0.5f, 0), top, "DMG", a.Damage.ToString(), d.Strikes ? d.Damage.ToString() : null);
        Numerals(P(PanelWidth * 0.85f, 0), top, "CRIT", crit, d.Strikes ? d.CritChance.ToString() : null);

        DrawLine(P(8, 200), P(PanelWidth - 8, 200), Rule, 1);
        UiText(P(8, 222), "strikes", Muted, 12);
        Pips(P(72, 218), a.Strikes ? a.StrikeCount : 0, Look(LookPalette.Player));
        UiText(P(PanelWidth * 0.52f, 222), "counter", Muted, 12);
        Pips(P(PanelWidth * 0.52f + 64, 218), d.Strikes ? d.StrikeCount : 0, Look(LookPalette.EnemyBone));
        RightText(P(PanelWidth - 8, 222), card.Doubling, Muted, 12);
        return top + ForecastHeight;
    }

    /// <summary>
    /// One numeral column centred on <paramref name="centre"/>'s x: the label in spaced capitals,
    /// ours right-aligned in amber, a hairline, and theirs in bone, or an empty slot when they
    /// cannot strike back.
    /// </summary>
    private void Numerals(Vector2 centre, float top, string label, string ours, string? theirs)
    {
        var x = centre.X;
        var labelWidth = _caps.GetStringSize(label, fontSize: 11).X;
        DrawString(_caps, new Vector2(x - labelWidth / 2, top + 150), label, fontSize: 11, modulate: Muted);
        RightText(new Vector2(x - 10, top + 188), ours, Look(LookPalette.Player), 38, bold: true);
        DrawLine(new Vector2(x, top + 160), new Vector2(x, top + 188), Rule, 1);
        if (theirs is null)
        {
            DrawRect(new Rect2(x + 12, top + 162, 30, 26), Rule, filled: false, width: 1.5f);
            return;
        }

        UiText(new Vector2(x + 10, top + 188), theirs, Look(LookPalette.EnemyBone), 38, bold: true);
    }

    /// <summary>A side's strikes as pips: one filled per strike, up to two slots with the empty ones ringed.</summary>
    private void Pips(Vector2 at, int strikes, Color colour)
    {
        for (var i = 0; i < Math.Max(2, strikes); i++)
        {
            var c = at + new Vector2(i * 16, 0);
            if (i < strikes)
            {
                DrawCircle(c, 6, colour);
            }
            else
            {
                DrawArc(c, 5.5f, 0, Mathf.Tau, 24, Rule, 1.5f, antialiased: true);
            }
        }
    }

    /// <summary>
    /// A side's HP bar in three states (round 131): what it keeps if every strike lands in the
    /// side's colour, this strike's cost faded under an ink hatch, and HP already lost before the
    /// strike in <c>ui.lost</c>.
    /// </summary>
    private void HpBar(Rect2 r, ForecastSide side, Color colour) => HpBar(r, side.Hp, side.After, side.MaxHp, colour);

    private void HpBar(Rect2 r, int hp, int after, int max, Color colour)
    {
        var w = r.Size.X / Math.Max(1, max);
        Card(r, UiColour("ink"), 3);
        var lost = new Rect2(r.Position + new Vector2(hp * w, 0), new Vector2((max - hp) * w, r.Size.Y));
        if (lost.Size.X > 0)
        {
            DrawRect(lost, UiColour("lost"));
        }

        if (after > 0)
        {
            DrawRect(new Rect2(r.Position, new Vector2(after * w, r.Size.Y)), colour);
        }

        var cost = new Rect2(r.Position + new Vector2(after * w, 0), new Vector2((hp - after) * w, r.Size.Y));
        if (cost.Size.X > 0)
        {
            DrawRect(cost, new Color(colour, 0.35f));
            Hatch(cost, UiColour("ink", 0.6f), 1.5f, 6);
        }
    }

    /// <summary>A unit's disc and class silhouette at a card's scale, by its id; nothing when it has left the board.</summary>
    private void DrawDisc(Vector2 centre, float radius, string id) => DrawUnitDisc(centre, radius, _client!.State.Find(id));

    /// <summary>A unit's disc and class silhouette at a card's scale; nothing for no unit.</summary>
    private void DrawUnitDisc(Vector2 centre, float radius, BattleUnit? unit)
    {
        var state = _client!.State;
        if (unit is null)
        {
            return;
        }

        var player = unit.Side == CoreSide.Player;
        var ink = UiColour("ink");
        DrawSetTransform(centre + new Vector2(0, 3), 0, new Vector2(1, (radius - 1) / radius));
        DrawCircle(Vector2.Zero, radius, player ? Look(LookPalette.PlayerDeep) : new Color(ink, 0.55f));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        DrawCircle(centre, radius, player ? Look(LookPalette.Player) : Look(LookPalette.Enemy));
        if (!player)
        {
            DrawArc(centre, radius - 1.5f, 0, Mathf.Tau, 48, Look(LookPalette.EnemyBone), 0.75f, antialiased: true);
        }

        DrawSilhouette(state, unit, centre, radius / 16f, player ? ink : Look(LookPalette.EnemyBone));
    }

    /// <summary>
    /// The unit card (issue 512): the silhouette on its disc, the name, class and level, where it
    /// stands, the HP bar and a player unit's EXP bar beside it (issue 533), the stats as a row of labelled numerals, and the weapon as the console's
    /// <c>show</c> prints it. Returns the card's bottom edge.
    /// </summary>
    private float DrawUnitCard(float top, UnitCard card)
    {
        var x0 = PanelOrigin.X;
        Vector2 P(float x, float y) => new(x0 + x, top + y);
        Card(new Rect2(x0 - 12, top, PanelWidth + 24, UnitCardHeight), Box, 10);
        DrawDisc(P(30, 34), 18, card.Id);
        UiText(P(60, 30), card.Name, Ink, 17, bold: true);
        var sub = $"{card.ClassName} L{card.Level}  |  {card.Terrain} {card.At.X},{card.At.Y}";
        UiText(P(60 + UiWidth(card.Name, 17, bold: true) + 10, 30), sub, Muted, 12);
        var colour = card.Side == CoreSide.Player ? Look(LookPalette.Player) : Look(LookPalette.EnemyBone);
        HpBar(new Rect2(P(60, 40), new Vector2(160, 10)), card.Hp, card.Hp, card.MaxHp, colour);
        UiText(P(228, 50), $"{card.Hp} / {card.MaxHp}", Ink, 13, bold: true);
        if (card.Exp is { } exp)
        {
            // The EXP bar beside HP (issue 533): a thin rule filling toward the next level, the number after it.
            DrawString(_caps, P(318, 50), "EXP", fontSize: 9, modulate: Muted);
            var bar = new Rect2(P(346, 42), new Vector2(110, 6));
            Card(bar, UiColour("ink"), 3);
            if (exp > 0)
            {
                Card(new Rect2(bar.Position, new Vector2(bar.Size.X * exp / Experience.LevelUpAt, bar.Size.Y)), new Color(colour, 0.8f), 3);
            }

            RightText(P(PanelWidth - 8, 50), exp.ToString(), Ink, 13, bold: true);
        }
        var s = card.Stats;
        var columns = new (string Label, int Value)[]
        {
            ("STR", s.Str), ("MAG", s.Mag), ("DEX", s.Dex), ("SPD", s.Spd), ("LCK", s.Lck), ("DEF", s.Def), ("RES", s.Res), ("CHA", s.Cha), ("MOV", card.Mov),
        };
        var step = (PanelWidth - 16) / (float)columns.Length;
        for (var i = 0; i < columns.Length; i++)
        {
            var cx = 8 + step * (i + 0.5f);
            var (label, value) = columns[i];
            DrawString(_caps, P(cx - _caps.GetStringSize(label, fontSize: 9).X / 2, 74), label, fontSize: 9, modulate: Muted);
            UiText(P(cx, 94), value.ToString(), Ink, 16, bold: true, centred: true);
        }

        var line = TextLayout.Wrap(card.WeaponLine, Columns).First();
        Text(P(8, 118), line, Muted, 13);
        return top + UnitCardHeight;
    }

    /// <summary>Text in the UI face with its right end at <paramref name="at"/>.</summary>
    private void RightText(Vector2 at, string text, Color colour, int size, bool bold = false) =>
        UiText(at - new Vector2(UiWidth(text, size, bold), 0), text, colour, size, bold);
}
