using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Terrain;

/// <summary>One qualified component owner for excavation geometry, material, shared effort and earned work opportunities.</summary>
public sealed class MaterialExcavationState
{
    internal MaterialExcavationState(CountedAuthorityState inventory, ProductionSnapshot production, GatheringSnapshot gathering,
        LocalSpatialSnapshot space, ExcavationMaterialBoundary boundary)
    {
        Inventory = inventory;
        Production = production;
        Gathering = gathering;
        Space = space;
        Boundary = boundary;
        Validate();
        ContentHash = ContentHash.Compute(CanonicalEncodingV1.Frame("excavation.material-owner.v1", inventory.ContentHash.ToByteArray(),
            production.ContentHash.ToByteArray(), gathering.ContentHash.ToByteArray(),
            ContentHash.Compute(ExcavationMaterialSource.EncodeSpace(space)).ToByteArray(), boundary.ContentHash.ToByteArray()));
    }
    public CountedAuthorityState Inventory { get; }
    public ProductionSnapshot Production { get; }
    public GatheringSnapshot Gathering { get; }
    public LocalSpatialSnapshot Space { get; }
    public ExcavationSnapshot Excavation => Boundary.Excavation;
    public ConstructionSnapshot Construction => Boundary.Source.Construction;
    public ConstructionPhysicalSnapshot? Physical => Boundary.Source.Physical;
    public ExcavationMaterialBoundary Boundary { get; }
    public ContentHash ContentHash { get; }

    public static MaterialExcavationState Capture(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        ExcavationMaterialSource.RequireSupported(world);
        world.Excavation!.ValidateForWorld(world);
        var source = new ExcavationMaterialSource(world);
        var boundary = new ExcavationMaterialBoundary(world.Excavation, world.Space.Regions,
            world.Inventory.Actors.Select(actor => ExcavationMaterialBoundary.Observe(world, actor)), source);
        return new(world.Inventory, world.Production!, world.Gathering!, world.Space, boundary);
    }

    public MaterialExcavationPlan PlanContinue(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Validate();
        var next = Production.ContinueExcavation(Inventory, Boundary, Construction, Gathering);
        var candidate = new MaterialExcavationState(Inventory, next, Gathering, Space, Boundary);
        cancellationToken.ThrowIfCancellationRequested();
        return new(this, candidate);
    }

    public MaterialExcavationPlan PlanReconcile(CountedAuthorityState inventory, ProductionSnapshot production,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(production);
        cancellationToken.ThrowIfCancellationRequested();
        if (inventory.ContentHash == Inventory.ContentHash && production.ContentHash == Production.ContentHash) return new(this, this);
        if (Production.Continuation?.SupportsExcavation != true || production.Continuation?.ContentHash != Production.Continuation.ContentHash
            || inventory.Custody.Revision <= Inventory.Custody.Revision
            || Inventory.WithCustody(inventory.Custody).ContentHash != inventory.ContentHash)
            throw new ArgumentException("Excavation material reconciliation retains its exact capability, admission and advancing custody.");
        ProductionRetirementHistory.ValidateSuccessor(Production, production);
        var candidate = new MaterialExcavationState(inventory, production, Gathering, Space, Boundary);
        cancellationToken.ThrowIfCancellationRequested();
        return new(this, candidate);
    }

    internal LocalSpatialSnapshot CurrentSpace(WorldSnapshot world)
    {
        Boundary.Source.ValidateFrame(world);
        return new(Space.Scope, Boundary.Regions, world.Space.Actors, Boundary.Source.Structures,
            Space.GroundSupport, Space.StructureSupport);
    }

    internal bool ContainsIdentity(EntityId id) => Inventory.ContainsIdentity(id) || Production.ContainsIdentity(id)
        || Boundary.Source.InitialIdentities.Contains(id);

    private void Validate()
    {
        Production.ValidateForAuthority(Inventory);
        if (Production.IsContinued && (Production.Continuation?.SupportsExcavation != true
            || Production.MaterialHistory?.Excavation?.ContentHash != Boundary.ContentHash
            || Production.MaterialHistory.Gathering?.ContentHash != Gathering.ContentHash))
            throw new ArgumentException("Continued excavation must retain its exact current terrain, effort, sites and actor frontiers.");
        if (Gathering.WorldId != Inventory.WorldId || Gathering.BranchId != Inventory.BranchId
            || !Inventory.Actors.SequenceEqual(Gathering.Actors.Select(value => value.ActorId))
            || !Boundary.Regions.Select(value => value.ComputeContentHash()).SequenceEqual(Space.Regions.Select(value => value.ComputeContentHash())))
            throw new ArgumentException("Excavation requires exact complete material, terrain and worker scope.");
        Boundary.Source.ValidateStaticMaterial(Inventory, Production, Space);
        foreach (var site in Excavation.Sites)
        {
            var region = Space.Regions.Single(value => value.Bounds.Contains(site.Cell));
            var cell = region.GetCell(site.Cell);
            var source = Inventory.Sources.SingleOrDefault(value => value.SourceId == site.CountedSourceId);
            if (cell.Stratum == TerrainStratum.Void || cell.SourceId != site.TerrainSourceId || cell.Material != site.Material.Item
                || cell.IsExcavated != site.IsComplete || source is null || source.Definition != site.Material.Item || source.UnitCapacity != site.Quantity
                || !Inventory.Actors.Contains(site.OwnerActorId) || Gathering.Nodes.Any(value => value.SourceId == site.CountedSourceId))
                throw new ArgumentException("Excavation retains its actual terrain ancestry and exclusive finite source entitlement.");
            Production.ValidateMaterialBinding(site.Material);
            IEnumerable<SourceInterval> claims = Inventory.Custody.Lots.Where(value => value.Source.SourceId == site.CountedSourceId).Select(value => value.Source)
                .Concat(ProductionRetirementHistory.CurrentRetirements(Production).Where(value => value.Ingredient.Source?.SourceId == site.CountedSourceId)
                    .Select(value => value.Ingredient.Source!));
            if (Production.Continuation is { } context)
                claims = claims.Concat(context.Basis.RawRetirements.Where(value => value.Source.SourceId == site.CountedSourceId).Select(value => value.Source));
            ulong cursor = 0;
            foreach (var claim in claims.OrderBy(value => value.Start))
            {
                if (!site.IsComplete || claim.Start != cursor || claim.End > site.Quantity)
                    throw new ArgumentException("Completed excavation retains exact live plus direct-retired source coverage; unfinished sites issue nothing.");
                cursor = claim.End;
            }
            if (cursor != (site.IsComplete ? site.Quantity : 0))
                throw new ArgumentException("Excavated source rights cannot disappear or be issued twice.");
        }
    }
}
