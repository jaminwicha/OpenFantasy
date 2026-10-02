using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>Admits explicit ordinary policies and joins their actual material and resource witnesses.</summary>
public static class WorldRestorativeAdmission
{
    public static WorldSnapshot CreateFresh(WorldSnapshot basis, OrdinaryCareSnapshot ordinary)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(ordinary);
        if (basis.ProfileVersion != 25 || basis.Revision != 0 || basis.AdvancingSteps != 0
            || basis.Care!.Revision != 0 || ordinary.Revision != 0 || basis.EncounterLifecycle!.EncounterCount != 0
            || basis.Production!.Revision != 0 || basis.Progression!.Revision != 0
            || basis.ResourceHistory!.ArchiveAnchor is not null || basis.ResourceHistory.History.Batches.Count != 0)
            throw new ArgumentException("Ordinary admission requires a fresh unearned care basis and authored permissions.");
        var care = new CareSnapshot(basis.WorldId, basis.BranchId, 0, basis.Care.Remedies, basis.Care.Agreements, [], [], ordinary);
        var admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.restorative-admission.v1",
            basis.ContentHash.ToByteArray(), care.ContentHash.ToByteArray()));
        var world = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
            care: care, resourceHistory: WorldResourceHistory.CreateFresh(admission, basis.Progression));
        _ = WorldSnapshotCodec.Encode(world);
        return world;
    }

    internal static void Validate(WorldSnapshot world)
    {
        if (world.Care?.Ordinary is not { } ordinary) return;
        if (world.ProfileVersion is not 26 and not 27 and not 28 and not 30 and not 32 and not 33 and not 34 and not 35 and not 36 and not 37) throw new ArgumentException("Ordinary care requires its explicit world interpretation.");
        foreach (var definition in ordinary.Definitions)
        {
            foreach (var form in definition.SupportedForms)
            {
                if (!world.Space.Actors.Any(actor => actor.Binding.Kind == ActorFormKind.Corporeal
                    && actor.Binding.DefinitionId == form.DefinitionId && actor.Binding.DefinitionVersion == form.Version))
                    throw new ArgumentException("Ordinary delivery must pin an admitted corporeal form.");
            }
        }
        foreach (var permission in ordinary.Permissions)
        {
            if (!world.Inventory.Actors.Contains(permission.PatientActorId) || !world.Inventory.Actors.Contains(permission.HelperActorId))
                throw new ArgumentException("An ordinary permission must name actual admitted participants.");
            if (permission.Revocation is { } revoke && revoke.WorldSequence > world.Revision)
                throw new ArgumentException("A revoked permission must retain its accepted world boundary.");
        }
        HashSet<EntityId> claims = world.Care.Treatments.Select(value => value.ConsumptionId).ToHashSet();
        foreach (var use in ordinary.Uses)
        {
            var definition = ordinary.GetDefinition(use.UseDefinitionId);
            var remedy = world.Care.GetRemedy(definition.RemedyDefinitionId);
            var claim = world.Production!.ConsumedItems.SingleOrDefault(value => value.ConsumptionId == use.ConsumptionId);
            var patient = world.Progression!.GetActor(use.RecipientActorId);
            if (!claims.Add(use.ConsumptionId) || use.WorldSequence > world.Revision || claim is null
                || claim.CommandId != use.CommandId || claim.ActorId != use.ActorId || claim.RecipientActorId != use.RecipientActorId
                || claim.CustodianId != world.GetResident(use.ActorId).InventoryCustodianId
                || claim.Ingredient.Definition != remedy.Item || claim.Ingredient.Quantity != 1
                || use.HpAfter != Math.Min((long)use.MaximumHp, (long)use.HpBefore + remedy.RestoredHp)
                || patient.Revision < use.PatientRevisionAfter
                || patient.Revision == use.PatientRevisionAfter && patient.CurrentHp != use.HpAfter)
                throw new ArgumentException("Ordinary use requires its exact disjoint dose and capped historical resource boundary.");
        }
        if (!claims.SetEquals(world.Production!.ConsumedItems.Select(value => value.ConsumptionId)))
            throw new ArgumentException("Every consumed dose must belong to exactly one named care operation.");
    }
}
