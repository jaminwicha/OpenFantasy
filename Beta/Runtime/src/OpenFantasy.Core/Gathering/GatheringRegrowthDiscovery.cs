using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Gathering;

/// <summary>A due occurrence and its actual finite-stock prerequisite, not a grant of new material.</summary>
public sealed record GatherRegrowthOccurrence(EntityId NodeId, ulong NodeRevision, ulong DueInterval,
    ulong RequiredUnits, ulong UnissuedUnits)
{
    public bool CanRegrow => UnissuedUnits >= RequiredUnits;
}

/// <summary>Derived discovery from one immutable gathering owner at a selected process interval.</summary>
public sealed class GatheringRegrowthDiscovery
{
    private readonly GatheringSnapshot _source;

    internal GatheringRegrowthDiscovery(GatheringSnapshot source, ulong throughInterval)
    {
        if (throughInterval < source.AdvancingStep)
        {
            throw new ArgumentOutOfRangeException(nameof(throughInterval), "Discovery cannot precede the owner's process interval.");
        }
        _source = source;
        ThroughInterval = throughInterval;
        Occurrences = Array.AsReadOnly(source.Nodes
            .Where(node => node.RespawnAtAdvancingStep is { } due && due <= throughInterval)
            .Select(node => new GatherRegrowthOccurrence(node.NodeId, node.Revision,
                node.RespawnAtAdvancingStep!.Value,
                checked((ulong)node.Definition.MaxSwings * source.Tools
                    .Where(tool => tool.Kind == node.Definition.RequiredToolKind).Max(tool => tool.Power)),
                node.UnitCapacity - node.NextUnissuedUnit))
            .OrderBy(value => value.DueInterval).ThenBy(value => value.NodeId).ToArray());
    }

    public ulong ThroughInterval { get; }
    public ReadOnlyCollection<GatherRegrowthOccurrence> Occurrences { get; }

    /// <summary>Reference affinity to the complete immutable source; not persistence or authentication.</summary>
    public bool IsFor(GatheringSnapshot source) => ReferenceEquals(_source, source);

    internal IEnumerable<GatherNodeState> RegrowNodes(GatheringSnapshot source)
    {
        if (!IsFor(source))
        {
            throw new InvalidOperationException("Regrowth discovery belongs to a different gathering owner.");
        }
        var eligible = Occurrences.Where(value => value.CanRegrow).Select(value => value.NodeId).ToHashSet();
        // Keep the owner's original node order and exact Regrow behavior, including revision overflow.
        return source.Nodes.Select(node => eligible.Contains(node.NodeId) ? node.Regrow() : node);
    }
}
