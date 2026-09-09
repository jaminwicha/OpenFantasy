using System.Text.Json;
using System.Runtime.InteropServices;
using OpenFantasy.Core;

if (args.Length != 1 || args[0] != "--inspect-foundation")
{
    Console.WriteLine("OpenFantasy host\nUsage: OpenFantasy.Host --inspect-foundation");
    Console.WriteLine("World hosting is not yet implemented.");
    return args.Length == 0 ? 0 : 2;
}

var seed = new byte[32];
seed[^1] = 42;
var id = EntityId.DeriveGenerated(seed, "world-v1", "0/0/0", "resident", 0);
var random = new CounterRandomV1(seed, "rules-v1", "foundation", id);
var instant = new WorldInstant(-1, 86_399_999).AddMilliseconds(1);
Console.WriteLine(JsonSerializer.Serialize(new
{
    Contract = "foundation-v1",
    Runtime = RuntimeInformation.FrameworkDescription,
    Entity = id.ToString(),
    Sample = random.NextUInt64(),
    Day = instant.Days,
    Millisecond = instant.MillisecondsWithinDay
}));
return 0;
