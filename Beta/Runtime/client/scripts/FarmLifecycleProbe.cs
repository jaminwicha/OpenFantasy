using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Godot;
using Microsoft.Data.Sqlite;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Client;

/// <summary>Actual native-node shutdown qualification; headless execution makes no rendering claim.</summary>
internal partial class FarmLifecycleProbe : Node
{
    private readonly string _reportPath;
    private readonly string _mode;
    private readonly Task<LifecycleObservation> _inspection;
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private bool _done;

    private FarmLifecycleProbe(string savePath, string reportPath, string mode)
    {
        _reportPath = reportPath;
        _mode = mode;
        _inspection = Task.Run(() => InspectAsync(savePath, mode == "remove-opening"));
    }

    internal static void Start(Node parent, string savePath, string reportPath, string mode)
    {
        ArgumentNullException.ThrowIfNull(parent);
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(savePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
            if (mode == "inspect")
            {
                Require(File.Exists(savePath), "Inspection requires the existing lifecycle save.");
                parent.AddChild(new FarmLifecycleProbe(savePath, reportPath, mode));
                return;
            }
            Require(mode is "remove-opening" or "close-opening", "Unknown native lifecycle mode.");
            Require(!File.Exists(savePath), "Lifecycle startup requires a fresh save path.");
            var game = new FarmGame { SavePathOverride = savePath, StartPaused = true };
            parent.AddChild(game);
            // No frame or await occurs between the real _Ready callback and the lifecycle action.
            Require(game.CurrentView is null && !game.HasFailed, "The startup lifecycle action ran after public readiness.");
            if (mode == "remove-opening")
            {
                parent.RemoveChild(game);
                game.QueueFree();
                parent.AddChild(new FarmLifecycleProbe(savePath, reportPath, mode));
            }
            else
            {
                Write(reportPath, new
                {
                    Kind = "native-farm-lifecycle-v1", Mode = mode, Status = "initiated",
                    ActionBeforePublicReadiness = true, Runtime = RuntimeInformation.FrameworkDescription,
                    DisplayDriver = DisplayServer.GetName()
                });
                game.RequestClose();
                // FarmGame's actual close task owns opening/disposal and exits the native process.
                // A separate process must inspect the save before the checker can call this passed.
            }
        }
        catch (Exception error)
        {
            GD.PushError("Native farm lifecycle startup failed: " + error);
            parent.GetTree().Quit(1);
        }
    }

    public override void _Process(double delta)
    {
        if (_done)
        {
            return;
        }
        try
        {
            Require(_elapsed.Elapsed < TimeSpan.FromSeconds(25), "Native lifecycle inspection timed out.");
            if (!_inspection.IsCompleted)
            {
                return;
            }
            LifecycleObservation result = _inspection.GetAwaiter().GetResult();
            Write(_reportPath, new
            {
                Kind = "native-farm-lifecycle-v1", Mode = _mode, Status = "passed",
                ActionBeforePublicReadiness = _mode == "remove-opening",
                Runtime = RuntimeInformation.FrameworkDescription, DisplayDriver = DisplayServer.GetName(),
                Observation = result, DurationMilliseconds = _elapsed.ElapsedMilliseconds
            });
            _done = true;
            GD.Print("Native farm lifecycle passed: " + _mode);
            GetTree().Quit();
        }
        catch (Exception error)
        {
            _done = true;
            GD.PushError("Native farm lifecycle inspection failed: " + error);
            GetTree().Quit(1);
        }
    }

    private static async Task<LifecycleObservation> InspectAsync(string savePath, bool awaitStartupRelease)
    {
        var elapsed = Stopwatch.StartNew();
        Exception? last = null;
        int attempts = 0;
        do
        {
            attempts++;
            SqliteWorldAuthority? authority = null;
            try
            {
                authority = SqliteWorldAuthority.OpenExisting(savePath);
            }
            catch (Exception error) when (awaitStartupRelease &&
                error is IOException or InvalidDataException or SqliteException)
            {
                // File creation/metadata admission and exclusive ownership can still be in progress.
                // Only a successful complete shared-service recovery can establish the final result.
                last = error;
            }
            if (authority is not null)
            {
                using (authority)
                {
                    WorldInspection inspection = authority.InspectForHost();
                    WorldSnapshot state = inspection.State;
                    WorldResident player = state.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player);
                    ulong wood = state.Inventory.Custody.GetStoredQuantity(player.InventoryCustodianId);
                    Require(state.Revision == 0 && state.AdvancingSteps == 0 && wood == 8 && !state.HasReadingHold,
                        "Startup shutdown changed the initial world, wood or reading hold.");
                    WorldSnapshot expected = FarmWorldAdmission.CreateEmbedded("aldermere-first-walk-v1", "Traveler");
                    Require(state.ContentHash == expected.ContentHash, "Startup shutdown differs from the exact admitted genesis.");
                    Require(inspection.Database.LockingMode == "exclusive" && inspection.Database.JournalMode == "wal"
                        && inspection.Database.Synchronous == 2 && inspection.Database.ForeignKeys == 1,
                        "The reopened lifecycle save did not acquire the retained exclusive SQLite profile.");
                    return new LifecycleObservation(state.WorldId.ToString(), state.ContentHash.ToString(), state.Revision,
                        state.AdvancingSteps, wood, state.HasReadingHold, inspection.Database.Version,
                        inspection.Database.LockingMode, attempts);
                }
            }
            await Task.Delay(40).ConfigureAwait(false);
        }
        while (awaitStartupRelease && elapsed.Elapsed < TimeSpan.FromSeconds(15));
        throw new TimeoutException("The removed startup node did not release a complete, reopenable world save.", last);
    }

    private static void Write(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record LifecycleObservation(string WorldId, string StateHash, ulong Revision,
        ulong AdvancingSteps, ulong PlayerWood, bool ReadingHold, string NativeSqlite,
        string LockingMode, int OpenAttempts);
}
