using System.Text.Json;

namespace OpenFantasy.Sim;

/// <summary>
/// A character's visual identity: indices into the appearance manifest's
/// part and palette tables. 7 bytes on the wire; validated server-side.
/// </summary>
public struct Appearance : IEquatable<Appearance>
{
    public byte Skin;
    public byte HairStyle;
    public byte HairColor;
    public byte Outfit;
    public byte OutfitA;
    public byte OutfitB;
    public byte Acc;

    public static readonly Appearance Default = new()
    {
        Skin = 1, HairStyle = 0, HairColor = 2, Outfit = 0, OutfitA = 0, OutfitB = 7, Acc = 0,
    };

    public readonly byte[] ToBytes() => new[] { Skin, HairStyle, HairColor, Outfit, OutfitA, OutfitB, Acc };

    public static Appearance FromBytes(byte[] b) => b.Length < 7
        ? Default
        : new Appearance { Skin = b[0], HairStyle = b[1], HairColor = b[2], Outfit = b[3], OutfitA = b[4], OutfitB = b[5], Acc = b[6] };

    public readonly void Write(BinaryWriter w)
    {
        w.Write(Skin); w.Write(HairStyle); w.Write(HairColor);
        w.Write(Outfit); w.Write(OutfitA); w.Write(OutfitB); w.Write(Acc);
    }

    public static Appearance Read(BinaryReader r) => new()
    {
        Skin = r.ReadByte(), HairStyle = r.ReadByte(), HairColor = r.ReadByte(),
        Outfit = r.ReadByte(), OutfitA = r.ReadByte(), OutfitB = r.ReadByte(), Acc = r.ReadByte(),
    };

    /// <summary>Clamps every field into the manifest's valid ranges.</summary>
    public readonly Appearance Sanitized(AppearanceData data) => new()
    {
        Skin = (byte)Math.Min(Skin, data.Skins.Count - 1),
        HairStyle = (byte)Math.Min(HairStyle, data.HairStyles - 1),
        HairColor = (byte)Math.Min(HairColor, data.HairColors.Count - 1),
        Outfit = (byte)Math.Min(Outfit, data.Outfits - 1),
        OutfitA = (byte)Math.Min(OutfitA, data.OutfitColors.Count - 1),
        OutfitB = (byte)Math.Min(OutfitB, data.OutfitColors.Count - 1),
        Acc = (byte)Math.Min(Acc, data.Accessories - 1),
    };

    public readonly bool Equals(Appearance o)
        => Skin == o.Skin && HairStyle == o.HairStyle && HairColor == o.HairColor
           && Outfit == o.Outfit && OutfitA == o.OutfitA && OutfitB == o.OutfitB && Acc == o.Acc;

    public override readonly bool Equals(object? obj) => obj is Appearance a && Equals(a);

    public override readonly int GetHashCode()
        => HashCode.Combine(Skin, HairStyle, HairColor, Outfit, OutfitA, OutfitB, Acc);
}

public sealed class AppearancePreset
{
    public string Name { get; set; } = "";
    public int Skin { get; set; }
    public int HairStyle { get; set; }
    public int HairColor { get; set; }
    public int Outfit { get; set; }
    public int OutfitA { get; set; }
    public int OutfitB { get; set; }
    public int Acc { get; set; }

    public Appearance ToAppearance() => new()
    {
        Skin = (byte)Skin, HairStyle = (byte)HairStyle, HairColor = (byte)HairColor,
        Outfit = (byte)Outfit, OutfitA = (byte)OutfitA, OutfitB = (byte)OutfitB, Acc = (byte)Acc,
    };
}

/// <summary>The appearance manifest (data/appearance.json): palette ramps,
/// part counts, and the preset gallery. Written by tools/gen_layers.py.</summary>
public sealed class AppearanceData
{
    public List<int> Gray { get; set; } = new();
    public List<List<List<int>>> Skins { get; set; } = new();
    public int HairStyles { get; set; }
    public List<List<List<int>>> HairColors { get; set; } = new();
    public int Outfits { get; set; }
    public List<List<List<int>>> OutfitColors { get; set; } = new();
    public int Accessories { get; set; }
    public List<AppearancePreset> Presets { get; set; } = new();

    public static AppearanceData LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new DataValidationException($"missing content file: {path}");
        AppearanceData data;
        try
        {
            data = JsonSerializer.Deserialize<AppearanceData>(File.ReadAllText(path), GameData.JsonOptions)
                   ?? throw new DataValidationException($"{path}: file parsed to null");
        }
        catch (JsonException e)
        {
            throw new DataValidationException($"{path}: invalid JSON — {e.Message}");
        }

        if (data.Gray.Count != 4)
            throw new DataValidationException($"{path}: gray ramp must have 4 entries");
        if (data.Skins.Count == 0 || data.HairColors.Count == 0 || data.OutfitColors.Count == 0)
            throw new DataValidationException($"{path}: empty palette table");
        if (data.HairStyles <= 0 || data.Outfits <= 0 || data.Accessories <= 0)
            throw new DataValidationException($"{path}: part counts must be positive");
        foreach (var table in new[] { data.Skins, data.HairColors, data.OutfitColors })
            foreach (var ramp in table)
                if (ramp.Count != 4 || ramp.Any(c => c.Count != 3))
                    throw new DataValidationException($"{path}: every ramp needs 4 RGB entries");
        return data;
    }
}
