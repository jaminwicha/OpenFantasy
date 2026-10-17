using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Spatial;

public sealed class SpatialOccupancyReadPlanTests
{
    [Theory]
    [InlineData(-17, -2)]
    [InlineData(-16, -1)]
    [InlineData(-1, -1)]
    [InlineData(0, 0)]
    [InlineData(15, 0)]
    [InlineData(16, 1)]
    public void Page_addresses_floor_signed_cells(long cell, long page)
    {
        var key = SpatialOccupancyPageKey.At(new(cell, cell, cell));
        Assert.Equal(new SpatialOccupancyPageKey(page, page, page), key);
        Assert.True(key.Bounds.Contains(new(cell, cell, cell)));
    }

    [Fact]
    public void Empty_pages_are_required_and_later_arrivals_invalidate_their_pins()
    {
        var bounds = new TerrainRegionBounds(new(0, 0, 0), 1, 1, 1);
        var key = SpatialOccupancyPageKey.At(bounds.Minimum);
        var empty = new SpatialOccupancyPage(SpatialFixture.Scope, key, 0, []);
        var plan = new SpatialOccupancyReadPlan(SpatialFixture.Scope, bounds, _ => empty, 1, 1, 0);
        Assert.Empty(plan.Actors);
        Assert.Single(plan.Pages);
        Assert.Throws<InvalidOperationException>(() => new SpatialOccupancyReadPlan(SpatialFixture.Scope, bounds, _ => null, 1, 1, 0));
        Assert.Throws<InvalidOperationException>(() => plan.ValidateCurrent(_ => null));
        var actor = SpatialFixture.Actor(1, new(128, 128, 0));
        var arrived = new SpatialOccupancyPage(SpatialFixture.Scope, key, 1, [SpatialOccupancyReference.Capture(actor)]);
        Assert.Throws<InvalidOperationException>(() => plan.ValidateCurrent(_ => arrived));
        var newPlan = new SpatialOccupancyReadPlan(SpatialFixture.Scope, bounds, _ => arrived, 1, 1, 0);
        Assert.Throws<InvalidOperationException>(() => newPlan.ValidateCurrent(_ => empty));
        plan.ValidateCurrent(_ => new(SpatialFixture.Scope, key, 0, []));
    }

    [Fact]
    public void All_queried_copies_must_agree_and_no_body_can_be_omitted_to_meet_a_budget()
    {
        var actor = SpatialFixture.Actor(1, new(0, 128, 256));
        var reference = SpatialOccupancyReference.Capture(actor);
        var bounds = new TerrainRegionBounds(new(-1, 0, 1), 2, 1, 1);
        var pages = Pages(bounds, [reference]);
        var plan = new SpatialOccupancyReadPlan(SpatialFixture.Scope, bounds, key => pages[key], 2, 1, 0);
        Assert.Single(plan.Actors);
        Assert.Equal(2, plan.Pages.Count);
        var left = SpatialOccupancyPageKey.At(bounds.Minimum);
        var omitted = new Dictionary<SpatialOccupancyPageKey, SpatialOccupancyPage>(pages)
        { [left] = new(SpatialFixture.Scope, left, 1, []) };
        Assert.Throws<InvalidOperationException>(() => new SpatialOccupancyReadPlan(SpatialFixture.Scope, bounds, key => omitted[key], 2, 1, 0));
        var forged = new SpatialOccupancyReference(reference.Scope, reference.Kind, reference.EntityId,
            reference.Bounds, ContentHash.Compute("different-body"u8));
        omitted[left] = new(SpatialFixture.Scope, left, 1, [forged]);
        Assert.Throws<InvalidOperationException>(() => new SpatialOccupancyReadPlan(SpatialFixture.Scope, bounds, key => omitted[key], 2, 1, 0));
        int reads = 0;
        Assert.Throws<InvalidOperationException>(() => new SpatialOccupancyReadPlan(SpatialFixture.Scope, bounds,
            key => { reads++; return pages[key]; }, 1, 1, 0));
        Assert.Equal(0, reads);
        var second = SpatialOccupancyReference.Capture(SpatialFixture.Actor(2, new(16, 128, 256)));
        var dense = Pages(bounds, [reference, second]);
        Assert.Throws<InvalidOperationException>(() => new SpatialOccupancyReadPlan(SpatialFixture.Scope, bounds, key => dense[key], 2, 1, 0));
        Assert.Throws<ArgumentException>(() => new SpatialOccupancyPage(SpatialFixture.Scope, left, 0, [reference, reference]));
    }

    [Fact]
    public void Actor_archives_bind_scope_identity_geometry_and_exact_bytes()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var reference = SpatialOccupancyReference.Capture(actor);
        byte[] bytes = SpatialActorArchiveV1.Encode(actor);
        var decoded = SpatialActorArchiveV1.Decode(bytes, reference);
        Assert.Equal(reference, SpatialOccupancyReference.Capture(decoded));
        Assert.Equal(bytes, SpatialActorArchiveV1.Encode(decoded));
        foreach (var wrong in new[] {
            new SpatialOccupancyReference(reference.Scope, reference.Kind, SpatialFixture.Id(999), reference.Bounds, reference.ContentHash),
            new SpatialOccupancyReference(reference.Scope, reference.Kind, reference.EntityId, new(new(0, 0, 0), 1, 1, 1), reference.ContentHash),
            new SpatialOccupancyReference(new(SpatialFixture.Id(900), reference.Scope.BranchId, reference.Scope.RealmId), reference.Kind, reference.EntityId, reference.Bounds, reference.ContentHash)
        }) Assert.Throws<FormatException>(() => SpatialActorArchiveV1.Decode(bytes, wrong));
        Assert.Throws<FormatException>(() => SpatialActorArchiveV1.Decode(bytes[..^1], reference));
        Assert.Throws<FormatException>(() => SpatialActorArchiveV1.Decode(new byte[SpatialActorArchiveV1.MaximumByteLength + 1], reference));
    }

    [Fact]
    public void Resolved_directory_uses_actual_colliders_and_rejects_missing_or_substituted_detail()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var blocker = SpatialFixture.Actor(2, new(512, 128, 256));
        var scene = new LocalSpatialSnapshot(SpatialFixture.Scope, [SpatialFixture.Terrain()], [actor, blocker]);
        var footprint = MovementTerrainFootprint.Create(actor, scene.GroundSupport, 256, 0);
        var pages = Pages(footprint.Bounds, scene.Actors.Select(SpatialOccupancyReference.Capture));
        var plan = new SpatialOccupancyReadPlan(scene.Scope, footprint.Bounds, key => pages[key], 8, 2, 0);
        var terrain = new TerrainReadPlan(scene.Scope, footprint.Bounds, scene.Regions.Select(TerrainRegionReference.Capture), 1);
        Assert.Throws<InvalidOperationException>(() => plan.Resolve([actor], []));
        Assert.Throws<ArgumentException>(() => plan.Resolve([actor, actor], []));
        var changed = new ActorPresence(blocker.Binding, blocker.Condition, blocker.Scope, blocker.Feet.Offset(1, 0, 0), blocker.Revision, blocker.HomePlaceId);
        Assert.Throws<ArgumentException>(() => plan.Resolve([actor, changed], []));
        var prepared = SpatialMovementDependencies.FromDirectory(footprint, terrain, plan.Resolve([actor, blocker], []), scene.StructureSupport);
        Assert.Equal(scene.PrepareMove(actor.ActorId, actor.Revision, 256, 0).Status, prepared.Evaluate(terrain.Resolve(scene.Regions)).Status);
        Assert.Equal(SpatialMoveStatus.BlockedActor, prepared.Evaluate(terrain.Resolve(scene.Regions)).Status);
        prepared.ValidateCurrent(scene);
        prepared.ValidateCurrentDirectory(key => pages[key], scene.Regions.Select(TerrainRegionReference.Capture), scene.GroundSupport, scene.StructureSupport);
        var other = MovementTerrainFootprint.Create(actor, scene.GroundSupport, -32, 0);
        Assert.Throws<ArgumentException>(() => SpatialMovementDependencies.FromDirectory(other, terrain, plan.Resolve([actor, blocker], []), scene.StructureSupport));
    }

    [Fact]
    public void Metadata_limits_and_wrong_page_scope_fail_without_unbounded_enumeration()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var reference = SpatialOccupancyReference.Capture(actor);
        var key = SpatialOccupancyPageKey.At(reference.Bounds.Minimum);
        int examined = 0;
        IEnumerable<SpatialOccupancyReference> TooMany()
        {
            for (ulong index = 0; ; index++)
            {
                examined++;
                Assert.True(examined <= SpatialOccupancyPage.MaximumEntries + 1);
                yield return new(reference.Scope, reference.Kind, SpatialFixture.Id(10000 + index), reference.Bounds, reference.ContentHash);
            }
        }
        Assert.Throws<ArgumentException>(() => new SpatialOccupancyPage(reference.Scope, key, 0, TooMany()));
        Assert.Equal(SpatialOccupancyPage.MaximumEntries + 1, examined);
        var foreign = new TerrainScope(SpatialFixture.Id(900), reference.Scope.BranchId, reference.Scope.RealmId);
        Assert.Throws<ArgumentException>(() => new SpatialOccupancyReadPlan(reference.Scope, reference.Bounds,
            requested => new(foreign, requested, 0, []), 8, 1, 0));
        Assert.Throws<ArgumentException>(() => new SpatialOccupancyReadPlan(reference.Scope, reference.Bounds,
            requested => new(reference.Scope, new(requested.X + 1, requested.Y, requested.Z), 0, []), 8, 1, 0));
        Assert.Throws<OverflowException>(() => new SpatialOccupancyPage(reference.Scope, new(long.MaxValue, 0, 0), 0, []));
        Assert.True(SpatialOccupancyPageKey.At(new(long.MinValue, long.MaxValue, 0)).Bounds.Contains(new(long.MinValue, long.MaxValue, 0)));
    }

    [Fact]
    public void Structure_envelopes_load_conservatively_but_actual_boxes_and_bearings_remain_pinned()
    {
        var actor = SpatialFixture.Actor(1, new(160, 128, 256));
        var scene = StairWalkingTests.Scene(actor, StairWalkingTests.Tread(1, 256, 480, 320));
        var footprint = MovementTerrainFootprint.Create(actor, scene.GroundSupport, 32, 0);
        var references = scene.Actors.Select(SpatialOccupancyReference.Capture)
            .Concat(scene.Structures.Select(structure => SpatialOccupancyReference.Capture(scene.Scope, structure)));
        var pages = Pages(footprint.Bounds, references);
        var plan = new SpatialOccupancyReadPlan(scene.Scope, footprint.Bounds, key => pages[key], 8, 1, 1);
        Assert.Single(plan.Structures);
        Assert.Throws<InvalidOperationException>(() => plan.Resolve([actor], []));
        var original = scene.Structures.Single();
        Assert.Throws<ArgumentException>(() => plan.Resolve([actor], [new SpatialStructure(original.StructureId, original.Boxes)]));
        var terrain = new TerrainReadPlan(scene.Scope, footprint.Bounds, scene.Regions.Select(TerrainRegionReference.Capture), 1);
        var prepared = SpatialMovementDependencies.FromDirectory(footprint, terrain, plan.Resolve([actor], scene.Structures), scene.StructureSupport);
        Assert.Equal(320, prepared.Evaluate(terrain.Resolve(scene.Regions)).Candidate!.Feet.Z);
        prepared.ValidateCurrent(scene);
    }

    [Fact]
    public void A_structure_envelope_can_overfetch_without_inventing_a_collider_in_its_empty_gap()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var structure = new SpatialStructure(SpatialFixture.Id(7000), [
            new(new(-512, 0, 256), new(-256, 256, 512)), new(new(512, 0, 256), new(768, 256, 512))
        ]);
        var scene = new LocalSpatialSnapshot(SpatialFixture.Scope, [SpatialFixture.Terrain()], [actor], [structure]);
        var footprint = MovementTerrainFootprint.Create(actor, scene.GroundSupport, 32, 0);
        var pages = Pages(footprint.Bounds, [SpatialOccupancyReference.Capture(actor), SpatialOccupancyReference.Capture(scene.Scope, structure)]);
        var plan = new SpatialOccupancyReadPlan(scene.Scope, footprint.Bounds, key => pages[key], 8, 1, 1);
        Assert.Single(plan.Structures);
        var terrain = new TerrainReadPlan(scene.Scope, footprint.Bounds, scene.Regions.Select(TerrainRegionReference.Capture), 1);
        var prepared = SpatialMovementDependencies.FromDirectory(footprint, terrain, plan.Resolve([actor], [structure]), scene.StructureSupport);
        Assert.Empty(prepared.Structures);
        Assert.Equal(SpatialMoveStatus.Prepared, prepared.Evaluate(terrain.Resolve(scene.Regions)).Status);
        prepared.ValidateCurrent(scene);
    }

    private static Dictionary<SpatialOccupancyPageKey, SpatialOccupancyPage> Pages(TerrainRegionBounds requested,
        IEnumerable<SpatialOccupancyReference> references)
    {
        var entries = references.ToArray();
        var result = new Dictionary<SpatialOccupancyPageKey, SpatialOccupancyPage>();
        var minimum = SpatialOccupancyPageKey.At(requested.Minimum);
        var maximum = SpatialOccupancyPageKey.At(requested.Maximum);
        for (long z = minimum.Z; z <= maximum.Z; z++)
            for (long y = minimum.Y; y <= maximum.Y; y++)
                for (long x = minimum.X; x <= maximum.X; x++)
                {
                    var key = new SpatialOccupancyPageKey(x, y, z);
                    var bounds = key.Bounds;
                    result[key] = new(SpatialFixture.Scope, key, 0, entries.Where(entry =>
                        entry.Bounds.Minimum.X <= bounds.Maximum.X && entry.Bounds.Maximum.X >= bounds.Minimum.X
                        && entry.Bounds.Minimum.Y <= bounds.Maximum.Y && entry.Bounds.Maximum.Y >= bounds.Minimum.Y
                        && entry.Bounds.Minimum.Z <= bounds.Maximum.Z && entry.Bounds.Maximum.Z >= bounds.Minimum.Z));
                }
        return result;
    }
}
