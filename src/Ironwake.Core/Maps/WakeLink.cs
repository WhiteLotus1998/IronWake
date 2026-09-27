namespace Ironwake.Core;

/// <summary>
/// One pair of the <c>wake_links:</c> header (issue 393, DESIGN.md sections 8 and 10): when
/// group <paramref name="From"/> wakes, sleeping group <paramref name="To"/> wakes with it,
/// in the same wake check, with the cause <see cref="WakeCause.Call"/>.
/// </summary>
public sealed record WakeLink(string From, string To)
{
    /// <summary>The pair as the header writes it, <c>from&gt;to</c>.</summary>
    public override string ToString() => $"{From}>{To}";
}
