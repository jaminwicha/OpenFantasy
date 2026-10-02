using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;
using OpenFantasy.Hosting.Simulation.Retreat;
using CargoPresentation = OpenFantasy.Hosting.Simulation.CargoPresentation;
using EncounterOutcome = OpenFantasy.Hosting.Simulation.EncounterOutcome;
using Retreat = OpenFantasy.Hosting.Simulation.Retreat;
using RoadThreat = OpenFantasy.Hosting.Simulation.RoadThreat;

namespace OpenFantasy.Hosting.Networking.ObservationViews;

internal sealed record WorksiteViewData(
    ReadOnlyCollection<LocalWorksiteTerrainView> Terrain,
    ReadOnlyCollection<LocalWorksiteMemberView> Members,
    ReadOnlyCollection<LocalExcavationView> ExcavationTargets,
    ReadOnlyCollection<LocalExcavationToolView> Tools,
    uint PlayerEffort,
    bool CanManipulate,
    bool IsResolvingConsequences,
    bool HasPhysicalConstruction,
    bool SupportsAssistedTraversal)
{
    internal LocalWorksiteView ToView() => new(Terrain, Members, ExcavationTargets, Tools, PlayerEffort, CanManipulate, IsResolvingConsequences, HasPhysicalConstruction, SupportsAssistedTraversal);

    internal static WorksiteViewData From(LocalWorksiteView value) => new(
        value.Terrain,
        value.Members,
        value.ExcavationTargets,
        value.Tools,
        value.PlayerEffort,
        value.CanManipulate,
        value.IsResolvingConsequences,
        value.HasPhysicalConstruction,
        value.SupportsAssistedTraversal);
}

internal sealed record WorksiteTerrainViewData(
    TerrainRegionBounds Bounds,
    ulong Revision,
    ReadOnlyCollection<bool> Occupancy)
{
    internal LocalWorksiteTerrainView ToView() => new(Bounds, Revision, Occupancy);

    internal static WorksiteTerrainViewData From(LocalWorksiteTerrainView value) => new(
        value.Bounds,
        value.Revision,
        value.Occupancy);
}

internal sealed record LiquidTerrainViewData(
    TerrainRegionBounds Bounds,
    ulong Revision,
    ReadOnlyCollection<bool> Occupancy)
{
    internal LocalLiquidTerrainView ToView() => new(Bounds, Revision, Occupancy);

    internal static LiquidTerrainViewData From(LocalLiquidTerrainView value) => new(
        value.Bounds,
        value.Revision,
        value.Occupancy);
}

internal sealed record LiquidViewData(
    ReadOnlyCollection<LocalLiquidCellView> Cells,
    ReadOnlyCollection<LocalLiquidTerrainView> Terrain)
{
    internal LocalLiquidView ToView() => new(Cells, Terrain);

    internal static LiquidViewData From(LocalLiquidView value) => new(
        value.Cells,
        value.Terrain);
}

internal sealed record ObservedRegionData(
    TerrainRegionBounds Bounds,
    ulong Revision,
    LocalGroundKind[] Ground,
    byte[] OpeningDepth)
{
    internal LocalObservedRegion ToView() => new(Bounds, Revision, Ground, OpeningDepth);

    internal static ObservedRegionData From(LocalObservedRegion value) => new(
        value.Bounds,
        value.Revision,
        value.Ground.ToArray(),
        value.OpeningDepth.ToArray());
}

internal sealed record RegionObservationData(
    EntityId PlayerActorId,
    SubunitPosition PlayerFeet,
    ReadOnlyCollection<LocalObservedRegion> Regions,
    ReadOnlyCollection<LocalObservedWater> Water)
{
    internal LocalRegionObservation ToView() => new(PlayerActorId, PlayerFeet, Regions, Water);

    internal static RegionObservationData From(LocalRegionObservation value) => new(
        value.PlayerActorId,
        value.PlayerFeet,
        value.Regions,
        value.Water);
}
