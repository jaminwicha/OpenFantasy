using OpenFantasy.Hosting.Content.NativeCargo;

namespace OpenFantasy.Client.Cargo;

/// <summary>One retained layer sheet by its source file stem and the palette ramp (family, index) that tints it.</summary>
internal sealed record CargoPaperdollStep(string LayerStem, string PaletteFamily, int PaletteIndex);

/// <summary>
/// The predecessor paper-doll order for one source appearance, unchanged from the original compositor: body in the
/// skin ramp, outfit sheets A and B in their own outfit ramps, hair in the hair ramp, then a race layer when the race
/// is not human (a dwarf's beard takes the hair ramp, elf or gnome ears take the skin ramp) and an accessory in
/// outfit B's ramp when one is worn. It invents no layer, ramp or fallback; an unsupported field is refused.
/// </summary>
internal static class CargoPaperdollPlan
{
    private const int Dwarf = 2;

    internal static IReadOnlyList<CargoPaperdollStep> Steps(NativeCargoAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        if (appearance.Race < 0 || appearance.Outfit < 0 || appearance.HairStyle < 0 || appearance.Accessory < 0)
        {
            throw new ArgumentException("A source appearance names a negative layer.", nameof(appearance));
        }
        List<CargoPaperdollStep> steps =
        [
            new("body", "skins", appearance.Skin),
            new($"outfit_{appearance.Outfit:D2}_a", "outfitColors", appearance.OutfitA),
            new($"outfit_{appearance.Outfit:D2}_b", "outfitColors", appearance.OutfitB),
            new($"hair_{appearance.HairStyle:D2}", "hairColors", appearance.HairColor)
        ];
        if (appearance.Race > 0)
        {
            steps.Add(appearance.Race == Dwarf
                ? new($"race_{appearance.Race:D2}", "hairColors", appearance.HairColor)
                : new($"race_{appearance.Race:D2}", "skins", appearance.Skin));
        }
        if (appearance.Accessory > 0)
        {
            steps.Add(new($"acc_{appearance.Accessory:D2}", "outfitColors", appearance.OutfitB));
        }
        return steps.AsReadOnly();
    }
}
