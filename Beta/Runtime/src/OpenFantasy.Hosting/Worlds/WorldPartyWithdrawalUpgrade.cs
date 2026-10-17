using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Preserves the source and publishes a distinct destination only after complete transition replay.</summary>
internal sealed class WorldPartyWithdrawalUpgrade
{
    internal void Publish(string sourcePath, string destinationPath, WorldPartyWithdrawalTransition request,
        Action<WorldPartyWithdrawalTransitionBoundary>? fault)
    {
        ArgumentNullException.ThrowIfNull(request);
        var publication = new WorldUpgradePublication(sourcePath, destinationPath);
        using WorldDatabase original = WorldDatabase.Open(publication.SourcePath, create: false);
        if (original.SchemaVersion != WorldDatabaseSchemaTwelve.Version)
        {
            throw new InvalidDataException("The withdrawal source requires effective-control recovery storage.");
        }
        RecoveredWorld retained = new WorldJournalRecovery().Recover(original);
        if (retained.Genesis.ProfileVersion != WorldPartyWithdrawalTransition.SourceProfileVersion)
        {
            throw new InvalidDataException("Withdrawal transition must retain an effective-control genesis.");
        }
        var preparation = new WorldPartyWithdrawalTransitionPreparation();
        ExpectedJournalEntry transition = preparation.Prepare(retained.State, retained.Genesis.IdentityIndex,
            retained.StateHash, request, identity => original.ContainsIdentity(identity));
        WorldJournalAdvance advance = WorldJournalPolicy.Advance(retained.Usage, transition.Stored.Sequence,
            transition.Stored.AdvancingSteps, 0, (ulong)transition.Stored.AcceptedBatch.Length,
            (ulong)transition.Stored.Outcomes.Length, profileVersion: retained.State.ProfileVersion);
        var recovery = original.ReadControllerRecovery();
        string staging = publication.CreateStagingCopy(original, "party-withdrawal");
        fault?.Invoke(WorldPartyWithdrawalTransitionBoundary.AfterBackup);
        using (WorldDatabase candidate = WorldDatabase.Open(staging, create: false))
        {
            WorldUpgradePublication.RequireExactCopy(original, candidate, retained);
            RequireRecovery(recovery, candidate);
            candidate.AppendPartyWithdrawalTransition(transition, advance.WriteCheckpoint, fault);
            WorldUpgradePublication.RequireExactReplay(candidate, transition.Stored, transition.CandidateBytes, advance.Usage);
            RequireRecovery(recovery, candidate);
            candidate.CheckpointForPublication();
        }
        publication.Publish(() => fault?.Invoke(WorldPartyWithdrawalTransitionBoundary.BeforePublish),
            () => fault?.Invoke(WorldPartyWithdrawalTransitionBoundary.AfterPublish));
    }

    private static void RequireRecovery(ControllerRecoveryState expected, WorldDatabase candidate)
    {
        ControllerRecoveryState actual = candidate.ReadControllerRecovery();
        if (actual.Hash != expected.Hash
            || !ControllerRecoveryState.EncodeActivation(actual.Activation).AsSpan()
                .SequenceEqual(ControllerRecoveryState.EncodeActivation(expected.Activation)))
        {
            throw new InvalidDataException("The withdrawal copy changed controller recovery provenance or allowances.");
        }
    }
}
