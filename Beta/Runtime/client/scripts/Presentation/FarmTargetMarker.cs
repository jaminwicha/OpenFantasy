using Godot;

namespace OpenFantasy.Client.Presentation;

/// <summary>A field selection cue, independent of the target's art or authoritative collision.</summary>
internal sealed partial class FarmTargetMarker : Node2D
{
    public override void _Draw()
    {
        Color outline = new(0.08f, 0.13f, 0.14f);
        Color accent = new(1f, 0.91f, 0.58f);
        Vector2[] corners = [new(-13, -5), new(13, -5), new(-13, 5), new(13, 5)];
        foreach (Vector2 corner in corners)
        {
            Vector2 inward = new(-Mathf.Sign(corner.X) * 5, 0);
            DrawLine(corner, corner + inward, outline, 3);
            DrawLine(corner, corner + inward, accent, 1);
        }
        DrawPolyline([new(-4, -37), new(0, -33), new(4, -37)], outline, 3);
        DrawPolyline([new(-4, -37), new(0, -33), new(4, -37)], accent, 1);
    }
}
