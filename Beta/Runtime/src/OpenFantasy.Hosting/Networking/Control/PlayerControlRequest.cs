using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking.Control;

public enum PlayerControlRequestKind { ReadPermissions = 1, SubmitPermission = 2, Acquire = 3, Release = 4 }

/// <summary>Home-authorized administration is independent of the actor selected for gameplay.</summary>
public sealed record PlayerControlRequest(PlayerControlRequestKind Kind, WorldActionCommand? Command = null,
    LocalControlAcquisition? Acquisition = null, WorldControlWitness? Witness = null);

public sealed record PlayerControlResponse(EntityId WorldId, EntityId BranchId, EntityId HomeActorId,
    PlayerControlRequestKind Kind, LocalControlPermissionBoard? Board = null,
    WorldActionReceipt? Receipt = null, WorldControlWitness? Witness = null);
