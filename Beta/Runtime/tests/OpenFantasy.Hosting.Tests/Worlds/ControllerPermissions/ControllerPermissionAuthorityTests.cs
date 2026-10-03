using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.ControllerPermissions;

public sealed class ControllerPermissionAuthorityTests
{
    [Fact]
    public void Four_real_home_actors_can_accept_all_twelve_pairs_without_transferring_control()
    {
        var basis = WorldControllerAdmission.CreateFresh(CargoOutcomeWorldAdmission.CreateEmbedded(
            "permission-four-player", ["Traveler", "Mira", "Joss", "Lark"]));
        var living = basis.Care!.Ordinary!.Definitions.First(value => value.DeliveryMode == RestorativeDeliveryMode.LivingAssistance);
        var recovery = RestorativeUseDefinition.CreateIncapacitatedAssistance(new(0xc039aaaa00000000, 1),
            living.RemedyDefinitionId, living.RemedyVersion, living.SupportedForms);
        var genesis = WorldControlPermissionAdmission.CreateFresh(WorldCareDispositionAdmission.CreateFresh(
            WorldRecoveryAdmission.CreateFresh(basis, [recovery], [])));
        var commands = new EncounterJourneyWorldFixture();
        using var files = new StorageFiles();
        WorldSnapshot final;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            var actors = genesis.Controllers!.Assignments.Select(row => row.ActorId).ToArray();
            Assert.Equal(4, actors.Length);
            foreach (var owner in actors)
            {
                foreach (var grantee in actors.Where(actor => actor != owner))
                {
                    var state = authority.ReadStateForHost();
                    var offer = commands.Command(state, owner,
                        new OfferControlPermissionIntent(grantee, 0, WorldControlCapabilities.All));
                    var offered = authority.Commit(commands.Batch(state, WorldBatchKind.ControlPermission, [offer]));
                    Assert.Equal(WorldActionRejection.None, Assert.Single(offered.Receipts).Rejection);
                    var slot = offered.Candidate.ControlPermissions!.Find(owner, grantee)!;
                    var accept = commands.Command(offered.Candidate, grantee,
                        new AcceptControlPermissionIntent(owner, slot.Revision, offer.CommandId, slot.TermsHash!));
                    var accepted = authority.Commit(commands.Batch(offered.Candidate, WorldBatchKind.ControlPermission, [accept]));
                    Assert.Equal(WorldActionRejection.None, Assert.Single(accepted.Receipts).Rejection);
                }
            }
            final = authority.ReadStateForHost();
            Assert.Equal(WorldControlPermissionState.MaximumSlots, final.ControlPermissions!.Slots.Count);
            Assert.Equal(24UL, final.ControlPermissions.Revision);
            Assert.Equal(24UL, final.Revision);
            Assert.All(final.ControlPermissions.Slots, slot => Assert.Equal(WorldControlPermissionStatus.Accepted, slot.Status));
            foreach (var binding in final.Inventory.Controls.Where(binding => actors.Contains(binding.ActorId)))
            {
                var delivery = new TrustedCommandDelivery(binding.PrincipalId, binding.SessionId);
                foreach (var actor in actors)
                    Assert.Equal(actor == binding.ActorId, final.Inventory.CanControl(delivery, actor));
            }
            PermissionStorageFixture.AssertGameplayUnchanged(genesis, final);
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Equal(24UL, reopened.InspectForHost().Journal.CommandOutcomeCount);
        Assert.Empty(reopened.ReadControllerAllowancesForHost());
    }

    [Fact]
    public void Absent_players_cannot_offer_or_accept_but_the_owner_can_revoke_without_earning_time()
    {
        var fixture = new PermissionStorageFixture();
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var offered = fixture.Apply(authority, fixture.Offer(fixture.Genesis));
        var originalTerms = offered.ControlPermissions!.ContentHash;
        var absent = fixture.Event(authority, fixture.Grantee, WorldControllerEventKind.Disconnected);
        var deniedAccept = fixture.Commit(authority, fixture.Accept(absent));
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(deniedAccept.Receipts).Rejection);
        Assert.Equal(originalTerms, deniedAccept.Candidate.ControlPermissions!.ContentHash);
        var present = fixture.Event(authority, fixture.Grantee, WorldControllerEventKind.Reconnected);
        var accepted = fixture.Apply(authority, fixture.Accept(present));
        var acceptedTerms = accepted.ControlPermissions!.ContentHash;
        absent = fixture.Event(authority, fixture.Owner, WorldControllerEventKind.Disconnected);
        var deniedOffer = fixture.Commit(authority, fixture.Offer(absent, 2));
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(deniedOffer.Receipts).Rejection);
        Assert.Equal(acceptedTerms, deniedOffer.Candidate.ControlPermissions!.ContentHash);
        var allowance = Assert.Single(authority.ReadControllerAllowancesForHost());
        var revoked = fixture.Apply(authority, fixture.Revoke(deniedOffer.Candidate));
        Assert.Equal(WorldControlPermissionStatus.Revoked, Assert.Single(revoked.ControlPermissions!.Slots).Status);
        Assert.Equal(allowance, Assert.Single(authority.ReadControllerAllowancesForHost()));
        Assert.Equal(WorldControllerMode.ReconnectAllowance, revoked.Controllers!.Get(fixture.Owner).Mode);
        Assert.Equal(fixture.Genesis.Calendar, revoked.Calendar);
        Assert.Equal(fixture.Genesis.Inventory.ContentHash, revoked.Inventory.ContentHash);
    }

    [Fact]
    public void Accepted_scopes_cannot_be_used_to_impersonate_the_owner_or_delegate_onward()
    {
        var fixture = new PermissionStorageFixture();
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var offered = fixture.Apply(authority, fixture.Offer(fixture.Genesis, capabilities: WorldControlCapabilities.All));
        var accepted = fixture.Apply(authority, fixture.Accept(offered));
        var delivery = fixture.Delivery(fixture.Grantee);
        var forged = new WorldActionCommand(fixture.Commands.NextId(), delivery.PrincipalId, delivery.SessionId,
            fixture.Owner, accepted.WorldId, accepted.BranchId, accepted.Space.GetActor(fixture.Owner).Revision,
            new RevokeControlPermissionIntent(fixture.Grantee, 2));
        var forgedBatch = fixture.Commands.Batch(accepted, WorldBatchKind.ControlPermission, [forged]);
        var refused = new WorldStepProcessor().Decide(accepted, forgedBatch);
        Assert.Equal(WorldActionRejection.NotAuthorized, Assert.Single(refused.Receipts).Rejection);
        Assert.Equal(accepted.ControlPermissions!.ContentHash, refused.Candidate.ControlPermissions!.ContentHash);
        Assert.Throws<AuthorityDispatchException>(() => authority.Commit(forgedBatch));
        Assert.Same(accepted, authority.ReadStateForHost());
        var onward = new WorldActionCommand(fixture.Commands.NextId(), delivery.PrincipalId, delivery.SessionId,
            fixture.Owner, accepted.WorldId, accepted.BranchId, accepted.Space.GetActor(fixture.Owner).Revision,
            new OfferControlPermissionIntent(fixture.Grantee, 2, WorldControlCapabilities.All));
        var onwardBatch = fixture.Commands.Batch(accepted, WorldBatchKind.ControlPermission, [onward]);
        var rejected = new WorldStepProcessor().Decide(accepted, onwardBatch);
        Assert.Equal(WorldActionRejection.NotAuthorized, Assert.Single(rejected.Receipts).Rejection);
        Assert.Equal(accepted.ControlPermissions.ContentHash, rejected.Candidate.ControlPermissions!.ContentHash);
        Assert.Throws<AuthorityDispatchException>(() => authority.Commit(onwardBatch));
        Assert.Same(accepted, authority.ReadStateForHost());
        Assert.Equal(2UL, authority.InspectForHost().Journal.CommandOutcomeCount);
        Assert.False(authority.IsIdentityOccupiedForHost(forged.CommandId));
        Assert.False(authority.IsIdentityOccupiedForHost(onward.CommandId));
    }
}
