using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>One observer's exact pending-request choices. Only the helper sees its own dose terms.</summary>
public sealed record LocalCareDecisionView(EntityId PatientActorId, string PatientName,
    CancelPendingCareIntent? Cancellation, DeclinePendingCareIntent? Decline, AdministerRemedyIntent? Treatment);

public enum LocalCareClosureKind { Treated = 1, PatientCancelled = 2, HelperDeclined = 3 }

/// <summary>A completed request, not encounter acceptance or a promise of subsequent recovery.</summary>
public sealed record LocalCareClosureView(EntityId AcknowledgmentId, EntityId PatientActorId,
    LocalCareClosureKind Kind);
