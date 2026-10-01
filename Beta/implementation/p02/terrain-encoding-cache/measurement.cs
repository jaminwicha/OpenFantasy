using System.Diagnostics;
using System.Text.Json;
using OpenFantasy.Hosting.Worlds;
var timer = Stopwatch.StartNew();
using (var authority = SqliteWorldAuthority.OpenExisting(args[0], File.ReadAllBytes(args[1])))
{ Console.WriteLine(JsonSerializer.Serialize(new { OpenSeconds = timer.Elapsed.TotalSeconds })); }
Console.WriteLine(JsonSerializer.Serialize(new { IncludingDisposeSeconds = timer.Elapsed.TotalSeconds }));
