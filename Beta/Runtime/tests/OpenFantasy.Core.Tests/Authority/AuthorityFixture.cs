using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Tests.Authority;

internal static class AuthorityFixture
{
    internal static EntityId Id(ulong value) => new(0, value);
    internal static readonly CountedItemDefinition Wood = new(Id(10), ContentHash.Compute("wood-source-v1"u8));
    internal static readonly TrustedCommandDelivery Delivery = new(Id(21), Id(22));

    internal static CountedAuthorityState Create(CustodyDepositRule depositRule = CustodyDepositRule.AnyControlledActor,
        ulong capacity = 8, LotProtection protection = LotProtection.None, EntityId? reservedBy = null,
        ulong revision = 7, IEnumerable<EntityId>? extraActors = null,
        IEnumerable<AdmittedCountedSource>? extraSources = null)
    {
        var custody = new CustodySnapshot(Id(1), Id(2), revision,
            [new(Id(3), 10, 4), new(Id(4), capacity, 5)],
            [new(Id(5), Id(3), Wood, new SourceInterval(Id(6), 10, 18), 3, protection, reservedBy)]);
        return new CountedAuthorityState(custody, 1000,
            new[] { Id(23), Id(24) }.Concat(extraActors ?? []),
            [new(Id(21), Id(22), Id(23))],
            [new(Id(3), Id(23), CustodyDepositRule.OwnerOnly), new(Id(4), Id(24), depositRule)],
            new[] { new AdmittedCountedSource(Id(6), Wood, 20) }.Concat(extraSources ?? []),
            reservedBy is { } reservation ? [reservation] : []);
    }

    internal static CommandEnvelope Command(CountedAuthorityState state, ulong quantity = 6,
        EntityId? principal = null, EntityId? session = null, EntityId? actor = null,
        EntityId? lot = null, EntityId? source = null, EntityId? destination = null,
        IEnumerable<ExpectedRevision>? revisions = null, EntityId? commandId = null)
    {
        return new CommandEnvelope(commandId ?? Id(20), principal ?? Id(21), session ?? Id(22), actor ?? Id(23),
            state.WorldId, state.BranchId,
            revisions ?? [new(RevisionTargetKind.Branch, state.BranchId, state.Custody.Revision),
                new(RevisionTargetKind.Entity, lot ?? Id(5), 3),
                new(RevisionTargetKind.Entity, source ?? Id(3), 4),
                new(RevisionTargetKind.Entity, destination ?? Id(4), 5)],
            new TransferLotIntent(lot ?? Id(5), source ?? Id(3), destination ?? Id(4), quantity));
    }

    internal static AuthorityDecision Decide(CountedAuthorityState state, CommandEnvelope? command = null,
        TrustedCommandDelivery? delivery = null) => new CountedTransferAuthority().Decide(state,
            command ?? Command(state), delivery ?? Delivery, new CommandCommitIdentity(state.WorldId, 11));
}
