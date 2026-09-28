using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Geography;

public sealed record ReservoirTransportInterval(EntityId BasinId, EntityId SourceCustodianId,
    EntityId ReceiverCustodianId, SourceInterval Source);

/// <summary>Opaque evidence of an actual indivisible due-stage delivery, with exact material inputs and result.</summary>
public sealed class ReservoirTransportEvidence
{
    // Fragment ordinals are bounded by the live-lot limit, so this permanent cause has a distinct reserved ordinal.
    public static EntityId DeliveryCause(EntityId stageId) => EntityId.DeriveSpawned(stageId, ulong.MaxValue);

    internal ReservoirTransportEvidence(ReservoirTransportState before, PreparedWatershedRunoff runoff,
        RunoffTransportStage stage, WorldInstant completedAt, IReadOnlyCollection<ReservoirTransportInterval> intervals,
        CountedAuthorityState inventory, IReadOnlyCollection<EntityId> createdIds)
    {
        StageId = stage.StageId;
        ReservationId = stage.ReservationId;
        CauseId = DeliveryCause(StageId);
        FundingHash = stage.FundingHash;
        DueAt = stage.DueAt;
        CompletedAt = completedAt;
        Units = stage.Units;
        Material = runoff.Request.Material.Item;
        CatalogHash = TransportMaterialBoundary.Catalog(before);
        BeforeTransportHash = before.ContentHash;
        BeforeInventoryHash = before.Reservoirs.Capacity.Inventory.ContentHash;
        BeforeProductionHash = before.Reservoirs.Capacity.Production!.ContentHash;
        AfterInventoryHash = inventory.ContentHash;
        Intervals = Array.AsReadOnly(intervals.OrderBy(value => value.BasinId).ToArray());
        CreatedIds = Array.AsReadOnly(createdIds.ToArray());
        Routes = Array.AsReadOnly(stage.Routes.ToArray());
        if (completedAt < DueAt || completedAt != before.Boundary || !stage.IsPending
            || FundingHash != RunoffTransportStage.Fingerprint(runoff.Request))
            throw new ArgumentException("Delivery evidence requires the actual due stage and processing boundary.");
        ContentHash = ContentHash.Compute(CanonicalEncodingV1.Frame("geography.transport-delivery-evidence.v1",
            StageId.ToByteArray(), ReservationId.ToByteArray(), CauseId.ToByteArray(), FundingHash.ToByteArray(),
            WatershedRunoffCodecV1.EncodeInstant(DueAt), WatershedRunoffCodecV1.EncodeInstant(CompletedAt),
            CanonicalEncodingV1.Unsigned64(Units), Material.DefinitionId.ToByteArray(), Material.Version.ToByteArray(),
            CatalogHash.ToByteArray(), BeforeTransportHash.ToByteArray(), BeforeInventoryHash.ToByteArray(),
            BeforeProductionHash.ToByteArray(), AfterInventoryHash.ToByteArray(), ReservoirTransportCodecV1.EncodeRoutes(Routes),
            CanonicalEncodingV1.Frame("geography.transport-delivery-intervals.v1", Intervals.Select(value =>
                (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("geography.transport-delivery-interval.v1",
                    value.BasinId.ToByteArray(), value.SourceCustodianId.ToByteArray(), value.ReceiverCustodianId.ToByteArray(),
                    value.Source.SourceId.ToByteArray(), CanonicalEncodingV1.Unsigned64(value.Source.Start),
                    CanonicalEncodingV1.Unsigned64(value.Source.End))).ToArray()),
            CanonicalEncodingV1.Frame("geography.transport-delivery-created.v1",
                CreatedIds.Select(value => (ReadOnlyMemory<byte>)value.ToByteArray()).ToArray())));
    }

    public EntityId StageId { get; }
    public EntityId ReservationId { get; }
    public EntityId CauseId { get; }
    public ContentHash FundingHash { get; }
    public WorldInstant DueAt { get; }
    public WorldInstant CompletedAt { get; }
    public ulong Units { get; }
    public CountedItemDefinition Material { get; }
    public ContentHash CatalogHash { get; }
    public ContentHash BeforeTransportHash { get; }
    public ContentHash BeforeInventoryHash { get; }
    public ContentHash BeforeProductionHash { get; }
    public ContentHash AfterInventoryHash { get; }
    public ReadOnlyCollection<ReservoirTransportInterval> Intervals { get; }
    public ReadOnlyCollection<RunoffTransportRoute> Routes { get; }
    public ReadOnlyCollection<EntityId> CreatedIds { get; }
    public ContentHash ContentHash { get; }
}
