using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

/// <summary>
/// Pins the physical calibration of one counted material unit. Defining an output material does
/// not manufacture a lot or replace the input claims of a transformed mixture.
/// </summary>
public sealed class MaterialDefinition
{
    public MaterialDefinition(PackageId package, EntityId definitionId, string key,
        MassMilligrams massPerUnit, VolumeMicrolitres volumePerUnit, HardnessRating hardness)
    {
        ArgumentNullException.ThrowIfNull(package);
        ProductionDefinitionRules.ValidateKey(key);
        if (massPerUnit.Value == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(massPerUnit), "A counted ordinary material unit must have positive mass.");
        }
        if (volumePerUnit.Value == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(volumePerUnit), "A counted ordinary material unit must occupy positive volume.");
        }
        Package = package;
        DefinitionId = definitionId;
        Key = key;
        MassPerUnit = massPerUnit;
        VolumePerUnit = volumePerUnit;
        Hardness = hardness;
        Version = ContentHash.Compute(MaterialRecipeCodecV1.EncodeMaterial(this));
    }

    public PackageId Package { get; }
    public EntityId DefinitionId { get; }
    public string Key { get; }
    public MassMilligrams MassPerUnit { get; }
    public VolumeMicrolitres VolumePerUnit { get; }
    public HardnessRating Hardness { get; }
    public ContentHash Version { get; }

    public CountedItemDefinition Pin() => new(DefinitionId, Version);
}
