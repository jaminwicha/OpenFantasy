using OpenFantasy.Core;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.EffectiveControl;

public sealed class EffectiveControlJournalTests
{
    [Fact]
    public void Named_grantee_really_moves_the_owner_with_true_delivery_and_exact_reopen()
    {
        var fixture = new EffectiveControlFixture();
        using var files = new StorageFiles();
        WorldSnapshot final;
        WorldControlWitness witness;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            Assert.True(authority.SupportsControllerRecovery);
            fixture.Grant(authority);
            var expired = fixture.LoseAndExpire(authority);
            var acquired = fixture.Acquire(authority);
            witness = acquired.EffectiveControl!.Find(fixture.Owner)!.Witness!;
            Assert.Equal(expired.Calendar, acquired.Calendar);
            Assert.Equal(expired.AdvancingSteps, acquired.AdvancingSteps);
            Assert.Equal(expired.Inventory.ContentHash, acquired.Inventory.ContentHash);
            Assert.Equal(expired.Progression!.ContentHash, acquired.Progression!.ContentHash);
            Assert.Equal(WorldControllerMode.Autonomous, acquired.Controllers!.Get(fixture.Owner).Mode);
            Assert.False(acquired.Inventory.CanControl(fixture.Delivery(fixture.Grantee), fixture.Owner));
            Assert.Equal(fixture.Grantee, witness.GranteeActorId);
            var owner = acquired.Space.GetActor(fixture.Owner);
            var offset = new (long X, long Y)[] { (32, 0), (-32, 0), (0, 32), (0, -32) }
                .First(value => acquired.Space.PrepareMove(fixture.Owner, owner.Revision, value.X, value.Y).Status == SpatialMoveStatus.Prepared);
            var movement = new WorldMovementInput(fixture.Delivery(fixture.Grantee), fixture.Owner, offset.X, offset.Y, witness);
            Assert.NotNull(WorldActorCommandAuthority.AuthorizeMovement(acquired, movement));
            var batch = new WorldStepBatch(fixture.Commands.NextId(), acquired.WorldId, acquired.BranchId,
                acquired.Revision, acquired.AdvancingSteps, WorldBatchKind.AdvancingStep, [movement], [], acquired.ProfileVersion);
            final = authority.Commit(batch).Candidate;
            Assert.Equal(owner.Feet.Offset(offset.X, offset.Y, 0), final.Space.GetActor(fixture.Owner).Feet);
            Assert.Equal(acquired.Space.GetActor(fixture.Grantee).Feet, final.Space.GetActor(fixture.Grantee).Feet);
            Assert.Equal(acquired.Inventory.ContentHash, final.Inventory.ContentHash);
            Assert.Equal(witness, final.EffectiveControl!.Find(fixture.Owner)!.Witness);
            Assert.Equal(acquired.AdvancingSteps + 1, final.AdvancingSteps);
        }
        using (var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis)))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
            Assert.Equal(witness, reopened.ReadStateForHost().EffectiveControl!.Find(fixture.Owner)!.Witness);
            Assert.Empty(reopened.ReadControllerAllowancesForHost());
        }
        using var database = WorldDatabase.Open(files.Path, false);
        Assert.Equal(12, database.SchemaVersion);
        var stored = database.ReadJournal().Last();
        var retained = Assert.Single(WorldStepCodec.Decode(stored.AcceptedBatch).Movement);
        Assert.Equal(fixture.Delivery(fixture.Grantee), retained.Delivery);
        Assert.Equal(fixture.Owner, retained.ActorId);
        Assert.Equal(witness, retained.ControlWitness);
    }

    [Theory]
    [InlineData("revoke")]
    [InlineData("return")]
    [InlineData("grantee-loss")]
    [InlineData("release")]
    public void Closing_authority_invalidates_the_captured_assignment_without_changing_ownership_or_earning_time(string closure)
    {
        var fixture = new EffectiveControlFixture();
        using var files = new StorageFiles();
        WorldSnapshot closed;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            fixture.Grant(authority);
            fixture.LoseAndExpire(authority);
            var acquired = fixture.Acquire(authority);
            var witness = acquired.EffectiveControl!.Find(fixture.Owner)!.Witness!;
            var movement = new WorldMovementInput(fixture.Delivery(fixture.Grantee), fixture.Owner, 32, 0, witness);
            Assert.NotNull(WorldActorCommandAuthority.AuthorizeMovement(acquired, movement));
            closed = closure switch
            {
                "revoke" => fixture.Revoke(authority),
                "return" => fixture.Event(authority, fixture.Owner, WorldControllerEventKind.Reconnected),
                "grantee-loss" => fixture.Event(authority, fixture.Grantee, WorldControllerEventKind.Disconnected),
                _ => fixture.Release(authority)
            };
            Assert.Null(closed.EffectiveControl!.Find(fixture.Owner)!.Witness);
            Assert.Null(WorldActorCommandAuthority.AuthorizeMovement(closed, movement));
            Assert.Equal(acquired.Calendar, closed.Calendar);
            Assert.Equal(acquired.AdvancingSteps, closed.AdvancingSteps);
            Assert.Equal(acquired.Inventory.ContentHash, closed.Inventory.ContentHash);
            Assert.Equal(acquired.Progression!.ContentHash, closed.Progression!.ContentHash);
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(new WorldStepBatch(fixture.Commands.NextId(),
                closed.WorldId, closed.BranchId, closed.Revision, closed.AdvancingSteps,
                WorldBatchKind.AdvancingStep, [movement], [], closed.ProfileVersion)));
            Assert.Same(closed, authority.ReadStateForHost());
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(closed), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
    }

    [Fact]
    public void Scope_and_direct_owner_competition_are_refused_before_gameplay_dispatch()
    {
        var fixture = new EffectiveControlFixture();
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        fixture.Grant(authority, WorldControlCapabilities.Traversal);
        fixture.LoseAndExpire(authority);
        var acquired = fixture.Acquire(authority);
        var speak = new SpeakToActorIntent(fixture.Other, acquired.Space.GetActor(fixture.Other).Revision);
        var delegated = fixture.Delegated(acquired, speak);
        var direct = fixture.Commands.Command(acquired, fixture.Owner, speak);
        // Establish the intended authorization refusal independently of reach, dialogue or readiness.
        Assert.Null(WorldActorCommandAuthority.AuthorizeSelection(acquired, delegated));
        Assert.Null(WorldActorCommandAuthority.AuthorizeSelection(acquired, direct));
        foreach (var command in new[] { delegated, direct })
        {
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(
                fixture.Commands.Batch(acquired, WorldBatchKind.AdvancingStep, [command])));
            Assert.Throws<AuthorityDispatchException>(() => authority.FindReceipt(command,
                fixture.Delivery(command == delegated ? fixture.Grantee : fixture.Owner)));
            Assert.False(authority.IsIdentityOccupiedForHost(command.CommandId));
            Assert.Same(acquired, authority.ReadStateForHost());
        }
        Assert.NotNull(WorldActorCommandAuthority.AuthorizeMovement(acquired,
            new WorldMovementInput(fixture.Delivery(fixture.Grantee), fixture.Owner, 32, 0, acquired.EffectiveControl!.Find(fixture.Owner)!.Witness)));
    }
}
