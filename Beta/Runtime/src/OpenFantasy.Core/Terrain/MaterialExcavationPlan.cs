using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Terrain;

/// <summary>Whole component candidate pinned to every current material, terrain, effort and opportunity fact.</summary>
public sealed class MaterialExcavationPlan
{
    private readonly ContentHash _basis;
    internal MaterialExcavationPlan(MaterialExcavationState basis, MaterialExcavationState candidate,
        WorldActionRejection rejection = WorldActionRejection.None, IEnumerable<EntityId>? createdIds = null,
        ExcavationWorkEvidence? evidence = null)
    {
        _basis = basis.ContentHash;
        Candidate = candidate;
        Rejection = rejection;
        CreatedIds = Array.AsReadOnly((createdIds ?? []).ToArray());
        Evidence = evidence;
    }
    public MaterialExcavationState Candidate { get; }
    public WorldActionRejection Rejection { get; }
    public bool Succeeded => Rejection == WorldActionRejection.None;
    public ReadOnlyCollection<EntityId> CreatedIds { get; }
    public ExcavationWorkEvidence? Evidence { get; }
    public MaterialExcavationState ApplyTo(MaterialExcavationState current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.ContentHash != _basis) throw new InvalidOperationException("The complete excavation material owner changed before publication.");
        return Candidate;
    }
}
