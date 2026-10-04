namespace Ironwake.Core;

/// <summary>The way the wind blows toward on a <c>wind:</c> map (DESIGN.md 13.28): <c>East</c> blows from the west.</summary>
public enum WindDirection
{
    North,
    East,
    South,
    West,
}

/// <summary>From <paramref name="Turn"/> on, the wind blows toward <paramref name="To"/>.</summary>
public readonly record struct WindShift(int Turn, WindDirection To);

/// <summary>
/// The <c>wind:</c> header (DESIGN.md 13.28, experiment, samples): the way the wind blows on
/// turn 1 and the announced turns it turns, in order. <see cref="Wind"/> reads it.
/// </summary>
public sealed record WindRule(WindDirection Start, ValueList<WindShift> Shifts)
{
    /// <summary>The way the wind blows on <paramref name="turn"/>: the last shift at or before it, else the start.</summary>
    public WindDirection On(int turn)
    {
        var way = Start;
        foreach (var shift in Shifts)
        {
            if (shift.Turn <= turn)
            {
                way = shift.To;
            }
        }

        return way;
    }

    /// <summary>The header's value, as <c>MapFormat.Write</c> writes it: <c>east; turn 4 north</c>.</summary>
    public override string ToString() =>
        string.Join("; ", new[] { Wind.Word(Start) }.Concat(Shifts.Select(s => $"turn {s.Turn} {Wind.Word(s.To)}")));
}
