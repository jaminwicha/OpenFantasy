using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Structures;

/// <summary>Explicit positive mass for one exact body definition; dimensions do not imply density.</summary>
public sealed record StructuralBodyMass
{
    public StructuralBodyMass(EntityId definitionId, ContentHash bodyVersion, MassMilligrams mass)
    {
        ArgumentNullException.ThrowIfNull(bodyVersion);
        if (mass.Value == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mass));
        }
        DefinitionId = definitionId;
        BodyVersion = bodyVersion;
        Mass = mass;
    }

    public EntityId DefinitionId { get; }
    public ContentHash BodyVersion { get; }
    public MassMilligrams Mass { get; }
}
