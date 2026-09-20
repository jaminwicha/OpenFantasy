using System.Diagnostics;
using System.Text.Json;
using OpenFantasy.Hosting.Worlds;

if (args.Length != 3) throw new ArgumentException("Usage: <split|closed> <database> <revision>");
string mode = args[0];
ulong revision = ulong.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
var results = new List<object>();
for (int attempt = 0; attempt < 3; attempt++)
{
    RecordedWorldState recorded;
    object? open = null;
    object read;
    if (mode == "split")
    {
        SqliteWorldAuthority? authority = null;
        open = Measure(() => authority = SqliteWorldAuthority.OpenExisting(args[1]));
        using (authority!)
        {
            RecordedWorldState? value = null;
            read = Measure(() => value = authority!.ReadRecordedStateForHost(revision));
            recorded = value!;
        }
    }
    else if (mode == "closed")
    {
        // Reflection lets this identical harness compile/run against the preserved baseline,
        // which predates the new closed-file reader. Lookup occurs outside the timed call.
        var type = typeof(SqliteWorldAuthority).Assembly.GetType("OpenFantasy.Hosting.Worlds.RecordedWorldReader", true)!;
        var method = type.GetMethod("ReadForHost", [typeof(string), typeof(ulong)])!;
        RecordedWorldState? value = null;
        read = Measure(() => value = (RecordedWorldState)method.Invoke(null, [args[1], revision])!);
        recorded = value!;
    }
    else throw new ArgumentException("Unknown timing mode.");
    results.Add(new { attempt, open, read, recorded.SourceHeadRevision, SourceHeadHash = recorded.SourceHeadHash.ToString(),
        Revision = recorded.State.Revision, StateHash = recorded.StateHash.ToString() });
}
Console.WriteLine(JsonSerializer.Serialize(new { mode, configuration = "Debug", results }, new JsonSerializerOptions { WriteIndented = true }));

static object Measure(Action action)
{
    using var process = Process.GetCurrentProcess();
    TimeSpan cpu = process.TotalProcessorTime;
    long allocations = GC.GetTotalAllocatedBytes(true);
    int[] counts = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
    var watch = Stopwatch.StartNew();
    action();
    watch.Stop();
    process.Refresh();
    return new { WallMilliseconds = watch.Elapsed.TotalMilliseconds,
        CpuMilliseconds = (process.TotalProcessorTime - cpu).TotalMilliseconds,
        AllocatedBytes = GC.GetTotalAllocatedBytes(true) - allocations,
        Gen0 = GC.CollectionCount(0) - counts[0], Gen1 = GC.CollectionCount(1) - counts[1], Gen2 = GC.CollectionCount(2) - counts[2] };
}
