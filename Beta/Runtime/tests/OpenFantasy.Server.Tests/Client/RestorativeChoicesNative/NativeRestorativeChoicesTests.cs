using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Host;
using OpenFantasy.Hosting.Networking;

namespace OpenFantasy.Server.Tests.RestorativeChoicesNative;

public sealed partial class NativeRestorativeChoicesTests
{
    public static bool Configured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENFANTASY_GODOT"));

    [Fact(SkipUnless = nameof(Configured), Skip = "Requires qualified Godot and retained native player evidence.")]
    public async Task Real_native_care_library_pins_patient_permission_and_finite_dose_without_Inventory_and_refuses_a_returned_owner_capture()
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ServerWorld.Cancellation);
        bound.CancelAfter(TimeSpan.FromSeconds(210));
        var elapsed = Stopwatch.StartNew();
        string output = Path.Combine(Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_PLAYER_EVIDENCE")
            ?? throw new InvalidOperationException("Care evidence requires OPENFANTASY_NATIVE_PLAYER_EVIDENCE."), "restorative-choices");
        Directory.CreateDirectory(output);
        using var files = new ServerFiles();
        await using var world = await RestorativeChoicesWorld.Open(output);
        string certificate = Path.Combine(output, "public.cer");
        File.WriteAllBytes(certificate, world.Certificate.Export(X509ContentType.Cert));
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../client"));
        string background = NativeRosterProcess.BackgroundProject(source, files.DirectoryPath);
        var processes = new List<NativeRosterProcess>();
        var active = new Dictionary<EntityId, NativeRosterProcess>();
        var stages = new List<object>();
        double allowanceSeconds = 0;
        try
        {
            foreach (var actor in world.Actors) active.Add(actor, Start(actor, "first", actor == world.Grantee));
            await Task.WhenAll(active.Values.Select(process => process.Opened()));
            var owner = active[world.Owner];
            var grantee = active[world.Grantee];
            foreach (var pair in active) AssertPaused(pair.Value, pair.Key, pair.Key);
            await owner.Send("control-offer", "Recovery, CareDisposition", world.Grantee);
            await owner.Send("control-close");
            await grantee.Send("control-open");
            await grantee.Send("control-accept", target: world.Owner);
            var allowance = Stopwatch.StartNew();
            await owner.Send("close");
            active.Remove(world.Owner);
            while (true)
            {
                await grantee.Send("control-refresh");
                bound.Token.ThrowIfCancellationRequested();
                Assert.True(allowance.Elapsed < TimeSpan.FromSeconds(35), "The actual 30-second allowance did not expose acquisition within 35 seconds.");
                Assert.Equal(world.Prepared.Calendar, grantee.View.Calendar);
                if (Slot(grantee, world.Owner, world.Grantee).GetProperty("AcquisitionAvailable").GetBoolean()) break;
                await Task.Delay(500, bound.Token);
            }
            allowanceSeconds = allowance.Elapsed.TotalSeconds;
            Assert.True(allowanceSeconds >= 30);
            await grantee.Send("control-acquire", target: world.Owner);
            var witness = WorldControlWitnessCodecV1.Decode(Convert.FromBase64String(
                grantee.Last.GetProperty("ControlBoard").GetProperty("HeldWitness").GetString()!));
            Assert.Equal(WorldControlCapabilities.Recovery | WorldControlCapabilities.CareDisposition, witness.Capabilities);
            await grantee.Send("control-select", target: world.Owner);
            AssertPaused(grantee, world.Grantee, world.Owner);
            await grantee.Send("control-close");
            await grantee.Send("resume");
            await grantee.Send("care-library-open");
            var library = Care(grantee);
            Assert.True(library.GetProperty("RecoveryDisclosed").GetBoolean());
            Assert.True(library.GetProperty("DispositionDisclosed").GetBoolean());
            Assert.False(library.GetProperty("InventoryDisclosed").GetBoolean());
            Assert.False(library.GetProperty("CombatDisclosed").GetBoolean());
            Assert.Equal(world.Owner.ToString(), library.GetProperty("Actor").GetString());
            Assert.Equal(2, library.GetProperty("Entries").GetArrayLength());
            Assert.Equal(JsonValueKind.Null, library.GetProperty("Selected").ValueKind);
            Assert.Equal(JsonValueKind.Null, library.GetProperty("Prepared").ValueKind);
            int hp = world.Prepared.Progression!.GetActor(world.Owner).CurrentHp;
            Assert.Equal(hp, library.GetProperty("Hp").GetInt32());
            await grantee.Send("capture-care-library", "two-real-choice-kinds");
            await grantee.Send("care-library-select", "WithdrawPermission", world.Permission);
            await grantee.Send("care-library-prepare");
            await grantee.Send("capture-care-library", "exact-permission-review");
            Assert.Empty(grantee.Submissions);
            await grantee.Send("care-library-confirm", "Applied");
            var withdrawal = LastCommand(grantee);
            Assert.Equal(world.Permission, Assert.IsType<RevokeCarePermissionIntent>(withdrawal.Intent).PermissionId);
            Assert.Equal(hp, Care(grantee).GetProperty("Hp").GetInt32());
            Assert.DoesNotContain(Care(grantee).GetProperty("Permissions").EnumerateArray(),
                row => row.GetProperty("Id").GetString() == world.Permission.ToString());
            Mark("patient-withdrew-exact-permission-without-medicine");

            await grantee.Send("care-library-select", "UseRemedy", world.UseDefinition);
            await grantee.Send("care-library-prepare");
            await grantee.Send("capture-care-library", "own-finite-raw-dose-review");
            Assert.Single(grantee.Submissions);
            Assert.Equal(hp, Care(grantee).GetProperty("Hp").GetInt32());
            await grantee.Send("care-library-confirm", "Applied");
            var dose = LastCommand(grantee);
            Assert.Equal(IngredientLotKind.Source, Assert.IsType<UseRestorativeItemIntent>(dose.Intent).Ingredient.Kind);
            Assert.Equal(hp + 40, Care(grantee).GetProperty("Hp").GetInt32());
            Assert.InRange(Care(grantee).GetProperty("Hp").GetInt32(), 41, 59);
            Mark("delegated-own-dose-paid-and-healed");

            // This is a second genuine finite dose and a still-injured patient, not a blocked/full-health placeholder.
            await grantee.Send("care-library-select", "UseRemedy", world.UseDefinition);
            await grantee.Send("care-library-prepare");
            await grantee.Send("capture-care-library", "crafted-dose-before-owner-return");
            var stale = Care(grantee).GetProperty("Prepared").Clone();
            Assert.True(stale.GetProperty("CanExecute").GetBoolean());
            Assert.Equal((int)IngredientLotKind.Transformed, stale.GetProperty("Use").GetProperty("Ingredient").GetProperty("Kind").GetInt32());
            int committed = grantee.Submissions.Count;
            var returning = Start(world.Owner, "return", rendered: false);
            active.Add(world.Owner, returning);
            await returning.Opened();
            await Until(() => Selection(grantee).GetProperty("Actor").GetString() == world.Grantee.ToString(),
                "Owner return did not invalidate the care selection.", active.Values, bound.Token);
            AssertPaused(grantee, world.Grantee, world.Grantee);
            await returning.Send("control-revoke", target: world.Grantee);
            await returning.Send("control-close");
            await grantee.Send("care-library-confirm", "blocked");
            Assert.Equal(committed, grantee.Submissions.Count);
            Assert.False(Care(grantee).GetProperty("Open").GetBoolean());
            Assert.Equal(JsonValueKind.Null, Care(grantee).GetProperty("Prepared").ValueKind);
            Mark("unconsumed-crafted-dose-capture-refused");
            foreach (var process in active.Values) await process.Send("pause");
            foreach (var process in active.Values) await process.Send("close");
            await world.Stop();
            Verify(output, world, processes, witness, withdrawal, dose, stale);
            bound.Token.ThrowIfCancellationRequested();
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(210), "The native care library journey exceeded its absolute 210-second limit.");
            Write(output, "journey.json", new { Kind = "native-restorative-choices-v1", Status = "passed", AllowanceSeconds = allowanceSeconds,
                ElapsedMilliseconds = elapsed.ElapsedMilliseconds, Stages = stages, Preparation = world.Description,
                Scope = "Two existing distinct choice kinds; exact Recovery/CareDisposition without Inventory; source-stat injury and finite raw/crafted medicine. Multiple remedy definitions and otherwise-eligible helper suppression remain separate managed contrasts." });
        }
        catch (Exception error)
        {
            Write(output, "journey.failed.json", new { Kind = "native-restorative-choices-v1", Status = "failed", Error = error.ToString(),
                AllowanceSeconds = allowanceSeconds, ElapsedMilliseconds = elapsed.ElapsedMilliseconds, Stages = stages });
            throw;
        }
        finally
        {
            try { foreach (var process in processes) await process.DisposeAsync(); }
            finally { await world.Stop(); PreserveFinal(output, world); }
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
            Peers = active.Select(pair => new { Home = pair.Key.ToString(), Actor = Selection(pair.Value).GetProperty("Actor").GetString(),
                Commands = pair.Value.Submissions.Count }).ToArray() });
    }
}
