using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Scry.Data.Converters;

// SQLite has no native DateTimeOffset type. Storing as UTC ticks (long/INTEGER) makes
// SQL comparison operators work correctly, which TEXT storage does not support.
internal sealed class DateTimeOffsetToTicksConverter : ValueConverter<DateTimeOffset, long>
{
    public DateTimeOffsetToTicksConverter() : base(
        v => v.UtcTicks,
        v => new DateTimeOffset(v, TimeSpan.Zero))
    {
    }
}

internal sealed class NullableDateTimeOffsetToTicksConverter : ValueConverter<DateTimeOffset?, long?>
{
    public NullableDateTimeOffsetToTicksConverter() : base(
        v => v.HasValue ? (long?)v.Value.UtcTicks : null,
        v => v.HasValue ? (DateTimeOffset?)new DateTimeOffset(v.Value, TimeSpan.Zero) : null)
    {
    }
}
