using System.Collections.ObjectModel;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Terrain;

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
        TerrainRepresentationBoundary? terrain = context.TerrainRepresentation;
        ExcavationMaterialBoundary? excavation = context.Excavation;
        var deliveredStages = new HashSet<EntityId>();
        foreach (MaterialOperation operation in operations)
        {
            ArgumentNullException.ThrowIfNull(operation);
            if (values.Count == MaximumOperations || operation.Sequence != checked(context.BaseProductionRevision + (ulong)values.Count + 1)
                || !commands.Add(operation.CommandId))
                throw new ArgumentException("Material operations require bounded exact order and distinct permanent commands.");
            if (context.SupportsExcavation && operation is MaterialGatherOperation or MaterialConstructionOperation)
                throw new ArgumentException("Excavation work must use the shared terrain, effort and actor opportunity owner.");
            if (operation is MaterialExcavationWorkOperation work)
            {
                excavation = (excavation ?? throw new ArgumentException("Excavation work requires its explicit V8 capability."))
                    .Apply(work.Evidence);
                gathering = work.Evidence.ReplayGathering(gathering ?? throw new ArgumentException("Excavation requires shared effort."));
            }
            if (operation is MaterialReservedTransportOperation transport
                && (context.ReservedTransport is not { } binding || transport.Evidence.CatalogHash != binding.CatalogHash
                    || !deliveredStages.Add(transport.Evidence.StageId)))
                throw new ArgumentException("Reserved transport requires its exact V7 catalog and distinct actual stage deliveries.");
            if (operation is MaterialConstructionOperation && !context.SupportsConstruction)
                throw new ArgumentException("Construction operations require the explicit V5 capability.");
            if (operation is MaterialTerrainRepresentationOperation representation)
                terrain = (terrain ?? throw new ArgumentException("Terrain representation requires its explicit V6 capability."))
                    .Apply(representation.Evidence);
            if (operation is MaterialGatherOperation gather)
                gathering = gather.Evidence.Replay(gathering ?? throw new ArgumentException("Gather issuance requires a captured source owner."));
            values.Add(operation);
        }
        Operations = values.AsReadOnly();
        Gathering = gathering;
        TerrainRepresentation = terrain;
        Excavation = excavation;
        ExcavationEvidence = Array.AsReadOnly(values.OfType<MaterialExcavationWorkOperation>().Select(value => value.Evidence).ToArray());
        TransportEvidence = Array.AsReadOnly(values.OfType<MaterialReservedTransportOperation>().Select(value => value.Evidence).ToArray());
        TerrainEvidence = Array.AsReadOnly(values.OfType<MaterialTerrainRepresentationOperation>().Select(value => value.Evidence).ToArray());
        if (values.OfType<MaterialConstructionOperation>().Count() > ConstructionSnapshot.MaximumProjects)
            throw new ArgumentException("A segment retains at most one completion per bounded construction project.");
        ConstructionEvidence = Array.AsReadOnly(values.OfType<MaterialConstructionOperation>().Select(value => value.Evidence).ToArray());
        GatherEvidence = Array.AsReadOnly(values.OfType<MaterialGatherOperation>().Select(value => value.Evidence).ToArray());
    }
    public ReadOnlyCollection<MaterialOperation> Operations { get; }
    public GatheringSnapshot? Gathering { get; }
    public TerrainRepresentationBoundary? TerrainRepresentation { get; }
    public ExcavationMaterialBoundary? Excavation { get; }
    public ReadOnlyCollection<ExcavationWorkEvidence> ExcavationEvidence { get; }
    public ReadOnlyCollection<ReservoirTransportEvidence> TransportEvidence { get; }
    public ReadOnlyCollection<TerrainRepresentationEvidence> TerrainEvidence { get; }
    public ReadOnlyCollection<ConstructionCompletionEvidence> ConstructionEvidence { get; }
    public ReadOnlyCollection<GatherIssueEvidence> GatherEvidence { get; }
    internal MaterialOperationHistory Append(ProductionContinuationContext context, MaterialOperation operation) => new(context, [.. Operations, operation]);
}
