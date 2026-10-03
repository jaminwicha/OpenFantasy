using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Captured consent and expired episode; the host supplies live presence and a fresh assignment cause.</summary>
public sealed record LocalControlAcquisition(EntityId OwnerActorId, EntityId GrantCommandId,
    EntityId AcceptanceCommandId, ulong GrantRevision, WorldControlCapabilities Capabilities,
    ContentHash TermsHash, ulong ExpectedAssignmentRevision, EntityId DisconnectId, ulong OwnerControlRevision);
