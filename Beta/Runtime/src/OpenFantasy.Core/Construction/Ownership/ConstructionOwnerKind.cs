namespace OpenFantasy.Core.Construction.Ownership;

/// <summary>Explicit owners of the complete legacy construction interpretation; not optional generic facets.</summary>
public enum ConstructionOwnerKind
{
    InventoryBasis = 1,
    Timing = 2,
    Actor = 3,
    BodyDefinition = 4,
    Control = 5,
    Custodian = 6,
    ProductionHistory = 7,
    ConstructionCatalog = 8,
    Project = 9,
    Gathering = 10,
    Progression = 11,
    Quests = 12,
    Dialogues = 13,
    ConstructionDefinition = 14,
    InventoryLedger = 15,
    Recipe = 16,
    ProductionBatch = 17
}

/// <summary>Stable role/subject within a directory's world and historical branch, independent of spatial membership.</summary>
public readonly record struct ConstructionOwnerKey(ConstructionOwnerKind Kind, EntityId SubjectId);
