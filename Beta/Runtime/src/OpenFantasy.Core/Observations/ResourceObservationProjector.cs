namespace OpenFantasy.Core.Observations;

public sealed class ObservationAccessException : InvalidOperationException
{
    public ObservationAccessException() : base("The observation view is unavailable.") { }
}

/// <summary>
/// Enforces exact supplied audience/scope bindings and copies the permitted field set.
/// The host must obtain the current binding from authenticated, current control state.
/// </summary>
public sealed class ResourceObservationProjector
{
    public ResourceObservationProjection Project(ObservationAudience request,
        ObservationAudience? currentBinding, ResourceKnowledgeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (currentBinding is null || request != currentBinding || snapshot.Scope != currentBinding.Scope
            || snapshot.ContainsPrivateSource(currentBinding.PrincipalId)
            || snapshot.ContainsPrivateSource(currentBinding.SessionId))
        {
            throw new ObservationAccessException();
        }

        return new ResourceObservationProjection(currentBinding, snapshot.DisclosureEpoch,
            snapshot.DisclosureRevision, snapshot.Entries.Select(entry => entry.Observation));
    }
}
