using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Structures;

/// <summary>
/// Conservative connected load dependencies, including every member of each touched rigid assembly.
/// Both support directions matter: losing one contact may redistribute mass onto another support and
/// affect its other dependents. This catalog is neither swept collision closure nor a claim that its
/// elements fall; it grants no authority to change material, physical placement or world time.
/// </summary>
public sealed class ConstructionLoadNeighborhood
{
    private ConstructionLoadNeighborhood(ConstructionBearingModel model, IEnumerable<EntityId> seeds,
        HashSet<EntityId> elements, HashSet<EntityId> structures)
    {
        InputPin = model.InputPin;
        ChangedElementIds = Array.AsReadOnly(seeds.Order().ToArray());
        ElementIds = Array.AsReadOnly(elements.Order().ToArray());
        StructureIds = Array.AsReadOnly(structures.Order().ToArray());
        ActorIds = Array.AsReadOnly(model.Actors.Where(actor => elements.Contains(actor.ElementId))
            .Select(actor => actor.ActorId).Distinct().Order().ToArray());
    }

    /// <summary>Exact source model pin; the neighborhood is derived without an independent saved authority.</summary>
    public ContentHash InputPin { get; }
    public ReadOnlyCollection<EntityId> ChangedElementIds { get; }
    public ReadOnlyCollection<EntityId> ElementIds { get; }
    public ReadOnlyCollection<EntityId> StructureIds { get; }
    public ReadOnlyCollection<EntityId> ActorIds { get; }

    public static ConstructionLoadNeighborhood Create(ConstructionBearingModel model, IEnumerable<EntityId> changedElementIds)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(changedElementIds);
        EntityId[] seeds = changedElementIds.Take(StructuralLoadGraph.MaximumElements + 1).ToArray();
        var neighbors = model.Graph.Elements.ToDictionary(element => element.ElementId, _ => new List<EntityId>());
        if (seeds.Length is 0 or > StructuralLoadGraph.MaximumElements
            || seeds.Distinct().Count() != seeds.Length || seeds.Any(seed => !neighbors.ContainsKey(seed)))
        {
            throw new ArgumentException("Load-neighborhood seeds must be a nonempty bounded unique set of known graph elements.", nameof(changedElementIds));
        }
        foreach (StructuralBearing bearing in model.Graph.Bearings)
        {
            neighbors[bearing.SupportedElementId].Add(bearing.SupportingElementId);
            neighbors[bearing.SupportingElementId].Add(bearing.SupportedElementId);
        }
        var assemblies = model.Members.GroupBy(member => member.StructureId).ToDictionary(group => group.Key,
            group => group.Select(member => member.ElementId).ToArray());
        var membership = model.Members.ToDictionary(member => member.ElementId, member => member.StructureId);
        HashSet<EntityId> included = [];
        HashSet<EntityId> structures = [];
        Queue<EntityId> pending = new();
        foreach (EntityId seed in seeds) { Include(seed); }
        while (pending.TryDequeue(out EntityId current))
        {
            foreach (EntityId neighbor in neighbors[current]) { Include(neighbor); }
            if (membership.TryGetValue(current, out EntityId structure) && structures.Add(structure))
            {
                // Expand each assembly once, then visit those members' external dependencies too.
                foreach (EntityId member in assemblies[structure]) { Include(member); }
            }
        }
        return new(model, seeds, included, structures);

        void Include(EntityId element)
        {
            if (!neighbors.ContainsKey(element))
            {
                throw new ArgumentException("Every retained assembly member must belong to the pinned load graph.", nameof(model));
            }
            if (included.Add(element)) { pending.Enqueue(element); }
        }
    }
}
