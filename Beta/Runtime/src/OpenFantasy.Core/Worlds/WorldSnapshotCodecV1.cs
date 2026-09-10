using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;
using static OpenFantasy.Core.Worlds.WorldSpatialCodecV1;

namespace OpenFantasy.Core.Worlds;

public static class WorldSnapshotCodecV1
{
    public const int MaximumStateByteLength = 16 * 1024 * 1024;

    public static byte[] Encode(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.ProfileVersion != 1)
        {
            throw new ArgumentException("This encoder cannot omit a newer world's admitted components.", nameof(world));
        }
        return EncodeExplorationBasis(world);
    }

    // Used only inside an explicitly versioned outer snapshot after full world admission. The outer
    // codec must encode all additional components; this avoids rebuilding unchanged terrain indexes.
    internal static byte[] EncodeExplorationBasis(WorldSnapshot world)
    {
        byte[] bytes = CanonicalEncodingV1.Frame("world.snapshot", Number(1), AuthorityEncodingV1.Encode(world.Inventory),
            WorldSpatialCodecV1.Encode(world.Space),
            CanonicalEncodingV1.Frame("world.residents", world.Residents.Select(resident =>
                (ReadOnlyMemory<byte>)WorldResidentCodecV1.Encode(resident)).ToArray()),
            world.AdmissionVersion.ToByteArray(), Signed(world.Calendar.Days), Number((ulong)world.Calendar.MillisecondsWithinDay),
            Number(world.Revision), Number(world.AdvancingSteps),
            CanonicalEncodingV1.Frame("world.dialogues", world.Dialogues.Select(dialogue =>
                (ReadOnlyMemory<byte>)EncodeDialogue(dialogue)).ToArray()));
        if (bytes.Length > MaximumStateByteLength)
        {
            throw new ArgumentException("The world snapshot exceeds its admitted storage profile.", nameof(world));
        }
        return bytes;
    }

    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumStateByteLength, 11);
            reader.Tag("world.snapshot"u8);
            reader.Version();
            CountedAuthorityState inventory = AuthorityEncodingV1.Decode(reader.Field(AuthorityEncodingV1.MaximumStateByteLength));
            LocalSpatialSnapshot space = WorldSpatialCodecV1.Decode(reader.Field(MaximumStateByteLength));
            var residentsReader = new WorldFrameReader(reader.Field(4 * 1024 * 1024), 4 * 1024 * 1024, 65);
            residentsReader.Tag("world.residents"u8);
            List<WorldResident> residents = [];
            while (residentsReader.Remaining != 0)
            {
                residents.Add(WorldResidentCodecV1.Decode(residentsReader.Field(WorldResidentCodecV1.MaximumResidentByteLength)));
            }
            residentsReader.Complete();
            var admission = new ContentHash(Convert.ToHexString(reader.Exact(32)).ToLowerInvariant());
            WorldInstant calendar = ReadInstant(ref reader);
            ulong revision = reader.Number();
            ulong steps = reader.Number();
            var dialoguesReader = new WorldFrameReader(reader.Field(4096), 4096, 5);
            dialoguesReader.Tag("world.dialogues"u8);
            List<DialogueSession> dialogues = [];
            while (dialoguesReader.Remaining != 0)
            {
                dialogues.Add(DecodeDialogue(dialoguesReader.Field(512)));
            }
            dialoguesReader.Complete();
            reader.Complete();
            var result = new WorldSnapshot(inventory, space, residents, admission, calendar, revision, steps, dialogues);
            if (!Encode(result).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The world snapshot is not canonical or includes redundant definitions.");
            }
            return result;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The world snapshot violates its admitted invariants.", error);
        }
    }

    private static byte[] EncodeDialogue(DialogueSession dialogue) => CanonicalEncodingV1.Frame("world.dialogue",
        dialogue.DialogueId.ToByteArray(), dialogue.ListenerActorId.ToByteArray(), dialogue.SpeakerActorId.ToByteArray(),
        dialogue.UtteranceDefinitionId.ToByteArray(), dialogue.CauseId.ToByteArray(),
        Signed(dialogue.StartedAt.Days), Number((ulong)dialogue.StartedAt.MillisecondsWithinDay));

    private static DialogueSession DecodeDialogue(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, 512, 8);
        reader.Tag("world.dialogue"u8);
        EntityId id = reader.Identity();
        EntityId listener = reader.Identity();
        EntityId speaker = reader.Identity();
        EntityId utterance = reader.Identity();
        EntityId cause = reader.Identity();
        WorldInstant time = ReadInstant(ref reader);
        reader.Complete();
        return new DialogueSession(id, listener, speaker, utterance, cause, time);
    }

    private static WorldInstant ReadInstant(ref WorldFrameReader reader)
    {
        long days = reader.Signed();
        ulong milliseconds = reader.Number();
        if (milliseconds >= WorldInstant.MillisecondsPerDay)
        {
            throw new FormatException("A world calendar instant is noncanonical.");
        }
        return new WorldInstant(days, (int)milliseconds);
    }
}
