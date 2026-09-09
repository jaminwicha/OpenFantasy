namespace OpenFantasy.Core.Commands;

/// <summary>
/// Immutable submitted claims and intent. None of these identifiers proves authentication,
/// actor control, knowledge, custody or permission. The host verifies delivery context separately.
/// </summary>
public sealed class CommandEnvelope
{
    public const ulong CurrentProtocolVersion = 1;

    public CommandEnvelope(EntityId commandId, EntityId principalId, EntityId originSessionId,
        EntityId controlledActorId, EntityId worldId, EntityId branchId,
        IEnumerable<ExpectedRevision> expectedRevisions, TransferLotIntent payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ExpectedRevisions = new ExpectedRevisionSet(expectedRevisions);
        foreach (ExpectedRevision revision in ExpectedRevisions)
        {
            if (revision.TargetKind == RevisionTargetKind.Branch && revision.TargetId != branchId)
            {
                throw new ArgumentException("A branch revision must address the envelope's branch.",
                    nameof(expectedRevisions));
            }
        }

        CommandId = commandId;
        PrincipalId = principalId;
        OriginSessionId = originSessionId;
        ControlledActorId = controlledActorId;
        WorldId = worldId;
        BranchId = branchId;
        Payload = payload;
    }

    public ulong ProtocolVersion => CurrentProtocolVersion;
    public EntityId CommandId { get; }
    public EntityId PrincipalId { get; }
    public EntityId OriginSessionId { get; }
    public EntityId ControlledActorId { get; }
    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public ExpectedRevisionSet ExpectedRevisions { get; }
    public TransferLotIntent Payload { get; }

    public CommandContentHash ComputeContentHash() => CommandContentHash.Compute(CommandCodecV1.Encode(this));
}
