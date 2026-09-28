using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class StructuralMovementAdmissionTests
{
    [Theory]
    [InlineData(false, "accepted")]
    [InlineData(true, "accepted")]
    [InlineData(true, "unauthorized")]
    [InlineData(true, "unavailable")]
    [InlineData(true, "overloaded")]
    public void Later_movement_preserves_every_preceding_accepted_pose_and_one_time_boundary(
        bool eventOrdered, string laterOutcome)
    {
        var fixture = new CoupledSettlementWorldFixture(eventOrdered: eventOrdered);
        WorldSnapshot before = fixture.Genesis;
        ActorPresence first = before.Space.Actors[0];
        ActorPresence second = before.Space.Actors[1];
        long secondX = 0;
        long secondY = WorldMovementInput.DisplacementPerStep;

        if (laterOutcome is "unavailable" or "overloaded")
        {
            // These are separately authored fresh roots, not a claimed teleport or policy change
            // through authority. Each starting root still passes complete structural admission.
            long width = second.Condition.Definition.ClearanceDimensions.WidthSubunits;
            long centerX;
            ConstructionLoadPolicy policy = before.StructuralPolicy!;
            if (laterOutcome == "unavailable")
            {
                TerrainRegionBounds bounds = before.Space.Regions.Single(region => region.Bounds.Contains(second.Feet.Cell)).Bounds;
                centerX = checked((bounds.Minimum.X + bounds.Width) * 256 - (width - width / 2));
                secondX = WorldMovementInput.DisplacementPerStep;
            }
            else
            {
                centerX = checked(second.Feet.Cell.X * 256 + width / 2);
                secondX = -WorldMovementInput.DisplacementPerStep;
                CellCoordinate weakCell = second.Feet.Cell.Offset(-1, 0, -1);
                Assert.Single(policy.Foundations, anchor => anchor.Cell == weakCell);
                policy = new(policy.Scope, policy.Templates, policy.Foundations.Select(anchor => anchor.Cell == weakCell
                    ? new StableTerrainBearing(anchor.ElementId, anchor.Cell, anchor.SourceId, anchor.Material, new(1)) : anchor),
                    policy.Masses);
            }
            secondY = 0;
            second = At(second, new(centerX, second.Feet.Y, second.Feet.Z), second.Revision);
            before = Reconstruct(before, before.Space.WithActors([first, second]), policy: policy);
        }

        ActorPresence expectedFirst = Moved(first, 0, WorldMovementInput.DisplacementPerStep);
        LocalSpatialSnapshot firstSpace = before.Space.WithActors([expectedFirst, second]);
        Assert.Equal(SpatialMoveStatus.Prepared,
            before.Space.PrepareMove(first.ActorId, first.Revision, 0, WorldMovementInput.DisplacementPerStep).Status);
        SpatialMoveResult later = firstSpace.PrepareMove(second.ActorId, second.Revision, secondX, secondY);
        if (laterOutcome == "unavailable")
        {
            Assert.Equal(SpatialMoveStatus.Unavailable, later.Status);
        }
        else
        {
            Assert.Equal(SpatialMoveStatus.Prepared, later.Status);
        }
        if (laterOutcome == "overloaded")
        {
            LocalSpatialSnapshot geometricallyValid = firstSpace.WithActors([expectedFirst, Moved(second, secondX, secondY)]);
            ArgumentException refusal = Assert.Throws<ArgumentException>(() => Reconstruct(before, geometricallyValid, advance: true));
            Assert.Contains("stable complete member, actor and carried loads", refusal.Message);
        }

        TrustedCommandDelivery laterDelivery = ConstructionUpgradeFixture.Delivery(before,
            laterOutcome == "unauthorized" ? first.ActorId : second.ActorId);
        if (laterOutcome == "unauthorized")
        {
            Assert.False(before.Inventory.CanControl(laterDelivery, second.ActorId));
        }
        WorldStepBatch batch = fixture.Actions.Batch(before, [],
            [new(laterDelivery, second.ActorId, secondX, secondY),
             new(ConstructionUpgradeFixture.Delivery(before, first.ActorId), first.ActorId, 0, WorldMovementInput.DisplacementPerStep)]);
        Assert.Equal(new[] { first.ActorId, second.ActorId }, batch.Movement.Select(input => input.ActorId));
        ActorPresence expectedSecond = laterOutcome == "accepted" ? Moved(second, secondX, secondY) : second;
        WorldSnapshot expected = Reconstruct(before, before.Space.WithActors([expectedFirst, expectedSecond]), advance: true);

        using var files = new StorageFiles();
        using SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, before);
        WorldStepDecision committed;
        if (laterOutcome == "unauthorized")
        {
            // The pure interpreter ignores a foreign movement sample. Hosting rejects that
            // entire untrusted selection before dispatch, so it must not publish even the first move.
            committed = new WorldStepProcessor().Decide(before, batch);
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(batch));
            Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(authority.InspectForHost().State));
        }
        else
        {
            committed = authority.Commit(batch);
        }
        Assert.Empty(committed.Receipts);
        WorldCommittedIdentity cause = Assert.Single(committed.Identities);
        Assert.Equal(batch.CauseId, cause.Identity);
        Assert.Equal(WorldCommittedIdentityKind.CommitCause, cause.Kind);
        AssertExactAdvance(before, expected, committed.Candidate);
        var frame = Assert.IsType<WorldCanonicalFrame>(committed.CandidateFrame);
        Assert.True(frame.IsBoundTo(committed.Candidate));
        Assert.Equal(WorldSnapshotCodec.Encode(expected), frame.ToArray());
    }

    [Fact]
    public void Accepted_material_transfer_then_recipient_movement_keeps_exact_custody_outcome_and_recovery()
    {
        var fixture = new CoupledSettlementWorldFixture(eventOrdered: true);
        WorldSnapshot before = fixture.Genesis;
        ActorPresence giver = before.Space.GetActor(fixture.Player);
        ActorPresence recipient = before.Space.GetActor(fixture.Builder);
        Assert.Equal(SpatialInteraction.Reachable, before.Space.QueryInteraction(giver.ActorId, recipient.ActorId));
        EntityId source = before.GetResident(giver.ActorId).InventoryCustodianId;
        EntityId destination = before.GetResident(recipient.ActorId).InventoryCustodianId;
        CountedSourceLot lot = before.Inventory.Custody.Lots.First(value => value.CustodianId == source
            && value.Definition == before.Quests!.Definitions[0].Item && value.Quantity > 1
            && value.ReservedBy is null && value.Protection == LotProtection.None);
        TrustedCommandDelivery delivery = ConstructionUpgradeFixture.Delivery(before, giver.ActorId);
        var command = new WorldActionCommand(fixture.Actions.NextIdentity(), delivery.PrincipalId, delivery.SessionId,
            giver.ActorId, before.WorldId, before.BranchId, giver.Revision,
            new GiveItemIntent(recipient.ActorId, recipient.Revision, lot.LotId, lot.Revision, source,
                before.Inventory.Custody.GetStorage(source).Revision, 1));
        EntityId child = EntityId.DeriveSpawned(command.CommandId, 0);

        // Construct the expected one-unit source partition directly, independently of the world
        // processor and custody planner. No new source entitlement is introduced by this gift.
        var expectedLots = before.Inventory.Custody.Lots.Where(value => value.LotId != lot.LotId)
            .Append(new CountedSourceLot(lot.LotId, source, lot.Definition,
                new(lot.Source.SourceId, checked(lot.Source.Start + 1), lot.Source.End), checked(lot.Revision + 1)))
            .Append(new CountedSourceLot(child, destination, lot.Definition,
                new(lot.Source.SourceId, lot.Source.Start, checked(lot.Source.Start + 1)), 0));
        var custody = new CustodySnapshot(before.WorldId, before.BranchId, checked(before.Inventory.Custody.Revision + 1),
            before.Inventory.Custody.Storages.Select(storage => storage.CustodianId == source || storage.CustodianId == destination
                ? storage with { Revision = checked(storage.Revision + 1) } : storage), expectedLots);
        var inventory = new CountedAuthorityState(custody, before.Inventory.AdmittedUnitCapacity,
            before.Inventory.Actors, before.Inventory.Controls, before.Inventory.Policies,
            before.Inventory.Sources, before.Inventory.ReservationIds);
        ActorPresence movedRecipient = Moved(recipient, 0, WorldMovementInput.DisplacementPerStep);
        Assert.Equal(SpatialMoveStatus.Prepared,
            before.Space.PrepareMove(recipient.ActorId, recipient.Revision, 0, WorldMovementInput.DisplacementPerStep).Status);
        WorldSnapshot expected = Reconstruct(before, before.Space.WithActors([giver, movedRecipient]), advance: true, inventory: inventory);
        WorldStepBatch batch = fixture.Actions.Batch(before, [command],
            [new(ConstructionUpgradeFixture.Delivery(before, recipient.ActorId), recipient.ActorId, 0, WorldMovementInput.DisplacementPerStep)]);

        using var files = new StorageFiles();
        byte[] receiptBytes;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, before))
        {
            WorldStepDecision committed = authority.Commit(batch);
            AssertExactAdvance(before, expected, committed.Candidate);
            WorldActionReceipt receipt = Assert.Single(committed.Receipts);
            Assert.Equal(WorldActionRejection.None, receipt.Rejection);
            Assert.Equal(command.CommandId, receipt.CommandId);
            Assert.Equal(command.ContentHash, receipt.CommandHash);
            Assert.Equal(expected.Revision, receipt.CommitSequence);
            receiptBytes = WorldReceiptCodecV1.Encode(receipt);
            Assert.Equal(4, committed.Identities.Count);
            Assert.Contains(committed.Identities, value => value.Identity == batch.CauseId && value.Kind == WorldCommittedIdentityKind.CommitCause);
            Assert.Contains(committed.Identities, value => value.Identity == command.CommandId && value.Kind == WorldCommittedIdentityKind.Command);
            Assert.Contains(committed.Identities, value => value.Identity == receipt.ResultId && value.Kind == WorldCommittedIdentityKind.Result);
            Assert.Contains(committed.Identities, value => value.Identity == child && value.Kind == WorldCommittedIdentityKind.SpawnedEntity);
        }

        byte[] closedBytes = File.ReadAllBytes(files.Path);
        using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
            Assert.Equal(receiptBytes, WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(reopened.FindReceipt(command, delivery))));
            Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost(before.Revision).State));
            Assert.Throws<AuthorityDispatchException>(() => reopened.Commit(batch));
            Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
        }
        Assert.Equal(closedBytes, File.ReadAllBytes(files.Path));
    }

    private static ActorPresence Moved(ActorPresence actor, long dx, long dy) =>
        At(actor, actor.Feet.Offset(dx, dy, 0), checked(actor.Revision + 1));

    private static ActorPresence At(ActorPresence actor, SubunitPosition feet, ulong revision) =>
        new(actor.Binding, actor.Condition, actor.Scope, feet, revision, actor.HomePlaceId);

    private static WorldSnapshot Reconstruct(WorldSnapshot before, LocalSpatialSnapshot space, bool advance = false,
        CountedAuthorityState? inventory = null, ConstructionLoadPolicy? policy = null)
    {
        ulong steps = advance ? checked(before.AdvancingSteps + 1) : before.AdvancingSteps;
        WorldResident[] residents = before.Residents.Select(resident => advance
            ? new WorldResident(resident.ActorId, resident.InventoryCustodianId, resident.DisplayName,
                resident.ControllerKind, resident.HomeFeet, resident.WanderRadiusCells, resident.Comprehension,
                resident.ExperiencedTime.Add(LocalExposureDuration.FromMilliseconds(1800)), resident.OpeningUtterance)
            : resident).ToArray();
        return new(inventory ?? before.Inventory, space, residents, before.AdmissionVersion,
            advance ? before.Calendar.AddMilliseconds(1800) : before.Calendar,
            advance ? checked(before.Revision + 1) : before.Revision, steps, before.Dialogues,
            advance ? before.Gathering!.AdvanceStep(steps) : before.Gathering, before.Production,
            before.Progression, before.Quests, before.Construction, excavation: before.Excavation,
            constructionPhysical: before.ConstructionPhysical, structuralPolicy: policy ?? before.StructuralPolicy,
            structuralSettlementPolicy: before.StructuralSettlementPolicy, consequencePolicy: before.ConsequencePolicy,
            coupledSettlementPolicy: before.CoupledSettlementPolicy, descentMode: before.DescentMode);
    }

    private static void AssertExactAdvance(WorldSnapshot before, WorldSnapshot expected, WorldSnapshot actual)
    {
        Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(actual));
        Assert.Equal(checked(before.Revision + 1), actual.Revision);
        Assert.Equal(checked(before.AdvancingSteps + 1), actual.AdvancingSteps);
        Assert.Equal(before.Calendar.AddMilliseconds(1800), actual.Calendar);
        foreach (WorldResident resident in before.Residents)
        {
            Assert.Equal(resident.ExperiencedTime.TotalMilliseconds + 1800,
                actual.GetResident(resident.ActorId).ExperiencedTime.TotalMilliseconds);
        }
        Assert.Equal(before.Gathering!.AdvanceStep(actual.AdvancingSteps).ContentHash, actual.Gathering!.ContentHash);
    }
}
