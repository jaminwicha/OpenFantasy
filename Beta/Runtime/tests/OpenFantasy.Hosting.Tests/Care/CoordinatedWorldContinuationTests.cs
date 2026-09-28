using OpenFantasy.Core;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>
/// Core-only coordinated continuation over the actual ordinary-care genesis. No storage, host
/// authentication, process recovery or native behavior is exercised here.
/// </summary>
public sealed class CoordinatedWorldContinuationTests
{
    private static readonly Func<EntityId, bool> NoHistoricalIdentity = _ => false;

    [Fact]
    public void Fresh_opt_in_selects_the_coordinated_profile_without_changing_the_ordinary_genesis()
    {
        WorldSnapshot ordinary = new OrdinaryCareWorldFixture().Genesis;
        byte[] ordinaryBytes = WorldSnapshotCodec.Encode(ordinary);

        WorldSnapshot coordinated = WorldCoordinatedAdmission.CreateFresh(ordinary);

        Assert.Equal(26, ordinary.ProfileVersion);
        Assert.Equal(ordinaryBytes, WorldSnapshotCodec.Encode(ordinary));
        Assert.Equal(27, coordinated.ProfileVersion);
        Assert.NotNull(coordinated.Coordination);
        Assert.Null(coordinated.Coordination!.Anchor);
        Assert.False(coordinated.Production!.IsContinued);
        Assert.Null(coordinated.Care!.Continuation);
        Assert.NotEqual(ordinary.AdmissionVersion, coordinated.AdmissionVersion);
        byte[] bytes = WorldSnapshotCodec.Encode(coordinated);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.DecodeCoordinated(bytes, null)));
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.DecodeCoordinated(ordinaryBytes, null));
    }

    [Fact]
    public void A_lived_ordinary_world_is_never_upgraded()
    {
        WorldSnapshot lived = Advance(new OrdinaryCareWorldFixture().Genesis, 1);

        Assert.Equal(1UL, lived.Revision);
        Assert.Throws<ArgumentException>(() => WorldCoordinatedAdmission.CreateFresh(lived));
    }

    [Fact]
    public void A_cut_changes_representation_only_and_requires_its_exact_joint_context()
    {
        WorldSnapshot closing = Advance(WorldCoordinatedAdmission.CreateFresh(new OrdinaryCareWorldFixture().Genesis), 1);

        var segment = WorldMaterialCareArchiveSegment.Seal(closing);
        WorldSnapshot continued = segment.Continued;

        Assert.Equal(0UL, segment.Reference.Ordinal);
        Assert.Equal(closing.Revision, segment.Anchor.ClosingWorldSequence);
        Assert.True(segment.Anchor.LatestCareSequence <= segment.Anchor.ClosingWorldSequence);
        Assert.Equal(closing.ContentHash, segment.Reference.BoundaryHash);
        Assert.Equal(closing.Revision, continued.Revision);
        Assert.Equal(closing.AdvancingSteps, continued.AdvancingSteps);
        Assert.Equal(closing.Calendar, continued.Calendar);
        Assert.Equal(closing.Inventory.ContentHash, continued.Inventory.ContentHash);
        Assert.Equal(closing.Progression!.ContentHash, continued.Progression!.ContentHash);
        Assert.Equal(closing.Gathering!.ContentHash, continued.Gathering!.ContentHash);
        Assert.Equal(closing.Production!.Revision, continued.Production!.Revision);
        Assert.True(continued.Production.Continuation!.SupportsCompleteWorld);
        Assert.Null(continued.Production.MaterialHistory);
        Assert.NotNull(continued.Care!.Continuation);
        Assert.Same(segment.Anchor, continued.ResourceHistory!.CoordinatedAnchor);
        Assert.Null(continued.ResourceHistory.ArchiveAnchor);
        Assert.Throws<ArgumentException>(() => WorldResourceHistoryCodecV1.Encode(continued.ResourceHistory));

        byte[] bytes = WorldSnapshotCodec.Encode(continued);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.DecodeCoordinated(bytes, segment.Anchor)));
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(bytes));
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.DecodeCoordinated(bytes, null));

        var retrieved = WorldMaterialCareArchiveSegment.Decode(segment.Reference, segment.Encode());
        Assert.Equal(segment.Anchor.ContentHash, retrieved.Anchor.ContentHash);
        var wrongOrdinal = new WorldMaterialCareArchiveReference(segment.Reference.WorldId, segment.Reference.BranchId,
            segment.Reference.Ordinal + 1, segment.Reference.BoundarySequence, segment.Reference.BoundaryHash);
        Assert.Throws<FormatException>(() => WorldMaterialCareArchiveSegment.Decode(wrongOrdinal, segment.Encode()));
    }

    [Fact]
    public void Standalone_production_contracts_refuse_the_coordinated_owner()
    {
        WorldSnapshot closing = Advance(WorldCoordinatedAdmission.CreateFresh(new OrdinaryCareWorldFixture().Genesis), 1);
        WorldSnapshot continued = WorldMaterialCareArchiveSegment.Seal(closing).Continued;

        Assert.Throws<ArgumentException>(() => ProductionCodecV3.Encode(continued.Production!));
        Assert.Throws<ArgumentException>(() => continued.Production!.Continue(continued.Inventory));
        Assert.Throws<ArgumentException>(() => continued.Production!.ContinueMaterial(continued.Inventory));
    }

    [Fact]
    public void A_second_cut_keeps_one_bounded_anchor_and_decodes_with_its_predecessor()
    {
        WorldSnapshot first = Advance(WorldCoordinatedAdmission.CreateFresh(new OrdinaryCareWorldFixture().Genesis), 1);
        var firstSegment = WorldMaterialCareArchiveSegment.Seal(first);
        Assert.Throws<InvalidOperationException>(() => new WorldStepProcessor().Decide(firstSegment.Continued,
            Step(firstSegment.Continued, 0)));

        WorldSnapshot second = Advance(firstSegment.Continued, 2);
        var secondSegment = WorldMaterialCareArchiveSegment.Seal(second);

        Assert.Equal(1UL, secondSegment.Reference.Ordinal);
        Assert.Equal(second.Revision, secondSegment.Anchor.ClosingWorldSequence);
        Assert.Same(secondSegment.Anchor, secondSegment.Continued.Coordination!.Anchor);
        var retrieved = WorldMaterialCareArchiveSegment.Decode(secondSegment.Reference, secondSegment.Encode(), firstSegment.Anchor);
        Assert.Equal(secondSegment.Anchor.ContentHash, retrieved.Anchor.ContentHash);
        Assert.Throws<FormatException>(() => WorldMaterialCareArchiveSegment.Decode(secondSegment.Reference, secondSegment.Encode()));
    }

    private static WorldSnapshot Advance(WorldSnapshot state, int steps)
    {
        var processor = new WorldStepProcessor();
        for (int step = 0; step < steps; step++)
        {
            state = processor.Decide(state, Step(state, (ulong)step), NoHistoricalIdentity).Candidate;
        }
        return state;
    }

    private static WorldStepBatch Step(WorldSnapshot state, ulong salt) => new(
        EntityId.DeriveSpawned(state.WorldId, checked(0xC00D_0000UL + state.Revision * 16 + salt)), state.WorldId,
        state.BranchId, state.Revision, state.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [], state.ProfileVersion);
}
