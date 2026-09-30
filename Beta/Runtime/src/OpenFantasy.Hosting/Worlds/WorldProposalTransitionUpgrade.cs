using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>
/// Copies a lived coordinated schema-eight world, appends one explicit proposal transition to the
/// verified copy, and publishes the unused destination only after complete authenticated replay.
/// </summary>
internal sealed class WorldProposalTransitionUpgrade
{
    internal void Publish(string sourcePath, string destinationPath, WorldProposalTransition request,
        Action<WorldProposalTransitionBoundary>? fault)
    {
        ArgumentNullException.ThrowIfNull(request);
        var publication = new WorldUpgradePublication(sourcePath, destinationPath);
        // Opening holds the exclusive source lease until the verified copy is published.
        using WorldDatabase original = WorldDatabase.Open(publication.SourcePath, create: false);
        if (original.SchemaVersion != WorldDatabaseSchemaEight.Version)
        {
            throw new InvalidDataException("The proposal transition source must retain the coordinated archive schema.");
        }
        RecoveredWorld retained = new WorldJournalRecovery().Recover(original);
        if (retained.Genesis.ProfileVersion != WorldProposalTransition.SourceProfileVersion)
        {
            throw new InvalidDataException("Only a world whose genesis selected the coordinated interpretation can transition.");
        }
        PreparedWorldProposalTransition transition = new WorldProposalTransitionPreparation().Prepare(retained.State,
            retained.Genesis.IdentityIndex, retained.StateHash, request, identity => original.ContainsIdentity(identity));
        WorldJournalAdvance advance = WorldJournalPolicy.Advance(retained.Usage, transition.Stored.Sequence,
            transition.Stored.AdvancingSteps, 0, (ulong)transition.Stored.AcceptedBatch.Length,
            (ulong)transition.Stored.Outcomes.Length, null, retained.State.ProfileVersion, 0,
            (ulong)transition.Archive.State.Length);

        string staging = publication.CreateStagingCopy(original, "proposal");
        fault?.Invoke(WorldProposalTransitionBoundary.AfterBackup);
        using (WorldDatabase candidate = WorldDatabase.Open(staging, create: false))
        {
            WorldUpgradePublication.RequireExactCopy(original, candidate, retained);
            candidate.AppendProposalTransition(transition, advance.WriteCheckpoint, fault);
            WorldUpgradePublication.RequireExactReplay(candidate, transition.Stored, transition.CandidateBytes, advance.Usage);
            candidate.CheckpointForPublication();
        }
        publication.Publish(() => fault?.Invoke(WorldProposalTransitionBoundary.BeforePublish),
            () => fault?.Invoke(WorldProposalTransitionBoundary.AfterPublish));
    }
}
