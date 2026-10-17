using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.EffectiveControl;

public sealed class ControlDependencyTests
{
    [Fact]
    public void Owner_read_tracks_new_assignment_but_not_unrelated_grants_or_another_actors_control()
    {
        var fixture = new EffectiveControlFixture();
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var owner = Capture(fixture.Genesis, fixture, delegated: false);
        var grantee = WorldActorControlDependencies.Capture(fixture.Genesis, fixture.Grantee,
            fixture.Delivery(fixture.Grantee), null, WorldControlCapabilities.Traversal);
        Assert.True(owner.IsAuthorized);
        Assert.True(grantee.IsAuthorized);
        owner.ValidateCurrent(fixture.Grant(authority));
        fixture.LoseAndExpire(authority);
        var acquired = fixture.Acquire(authority);
        Assert.Throws<InvalidOperationException>(() => owner.ValidateCurrent(acquired));
        Assert.False(Capture(acquired, fixture, delegated: false).IsAuthorized);
        grantee.ValidateCurrent(acquired);
        Assert.True(grantee.IsAuthorized);
    }

    [Theory]
    [InlineData("revoke")]
    [InlineData("return")]
    [InlineData("grantee-loss")]
    [InlineData("release")]
    public void Unchanged_geometry_cannot_hide_revocation_return_loss_or_release(string closure)
    {
        var fixture = new EffectiveControlFixture();
        using var files = new StorageFiles();
        WorldActorControlDependencies control;
        SpatialMovementDependencies spatial;
        WorldSnapshot closed;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            var acquired = Acquire(fixture, authority);
            control = Capture(acquired, fixture);
            var actor = acquired.Space.GetActor(fixture.Owner);
            var offset = LegalOffset(acquired, fixture.Owner);
            spatial = SpatialMovementDependencies.Capture(acquired.Space, fixture.Owner, actor.Revision,
                offset.X, offset.Y, LocalSpatialSnapshot.MaximumRegions);
            Assert.True(control.IsAuthorized);
            closed = closure switch
            {
                "revoke" => fixture.Revoke(authority),
                "return" => fixture.Event(authority, fixture.Owner, WorldControllerEventKind.Reconnected),
                "grantee-loss" => fixture.Event(authority, fixture.Grantee, WorldControllerEventKind.Disconnected),
                _ => fixture.Release(authority)
            };
            spatial.ValidateCurrent(closed.Space);
            Assert.Equal(SpatialMoveStatus.Prepared, spatial.Evaluate(spatial.Terrain.Resolve(
                closed.Space.Regions.Where(region => spatial.Terrain.References.Any(reference => reference.Bounds == region.Bounds)))).Status);
            Assert.Throws<InvalidOperationException>(() => control.ValidateCurrent(closed));
            Assert.True(control.IsAuthorized); // Historical selection; it cannot assert current authority.
            Assert.False(WorldActorControlDependencies.Capture(closed, fixture.Owner, control.Delivery,
                control.Witness, control.RequiredCapabilities).IsAuthorized);
            Assert.True(WorldActorCommandAuthority.ValidateRecordedEvidence(closed.Inventory, fixture.Owner,
                control.Delivery, control.Witness, control.RequiredCapabilities));
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        var restored = reopened.ReadStateForHost();
        Assert.Equal(WorldSnapshotCodec.Encode(closed), WorldSnapshotCodec.Encode(restored));
        spatial.ValidateCurrent(restored.Space);
        Assert.Throws<InvalidOperationException>(() => control.ValidateCurrent(restored));
    }

    [Fact]
    public void Actual_movement_time_and_original_history_reopening_preserve_unchanged_control_dependencies()
    {
        var fixture = new EffectiveControlFixture();
        using var files = new StorageFiles();
        WorldActorControlDependencies control;
        WorldSnapshot final;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            var acquired = Acquire(fixture, authority);
            control = Capture(acquired, fixture);
            var offset = LegalOffset(acquired, fixture.Owner);
            var input = new WorldMovementInput(control.Delivery, fixture.Owner, offset.X, offset.Y, control.Witness);
            final = authority.Commit(new WorldStepBatch(fixture.Commands.NextId(), acquired.WorldId, acquired.BranchId,
                acquired.Revision, acquired.AdvancingSteps, WorldBatchKind.AdvancingStep, [input], [], acquired.ProfileVersion)).Candidate;
            Assert.NotEqual(acquired.Space.GetActor(fixture.Owner).Feet, final.Space.GetActor(fixture.Owner).Feet);
            Assert.Equal(acquired.AdvancingSteps + 1, final.AdvancingSteps);
            Assert.NotEqual(acquired.GetResident(fixture.Owner).ExperiencedTime, final.GetResident(fixture.Owner).ExperiencedTime);
            control.ValidateCurrent(final);
            control.ValidateCurrent(WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(final)));
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        control.ValidateCurrent(reopened.ReadStateForHost());
    }

    [Fact]
    public void Release_and_reacquisition_of_the_same_grant_cannot_resurrect_an_old_read()
    {
        var fixture = new EffectiveControlFixture();
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var acquired = Acquire(fixture, authority);
        var prior = Capture(acquired, fixture);
        fixture.Release(authority);
        var reacquired = fixture.Acquire(authority);
        Assert.Equal(acquired.ControlPermissions!.ContentHash, reacquired.ControlPermissions!.ContentHash);
        Assert.Throws<InvalidOperationException>(() => prior.ValidateCurrent(reacquired));
        Assert.False(WorldActorControlDependencies.Capture(reacquired, fixture.Owner, prior.Delivery,
            prior.Witness, prior.RequiredCapabilities).IsAuthorized);
        Assert.True(Capture(reacquired, fixture).IsAuthorized);
    }

    [Fact]
    public void Stable_read_does_not_grant_missing_scope_or_authenticate_a_false_delivery()
    {
        var fixture = new EffectiveControlFixture();
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var acquired = Acquire(fixture, authority);
        var witness = acquired.EffectiveControl!.Find(fixture.Owner)!.Witness;
        var real = fixture.Delivery(fixture.Grantee);
        (TrustedCommandDelivery Delivery, WorldControlCapabilities Required)[] denied = [
            (real, WorldControlCapabilities.Trade),
            (real, 0),
            (fixture.Delivery(fixture.Owner), WorldControlCapabilities.Traversal),
            (new(real.PrincipalId, fixture.Delivery(fixture.Owner).SessionId), WorldControlCapabilities.Traversal)
        ];
        foreach (var input in denied)
        {
            var prepared = WorldActorControlDependencies.Capture(acquired, fixture.Owner, input.Delivery, witness, input.Required);
            Assert.False(prepared.IsAuthorized);
            prepared.ValidateCurrent(acquired);
            Assert.False(WorldActorCommandAuthority.IsCurrentWitness(acquired, fixture.Owner, input.Delivery, witness, input.Required));
        }
    }

    private static WorldSnapshot Acquire(EffectiveControlFixture fixture, SqliteWorldAuthority authority)
    {
        fixture.Grant(authority);
        fixture.LoseAndExpire(authority);
        return fixture.Acquire(authority);
    }

    private static WorldActorControlDependencies Capture(WorldSnapshot world, EffectiveControlFixture fixture, bool delegated = true) =>
        WorldActorControlDependencies.Capture(world, fixture.Owner,
            fixture.Delivery(delegated ? fixture.Grantee : fixture.Owner),
            delegated ? world.EffectiveControl!.Find(fixture.Owner)!.Witness : null, WorldControlCapabilities.Traversal);

    private static (long X, long Y) LegalOffset(WorldSnapshot world, EntityId actorId) =>
        new (long X, long Y)[] { (32, 0), (-32, 0), (0, 32), (0, -32) }.First(value =>
            world.Space.PrepareMove(actorId, world.Space.GetActor(actorId).Revision, value.X, value.Y).Status == SpatialMoveStatus.Prepared);
}
