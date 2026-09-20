using System.Buffers.Binary;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class CoupledSettlementGeometryTests
{
    [Fact]
    public void Funded_coupled_geometry_is_identical_across_work_slices_and_every_retained_prefix()
    {
        var fixture = new CoupledSettlementWorldFixture();
        using var files = new StorageFiles();
        WorldSnapshot basis = fixture.PrepareBeforeExcavation(files.Path);
        byte[] originalRoot = WorldSnapshotCodec.Encode(basis);
        byte[] originalDatabase = File.ReadAllBytes(files.Path);
        TerrainRegionSnapshot[] regions = Excavate(basis, CoupledSettlementWorldFixture.Pedestal);
        ConstructionSettlementResolution initial = ConstructionSettlementResolution.BeginCoupled(basis, regions);
        Assert.False(initial.IsComplete, initial.FailureReason);
        Assert.Equal(0UL, initial.CompletedWorkUnits);
        Assert.Equal(basis.CoupledSettlementPolicy!.MaximumWorkUnits, initial.MaximumTotalWorkUnits);
        ConstructionSettlementResolution expected = Finish(initial, ConstructionSettlementResolution.MaximumWorkBudget);
        Assert.False(expected.IsFailed, expected.FailureReason);
        Assert.True(expected.CompletedWorkUnits > 1);
        AssertCompleteDrop(basis, expected.Result, fixture);
        byte[] completedBytes = ConstructionSettlementResolutionCodecV2.Encode(expected);

        foreach (int budget in new[] { 1, 2, 3, 16, 256 })
        {
            ConstructionSettlementResolution sliced = Finish(initial, budget);
            Assert.Equal(completedBytes, ConstructionSettlementResolutionCodecV2.Encode(sliced));
            AssertSameGeometry(expected.Result, sliced.Result);
        }

        ConstructionSettlementResolution prefix = initial;
        while (true)
        {
            byte[] archive = ConstructionSettlementResolutionCodecV2.Encode(prefix);
            ConstructionSettlementResolution restored = ConstructionSettlementResolutionCodecV2.Decode(archive,
                basis, regions.Reverse(), TestContext.Current.CancellationToken);
            Assert.Equal(archive, ConstructionSettlementResolutionCodecV2.Encode(restored));
            ConstructionSettlementResolution resumed = Finish(restored, 2);
            Assert.Equal(completedBytes, ConstructionSettlementResolutionCodecV2.Encode(resumed));
            AssertSameGeometry(expected.Result, resumed.Result);
            if (prefix.IsComplete) { break; }
            prefix = prefix.Advance(1);
        }

        Assert.False(initial.IsComplete);
        Assert.Equal(0UL, initial.CompletedWorkUnits);
        Assert.Equal(originalRoot, WorldSnapshotCodec.Encode(basis));
        Assert.Equal(originalDatabase, File.ReadAllBytes(files.Path));
        // A geometric result has no source-issuance or authority capability. The actual excavation,
        // fabrication backing and stock remain retained until their world transaction publishes them.
        Assert.Equal(0UL, basis.Excavation!.Sites.Single().CompletedWork);
        Assert.DoesNotContain(basis.Inventory.Custody.Lots, lot => lot.Source.SourceId == fixture.Site.CountedSourceId);
    }

    [Fact]
    public void Saved_progress_rejects_forged_status_cursor_dependencies_and_a_later_world_with_identical_geometry()
    {
        var fixture = new CoupledSettlementWorldFixture();
        using var files = new StorageFiles();
        WorldSnapshot basis = fixture.PrepareBeforeExcavation(files.Path);
        TerrainRegionSnapshot[] regions = Excavate(basis, CoupledSettlementWorldFixture.Pedestal);
        ConstructionSettlementResolution prefix = ConstructionSettlementResolution.BeginCoupled(basis, regions).Advance(1);
        Assert.False(prefix.IsComplete, prefix.FailureReason);
        ConstructionSettlementResolution completed = Finish(prefix, 256);
        Assert.False(completed.IsFailed, completed.FailureReason);
        byte[] archive = ConstructionSettlementResolutionCodecV2.Encode(prefix);
        ConstructionSettlementResolution Decode(byte[] bytes) => ConstructionSettlementResolutionCodecV2.Decode(
            bytes, basis, regions, TestContext.Current.CancellationToken);

        byte[] wrongPin = (byte[])archive.Clone();
        wrongPin[FieldOffset(wrongPin, 1)] ^= 1;
        Assert.Throws<FormatException>(() => Decode(wrongPin));
        foreach (ulong forgedStatus in new[] { 1UL, 2UL, 3UL, ulong.MaxValue })
        {
            Assert.Throws<FormatException>(() => Decode(ChangeNumber(archive, 3, forgedStatus)));
        }
        // A different genuine pending cursor can be valid. Reject an impossible prefix beyond the
        // actual terminal result, rather than assuming that every cursor change is corruption.
        byte[] terminal = ConstructionSettlementResolutionCodecV2.Encode(completed);
        Assert.Throws<FormatException>(() => Decode(ChangeNumber(terminal, 2, completed.CompletedWorkUnits + 1)));
        Assert.Throws<FormatException>(() => Decode(ChangeNumber(archive, 2, (ulong)prefix.MaximumTotalWorkUnits + 1)));
        Assert.Throws<FormatException>(() => Decode(ChangeNumber(archive, 2, ulong.MaxValue)));
        Assert.Throws<FormatException>(() => Decode(ChangeNumber(terminal, 3, 0)));
        for (int length = 0; length < archive.Length; length++)
        {
            Assert.Throws<FormatException>(() => Decode(archive[..length]));
        }
        Assert.Throws<FormatException>(() => Decode([.. archive, 0]));
        byte[] excessiveFields = (byte[])archive.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(excessiveFields.AsSpan(8), uint.MaxValue);
        Assert.Throws<FormatException>(() => Decode(excessiveFields));
        byte[] oversizedField = (byte[])archive.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(oversizedField.AsSpan(FieldOffset(oversizedField, 1) - 4), uint.MaxValue);
        Assert.Throws<FormatException>(() => Decode(oversizedField));
        Assert.Throws<OperationCanceledException>(() => ConstructionSettlementResolutionCodecV2.Decode(
            archive, basis, regions, new CancellationToken(canceled: true)));

        WorldSnapshot later;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            later = authority.Commit(fixture.Actions.Batch(basis, [], [])).Candidate;
        }
        AssertSameGeometry(new(basis.Space, basis.ConstructionPhysical!), new(later.Space, later.ConstructionPhysical!));
        Assert.Equal(basis.Inventory.ContentHash, later.Inventory.ContentHash);
        Assert.Equal(basis.Construction!.ContentHash, later.Construction!.ContentHash);
        Assert.Equal(basis.Production!.ContentHash, later.Production!.ContentHash);
        Assert.NotEqual(basis.Calendar, later.Calendar);
        Assert.NotEqual(basis.ContentHash, later.ContentHash);
        ConstructionSettlementResolution laterResolution = ConstructionSettlementResolution.BeginCoupled(later, regions);
        Assert.NotEqual(prefix.InputPin, laterResolution.InputPin);
        Assert.Throws<FormatException>(() => ConstructionSettlementResolutionCodecV2.Decode(archive,
            later, regions, TestContext.Current.CancellationToken));
        AssertSameGeometry(completed.Result, Finish(laterResolution, 3).Result);

        TerrainRegionSnapshot[] deeper = Excavate(basis,
            CoupledSettlementWorldFixture.Pedestal, CoupledSettlementWorldFixture.LandingCell);
        Assert.Throws<FormatException>(() => ConstructionSettlementResolutionCodecV2.Decode(archive,
            basis, deeper, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void A_deeper_excavation_refuses_the_whole_group_without_inventing_a_landing_or_publishing_partial_motion()
    {
        var fixture = new CoupledSettlementWorldFixture();
        using var files = new StorageFiles();
        WorldSnapshot basis = fixture.PrepareBeforeExcavation(files.Path);
        byte[] originalRoot = WorldSnapshotCodec.Encode(basis);
        byte[] originalDatabase = File.ReadAllBytes(files.Path);
        TerrainRegionSnapshot[] regions = Excavate(basis,
            CoupledSettlementWorldFixture.Pedestal, CoupledSettlementWorldFixture.LandingCell);
        // The original funded world is legal. Removing the extra landing cell is only a proposal:
        // the intact post now needs a 512-subunit fall, beyond this world's retained 256-unit horizon.
        Assert.Equal(256L, basis.StructuralSettlementPolicy!.MaximumDropSubunits);
        Assert.True(basis.Space.Regions.Single(region => region.Bounds.Contains(CoupledSettlementWorldFixture.LandingCell))
            .GetCell(CoupledSettlementWorldFixture.LandingCell).IsSolid);
        ConstructionSettlementResolution initial = ConstructionSettlementResolution.BeginCoupled(basis, regions);
        Assert.False(initial.IsComplete, initial.FailureReason);
        ConstructionSettlementResolution refused = Finish(initial, 1);
        Assert.True(refused.IsFailed);
        Assert.False(string.IsNullOrEmpty(refused.FailureReason));
        Assert.InRange(refused.CompletedWorkUnits, 1UL, (ulong)refused.MaximumTotalWorkUnits);
        Assert.Throws<InvalidOperationException>(() => refused.Result);
        Assert.Same(refused, refused.Advance(256));
        byte[] archive = ConstructionSettlementResolutionCodecV2.Encode(refused);
        Assert.Equal(archive, ConstructionSettlementResolutionCodecV2.Encode(Finish(initial, 256)));
        ConstructionSettlementResolution restored = ConstructionSettlementResolutionCodecV2.Decode(archive,
            basis, regions, TestContext.Current.CancellationToken);
        Assert.True(restored.IsFailed);
        Assert.Equal(archive, ConstructionSettlementResolutionCodecV2.Encode(restored));
        Assert.Throws<InvalidOperationException>(() => restored.Result);
        Assert.Equal(originalRoot, WorldSnapshotCodec.Encode(basis));
        Assert.Equal(originalDatabase, File.ReadAllBytes(files.Path));
    }

    private static ConstructionSettlementResolution Finish(ConstructionSettlementResolution resolution, int budget)
    {
        while (!resolution.IsComplete)
        {
            Assert.True(resolution.CompletedWorkUnits < (ulong)resolution.MaximumTotalWorkUnits);
            ulong previous = resolution.CompletedWorkUnits;
            resolution = resolution.Advance(budget);
            Assert.InRange(resolution.CompletedWorkUnits - previous, 1UL, (ulong)budget);
        }
        return resolution;
    }

    private static TerrainRegionSnapshot[] Excavate(WorldSnapshot basis, params CellCoordinate[] cells)
    {
        foreach (CellCoordinate cell in cells)
        {
            Assert.Single(basis.Space.Regions, region => region.Bounds.Contains(cell));
        }
        return basis.Space.Regions.Select(region =>
        {
            CellCoordinate[] selected = cells.Where(region.Bounds.Contains).ToArray();
            return selected.Length == 0 ? region : region.Excavate(region.Revision, selected);
        }).ToArray();
    }

    private static void AssertCompleteDrop(WorldSnapshot basis, ConstructionSettlementResult result,
        CoupledSettlementWorldFixture fixture)
    {
        Assert.Equal(3, result.Physical.Members.Count);
        Assert.Equal(2, result.Physical.Members.Select(member => member.StructureId).Distinct().Count());
        Assert.Equal(basis.Construction!.ContentHash, result.Physical.ConstructionHash);
        Assert.Equal(basis.ConstructionPhysical!.Revision + 1, result.Physical.Revision);
        foreach (ConstructionMemberState member in result.Physical.Members)
        {
            ConstructionMemberState previous = basis.ConstructionPhysical.GetMember(member.StructureId, member.MemberIndex);
            Assert.Equal(previous.ElementId, member.ElementId);
            Assert.Equal(previous.ConditionParts, member.ConditionParts);
            Assert.Equal(previous.Revision + 1, member.Revision);
            Assert.Equal(previous.Displacement.Offset(0, 0, -256), member.Displacement);
            Assert.Equal(ConstructionMemberDisposition.Resting, member.Disposition);
        }
        ActorPresence beforeRider = basis.Space.GetActor(fixture.Builder);
        ActorPresence rider = result.Space.GetActor(fixture.Builder);
        Assert.Equal(beforeRider.Feet.Offset(0, 0, -256), rider.Feet);
        Assert.Equal(beforeRider.Revision + 1, rider.Revision);
        Assert.Equal(beforeRider.Binding, rider.Binding);
        Assert.Equal(beforeRider.Condition.ComputeContentHash(), rider.Condition.ComputeContentHash());
        Assert.Equal(basis.Space.GetActor(fixture.Player).Feet, result.Space.GetActor(fixture.Player).Feet);
        Assert.Equal(basis.Space.GetActor(fixture.Player).Revision, result.Space.GetActor(fixture.Player).Revision);
    }

    private static void AssertSameGeometry(ConstructionSettlementResult expected, ConstructionSettlementResult actual)
    {
        Assert.Equal(ConstructionPhysicalCodecV1.Encode(expected.Physical), ConstructionPhysicalCodecV1.Encode(actual.Physical));
        Assert.Equal(expected.Space.Scope, actual.Space.Scope);
        Assert.Equal(expected.Space.GroundSupport, actual.Space.GroundSupport);
        Assert.Equal(expected.Space.StructureSupport, actual.Space.StructureSupport);
        Assert.Equal(expected.Space.Regions.Select(region => region.ComputeContentHash()).ToArray(),
            actual.Space.Regions.Select(region => region.ComputeContentHash()).ToArray());
        Assert.Equal(expected.Space.Actors.Select(actor => actor.ActorId).ToArray(), actual.Space.Actors.Select(actor => actor.ActorId).ToArray());
        foreach (ActorPresence actor in expected.Space.Actors)
        {
            ActorPresence other = actual.Space.GetActor(actor.ActorId);
            Assert.Equal(actor.Binding, other.Binding);
            Assert.Equal(actor.Condition.ComputeContentHash(), other.Condition.ComputeContentHash());
            Assert.Equal(actor.Scope, other.Scope);
            Assert.Equal(actor.Feet, other.Feet);
            Assert.Equal(actor.Revision, other.Revision);
            Assert.Equal(actor.HomePlaceId, other.HomePlaceId);
        }
    }

    private static byte[] ChangeNumber(byte[] bytes, int field, ulong value)
    {
        byte[] changed = (byte[])bytes.Clone();
        BinaryPrimitives.WriteUInt64BigEndian(changed.AsSpan(FieldOffset(changed, field)), value);
        return changed;
    }

    private static int FieldOffset(byte[] bytes, int field)
    {
        int offset = 12;
        for (int index = 0; index < field; index++)
        {
            offset += checked(4 + (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
        }
        return offset + 4;
    }
}
