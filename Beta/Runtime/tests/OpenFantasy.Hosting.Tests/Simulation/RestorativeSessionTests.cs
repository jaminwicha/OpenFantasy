using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class RestorativeSessionTests
{
    [Fact]
    public async Task Actual_stocked_helper_waits_through_pause_then_commits_one_owned_dose_at_a_ready_cutoff_and_reopens()
    {
        var fixture = new OrdinaryCareWorldFixture();
        using var files = new StorageFiles();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var journey = new CareWorldSession(authority);
        fixture.ReachFullDoseInjuredExploration(journey);
        var before = fixture.Supply(journey, self: false);
        var helper = Assert.Single(new LocalRestorativeController().SelectActions(before,
            authority.IsIdentityOccupiedForHost, new HashSet<OpenFantasy.Core.EntityId>()));
        var patientControl = before.Inventory.Controls.Single(value => value.ActorId == fixture.Player);
        var time = new ManualSessionTimeProvider();
        WorldSnapshot after;
        WorldActionReceipt receipt;
        ulong priorReceipts = authority.InspectForHost().Journal.CommandOutcomeCount;
        await using (var session = new LocalWorldSession(authority, fixture.Player,
            new(patientControl.PrincipalId, patientControl.SessionId), time))
        {
            await Tick(time, TimeSpan.FromHours(1));
            Assert.Equal(before.ContentHash, authority.ReadStateForHost().ContentHash);
            Assert.Equal(LocalWorldRunState.Paused, session.ReadRunState());
            Assert.Null(authority.FindReceipt(helper, fixture.Journey.Delivery(helper)));
            session.SetReady(true);
            await Tick(time);
            await Until(() => authority.ReadStateForHost().Care!.Ordinary!.Uses.Count == 1);
            session.SetReady(false);
            after = authority.ReadStateForHost();
            var use = Assert.Single(after.Care!.Ordinary!.Uses);
            Assert.Equal(helper.CommandId, use.CommandId);
            Assert.Equal(fixture.Helper, use.ActorId);
            Assert.Equal(fixture.Player, use.RecipientActorId);
            Assert.Equal(before.Revision + 1, after.Revision);
            Assert.Equal(priorReceipts + 1, authority.InspectForHost().Journal.CommandOutcomeCount);
            Assert.Equal(before.Calendar.AddMilliseconds(1_800), after.Calendar);
            Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
            Assert.Equal(before.Progression!.GetActor(fixture.Player).CurrentHp + 40,
                after.Progression!.GetActor(fixture.Player).CurrentHp);
            Assert.Equal(before.Production!.ConsumedItems.Count + 1, after.Production!.ConsumedItems.Count);
            Assert.Equal(IngredientLotKind.Transformed,
                after.Production.ConsumedItems.Single(value => value.ConsumptionId == use.ConsumptionId).Ingredient.Kind);
            Assert.Equal(before.Care!.Acknowledgments.Count, after.Care.Acknowledgments.Count);
            Assert.Equal(before.Care.Treatments.Count, after.Care.Treatments.Count);
            Assert.Null(after.Care.Pending);
            receipt = Assert.IsType<WorldActionReceipt>(authority.FindReceipt(helper, fixture.Journey.Delivery(helper)));
            Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
            await Tick(time, TimeSpan.FromHours(1));
            Assert.Equal(after.ContentHash, authority.ReadStateForHost().ContentHash);
            Assert.False(session.Completion.IsCompleted);
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(after), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(
            reopened.FindReceipt(helper, fixture.Journey.Delivery(helper)))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Queued_patient_self_use_or_revoke_wins_the_same_cutoff_over_an_otherwise_ready_helper(bool revoke)
    {
        var fixture = new OrdinaryCareWorldFixture();
        using var files = new StorageFiles();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var journey = new CareWorldSession(authority);
        fixture.ReachFullDoseInjuredExploration(journey);
        var before = fixture.Supply(journey, self: false);
        var helper = Assert.Single(new LocalRestorativeController().SelectActions(before,
            authority.IsIdentityOccupiedForHost, new HashSet<OpenFantasy.Core.EntityId>()));
        var helperIntent = Assert.IsType<UseRestorativeItemIntent>(helper.Intent);
        var permission = before.Care!.Ordinary!.Permissions.Single();
        WorldActionIntent intent = revoke
            ? new RevokeCarePermissionIntent(permission.PermissionId, permission.Revision, permission.GrantVersion)
            : fixture.Intent(before, self: true);
        var command = fixture.Journey.Command(before, fixture.Player, intent);
        var time = new ManualSessionTimeProvider();
        WorldSnapshot after;
        WorldActionReceipt receipt;
        ulong priorReceipts = authority.InspectForHost().Journal.CommandOutcomeCount;
        await using (var session = new LocalWorldSession(authority, fixture.Player, fixture.Journey.Delivery(command), time))
        {
            session.SetReady(true);
            var submitted = session.SubmitAction(command);
            await Tick(time);
            await Until(() => submitted.IsCompleted);
            receipt = await submitted;
            Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
            session.SetReady(false);
            after = authority.ReadStateForHost();
            Assert.Equal(before.Revision + 1, after.Revision);
            Assert.Equal(priorReceipts + 1, authority.InspectForHost().Journal.CommandOutcomeCount);
            Assert.Null(authority.FindReceipt(helper, fixture.Journey.Delivery(helper)));
            Assert.False(authority.IsIdentityOccupiedForHost(helper.CommandId));
            var unspent = after.Production!.Lots.Single(value => value.LotId == helperIntent.Ingredient.LotId);
            Assert.Equal(helperIntent.Ingredient.ExpectedRevision, unspent.Revision);
            Assert.Equal(1UL, unspent.Quantity);
            Assert.Equal(before.Calendar.AddMilliseconds(1_800), after.Calendar);
            Assert.Equal(before.Care.Acknowledgments.Count, after.Care!.Acknowledgments.Count);
            Assert.Equal(before.Care.Treatments.Count, after.Care.Treatments.Count);
            Assert.Null(after.Care.Pending);
            if (revoke)
            {
                Assert.Empty(after.Care.Ordinary!.Uses);
                Assert.False(after.Care.Ordinary.Permissions.Single().IsActive);
                Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);
                Assert.Equal(before.Production!.ContentHash, after.Production.ContentHash);
                Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
            }
            else
            {
                var use = Assert.Single(after.Care.Ordinary!.Uses);
                Assert.Equal(command.CommandId, use.CommandId);
                Assert.Equal(fixture.Player, use.ActorId);
                Assert.Null(use.PermissionId);
                Assert.True(after.Care.Ordinary.Permissions.Single().IsActive);
                Assert.Equal(before.Production!.ConsumedItems.Count + 1, after.Production.ConsumedItems.Count);
                Assert.Equal(IngredientLotKind.Source,
                    after.Production.ConsumedItems.Single(value => value.ConsumptionId == use.ConsumptionId).Ingredient.Kind);
            }
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(await session.SubmitAction(command)));
            await Tick(time, TimeSpan.FromHours(1));
            Assert.Equal(after.ContentHash, authority.ReadStateForHost().ContentHash);
            Assert.False(session.Completion.IsCompleted);
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(after), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Null(reopened.FindReceipt(helper, fixture.Journey.Delivery(helper)));
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(
            reopened.FindReceipt(command, fixture.Journey.Delivery(command)))));
    }
}
