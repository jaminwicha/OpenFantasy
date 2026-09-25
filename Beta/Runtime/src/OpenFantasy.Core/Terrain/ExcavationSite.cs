using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Terrain;

/// <summary>A finite permission to extract one retained cell, with labor and a distinct counted entitlement.</summary>
public sealed class ExcavationSite
{
    public const ulong CellVolumeMicrolitres = 1_000_000_000;
    public const ulong MaximumWork = 4096;

    public ExcavationSite(EntityId siteId, CellCoordinate cell, EntityId terrainSourceId, EntityId countedSourceId,
        EntityId ownerActorId, ItemPhysicalBinding material, CountedItemDefinition requiredTool,
        ulong requiredWork, ulong completedWork, SubunitPosition stance,
        ExcavationReachMode reachMode = ExcavationReachMode.SameLevelSide)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(requiredTool);
        if (!Enum.IsDefined(reachMode) || requiredWork is 0 or > MaximumWork || completedWork > requiredWork
            || CellVolumeMicrolitres % material.Physical.VolumePerUnit.Value != 0
            || material.Physical.VolumePerUnit.Value > CellVolumeMicrolitres
            || siteId == terrainSourceId || siteId == countedSourceId || terrainSourceId == countedSourceId)
        {
            throw new ArgumentException("Excavation needs finite work, distinct source roles and exact whole-cell material calibration.");
        }
        CellCoordinate adjacent = stance.Cell;
        long stanceCellZ = reachMode == ExcavationReachMode.RaisedBankTop ? checked(cell.Z + 1) : cell.Z;
        if (adjacent.Z != stanceCellZ || Int128.Abs((Int128)adjacent.X - cell.X) + Int128.Abs((Int128)adjacent.Y - cell.Y) != 1
            || stance.X != checked(adjacent.X * 256 + 128) || stance.Y != checked(adjacent.Y * 256 + 128)
            || stance.Z != checked(stanceCellZ * 256))
        {
            throw new ArgumentException("An excavation stance is a centred cardinal neighbour at the admitted side or raised-bank elevation.");
        }
        SiteId = siteId;
        Cell = cell;
        TerrainSourceId = terrainSourceId;
        CountedSourceId = countedSourceId;
        OwnerActorId = ownerActorId;
        Material = material;
        RequiredTool = requiredTool;
        RequiredWork = requiredWork;
        CompletedWork = completedWork;
        Stance = stance;
        ReachMode = reachMode;
        _ = checked(Quantity * material.Physical.MassPerUnit.Value);
    }

    public EntityId SiteId { get; }
    public CellCoordinate Cell { get; }
    public EntityId TerrainSourceId { get; }
    public EntityId CountedSourceId { get; }
    public EntityId OwnerActorId { get; }
    public ItemPhysicalBinding Material { get; }
    public CountedItemDefinition RequiredTool { get; }
    public ulong RequiredWork { get; }
    public ulong CompletedWork { get; }
    public ulong Revision => CompletedWork;
    public ulong Quantity => CellVolumeMicrolitres / Material.Physical.VolumePerUnit.Value;
    public SubunitPosition Stance { get; }
    public ExcavationReachMode ReachMode { get; }
    public bool IsComplete => CompletedWork == RequiredWork;
    internal ExcavationSite Work() => new(SiteId, Cell, TerrainSourceId, CountedSourceId, OwnerActorId, Material,
        RequiredTool, RequiredWork, checked(CompletedWork + 1), Stance, ReachMode);
}
