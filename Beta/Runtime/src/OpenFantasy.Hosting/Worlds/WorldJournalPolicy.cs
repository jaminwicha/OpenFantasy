using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Privileged storage/recovery diagnostics, never a player knowledge projection.</summary>
public sealed record WorldJournalUsage(ulong CommitCount, ulong CommandOutcomeCount, ulong AcceptedJournalBytes,
    ulong LastCheckpointSequence, ulong LastCheckpointSteps,
    ulong ResourceArchiveCount = 0, ulong ResourceArchiveBytes = 0,
    ulong MaterialCareArchiveCount = 0, ulong MaterialCareArchiveBytes = 0);

/// <summary>A visible host stop condition, never an actor gameplay rejection or recoverable dispatch collision.</summary>
public sealed class WorldRecoveryLimitException(string message) : InvalidOperationException(message);

/// <summary>Explicit development limits until measured checkpoint-tail recovery replaces full replay.</summary>
internal static class WorldJournalPolicy
{
    internal const ulong MaximumCommitCount = 250_000;
    internal const ulong MaximumCommandOutcomeCount = 65_536;
    internal const ulong MaximumAcceptedJournalBytes = 128 * 1024 * 1024;
    internal const ulong MaximumResourceArchiveCount = 1_024;
    internal const ulong MaximumResourceArchiveBytes = 256 * 1024 * 1024;
    internal const ulong MaximumMaterialCareArchiveCount = 1_024;
    internal const ulong MaximumMaterialCareArchiveBytes = 256 * 1024 * 1024;
    internal const ulong CheckpointStepInterval = 6_000;
    internal const ulong CheckpointCommitInterval = 10_000;

    internal static WorldJournalAdvance Advance(WorldJournalUsage current, ulong sequence, ulong steps,
        ulong commandCount, ulong batchBytes, ulong outcomeBytes, PendingExcavation? newlyPending = null,
        ushort profileVersion = 16, ulong resourceArchiveBytes = 0, ulong materialCareArchiveBytes = 0)
    {
        // Only the named coordinated, consented and saved-proposal interpretations close joint history.
        bool jointArchives = profileVersion is 27 or 28 or 30;
        if (!jointArchives && (materialCareArchiveBytes != 0
                || current.MaterialCareArchiveCount != 0 || current.MaterialCareArchiveBytes != 0)
            || jointArchives && (resourceArchiveBytes != 0
                || current.ResourceArchiveCount != 0 || current.ResourceArchiveBytes != 0))
        {
            throw new InvalidDataException("World archive accounting must retain its explicit profile interpretation.");
        }
        ulong commands = checked(current.CommandOutcomeCount + commandCount);
        ulong bytes = checked(current.AcceptedJournalBytes + batchBytes + outcomeBytes);
        ulong archives = checked(current.ResourceArchiveCount + (resourceArchiveBytes == 0 ? 0UL : 1UL));
        ulong archiveBytes = checked(current.ResourceArchiveBytes + resourceArchiveBytes);
        ulong materialArchives = checked(current.MaterialCareArchiveCount + (materialCareArchiveBytes == 0 ? 0UL : 1UL));
        ulong materialBytes = checked(current.MaterialCareArchiveBytes + materialCareArchiveBytes);
        if (sequence > MaximumCommitCount || commands > MaximumCommandOutcomeCount || bytes > MaximumAcceptedJournalBytes)
        {
            throw new WorldRecoveryLimitException("The local world reached its development recovery envelope.");
        }
        if (archives > MaximumResourceArchiveCount || archiveBytes > MaximumResourceArchiveBytes)
        {
            throw new WorldRecoveryLimitException("The local world reached its development resource archive envelope.");
        }
        if (materialArchives > MaximumMaterialCareArchiveCount || materialBytes > MaximumMaterialCareArchiveBytes)
        {
            throw new WorldRecoveryLimitException("The local world reached its development material and care archive envelope.");
        }
        if (newlyPending is not null)
        {
            RequireCompletionCapacity(newlyPending, sequence, steps, commands, bytes, profileVersion);
        }
        bool checkpoint = checked(sequence - current.LastCheckpointSequence) >= CheckpointCommitInterval
            || checked(steps - current.LastCheckpointSteps) >= CheckpointStepInterval;
        return new WorldJournalAdvance(new WorldJournalUsage(sequence, commands, bytes,
            checkpoint ? sequence : current.LastCheckpointSequence,
            checkpoint ? steps : current.LastCheckpointSteps, archives, archiveBytes, materialArchives, materialBytes), checkpoint);
    }

    private static void RequireCompletionCapacity(PendingExcavation pending, ulong sequence, ulong steps,
        ulong commands, ulong bytes, ushort profileVersion)
    {
        ulong continuations = pending.MaximumContinuationCount;
        ulong finalSequence = checked(sequence + continuations);
        var command = pending.Command;
        // The accepted cause is used only to size the fixed-width continuation frame. It is not
        // selected or reserved again; each real continuation supplies a fresh unoccupied cause.
        var continuation = new WorldStepBatch(pending.TriggerCauseId, command.WorldId, command.BranchId,
            sequence, steps, WorldBatchKind.ConsequenceContinuation, [], [], profileVersion);
        ulong batchLength = (ulong)WorldStepCodec.Encode(continuation).Length;
        ulong emptyOutcomes = (ulong)WorldReceiptCodecV1.EncodeBatch([]).Length;
        ulong terminalOutcomes = checked((ulong)pending.MaximumTerminalOutcomesByteLength);
        ulong futureBytes = checked(continuations * checked(batchLength + emptyOutcomes)
            + terminalOutcomes - emptyOutcomes);
        if (finalSequence > MaximumCommitCount || checked(commands + 1) > MaximumCommandOutcomeCount
            || checked(bytes + futureBytes) > MaximumAcceptedJournalBytes)
        {
            throw new WorldRecoveryLimitException("The local world cannot retain the complete bounded consequence continuation.");
        }
    }
}

internal sealed record WorldJournalAdvance(WorldJournalUsage Usage, bool WriteCheckpoint);
