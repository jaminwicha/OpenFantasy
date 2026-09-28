using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Terrain;

public sealed record ExcavationWorkFrontier(EntityId ActorId, ulong WorldRevision, ulong AdvancingSteps,
    ulong ProcessIntervals, LocalExposureDuration ExperiencedTime);

/// <summary>Finite current site, terrain and shared actor opportunity observation, captured only from an actual owner.</summary>
public sealed class ExcavationMaterialBoundary
{
    internal ExcavationMaterialBoundary(ExcavationSnapshot excavation, IEnumerable<TerrainRegionSnapshot> regions,
        IEnumerable<ExcavationWorkFrontier> frontiers, ExcavationMaterialSource source)
    {
        Excavation = excavation;
        Regions = Array.AsReadOnly(regions.ToArray());
        Frontiers = Array.AsReadOnly(frontiers.OrderBy(value => value.ActorId).ToArray());
        Source = source;
        ContentHash = ContentHash.Compute(CanonicalEncodingV1.Frame("excavation.material-boundary.v1", source.ContentHash.ToByteArray(),
            excavation.ContentHash.ToByteArray(), ExcavationMaterialSource.RegionsHash(Regions).ToByteArray(),
            CanonicalEncodingV1.Frame("excavation.work-frontiers.v1", Frontiers.Select(value => (ReadOnlyMemory<byte>)EncodeFrontier(value)).ToArray())));
    }
    public ExcavationSnapshot Excavation { get; }
    public ReadOnlyCollection<TerrainRegionSnapshot> Regions { get; }
    public ReadOnlyCollection<ExcavationWorkFrontier> Frontiers { get; }
    public ContentHash ContentHash { get; }
    internal ExcavationMaterialSource Source { get; }
    internal static ExcavationWorkFrontier Observe(WorldSnapshot world, EntityId actor) => new(actor, world.Revision,
        world.AdvancingSteps, world.ProcessIntervals, world.GetResident(actor).ExperiencedTime);

    internal bool CanAdvance(ExcavationWorkFrontier current)
    {
        var prior = Frontiers.SingleOrDefault(value => value.ActorId == current.ActorId);
        return prior is not null && current.WorldRevision >= Frontiers.Max(value => value.WorldRevision)
            && current.AdvancingSteps >= Frontiers.Max(value => value.AdvancingSteps)
            && current.ProcessIntervals >= Frontiers.Max(value => value.ProcessIntervals)
            && current.WorldRevision > prior.WorldRevision && current.AdvancingSteps > prior.AdvancingSteps
            && current.ProcessIntervals > prior.ProcessIntervals && current.ExperiencedTime.TotalMilliseconds >= prior.ExperiencedTime.TotalMilliseconds
            && current.ExperiencedTime.TotalMilliseconds - prior.ExperiencedTime.TotalMilliseconds >= ExposurePacingV1.ExplorationMillisecondsPerStep;
    }

    internal ExcavationMaterialBoundary Advance(WorldActionCommand command, ExcavationWorkFrontier frontier)
    {
        if (command.ActorId != frontier.ActorId || !CanAdvance(frontier))
            throw new ArgumentException("Mining and gathering share one fresh work opportunity per actual actor boundary.");
        var excavation = Excavation;
        var regions = Regions.ToArray();
        if (command.Intent is ExcavateIntent intent)
        {
            var site = excavation.Sites.Single(value => value.SiteId == intent.SiteId);
            int index = Array.FindIndex(regions, value => value.Bounds.Contains(site.Cell));
            if (site.IsComplete || site.OwnerActorId != command.ActorId || site.Revision != intent.ExpectedSiteRevision
                || regions[index].Revision != intent.ExpectedTerrainRevision)
                throw new ArgumentException("Excavation evidence requires the exact current site and region.");
            var worked = site.Work();
            excavation = excavation.WithSite(worked);
            if (worked.IsComplete) regions[index] = regions[index].Excavate(regions[index].Revision, [site.Cell]);
        }
        else if (command.Intent is not GatherIntent)
            throw new ArgumentException("Only mining or gathering can advance this shared work frontier.");
        return new(excavation, regions, Frontiers.Select(value => value.ActorId == frontier.ActorId ? frontier : value), Source);
    }

    internal ExcavationMaterialBoundary Apply(ExcavationWorkEvidence evidence)
    {
        if (evidence.BeforeBoundaryHash != ContentHash || evidence.SourceHash != Source.ContentHash)
            throw new ArgumentException("Work evidence requires its exact preceding excavation owner.");
        var result = Advance(evidence.Command, evidence.Frontier);
        if (result.ContentHash != evidence.AfterBoundaryHash)
            throw new ArgumentException("Work evidence cannot change unrelated sites, terrain or actor frontiers.");
        return result;
    }
    internal static byte[] EncodeFrontier(ExcavationWorkFrontier value) => CanonicalEncodingV1.Frame("excavation.work-frontier.v1",
        value.ActorId.ToByteArray(), CanonicalEncodingV1.Unsigned64(value.WorldRevision), CanonicalEncodingV1.Unsigned64(value.AdvancingSteps),
        CanonicalEncodingV1.Unsigned64(value.ProcessIntervals), CanonicalEncodingV1.Unsigned64(value.ExperiencedTime.Days),
        CanonicalEncodingV1.Unsigned64(value.ExperiencedTime.MillisecondsWithinDay));
}
