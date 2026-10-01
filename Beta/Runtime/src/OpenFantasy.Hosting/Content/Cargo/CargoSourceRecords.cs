using OpenFantasy.Core.Content;

namespace OpenFantasy.Hosting.Content.Cargo;

/// <summary>
/// One retained legacy region rectangle in source tiles. Source tiles map one-to-one to terrain cells, so
/// containment is checked in the same coordinates the farm admission already uses.
/// </summary>
public sealed record CargoSourcePlace(string SourceId, long MinimumX, long MinimumY, int Width, int Depth,
    string Pointer, ContentHash PayloadHash)
{
    public bool Contains(long tileX, long tileY) =>
        tileX >= MinimumX && tileX < MinimumX + Width && tileY >= MinimumY && tileY < MinimumY + Depth;

    /// <summary>The place's tiles as a half-open rectangle in the shared tile/cell plane.</summary>
    public CargoTileRect Tiles => new(MinimumX, MinimumY, MinimumX + Width, MinimumY + Depth);
}

/// <summary>A half-open tile rectangle <c>[x0,x1)×[y0,y1)</c>; source tiles map one-to-one to terrain cells.</summary>
public readonly record struct CargoTileRect(long MinimumX, long MinimumY, long MaximumX, long MaximumY)
{
    public long Area => (MaximumX - MinimumX) * (MaximumY - MinimumY);

    /// <summary>The common tiles of two rectangles, or null when they share none.</summary>
    public CargoTileRect? Intersect(CargoTileRect other)
    {
        var common = new CargoTileRect(Math.Max(MinimumX, other.MinimumX), Math.Max(MinimumY, other.MinimumY),
            Math.Min(MaximumX, other.MaximumX), Math.Min(MaximumY, other.MaximumY));
        return common.MinimumX < common.MaximumX && common.MinimumY < common.MaximumY ? common : null;
    }

    public override string ToString() => string.Create(System.Globalization.CultureInfo.InvariantCulture,
        $"[{MinimumX},{MaximumX})x[{MinimumY},{MaximumY})");
}

/// <summary>One retained item record; its source price is cited by an authored market rule and never edited.</summary>
public sealed record CargoSourceGood(string SourceId, string Name, string Kind, ulong SourcePrice, string Pointer,
    ContentHash PayloadHash);

/// <summary>
/// One retained merchant record with its original identity, tile, place, dialogue, shop list, quests, wander
/// radius and appearance. The payload hash covers the whole record in the source packet's canonical form.
/// </summary>
public sealed record CargoSourceMerchant(string SourceId, string Name, long TileX, long TileY, CargoSourcePlace Place,
    string Dialogue, IReadOnlyList<string> Shop, IReadOnlyList<string> Quests, int WanderRadius,
    ContentHash AppearanceHash, string Pointer, ContentHash PayloadHash);
