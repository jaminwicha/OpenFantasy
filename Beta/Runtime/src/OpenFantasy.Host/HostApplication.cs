using System.Runtime.InteropServices;
using System.Text.Json;
using OpenFantasy.Core;

namespace OpenFantasy.Host;

/// <summary>Routes host commands and writes their results to the configured output.</summary>
internal sealed class HostApplication
{
    private readonly TextWriter _output;

    public HostApplication(TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);
        _output = output;
    }

    public int Run(string[] arguments)
    {
        if (arguments.Length == 1 && arguments[0] == "--inspect-foundation")
        {
            _output.WriteLine(CreateFoundationReport());
            return 0;
        }

        WriteUsage();
        return arguments.Length == 0 ? 0 : 2;
    }

    private void WriteUsage()
    {
        _output.WriteLine("OpenFantasy host\nUsage: OpenFantasy.Host --inspect-foundation");
        _output.WriteLine("World hosting is not yet implemented.");
    }

    private static string CreateFoundationReport()
    {
        var seed = new byte[32];
        seed[^1] = 42;
        var identity = EntityId.DeriveGenerated(seed, "world-v1", "0/0/0", "resident", 0);
        var random = new CounterRandomV1(seed, "rules-v1", "foundation", identity);
        var midnight = new WorldInstant(-1, WorldInstant.MillisecondsPerDay - 1).AddMilliseconds(1);

        return JsonSerializer.Serialize(new
        {
            Contract = "foundation-v1",
            Runtime = RuntimeInformation.FrameworkDescription,
            Entity = identity.ToString(),
            Sample = random.NextUInt64(),
            Day = midnight.Days,
            Millisecond = midnight.MillisecondsWithinDay
        });
    }
}
