using System.Collections;
using System.Collections.Immutable;

namespace Ironwake.Core;

/// <summary>
/// An immutable list with structural equality. Records that hold collections use this
/// instead of <see cref="ImmutableArray{T}"/> so that two states built from the same
/// data compare equal, which the determinism and round-trip tests depend on.
/// </summary>
public readonly struct ValueList<T> : IReadOnlyList<T>, IEquatable<ValueList<T>>
{
    private readonly ImmutableArray<T> _items;

    private ValueList(ImmutableArray<T> items) => _items = items;

    public static ValueList<T> Empty { get; } = new(ImmutableArray<T>.Empty);

    public static ValueList<T> Of(params T[] items) => new(ImmutableArray.Create(items));

    public static ValueList<T> From(IEnumerable<T> items) => new(items.ToImmutableArray());

    /// <summary>A default-initialized value behaves as an empty list rather than throwing.</summary>
    private ImmutableArray<T> Items => _items.IsDefault ? ImmutableArray<T>.Empty : _items;

    public int Count => Items.Length;

    public T this[int index] => Items[index];

    public ValueList<T> Add(T item) => new(Items.Add(item));

    public ValueList<T> SetItem(int index, T item) => new(Items.SetItem(index, item));

    public ValueList<T> RemoveAt(int index) => new(Items.RemoveAt(index));

    public bool Contains(T item) => Items.Contains(item);

    public int IndexOf(T item) => Items.IndexOf(item);

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(ValueList<T> other)
    {
        var a = Items;
        var b = other.Items;
        if (a.Length != b.Length)
        {
            return false;
        }

        var comparer = EqualityComparer<T>.Default;
        for (var i = 0; i < a.Length; i++)
        {
            if (!comparer.Equals(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is ValueList<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Items.Length);
        foreach (var item in Items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(ValueList<T> left, ValueList<T> right) => left.Equals(right);

    public static bool operator !=(ValueList<T> left, ValueList<T> right) => !left.Equals(right);

    public override string ToString() => "[" + string.Join(", ", Items) + "]";
}
