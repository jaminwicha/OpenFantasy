using System.Collections.ObjectModel;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Structures;

/// <summary>
/// Finite mass calibration. Structural worlds include explicitly builder-carried project custody;
/// older diagnostic worlds retain their primary-inventory interpretation.
/// </summary>
public sealed class StructuralMassCatalog
{
    public const int MaximumBodyDefinitions = 64;
    public const int MaximumSupplementalItems = 256;

    private readonly Dictionary<EntityId, StructuralBodyMass> _bodies = [];
    private readonly Dictionary<EntityId, StructuralItemMass> _supplementalItems = [];

    public StructuralMassCatalog(IEnumerable<StructuralBodyMass> bodies,
        IEnumerable<StructuralItemMass> supplementalItems)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentNullException.ThrowIfNull(supplementalItems);
        foreach (StructuralBodyMass body in bodies)
        {
            ArgumentNullException.ThrowIfNull(body);
            if (_bodies.Count == MaximumBodyDefinitions || !_bodies.TryAdd(body.DefinitionId, body))
            {
                throw new ArgumentException("Body mass definitions must be unique and within the catalog limit.", nameof(bodies));
            }
        }
        foreach (StructuralItemMass item in supplementalItems)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (_supplementalItems.Count == MaximumSupplementalItems
                || !_supplementalItems.TryAdd(item.Item.DefinitionId, item))
            {
                throw new ArgumentException("Supplemental item masses must be unique and within the catalog limit.", nameof(supplementalItems));
            }
        }
        Bodies = _bodies.Values.OrderBy(value => value.DefinitionId).ToList().AsReadOnly();
        SupplementalItems = _supplementalItems.Values.OrderBy(value => value.Item.DefinitionId).ToList().AsReadOnly();
    }

    public ReadOnlyCollection<StructuralBodyMass> Bodies { get; }
    public ReadOnlyCollection<StructuralItemMass> SupplementalItems { get; }

    /// <summary>
    /// Splits the installed output's actual mass by exact member volume. Fractional milligrams are
    /// floored, then the remaining milligrams go to ascending canonical member ordinals.
    /// </summary>
    public IReadOnlyList<MassMilligrams> MemberMasses(ConstructionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        CraftRecipeDefinition recipe = definition.MaterialTransform;
        ItemPhysicalBinding installed = recipe.BindingFor(recipe.ItemFor(recipe.Outputs[(int)definition.InstalledOutputIndex]));
        ulong totalMass = installed.Physical.MassPerUnit.Value;
        ulong totalVolume = installed.Physical.VolumePerUnit.Value;
        MassMilligrams[] result = new MassMilligrams[definition.Members.Count];
        ulong assigned = 0;
        for (int index = 0; index < result.Length; index++)
        {
            ulong share = checked((ulong)(checked((UInt128)totalMass * definition.Members[index].VolumeMicrolitres) / totalVolume));
            result[index] = new MassMilligrams(share);
            assigned = checked(assigned + share);
        }
        ulong remainder = checked(totalMass - assigned);
        for (int index = 0; (ulong)index < remainder; index++)
        {
            result[index] = result[index].Add(new MassMilligrams(1));
        }
        return Array.AsReadOnly(result);
    }

    /// <summary>
    /// Adds the pinned body and live carried goods. In a structural world, builder-carried project
    /// custody also counts; installed backing belongs to members instead. Reservations and equipment
    /// do not remove carried mass, and retired ingredients are not live lots.
    /// </summary>
    public UInt128 ActorLoad(WorldSnapshot world, EntityId actorId)
    {
        ArgumentNullException.ThrowIfNull(world);
        var actor = world.Space.GetActor(actorId);
        if (!_bodies.TryGetValue(actor.Binding.DefinitionId, out StructuralBodyMass? body)
            || body.BodyVersion != actor.Binding.DefinitionVersion)
        {
            throw new ArgumentException("The actor requires an exact body mass calibration.", nameof(world));
        }
        HashSet<EntityId> carried = [world.GetResident(actorId).InventoryCustodianId];
        HashSet<EntityId> installedBacking = [];
        if (world.StructuralPolicy is not null && world.Construction is { } construction)
        {
            foreach (ConstructionProject project in construction.Projects.Where(value => value.BuilderActorId == actorId))
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
        Dictionary<EntityId, StructuralItemMass> masses = ResolveItems(world.Production?.Recipes.AsEnumerable() ?? []);
        UInt128 load = body.Mass.Value;
        foreach (CountedSourceLot lot in world.Inventory.Custody.Lots)
        {
            if (carried.Contains(lot.CustodianId))
            {
                load = checked(load + LotMass(masses, lot.Definition, lot.Quantity));
            }
        }
        if (world.Production is { } production)
        {
            foreach (TransformedLot lot in production.Lots)
            {
                if (carried.Contains(lot.CustodianId) && !installedBacking.Contains(lot.LotId))
                {
                    load = checked(load + LotMass(masses, lot.Definition, lot.Quantity));
                }
            }
        }
        return load;
    }

    private Dictionary<EntityId, StructuralItemMass> ResolveItems(IEnumerable<CraftRecipeDefinition> recipes)
    {
        Dictionary<EntityId, StructuralItemMass> result = new(_supplementalItems);
        Dictionary<EntityId, ItemPhysicalBinding> bindings = [];
        foreach (CraftRecipeDefinition recipe in recipes)
        {
            foreach (ItemPhysicalBinding binding in recipe.Bindings)
            {
                if (bindings.TryGetValue(binding.Item.DefinitionId, out ItemPhysicalBinding? prior)
                    && (prior.Item != binding.Item || prior.Physical.Pin() != binding.Physical.Pin()))
                {
                    throw new ArgumentException("Recipe mass calibrations require consistent item and physical material pins.", nameof(recipes));
                }
                bindings[binding.Item.DefinitionId] = binding;
                if (result.TryGetValue(binding.Item.DefinitionId, out StructuralItemMass? existing)
                    && (existing.Item != binding.Item || existing.MassPerUnit != binding.Physical.MassPerUnit))
                {
                    throw new ArgumentException("Supplemental item masses must match the exact production item pin and calibration.", nameof(recipes));
                }
                result[binding.Item.DefinitionId] = new StructuralItemMass(binding.Item, binding.Physical.MassPerUnit);
            }
        }
        return result;
    }

    private static UInt128 LotMass(Dictionary<EntityId, StructuralItemMass> masses,
        CountedItemDefinition item, ulong quantity)
    {
        if (!masses.TryGetValue(item.DefinitionId, out StructuralItemMass? mass) || mass.Item != item)
        {
            throw new ArgumentException("Every carried item requires an exact positive mass calibration.", nameof(item));
        }
        return checked((UInt128)mass.MassPerUnit.Value * quantity);
    }
}
