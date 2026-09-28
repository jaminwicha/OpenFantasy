using OpenFantasy.Core.Content;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Construction;

/// <summary>Current fabrication and bounded builder work frontiers captured from an admitted source world.</summary>
public sealed class MaterialConstructionState
{
    private sealed record BuilderFrontier(EntityId ActorId, ulong WorldRevision, ulong AdvancingSteps,
        ulong ProcessIntervals, LocalExposureDuration ExperiencedTime);
    private readonly BuilderFrontier[] _builders;
    private sealed record SourceAdmission(ContentHash Version, ushort Profile, ContentHash InventoryAdmission,
        ContentHash Construction, ContentHash? Physical, ContentHash InitialInventory, ContentHash InitialProduction);
    private readonly SourceAdmission _source;

    private MaterialConstructionState(ConstructionSnapshot construction, ConstructionPhysicalSnapshot? physical,
        BuilderFrontier[] builders, SourceAdmission source)
    {
        Construction = construction;
        Physical = physical;
        _builders = builders;
        _source = source;
        ContentHash = ContentHash.Compute(CanonicalEncodingV1.Frame("construction.material-state", CanonicalEncodingV1.Unsigned64(1),
            construction.ContentHash.ToByteArray(), physical?.ContentHash.ToByteArray() ?? Array.Empty<byte>(),
            source.Version.ToByteArray(), CanonicalEncodingV1.Unsigned64(source.Profile), source.InventoryAdmission.ToByteArray(),
            source.Construction.ToByteArray(), source.Physical?.ToByteArray() ?? Array.Empty<byte>(),
            source.InitialInventory.ToByteArray(), source.InitialProduction.ToByteArray(),
            CanonicalEncodingV1.Frame("construction.builder-frontiers", builders.Select(value =>
                (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("construction.builder-frontier", value.ActorId.ToByteArray(),
                    CanonicalEncodingV1.Unsigned64(value.WorldRevision), CanonicalEncodingV1.Unsigned64(value.AdvancingSteps),
                    CanonicalEncodingV1.Unsigned64(value.ProcessIntervals), CanonicalEncodingV1.Unsigned64(value.ExperiencedTime.Days),
                    CanonicalEncodingV1.Unsigned64(value.ExperiencedTime.MillisecondsWithinDay))).ToArray())));
    }

    public ConstructionSnapshot Construction { get; }
    public ConstructionPhysicalSnapshot? Physical { get; }
    public ContentHash ContentHash { get; }

    /// <summary>Starts a structural continuation session from the actual admitted closing world.</summary>
    public static MaterialConstructionState Capture(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        ConstructionSnapshot construction = world.Construction
            ?? throw new ArgumentException("Construction continuation needs the actual admitted fabrication owner.", nameof(world));
        construction.ValidateForWorld(world);
        return new(construction, world.ConstructionPhysical, construction.Projects.Select(value => value.BuilderActorId)
            .Distinct().Order().Select(actor => Frontier(world, actor)).ToArray(),
            new(world.AdmissionVersion, world.ProfileVersion, ProductionContinuationContext.AdmissionHash(world.Inventory),
                construction.ContentHash, world.ConstructionPhysical?.ContentHash, world.Inventory.ContentHash, world.Production!.ContentHash));
    }

    internal bool CanAdvance(WorldSnapshot world, EntityId actorId)
    {
        BuilderFrontier? prior = _builders.FirstOrDefault(value => value.ActorId == actorId);
        if (prior is null) return false;
        BuilderFrontier current = Frontier(world, actorId);
        return world.AdmissionVersion == _source.Version && world.ProfileVersion == _source.Profile
            && ProductionContinuationContext.AdmissionHash(world.Inventory) == _source.InventoryAdmission
            && world.Construction?.ContentHash == _source.Construction && world.ConstructionPhysical?.ContentHash == _source.Physical
            && world.Revision >= _builders.Max(value => value.WorldRevision)
            && world.AdvancingSteps >= _builders.Max(value => value.AdvancingSteps)
            && world.ProcessIntervals >= _builders.Max(value => value.ProcessIntervals)
            && current.WorldRevision > prior.WorldRevision && current.AdvancingSteps > prior.AdvancingSteps
            && current.ProcessIntervals > prior.ProcessIntervals && current.ExperiencedTime.TotalMilliseconds >= prior.ExperiencedTime.TotalMilliseconds
            && current.ExperiencedTime.TotalMilliseconds - prior.ExperiencedTime.TotalMilliseconds >= ExposurePacingV1.ExplorationMillisecondsPerStep;
    }

    internal MaterialConstructionState Advance(WorldSnapshot world, EntityId actorId,
        ConstructionSnapshot construction, ConstructionPhysicalSnapshot? physical) =>
        new(construction, physical, _builders.Select(value => value.ActorId == actorId ? Frontier(world, actorId) : value).ToArray(), _source);

    internal void ValidateMaterialBoundary(CountedAuthorityState inventory, ProductionSnapshot production)
    {
        if (inventory.WorldId != Construction.WorldId || inventory.BranchId != Construction.BranchId
            || production.WorldId != Construction.WorldId || production.BranchId != Construction.BranchId
            || production.Continuation?.SupportsConstruction != true
                && (inventory.ContentHash != _source.InitialInventory || production.ContentHash != _source.InitialProduction))
            throw new ArgumentException("First construction capability must use the actual owners captured from the admitted world.");
    }

    private static BuilderFrontier Frontier(WorldSnapshot world, EntityId actorId) => new(actorId, world.Revision,
        world.AdvancingSteps, world.ProcessIntervals, world.GetResident(actorId).ExperiencedTime);
}
