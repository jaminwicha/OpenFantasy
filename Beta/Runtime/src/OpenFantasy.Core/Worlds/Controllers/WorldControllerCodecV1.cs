namespace OpenFantasy.Core.Worlds;

/// <summary>Canonical OFC framing for controller ownership and host evidence; decoding authenticates shape, not history.</summary>
public static class WorldControllerCodecV1
{
    public const int MaximumStateByteLength = 4096;
    public const int MaximumEventByteLength = 512;
    private const int MaximumAssignmentByteLength = 192;

    public static byte[] Encode(WorldControllerState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        byte[] rows = CanonicalEncodingV1.Frame("world.controller-assignments.v1", state.Assignments.Select(row =>
            (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("world.controller-assignment.v1", row.ActorId.ToByteArray(),
                Number(row.Revision), Number((ulong)row.Mode), Optional(row.DisconnectId))).ToArray());
        byte[] bytes = CanonicalEncodingV1.Frame("world.controllers.v1", Number(1), WorldControllerState.RulesPin.ToByteArray(), rows);
        if (bytes.Length > MaximumStateByteLength) throw new ArgumentException("Controller state exceeds its byte bound.", nameof(state));
        return bytes;
    }

    public static WorldControllerState Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumStateByteLength, 4);
            reader.Tag("world.controllers.v1"u8);
            reader.Version();
            if (!reader.Exact(32).SequenceEqual(WorldControllerState.RulesPin.ToByteArray()))
                throw new FormatException("Unknown controller interpretation pin.");
            var rows = new WorldFrameReader(reader.Field(MaximumStateByteLength), MaximumStateByteLength,
                WorldControllerState.MaximumActors + 1);
            reader.Complete();
            rows.Tag("world.controller-assignments.v1"u8);
            List<WorldControllerAssignment> assignments = [];
            while (rows.Remaining != 0)
            {
                var row = new WorldFrameReader(rows.Field(MaximumAssignmentByteLength), MaximumAssignmentByteLength, 5);
                row.Tag("world.controller-assignment.v1"u8);
                EntityId actor = row.Identity();
                ulong revision = row.Number();
                var mode = (WorldControllerMode)checked((int)row.Number());
                EntityId? episode = Optional(row.Field(EntityId.ByteLength));
                row.Complete();
                if (assignments.Count != 0 && actor.CompareTo(assignments[^1].ActorId) <= 0)
                    throw new FormatException("Controller assignment rows must be in strict actor identity order.");
                assignments.Add(new(actor, revision, mode, episode));
            }
            rows.Complete();
            var state = new WorldControllerState(assignments);
            if (!Encode(state).AsSpan().SequenceEqual(bytes)) throw new FormatException("Controller state is not canonical.");
            return state;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("Controller state violates its bounded structural contract.", error);
        }
    }

    public static byte[] EncodeEvent(WorldControllerEvent input)
    {
        ArgumentNullException.ThrowIfNull(input);
        byte[] bytes = CanonicalEncodingV1.Frame("world.controller-event.v1", Number(1), input.ActorId.ToByteArray(),
            Number((ulong)input.Kind), Number(input.ExpectedControlRevision), Optional(input.DisconnectId),
            Number(input.ElapsedHostMilliseconds), input.Reason is { } reason ? Number((ulong)reason) : []);
        if (bytes.Length > MaximumEventByteLength) throw new ArgumentException("Controller event exceeds its byte bound.", nameof(input));
        return bytes;
    }

    public static WorldControllerEvent DecodeEvent(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEventByteLength, 8);
            reader.Tag("world.controller-event.v1"u8);
            reader.Version();
            EntityId actor = reader.Identity();
            var kind = (WorldControllerEventKind)checked((int)reader.Number());
            ulong revision = reader.Number();
            EntityId? episode = Optional(reader.Field(EntityId.ByteLength));
            ulong elapsed = reader.Number();
            ReadOnlySpan<byte> reasonBytes = reader.Field(sizeof(ulong));
            WorldControllerDisconnectReason? reason = reasonBytes.IsEmpty ? null : reasonBytes.Length == sizeof(ulong)
                ? (WorldControllerDisconnectReason)checked((int)System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(reasonBytes))
                : throw new FormatException("A disconnect reason has the wrong fixed length.");
            reader.Complete();
            var input = new WorldControllerEvent(actor, kind, revision, episode, elapsed, reason);
            if (!EncodeEvent(input).AsSpan().SequenceEqual(bytes)) throw new FormatException("Controller event is not canonical.");
            return input;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("Controller event violates its bounded structural contract.", error);
        }
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    private static byte[] Optional(EntityId? identity) => identity?.ToByteArray() ?? [];
    private static EntityId? Optional(ReadOnlySpan<byte> bytes) => bytes.IsEmpty ? null : EntityId.FromBytes(bytes);
}
