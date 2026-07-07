using Godot;
using OpenFantasy.Sim;

namespace OpenFantasy.Client;

/// <summary>
/// Renders the static world from the same world.json the server simulates.
/// Pure presentation — collision truth stays on the server.
/// </summary>
public partial class WorldRenderer : Node2D
{
    public const int TilePx = 16;

    public WorldMap Map { get; private set; } = null!;

    public void Build(WorldMap map)
    {
        Map = map;

        var atlas = GD.Load<Texture2D>("res://assets/tiles.png");
        var tileSet = new TileSet { TileSize = new Vector2I(TilePx, TilePx) };
        var source = new TileSetAtlasSource
        {
            Texture = atlas,
            TextureRegionSize = new Vector2I(TilePx, TilePx),
        };
        int tileKinds = atlas.GetWidth() / TilePx;
        for (int i = 0; i < tileKinds; i++)
            source.CreateTile(new Vector2I(i, 0));
        tileSet.AddSource(source, 0);

        var layer = new TileMapLayer { TileSet = tileSet };
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                int t = map.TileAt(x, y);
                if (t >= 0 && t < tileKinds)
                    layer.SetCell(new Vector2I(x, y), 0, new Vector2I(t, 0));
            }
        AddChild(layer);
    }

    /// <summary>World-unit position (sim space) → pixel position.</summary>
    public static Vector2 UnitsToPixels(int ux, int uy)
        => new(ux * (float)TilePx / SimConstants.UnitsPerTile,
               uy * (float)TilePx / SimConstants.UnitsPerTile);
}
