using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Structures;

/// <summary>Canonical bounded work archives; decoding proves the retained continuation against its exact input graph.</summary>
public static class StructuralLoadCodec
{
    public const int MaximumArchiveBytes = 16 * 1024 * 1024;
    private const int MaximumElementBytes = 256;
    private const int MaximumBearingBytes = 192;

    public static byte[] EncodeGraph(StructuralLoadGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var elements = graph.Elements.Select(value => (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("structure.element",
            value.ElementId.ToByteArray(), Number(value.OwnMass.Value), Number(value.AppliedMass.Value),
            Number(value.RatedCapacity.Value), Number(value.ConditionParts), Number(value.IsFoundation ? 1UL : 0),
            Number(value.MinimumSupportCount))).ToArray();
        var bearings = graph.Bearings.Select(value => (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("structure.bearing",
            value.BearingId.ToByteArray(), value.SupportedElementId.ToByteArray(), value.SupportingElementId.ToByteArray(),
            Number(value.Weight))).ToArray();
        return Bounded(CanonicalEncodingV1.Frame("structure.load-graph.v1",
            CanonicalEncodingV1.Frame("structure.elements", elements), CanonicalEncodingV1.Frame("structure.bearings", bearings)));
    }

    public static byte[] Encode(StructuralLoadResolution resolution, ContentHash dependencyPin)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(dependencyPin);
        var failures = resolution.Failures.Select(value => (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("structure.failure",
            value.ElementId.ToByteArray(), Number((ulong)value.Reason), Wide(value.Load), Number(value.Pass))).ToArray();
        var loads = resolution.Graph.Elements.Select((value, index) => (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("structure.pending-load",
            value.ElementId.ToByteArray(), Wide(resolution.CurrentPassLoads[index]))).ToArray();
        return Bounded(CanonicalEncodingV1.Frame("structure.resolution.v1", dependencyPin.ToByteArray(), EncodeGraph(resolution.Graph),
            CanonicalEncodingV1.Frame("structure.removed", resolution.RemovedIds.Select(value => (ReadOnlyMemory<byte>)value.ToByteArray()).ToArray()),
            Number(resolution.Pass), Number((ulong)resolution.Cursor), Number(resolution.TotalEvaluations),
            Number(resolution.IsComplete ? 1UL : 0), CanonicalEncodingV1.Frame("structure.failures", failures),
            CanonicalEncodingV1.Frame("structure.pending-loads", loads)));
    }

    public static StructuralLoadResolution Decode(ReadOnlySpan<byte> bytes, ConstructionBearingModel expectedModel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedModel);
        return Decode(bytes, expectedModel.InputPin, expectedModel.Graph, cancellationToken);
    }

    public static StructuralLoadResolution Decode(ReadOnlySpan<byte> bytes, ContentHash expectedDependencyPin,
        StructuralLoadGraph expectedGraph, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedDependencyPin);
        ArgumentNullException.ThrowIfNull(expectedGraph);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumArchiveBytes, 10);
            reader.Tag("structure.resolution.v1"u8);
            if (!reader.Exact(32).SequenceEqual(expectedDependencyPin.ToByteArray()))
            {
                throw new FormatException("The structural continuation belongs to different retained dependencies.");
            }
            ReadOnlySpan<byte> graphBytes = reader.Field(MaximumArchiveBytes);
            if (!graphBytes.SequenceEqual(EncodeGraph(expectedGraph)))
            {
                throw new FormatException("The structural continuation substitutes its expected load graph.");
            }
            StructuralLoadGraph graph = expectedGraph;
            EntityId[] removed = ReadRemoved(reader.Field(MaximumArchiveBytes));
            uint pass = checked((uint)reader.Number());
            int cursor = checked((int)reader.Number());
            ulong evaluations = reader.Number();
            bool complete = ReadBoolean(ref reader);
            StructuralFailure[] failures = ReadFailures(reader.Field(MaximumArchiveBytes));
            UInt128[] loads = ReadLoads(reader.Field(MaximumArchiveBytes), graph);
            reader.Complete();
            StructuralLoadResolution restored = StructuralLoadResolution.Restore(graph, removed, pass, cursor,
                evaluations, complete, failures, loads, cancellationToken);
            if (!Encode(restored, expectedDependencyPin).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The structural continuation is not canonical.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The structural continuation violates its retained work contract.", error);
        }
    }

    public static StructuralLoadGraph DecodeGraph(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken = default)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumArchiveBytes, 3);
            reader.Tag("structure.load-graph.v1"u8);
            StructuralElement[] elements = ReadElements(reader.Field(MaximumArchiveBytes), cancellationToken);
            StructuralBearing[] bearings = ReadBearings(reader.Field(MaximumArchiveBytes), cancellationToken);
            reader.Complete();
            var graph = new StructuralLoadGraph(elements, bearings);
            if (!EncodeGraph(graph).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The structural graph is not canonical.");
            }
            return graph;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The structural graph violates its declared bounds or topology.", error);
        }
    }

    private static StructuralElement[] ReadElements(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken)
    {
        var reader = new WorldFrameReader(bytes, MaximumArchiveBytes, StructuralLoadGraph.MaximumElements + 1);
        reader.Tag("structure.elements"u8);
        List<StructuralElement> result = [];
        while (reader.Remaining != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = new WorldFrameReader(reader.Field(MaximumElementBytes), MaximumElementBytes, 8);
            item.Tag("structure.element"u8);
            EntityId id = item.Identity();
            ulong own = item.Number();
            ulong applied = item.Number();
            ulong capacity = item.Number();
            uint condition = checked((uint)item.Number());
            bool foundation = ReadBoolean(ref item);
            ushort minimumSupports = checked((ushort)item.Number());
            item.Complete();
            result.Add(new(id, new(own), new(applied), new(capacity), condition, foundation, minimumSupports));
        }
        reader.Complete();
        return result.ToArray();
    }

    private static StructuralBearing[] ReadBearings(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken)
    {
        var reader = new WorldFrameReader(bytes, MaximumArchiveBytes, StructuralLoadGraph.MaximumBearings + 1);
        reader.Tag("structure.bearings"u8);
        List<StructuralBearing> result = [];
        while (reader.Remaining != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = new WorldFrameReader(reader.Field(MaximumBearingBytes), MaximumBearingBytes, 5);
            item.Tag("structure.bearing"u8);
            var bearing = new StructuralBearing(item.Identity(), item.Identity(), item.Identity(), item.Number());
            item.Complete();
            result.Add(bearing);
        }
        reader.Complete();
        return result.ToArray();
    }

    private static EntityId[] ReadRemoved(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumArchiveBytes, StructuralLoadGraph.MaximumElements + 1);
        reader.Tag("structure.removed"u8);
        List<EntityId> result = [];
        while (reader.Remaining != 0) { result.Add(reader.Identity()); }
        reader.Complete();
        return result.ToArray();
    }

    private static StructuralFailure[] ReadFailures(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumArchiveBytes, StructuralLoadGraph.MaximumElements + 1);
        reader.Tag("structure.failures"u8);
        List<StructuralFailure> result = [];
        while (reader.Remaining != 0)
        {
            var item = new WorldFrameReader(reader.Field(192), 192, 5);
            item.Tag("structure.failure"u8);
            var failure = new StructuralFailure(item.Identity(), (StructuralFailureReason)checked((int)item.Number()),
                ReadWide(ref item), checked((uint)item.Number()));
            item.Complete();
            result.Add(failure);
        }
        reader.Complete();
        return result.ToArray();
    }

    private static UInt128[] ReadLoads(ReadOnlySpan<byte> bytes, StructuralLoadGraph graph)
    {
        var reader = new WorldFrameReader(bytes, MaximumArchiveBytes, StructuralLoadGraph.MaximumElements + 1);
        reader.Tag("structure.pending-loads"u8);
        if (reader.Remaining != graph.Elements.Count) { throw new FormatException("Every retained element needs one pending load."); }
        var result = new UInt128[graph.Elements.Count];
        for (int index = 0; index < result.Length; index++)
        {
            var item = new WorldFrameReader(reader.Field(128), 128, 3);
            item.Tag("structure.pending-load"u8);
            if (item.Identity() != graph.Elements[index].ElementId) { throw new FormatException("Pending loads must follow canonical element order."); }
            result[index] = ReadWide(ref item);
            item.Complete();
        }
        reader.Complete();
        return result;
    }

    private static bool ReadBoolean(ref WorldFrameReader reader) => reader.Number() switch
    {
        0 => false,
        1 => true,
        _ => throw new FormatException("A structural boolean must have its canonical integer value.")
    };

    private static UInt128 ReadWide(ref WorldFrameReader reader)
    {
        ReadOnlySpan<byte> value = reader.Exact(16);
        return ((UInt128)BinaryPrimitives.ReadUInt64BigEndian(value) << 64) | BinaryPrimitives.ReadUInt64BigEndian(value[8..]);
    }

    private static byte[] Wide(UInt128 value)
    {
        byte[] bytes = new byte[16];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, (ulong)(value >> 64));
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(8), (ulong)(value & ulong.MaxValue));
        return bytes;
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);

    private static byte[] Bounded(byte[] bytes) => bytes.Length <= MaximumArchiveBytes ? bytes
        : throw new ArgumentException("The structural archive exceeds its retained byte bound.");
}
