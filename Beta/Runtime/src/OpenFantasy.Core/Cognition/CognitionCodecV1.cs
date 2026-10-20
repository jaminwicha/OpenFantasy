using System.Buffers.Binary;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Cognition;

/// <summary>
/// Strict finite encoding of the cognition facet. Every field has a fixed or bounded length, absent optional values
/// are empty fields, and decoding re-encodes and compares so only the canonical byte form is accepted.
/// </summary>
public static class CognitionCodecV1
{
    public const string Format = "cognition.snapshot.v1";
    public const int MaximumEvidenceByteLength = 512;
    public const int MaximumIntentionByteLength = 512;
    public const int MaximumMemoryByteLength = 512;
    public const int MaximumActorByteLength = 48 * 1024;
    public const int MaximumSnapshotByteLength = CognitionSnapshot.MaximumActors * (MaximumActorByteLength + 4) + 1024;
    public const int MaximumDecisionByteLength = 1024;
    private const int InstantByteLength = 128;
    private const int WakeByteLength = 128;

    public static byte[] Encode(CognitionSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        byte[] bytes = Frame("cognition.snapshot", Number(1), Id(state.WorldId), Id(state.BranchId), Number(state.Revision),
            Collection("cognition.actors", state.Actors.Select(EncodeActor)));
        return bytes.Length <= MaximumSnapshotByteLength ? bytes
            : throw new ArgumentException("The cognition snapshot exceeds its canonical byte bound.");
    }

    public static CognitionSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumSnapshotByteLength, 6);
            reader.Tag("cognition.snapshot"u8);
            reader.Version();
            EntityId world = reader.Identity();
            EntityId branch = reader.Identity();
            ulong revision = reader.Number();
            var actors = new WorldFrameReader(reader.Field(MaximumSnapshotByteLength), MaximumSnapshotByteLength,
                CognitionSnapshot.MaximumActors + 1);
            actors.Tag("cognition.actors"u8);
            List<ActorCognition> restoredActors = [];
            while (actors.Remaining != 0) restoredActors.Add(DecodeActor(actors.Field(MaximumActorByteLength)));
            actors.Complete();
            reader.Complete();
            var restored = new CognitionSnapshot(world, branch, revision, restoredActors);
            RequireCanonical(bytes, Encode(restored));
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The cognition snapshot violates its invariants.", error);
        }
    }

    private static byte[] EncodeActor(ActorCognition actor)
    {
        byte[] bytes = Frame("cognition.actor", Number(1), Id(actor.ActorId), Number(actor.Revision),
            Collection("cognition.acquired", actor.Evidence.Select(EncodeEvidence)),
            actor.Intention is { } intention ? EncodeIntention(intention) : [],
            Collection("cognition.memories", actor.Memory.Select(EncodeMemory)),
            actor.Wake is { } wake ? EncodeWake(wake) : []);
        return bytes.Length <= MaximumActorByteLength ? bytes
            : throw new ArgumentException("An actor's cognition exceeds its canonical byte bound.");
    }

    private static ActorCognition DecodeActor(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumActorByteLength, 8);
        reader.Tag("cognition.actor"u8);
        reader.Version();
        EntityId actor = reader.Identity();
        ulong revision = reader.Number();
        var acquired = new WorldFrameReader(reader.Field(MaximumActorByteLength), MaximumActorByteLength,
            ActorCognition.MaximumEvidence + 1);
        acquired.Tag("cognition.acquired"u8);
        List<CognitionEvidence> evidence = [];
        while (acquired.Remaining != 0) evidence.Add(DecodeEvidence(acquired.Field(MaximumEvidenceByteLength), actor));
        acquired.Complete();
        ReadOnlySpan<byte> intention = reader.Field(MaximumIntentionByteLength);
        CognitionIntention? current = intention.IsEmpty ? null : DecodeIntention(intention);
        var memories = new WorldFrameReader(reader.Field(MaximumActorByteLength), MaximumActorByteLength,
            ActorCognition.MaximumMemory + 1);
        memories.Tag("cognition.memories"u8);
        List<CognitionMemory> memory = [];
        while (memories.Remaining != 0) memory.Add(DecodeMemory(memories.Field(MaximumMemoryByteLength)));
        memories.Complete();
        ReadOnlySpan<byte> wake = reader.Field(WakeByteLength);
        CognitionWake? pending = wake.IsEmpty ? null : DecodeWake(wake);
        reader.Complete();
        return new ActorCognition(actor, revision, evidence, current, memory, pending);
    }

    // The knower is the owning actor, so an evidence frame does not repeat it.
    private static byte[] EncodeEvidence(CognitionEvidence entry) => Frame("cognition.evidence", Number(1),
        Id(entry.EvidenceId), Number((ulong)entry.Kind), Id(entry.SourceCommandId),
        entry.CounterpartActorId is { } counterpart ? Id(counterpart) : [], Id(entry.SubjectId), Number(entry.Quantity),
        EncodeInstant(entry.AcquiredAt));

    private static CognitionEvidence DecodeEvidence(ReadOnlySpan<byte> bytes, EntityId knower)
    {
        var reader = new WorldFrameReader(bytes, MaximumEvidenceByteLength, 9);
        reader.Tag("cognition.evidence"u8);
        reader.Version();
        EntityId evidence = reader.Identity();
        var kind = (EvidenceKind)checked((int)reader.Number());
        EntityId source = reader.Identity();
        ReadOnlySpan<byte> counterpart = reader.Field(16);
        EntityId subject = reader.Identity();
        ulong quantity = reader.Number();
        WorldInstant acquiredAt = DecodeInstant(reader.Field(InstantByteLength));
        reader.Complete();
        return new CognitionEvidence(evidence, knower, kind, source,
            counterpart.IsEmpty ? null : EntityId.FromBytes(counterpart), subject, quantity, acquiredAt);
    }

    private static byte[] EncodeIntention(CognitionIntention intention) => Frame("cognition.intention", Number(1),
        Number((ulong)intention.Kind), Id(intention.SubjectId), EncodeInstant(intention.AdoptedAt),
        Collection("cognition.basis", intention.Basis.Select(Id)));

    private static CognitionIntention DecodeIntention(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumIntentionByteLength, 6);
        reader.Tag("cognition.intention"u8);
        reader.Version();
        var kind = (IntentionKind)checked((int)reader.Number());
        EntityId subject = reader.Identity();
        WorldInstant adoptedAt = DecodeInstant(reader.Field(InstantByteLength));
        var basisReader = new WorldFrameReader(reader.Field(MaximumIntentionByteLength), MaximumIntentionByteLength,
            CognitionIntention.MaximumBasis + 1);
        basisReader.Tag("cognition.basis"u8);
        List<EntityId> basis = [];
        while (basisReader.Remaining != 0) basis.Add(basisReader.Identity());
        basisReader.Complete();
        reader.Complete();
        return new CognitionIntention(kind, subject, adoptedAt, basis);
    }

    private static byte[] EncodeMemory(CognitionMemory memory) => Frame("cognition.memory", Number(1),
        Number((ulong)memory.Intention), Id(memory.SubjectId), Number((ulong)memory.Outcome), EncodeInstant(memory.At),
        Number((ulong)memory.Reason), CanonicalEncodingV1.Text(memory.PolicyVersion, nameof(memory)),
        Collection("cognition.basis", memory.Basis.Select(Id)));

    private static CognitionMemory DecodeMemory(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumMemoryByteLength, 9);
        reader.Tag("cognition.memory"u8);
        reader.Version();
        var intention = (IntentionKind)checked((int)reader.Number());
        EntityId subject = reader.Identity();
        var outcome = (MemoryOutcome)checked((int)reader.Number());
        WorldInstant at = DecodeInstant(reader.Field(InstantByteLength));
        var reason = (CognitionDeferral)checked((int)reader.Number());
        string policy = reader.Text(CognitionMemory.MaximumPolicyVersionBytes);
        var basisReader = new WorldFrameReader(reader.Field(MaximumMemoryByteLength), MaximumMemoryByteLength,
            CognitionIntention.MaximumBasis + 1);
        basisReader.Tag("cognition.basis"u8);
        List<EntityId> basis = [];
        while (basisReader.Remaining != 0) basis.Add(basisReader.Identity());
        basisReader.Complete();
        reader.Complete();
        return new CognitionMemory(intention, subject, outcome, at, reason, policy, basis);
    }

    /// <summary>
    /// The recorded decision a cognition command carries: its policy version, the intention it holds, the deferral, the
    /// next fallback wake and whether the current intention completed. Replay decodes and applies it; nothing reruns the
    /// policy that produced it.
    /// </summary>
    public static byte[] EncodeDecision(CognitionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(decision.NextWake);
        if (!Enum.IsDefined(decision.Deferral)) throw new ArgumentException("A decision names a known deferral.");
        byte[] bytes = Frame("cognition.decision", Number(1),
            CanonicalEncodingV1.Text(CognitionMemory.RequirePolicyVersion(decision.PolicyVersion), nameof(decision)),
            decision.Intention is { } intention ? EncodeIntention(intention) : [], Number((ulong)decision.Deferral),
            EncodeWake(decision.NextWake.Validated()), Number(decision.CompletedCurrent ? 1UL : 0UL));
        return bytes.Length <= MaximumDecisionByteLength ? bytes
            : throw new ArgumentException("A recorded decision exceeds its canonical byte bound.");
    }

    public static CognitionDecision DecodeDecision(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumDecisionByteLength, 7);
            reader.Tag("cognition.decision"u8);
            reader.Version();
            string policy = reader.Text(CognitionMemory.MaximumPolicyVersionBytes);
            ReadOnlySpan<byte> intention = reader.Field(MaximumIntentionByteLength);
            CognitionIntention? held = intention.IsEmpty ? null : DecodeIntention(intention);
            var deferral = (CognitionDeferral)checked((int)reader.Number());
            CognitionWake wake = DecodeWake(reader.Field(WakeByteLength));
            ulong completed = reader.Number();
            reader.Complete();
            if (!Enum.IsDefined(deferral) || completed > 1) throw new FormatException("A recorded decision names unknown values.");
            var restored = new CognitionDecision(policy, held, deferral, wake, completed == 1);
            RequireCanonical(bytes, EncodeDecision(restored));
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The recorded decision violates its invariants.", error);
        }
    }

    private static byte[] EncodeWake(CognitionWake wake) => Frame("cognition.wake", Number(1), Number(wake.DueStep),
        Number((ulong)wake.Cause), Number((ulong)wake.Suppression));

    private static CognitionWake DecodeWake(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, WakeByteLength, 5);
        reader.Tag("cognition.wake"u8);
        reader.Version();
        ulong due = reader.Number();
        var cause = (WakeCause)checked((int)reader.Number());
        var suppression = (WakeSuppression)checked((int)reader.Number());
        reader.Complete();
        return new CognitionWake(due, cause, suppression).Validated();
    }

    private static byte[] EncodeInstant(WorldInstant instant) => Frame("cognition.instant", Number(1),
        Signed(instant.Days), Number(checked((ulong)instant.MillisecondsWithinDay)));

    private static WorldInstant DecodeInstant(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, InstantByteLength, 4);
        reader.Tag("cognition.instant"u8);
        reader.Version();
        var instant = new WorldInstant(reader.Signed(), checked((int)reader.Number()));
        reader.Complete();
        return instant;
    }

    private static void RequireCanonical(ReadOnlySpan<byte> original, byte[] encoded)
    {
        if (!original.SequenceEqual(encoded)) throw new FormatException("Cognition fields are not canonically encoded.");
    }

    private static byte[] Id(EntityId value) => value.ToByteArray();
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    private static byte[] Signed(long value)
    {
        byte[] result = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(result, value);
        return result;
    }
    private static byte[] Collection(string tag, IEnumerable<byte[]> fields) => Frame(tag,
        fields.Select(value => (ReadOnlyMemory<byte>)value).ToArray());
    private static byte[] Frame(string tag, params ReadOnlyMemory<byte>[] fields) => CanonicalEncodingV1.Frame(tag, fields);
}
