namespace OpenFantasy.Core.Commands;

/// <summary>A positive authority commit sequence within one world, supplied by its durable writer.</summary>
public sealed record CommandCommitIdentity
{
    public CommandCommitIdentity(EntityId worldId, ulong sequence)
    {
        if (sequence == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Committed receipts require a positive sequence.");
        }

        WorldId = worldId;
        Sequence = sequence;
    }

    public EntityId WorldId { get; }
    public ulong Sequence { get; }
}
