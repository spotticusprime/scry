using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Scry.Data.Converters;

internal sealed class DictionaryJsonConverter : ValueConverter<Dictionary<string, string>, string>
{
    public DictionaryJsonConverter()
        : base(
            v => JsonSerializer.Serialize(v, JsonOptions),
            v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonOptions) ?? new())
    {
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
}

internal sealed class DictionaryValueComparer : ValueComparer<Dictionary<string, string>>
{
    public DictionaryValueComparer()
        : base(
            (a, b) => DictionariesEqual(a, b),
            v => v.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key.GetHashCode(StringComparison.Ordinal), pair.Value.GetHashCode(StringComparison.Ordinal))),
            v => new Dictionary<string, string>(v))
    {
    }

    private static bool DictionariesEqual(Dictionary<string, string>? a, Dictionary<string, string>? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }
        if (a is null || b is null)
        {
            return false;
        }
        if (a.Count != b.Count)
        {
            return false;
        }
        foreach (var pair in a)
        {
            if (!b.TryGetValue(pair.Key, out var other) || !string.Equals(other, pair.Value, StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }
}
