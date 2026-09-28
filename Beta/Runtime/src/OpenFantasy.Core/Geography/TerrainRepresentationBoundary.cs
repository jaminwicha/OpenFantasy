using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>Bounded observation of the actual fixed allocation catalog and complete active bindings at a material boundary.</summary>
public sealed class TerrainRepresentationBoundary
{
    private readonly Dictionary<EntityId, CellCoordinate[]> _active;

    private TerrainRepresentationBoundary(PreparedWatershedTerrain terrain,
        IReadOnlyList<TerrainMaterialAllocation> allocations, IReadOnlyDictionary<EntityId, CellCoordinate[]> active, ContentHash? catalogHash = null)
    {
        Terrain = terrain;
        Allocations = Array.AsReadOnly(allocations.ToArray());
        _active = active.ToDictionary(pair => pair.Key, pair => pair.Value.Order().ToArray());
        CatalogHash = catalogHash ?? ContentHash.Compute(CanonicalEncodingV1.Frame("terrain.representation-catalog.v1",
            terrain.ComputeContentHash().ToByteArray(), CanonicalEncodingV1.Frame("terrain.representation-allocations.v1",
                Allocations.Select(value => (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("terrain.representation-allocation.v1",
                    value.AllocationId.ToByteArray(), TerrainMaterialCodecV1.EncodeBinding(value.Deposit))).ToArray())));
        ContentHash = ContentHash.Compute(CanonicalEncodingV1.Frame("terrain.representation-boundary.v1", CatalogHash.ToByteArray(),
            CanonicalEncodingV1.Frame("terrain.representation-active.v1", Allocations.Select(value =>
                (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("terrain.representation-cells.v1", value.AllocationId.ToByteArray(),
                    EncodeCells(_active[value.AllocationId]))).ToArray())));
    }

    public ContentHash CatalogHash { get; }
    public ContentHash ContentHash { get; }
    internal PreparedWatershedTerrain Terrain { get; }
    internal ReadOnlyCollection<TerrainMaterialAllocation> Allocations { get; }
    internal IReadOnlyList<CellCoordinate> Cells(EntityId allocationId) => _active[allocationId];
    internal TerrainMaterialDeposit Deposit(EntityId allocationId) => Allocations.Single(value => value.AllocationId == allocationId).Deposit;
    internal static TerrainRepresentationBoundary Capture(TerrainMaterialRegistry registry) =>
        new(registry.Terrain, registry.Allocations, registry.Allocations.ToDictionary(value => value.AllocationId,
            value => registry.GetState(value.AllocationId).ActiveCells.ToArray()));

    internal TerrainRepresentationBoundary Change(EntityId allocationId, IEnumerable<CellCoordinate> cells)
    {
        _ = Deposit(allocationId);
        var active = _active.ToDictionary(pair => pair.Key, pair => pair.Value);
        active[allocationId] = cells.Order().ToArray();
        if (active.Values.Sum(value => value.Length) > TerrainMaterialRegistry.MaximumActiveCells)
            throw new ArgumentException("Terrain representation exceeds its shared active-cell bound.");
        return new(Terrain, Allocations, active, CatalogHash);
    }

    internal TerrainRepresentationBoundary Apply(TerrainRepresentationEvidence evidence)
    {
        if (evidence.CatalogHash != CatalogHash || evidence.BeforeRepresentationHash != ContentHash)
            throw new ArgumentException("Terrain operations require the exact preceding complete representation binding.");
        var current = Cells(evidence.AllocationId).ToHashSet();
        foreach (CellCoordinate cell in evidence.Cells)
        {
            _ = Deposit(evidence.AllocationId).IntervalFor(cell);
            if (evidence.Refining ? !current.Add(cell) : !current.Remove(cell))
                throw new ArgumentException("Terrain representation cannot repeat an active or absent cell transition.");
        }
        TerrainRepresentationBoundary next = Change(evidence.AllocationId, current);
        if (next.ContentHash != evidence.AfterRepresentationHash)
            throw new ArgumentException("Terrain evidence cannot change unselected bindings or its fixed catalog.");
        return next;
    }

    internal bool ContainsIdentity(EntityId id) => Terrain.Network.MaterialIdentities.Contains(id)
        || Allocations.Any(value => value.AllocationId == id || value.Deposit.Material.Physical.DefinitionId == id);

    internal void ValidateView(TerrainMaterialDeposit deposit, IReadOnlyCollection<CellCoordinate> cells)
    {
        byte[] binding = TerrainMaterialCodecV1.EncodeBinding(deposit);
        var allocation = Allocations.SingleOrDefault(value => TerrainMaterialCodecV1.EncodeBinding(value.Deposit).AsSpan().SequenceEqual(binding));
        if (allocation is null || deposit.Terrain.ComputeContentHash() != Terrain.ComputeContentHash()
            || !Cells(allocation.AllocationId).SequenceEqual(cells.Order()))
            throw new ArgumentException("A continued terrain view requires its exact catalog member and complete current active binding.");
    }

    internal static byte[] EncodeCells(IEnumerable<CellCoordinate> cells) => CanonicalEncodingV1.Frame("terrain.cells.v1",
        cells.Select(value => (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("terrain.cell.v1",
            CanonicalEncodingV1.Unsigned64(unchecked((ulong)value.X)), CanonicalEncodingV1.Unsigned64(unchecked((ulong)value.Y)), CanonicalEncodingV1.Unsigned64(unchecked((ulong)value.Z)))).ToArray());
}
