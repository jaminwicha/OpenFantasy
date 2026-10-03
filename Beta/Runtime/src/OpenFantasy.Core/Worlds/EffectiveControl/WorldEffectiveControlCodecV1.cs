namespace OpenFantasy.Core.Worlds;

public static class WorldEffectiveControlCodecV1
{
    public const int MaximumStateByteLength = 2048;
    public const int MaximumEventByteLength = 512;
    public static byte[] Encode(WorldEffectiveControlState state) => CanonicalEncodingV1.Frame("world.effective-control.v1",
        WorldEffectiveControlState.RulesPin.ToByteArray(), Number(state.Revision),
        CanonicalEncodingV1.Frame("world.effective-control-slots.v1", state.Slots.Select(slot => (ReadOnlyMemory<byte>)
            CanonicalEncodingV1.Frame("world.effective-control-slot.v1", slot.ActorId.ToByteArray(), Number(slot.Revision),
                Number(slot.ChangedSequence), slot.Witness is null ? [] : WorldControlWitnessCodecV1.Encode(slot.Witness))).ToArray()));
    public static WorldEffectiveControlState Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumStateByteLength, 4);
            reader.Tag("world.effective-control.v1"u8);
            if (!reader.Exact(32).SequenceEqual(WorldEffectiveControlState.RulesPin.ToByteArray())) throw new FormatException("Unknown effective control law.");
            ulong revision = reader.Number();
            var rows = new WorldFrameReader(reader.Field(MaximumStateByteLength), MaximumStateByteLength, WorldControllerState.MaximumActors + 1);
            rows.Tag("world.effective-control-slots.v1"u8);
            List<WorldEffectiveControlSlot> slots = [];
            while (rows.Remaining != 0)
            {
                var row = new WorldFrameReader(rows.Field(384), 384, 5); row.Tag("world.effective-control-slot.v1"u8);
                var actor = row.Identity(); ulong slotRevision = row.Number(), sequence = row.Number();
                var witness = row.Field(WorldControlWitnessCodecV1.MaximumByteLength); row.Complete();
                slots.Add(new(actor, slotRevision, sequence, witness.IsEmpty ? null : WorldControlWitnessCodecV1.Decode(witness)));
            }
            rows.Complete(); reader.Complete();
            var result = new WorldEffectiveControlState(revision, slots);
            if (!Encode(result).AsSpan().SequenceEqual(bytes)) throw new FormatException("Noncanonical effective control state.");
            return result;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException) { throw new FormatException("Invalid effective control state.", error); }
    }
    public static byte[] EncodeEvent(WorldControlAssignmentEvent input) => CanonicalEncodingV1.Frame("world.control-assignment-event.v1",
        input.ActorId.ToByteArray(), Number((ulong)input.Kind), Number(input.ExpectedAssignmentRevision),
        input.Witness is null ? [] : WorldControlWitnessCodecV1.Encode(input.Witness));
    public static WorldControlAssignmentEvent DecodeEvent(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEventByteLength, 5); reader.Tag("world.control-assignment-event.v1"u8);
            var actor = reader.Identity(); var kind = (WorldControlAssignmentEventKind)checked((int)reader.Number()); ulong revision = reader.Number();
            var witness = reader.Field(WorldControlWitnessCodecV1.MaximumByteLength); reader.Complete();
            var result = new WorldControlAssignmentEvent(actor, kind, revision, witness.IsEmpty ? null : WorldControlWitnessCodecV1.Decode(witness));
            if (!EncodeEvent(result).AsSpan().SequenceEqual(bytes)) throw new FormatException("Noncanonical effective control event.");
            return result;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException) { throw new FormatException("Invalid effective control event.", error); }
    }
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
