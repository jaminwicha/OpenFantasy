using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Terrain;

/// <summary>Opaque actual mining or gathering qualification, with shared effort and exact material prefix pins.</summary>
public sealed class ExcavationWorkEvidence
{
    internal ExcavationWorkEvidence(WorldSnapshot world, MaterialExcavationState before, LocalSpatialSnapshot beforeSpace,
        GatheringSnapshot beforeGathering, ExcavationMaterialBoundary afterBoundary, LocalSpatialSnapshot afterSpace,
        GatheringSnapshot afterGathering, CountedAuthorityState inventory, WorldActionCommand command,
        CountedSourceLot? issuedLot, GatherIssueEvidence? gatherEvidence)
    {
        Command = command;
        WorldHash = world.ContentHash;
        SourceHash = before.Boundary.Source.ContentHash;
        BeforeBoundaryHash = before.Boundary.ContentHash;
        AfterBoundaryHash = afterBoundary.ContentHash;
        BeforeSpaceHash = ContentHash.Compute(ExcavationMaterialSource.EncodeSpace(beforeSpace));
        AfterSpaceHash = ContentHash.Compute(ExcavationMaterialSource.EncodeSpace(afterSpace));
        BeforeInventoryHash = before.Inventory.ContentHash;
        AfterInventoryHash = inventory.ContentHash;
        BeforeProductionHash = before.Production.ContentHash;
        BeforeGatheringHash = beforeGathering.ContentHash;
        AfterGatheringHash = afterGathering.ContentHash;
        Frontier = ExcavationMaterialBoundary.Observe(world, command.ActorId);
        IssuedLot = issuedLot;
        GatherEvidence = gatherEvidence;
        if ((command.Intent is GatherIntent) != (gatherEvidence is not null))
            throw new ArgumentException("Gathering within excavation requires its genuine issuing proof.");
        ContentHash = ContentHash.Compute(CanonicalEncodingV1.Frame("excavation.work-evidence.v1", WorldActionCodec.Encode(command),
            WorldHash.ToByteArray(), SourceHash.ToByteArray(), BeforeBoundaryHash.ToByteArray(), AfterBoundaryHash.ToByteArray(),
            BeforeSpaceHash.ToByteArray(), AfterSpaceHash.ToByteArray(), BeforeInventoryHash.ToByteArray(), AfterInventoryHash.ToByteArray(),
            BeforeProductionHash.ToByteArray(), BeforeGatheringHash.ToByteArray(), AfterGatheringHash.ToByteArray(),
            ExcavationMaterialBoundary.EncodeFrontier(Frontier), gatherEvidence?.ContentHash.ToByteArray() ?? [],
            issuedLot is null ? [] : CanonicalEncodingV1.Frame("excavation.issued-lot.v1", issuedLot.LotId.ToByteArray(),
                issuedLot.CustodianId.ToByteArray(), issuedLot.Definition.DefinitionId.ToByteArray(), issuedLot.Definition.Version.ToByteArray(),
                issuedLot.Source.SourceId.ToByteArray(), CanonicalEncodingV1.Unsigned64(issuedLot.Source.Start),
                CanonicalEncodingV1.Unsigned64(issuedLot.Source.End))));
    }
    public WorldActionCommand Command { get; }
    public ContentHash WorldHash { get; }
    public ContentHash SourceHash { get; }
    public ContentHash BeforeBoundaryHash { get; }
    public ContentHash AfterBoundaryHash { get; }
    public ContentHash BeforeSpaceHash { get; }
    public ContentHash AfterSpaceHash { get; }
    public ContentHash BeforeInventoryHash { get; }
    public ContentHash AfterInventoryHash { get; }
    public ContentHash BeforeProductionHash { get; }
    public ContentHash BeforeGatheringHash { get; }
    public ContentHash AfterGatheringHash { get; }
    public ExcavationWorkFrontier Frontier { get; }
    public CountedSourceLot? IssuedLot { get; }
    public GatherIssueEvidence? GatherEvidence { get; }
    public ContentHash ContentHash { get; }

    internal GatheringSnapshot ReplayGathering(GatheringSnapshot prior)
    {
        var before = prior.AdvanceTo(Frontier.ProcessIntervals);
        if (before.ContentHash != BeforeGatheringHash)
            throw new ArgumentException("Work evidence must follow the exact shared gathering and effort owner.");
        GatheringSnapshot after = GatherEvidence is { } gather ? gather.Replay(prior) : before.SpendEffort(Command.ActorId);
        if (after.ContentHash != AfterGatheringHash)
            throw new ArgumentException("Accepted work cannot restore effort or alter unrelated source cursors.");
        return after;
    }
}
