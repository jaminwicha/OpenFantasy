using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Authenticates activation against its original complete replay boundary, then validates current operational rows.</summary>
internal sealed class ControllerRecoveryReplay(WorldDatabase database)
{
    private readonly ControllerRecoveryState _stored = database.ReadControllerRecovery();
    private bool _activationObserved;

    internal void ValidateEvent(StoredWorldCommit stored)
    {
        // A copied schema-nine prefix keeps its original event interpretation. Only publications
        // after this save's explicit activation admit the measured, capped operational policy.
        if (stored.Sequence <= _stored.Activation.SourceRevision || stored.Kind != WorldCommitKind.ControllerLifecycle) return;
        WorldStepBatch batch = WorldStepCodec.Decode(stored.AcceptedBatch);
        if (batch.ControllerEvent is { Kind: WorldControllerEventKind.AllowanceExpired } expiry
            && expiry.ElapsedHostMilliseconds != WorldControllerState.ReconnectAllowanceMilliseconds)
            throw new InvalidDataException("Post-activation controller expiry exceeds its admitted persisted lower-bound policy.");
    }

    internal void Observe(WorldSnapshot state)
    {
        var activation = _stored.Activation;
        if ((database.SchemaVersion is WorldDatabaseSchemaEleven.Version or WorldDatabaseSchemaTwelve.Version)
            && (activation.Upgraded || activation.SourceRevision != 0 || activation.UnknownPriorElapsed.Count != 0))
            throw new InvalidDataException("Permission storage requires recovery provenance from its exact fresh genesis.");
        if (state.Revision != activation.SourceRevision) return;
        if (state.ContentHash != activation.SourceHash
            || !activation.UnknownPriorElapsed.SequenceEqual(activation.Upgraded
                ? ControllerRecoveryState.FromWorld(state) : Array.Empty<ControllerAllowanceProgress>()))
            throw new InvalidDataException("Controller recovery activation differs from its original authenticated snapshot.");
        if (!activation.Upgraded && (state.Revision != 0 || ControllerRecoveryState.FromWorld(state).Count != 0))
            throw new InvalidDataException("Fresh controller recovery must start at its unplayed human genesis.");
        _activationObserved = true;
    }

    internal void Complete(WorldSnapshot head)
    {
        if (!_activationObserved) throw new InvalidDataException("Controller recovery activation is beyond the authenticated history.");
        _stored.ValidateHead(head);
    }
}
