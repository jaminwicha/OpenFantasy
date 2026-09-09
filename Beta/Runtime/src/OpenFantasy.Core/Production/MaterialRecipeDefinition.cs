using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Production;

/// <summary>
/// One finite ordinary-material transform with exact input/output/waste mass accounting.
/// This definition neither runs crafting nor assigns transformed output provenance or export rights.
/// </summary>
public sealed class MaterialRecipeDefinition
{
    public const int MaximumTermCount = 64;

    public MaterialRecipeDefinition(PackageId package, EntityId definitionId, string key,
        WorkUnits requiredWork, ContentHash? sourceRecipePayloadHash,
        MaterialCatalog catalog, IEnumerable<RecipeTerm> terms)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(terms);
        ProductionDefinitionRules.ValidateKey(key);
        if (requiredWork.Value == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requiredWork));
        }

        List<RecipeTerm> copied = [];
        HashSet<(RecipeMaterialRole Role, EntityId Id)> roleIds = [];
        Dictionary<EntityId, MaterialDefinition> usedMaterials = [];
        MassMilligrams inputMass = default;
        MassMilligrams outputMass = default;
        MassMilligrams wasteMass = default;
        bool hasInput = false;
        bool hasOutput = false;
        foreach (RecipeTerm term in terms)
        {
            ArgumentNullException.ThrowIfNull(term);
            if (copied.Count == MaximumTermCount)
            {
                throw new ArgumentException("The recipe exceeds its material-term limit.", nameof(terms));
            }
            if (!roleIds.Add((term.Role, term.Material.DefinitionId)))
            {
                throw new ArgumentException("A material can occur only once in each recipe role.", nameof(terms));
            }
            MaterialDefinition material = catalog.Resolve(term.Material);
            usedMaterials.TryAdd(material.DefinitionId, material);
            MassMilligrams mass = material.MassPerUnit.Multiply(term.Count);
            switch (term.Role)
            {
                case RecipeMaterialRole.Input:
                    inputMass = inputMass.Add(mass);
                    hasInput = true;
                    break;
                case RecipeMaterialRole.Output:
                    outputMass = outputMass.Add(mass);
                    hasOutput = true;
                    break;
                case RecipeMaterialRole.Waste:
                    wasteMass = wasteMass.Add(mass);
                    break;
            }
            copied.Add(term);
        }
        if (!hasInput || !hasOutput)
        {
            throw new ArgumentException("A recipe requires at least one input and one output.", nameof(terms));
        }
        if (inputMass != outputMass.Add(wasteMass))
        {
            throw new ArgumentException("All input mass must remain in explicit outputs and waste.", nameof(terms));
        }

        copied.Sort(CompareTerms);
        Package = package;
        DefinitionId = definitionId;
        Key = key;
        RequiredWork = requiredWork;
        SourceRecipePayloadHash = sourceRecipePayloadHash;
        Terms = copied.AsReadOnly();
        Materials = usedMaterials.Values.OrderBy(material => material.DefinitionId).ToList().AsReadOnly();
        InputMass = inputMass;
        OutputMass = outputMass;
        WasteMass = wasteMass;
        Version = ContentHash.Compute(MaterialRecipeCodecV1.Encode(this));
    }

    public PackageId Package { get; }
    public EntityId DefinitionId { get; }
    public string Key { get; }
    public WorkUnits RequiredWork { get; }
    public ContentHash? SourceRecipePayloadHash { get; }
    public ReadOnlyCollection<RecipeTerm> Terms { get; }
    public ReadOnlyCollection<MaterialDefinition> Materials { get; }
    public MassMilligrams InputMass { get; }
    public MassMilligrams OutputMass { get; }
    public MassMilligrams WasteMass { get; }
    public ContentHash Version { get; }

    internal static int CompareTerms(RecipeTerm left, RecipeTerm right)
    {
        int roleOrder = left.Role.CompareTo(right.Role);
        return roleOrder != 0 ? roleOrder : left.Material.DefinitionId.CompareTo(right.Material.DefinitionId);
    }
}
