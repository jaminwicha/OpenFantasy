using System.Text.Json;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using static OpenFantasy.Core.Tests.Custody.CustodyFixture;

namespace OpenFantasy.Core.Tests.Custody;

public sealed class CustodyTransferTests
{
    private readonly CustodyTransferPlanner _planner = new();

    [Fact]
    public void SixWoodTransferPartitionsEightSourceUnitsWithoutMutatingOriginal()
    {
        CustodySnapshot before = Create();
        byte[] original = CustodyEncodingV1.Encode(before);
        CustodyTransferPlan plan = _planner.Plan(before, Command(before));
        CustodySnapshot after = plan.ApplyTo(before);
        CountedSourceLot moved = after.GetLot(plan.MovedLotId);
        CountedSourceLot remainder = after.GetLot(Id(5));
        Assert.Equal(6UL, after.GetStoredQuantity(Id(4)));
        Assert.Equal(2UL, after.GetStoredQuantity(Id(3)));
        Assert.Equal(new SourceInterval(Id(6), 10, 16), moved.Source);
        Assert.Equal(new SourceInterval(Id(6), 16, 18), remainder.Source);
        Assert.Equal(Wood, moved.Definition);
        Assert.Equal(0UL, moved.Revision);
        Assert.Equal(4UL, remainder.Revision);
        Assert.Equal(5UL, after.GetStorage(Id(3)).Revision);
        Assert.Equal(6UL, after.GetStorage(Id(4)).Revision);
        Assert.Equal(8UL, after.Revision);
        Assert.Equal(original, CustodyEncodingV1.Encode(before));
        Assert.Equal(after.ContentHash, _planner.Plan(before, Command(before)).Candidate.ContentHash);
    }

    [Fact]
    public void CanonicalTransferSnapshotMatchesIndependentPythonOracle()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "custody-v1.json")));
        JsonElement expected = document.RootElement;
        CustodySnapshot before = Create();
        CustodyTransferPlan plan = _planner.Plan(before, Command(before));
        Assert.Equal(expected.GetProperty("originalHex").GetString(),
            Convert.ToHexString(CustodyEncodingV1.Encode(before)).ToLowerInvariant());
        Assert.Equal(expected.GetProperty("candidateHex").GetString(),
            Convert.ToHexString(CustodyEncodingV1.Encode(plan.Candidate)).ToLowerInvariant());
        Assert.Equal(expected.GetProperty("originalHash").GetString(), before.ContentHash.Value);
        Assert.Equal(expected.GetProperty("candidateHash").GetString(), plan.Candidate.ContentHash.Value);
        Assert.Equal(expected.GetProperty("childId").GetString(), plan.MovedLotId.ToString());
    }

    [Fact]
    public void FullTransferKeepsIdentityAndIncrementsRevision()
    {
        CustodySnapshot before = Create();
        CustodyTransferPlan plan = _planner.Plan(before, Command(before, 8));
        Assert.Equal(Id(5), plan.MovedLotId);
        CountedSourceLot only = Assert.Single(plan.Candidate.Lots);
        Assert.Equal(8UL, only.Quantity);
        Assert.Equal(before.GetLot(Id(5)).Source, only.Source);
        Assert.Equal(4UL, only.Revision);
        Assert.Equal(0UL, plan.Candidate.GetStoredQuantity(Id(3)));
    }

    [Fact]
    public void CapacityFailureWithExistingReceiverStockLeavesBothSidesExactlyUnchanged()
    {
        CustodySnapshot emptyReceiver = Create(receiverCapacity: 7);
        var before = new CustodySnapshot(Id(1), Id(2), 7, emptyReceiver.Storages,
            emptyReceiver.Lots.Concat([new CountedSourceLot(Id(7), Id(4), Wood,
                new SourceInterval(Id(8), 0, 2), 0)]));
        byte[] original = CustodyEncodingV1.Encode(before);
        AssertConflict(CustodyConflict.CapacityExceeded, () => _planner.Plan(before, Command(before)));
        Assert.Equal(original, CustodyEncodingV1.Encode(before));
        Assert.Equal(8UL, before.GetStoredQuantity(Id(3)));
        Assert.Equal(2UL, before.GetStoredQuantity(Id(4)));
    }

    [Fact]
    public void ExplicitTransferCarriesDesignationsOntoBothPartitions()
    {
        const LotProtection flags = LotProtection.Favorite | LotProtection.Heirloom | LotProtection.UserReserved;
        CustodySnapshot before = Create(protection: flags);
        CustodySnapshot after = _planner.Plan(before, Command(before)).Candidate;
        Assert.All(after.Lots, lot => Assert.Equal(flags, lot.Protection));
    }

    [Fact]
    public void EquippedOrJobReservedLotsRejectBeforeReplacement()
    {
        CustodySnapshot equipped = Create(protection: LotProtection.Equipped);
        CustodySnapshot reserved = Create(reservedBy: Id(80));
        AssertConflict(CustodyConflict.EquippedLot, () => _planner.Plan(equipped, Command(equipped)));
        AssertConflict(CustodyConflict.ResourceReserved, () => _planner.Plan(reserved, Command(reserved)));
        Assert.Equal(8UL, equipped.GetLot(Id(5)).Quantity);
        Assert.Equal(Id(80), reserved.GetLot(Id(5)).ReservedBy);
    }

    [Fact]
    public void InsufficientQuantityWrongScopeAndWrongCustodyReject()
    {
        CustodySnapshot before = Create();
        AssertConflict(CustodyConflict.InsufficientQuantity, () => _planner.Plan(before, Command(before, 9)));
        var otherBranch = new CustodySnapshot(Id(1), Id(99), 7, before.Storages, before.Lots);
        AssertConflict(CustodyConflict.WrongScope, () => _planner.Plan(otherBranch, Command(before)));
        CommandEnvelope command = Command(before);
        var reversed = new CommandEnvelope(command.CommandId, command.PrincipalId, command.OriginSessionId,
            command.ControlledActorId, command.WorldId, command.BranchId, command.ExpectedRevisions,
            new TransferLotIntent(Id(5), Id(4), Id(3), 6));
        AssertConflict(CustodyConflict.WrongCustodian, () => _planner.Plan(before, reversed));
    }

    [Fact]
    public void EveryRequiredRevisionMustBePresentAndCurrent()
    {
        CustodySnapshot before = Create();
        ExpectedRevision[] revisions = Command(before).ExpectedRevisions.ToArray();
        for (int index = 0; index < revisions.Length; index++)
        {
            int missing = index;
            AssertConflict(CustodyConflict.MissingRevision, () => _planner.Plan(before,
                Command(before, revisions: revisions.Where((_, at) => at != missing))));
            ExpectedRevision[] changed = revisions.ToArray();
            ExpectedRevision value = changed[index];
            changed[index] = new ExpectedRevision(value.TargetKind, value.TargetId, value.Revision + 1);
            AssertConflict(CustodyConflict.RevisionConflict, () => _planner.Plan(before,
                Command(before, revisions: changed)));
        }
    }

    [Fact]
    public void CompetingCandidatesAndSameRevisionDifferentStateCannotApply()
    {
        CustodySnapshot before = Create();
        CustodyTransferPlan first = _planner.Plan(before, Command(before));
        CustodyTransferPlan competing = _planner.Plan(before, Command(before, commandId: 30));
        AssertConflict(CustodyConflict.StalePlan, () => competing.ApplyTo(first.Candidate));
        AssertConflict(CustodyConflict.StalePlan, () => first.ApplyTo(first.Candidate));
        AssertConflict(CustodyConflict.StalePlan, () => first.ApplyTo(Create(receiverCapacity: 9)));
        Assert.Equal(first.Candidate.ContentHash, first.ApplyTo(before).ContentHash);
    }

    [Fact]
    public void AdditionalDeclaredRevisionsMustAlsoBeCurrentAndLocallyKnown()
    {
        CustodySnapshot original = Create();
        var before = new CustodySnapshot(Id(1), Id(2), 7,
            original.Storages.Concat([new CustodianStorage(Id(40), 1, 12)]), original.Lots);
        ExpectedRevision[] required = Command(before).ExpectedRevisions.ToArray();
        _planner.Plan(before, Command(before, revisions: required.Concat(
            [new ExpectedRevision(RevisionTargetKind.Entity, Id(40), 12)])));
        AssertConflict(CustodyConflict.RevisionConflict, () => _planner.Plan(before,
            Command(before, revisions: required.Concat([new ExpectedRevision(RevisionTargetKind.Entity, Id(40), 11)]))));
        AssertConflict(CustodyConflict.UnknownRevisionTarget, () => _planner.Plan(before,
            Command(before, revisions: required.Concat([new ExpectedRevision(RevisionTargetKind.Entity, Id(41), 0)]))));
    }

    [Fact]
    public void SplitChildCollisionWithKnownSourceOrDefinitionRejects()
    {
        CustodySnapshot original = Create();
        EntityId child = EntityId.DeriveSpawned(Id(20), 0);
        var sourceCollision = new CustodySnapshot(Id(1), Id(2), 7, original.Storages,
            [new CountedSourceLot(Id(5), Id(3), Wood, new SourceInterval(child, 10, 18), 3)]);
        AssertConflict(CustodyConflict.IdentityCollision, () => _planner.Plan(sourceCollision, Command(sourceCollision)));
        var definitionCollision = new CustodySnapshot(Id(1), Id(2), 7, original.Storages,
            [new CountedSourceLot(Id(5), Id(3), new CountedItemDefinition(child, Wood.Version),
                new SourceInterval(Id(6), 10, 18), 3)]);
        AssertConflict(CustodyConflict.IdentityCollision, () => _planner.Plan(definitionCollision, Command(definitionCollision)));
    }

    [Fact]
    public void SplitChildCollisionWithStorageOrExistingLotRejects()
    {
        CustodySnapshot before = Create();
        EntityId child = EntityId.DeriveSpawned(Id(20), 0);
        var storageCollision = new CustodySnapshot(Id(1), Id(2), 7,
            before.Storages.Concat([new CustodianStorage(child, 0, 0)]), before.Lots);
        AssertConflict(CustodyConflict.IdentityCollision, () => _planner.Plan(storageCollision, Command(storageCollision)));
        var lotCollision = new CustodySnapshot(Id(1), Id(2), 7, before.Storages,
            before.Lots.Concat([new CountedSourceLot(child, Id(4), Wood, new SourceInterval(Id(90), 0, 1), 0)]));
        AssertConflict(CustodyConflict.IdentityCollision, () => _planner.Plan(lotCollision, Command(lotCollision)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OverflowRejectsAndPreservesOriginal(bool branchOverflow)
    {
        CustodySnapshot before = branchOverflow ? Create(revision: ulong.MaxValue) : Create(lotRevision: ulong.MaxValue);
        byte[] original = CustodyEncodingV1.Encode(before);
        Assert.Throws<OverflowException>(() => _planner.Plan(before, Command(before)));
        Assert.Equal(original, CustodyEncodingV1.Encode(before));
    }

    private static void AssertConflict(CustodyConflict conflict, Action operation) =>
        Assert.Equal(conflict, Assert.Throws<CustodyConflictException>(operation).Conflict);

    [Fact]
    public void SuccessiveSplitsNearUnsignedBoundaryConserveDisjointSourceIntervals()
    {
        CustodySnapshot original = Create();
        var before = new CustodySnapshot(Id(1), Id(2), 7, original.Storages,
            [new CountedSourceLot(Id(5), Id(3), Wood,
                new SourceInterval(Id(6), ulong.MaxValue - 8, ulong.MaxValue), 3)]);
        CustodySnapshot first = _planner.Plan(before, Command(before)).Candidate;
        CustodySnapshot second = _planner.Plan(first, Command(first, quantity: 1, commandId: 30)).Candidate;
        Assert.Equal(7UL, second.GetStoredQuantity(Id(4)));
        Assert.Equal(1UL, second.GetStoredQuantity(Id(3)));
        CountedSourceLot[] intervals = second.Lots.OrderBy(lot => lot.Source.Start).ToArray();
        Assert.Equal(ulong.MaxValue - 8, intervals[0].Source.Start);
        Assert.Equal(ulong.MaxValue, intervals[^1].Source.End);
        for (int index = 1; index < intervals.Length; index++)
        {
            Assert.Equal(intervals[index - 1].Source.End, intervals[index].Source.Start);
        }
    }

    [Theory]
    [InlineData(3UL)]
    [InlineData(4UL)]
    public void StorageRevisionOverflowLeavesOriginalUnchanged(ulong owner)
    {
        CustodySnapshot original = Create();
        var before = new CustodySnapshot(Id(1), Id(2), 7,
            original.Storages.Select(storage => storage.CustodianId == Id(owner)
                ? storage with { Revision = ulong.MaxValue } : storage), original.Lots);
        byte[] bytes = CustodyEncodingV1.Encode(before);
        Assert.Throws<OverflowException>(() => _planner.Plan(before, Command(before)));
        Assert.Equal(bytes, CustodyEncodingV1.Encode(before));
    }
}
