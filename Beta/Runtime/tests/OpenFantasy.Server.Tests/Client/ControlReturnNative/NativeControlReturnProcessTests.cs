using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Host;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests;

public sealed class NativeControlReturnProcessTests
{
    public static bool Configured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENFANTASY_GODOT"));

    [Fact(SkipUnless = nameof(Configured), Skip = "Requires explicit Godot and retained native control-return fixtures.")]
    public async Task Rendered_returning_player_observes_pending_work_and_explicitly_resumes_after_real_retreat()
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ServerWorld.Cancellation);
        limit.CancelAfter(TimeSpan.FromSeconds(180));
        var elapsed = Stopwatch.StartNew();
        string fixtures = Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_CONTROL_RETURN_FIXTURES")
            ?? throw new InvalidOperationException("Native control return requires exact retained preparation.");
        string output = Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_PLAYER_EVIDENCE")
            ?? throw new InvalidOperationException("Native control return requires a retained output directory.");
        output = Path.Combine(output, "control-return");
        Directory.CreateDirectory(output);
        string original = Path.Combine(fixtures, "pending-retreat.sqlite");
        byte[] originalHash = SHA256.HashData(File.ReadAllBytes(original));
        WorldSnapshot basis = RoadThreatWorldAdmission.CreateEmbedded("hostile-controller-survival", "Traveler");
        EntityId observer = basis.EncounterLifecycle!.Scenarios.Single(row => row.Key
            == EncounterJourneyAdmission.SecondScenarioKey).TargetActorId;
        WorldSnapshot genesis = WorldControllerAdmission.CreateFresh(WorldEncounterOutcomeAdmission.CreateCalibratedFresh(basis,
            new EncounterOutcomeCalibration(new Dictionary<EntityId, int>(), [observer])));
        Assert.Equal(WorldSnapshotCodec.Encode(genesis), File.ReadAllBytes(Path.Combine(fixtures, "genesis.ofc")));
        EntityId actor = basis.EncounterLifecycle.Scenarios.Single(row => row.Key
            == EncounterJourneyAdmission.FirstScenarioKey).InitiatingActorId;
        var entitlement = genesis.Inventory.Controls.Single(value => value.ActorId == actor);
        var delivery = new TrustedCommandDelivery(entitlement.PrincipalId, entitlement.SessionId);
        string database = Path.Combine(output, "native-return.sqlite");
        File.Copy(original, database);
        using var files = new ServerFiles();
        using var certificate = ServerWorld.CreateCertificate();
        string certificatePath = Path.Combine(output, "public.cer");
        File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Cert));
        var time = new NativeControlReturnClock();
        WorldSnapshot before;
        byte[] final;
        object? result = null;
        NativeRosterProcess? process = null;
        try
        {
            var authority = SqliteWorldAuthority.OpenExisting(database, WorldSnapshotCodec.Encode(genesis));
            before = authority.ReadStateForHost();
            Assert.Equal(File.ReadAllBytes(Path.Combine(fixtures, "pending-retreat.ofc")), WorldSnapshotCodec.Encode(before));
            Assert.Equal(WorldControllerMode.Autonomous, before.Controllers!.Get(actor).Mode);
            Assert.True(WorldEncounterRetreats.HoldsActor(before, actor));
            await using (var host = new LocalWorldSession(authority, actor, delivery, time,
                regions: new LocalRegionObserver(actor)))
            {
                await host.Player.DisposeAsync();
                var bootstrap = new PlayerPresentationBootstrap(genesis.Quests!.Definitions.Select(q => q.GiverActorId).Distinct().Single(),
                    genesis.Gathering!.Nodes.Single(n => n.Definition.Kind == GatherNodeKind.Oak).Definition.YieldDefinition.DefinitionId,
                    genesis.Gathering.Nodes.Single(n => n.Definition.Kind == GatherNodeKind.Berry).Definition.YieldDefinition.DefinitionId,
                    true, NativeCargoWorld.Stalls(genesis));
                var admission = new PlayerAdmissionService(host, FarmPackageCatalog.LoadEmbedded());
                var credential = admission.Issue(actor, delivery, TimeSpan.FromMinutes(4), new LocalRegionObserver(actor), bootstrap);
                await using var server = await PlayerWebSocketServer.StartAsync(admission,
                    new(IPAddress.Loopback, 0, certificate), limit.Token);
                string invitation = Path.Combine(files.DirectoryPath, "return.invitation.json");
                PlayerInvitationFile.Create(invitation, new(server.Endpoint, admission.Compatibility, actor, credential.Export()));
                string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../client"));
                string background = NativeRosterProcess.BackgroundProject(source, files.DirectoryPath);
                process = new NativeRosterProcess(Path.Combine(output, "rendered"), invitation, certificatePath,
                    background, true, limit.Token);
                await process.Opened();
                await ObserveControl("Pending");
                Assert.True(process.Last.GetProperty("LocallyPaused").GetBoolean());
                Assert.Equal(before.ContentHash, authority.ReadStateForHost().ContentHash);
                await process.Send("capture-control");
                await process.Send("try-pending-resume");
                await process.Send("hold-right");
                Assert.Equal(before.ContentHash, authority.ReadStateForHost().ContentHash);

                int cutoffs = 0;
                while (authority.ReadStateForHost().Controllers!.Get(actor).Mode != WorldControllerMode.Human)
                {
                    Assert.True(++cutoffs <= 256, "Accepted escape and control return must complete within the bounded route.");
                    await Tick();
                }
                WorldSnapshot restored = authority.ReadStateForHost();
                Assert.False(WorldEncounterRetreats.HoldsActor(restored, actor));
                Assert.NotEqual(before.Space.GetActor(actor).Feet, restored.Space.GetActor(actor).Feet);
                Assert.Equal(before.Inventory.ContentHash, restored.Inventory.ContentHash);
                await ObserveControl("Restored");
                Assert.True(process.Last.GetProperty("LocallyPaused").GetBoolean());
                await process.Send("capture-control");
                await process.Send("focus-return");
                for (int count = 0; count < 3; count++) await Tick();
                Assert.Equal(restored.ContentHash, authority.ReadStateForHost().ContentHash);
                await process.Send("resume");
                await ObserveControl("Available");
                Assert.False(process.Last.GetProperty("LocallyPaused").GetBoolean());
                await process.Send("capture-control");
                // The key held while blocked is still held. Resume must not turn it into movement.
                for (int count = 0; count < 3; count++) await Tick();
                Assert.Equal(restored.Space.GetActor(actor).Feet, authority.ReadStateForHost().Space.GetActor(actor).Feet);
                await process.Send("release-right");
                Assert.Empty(process.Submissions);
                await process.Send("close");
                await server.DisposeAsync();
                final = WorldSnapshotCodec.Encode(authority.ReadStateForHost());
                await host.StopAsync();
                limit.Token.ThrowIfCancellationRequested();
                Assert.True(elapsed.Elapsed.TotalSeconds < 180);
                result = new
                {
                    Status = "passed", Actor = actor.ToString(), PreparedRevision = before.Revision,
                    RestoredRevision = restored.Revision, Cutoffs = cutoffs, ElapsedSeconds = elapsed.Elapsed.TotalSeconds,
                    Boundary = "Actual saved autonomous retreat; native observes pending work, held-input refusal, completed retreat and explicit Resume over TLS."
                };
            }
            using var reopened = SqliteWorldAuthority.OpenExisting(database, WorldSnapshotCodec.Encode(genesis));
            Assert.Equal(final, WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
            Assert.Equal(before.ContentHash, reopened.ReadRecordedStateForHost(before.Revision).State.ContentHash);
            Assert.Equal(originalHash, SHA256.HashData(File.ReadAllBytes(original)));
            limit.Token.ThrowIfCancellationRequested();
            Assert.True(elapsed.Elapsed.TotalSeconds < 180);
            File.WriteAllText(Path.Combine(output, "journey.json"),
                JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "journey.failed.json"), error.ToString());
            throw;
        }
        finally { if (process is not null) await process.DisposeAsync(); }

        async Task Tick()
        {
            await Until(() => time.HasPendingTimer);
            time.Advance(LocalWorldSession.StepInterval);
            await Until(() => time.HasPendingTimer);
        }
        async Task ObserveControl(string expected)
        {
            var watch = Stopwatch.StartNew();
            do
            {
                await process!.Send("observe");
                limit.Token.ThrowIfCancellationRequested();
                Assert.True(watch.Elapsed.TotalSeconds < 5, "The control marker did not settle within five seconds.");
                if (process.Last.GetProperty("ControlState").GetString() == expected) return;
                await Task.Delay(10, limit.Token);
            } while (true);
        }
        async Task Until(Func<bool> ready)
        {
            var watch = Stopwatch.StartNew();
            while (!ready())
            {
                limit.Token.ThrowIfCancellationRequested();
                Assert.True(watch.Elapsed.TotalSeconds < 5);
                await Task.Delay(1, limit.Token);
            }
            Assert.True(watch.Elapsed.TotalSeconds < 5);
        }
    }
}
