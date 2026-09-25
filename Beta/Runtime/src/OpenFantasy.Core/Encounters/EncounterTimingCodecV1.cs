using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Encounters;

/// <summary>
/// Bounded canonical timing bytes. Decoding validates their shape and expected scope, not whether
/// the actor set, controller assignments, incurred debt or requested actions were authorized by a world.
/// </summary>
public static class EncounterTimingCodecV1
{
    public const int MaximumEncodedByteLength = 16 * 1024;
    private const int ActorRowLength = 40;
    private const int DecisionRowLength = 72;

    public static byte[] Encode(EncounterTimingSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        byte[] actors = new byte[checked(snapshot.Actors.Count * ActorRowLength)];
        for (int index = 0; index < snapshot.Actors.Count; index++)
        {
            EncounterActorTiming actor = snapshot.Actors[index];
            Span<byte> row = actors.AsSpan(index * ActorRowLength, ActorRowLength);
            actor.ActorId.WriteBytes(row);
            BinaryPrimitives.WriteUInt64BigEndian(row[16..], actor.NormalizedSpeed);
            BinaryPrimitives.WriteUInt64BigEndian(row[24..], actor.RemainingDelay);
            BinaryPrimitives.WriteUInt64BigEndian(row[32..], (ulong)actor.Control);
        }
        byte[] decisions = new byte[checked(snapshot.Decisions.Count * DecisionRowLength)];
        for (int index = 0; index < snapshot.Decisions.Count; index++)
        {
            EncounterDecision decision = snapshot.Decisions[index];
            Span<byte> row = decisions.AsSpan(index * DecisionRowLength, DecisionRowLength);
            decision.ActorId.WriteBytes(row);
            decision.DecisionId.WriteBytes(row[16..]);
            BinaryPrimitives.WriteUInt64BigEndian(row[32..], (ulong)decision.Kind);
            decision.ActionPin.ToByteArray().CopyTo(row[40..]);
        }

        return CanonicalEncodingV1.Frame("encounter.timing.v1", CanonicalEncodingV1.Unsigned64(1),
            snapshot.WorldId.ToByteArray(), snapshot.BranchId.ToByteArray(), snapshot.EncounterId.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(snapshot.Revision), CanonicalEncodingV1.Unsigned64(snapshot.ElapsedTacticalUnits),
            CanonicalEncodingV1.Frame("encounter.actors.v1", actors),
            CanonicalEncodingV1.Frame("encounter.decisions.v1", decisions));
    }

    public static EncounterTimingSnapshot Decode(ReadOnlySpan<byte> bytes, EncounterTimingScope expectedScope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedScope);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 9);
            reader.Tag("encounter.timing.v1"u8);
            reader.Version();
            var scope = new EncounterTimingScope(reader.Identity(), reader.Identity(), reader.Identity());
            if (scope != expectedScope)
            {
                throw new FormatException("The timing archive belongs to a different world, branch or encounter.");
            }
            ulong revision = reader.Number();
            ulong elapsed = reader.Number();
            var actors = DecodeActors(reader.Field(MaximumEncodedByteLength), cancellationToken);
            var decisions = DecodeDecisions(reader.Field(MaximumEncodedByteLength), cancellationToken);
            reader.Complete();
            var state = new EncounterTimingSnapshot(scope, revision, elapsed, actors, decisions);
            if (!Encode(state).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The timing archive is not canonical.");
            }
            return state;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            throw new FormatException("Invalid retained tactical timing.", error);
        }
    }

    private static List<EncounterActorTiming> DecodeActors(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken)
    {
        var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 2);
        reader.Tag("encounter.actors.v1"u8);
        ReadOnlySpan<byte> rows = reader.Field(EncounterTimingSnapshot.MaximumActors * ActorRowLength);
        if (rows.Length == 0 || rows.Length % ActorRowLength != 0)
        {
            throw new FormatException("Timing actor rows must be complete and nonempty.");
        }
        List<EncounterActorTiming> actors = [];
        for (int offset = 0; offset < rows.Length; offset += ActorRowLength)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadOnlySpan<byte> row = rows.Slice(offset, ActorRowLength);
            actors.Add(new EncounterActorTiming(EntityId.FromBytes(row[..16]),
                checked((uint)BinaryPrimitives.ReadUInt64BigEndian(row[16..])),
                BinaryPrimitives.ReadUInt64BigEndian(row[24..]),
                (EncounterActorControl)checked((int)BinaryPrimitives.ReadUInt64BigEndian(row[32..]))));
        }
        reader.Complete();
        return actors;
    }

    private static List<EncounterDecision> DecodeDecisions(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken)
    {
        var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 2);
        reader.Tag("encounter.decisions.v1"u8);
        ReadOnlySpan<byte> rows = reader.Field(EncounterTimingSnapshot.MaximumActors * DecisionRowLength);
        if (rows.Length % DecisionRowLength != 0)
        {
            throw new FormatException("Timing decision rows must be complete.");
        }
        List<EncounterDecision> decisions = [];
        for (int offset = 0; offset < rows.Length; offset += DecisionRowLength)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadOnlySpan<byte> row = rows.Slice(offset, DecisionRowLength);
            decisions.Add(new EncounterDecision(EntityId.FromBytes(row[..16]), EntityId.FromBytes(row.Slice(16, 16)),
                (EncounterDecisionKind)checked((int)BinaryPrimitives.ReadUInt64BigEndian(row[32..])),
                new ContentHash(Convert.ToHexString(row[40..]).ToLowerInvariant())));
        }
        reader.Complete();
        return decisions;
    }
}
