using System.Diagnostics;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Host;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests;

public sealed partial class NativeRosterJourneyTests
{
    public static bool Configured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENFANTASY_GODOT"));

    [Fact(SkipUnless = nameof(Configured), Skip = "Requires qualified Godot and an explicit native roster evidence directory.")]
    public async Task Four_native_players_assemble_choose_wait_and_reconnect_the_missing_required_human()
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ServerWorld.Cancellation);
        bound.CancelAfter(TimeSpan.FromSeconds(240));
        var elapsed = Stopwatch.StartNew();
        using var files = new ServerFiles();
        string output = Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_PLAYER_EVIDENCE")
            ?? throw new InvalidOperationException("OPENFANTASY_NATIVE_PLAYER_EVIDENCE must name a retained evidence directory.");
        output = Path.Combine(output, "roster");
        Directory.CreateDirectory(output);
        string database = Path.Combine(output, "native-roster.sqlite3");
        WorldSnapshot genesis = CargoOutcomeWorldAdmission.CreateEmbedded(CargoOutcomeWorldAdmission.SeedText,
            new[] { "Amber", "Birch", "Cedar", "Dawn" });
        using (var authority = SqliteWorldAuthority.CreateNew(database, genesis)) { }
        File.WriteAllBytes(Path.Combine(output, "genesis.ofc"), WorldSnapshotCodec.Encode(genesis));
        var scenario = genesis.EncounterLifecycle!.Scenarios.Single(value => value.Key
            == EncounterJourneyAdmission.FirstScenarioKey + "_player_2");
        EntityId[] humans = genesis.Residents.Where(value => value.ControllerKind == ResidentControllerKind.Player)
            .Select(value => value.ActorId).Order().ToArray();
        using var certificate = ServerWorld.CreateCertificate();
        string certificatePath = Path.Combine(output, "public.cer");
        File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Cert));
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../client"));
        string background = NativeRosterProcess.BackgroundProject(source, files.DirectoryPath);
        var allProcesses = new List<NativeRosterProcess>();
        var active = new Dictionary<EntityId, NativeRosterProcess>();
        var invitations = new Dictionary<EntityId, string>();
        ulong heldUnits = 0;
        WorldInstant heldCalendar = default;
        try
        {
            await using (var host = await CargoPlayerHost.OpenExistingAsync(database, bound.Token))
            await using (var server = await PlayerWebSocketServer.StartAsync(host.Admission,
                new(IPAddress.Loopback, 0, certificate), bound.Token))
            {
                foreach (EntityId human in humans)
                {
                    var invitation = new PlayerConnectionInvitation(server.Endpoint, host.Admission.Compatibility, human,
                        host.Issue(human, TimeSpan.FromMinutes(6)).Export());
                    string path = Path.Combine(files.DirectoryPath, human + ".invitation.json");
                    PlayerInvitationFile.Create(path, invitation);
                    invitations.Add(human, path);
                    var process = new NativeRosterProcess(Path.Combine(output, human + "-first"), path, certificatePath,
                        human == scenario.InitiatingActorId ? background : source, human == scenario.InitiatingActorId, bound.Token);
                    allProcesses.Add(process);
                    active.Add(human, process);
                }
                await Task.WhenAll(active.Values.Select(process => process.Opened()));
                foreach (var pair in active)
                {
                    Assert.Equal(pair.Key, pair.Value.View.PlayerActorId);
                    Assert.Equal(genesis.WorldId, pair.Value.View.WorldId);
                    Assert.Equal(genesis.Calendar, pair.Value.View.Calendar);
                    Assert.True(pair.Value.Last.GetProperty("LocallyPaused").GetBoolean());
                    Assert.Equal("Paused", pair.Value.Last.GetProperty("RunState").GetString());
                }
                await Task.Delay(350, bound.Token);
                await ObserveAll(active);
                Assert.All(active.Values, process => Assert.Equal(genesis.Calendar, process.View.Calendar));
                await Assemble(genesis, scenario, active, bound.Token);
                NativeRosterProcess initiator = active[scenario.InitiatingActorId];
                await initiator.Send("enter", target: scenario.TargetActorId);
                await ObservePhase(active, EncounterLifecyclePhase.Active, bound.Token);
                Assert.All(active.Values, process => Assert.Equal(EncounterLifecyclePhase.Active, process.View.EncounterLifecycle!.Phase));
                EntityId withheld = humans.Last(value => value != scenario.InitiatingActorId);
                foreach (EntityId human in humans.Where(value => value != withheld))
                    await SendChoice(active, active[human], "wait", bound.Token);
                await ObserveAll(active);
                heldUnits = initiator.View.Encounter!.ElapsedTacticalUnits;
                heldCalendar = initiator.View.Calendar;
                Assert.False(active[withheld].View.Encounter!.PlayerDecisionQueued);
                await Hold(active, heldUnits, heldCalendar, bound.Token);
                await initiator.Send("capture");
                await active[withheld].Send("close");
                active.Remove(withheld);
                await Hold(active, heldUnits, heldCalendar, bound.Token);
                var rejoined = new NativeRosterProcess(Path.Combine(output, withheld + "-rejoin"), invitations[withheld],
                    certificatePath, source, false, bound.Token);
                allProcesses.Add(rejoined);
                active.Add(withheld, rejoined);
                await rejoined.Opened();
                Assert.Equal(withheld, rejoined.View.PlayerActorId);
                Assert.True(rejoined.Last.GetProperty("LocallyPaused").GetBoolean());
                Assert.Equal(heldUnits, rejoined.View.Encounter!.ElapsedTacticalUnits);
                Assert.False(rejoined.View.Encounter.PlayerDecisionQueued);
                await SendChoice(active, rejoined, "wait", bound.Token);
                var progress = Stopwatch.StartNew();
                do
                {
                    await ObserveAll(active);
                    Assert.True(progress.Elapsed.TotalSeconds < 10, "The reconnected human's deliberate Wait did not resume resolution.");
                    bound.Token.ThrowIfCancellationRequested();
                } while (active.Values.All(process => process.View.Encounter!.ElapsedTacticalUnits == heldUnits));
                Assert.Contains(active.Values, process => process.View.Encounter!.ElapsedTacticalUnits > heldUnits);
                await initiator.Send("capture");
                await FinishEncounter(active, bound.Token);
                await initiator.Send("capture");
                foreach (var process in active.Values) await process.Send("pause");
                foreach (var process in active.Values) await process.Send("close");
                Assert.False(host.Session.Completion.IsCompleted);
            }
            VerifyHistory(database, genesis, allProcesses, heldUnits);
            bound.Token.ThrowIfCancellationRequested();
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(240), "The complete native roster proof exceeded 240 seconds.");
            File.WriteAllText(Path.Combine(output, "journey.json"), JsonSerializer.Serialize(new
            {
                Status = "passed", World = genesis.WorldId.ToString(), Players = humans.Select(value => value.ToString()),
                HeldUnits = heldUnits, HeldCalendar = heldCalendar, ElapsedMilliseconds = elapsed.ElapsedMilliseconds,
                Planning = "Test coordinator uses admitted source geometry and observed positions; no player route UI is claimed."
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "journey.failed.json"), JsonSerializer.Serialize(new
            { Status = "failed", Error = error.ToString(), ElapsedMilliseconds = elapsed.ElapsedMilliseconds }));
            throw;
        }
        finally
        {
            foreach (NativeRosterProcess process in allProcesses) await process.DisposeAsync();
        }
    }

    private static Task ObserveAll(Dictionary<EntityId, NativeRosterProcess> processes) =>
        Task.WhenAll(processes.Values.Select(process => process.Send("observe")));

    private static async Task ObservePhase(Dictionary<EntityId, NativeRosterProcess> processes,
        EncounterLifecyclePhase phase, CancellationToken token)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            await ObserveAll(processes);
            token.ThrowIfCancellationRequested();
            Assert.True(elapsed.Elapsed.TotalSeconds < 5, $"Every native stream must observe encounter phase {phase}.");
            if (processes.Values.All(process => process.View.EncounterLifecycle?.Phase == phase)) return;
            await Task.Delay(20, token);
        }
    }

    private static async Task Hold(Dictionary<EntityId, NativeRosterProcess> processes, ulong elapsed,
        WorldInstant calendar, CancellationToken token)
    {
        await Task.Delay(600, token);
        await ObserveAll(processes);
        Assert.All(processes.Values, process =>
        {
            Assert.Equal(elapsed, process.View.Encounter!.ElapsedTacticalUnits);
            Assert.Equal(calendar, process.View.Calendar);
            Assert.True(process.View.Encounter.HasRequiredDecisions);
        });
    }
}
