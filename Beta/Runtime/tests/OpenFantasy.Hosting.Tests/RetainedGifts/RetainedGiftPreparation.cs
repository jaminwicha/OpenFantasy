using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.ProposalMigration;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.RetainedGifts;

/// <summary>One exactly labeled managed preparatory commit. Preparation is never a native action.</summary>
internal sealed record RetainedGiftPreparatoryAction(string Label, ulong Sequence, EntityId ActorId, EntityId? CommandId);

/// <summary>
/// Managed preparation of the transitioned original world for the native gift journey, using only actual
/// ordinary commands under each actor's own control. The patient withdraws the helper's care permission
/// so helper treatment cannot stand in for own use; the helper's own offers and withdrawals fill the
/// saved-proposal outcome window to one short of the reservation that closes a segment; empty ordinary
/// steps then begin the helper's idle amble interval. No stock, injury, budget, control or cut is edited.
/// </summary>
internal sealed class RetainedGiftPreparation
{
    /// <summary>
    /// With this many retained outcomes the helper's native offer still fits (62 + 1 + 1 reserved = 64),
    /// while any further offer created during that open gift requires the automatic cut (62 + 1 + 1 + 1).
    /// </summary>
    internal const int PreparedOutcomes = 62;
    internal const int IdleAmblePhase = 16;
    private const int AmbleCycle = 64;
    private const ulong PressureOfferLifetime = 64;
    private readonly SqliteWorldAuthority _authority;
    private readonly MaterialProposalService _service;
    private ulong _next;

    private RetainedGiftPreparation(SqliteWorldAuthority authority)
    {
        _authority = authority;
        _service = new MaterialProposalService(authority, () => NextId("cause"));
        WorldSnapshot state = authority.ReadStateForHost();
        Player = state.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        // Reciprocal quests name no single companion; the patient's own permission names the helper.
        Helper = state.Care!.Ordinary!.Permissions.Single(value => value.PatientActorId == Player).HelperActorId;
        Remedy = state.Care.Remedies.Single();
        PotionLot = state.Inventory.Custody.Lots.Single(value => value.CustodianId == Bag(state, Helper)
            && value.Definition == Remedy.Item).LotId;
    }

    internal EntityId Player { get; }
    internal EntityId Helper { get; }
    internal EntityId PotionLot { get; }
    internal RestorativeRemedyDefinition Remedy { get; }
    internal bool PermissionWasActive { get; private set; }
    internal List<RetainedGiftPreparatoryAction> Actions { get; } = [];
    internal WorldSnapshot Prepared => _authority.ReadStateForHost();

    internal static RetainedGiftPreparation Run(SqliteWorldAuthority authority)
    {
        var preparation = new RetainedGiftPreparation(authority);
        preparation.WithdrawCarePermission();
        preparation.FillOutcomeWindow();
        preparation.ReachIdleAmble();
        return preparation;
    }

    internal static EntityId Bag(WorldSnapshot state, EntityId actor) => state.GetResident(actor).InventoryCustodianId;

    internal static ulong Potions(WorldSnapshot state, RestorativeRemedyDefinition remedy, EntityId? bag = null) =>
        state.Inventory.Custody.Lots.Where(value => value.Definition == remedy.Item && (bag is null || value.CustodianId == bag))
            .Aggregate(0UL, (sum, value) => checked(sum + value.Quantity))
        + state.Production!.Lots.Where(value => value.Definition == remedy.Item && (bag is null || value.CustodianId == bag))
            .Aggregate(0UL, (sum, value) => checked(sum + value.Quantity));

    internal static ulong AmblePhase(WorldSnapshot state) =>
        (state.EncounterLifecycle is null ? state.AdvancingSteps : state.ProcessIntervals) % AmbleCycle;

    internal static WorldActionCommand Command(WorldSnapshot state, EntityId actor, WorldActionIntent intent, EntityId commandId)
    {
        ActorControlBinding control = state.Inventory.Controls.Single(value => value.ActorId == actor);
        return new(commandId, control.PrincipalId, control.SessionId, actor, state.WorldId, state.BranchId,
            state.Space.GetActor(actor).Revision, intent);
    }

    internal static TrustedCommandDelivery Delivery(WorldSnapshot state, EntityId actor)
    {
        ActorControlBinding control = state.Inventory.Controls.Single(value => value.ActorId == actor);
        return new(control.PrincipalId, control.SessionId);
    }

    internal static EntityId NextId(string label, ulong number) => RetainedTransitionFixture.Cause("retained-gift." + label + "." + number);

    private EntityId NextId(string label) => NextId(label, _next++);

    /// <summary>The patient's own command through an ordinary advancing step, exactly as native input selects it.</summary>
    private void WithdrawCarePermission()
    {
        WorldSnapshot state = Prepared;
        CarePermission permission = state.Care!.Ordinary!.Permissions.Single(value => value.PatientActorId == Player);
        PermissionWasActive = permission.IsActive;
        if (!PermissionWasActive) return;
        WorldActionCommand revoke = Command(state, Player,
            new RevokeCarePermissionIntent(permission.PermissionId, permission.Revision, permission.GrantVersion), NextId("revoke"));
        WorldStepDecision decision = Step(state, [revoke]);
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(decision.Receipts).Status);
        Assert.False(decision.Candidate.Care!.Ordinary!.Permissions.Single(value => value.PatientActorId == Player).IsActive);
        Actions.Add(new("prepare-revoke-care-permission", decision.Candidate.Revision, Player, revoke.CommandId));
    }

    /// <summary>The helper's own independently delivered offers of one real dose, each withdrawn by the helper.</summary>
    private void FillOutcomeWindow()
    {
        ulong ordinal = Prepared.Coordination!.Anchor!.Reference.Ordinal;
        while (Prepared.Coordination!.Proposals!.Outcomes.Count < PreparedOutcomes)
        {
            WorldSnapshot state = Prepared;
            CountedSourceLot lot = state.Inventory.Custody.GetLot(PotionLot);
            var terms = new ConsentedMaterialTerms(state.WorldId, state.BranchId, Helper, Player,
                ConsentedMaterialLotKind.CountedSource, lot.LotId, lot.Definition, Bag(state, Helper), Bag(state, Player), 1, lot.Revision);
            WorldActionCommand create = Command(state, Helper,
                new ProposeMaterialIntent(terms, state.AdvancingSteps + 1 + PressureOfferLifetime), NextId("pressure-offer"));
            WorldActionReceipt created = _service.Submit(create, Delivery(state, Helper));
            Assert.Equal(WorldActionRejection.None, created.Rejection);
            Actions.Add(new("prepare-pressure-offer", created.CommitSequence, Helper, create.CommandId));
            state = Prepared;
            WorldActionCommand withdraw = Command(state, Helper,
                new WithdrawMaterialProposalIntent(WorldMaterialProposal.DeriveIdentity(create.CommandId)), NextId("pressure-withdrawal"));
            WorldActionReceipt withdrawn = _service.Submit(withdraw, Delivery(state, Helper));
            Assert.Equal(WorldActionRejection.None, withdrawn.Rejection);
            Actions.Add(new("prepare-pressure-withdrawal", withdrawn.CommitSequence, Helper, withdraw.CommandId));
            Assert.True(Actions.Count < 2 * PreparedOutcomes + 2, "Pressure must reach its window without an unbounded loop.");
        }
        // Preparation never closes a segment itself.
        Assert.Equal(ordinal, Prepared.Coordination!.Anchor!.Reference.Ordinal);
        Assert.Empty(Prepared.Coordination.Proposals!.Open);
    }

    /// <summary>
    /// Ordinary empty steps start the helper's idle amble interval, so the short native journey does not
    /// depend on a sampled step moving the helper between offer inspection and acceptance.
    /// </summary>
    private void ReachIdleAmble()
    {
        for (int step = 0; AmblePhase(Prepared) != IdleAmblePhase; step++)
        {
            Assert.True(step < AmbleCycle, "Ordinary empty steps must reach the idle amble interval.");
            WorldStepDecision decision = Step(Prepared, []);
            Actions.Add(new("prepare-idle-step", decision.Candidate.Revision, Player, null));
        }
    }

    private WorldStepDecision Step(WorldSnapshot state, IReadOnlyList<WorldActionCommand> actions) =>
        _authority.Commit(new WorldStepBatch(NextId("step"), state.WorldId, state.BranchId, state.Revision,
            state.AdvancingSteps, WorldBatchKind.AdvancingStep, [], actions, state.ProfileVersion));
}
