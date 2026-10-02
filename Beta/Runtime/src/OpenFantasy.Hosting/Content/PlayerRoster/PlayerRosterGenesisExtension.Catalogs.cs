using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content.PlayerRoster;

internal sealed partial class PlayerRosterGenesisExtension
{
    private ConstructionSnapshot ExtendConstruction(LocalSpatialSnapshot space, IReadOnlyList<Member> added)
    {
        List<GroundAccessRequirement> access = [];
        foreach (Member member in added)
        {
            SubunitPosition? exit = Cardinal.Select(offset => member.Presence.Feet.Offset(offset.X, offset.Y, 0))
                .Where(feet => space.QueryGroundRoute(member.ActorId, feet).Status == GroundRouteStatus.Reachable)
                .Select(feet => (SubunitPosition?)feet).FirstOrDefault();
            if (exit is null) throw new InvalidDataException("A roster member must retain a grounded exit after all members spawn.");
            access.Add(new GroundAccessRequirement(member.ActorId, member.Presence.Feet, exit.Value));
        }
        ConstructionSnapshot original = basis.Construction!;
        foreach (GroundAccessRequirement retained in original.Projects.SelectMany(project => project.RequiredAccess))
            if (space.QueryGroundRoute(retained.ActorId, retained.From, retained.To).Status != GroundRouteStatus.Reachable)
                throw new InvalidDataException("The roster cannot obstruct a retained construction access requirement.");
        var projects = original.Projects.Select(project => new ConstructionProject(project.ProjectId, project.Definition,
            project.OwnerActorId, project.BuilderActorId, project.QuestId, project.ProjectCustodianId, project.ReservationId,
            project.SiteFeet, project.WorkFeet, project.RequiredAccess.Concat(access), project.Revision, project.Progress,
            project.Installed, project.StockLocation));
        return new(original.WorldId, original.BranchId, 0, original.Definitions, projects);
    }

    private EncounterLifecycleSnapshot ExtendLifecycle(EntityId first, IReadOnlyList<WorldResident> residents,
        IReadOnlyList<Member> added)
    {
        EncounterLifecycleSnapshot original = basis.EncounterLifecycle!;
        List<EncounterScenarioDefinition> scenarios = [];
        foreach (EncounterScenarioDefinition scenario in original.Scenarios)
        {
            EncounterCombatant lead = scenario.Combatants.Single(actor => actor.ActorId == first);
            if (lead.Side == 0 || (scenario.InitiatingActorId == first) == (scenario.TargetActorId == first))
                throw new InvalidDataException("The cooperative roster requires an original player endpoint on a positive side.");
            EncounterCombatant[] combatants = scenario.Combatants.Concat(added.Select(member =>
                new EncounterCombatant(member.ActorId, lead.Side, lead.AbilityIds))).ToArray();
            // Preserve each original identity, source attack and NPC role while authoring the cooperative side.
            scenarios.Add(new EncounterScenarioDefinition(scenario.DefinitionId, scenario.Key, scenario.DisplayName,
                scenario.InitiatingActorId, scenario.TargetActorId, scenario.EntryRangeSubunits, combatants));
            foreach (Member member in added)
            {
                string slot = member.Slot.ToString(System.Globalization.CultureInfo.InvariantCulture);
                scenarios.Add(new EncounterScenarioDefinition(Instance(member.Slot, "scenario." + scenario.Key),
                    scenario.Key + "_player_" + slot, scenario.DisplayName,
                    scenario.InitiatingActorId == first ? member.ActorId : scenario.InitiatingActorId,
                    scenario.TargetActorId == first ? member.ActorId : scenario.TargetActorId,
                    scenario.EntryRangeSubunits, combatants));
            }
        }
        // The explicit catalog interpretation admits the complete twelve-entry four-player composition.
        // Legacy version-one catalogs retain their original eight-entry bound and bytes.
        return EncounterLifecycleSnapshot.BeginFresh(basis.WorldId, basis.BranchId, original.Seed, original.Abilities,
            scenarios, residents.Select(resident => resident.ActorId), catalogVersion: 2);
    }
}
