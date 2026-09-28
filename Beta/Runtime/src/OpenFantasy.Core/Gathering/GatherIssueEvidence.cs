using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Gathering;

/// <summary>
/// Compact evidence created only by an accepted gather plan. It retains exact operation pins and
/// issued material, never an inventory, spatial root or chain of source owners. This is a structural
/// planner witness; a durable host must retrieve and authenticate its actual source history.
/// </summary>
public sealed class GatherIssueEvidence
{
    internal GatherIssueEvidence(WorldSnapshot world, LocalSpatialSnapshot space, WorldActionCommand command,
        CountedAuthorityState beforeInventory, CountedAuthorityState afterInventory,
        GatheringSnapshot beforeGathering, GatheringSnapshot afterGathering, CountedSourceLot issuedLot)
    {
        Command = command;
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
        BeforeGatheringHash = beforeGathering.ContentHash;
        AfterGatheringHash = afterGathering.ContentHash;
        AdvancingStep = beforeGathering.AdvancingStep;
        IssuedLot = issuedLot;
        ContentHash = ContentHash.Compute(CanonicalEncodingV1.Frame("gather.issue-evidence", Number(1),
            WorldActionCodec.Encode(command), WorldHash.ToByteArray(), SpaceHash.ToByteArray(),
            BeforeInventoryHash.ToByteArray(), AfterInventoryHash.ToByteArray(), BeforeGatheringHash.ToByteArray(),
            AfterGatheringHash.ToByteArray(), Number(AdvancingStep), issuedLot.LotId.ToByteArray(),
            issuedLot.CustodianId.ToByteArray(), issuedLot.Definition.DefinitionId.ToByteArray(),
            issuedLot.Definition.Version.ToByteArray(), issuedLot.Source.SourceId.ToByteArray(),
            Number(issuedLot.Source.Start), Number(issuedLot.Source.End)));
    }

    public WorldActionCommand Command { get; }
    public ContentHash WorldHash { get; }
    public ContentHash SpaceHash { get; }
    public ContentHash BeforeInventoryHash { get; }
    public ContentHash AfterInventoryHash { get; }
    public ContentHash BeforeGatheringHash { get; }
    public ContentHash AfterGatheringHash { get; }
    public ulong AdvancingStep { get; }
    public CountedSourceLot IssuedLot { get; }
    public ContentHash ContentHash { get; }

    internal GatheringSnapshot Replay(GatheringSnapshot prior)
    {
        GatheringSnapshot before = prior.AdvanceTo(AdvancingStep);
        if (before.ContentHash != BeforeGatheringHash || Command.Intent is not GatherIntent intent)
            throw new ArgumentException("Gather evidence must follow the exact known source and effort owner.");
        GatherNodeState node = before.GetNode(intent.NodeId);
        if (node.SourceId != IssuedLot.Source.SourceId || node.NextUnissuedUnit != IssuedLot.Source.Start
            || node.Definition.YieldDefinition != IssuedLot.Definition)
            throw new ArgumentException("Gather evidence cannot reset or skip an issued source cursor.");
        GatheringSnapshot after = before.Harvest(node.Harvest(checked((uint)IssuedLot.Quantity), AdvancingStep),
            before.GetActorWork(Command.ActorId).Spend());
        if (after.ContentHash != AfterGatheringHash)
            throw new ArgumentException("The actual gather transition does not match its source owner.");
        return after;
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
