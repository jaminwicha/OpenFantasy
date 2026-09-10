using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Quests;

/// <summary>Strict finite quest admission and participation encoding, retaining exact reward definitions.</summary>
public static class QuestCodecV1
{
    public const int MaximumSnapshotByteLength = 4 * 1024 * 1024;
    public const int MaximumDefinitionByteLength = 16 * 1024;

    public static byte[] Encode(QuestSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        byte[] bytes = Frame("quest.snapshot", Number(1), Id(state.WorldId), Id(state.BranchId), Number(state.Revision),
            Collection("quest.definitions", state.Definitions.Select(EncodeDefinition)),
            Collection("quest.participations", state.Participations.Select(EncodeParticipation)));
        if (bytes.Length > MaximumSnapshotByteLength)
        {
            throw new ArgumentException("The quest snapshot exceeds its canonical byte bound.");
        }
        return bytes;
    }

    public static QuestSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumSnapshotByteLength, 7);
            reader.Tag("quest.snapshot"u8);
            reader.Version();
            EntityId world = reader.Identity();
            EntityId branch = reader.Identity();
            ulong revision = reader.Number();
            var definitions = new WorldFrameReader(reader.Field(MaximumSnapshotByteLength),
                MaximumSnapshotByteLength, QuestSnapshot.MaximumDefinitions + 1);
            definitions.Tag("quest.definitions"u8);
            List<DeliveryQuestDefinition> admitted = [];
            while (definitions.Remaining != 0)
            {
                admitted.Add(DecodeDefinition(definitions.Field(MaximumDefinitionByteLength)));
            }
            definitions.Complete();
            var participants = new WorldFrameReader(reader.Field(MaximumSnapshotByteLength),
                MaximumSnapshotByteLength, QuestSnapshot.MaximumParticipations + 1);
            participants.Tag("quest.participations"u8);
            List<ActorQuestState> entries = [];
            while (participants.Remaining != 0)
            {
                entries.Add(DecodeParticipation(participants.Field(512)));
            }
            participants.Complete();
            reader.Complete();
            var restored = new QuestSnapshot(world, branch, revision, admitted, entries);
            RequireCanonical(bytes, Encode(restored));
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The quest snapshot violates its invariants.", error);
        }
    }

    public static byte[] EncodeDefinition(DeliveryQuestDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        byte[] bytes = Frame("quest.delivery-definition", Number(1), Id(definition.DefinitionId), Text(definition.Key),
            Text(definition.Title), Text(definition.Description), Id(definition.GiverActorId), Number(definition.MinimumLevel),
            Id(definition.Item.DefinitionId), definition.Item.Version.ToByteArray(), Number(definition.Quantity),
            Id(definition.DestinationCustodianId), Id(definition.MaterialReservationId),
            ProgressionCodecV1.EncodeReward(definition.Reward));
        if (bytes.Length > MaximumDefinitionByteLength)
        {
            throw new ArgumentException("The quest definition exceeds its canonical byte bound.");
        }
        return bytes;
    }

    public static DeliveryQuestDefinition DecodeDefinition(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumDefinitionByteLength, 14);
            reader.Tag("quest.delivery-definition"u8);
            reader.Version();
            EntityId id = reader.Identity();
            string key = reader.Text(128);
            string title = reader.Text(256);
            string description = reader.Text(2048);
            EntityId giver = reader.Identity();
            uint minimumLevel = checked((uint)reader.Number());
            var item = new CountedItemDefinition(reader.Identity(), Hash(reader.Exact(32)));
            ulong quantity = reader.Number();
            EntityId destination = reader.Identity();
            EntityId reservation = reader.Identity();
            ActorRewardDefinition reward = ProgressionCodecV1.DecodeReward(reader.Field(MaximumDefinitionByteLength));
            reader.Complete();
            var restored = new DeliveryQuestDefinition(id, key, title, description, giver, minimumLevel,
                item, quantity, destination, reservation, reward);
            RequireCanonical(bytes, EncodeDefinition(restored));
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The delivery definition violates its invariants.", error);
        }
    }

    private static byte[] EncodeParticipation(ActorQuestState entry) => Frame("quest.participation", Number(1),
        Id(entry.ActorId), Id(entry.QuestId), Id(entry.AcceptanceCommandId), EncodeInstant(entry.AcceptedAt),
        entry.CompletionCommandId is { } completion ? Id(completion) : [],
        entry.CompletedAt is { } completed ? EncodeInstant(completed) : []);

    private static ActorQuestState DecodeParticipation(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, 512, 8);
        reader.Tag("quest.participation"u8);
        reader.Version();
        EntityId actor = reader.Identity();
        EntityId quest = reader.Identity();
        EntityId acceptance = reader.Identity();
        WorldInstant acceptedAt = DecodeInstant(reader.Field(128));
        ReadOnlySpan<byte> completion = reader.Field(16);
        ReadOnlySpan<byte> completedAt = reader.Field(128);
        reader.Complete();
        return new ActorQuestState(actor, quest, acceptance, acceptedAt,
            completion.IsEmpty ? null : EntityId.FromBytes(completion),
            completedAt.IsEmpty ? null : DecodeInstant(completedAt));
    }

    private static byte[] EncodeInstant(WorldInstant instant) => Frame("quest.instant", Number(1),
        Signed(instant.Days), Number(checked((ulong)instant.MillisecondsWithinDay)));

    private static WorldInstant DecodeInstant(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, 128, 4);
        reader.Tag("quest.instant"u8);
        reader.Version();
        var instant = new WorldInstant(reader.Signed(), checked((int)reader.Number()));
        reader.Complete();
        return instant;
    }

    private static void RequireCanonical(ReadOnlySpan<byte> original, byte[] encoded)
    {
        if (!original.SequenceEqual(encoded))
        {
            throw new FormatException("Quest fields are not canonically encoded.");
        }
    }

    private static ContentHash Hash(ReadOnlySpan<byte> value) => new(Convert.ToHexString(value).ToLowerInvariant());
    private static byte[] Text(string value) => CanonicalEncodingV1.Text(value, nameof(value));
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
