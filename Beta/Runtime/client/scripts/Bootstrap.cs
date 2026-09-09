using System.Runtime.InteropServices;
using System.Text.Json;
using Godot;
using OpenFantasy.Core;

namespace OpenFantasy.Client;

public partial class Bootstrap : Control
{
    public override void _Ready()
    {
        WriteFoundationDiagnostic();

        if (OS.GetCmdlineUserArgs().Contains("--smoke-test"))
        {
            GetTree().Quit();
        }
    }

    private static void WriteFoundationDiagnostic()
    {
        var seed = new byte[32];
        seed[^1] = 42;
        var identity = EntityId.DeriveGenerated(seed, "world-v1", "0/0/0", "resident", 0);
        var random = new CounterRandomV1(seed, "rules-v1", "foundation", identity);
        var midnight = new WorldInstant(-1, WorldInstant.MillisecondsPerDay - 1).AddMilliseconds(1);

        GD.Print(JsonSerializer.Serialize(new
        {
            Contract = "foundation-v1",
            Runtime = RuntimeInformation.FrameworkDescription,
            Entity = identity.ToString(),
            Sample = random.NextUInt64(),
            Day = midnight.Days,
            Millisecond = midnight.MillisecondsWithinDay
        }));
    }
}
