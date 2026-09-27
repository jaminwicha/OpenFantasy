using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Actors.BodyDecodingTestFrames;

namespace OpenFantasy.Core.Tests.Care;

public sealed class CareProtocolTests
{
    private static EntityId Id(ulong value) => new(0xca4e25, value);
    private static readonly ContentHash ActionHash = ContentHash.Compute("care outcome protocol"u8);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Commands_retain_exact_captured_revisions_and_material_selection(int variant)
    {
        var command = Command(Intent(variant));
        byte[] bytes = WorldActionCodec.Encode(command);
        Assert.InRange(bytes.Length, 1, WorldActionCodecV13.MaximumCommandByteLength);
        Assert.Equal(ExpectedBytes(command), bytes);
        var restored = WorldActionCodec.Decode(bytes);
        Assert.Equal(command.Intent, restored.Intent);
        Assert.Equal(command.CommandId, restored.CommandId);
        Assert.Equal(command.ExpectedActorRevision, restored.ExpectedActorRevision);
        Assert.Equal(command.ContentHash, restored.ContentHash);
        Assert.Equal(bytes, WorldActionCodecV13.Encode(restored));
        Assert.Throws<ArgumentException>(() => WorldActionCodecV12.Encode(command));
        Assert.Throws<FormatException>(() => WorldActionCodecV12.Decode(bytes));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Care_commands_reject_wrong_versions_widths_trailing_data_and_malformed_ingredients(int variant)
    {
        byte[] bytes = WorldActionCodec.Encode(Command(Intent(variant)));
        byte[] intent = Fields(bytes)[9];
        var bad = new List<byte[]>
        {
            bytes[..^1], bytes.Concat(new byte[] { 0 }).ToArray(), new byte[WorldActionCodecV13.MaximumCommandByteLength + 1],
            Replace(bytes, 1, Number(14)), Replace(bytes, 8, new byte[7]), Replace(bytes, 2, new byte[15]),
            Replace(bytes, 9, Replace(intent, 0, Text("world.action.unknown-care"))),
            Replace(bytes, 9, Replace(intent, 1, Number(2))),
            Replace(bytes, 9, Frame(Fields(intent).Append(Number(0))))
        };
        if (variant == 0)
        {
            bad.Add(Replace(bytes, 9, Replace(intent, 5, new byte[31])));
            bad.Add(Replace(bytes, 9, Replace(intent, 4, new byte[9])));
        }
        else
        {
            byte[] ingredient = Fields(intent)[8];
            foreach (byte[] invalid in new[]
            {
                Replace(ingredient, 1, Number(0)), Replace(ingredient, 1, Number(3)),
                Replace(ingredient, 1, Number(ulong.MaxValue)), Replace(ingredient, 2, new byte[17]),
                Replace(ingredient, 3, new byte[7]), Replace(ingredient, 4, Number(0)),
                Frame(Fields(ingredient).Append(Number(0)))
            })
                bad.Add(Replace(bytes, 9, Replace(intent, 8, invalid)));
        }
        foreach (byte[] invalid in bad)
        {
            Assert.Throws<FormatException>(() => WorldActionCodecV13.Decode(invalid));
            Assert.Throws<FormatException>(() => WorldActionCodec.Decode(invalid));
        }
    }

    [Fact]
    public void Care_intents_require_owned_nonnull_pins_and_selection()
    {
        Assert.Throws<ArgumentNullException>(() => new AcknowledgeCareIntent(Id(20), Id(21), 0, null!));
        Assert.Throws<ArgumentNullException>(() => new AdministerRemedyIntent(Id(22), 0, 0, 0, 0, 0, null!));
        Assert.Throws<ArgumentException>(() => WorldActionCodecV13.Encode(Command(new CloseDialogueIntent(Id(30)))));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Care_boundary_is_one_profile25_command_without_movement(int variant)
    {
        var command = Command(Intent(variant));
        var batch = Batch(WorldBatchKind.Care, [command]);
        byte[] bytes = WorldStepCodec.Encode(batch);
        Assert.Equal(Number(25), Fields(bytes)[1]);
        Assert.Equal(Number(9), Fields(bytes)[7]);
        var restored = WorldStepCodec.Decode(bytes);
        Assert.Equal(WorldBatchKind.Care, restored.Kind);
        Assert.Empty(restored.Movement);
        Assert.Equal(command.ContentHash, Assert.Single(restored.Actions).ContentHash);
        Assert.Equal(bytes, WorldStepCodecV25.Encode(restored));
        for (ushort profile = 1; profile < 25; profile++)
        {
            Assert.Throws<ArgumentException>(() => Batch(WorldBatchKind.Care, [command], profile));
            Assert.Throws<FormatException>(() => WorldStepCodec.Decode(Replace(bytes, 1, Number(profile))));
        }
        foreach (var kind in Enum.GetValues<WorldBatchKind>().Where(value => value != WorldBatchKind.Care))
        {
            Assert.Throws<ArgumentException>(() => Batch(kind, [command]));
            Assert.Throws<FormatException>(() => WorldStepCodec.Decode(Replace(bytes, 7, Number((ulong)kind))));
        }
        Assert.Throws<ArgumentException>(() => Batch(WorldBatchKind.Care, []));
        Assert.Throws<ArgumentException>(() => Batch(WorldBatchKind.Care, [command, Command(Intent(variant), 9, 11)]));
        Assert.Throws<ArgumentException>(() => Batch(WorldBatchKind.Care, [command], movement: [new(new(Id(2), Id(3)), Id(4), 32, 0)]));
        Assert.Throws<ArgumentException>(() => Batch(WorldBatchKind.Care, [Command(new CloseDialogueIntent(Id(30)))]));
        Assert.Throws<ArgumentException>(() => Batch(WorldBatchKind.Care, [Command(new QueueEncounterWaitIntent(Id(21), 1))]));
        Assert.Throws<FormatException>(() => WorldStepCodec.Decode(Replace(bytes, 9, Frame([Text("world.step-actions")]))));
        Assert.Throws<FormatException>(() => WorldStepCodec.Decode(Replace(bytes, 7, Number(10))));
        Assert.Throws<FormatException>(() => WorldStepCodec.Decode(bytes[..^1]));
        Assert.Throws<FormatException>(() => WorldStepCodec.Decode(bytes.Concat(new byte[] { 0 }).ToArray()));
        Assert.Throws<ArgumentException>(() => WorldStepCodecV24.Encode(batch));
    }

    [Fact]
    public void Profile25_retains_profile24_action_interpreters_and_system_boundaries()
    {
        (WorldBatchKind Kind, WorldActionIntent Intent)[] cases =
        [
            (WorldBatchKind.AdvancingStep, new SpeakToActorIntent(Id(30), 0)),
            (WorldBatchKind.HeldInteraction, new CloseDialogueIntent(Id(30))),
            (WorldBatchKind.AdvancingStep, new ExcavateIntent(Id(31), 1, 2, Id(32), 3, 4)),
            (WorldBatchKind.EncounterDecision, new QueueEncounterWaitIntent(Id(21), 1)),
            (WorldBatchKind.EncounterDecision, new QueueEncounterAbilityIntent(Id(21), 1, Id(33), Id(30))),
            (WorldBatchKind.EncounterLifecycle, new EnterEncounterIntent(Id(34), Id(21), 1, 2)),
            (WorldBatchKind.EncounterLifecycle, new ReleaseEncounterIntent(Id(21), ActionHash))
        ];
        foreach (var item in cases)
        {
            var command = Command(item.Intent);
            byte[] oldBytes = WorldStepCodec.Encode(Batch(item.Kind, [command], 24));
            byte[] newBytes = WorldStepCodec.Encode(Batch(item.Kind, [command]));
            Assert.Equal(Replace(oldBytes, 1, Number(25)), newBytes);
            Assert.Equal(newBytes, WorldStepCodec.Encode(WorldStepCodec.Decode(newBytes)));
            Assert.Equal(oldBytes, WorldStepCodec.Encode(WorldStepCodec.Decode(oldBytes)));
        }
        foreach (var kind in new[] { WorldBatchKind.AdvancingStep, WorldBatchKind.TacticalAdvance,
            WorldBatchKind.EncounterResolution, WorldBatchKind.EncounterLifecycle })
        {
            byte[] bytes = WorldStepCodec.Encode(Batch(kind, []));
            Assert.Equal(bytes, WorldStepCodecV25.Encode(WorldStepCodecV25.Decode(bytes)));
        }
        Assert.Throws<ArgumentException>(() => Batch(WorldBatchKind.ConsequenceContinuation, []));
    }

    private static WorldActionIntent Intent(int variant) => variant == 0
        ? new AcknowledgeCareIntent(Id(20), Id(21), ulong.MaxValue, ActionHash)
        : new AdministerRemedyIntent(Id(22), ulong.MaxValue, ulong.MaxValue - 1, ulong.MaxValue - 2,
            ulong.MaxValue - 3, ulong.MaxValue - 4,
            new((IngredientLotKind)variant, Id(23), ulong.MaxValue - 5, ulong.MaxValue));

    private static WorldActionCommand Command(WorldActionIntent intent, ulong commandId = 1, ulong actorId = 4) =>
        new(Id(commandId), Id(2), Id(3), Id(actorId), Id(5), Id(6), ulong.MaxValue, intent);

    private static WorldStepBatch Batch(WorldBatchKind kind, IEnumerable<WorldActionCommand> actions,
        ushort profile = 25, IEnumerable<WorldMovementInput>? movement = null) =>
        new(Id(7), Id(5), Id(6), ulong.MaxValue - 1, ulong.MaxValue - 2, kind, movement ?? [], actions, profile);

    private static byte[] ExpectedBytes(WorldActionCommand command)
    {
        byte[] intent = command.Intent switch
        {
            AcknowledgeCareIntent acknowledge => Frame([Text("world.action.acknowledge-care"), Number(1),
                acknowledge.AgreementId.ToByteArray(), acknowledge.EncounterId.ToByteArray(),
                Number(acknowledge.ExpectedLifecycleRevision), acknowledge.ExpectedActionHash.ToByteArray()]),
            AdministerRemedyIntent administer => Frame([Text("world.action.administer-remedy"), Number(1),
                administer.AcknowledgmentId.ToByteArray(), Number(administer.ExpectedCareRevision),
                Number(administer.ExpectedPatientPresenceRevision), Number(administer.ExpectedPatientProgressionRevision),
                Number(administer.ExpectedStorageRevision), Number(administer.ExpectedProductionRevision),
                Frame([Text("world.care.ingredient"), Number((ulong)administer.Ingredient.Kind), administer.Ingredient.LotId.ToByteArray(),
                    Number(administer.Ingredient.ExpectedRevision), Number(administer.Ingredient.Quantity)])]),
            _ => throw new InvalidOperationException("The independent care vector requires a care intent.")
        };
        return Frame([Text("world.actor-action"), Number(13), command.CommandId.ToByteArray(), command.PrincipalId.ToByteArray(),
            command.OriginSessionId.ToByteArray(), command.ActorId.ToByteArray(), command.WorldId.ToByteArray(),
            command.BranchId.ToByteArray(), Number(command.ExpectedActorRevision), intent]);
    }
}
