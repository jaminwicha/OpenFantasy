using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Host;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests;

public sealed class NativeSavedCombatContinuationTests
{
    public static bool Configured => NativeSharedAdventureTests.Configured
        && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENFANTASY_SAVED_COMBAT"));

    [Fact(SkipUnless = nameof(Configured), Skip = "Requires the exact retained native combat boundary and Godot.")]
    public async Task Interrupted_earned_expedition_reopens_and_withdraws_without_retrying_the_stale_choice()
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ServerWorld.Cancellation);
        bound.CancelAfter(TimeSpan.FromSeconds(180));
        var watch = Stopwatch.StartNew();
        string source = Environment.GetEnvironmentVariable("OPENFANTASY_SAVED_COMBAT")!;
        byte[] original = File.ReadAllBytes(source);
        string expectedHash = Convert.ToHexStringLower(SHA256.HashData(original));
        var expectedPhase = expectedHash switch
        {
            "99455e80e6f2571826d4f80b9fa0bf8739070b8176b4e5bec01152699e607f16" => EncounterLifecyclePhase.Active,
            "0110862457bbfa90a8e31b73b5a1ff8257791018bb18365fb52b3593117fddc9" => EncounterLifecyclePhase.Staging,
            _ => throw new InvalidDataException("Use one of the independently retained, exact native combat saves.")
        };
        string output = Path.Combine(Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_PLAYER_EVIDENCE")
            ?? throw new InvalidOperationException("Retained evidence is required."), "saved-combat");
        Directory.CreateDirectory(output);
        string database = Path.Combine(output, "world.sqlite3");
        File.Copy(source, database, overwrite: false);
        WorldSnapshot genesis, basis;
        using (var saved = SqliteWorldAuthority.OpenExisting(database))
        {
            basis = saved.ReadStateForHost();
            genesis = saved.ReadRecordedStateForHost(0).State;
            Assert.Equal(41, basis.ProfileVersion);
            Assert.Equal(expectedPhase, basis.EncounterLifecycle!.Phase);
            Assert.Equal(1UL, basis.EncounterLifecycle.EncounterCount);
        }
        EntityId lead = genesis.Excavation!.Sites.Single().OwnerActorId;
        EntityId[] humans = genesis.Residents.Where(actor => actor.ControllerKind == ResidentControllerKind.Player)
            .Select(actor => actor.ActorId).OrderBy(actor => actor == lead ? 0 : 1).ToArray();
        Assert.Equal(2, humans.Length);
        EntityId chief = genesis.EncounterLifecycle!.Scenarios.Single(row => row.TargetActorId == lead
            && genesis.GetResident(row.InitiatingActorId).ControllerKind == ResidentControllerKind.Host).InitiatingActorId;
        using var certificate = ServerWorld.CreateCertificate();
        string publicCertificate = Path.Combine(output, "public.cer");
        File.WriteAllBytes(publicCertificate, certificate.Export(X509ContentType.Cert));
        using var files = new ServerFiles();
        string project = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../client"));
        string background = NativeRosterProcess.BackgroundProject(project, files.DirectoryPath);
        var peers = new Dictionary<EntityId, NativeRosterProcess>();
        var stages = new List<object>();
        string stage = "opening-existing-combat";
        try
        {
            await using (var host = await CargoPlayerHost.OpenExistingAsync(database, bound.Token))
            await using (var server = await PlayerWebSocketServer.StartAsync(host.Admission,
                new(IPAddress.Loopback, 0, certificate), bound.Token))
            {
                foreach (EntityId actor in humans)
                {
                    var invitation = new PlayerConnectionInvitation(server.Endpoint, host.Admission.Compatibility, actor,
                        host.Issue(actor, TimeSpan.FromMinutes(4)).Export());
                    string path = Path.Combine(files.DirectoryPath, actor + ".invitation.json");
                    PlayerInvitationFile.Create(path, invitation);
                    peers.Add(actor, new NativeRosterProcess(Path.Combine(output, actor.ToString()), path,
                        publicCertificate, actor == lead ? background : project, actor == lead, bound.Token));
                }
                await Task.WhenAll(peers.Values.Select(peer => peer.Opened()));
                Assert.All(peers.Values, peer => Assert.True(peer.View.IsInEncounter));
                var healthy = humans.ToDictionary(actor => actor, actor => peers[actor].View.Progression!.Hp);
                await new NativeExpeditionEncounter(peers, lead, Mark, value => stage = value, bound.Token).Complete(healthy);
                foreach (var peer in peers.Values) await peer.Send("pause");
                await peers[lead].Send("capture");
                foreach (var peer in peers.Values) await peer.Send("close");
            }
            WorldStepBatch[] batches;
            using (var journal = WorldDatabase.Open(database, create: false))
                batches = journal.ReadJournal(basis.Revision).Select(row => WorldStepCodec.Decode(row.AcceptedBatch)).ToArray();
            var hostile = batches.SelectMany(batch => batch.Actions).Where(command => command.ActorId == chief
                && command.Intent is QueueEncounterAbilityIntent).ToArray();
            Assert.NotEmpty(hostile);
            using var finalSave = SqliteWorldAuthority.OpenExisting(database, WorldSnapshotCodec.Encode(genesis));
            WorldSnapshot final = finalSave.ReadStateForHost();
            Assert.Equal(final.ContentHash, finalSave.ReadRecordedStateForHost(final.Revision).State.ContentHash);
            Assert.Equal(genesis.AdmissionVersion, final.AdmissionVersion);
            foreach (var row in peers.Values.SelectMany(peer => peer.Submissions))
            {
                var command = WorldActionCodec.Decode(Convert.FromHexString(row.GetProperty("Command").GetString()!));
                Assert.Contains(command.ActorId, humans);
                Assert.Null(command.ControlWitness);
                var receipt = finalSave.FindReceipt(command, new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId));
                Assert.NotNull(receipt);
                Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
                Assert.False(row.GetProperty("IsRetry").GetBoolean());
                Assert.Equal(Convert.FromHexString(row.GetProperty("Receipt").GetString()!), WorldReceiptCodecV1.Encode(receipt));
            }
            Assert.Equal(original, File.ReadAllBytes(source));
            bound.Token.ThrowIfCancellationRequested();
            Assert.True(watch.Elapsed.TotalSeconds < 180);
            Write("result.json", new { Status = "passed", Seconds = watch.Elapsed.TotalSeconds,
                SourceHash = expectedHash, InitialPhase = expectedPhase.ToString(), BasisRevision = basis.Revision, FinalRevision = final.Revision,
                FinalHash = final.ContentHash.Value, HostileActions = hostile.Length, Stages = stages,
                Limitations = "Saved combat continuation only; not the complete commerce/return journey, human or scale acceptance." });
        }
        catch (Exception error)
        {
            Write("failure.json", new { Stage = stage, Error = error.ToString(), Seconds = watch.Elapsed.TotalSeconds, Stages = stages });
            throw;
        }
        finally { foreach (var peer in peers.Values) await peer.DisposeAsync(); }

        void Mark(string name)
        {
            stages.Add(new { Stage = name, Seconds = watch.Elapsed.TotalSeconds,
                Players = peers.Select(pair => new { Actor = pair.Key.ToString(), pair.Value.View.Progression!.Hp,
                    pair.Value.View.IsInEncounter, Withdrawal = pair.Value.View.Retreat!.Withdrawal!.Last }).ToArray() });
            Write("progress.json", stages);
        }
        void Write(string name, object value) => File.WriteAllText(Path.Combine(output, name),
            JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }
}
