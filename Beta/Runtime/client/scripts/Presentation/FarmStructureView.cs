using Godot;
using OpenFantasy.Core.Construction;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client.Presentation;

/// <summary>Renders a committed small assembly; this node has no authority, collision or material inventory.</summary>
internal sealed partial class FarmStructureView : Node2D
{
    // Exact retained construction-farm shape in the definition's canonical member order.
    // The original board art is not a generic structure icon.
    private static readonly MillimetreBox[] GuardShape =
    [
        new(0, 0, 0, 75, 5, 10), new(0, 0, 10, 400, 5, 246),
        new(75, -60, 0, 85, 65, 10), new(75, -60, 10, 85, 0, 20), new(75, 5, 10, 85, 65, 20),
        new(85, 0, 0, 315, 5, 10), new(315, -60, 0, 325, 65, 10), new(315, -60, 10, 325, 0, 20),
        new(315, 5, 10, 325, 65, 20), new(325, 0, 0, 400, 5, 10)
    ];
    private Vector2[][] _polygons = [];
    private Rect2? _geometryBounds;

    internal Rect2? SpriteViewportBounds => IsVisibleInTree() && GetNode<Sprite2D>("Guard") is { Visible: true } sprite
        ? sprite.GetGlobalTransformWithCanvas() * sprite.GetRect() : null;

    internal Rect2? ViewportBounds => SpriteViewportBounds ?? (IsVisibleInTree() && _geometryBounds is { } bounds
        ? GetGlobalTransformWithCanvas() * bounds : null);

    internal void Present(FarmStructureFrame frame) => Present(frame, FarmCanvasCoordinates.Position(frame.Feet));

    internal void Present(FarmStructureFrame frame, Vector2 position)
    {
        Position = position;
        GetNode<Sprite2D>("Guard").Show();
        _polygons = [];
        _geometryBounds = null;
        QueueRedraw();
    }

    internal void Present(LocalPublicStructureView structure, Vector2 position, bool oblique)
    {
        Position = position;
        bool guard = structure.Members.Count == GuardShape.Length && structure.Members.Select((member, index) =>
            member.MemberIndex == index && member.Origin == structure.Origin && member.Geometry == GuardShape[index]).All(value => value);
        GetNode<Sprite2D>("Guard").Visible = guard;
        _geometryBounds = null;
        if (guard) _polygons = [];
        else
        {
            var polygons = new List<Vector2[]>();
            foreach (var member in structure.Members)
            {
                var box = member.Geometry;
                double x = member.Origin.X - structure.Origin.X;
                double y = member.Origin.Y - structure.Origin.Y;
                double z = member.Origin.Z - structure.Origin.Z;
                Vector2 Point(long dx, long dy, long dz) => new(
                    (float)((x + dx * 0.256) / FarmCanvasCoordinates.SubunitsPerPixel),
                    (float)((y + dy * 0.256 - (oblique ? z + dz * 0.256 : 0)) / FarmCanvasCoordinates.SubunitsPerPixel));
                polygons.Add([Point(box.MinX, box.MinY, box.MaxZ), Point(box.MaxX, box.MinY, box.MaxZ),
                    Point(box.MaxX, box.MaxY, box.MaxZ), Point(box.MinX, box.MaxY, box.MaxZ)]);
                if (oblique) polygons.Add([Point(box.MinX, box.MaxY, box.MaxZ), Point(box.MaxX, box.MaxY, box.MaxZ),
                    Point(box.MaxX, box.MaxY, box.MinZ), Point(box.MinX, box.MaxY, box.MinZ)]);
            }
            _polygons = polygons.ToArray();
            Vector2[] points = _polygons.SelectMany(polygon => polygon).ToArray();
            if (points.Length > 0)
            {
                var minimum = new Vector2(points.Min(point => point.X), points.Min(point => point.Y));
                var maximum = new Vector2(points.Max(point => point.X), points.Max(point => point.Y));
                _geometryBounds = new Rect2(minimum, maximum - minimum).Grow(1);
            }
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var polygon in _polygons)
        {
            DrawColoredPolygon(polygon, new Color("8b9189"));
            DrawPolyline([.. polygon, polygon[0]], new Color("353c38"), 1);
        }
    }
}
