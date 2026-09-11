using Godot;
using OpenFantasy.Core;

namespace OpenFantasy.Client.Presentation;

/// <summary>Outdoor ambient light sampled from the permitted world calendar, never a local timer.</summary>
public partial class FarmDaylight : CanvasModulate
{
    [Export] public Gradient Palette { get; set; } = null!;

    internal WorldInstant? PresentedAt { get; private set; }

    public override void _Ready()
    {
        ValidatePalette();
    }

    internal void Present(WorldInstant calendar)
    {
        Color = Palette.Sample(calendar.MillisecondsWithinDay / (float)WorldInstant.MillisecondsPerDay);
        PresentedAt = calendar;
    }

    private void ValidatePalette()
    {
        if (Palette is null || Palette.GetPointCount() < 2
            || Palette.GetOffset(0) != 0 || Palette.GetOffset(Palette.GetPointCount() - 1) != 1
            || Palette.GetColor(0) != Palette.GetColor(Palette.GetPointCount() - 1))
        {
            throw new InvalidDataException("Outdoor daylight requires a complete palette with matching midnight endpoints.");
        }
        foreach (Color color in Palette.Colors)
        {
            if (!float.IsFinite(color.R) || !float.IsFinite(color.G) || !float.IsFinite(color.B)
                || !float.IsFinite(color.A) || color.A != 1 || color.R < 0 || color.R > 1
                || color.G < 0 || color.G > 1 || color.B < 0 || color.B > 1)
            {
                throw new InvalidDataException("Outdoor daylight colors must be finite opaque tint values between zero and one.");
            }
        }
    }
}
