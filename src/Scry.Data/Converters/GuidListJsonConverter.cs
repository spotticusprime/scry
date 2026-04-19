using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Scry.Data.Converters;

internal sealed class GuidListJsonConverter : ValueConverter<IReadOnlyList<Guid>?, string?>
{
    public GuidListJsonConverter()
        : base(
            v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
            v => string.IsNullOrEmpty(v) ? null : JsonSerializer.Deserialize<List<Guid>>(v, JsonOptions))
    {
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
}

internal sealed class GuidListValueComparer : ValueComparer<IReadOnlyList<Guid>?>
{
    public GuidListValueComparer()
        : base(
            (a, b) => ListsEqual(a, b),
            v => v == null ? 0 : v.Aggregate(0, (hash, id) => HashCode.Combine(hash, id.GetHashCode())),
            v => v == null ? null : (IReadOnlyList<Guid>)v.ToList())
    {
    }

    private static bool ListsEqual(IReadOnlyList<Guid>? a, IReadOnlyList<Guid>? b)
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
        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }
}
