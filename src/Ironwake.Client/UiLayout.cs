using Ironwake.Content;

namespace Ironwake.Client;

/// <summary>
/// The client's layout at a UI scale (issue 698): one factor for every size on screen. The window
/// draws a canvas of <see cref="ViewWidth"/> by <see cref="ViewHeight"/> scaled up by
/// <see cref="Factor"/> to fill the same 1280x720 window, so every type size, card and chip grows
/// by the factor, and the layout reflows into the smaller canvas: the column narrows and wraps,
/// the top bar and the key strip take a second row, and the board's tile shrinks to make room.
/// At 100 every number is the one the client drew before scale existed.
/// </summary>
public sealed record UiLayout
{
    /// <summary>The window's size in pixels at every scale, which the canvas is stretched to fill.</summary>
    public const int WindowWidth = 1280;

    /// <summary>The window's height in pixels at every scale.</summary>
    public const int WindowHeight = 720;

    /// <summary>The space between the window's edge and the board, the top bar and the key strip.</summary>
    public const int Margin = 16;

    /// <summary>The fixed gap between the board's right edge and the column (issue 513).</summary>
    public const int ColumnGap = 40;

    /// <summary>The room under the board for its legend, counted in the block that is centred on the screen.</summary>
    public const int LegendRoom = 76;

    /// <summary>The height one row of the top bar takes, chips and the space under them.</summary>
    public const int TopRowHeight = 34;

    /// <summary>The height one row of the key strip takes.</summary>
    public const int KeyRowHeight = 28;

    /// <summary>The column's slack: the forecast card's bleed past the column on its right.</summary>
    public const int ColumnBleed = 12;

    private UiLayout(int scale) => Scale = scale;

    /// <summary>The scale as the profile writes it: 100, 125 or 150.</summary>
    public int Scale { get; }

    /// <summary>How much larger everything is drawn than at 100.</summary>
    public float Factor => Scale / 100f;

    /// <summary>The layout at <paramref name="scale"/>, one of <see cref="Options.UiScales"/>.</summary>
    public static UiLayout For(int scale)
    {
        if (!Options.UiScales.Contains(scale))
        {
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "UI scale must be one of " + string.Join(", ", Options.UiScales));
        }

        return new UiLayout(scale);
    }

    /// <summary>The canvas's width: the window's over the factor, rounded down so the canvas never overruns it.</summary>
    public int ViewWidth => WindowWidth * 100 / Scale;

    /// <summary>The canvas's height, rounded down.</summary>
    public int ViewHeight => WindowHeight * 100 / Scale;

    /// <summary>The column's width: 500 at 100, narrower as the canvas narrows so the board keeps a playable tile.</summary>
    public int PanelWidth => Scale switch { 100 => 500, 125 => 420, _ => 360 };

    /// <summary>
    /// The top bar's rows: one where the name, the chips, the speed buttons and the scenes chip
    /// fit across, else two, the speed buttons and the scenes chip on the second.
    /// </summary>
    public int TopRows => ViewWidth >= WindowWidth ? 1 : 2;

    /// <summary>Where the board's block starts, under the top bar.</summary>
    public int Top => 48 + (TopRows - 1) * TopRowHeight;

    /// <summary>The key strip's rows: one at 100, else two (held by test against the strip's longest wording).</summary>
    public int KeyRows => ViewWidth >= WindowWidth ? 1 : 2;

    /// <summary>The lowest baseline the key strip leaves free.</summary>
    public int FooterTop => ViewHeight - 40 - (KeyRows - 1) * KeyRowHeight;

    /// <summary>
    /// The tile for a board <paramref name="width"/> by <paramref name="height"/> (issue 513): as
    /// large as fills the height the block allows under the top bar and above the legend and the
    /// key strip, and never so wide the column does not fit beside it.
    /// </summary>
    public int TileFor(int width, int height) =>
        Math.Min((ViewWidth - PanelWidth - ColumnGap - 2 * Margin - ColumnBleed) / width, (FooterTop - Top - LegendRoom - 8) / height);

    /// <summary>The width the board, the gap and the column take together at <paramref name="tile"/>.</summary>
    public int BlockWidth(int width, int tile) => width * tile + ColumnGap + PanelWidth + ColumnBleed;

    /// <summary>
    /// The column's lowest baseline (issue 512): level with the legend's foot under the board, but
    /// never so short the forecast card and <paramref name="minimum"/> below its top do not fit,
    /// and never into the key strip. At 100 the board is tall enough that the legend decides.
    /// </summary>
    public float ColumnBottom(float boardFoot, float columnTop, float minimum) =>
        Math.Max(boardFoot + LegendRoom, Math.Min(FooterTop - 8, columnTop + minimum));

    /// <summary>
    /// The rows a strip of items <paramref name="widths"/> wide wraps into across
    /// <paramref name="available"/>, <paramref name="gap"/> apart: each item's row, in order, a
    /// new row starting where the next item would overrun. An item wider than the strip has a row
    /// to itself.
    /// </summary>
    public static IReadOnlyList<int> Wrap(IReadOnlyList<float> widths, float available, float gap)
    {
        var rows = new int[widths.Count];
        var (row, used) = (0, 0f);
        for (var i = 0; i < widths.Count; i++)
        {
            var need = used == 0 ? widths[i] : used + gap + widths[i];
            if (used > 0 && need > available)
            {
                (row, need) = (row + 1, widths[i]);
            }

            rows[i] = row;
            used = need;
        }

        return rows;
    }
}
