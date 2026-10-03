using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Worlds.EffectiveControl;

public sealed class EffectiveControlCodecTests
{
    [Fact]
    public void Witness_and_assignment_roundtrip_exactly_and_refuse_malformed_or_impossible_history()
    {
        var witness = Witness();
        byte[] bytes = WorldControlWitnessCodecV1.Encode(witness);
        Assert.InRange(bytes.Length, 1, WorldControlWitnessCodecV1.MaximumByteLength);
        Assert.Equal(witness, WorldControlWitnessCodecV1.Decode(bytes));
        Assert.Throws<FormatException>(() => WorldControlWitnessCodecV1.Decode(bytes[..^1]));
        Assert.Throws<FormatException>(() => WorldControlWitnessCodecV1.Decode([.. bytes, 0]));
        var state = new WorldEffectiveControlState(1, [new(witness.OwnerActorId, 1, witness.AcquiredSequence, witness)]);
        byte[] stateBytes = WorldEffectiveControlCodecV1.Encode(state);
        Assert.Equal(stateBytes, WorldEffectiveControlCodecV1.Encode(WorldEffectiveControlCodecV1.Decode(stateBytes)));
        Assert.Equal(witness, WorldEffectiveControlCodecV1.Decode(stateBytes).Find(witness.OwnerActorId)!.Witness);
        var released = new WorldEffectiveControlState(2, [new(witness.OwnerActorId, 2, 6)]);
        Assert.Null(WorldEffectiveControlCodecV1.Decode(WorldEffectiveControlCodecV1.Encode(released)).Find(witness.OwnerActorId)!.Witness);
        Assert.Throws<ArgumentException>(() => new WorldEffectiveControlSlot(witness.OwnerActorId, 1, 6));
        Assert.Throws<ArgumentException>(() => new WorldEffectiveControlState(3, released.Slots));
        Assert.Throws<ArgumentException>(() => new WorldEffectiveControlState(1, [new(witness.OwnerActorId, 2, 6)]));
        var input = new WorldControlAssignmentEvent(witness.OwnerActorId, WorldControlAssignmentEventKind.Acquire, 0, witness);
        Assert.Equal(input, WorldEffectiveControlCodecV1.DecodeEvent(WorldEffectiveControlCodecV1.EncodeEvent(input)));
        Assert.Throws<ArgumentException>(() => new WorldControlAssignmentEvent(witness.OwnerActorId, WorldControlAssignmentEventKind.Release, 0, witness));
    }

    [Fact]
    public void Retained_combat_retreat_and_remedy_envelopes_fit_unchanged_domain_bounds()
    {
        var dose = new IngredientRequest(IngredientLotKind.Transformed, Id(30), ulong.MaxValue, ulong.MaxValue);
        WorldActionIntent[] intents = [
            new QueueEncounterAbilityIntent(Id(20), ulong.MaxValue, Id(21), Id(11)),
            new QueueEncounterRetreatIntent(Id(20), ulong.MaxValue, new(long.MaxValue, long.MinValue, long.MaxValue),
                Enumerable.Repeat(EncounterRetreatDirection.East, EncounterRetreatRules.MaximumRouteSamples)),
            new AdministerRemedyIntent(Id(20), ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, dose),
            new UseRestorativeItemIntent(Id(11), Id(21), Pin, Id(22), Pin, ulong.MaxValue,
                ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, dose)
        ];
        foreach (var intent in intents)
        {
            var command = Command(intent);
            byte[] bytes = WorldActionCodec.Encode(command);
            Assert.InRange(bytes.Length, 1, 1024);
            var restored = WorldActionCodec.Decode(bytes);
            Assert.Equal(command.ContentHash, restored.ContentHash);
            Assert.Equal(command.ControlWitness, restored.ControlWitness);
            Assert.Equal(command.PrincipalId, restored.PrincipalId);
            Assert.Equal(command.OriginSessionId, restored.OriginSessionId);
            Assert.Equal(command.ActorId, restored.ActorId);
            Assert.Equal(bytes, WorldActionCodecV23.Encode(restored));
            Assert.Throws<FormatException>(() => WorldActionCodecV23.Decode(bytes[..^1]));
        }
        Assert.Throws<ArgumentException>(() => WorldActionCodecV11.Encode(Command(intents[0])));
        Assert.Throws<ArgumentException>(() => WorldActionCodecV19.Encode(Command(intents[1])));
        Assert.Throws<ArgumentException>(() => WorldActionCodecV13.Encode(Command(intents[2])));
        Assert.Throws<ArgumentException>(() => WorldActionCodecV14.Encode(Command(intents[3])));
    }

    [Fact]
    public void Profile40_movement_and_assignment_batches_are_explicit_and_legacy_bytes_stay_unchanged()
    {
        var witness = Witness();
        var move = new WorldMovementInput(new(Id(101), Id(111)), Id(10), 32, 0, witness);
        var batch = new WorldStepBatch(Id(40), Id(1), Id(2), 5, 0, WorldBatchKind.AdvancingStep, [move], [], 40);
        byte[] bytes = WorldStepCodec.Encode(batch);
        Assert.Equal(bytes, WorldStepCodec.Encode(WorldStepCodec.Decode(bytes)));
        Assert.Equal(witness, Assert.Single(WorldStepCodec.Decode(bytes).Movement).ControlWitness);
        Assert.Throws<ArgumentException>(() => new WorldStepBatch(Id(40), Id(1), Id(2), 5, 0, WorldBatchKind.AdvancingStep, [move], [], 39));
        Assert.Throws<FormatException>(() => WorldStepCodecV39.Decode(bytes));
        var assignment = new WorldStepBatch(witness.AssignmentId, Id(1), Id(2), 4, 0, WorldBatchKind.ControlAssignment, [], [], 40,
            controlAssignmentEvent: new(Id(10), WorldControlAssignmentEventKind.Acquire, 0, witness));
        Assert.Equal(WorldStepCodec.Encode(assignment), WorldStepCodec.Encode(WorldStepCodec.Decode(WorldStepCodec.Encode(assignment))));
        var old = new WorldActionCommand(Id(41), Id(100), Id(110), Id(10), Id(1), Id(2), 0, new CloseDialogueIntent(Id(42)));
        Assert.Equal(WorldActionCodecV1.Encode(old), WorldActionCodec.Encode(old));
        Assert.Null(WorldActionCodec.Decode(WorldActionCodec.Encode(old)).ControlWitness);
    }

    [Fact]
    public void Historical_witness_retains_true_grantee_without_granting_permanent_or_current_authority()
    {
        var world = AdmissionFixture.Create();
        var delivery = new TrustedCommandDelivery(Id(101), Id(111));
        var command = Command(new QueueEncounterAbilityIntent(Id(20), 0, Id(21), Id(11)));
        Assert.False(world.Inventory.CanControl(delivery, Id(10)));
        Assert.True(WorldActorCommandAuthority.ValidateRecordedEvidence(world.Inventory, command));
        Assert.False(WorldActorCommandAuthority.ValidateRecordedEvidence(world.Inventory, Id(10), new(Id(100), Id(110)), Witness(), WorldControlCapabilities.Combat));
        Assert.Null(WorldActorCommandAuthority.AuthorizeSelection(world, command));
        Assert.Null(WorldActorCommandAuthority.AuthorizeMovement(world, new(delivery, Id(10), 32, 0, Witness())));
        Assert.False(WorldActorCommandAuthority.ValidateRecordedEvidence(world.Inventory, Id(10), delivery, Witness(WorldControlCapabilities.Recovery), WorldControlCapabilities.CareDisposition));
        var owner = new WorldActionCommand(Id(41), Id(100), Id(110), Id(10), Id(1), Id(2), 0, new CloseDialogueIntent(Id(42)));
        var allowed = Assert.IsType<WorldActorAuthorization>(WorldActorCommandAuthority.AuthorizeSelection(world, owner));
        Assert.True(allowed.Matches(owner));
        Assert.False(allowed.Matches(new(Id(100), Id(110)), Id(10), Id(43)));
        Assert.True(allowed.Permits(WorldControlCapabilities.ConversationAndQuests));
        Assert.False(allowed.Permits(WorldControlCapabilities.Trade));
        Assert.Throws<ArgumentException>(() => allowed.Require(Id(10), new(Id(100), Id(110)), Id(43), WorldControlCapabilities.ConversationAndQuests));
    }

    private static EntityId Id(ulong value) => AdmissionFixture.Id(value);
    private static ContentHash Pin => ContentHash.Compute("effective-control-test"u8);
    private static WorldControlWitness Witness(WorldControlCapabilities capabilities = WorldControlCapabilities.All)
    {
        var terms = new WorldControlPermissionTerms(Id(1), Id(2), Id(10), Id(100), Id(110), Id(11), Id(101), Id(111), Id(200), capabilities);
        return new(Id(10), Id(11), Id(200), Id(201), 2, capabilities, terms.ContentHash, Id(202), 1, 5, Id(203), 2);
    }
    private static WorldActionCommand Command(WorldActionIntent intent) => new(Id(204), Id(101), Id(111), Id(10), Id(1), Id(2), ulong.MaxValue, intent, Witness());
}
