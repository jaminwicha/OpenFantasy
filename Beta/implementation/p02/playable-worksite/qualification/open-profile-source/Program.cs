using System.Diagnostics;
using System.Text.Json;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;
if (args.Length > 1)
{
    using var connection=new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder{DataSource=args[0],Mode=Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly}.ToString());
    connection.Open();
    using var query=connection.CreateCommand();
    query.CommandText="SELECT state FROM world_genesis";
    var state=WorldSnapshotCodec.Decode((byte[])query.ExecuteScalar()!);
    var genesisIds=state.IdentityIndex;
    var occupied=new HashSet<OpenFantasy.Core.EntityId>();
    var processor=new WorldStepProcessor();
    query.CommandText="SELECT accepted_batch,after_hash FROM world_commits ORDER BY sequence";
    using var rows=query.ExecuteReader();
    var totals=new Dictionary<string,(long ticks,long allocated)>();
    T Measure<T>(string stage,Func<T> work) {long b=GC.GetAllocatedBytesForCurrentThread(),t=Stopwatch.GetTimestamp(); T value=work(); var old=totals.GetValueOrDefault(stage); totals[stage]=(old.ticks+Stopwatch.GetTimestamp()-t,old.allocated+GC.GetAllocatedBytesForCurrentThread()-b);return value;}
    while(rows.Read())
    {
        var batch=Measure("batch-decode",()=>WorldStepCodec.Decode((byte[])rows[0]));
        var decision=Measure("deterministic-decide",()=>processor.Decide(state,batch,id=>genesisIds.Contains(id)||occupied.Contains(id)));
        var encoded=Measure("candidate-encode",()=>WorldSnapshotCodec.Encode(decision.Candidate));
        var hash=Measure("candidate-hash",()=>System.Security.Cryptography.SHA256.HashData(encoded));
        if(!hash.AsSpan().SequenceEqual((byte[])rows[1]))throw new Exception("Replay bytes differ at "+decision.Candidate.Revision);
        foreach(var id in decision.Identities)occupied.Add(id.Identity);
        state=decision.Candidate;
    }
    Console.WriteLine(JsonSerializer.Serialize(new {state.Revision,exactCandidateHashes=true,phases=totals.Select(x=>new{stage=x.Key,milliseconds=x.Value.ticks*1000.0/Stopwatch.Frequency,allocatedBytes=x.Value.allocated})},new JsonSerializerOptions{WriteIndented=true}));
    return;
}
var records = new List<object>();
var clock = Stopwatch.StartNew();
long bytes = GC.GetTotalAllocatedBytes(true);
void Mark(string name) { records.Add(new { stage=name, milliseconds=clock.Elapsed.TotalMilliseconds, allocatedBytes=GC.GetTotalAllocatedBytes(true)-bytes }); Console.Error.WriteLine(name + " " + clock.Elapsed.TotalMilliseconds); clock.Restart(); bytes=GC.GetTotalAllocatedBytes(true); }
var genesis = WorksiteFarmAdmission.CreateEmbedded("aldermere-worksite-v1", "Traveler");
Mark("fresh-admission");
var authority = SqliteWorldAuthority.OpenExisting(args[0]);
Mark("complete-save-recovery");
var current = authority.InspectForHost().State;
var actor=current.Residents.Single(x=>x.ControllerKind==ResidentControllerKind.Player);
var control=current.Inventory.Controls.Single(x=>x.ActorId==actor.ActorId);
Mark("inspection");
await using var session=new LocalWorldSession(authority, actor.ActorId, new TrustedCommandDelivery(control.PrincipalId,control.SessionId));
var view=session.ReadView();
Mark("attach-and-view");
Console.WriteLine(JsonSerializer.Serialize(new { pid=Environment.ProcessId, runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, current.Revision, current.AdvancingSteps, records }, new JsonSerializerOptions{WriteIndented=true}));
