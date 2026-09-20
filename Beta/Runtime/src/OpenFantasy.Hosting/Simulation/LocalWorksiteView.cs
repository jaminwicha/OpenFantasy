using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>
/// Committed geometry and the controlled actor's extraction inputs in the admitted sight-complete
/// locality. This is not general fog-of-war, an excavation grant, or a prediction of its consequences.
/// Source lineage, output entitlements, other actors' custody and pending calculation details stay private.
/// </summary>
public sealed class LocalWorksiteView
{
    internal LocalWorksiteView(WorldSnapshot state, EntityId playerActorId)
    {
        var physical = state.ConstructionPhysical
            ?? throw new ArgumentException("A worksite view requires retained physical construction.", nameof(state));
        var excavation = state.Excavation
            ?? throw new ArgumentException("A worksite view requires admitted excavation.", nameof(state));
        EntityId destination = state.GetResident(playerActorId).InventoryCustodianId;
        ulong destinationRevision = state.Inventory.Custody.GetStorage(destination).Revision;
        PlayerEffort = checked((uint)state.Gathering!.GetActorWork(playerActorId).Effort);
        CanManipulate = state.Space.GetActor(playerActorId).Condition.SupportsFunction(BodyFunction.Manipulation);
        IsResolvingConsequences = state.PendingExcavation is not null;

        // A view is created at session initialization and committed publication, never per render frame.
        // The regions are already bounded by local spatial admission; occupancy omits geological lineage.
        Terrain = Array.AsReadOnly(state.Space.Regions.Select(region => new LocalWorksiteTerrainView(region)).ToArray());
        var projects = state.Construction!.Projects.Where(project => project.Installed is not null)
            .ToDictionary(project => project.Installed!.StructureId);
        Members = Array.AsReadOnly(physical.Members.Select(member =>
        {
            var project = projects[member.StructureId];
            var origin = project.SiteFeet.Offset(member.Displacement.X, member.Displacement.Y, member.Displacement.Z);
            var geometry = project.Definition.Members[checked((int)member.MemberIndex)];
            return new LocalWorksiteMemberView(member.StructureId, member.MemberIndex, member.Revision,
                member.ConditionParts, member.Disposition, origin, geometry, geometry.At(origin));
        }).ToArray());

        ExcavationSite[] ownedSites = excavation.Sites.Where(site => site.OwnerActorId == playerActorId).ToArray();
        ExcavationTargets = Array.AsReadOnly(ownedSites.Select(site => new LocalExcavationView(site.SiteId,
            site.Cell, site.Stance, site.Revision,
            state.Space.Regions.Single(region => region.Bounds.Contains(site.Cell)).Revision,
            site.RequiredWork, site.CompletedWork, site.RequiredTool.DefinitionId, destinationRevision)).ToArray());
        // World admission makes the destination this actor's own primary storage, which accepts its
        // owner's deposits. Source ownership and the actual lot restrictions remain necessary here.
        Tools = Array.AsReadOnly(state.Inventory.Custody.Lots.Where(lot =>
                ownedSites.Any(site => site.RequiredTool == lot.Definition)
                && lot.ReservedBy is null && !lot.Protection.HasFlag(LotProtection.UserReserved)
                && state.Inventory.Policies.Any(policy => policy.CustodianId == lot.CustodianId
                    && policy.OwnerActorId == playerActorId))
            .Select(lot => new LocalExcavationToolView(lot.LotId, lot.Definition.DefinitionId, lot.Revision))
            .OrderBy(tool => tool.LotId).ToArray());
    }

    public ReadOnlyCollection<LocalWorksiteTerrainView> Terrain { get; }
    public ReadOnlyCollection<LocalWorksiteMemberView> Members { get; }
    public ReadOnlyCollection<LocalExcavationView> ExcavationTargets { get; }
    public ReadOnlyCollection<LocalExcavationToolView> Tools { get; }
    public uint PlayerEffort { get; }
    public uint EffortPerWork => GatherRulesV1.EffortPerSwing;
    public bool CanManipulate { get; }
    public bool IsResolvingConsequences { get; }
}
