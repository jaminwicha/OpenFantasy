using System.Buffers.Binary;
using OpenFantasy.Core.Authority;

namespace OpenFantasy.Core.Worlds;

public static class WorldStepCodecV2
{
    public const int MaximumBatchByteLength = 256 * 1024;

    public static byte[] Encode(WorldStepBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.ProfileVersion != 2)
        {
            throw new ArgumentException("A newer rules profile requires its own step codec.", nameof(batch));
        }
        return CanonicalEncodingV1.Frame("world.step-batch", Number(2), batch.CauseId.ToByteArray(),
            batch.WorldId.ToByteArray(), batch.BranchId.ToByteArray(), Number(batch.ExpectedRevision),
            Number(batch.ExpectedAdvancingSteps), Number((ulong)batch.Kind),
            CanonicalEncodingV1.Frame("world.step-movement", batch.Movement.Select(value =>
                (ReadOnlyMemory<byte>)EncodeMovement(value)).ToArray()),
            CanonicalEncodingV1.Frame("world.step-actions", batch.Actions.Select(value =>
                (ReadOnlyMemory<byte>)WorldActionCodec.Encode(value)).ToArray()));
    }

    public static WorldStepBatch Decode(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumBatchByteLength, 10);
        reader.Tag("world.step-batch"u8);
        if (reader.Number() != 2) throw new FormatException("Unsupported gathering step version.");
        EntityId cause = reader.Identity();
        EntityId world = reader.Identity();
        EntityId branch = reader.Identity();
        ulong revision = reader.Number();
        ulong steps = reader.Number();
        ulong rawKind = reader.Number();
        if (rawKind is not 1 and not 2)
        {
            throw new FormatException("Unsupported world batch kind.");
        }
        List<WorldMovementInput> movement = DecodeMovement(reader.Field(MaximumBatchByteLength));
        List<WorldActionCommand> actions = DecodeActions(reader.Field(MaximumBatchByteLength));
        reader.Complete();
        try
        {
            return new WorldStepBatch(cause, world, branch, revision, steps, (WorldBatchKind)rawKind, movement, actions, profileVersion: 2);
        }
        catch (ArgumentException error)
        {
            throw new FormatException("The world batch violates its invariants.", error);
        }
    }

    private static byte[] EncodeMovement(WorldMovementInput input) => CanonicalEncodingV1.Frame("world.control-sample",
        input.Delivery.PrincipalId.ToByteArray(), input.Delivery.SessionId.ToByteArray(), input.ActorId.ToByteArray(),
        Signed(input.DeltaX), Signed(input.DeltaY));

    private static List<WorldMovementInput> DecodeMovement(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumBatchByteLength, WorldStepBatch.MaximumActorsPerBatch + 1);
        reader.Tag("world.step-movement"u8);
        List<WorldMovementInput> values = [];
        while (reader.Remaining != 0)
        {
            var sample = new WorldFrameReader(reader.Field(256), 256, 6);
            sample.Tag("world.control-sample"u8);
            var delivery = new TrustedCommandDelivery(sample.Identity(), sample.Identity());
            EntityId actor = sample.Identity();
            long x = sample.Signed();
            long y = sample.Signed();
            sample.Complete();
            if (values.Count != 0 && actor.CompareTo(values[^1].ActorId) <= 0)
            {
                throw new FormatException("Movement samples are not in strict actor order.");
            }
            try
            {
                values.Add(new WorldMovementInput(delivery, actor, x, y));
            }
            catch (ArgumentException error)
            {
                throw new FormatException("The movement sample violates its invariants.", error);
            }
        }
        reader.Complete();
        return values;
    }

    private static List<WorldActionCommand> DecodeActions(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumBatchByteLength, WorldStepBatch.MaximumActorsPerBatch + 1);
        reader.Tag("world.step-actions"u8);
        List<WorldActionCommand> values = [];
        while (reader.Remaining != 0)
        {
            WorldActionCommand command = WorldActionCodec.Decode(reader.Field(WorldActionCodec.MaximumCommandByteLength));
            if (values.Count != 0 && command.ActorId.CompareTo(values[^1].ActorId) <= 0)
            {
                throw new FormatException("Discrete actions are not in strict actor order.");
            }
            values.Add(command);
        }
        reader.Complete();
        return values;
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    private static byte[] Signed(long value)
    {
        byte[] result = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(result, value);
        return result;
    }
}
