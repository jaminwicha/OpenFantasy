using OpenFantasy.Core.Content;
using OpenFantasy.Core.Geography;

namespace OpenFantasy.Core.Worlds;

/// <summary>Finite runoff commitment controls with complete-owner pins and no replaceable world/domain payload.</summary>
public static class WorldActionCodecV6
{
    public const int MaximumCommandByteLength = 2048;
    private const int InputRowByteLength = 72;

    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Intent is not RunoffCommitmentIntent intent)
        {
            throw new ArgumentException("This action version encodes runoff commitments only.", nameof(command));
        }
        ulong kind = intent switch
        {
            AdmitRunoffIntent => 1,
            CancelRunoffIntent => 2,
            _ => throw new ArgumentException("Unsupported runoff commitment operation.", nameof(command))
        };
        List<ReadOnlyMemory<byte>> fields = [Number(1), Number(kind), intent.ExpectedReservoirHash.ToByteArray(),
            Number(intent.ExpectedCapacityRevision), intent.ReservationId.ToByteArray()];
        if (intent is AdmitRunoffIntent admit)
        {
            fields.Add(WatershedRunoffCodecV1.EncodeInstant(admit.Start));
            fields.Add(WatershedRunoffCodecV1.EncodeInstant(admit.End));
            fields.Add(WatershedRunoffCodecV1.EncodeInputs(admit.Inputs));
        }
        byte[] bytes = Frame("world.actor-action", Number(6), command.CommandId.ToByteArray(), command.PrincipalId.ToByteArray(),
            command.OriginSessionId.ToByteArray(), command.ActorId.ToByteArray(), command.WorldId.ToByteArray(),
            command.BranchId.ToByteArray(), Number(command.ExpectedActorRevision), Frame("world.action.runoff-commitment", fields.ToArray()));
        if (bytes.Length > MaximumCommandByteLength)
        {
            throw new ArgumentException("The runoff commitment action exceeds its byte bound.", nameof(command));
        }
        return bytes;
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumCommandByteLength, 10);
            reader.Tag("world.actor-action"u8);
            if (reader.Number() != 6)
            {
                throw new FormatException("Unsupported runoff commitment action version.");
            }
            EntityId command = reader.Identity(), principal = reader.Identity(), session = reader.Identity();
            EntityId actor = reader.Identity(), world = reader.Identity(), branch = reader.Identity();
            ulong revision = reader.Number();
            var action = new WorldFrameReader(reader.Field(MaximumCommandByteLength), MaximumCommandByteLength, 9);
            action.Tag("world.action.runoff-commitment"u8);
            action.Version();
            ulong kind = action.Number();
            var hash = new ContentHash(Convert.ToHexString(action.Exact(32)).ToLowerInvariant());
            ulong capacityRevision = action.Number();
            EntityId reservation = action.Identity();
            RunoffCommitmentIntent intent;
            if (kind == 1)
            {
                WorldInstant start = WatershedRunoffCodecV1.ReadInstant(action.Exact(12));
                WorldInstant end = WatershedRunoffCodecV1.ReadInstant(action.Exact(12));
                var inputs = action.Field(PreparedWatershedNetwork.MaximumBasins * InputRowByteLength);
                if (inputs.Length == 0 || inputs.Length % InputRowByteLength != 0)
                {
                    throw new FormatException("Runoff commitment input rows must be complete and nonempty.");
                }
                intent = new AdmitRunoffIntent(hash, capacityRevision, reservation, start, end,
                    WatershedRunoffCodecV1.ReadInputs(inputs, default));
            }
            else if (kind == 2)
            {
                intent = new CancelRunoffIntent(hash, capacityRevision, reservation);
            }
            else
            {
                throw new FormatException("Unknown runoff commitment operation.");
            }
            action.Complete();
            reader.Complete();
            var restored = new WorldActionCommand(command, principal, session, actor, world, branch, revision, intent);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The runoff commitment action is not canonical.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            throw new FormatException("Invalid runoff commitment action.", error);
        }
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    private static byte[] Frame(string tag, params ReadOnlyMemory<byte>[] fields) => CanonicalEncodingV1.Frame(tag, fields);
}
