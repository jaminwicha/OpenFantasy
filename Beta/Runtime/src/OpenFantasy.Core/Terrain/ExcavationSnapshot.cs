using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Terrain;

/// <summary>Complete finite local extraction ownership; issuance is inseparable from retained terrain removal.</summary>
public sealed class ExcavationSnapshot
{
    public const int MaximumSites = 32;
    public ExcavationSnapshot(IEnumerable<ExcavationSite> sites)
    {
        ArgumentNullException.ThrowIfNull(sites);
        var rows = new SortedDictionary<EntityId, ExcavationSite>();
        Dictionary<CountedItemDefinition, CountedItemDefinition> calibrations = [];
        HashSet<CellCoordinate> cells = [];
        HashSet<EntityId> terrainSources = [], countedSources = [];
        foreach (var site in sites)
        {
            ArgumentNullException.ThrowIfNull(site);
            if (calibrations.TryGetValue(site.Material.Item, out var physical) && physical != site.Material.Physical.Pin())
            {
                throw new ArgumentException("A shared extraction item requires one exact physical calibration.");
            }
            calibrations[site.Material.Item] = site.Material.Physical.Pin();
            if (rows.Count == MaximumSites || !rows.TryAdd(site.SiteId, site) || !cells.Add(site.Cell)
                || !terrainSources.Add(site.TerrainSourceId) || !countedSources.Add(site.CountedSourceId))
            {
                throw new ArgumentException("Extraction sites require unique cells and source ownership within the finite envelope.");
            }
        }
        if (rows.Count == 0)
        {
            throw new ArgumentException("Extraction admission cannot be empty.");
        }
        Sites = Array.AsReadOnly(rows.Values.ToArray());
    }
    public ReadOnlyCollection<ExcavationSite> Sites { get; }
    public ContentHash ContentHash => ContentHash.Compute(Sites.All(site => site.ReachMode == ExcavationReachMode.SameLevelSide)
        ? ExcavationCodecV1.Encode(this) : ExcavationCodecV2.Encode(this));
    internal ExcavationSnapshot WithSite(ExcavationSite changed) => new(Sites.Select(s => s.SiteId == changed.SiteId ? changed : s));

    internal void ValidateForWorld(WorldSnapshot world)
    {
        foreach (var site in Sites)
        {
            var region = world.Space.Regions.SingleOrDefault(r => r.Bounds.Contains(site.Cell));
            var cell = region?.GetCell(site.Cell);
            var source = world.Inventory.Sources.SingleOrDefault(s => s.SourceId == site.CountedSourceId);
            if (cell is null || cell.Stratum == TerrainStratum.Void || cell.SourceId != site.TerrainSourceId
                || cell.Material != site.Material.Item || cell.IsExcavated != site.IsComplete
                || source is null || source.Definition != site.Material.Item || source.UnitCapacity != site.Quantity
                || !world.Inventory.Actors.Contains(site.OwnerActorId)
                || !world.Inventory.Sources.Any(s => s.Definition == site.RequiredTool)
                || world.Gathering!.Nodes.Any(n => n.SourceId == site.CountedSourceId))
            {
                throw new ArgumentException("Extraction requires exact terrain ancestry, exclusive finite issuance, worker and tool bindings.");
            }
            world.Production!.ValidateMaterialBinding(site.Material);
            // The coordinated world joins archived direct retirement with its active segment exactly once.
            IEnumerable<SourceInterval?> retired = world.Production.Continuation is { SupportsCompleteWorld: true } coordinated
                ? ProductionRetirementHistory.CurrentRetirements(world.Production).Select(r => r.Ingredient.Source)
                    .Concat(coordinated.Basis.RawRetirements.Select(r => (SourceInterval?)r.Source))
                : ProductionRetirementHistory.Retirements(world.Production).Select(r => r.Ingredient.Source);
            IEnumerable<SourceInterval> claims = world.Inventory.Custody.Lots.Where(l => l.Source.SourceId == site.CountedSourceId).Select(l => l.Source)
                .Concat(retired.Where(s => s?.SourceId == site.CountedSourceId).Select(s => s!));
            ulong cursor = 0;
            foreach (var claim in claims.OrderBy(c => c.Start))
            {
                if (!site.IsComplete || claim.Start != cursor || claim.End > site.Quantity)
                {
                    throw new ArgumentException("Unexcavated stock cannot be issued or consumed; completed extraction must have exclusive continuous source coverage.");
                }
                cursor = claim.End;
            }
            if (cursor != (site.IsComplete ? site.Quantity : 0))
            {
                throw new ArgumentException("Excavated material cannot disappear from live custody or direct production retirement.");
            }
        }
        foreach (var ownerSites in Sites.GroupBy(s => s.OwnerActorId))
        {
            UInt128 work = ownerSites.Aggregate((UInt128)0, (n, s) => n + s.CompletedWork);
            UInt128 constructionWork = world.Construction!.Projects.Where(p => p.BuilderActorId == ownerSites.Key)
                .Aggregate((UInt128)0, (n, p) => n + p.Revision);
            UInt128 totalWork = work + constructionWork;
            if (totalWork > world.AdvancingSteps
                || totalWork * ExposurePacingV1.ExplorationMillisecondsPerStep > world.GetResident(ownerSites.Key).ExperiencedTime.TotalMilliseconds)
            {
                throw new ArgumentException("One worker's excavation and construction must fit advancing actions and experienced time.");
            }
        }
    }
}
