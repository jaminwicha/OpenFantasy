using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>A port-bound input lifetime, independent of world revisions and permanent actor entitlements.</summary>
public sealed record LocalActorSelection(EntityId PortId, EntityId HomeActorId, EntityId ActorId,
    ulong Generation, WorldControlWitness? ControlWitness);

/// <summary>One coherent published selection with the home character's continuing human obligations.</summary>
public sealed record LocalSelectedPlayerObservation(LocalActorSelection Selection, LocalWorldView HomeView,
    LocalControlledActorView? ControlledView, PlayerControlStatus ControlStatus, bool IsReady);
