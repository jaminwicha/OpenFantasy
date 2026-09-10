using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>A discrete actor request. Movement is sampled by the host's advancing step instead.</summary>
public sealed class WorldActionCommand
{
    public WorldActionCommand(EntityId commandId, EntityId principalId, EntityId originSessionId,
        EntityId actorId, EntityId worldId, EntityId branchId, ulong expectedActorRevision, WorldActionIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (commandId == principalId || commandId == originSessionId || commandId == actorId
            || commandId == worldId || commandId == branchId)
        {
            throw new ArgumentException("A command needs a distinct cause identity.");
        }
        CommandId = commandId;
        PrincipalId = principalId;
        OriginSessionId = originSessionId;
        ActorId = actorId;
        WorldId = worldId;
        BranchId = branchId;
        ExpectedActorRevision = expectedActorRevision;
        Intent = intent;
    }

    public EntityId CommandId { get; }
    public EntityId PrincipalId { get; }
    public EntityId OriginSessionId { get; }
    public EntityId ActorId { get; }
    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public ulong ExpectedActorRevision { get; }
    public WorldActionIntent Intent { get; }
    public ContentHash ContentHash => ContentHash.Compute(WorldActionCodecV1.Encode(this));
}

public abstract record WorldActionIntent;
public sealed record SpeakToActorIntent(EntityId TargetActorId, ulong ExpectedTargetRevision) : WorldActionIntent;
public sealed record CloseDialogueIntent(EntityId DialogueId) : WorldActionIntent;

/// <summary>Only caller-owned inventory revisions are supplied; recipient private state is read by authority.</summary>
public sealed record GiveItemIntent : WorldActionIntent
{
    public GiveItemIntent(EntityId targetActorId, ulong expectedTargetRevision, EntityId lotId,
        ulong expectedLotRevision, EntityId sourceCustodianId, ulong expectedSourceRevision, ulong quantity)
    {
        ArgumentOutOfRangeException.ThrowIfZero(quantity);
        TargetActorId = targetActorId;
        ExpectedTargetRevision = expectedTargetRevision;
        LotId = lotId;
        ExpectedLotRevision = expectedLotRevision;
        SourceCustodianId = sourceCustodianId;
        ExpectedSourceRevision = expectedSourceRevision;
        Quantity = quantity;
    }

    public EntityId TargetActorId { get; }
    public ulong ExpectedTargetRevision { get; }
    public EntityId LotId { get; }
    public ulong ExpectedLotRevision { get; }
    public EntityId SourceCustodianId { get; }
    public ulong ExpectedSourceRevision { get; }
    public ulong Quantity { get; }
}
