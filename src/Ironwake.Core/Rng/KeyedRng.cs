using System.Text;

namespace Ironwake.Core;

/// <summary>
/// The production <see cref="IRng"/>: a roll is a hash of the seed and the key's text.
/// The hash is written out here (FNV-1a over the UTF-8 text, then a 64-bit finalizer) so
/// the same seed and key give the same roll on every platform and in every process;
/// <c>string.GetHashCode</c> is randomized per process and would break gate 6's replay.
/// </summary>
public sealed class KeyedRng : IRng
{
    private const ulong FnvOffset = 14695981039346656037;
    private const ulong FnvPrime = 1099511628211;

    public KeyedRng(ulong seed)
    {
        Seed = seed;
    }

    public ulong Seed { get; }

    public int Roll(RollKey key)
    {
        var hash = FnvOffset ^ Seed;
        foreach (var b in Encoding.UTF8.GetBytes(key.Text))
        {
            hash ^= b;
            hash *= FnvPrime;
        }

        hash ^= hash >> 30;
        hash *= 0xBF58476D1CE4E5B9;
        hash ^= hash >> 27;
        hash *= 0x94D049BB133111EB;
        hash ^= hash >> 31;
        return (int)(hash % 100);
    }
}
