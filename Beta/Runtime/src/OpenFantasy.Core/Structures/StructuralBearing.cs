namespace OpenFantasy.Core.Structures;

/// <summary>A positive relative share of the supported element's load, directed toward its support.</summary>
public sealed class StructuralBearing
{
    public StructuralBearing(EntityId bearingId, EntityId supportedElementId, EntityId supportingElementId, ulong weight)
    {
        if (weight == 0 || supportedElementId == supportingElementId)
        {
            throw new ArgumentException("A bearing requires distinct element endpoints and a positive weight.");
        }
        BearingId = bearingId;
        SupportedElementId = supportedElementId;
        SupportingElementId = supportingElementId;
        Weight = weight;
    }

    public EntityId BearingId { get; }
    public EntityId SupportedElementId { get; }
    public EntityId SupportingElementId { get; }
    public ulong Weight { get; }
}
