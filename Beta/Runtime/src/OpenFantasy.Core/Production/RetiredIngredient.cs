using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

/// <summary>Permanent consumed rights. Its two constructors form an explicit raw/derived provenance union.</summary>
public sealed class RetiredIngredient
{
    public RetiredIngredient(EntityId lotId, ulong revision, CountedItemDefinition definition, SourceInterval source)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(source);
        LotId = lotId;
        Revision = revision;
        Definition = definition;
        Kind = IngredientLotKind.Source;
        Source = source;
    }

    public RetiredIngredient(EntityId lotId, ulong revision, CountedItemDefinition definition, ProductionOutputInterval output)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(output);
        LotId = lotId;
        Revision = revision;
        Definition = definition;
        Kind = IngredientLotKind.Transformed;
        Output = output;
    }

    public EntityId LotId { get; }
    public ulong Revision { get; }
    public CountedItemDefinition Definition { get; }
    public IngredientLotKind Kind { get; }
    public SourceInterval? Source { get; }
    public ProductionOutputInterval? Output { get; }
    public ulong Quantity => Source?.Quantity ?? Output!.Quantity;
    internal ulong Start => Source?.Start ?? Output!.Start;
    internal ulong End => Source?.End ?? Output!.End;
}
