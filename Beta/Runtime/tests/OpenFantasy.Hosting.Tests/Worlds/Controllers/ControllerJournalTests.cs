using System.Buffers.Binary;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.Controllers;

/// <summary>Actual same-authority session-event history, distinct from transport/timer acceptance.</summary>
public sealed partial class ControllerJournalTests
{
    private static readonly Lazy<WorldSnapshot> Basis = new(() =>
        CargoOutcomeWorldAdmission.CreateEmbedded("controller-history", new[] { "Traveler", "Mira", "Joss", "Lark" }));
    private static WorldSnapshot Fresh() => WorldControllerAdmission.CreateFresh(Basis.Value);
    private static EntityId Id(ulong ordinal) => new(0xea55000000000000, ordinal);
    private static EntityId Player(WorldSnapshot state) => state.Controllers!.Assignments[0].ActorId;
    private static WorldStepBatch Event(WorldSnapshot state, WorldControllerEvent input, ulong ordinal) =>
        new(Id(ordinal), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
            WorldBatchKind.ControllerLifecycle, [], [], state.ProfileVersion, input);
    private static WorldControllerEvent Disconnect(WorldSnapshot state, EntityId actor) =>
        new(actor, WorldControllerEventKind.Disconnected, state.Controllers!.Get(actor).Revision,
            reason: WorldControllerDisconnectReason.TransportLost);
    private static WorldControllerEvent Expire(WorldSnapshot state, EntityId actor) =>
        new(actor, WorldControllerEventKind.AllowanceExpired, state.Controllers!.Get(actor).Revision,
            state.Controllers.Get(actor).DisconnectId, WorldControllerState.ReconnectAllowanceMilliseconds);
    private static WorldControllerEvent Reconnect(WorldSnapshot state, EntityId actor) =>
        new(actor, WorldControllerEventKind.Reconnected, state.Controllers!.Get(actor).Revision,
            state.Controllers.Get(actor).DisconnectId);

    [Fact]
    public void Explicit_profile_roundtrips_without_reinterpreting_the_old_genesis_or_batch()
    {
        WorldSnapshot basis = Basis.Value, world = Fresh();
        Assert.Equal((ushort)35, basis.ProfileVersion);
        Assert.Equal((ushort)36, world.ProfileVersion);
        Assert.NotEqual(basis.AdmissionVersion, world.AdmissionVersion);
        Assert.Same(basis.Inventory, world.Inventory);
        Assert.Same(basis.Space, world.Space);
        Assert.Same(basis.EncounterLifecycle, world.EncounterLifecycle);
        Assert.Same(basis.Progression, world.Progression);
        byte[] original = WorldSnapshotCodec.Encode(basis), bytes = WorldSnapshotCodec.Encode(world);
        Assert.Equal(original, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(original)));
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
        Assert.Throws<FormatException>(() => WorldSnapshotCodecV35.Decode(bytes));
        Assert.Throws<FormatException>(() => WorldSnapshotCodecV36.Decode(original));
        Assert.Throws<ArgumentException>(() => WorldSnapshotCodecV35.Encode(world));
        Assert.Throws<ArgumentException>(() => WorldControllerAdmission.CreateFresh(world));
        var batch = Event(world, Disconnect(world, Player(world)), 1);
        byte[] command = WorldStepCodec.Encode(batch);
        Assert.Equal(command, WorldStepCodec.Encode(WorldStepCodec.Decode(command)));
        Assert.Throws<FormatException>(() => WorldStepCodecV35.Decode(command));
        Assert.Throws<ArgumentException>(() => new WorldStepBatch(Id(2), basis.WorldId, basis.BranchId, 0, 0,
            WorldBatchKind.ControllerLifecycle, [], [], basis.ProfileVersion, batch.ControllerEvent));
        Assert.Throws<ArgumentException>(() => new WorldStepBatch(Id(3), world.WorldId, world.BranchId, 0, 0,
            WorldBatchKind.AdvancingStep, [], [], world.ProfileVersion, batch.ControllerEvent));
        Assert.Throws<FormatException>(() => WorldStepCodec.Decode(command.Concat(new byte[] { 0 }).ToArray()));
    }

    [Fact]
    public void Every_actor_disconnect_expiry_and_rejoin_replays_without_time_or_ownership_changes()
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Fresh(), state = genesis;
        ulong next = 10;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            foreach (var row in genesis.Controllers!.Assignments)
            {
                WorldSnapshot before = state;
                state = authority.Commit(Event(state, Disconnect(state, row.ActorId), next++)).Candidate;
                AssertGameplayUnchanged(before, state);
            }
            foreach (var row in genesis.Controllers.Assignments)
            {
                state = authority.Commit(Event(state, Expire(state, row.ActorId), next++)).Candidate;
                Assert.Equal(WorldControllerMode.Autonomous, state.Controllers!.Get(row.ActorId).Mode);
            }
        }
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis)))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(state), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            foreach (var row in genesis.Controllers!.Assignments)
                state = authority.Commit(Event(state, Reconnect(state, row.ActorId), next++)).Candidate;
            AssertGameplayUnchanged(genesis, state);
            Assert.Equal(12UL, state.Revision);
            Assert.All(state.Controllers!.Assignments, row => Assert.Equal((3UL, WorldControllerMode.Human, (EntityId?)null),
                (row.Revision, row.Mode, row.DisconnectId)));
            Assert.Equal(0UL, authority.InspectForHost().Journal.CommandOutcomeCount);
        }
        using var recovered = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(state), WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
    }

    [Fact]
    public void Duplicate_stale_and_aliased_events_are_refused_without_publishing_a_new_root()
    {
        using var files = new StorageFiles();
        var genesis = Fresh();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        EntityId actor = Player(genesis);
        var first = Event(genesis, Disconnect(genesis, actor), 40);
        WorldSnapshot state = authority.Commit(first).Candidate;
        Assert.Throws<AuthorityDispatchException>(() => authority.Commit(first));
        Assert.Throws<AuthorityDispatchException>(() => authority.Commit(Event(state, Disconnect(genesis, actor), 41)));
        Assert.Throws<AuthorityDispatchException>(() => authority.Commit(Event(state, Expire(state, actor), 40)));
        Assert.Throws<ArgumentException>(() => new WorldControllerEvent(actor, WorldControllerEventKind.AllowanceExpired,
            1, state.Controllers!.Get(actor).DisconnectId, 29_999));
        Assert.Same(state, authority.ReadStateForHost());
        Assert.Equal(1UL, state.Revision);
        Assert.Throws<ArgumentException>(() => WorldControllerAdmission.CreateFresh(state));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_transaction_fault_recovers_exactly_before_or_after_the_control_event(bool committed)
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Fresh();
        var batch = Event(genesis, Disconnect(genesis, Player(genesis)), 50);
        var expected = new WorldStepProcessor().Decide(genesis, batch).Candidate;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            Assert.Throws<IOException>(() => authority.Commit(batch, boundary =>
            {
                if (boundary == (committed ? AuthorityCommitBoundary.AfterCommit : AuthorityCommitBoundary.BeforeCommit))
                    throw new IOException("Injected transaction boundary failure.");
            }));
        }
        using var recovered = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(committed ? expected : genesis),
            WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
    }

    [Fact]
    public void Disconnect_and_rejoin_do_not_release_a_reading_hold_or_change_its_dialogue()
    {
        using var files = new StorageFiles();
        var genesis = Fresh();
        var fixture = new ConstructionUpgradeFixture();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        EntityId wren = genesis.Quests!.Definitions[0].GiverActorId;
        EntityId actor = genesis.Residents.First(row => row.ControllerKind == ResidentControllerKind.Player
            && genesis.Space.QueryInteraction(row.ActorId, wren) == OpenFantasy.Core.Spatial.SpatialInteraction.Reachable).ActorId;
        var held = fixture.Act(authority, genesis, actor, new SpeakToActorIntent(wren, genesis.Space.GetActor(wren).Revision));
        Assert.True(held.HasReadingHold);
        var state = authority.Commit(Event(held, Disconnect(held, actor), 60)).Candidate;
        state = authority.Commit(Event(state, Reconnect(state, actor), 61)).Candidate;
        Assert.True(state.HasReadingHold);
        AssertGameplayUnchanged(held, state);
    }

    internal static void AssertGameplayUnchanged(WorldSnapshot before, WorldSnapshot after)
    {
        // Compare every canonical root field, including all inherited facets. Only journal revision
        // and the controller owner may differ; lifecycle bookkeeping follows the world revision.
        byte[][] original = Fields(WorldSnapshotCodec.Encode(before));
        byte[][] current = Fields(WorldSnapshotCodec.Encode(after));
        Assert.Equal(27, original.Length);
        Assert.Equal(original.Length, current.Length);
        for (int field = 0; field < original.Length; field++)
            if (field is not 8 and not 21 and not 26) Assert.Equal(original[field], current[field]);
        byte[][] originalLifecycle = Fields(original[21]), currentLifecycle = Fields(current[21]);
        Assert.Equal(17, originalLifecycle.Length);
        Assert.Equal(originalLifecycle.Length, currentLifecycle.Length);
        for (int field = 0; field < originalLifecycle.Length; field++)
            if (field != 5) Assert.Equal(originalLifecycle[field], currentLifecycle[field]);
        Assert.Equal(after.Revision, after.EncounterLifecycle!.Revision);
        Assert.Equal(before.Revision, before.EncounterLifecycle!.Revision);
    }

    private static byte[][] Fields(byte[] bytes)
    {
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8))), offset = 12;
        var fields = new byte[count][];
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
            offset += 4;
            fields[index] = bytes.AsSpan(offset, length).ToArray();
            offset += length;
        }
        Assert.Equal(bytes.Length, offset);
        return fields;
    }
}
