using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

internal static class LocalConstructionWorkProjection
{
    internal static bool IsEligibleTool(CountedSourceLot lot, EntityId bag, CountedItemDefinition required) =>
        lot.CustodianId == bag && lot.Definition == required && lot.Quantity == 1
        && lot.Protection == LotProtection.None && lot.ReservedBy is null;

    internal static ReadOnlyCollection<LocalConstructionWorkView> Project(WorldSnapshot state, EntityId actorId)
    {
        var actor = state.Space.GetActor(actorId);
        EntityId bag = state.GetResident(actorId).InventoryCustodianId;
        return Array.AsReadOnly(state.Construction?.Projects.Where(project => !project.IsCompleted
            && project.BuilderActorId == actorId && project.OwnerActorId == actorId).OrderBy(project => project.ProjectId)
            .Select(Build).ToArray() ?? []);

        LocalConstructionWorkView Build(ConstructionProject project)
        {
            var choices = state.Inventory.Custody.Lots.Where(lot => IsEligibleTool(lot, bag, project.Definition.RequiredTool.Item))
                .Select(lot => new WorkConstructionIntent(project.ProjectId, project.Revision,
                    state.Inventory.Custody.GetStorage(project.ProjectCustodianId).Revision, lot.LotId, lot.Revision)).ToArray();
            var recipe = project.Definition.MaterialTransform;
            var input = recipe.ItemFor(recipe.Inputs.Single());
            var stock = state.Inventory.Custody.Lots.Where(lot => lot.ReservedBy == project.ReservationId).ToArray();
            bool funded = state.Quests!.Participations.Any(participation => participation.QuestId == project.QuestId
                    && participation.Status == ActorQuestStatus.Completed)
                && stock.Length is > 0 and <= MaterialRecipeDefinition.MaximumTermCount && stock.All(lot => lot.CustodianId == project.ProjectCustodianId
                    && lot.Definition == input && lot.Protection == LotProtection.None)
                && stock.Aggregate((UInt128)0, (sum, lot) => sum + lot.Quantity) == recipe.Inputs[0].Count;
            bool canAct = WorldActorAvailability.CanAct(state, actorId);
            bool body = actor.Binding.Kind == ActorFormKind.Corporeal
                && actor.Condition.SupportsFunction(BodyFunction.Manipulation) && actor.Condition.CanLocomote(LocomotionMode.Ground)
                && state.Progression?.GetActor(actorId).CurrentHp > 0;
            bool stance = actor.Feet == project.WorkFeet;
            bool reach = state.Space.QueryPointInteraction(actorId, project.SiteFeet,
                ConstructionDefinition.WorkReachSubunits) == SpatialInteraction.Reachable;
            var blocker = state.HasReadingHold ? LocalConstructionWorkBlocker.ReadingHold
                : state.PendingExcavation is not null ? LocalConstructionWorkBlocker.ResolvingConsequences
                : state.EncounterLifecycle is { Phase: not EncounterLifecyclePhase.Exploration } ? LocalConstructionWorkBlocker.NotExploration
                : !canAct ? LocalConstructionWorkBlocker.ActorUnavailable
                : !body ? LocalConstructionWorkBlocker.BodyUnavailable
                : !funded ? LocalConstructionWorkBlocker.NotFunded
                : choices.Length == 0 ? LocalConstructionWorkBlocker.MissingTool
                : !stance ? LocalConstructionWorkBlocker.WrongWorkStance
                : !reach ? LocalConstructionWorkBlocker.SiteUnreachable
                : LocalConstructionWorkBlocker.None;
            if (blocker == LocalConstructionWorkBlocker.None)
            {
                // The identity is only a query-local geometry key; it is never admitted or submitted.
                var geometry = project.Definition.CreateStructure(project.ProjectId, project.SiteFeet,
                    state.Space.GroundSupport != GroundSupportMode.TerrainOnly);
                var access = project.RequiredAccess.Concat(project.RequiredAccess.Select(value =>
                    new GroundAccessRequirement(value.ActorId, state.Space.GetActor(value.ActorId).Feet, value.To))).Distinct();
                if (state.Space.PrepareStructurePlacement(geometry, access).Status != StructurePlacementStatus.Prepared)
                    blocker = LocalConstructionWorkBlocker.PlacementBlocked;
            }
            var view = new LocalConstructionView(project.ProjectId, actorId, project.Definition.DisplayName,
                project.SiteFeet, project.WorkFeet, project.Progress.CompletedUnits, project.Definition.RequiredWork.Value, false);
            ulong earned = ExposureProgress.Restore(project.Progress, project.Definition.WorkRate)
                .Advance(LocalExposureDuration.FromMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep)).CompletedUnits
                - project.Progress.CompletedUnits;
            return new(actorId, view, project.Revision, earned, funded, canAct, body, stance, reach, blocker, choices);
        }
    }
}
