using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Structures;

/// <summary>A complete finite directed acyclic support graph, ordered independently of input enumeration.</summary>
public sealed class StructuralLoadGraph
{
    public const int MaximumElements = 4096;
    public const int MaximumBearingsPerElement = 32;
    public const int MaximumBearings = MaximumElements * MaximumBearingsPerElement;
    private readonly Dictionary<EntityId, int> _indices;
    private readonly StructuralBearing[][] _outgoing;
    private readonly int[] _topologicalIndices;

    public StructuralLoadGraph(IEnumerable<StructuralElement> elements, IEnumerable<StructuralBearing> bearings)
    {
        ArgumentNullException.ThrowIfNull(elements);
        ArgumentNullException.ThrowIfNull(bearings);
        var ordered = new SortedDictionary<EntityId, StructuralElement>();
        foreach (StructuralElement element in elements)
        {
            ArgumentNullException.ThrowIfNull(element);
            if (ordered.Count == MaximumElements || !ordered.TryAdd(element.ElementId, element))
            {
                throw new ArgumentException("Structural elements require bounded unique identities.", nameof(elements));
            }
        }
        if (ordered.Count == 0)
        {
            throw new ArgumentException("A structural graph cannot be empty.", nameof(elements));
        }
        Elements = Array.AsReadOnly(ordered.Values.ToArray());
        _indices = Elements.Select((element, index) => (element.ElementId, index))
            .ToDictionary(value => value.ElementId, value => value.index);
        HashSet<EntityId> identities = [.. ordered.Keys];
        HashSet<(EntityId Supported, EntityId Supporting)> pairs = [];
        var sortedBearings = new SortedDictionary<EntityId, StructuralBearing>();
        var outgoing = Enumerable.Range(0, Elements.Count).Select(_ => new List<StructuralBearing>()).ToArray();
        foreach (StructuralBearing bearing in bearings)
        {
            ArgumentNullException.ThrowIfNull(bearing);
            if (sortedBearings.Count == MaximumBearings || !identities.Add(bearing.BearingId)
                || !_indices.TryGetValue(bearing.SupportedElementId, out int supported)
                || !_indices.ContainsKey(bearing.SupportingElementId)
                || !pairs.Add((bearing.SupportedElementId, bearing.SupportingElementId)))
            {
                throw new ArgumentException("Bearings need unique roles, complete endpoints and one edge per element pair.", nameof(bearings));
            }
            if (Elements[supported].IsFoundation || outgoing[supported].Count == MaximumBearingsPerElement)
            {
                throw new ArgumentException("Foundations terminate load paths; other elements have bounded bearings.", nameof(bearings));
            }
            sortedBearings.Add(bearing.BearingId, bearing);
            outgoing[supported].Add(bearing);
        }
        Bearings = Array.AsReadOnly(sortedBearings.Values.ToArray());
        _outgoing = outgoing.Select(values => values.OrderBy(value => value.BearingId).ToArray()).ToArray();
        _topologicalIndices = OrderTopologically();
        TopologicalOrder = Array.AsReadOnly(_topologicalIndices.Select(index => Elements[index].ElementId).ToArray());
        TotalInputMass = Elements.Aggregate((UInt128)0, (total, element) => total + element.InputMass);
    }

    public ReadOnlyCollection<StructuralElement> Elements { get; }
    public ReadOnlyCollection<StructuralBearing> Bearings { get; }
    public ReadOnlyCollection<EntityId> TopologicalOrder { get; }
    public UInt128 TotalInputMass { get; }

    internal int IndexOf(EntityId elementId) => _indices.TryGetValue(elementId, out int index) ? index
        : throw new ArgumentException("The element is outside this structural graph.", nameof(elementId));
    internal int IndexAtCursor(int cursor) => _topologicalIndices[cursor];
    internal IReadOnlyList<StructuralBearing> Outgoing(int index) => _outgoing[index];

    private int[] OrderTopologically()
    {
        var incomingCounts = new int[Elements.Count];
        foreach (StructuralBearing bearing in Bearings)
        {
            incomingCounts[IndexOf(bearing.SupportingElementId)]++;
        }
        var ready = new SortedSet<EntityId>(Elements.Where((_, index) => incomingCounts[index] == 0)
            .Select(element => element.ElementId));
        List<int> order = [];
        while (ready.Count != 0)
        {
            EntityId identity = ready.Min;
            ready.Remove(identity);
            int index = IndexOf(identity);
            order.Add(index);
            foreach (StructuralBearing bearing in _outgoing[index])
            {
                int supporting = IndexOf(bearing.SupportingElementId);
                if (--incomingCounts[supporting] == 0)
                {
                    ready.Add(bearing.SupportingElementId);
                }
            }
        }
        if (order.Count != Elements.Count)
        {
            throw new ArgumentException("Structural bearing cycles cannot determine a load order.");
        }
        return order.ToArray();
    }
}
