using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Care.WorldResourceJourneyTests;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class OrdinaryCareWorldTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Source_stat_injury_then_raw_self_or_real_crafted_aid_commit_one_dose_time_and_durable_history(bool self)
    {
        var fixture = new OrdinaryCareWorldFixture();
        using var files = new StorageFiles();
        WorldSnapshot before, after, revoked;
        WorldActionCommand command;
        WorldActionReceipt receipt;
        WorldStepBatch batch;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            var session = new CareWorldSession(authority);
            fixture.ReachInjuredExploration(session);
            before = fixture.Supply(session, self);
            EntityId actor = self ? fixture.Player : fixture.Helper;
            command = fixture.Journey.Command(before, actor, fixture.Intent(before, self));
            var control = before.Inventory.Controls.Single(value => value.ActorId == actor);
            var basis = fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep, [command]);
            batch = new(basis.CauseId, basis.WorldId, basis.BranchId, basis.ExpectedRevision, basis.ExpectedAdvancingSteps,
                basis.Kind, [new(new(control.PrincipalId, control.SessionId), actor, 32, 0)], basis.Actions, basis.ProfileVersion);
            var decision = CareRewardJourney.Commit(session, batch);
            receipt = Assert.Single(decision.Receipts);
            after = decision.Candidate;
            var use = Assert.Single(after.Care!.Ordinary!.Uses);
            Assert.Equal(command.CommandId, use.CommandId);
            Assert.Equal(actor, use.ActorId);
            Assert.Equal(fixture.Player, use.RecipientActorId);
            Assert.Equal(before.Calendar.AddMilliseconds(1800), after.Calendar);
            Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
            Assert.Equal(before.Space.GetActor(actor).Feet, after.Space.GetActor(actor).Feet);
            Assert.Equal(before.Progression!.GetActor(fixture.Player).CurrentMp, after.Progression!.GetActor(fixture.Player).CurrentMp);
            Assert.Equivalent(before.Progression.Currency, after.Progression.Currency, strict: true);
            Assert.Equal(Math.Min(60, before.Progression.GetActor(fixture.Player).CurrentHp + 40), after.Progression.GetActor(fixture.Player).CurrentHp);
            Assert.Equal(before.Care!.Acknowledgments.Count, after.Care.Acknowledgments.Count);
            Assert.Equal(before.Care.Treatments.Count, after.Care.Treatments.Count);
            Assert.Equal(before.Production!.ConsumedItems.Count + 1, after.Production!.ConsumedItems.Count);
            var consumed = after.Production.ConsumedItems.Single(value => value.ConsumptionId == use.ConsumptionId);
            Assert.Equal(self ? IngredientLotKind.Source : IngredientLotKind.Transformed, consumed.Ingredient.Kind);
            Assert.Equal(1UL, consumed.Ingredient.Quantity);
            Assert.IsType<WorldRestorativeUseResourceEvidence>(after.ResourceHistory!.Evidence[^1]);
            Assert.Equal(WorldActionCodec.Encode(command), WorldActionCodec.Encode(WorldActionCodec.Decode(WorldActionCodec.Encode(command))));
            Assert.Equal(WorldStepCodec.Encode(batch), WorldStepCodec.Encode(WorldStepCodec.Decode(WorldStepCodec.Encode(batch))));
            RoundTrip(after);
            Assert.Equal(before.ContentHash, authority.ReadRecordedStateForHost(before.Revision).StateHash);
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(authority.FindReceipt(command, fixture.Journey.Delivery(command)))));
            Assert.ThrowsAny<Exception>(() => authority.Commit(batch));
            Assert.Equal(after.ContentHash, authority.ReadStateForHost().ContentHash);
            var permission = after.Care.Ordinary.Permissions.Single();
            var revoke = fixture.Journey.Command(after, fixture.Player,
                new RevokeCarePermissionIntent(permission.PermissionId, permission.Revision, permission.GrantVersion));
            revoked = CareRewardJourney.Commit(session, fixture.Journey.Batch(after, WorldBatchKind.AdvancingStep, [revoke])).Candidate;
            Assert.False(revoked.Care!.Ordinary!.Permissions.Single().IsActive);
            Assert.Equal(after.Care.Ordinary.Uses, revoked.Care.Ordinary.Uses);
            Assert.Equal(after.Progression.ContentHash, revoked.Progression!.ContentHash);
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(revoked), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Equal(WorldSnapshotCodec.Encode(after), WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost(after.Revision).State));
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(reopened.FindReceipt(command, fixture.Journey.Delivery(command)))));
    }

    [Fact]
    public void Profile25_and_earlier_do_not_admit_ordinary_commands_or_reinterpret_their_bytes()
    {
        var source = CareJourneyAdmission.CreateEmbedded(EncounterJourneyWorldFixture.SeedText, EncounterJourneyWorldFixture.PlayerName);
        // Pinned from the committed production genesis in care-continuation/pending-source-02,
        // before ordinary-care interpretation and the separate fresh supply split existed.
        Assert.Equal("1844f97105a8144c3076c8edf7dbe1d12917dfc6e90410511385ad1d33001634", source.ContentHash.Value);
        byte[] encoded = WorldSnapshotCodec.Encode(source);
        Assert.Equal(encoded, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(encoded)));
        Assert.False(source.Care!.SupportsOrdinary);
        var fixture = new OrdinaryCareWorldFixture();
        var permission = fixture.Genesis.Care!.Ordinary!.Permissions.Single();
        var command = fixture.Journey.Command(fixture.Genesis, fixture.Player,
            new RevokeCarePermissionIntent(permission.PermissionId, 0, permission.GrantVersion));
        Assert.Throws<ArgumentException>(() => new WorldStepBatch(new(0xaca260, 1), source.WorldId, source.BranchId,
            0, 0, WorldBatchKind.AdvancingStep, [], [command], 25));
        Assert.Throws<ArgumentException>(() => CareCodecV1.Encode(fixture.Genesis.Care));
        Assert.Throws<ArgumentException>(() => WorldSnapshotCodecV25.Encode(fixture.Genesis));
    }
}
