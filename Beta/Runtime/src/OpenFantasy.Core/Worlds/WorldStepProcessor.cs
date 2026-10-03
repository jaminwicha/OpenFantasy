using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Structures;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Replays a host-selected batch without IO or wall-clock access. Authentication and selection at
/// the fixed host cutoff precede this boundary; encoded delivery claims do not authenticate callers.
/// </summary>
public sealed class WorldStepProcessor
{
    private readonly WorldActionResolver _actions = new();
    private readonly WorldRestorativeResolver _restorative = new();
    private readonly ExcavationPlanner _excavation = new();
    private readonly GatherPlanner _gathering = new();
    private readonly WorldCraftResolver _crafting = new();
    private readonly DeliveryQuestPlanner _quests = new();
    private readonly ConstructionPlanner _construction = new();
    private readonly WorldRunoffResolver _runoff = new();
    private readonly WorldTransportResolver _transport = new();
    private readonly LiquidCellPlanner _liquidFlow = new();

    public WorldStepDecision Decide(WorldSnapshot state, WorldStepBatch batch,
        Func<EntityId, bool>? historicallyOccupied = null) =>
        DecideSelected(state, batch, historicallyOccupied, observeMaterial: false);

    /// <summary>
    /// Prepares the same transition as <see cref="Decide"/> and additionally returns one bounded
    /// observation per accepted material or work action. This first binding interprets only the
    /// complete profile 26 world and refuses material intents it cannot observe. The observations
    /// are structural and unauthenticated; they are neither persisted nor a material grant.
    /// </summary>
    public WorldStepDecision DecideWithMaterialEvidence(WorldSnapshot state, WorldStepBatch batch,
        Func<EntityId, bool>? historicallyOccupied = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(batch);
        WorldMaterialEvidenceCoverage.RequireProfile(state);
        WorldMaterialEvidenceCoverage.RequireCovered(batch);
        return DecideSelected(state, batch, historicallyOccupied, observeMaterial: true);
    }

    private WorldStepDecision DecideSelected(WorldSnapshot state, WorldStepBatch batch,
        Func<EntityId, bool>? historicallyOccupied, bool observeMaterial)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.ProfileVersion != state.ProfileVersion || batch.WorldId != state.WorldId || batch.BranchId != state.BranchId
            || batch.ExpectedRevision != state.Revision || batch.ExpectedAdvancingSteps != state.AdvancingSteps
            || batch.Kind is not WorldBatchKind.ControllerLifecycle and not WorldBatchKind.ControlPermission
                && (batch.Kind == WorldBatchKind.HeldInteraction) != state.HasReadingHold)
        {
            throw new InvalidOperationException("The selected batch does not match the current world, revision or hold.");
        }
        if (state.ResourceHistory?.ArchiveAnchor is not null && historicallyOccupied is null)
            throw new InvalidOperationException("Continued resource history requires the complete historical identity lookup.");

        if (state.Coordination?.Anchor is not null && historicallyOccupied is null)
            throw new InvalidOperationException("Coordinated continuation requires the complete historical identity lookup.");

        // External session facts preserve all gameplay owners and do not trigger a representation cut.
        if (batch.Kind == WorldBatchKind.ControllerLifecycle)
            return WorldControllerProcessor.Decide(state, batch, historicallyOccupied);

        if (batch.Kind == WorldBatchKind.ControlPermission)
            return WorldControlPermissionProcessor.Decide(state, batch, historicallyOccupied);

        var prepared = WorldResourceContinuation.Prepare(state, batch);
        if (prepared.Sealed is not null && historicallyOccupied is null)
            throw new InvalidOperationException("Resource archival continuation requires the complete historical identity lookup.");
        var joint = WorldMaterialCareContinuation.Prepare(prepared.State, batch);
        if (joint.Sealed is not null && historicallyOccupied is null)
            throw new InvalidOperationException("Coordinated archival continuation requires the complete historical identity lookup.");
        // Observations bind the prepared root that is actually interpreted. When preparation sealed
        // an archive segment that root differs from the selected input, whose hash is kept beside it.
        var materialEvidence = observeMaterial ? new WorldMaterialEvidenceCollector(state, prepared.State) : null;
        var decision = DecidePrepared(joint.State, batch, historicallyOccupied, materialEvidence);
        return prepared.Sealed is null && joint.Sealed is null ? decision
            : new WorldStepDecision(decision.Candidate, decision.Receipts, decision.Identities, prepared.Sealed,
                decision.MaterialEvidence, joint.Sealed, decision.CandidateFrame);
    }

    private WorldStepDecision DecidePrepared(WorldSnapshot state, WorldStepBatch batch, Func<EntityId, bool>? historicallyOccupied,
        WorldMaterialEvidenceCollector? materialEvidence)
    {
        if (batch.Kind == WorldBatchKind.Care)
        {
            return WorldCareProcessor.Decide(state, batch, historicallyOccupied, materialEvidence);
        }
        if (state.Care?.Pending is not null)
        {
            throw new InvalidOperationException("An acknowledged care obligation must resolve before another world activity.");
        }
        if (batch.Kind == WorldBatchKind.EncounterLifecycle)
        {
            return WorldEncounterLifecycleProcessor.Decide(state, batch, historicallyOccupied);
        }
        if (state.EncounterLifecycle?.Phase is EncounterLifecyclePhase.Confrontation or EncounterLifecyclePhase.Staging or EncounterLifecyclePhase.Outcome)
        {
            throw new InvalidOperationException("This encounter phase requires its lifecycle boundary.");
        }
        if (state.EncounterActions is not null)
        {
            return WorldEncounterActionProcessor.Decide(state, batch, historicallyOccupied);
        }
        if (state.Pacing?.Encounter is not null)
        {
            return WorldEncounterProcessor.Decide(state, batch, historicallyOccupied);
        }
        if (batch.Kind is WorldBatchKind.EncounterDecision or WorldBatchKind.EncounterResolution or WorldBatchKind.TacticalAdvance)
        {
            throw new InvalidOperationException("No active encounter owns the selected tactical boundary.");
        }
        if ((batch.Kind == WorldBatchKind.ConsequenceContinuation) != (state.PendingExcavation is not null))
        {
            throw new InvalidOperationException("Pending work accepts only its explicit continuation.");
        }
        if (state.PendingExcavation is not null)
        {
            return WorldConsequenceProcessor.Continue(this, state, batch, historicallyOccupied);
        }
        return WorldConsequenceProcessor.TryBegin(state, batch, historicallyOccupied)
            ?? DecideImmediate(state, batch, historicallyOccupied, materialEvidence: materialEvidence);
    }

    internal WorldStepDecision DecideImmediate(WorldSnapshot state, WorldStepBatch batch,
        Func<EntityId, bool>? historicallyOccupied, ConstructionSettlementResolution? completedSettlement = null,
        WorldActionCommand? acceptedCommand = null, WorldMaterialEvidenceCollector? materialEvidence = null)
    {
        ulong sequence = checked(state.Revision + 1);
        HashSet<EntityId> reserved = [];
        List<WorldCommittedIdentity> identities = [];
        bool Occupied(EntityId identity) => reserved.Contains(identity) || state.IdentityIndex.Contains(identity)
            || historicallyOccupied?.Invoke(identity) == true;
        void Reserve(EntityId identity, WorldCommittedIdentityKind kind)
        {
            if (Occupied(identity))
            {
                throw new InvalidOperationException("A committed identity is already occupied.");
            }
            reserved.Add(identity);
            identities.Add(new WorldCommittedIdentity(identity, kind));
        }

        Reserve(batch.CauseId, WorldCommittedIdentityKind.CommitCause);
        foreach (WorldActionCommand action in batch.Actions)
        {
            if (acceptedCommand is null) { Reserve(action.CommandId, WorldCommittedIdentityKind.Command); }
            else if (batch.Actions.Count != 1 || action.ContentHash != acceptedCommand.ContentHash
                || action.CommandId != acceptedCommand.CommandId)
            {
                throw new InvalidOperationException("Only the exact accepted excavation can finish without a new command identity.");
            }
        }
        CountedAuthorityState inventory = state.Inventory;
        GatheringSnapshot? gathering = state.Gathering;
        ProductionSnapshot? production = state.Production;
        ProgressionSnapshot? progression = state.Progression;
        var resourceHistory = state.ResourceHistory;
        var care = state.Care;
        var coordination = state.Coordination;
        var materialPairs = coordination?.ConsentedMaterial is null ? null : new WorldMaterialPairing(batch);
        QuestSnapshot? quests = state.Quests;
        ConstructionSnapshot? construction = state.Construction;
        ConstructionPhysicalSnapshot? physical = state.ConstructionPhysical;
        LocalSpatialSnapshot space = state.Space;
        LiquidReservoirState? reservoirs = state.Reservoirs;
        ReservoirTransportState? transport = state.Transport;
        LiquidCellState? liquidCells = state.LiquidCells;
        ExcavationSnapshot? excavation = state.Excavation;
        HashSet<EntityId> worked = [];
        List<DialogueSession> dialogues = state.Dialogues.ToList();
        List<WorldActionReceipt> receipts = [];
        WorldInstant calendar = state.Calendar;
        ulong advancingSteps = state.AdvancingSteps;
        WorldPacingState? pacing = state.Pacing;
        var lifecycle = state.EncounterLifecycle?.RetainBoundary();
        IEnumerable<WorldResident> residents = state.Residents;

        bool FitsCompleteCandidate(CountedAuthorityState proposedInventory,
            IEnumerable<DialogueSession> proposedDialogues, ConstructionPhysicalSnapshot? proposedPhysical,
            LiquidReservoirState? proposedReservoirs, ReservoirTransportState? proposedTransport,
            LiquidCellState? proposedLiquidCells,
            out WorldSnapshot? admittedCandidate, out WorldCanonicalFrame? admittedFrame)
        {
            admittedCandidate = null;
            admittedFrame = null;
            if (state.StructuralPolicy is null && state.ReservoirSeals is null)
            {
                return true;
            }
            try
            {
                // Use the same complete root admission as publication. This includes actual carried
                // goods and every occupant, including the full nominal contained reservoir envelope.
                // During action preparation the selected advancing step has not yet been applied.
                // Its earned labor is validated against that eventual time boundary, exactly once.
                bool beforeTimeBoundary = (state.StructuralSettlementPolicy is not null || state.ReservoirSeals is not null)
                    && batch.Kind == WorldBatchKind.AdvancingStep && advancingSteps == state.AdvancingSteps;
                var validationPacing = beforeTimeBoundary ? pacing?.AdvanceExploration() : pacing;
                var validationLifecycle = beforeTimeBoundary ? state.EncounterLifecycle?.AdvanceExploration() : lifecycle;
                var validationCalendar = validationPacing?.Calendar
                    ?? (beforeTimeBoundary ? calendar.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep) : calendar);
                var validationResidents = beforeTimeBoundary ? state.Residents.Select(resident => resident.ExperienceStep()) : residents;
                ulong validationSteps = beforeTimeBoundary ? checked(advancingSteps + 1) : advancingSteps;
                var validationGathering = beforeTimeBoundary ? gathering?.AdvanceStep(validationPacing?.ProcessIntervals ?? validationSteps) : gathering;
                // The contained profile must join the accepted material candidate with its own
                // reconciled physical storage. Earlier profiles retain their exact interpreter.
                var validationReservoirs = state.ReservoirSeals is not null ? proposedReservoirs : reservoirs;
                var validationTransport = state.ReservoirSeals is not null ? proposedTransport : transport;
                // Action admission validates the eventual clock without speculatively flowing material.
                // Only the actual advancing boundary below selects and applies liquid transfers.
                var validationLiquidCells = beforeTimeBoundary && proposedLiquidCells is not null
                    ? proposedLiquidCells.AdvanceBoundary(state.EncounterLifecycle is null ? validationCalendar : validationPacing!.ProcessBoundary) : proposedLiquidCells;
                var admitted = state.WithState(proposedInventory, space, validationResidents, validationCalendar, sequence, validationSteps,
                    proposedDialogues, validationGathering, production, progression, quests, construction,
                    validationReservoirs, validationTransport, excavation, proposedPhysical, validationLiquidCells, validationPacing, encounterLifecycle: validationLifecycle, care: care, resourceHistory: resourceHistory, coordination: coordination);
                // The envelope check stays inside this admission: a root that cannot be encoded is
                // refused here. Its bytes belong to this admitted root alone.
                WorldCanonicalFrame? captured = state.ConsequencePolicy is not null || state.ReservoirSeals is not null
                    ? WorldCanonicalFrame.Capture(admitted) : null;
                admittedCandidate = admitted;
                admittedFrame = captured;
                return true;
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
            {
                return false;
            }
        }

        for (int actionIndex = 0; actionIndex < batch.Actions.Count; actionIndex++)
        {
            WorldActionCommand action = batch.Actions[actionIndex];
            if (materialPairs?.DefersAcceptance(action) == true)
            {
                // The offer's ordinal selects the atomic pair. Its actual recipient submission
                // receives its own outcome there, even when it occurs earlier in the action list.
                continue;
            }
            // Material validation covers the entire action, including its non-inventory side effects.
            var previousExcavation = excavation;
            var previousGathering = gathering;
            var previousProduction = production;
            var previousProgression = progression;
            var previousResourceHistory = resourceHistory;
            var previousCare = care;
            var previousCoordination = coordination;
            var previousQuests = quests;
            var previousConstruction = construction;
            var previousPhysical = physical;
            var previousSpace = space;
            var previousReservoirs = reservoirs;
            var previousTransport = transport;
            var previousLiquidCells = liquidCells;
            bool performedWork = false;
            WorldActionResolution resolution;
            IReadOnlyList<EntityId> createdIds = [];
            WorldMaterialPair? materialPair = null;
            WorldConsentedTransferEvidence? materialTransfer = null;
            WorldActionRejection recipientRejection = WorldActionRejection.Unavailable;
            WorldMaterialProposalSettlement? proposalSettlement = null;
            WorldCustodyWitness? paidWitness = null;
            EntityId? proposalCounterpart = null;
            if (action.Intent is MarketIntent)
            {
                if (coordination?.PaidExchange is not { } quotes || coordination.Markets is not { } markets
                    || production is null || progression is null)
                {
                    throw new InvalidOperationException("This world does not interpret local markets.");
                }
                (WorldPaidExchangePlan plan, WorldMarketBook learned) = WorldMarketResolver.Resolve(state, batch, inventory,
                    space, production, progression, quotes, markets, dialogues.Count != 0, action, checked((uint)actionIndex),
                    sequence, worked, Occupied);
                resolution = new(plan.Inventory, dialogues, plan.Rejection, null);
                if (plan.Rejection == WorldActionRejection.None)
                {
                    // Issuance records its market basis beside the paid quote; observation records knowledge.
                    // Neither moves goods or money nor performs work.
                    coordination = coordination.WithPaidExchange(plan.Book).WithMarkets(learned);
                    createdIds = plan.CreatedIds;
                }
            }
            else if (action.Intent is PaidExchangeIntent)
            {
                if (coordination?.PaidExchange is not { } quotes || production is null || progression is null)
                {
                    throw new InvalidOperationException("This world does not interpret paid spot exchange.");
                }
                // The local-market world keeps bilateral quotes, but a buy quote there may pin only a lot its
                // counterparty explicitly showed to the issuer; the paid world keeps its own interpretation.
                WorldMarketBook? shown = coordination.Markets;
                Func<SpotQuote, bool>? disclosed = shown is null ? null
                    : quote => shown.DisclosesTo(quote, null, state.AdvancingSteps);
                WorldPaidExchangePlan plan = WorldPaidExchangeResolver.Resolve(state, batch, inventory, space, production,
                    progression, quotes, dialogues.Count != 0, action, checked((uint)actionIndex), sequence, worked, Occupied,
                    disclosed);
                if (plan.Rejection == WorldActionRejection.None && action.Intent is AcceptSpotQuoteIntent accepted
                    && coordination.Markets is { } pricing && pricing.FindQuote(accepted.Acceptance.QuoteId) is { } priced
                    && !WorldMarketResolver.IsCurrent(priced, pricing, inventory, production, space))
                {
                    // A market quote settles only against the market state that priced it; nothing is requoted.
                    plan = new(inventory, production, progression, quotes, WorldActionRejection.RevisionConflict, [], null, null);
                }
                resolution = new(plan.Inventory, dialogues, plan.Rejection, null);
                if (plan.Rejection == WorldActionRejection.None)
                {
                    production = plan.Production;
                    progression = plan.Progression;
                    coordination = coordination.WithPaidExchange(plan.Book);
                    createdIds = plan.CreatedIds;
                    paidWitness = plan.Witness;
                    proposalCounterpart = plan.CounterpartActorId;
                    // Issuing and withdrawing declare terms; only a settled fill performs both participants'
                    // work. A fill's supersessions are closed inside the same paid owner candidate.
                    performedWork = plan.Witness is not null;
                }
            }
            else if (action.Intent is MaterialProposalIntent)
            {
                if (coordination?.Proposals is not { } book || production is null)
                {
                    throw new InvalidOperationException("This world does not interpret saved material proposals.");
                }
                WorldMaterialProposalPlan plan = WorldMaterialProposalResolver.Resolve(state, batch, inventory, space,
                    production, book, dialogues.Count != 0, action, checked((uint)actionIndex), sequence, worked, Occupied);
                resolution = new(plan.Inventory, dialogues, plan.Rejection, null);
                if (plan.Rejection == WorldActionRejection.None)
                {
                    production = plan.Production;
                    coordination = coordination.WithProposals(plan.Book);
                    createdIds = plan.CreatedIds;
                    proposalSettlement = plan.Settlement;
                    proposalCounterpart = plan.CounterpartActorId;
                    // Saving, declining and withdrawing declare; only a settlement performs work.
                    performedWork = plan.Settlement is not null;
                }
            }
            else if (action.Intent is OfferMaterialIntent or AcceptMaterialIntent)
            {
                if (coordination?.ConsentedMaterial is not { } history || production is null || materialPairs is null)
                {
                    throw new InvalidOperationException("This world does not interpret paired material transactions.");
                }
                materialPair = materialPairs.FindOffer(action);
                if (materialPair is null)
                {
                    resolution = new(inventory, dialogues, WorldMaterialPairing.UnmatchedRejection(state, action), null);
                }
                else
                {
                    bool MaterialOccupied(EntityId identity) => identity != materialPair.Offer.CommandId
                        && identity != materialPair.Acceptance.CommandId && Occupied(identity);
                    var plan = WorldConsentedMaterialPlanner.Decide(state, inventory, space, production,
                        dialogues.Count != 0, batch.Kind, materialPair.Offer, materialPair.OfferIndex,
                        materialPair.SenderDelivery, materialPair.Acceptance, materialPair.AcceptanceIndex,
                        materialPair.RecipientDelivery, sequence, MaterialOccupied);
                    resolution = new(plan.CandidateInventory, dialogues, plan.OfferRejection, null);
                    recipientRejection = plan.AcceptanceRejection;
                    if (plan.Evidence is { } evidence)
                    {
                        materialTransfer = evidence;
                        production = plan.CandidateProduction;
                        coordination = coordination.WithConsentedMaterial(history.Append(evidence));
                        if (evidence.MovedLotId != evidence.Approval.Command.Terms.LotId)
                        {
                            createdIds = [evidence.MovedLotId];
                        }
                        performedWork = true;
                    }
                }
            }
            else if (state.EncounterLifecycle is not null && !WorldActorAvailability.PermitsAction(state, action))
            {
                resolution = new(inventory, dialogues, WorldActionRejection.Unavailable, null);
            }
            else if (action.Intent is UseRestorativeItemIntent or RevokeCarePermissionIntent)
            {
                if (care?.Ordinary is null || production is null || progression is null || resourceHistory is null)
                    throw new InvalidOperationException("This saved profile does not interpret ordinary care.");
                var plan = _restorative.Resolve(state, inventory, space, production, progression, care, resourceHistory,
                    action, batch.Kind, dialogues.Count != 0, sequence, checked((uint)actionIndex), Occupied);
                resolution = new(plan.Inventory, dialogues, plan.Rejection, null);
                production = plan.Production;
                progression = plan.Progression;
                care = plan.Care;
                resourceHistory = plan.ResourceHistory;
                createdIds = plan.CreatedIds;
                performedWork = plan.Rejection == WorldActionRejection.None;
            }
            else if (action.Intent is ClimbIntent)
            {
                if (gathering is null || state.ProfileVersion is not 11 and not 12 and not 13 and not 14 and not 15 and not 16 and not 17 and not 18)
                {
                    throw new InvalidOperationException("This saved profile does not interpret assisted climbing.");
                }
                var plan = WorldClimbResolver.Resolve(inventory, space, gathering, action,
                    batch.Kind == WorldBatchKind.AdvancingStep, dialogues.Count != 0);
                space = plan.Space;
                gathering = plan.Gathering;
                resolution = new(inventory, dialogues, plan.Rejection, null);
                performedWork = plan.Rejection == WorldActionRejection.None;
            }
            else if (action.Intent is ExcavateIntent)
            {
                if (excavation is null || gathering is null || production is null)
                {
                    throw new InvalidOperationException("This saved profile does not interpret excavation.");
                }
                var plan = completedSettlement is not null
                    ? _excavation.PlanResolved(state, action, Occupied, completedSettlement)
                    : _excavation.Plan(state, inventory, space, gathering, production, excavation, action,
                        batch.Kind == WorldBatchKind.AdvancingStep, dialogues.Count != 0, Occupied, physical);
                resolution = new(plan.Inventory, dialogues, plan.Rejection, plan.SpawnedId);
                excavation = plan.Excavation;
                gathering = plan.Gathering;
                space = plan.Space;
                physical = plan.Physical;
                performedWork = plan.Succeeded;
            }
            else if (action.Intent is ScheduleRunoffTransportIntent)
            {
                if (transport is null)
                {
                    throw new InvalidOperationException("This saved profile does not interpret scheduled transport.");
                }
                var plan = _transport.Resolve(transport, space, action,
                    batch.Kind == WorldBatchKind.AdvancingStep, dialogues.Count != 0, Occupied);
                transport = plan.Candidate;
                createdIds = plan.CreatedIds;
                resolution = new WorldActionResolution(inventory, dialogues, plan.Rejection, null);
            }
            else if (action.Intent is RunoffCommitmentIntent)
            {
                if (reservoirs is null)
                {
                    throw new InvalidOperationException("This saved profile does not interpret runoff commitments.");
                }
                var plan = _runoff.Resolve(state, reservoirs, space, action,
                    batch.Kind == WorldBatchKind.AdvancingStep, dialogues.Count != 0);
                reservoirs = plan.Candidate;
                if (plan.Rejection == WorldActionRejection.None && transport is not null)
                {
                    transport = action.Intent is CancelRunoffIntent cancel
                        ? transport.PlanCancel(cancel.ReservationId).ApplyTo(transport)
                        : transport.PlanRebind(reservoirs).ApplyTo(transport);
                    reservoirs = transport.Reservoirs;
                }
                resolution = new WorldActionResolution(inventory, dialogues, plan.Rejection, null);
            }
            else if (action.Intent is WorkConstructionIntent)
            {
                if (construction is null || production is null)
                {
                    throw new InvalidOperationException("This saved profile does not interpret construction work.");
                }
                ConstructionPlan plan = _construction.Plan(state, inventory, production, construction, space,
                    action, batch.Kind == WorldBatchKind.AdvancingStep, dialogues.Count != 0, Occupied);
                resolution = new WorldActionResolution(plan.CandidateInventory, dialogues, plan.Rejection, null);
                production = plan.CandidateProduction;
                construction = plan.CandidateConstruction;
                space = plan.CandidateSpace;
                createdIds = plan.CreatedIds;
                if (plan.Succeeded)
                {
                    performedWork = true;
                }
            }
            else if (action.Intent is DeliveryQuestIntent)
            {
                if (quests is null || progression is null || production is null)
                {
                    throw new InvalidOperationException("This saved profile does not interpret quest actions.");
                }
                DeliveryQuestPlan plan = _quests.Plan(state, inventory, production, progression, quests,
                    action, dialogues, identity => identity != action.CommandId && Occupied(identity), space);
                resolution = new WorldActionResolution(plan.CandidateInventory, dialogues, plan.Rejection, null);
                progression = plan.CandidateProgression;
                quests = plan.CandidateQuests;
                createdIds = plan.CreatedIds;
                if (plan.Rejection == WorldActionRejection.None && action.Intent is CompleteDeliveryQuestIntent completed
                    && resourceHistory is not null)
                {
                    var reward = quests.FindDefinition(completed.QuestId)!.Reward;
                    var cause = new ResourceChangeCause(action.CommandId, reward.DefinitionId, reward.Version);
                    var change = new ActorResourceChange(0, cause, ActorResourceOperation.ExperienceReward,
                        reward.Experience, previousProgression!.GetActor(action.ActorId), progression.GetActor(action.ActorId));
                    resourceHistory = resourceHistory.Append(new(sequence, checked((uint)actionIndex), action.CommandId, [change]),
                        new WorldRewardResourceEvidence(action.CommandId));
                }
            }
            else if (action.Intent is GatherIntent)
            {
                if (gathering is null)
                {
                    throw new InvalidOperationException("This saved profile does not interpret gathering actions.");
                }
                GatherPlan? plan = dialogues.Count == 0 ? _gathering.Plan(state, inventory, gathering, action, space) : null;
                if (plan is null || plan.SpawnedId is { } generated && Occupied(generated)
                    || !FitsProduction(production, plan.CandidateInventory))
                {
                    resolution = new WorldActionResolution(inventory, dialogues, WorldActionRejection.Unavailable, null);
                }
                else
                {
                    resolution = new WorldActionResolution(plan.CandidateInventory, dialogues, plan.Rejection, plan.SpawnedId);
                    gathering = plan.CandidateGathering;
                }
            }
            else if (action.Intent is CraftIntent)
            {
                if (production is null)
                {
                    throw new InvalidOperationException("This saved profile does not interpret crafting actions.");
                }
                CraftPlan plan = _crafting.Resolve(state, inventory, production, action, dialogues.Count != 0, Occupied, space);
                resolution = new WorldActionResolution(plan.CandidateInventory, dialogues, plan.Rejection, null);
                production = plan.CandidateProduction;
                createdIds = plan.CreatedIds;
            }
            else
            {
                resolution = _actions.Resolve(state, inventory, dialogues, action, Occupied, space);
                if (!FitsProduction(production, resolution.Inventory))
                {
                    resolution = new WorldActionResolution(inventory, dialogues, WorldActionRejection.Unavailable, null);
                }
            }
            if (!TryReconcilePhysical(physical, construction, space, createdIds, Occupied, out var reconciledPhysical, out var physicalIds)
                || !WorldReservoirBinding.TryReconcile(reservoirs, resolution.Inventory, production, out var reconciled,
                    materialTransfer, proposalSettlement?.Witness ?? paidWitness)
                || !WorldTransportBinding.TryReconcile(transport, reconciled, out var reconciledTransport)
                || !WorldLiquidCellBinding.TryReconcile(liquidCells, reconciled, out var reconciledLiquidCells, space, state.ReservoirSeals)
                || !FitsCompleteCandidate(resolution.Inventory, resolution.Dialogues, reconciledPhysical,
                    reconciled, reconciledTransport, reconciledLiquidCells, out _, out _))
            {
                excavation = previousExcavation;
                gathering = previousGathering;
                production = previousProduction;
                progression = previousProgression;
                resourceHistory = previousResourceHistory;
                care = previousCare;
                coordination = previousCoordination;
                quests = previousQuests;
                construction = previousConstruction;
                physical = previousPhysical;
                space = previousSpace;
                reservoirs = previousReservoirs;
                transport = previousTransport;
                liquidCells = previousLiquidCells;
                performedWork = false;
                recipientRejection = WorldActionRejection.Unavailable;
                createdIds = [];
                resolution = new WorldActionResolution(inventory, dialogues, WorldActionRejection.Unavailable, null);
            }
            else
            {
                physical = reconciledPhysical;
                createdIds = physicalIds;
                reservoirs = reconciled;
                transport = reconciledTransport;
                liquidCells = reconciledLiquidCells;
            }
            if (materialEvidence is not null && resolution.Rejection == WorldActionRejection.None)
            {
                // Observed after complete admission and before any end-of-step process; a refusal
                // or rollback above already restored the running candidates and reports nothing.
                materialEvidence.Capture(actionIndex, action,
                    new(inventory, previousProduction, previousGathering, previousExcavation, previousConstruction,
                        previousSpace, previousQuests, previousProgression, previousCare, previousReservoirs, previousLiquidCells),
                    new(resolution.Inventory, production, gathering, excavation, construction, space, quests,
                        progression, care, reservoirs, liquidCells), resolution.SpawnedId, createdIds);
            }
            if (performedWork)
            {
                worked.Add(action.ActorId);
                if (materialPair is not null)
                {
                    worked.Add(materialPair.Acceptance.ActorId);
                }
                if (proposalCounterpart is { } counterpart)
                {
                    // The absent sender's or issuer's opportunity of this step is spent by its settlement.
                    worked.Add(counterpart);
                }
                foreach (ActorPresence actor in space.Actors)
                {
                    if (actor.Feet != previousSpace.GetActor(actor.ActorId).Feet)
                    {
                        // Coupled displacement consumes this actor's selected movement opportunity.
                        worked.Add(actor.ActorId);
                    }
                }
            }
            var receipt = WorldActionReceipt.Create(action, sequence, resolution.Rejection);
            Reserve(receipt.ResultId, WorldCommittedIdentityKind.Result);
            if (materialPair is not null)
            {
                var acceptedReceipt = WorldActionReceipt.Create(materialPair.Acceptance, sequence, recipientRejection);
                Reserve(acceptedReceipt.ResultId, WorldCommittedIdentityKind.Result);
                receipts.Add(acceptedReceipt);
            }
            if (resolution.SpawnedId is { } spawned)
            {
                Reserve(spawned, WorldCommittedIdentityKind.SpawnedEntity);
            }
            foreach (EntityId created in createdIds)
            {
                Reserve(created, WorldCommittedIdentityKind.SpawnedEntity);
            }
            inventory = resolution.Inventory;
            dialogues = resolution.Dialogues;
            receipts.Add(receipt);
        }

        WorldSnapshot? admittedMovement = null;
        // Replaced together with the admitted movement root, so it never describes an earlier one.
        WorldCanonicalFrame? admittedMovementFrame = null;
        if (batch.Kind == WorldBatchKind.AdvancingStep)
        {
            // The already selected step contributes once, including when an action opens a hold.
            pacing = pacing?.AdvanceExploration();
            lifecycle = state.EncounterLifecycle?.AdvanceExploration();
            calendar = pacing?.Calendar ?? calendar.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep);
            advancingSteps = checked(advancingSteps + 1);
            if (coordination?.Proposals is { } pendingProposals)
            {
                // Every action of this step preceded the boundary; offers due at the new count close here.
                coordination = coordination.WithProposals(pendingProposals.ExpireDue(advancingSteps, sequence));
            }
            if (coordination?.PaidExchange is { } pendingQuotes)
            {
                // Quotes follow the same fixed-world rule: a quote due at the new count closes here.
                coordination = coordination.WithPaidExchange(pendingQuotes.ExpireDue(advancingSteps, sequence));
            }
            if (transport is not null)
            {
                var stages = transport.PlanAdvance(calendar, dialogues.Count != 0, Occupied);
                transport = stages.ApplyTo(transport);
                reservoirs = transport.Reservoirs;
                inventory = reservoirs.Capacity.Inventory;
                foreach (var identity in stages.CreatedIds)
                {
                    Reserve(identity, WorldCommittedIdentityKind.SpawnedEntity);
                }
            }
            gathering = gathering?.AdvanceStep(pacing?.ProcessIntervals ?? advancingSteps);
            residents = state.Residents.Select(resident => resident.ExperienceStep());
            if (liquidCells is not null)
            {
                // A failed bounded plan aborts the whole selected step; no partial flow or time is published.
                liquidCells = _liquidFlow.PlanAdvance(liquidCells, state.EncounterLifecycle is null ? calendar : pacing!.ProcessBoundary, dialogues.Count != 0).ApplyTo(liquidCells);
            }
            if (dialogues.Count == 0)
            {
                foreach (WorldMovementInput input in batch.Movement)
                {
                    if (worked.Contains(input.ActorId) || !inventory.CanControl(input.Delivery, input.ActorId)
                        || !WorldActorAvailability.CanAct(state, input.ActorId))
                    {
                        continue;
                    }
                    ActorPresence actor = space.GetActor(input.ActorId);
                    SpatialMoveResult move = space.PrepareMove(input.ActorId, actor.Revision, input.DeltaX, input.DeltaY);
                    if (move.Status == SpatialMoveStatus.Prepared)
                    {
                        LocalSpatialSnapshot previousSpace = space;
                        space = space.WithActors(space.Actors.Select(other => other.ActorId == input.ActorId ? move.Candidate! : other));
                        if (FitsCompleteCandidate(inventory, dialogues, physical, reservoirs, transport, liquidCells,
                            out WorldSnapshot? admitted, out WorldCanonicalFrame? admittedFrame))
                        {
                            // Only movement runs after the actual time boundary and all action effects.
                            // Later rejected moves restore this same space; successful moves replace it.
                            admittedMovement = admitted;
                            admittedMovementFrame = admittedFrame;
                        }
                        else
                        {
                            space = previousSpace;
                        }
                    }
                }
            }
        }
        WorldSnapshot candidate = admittedMovement ?? state.WithState(inventory, space, residents, calendar, sequence,
            advancingSteps, dialogues, gathering, production, progression, quests, construction, reservoirs, transport, excavation, physical, liquidCells, pacing, encounterLifecycle: lifecycle, care: care, resourceHistory: resourceHistory, coordination: coordination);
        // Only the frame captured for the exact reused movement root accompanies it. A candidate
        // that was never encoded here carries none and is encoded by its durable owner.
        WorldCanonicalFrame? candidateFrame = admittedMovement is null ? null : admittedMovementFrame;
        if (liquidCells is not null && admittedMovement is null)
        {
            // Fragment growth must also fit the complete retained root before authority preparation succeeds.
            // A reused movement candidate already passed this exact encoding boundary.
            candidateFrame = WorldCanonicalFrame.Capture(candidate);
        }
        return new WorldStepDecision(candidate, receipts, identities, materialEvidence: materialEvidence?.Snapshot(),
            candidateFrame: candidateFrame);
    }

    private static bool TryReconcilePhysical(ConstructionPhysicalSnapshot? physical, ConstructionSnapshot? construction, LocalSpatialSnapshot space,
        IReadOnlyList<EntityId> createdIds, Func<EntityId, bool> occupied,
        out ConstructionPhysicalSnapshot? candidate, out IReadOnlyList<EntityId> combinedIds)
    {
        candidate = physical;
        combinedIds = createdIds;
        if (physical is null) { return true; }
        try
        {
            candidate = physical.ReconcileCompletions(construction
                ?? throw new ArgumentException("Physical construction cannot lose its fabrication history."));
            candidate.ValidateStableGeometry(space);
            var prior = physical.Members.Select(value => value.ElementId).ToHashSet();
            EntityId[] added = candidate.Members.Select(value => value.ElementId).Where(value => !prior.Contains(value)).ToArray();
            EntityId[] all = createdIds.Concat(added).ToArray();
            if (added.Any(occupied) || all.Distinct().Count() != all.Length)
            {
                return false;
            }
            combinedIds = all;
            return true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            return false;
        }
    }

    private static bool FitsProduction(ProductionSnapshot? production, CountedAuthorityState inventory)
    {
        try
        {
            production?.ValidateForAuthority(inventory);
            return true;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            // A material candidate can exceed combined capacity although its raw subset still fits.
            return false;
        }
    }
}
