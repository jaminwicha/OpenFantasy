using System.Buffers.Binary;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ConsequencePreparationTests
{
    [Fact]
    public void An_admitted_exhausted_terrain_revision_refuses_before_pending_acceptance_and_preserves_one_interval()
    {
        var fixture = new StructuralSettlementWorldFixture(consequenceWorkBudget: 1);
        using var files = new StorageFiles();
        WorldSnapshot funded = fixture.PrepareBeforeExcavation(files.Path);
        // Funding, construction, tools, access and earned effort are real. Only this numeric-boundary
        // counterfactual changes a supplied region revision; it does not rewrite the earned journal.
        WorldSnapshot immediateBasis = Copy(funded, funded.Space, deferred: false);
        WorldStepDecision available = new WorldStepProcessor().Decide(immediateBasis, fixture.NextExcavation(immediateBasis));
        Assert.Equal(WorldActionRejection.None, Assert.Single(available.Receipts).Rejection);
        Assert.True(available.Candidate.Excavation!.Sites.Single().IsComplete);

        TerrainRegionSnapshot original = funded.Space.Regions.Single(region => region.Bounds.Contains(fixture.Site.Cell));
        byte[] regionBytes = TerrainRegionCodecV1.Encode(original);
        BinaryPrimitives.WriteUInt64BigEndian(regionBytes.AsSpan(FieldOffset(regionBytes, 5)), ulong.MaxValue);
        TerrainRegionSnapshot exhausted = TerrainRegionCodecV1.Decode(regionBytes);
        Assert.Equal(ulong.MaxValue, exhausted.Revision);
        Assert.Equal(regionBytes, TerrainRegionCodecV1.Encode(exhausted));
        Assert.Equal(original.Cells.Select(cell => (cell.Coordinate, cell.SourceId, cell.Material, cell.IsExcavated)),
            exhausted.Cells.Select(cell => (cell.Coordinate, cell.SourceId, cell.Material, cell.IsExcavated)));
        Assert.Throws<OverflowException>(() => exhausted.Excavate(exhausted.Revision, [fixture.Site.Cell]));
        var space = new LocalSpatialSnapshot(funded.Space.Scope,
            funded.Space.Regions.Select(region => ReferenceEquals(region, original) ? exhausted : region),
            funded.Space.Actors, funded.Space.Structures, funded.Space.GroundSupport);
        WorldSnapshot deferred = Copy(funded, space, deferred: true);
        WorldSnapshot immediate = Copy(funded, space, deferred: false);
        WorldStepBatch selected = fixture.NextExcavation(deferred);
        var intent = Assert.IsType<ExcavateIntent>(selected.Actions.Single().Intent);
        Assert.Equal(ulong.MaxValue, intent.ExpectedTerrainRevision);
        Assert.Equal(funded.Excavation!.Sites.Single().Revision, intent.ExpectedSiteRevision);

        WorldStepDecision? immediateRefusal = null;
        foreach (WorldSnapshot basis in new[] { immediate, deferred })
        {
            byte[] admitted = WorldSnapshotCodec.Encode(basis);
            WorldSnapshot restored = WorldSnapshotCodec.Decode(admitted);
            Assert.Equal(admitted, WorldSnapshotCodec.Encode(restored));
            var batch = new WorldStepBatch(selected.CauseId, basis.WorldId, basis.BranchId, basis.Revision,
                basis.AdvancingSteps, WorldBatchKind.AdvancingStep, [], selected.Actions, basis.ProfileVersion);
            WorldStepDecision refused = new WorldStepProcessor().Decide(restored, batch);
            WorldSnapshot after = refused.Candidate;
            Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(refused.Receipts).Rejection);
            Assert.Null(after.PendingExcavation);
            Assert.Equal(basis.Revision + 1, after.Revision);
            Assert.Equal(basis.AdvancingSteps + 1, after.AdvancingSteps);
            Assert.Equal(basis.Calendar.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep), after.Calendar);
            Assert.All(basis.Residents, resident => Assert.Equal(
                resident.ExperiencedTime.Add(ExposurePacingV1.FromExplorationSteps(1)),
                after.GetResident(resident.ActorId).ExperiencedTime));
            Assert.Equal(basis.Gathering!.AdvanceStep(after.AdvancingSteps).ContentHash, after.Gathering!.ContentHash);
            Assert.Equal(basis.Inventory.ContentHash, after.Inventory.ContentHash);
            Assert.Equal(basis.Production!.ContentHash, after.Production!.ContentHash);
            Assert.Equal(basis.Construction!.ContentHash, after.Construction!.ContentHash);
            Assert.Equal(basis.ConstructionPhysical!.ContentHash, after.ConstructionPhysical!.ContentHash);
            Assert.Equal(basis.Excavation!.ContentHash, after.Excavation!.ContentHash);
            Assert.Equal(basis.Space.Regions.Select(region => region.ComputeContentHash()),
                after.Space.Regions.Select(region => region.ComputeContentHash()));
            Assert.Equal(basis.Space.Actors.Select(actor => (actor.ActorId, actor.Feet, actor.Revision, actor.Condition.ComputeContentHash())),
                after.Space.Actors.Select(actor => (actor.ActorId, actor.Feet, actor.Revision, actor.Condition.ComputeContentHash())));
            Assert.DoesNotContain(refused.Identities, identity => identity.Kind == WorldCommittedIdentityKind.SpawnedEntity);
            Assert.Equal(new[] { WorldCommittedIdentityKind.CommitCause, WorldCommittedIdentityKind.Command, WorldCommittedIdentityKind.Result },
                refused.Identities.Select(identity => identity.Kind).Order());
            Assert.Equal(admitted, WorldSnapshotCodec.Encode(basis));
            if (basis.ProfileVersion == 15)
            {
                immediateRefusal = refused;
            }
            else
            {
                WorldStepDecision legacy = Assert.IsType<WorldStepDecision>(immediateRefusal);
                Assert.Equal(WorldReceiptCodecV1.Encode(legacy.Receipts.Single()), WorldReceiptCodecV1.Encode(refused.Receipts.Single()));
                Assert.Equal(WorldSnapshotCodec.Encode(legacy.Candidate),
                    WorldSnapshotCodec.Encode(Copy(after, after.Space, deferred: false)));
            }
        }
    }

    private static WorldSnapshot Copy(WorldSnapshot basis, LocalSpatialSnapshot space, bool deferred) => new(
        basis.Inventory, space, basis.Residents, basis.AdmissionVersion, basis.Calendar, basis.Revision,
        basis.AdvancingSteps, basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests,
        basis.Construction, excavation: basis.Excavation, constructionPhysical: basis.ConstructionPhysical,
        structuralPolicy: basis.StructuralPolicy, structuralSettlementPolicy: basis.StructuralSettlementPolicy,
        consequencePolicy: deferred ? basis.ConsequencePolicy : null);

    private static int FieldOffset(byte[] frame, int ordinal)
    {
        int offset = 12;
        for (int index = 0; index < ordinal; index++)
        {
            offset += 4 + checked((int)BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(offset)));
        }
        return offset + 4;
    }
}
