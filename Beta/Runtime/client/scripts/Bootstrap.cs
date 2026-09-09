using Godot;
using OpenFantasy.Core;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace OpenFantasy.Client;

public partial class Bootstrap : Control
{
    public override void _Ready()
    {
        var seed = new byte[32];
        seed[^1] = 42;
        var id = EntityId.DeriveGenerated(seed, "world-v1", "0/0/0", "resident", 0);
        var random = new CounterRandomV1(seed, "rules-v1", "foundation", id);
        var instant = new WorldInstant(-1, 86_399_999).AddMilliseconds(1);
        GD.Print(JsonSerializer.Serialize(new
        {
            Contract = "foundation-v1",
            Runtime = RuntimeInformation.FrameworkDescription,
            Entity = id.ToString(),
            Sample = random.NextUInt64(),
            Day = instant.Days,
            Millisecond = instant.MillisecondsWithinDay
        }));

        if (OS.GetCmdlineUserArgs().Contains("--smoke-test"))
        {
            GetTree().Quit();
            return;
        }

        var backdrop = new ColorRect { Color = new Color("17212b") };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var label = new Label
        {
            Text = "OpenFantasy\n\nDevelopment foundation\nPortable core loaded successfully.\n\nThis is a build harness; gameplay is not yet implemented.",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        center.AddChild(label);
    }
}
