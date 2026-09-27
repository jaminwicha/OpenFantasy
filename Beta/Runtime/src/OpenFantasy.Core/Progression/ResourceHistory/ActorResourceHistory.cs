using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Progression.ResourceHistory;

/// <summary>
/// Bounded continuity evidence anchored to a caller-admitted progression basis. The current actor
/// remains authoritative. Resolving causal references, permissions and journal replay belongs to the world.
/// </summary>
public sealed class ActorResourceHistory
{
    public const int MaximumBatches = 256;
    public const int MaximumChanges = 1024;
    private readonly Dictionary<EntityId, ActorProgressionState> _head;
    private readonly ulong _minimumProgressionRevision;

    public ActorResourceHistory(ProgressionSnapshot basis, ulong basisWorldSequence, IEnumerable<ResourceChangeBatch> batches)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(batches);
        Basis = basis;
        BasisWorldSequence = basisWorldSequence;
        List<ResourceChangeBatch> retained = [];
        int count = 0;
        foreach (var batch in batches)
        {
            ArgumentNullException.ThrowIfNull(batch);
            if (retained.Count == MaximumBatches || batch.Changes.Count > MaximumChanges - count)
                throw new ArgumentException("Resource history requires archival continuation before its storage envelope is exhausted.");
            count += batch.Changes.Count;
            retained.Add(batch);
        }
        Batches = retained.AsReadOnly();
        ChangeCount = count;
        _minimumProgressionRevision = checked(basis.Revision + (ulong)retained.Count);
        _head = basis.Actors.ToDictionary(actor => actor.ActorId);
        ValidateAndReplay();
    }

    public ProgressionSnapshot Basis { get; }
    public ulong BasisWorldSequence { get; }
    public ulong LastWorldSequence => Batches.Count == 0 ? BasisWorldSequence : Batches[^1].WorldSequence;
    public ReadOnlyCollection<ResourceChangeBatch> Batches { get; }
    public int ChangeCount { get; }
    public ContentHash ContentHash => ContentHash.Compute(ActorResourceHistoryCodecV1.Encode(this));

    public ActorResourceHistory Append(ResourceChangeBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        return new(Basis, BasisWorldSequence, Batches.Append(batch));
    }

    /// <summary>Checks the complete actor set, not currency effects or authenticity of the claimed causes.</summary>
    public void ValidateCurrent(ProgressionSnapshot current, ulong worldSequence)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.WorldId != Basis.WorldId || current.BranchId != Basis.BranchId
            || worldSequence < LastWorldSequence || current.Revision < _minimumProgressionRevision
            || !current.Profiles.Select(p => (p.DefinitionId, p.Version)).SequenceEqual(Basis.Profiles.Select(p => (p.DefinitionId, p.Version)))
            || !current.Curves.Select(c => (c.DefinitionId, c.Version)).SequenceEqual(Basis.Curves.Select(c => (c.DefinitionId, c.Version)))
            || current.Actors.Count != _head.Count
            || current.Actors.Any(actor => !_head.TryGetValue(actor.ActorId, out var expected) || !ActorResourceChange.Same(actor, expected)))
        {
            throw new ArgumentException("Current progression must retain scope, definition admission and every replayed actor revision.");
        }
    }

    private void ValidateAndReplay()
    {
        Dictionary<EntityId, ContentHash> pins = Basis.Profiles.ToDictionary(p => p.DefinitionId, p => p.Version);
        foreach (var curve in Basis.Curves) pins.Add(curve.DefinitionId, curve.Version);
        pins.Add(Basis.Currency.Currency.DefinitionId, Basis.Currency.Currency.Version);
        foreach (var change in Batches.SelectMany(batch => batch.Changes))
        {
            var cause = change.Cause;
            if (pins.TryGetValue(cause.DefinitionId, out var pin))
            {
                if (pin != cause.DefinitionVersion)
                    throw new ArgumentException("A resource history cannot pin conflicting versions of one rule definition.");
            }
            else
            {
                if (Basis.ContainsIdentity(cause.DefinitionId))
                    throw new ArgumentException("A resource rule cannot alias an instance identity in its admitted basis.");
                pins.Add(cause.DefinitionId, cause.DefinitionVersion);
            }
        }
        HashSet<EntityId> commands = [];
        ResourceChangeBatch? prior = null;
        foreach (var batch in Batches)
        {
            if (batch.WorldSequence <= BasisWorldSequence
                || prior is not null && (batch.WorldSequence < prior.WorldSequence
                    || batch.WorldSequence == prior.WorldSequence && batch.CommandOrdinal <= prior.CommandOrdinal)
                || !commands.Add(batch.CommandId) || Basis.ContainsIdentity(batch.CommandId) || pins.ContainsKey(batch.CommandId))
                throw new ArgumentException("Resource batches require ordered world positions and unique, non-aliasing commands.");
            foreach (var change in batch.Changes)
            {
                if (Basis.ContainsIdentity(change.Cause.EvidenceId) || pins.ContainsKey(change.Cause.EvidenceId)
                    || !_head.TryGetValue(change.Before.ActorId, out var current))
                    throw new ArgumentException("Resource evidence must name an admitted actor and a distinct causal record.");
                _head[current.ActorId] = change.ApplyTo(current);
            }
            prior = batch;
        }
    }
}
