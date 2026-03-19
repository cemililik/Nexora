using Nexora.SharedKernel.Domain.Exceptions;
using Nexora.SharedKernel.Domain.ValueObjects;

namespace Nexora.SharedKernel.Tests.Domain.ValueObjects;

public sealed class DateRangeTests
{
    [Fact]
    public void Create_ValidRange_ShouldSucceed()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddHours(2);

        var range = new DateRange(start, end);

        range.Start.Should().Be(start);
        range.End.Should().Be(end);
    }

    [Fact]
    public void Create_EndBeforeStart_ShouldThrow()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddHours(-1);

        var act = () => new DateRange(start, end);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_SameStartAndEnd_ShouldThrow()
    {
        var time = DateTimeOffset.UtcNow;

        var act = () => new DateRange(time, time);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Duration_ShouldReturnCorrectTimeSpan()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddHours(3);

        var range = new DateRange(start, end);

        range.Duration.Should().Be(TimeSpan.FromHours(3));
    }

    [Fact]
    public void Contains_PointInRange_ShouldBeTrue()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddHours(2);
        var range = new DateRange(start, end);

        range.Contains(start.AddHours(1)).Should().BeTrue();
    }

    [Fact]
    public void Contains_PointOutsideRange_ShouldBeFalse()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddHours(2);
        var range = new DateRange(start, end);

        range.Contains(start.AddHours(3)).Should().BeFalse();
    }

    [Fact]
    public void Overlaps_OverlappingRanges_ShouldBeTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var range1 = new DateRange(now, now.AddHours(2));
        var range2 = new DateRange(now.AddHours(1), now.AddHours(3));

        range1.Overlaps(range2).Should().BeTrue();
    }

    [Fact]
    public void Overlaps_NonOverlappingRanges_ShouldBeFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var range1 = new DateRange(now, now.AddHours(1));
        var range2 = new DateRange(now.AddHours(2), now.AddHours(3));

        range1.Overlaps(range2).Should().BeFalse();
    }
}
