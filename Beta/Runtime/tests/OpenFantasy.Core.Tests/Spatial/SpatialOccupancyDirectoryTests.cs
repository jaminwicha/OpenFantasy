using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Spatial;

public sealed class SpatialOccupancyDirectoryTests
{
    [Fact]
    public void Complete_catalog_is_order_independent_bounded_and_supplies_known_empty_pages()
    {
        var first = Actor(1, 128);
        var second = Actor(2, 8192);
        var index = SpatialOccupancyDirectory.Create(SpatialFixture.Scope, [first, second], 2, 16);
        var reversed = SpatialOccupancyDirectory.Create(SpatialFixture.Scope, [second, first], 2, 16);
        Assert.Equal(2, index.EntityCount);
        Assert.Equal(first, index.Find(first.EntityId));
        var key = SpatialOccupancyPageKey.At(first.Bounds.Minimum);
        Assert.Equal(index.ReadPage(key).ContentHash, reversed.ReadPage(key).ContentHash);
        Assert.Empty(index.ReadPage(new(100, 0, 0)).Entries);
        Assert.Throws<ArgumentException>(() => SpatialOccupancyDirectory.Create(SpatialFixture.Scope, [first, first], 2, 16));
        Assert.Throws<ArgumentException>(() => SpatialOccupancyDirectory.Create(SpatialFixture.Scope, [first, second], 1, 16));
        Assert.Throws<InvalidOperationException>(() => SpatialOccupancyDirectory.Create(SpatialFixture.Scope, [first, second], 2, 1));
        int enumerated = 0;
        IEnumerable<SpatialOccupancyReference> Endless()
        {
            while (true) { enumerated++; yield return Actor((ulong)enumerated, 128); }
        }
        Assert.Throws<ArgumentException>(() => SpatialOccupancyDirectory.Create(SpatialFixture.Scope, Endless(), 3, 16));
        Assert.Equal(4, enumerated);
    }

    [Fact]
    public void Crossing_updates_every_copy_and_keeps_old_snapshots_and_unrelated_pages_intact()
    {
        var before = Actor(1, 0);
        var after = Actor(1, -256);
        var distant = Actor(2, 8192);
        var index = SpatialOccupancyDirectory.Create(SpatialFixture.Scope, [before, distant], 3, 32);
        var oldPlan = new SpatialOccupancyReadPlan(SpatialFixture.Scope, before.Bounds, index.ReadPage, 8, 2, 0);
        var distantKey = SpatialOccupancyPageKey.At(distant.Bounds.Minimum);
        var next = index.Apply(0, [new(before, after)]);
        Assert.Equal(1UL, next.Revision);
        Assert.Equal(after, next.Find(before.EntityId));
        Assert.Equal(before, index.Find(before.EntityId));
        Assert.Same(index.ReadPage(distantKey), next.ReadPage(distantKey));
        Assert.Throws<InvalidOperationException>(() => oldPlan.ValidateCurrent(next.ReadPage));
        oldPlan.ValidateCurrent(index.ReadPage);
        var queried = new SpatialOccupancyReadPlan(SpatialFixture.Scope, after.Bounds, next.ReadPage, 8, 2, 0);
        Assert.Equal(after, Assert.Single(queried.Actors));
        Assert.Empty(next.ReadPage(new(0, 0, 0)).Entries);
        Assert.Equal(1UL, next.ReadPage(new(0, 0, 0)).Revision);
    }

    [Fact]
    public void Departure_and_identical_return_do_not_resurrect_an_old_empty_or_occupied_pin()
    {
        var actor = Actor(1, 128);
        var empty = SpatialOccupancyDirectory.Create(SpatialFixture.Scope, [], 2, 8);
        var initialRead = new SpatialOccupancyReadPlan(SpatialFixture.Scope, actor.Bounds, empty.ReadPage, 8, 2, 0);
        var arrived = empty.Apply(0, [new(null, actor)]);
        var occupiedRead = new SpatialOccupancyReadPlan(SpatialFixture.Scope, actor.Bounds, arrived.ReadPage, 8, 2, 0);
        var departed = arrived.Apply(1, [new(actor, null)]);
        var returned = departed.Apply(2, [new(null, actor)]);
        Assert.Null(departed.Find(actor.EntityId));
        Assert.Throws<InvalidOperationException>(() => initialRead.ValidateCurrent(departed.ReadPage));
        Assert.Throws<InvalidOperationException>(() => occupiedRead.ValidateCurrent(returned.ReadPage));
        Assert.Equal(actor, returned.Find(actor.EntityId));
        Assert.Equal(3UL, returned.Revision);
    }

    [Fact]
    public void Full_page_swaps_use_final_capacity_and_failures_cannot_publish_partial_state()
    {
        // Metadata density exercise, not an admitted overlapping physical population.
        var left = Enumerable.Range(1, SpatialOccupancyPage.MaximumEntries).Select(id => Actor((ulong)id, 128)).ToArray();
        var right = Actor(2000, 8320);
        var index = SpatialOccupancyDirectory.Create(SpatialFixture.Scope, left.Append(right), 1025, 16);
        var movedLeft = Actor(1, 8320);
        var movedRight = Actor(2000, 128);
        var a = index.Apply(0, [new(right, movedRight), new(left[0], movedLeft)]);
        var b = index.Apply(0, [new(left[0], movedLeft), new(right, movedRight)]);
        var leftKey = SpatialOccupancyPageKey.At(left[0].Bounds.Minimum);
        Assert.Equal(SpatialOccupancyPage.MaximumEntries, a.ReadPage(leftKey).Entries.Count);
        Assert.Equal(a.ReadPage(leftKey).ContentHash, b.ReadPage(leftKey).ContentHash);
        Assert.Throws<ArgumentException>(() => index.Apply(0, [new(right, movedRight)]));
        Assert.Equal(right, index.Find(right.EntityId));
        Assert.Equal(0UL, index.Revision);
        Assert.Equal(SpatialOccupancyPage.MaximumEntries, index.ReadPage(leftKey).Entries.Count);
    }

    [Fact]
    public void Stale_duplicate_scope_and_budget_changes_refuse_without_mutating_the_source()
    {
        var before = Actor(1, 128);
        var after = Actor(1, 8320);
        var index = SpatialOccupancyDirectory.Create(SpatialFixture.Scope, [before], 1, 1);
        Assert.Throws<InvalidOperationException>(() => index.Apply(1, [new(before, after)]));
        Assert.Throws<InvalidOperationException>(() => index.Apply(0, [new(after, before)]));
        Assert.Throws<InvalidOperationException>(() => index.Apply(0, [new(before, after)]));
        Assert.Throws<InvalidOperationException>(() => index.Apply(0, [new(null, Actor(2, 128))]));
        Assert.Throws<ArgumentException>(() => index.Apply(0, [new(before, after), new(before, after)]));
        Assert.Throws<ArgumentException>(() => index.Apply(0, []));
        Assert.Throws<ArgumentException>(() => new SpatialOccupancyChange(before, before));
        Assert.Throws<ArgumentException>(() => new SpatialOccupancyChange(before, Actor(2, 128)));
        var foreign = new SpatialOccupancyReference(new(SpatialFixture.Id(300), SpatialFixture.Id(301), SpatialFixture.Id(302)),
            before.Kind, before.EntityId, before.Bounds, before.ContentHash);
        Assert.Throws<InvalidOperationException>(() => index.Apply(0, [new(null, foreign)]));
        Assert.Equal(before, index.Find(before.EntityId));
        Assert.Equal(1, index.EntityCount);
        Assert.Equal(1, index.RetainedPageCount);
    }

    [Fact]
    public void Replaying_changes_reproduces_page_history_and_limits_input_enumeration()
    {
        var before = Actor(1, 128);
        var after = Actor(1, 8320);
        var one = SpatialOccupancyDirectory.Create(SpatialFixture.Scope, [before], 4, 32);
        var two = SpatialOccupancyDirectory.Create(SpatialFixture.Scope, [before], 4, 32);
        SpatialOccupancyChange[][] history = [[new(before, after)], [new(after, null)], [new(null, before)]];
        foreach (var changes in history)
        {
            one = one.Apply(one.Revision, changes);
            two = two.Apply(two.Revision, changes.Reverse());
            foreach (var key in new[] { new SpatialOccupancyPageKey(0, 0, 0), new(2, 0, 0) })
            {
                Assert.Equal(one.ReadPage(key).Revision, two.ReadPage(key).Revision);
                Assert.Equal(one.ReadPage(key).ContentHash, two.ReadPage(key).ContentHash);
            }
        }
        int count = 0;
        IEnumerable<SpatialOccupancyChange> Changes()
        {
            for (ulong id = 10; ; id++) { count++; yield return new(null, Actor(id, 128)); }
        }
        Assert.Throws<ArgumentException>(() => one.Apply(one.Revision, Changes()));
        Assert.Equal(SpatialOccupancyDirectory.MaximumChanges + 1, count);
    }

    private static SpatialOccupancyReference Actor(ulong id, long x) =>
        SpatialOccupancyReference.Capture(SpatialFixture.Actor(id + 10_000, new(x, 128, 256)));
}
