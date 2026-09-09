using OpenFantasy.Core.Commands;
using OpenFantasy.Core.History;

namespace OpenFantasy.Core.Tests.History;

public sealed class HistoryBoundaryTests
{
    [Fact]
    public void InclusiveWindowSupportsExactMillisecondsAndExtremeDays()
    {
        var earliest = new WorldInstant(long.MinValue, 0);
        var latest = new WorldInstant(long.MaxValue, WorldInstant.MillisecondsPerDay - 1);
        var atOrigin = new HistoryBoundary(earliest, latest, earliest, ulong.MaxValue);
        var atHead = new HistoryBoundary(earliest, latest, latest, ulong.MaxValue);
        Assert.Equal(earliest, atOrigin.RequestedInstant);
        Assert.Equal(latest, atHead.RequestedInstant);
        Assert.Equal(ulong.MaxValue, atHead.EffectiveCommitBoundary);
    }

    [Fact]
    public void ImpossibleWindowAndRequestsOutsideItReject()
    {
        var origin = new WorldInstant(1, 0);
        var head = new WorldInstant(2, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => new HistoryBoundary(head, origin, origin, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HistoryBoundary(origin, head, origin.AddMilliseconds(-1), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HistoryBoundary(origin, head, head.AddMilliseconds(1), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HistoryBoundary(origin, head, origin, 1, 2));
    }

    [Fact]
    public void CursorSelectsACommitBoundaryWithoutMovingTheCalendarDate()
    {
        var instant = new WorldInstant(-1, WorldInstant.MillisecondsPerDay - 1);
        WorldInstant origin = instant.AddMilliseconds(-1);
        var complete = new HistoryBoundary(origin, instant, instant, 17);
        var earlier = new HistoryBoundary(origin, instant, instant, 17, 9);
        var empty = new HistoryBoundary(instant, instant, instant, 17, 0);
        Assert.Equal(complete.RequestedInstant, earlier.RequestedInstant);
        Assert.True(complete.Includes(instant, 17));
        Assert.False(complete.Includes(instant, 18));
        Assert.True(earlier.Includes(instant, 9));
        Assert.False(earlier.Includes(instant, 10));
        Assert.False(earlier.Includes(instant.AddMilliseconds(1), 8));
        Assert.True(earlier.Includes(instant.AddMilliseconds(-1), 8));
        Assert.False(earlier.Includes(origin.AddMilliseconds(-1), 8));
        Assert.False(empty.Includes(instant, 1));
        Assert.False(empty.Includes(instant, 0));
        Assert.Equal(0UL, new HistoryBoundary(instant, instant, instant, 0).EffectiveCommitBoundary);
    }

    [Fact]
    public void WorldMismatchRejectsMembershipWithoutUsingPresentationLocation()
    {
        var address = new HistoryAddress(new EntityId(0, 1), new EntityId(0, 2),
            new EntityId(0, 3), long.MinValue, 0, long.MaxValue);
        var boundary = new HistoryBoundary(default, default, default, 4);
        var request = new HistoryRequest(address, boundary);
        var member = new HistoryCommitStamp(new CommandCommitIdentity(address.WorldId, 4), default);
        var foreign = new HistoryCommitStamp(new CommandCommitIdentity(new EntityId(0, 99), 4), default);
        Assert.True(request.Includes(member));
        Assert.False(request.Includes(foreign));
        Assert.Equal(address, request.Address);
        Assert.Same(boundary, request.Boundary);
        Assert.Throws<ArgumentNullException>(() => new HistoryRequest(address, null!));
        Assert.Throws<ArgumentNullException>(() => request.Includes(null!));
        Assert.Throws<ArgumentNullException>(() => new HistoryCommitStamp(null!, default));
    }
}
