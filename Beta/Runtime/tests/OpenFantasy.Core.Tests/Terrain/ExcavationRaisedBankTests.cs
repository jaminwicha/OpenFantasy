using OpenFantasy.Core.Content;
using System.Buffers.Binary;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Tests.Worlds;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Terrain;

public sealed class ExcavationRaisedBankTests
{
    [Fact]
    public void Raised_bank_reaches_the_original_solid_top_and_spends_the_same_earned_labor()
    {
        var fixture = new ExcavationRaisedBankFixture();
        var before = fixture.World;
        Assert.Equal(40UL, before.AdvancingSteps);
        Assert.Equal(400U, before.Gathering!.GetActorWork(ExcavationRaisedBankFixture.Player).Effort);
        Assert.True(before.Space.Regions.Single().GetCell(fixture.Site.Cell).IsSolid);

        var plan = fixture.Plan();

        Assert.True(plan.Succeeded);
        Assert.Same(before.Inventory, plan.Inventory);
        Assert.Same(before.Space, plan.Space);
        Assert.Null(plan.SpawnedId);
        Assert.Equal(0U, plan.Gathering.GetActorWork(ExcavationRaisedBankFixture.Player).Effort);
        var worked = Assert.Single(plan.Excavation.Sites);
        Assert.Equal(1UL, worked.CompletedWork);
        Assert.Equal(ExcavationReachMode.RaisedBankTop, worked.ReachMode);
        Assert.Equal(fixture.Site.Stance, worked.Stance);
        Assert.Equal(400U, before.Gathering.GetActorWork(ExcavationRaisedBankFixture.Player).Effort);
        Assert.Equal(0UL, fixture.Site.CompletedWork);
    }

    [Fact]
    public void Completing_work_removes_one_cell_and_issues_its_exact_finite_source()
    {
        var fixture = new ExcavationRaisedBankFixture(requiredWork: 1);

        var plan = fixture.Plan();

        Assert.True(plan.Succeeded);
        Assert.False(plan.Space.Regions.Single().GetCell(fixture.Site.Cell).IsSolid);
        Assert.True(fixture.World.Space.Regions.Single().GetCell(fixture.Site.Cell).IsSolid);
        Assert.Equal(fixture.World.Space.GetActor(ExcavationRaisedBankFixture.Player),
            plan.Space.GetActor(ExcavationRaisedBankFixture.Player));
        Assert.Equal(fixture.World.Inventory.Custody.Lots.Count + 1, plan.Inventory.Custody.Lots.Count);
        var extracted = Assert.Single(plan.Inventory.Custody.Lots, lot => lot.LotId == plan.SpawnedId);
        Assert.Equal(fixture.Site.CountedSourceId, extracted.Source.SourceId);
        Assert.Equal(0UL, extracted.Source.Start);
        Assert.Equal(fixture.Site.Quantity, extracted.Source.End);
        Assert.Equal(fixture.Site.Material.Item, extracted.Definition);
        Assert.Equal(ExcavationReachMode.RaisedBankTop, Assert.Single(plan.Excavation.Sites).ReachMode);
    }

    [Fact]
    public void Overlying_solid_terrain_blocks_the_direct_top_sightline_even_with_a_reachable_bank()
    {
        var fixture = new ExcavationRaisedBankFixture(coveredTarget: true);
        Assert.Equal(SpatialInteraction.Reachable, ResourceInteractionQueries.Query(fixture.World.Space,
            ExcavationRaisedBankFixture.Player, fixture.Site.Stance));

        AssertUnchangedRefusal(fixture, fixture.Plan(), WorldActionRejection.OutOfReach);
    }

    [Theory]
    [InlineData(-320L, true)]
    [InlineData(-321L, false)]
    public void Actual_target_uses_the_unchanged_448_subunit_range_not_the_nearer_stance(long x, bool reachable)
    {
        var fixture = new ExcavationRaisedBankFixture(actorFeet: new(x, 128, 256));
        Assert.Equal(SpatialInteraction.Reachable, ResourceInteractionQueries.Query(fixture.World.Space,
            ExcavationRaisedBankFixture.Player, fixture.Site.Stance));

        var plan = fixture.Plan();

        Assert.Equal(reachable, plan.Succeeded);
        if (!reachable) { AssertUnchangedRefusal(fixture, plan, WorldActionRejection.OutOfReach); }
    }

    [Fact]
    public void A_lower_actor_cannot_use_the_raised_stance_and_a_missing_bank_cannot_support_it()
    {
        var lower = new ExcavationRaisedBankFixture(actorFeet: new(-128, 128, 0), holes: [new(-1, 0, 0)]);
        Assert.Equal(SpatialInteraction.DifferentFloor, ResourceInteractionQueries.Query(lower.World.Space,
            ExcavationRaisedBankFixture.Player, lower.Site.Stance));
        AssertUnchangedRefusal(lower, lower.Plan(), WorldActionRejection.OutOfReach);

        var unsupported = new ExcavationRaisedBankFixture(actorFeet: new(128, 384, 256),
            holes: [new(1, 0, 0)], stance: new(384, 128, 256));
        Assert.Equal(SpatialOccupancy.Unsupported, ResourceInteractionQueries.QueryAnchor(unsupported.World.Space,
            unsupported.Site.Stance));
        AssertUnchangedRefusal(unsupported, unsupported.Plan(), WorldActionRejection.OutOfReach);
    }

    [Fact]
    public void Raised_mode_keeps_effort_tool_revision_actor_revision_and_held_gates()
    {
        var insufficient = new ExcavationRaisedBankFixture(earnedSteps: 39);
        AssertUnchangedRefusal(insufficient, insufficient.Plan(), WorldActionRejection.Unavailable);
        var fixture = new ExcavationRaisedBankFixture();
        AssertUnchangedRefusal(fixture, fixture.Plan(held: true), WorldActionRejection.Unavailable);
        AssertUnchangedRefusal(fixture, fixture.Plan(advancing: false), WorldActionRejection.Unavailable);
        AssertUnchangedRefusal(fixture, fixture.Plan(fixture.Command(toolRevision: 1)), WorldActionRejection.Unavailable);
        AssertUnchangedRefusal(fixture, fixture.Plan(fixture.Command(actorRevision: 1)), WorldActionRejection.RevisionConflict);
    }

    [Theory]
    [InlineData(-128L, 128L, 0L)]
    [InlineData(-128L, 128L, 512L)]
    [InlineData(-128L, 384L, 256L)]
    [InlineData(-127L, 128L, 256L)]
    public void Raised_stance_must_be_centered_cardinal_and_exactly_one_cell_above(long x, long y, long z)
    {
        var site = new ExcavationRaisedBankFixture(earnedSteps: 0).Site;
        Assert.Throws<ArgumentException>(() => ExcavationRaisedBankFixture.Copy(site, new(x, y, z),
            ExcavationReachMode.RaisedBankTop));
    }

    [Fact]
    public void Legacy_default_retains_original_side_behavior_and_exact_v1_fields()
    {
        var fixture = new ExcavationRaisedBankFixture(actorFeet: new(-128, 128, 0),
            holes: [new(-1, 0, 0)], stance: new(-128, 128, 0), mode: ExcavationReachMode.SameLevelSide);
        var site = fixture.Site;
        var defaultSite = new ExcavationSite(site.SiteId, site.Cell, site.TerrainSourceId, site.CountedSourceId,
            site.OwnerActorId, site.Material, site.RequiredTool, site.RequiredWork, site.CompletedWork, site.Stance);
        Assert.Equal(ExcavationReachMode.SameLevelSide, defaultSite.ReachMode);
        Assert.True(fixture.Plan().Succeeded);

        byte[] expectedSite = ReceiptFixture.Frame([
            "terrain.excavation-site.v1"u8.ToArray(), site.SiteId.ToByteArray(), Position(site.Cell.X, site.Cell.Y, site.Cell.Z),
            site.TerrainSourceId.ToByteArray(), site.CountedSourceId.ToByteArray(), site.OwnerActorId.ToByteArray(),
            [..site.Material.Item.DefinitionId.ToByteArray(), ..site.Material.Item.Version.ToByteArray()],
            MaterialRecipeCodecV1.EncodeMaterial(site.Material.Physical),
            [..site.RequiredTool.DefinitionId.ToByteArray(), ..site.RequiredTool.Version.ToByteArray()],
            ReceiptFixture.Number(site.RequiredWork), ReceiptFixture.Number(site.CompletedWork),
            Position(site.Stance.X, site.Stance.Y, site.Stance.Z)]);
        byte[] expected = ReceiptFixture.Frame(["terrain.excavation.v1"u8.ToArray(), expectedSite]);
        var state = new ExcavationSnapshot([defaultSite]);
        Assert.Equal(expected, ExcavationCodecV1.Encode(state));
        Assert.Equal(expected, ExcavationCodecV1.Encode(ExcavationCodecV1.Decode(expected)));
        Assert.Equal(ContentHash.Compute(expected), state.ContentHash);
        Assert.Equal(ExcavationReachMode.SameLevelSide, Assert.Single(ExcavationCodecV1.Decode(expected).Sites).ReachMode);
    }

    [Fact]
    public void Explicit_mode_roundtrips_canonically_and_cannot_silently_use_the_legacy_codec()
    {
        var fixture = new ExcavationRaisedBankFixture(earnedSteps: 0);
        byte[] bytes = ExcavationCodecV2.Encode(fixture.Excavation);
        var decoded = ExcavationCodecV2.Decode(bytes);
        Assert.Equal(bytes, ExcavationCodecV2.Encode(decoded));
        Assert.Equal(ExcavationReachMode.RaisedBankTop, Assert.Single(decoded.Sites).ReachMode);
        Assert.Equal(fixture.Site.Stance, Assert.Single(decoded.Sites).Stance);
        Assert.Equal(ContentHash.Compute(bytes), fixture.Excavation.ContentHash);
        Assert.Equal(fixture.Excavation.ContentHash, decoded.ContentHash);
        Assert.Throws<ArgumentException>(() => ExcavationCodecV1.Encode(fixture.Excavation));
        Assert.Throws<FormatException>(() => ExcavationCodecV1.Decode(bytes));
        var legacy = new ExcavationSnapshot([ExcavationRaisedBankFixture.Copy(fixture.Site, new(-128, 128, 0),
            ExcavationReachMode.SameLevelSide)]);
        Assert.Throws<FormatException>(() => ExcavationCodecV2.Decode(ExcavationCodecV1.Encode(legacy)));
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(3UL)]
    [InlineData(ulong.MaxValue)]
    public void Codec_rejects_unknown_or_geometry_inconsistent_mode(ulong mode)
    {
        var fixture = new ExcavationRaisedBankFixture(earnedSteps: 0);
        byte[] bytes = ExcavationCodecV2.Encode(fixture.Excavation);
        var outer = ReceiptFixture.Fields(bytes);
        outer[1] = ReceiptFixture.Replace(outer[1], 12, ReceiptFixture.Number(mode));
        Assert.Throws<FormatException>(() => ExcavationCodecV2.Decode(ReceiptFixture.Frame(outer)));
    }

    [Fact]
    public void Explicit_codec_rejects_truncation_trailing_data_and_byte_overflow()
    {
        byte[] bytes = ExcavationCodecV2.Encode(new ExcavationRaisedBankFixture(earnedSteps: 0).Excavation);
        Assert.Throws<FormatException>(() => ExcavationCodecV2.Decode(bytes[..^1]));
        Assert.Throws<FormatException>(() => ExcavationCodecV2.Decode([..bytes, 0]));
        Assert.Throws<FormatException>(() => ExcavationCodecV2.Decode(new byte[ExcavationCodecV2.MaximumEncodedByteLength + 1]));
    }

    private static void AssertUnchangedRefusal(ExcavationRaisedBankFixture fixture, ExcavationPlan plan,
        WorldActionRejection expected)
    {
        Assert.False(plan.Succeeded);
        Assert.Equal(expected, plan.Rejection);
        Assert.Same(fixture.World.Inventory, plan.Inventory);
        Assert.Same(fixture.World.Space, plan.Space);
        Assert.Same(fixture.World.Gathering, plan.Gathering);
        Assert.Same(fixture.Excavation, plan.Excavation);
        Assert.Null(plan.SpawnedId);
    }

    private static byte[] Position(long x, long y, long z)
    {
        byte[] result = new byte[24];
        BinaryPrimitives.WriteInt64BigEndian(result, x);
        BinaryPrimitives.WriteInt64BigEndian(result.AsSpan(8), y);
        BinaryPrimitives.WriteInt64BigEndian(result.AsSpan(16), z);
        return result;
    }
}
