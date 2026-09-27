using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Progression.ResourceHistory;

namespace OpenFantasy.Core.Worlds;

/// <summary>World-owned causal continuity beside the sole current progression owner.</summary>
public sealed class WorldResourceHistory
{
    public WorldResourceHistory(ContentHash anchorAdmission, ActorResourceHistory history,
        IEnumerable<WorldResourceEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(anchorAdmission);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(evidence);
        var retained = new List<WorldResourceEvidence>();
        foreach (var item in evidence)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (retained.Count >= history.Batches.Count || item.CommandId != history.Batches[retained.Count].CommandId)
                throw new ArgumentException("Each resource batch requires exactly one matching ordered domain witness.");
            retained.Add(item);
        }
        if (retained.Count != history.Batches.Count)
            throw new ArgumentException("Every resource batch requires retained domain evidence.");
        AnchorAdmission = anchorAdmission;
        History = history;
        Evidence = retained.AsReadOnly();
    }

    public ContentHash AnchorAdmission { get; }
    public ActorResourceHistory History { get; }
    public ReadOnlyCollection<WorldResourceEvidence> Evidence { get; }
    public ContentHash ContentHash => ContentHash.Compute(WorldResourceHistoryCodecV1.Encode(this));

    internal static WorldResourceHistory CreateFresh(ContentHash admission, ProgressionSnapshot basis) =>
        new(admission, new ActorResourceHistory(basis, 0, []), []);

    internal WorldResourceHistory Append(ResourceChangeBatch batch, WorldResourceEvidence evidence)
    {
        if (History.Batches.Count == ActorResourceHistory.MaximumBatches
            || History.ChangeCount + batch.Changes.Count > ActorResourceHistory.MaximumChanges)
            throw new WorldResourceContinuationRequiredException();
        return new(AnchorAdmission, History.Append(batch), Evidence.Append(evidence));
    }
}

/// <summary>
/// Storage continuation must precede further resource publication. This deliberately escapes the
/// ordinary action-rejection catches; envelope exhaustion must never become a permanent gameplay refusal.
/// </summary>
public sealed class WorldResourceContinuationRequiredException()
    : Exception("Resource history requires authenticated archival continuation before further publication.");
