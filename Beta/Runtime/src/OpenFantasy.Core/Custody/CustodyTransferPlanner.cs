using OpenFantasy.Core.Commands;

namespace OpenFantasy.Core.Custody;

/// <summary>Plans one explicit branch-local counted-lot transfer against complete finite inputs.</summary>
public sealed class CustodyTransferPlanner
{
    // This command payload allocates at most one child; further spawning needs a new contract.
    public const ulong SplitChildOrdinal = 0;

    public CustodyTransferPlan Plan(CustodySnapshot snapshot, CommandEnvelope command)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(command);
        if (command.WorldId != snapshot.WorldId || command.BranchId != snapshot.BranchId)
        {
            throw new CustodyConflictException(CustodyConflict.WrongScope);
        }
        ValidateDeclaredRevisions(snapshot, command);
        RequireRevision(command, RevisionTargetKind.Branch, snapshot.BranchId, snapshot.Revision);
        TransferLotIntent intent = command.Payload;
        CountedSourceLot lot = snapshot.GetLot(intent.LotId);
        CustodianStorage source = snapshot.GetStorage(intent.SourceCustodianId);
        CustodianStorage destination = snapshot.GetStorage(intent.DestinationCustodianId);
        RequireRevision(command, RevisionTargetKind.Entity, lot.LotId, lot.Revision);
        RequireRevision(command, RevisionTargetKind.Entity, source.CustodianId, source.Revision);
        RequireRevision(command, RevisionTargetKind.Entity, destination.CustodianId, destination.Revision);
        if (lot.CustodianId != source.CustodianId)
        {
            throw new CustodyConflictException(CustodyConflict.WrongCustodian);
        }
        if (lot.ReservedBy is not null)
        {
            throw new CustodyConflictException(CustodyConflict.ResourceReserved);
        }
        if ((lot.Protection & LotProtection.Equipped) != 0)
        {
            throw new CustodyConflictException(CustodyConflict.EquippedLot);
        }
        if (intent.Quantity > lot.Quantity)
        {
            throw new CustodyConflictException(CustodyConflict.InsufficientQuantity);
        }
        ulong availableCapacity = destination.UnitCapacity - snapshot.GetStoredQuantity(destination.CustodianId);
        if (intent.Quantity > availableCapacity)
        {
            throw new CustodyConflictException(CustodyConflict.CapacityExceeded);
        }

        var lots = snapshot.Lots.Where(existing => existing.LotId != lot.LotId).ToList();
        EntityId movedId = lot.LotId;
        ulong movedRevision = checked(lot.Revision + 1);
        SourceInterval movedSource = lot.Source;
        if (intent.Quantity < lot.Quantity)
        {
            movedId = EntityId.DeriveSpawned(command.CommandId, SplitChildOrdinal);
            if (snapshot.ContainsEntity(movedId) || movedId == command.PrincipalId
                || movedId == command.ControlledActorId || movedId == command.OriginSessionId
                || movedId == command.CommandId)
            {
                throw new CustodyConflictException(CustodyConflict.IdentityCollision);
            }
            ulong boundary = checked(lot.Source.Start + intent.Quantity);
            movedSource = new SourceInterval(lot.Source.SourceId, lot.Source.Start, boundary);
            lots.Add(new CountedSourceLot(lot.LotId, source.CustodianId, lot.Definition,
                new SourceInterval(lot.Source.SourceId, boundary, lot.Source.End),
                movedRevision, lot.Protection, lot.ReservedBy));
            movedRevision = 0;
        }
        lots.Add(new CountedSourceLot(movedId, destination.CustodianId, lot.Definition,
            movedSource, movedRevision, lot.Protection, lot.ReservedBy));
        var storages = snapshot.Storages.Select(storage =>
            storage.CustodianId == source.CustodianId || storage.CustodianId == destination.CustodianId
                ? storage with { Revision = checked(storage.Revision + 1) } : storage);
        var candidate = new CustodySnapshot(snapshot.WorldId, snapshot.BranchId,
            checked(snapshot.Revision + 1), storages, lots);
        return new CustodyTransferPlan(snapshot, candidate, movedId);
    }

    private static void ValidateDeclaredRevisions(CustodySnapshot snapshot, CommandEnvelope command)
    {
        foreach (ExpectedRevision expected in command.ExpectedRevisions)
        {
            ulong? current = expected.TargetKind == RevisionTargetKind.Branch
                ? snapshot.Revision
                : snapshot.Lots.FirstOrDefault(lot => lot.LotId == expected.TargetId)?.Revision
                    ?? snapshot.Storages.FirstOrDefault(storage => storage.CustodianId == expected.TargetId)?.Revision;
            if (current is null)
            {
                throw new CustodyConflictException(CustodyConflict.UnknownRevisionTarget);
            }
            if (current != expected.Revision)
            {
                throw new CustodyConflictException(CustodyConflict.RevisionConflict);
            }
        }
    }

    private static void RequireRevision(CommandEnvelope command, RevisionTargetKind kind, EntityId target, ulong current)
    {
        ExpectedRevision? expected = command.ExpectedRevisions.FirstOrDefault(value =>
            value.TargetKind == kind && value.TargetId == target);
        if (expected is null)
        {
            throw new CustodyConflictException(CustodyConflict.MissingRevision);
        }
        if (expected.Revision != current)
        {
            throw new CustodyConflictException(CustodyConflict.RevisionConflict);
        }
    }
}
