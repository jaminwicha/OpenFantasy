using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Progression.ResourceHistory;

namespace OpenFantasy.Core.Worlds;

/// <summary>Publishes recovery acknowledgment and finite helper care through the complete-world boundary.</summary>
internal static class WorldCareProcessor
{
    internal static WorldStepDecision Decide(WorldSnapshot world, WorldStepBatch batch, Func<EntityId, bool>? historicallyOccupied,
        WorldMaterialEvidenceCollector? materialEvidence = null)
    {
        if (world.Care is null || world.HasReadingHold || world.PendingExcavation is not null)
            throw new InvalidOperationException("Care requires its available complete-world owner.");
        ulong sequence = checked(world.Revision + 1);
        HashSet<EntityId> reserved = [];
        List<WorldCommittedIdentity> identities = [];
        bool Occupied(EntityId identity) => world.IdentityIndex.Contains(identity) || reserved.Contains(identity)
            || historicallyOccupied?.Invoke(identity) == true;
        void Reserve(EntityId identity, WorldCommittedIdentityKind kind)
        {
            if (Occupied(identity)) throw new InvalidOperationException("The selected care identity is already occupied.");
            reserved.Add(identity);
            identities.Add(new(identity, kind));
        }
        Reserve(batch.CauseId, WorldCommittedIdentityKind.CommitCause);
        var command = batch.Actions.Single();
        Reserve(command.CommandId, WorldCommittedIdentityKind.Command);
        var rejection = ValidateControl(world, command);
        if (rejection == WorldActionRejection.None)
        {
            rejection = command.Intent switch
            {
                AcknowledgeCareIntent acknowledge => WorldCareActions.EvaluateAcknowledgment(world, command.ActorId, acknowledge),
                AdministerRemedyIntent administer => WorldCareActions.EvaluateRemedy(world, command.ActorId, administer),
                PendingCareDispositionIntent disposition => WorldCareActions.EvaluateDisposition(world, command.ActorId, disposition),
                _ => throw new InvalidOperationException("Unknown care intent.")
            };
        }
        WorldSnapshot? candidate = null;
        // Assigned and cleared together with the candidate; a refused root leaves no frame behind.
        WorldCanonicalFrame? candidateFrame = null;
        EntityId[] created = command.Intent is AcknowledgeCareIntent or PendingCareDispositionIntent
            ? [EntityId.DeriveSpawned(command.CommandId, 0)]
            : [EntityId.DeriveSpawned(command.CommandId, 0), EntityId.DeriveSpawned(command.CommandId, 1)];
        if (rejection == WorldActionRejection.None)
        {
            if (created.Distinct().Count() != created.Length || created.Any(Occupied)) rejection = WorldActionRejection.Unavailable;
            else
            {
                try
                {
                    (candidate, rejection) = command.Intent switch
                    {
                        AcknowledgeCareIntent acknowledge => (Acknowledge(world, command, acknowledge, sequence), WorldActionRejection.None),
                        PendingCareDispositionIntent disposition => (CloseRequest(world, command, disposition, sequence), WorldActionRejection.None),
                        AdministerRemedyIntent administer => Administer(world, command, administer, sequence,
                            world.Production!.IsContinued ? identity => identity != command.CommandId && Occupied(identity) : null),
                        _ => throw new InvalidOperationException("Unknown care intent.")
                    };
                    if (candidate is not null) candidateFrame = WorldCanonicalFrame.Capture(candidate);
                }
                catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
                {
                    // No component is published until the entire candidate, including material and
                    // geometry joins, is admitted and fits the canonical durable envelope.
                    candidate = null;
                    candidateFrame = null;
                    rejection = WorldActionRejection.Unavailable;
                }
            }
        }
        if (candidate is null)
        {
            candidate = Copy(world, sequence, world.Care, world.Inventory, world.Production!, world.Progression!,
                world.EncounterLifecycle!.RetainBoundary(), world.Pacing!, world.Reservoirs!, world.LiquidCells!);
            candidateFrame = WorldCanonicalFrame.Capture(candidate);
        }
        else
        {
            foreach (var identity in created) Reserve(identity, WorldCommittedIdentityKind.SpawnedEntity);
            // Only the administered dose is material. The admitted, encoded candidate is observed
            // as prepared above; acknowledgment and every refusal report nothing.
            if (command.Intent is AdministerRemedyIntent)
                materialEvidence?.Capture(0, command, WorldMaterialEvidenceOwners.Of(world),
                    WorldMaterialEvidenceOwners.Of(candidate), null, created);
        }
        var receipt = WorldActionReceipt.Create(command, sequence, rejection);
        Reserve(receipt.ResultId, WorldCommittedIdentityKind.Result);
        return new(candidate, [receipt], identities, materialEvidence: materialEvidence?.Snapshot(),
            candidateFrame: candidateFrame);
    }

    private static WorldActionRejection ValidateControl(WorldSnapshot world, WorldActionCommand command)
    {
        if (WorldActorCommandAuthority.AuthorizeSelection(world, command) is null)
            return WorldActionRejection.NotAuthorized;
        return world.Space.GetActor(command.ActorId).Revision != command.ExpectedActorRevision
            ? WorldActionRejection.RevisionConflict : WorldActionRejection.None;
    }

    private static WorldSnapshot Acknowledge(WorldSnapshot world, WorldActionCommand command,
        AcknowledgeCareIntent intent, ulong sequence)
    {
        var acknowledgment = new CareAcknowledgment(EntityId.DeriveSpawned(command.CommandId, 0), command.CommandId,
            intent.AgreementId, intent.EncounterId, sequence, world.EncounterActions!.ContentHash,
            world.Pacing!.Encounter!.ContentHash, world.EncounterActions.LastResolution!);
        return Copy(world, sequence, world.Care!.Acknowledge(acknowledgment), world.Inventory, world.Production!,
            world.Progression!, world.EncounterLifecycle!.RetainBoundary(), world.Pacing, world.Reservoirs!, world.LiquidCells!);
    }

    private static WorldSnapshot CloseRequest(WorldSnapshot world, WorldActionCommand command,
        PendingCareDispositionIntent intent, ulong sequence)
    {
        var disposition = new CareDisposition(EntityId.DeriveSpawned(command.CommandId, 0), command.CommandId,
            intent.AcknowledgmentId, command.ActorId, intent is CancelPendingCareIntent
                ? CareDispositionKind.PatientCancelled : CareDispositionKind.HelperDeclined, sequence);
        return Copy(world, sequence, world.Care!.CloseRequest(disposition), world.Inventory, world.Production!,
            world.Progression!, world.EncounterLifecycle!.RetainBoundary(), world.Pacing!, world.Reservoirs!, world.LiquidCells!);
    }

    private static (WorldSnapshot? Candidate, WorldActionRejection Rejection) Administer(WorldSnapshot world,
        WorldActionCommand command, AdministerRemedyIntent intent, ulong sequence, Func<EntityId, bool>? occupied)
    {
        var care = world.Care!;
        var acknowledgment = care.Pending!;
        var agreement = care.GetAgreement(acknowledgment.AgreementId);
        var remedy = care.GetRemedy(agreement.RemedyDefinitionId);
        var patient = world.Progression!.GetActor(agreement.PatientActorId);
        var restoration = new RestorativeItemPlanner().Plan(world.Inventory, world.Production!, world.Progression,
            new(command.PrincipalId, command.OriginSessionId), command.ActorId, command.CommandId,
            new(world.GetResident(command.ActorId).InventoryCustodianId, intent.ExpectedStorageRevision,
                intent.ExpectedProductionRevision, agreement.PatientActorId, remedy.Item, intent.Ingredient),
            intent.ExpectedPatientProgressionRevision, remedy.RestoredHp, occupied,
            WorldActorCommandAuthority.AuthorizeSelection(world, command));
        if (restoration.Rejection != WorldActionRejection.None) return (null, restoration.Rejection);
        int hp = restoration.ActorTransition!.Candidate.CurrentHp;
        var basisTiming = world.Pacing!.Encounter!;
        var helper = basisTiming.GetActor(command.ActorId);
        var timing = new EncounterTimingSnapshot(basisTiming.Scope, checked(basisTiming.Revision + 1), basisTiming.ElapsedTacticalUnits,
            basisTiming.Actors.Select(actor => actor.ActorId == command.ActorId ? actor.WithDelay(helper.StandardDelay) : actor), []);
        var treatment = new CareTreatment(EntityId.DeriveSpawned(command.CommandId, 1), command.CommandId,
            acknowledgment.AcknowledgmentId, restoration.Claim!.ConsumptionId, sequence, timing.Revision,
            patient.Revision, patient.Stats.MaxHp, hp, helper.NormalizedSpeed, helper.StandardDelay);
        if (!WorldReservoirBinding.TryReconcile(world.Reservoirs, restoration.CandidateInventory, restoration.CandidateProduction, out var reservoirs)
            || !WorldLiquidCellBinding.TryReconcile(world.LiquidCells, reservoirs, out var liquids, world.Space, world.ReservoirSeals))
            return (null, WorldActionRejection.Unavailable);
        var change = ActorResourceChange.FromTransition(0, new(treatment.TreatmentId, remedy.DefinitionId, remedy.Version),
            restoration.ActorTransition);
        var history = world.ResourceHistory!.Append(new(sequence, 0, command.CommandId, [change]),
            new WorldTreatmentResourceEvidence(command.CommandId, treatment.TreatmentId));
        // Treatment resumes the same unfinalized episode; in profile 35 earlier outcome readings described an outcome
        // that no longer exists, so they end here and cannot complete a later outcome.
        return (Copy(world, sequence, care.Treat(treatment), restoration.CandidateInventory, restoration.CandidateProduction,
            restoration.CandidateProgression, world.EncounterLifecycle!.ResumeAfterCare(timing), world.Pacing.ObserveEncounter(timing), reservoirs!, liquids!, history,
            world.EncounterOutcome?.ClearAcknowledgments()),
            WorldActionRejection.None);
    }

    private static WorldSnapshot Copy(WorldSnapshot world, ulong sequence, CareSnapshot care, CountedAuthorityState inventory,
        ProductionSnapshot production, ProgressionSnapshot progression, EncounterLifecycleSnapshot lifecycle,
        WorldPacingState pacing, Geography.LiquidReservoirState reservoirs, Geography.LiquidCellState liquids,
        WorldResourceHistory? history = null, EncounterOutcomeState? outcome = null) =>
        world.WithState(inventory, world.Space, world.Residents, world.Calendar, sequence, world.AdvancingSteps,
            world.Dialogues, world.Gathering, production, progression, world.Quests, world.Construction, reservoirs, world.Transport,
            world.Excavation, world.ConstructionPhysical, liquids, pacing, encounterLifecycle: lifecycle, care: care, resourceHistory: history,
            encounterOutcome: outcome);
}
