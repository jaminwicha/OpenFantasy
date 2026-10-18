using System.Collections.ObjectModel;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Construction;

/// <summary>
/// Unpublished work/time/material transition. The exact source, candidate and outcomes must be admitted
/// and serialized by the durable owner; this result itself neither commits nor issues a receipt.
/// </summary>
public sealed class ConstructionLocalityStep
{
    internal ConstructionLocalityStep(ConstructionLocalityState source, ConstructionLocalityState candidate,
        WorldActionReceipt receipt, IEnumerable<WorldCommittedIdentity> identities)
    {
        Source = source;
        Candidate = candidate;
        Receipt = receipt;
        Identities = Array.AsReadOnly(identities.OrderBy(value => value.Identity).ToArray());
    }

    /// <summary>
    /// Reconstructs the complete admitted candidate and its proposed outcomes. The caller must still
    /// establish exact source ownership, current history and selected input before durable publication.
    /// </summary>
    public WorldStepDecision Rehydrate(IEnumerable<TerrainRegionSnapshot> regions) =>
        new(Candidate.Rehydrate(regions), [Receipt], Identities);

    public ConstructionLocalityState Source { get; }
    public ConstructionLocalityState Candidate { get; }
    public WorldActionReceipt Receipt { get; }
    public ReadOnlyCollection<WorldCommittedIdentity> Identities { get; }
}
