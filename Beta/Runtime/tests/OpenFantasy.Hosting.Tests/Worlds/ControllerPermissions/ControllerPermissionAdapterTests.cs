using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.ControllerPermissions;

public sealed class ControllerPermissionAdapterTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public async Task Unbound_permission_commands_are_refused_before_queueing_without_stopping_the_player_host(
        bool permissionProfile, int operation)
    {
        var fixture = new PermissionStorageFixture();
        var genesis = permissionProfile ? fixture.Genesis : WorldCareDispositionAdmission.CreateFresh(fixture.Journey.Genesis);
        using var files = new StorageFiles();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, fixture.Owner, fixture.Delivery(fixture.Owner), time);
        session.SetReady(true);
        var before = authority.ReadStateForHost();
        ControlPermissionIntent intent = operation switch
        {
            0 => new OfferControlPermissionIntent(fixture.Grantee, 0, WorldControlCapabilities.CombatAndSurvival),
            1 => new AcceptControlPermissionIntent(fixture.Grantee, 1, fixture.Commands.NextId(), genesis.ContentHash),
            _ => new RevokeControlPermissionIntent(fixture.Grantee, 1)
        };
        var submitted = fixture.Commands.Command(genesis, fixture.Owner, intent);
        var decoded = WorldActionCodec.Decode(WorldActionCodec.Encode(submitted));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(decoded)
            .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        Assert.Same(before, authority.ReadStateForHost());
        Assert.Equal(0UL, authority.InspectForHost().Journal.CommandOutcomeCount);
        Assert.False(authority.IsIdentityOccupiedForHost(submitted.CommandId));
        Assert.False(session.Completion.IsCompleted);
        await SessionFixture.Tick(time);
        var after = authority.ReadStateForHost();
        Assert.Equal(before.Revision + 1, after.Revision);
        // The legacy adapter recognizes this calibration's immediate hostile entry; profile39 has no such binding.
        Assert.Equal(permissionProfile ? 1UL : 0UL, after.AdvancingSteps);
        if (!permissionProfile) Assert.Equal(EncounterLifecyclePhase.Confrontation, after.EncounterLifecycle!.Phase);
        Assert.False(session.Completion.IsCompleted);
        session.SetReady(false);
    }

    [Fact]
    public async Task A_paused_player_can_retrieve_an_exact_prior_permission_receipt_without_queuing_a_new_choice()
    {
        var fixture = new PermissionStorageFixture();
        using var files = new StorageFiles();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var command = fixture.Offer(fixture.Genesis);
        var committed = fixture.Commit(authority, command);
        var receipt = Assert.Single(committed.Receipts);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, fixture.Owner, fixture.Delivery(fixture.Owner), time);
        Assert.Equal(LocalWorldRunState.Paused, session.ReadRunState());
        var recovered = await session.SubmitAction(command).WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(recovered));
        await SessionFixture.Tick(time);
        Assert.Same(committed.Candidate, authority.ReadStateForHost());
        Assert.Equal(1UL, authority.InspectForHost().Journal.CommandOutcomeCount);
        Assert.False(session.Completion.IsCompleted);
    }
}
