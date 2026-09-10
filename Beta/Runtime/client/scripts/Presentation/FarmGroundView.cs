using Godot;

namespace OpenFantasy.Client.Presentation;

/// <summary>Draws retained walkable source floors; omitted source props never become fake colliders.</summary>
internal partial class FarmGroundView : Node2D
{
    private FarmVisualAssets _assets = null!;

    internal void Setup(FarmVisualAssets assets)
    {
        _assets = assets;
        TextureFilter = TextureFilterEnum.Nearest;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_assets is null)
        {
            return;
        }
        for (int y = 0; y < FarmCanvasCoordinates.RegionDepth; y++)
        {
            for (int x = 0; x < FarmCanvasCoordinates.RegionWidth; x++)
            {
                Vector2I atlas = AtlasCoordinate(_assets.GroundAt(x, y), x + FarmCanvasCoordinates.MinimumCellX,
                    y + FarmCanvasCoordinates.MinimumCellY);
                var destination = new Rect2(x * 32, y * 32, 32, 32);
                DrawTextureRectRegion(_assets.Atlas, destination, new Rect2(atlas.X * 16, atlas.Y * 16, 16, 16));
            }
        }
        DrawRect(FarmCanvasCoordinates.LocalBounds, new Color("283b33"), filled: false, width: 2);
    }

    private static Vector2I AtlasCoordinate(int tile, int x, int y)
    {
        // Preserve the predecessor's explicitly wrapping visual-only variant selector.
        int variant = unchecked((x * 73_856_093) ^ (y * 19_349_663)) & 0x7fffffff;
        return tile switch
        {
            0 => new Vector2I(variant % 3, 0),
            3 => new Vector2I(7 + variant % 3, 0),
            4 => new Vector2I(10 + variant % 2, 0),
            11 => new Vector2I(9 + variant % 2, 1),
            24 => new Vector2I(3, 5),
            27 => new Vector2I(6, 5),
            28 => new Vector2I(0, 6),
            _ => throw new InvalidDataException("An unadmitted floor tile reached the farm view.")
        };
    }
}
