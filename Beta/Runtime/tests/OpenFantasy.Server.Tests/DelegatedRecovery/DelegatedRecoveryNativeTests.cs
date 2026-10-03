using System.Diagnostics;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Host;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests.DelegatedRecovery;

public sealed class DelegatedRecoveryNativeTests(DelegatedRecoveryFixture fixture) : IClassFixture<DelegatedRecoveryFixture>
{
    public static bool Configured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENFANTASY_GODOT"));

    [Fact(SkipUnless = nameof(Configured), Skip = "Requires qualified Godot and retained native recovery evidence.")]
    public async Task Native_owner_returns_to_the_actual_killed_archived_world_and_resumes_after_its_original_paid_retreat()
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ServerWorld.Cancellation);
        limit.CancelAfter(TimeSpan.FromSeconds(180));
        var elapsed = Stopwatch.StartNew();
        string output = Path.Combine(Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_PLAYER_EVIDENCE")
            ?? throw new InvalidOperationException("Native recovery requires OPENFANTASY_NATIVE_PLAYER_EVIDENCE."), "delegated-recovery");
        Directory.CreateDirectory(output);
        var archive = Assert.Single(fixture.Boundaries, boundary => boundary.Name == "archive");
        string database = await DelegatedRecoveryProcessCrash.Execute(fixture, archive, committed: true,
            Path.Combine(output, "actual-crash"), limit.Token);
        File.WriteAllText(Path.Combine(output, "scope.txt"),
            "The archive publication is killed in a real external Host storage process. This is not a live TLS listener death.\n"
            + "Fresh-admission endurance, encounter setup, original loss/allowance, grant and paid retreat were prepared through the original journal.\n"
            + "This route uses the existing controlled monotonic host clock to expose the actual Pending/Restored boundary in Godot.\n"
            + "It does not claim native initial loss, wall-clock allowance or physical/OS input acceptance.\n");
        using var files = new ServerFiles();
        using var certificate = ServerWorld.CreateCertificate();
        string certificatePath = Path.Combine(output, "public.cer");
        File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Cert));
        var time = new NativeControlReturnClock();
        NativeRosterProcess? process = null;
        byte[]? finalBytes = null;
        object? result = null;
        try
        {
            var authority = SqliteWorldAuthority.OpenExisting(database, fixture.GenesisBytes);
            WorldSnapshot before = authority.ReadStateForHost();
            Assert.Equal(WorldSnapshotCodec.Encode(archive.After), WorldSnapshotCodec.Encode(before));
            Assert.Equal(fixture.Witness, before.EffectiveControl!.Find(fixture.Owner)!.Witness);
            Assert.True(WorldEncounterRetreats.HoldsActor(before, fixture.Owner));
            fixture.AssertPaidReceipt(authority);
            string beforeInspection = CaptureInspectionSnapshot(authority, before, output, "before-attachment");
            byte[] archiveBytes = DelegatedRecoveryProcessCrash.Archives(beforeInspection);
            await using (var host = new LocalWorldSession(authority, fixture.Owner, fixture.Delivery(fixture.Owner), time,
                trading: NativeCargoWorld.Configuration(before), regions: new LocalRegionObserver(fixture.Owner)))
            {
                WorldSnapshot attached = authority.ReadStateForHost();
                Assert.Null(attached.EffectiveControl!.Find(fixture.Owner)?.Witness);
                Assert.True(WorldEncounterRetreats.HoldsActor(attached, fixture.Owner));
                Assert.Equal(before.Calendar, attached.Calendar);
                Assert.Equal(before.AdvancingSteps, attached.AdvancingSteps);
                Assert.Equal(JsonSerializer.Serialize(before.Controllers), JsonSerializer.Serialize(attached.Controllers));
                Assert.Equal(JsonSerializer.Serialize(before.ControlPermissions), JsonSerializer.Serialize(attached.ControlPermissions));
                Assert.Equal(before.Inventory.ContentHash, attached.Inventory.ContentHash);
                Assert.Equal(before.Production!.ContentHash, attached.Production!.ContentHash);
                Assert.Equal(before.Progression!.ContentHash, attached.Progression!.ContentHash);
                Assert.Equal(before.Care!.ContentHash, attached.Care!.ContentHash);
                Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(authority.ReadRecordedStateForHost(before.Revision).State));
                fixture.AssertPaidReceipt(authority);
                string attachedInspection = CaptureInspectionSnapshot(authority, attached, output, "after-attachment");
                AssertAttachmentJournal(attachedInspection, before, attached, fixture.Witness);
                File.WriteAllBytes(Path.Combine(output, "attached.ofc"), WorldSnapshotCodec.Encode(attached));
                await host.Player.DisposeAsync();

                var genesis = fixture.Genesis;
                var bootstrap = new PlayerPresentationBootstrap(genesis.Quests!.Definitions.Select(quest => quest.GiverActorId).Distinct().Single(),
                    genesis.Gathering!.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Oak).Definition.YieldDefinition.DefinitionId,
                    genesis.Gathering.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Berry).Definition.YieldDefinition.DefinitionId,
                    true, NativeCargoWorld.Stalls(genesis));
                var admission = new PlayerAdmissionService(host, FarmPackageCatalog.LoadEmbedded());
                var credential = admission.Issue(fixture.Owner, fixture.Delivery(fixture.Owner), TimeSpan.FromMinutes(4),
                    new LocalRegionObserver(fixture.Owner), bootstrap);
                await using var server = await PlayerWebSocketServer.StartAsync(admission,
                    new(IPAddress.Loopback, 0, certificate), limit.Token);
                string invitation = Path.Combine(files.DirectoryPath, "owner-return.invitation.json");
                PlayerInvitationFile.Create(invitation, new(server.Endpoint, admission.Compatibility, fixture.Owner, credential.Export()));
                string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../client"));
                string background = NativeRosterProcess.BackgroundProject(source, files.DirectoryPath);
                process = new NativeRosterProcess(Path.Combine(output, "rendered-owner"), invitation, certificatePath,
                    background, rendered: true, limit.Token);
                await process.Opened();
                await ObserveControl("Pending");
                Assert.True(process.Last.GetProperty("LocallyPaused").GetBoolean());
                Assert.Equal(attached.ContentHash, authority.ReadStateForHost().ContentHash);
                await process.Send("capture-control");
                await process.Send("try-pending-resume");
                await process.Send("hold-right");
                Assert.Equal(attached.ContentHash, authority.ReadStateForHost().ContentHash);

                int cutoffs = 0;
                while (authority.ReadStateForHost().Controllers!.Get(fixture.Owner).Mode != WorldControllerMode.Human)
                {
                    Assert.True(++cutoffs <= 256, "The original paid retreat did not finish within the unchanged bounded return route.");
                    await Tick();
                }
                WorldSnapshot restored = authority.ReadStateForHost();
                Assert.False(WorldEncounterRetreats.HoldsActor(restored, fixture.Owner));
                Assert.NotEqual(before.Space.GetActor(fixture.Owner).Feet, restored.Space.GetActor(fixture.Owner).Feet);
                fixture.AssertOriginalOwnership(restored);
                fixture.AssertPaidReceipt(authority);
                fixture.AssertPaidCompletion(restored);
                AssertResourceContinuation(authority, before, restored, output);
                Assert.Equal(before.Inventory.ContentHash, restored.Inventory.ContentHash);
                Assert.Equal(before.Production!.ContentHash, restored.Production!.ContentHash);
                Assert.Equal(before.Progression!.Currency.Accounts.OrderBy(row => row.AccountId).Select(row => (row.AccountId, row.ActorId, row.Kind, row.Balance, row.Revision)),
                    restored.Progression!.Currency.Accounts.OrderBy(row => row.AccountId).Select(row => (row.AccountId, row.ActorId, row.Kind, row.Balance, row.Revision)));
                Assert.Equal(before.Space.Actors.OrderBy(row => row.ActorId).Select(row => (row.ActorId, row.Binding.FormInstanceId, row.Binding.PhysicalBodyId)),
                    restored.Space.Actors.OrderBy(row => row.ActorId).Select(row => (row.ActorId, row.Binding.FormInstanceId, row.Binding.PhysicalBodyId)));
                Assert.Equal(before.Space.Actors.Where(row => row.ActorId != fixture.Owner).OrderBy(row => row.ActorId).Select(row => (row.ActorId, row.Feet)),
                    restored.Space.Actors.Where(row => row.ActorId != fixture.Owner).OrderBy(row => row.ActorId).Select(row => (row.ActorId, row.Feet)));
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
                for (int count = 0; count < 3; count++) await Tick();
                Assert.Equal(restored.Space.GetActor(fixture.Owner).Feet, authority.ReadStateForHost().Space.GetActor(fixture.Owner).Feet);
                await process.Send("release-right");
                Assert.Empty(process.Submissions);
                await process.Send("close");
                await server.DisposeAsync();
                WorldSnapshot final = authority.ReadStateForHost();
                finalBytes = WorldSnapshotCodec.Encode(final);
                fixture.AssertPaidReceipt(authority);
                string finalInspection = CaptureInspectionSnapshot(authority, final, output, "after-native-continuation");
                Assert.Equal(archiveBytes, DelegatedRecoveryProcessCrash.Archives(finalInspection));
                AssertOriginalPaidContinuation(finalInspection, before, restored, fixture);
                File.WriteAllBytes(Path.Combine(output, "restored.ofc"), WorldSnapshotCodec.Encode(restored));
                File.WriteAllBytes(Path.Combine(output, "final.ofc"), finalBytes);
                await host.StopAsync();
                result = new
                {
                    Status = "passed", Actor = fixture.Owner.ToString(), OriginalDelegate = fixture.Grantee.ToString(),
                    CrashRevision = before.Revision, AttachmentRevision = attached.Revision, RestoredRevision = restored.Revision,
                    Cutoffs = cutoffs, PaidCommand = Convert.ToHexString(WorldActionCodec.Encode(fixture.PaidCommand)),
                    PaidReceipt = Convert.ToHexString(WorldReceiptCodecV1.Encode(fixture.PaidReceipt)),
                    ElapsedSeconds = elapsed.Elapsed.TotalSeconds,
                    Scope = "Actual killed archive publication; native Pending/Restored/explicit Resume over TLS, controlled monotonic host clock, original accepted retreat and archived bytes retained."
                };
            }
            using var reopened = SqliteWorldAuthority.OpenExisting(database, fixture.GenesisBytes);
            Assert.Equal(finalBytes, WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
            fixture.AssertPaidReceipt(reopened);
            limit.Token.ThrowIfCancellationRequested();
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(180), "The native recovery journey exceeded its unchanged 180-second bound.");
            DelegatedRecoveryProcessCrash.Write(output, "journey.json", result!);
        }
        catch (Exception error)
        {
            DelegatedRecoveryProcessCrash.Write(output, "journey.failed.json", new { Error = error.ToString(), ElapsedSeconds = elapsed.Elapsed.TotalSeconds });
            throw;
        }
        finally { if (process is not null) await process.DisposeAsync(); }

        async Task Tick()
        {
            await Until(() => time.HasPendingTimer, "The real host scheduler did not expose its next timer.");
            time.Advance(LocalWorldSession.StepInterval);
            await Until(() => time.HasPendingTimer, "The real host did not settle the released original cutoff.");
        }

        async Task ObserveControl(string expected)
        {
            var watch = Stopwatch.StartNew();
            while (true)
            {
                await process!.Send("observe");
                limit.Token.ThrowIfCancellationRequested();
                Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), "The recovered control observation did not settle within five seconds.");
                if (process.Last.GetProperty("ControlState").GetString() == expected) return;
                await Task.Delay(10, limit.Token);
            }
        }

        async Task Until(Func<bool> ready, string failure)
        {
            var watch = Stopwatch.StartNew();
            while (!ready())
            {
                limit.Token.ThrowIfCancellationRequested();
                Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), failure);
                await Task.Delay(1, limit.Token);
            }
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), failure);
        }
    }

    private string CaptureInspectionSnapshot(SqliteWorldAuthority authority, WorldSnapshot expected,
        string output, string name)
    {
        // The live authority owns the database's exclusive lease. Inspect only an actual
        // consistent backup, retaining proof that it is the exact observed authority root.
        byte[] expectedBytes = WorldSnapshotCodec.Encode(expected);
        Assert.Equal(expectedBytes, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
        string path = Path.Combine(output, name + "-inspection.sqlite3");
        authority.BackupForHost(path);
        Assert.Equal(expectedBytes, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
        using var snapshot = WorldDatabase.Open(path, create: false);
        Assert.Equal(fixture.GenesisBytes, snapshot.ReadGenesis());
        var head = snapshot.ReadHead();
        Assert.Equal(expected.Revision, head.Sequence);
        Assert.Equal(expected.AdvancingSteps, head.AdvancingSteps);
        Assert.Equal(expectedBytes, head.State);
        return path;
    }

    private static void AssertAttachmentJournal(string database, WorldSnapshot before, WorldSnapshot attached, WorldControlWitness witness)
    {
        using var store = WorldDatabase.Open(database, create: false);
        var row = Assert.Single(store.ReadJournal(before.Revision));
        var batch = WorldStepCodec.Decode(row.AcceptedBatch);
        Assert.Equal(WorldBatchKind.ControlAssignment, batch.Kind);
        Assert.Equal(WorldControlAssignmentEventKind.Release, batch.ControlAssignmentEvent!.Kind);
        Assert.Equal(witness, before.EffectiveControl!.Find(witness.OwnerActorId)!.Witness);
        Assert.Equal(witness.OwnerActorId, batch.ControlAssignmentEvent.ActorId);
        Assert.Equal(witness.AssignmentRevision, batch.ControlAssignmentEvent.ExpectedAssignmentRevision);
        Assert.Null(batch.ControlAssignmentEvent.Witness);
        Assert.Equal(before.AdvancingSteps, row.AdvancingSteps);
        Assert.Equal(before.Revision + 1, attached.Revision);
        Assert.Empty(batch.Movement);
        Assert.Empty(batch.Actions);
    }

    private static void AssertResourceContinuation(SqliteWorldAuthority authority, WorldSnapshot before,
        WorldSnapshot restored, string output)
    {
        // Full original-genesis replay authenticates these causal references; then independently
        // account every complete actor revision over the actual paid-work interval, including bites.
        Assert.Equal(WorldSnapshotCodec.Encode(restored),
            WorldSnapshotCodec.Encode(authority.ReadRecordedStateForHost(restored.Revision).State));
        var history = restored.ResourceHistory!.History;
        Assert.True(history.BasisWorldSequence <= before.Revision, "The bounded continuation must retain its full resource interval.");
        var resources = history.Batches.Where(batch => batch.WorldSequence > before.Revision).ToArray();
        var actors = before.Progression!.Actors.ToDictionary(actor => actor.ActorId);
        foreach (var batch in resources)
        {
            Assert.True(authority.IsIdentityOccupiedForHost(batch.CommandId));
            foreach (var change in batch.Changes)
            {
                Assert.Equal(JsonSerializer.Serialize(actors[change.Before.ActorId]), JsonSerializer.Serialize(change.Before));
                Assert.Equal(ActorResourceOperation.Damage, change.Operation);
                Assert.True(authority.IsIdentityOccupiedForHost(change.Cause.EvidenceId));
                Assert.Equal(JsonSerializer.Serialize(change.Before.TakeDamage(checked((uint)change.RequestedAmount)).Candidate),
                    JsonSerializer.Serialize(change.After));
                actors[change.After.ActorId] = change.After;
            }
        }
        Assert.Equal(actors.Keys.Order(), restored.Progression!.Actors.Select(actor => actor.ActorId).Order());
        foreach (var actor in restored.Progression.Actors)
            Assert.Equal(JsonSerializer.Serialize(actors[actor.ActorId]), JsonSerializer.Serialize(actor));
        DelegatedRecoveryProcessCrash.Write(output, "paid-continuation-resources.json", new
        {
            BeforeRevision = before.Revision, RestoredRevision = restored.Revision,
            Before = before.Progression.Actors, Changes = resources, After = restored.Progression.Actors
        });
    }

    private static void AssertOriginalPaidContinuation(string database, WorldSnapshot before, WorldSnapshot restored,
        DelegatedRecoveryFixture fixture)
    {
        using var store = WorldDatabase.Open(database, create: false);
        var continuation = store.ReadJournal(before.Revision).Where(row => row.Sequence <= restored.Revision)
            .Select(row => WorldStepCodec.Decode(row.AcceptedBatch)).ToArray();
        Assert.DoesNotContain(continuation.SelectMany(batch => batch.Actions), command => command.CommandId == fixture.PaidCommand.CommandId);
        Assert.Contains(continuation, batch => batch.ControllerEvent is { Kind: WorldControllerEventKind.Reconnected } reconnected
            && reconnected.ActorId == fixture.Owner
            && reconnected.DisconnectId == before.Controllers!.Get(fixture.Owner).DisconnectId);
        Assert.DoesNotContain(continuation, batch => batch.ControllerEvent is { Kind: WorldControllerEventKind.Disconnected } disconnected
            && disconnected.ActorId == fixture.Owner);
    }
}
