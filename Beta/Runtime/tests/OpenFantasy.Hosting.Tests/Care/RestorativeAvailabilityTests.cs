using System.Reflection;
using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class RestorativeAvailabilityTests(RestorativeAvailabilityFixture fixture)
    : IClassFixture<RestorativeAvailabilityFixture>
{
    [Fact]
    public void Known_full_health_choice_stays_inspectable_and_permission_withdrawal_does_not_require_an_injury()
    {
        var world = fixture.Source.Genesis;
        var view = Assert.IsType<LocalRestorativeView>(LocalRestorativeView.Project(world, fixture.Player));
        var use = Assert.Single(view.SelfUseOffers);
        Assert.Equal("Potion", use.ItemName);
        Assert.Equal(40U, use.HealHitPoints);
        Assert.Equal(0U, use.ExpectedHealingHp);
        Assert.Equal(1800UL, use.ExplorationMilliseconds);
        Blocked(use.Availability, RestorativeAvailabilityReason.FullHealth);
        var permission = Assert.Single(view.PermissionOffers);
        Assert.True(permission.IsActive);
        Assert.True(permission.CanRevoke);
        Assert.Equal(fixture.Helper, permission.HelperActorId);
        Assert.Equal(world.GetResident(fixture.Helper).DisplayName, permission.HelperName);
        Assert.IsType<RevokeCarePermissionIntent>(permission.Intent);
        Assert.Null(LocalRestorativeView.Project(CareJourneyAdmission.CreateEmbedded("availability-old-profile", "Traveler"), fixture.Player));
    }

    [Fact]
    public void Earned_injury_offers_exact_owned_dose_then_actual_use_invalidates_the_captured_selection()
    {
        var world = fixture.Ready;
        var offer = Assert.Single(LocalRestorativeView.Project(world, fixture.Player)!.SelfUseOffers);
        var intent = Assert.IsType<UseRestorativeItemIntent>(offer.Intent);
        var lot = world.Inventory.Custody.GetLot(intent.Ingredient.LotId);
        Assert.True(offer.CanUse);
        Assert.Equal(world.GetResident(fixture.Player).InventoryCustodianId, lot.CustodianId);
        Assert.Equal(1UL, lot.Quantity);
        Assert.Equal(1UL, intent.Ingredient.Quantity);
        Assert.Equal(lot.Revision, intent.Ingredient.ExpectedRevision);
        Assert.Equal(IngredientLotKind.Source, intent.Ingredient.Kind);
        Assert.Equal(Math.Min(40U, (uint)(60 - world.Progression!.GetActor(fixture.Player).CurrentHp)), offer.ExpectedHealingHp);
        Assert.Equal(intent, WorldRestorativeActions.Prepare(world, fixture.Player, fixture.Player, intent.UseDefinitionId));

        var session = new CareWorldSession(world);
        var after = CareRewardJourney.Commit(session, fixture.Source.Journey.Batch(world, WorldBatchKind.AdvancingStep,
            [fixture.Source.Journey.Command(world, fixture.Player, intent)])).Candidate;
        Assert.Equal(world.Production!.ConsumedItems.Count + 1, after.Production!.ConsumedItems.Count);
        Assert.Single(after.Care!.Ordinary!.Uses);
        Assert.DoesNotContain(after.Inventory.Custody.Lots, value => value.LotId == lot.LotId);
        var stale = WorldRestorativeActions.Assess(after, fixture.Player, intent);
        Blocked(stale, RestorativeAvailabilityReason.StaleSelection, WorldActionRejection.RevisionConflict);
        Assert.Equal(WorldActionRejection.RevisionConflict, WorldRestorativeActions.Evaluate(after, fixture.Player, intent));
    }

    [Fact]
    public void Actual_missing_helper_dose_and_permission_failures_are_not_mislabeled_full_health_or_debt()
    {
        var world = fixture.Ready;
        Assert.InRange(world.Progression!.GetActor(fixture.Player).CurrentHp, 1, 59);
        Assert.True(WorldActorAvailability.CanAct(world, fixture.Helper));
        var definition = fixture.Assistance;
        Blocked(WorldRestorativeActions.PrepareAvailability(world, fixture.Helper, fixture.Player, definition.DefinitionId),
            RestorativeAvailabilityReason.PermissionRequired, WorldActionRejection.NotAuthorized);
        Blocked(WorldRestorativeActions.PrepareAvailability(world, fixture.Helper, fixture.Player, definition.DefinitionId, Id(9900)),
            RestorativeAvailabilityReason.PermissionMismatch, WorldActionRejection.NotAuthorized);
        Blocked(WorldRestorativeActions.PrepareAvailability(world, fixture.Helper, fixture.Player, definition.DefinitionId,
            fixture.Permission.PermissionId), RestorativeAvailabilityReason.NoOwnedDose);
        var view = LocalRestorativeView.Project(world, fixture.Player)!;
        Assert.All(view.SelfUseOffers, value => Assert.Equal(fixture.Self.DefinitionId, value.DefinitionId));
        Assert.All(view.SelfUseOffers.Where(value => value.Intent is not null), value =>
        {
            Assert.Equal(fixture.Player, value.Intent!.RecipientActorId);
            Assert.Null(value.Intent.PermissionId);
        });
    }

    [Fact]
    public void Patient_withdrawal_captures_exact_permission_and_revoked_choice_remains_inspectable()
    {
        var world = fixture.Ready;
        var offer = Assert.Single(LocalRestorativeView.Project(world, fixture.Player)!.PermissionOffers);
        var revoke = Assert.IsType<RevokeCarePermissionIntent>(offer.Intent);
        Assert.Equal(fixture.Permission.PermissionId, revoke.PermissionId);
        Assert.Equal(fixture.Permission.Revision, revoke.ExpectedPermissionRevision);
        Assert.Equal(fixture.Permission.GrantVersion, revoke.ExpectedPermissionGrantVersion);
        var session = new CareWorldSession(world);
        var after = CareRewardJourney.Commit(session, fixture.Source.Journey.Batch(world, WorldBatchKind.AdvancingStep,
            [fixture.Source.Journey.Command(world, fixture.Player, revoke)])).Candidate;
        var revoked = Assert.Single(LocalRestorativeView.Project(after, fixture.Player)!.PermissionOffers);
        Assert.False(revoked.IsActive);
        Assert.False(revoked.CanRevoke);
        Assert.Null(revoked.Intent);
        Assert.Equal(RestorativeAvailabilityReason.PermissionRevoked, revoked.Reason);
        Blocked(WorldRestorativeActions.PrepareAvailability(after, fixture.Helper, fixture.Player,
            fixture.Assistance.DefinitionId, fixture.Permission.PermissionId),
            RestorativeAvailabilityReason.PermissionRevoked, WorldActionRejection.NotAuthorized);
        Assert.Equal(world.Inventory.ContentHash, after.Inventory.ContentHash);
        Assert.Equal(world.Production!.ContentHash, after.Production!.ContentHash);
    }

    [Fact]
    public void Actual_encounter_aftermath_readiness_and_reading_holds_explain_their_own_context()
    {
        Blocked(Prepare(fixture.Defeated), RestorativeAvailabilityReason.EncounterContext);
        Blocked(Prepare(fixture.Pending), RestorativeAvailabilityReason.PendingAftermathCare);
        Assert.InRange(fixture.Released.Progression!.GetActor(fixture.Player).CurrentHp, 1, 59);
        Assert.True(fixture.Released.EncounterLifecycle!.GetDebt(fixture.Player).RemainingDelay > 0);
        Blocked(Prepare(fixture.Released), RestorativeAvailabilityReason.ActionDebt);
        var session = new CareWorldSession(fixture.Ready);
        var world = fixture.Ready;
        var held = CareRewardJourney.Commit(session, fixture.Source.Journey.Batch(world, WorldBatchKind.AdvancingStep,
            [fixture.Source.Journey.Command(world, fixture.Player,
                new SpeakToActorIntent(fixture.Helper, world.Space.GetActor(fixture.Helper).Revision))])).Candidate;
        Assert.True(held.HasReadingHold);
        Blocked(Prepare(held), RestorativeAvailabilityReason.ReadingHold);
        var permission = Assert.Single(LocalRestorativeView.Project(held, fixture.Player)!.PermissionOffers);
        Assert.False(permission.CanRevoke);
        Assert.Null(permission.Intent);
        Assert.Equal(RestorativeAvailabilityReason.ReadingHold, permission.Reason);
    }

    [Fact]
    public void Captured_lot_revision_is_explained_as_stale_without_changing_its_saved_receipt_meaning()
    {
        var good = Assert.IsType<UseRestorativeItemIntent>(Prepare(fixture.Ready).Intent);
        var stale = new UseRestorativeItemIntent(good.RecipientActorId, good.UseDefinitionId, good.UseDefinitionVersion,
            good.PermissionId, good.PermissionGrantVersion, good.ExpectedPermissionRevision, good.ExpectedPatientPresenceRevision,
            good.ExpectedPatientProgressionRevision, good.ExpectedStorageRevision, good.ExpectedProductionRevision,
            new(good.Ingredient.Kind, good.Ingredient.LotId, good.Ingredient.ExpectedRevision + 1, 1));
        Blocked(WorldRestorativeActions.Assess(fixture.Ready, fixture.Player, stale),
            RestorativeAvailabilityReason.StaleSelection, WorldActionRejection.Unavailable);
        Assert.Equal(WorldActionRejection.Unavailable, WorldRestorativeActions.Evaluate(fixture.Ready, fixture.Player, stale));
    }

    [Fact]
    public void Actual_crafted_aid_reports_range_after_real_helper_movement_without_refreshing_the_selected_dose()
    {
        var session = new CareWorldSession(fixture.Ready);
        var state = fixture.Source.Supply(session, self: false);
        var captured = fixture.Source.Intent(state, self: false);
        Assert.True(WorldRestorativeActions.Assess(state, fixture.Helper, captured).CanUse);
        var target = state.Space.GetActor(fixture.Player).Feet.Offset(0,
            -checked((long)state.Care!.Remedies.Single().RangeSubunits) - 32, 0);
        for (int step = 0; state.Space.GetActor(fixture.Helper).Feet != target; step++)
        {
            Assert.True(step < 64);
            var route = state.Space.QueryGroundRoute(fixture.Helper, target);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            var next = Assert.IsType<SubunitPosition>(route.NextWaypoint);
            var feet = state.Space.GetActor(fixture.Helper).Feet;
            long dx = Math.Clamp(next.X - feet.X, -32, 32);
            long dy = dx == 0 ? Math.Clamp(next.Y - feet.Y, -32, 32) : 0;
            var control = state.Inventory.Controls.Single(value => value.ActorId == fixture.Helper);
            var batch = fixture.Source.Journey.Batch(state, WorldBatchKind.AdvancingStep);
            state = CareRewardJourney.Commit(session, new(batch.CauseId, batch.WorldId, batch.BranchId,
                batch.ExpectedRevision, batch.ExpectedAdvancingSteps, batch.Kind,
                [new(new(control.PrincipalId, control.SessionId), fixture.Helper, dx, dy)], [], batch.ProfileVersion)).Candidate;
        }
        Assert.InRange(state.Progression!.GetActor(fixture.Player).CurrentHp, 1, 59);
        Assert.True(WorldActorAvailability.CanAct(state, fixture.Helper));
        Blocked(WorldRestorativeActions.Assess(state, fixture.Helper, captured),
            RestorativeAvailabilityReason.OutOfReach, WorldActionRejection.OutOfReach);
        Assert.Equal(1UL, state.Production!.Lots.Single(value => value.LotId == captured.Ingredient.LotId).Quantity);
    }

    [Fact]
    public void Structural_candidate_health_form_and_manipulation_diagnostics_are_independent_of_other_blockers()
    {
        // These candidate owners intentionally do not claim an earned or admitted complete world.
        // The shared assessment boundary is isolated while the genuine basis stays untouched.
        var world = fixture.Ready;
        var actor = world.Progression!.GetActor(fixture.Player);
        var zero = new ActorProgressionState(actor.ActorId, actor.Revision, actor.Profile, actor.Curve,
            actor.Level, actor.Experience, 0, actor.CurrentMp);
        var progression = new ProgressionSnapshot(world.WorldId, world.BranchId, world.Progression.Revision,
            world.Progression.Profiles, world.Progression.Curves,
            world.Progression.Actors.Select(value => value.ActorId == fixture.Player ? zero : value), world.Progression.Currency);
        Blocked(Diagnostic(progression: progression), RestorativeAvailabilityReason.RecipientIncapacitated);

        var presence = world.Space.GetActor(fixture.Player);
        var injuries = presence.Condition.FunctionalSlots(BodyFunction.Manipulation).Select((slot, index) =>
            new BodyInjury(Id(6000UL + (ulong)index), Id(6100), slot, BodyInjuryKind.FunctionLoss, BodyFunction.Manipulation));
        var condition = new BodyConditionSnapshot(presence.Binding.FormInstanceId, presence.Condition.Definition, injuries);
        Assert.False(condition.SupportsFunction(BodyFunction.Manipulation));
        var changed = new ActorPresence(presence.Binding, condition, presence.Scope, presence.Feet, presence.Revision, presence.HomePlaceId);
        var space = world.Space.WithActors(world.Space.Actors.Select(value => value.ActorId == fixture.Player ? changed : value));
        Blocked(Diagnostic(space: space), RestorativeAvailabilityReason.ManipulationUnavailable);

        var unsupported = new RestorativeUseDefinition(fixture.Self.DefinitionId, fixture.Self.RemedyDefinitionId,
            fixture.Self.RemedyVersion, RestorativeDeliveryMode.SelfUse,
            [new(Id(6200), presence.Binding.DefinitionVersion)]);
        var ordinary = new OrdinaryCareSnapshot([unsupported, fixture.Assistance], [fixture.Permission], []);
        Blocked(Diagnostic(care: Care(ordinary)), RestorativeAvailabilityReason.UnsupportedForm);
        Assert.True(Prepare(world).CanUse);
    }

    [Theory]
    [InlineData(255, 0, RestorativeAvailabilityReason.Available)]
    [InlineData(256, 0, RestorativeAvailabilityReason.MaterialHistoryCapacity)]
    [InlineData(1, 255, RestorativeAvailabilityReason.Available)]
    [InlineData(1, 256, RestorativeAvailabilityReason.OrdinaryHistoryCapacity)]
    [InlineData(256, 256, RestorativeAvailabilityReason.MaterialHistoryCapacity)]
    public void Structural_retained_owner_limits_are_distinct_and_do_not_claim_archives_free_capacity(
        int materialCount, int ordinaryCount, RestorativeAvailabilityReason reason)
    {
        // Fully constructed structural owners, NOT a manufactured durable genesis or proof of
        // 256 earned treatments. Their history is not admitted against this real world basis.
        var production = ProductionAtCapacity(materialCount);
        var ordinary = OrdinaryAtCapacity(ordinaryCount);
        Assert.Equal(materialCount, production.ConsumedItems.Count);
        Assert.Equal(ordinaryCount, ordinary.Uses.Count);
        var assessment = Diagnostic(production: production, care: Care(ordinary));
        Assert.Equal(reason, assessment.Reason);
        if (reason == RestorativeAvailabilityReason.Available)
        {
            Assert.True(assessment.CanUse);
            Assert.NotNull(assessment.Intent);
        }
        else Blocked(assessment, reason);
        // Authority's candidate-owner entrypoint independently rejects an exact current request
        // at capacity; no partially published material or health result is produced by preflight.
        var available = Assert.IsType<UseRestorativeItemIntent>(Prepare(fixture.Ready).Intent);
        var current = new UseRestorativeItemIntent(available.RecipientActorId, available.UseDefinitionId,
            available.UseDefinitionVersion, null, null, null, available.ExpectedPatientPresenceRevision,
            available.ExpectedPatientProgressionRevision, available.ExpectedStorageRevision, production.Revision,
            available.Ingredient);
        MethodInfo evaluate = typeof(WorldRestorativeActions).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(value => value.Name == "Evaluate");
        Assert.Equal(reason == RestorativeAvailabilityReason.Available ? WorldActionRejection.None : WorldActionRejection.Unavailable,
            Assert.IsType<WorldActionRejection>(evaluate.Invoke(null, [fixture.Ready, fixture.Ready.Inventory,
                fixture.Ready.Space, production, fixture.Ready.Progression!, Care(ordinary), fixture.Player, current, false])));
        Assert.True(Prepare(fixture.Ready).CanUse);
    }

    private RestorativeAvailability Prepare(WorldSnapshot world) => WorldRestorativeActions.PrepareAvailability(
        world, fixture.Player, fixture.Player, fixture.Self.DefinitionId);

    private static void Blocked(RestorativeAvailability value, RestorativeAvailabilityReason reason,
        WorldActionRejection rejection = WorldActionRejection.Unavailable)
    {
        Assert.Equal(reason, value.Reason);
        Assert.Equal(rejection, value.Rejection);
        Assert.False(value.CanUse);
        Assert.Null(value.Intent);
    }

    private RestorativeAvailability Diagnostic(ProductionSnapshot? production = null, CareSnapshot? care = null,
        ProgressionSnapshot? progression = null, LocalSpatialSnapshot? space = null)
    {
        // Core grants no test friend access. Keep this narrow reflective call to the actual
        // shared preflight boundary instead of introducing public authority for synthetic roots.
        MethodInfo assess = typeof(WorldRestorativeActions).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(value => value.Name == "Assess");
        var world = fixture.Ready;
        return Assert.IsType<RestorativeAvailability>(assess.Invoke(null,
            [world, world.Inventory, space ?? world.Space, production ?? world.Production!, progression ?? world.Progression!,
                care ?? world.Care!, fixture.Player, fixture.Player, fixture.Self.DefinitionId, null, null, false]));
    }

    private CareSnapshot Care(OrdinaryCareSnapshot ordinary)
    {
        var prior = fixture.Ready.Care!;
        return new(prior.WorldId, prior.BranchId,
            (ulong)(prior.Acknowledgments.Count + prior.Treatments.Count) + ordinary.Revision,
            prior.Remedies, prior.Agreements, prior.Acknowledgments, prior.Treatments, ordinary);
    }

    private OrdinaryCareSnapshot OrdinaryAtCapacity(int count)
    {
        var world = fixture.Ready;
        var uses = Enumerable.Range(0, count).Select(index =>
        {
            var command = Id(10_000UL + (ulong)index);
            return new RestorativeUse(EntityId.DeriveSpawned(command, 1), command, fixture.Player, fixture.Player,
                fixture.Self.DefinitionId, fixture.Self.Version, null, null, null, EntityId.DeriveSpawned(command, 0),
                world.Revision + 1 + (ulong)index, 0, (ulong)index, 60, 1, 41);
        });
        return new(world.Care!.Ordinary!.Definitions, world.Care.Ordinary.Permissions, uses);
    }

    private ProductionSnapshot ProductionAtCapacity(int count)
    {
        var world = fixture.Ready;
        var prior = world.Production!;
        Assert.Single(prior.ConsumedItems);
        var claims = prior.ConsumedItems.Concat(Enumerable.Range(0, count - 1).Select(index =>
        {
            var command = Id(20_000UL + (ulong)index);
            var ingredient = new RetiredIngredient(Id(30_000UL + (ulong)index), 0, world.Care!.Remedies.Single().Item,
                new SourceInterval(Id(40_000), (ulong)index, (ulong)index + 1));
            return new ConsumedItemClaim(EntityId.DeriveSpawned(command, 0), command, fixture.Player, fixture.Player,
                world.GetResident(fixture.Player).InventoryCustodianId, ingredient);
        }));
        return new(prior.WorldId, prior.BranchId, Math.Max(prior.Revision, (ulong)count), prior.Recipes,
            prior.Batches, prior.Lots, claims);
    }

    private static EntityId Id(ulong value) => new(0xcafe2600abcdef00, value);
}

/// <summary>Shares one actual source-stat journey; structural diagnostic owners never replace these admitted states.</summary>
public sealed class RestorativeAvailabilityFixture
{
    public RestorativeAvailabilityFixture()
    {
        Source = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(Source.Genesis);
        Defeated = Source.Journey.PrepareDefeat(session);
        Pending = CareRewardJourney.Commit(session, Source.Journey.Batch(Defeated, WorldBatchKind.Care,
            [Source.Journey.Acknowledge(Defeated)])).Candidate;
        var treated = CareRewardJourney.Commit(session, Source.Journey.Batch(Pending, WorldBatchKind.Care,
            [Source.Journey.Treat(Pending)])).Candidate;
        Assert.Equal(40, treated.Progression!.GetActor(Player).CurrentHp);
        Released = Source.Journey.WinAndRelease(session);
        var state = Released;
        for (int step = 0; !WorldActorAvailability.CanAct(state, Player) || !WorldActorAvailability.CanAct(state, Helper); step++)
        {
            Assert.True(step < 32);
            state = CareRewardJourney.Commit(session, Source.Journey.Batch(state, WorldBatchKind.AdvancingStep)).Candidate;
        }
        Ready = state;
        Assert.InRange(Ready.Progression!.GetActor(Player).CurrentHp, 1, 59);
    }

    internal OrdinaryCareWorldFixture Source { get; }
    internal EntityId Player => Source.Player;
    internal EntityId Helper => Source.Helper;
    internal WorldSnapshot Defeated { get; }
    internal WorldSnapshot Pending { get; }
    internal WorldSnapshot Released { get; }
    internal WorldSnapshot Ready { get; }
    internal RestorativeUseDefinition Self => Ready.Care!.Ordinary!.Definitions.Single(value => value.DeliveryMode == RestorativeDeliveryMode.SelfUse);
    internal RestorativeUseDefinition Assistance => Ready.Care!.Ordinary!.Definitions.Single(value => value.DeliveryMode == RestorativeDeliveryMode.LivingAssistance);
    internal CarePermission Permission => Ready.Care!.Ordinary!.Permissions.Single();
}
