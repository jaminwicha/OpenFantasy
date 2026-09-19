using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Geography;

/// <summary>Declared impermeability for one pinned solid, with a continuous minimum normal thickness.</summary>
public sealed record ReservoirSealRule
{
    public ReservoirSealRule(CountedItemDefinition material, uint minimumThicknessMillimetres)
    {
        ArgumentNullException.ThrowIfNull(material);
        if (minimumThicknessMillimetres is 0 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumThicknessMillimetres));
        }
        Material = material;
        MinimumThicknessMillimetres = minimumThicknessMillimetres;
    }

    public CountedItemDefinition Material { get; }
    public uint MinimumThicknessMillimetres { get; }
}

/// <summary>An explicit finite sealing profile, separate from hardness, strength, collision and fluid reactions.</summary>
public sealed class ReservoirSealPolicy
{
    public const int MaximumRules = 64;

    public ReservoirSealPolicy(CountedItemDefinition liquid, IEnumerable<ReservoirSealRule> rules)
    {
        ArgumentNullException.ThrowIfNull(liquid);
        ArgumentNullException.ThrowIfNull(rules);
        var copy = rules.Take(MaximumRules + 1).ToArray();
        if (copy.Length is 0 or > MaximumRules || copy.Any(r => r is null)
            || copy.Select(r => r.Material.DefinitionId).Distinct().Count() != copy.Length
            || copy.Any(r => r.Material.DefinitionId == liquid.DefinitionId))
        {
            throw new ArgumentException("Sealing needs finite unique solid material pins distinct from its liquid.");
        }
        Liquid = liquid;
        Rules = copy.OrderBy(r => r.Material.DefinitionId).ToList().AsReadOnly();
        Version = ContentHash.Compute(ReservoirSealPolicyCodecV1.Encode(this));
    }

    public CountedItemDefinition Liquid { get; }
    public ReadOnlyCollection<ReservoirSealRule> Rules { get; }
    public ContentHash Version { get; }
    public uint? MinimumThicknessFor(CountedItemDefinition material) =>
        Rules.SingleOrDefault(r => r.Material == material)?.MinimumThicknessMillimetres;
}
