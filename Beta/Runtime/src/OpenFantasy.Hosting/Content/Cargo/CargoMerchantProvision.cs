using OpenFantasy.Core.Content;

namespace OpenFantasy.Hosting.Content.Cargo;

/// <summary>
/// Declared finite authoring for one merchant: opening units of the shared good, store capacity and purse. These are
/// new versioned facts of the cargo world, never derived from source prices or from another actor's holdings.
/// </summary>
public sealed class CargoMerchantProvision
{
    public CargoMerchantProvision(string merchantSourceId, ulong openingUnits, ulong storageCapacity, ulong purseGil)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(merchantSourceId);
        if (storageCapacity == 0 || openingUnits > storageCapacity)
        {
            throw new ArgumentException("A merchant's opening units must fit its positive store capacity.");
        }
        MerchantSourceId = merchantSourceId;
        OpeningUnits = openingUnits;
        StorageCapacity = storageCapacity;
        PurseGil = purseGil;
        Version = CargoCanonicalFrame.Hash("cargo-world.provision.v2", [CargoCanonicalFrame.Text(merchantSourceId),
            CargoCanonicalFrame.Unsigned64(openingUnits), CargoCanonicalFrame.Unsigned64(storageCapacity),
            CargoCanonicalFrame.Unsigned64(purseGil)]);
    }

    public string MerchantSourceId { get; }
    public ulong OpeningUnits { get; }
    public ulong StorageCapacity { get; }
    public ulong PurseGil { get; }
    public ContentHash Version { get; }
}
