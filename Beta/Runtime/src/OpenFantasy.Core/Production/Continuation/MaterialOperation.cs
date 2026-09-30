using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Construction;

namespace OpenFantasy.Core.Production;

public enum MaterialOperationKind { Craft = 1, Consume = 2, Gather = 3, Transfer = 4, SourceDelivery = 5, Construction = 6, TerrainRepresentation = 7, ReservedTransport = 8, ExcavationWork = 9, ConsentedTransfer = 10, SpotExchange = 11 }

/// <summary>One ordered material transition; only its specific owner can create an accepted record.</summary>
public abstract class MaterialOperation
{
    private protected MaterialOperation(ulong sequence, EntityId commandId, MaterialOperationKind kind)
    {
        Sequence = sequence;
        CommandId = commandId;
        Kind = kind;
    }
    public ulong Sequence { get; }
    public EntityId CommandId { get; }
    public MaterialOperationKind Kind { get; }
}

public sealed class MaterialCraftOperation : MaterialOperation
{
    internal MaterialCraftOperation(ulong sequence, ProductionBatch batch) : base(sequence, batch.CommandId, MaterialOperationKind.Craft) => Batch = batch;
    public ProductionBatch Batch { get; }
}

public sealed class MaterialConsumptionOperation : MaterialOperation
{
    internal MaterialConsumptionOperation(ulong sequence, ConsumedItemClaim claim) : base(sequence, claim.CommandId, MaterialOperationKind.Consume) => Claim = claim;
    public ConsumedItemClaim Claim { get; }
}

public sealed class MaterialGatherOperation : MaterialOperation
{
    internal MaterialGatherOperation(ulong sequence, GatherIssueEvidence evidence) : base(sequence, evidence.Command.CommandId, MaterialOperationKind.Gather) => Evidence = evidence;
    public GatherIssueEvidence Evidence { get; }
}

public sealed class MaterialTransferOperation : MaterialOperation
{
    internal MaterialTransferOperation(ulong sequence, CommandEnvelope command, TrustedCommandDelivery delivery)
        : base(sequence, command.CommandId, MaterialOperationKind.Transfer) { Command = command; Delivery = delivery; }
    public CommandEnvelope Command { get; }
    public TrustedCommandDelivery Delivery { get; }
}

public sealed class MaterialDeliveryOperation : MaterialOperation
{
    internal MaterialDeliveryOperation(ulong sequence, EntityId commandId, EntityId actorId,
        TrustedCommandDelivery delivery, SourceDeliveryRequest request) : base(sequence, commandId, MaterialOperationKind.SourceDelivery)
    { ActorId = actorId; Delivery = delivery; Request = request; }
    public EntityId ActorId { get; }
    public TrustedCommandDelivery Delivery { get; }
    public SourceDeliveryRequest Request { get; }
}

/// <summary>Reserved construction retirement with genuine work and installation qualification.</summary>
public sealed class MaterialConstructionOperation : MaterialOperation
{
    internal MaterialConstructionOperation(ulong sequence, ConstructionCompletionEvidence evidence)
        : base(sequence, evidence.Command.CommandId, MaterialOperationKind.Construction) => Evidence = evidence;
    public ConstructionCompletionEvidence Evidence { get; }
}

/// <summary>One actual qualified change between coarse and detailed terrain custody.</summary>
public sealed class MaterialTerrainRepresentationOperation : MaterialOperation
{
    internal MaterialTerrainRepresentationOperation(ulong sequence, OpenFantasy.Core.Geography.TerrainRepresentationEvidence evidence)
        : base(sequence, evidence.CauseId, MaterialOperationKind.TerrainRepresentation) => Evidence = evidence;
    public OpenFantasy.Core.Geography.TerrainRepresentationEvidence Evidence { get; }
}

/// <summary>Actual delivery of one complete reserved transport stage.</summary>
public sealed class MaterialReservedTransportOperation : MaterialOperation
{
    internal MaterialReservedTransportOperation(ulong sequence, OpenFantasy.Core.Geography.ReservoirTransportEvidence evidence)
        : base(sequence, evidence.CauseId, MaterialOperationKind.ReservedTransport) => Evidence = evidence;
    public OpenFantasy.Core.Geography.ReservoirTransportEvidence Evidence { get; }
}

/// <summary>
/// One bilateral approved custody change of actual raw or produced stock with its exact result.
/// The command and the acceptance are separate permanent identities of this one operation.
/// </summary>
public sealed class MaterialConsentedTransferOperation : MaterialOperation
{
    internal MaterialConsentedTransferOperation(ulong sequence,
        OpenFantasy.Core.Authority.ConsentedMaterial.ConsentedMaterialApproval approval, EntityId movedLotId)
        : base(sequence, approval.Command.CommandId, MaterialOperationKind.ConsentedTransfer)
    { Approval = approval; MovedLotId = movedLotId; }
    public OpenFantasy.Core.Authority.ConsentedMaterial.ConsentedMaterialApproval Approval { get; }
    public EntityId AcceptanceId => Approval.Acceptance.AcceptanceId;
    /// <summary>The whole original lot, or the child spawned for a partial amount.</summary>
    public EntityId MovedLotId { get; }
}

/// <summary>One actual mining or gathering action qualified by the shared excavation owner.</summary>
public sealed class MaterialExcavationWorkOperation : MaterialOperation
{
    internal MaterialExcavationWorkOperation(ulong sequence, OpenFantasy.Core.Terrain.ExcavationWorkEvidence evidence)
        : base(sequence, evidence.Command.CommandId, MaterialOperationKind.ExcavationWork) => Evidence = evidence;
    public OpenFantasy.Core.Terrain.ExcavationWorkEvidence Evidence { get; }
}

/// <summary>Goods side of an actual funded settlement, not gift approval or autonomous payment authority.</summary>
public sealed class MaterialSpotExchangeOperation : MaterialOperation
{
    internal MaterialSpotExchangeOperation(ulong sequence, OpenFantasy.Core.Markets.SpotSettlementEvidence evidence)
        : base(sequence, evidence.Acceptance.AcceptanceId, MaterialOperationKind.SpotExchange) => Evidence = evidence;
    public OpenFantasy.Core.Markets.SpotSettlementEvidence Evidence { get; }
}
