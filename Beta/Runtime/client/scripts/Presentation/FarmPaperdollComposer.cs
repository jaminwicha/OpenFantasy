using System.Text.Json;
using Godot;

namespace OpenFantasy.Client.Presentation;

/// <summary>Reuses the original grayscale-ramp composition and exact Wren layer order.</summary>
internal sealed class FarmPaperdollComposer
{
    private const int SheetWidth = 48;
    private const int SheetHeight = 96;
    private readonly int[] _gray;
    private readonly JsonElement _palettes;

    internal FarmPaperdollComposer(string paletteJson)
    {
        if (paletteJson.Length > 4096)
        {
            throw new InvalidDataException("The retained farm palette manifest exceeds its profile.");
        }
        using JsonDocument document = JsonDocument.Parse(paletteJson, new JsonDocumentOptions { MaxDepth = 8 });
        _palettes = document.RootElement.Clone();
        _gray = _palettes.GetProperty("gray").EnumerateArray().Select(value => value.GetInt32()).ToArray();
        if (!_gray.SequenceEqual(new[] { 40, 110, 175, 235 }))
        {
            throw new InvalidDataException("The farm compositor requires the exact retained source grayscale ramp.");
        }
    }

    internal ImageTexture ComposeWren(Resource assets)
    {
        using Image canvas = Image.CreateEmpty(SheetWidth, SheetHeight, false, Image.Format.Rgba8);
        Blend(canvas, FarmVisualAssets.Texture(assets, "body"), Ramp("skins", 5));
        Blend(canvas, FarmVisualAssets.Texture(assets, "outfit_a"), Ramp("outfitColors", 10));
        Blend(canvas, FarmVisualAssets.Texture(assets, "outfit_b"), Ramp("outfitColors", 10));
        Blend(canvas, FarmVisualAssets.Texture(assets, "hair"), Ramp("hairColors", 13));
        // Original race index zero adds no race layer; the accessory uses outfit B's palette.
        Blend(canvas, FarmVisualAssets.Texture(assets, "accessory"), Ramp("outfitColors", 10));
        return ImageTexture.CreateFromImage(canvas);
    }

    private Color[] Ramp(string family, int index)
    {
        JsonElement values = _palettes.GetProperty(family)[index];
        if (values.GetArrayLength() != 4)
        {
            throw new InvalidDataException("A retained paperdoll palette requires four colors.");
        }
        return values.EnumerateArray().Select(rgb => Color.Color8(rgb[0].GetByte(), rgb[1].GetByte(), rgb[2].GetByte())).ToArray();
    }

    private void Blend(Image canvas, Texture2D texture, Color[] palette)
    {
        using Image layer = texture.GetImage();
        if (layer.GetWidth() != SheetWidth || layer.GetHeight() != SheetHeight)
        {
            throw new InvalidDataException("A retained paperdoll layer has an incompatible sheet size.");
        }
        if (layer.IsCompressed() && layer.Decompress() != Error.Ok)
        {
            throw new InvalidDataException("A retained paperdoll layer could not be read losslessly.");
        }
        if (layer.GetFormat() != Image.Format.Rgba8)
        {
            layer.Convert(Image.Format.Rgba8);
        }
        for (int y = 0; y < SheetHeight; y++)
        {
            for (int x = 0; x < SheetWidth; x++)
            {
                Color pixel = layer.GetPixel(x, y);
                if (pixel.A < 0.5f)
                {
                    continue;
                }
                int gray = (int)(pixel.R * 255f);
                int closest = 0;
                int bestDistance = int.MaxValue;
                for (int index = 0; index < _gray.Length; index++)
                {
                    int distance = Math.Abs(_gray[index] - gray);
                    if (distance < bestDistance)
                    {
                        closest = index;
                        bestDistance = distance;
                    }
                }
                canvas.SetPixel(x, y, palette[closest]);
            }
        }
    }
}
