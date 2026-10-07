namespace Ironwake.Core;

/// <summary>
/// One entry of the <c>wake_on_death:</c> header (issue 1264, DESIGN.md sections 8 and 10): the
/// Guard group <paramref name="Group"/> sleeps through proximity and noise, and wakes only when a
/// member of <paramref name="Group"/> or of <paramref name="By"/> dies, with the cause
/// <see cref="WakeCause.Death"/> and <paramref name="By"/> as its caller when the death was there.
/// </summary>
public sealed record DeathWake(string Group, string By)
{
    /// <summary>The entry as the header writes it, <c>group by other</c>.</summary>
    public override string ToString() => $"{Group} by {By}";
}
