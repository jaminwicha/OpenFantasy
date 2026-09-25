using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Gathering;

public sealed class GatheringSnapshot
{
    public const int MaximumNodes = 32;
    public const int MaximumTools = 16;
    public const int MaximumActors = 64;
    public GatheringSnapshot(EntityId worldId, EntityId branchId, ulong advancingStep,
        IEnumerable<GatherNodeState> nodes, IEnumerable<GatherToolDefinition> tools, IEnumerable<GatherActorWork> actors)
    {
        Nodes = Copy(nodes, MaximumNodes, node => node.NodeId);
        Tools = Copy(tools, MaximumTools, tool => tool.Definition.DefinitionId);
        Actors = Copy(actors, MaximumActors, actor => actor.ActorId);
        if (worldId == branchId || Nodes.Select(node => node.SourceId).Distinct().Count() != Nodes.Count)
        {
            throw new ArgumentException("Gathering requires distinct scope and one cursor owner per source.");
        }
        var kinds = new Dictionary<EntityId, string> { [worldId] = "world", [branchId] = "branch" };
        var definitions = new Dictionary<EntityId, ContentHash>();
        var itemPins = new Dictionary<EntityId, ContentHash>();
        void Add(EntityId id, string kind)
        {
            if (kinds.TryGetValue(id, out string? existing) && existing != kind)
            {
                throw new ArgumentException("Gathering identities cannot alias incompatible local roles.");
            }
            kinds[id] = kind;
        }
        void AddItemPin(CountedItemDefinition definition)
        {
            Add(definition.DefinitionId, "item-definition");
            if (itemPins.TryGetValue(definition.DefinitionId, out ContentHash? version) && version != definition.Version)
            {
                throw new ArgumentException("A shared gathering item identity requires one exact version.");
            }
            itemPins[definition.DefinitionId] = definition.Version;
        }
        foreach (GatherNodeState node in Nodes)
        {
            Add(node.NodeId, "node");
            Add(node.SourceId, "source");
            Add(node.Definition.DefinitionId, "node-definition");
            AddItemPin(node.Definition.YieldDefinition);
            if (definitions.TryGetValue(node.Definition.DefinitionId, out ContentHash? pin) && pin != node.Definition.Version)
            {
                throw new ArgumentException("A shared gathering node definition requires one exact version.");
            }
            definitions[node.Definition.DefinitionId] = node.Definition.Version;
            if (node.DepletedAtStep > advancingStep || !Tools.Any(tool => tool.Kind == node.Definition.RequiredToolKind))
            {
                throw new ArgumentException("Node clocks and required tool admission must fit the gathering profile.");
            }
            uint maximumPower = Tools.Where(tool => tool.Kind == node.Definition.RequiredToolKind).Max(tool => tool.Power);
            ulong requiredReserve = checked((ulong)node.RemainingSwings * maximumPower);
            if (node.UnitCapacity - node.NextUnissuedUnit < requiredReserve)
            {
                throw new ArgumentException("A live node must retain enough admitted stock to finish its current cycle.");
            }
        }
        foreach (GatherToolDefinition tool in Tools)
        {
            AddItemPin(tool.Definition);
        }
        foreach (GatherActorWork actor in Actors)
        {
            Add(actor.ActorId, "actor");
        }
        WorldId = worldId;
        BranchId = branchId;
        AdvancingStep = advancingStep;
    }
    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public ulong AdvancingStep { get; }
    public ReadOnlyCollection<GatherNodeState> Nodes { get; }
    public ReadOnlyCollection<GatherToolDefinition> Tools { get; }
    public ReadOnlyCollection<GatherActorWork> Actors { get; }
    public ContentHash ContentHash => ContentHash.Compute(GatheringCodecV1.Encode(this));
    public GatherNodeState GetNode(EntityId nodeId) => Nodes.FirstOrDefault(node => node.NodeId == nodeId)
        ?? throw new ArgumentException("The resource node is outside this gathering profile.", nameof(nodeId));
    public GatherActorWork GetActorWork(EntityId actorId) => Actors.FirstOrDefault(actor => actor.ActorId == actorId)
        ?? throw new ArgumentException("The actor has no admitted gathering work state.", nameof(actorId));

    public GatheringSnapshot AdvanceStep(ulong nextOrdinal)
    {
        if (nextOrdinal != checked(AdvancingStep + 1))
        {
            throw new ArgumentException("Gathering advances exactly once per committed advancing world step.", nameof(nextOrdinal));
        }
        var nodes = Nodes.Select(node =>
        {
            uint maximumPower = Tools.Where(tool => tool.Kind == node.Definition.RequiredToolKind).Max(tool => tool.Power);
            ulong fullCycle = checked((ulong)node.Definition.MaxSwings * maximumPower);
            return node.RespawnAtAdvancingStep is { } due && due <= nextOrdinal
                && node.UnitCapacity - node.NextUnissuedUnit >= fullCycle ? node.Regrow() : node;
        });
        return new GatheringSnapshot(WorldId, BranchId, nextOrdinal, nodes, Tools, Actors.Select(actor => actor.Advance()));
    }

    public void ValidateForWorld(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.WorldId != WorldId || world.BranchId != BranchId || world.ProcessIntervals != AdvancingStep
            || !world.Inventory.Actors.SequenceEqual(Actors.Select(actor => actor.ActorId)))
        {
            throw new ArgumentException("Gathering scope, committed clock and complete actor work set must match the world.");
        }
        foreach (GatherToolDefinition tool in Tools)
        {
            if (!world.Inventory.Sources.Any(source => source.Definition == tool.Definition))
            {
                throw new ArgumentException("Gathering tools must resolve to an exact admitted counted-item pin.");
            }
        }
        foreach (GatherNodeState node in Nodes)
        {
            AdmittedCountedSource? source = world.Inventory.Sources.FirstOrDefault(value => value.SourceId == node.SourceId);
            if (source is null || source.Definition != node.Definition.YieldDefinition || source.UnitCapacity != node.UnitCapacity)
            {
                throw new ArgumentException("The node cursor must own an exact finite admitted source.");
            }
            if (ResourceInteractionQueries.QueryAnchor(world.Space, node.Feet) != SpatialOccupancy.Clear)
            {
                throw new ArgumentException("A resource anchor requires admitted clear terrain and a directly supported cell floor.");
            }
            if (world.Inventory.Custody.Lots.Any(lot => lot.Source.SourceId == node.SourceId && lot.Source.End > node.NextUnissuedUnit))
            {
                throw new ArgumentException("Issued gathering claims cannot extend beyond the retained source cursor.");
            }
        }
    }

    internal GatheringSnapshot SpendEffort(EntityId actorId) => new(WorldId, BranchId, AdvancingStep, Nodes, Tools,
        Actors.Select(actor => actor.ActorId == actorId ? actor.Spend() : actor));

    internal GatheringSnapshot Harvest(GatherNodeState node, GatherActorWork actor) => new(WorldId, BranchId, AdvancingStep,
        Nodes.Select(value => value.NodeId == node.NodeId ? node : value), Tools,
        Actors.Select(value => value.ActorId == actor.ActorId ? actor : value));

    private static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> values, int maximum, Func<T, EntityId> identity) where T : class
    {
        ArgumentNullException.ThrowIfNull(values);
        var result = new SortedDictionary<EntityId, T>();
        foreach (T value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            if (result.Count == maximum || !result.TryAdd(identity(value), value))
            {
                throw new ArgumentException("A gathering collection exceeds its bound or repeats an identity.", nameof(values));
            }
        }
        return Array.AsReadOnly(result.Values.ToArray());
    }
}
