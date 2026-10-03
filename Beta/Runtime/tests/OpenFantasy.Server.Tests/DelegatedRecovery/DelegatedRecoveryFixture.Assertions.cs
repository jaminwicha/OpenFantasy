using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests.DelegatedRecovery;

public sealed partial class DelegatedRecoveryFixture
{
    internal void AssertOriginalOwnership(WorldSnapshot state)
    {
        Assert.Equal(Genesis.WorldId, state.WorldId);
        Assert.Equal(Genesis.BranchId, state.BranchId);
        Assert.Equal(Genesis.Inventory.Controls, state.Inventory.Controls);
        Assert.Equal(Genesis.Residents.Select(row => (row.ActorId, row.ControllerKind, row.InventoryCustodianId, row.HomeFeet)),
            state.Residents.Select(row => (row.ActorId, row.ControllerKind, row.InventoryCustodianId, row.HomeFeet)));
        Assert.Equal(Genesis.Space.Actors.Select(row => (row.ActorId, row.Binding, row.HomePlaceId)),
            state.Space.Actors.Select(row => (row.ActorId, row.Binding, row.HomePlaceId)));
        Assert.False(state.Inventory.CanControl(Delivery(Grantee), Owner));
        Assert.True(state.Inventory.CanControl(Delivery(Owner), Owner));
        Assert.Equal(Genesis.Inventory.ContentHash, state.Inventory.ContentHash);
        AssertProductionConservation(state);
        Assert.Equal(Genesis.Progression!.Currency.AdmittedSupply, state.Progression!.Currency.AdmittedSupply);
        Assert.Equal(Genesis.Progression.Currency.Accounts.OrderBy(row => row.AccountId)
                .Select(row => (row.AccountId, row.Kind, row.ActorId, row.Balance, row.Revision)),
            state.Progression.Currency.Accounts.OrderBy(row => row.AccountId)
                .Select(row => (row.AccountId, row.Kind, row.ActorId, row.Balance, row.Revision)));
        Assert.Equal(Genesis.Progression.Currency.Revision, state.Progression.Currency.Revision);
        Assert.Equal(Genesis.Progression.Actors.Select(row => (row.ActorId, row.CurrentMp)),
            state.Progression.Actors.Select(row => (row.ActorId, row.CurrentMp)));
        AssertCareConservation(state);
    }

    private void AssertProductionConservation(WorldSnapshot state)
    {
        var before = Genesis.Production!;
        var after = state.Production!;
        // A coordinated cut changes the continuation envelope, while these complete live rights and
        // cumulative operation counts stay exact. Named boundary comparisons authenticate that envelope.
        Assert.Equal((before.WorldId, before.BranchId, before.Revision, before.SupportsConsumption),
            (after.WorldId, after.BranchId, after.Revision, after.SupportsConsumption));
        Assert.Equal(before.Recipes.Select(row => (row.DefinitionId, row.Version)),
            after.Recipes.Select(row => (row.DefinitionId, row.Version)));
        Assert.Equal(before.Lots.Select(row => (row.LotId, row.CustodianId, row.Definition, row.Output,
                row.Revision, row.Protection, row.ReservedBy)),
            after.Lots.Select(row => (row.LotId, row.CustodianId, row.Definition, row.Output,
                row.Revision, row.Protection, row.ReservedBy)));
        Assert.Equal((0UL, 0UL, 0UL),
            (before.TotalBatchCount, before.TotalConsumedItemCount, before.TotalMaterialOperationCount));
        Assert.Equal((before.TotalBatchCount, before.TotalConsumedItemCount, before.TotalMaterialOperationCount),
            (after.TotalBatchCount, after.TotalConsumedItemCount, after.TotalMaterialOperationCount));
        Assert.Empty(after.Batches);
        Assert.Empty(after.ConsumedItems);
    }

    private void AssertCareConservation(WorldSnapshot state)
    {
        var before = Genesis.Care!;
        var after = state.Care!;
        Assert.Equal((before.WorldId, before.BranchId, before.Revision, before.SupportsOrdinary, before.SupportsDispositions),
            (after.WorldId, after.BranchId, after.Revision, after.SupportsOrdinary, after.SupportsDispositions));
        Assert.Equal(before.Remedies.Select(row => (row.DefinitionId, row.Version)),
            after.Remedies.Select(row => (row.DefinitionId, row.Version)));
        Assert.Equal(before.Agreements.Select(row => (row.AgreementId, row.Version)),
            after.Agreements.Select(row => (row.AgreementId, row.Version)));
        Assert.Equal((0UL, 0UL, 0UL, 0UL),
            (before.TotalAcknowledgments, before.TotalTreatments, before.TotalDispositions, before.TotalOrdinaryUses));
        Assert.Equal((before.TotalAcknowledgments, before.TotalTreatments, before.TotalDispositions, before.TotalOrdinaryUses),
            (after.TotalAcknowledgments, after.TotalTreatments, after.TotalDispositions, after.TotalOrdinaryUses));
        Assert.Null(after.Pending);
        Assert.Empty(after.Acknowledgments);
        Assert.Empty(after.Treatments);
        Assert.Empty(after.Dispositions);
        Assert.Equal(before.Ordinary!.Revision, after.Ordinary!.Revision);
        Assert.Equal(before.Ordinary.Definitions.Select(row => (row.DefinitionId, row.Version)),
            after.Ordinary.Definitions.Select(row => (row.DefinitionId, row.Version)));
        Assert.Equal(before.Ordinary.Permissions.Select(row => (row.PermissionId, row.Version)),
            after.Ordinary.Permissions.Select(row => (row.PermissionId, row.Version)));
        Assert.Empty(after.Ordinary.Uses);
    }

    internal void AssertPaidReceipt(SqliteWorldAuthority authority)
    {
        var actual = Assert.IsType<WorldActionReceipt>(authority.FindReceipt(PaidCommand, Delivery(Grantee)));
        Assert.Equal(WorldReceiptCodecV1.Encode(PaidReceipt), WorldReceiptCodecV1.Encode(actual));
        Assert.Equal(Owner, PaidCommand.ActorId);
        Assert.Equal(Delivery(Grantee).PrincipalId, PaidCommand.PrincipalId);
        Assert.Equal(Delivery(Grantee).SessionId, PaidCommand.OriginSessionId);
        Assert.Equal(Witness, PaidCommand.ControlWitness);
        Assert.True(authority.IsIdentityOccupiedForHost(PaidCommand.CommandId));
        Assert.True(authority.IsIdentityOccupiedForHost(PaidReceipt.ResultId));
        Assert.Throws<AuthorityDispatchException>(() => authority.FindReceipt(PaidCommand, Delivery(Owner)));
        foreach (var command in new[] { GiftCommand, GiftWithdrawal })
        {
            Assert.NotNull(authority.FindReceipt(command, Delivery(Grantee)));
            Assert.True(authority.IsIdentityOccupiedForHost(command.CommandId));
        }
    }

    internal void AssertPureRecovery(DelegatedRecoveryBoundary boundary, WorldSnapshot actual, bool afterPublication)
    {
        var expected = afterPublication ? boundary.After : boundary.Before;
        Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(actual));
        AssertOriginalOwnership(actual);
        Assert.Equal(expected.Controllers!.Get(Owner), actual.Controllers!.Get(Owner));
        Assert.Equal(expected.ControlPermissions!.ContentHash, actual.ControlPermissions!.ContentHash);
        Assert.Equal(expected.EffectiveControl!.ContentHash, actual.EffectiveControl!.ContentHash);
        if (actual.Retreat!.Active is { } active)
        {
            Assert.Equal(WorldActionCodec.Encode(PaidCommand), WorldActionCodec.Encode(active.Command));
            Assert.Equal(Witness, active.Command.ControlWitness);
            Assert.Equal(expected.Retreat!.Active!.ReadinessCharged, active.ReadinessCharged);
            Assert.Equal(expected.Retreat.Active.SamplesTaken, active.SamplesTaken);
        }
    }

    internal void AssertPaidCompletion(WorldSnapshot state)
    {
        var intent = Assert.IsType<QueueEncounterRetreatIntent>(PaidCommand.Intent);
        var outcome = Assert.IsType<EncounterRetreatOutcome>(state.Retreat!.Last);
        Assert.Null(state.Retreat.Active);
        Assert.Equal(EncounterRetreatStatus.Disengaged, outcome.Status);
        Assert.Equal(EncounterRetreatReason.None, outcome.Reason);
        Assert.Equal(PaidCommand.CommandId, outcome.CommandId);
        Assert.Equal(intent.Route.Count, outcome.SamplesTaken);
        Assert.Equal(intent.Destination, state.Space.GetActor(Owner).Feet);
        var before = Boundaries.Single(row => row.Name == "archive").Before;
        ulong sampleDelay = EncounterRetreatRules.SampleDelay(before.Pacing!.Encounter!.GetActor(Owner).NormalizedSpeed);
        Assert.Equal(checked((ulong)intent.Route.Count * sampleDelay), outcome.ReadinessCharged);
        Assert.Equal(sampleDelay, state.EncounterLifecycle!.GetDebt(Owner).RemainingDelay);
        Assert.Equal(EncounterLifecyclePhase.Exploration, state.EncounterLifecycle.Phase);
        Assert.InRange(state.Progression!.GetActor(Owner).CurrentHp, 1, Genesis.Progression!.GetActor(Owner).CurrentHp - 1);
        AssertDamageEvidence(state);
    }

    internal void AssertDamageEvidence(WorldSnapshot state)
    {
        var closing = Boundaries.Single(row => row.Name == "archive").Before;
        Assert.Equal(closing.Revision, state.Coordination!.Anchor!.Reference.BoundarySequence);
        var batches = closing.ResourceHistory!.History.Batches.Concat(state.ResourceHistory!.History.Batches).ToArray();
        Assert.Equal(batches.Length, batches.Select(batch => batch.CommandId).Distinct().Count());
        var changes = batches.SelectMany(batch => batch.Changes).ToArray();
        Assert.All(changes, change =>
        {
            Assert.Equal(ActorResourceOperation.Damage, change.Operation);
            Assert.Equal(Owner, change.Before.ActorId);
            Assert.True(change.Before.CurrentHp > change.After.CurrentHp);
        });
        long damage = changes.Sum(change => (long)change.Before.CurrentHp - change.After.CurrentHp);
        Assert.Equal(Genesis.Progression!.GetActor(Owner).CurrentHp - damage, state.Progression!.GetActor(Owner).CurrentHp);
        Assert.Equal(Genesis.Progression.Actors.Where(actor => actor.ActorId != Owner)
                .Select(actor => (actor.ActorId, actor.CurrentHp, actor.CurrentMp)),
            state.Progression.Actors.Where(actor => actor.ActorId != Owner)
                .Select(actor => (actor.ActorId, actor.CurrentHp, actor.CurrentMp)));
    }
}
