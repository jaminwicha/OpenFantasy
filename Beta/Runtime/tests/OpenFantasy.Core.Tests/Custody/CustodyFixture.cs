using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Tests.Custody;

internal static class CustodyFixture
{
    internal static EntityId Id(ulong value) => new(0, value);
    internal static readonly CountedItemDefinition Wood = new(Id(10), ContentHash.Compute("wood-source-v1"u8));

    internal static CustodySnapshot Create(ulong receiverCapacity = 8, ulong revision = 7,
        ulong lotRevision = 3, LotProtection protection = LotProtection.None, EntityId? reservedBy = null) =>
        new(Id(1), Id(2), revision,
            [new(Id(3), 10, 4), new(Id(4), receiverCapacity, 5)],
            [new(Id(5), Id(3), Wood, new SourceInterval(Id(6), 10, 18), lotRevision, protection, reservedBy)]);

    internal static CommandEnvelope Command(CustodySnapshot snapshot, ulong quantity = 6,
        ulong commandId = 20, IEnumerable<ExpectedRevision>? revisions = null)
    {
        CountedSourceLot lot = snapshot.GetLot(Id(5));
        return new CommandEnvelope(Id(commandId), Id(21), Id(22), Id(23), snapshot.WorldId, snapshot.BranchId,
            revisions ?? [new(RevisionTargetKind.Branch, snapshot.BranchId, snapshot.Revision),
                new(RevisionTargetKind.Entity, lot.LotId, lot.Revision),
                new(RevisionTargetKind.Entity, Id(3), snapshot.GetStorage(Id(3)).Revision),
                new(RevisionTargetKind.Entity, Id(4), snapshot.GetStorage(Id(4)).Revision)],
            new TransferLotIntent(lot.LotId, Id(3), Id(4), quantity));
    }
}
