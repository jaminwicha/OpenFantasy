using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

/// <summary>Canonical finite material/process definitions with strict pin and accounting validation.</summary>
public static class MaterialRecipeCodecV1
{
    public const int MaximumRecipeByteLength = 64 * 1024;
    private const int MaximumMaterialByteLength = 512;
    private const int MaximumCatalogByteLength = 40 * 1024;
    private const int MaximumTermByteLength = 192;
    private const int MaximumTermSetByteLength = 16 * 1024;
    private const ulong ContractVersion = 1;

    public static byte[] Encode(MaterialRecipeDefinition recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        List<ReadOnlyMemory<byte>> materials = [CanonicalEncodingV1.Unsigned64(ContractVersion)];
        foreach (MaterialDefinition material in recipe.Materials)
        {
            materials.Add(EncodeMaterial(material));
        }
        List<ReadOnlyMemory<byte>> terms = [CanonicalEncodingV1.Unsigned64(ContractVersion)];
        foreach (RecipeTerm term in recipe.Terms)
        {
            terms.Add(CanonicalEncodingV1.Frame("production.recipe-term",
                CanonicalEncodingV1.Unsigned64(ContractVersion), CanonicalEncodingV1.Unsigned64((ulong)term.Role),
                term.Material.DefinitionId.ToByteArray(), term.Material.Version.ToByteArray(),
                CanonicalEncodingV1.Unsigned64(term.Count)));
        }
        byte[] sourceHash = recipe.SourceRecipePayloadHash?.ToByteArray() ?? [];
        return CanonicalEncodingV1.Frame("production.recipe", CanonicalEncodingV1.Unsigned64(ContractVersion),
            CanonicalEncodingV1.Text(recipe.Package.Value, nameof(recipe.Package)), recipe.DefinitionId.ToByteArray(),
            CanonicalEncodingV1.Text(recipe.Key, nameof(recipe.Key)), CanonicalEncodingV1.Unsigned64(recipe.RequiredWork.Value),
            sourceHash, CanonicalEncodingV1.Frame("production.material-catalog", materials.ToArray()),
            CanonicalEncodingV1.Frame("production.recipe-terms", terms.ToArray()));
    }

    public static MaterialRecipeDefinition Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            CanonicalProductionReader reader = new(bytes, MaximumRecipeByteLength, 10);
            reader.ExpectTag("production.recipe"u8);
            reader.ExpectVersion(ContractVersion);
            PackageId package = new(reader.ReadText(PackageId.MaximumLength));
            EntityId definitionId = reader.ReadIdentity();
            string key = reader.ReadText(ProductionDefinitionRules.MaximumKeyLength);
            WorkUnits work = new(reader.ReadUnsigned64());
            ReadOnlySpan<byte> sourceBytes = reader.ReadField(32);
            ContentHash? sourceHash = sourceBytes.Length == 0 ? null : ReadHash(sourceBytes);
            MaterialCatalog catalog = DecodeCatalog(reader.ReadField(MaximumCatalogByteLength));
            List<RecipeTerm> terms = DecodeTerms(reader.ReadField(MaximumTermSetByteLength));
            reader.Complete();
            MaterialRecipeDefinition recipe = new(package, definitionId, key, work, sourceHash, catalog, terms);
            if (recipe.Materials.Count != catalog.Definitions.Count)
            {
                throw CanonicalProductionReader.Failure(ProductionFormatFailure.NonCanonical,
                    "A canonical recipe contains exactly its referenced material definitions.");
            }
            return recipe;
        }
        catch (ArgumentException)
        {
            throw CanonicalProductionReader.Failure(ProductionFormatFailure.InvalidValue,
                "A material or recipe definition violates its value, identity or reference invariants.");
        }
        catch (OverflowException)
        {
            throw CanonicalProductionReader.Failure(ProductionFormatFailure.InvalidValue,
                "Recipe quantity accounting exceeds its exact supported integer range.");
        }
    }

    public static byte[] EncodeMaterial(MaterialDefinition material)
    {
        ArgumentNullException.ThrowIfNull(material);
        return CanonicalEncodingV1.Frame("production.material", CanonicalEncodingV1.Unsigned64(ContractVersion),
            CanonicalEncodingV1.Text(material.Package.Value, nameof(material.Package)), material.DefinitionId.ToByteArray(),
            CanonicalEncodingV1.Text(material.Key, nameof(material.Key)),
            CanonicalEncodingV1.Unsigned64(material.MassPerUnit.Value),
            CanonicalEncodingV1.Unsigned64(material.VolumePerUnit.Value),
            CanonicalEncodingV1.Unsigned64(material.Hardness.Value));
    }

    private static MaterialCatalog DecodeCatalog(ReadOnlySpan<byte> bytes)
    {
        CanonicalProductionReader reader = new(bytes, MaximumCatalogByteLength, MaterialCatalog.MaximumDefinitionCount + 2);
        reader.ExpectTag("production.material-catalog"u8);
        reader.ExpectVersion(ContractVersion);
        List<MaterialDefinition> materials = [];
        while (reader.RemainingFields > 0)
        {
            MaterialDefinition material = DecodeMaterial(reader.ReadField(MaximumMaterialByteLength));
            if (materials.Count > 0 && materials[^1].DefinitionId >= material.DefinitionId)
            {
                throw CanonicalProductionReader.Failure(ProductionFormatFailure.NonCanonical,
                    "Material definitions must have strictly sorted unique identities.");
            }
            materials.Add(material);
        }
        reader.Complete();
        return new MaterialCatalog(materials);
    }

    private static MaterialDefinition DecodeMaterial(ReadOnlySpan<byte> bytes)
    {
        CanonicalProductionReader reader = new(bytes, MaximumMaterialByteLength, 9);
        reader.ExpectTag("production.material"u8);
        reader.ExpectVersion(ContractVersion);
        PackageId package = new(reader.ReadText(PackageId.MaximumLength));
        EntityId definitionId = reader.ReadIdentity();
        string key = reader.ReadText(ProductionDefinitionRules.MaximumKeyLength);
        MassMilligrams mass = new(reader.ReadUnsigned64());
        VolumeMicrolitres volume = new(reader.ReadUnsigned64());
        ulong hardness = reader.ReadUnsigned64();
        if (hardness > uint.MaxValue)
        {
            throw CanonicalProductionReader.Failure(ProductionFormatFailure.InvalidValue,
                "Material hardness exceeds the supported rule-rating range.");
        }
        reader.Complete();
        return new MaterialDefinition(package, definitionId, key, mass, volume, new HardnessRating((uint)hardness));
    }

    private static List<RecipeTerm> DecodeTerms(ReadOnlySpan<byte> bytes)
    {
        CanonicalProductionReader reader = new(bytes, MaximumTermSetByteLength, MaterialRecipeDefinition.MaximumTermCount + 2);
        reader.ExpectTag("production.recipe-terms"u8);
        reader.ExpectVersion(ContractVersion);
        List<RecipeTerm> terms = [];
        while (reader.RemainingFields > 0)
        {
            RecipeTerm term = DecodeTerm(reader.ReadField(MaximumTermByteLength));
            if (terms.Count > 0 && MaterialRecipeDefinition.CompareTerms(terms[^1], term) >= 0)
            {
                throw CanonicalProductionReader.Failure(ProductionFormatFailure.NonCanonical,
                    "Recipe material roles must be strictly sorted and unique within each role.");
            }
            terms.Add(term);
        }
        reader.Complete();
        return terms;
    }

    private static RecipeTerm DecodeTerm(ReadOnlySpan<byte> bytes)
    {
        CanonicalProductionReader reader = new(bytes, MaximumTermByteLength, 7);
        reader.ExpectTag("production.recipe-term"u8);
        reader.ExpectVersion(ContractVersion);
        ulong role = reader.ReadUnsigned64();
        if (role is not (ulong)RecipeMaterialRole.Input and not (ulong)RecipeMaterialRole.Output
            and not (ulong)RecipeMaterialRole.Waste)
        {
            throw CanonicalProductionReader.Failure(ProductionFormatFailure.UnknownType,
                "The recipe material role is unsupported.");
        }
        EntityId definitionId = reader.ReadIdentity();
        ContentHash version = ReadHash(reader.ReadField(32));
        ulong count = reader.ReadUnsigned64();
        reader.Complete();
        return new RecipeTerm((RecipeMaterialRole)role, new CountedItemDefinition(definitionId, version), count);
    }

    private static ContentHash ReadHash(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 32)
        {
            throw CanonicalProductionReader.Failure(ProductionFormatFailure.InvalidValue,
                "A definition hash must contain exactly thirty-two bytes.");
        }
        return new ContentHash(Convert.ToHexString(bytes).ToLowerInvariant());
    }
}
