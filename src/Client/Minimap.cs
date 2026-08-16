using Godot;
using OpenFantasy.Net;
using OpenFantasy.Sim;
using SiteGen = OpenFantasy.Sim.SiteGen;

namespace OpenFantasy.Client;

/// <summary>
/// Auto-map overlay (M key). In a delve it renders the current floor's tiles
/// so the loops read as navigable, not disorienting (Jaquays landmark rule's
/// UI half); on the overworld it shows the full Aldermere map. Player is the
/// gold dot, other players green, monsters red, stairs bright.
/// Pure presentation — reads client state only.
/// </summary>
public partial class Minimap : CanvasLayer
{
    private ClientNode _client = null!;
    private WorldMap _overworld = null!;
    private Control _root = null!;
    private MapCanvas _canvas = null!;
    private Label _title = null!;

    public bool IsOpen => _root.Visible;

    public void Setup(ClientNode client, WorldMap overworld)
    {
        _client = client;
        _overworld = overworld;
        Layer = 20;

        _root = new Control { Visible = false };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(dim);

        var panel = UiTheme.MakePanel();
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.CustomMinimumSize = new Vector2(560, 460);
        panel.Position = new Vector2(-280, -230);
        _root.AddChild(panel);

        _title = UiTheme.MakeLabel("Map", 14);
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        _title.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        _title.OffsetTop = 10;
        panel.AddChild(_title);

        _canvas = new MapCanvas { Position = new Vector2(24, 40), Size = new Vector2(512, 400) };
        panel.AddChild(_canvas);

        var hint = UiTheme.MakeLabel("M / Esc to close", 10);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        hint.OffsetTop = -22;
        hint.Modulate = new Color(0.7f, 0.75f, 0.9f);
        panel.AddChild(hint);
    }

    public void Toggle()
    {
        _root.Visible = !_root.Visible;
        if (_root.Visible) Refresh();
    }

    public void Close() => _root.Visible = false;

    public void Refresh()
    {
        if (!_root.Visible) return;
        _title.Text = _client.CurrentMap?.Label is { Length: > 0 } label ? label : "Aldermere";
        _canvas.Client = _client;
        _canvas.Overworld = _overworld;
        _canvas.QueueRedraw();
    }

    /// <summary>Custom-draw surface: tiles as filled rects, entities as dots.</summary>
    private partial class MapCanvas : Control
    {
        public ClientNode? Client;
        public WorldMap? Overworld;

        private static readonly Color WallCol = new("15131f");
        private static readonly Color FloorCol = new("5a5f6e");
        private static readonly Color WaterCol = new("2f5f9e");
        private static readonly Color HazardCol = new("a3502e");
        private static readonly Color StairsCol = new("f0d060");
        private static readonly Color ChestCol = new("c890f0");

        public override void _Draw()
        {
            if (Client is null) return;
            if (Client.CurrentMap is { } map) DrawInstance(map);
            else if (Overworld is not null) DrawOverworld(Overworld);
        }

        private void DrawInstance(Protocol.MapDataMsg map)
        {
            float s = Mathf.Min(Size.X / map.Width, Size.Y / map.Height);
            var origin = new Vector2((Size.X - map.Width * s) / 2, (Size.Y - map.Height * s) / 2);
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var t = (SiteGen.SiteTile)map.Tiles[y * map.Width + x];
                    Color c = t switch
                    {
                        SiteGen.SiteTile.Wall or SiteGen.SiteTile.SecretDoor or SiteGen.SiteTile.Rubble => WallCol,
                        SiteGen.SiteTile.Water => WaterCol,
                        SiteGen.SiteTile.Slag or SiteGen.SiteTile.Chasm => HazardCol,
                        SiteGen.SiteTile.StairsDown or SiteGen.SiteTile.StairsUp => StairsCol,
                        SiteGen.SiteTile.Chest => ChestCol,
                        SiteGen.SiteTile.LockedDoor or SiteGen.SiteTile.Door => new Color("8a6f4e"),
                        _ => FloorCol,
                    };
                    DrawRect(new Rect2(origin + new Vector2(x * s, y * s), new Vector2(s + 0.5f, s + 0.5f)), c);
                }
            DrawEntities(u => origin + new Vector2(
                u.x * s / SimConstants.UnitsPerTile, u.y * s / SimConstants.UnitsPerTile));
        }

        private void DrawOverworld(WorldMap map)
        {
            float s = Mathf.Min(Size.X / map.Width, Size.Y / map.Height);
            var origin = new Vector2((Size.X - map.Width * s) / 2, (Size.Y - map.Height * s) / 2);
            // 512×512 at sub-pixel scale: sample every 2 tiles for speed.
            for (int y = 0; y < map.Height; y += 2)
                for (int x = 0; x < map.Width; x += 2)
                {
                    int t = map.TileAt(x, y);
                    Color c = t switch
                    {
                        2 or 13 => WaterCol,                    // shallow, ocean
                        14 or 8 => new Color("6e6e78"),         // mountain, rock
                        16 => new Color("dfe6ee"),              // snow
                        17 or 10 => new Color("cbb069"),        // desert, sand
                        18 => new Color("4e5c40"),              // swamp
                        1 or 6 => new Color("2e6b34"),          // tree, forest
                        3 or 4 or 24 or 20 or 21 => new Color("9a8d76"), // roads/floors
                        _ => new Color("58a14e"),               // grass & friends
                    };
                    DrawRect(new Rect2(origin + new Vector2(x * s, y * s), new Vector2(s * 2 + 0.5f, s * 2 + 0.5f)), c);
                }
            DrawEntities(u => origin + new Vector2(
                u.x * s / SimConstants.UnitsPerTile, u.y * s / SimConstants.UnitsPerTile));
        }

        private void DrawEntities(Func<(int x, int y), Vector2> toPx)
        {
            if (Client is null) return;
            foreach (var (id, e) in Client.Entities)
            {
                if (!e.Alive) continue;
                Color c;
                float r;
                if (id == Client.LocalEntityId) { c = new Color("f8e050"); r = 4f; }
                else if (e.Kind == EntityKind.Player) { c = new Color("70e878"); r = 3f; }
                else if (e.Kind == EntityKind.Monster) { c = new Color("e05050"); r = 2.5f; }
                else continue;
                DrawCircle(toPx((e.X, e.Y)), r, c);
            }
        }
    }
}
