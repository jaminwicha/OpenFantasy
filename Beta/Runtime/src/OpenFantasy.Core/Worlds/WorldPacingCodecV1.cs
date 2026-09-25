using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Exposure;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Seven canonical fields: tag, world, branch, origin instant, exposure progress, optional timing,
/// optional encounter-origin exposure. The last two fields are empty together. The instant is
/// signed64 days/signed32 milliseconds; encounter-origin exposure is unsigned64 days/milliseconds.
/// </summary>
public static class WorldPacingCodecV1
{
    public const int MaximumEncodedByteLength = 18 * 1024;

    public static byte[] Encode(WorldPacingState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        byte[] origin = new byte[12];
        BinaryPrimitives.WriteInt64BigEndian(origin, state.OriginCalendar.Days);
        BinaryPrimitives.WriteInt32BigEndian(origin.AsSpan(8), state.OriginCalendar.MillisecondsWithinDay);
        byte[] initial = [];
        if (state.EncounterOriginExposure is { } exposure)
        {
            initial = new byte[16];
            BinaryPrimitives.WriteUInt64BigEndian(initial, exposure.Days);
            BinaryPrimitives.WriteUInt64BigEndian(initial.AsSpan(8), exposure.MillisecondsWithinDay);
        }
        byte[] bytes = CanonicalEncodingV1.Frame("world.pacing.v1", state.WorldId.ToByteArray(),
            state.BranchId.ToByteArray(), origin, ExposureEncodingV1.Encode(state.Progress.Capture()),
            state.Encounter is { } timing ? EncounterTimingCodecV1.Encode(timing) : ReadOnlyMemory<byte>.Empty,
            initial);
        if (bytes.Length > MaximumEncodedByteLength)
        {
            throw new ArgumentException("World pacing exceeds its canonical byte bound.", nameof(state));
        }
        return bytes;
    }

    public static WorldPacingState Decode(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 7);
            reader.Tag("world.pacing.v1"u8);
            EntityId world = reader.Identity();
            EntityId branch = reader.Identity();
            ReadOnlySpan<byte> originBytes = reader.Exact(12);
            var origin = new WorldInstant(BinaryPrimitives.ReadInt64BigEndian(originBytes),
                BinaryPrimitives.ReadInt32BigEndian(originBytes[8..]));
            ExposureProgressState progress = ReadProgress(reader.Field(512));
            ReadOnlySpan<byte> timingBytes = reader.Field(EncounterTimingCodecV1.MaximumEncodedByteLength);
            ReadOnlySpan<byte> initialBytes = reader.Field(16);
            reader.Complete();
            EncounterTimingSnapshot? timing = null;
            LocalExposureDuration? initial = null;
            if (!timingBytes.IsEmpty)
            {
                if (initialBytes.Length != 16)
                {
                    throw new FormatException("An encounter requires its exact initial exposure.");
                }
                var header = new WorldFrameReader(timingBytes, EncounterTimingCodecV1.MaximumEncodedByteLength, 9);
                header.Tag("encounter.timing.v1"u8);
                header.Version();
                EntityId timingWorld = header.Identity();
                EntityId timingBranch = header.Identity();
                EntityId encounter = header.Identity();
                if (timingWorld != world || timingBranch != branch)
                {
                    throw new FormatException("The encounter does not belong to the pacing scope.");
                }
                timing = EncounterTimingCodecV1.Decode(timingBytes, new(world, branch, encounter), cancellationToken);
                initial = new LocalExposureDuration(BinaryPrimitives.ReadUInt64BigEndian(initialBytes),
                    checked((uint)BinaryPrimitives.ReadUInt64BigEndian(initialBytes[8..])));
            }
            else if (!initialBytes.IsEmpty)
            {
                throw new FormatException("Initial encounter exposure cannot exist without a timing owner.");
            }
            var state = WorldPacingState.Restore(world, branch, origin, progress, timing, initial);
            if (!Encode(state).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The world pacing archive is not canonical.");
            }
            return state;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            throw new FormatException("Invalid retained world pacing.", error);
        }
    }

    public static WorldPacingState Decode(ReadOnlySpan<byte> bytes, EntityId expectedWorldId,
        EntityId expectedBranchId, CancellationToken cancellationToken = default)
    {
        WorldPacingState state = Decode(bytes, cancellationToken);
        if (state.WorldId != expectedWorldId || state.BranchId != expectedBranchId)
        {
            throw new FormatException("The pacing archive belongs to a different world or branch.");
        }
        return state;
    }

    private static ExposureProgressState ReadProgress(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, 512, 10);
        reader.Tag("exposure.progress.v1"u8);
        EntityId subject = reader.Identity();
        EntityId output = reader.Identity();
        var rules = new ContentHash(Convert.ToHexString(reader.Exact(32)).ToLowerInvariant());
        var rate = new ExposureRate(output, rules, reader.Number(), reader.Number());
        var elapsed = new LocalExposureDuration(reader.Number(), checked((uint)reader.Number()));
        var progress = new ExposureProgressState(subject, rate, elapsed, reader.Number(), reader.Number());
        reader.Complete();
        return progress;
    }
}
