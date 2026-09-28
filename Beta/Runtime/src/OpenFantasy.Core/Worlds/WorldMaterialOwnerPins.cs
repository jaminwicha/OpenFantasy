using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Canonical digests of the material owners joined by one complete-world candidate; an owner the
/// candidate does not carry is null. The pins are a bounded structural observation. They hold no
/// owner, confer no authority and cannot issue, restore or import material.
/// </summary>
public sealed class WorldMaterialOwnerPins : IEquatable<WorldMaterialOwnerPins>
{
    internal WorldMaterialOwnerPins(ContentHash? inventory, ContentHash? production, ContentHash? gathering,
        ContentHash? excavation, ContentHash? construction, ContentHash? space, ContentHash? quests,
        ContentHash? progression, ContentHash? care, ContentHash? reservoirs, ContentHash? liquidCells)
    {
        Inventory = inventory;
        Production = production;
        Gathering = gathering;
        Excavation = excavation;
        Construction = construction;
        Space = space;
        Quests = quests;
        Progression = progression;
        Care = care;
        Reservoirs = reservoirs;
        LiquidCells = liquidCells;
        ContentHash = WorldMaterialEvidenceEncoding.Hash(this);
    }

    public ContentHash? Inventory { get; }
    public ContentHash? Production { get; }
    public ContentHash? Gathering { get; }
    public ContentHash? Excavation { get; }
    public ContentHash? Construction { get; }
    /// <summary>Spatial components under the current support mode; Construction pins derived installed structures.</summary>
    public ContentHash? Space { get; }
    public ContentHash? Quests { get; }
    public ContentHash? Progression { get; }
    public ContentHash? Care { get; }
    public ContentHash? Reservoirs { get; }
    public ContentHash? LiquidCells { get; }
    public ContentHash ContentHash { get; }

    /// <summary>Pins the owners of a complete profile 26 root, for comparison with an observation.</summary>
    public static WorldMaterialOwnerPins Observe(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        WorldMaterialEvidenceCoverage.RequireProfile(world);
        return new WorldMaterialEvidencePinner().Pin(WorldMaterialEvidenceOwners.Of(world));
    }

    public bool Equals(WorldMaterialOwnerPins? other) => other is not null && ContentHash == other.ContentHash;
    public override bool Equals(object? obj) => Equals(obj as WorldMaterialOwnerPins);
    public override int GetHashCode() => ContentHash.GetHashCode();
}
