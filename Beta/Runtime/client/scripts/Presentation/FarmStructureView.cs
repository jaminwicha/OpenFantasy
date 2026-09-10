using Godot;

namespace OpenFantasy.Client.Presentation;

/// <summary>Renders a committed small assembly; this node has no authority, collision or material inventory.</summary>
internal sealed partial class FarmStructureView : Node2D
{
    internal void Present(FarmStructureFrame frame)
    {
        Position = FarmCanvasCoordinates.Position(frame.Feet);
    }
}
