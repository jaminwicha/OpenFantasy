namespace OpenFantasy.Core.Commands;

/// <summary>The owner of a revision within the envelope's world and branch.</summary>
public enum RevisionTargetKind
{
    Entity = 1,
    Branch = 2
}

public sealed record ExpectedRevision
{
    public ExpectedRevision(RevisionTargetKind targetKind, EntityId targetId, ulong revision)
    {
        if (!Enum.IsDefined(targetKind))
        {
            throw new ArgumentOutOfRangeException(nameof(targetKind));
        }

        TargetKind = targetKind;
        TargetId = targetId;
        Revision = revision;
    }

    public RevisionTargetKind TargetKind { get; }
    public EntityId TargetId { get; }
    public ulong Revision { get; }
}
