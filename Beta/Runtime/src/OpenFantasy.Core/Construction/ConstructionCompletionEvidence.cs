using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Construction;

/// <summary>Opaque successful construction qualification, without a retained world or prior owner chain.</summary>
public sealed class ConstructionCompletionEvidence
{
    internal ConstructionCompletionEvidence(WorldSnapshot world, LocalSpatialSnapshot space, WorldActionCommand command,
        CountedAuthorityState beforeInventory, CountedAuthorityState afterInventory, ProductionSnapshot production,
        ConstructionSnapshot beforeConstruction, ConstructionSnapshot afterConstruction, ConstructionProject project,
        ProductionBatch batch)
    {
        Command = command;
        Batch = batch;
        ReservationId = project.ReservationId;
        InstalledOutputIndex = project.Definition.InstalledOutputIndex;
        StructureId = project.Installed!.StructureId;
        WorldHash = world.ContentHash;
        SpaceHash = ContentHash.Compute(space.StructureSupport == StructureSupportMode.TerrainAndConstruction
            ? WorldSpatialCodecV4.Encode(space) : space.GroundSupport switch
            {
                GroundSupportMode.SteppedTerrainAndConstruction => WorldSpatialCodecV3.Encode(space),
                GroundSupportMode.TerrainAndConstruction => WorldSpatialCodecV2.Encode(space),
                _ => WorldSpatialCodecV1.Encode(space)
            });
        BeforeInventoryHash = beforeInventory.ContentHash;
        AfterInventoryHash = afterInventory.ContentHash;
        BeforeProductionHash = production.ContentHash;
        BeforeConstructionHash = beforeConstruction.ContentHash;
        AfterConstructionHash = afterConstruction.ContentHash;
        ProcessIntervals = world.ProcessIntervals;
        AdvancingSteps = world.AdvancingSteps;
        ContentHash = ContentHash.Compute(CanonicalEncodingV1.Frame("construction.completion-evidence", Number(1),
            WorldActionCodec.Encode(command), ProductionCodecV1.EncodeBatch(batch), ReservationId.ToByteArray(),
            Number(InstalledOutputIndex), StructureId.ToByteArray(), WorldHash.ToByteArray(), SpaceHash.ToByteArray(),
            BeforeInventoryHash.ToByteArray(), AfterInventoryHash.ToByteArray(), BeforeProductionHash.ToByteArray(),
            BeforeConstructionHash.ToByteArray(), AfterConstructionHash.ToByteArray(), Number(ProcessIntervals), Number(AdvancingSteps)));
    }

    public WorldActionCommand Command { get; }
    public ProductionBatch Batch { get; }
    public EntityId ReservationId { get; }
    public uint InstalledOutputIndex { get; }
    public EntityId StructureId { get; }
    public ContentHash WorldHash { get; }
    public ContentHash SpaceHash { get; }
    public ContentHash BeforeInventoryHash { get; }
    public ContentHash AfterInventoryHash { get; }
    public ContentHash BeforeProductionHash { get; }
    public ContentHash BeforeConstructionHash { get; }
    public ContentHash AfterConstructionHash { get; }
    public ulong ProcessIntervals { get; }
    public ulong AdvancingSteps { get; }
    public ContentHash ContentHash { get; }
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
