using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Tests.Production;

public sealed class MaterialRecipeTests
{
    [Fact]
    public void EveryInputMilligramIsAccountedForIncludingExplicitWaste()
    {
        MaterialRecipeDefinition recipe = ProductionFixture.Create();
        Assert.Equal(new MassMilligrams(90_000), recipe.InputMass);
        Assert.Equal(new MassMilligrams(80_000), recipe.OutputMass);
        Assert.Equal(new MassMilligrams(10_000), recipe.WasteMass);
        Assert.Equal(recipe.InputMass, recipe.OutputMass.Add(recipe.WasteMass));
        Assert.Throws<ArgumentException>(() => ProductionFixture.Create(recipe.Terms.Where(term => term.Role != RecipeMaterialRole.Waste)));
        RecipeTerm output = recipe.Terms.Single(term => term.Role == RecipeMaterialRole.Output);
        Assert.Throws<ArgumentException>(() => ProductionFixture.Create(recipe.Terms.Where(term => term.Role != RecipeMaterialRole.Output)
            .Append(new RecipeTerm(RecipeMaterialRole.Output, output.Material, output.Count + 1))));
    }

    [Fact]
    public void VolumeChangeIsRepresentableWithoutAllowingMassLoss()
    {
        MaterialRecipeDefinition recipe = ProductionFixture.Create();
        VolumeMicrolitres inputVolume = default;
        VolumeMicrolitres resultVolume = default;
        foreach (RecipeTerm term in recipe.Terms)
        {
            MaterialDefinition material = recipe.Materials.Single(value => value.DefinitionId == term.Material.DefinitionId);
            VolumeMicrolitres volume = material.VolumePerUnit.Multiply(term.Count);
            if (term.Role == RecipeMaterialRole.Input)
            {
                inputVolume = inputVolume.Add(volume);
            }
            else
            {
                resultVolume = resultVolume.Add(volume);
            }
        }
        Assert.Equal(new VolumeMicrolitres(160_000), inputVolume);
        Assert.Equal(new VolumeMicrolitres(115_000), resultVolume);
        Assert.NotEqual(inputVolume, resultVolume);
        Assert.Equal(recipe.InputMass, recipe.OutputMass.Add(recipe.WasteMass));
    }

    [Fact]
    public void MaterialAndTermInputPermutationCannotChangeRecipeIdentity()
    {
        MaterialRecipeDefinition baseline = ProductionFixture.Create();
        MaterialRecipeDefinition reversed = ProductionFixture.Create(ProductionFixture.Terms().Reverse(),
            new MaterialCatalog(ProductionFixture.Materials().Reverse()));
        Assert.Equal(baseline.Version, reversed.Version);
        Assert.Equal(MaterialRecipeCodecV1.Encode(baseline), MaterialRecipeCodecV1.Encode(reversed));
    }

    [Fact]
    public void MissingDefinitionsConflictingPinsAndDuplicateRolesReject()
    {
        RecipeTerm[] terms = ProductionFixture.Terms();
        Assert.Throws<ArgumentException>(() => ProductionFixture.Create(terms,
            new MaterialCatalog(ProductionFixture.Materials().Skip(1))));
        CountedItemDefinition stale = new(terms[0].Material.DefinitionId, ContentHash.Compute("different version"u8));
        Assert.Throws<ArgumentException>(() => ProductionFixture.Create(terms.Skip(1)
            .Prepend(new RecipeTerm(RecipeMaterialRole.Input, stale, terms[0].Count))));
        Assert.Throws<ArgumentException>(() => ProductionFixture.Create(terms.Append(terms[0])));
        Assert.Throws<ArgumentException>(() => ProductionFixture.Create(terms.Append(
            new RecipeTerm(RecipeMaterialRole.Waste, stale, 1))));
    }

    [Fact]
    public void CatalogRejectsDuplicateIdentitiesAndAmbiguousPackageKeys()
    {
        MaterialDefinition first = ProductionFixture.Materials()[0];
        Assert.Throws<ArgumentException>(() => new MaterialCatalog([first, first]));
        Assert.Throws<ArgumentException>(() => new MaterialCatalog([first,
            new MaterialDefinition(first.Package, new EntityId(0, 999), first.Key,
                first.MassPerUnit, first.VolumePerUnit, first.Hardness)]));
    }

    [Fact]
    public void CatalogAndRecipeCopyTheirInputsAndExposeReadOnlyValues()
    {
        List<MaterialDefinition> materials = ProductionFixture.Materials().ToList();
        List<RecipeTerm> terms = ProductionFixture.Terms().ToList();
        MaterialCatalog catalog = new(materials);
        MaterialRecipeDefinition recipe = ProductionFixture.Create(terms, catalog);
        materials.Clear();
        terms.Clear();
        Assert.Equal(4, catalog.Definitions.Count);
        Assert.Equal(4, recipe.Terms.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<MaterialDefinition>)catalog.Definitions).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<MaterialDefinition>)recipe.Materials).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<RecipeTerm>)recipe.Terms).Clear());
        Assert.Equal(ProductionFixture.Bytes("recipeHex"), MaterialRecipeCodecV1.Encode(recipe));
    }

    [Fact]
    public void DefinitionVersionBindsEveryPhysicalCalibrationAndRecipeWorkAndLineage()
    {
        MaterialDefinition source = ProductionFixture.Materials()[0];
        MaterialDefinition[] changed =
        [
            new(source.Package, source.DefinitionId, source.Key, new MassMilligrams(20_001), source.VolumePerUnit, source.Hardness),
            new(source.Package, source.DefinitionId, source.Key, source.MassPerUnit, new VolumeMicrolitres(30_001), source.Hardness),
            new(source.Package, source.DefinitionId, source.Key, source.MassPerUnit, source.VolumePerUnit, new HardnessRating(2))
        ];
        Assert.All(changed, material => Assert.NotEqual(source.Version, material.Version));
        MaterialRecipeDefinition baseline = ProductionFixture.Create();
        Assert.NotEqual(baseline.Version, ProductionFixture.Create(work: new WorkUnits(121)).Version);
        Assert.NotEqual(baseline.Version, ProductionFixture.Create(sourceHash: ContentHash.Compute("another source"u8)).Version);
    }

    [Fact]
    public void CheckedQuantitiesRejectMultiplicationAdditionAndAccountingOverflow()
    {
        Assert.Throws<OverflowException>(() => new MassMilligrams(ulong.MaxValue).Multiply(2));
        Assert.Throws<OverflowException>(() => new MassMilligrams(ulong.MaxValue).Add(new MassMilligrams(1)));
        Assert.Throws<OverflowException>(() => new VolumeMicrolitres(ulong.MaxValue).Multiply(2));
        Assert.Throws<OverflowException>(() => new VolumeMicrolitres(ulong.MaxValue).Add(new VolumeMicrolitres(1)));
        RecipeTerm[] terms = ProductionFixture.Terms();
        Assert.Throws<OverflowException>(() => ProductionFixture.Create(terms.Skip(1).Prepend(
            new RecipeTerm(RecipeMaterialRole.Input, terms[0].Material, ulong.MaxValue))));
        MaterialDefinition unit = Unit(1);
        MaterialRecipeDefinition maximum = new(ProductionFixture.Package, new EntityId(0, 900), "maximum",
            new WorkUnits(ulong.MaxValue), null, new MaterialCatalog([unit]),
            [new RecipeTerm(RecipeMaterialRole.Input, unit.Pin(), ulong.MaxValue),
             new RecipeTerm(RecipeMaterialRole.Output, unit.Pin(), ulong.MaxValue)]);
        Assert.Equal(ulong.MaxValue, maximum.InputMass.Value);
        Assert.Equal(maximum.Version, MaterialRecipeCodecV1.Decode(MaterialRecipeCodecV1.Encode(maximum)).Version);
    }

    [Fact]
    public void ExactCatalogAndTermCapacityWorksAndExcessEnumerationStops()
    {
        MaterialDefinition[] materials = Enumerable.Range(1, MaterialCatalog.MaximumDefinitionCount).Select(index => Unit((ulong)index)).ToArray();
        MaterialCatalog catalog = new(materials);
        Assert.Equal(64, catalog.Definitions.Count);
        RecipeTerm[] terms = materials.Take(32).Select(material => new RecipeTerm(RecipeMaterialRole.Input, material.Pin(), 1))
            .Concat(materials.Take(32).Select(material => new RecipeTerm(RecipeMaterialRole.Output, material.Pin(), 1))).ToArray();
        MaterialRecipeDefinition recipe = new(ProductionFixture.Package, new EntityId(0, 900), "bounded",
            new WorkUnits(1), null, catalog, terms);
        Assert.Equal(64, recipe.Terms.Count);
        Assert.Equal(32, recipe.Materials.Count);
        Assert.Equal(recipe.Version, MaterialRecipeCodecV1.Decode(MaterialRecipeCodecV1.Encode(recipe)).Version);
        int yielded = 0;
        IEnumerable<RecipeTerm> ExcessTerms()
        {
            foreach (RecipeTerm term in terms)
            {
                yielded++;
                yield return term;
            }
            while (true)
            {
                yielded++;
                yield return new RecipeTerm(RecipeMaterialRole.Waste, materials[0].Pin(), 1);
            }
        }
        Assert.Throws<ArgumentException>(() => new MaterialRecipeDefinition(ProductionFixture.Package,
            new EntityId(0, 900), "bounded", new WorkUnits(1), null, catalog, ExcessTerms()));
        Assert.Equal(65, yielded);
        int materialCount = 0;
        IEnumerable<MaterialDefinition> ExcessMaterials()
        {
            while (true)
            {
                materialCount++;
                yield return Unit((ulong)materialCount);
            }
        }
        Assert.Throws<ArgumentException>(() => new MaterialCatalog(ExcessMaterials()));
        Assert.Equal(65, materialCount);
    }

    [Fact]
    public void NonpositiveAndUnsupportedValuesAndMissingRolesReject()
    {
        MaterialDefinition first = ProductionFixture.Materials()[0];
        Assert.Throws<ArgumentOutOfRangeException>(() => new MaterialDefinition(first.Package, first.DefinitionId,
            first.Key, default, first.VolumePerUnit, first.Hardness));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MaterialDefinition(first.Package, first.DefinitionId,
            first.Key, first.MassPerUnit, default, first.Hardness));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProductionFixture.Create(work: default(WorkUnits)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecipeTerm((RecipeMaterialRole)0, first.Pin(), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecipeTerm(RecipeMaterialRole.Input, first.Pin(), 0));
        Assert.Throws<ArgumentException>(() => ProductionFixture.Create([]));
        Assert.Throws<ArgumentException>(() => ProductionFixture.Create(ProductionFixture.Terms().Where(term => term.Role != RecipeMaterialRole.Input)));
        Assert.Throws<ArgumentException>(() => ProductionFixture.Create(ProductionFixture.Terms().Where(term => term.Role != RecipeMaterialRole.Output)));
        Assert.Throws<ArgumentNullException>(() => new MaterialCatalog([null!]));
    }

    private static MaterialDefinition Unit(ulong id) => new(ProductionFixture.Package, new EntityId(0, id),
        "material_" + id, new MassMilligrams(1), new VolumeMicrolitres(1), default);
}
