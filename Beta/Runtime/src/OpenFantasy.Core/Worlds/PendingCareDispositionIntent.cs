namespace OpenFantasy.Core.Worlds;

/// <summary>A nonbodily terminal choice over an exact captured pending-care request.</summary>
public abstract record PendingCareDispositionIntent(EntityId AcknowledgmentId, EntityId EncounterId,
    ulong ExpectedCareRevision) : WorldActionIntent;

public sealed record CancelPendingCareIntent(EntityId AcknowledgmentId, EntityId EncounterId,
    ulong ExpectedCareRevision) : PendingCareDispositionIntent(AcknowledgmentId, EncounterId, ExpectedCareRevision);

public sealed record DeclinePendingCareIntent(EntityId AcknowledgmentId, EntityId EncounterId,
    ulong ExpectedCareRevision) : PendingCareDispositionIntent(AcknowledgmentId, EncounterId, ExpectedCareRevision);
