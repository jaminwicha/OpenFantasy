using System.Collections.ObjectModel;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Construction;

namespace OpenFantasy.Core.Production;

/// <summary>Bounded ordered evidence in the canonical Production owner; no independent inventory authority.</summary>
public sealed class MaterialOperationHistory
{
    public const int MaximumOperations = 512;
    internal MaterialOperationHistory(ProductionContinuationContext context, IEnumerable<MaterialOperation> operations)
    {
        if (!context.SupportsMaterialOperations) throw new ArgumentException("Material history requires its explicit continuation interpretation.");
        var values = new List<MaterialOperation>();
        var commands = new HashSet<EntityId>();
        GatheringSnapshot? gathering = context.GatheringBaseline;
        foreach (MaterialOperation operation in operations)
        {
            ArgumentNullException.ThrowIfNull(operation);
            if (values.Count == MaximumOperations || operation.Sequence != checked(context.BaseProductionRevision + (ulong)values.Count + 1)
                || !commands.Add(operation.CommandId))
                throw new ArgumentException("Material operations require bounded exact order and distinct permanent commands.");
            if (operation is MaterialConstructionOperation && !context.SupportsConstruction)
                throw new ArgumentException("Construction operations require the explicit V5 capability.");
            if (operation is MaterialGatherOperation gather)
                gathering = gather.Evidence.Replay(gathering ?? throw new ArgumentException("Gather issuance requires a captured source owner."));
            values.Add(operation);
        }
        Operations = values.AsReadOnly();
        Gathering = gathering;
        if (values.OfType<MaterialConstructionOperation>().Count() > ConstructionSnapshot.MaximumProjects)
            throw new ArgumentException("A segment retains at most one completion per bounded construction project.");
        ConstructionEvidence = Array.AsReadOnly(values.OfType<MaterialConstructionOperation>().Select(value => value.Evidence).ToArray());
        GatherEvidence = Array.AsReadOnly(values.OfType<MaterialGatherOperation>().Select(value => value.Evidence).ToArray());
    }
    public ReadOnlyCollection<MaterialOperation> Operations { get; }
    public GatheringSnapshot? Gathering { get; }
    public ReadOnlyCollection<ConstructionCompletionEvidence> ConstructionEvidence { get; }
    public ReadOnlyCollection<GatherIssueEvidence> GatherEvidence { get; }
    internal MaterialOperationHistory Append(ProductionContinuationContext context, MaterialOperation operation) => new(context, [.. Operations, operation]);
}
