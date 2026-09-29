using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedCustody;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Tests.Authority.ConsentedCustody;

/// <summary>Two controlled owners with OwnerOnly bags, one bystander and eight source-backed wood units.</summary>
internal static class ConsentedTransferFixture
{
    internal static EntityId Id(ulong value) => new(0, value);

    internal static readonly EntityId World = Id(1);
    internal static readonly EntityId Branch = Id(2);
    internal static readonly EntityId SenderStorage = Id(3);
    internal static readonly EntityId RecipientStorage = Id(4);
    internal static readonly EntityId Lot = Id(5);
    internal static readonly EntityId Source = Id(6);
    internal static readonly EntityId SenderSpareStorage = Id(7);
    internal static readonly EntityId BystanderStorage = Id(8);
    internal static readonly EntityId RecipientSpareStorage = Id(9);
    internal static readonly EntityId CommandId = Id(20);
    internal static readonly EntityId Sender = Id(23);
    internal static readonly EntityId Recipient = Id(24);
    internal static readonly EntityId Bystander = Id(25);
    internal static readonly EntityId AcceptanceId = Id(30);
    internal static readonly CountedItemDefinition Wood = new(Id(10), ContentHash.Compute("wood-source-v1"u8));
    internal static readonly TrustedCommandDelivery SenderDelivery = new(Id(21), Id(22));
    internal static readonly TrustedCommandDelivery RecipientDelivery = new(Id(31), Id(32));
    internal static readonly TrustedCommandDelivery BystanderDelivery = new(Id(41), Id(42));

    internal static CountedAuthorityState Create(ulong recipientCapacity = 8,
        LotProtection protection = LotProtection.None, EntityId? reservedBy = null, ulong branchRevision = 7,
        IEnumerable<ActorControlBinding>? controls = null, IEnumerable<EntityId>? extraActors = null)
    {
        var custody = new CustodySnapshot(World, Branch, branchRevision,
            [
                new(SenderStorage, 10, 4), new(RecipientStorage, recipientCapacity, 5),
                new(SenderSpareStorage, 10, 0), new(BystanderStorage, 10, 0), new(RecipientSpareStorage, 10, 0)
            ],
            [new(Lot, SenderStorage, Wood, new SourceInterval(Source, 10, 18), 3, protection, reservedBy)]);
        return new CountedAuthorityState(custody, 1000,
            new[] { Sender, Recipient, Bystander }.Concat(extraActors ?? []),
            controls ??
            [
                new(SenderDelivery.PrincipalId, SenderDelivery.SessionId, Sender),
                new(RecipientDelivery.PrincipalId, RecipientDelivery.SessionId, Recipient),
                new(BystanderDelivery.PrincipalId, BystanderDelivery.SessionId, Bystander)
            ],
            [
                new(SenderStorage, Sender, CustodyDepositRule.OwnerOnly),
                new(RecipientStorage, Recipient, CustodyDepositRule.OwnerOnly),
                new(SenderSpareStorage, Sender, CustodyDepositRule.OwnerOnly),
                new(BystanderStorage, Bystander, CustodyDepositRule.OwnerOnly),
                new(RecipientSpareStorage, Recipient, CustodyDepositRule.OwnerOnly)
            ],
            [new AdmittedCountedSource(Source, Wood, 20)],
            reservedBy is { } reservation ? [reservation] : []);
    }

    /// <summary>Terms a participant would capture by observing the supplied state, with explicit overrides.</summary>
    internal static ConsentedTransferTerms Terms(CountedAuthorityState state, ulong quantity = 6,
        EntityId? world = null, EntityId? branch = null, EntityId? sender = null, EntityId? recipient = null,
        EntityId? lot = null, EntityId? source = null, EntityId? destination = null, ulong? branchRevision = null,
        ulong? lotRevision = null, ulong? sourceRevision = null, ulong? destinationRevision = null)
    {
        EntityId lotId = lot ?? Lot;
        EntityId sourceId = source ?? SenderStorage;
        EntityId destinationId = destination ?? RecipientStorage;
        return new ConsentedTransferTerms(world ?? state.WorldId, branch ?? state.BranchId, sender ?? Sender,
            recipient ?? Recipient, lotId, sourceId, destinationId, quantity,
            branchRevision ?? state.Custody.Revision,
            lotRevision ?? state.Custody.Lots.FirstOrDefault(value => value.LotId == lotId)?.Revision ?? 0UL,
            sourceRevision ?? StorageRevision(state, sourceId),
            destinationRevision ?? StorageRevision(state, destinationId));
    }

    internal static ConsentedTransferCommand Command(ConsentedTransferTerms terms, EntityId? commandId = null,
        EntityId? principal = null, EntityId? session = null) => new(commandId ?? CommandId,
        principal ?? SenderDelivery.PrincipalId, session ?? SenderDelivery.SessionId, terms);

    internal static ConsentedTransferAcceptance Acceptance(ConsentedTransferCommand command,
        EntityId? acceptanceId = null, EntityId? principal = null, EntityId? session = null) =>
        ConsentedTransferAcceptance.Approving(command, acceptanceId ?? AcceptanceId,
            principal ?? RecipientDelivery.PrincipalId, session ?? RecipientDelivery.SessionId);

    internal static ConsentedTransferDecision Decide(CountedAuthorityState state, ConsentedTransferCommand command,
        ConsentedTransferAcceptance acceptance, TrustedCommandDelivery? senderDelivery = null,
        TrustedCommandDelivery? recipientDelivery = null, Func<EntityId, bool>? history = null) =>
        new ConsentedTransferAuthority().Decide(state, new(command, senderDelivery ?? SenderDelivery),
            new(acceptance, recipientDelivery ?? RecipientDelivery), new CommandCommitIdentity(state.WorldId, 11),
            history ?? NothingRetired);

    internal static bool NothingRetired(EntityId identity) => false;

    private static ulong StorageRevision(CountedAuthorityState state, EntityId custodianId) =>
        state.Custody.Storages.FirstOrDefault(value => value.CustodianId == custodianId)?.Revision ?? 0UL;
}
