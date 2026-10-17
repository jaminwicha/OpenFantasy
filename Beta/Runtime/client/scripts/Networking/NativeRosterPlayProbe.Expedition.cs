using System.Diagnostics;
using System.Globalization;
using Godot;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Client.Networking;

/// <summary>Bounded ordinary travel and captured withdrawal input for a saved cooperative expedition.</summary>
internal sealed partial class NativeRosterPlayProbe
{
    private async Task ChooseWithdrawal(bool cancel)
    {
        await Pause(false);
        await _driver.Until(() => _game.CurrentOffers.GetValueOrDefault(FarmSemanticAction.Retreat) is { CanExecute: true } offer
            && (cancel ? offer.Intent is CancelEncounterWithdrawalIntent : offer.Intent is QueueEncounterRetreatIntent)
            && MatchesView(offer), "The requested withdrawal operation has no exact live offer.");
        await ChooseWheel(FarmSemanticAction.Retreat);
    }

    private async Task WalkTo(string coordinates, bool allowEncounter = false)
    {
        long[] axes = coordinates.Split(',').Select(value => long.Parse(value, CultureInfo.InvariantCulture)).ToArray();
        MarketProbeDriver.Require(axes.Length == 3, "Walking needs one complete coordinate.");
        var target = new SubunitPosition(axes[0], axes[1], axes[2]);
        SubunitPosition Feet() => View.Actors.Single(actor => actor.ActorId == View.PlayerActorId).Feet;
        SubunitPosition origin = Feet();
        MarketProbeDriver.Require(origin.Z == target.Z && (origin.X == target.X || origin.Y == target.Y)
            && Math.Abs(target.X - origin.X) + Math.Abs(target.Y - origin.Y) <= 8 * 256
            && (target.X - origin.X) % WorldMovementInput.DisplacementPerStep == 0
            && (target.Y - origin.Y) % WorldMovementInput.DisplacementPerStep == 0,
            "A travel request must be one axis-aligned, at-most-eight-cell ordinary input leg.");
        var watch = Stopwatch.StartNew();
        Key? held = null;
        void Hold(Key? key)
        {
            if (held == key) return;
            if (held is { } previous)
                Input.ParseInputEvent(new InputEventKey { Keycode = previous, PhysicalKeycode = previous, Pressed = false });
            if (key is { } next)
                Input.ParseInputEvent(new InputEventKey { Keycode = next, PhysicalKeycode = next, Pressed = true });
            Input.FlushBufferedEvents();
            held = key;
            _driver.Inputs.Add(new { Stage = _stage, HeldDirection = key?.ToString() ?? "released" });
        }
        _driver.Inputs.Add(new { Stage = _stage, TravelOrigin = origin, TravelTarget = target });
        try
        {
            await Pause(false);
            SubunitPosition last = Feet();
            var progress = Stopwatch.StartNew();
            while (Feet() != target)
            {
                if (allowEncounter && View.IsInEncounter) break;
                _driver.Deadline();
                MarketProbeDriver.Require(watch.Elapsed.TotalSeconds < 15 && progress.Elapsed.TotalSeconds < 4,
                    "Held travel exceeded its movement or no-progress bound.");
                MarketProbeDriver.Require(!View.IsInEncounter, "Ordinary travel was interrupted by an encounter.");
                var current = Feet();
                bool crossed = origin.X != target.X
                    ? Math.Sign(target.X - current.X) != Math.Sign(target.X - origin.X)
                    : Math.Sign(target.Y - current.Y) != Math.Sign(target.Y - origin.Y);
                // Leave three samples of room for observation and stop delivery, without relaxing the final envelope.
                bool nearWaypoint = current != origin && Math.Abs(target.X - current.X) + Math.Abs(target.Y - current.Y)
                    <= 3 * WorldMovementInput.DisplacementPerStep;
                if (crossed || nearWaypoint) break;
                MarketProbeDriver.Require(current.Z == origin.Z && (current.X == origin.X || current.Y == origin.Y),
                    "Travel left its requested axis.");
                if (current != last)
                {
                    long distance = Math.Abs(current.X - last.X) + Math.Abs(current.Y - last.Y);
                    MarketProbeDriver.Require(distance % WorldMovementInput.DisplacementPerStep == 0,
                        "Observed travel was not aligned to ordinary movement samples.");
                    last = current;
                    progress.Restart();
                }
                Hold(target.X != origin.X ? target.X > origin.X ? Key.Right : Key.Left
                    : target.Y > origin.Y ? Key.Down : Key.Up);
                await _driver.Frame();
            }
            _driver.Inputs.Add(new { Stage = _stage, ReleaseObservedFeet = Feet(), Target = target,
                Seconds = watch.Elapsed.TotalSeconds });
            Hold(null);
            await Pause(true);
            await _driver.Delay(0.3);
            var settled = Feet();
            _driver.Inputs.Add(new { Stage = _stage, SettledFeet = settled, Target = target,
                Seconds = watch.Elapsed.TotalSeconds });
            MarketProbeDriver.Require(watch.Elapsed.TotalSeconds < 15, "Released travel exceeded its complete bound.");
            if (allowEncounter && View.IsInEncounter) return;
            MarketProbeDriver.Require(settled != origin && settled.Z == origin.Z
                && (origin.X == target.X ? settled.X == origin.X : settled.Y == origin.Y)
                && Math.Abs(target.X - settled.X) + Math.Abs(target.Y - settled.Y)
                    <= 4 * WorldMovementInput.DisplacementPerStep,
                "Released travel did not stay within its four-sample observation envelope.");
        }
        finally { Hold(null); }
    }
}
