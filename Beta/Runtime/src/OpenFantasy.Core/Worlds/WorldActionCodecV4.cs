using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Quests;

namespace OpenFantasy.Core.Worlds;

/// <summary>Quest requests carry exact definition and owned revisions, never client-selected reward amounts.</summary>
public static class WorldActionCodecV4
{
    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ControlWitness is not null) throw new ArgumentException("Delegated attribution requires action codec 23.", nameof(command));
        if (command.Intent is not DeliveryQuestIntent quest)
        {
            throw new ArgumentException("This action version encodes delivery quests only.", nameof(command));
        }
        byte[] bytes = Frame("world.actor-action", Number(4), Id(command.CommandId), Id(command.PrincipalId),
            Id(command.OriginSessionId), Id(command.ActorId), Id(command.WorldId), Id(command.BranchId),
            Number(command.ExpectedActorRevision), EncodeIntent(quest));
        if (bytes.Length > WorldActionCodec.MaximumCommandByteLength)
        {
            throw new ArgumentException("The quest action exceeds the admitted command bound.", nameof(command));
        }
        return bytes;
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, WorldActionCodec.MaximumCommandByteLength, 10);
            reader.Tag("world.actor-action"u8);
            if (reader.Number() != 4)
            {
                throw new FormatException("Unsupported quest action version.");
            }
            EntityId command = reader.Identity();
            EntityId principal = reader.Identity();
            EntityId session = reader.Identity();
            EntityId actor = reader.Identity();
            EntityId world = reader.Identity();
            EntityId branch = reader.Identity();
            ulong actorRevision = reader.Number();
            DeliveryQuestIntent intent = DecodeIntent(reader.Field(WorldActionCodec.MaximumCommandByteLength));
            reader.Complete();
            var restored = new WorldActionCommand(command, principal, session, actor, world, branch, actorRevision, intent);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("Quest selections are not canonically encoded.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            throw new FormatException("The quest command violates its invariants.", error);
        }
    }

    private static byte[] EncodeIntent(DeliveryQuestIntent intent)
    {
        ulong kind = intent switch
        {
            AcceptDeliveryQuestIntent => 1,
            CompleteDeliveryQuestIntent => 2,
            _ => throw new ArgumentException("Unsupported delivery quest operation.")
        };
        List<ReadOnlyMemory<byte>> fields = [Number(1), Number(kind), Id(intent.QuestId), intent.QuestVersion.ToByteArray(),
            Id(intent.GiverActorId), Number(intent.ExpectedGiverRevision), Number(intent.ExpectedParticipationRevision),
            Number(intent.ExpectedProgressionRevision)];
        if (intent is CompleteDeliveryQuestIntent complete)
        {
            fields.Add(Number(complete.ExpectedInventoryRevision));
            fields.Add(Frame("quest.selected-lots", complete.Selections.Select(value =>
                (ReadOnlyMemory<byte>)Frame("quest.source-selection", Id(value.LotId), Number(value.ExpectedRevision),
                    Number(value.Quantity))).ToArray()));
        }
        return Frame("world.action.quest", fields.ToArray());
    }

    private static DeliveryQuestIntent DecodeIntent(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, WorldActionCodec.MaximumCommandByteLength, 11);
        reader.Tag("world.action.quest"u8);
        reader.Version();
        ulong kind = reader.Number();
        EntityId quest = reader.Identity();
        ContentHash pin = new(Convert.ToHexString(reader.Exact(32)).ToLowerInvariant());
        EntityId giver = reader.Identity();
        ulong giverRevision = reader.Number();
        ulong participationRevision = reader.Number();
        ulong progressionRevision = reader.Number();
        DeliveryQuestIntent intent;
        if (kind == 1)
        {
            intent = new AcceptDeliveryQuestIntent(quest, pin, giver, giverRevision, participationRevision, progressionRevision);
        }
        else if (kind == 2)
        {
            ulong inventoryRevision = reader.Number();
            var selections = new WorldFrameReader(reader.Field(WorldActionCodec.MaximumCommandByteLength),
                WorldActionCodec.MaximumCommandByteLength, SourceDeliveryRequest.MaximumSelections + 1);
            selections.Tag("quest.selected-lots"u8);
            List<SourceLotSelection> values = [];
            while (selections.Remaining != 0)
            {
                var selected = new WorldFrameReader(selections.Field(128), 128, 4);
                selected.Tag("quest.source-selection"u8);
                values.Add(new SourceLotSelection(selected.Identity(), selected.Number(), selected.Number()));
                selected.Complete();
            }
            selections.Complete();
            intent = new CompleteDeliveryQuestIntent(quest, pin, giver, giverRevision, participationRevision,
                progressionRevision, inventoryRevision, values);
        }
        else
        {
            throw new FormatException("Unknown delivery quest operation.");
        }
        reader.Complete();
        return intent;
    }

    private static byte[] Id(EntityId value) => value.ToByteArray();
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    private static byte[] Frame(string tag, params ReadOnlyMemory<byte>[] fields) => CanonicalEncodingV1.Frame(tag, fields);
}
