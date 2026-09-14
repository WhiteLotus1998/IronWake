using System.Text.Json;

namespace Ironwake.Content;

/// <summary>
/// A JSON object with the file and entry it came from, so every accessor can raise a
/// <see cref="ContentException"/> that names the exact field.
/// </summary>
internal readonly struct EntryNode
{
    public EntryNode(string file, string? entry, JsonElement element)
    {
        File = file;
        Entry = entry;
        Element = element;
    }

    public string File { get; }

    public string? Entry { get; }

    public JsonElement Element { get; }

    public EntryNode WithEntry(string entry) => new(File, entry, Element);

    public ContentException Error(string? field, string problem) => new(File, Entry, field, problem);

    public bool Has(string field) => Element.TryGetProperty(field, out _);

    public string String(string field)
    {
        var element = Require(field);
        if (element.ValueKind != JsonValueKind.String)
        {
            throw Error(field, "must be a string");
        }

        var value = element.GetString()!;
        if (value.Length == 0)
        {
            throw Error(field, "must not be empty");
        }

        return value;
    }

    public string? OptionalString(string field) => Has(field) ? String(field) : null;

    public int Int(string field)
    {
        var element = Require(field);
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var value))
        {
            throw Error(field, "must be an integer");
        }

        return value;
    }

    public int IntOr(string field, int fallback) => Has(field) ? Int(field) : fallback;

    /// <summary>An integer, or null when the JSON value is null. Used for impassable terrain.</summary>
    public int? NullableInt(string field)
    {
        var element = Require(field);
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return Int(field);
    }

    public bool BoolOr(string field, bool fallback)
    {
        if (!Has(field))
        {
            return fallback;
        }

        var element = Element.GetProperty(field);
        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw Error(field, "must be true or false"),
        };
    }

    public EntryNode Object(string field)
    {
        var element = Require(field);
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Error(field, "must be an object");
        }

        return new EntryNode(File, Entry, element);
    }

    public EntryNode? OptionalObject(string field) => Has(field) ? Object(field) : null;

    public IReadOnlyList<JsonElement> Array(string field)
    {
        var element = Require(field);
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw Error(field, "must be an array");
        }

        return element.EnumerateArray().ToList();
    }

    public IReadOnlyList<JsonElement> ArrayOrEmpty(string field) =>
        Has(field) ? Array(field) : System.Array.Empty<JsonElement>();

    public IReadOnlyList<string> StringArray(string field)
    {
        var result = new List<string>();
        foreach (var element in Array(field))
        {
            if (element.ValueKind != JsonValueKind.String || element.GetString()!.Length == 0)
            {
                throw Error(field, "must contain only non-empty strings");
            }

            result.Add(element.GetString()!);
        }

        return result;
    }

    public IReadOnlyList<string> StringArrayOrEmpty(string field) =>
        Has(field) ? StringArray(field) : System.Array.Empty<string>();

    /// <summary>Parses an enum from a lower-case JSON string, e.g. "infantry".</summary>
    public T Enum<T>(string field) where T : struct, System.Enum => ParseEnum<T>(field, String(field));

    public T ParseEnum<T>(string field, string value) where T : struct, System.Enum
    {
        if (System.Enum.TryParse<T>(value, ignoreCase: true, out var parsed) && System.Enum.IsDefined(parsed))
        {
            return parsed;
        }

        var allowed = string.Join(", ", System.Enum.GetNames<T>().Select(n => n.ToLowerInvariant()));
        throw Error(field, $"'{value}' is not one of: {allowed}");
    }

    private JsonElement Require(string field)
    {
        if (!Element.TryGetProperty(field, out var element))
        {
            throw Error(field, "is required");
        }

        return element;
    }
}
