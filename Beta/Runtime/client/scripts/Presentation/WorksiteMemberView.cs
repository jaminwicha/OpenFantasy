using System.Globalization;
using Godot;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Structures;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client.Presentation;

/// <summary>Functional blockout of exact committed material, not its outward collision envelope.</summary>
internal sealed partial class WorksiteMemberView : Node2D
{
    private Vector2[] _top = [];
    private Vector2[] _front = [];
    private Color _topColor;
    private Color _frontColor;
    private double _height;
    private bool _cutaway;
    private bool _resting;
    private string? _level;

    internal void Setup(LocalWorksiteMemberView member)
    {
        MillimetreBox box = member.Geometry;
        double x0 = member.Origin.X + box.MinX * 0.256;
        double y0 = member.Origin.Y + box.MinY * 0.256;
        double z0 = member.Origin.Z + box.MinZ * 0.256;
        double x1 = member.Origin.X + box.MaxX * 0.256;
        double y1 = member.Origin.Y + box.MaxY * 0.256;
        _height = member.Origin.Z + box.MaxZ * 0.256;
        // Material corners are projected before pixel rasterization. The conservative Bounds
        // is deliberately not substituted for these exact authored millimetre dimensions.
        Position = WorksiteCoordinates.Point(x0, y1, _height);
        _top = [Point(x0, y0, _height), Point(x1, y0, _height), Point(x1, y1, _height), Point(x0, y1, _height)];
        _front = [Point(x0, y1, _height), Point(x1, y1, _height), Point(x1, y1, z0), Point(x0, y1, z0)];
        ZIndex = WorksiteCoordinates.DepthIndex(Math.Ceiling(_height));
        float condition = member.ConditionParts / (float)StructuralElement.MaximumConditionParts;
        _topColor = new Color("807366").Lerp(new Color("bca678"), condition);
        _frontColor = new Color("5e5042").Lerp(new Color("7e694c"), condition);
        _resting = member.Disposition == ConstructionMemberDisposition.Resting;
        _level = null;
        if (box.MaxX - box.MinX >= 1_000 && box.MaxY - box.MinY >= 1_000)
        {
            _level = ((_height - 256) / 256).ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture) + " m";
        }
        QueueRedraw();

        Vector2 Point(double x, double y, double z) => WorksiteCoordinates.Point(x, y, z) - Position;
    }

    internal void PresentLevel(long feetHeight)
    {
        bool next = _height > feetHeight + 1;
        if (_cutaway == next)
        {
            return;
        }
        _cutaway = next;
        QueueRedraw();
    }

    public override void _Draw()
    {
        // Higher material remains visible as a transparent cutaway. It never becomes a
        // hidden collider or a fabricated floor; the authoritative body still determines access.
        float alpha = _cutaway ? 0.24f : 1;
        DrawColoredPolygon(_front, _frontColor with { A = alpha });
        DrawColoredPolygon(_top, _topColor with { A = alpha });
        Color outline = _resting ? new Color("387e80") : new Color("363d38");
        outline.A = _cutaway ? 0.55f : 1;
        DrawPolyline([.. _front, _front[0]], outline, 1);
        DrawPolyline([.. _top, _top[0]], outline, 1);
        if (_level is not null && !_cutaway)
        {
            Vector2 label = (_top[0] + _top[2]) / 2 + new Vector2(-15, 4);
            DrawString(ThemeDB.FallbackFont, label, _level, fontSize: 10, modulate: new Color("352f29"));
        }
    }
}
