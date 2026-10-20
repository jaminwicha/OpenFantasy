using System.Buffers.Binary;
using OpenFantasy.Core.Authority;

namespace OpenFantasy.Core.Worlds;

internal static class WorldStepFrameCodec
{
    public const int MaximumBatchByteLength = 256 * 1024;

    internal static byte[] Encode(WorldStepBatch batch, ushort profileVersion)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.ProfileVersion != profileVersion || profileVersion is not 3 and not 4 and not 5 and not 6 and not 7 and not 8 and not 9 and not 10 and not 11 and not 12 and not 13 and not 14 and not 15 and not 16 and not 17 and not 18 and not 19 and not 20 and not 21 and not 22 and not 23 and not 24 and not 25 and not 26 and not 27 and not 28 and not 30 and not 32 and not 33 and not 34 and not 35 and not 36 and not 37 and not 38 and not 39 and not 40 and not 41 and not 42)
        {
            throw new ArgumentException("A newer rules profile requires its own step codec.", nameof(batch));
        }
        List<ReadOnlyMemory<byte>> fields = [Number(profileVersion), batch.CauseId.ToByteArray(),
            batch.WorldId.ToByteArray(), batch.BranchId.ToByteArray(), Number(batch.ExpectedRevision),
            Number(batch.ExpectedAdvancingSteps), Number((ulong)batch.Kind),
            CanonicalEncodingV1.Frame("world.step-movement", batch.Movement.Select(value =>
                (ReadOnlyMemory<byte>)EncodeMovement(value)).ToArray()),
            CanonicalEncodingV1.Frame("world.step-actions", batch.Actions.Select(value =>
                (ReadOnlyMemory<byte>)WorldActionCodec.Encode(value)).ToArray())];
        if (profileVersion is WorldControllerAdmission.ProfileVersion or WorldRecoveryAdmission.ProfileVersion or WorldCareDispositionAdmission.ProfileVersion or WorldControlPermissionAdmission.ProfileVersion or WorldDelegatedControlAdmission.ProfileVersion or WorldPartyWithdrawalAdmission.ProfileVersion or WorldCognitionAdmission.ProfileVersion)
            fields.Add(batch.ControllerEvent is null ? [] : WorldControllerCodecV1.EncodeEvent(batch.ControllerEvent));
        if (profileVersion is WorldDelegatedControlAdmission.ProfileVersion or WorldPartyWithdrawalAdmission.ProfileVersion or WorldCognitionAdmission.ProfileVersion)
            fields.Add(batch.ControlAssignmentEvent is null ? [] : WorldEffectiveControlCodecV1.EncodeEvent(batch.ControlAssignmentEvent));
        byte[] bytes = CanonicalEncodingV1.Frame("world.step-batch", fields.ToArray());
        if (bytes.Length > MaximumBatchByteLength)
        {
            throw new ArgumentException("The world batch exceeds the admitted byte bound.", nameof(batch));
        }
        return bytes;
    }

    internal static WorldStepBatch Decode(ReadOnlySpan<byte> bytes, ushort profileVersion)
    {
        var reader = new WorldFrameReader(bytes, MaximumBatchByteLength, profileVersion is WorldDelegatedControlAdmission.ProfileVersion or WorldPartyWithdrawalAdmission.ProfileVersion or WorldCognitionAdmission.ProfileVersion ? 12 : profileVersion is WorldControllerAdmission.ProfileVersion or WorldRecoveryAdmission.ProfileVersion or WorldCareDispositionAdmission.ProfileVersion or WorldControlPermissionAdmission.ProfileVersion or WorldDelegatedControlAdmission.ProfileVersion or WorldPartyWithdrawalAdmission.ProfileVersion or WorldCognitionAdmission.ProfileVersion ? 11 : 10);
        reader.Tag("world.step-batch"u8);
        if (profileVersion is not 3 and not 4 and not 5 and not 6 and not 7 and not 8 and not 9 and not 10 and not 11 and not 12 and not 13 and not 14 and not 15 and not 16 and not 17 and not 18 and not 19 and not 20 and not 21 and not 22 and not 23 and not 24 and not 25 and not 26 and not 27 and not 28 and not 30 and not 32 and not 33 and not 34 and not 35 and not 36 and not 37 and not 38 and not 39 and not 40 and not 41 and not 42 || reader.Number() != profileVersion)
        {
            throw new FormatException("Unsupported world step version.");
        }
        EntityId cause = reader.Identity();
        EntityId world = reader.Identity();
        EntityId branch = reader.Identity();
        ulong revision = reader.Number();
        ulong steps = reader.Number();
        ulong rawKind = reader.Number();
        if (rawKind is not 1 and not 2
            && (rawKind != 4 || profileVersion is not 16 and not 17 and not 18)
            && (profileVersion is not 22 and not 23 and not 24 and not 25 and not 26 and not 27 and not 28 and not 30 and not 32 and not 33 and not 34 and not 35 and not 36 and not 37 and not 38 and not 39 and not 40 and not 41 and not 42 || rawKind is not 5 and not 6 and not 7)
            && (profileVersion is not 24 and not 25 and not 26 and not 27 and not 28 and not 30 and not 32 and not 33 and not 34 and not 35 and not 36 and not 37 and not 38 and not 39 and not 40 and not 41 and not 42 || rawKind != 8)
            && (profileVersion is not 25 and not 26 and not 27 and not 28 and not 30 and not 32 and not 33 and not 34 and not 35 and not 36 and not 37 and not 38 and not 39 and not 40 and not 41 and not 42 || rawKind != 9)
            && (profileVersion is not WorldControllerAdmission.ProfileVersion and not WorldRecoveryAdmission.ProfileVersion and not WorldCareDispositionAdmission.ProfileVersion and not WorldControlPermissionAdmission.ProfileVersion and not WorldDelegatedControlAdmission.ProfileVersion and not WorldPartyWithdrawalAdmission.ProfileVersion and not WorldCognitionAdmission.ProfileVersion || rawKind != 10)
            && (profileVersion is not WorldControlPermissionAdmission.ProfileVersion and not WorldDelegatedControlAdmission.ProfileVersion and not WorldPartyWithdrawalAdmission.ProfileVersion and not WorldCognitionAdmission.ProfileVersion || rawKind != 11)
            && (profileVersion is not WorldDelegatedControlAdmission.ProfileVersion and not WorldPartyWithdrawalAdmission.ProfileVersion and not WorldCognitionAdmission.ProfileVersion || rawKind != 12))
        {
            throw new FormatException("Unsupported world batch kind.");
        }
        List<WorldMovementInput> movement = DecodeMovement(reader.Field(MaximumBatchByteLength), profileVersion);
        List<WorldActionCommand> actions = DecodeActions(reader.Field(MaximumBatchByteLength));
        WorldControllerEvent? controllerEvent = null;
        if (profileVersion is WorldControllerAdmission.ProfileVersion or WorldRecoveryAdmission.ProfileVersion or WorldCareDispositionAdmission.ProfileVersion or WorldControlPermissionAdmission.ProfileVersion or WorldDelegatedControlAdmission.ProfileVersion or WorldPartyWithdrawalAdmission.ProfileVersion or WorldCognitionAdmission.ProfileVersion)
        {
            ReadOnlySpan<byte> eventBytes = reader.Field(WorldControllerCodecV1.MaximumEventByteLength);
            if (!eventBytes.IsEmpty) controllerEvent = WorldControllerCodecV1.DecodeEvent(eventBytes);
        }
        WorldControlAssignmentEvent? assignmentEvent = null;
        if (profileVersion is WorldDelegatedControlAdmission.ProfileVersion or WorldPartyWithdrawalAdmission.ProfileVersion or WorldCognitionAdmission.ProfileVersion)
        {
            var eventBytes = reader.Field(WorldEffectiveControlCodecV1.MaximumEventByteLength);
            if (!eventBytes.IsEmpty) assignmentEvent = WorldEffectiveControlCodecV1.DecodeEvent(eventBytes);
        }
        reader.Complete();
        try
        {
            return new WorldStepBatch(cause, world, branch, revision, steps, (WorldBatchKind)rawKind, movement, actions, profileVersion: profileVersion, controllerEvent: controllerEvent, controlAssignmentEvent: assignmentEvent);
        }
        catch (ArgumentException error)
        {
            throw new FormatException("The world batch violates its invariants.", error);
        }
    }

    private static byte[] EncodeMovement(WorldMovementInput input) => input.ControlWitness is null
        ? CanonicalEncodingV1.Frame("world.control-sample", input.Delivery.PrincipalId.ToByteArray(), input.Delivery.SessionId.ToByteArray(),
            input.ActorId.ToByteArray(), Signed(input.DeltaX), Signed(input.DeltaY))
        : CanonicalEncodingV1.Frame("world.control-sample", input.Delivery.PrincipalId.ToByteArray(), input.Delivery.SessionId.ToByteArray(),
            input.ActorId.ToByteArray(), Signed(input.DeltaX), Signed(input.DeltaY), WorldControlWitnessCodecV1.Encode(input.ControlWitness));

    private static List<WorldMovementInput> DecodeMovement(ReadOnlySpan<byte> bytes, ushort profileVersion)
    {
        var reader = new WorldFrameReader(bytes, MaximumBatchByteLength, WorldStepBatch.MaximumActorsPerBatch + 1);
        reader.Tag("world.step-movement"u8);
        List<WorldMovementInput> values = [];
        while (reader.Remaining != 0)
        {
            var sample = profileVersion is WorldDelegatedControlAdmission.ProfileVersion or WorldPartyWithdrawalAdmission.ProfileVersion or WorldCognitionAdmission.ProfileVersion
                ? new WorldFrameReader(reader.Field(512), 512, 7) : new WorldFrameReader(reader.Field(256), 256, 6);
            sample.Tag("world.control-sample"u8);
            var delivery = new TrustedCommandDelivery(sample.Identity(), sample.Identity());
            EntityId actor = sample.Identity();
            long x = sample.Signed();
            long y = sample.Signed();
            var witness = sample.Remaining == 0 ? null : WorldControlWitnessCodecV1.Decode(sample.Field(WorldControlWitnessCodecV1.MaximumByteLength));
            sample.Complete();
            if (values.Count != 0 && actor.CompareTo(values[^1].ActorId) <= 0)
            {
                throw new FormatException("Movement samples are not in strict actor order.");
            }
            try
            {
                values.Add(new WorldMovementInput(delivery, actor, x, y, witness));
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
