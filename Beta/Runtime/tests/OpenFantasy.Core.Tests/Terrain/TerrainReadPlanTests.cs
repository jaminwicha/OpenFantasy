using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Tests.Spatial;

namespace OpenFantasy.Core.Tests.Terrain;

public sealed class TerrainReadPlanTests
{
    private static readonly TerrainRegionBounds Request = new(new(-1, -1, 0), 2, 2, 3);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Partial_terrain_reads_preserve_actual_cross_border_clearance_and_collision(bool blocked)
    {
        var left = SpatialFixture.Terrain(x: -4, width: 4, depth: 8, height: 4);
        var right = SpatialFixture.Terrain(x: 0, width: 4, depth: 8, height: 4,
            keptSolids: blocked ? [new(0, 0, 1)] : []);
        var distant = SpatialFixture.Terrain(x: 100, width: 4, depth: 8, height: 4);
        var actor = SpatialFixture.Actor(1, new(-128, 0, 256));
        TerrainRegionSnapshot[] all = [left, right, distant];
        var full = new LocalSpatialSnapshot(SpatialFixture.Scope, all, [actor]);
        var plan = new TerrainReadPlan(SpatialFixture.Scope, Request,
            all.Reverse().Select(TerrainRegionReference.Capture), maximumLoadedRegions: 2);
        Assert.Equal(2, plan.References.Count);
        var read = plan.Resolve([right, left]);
        var local = new LocalSpatialSnapshot(SpatialFixture.Scope, read.Regions, [actor]);
        var expected = full.PrepareMove(actor.ActorId, actor.Revision, 256, 0);
        var actual = local.PrepareMove(actor.ActorId, actor.Revision, 256, 0);
        Assert.Equal(blocked ? SpatialMoveStatus.BlockedTerrain : SpatialMoveStatus.Prepared, actual.Status);
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.Candidate?.Feet, actual.Candidate?.Feet);
        Assert.Equal(expected.Candidate?.Revision, actual.Candidate?.Revision);
        Assert.DoesNotContain(read.Regions, region => region.Bounds == distant.Bounds);
    }

    [Fact]
    public void Missing_support_coverage_duplicates_and_excess_residency_never_form_a_plan()
    {
        var left = SpatialFixture.Terrain(x: -4, width: 4, depth: 8, height: 4);
        var right = SpatialFixture.Terrain(x: 0, width: 4, depth: 8, height: 4);
        var a = TerrainRegionReference.Capture(left);
        var b = TerrainRegionReference.Capture(right);
        Assert.Throws<InvalidOperationException>(() => new TerrainReadPlan(SpatialFixture.Scope, Request, [a], 2));
        Assert.Throws<InvalidOperationException>(() => new TerrainReadPlan(SpatialFixture.Scope, Request, [a, b], 1));
        Assert.Throws<ArgumentException>(() => new TerrainReadPlan(SpatialFixture.Scope, Request, [a, b, a], 3));
        var aboveSupport = new TerrainRegionReference(a.Scope, new(new(-1, -1, 1), 2, 2, 2), 0, a.ContentHash);
        Assert.Throws<InvalidOperationException>(() => new TerrainReadPlan(SpatialFixture.Scope, Request, [aboveSupport], 2));
    }

    [Fact]
    public void Exact_resolved_set_refuses_missing_extra_duplicate_or_changed_regions()
    {
        var left = SpatialFixture.Terrain(x: -4, width: 4, depth: 8, height: 4);
        var right = SpatialFixture.Terrain(x: 0, width: 4, depth: 8, height: 4);
        var plan = new TerrainReadPlan(SpatialFixture.Scope, Request,
            new[] { left, right }.Select(TerrainRegionReference.Capture), 2);
        Assert.Throws<InvalidOperationException>(() => plan.Resolve([left]));
        Assert.Throws<ArgumentException>(() => plan.Resolve([left, right, left]));
        Assert.Throws<ArgumentException>(() => plan.Resolve([left, right,
            SpatialFixture.Terrain(x: 100, width: 4, depth: 8, height: 4)]));
        var changed = right.Excavate(right.Revision, [new(0, 0, 0)]);
        Assert.Throws<ArgumentException>(() => plan.Resolve([left, changed]));
        Assert.Throws<InvalidOperationException>(() => plan.ValidateCurrentReferences(
            new[] { left, changed }.Select(TerrainRegionReference.Capture)));
        Assert.Equal(right.Revision, plan.Resolve([left, right]).Regions[1].Revision);
    }

    [Fact]
    public void An_unrelated_region_change_does_not_invalidate_the_exact_read_dependencies()
    {
        var near = SpatialFixture.Terrain(width: 8, depth: 8, height: 4);
        var distant = SpatialFixture.Terrain(x: 100, width: 4, depth: 8, height: 4);
        var plan = new TerrainReadPlan(SpatialFixture.Scope, Request,
            new[] { near, distant }.Select(TerrainRegionReference.Capture), 1);
        var changed = distant.Excavate(distant.Revision, [new(100, 0, 0)]);
        plan.ValidateCurrentReferences(new[] { changed, near }.Select(TerrainRegionReference.Capture));
        Assert.Same(near, Assert.Single(plan.Resolve([near]).Regions));
    }

    [Fact]
    public void Matching_hash_does_not_allow_false_scope_revision_or_bounds_metadata()
    {
        var region = SpatialFixture.Terrain(width: 8, depth: 8, height: 4);
        var expected = TerrainRegionReference.Capture(region);
        var otherRealm = expected.Scope with { RealmId = SpatialFixture.Id(777) };
        Assert.Throws<ArgumentException>(() => new TerrainReadPlan(SpatialFixture.Scope, Request,
            [new(otherRealm, region.Bounds, region.Revision, expected.ContentHash)], 1));
        foreach (var forged in new[]
        {
            new TerrainRegionReference(region.Scope, region.Bounds, region.Revision + 1, expected.ContentHash),
            new TerrainRegionReference(region.Scope, Request, region.Revision, expected.ContentHash),
            new TerrainRegionReference(region.Scope, region.Bounds, region.Revision, ContentHash.Compute("wrong"u8))
        })
        {
            var plan = new TerrainReadPlan(SpatialFixture.Scope, Request, [forged], 1);
            Assert.Throws<ArgumentException>(() => plan.Resolve([region]));
        }
    }

    [Fact]
    public void Metadata_scan_has_a_separate_bound_even_when_extra_regions_would_not_be_loaded()
    {
        var near = TerrainRegionReference.Capture(SpatialFixture.Terrain(width: 8, depth: 8, height: 4));
        var far = TerrainRegionReference.Capture(SpatialFixture.Terrain(x: 100, width: 4, depth: 8, height: 4));
        Assert.Throws<ArgumentException>(() => new TerrainReadPlan(SpatialFixture.Scope, Request,
            new[] { near }.Concat(Enumerable.Repeat(far, TerrainReadPlan.MaximumCandidateRegions)), 1));
    }

    [Fact]
    public void Expected_decode_checks_header_before_realization_and_preserves_ordinary_decoder_bytes()
    {
        var region = SpatialFixture.Terrain(width: 8, depth: 8, height: 4);
        byte[] bytes = TerrainRegionCodecV1.Encode(region);
        var reference = TerrainRegionReference.Capture(region);
        Assert.Contains("encoded byte bound", Assert.Throws<FormatException>(() => TerrainRegionCodecV1.Decode(
            new byte[TerrainRegionCodecV1.MaximumEncodedByteLength + 1], reference)).Message);
        var restored = TerrainRegionCodecV1.Decode(bytes, reference);
        Assert.Equal(bytes, TerrainRegionCodecV1.Encode(restored));
        Assert.Equal(TerrainRegionCodecV1.Encode(TerrainRegionCodecV1.Decode(bytes)), TerrainRegionCodecV1.Encode(restored));
        foreach (var forged in new[]
        {
            new TerrainRegionReference(reference.Scope, Request, reference.Revision, reference.ContentHash),
            new TerrainRegionReference(reference.Scope, reference.Bounds, reference.Revision + 1, reference.ContentHash),
            new TerrainRegionReference(reference.Scope with { BranchId = SpatialFixture.Id(888) },
                reference.Bounds, reference.Revision, reference.ContentHash)
        })
        {
            var error = Assert.Throws<FormatException>(() => TerrainRegionCodecV1.Decode(bytes, forged));
            Assert.Contains("header", error.Message);
        }
    }
}
