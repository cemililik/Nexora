using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.SharedKernel.Domain.ValueObjects;

/// <summary>
/// Value object representing a date/time range.
/// </summary>
public sealed record DateRange
{
    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }

    public DateRange(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
            throw new DomainException("lockey_shared_daterange_end_before_start");

        Start = start;
        End = end;
    }

    public TimeSpan Duration => End - Start;

    public bool Contains(DateTimeOffset point) => point >= Start && point <= End;

    public bool Overlaps(DateRange other) => Start < other.End && End > other.Start;
}
