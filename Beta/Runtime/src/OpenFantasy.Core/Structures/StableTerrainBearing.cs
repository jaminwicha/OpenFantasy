using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Structures;

/// <summary>A declared stable boundary with exact terrain ancestry; solidity alone never creates this rating.</summary>
public sealed record StableTerrainBearing
{
    public StableTerrainBearing(EntityId elementId, CellCoordinate cell, EntityId sourceId,
        CountedItemDefinition material, MassMilligrams capacity)
    {
        ArgumentNullException.ThrowIfNull(material);
        if (capacity.Value == 0 || elementId == sourceId || elementId == material.DefinitionId)
        {
            throw new ArgumentException("A stable terrain bearing needs a positive rating and distinct structural identity.");
        }
        ElementId = elementId;
        Cell = cell;
        SourceId = sourceId;
        Material = material;
        Capacity = capacity;
    }

    public EntityId ElementId { get; }
    public CellCoordinate Cell { get; }
    public EntityId SourceId { get; }
    public CountedItemDefinition Material { get; }
    public MassMilligrams Capacity { get; }
}
