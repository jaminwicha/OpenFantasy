namespace OpenFantasy.Core.Production;

public enum IngredientLotKind
{
    Source = 1,
    Transformed = 2
}

public sealed record IngredientRequest
{
    public IngredientRequest(IngredientLotKind kind, EntityId lotId, ulong expectedRevision, ulong quantity)
    {
        if (!Enum.IsDefined(kind) || quantity == 0)
        {
            throw new ArgumentException("An ingredient selection requires a known provenance family and positive quantity.");
        }
        Kind = kind;
        LotId = lotId;
        ExpectedRevision = expectedRevision;
        Quantity = quantity;
    }

    public IngredientLotKind Kind { get; }
    public EntityId LotId { get; }
    public ulong ExpectedRevision { get; }
    public ulong Quantity { get; }
}
