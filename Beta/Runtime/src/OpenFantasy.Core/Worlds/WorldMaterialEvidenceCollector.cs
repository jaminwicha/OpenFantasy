using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Observes one private decision without deciding anything. The interpreting processor reports each
/// accepted action after its complete candidate was reconciled and admitted; a refused or rolled-back
/// action is never reported. Root hashes are computed only when the first observation is captured.
/// </summary>
internal sealed class WorldMaterialEvidenceCollector
{
    // A delivery splits at most one child per selection. Construction creates one batch,
    // each recipe output and its installed structure; other supported actions create fewer.
    internal static int MaximumCreatedIdentities => Math.Max(SourceDeliveryRequest.MaximumSelections,
        MaterialRecipeDefinition.MaximumTermCount + 2);

    private readonly WorldSnapshot _input;
    private readonly WorldSnapshot _source;
    private readonly WorldMaterialEvidencePinner _pinner = new();
    private readonly List<WorldMaterialActionEvidence> _captured = [];

    /// <param name="input">The durable root selected by the caller.</param>
    /// <param name="source">The root actually interpreted, continued when resource history was cut.</param>
    internal WorldMaterialEvidenceCollector(WorldSnapshot input, WorldSnapshot source)
    {
        if (input.WorldId != source.WorldId || input.BranchId != source.BranchId || input.Revision != source.Revision)
        {
            throw new ArgumentException("Resource preparation must retain the selected world scope and revision.", nameof(source));
        }
        _input = input;
        _source = source;
    }

    internal void Capture(int actionOrdinal, WorldActionCommand command, in WorldMaterialEvidenceOwners before,
        in WorldMaterialEvidenceOwners after, EntityId? spawnedId, IReadOnlyList<EntityId> createdIds)
    {
        if (WorldMaterialEvidenceCoverage.Classify(command.Intent) is not { } kind)
        {
            return;
        }
        EntityId[] created = spawnedId is { } spawned ? [spawned, .. createdIds] : [.. createdIds];
        if (created.Length > MaximumCreatedIdentities)
        {
            throw new InvalidOperationException("The accepted action exceeds the bounded material evidence envelope.");
        }
        _captured.Add(new WorldMaterialActionEvidence(_source.WorldId, _source.BranchId, _input.ContentHash,
            _source.ContentHash, _source.Revision, checked(_source.Revision + 1), actionOrdinal, command, kind,
            _pinner.Pin(before), _pinner.Pin(after), created));
    }

    internal WorldMaterialActionEvidence[] Snapshot() => [.. _captured];
}
