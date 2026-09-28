using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Care;

/// <summary>
/// Composes one exact dose with a caller-admitted health restoration amount. This is a private
/// domain planner, not a public treatment command or proof of medical permission/content admission.
/// </summary>
public sealed class RestorativeItemPlanner
{
    public RestorativeItemPlan Plan(CountedAuthorityState inventory, ProductionSnapshot production,
        ProgressionSnapshot progression, TrustedCommandDelivery delivery, EntityId actorId, EntityId commandId,
        ConsumeItemRequest dose, ulong expectedPatientRevision, uint restoredHp)
    {
        ArgumentNullException.ThrowIfNull(progression);
        ArgumentNullException.ThrowIfNull(production);
        if (production.IsContinued)
        {
            throw new ArgumentException("Restorative use requires coordinated material and care continuation.", nameof(production));
        }
        RestorativeItemPlan Reject(WorldActionRejection reason) => new(inventory, production, progression, reason);
        // Material admission authenticates the supplier before any patient state is inspected.
        var consumption = new ItemConsumptionPlanner().Plan(inventory, production, delivery, actorId, commandId, dose);
        if (consumption.Rejection != WorldActionRejection.None)
        {
            return Reject(consumption.Rejection);
        }
        if (progression.WorldId != inventory.WorldId || progression.BranchId != inventory.BranchId)
        {
            throw new ArgumentException("Treatment resources and material must belong to the same world and branch.");
        }
        progression.ValidateActorSet(inventory.Actors);
        var patient = progression.GetActor(dose.RecipientActorId);
        if (patient.Revision != expectedPatientRevision)
        {
            return Reject(WorldActionRejection.RevisionConflict);
        }
        if (restoredHp == 0 || dose.Ingredient.Quantity != 1 || patient.CurrentHp == patient.Stats.MaxHp)
        {
            // This instantaneous remedy refuses a no-effect use before publishing any expenditure.
            return Reject(WorldActionRejection.Unavailable);
        }
        try
        {
            var restoration = patient.RestoreHealth(restoredHp);
            var candidate = progression.WithResourceTransition(restoration);
            return new(consumption.CandidateInventory, consumption.CandidateProduction, candidate,
                WorldActionRejection.None, consumption.Claim, restoration);
        }
        catch (OverflowException)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
    }
}
