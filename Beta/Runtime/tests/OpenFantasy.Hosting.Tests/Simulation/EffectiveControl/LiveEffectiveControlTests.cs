using System.Buffers.Binary;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Tests.Worlds.EffectiveControl;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.EffectiveControl;

public sealed class LiveEffectiveControlTests
{
    [Fact]
    public async Task Actual_establishment_and_measured_owner_expiry_are_required_and_acquisition_clears_unaccepted_input()
    {
        await using var fixture = await LiveEffectiveControlFixture.Open();
        var before = fixture.State;
        await using (var incomplete = await fixture.Host.ConnectNetworkPlayerAsync(fixture.Grantee,
            fixture.Source.Delivery(fixture.Grantee), fixture.Admission.Compatibility, TestContext.Current.CancellationToken))
        {
            Assert.Equal(WorldControllerMode.Human, fixture.State.Controllers!.Get(fixture.Grantee).Mode);
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => incomplete.AcquireControlAsync(fixture.Capture()));
            Assert.Same(before, fixture.State);
        }
        var owner = await fixture.Join(fixture.Owner);
        var grantee = await fixture.Join(fixture.Grantee);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => grantee.Channel.Player.AcquireControlAsync(fixture.Capture()));
        Assert.Same(before, fixture.State);
        await fixture.Lose(owner);
        var disconnected = fixture.State;
        var episode = disconnected.Controllers!.Get(fixture.Owner).DisconnectId;
        await fixture.Tick(29_950);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(49));
        Assert.Equal(WorldControllerMode.ReconnectAllowance, fixture.State.Controllers!.Get(fixture.Owner).Mode);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => grantee.Channel.Player.AcquireControlAsync(fixture.Capture()));
        await grantee.Channel.DisposeAsync();
        await fixture.Tick(1);
        Assert.Equal(WorldControllerMode.Autonomous, fixture.State.Controllers!.Get(fixture.Owner).Mode);
        Assert.Equal(episode, fixture.State.Controllers.Get(fixture.Owner).DisconnectId);
        AssertGameplayUnchanged(before, fixture.State);
        var validCapture = fixture.Capture();
        var eligible = fixture.State;
        Assert.True(WorldControllerBoundaries.CanRestoreHuman(eligible, fixture.Owner));
        await using (var provisional = await fixture.Host.ConnectNetworkPlayerAsync(fixture.Grantee,
            fixture.Source.Delivery(fixture.Grantee), fixture.Admission.Compatibility, TestContext.Current.CancellationToken))
        {
            Assert.Equal(WorldControllerMode.Human, fixture.State.Controllers!.Get(fixture.Grantee).Mode);
            // Exact accepted grant, expired episode and safe boundary are all present. Only final
            // authenticated establishment is missing; these same terms succeed below after admission.
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => provisional.AcquireControlAsync(validCapture));
            Assert.Same(eligible, fixture.State);
        }
        grantee = await fixture.Join(fixture.Grantee);
        Assert.Equal(validCapture, fixture.Capture());
        grantee.Channel.Player.SetReady(true);
        grantee.Channel.Player.SetMovement(32, 0);
        var pending = grantee.Channel.Player.SubmitAction(fixture.Source.Commands.Command(fixture.State, fixture.Grantee,
            new SpeakToActorIntent(fixture.Owner, fixture.State.Space.GetActor(fixture.Owner).Revision)));
        Assert.NotNull(grantee.Channel.Player.Pending);
        var expired = fixture.State;
        var captured = validCapture;
        var witness = await grantee.Channel.Player.AcquireControlAsync(captured);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => pending);
        Assert.Null(grantee.Channel.Player.Pending);
        Assert.False(grantee.Channel.Player.Ready);
        Assert.Equal(0L, grantee.Channel.Player.MovementX);
        Assert.Equal(0L, grantee.Channel.Player.MovementY);
        Assert.Equal(fixture.Grantee, witness.GranteeActorId);
        Assert.Equal(captured.DisconnectId, witness.DisconnectId);
        Assert.Equal(witness, fixture.State.EffectiveControl!.Find(fixture.Owner)!.Witness);
        Assert.Equal(expired.Revision + 1, fixture.State.Revision);
        AssertGameplayUnchanged(expired, fixture.State);
        Assert.False(fixture.Host.Completion.IsCompleted);
        var saved = fixture.State;
        await fixture.Stop();
        using var reopened = SqliteWorldAuthority.OpenExisting(fixture.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        var stopped = reopened.ReadStateForHost();
        Assert.Null(stopped.EffectiveControl!.Find(fixture.Owner)!.Witness);
        Assert.Equal(saved.Revision + 1, stopped.Revision);
        Assert.Equal(saved.Controllers!.ContentHash, stopped.Controllers!.ContentHash);
        Assert.Equal(saved.ControlPermissions!.ContentHash, stopped.ControlPermissions!.ContentHash);
        AssertGameplayUnchanged(saved, stopped);
        Assert.Equal(WorldSnapshotCodec.Encode(stopped), WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost(stopped.Revision).State));
    }

    [Fact]
    public async Task Captured_grant_episode_and_revision_must_match_and_stale_or_foreign_release_preserves_replacement()
    {
        await using var fixture = await LiveEffectiveControlFixture.Open();
        var owner = await fixture.Join(fixture.Owner);
        var grantee = await fixture.Join(fixture.Grantee);
        await fixture.Lose(owner);
        await fixture.Tick(30_000);
        var request = fixture.Capture();
        var unchanged = fixture.State;
        LocalControlAcquisition[] altered =
        [
            request with { GrantCommandId = fixture.Source.Commands.NextId() },
            request with { AcceptanceCommandId = fixture.Source.Commands.NextId() },
            request with { GrantRevision = request.GrantRevision + 1 },
            request with { TermsHash = ContentHash.Compute("different terms"u8) },
            request with { Capabilities = WorldControlCapabilities.Combat },
            request with { ExpectedAssignmentRevision = request.ExpectedAssignmentRevision + 1 },
            request with { DisconnectId = fixture.Source.Commands.NextId() },
            request with { OwnerControlRevision = request.OwnerControlRevision + 1 },
            request with { OwnerActorId = fixture.Grantee }
        ];
        foreach (var stale in altered)
        {
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => grantee.Channel.Player.AcquireControlAsync(stale));
            Assert.Same(unchanged, fixture.State);
            Assert.False(fixture.Host.Completion.IsCompleted);
        }
        var first = await grantee.Channel.Player.AcquireControlAsync(request);
        await grantee.Channel.Player.ReleaseControlAsync(first);
        var second = await grantee.Channel.Player.AcquireControlAsync(fixture.Capture());
        Assert.NotEqual(first.AssignmentId, second.AssignmentId);
        unchanged = fixture.State;
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => grantee.Channel.Player.ReleaseControlAsync(first));
        Assert.Same(unchanged, fixture.State);
        await using (var foreign = await fixture.Host.ConnectPlayerAsync(fixture.Owner, fixture.Source.Delivery(fixture.Owner)))
        {
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => foreign.ReleaseControlAsync(second));
            Assert.Same(unchanged, fixture.State);
        }
        Assert.Equal(second, fixture.State.EffectiveControl!.Find(fixture.Owner)!.Witness);
        await grantee.Channel.Player.ReleaseControlAsync(second);
        Assert.Null(fixture.State.EffectiveControl!.Find(fixture.Owner)!.Witness);
        AssertGameplayUnchanged(unchanged, fixture.State);
    }

    [Fact]
    public async Task Stale_port_disposal_and_transport_callback_cannot_release_a_replacement_ports_assignment()
    {
        await using var fixture = await LiveEffectiveControlFixture.Open();
        var (old, first) = await fixture.AcquireAfterRealLoss();
        await old.DisposeAsync();
        var current = await fixture.Join(fixture.Grantee);
        var replacement = await current.Channel.Player.AcquireControlAsync(fixture.Capture());
        Assert.NotEqual(first.AssignmentId, replacement.AssignmentId);
        var saved = fixture.State;
        await old.Player.ConfirmTransportDisconnectedAsync(WorldControllerDisconnectReason.TransportLost);
        await old.Player.DisposeAsync();
        Assert.Same(saved, fixture.State);
        Assert.Equal(replacement, fixture.State.EffectiveControl!.Find(fixture.Owner)!.Witness);
        Assert.Equal(WorldControllerMode.Human, fixture.State.Controllers!.Get(fixture.Grantee).Mode);
        Assert.Empty(fixture.Authority.ReadControllerAllowancesForHost());
        Assert.False(fixture.Host.Completion.IsCompleted);
    }

    [Fact]
    public async Task Revocation_clears_live_custody_before_regrant_and_a_new_episode_requires_fresh_capture()
    {
        await using var fixture = await LiveEffectiveControlFixture.Open();
        var (grantee, original) = await fixture.AcquireAfterRealLoss();
        var oldCapture = fixture.Capture();
        grantee.Player.SetReady(true);
        grantee.Player.SetMovement(32, 0);
        var revoked = fixture.Source.Revoke(fixture.Authority);
        Assert.Null(revoked.EffectiveControl!.Find(fixture.Owner)!.Witness);
        await fixture.Tick();
        Assert.Same(revoked, fixture.State);
        Assert.Null(grantee.Player.LiveControlWitness);
        Assert.False(grantee.Player.Ready);
        Assert.Equal(0L, grantee.Player.MovementX);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => grantee.Player.ReleaseControlAsync(original));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => grantee.Player.AcquireControlAsync(oldCapture));
        Assert.Same(revoked, fixture.State);

        var owner = await fixture.Join(fixture.Owner);
        fixture.Source.Grant(fixture.Authority, WorldControlCapabilities.All);
        await fixture.Lose(owner);
        await fixture.Tick(30_000);
        var ready = fixture.State;
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => grantee.Player.AcquireControlAsync(oldCapture));
        Assert.Same(ready, fixture.State);
        var replacement = await grantee.Player.AcquireControlAsync(fixture.Capture());
        Assert.NotEqual(original.AssignmentId, replacement.AssignmentId);
        Assert.NotEqual(original.DisconnectId, replacement.DisconnectId);
        Assert.True(replacement.GrantRevision > original.GrantRevision);
        Assert.True(replacement.AssignmentRevision > original.AssignmentRevision);
        Assert.Equal(replacement, grantee.Player.LiveControlWitness);
        var acquired = fixture.State;
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => grantee.Player.ReleaseControlAsync(original));
        Assert.Same(acquired, fixture.State);
        Assert.False(fixture.Host.Completion.IsCompleted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ordinary_dispose_and_actual_transport_loss_release_only_the_grantees_live_custody(bool transportLoss)
    {
        await using var fixture = await LiveEffectiveControlFixture.Open();
        var owner = await fixture.Join(fixture.Owner);
        var grantee = await fixture.Join(fixture.Grantee);
        await fixture.Lose(owner);
        await fixture.Tick(30_000);
        await grantee.Channel.Player.AcquireControlAsync(fixture.Capture());
        var before = fixture.State;
        var originalOwner = before.Controllers!.Get(fixture.Owner);
        if (transportLoss) await fixture.Lose(grantee);
        else await grantee.Channel.DisposeAsync();
        var after = fixture.State;
        Assert.Null(after.EffectiveControl!.Find(fixture.Owner)!.Witness);
        Assert.Equal(originalOwner, after.Controllers!.Get(fixture.Owner));
        Assert.Equal(transportLoss ? WorldControllerMode.ReconnectAllowance : WorldControllerMode.Human,
            after.Controllers.Get(fixture.Grantee).Mode);
        var rows = fixture.Authority.ReadControllerAllowancesForHost();
        if (transportLoss)
        {
            var row = Assert.Single(rows);
            Assert.Equal(fixture.Grantee, row.ActorId);
            Assert.NotEqual(originalOwner.DisconnectId, row.DisconnectId);
            Assert.Equal(0UL, row.ElapsedHostMilliseconds);
        }
        else Assert.Empty(rows);
        AssertGameplayUnchanged(before, after);
    }

    [Fact]
    public async Task Owner_rejoins_before_paid_work_is_safe_and_invalidates_possession_without_erasing_the_accepted_attack()
    {
        await using var fixture = await LiveEffectiveControlFixture.Open(combatApproach: true);
        var (grantee, witness) = await fixture.AcquireAfterRealLoss();
        var state = fixture.EnterEncounter();
        var automatic = new LocalAutonomousEncounterController();
        Assert.DoesNotContain(automatic.SelectChoices(state, [], fixture.Source.Commands.NextId), value => value.ActorId == fixture.Owner);
        await grantee.Player.ReleaseControlAsync(witness);
        Assert.Contains(automatic.SelectChoices(fixture.State, [], fixture.Source.Commands.NextId), value => value.ActorId == fixture.Owner);
        witness = await grantee.Player.AcquireControlAsync(fixture.Capture());
        // EnterEncounter is already complete; select the actual due owner's admitted Strike directly.
        state = fixture.State;
        var target = state.EncounterLifecycle!.GetScenario(fixture.Source.Journey.FirstScenario).TargetActorId;
        var ability = state.EncounterActions!.GetCombatant(fixture.Owner).AbilityIds.Select(state.EncounterActions.GetAbility)
            .Single(value => value.Key == OpenFantasy.Hosting.Content.EncounterFarmAdmission.StrikeKey);
        var attack = fixture.Source.Delegated(state, new QueueEncounterAbilityIntent(state.Pacing!.Encounter!.EncounterId,
            state.Pacing.Encounter.Revision, ability.DefinitionId, target));
        var originalMp = state.Progression!.GetActor(fixture.Owner).CurrentMp;
        var originalTargetHp = state.Progression.GetActor(target).CurrentHp;
        fixture.Source.Apply(fixture.Authority, WorldBatchKind.EncounterDecision, attack);
        Assert.False(WorldControllerBoundaries.CanRestoreHuman(fixture.State, fixture.Owner));
        var owner = await fixture.Join(fixture.Owner);
        Assert.Null(fixture.State.EffectiveControl!.Find(fixture.Owner)!.Witness);
        Assert.Equal(WorldControllerMode.Autonomous, fixture.State.Controllers!.Get(fixture.Owner).Mode);
        Assert.Contains(fixture.State.EncounterActions!.Pending, value => value.ContentHash == attack.ContentHash);
        Assert.False(owner.Channel.Player.Ready);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => grantee.Player.AcquireControlAsync(fixture.Capture()));
        for (int step = 0; fixture.State.EncounterActions!.LastResolution?.DecisionCommandId != attack.CommandId; step++)
        {
            Assert.True(step < 64);
            EffectiveControlCombatTests.ResolveOneWithOtherActorsWaiting(fixture.Authority, fixture.Source);
        }
        var resolved = fixture.State.EncounterActions!.LastResolution!;
        Assert.Equal(EncounterActionResolutionStatus.Damaged, resolved.Status);
        Assert.Equal(originalMp - (int)ability.ManaCost, fixture.State.Progression!.GetActor(fixture.Owner).CurrentMp);
        Assert.Equal(originalTargetHp - (int)resolved.Damage, fixture.State.Progression.GetActor(target).CurrentHp);
        Assert.True(resolved.Delay > 0);
        var safeButNotRestored = fixture.State;
        Assert.True(WorldControllerBoundaries.CanRestoreHuman(safeButNotRestored, fixture.Owner));
        Assert.Equal(WorldControllerMode.Autonomous, safeButNotRestored.Controllers!.Get(fixture.Owner).Mode);
        Assert.Equal(witness.DisconnectId, safeButNotRestored.Controllers.Get(fixture.Owner).DisconnectId);
        // Core eligibility and the old expired episode still hold. The established owner's actual
        // port, before the next restoration tick, is the reason this acquisition must refuse.
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => grantee.Player.AcquireControlAsync(fixture.Capture()));
        Assert.Same(safeButNotRestored, fixture.State);
        await fixture.Tick();
        Assert.Equal(WorldControllerMode.Human, fixture.State.Controllers!.Get(fixture.Owner).Mode);
        Assert.False(owner.Channel.Player.Ready);
        Assert.NotNull(fixture.Authority.FindReceipt(attack, fixture.Source.Delivery(fixture.Grantee)));
        Assert.Null(WorldActorCommandAuthority.AuthorizeSelection(fixture.State, attack));
    }

    [Fact]
    public async Task Startup_releases_saved_possession_without_offline_gameplay_or_loss_of_permission_and_paid_work()
    {
        await using var fixture = await LiveEffectiveControlFixture.Open(combatApproach: true);
        await fixture.AcquireAfterRealLoss();
        var accepted = fixture.QueueAttack();
        var saved = fixture.State;
        using var copy = new StorageFiles();
        fixture.Authority.BackupForHost(copy.Path);
        await fixture.Stop();
        var authority = SqliteWorldAuthority.OpenExisting(copy.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(saved.ContentHash, authority.ReadStateForHost().ContentHash);
        var time = new ManualSessionTimeProvider();
        time.Advance(TimeSpan.FromDays(10));
        await using var host = new LocalWorldSession(authority, fixture.Owner, fixture.Source.Delivery(fixture.Owner), time);
        await host.Player.DisposeAsync();
        var released = authority.ReadStateForHost();
        Assert.Null(released.EffectiveControl!.Find(fixture.Owner)!.Witness);
        Assert.Equal(saved.Revision + 1, released.Revision);
        Assert.Equal(saved.ControlPermissions!.ContentHash, released.ControlPermissions!.ContentHash);
        Assert.Equal(saved.Controllers!.Get(fixture.Owner), released.Controllers!.Get(fixture.Owner));
        Assert.Contains(released.EncounterActions!.Pending, value => value.ContentHash == accepted.Command.ContentHash);
        Assert.Equal(accepted.Mp, released.Progression!.GetActor(fixture.Owner).CurrentMp);
        AssertGameplayUnchanged(saved, released);
        await Controllers.LiveControllerHostTests.Tick(time, TimeSpan.FromDays(10));
        Assert.Equal(released.ContentHash, authority.ReadStateForHost().ContentHash);
        await host.StopAsync();
        using var reopened = SqliteWorldAuthority.OpenExisting(copy.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(released), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.NotNull(reopened.FindReceipt(accepted.Command, fixture.Source.Delivery(fixture.Grantee)));
    }

    private static void AssertGameplayUnchanged(WorldSnapshot before, WorldSnapshot after)
    {
        var a = Fields(WorldSnapshotCodec.Encode(before));
        var b = Fields(WorldSnapshotCodec.Encode(after));
        Assert.Equal(31, a.Length);
        Assert.Equal(a.Length, b.Length);
        for (int i = 0; i < a.Length; i++)
            if (i is not 8 and not 21 and not 26 and not 30) Assert.Equal(a[i], b[i]);
        var oldLifecycle = Fields(a[21]);
        var newLifecycle = Fields(b[21]);
        Assert.Equal(17, oldLifecycle.Length);
        Assert.Equal(oldLifecycle.Length, newLifecycle.Length);
        for (int i = 0; i < oldLifecycle.Length; i++)
            if (i != 5) Assert.Equal(oldLifecycle[i], newLifecycle[i]);
        Assert.Equal(before.Calendar, after.Calendar);
        Assert.Equal(before.AdvancingSteps, after.AdvancingSteps);
    }

    private static byte[][] Fields(byte[] bytes)
    {
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8))), offset = 12;
        var fields = new byte[count][];
        for (int i = 0; i < count; i++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
            offset += 4;
            fields[i] = bytes.AsSpan(offset, length).ToArray();
            offset += length;
        }
        Assert.Equal(bytes.Length, offset);
        return fields;
    }
}
