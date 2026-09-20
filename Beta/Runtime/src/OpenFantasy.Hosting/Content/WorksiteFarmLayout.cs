using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Hosting.Content;

/// <summary>Authored metre-grid locations for the fresh worksite; every later journey uses normal movement.</summary>
public static class WorksiteFarmLayout
{
    public const string StairDefinitionKey = "worksite_stairs";
    public const string RemovableDeckDefinitionKey = "worksite_raised_deck";
    public const string DeckQuestKey = "worksite_deck_stock";
    public const string CalibrationNotice = "Bounded worksite demonstration: authored geometry, mass and load ratings; not ordinary timber realism or final balance.";

    public static SubunitPosition StairOrigin { get; } = new(110 * 256, 324 * 256, 256);
    public static SubunitPosition StairApproach { get; } = Feet(105, 324, 256);
    public static SubunitPosition StairWork { get; } = Feet(110, 323, 256);
    public static SubunitPosition FixedDeck { get; } = Feet(113, 326, 832);
    public static SubunitPosition RemovableDeckOrigin { get; } = new(115 * 256, 325 * 256, 256);
    public static SubunitPosition RemovableDeck { get; } = Feet(115, 326, 832);
    public static SubunitPosition RemovableDeckWork { get; } = Feet(114, 325, 256);
    public static SubunitPosition PitApproach { get; } = Feet(116, 324, 256);
    public static SubunitPosition ExcavationStance { get; } = Feet(116, 325, 0);
    public static SubunitPosition PlayerParking { get; } = Feet(103, 327, 256);
    public static CellCoordinate SupportCell { get; } = new(115, 325, 0);
    public static CellCoordinate PitCell { get; } = new(116, 325, 0);

    private static SubunitPosition Feet(long x, long y, long z) => new(x * 256 + 128, y * 256 + 128, z);
}
