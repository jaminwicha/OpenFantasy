using System.Collections.ObjectModel;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Immutable physical-carriage projection from one admitted world. Access ownership alone does not
/// place goods on an actor. This view grants no transfer, movement, publication or partial admission.
/// </summary>
public sealed class WorldActorCarriage
{
    private WorldActorCarriage(WorldSnapshot world, EntityId actorId, HashSet<EntityId> custodians,
        HashSet<EntityId> installedBacking)
    {
        WorldId = world.WorldId;
        BranchId = world.BranchId;
        WorldRevision = world.Revision;
        ProfileVersion = world.ProfileVersion;
        ActorId = actorId;
        CustodianIds = Array.AsReadOnly(custodians.Order().ToArray());
        CountedLots = Array.AsReadOnly(world.Inventory.Custody.Lots
            .Where(lot => custodians.Contains(lot.CustodianId)).ToArray());
        ProducedLots = Array.AsReadOnly(world.Production?.Lots
            .Where(lot => custodians.Contains(lot.CustodianId) && !installedBacking.Contains(lot.LotId))
            .ToArray() ?? []);
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public ulong WorldRevision { get; }
    public ushort ProfileVersion { get; }
    public EntityId ActorId { get; }
    public ReadOnlyCollection<EntityId> CustodianIds { get; }
    public ReadOnlyCollection<CountedSourceLot> CountedLots { get; }
    public ReadOnlyCollection<TransformedLot> ProducedLots { get; }

    /// <summary>Checks exact carried material, not world freshness, access permission or support capacity.</summary>
    public void ValidateCurrent(WorldSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(current);
        WorldActorCarriage actual = Capture(current, ActorId);
        if (WorldId != actual.WorldId || BranchId != actual.BranchId || ProfileVersion != actual.ProfileVersion
            || !CustodianIds.SequenceEqual(actual.CustodianIds) || !CountedLots.SequenceEqual(actual.CountedLots)
            || ProducedLots.Count != actual.ProducedLots.Count
            || ProducedLots.Where((lot, index) => !SameProducedLot(lot, actual.ProducedLots[index])).Any())
        {
            throw new InvalidOperationException("The prepared actor carriage changed.");
        }
    }

    private static bool SameProducedLot(TransformedLot left, TransformedLot right) =>
        left.LotId == right.LotId && left.CustodianId == right.CustodianId && left.Definition == right.Definition
        && left.Output == right.Output && left.Revision == right.Revision && left.Protection == right.Protection
        && left.ReservedBy == right.ReservedBy;

    /// <summary>
    /// Retains the existing physical interpretation: primary inventory in every world, plus explicit
    /// builder-carried project stock in structural worlds. Installed backing belongs to the assembly.
    /// Reservations and equipment do not make otherwise carried material weightless.
    /// </summary>
    public static WorldActorCarriage Capture(WorldSnapshot world, EntityId actorId)
    {
        ArgumentNullException.ThrowIfNull(world);
        HashSet<EntityId> carried = [world.GetResident(actorId).InventoryCustodianId];
        HashSet<EntityId> installedBacking = [];
        if (world.StructuralPolicy is not null && world.Construction is { } construction)
        {
            foreach (ConstructionProject project in construction.Projects.Where(project => project.BuilderActorId == actorId))
            {
                if (project.StockLocation != ConstructionStockLocation.BuilderCarried)
                {
                    throw new ArgumentException("Project stock requires an explicit spatial load owner.", nameof(world));
                }
                carried.Add(project.ProjectCustodianId);
                if (project.Installed is { } installed)
                {
                    installedBacking.Add(installed.GuardLotId);
                }
            }
        }
        return new(world, actorId, carried, installedBacking);
    }
}
