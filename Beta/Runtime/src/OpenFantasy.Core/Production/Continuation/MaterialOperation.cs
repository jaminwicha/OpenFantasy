using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Construction;

namespace OpenFantasy.Core.Production;

public enum MaterialOperationKind { Craft = 1, Consume = 2, Gather = 3, Transfer = 4, SourceDelivery = 5, Construction = 6 }

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
