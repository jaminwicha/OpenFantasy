using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

public enum RecipeMaterialRole
{
    Input = 1,
    Output = 2,
    Waste = 3
}

/// <summary>A positive count of one exact material version in one explicit transform role.</summary>
public sealed record RecipeTerm
{
    public RecipeTerm(RecipeMaterialRole role, CountedItemDefinition material, ulong count)
    {
        ArgumentNullException.ThrowIfNull(material);
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }
        if (count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        Role = role;
        Material = material;
        Count = count;
    }

    public RecipeMaterialRole Role { get; }
    public CountedItemDefinition Material { get; }
    public ulong Count { get; }
}
