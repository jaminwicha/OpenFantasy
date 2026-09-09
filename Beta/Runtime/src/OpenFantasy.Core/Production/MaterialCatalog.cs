using System.Collections.ObjectModel;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

/// <summary>A finite immutable set of exact material versions available for recipe resolution.</summary>
public sealed class MaterialCatalog
{
    public const int MaximumDefinitionCount = 64;
    private readonly Dictionary<EntityId, MaterialDefinition> _byId = [];

    public MaterialCatalog(IEnumerable<MaterialDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        List<MaterialDefinition> copied = [];
        HashSet<(string Package, string Key)> names = [];
        foreach (MaterialDefinition definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            if (copied.Count == MaximumDefinitionCount)
            {
                throw new ArgumentException("The material catalog exceeds its definition limit.", nameof(definitions));
            }
            if (!_byId.TryAdd(definition.DefinitionId, definition) ||
                !names.Add((definition.Package.Value, definition.Key)))
            {
                throw new ArgumentException("Material identities and package keys must be unique.", nameof(definitions));
            }
            copied.Add(definition);
        }
        copied.Sort((left, right) => left.DefinitionId.CompareTo(right.DefinitionId));
        Definitions = copied.AsReadOnly();
    }

    public ReadOnlyCollection<MaterialDefinition> Definitions { get; }

    public MaterialDefinition Resolve(CountedItemDefinition pin)
    {
        ArgumentNullException.ThrowIfNull(pin);
        if (!_byId.TryGetValue(pin.DefinitionId, out MaterialDefinition? definition))
        {
            throw new ArgumentException("A recipe references an unknown material definition.", nameof(pin));
        }
        if (definition.Version != pin.Version)
        {
            throw new ArgumentException("A recipe material pin does not match its available definition version.", nameof(pin));
        }
        return definition;
    }
}
