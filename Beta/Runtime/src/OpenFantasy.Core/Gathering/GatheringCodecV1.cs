using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Gathering;

public static class GatheringCodecV1
{
    public const int MaximumSnapshotByteLength = 262144;
    public static byte[] Encode(GatheringSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Frame("gather.snapshot", Number(1), Id(snapshot.WorldId), Id(snapshot.BranchId), Number(snapshot.AdvancingStep),
            Rules(), Frame("gather.nodes", snapshot.Nodes.Select(node => (ReadOnlyMemory<byte>)EncodeNode(node)).ToArray()),
            Frame("gather.tools", snapshot.Tools.Select(tool => (ReadOnlyMemory<byte>)EncodeTool(tool)).ToArray()),
            Frame("gather.actors", snapshot.Actors.Select(actor => (ReadOnlyMemory<byte>)Frame("gather.actor-work", Number(1),
                Id(actor.ActorId), Number(actor.Effort))).ToArray()));
    }
    public static GatheringSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumSnapshotByteLength, 9);
            reader.Tag("gather.snapshot"u8);
            reader.Version();
            EntityId world = reader.Identity();
            EntityId branch = reader.Identity();
            ulong step = reader.Number();
            if (!reader.Field(256).SequenceEqual(Rules()))
            {
                throw new FormatException("Unsupported gathering work profile.");
            }
            var nodesReader = new WorldFrameReader(reader.Field(65536), 65536, GatheringSnapshot.MaximumNodes + 1);
            nodesReader.Tag("gather.nodes"u8);
            List<GatherNodeState> nodes = [];
            while (nodesReader.Remaining != 0)
            {
                GatherNodeState node = DecodeNode(nodesReader.Field(1024));
                if (nodes.Count != 0 && node.NodeId <= nodes[^1].NodeId)
                {
                    throw new FormatException("Node order is noncanonical.");
                }
                nodes.Add(node);
            }
            nodesReader.Complete();
            var toolsReader = new WorldFrameReader(reader.Field(8192), 8192, GatheringSnapshot.MaximumTools + 1);
            toolsReader.Tag("gather.tools"u8);
            List<GatherToolDefinition> tools = [];
            while (toolsReader.Remaining != 0)
            {
                GatherToolDefinition tool = DecodeTool(toolsReader.Field(256));
                if (tools.Count != 0 && tool.Definition.DefinitionId <= tools[^1].Definition.DefinitionId)
                {
                    throw new FormatException("Tool order is noncanonical.");
                }
                tools.Add(tool);
            }
            toolsReader.Complete();
            var actorsReader = new WorldFrameReader(reader.Field(16384), 16384, GatheringSnapshot.MaximumActors + 1);
            actorsReader.Tag("gather.actors"u8);
            List<GatherActorWork> actors = [];
            while (actorsReader.Remaining != 0)
            {
                var actorReader = new WorldFrameReader(actorsReader.Field(128), 128, 4);
                actorReader.Tag("gather.actor-work"u8);
                actorReader.Version();
                EntityId id = actorReader.Identity();
                ulong effort = actorReader.Number();
                actorReader.Complete();
                if (effort > GatherRulesV1.MaximumEffort || actors.Count != 0 && id <= actors[^1].ActorId)
                {
                    throw new FormatException("Actor work is excessive or noncanonical.");
                }
                actors.Add(new GatherActorWork(id, (uint)effort));
            }
            actorsReader.Complete();
            reader.Complete();
            var result = new GatheringSnapshot(world, branch, step, nodes, tools, actors);
            if (!Encode(result).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("Gathering bytes are noncanonical.");
            }
            return result;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("Gathering state violates its bounded invariants.", error);
        }
    }

    internal static byte[] EncodeDefinition(GatherNodeDefinition definition) => Frame("gather.node-definition", Number(1),
        Id(definition.DefinitionId), Number((ulong)definition.Kind), Id(definition.YieldDefinition.DefinitionId),
        definition.YieldDefinition.Version.ToByteArray(), Number((ulong)definition.MaxSwings), Number(definition.RegrowthDelaySteps),
        Number(definition.StumpSteps), Number((ulong)definition.RequiredToolKind));
    private static GatherNodeDefinition DecodeDefinition(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, 512, 10);
        reader.Tag("gather.node-definition"u8);
        reader.Version();
        EntityId id = reader.Identity();
        ulong kind = reader.Number();
        var pin = new CountedItemDefinition(reader.Identity(), Hash(reader.Exact(32)));
        if (kind is < 1 or > 2)
        {
            throw new FormatException("Unsupported resource kind.");
        }
        var result = new GatherNodeDefinition(id, (GatherNodeKind)kind, pin);
        if (reader.Number() != (ulong)result.MaxSwings || reader.Number() != result.RegrowthDelaySteps
            || reader.Number() != result.StumpSteps || reader.Number() != (ulong)result.RequiredToolKind)
        {
            throw new FormatException("Unsupported source gathering definition values.");
        }
        reader.Complete();
        return result;
    }
    private static byte[] EncodeNode(GatherNodeState node) => Frame("gather.node-state", Number(1), Id(node.NodeId),
        EncodeDefinition(node.Definition), Id(node.SourceId), Number(node.UnitCapacity), Signed(node.Feet.X), Signed(node.Feet.Y),
        Signed(node.Feet.Z), Number(node.Revision), Number(node.NextUnissuedUnit), Number((ulong)node.RemainingSwings),
        node.DepletedAtStep is { } depleted ? Number(depleted) : []);
    private static GatherNodeState DecodeNode(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, 1024, 13);
        reader.Tag("gather.node-state"u8);
        reader.Version();
        EntityId id = reader.Identity();
        GatherNodeDefinition definition = DecodeDefinition(reader.Field(512));
        EntityId source = reader.Identity();
        ulong capacity = reader.Number();
        var feet = new SubunitPosition(reader.Signed(), reader.Signed(), reader.Signed());
        ulong revision = reader.Number();
        ulong cursor = reader.Number();
        ulong swings = reader.Number();
        ReadOnlySpan<byte> depleted = reader.Field(8);
        reader.Complete();
        if (swings > (ulong)definition.MaxSwings || depleted.Length is not 0 and not 8)
        {
            throw new FormatException("Invalid node swings or depletion width.");
        }
        return new GatherNodeState(id, definition, source, capacity, feet, revision, cursor, (int)swings,
            depleted.IsEmpty ? null : BinaryPrimitives.ReadUInt64BigEndian(depleted));
    }
    private static byte[] EncodeTool(GatherToolDefinition tool) => Frame("gather.tool", Number(1), Id(tool.Definition.DefinitionId),
        tool.Definition.Version.ToByteArray(), Number((ulong)tool.Kind), Number(tool.Power));
    private static GatherToolDefinition DecodeTool(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, 256, 6);
        reader.Tag("gather.tool"u8);
        reader.Version();
        var pin = new CountedItemDefinition(reader.Identity(), Hash(reader.Exact(32)));
        ulong kind = reader.Number();
        ulong power = reader.Number();
        reader.Complete();
        if (kind is < 1 or > 2 || power is < 1 or > 3)
        {
            throw new FormatException("Unsupported source tool kind or power.");
        }
        return new GatherToolDefinition(pin, (GatherToolKind)kind, (uint)power);
    }
    private static byte[] Rules() => Frame("gather.rules", Number(1), Number(GatherRulesV1.MaximumEffort),
        Number(GatherRulesV1.EffortPerAdvancingStep), Number(GatherRulesV1.EffortPerSwing),
        Number((ulong)GatherRulesV1.ReachSubunits), Number(GatherRulesV1.StumpSteps));
    private static ContentHash Hash(ReadOnlySpan<byte> bytes) => new(Convert.ToHexString(bytes).ToLowerInvariant());
    private static byte[] Id(EntityId id) => id.ToByteArray();
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    private static byte[] Signed(long value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }
    private static byte[] Frame(string tag, params ReadOnlyMemory<byte>[] fields) => CanonicalEncodingV1.Frame(tag, fields);
}
