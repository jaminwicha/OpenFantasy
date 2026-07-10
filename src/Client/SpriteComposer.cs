using Godot;
using OpenFantasy.Sim;

namespace OpenFantasy.Client;

/// <summary>
/// Runtime paper-doll compositor: stacks grayscale-ramp layer sheets
/// (assets/layers/) and tints each through the appearance manifest's 4-color
/// ramps. Results are cached per Appearance. Same math as tools/preview_sprites.py.
/// </summary>
public static class SpriteComposer
{
    private static readonly Dictionary<Appearance, ImageTexture> Cache = new();
    private static readonly Dictionary<string, Image> Layers = new();
    private static AppearanceData? _data;

    public static void Init(AppearanceData data) => _data = data;

    public static ImageTexture Get(Appearance app)
    {
        if (Cache.TryGetValue(app, out var cached)) return cached;
        var data = _data ?? throw new InvalidOperationException("SpriteComposer.Init not called");
        app = app.Sanitized(data);

        var canvas = Image.CreateEmpty(48, 96, false, Image.Format.Rgba8);
        BlendLayer(canvas, "body", data.Skins[app.Skin], data);
        BlendLayer(canvas, $"outfit_{app.Outfit:D2}_a", data.OutfitColors[app.OutfitA], data);
        BlendLayer(canvas, $"outfit_{app.Outfit:D2}_b", data.OutfitColors[app.OutfitB], data);
        BlendLayer(canvas, $"hair_{app.HairStyle:D2}", data.HairColors[app.HairColor], data);
        if (app.Acc > 0)
            BlendLayer(canvas, $"acc_{app.Acc:D2}", data.OutfitColors[app.OutfitB], data);

        var texture = ImageTexture.CreateFromImage(canvas);
        Cache[app] = texture;
        return texture;
    }

    private static void BlendLayer(Image canvas, string name, List<List<int>> ramp, AppearanceData data)
    {
        if (!Layers.TryGetValue(name, out var layer))
        {
            var tex = GD.Load<Texture2D>($"res://assets/layers/{name}.png");
            layer = tex.GetImage();
            if (layer.GetFormat() != Image.Format.Rgba8)
                layer.Convert(Image.Format.Rgba8);
            Layers[name] = layer;
        }

        for (int y = 0; y < 96; y++)
        {
            for (int x = 0; x < 48; x++)
            {
                var px = layer.GetPixel(x, y);
                if (px.A < 0.5f) continue;
                int gray = (int)(px.R * 255f);
                int best = 0;
                int bestDist = int.MaxValue;
                for (int i = 0; i < 4; i++)
                {
                    int d = Math.Abs(data.Gray[i] - gray);
                    if (d < bestDist) { bestDist = d; best = i; }
                }
                var c = ramp[best];
                canvas.SetPixel(x, y, Color.Color8((byte)c[0], (byte)c[1], (byte)c[2]));
            }
        }
    }
}
