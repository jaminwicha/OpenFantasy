using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking.Selection;

public enum PlayerSelectedInputKind : byte { SelectActor = 1, Pause, Resume, Movement, Command, Query }

/// <summary>One exact selected input. A delivered observation identity is a Resume proof, never new authority.</summary>
public sealed class PlayerSelectedInput
{
    private PlayerSelectedInput(PlayerSelectedInputKind kind, LocalActorSelection selection,
        EntityId? targetActorId = null, EntityId? streamId = null, ulong? revision = null,
        int deltaX = 0, int deltaY = 0, WorldActionCommand? action = null,
        LocalSelectedQueryOperation? operation = null, LocalTradeInquiry? inquiry = null, EntityId? subjectId = null)
    {
        Kind = kind; Selection = selection; TargetActorId = targetActorId; StreamId = streamId; Revision = revision;
        DeltaX = deltaX; DeltaY = deltaY; Action = action; Operation = operation; Inquiry = inquiry; SubjectId = subjectId;
    }
    public PlayerSelectedInputKind Kind { get; }
    public LocalActorSelection Selection { get; }
    public EntityId? TargetActorId { get; }
    public EntityId? StreamId { get; }
    public ulong? Revision { get; }
    public int DeltaX { get; }
    public int DeltaY { get; }
    public WorldActionCommand? Action { get; }
    public LocalSelectedQueryOperation? Operation { get; }
    public LocalTradeInquiry? Inquiry { get; }
    public EntityId? SubjectId { get; }
    public static PlayerSelectedInput SelectActor(LocalActorSelection selection, EntityId target) => new(PlayerSelectedInputKind.SelectActor, selection, targetActorId: target);
    public static PlayerSelectedInput Pause(LocalActorSelection selection) => new(PlayerSelectedInputKind.Pause, selection);
    public static PlayerSelectedInput Resume(LocalActorSelection selection, EntityId streamId, ulong revision) => new(PlayerSelectedInputKind.Resume, selection, streamId: streamId, revision: revision);
    public static PlayerSelectedInput Movement(LocalActorSelection selection, int x, int y) => new(PlayerSelectedInputKind.Movement, selection, deltaX: x, deltaY: y);
    public static PlayerSelectedInput Command(LocalActorSelection selection, WorldActionCommand command) => new(PlayerSelectedInputKind.Command, selection, action: command);
    public static PlayerSelectedInput Query(LocalActorSelection selection, LocalSelectedQueryOperation operation,
        LocalTradeInquiry? inquiry = null, EntityId? subjectId = null) => new(PlayerSelectedInputKind.Query, selection, operation: operation, inquiry: inquiry, subjectId: subjectId);
}
