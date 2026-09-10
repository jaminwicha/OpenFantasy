using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Worlds;

public sealed class CommandBatchTests
{
    [Fact]
    public void Permutation_cannot_change_batch_bytes_and_caller_arrays_are_defensively_copied()
    {
        WorldMovementInput[] movement = [CommandFixture.Move(103, 0, -32), CommandFixture.Move(101)];
        WorldActionCommand[] actions = [CommandFixture.Speak(103, 403), CommandFixture.Speak(101, 401)];
        WorldStepBatch ordered = CommandFixture.Step(movement.Reverse(), actions.Reverse());
        WorldStepBatch reversed = CommandFixture.Step(movement, actions);
        byte[] encoded = WorldStepCodecV1.Encode(ordered);
        Assert.Equal(encoded, WorldStepCodecV1.Encode(reversed));
        Assert.Equal(CommandFixture.Id(101), reversed.Movement[0].ActorId);
        Assert.Equal(CommandFixture.Id(101), reversed.Actions[0].ActorId);
        movement[0] = movement[1];
        actions[0] = actions[1];
        Assert.Equal(encoded, WorldStepCodecV1.Encode(reversed));
        Assert.Throws<NotSupportedException>(() => ((IList<WorldMovementInput>)reversed.Movement)[0] = movement[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<WorldActionCommand>)reversed.Actions)[0] = actions[0]);
    }

    [Fact]
    public void Sixty_four_moving_and_acting_actors_fit_and_the_first_excess_entry_rejects_without_dropping_work()
    {
        WorldMovementInput[] movement = Enumerable.Range(0, 64).Select(index => CommandFixture.Move((ulong)index + 1000)).ToArray();
        WorldActionCommand[] actions = Enumerable.Range(0, 64).Select(index => new WorldActionCommand(
            CommandFixture.Id((ulong)index + 5000), CommandFixture.Id(201), CommandFixture.Id(301), CommandFixture.Id((ulong)index + 1000),
            CommandFixture.Id(1), CommandFixture.Id(2), ulong.MaxValue, new GiveItemIntent(CommandFixture.Id(109), ulong.MaxValue,
                CommandFixture.Id((ulong)index + 6000), ulong.MaxValue, CommandFixture.Id((ulong)index + 7000), ulong.MaxValue, ulong.MaxValue))).ToArray();
        WorldStepBatch maximum = CommandFixture.Step(movement, actions);
        byte[] encoded = WorldStepCodecV1.Encode(maximum);
        Assert.True(encoded.Length < WorldStepCodecV1.MaximumBatchByteLength);
        WorldStepBatch restored = WorldStepCodecV1.Decode(encoded);
        Assert.Equal(64, restored.Movement.Count);
        Assert.Equal(64, restored.Actions.Count);
        Assert.Equal(encoded, WorldStepCodecV1.Encode(restored));
        Assert.Throws<ArgumentException>(() => CommandFixture.Step(movement.Append(CommandFixture.Move(1064)), actions));
        Assert.Throws<ArgumentException>(() => CommandFixture.Step(movement, actions.Append(CommandFixture.Speak(1064, 5064))));
        Assert.Equal(64, maximum.Movement.Count);
        Assert.Equal(64, maximum.Actions.Count);
        int readMovement = 0;
        IEnumerable<WorldMovementInput> UnlimitedMovement()
        {
            while (true)
            {
                readMovement++;
                if (readMovement > 65) { throw new InvalidOperationException("Read beyond the admission bound."); }
                yield return CommandFixture.Move((ulong)readMovement + 1000);
            }
        }
        int readActions = 0;
        IEnumerable<WorldActionCommand> UnlimitedActions()
        {
            while (true)
            {
                readActions++;
                if (readActions > 65) { throw new InvalidOperationException("Read beyond the admission bound."); }
                yield return CommandFixture.Speak((ulong)readActions + 1000, (ulong)readActions + 5000);
            }
        }
        Assert.Throws<ArgumentException>(() => CommandFixture.Step(UnlimitedMovement()));
        Assert.Throws<ArgumentException>(() => CommandFixture.Step(actions: UnlimitedActions()));
        Assert.Equal(65, readMovement);
        Assert.Equal(65, readActions);
    }

    [Fact]
    public void One_movement_and_one_discrete_action_can_share_an_actor_without_sharing_cause_identity()
    {
        WorldActionCommand action = CommandFixture.Speak();
        WorldMovementInput move = CommandFixture.Move();
        WorldStepBatch batch = CommandFixture.Step([move], [action]);
        Assert.Equal(batch.Movement[0].ActorId, batch.Actions[0].ActorId);
        Assert.NotEqual(batch.CauseId, action.CommandId);
        // Origin is immutable request content; current sample delivery can be a different session.
        // Whether either delivery controls the actor is adjudicated by the host, not this codec.
        Assert.NotEqual(action.OriginSessionId, move.Delivery.SessionId);
        Assert.Equal(WorldStepCodecV1.Encode(batch), WorldStepCodecV1.Encode(WorldStepCodecV1.Decode(WorldStepCodecV1.Encode(batch))));
    }

    [Fact]
    public void Held_batches_require_actions_and_exclude_movement_while_empty_advancing_steps_remain_valid()
    {
        WorldStepBatch empty = CommandFixture.Step();
        Assert.Empty(empty.Actions);
        Assert.Empty(empty.Movement);
        WorldStepBatch held = CommandFixture.Step(actions: [CommandFixture.Speak()], kind: WorldBatchKind.HeldInteraction);
        Assert.Equal(WorldBatchKind.HeldInteraction, WorldStepCodecV1.Decode(WorldStepCodecV1.Encode(held)).Kind);
        Assert.Throws<ArgumentException>(() => CommandFixture.Step(kind: WorldBatchKind.HeldInteraction));
        Assert.Throws<ArgumentException>(() => CommandFixture.Step([CommandFixture.Move()], [CommandFixture.Speak()], WorldBatchKind.HeldInteraction));
        Assert.Throws<ArgumentException>(() => CommandFixture.Step(kind: (WorldBatchKind)3));
    }

    [Fact]
    public void Duplicated_actors_commands_foreign_scope_and_batch_cause_aliases_reject()
    {
        WorldActionCommand first = CommandFixture.Speak();
        Assert.Throws<ArgumentException>(() => CommandFixture.Step([CommandFixture.Move(), CommandFixture.Move(x: -32)]));
        Assert.Throws<ArgumentException>(() => CommandFixture.Step(actions: [first, CommandFixture.Speak(command: 402)]));
        Assert.Throws<ArgumentException>(() => CommandFixture.Step(actions: [first, CommandFixture.Speak(102)]));
        Assert.Throws<ArgumentException>(() => CommandFixture.Step(actions: [CommandFixture.Speak(command: 801)]));
        foreach ((EntityId world, EntityId branch) in new[] { (CommandFixture.Id(9), first.BranchId), (first.WorldId, CommandFixture.Id(9)) })
        {
            var foreign = new WorldActionCommand(first.CommandId, first.PrincipalId, first.OriginSessionId, first.ActorId,
                world, branch, first.ExpectedActorRevision, first.Intent);
            Assert.Throws<ArgumentException>(() => CommandFixture.Step(actions: [foreign]));
        }
        foreach (EntityId cause in new[] { CommandFixture.Id(1), CommandFixture.Id(2) })
        {
            Assert.Throws<ArgumentException>(() => new WorldStepBatch(cause, CommandFixture.Id(1), CommandFixture.Id(2), 0, 0,
                WorldBatchKind.AdvancingStep, [], []));
        }
    }

    [Fact]
    public void Cardinal_samples_are_exact_and_do_not_allow_clients_to_invent_more_time_or_distance()
    {
        foreach ((long x, long y) in new (long, long)[] { (32, 0), (-32, 0), (0, 32), (0, -32) })
        {
            WorldMovementInput sample = CommandFixture.Move(x: x, y: y);
            Assert.Equal(x, sample.DeltaX);
            Assert.Equal(y, sample.DeltaY);
        }
        foreach ((long x, long y) in new (long, long)[] { (0, 0), (32, 32), (-32, -32), (31, 0), (33, 0),
            (64, 0), (0, 64), (long.MinValue, 0), (0, long.MaxValue) })
        {
            Assert.Throws<ArgumentException>(() => CommandFixture.Move(x: x, y: y));
        }
    }

    [Fact]
    public void Constructors_reject_missing_inputs_zero_quantity_and_aliasing_command_cause()
    {
        WorldActionCommand action = CommandFixture.Speak();
        foreach (EntityId cause in new[] { action.PrincipalId, action.OriginSessionId, action.ActorId, action.WorldId, action.BranchId })
        {
            Assert.Throws<ArgumentException>(() => new WorldActionCommand(cause, action.PrincipalId, action.OriginSessionId,
                action.ActorId, action.WorldId, action.BranchId, 0, action.Intent));
        }
        Assert.Throws<ArgumentNullException>(() => new WorldActionCommand(action.CommandId, action.PrincipalId, action.OriginSessionId,
            action.ActorId, action.WorldId, action.BranchId, 0, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GiveItemIntent(CommandFixture.Id(109), 0, CommandFixture.Id(501), 0,
            CommandFixture.Id(601), 0, 0));
        Assert.Throws<ArgumentNullException>(() => new WorldMovementInput(null!, action.ActorId, 32, 0));
        Assert.Throws<ArgumentNullException>(() => new WorldStepBatch(CommandFixture.Id(801), action.WorldId, action.BranchId, 0, 0,
            WorldBatchKind.AdvancingStep, null!, []));
        Assert.Throws<ArgumentNullException>(() => new WorldStepBatch(CommandFixture.Id(801), action.WorldId, action.BranchId, 0, 0,
            WorldBatchKind.AdvancingStep, [], null!));
        Assert.Throws<ArgumentNullException>(() => CommandFixture.Step([null!]));
        Assert.Throws<ArgumentNullException>(() => CommandFixture.Step(actions: [null!]));
    }
}
