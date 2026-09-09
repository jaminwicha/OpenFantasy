using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;

namespace OpenFantasy.Hosting;

/// <summary>Combines pure decisions with identities retained across the entire journal.</summary>
internal sealed class AuthorityCommitPreparation
{
    private readonly CountedTransferAuthority _authority = new();

    internal PreparedAuthorityCommit Prepare(CountedAuthorityState state, CountedAuthorityState genesis,
        CommandEnvelope command, TrustedCommandDelivery delivery, ulong sequence, Func<EntityId, bool> isOccupied)
    {
        bool Unavailable(EntityId identity) => state.ContainsIdentity(identity)
            || genesis.ContainsIdentity(identity) || isOccupied(identity);

        // Identity collisions are dispatch failures. A new command ID is required; no conflicting
        // identity can safely be reserved as the durable cause of a rejection either.
        if (Unavailable(command.CommandId))
        {
            throw new AuthorityDispatchException("The command identity is unavailable.");
        }

        var commit = new CommandCommitIdentity(state.WorldId, sequence);
        AuthorityDecision decision = _authority.Decide(state, command, delivery, commit);
        CountedAuthorityState candidate = decision.Candidate;
        CommandReceipt receipt = decision.Receipt;
        EntityId? createdLot = decision.MovedLotId is { } moved && moved != command.Payload.LotId ? moved : null;
        if (createdLot is { } child && (Unavailable(child) || child == command.CommandId))
        {
            candidate = state;
            createdLot = null;
            receipt = CommandReceipt.CreateRejected(command, commit, CommandRejectionReason.ResourceUnavailable);
        }

        if (Unavailable(receipt.ResultId) || receipt.ResultId == command.CommandId || receipt.ResultId == createdLot)
        {
            throw new AuthorityDispatchException("The result identity is unavailable.");
        }

        List<StoredIdentity> identities = [new(command.CommandId, sequence, 1), new(receipt.ResultId, sequence, 2)];
        if (createdLot is { } lotId)
        {
            identities.Add(new StoredIdentity(lotId, sequence, 3));
        }

        return new PreparedAuthorityCommit(candidate, receipt,
            new StoredAuthorityCommit(sequence, command.PrincipalId, command.CommandId,
                CommandCodecV1.Encode(command), CommandReceiptCodecV1.Encode(receipt),
                state.ContentHash, AuthorityEncodingV1.Encode(candidate)), identities);
    }
}

internal sealed record PreparedAuthorityCommit(CountedAuthorityState State, CommandReceipt Receipt,
    StoredAuthorityCommit Stored, IReadOnlyList<StoredIdentity> Identities);
