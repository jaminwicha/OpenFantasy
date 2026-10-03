using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Host;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Server.Tests.ConnectedControl;

namespace OpenFantasy.Server.Tests.SelectedControlNative;

public sealed partial class NativeSelectedControlJourneyTests
{
    public static bool Configured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENFANTASY_GODOT"));

    [Fact(SkipUnless = nameof(Configured), Skip = "Requires qualified Godot and a retained native player evidence directory.")]
    public async Task Four_native_humans_consent_take_over_choose_paid_combat_and_return_control_over_tls()
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ServerWorld.Cancellation);
        bound.CancelAfter(TimeSpan.FromSeconds(180));
        var elapsed = Stopwatch.StartNew();
        string output = Path.Combine(Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_PLAYER_EVIDENCE")
            ?? throw new InvalidOperationException("OPENFANTASY_NATIVE_PLAYER_EVIDENCE must retain the native journey."), "selected-control");
        Directory.CreateDirectory(output);
        using var files = new ServerFiles();
        await using var world = await ConnectedControlWorld.Open(prepareCombatApproach: true);
        File.WriteAllBytes(Path.Combine(output, "genesis.ofc"), WorldSnapshotCodec.Encode(world.Genesis));
        File.WriteAllBytes(Path.Combine(output, "prepared.ofc"), WorldSnapshotCodec.Encode(world.Prepared));
        File.WriteAllText(Path.Combine(output, "preparation.txt"), world.Describe());
        string certificate = Path.Combine(output, "public.cer");
        File.WriteAllBytes(certificate, world.Certificate.Export(X509ContentType.Cert));
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../client"));
        string background = NativeRosterProcess.BackgroundProject(source, files.DirectoryPath);
        var processes = new List<NativeRosterProcess>();
        var active = new Dictionary<EntityId, NativeRosterProcess>();
        var stages = new List<object>();
        double expirySeconds = 0;
        try
        {
            foreach (EntityId actor in world.Actors)
                active.Add(actor, Start(actor, "first", actor == world.Grantee));
            await Task.WhenAll(active.Values.Select(process => process.Opened()));
            foreach (var pair in active)
            {
                Assert.Equal(pair.Key, pair.Value.View.PlayerActorId);
                Assert.Equal(world.Prepared.Calendar, pair.Value.View.Calendar);
                AssertHomePaused(pair.Value, pair.Key);
            }
            var owner = active[world.Owner];
            var grantee = active[world.Grantee];
            await owner.Send("control-open");
            await owner.Send("control-offer", "Combat,Traversal", world.Grantee);
            var offered = Slot(owner, world.Owner, world.Grantee);
            Assert.Equal("Offered", offered.GetProperty("Status").GetString());
            EntityId grantId = EntityId.Parse(offered.GetProperty("GrantCommandId").GetString()!);
            await owner.Send("control-close");
            await grantee.Send("control-open");
            Assert.Equal("Offered", Slot(grantee, world.Owner, world.Grantee).GetProperty("Status").GetString());
            await grantee.Send("capture-selection");
            await grantee.Send("control-accept", target: world.Owner);
            var accepted = Slot(grantee, world.Owner, world.Grantee);
            Assert.Equal("Accepted", accepted.GetProperty("Status").GetString());
            EntityId acceptanceId = EntityId.Parse(accepted.GetProperty("AcceptanceCommandId").GetString()!);
            Assert.False(accepted.GetProperty("AcquisitionAvailable").GetBoolean());
            foreach (EntityId actor in world.Actors.Skip(2))
            {
                await active[actor].Send("control-open");
                Assert.Empty(active[actor].Last.GetProperty("ControlBoard").GetProperty("Slots").EnumerateArray());
                await active[actor].Send("control-close");
            }
            Mark("consent-accepted");
            var allowance = Stopwatch.StartNew();
            await owner.Send("close");
            active.Remove(world.Owner);
            while (true)
            {
                await grantee.Send("control-refresh");
                await Observe(active.Values);
                bound.Token.ThrowIfCancellationRequested();
                Assert.True(allowance.Elapsed < TimeSpan.FromSeconds(35), "The original 30-second allowance did not expose native acquisition within 35 seconds.");
                Assert.All(active.Values, process => Assert.Equal(world.Prepared.Calendar, process.View.Calendar));
                if (Slot(grantee, world.Owner, world.Grantee).GetProperty("AcquisitionAvailable").GetBoolean()) break;
                await Task.Delay(100, bound.Token);
            }
            expirySeconds = allowance.Elapsed.TotalSeconds;
            Assert.True(expirySeconds >= 30, "Native acquisition preceded the original host-measured reconnect allowance.");
            await grantee.Send("control-acquire", target: world.Owner);
            var witness = WorldControlWitnessCodecV1.Decode(Convert.FromBase64String(
                grantee.Last.GetProperty("ControlBoard").GetProperty("HeldWitness").GetString()!));
            Assert.Equal(world.Owner, witness.OwnerActorId);
            Assert.Equal(world.Grantee, witness.GranteeActorId);
            Assert.Equal(grantId, witness.GrantCommandId);
            Assert.Equal(acceptanceId, witness.AcceptanceCommandId);
            Assert.Equal(WorldControlCapabilities.Combat | WorldControlCapabilities.Traversal, witness.Capabilities);
            await grantee.Send("control-select", target: world.Owner);
            AssertSelectedPaused(grantee, world.Grantee, world.Owner);
            ulong firstSelection = Selection(grantee).GetProperty("Generation").GetUInt64();
            await grantee.Send("capture-selection");
            await grantee.Send("control-open");
            await grantee.Send("control-home");
            AssertHomePaused(grantee, world.Grantee);
            await grantee.Send("control-open");
            await grantee.Send("control-select", target: world.Owner);
            Assert.Equal(firstSelection + 2, Selection(grantee).GetProperty("Generation").GetUInt64());
            AssertSelectedPaused(grantee, world.Grantee, world.Owner);
            await grantee.Send("control-close");
            await grantee.Send("capture-selection");
            Mark("selected-paused");

            await grantee.Send("resume");
            var beforeFeet = SelectedFeet(grantee);
            string direction = world.EntryMovement.X < 0 ? "left" : world.EntryMovement.X > 0 ? "right"
                : world.EntryMovement.Y < 0 ? "up" : "down";
            await grantee.Send("selected-walk", direction);
            Assert.NotEqual(beforeFeet, SelectedFeet(grantee));
            await grantee.Send("selected-enter", target: world.TargetActorId);
            await Until(() => active.Values.All(process => Selection(process).GetProperty("Phase").GetString() == "Active"),
                "All native participants must observe the actual entered encounter.", active.Values, bound.Token);
            await grantee.Send("selected-strike");
            WorldActionCommand attack = LastAction(grantee);
            Assert.IsType<QueueEncounterAbilityIntent>(attack.Intent);
            Assert.Equal(world.Owner, attack.ActorId);
            Assert.Equal(witness, attack.ControlWitness);
            var granteeBinding = world.Genesis.Inventory.Controls.Single(row => row.ActorId == world.Grantee);
            Assert.Equal(granteeBinding.PrincipalId, attack.PrincipalId);
            Assert.Equal(granteeBinding.SessionId, attack.OriginSessionId);
            int targetHp = SelectedTargetHp(grantee, world.TargetActorId);
            var waitCommands = new List<WorldActionCommand>();
            ulong previousTiming = ((QueueEncounterAbilityIntent)attack.Intent).ExpectedTimingRevision;
            await grantee.Send("control-open");
            await grantee.Send("control-home");
            await grantee.Send("control-close");
            foreach (EntityId actor in world.Actors.Skip(1))
            {
                var process = active[actor];
                await Until(() => process.View.EncounterActions is { CanWait: true } choices
                        && choices.TimingRevision > previousTiming,
                    "The next actual home character has not received its settled Wait choice.", active.Values, bound.Token);
                await process.Send("selected-wait");
                var wait = LastAction(process);
                var intent = Assert.IsType<QueueEncounterWaitIntent>(wait.Intent);
                Assert.Equal(actor, wait.ActorId);
                Assert.Null(wait.ControlWitness);
                waitCommands.Add(wait);
                previousTiming = intent.ExpectedTimingRevision;
            }
            await grantee.Send("control-open");
            await grantee.Send("control-select", target: world.Owner);
            await grantee.Send("control-close");
            await grantee.Send("resume");
            await Until(() => SelectedTargetHp(grantee, world.TargetActorId) < targetHp,
                "The accepted native Strike did not publish target damage.", active.Values, bound.Token);
            await grantee.Send("capture-selection");
            foreach (var process in active.Values) await process.Send("pause");
            await Observe(active.Values);
            Assert.All(active.Values, process => Assert.True(process.Last.GetProperty("LocallyPaused").GetBoolean()));
            ulong operatedGeneration = Selection(grantee).GetProperty("Generation").GetUInt64();
            Mark("paid-action-resolved");

            var returnedOwner = Start(world.Owner, "return", rendered: false);
            active.Add(world.Owner, returnedOwner);
            await returnedOwner.Opened();
            await Until(() => SelectedActor(grantee) == world.Grantee,
                "Owner return did not force the native delegate home.", active.Values, bound.Token);
            AssertHomePaused(grantee, world.Grantee);
            Assert.True(Selection(grantee).GetProperty("Generation").GetUInt64() > operatedGeneration);
            await returnedOwner.Send("control-open");
            await returnedOwner.Send("control-revoke", target: world.Grantee);
            Assert.Equal("Revoked", Slot(returnedOwner, world.Owner, world.Grantee).GetProperty("Status").GetString());
            await returnedOwner.Send("control-close");
            await grantee.Send("control-open");
            Assert.Equal(JsonValueKind.Null, grantee.Last.GetProperty("ControlBoard").GetProperty("HeldWitness").ValueKind);
            Assert.Equal("Revoked", Slot(grantee, world.Owner, world.Grantee).GetProperty("Status").GetString());
            await grantee.Send("capture-selection");
            await grantee.Send("control-close");
            AssertHomePaused(grantee, world.Grantee);
            Mark("owner-returned-revoked");
            foreach (var process in active.Values) await process.Send("close");
            Assert.False(world.Host.Session.Completion.IsCompleted);
            await world.Stop();
            Verify(output, world, processes, witness, attack, waitCommands, targetHp);
            bound.Token.ThrowIfCancellationRequested();
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(180), "The native selected-control journey exceeded its absolute 180-second bound.");
            Write(output, "journey.json", new
            {
                Kind = "native-selected-control-v1", Status = "passed", World = world.Genesis.WorldId.ToString(),
                Owner = world.Owner.ToString(), Grantee = world.Grantee.ToString(), ExpirySeconds = expirySeconds,
                ElapsedMilliseconds = elapsed.ElapsedMilliseconds, Stages = stages,
                Preparation = "Original-genesis journaled ordinary travel; no native travel claim for preparation.",
                Movement = "Ordinary native held input until the first observed step within five seconds, then release; exact canonical movement attribution is checked, wire packet count is not asserted."
            });
        }
        catch (Exception error)
        {
            Write(output, "journey.failed.json", new { Kind = "native-selected-control-v1", Status = "failed",
                Error = error.ToString(), ElapsedMilliseconds = elapsed.ElapsedMilliseconds, ExpirySeconds = expirySeconds, Stages = stages });
            throw;
        }
        finally
        {
            try
            {
                foreach (var process in processes) await process.DisposeAsync();
            }
            finally
            {
                await world.Stop();
                PreserveFinal(output, world);
            }
        }

        NativeRosterProcess Start(EntityId actor, string suffix, bool rendered)
        {
            var invitation = new PlayerConnectionInvitation(world.Server.Endpoint, world.Host.Admission.Compatibility,
                actor, world.Host.Issue(actor, TimeSpan.FromMinutes(4)).Export());
            string path = Path.Combine(files.DirectoryPath, actor + "-" + suffix + ".invitation.json");
            PlayerInvitationFile.Create(path, invitation);
            var process = new NativeRosterProcess(Path.Combine(output, actor + "-" + suffix), path, certificate,
                rendered ? background : source, rendered, bound.Token);
            processes.Add(process);
            return process;
        }
        void Mark(string stage) => stages.Add(new { Stage = stage, ElapsedMilliseconds = elapsed.ElapsedMilliseconds,
            Observations = active.Select(pair => new { Actor = pair.Key.ToString(), pair.Value.Last }).ToArray() });
    }
}
