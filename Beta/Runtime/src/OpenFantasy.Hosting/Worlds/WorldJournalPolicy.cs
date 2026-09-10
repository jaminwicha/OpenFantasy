namespace OpenFantasy.Hosting.Worlds;

/// <summary>Privileged storage/recovery diagnostics, never a player knowledge projection.</summary>
public sealed record WorldJournalUsage(ulong CommitCount, ulong CommandOutcomeCount, ulong AcceptedJournalBytes,
    ulong LastCheckpointSequence, ulong LastCheckpointSteps);

/// <summary>A visible host stop condition, never an actor gameplay rejection or recoverable dispatch collision.</summary>
public sealed class WorldRecoveryLimitException(string message) : InvalidOperationException(message);

/// <summary>Explicit development limits until measured checkpoint-tail recovery replaces full replay.</summary>
internal static class WorldJournalPolicy
{
    internal const ulong MaximumCommitCount = 250_000;
    internal const ulong MaximumCommandOutcomeCount = 65_536;
    internal const ulong MaximumAcceptedJournalBytes = 128 * 1024 * 1024;
    internal const ulong CheckpointStepInterval = 6_000;
    internal const ulong CheckpointCommitInterval = 10_000;

    internal static WorldJournalAdvance Advance(WorldJournalUsage current, ulong sequence, ulong steps,
        ulong commandCount, ulong batchBytes, ulong outcomeBytes)
    {
        ulong commands = checked(current.CommandOutcomeCount + commandCount);
        ulong bytes = checked(current.AcceptedJournalBytes + batchBytes + outcomeBytes);
        if (sequence > MaximumCommitCount || commands > MaximumCommandOutcomeCount || bytes > MaximumAcceptedJournalBytes)
        {
            throw new WorldRecoveryLimitException("The local world reached its development recovery envelope.");
        }
        bool checkpoint = checked(sequence - current.LastCheckpointSequence) >= CheckpointCommitInterval
            || checked(steps - current.LastCheckpointSteps) >= CheckpointStepInterval;
        return new WorldJournalAdvance(new WorldJournalUsage(sequence, commands, bytes,
            checkpoint ? sequence : current.LastCheckpointSequence,
            checkpoint ? steps : current.LastCheckpointSteps), checkpoint);
    }
}

internal sealed record WorldJournalAdvance(WorldJournalUsage Usage, bool WriteCheckpoint);
