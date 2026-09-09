using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;

namespace OpenFantasy.Hosting;

/// <summary>Replays committed decisions before exposing a reopened world to any caller.</summary>
internal sealed class AuthorityJournalRecovery
{
    internal RecoveredAuthority Recover(SqliteAuthorityDatabase database)
    {
        CountedAuthorityState genesis = AuthorityEncodingV1.Decode(database.ReadGenesis());
        CountedAuthorityState state = genesis;
        ulong sequence = 0;
        Dictionary<EntityId, StoredIdentity> identities = [];
        var preparation = new AuthorityCommitPreparation();
        foreach (StoredAuthorityCommit stored in database.ReadJournal())
        {
            if (stored.Sequence != checked(sequence + 1) || stored.BeforeHash != state.ContentHash)
            {
                throw new InvalidDataException("The command journal is discontinuous.");
            }

            CommandEnvelope command = CommandCodecV1.Decode(stored.Command);
            var delivery = new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId);
            if (command.PrincipalId != stored.PrincipalId || command.CommandId != stored.CommandId
                || command.WorldId != state.WorldId || !state.CanControl(delivery, command.ControlledActorId))
            {
                throw new InvalidDataException("The command journal contains an invalid dispatch.");
            }

            PreparedAuthorityCommit expected = preparation.Prepare(state, genesis, command,
                delivery, stored.Sequence, identities.ContainsKey);
            if (!expected.Stored.Command.AsSpan().SequenceEqual(stored.Command)
                || !expected.Stored.Receipt.AsSpan().SequenceEqual(stored.Receipt)
                || !expected.Stored.AfterState.AsSpan().SequenceEqual(stored.AfterState))
            {
                throw new InvalidDataException("A stored decision does not match authority replay.");
            }

            foreach (StoredIdentity identity in expected.Identities)
            {
                identities.Add(identity.Identity, identity);
            }
            state = expected.State;
            sequence = stored.Sequence;
        }

        StoredAuthorityHead head = database.ReadHead();
        if (head.Sequence != sequence || !head.State.AsSpan().SequenceEqual(AuthorityEncodingV1.Encode(state)))
        {
            throw new InvalidDataException("The current state does not match the committed journal.");
        }

        int count = 0;
        foreach (StoredIdentity stored in database.ReadIdentities())
        {
            if (!identities.TryGetValue(stored.Identity, out StoredIdentity? expected) || expected != stored)
            {
                throw new InvalidDataException("The retained identity registry does not match the journal.");
            }
            count++;
        }
        if (count != identities.Count)
        {
            throw new InvalidDataException("The retained identity registry is incomplete.");
        }

        return new RecoveredAuthority(genesis, state, sequence);
    }
}

internal sealed record RecoveredAuthority(CountedAuthorityState Genesis, CountedAuthorityState State, ulong Sequence);
