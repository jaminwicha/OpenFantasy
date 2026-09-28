using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Worlds;

/// <summary>Joins finite restorative treatment to real material, resource, clock and encounter owners.</summary>
public static class WorldCareAdmission
{
    public static WorldSnapshot CreateFresh(WorldSnapshot basis, CareSnapshot care)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(care);
        if (basis.ProfileVersion != 24 || basis.Revision != 0 || basis.AdvancingSteps != 0
            || basis.EncounterLifecycle!.EncounterCount != 0 || care.Revision != 0
            || basis.Production!.Revision != 0 || basis.Production.Batches.Count != 0 || basis.Production.Lots.Count != 0)
            throw new ArgumentException("Care admission requires an entirely fresh unearned lifecycle and material basis.");
        var production = new ProductionSnapshot(basis.WorldId, basis.BranchId, 0, basis.Production.Recipes, [], [], []);
        var oldCapacity = basis.Reservoirs!.Capacity;
        var pending = oldCapacity.Pending.Select(value =>
        {
            var request = value.Request;
            return new WatershedRunoffPlanner().Prepare(new(request.Network, request.Mapping, basis.Inventory,
                production, request.ReservationId, request.OwnerActorId, request.Start, request.End,
                request.Material, request.Inputs, request.Terminals));
        });
        var capacity = new WatershedRunoffCapacityState(oldCapacity.Domain, basis.Inventory, production,
            oldCapacity.Revision, pending, oldCapacity.Closed);
        var reservoirs = LiquidReservoirState.Create(capacity, basis.Reservoirs.Geometries);
        if (!WorldLiquidCellBinding.TryReconcile(basis.LiquidCells, reservoirs, out var liquids, basis.Space, basis.ReservoirSeals))
            throw new ArgumentException("Fresh care must preserve exact terrain and liquid material placement.");
        var admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.care-admission.v1",
            basis.ContentHash.ToByteArray(), care.ContentHash.ToByteArray()));
        var world = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: liquids, excavation: basis.Excavation,
            pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle, care: care,
            resourceHistory: WorldResourceHistory.CreateFresh(admission, basis.Progression!));
        _ = WorldSnapshotCodec.Encode(world);
        return world;
    }

    internal static void Validate(WorldSnapshot world)
    {
        if (world.Care is not { } care) return;
        if (world.ProfileVersion is not 25 and not 26 and not 27 || world.BaseProfileVersion != 21 || world.EncounterLifecycle is not { } lifecycle
            || world.Production is not { SupportsConsumption: true } production || care.WorldId != world.WorldId
            || care.BranchId != world.BranchId || !care.SupportsOrdinary && care.Revision > world.Revision
            || care.Treatments.Count + (care.Ordinary?.Uses.Count ?? 0) != production.ConsumedItems.Count)
            throw new ArgumentException("Care requires a complete enabled lifecycle/material owner in its exact world scope.");
        WorldRestorativeAdmission.Validate(world);
        foreach (var agreement in care.Agreements)
        {
            if (!world.Inventory.Actors.Contains(agreement.PatientActorId) || !world.Inventory.Actors.Contains(agreement.HelperActorId))
                throw new ArgumentException("Care participants must remain actual admitted actors.");
            var scenarios = lifecycle.Scenarios.Where(value => value.InitiatingActorId == agreement.PatientActorId).ToArray();
            if (scenarios.Length == 0 || scenarios.Any(value => value.Combatants.Single(actor => actor.ActorId == agreement.HelperActorId).Side != 0))
                throw new ArgumentException("Aftermath care requires a neutral helper outside its patient's combat side.");
        }
        foreach (var acknowledgment in care.Acknowledgments)
        {
            var agreement = care.GetAgreement(acknowledgment.AgreementId);
            var defeat = acknowledgment.Defeat;
            var scenario = lifecycle.Scenarios.SingleOrDefault(value =>
                EncounterEpisodeIdentity.Family(lifecycle, value.DefinitionId).High == acknowledgment.EncounterId.High);
            if (acknowledgment.WorldSequence > world.Revision
                || scenario is null || scenario.InitiatingActorId != agreement.PatientActorId
                || (EncounterEpisodeIdentity.Family(lifecycle, scenario.DefinitionId).Low ^ acknowledgment.EncounterId.Low) >= lifecycle.EncounterCount)
                throw new ArgumentException("Acknowledged defeat requires a previously entered episode in its patient's exact scenario family.");
            var attacker = scenario.Combatants.Single(value => value.ActorId == defeat.ActorId);
            var patient = scenario.Combatants.Single(value => value.ActorId == agreement.PatientActorId);
            if (attacker.Side == 0 || patient.Side == 0 || attacker.Side == patient.Side
                || !attacker.AbilityIds.Contains(defeat.AbilityDefinitionId!.Value))
                throw new ArgumentException("The witnessed lethal action requires an admitted opposing combatant and actual ability grant.");
        }
        foreach (var treatment in care.Treatments)
        {
            var acknowledgment = care.GetAcknowledgment(treatment.AcknowledgmentId);
            var agreement = care.GetAgreement(acknowledgment.AgreementId);
            var remedy = care.GetRemedy(agreement.RemedyDefinitionId);
            var claim = production.ConsumedItems.SingleOrDefault(value => value.ConsumptionId == treatment.ConsumptionId);
            var patient = world.Progression!.GetActor(agreement.PatientActorId);
            if (treatment.WorldSequence > world.Revision || claim is null || claim.CommandId != treatment.CommandId
                || claim.ActorId != agreement.HelperActorId || claim.RecipientActorId != agreement.PatientActorId
                || claim.CustodianId != world.GetResident(agreement.HelperActorId).InventoryCustodianId
                || claim.Ingredient.Definition != remedy.Item || claim.Ingredient.Quantity != 1
                || patient.Revision < treatment.PatientRevisionAfter
                || patient.Revision == treatment.PatientRevisionAfter && patient.CurrentHp != treatment.HpAfter)
                throw new ArgumentException("A treatment requires its exact single consumed dose, participants and ordered resource witness.");
            if (world.Pacing!.Encounter is { } timing && timing.EncounterId == acknowledgment.EncounterId
                && timing.Revision == treatment.TimingRevision)
            {
                var helper = timing.GetActor(agreement.HelperActorId);
                if (helper.NormalizedSpeed != treatment.HelperNormalizedSpeed || helper.RemainingDelay != treatment.HelperDelay
                    || timing.Decisions.Count != 0 || lifecycle.Phase != EncounterLifecyclePhase.Active)
                    throw new ArgumentException("The treatment boundary must retain its actual helper debt and resume without old choices.");
            }
        }
        if (care.Pending is { } pending)
        {
            var agreement = care.GetAgreement(pending.AgreementId);
            if (lifecycle.Phase != EncounterLifecyclePhase.Outcome || lifecycle.Actions!.Scope.EncounterId != pending.EncounterId
                || lifecycle.Actions.ContentHash != pending.ActionHash || world.Pacing!.Encounter!.ContentHash != pending.TimingHash
                || world.Progression!.GetActor(agreement.PatientActorId).CurrentHp != 0
                || lifecycle.Actions.LastResolution is not { } last
                || !EncounterActionCodecV1.EncodeResolution(last).AsSpan().SequenceEqual(EncounterActionCodecV1.EncodeResolution(pending.Defeat)))
                throw new ArgumentException("Pending care retains the exact acknowledged incapacitation and unadvanced encounter.");
        }
    }

}
