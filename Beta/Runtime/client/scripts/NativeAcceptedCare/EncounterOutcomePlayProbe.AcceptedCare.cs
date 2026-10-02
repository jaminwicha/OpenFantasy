using System.Diagnostics;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;

namespace OpenFantasy.Client;

/// <summary>The full-health native journey shares the outcome probe's passive submission and rendered-observation seams.</summary>
internal sealed partial class EncounterOutcomePlayProbe
{
    private object? _acceptedCare;

    private async Task AcceptCare(EntityId wolf)
    {
        Require(!View.IsInEncounter && EncounterCount == 0 && View.Progression!.Hp > 1,
            "Accepted care requires an unplayed healthy world.");
        int initialHp = View.Progression!.Hp;
        await Capture("healthy");
        await Resume();
        SetStage("approach-wolf");
        SubunitPosition target = View.Actors.Single(actor => actor.ActorId == wolf).Feet;
        CellCoordinate here = new(Feet.Cell.X, Feet.Cell.Y, Feet.Cell.Z - 1);
        // The authored opening is already beside this wolf. Walk away and return through actual input
        // so a zero-length route cannot masquerade as native approach coverage.
        await Walk([CargoCorridor.FeetAt(new CellCoordinate(here.X - 2, here.Y, here.Z))]);
        Require(_walkInputs.Count > 0, "The approach recorded no native travel input.");
        CellCoordinate near = new(target.Cell.X - 1, target.Cell.Y, here.Z);
        await Walk(CargoCorridor.Lane([here, new CellCoordinate(near.X, here.Y, here.Z), near])
            .Select(CargoCorridor.FeetAt).ToArray());
        SetStage("select-wolf");
        for (int presses = 0; ; presses++)
        {
            if (_game.CurrentOffers.TryGetValue(FarmSemanticAction.EnterEncounter, out var entry)
                && entry.CanExecute && entry.TargetId == wolf) break;
            Require(presses < 12, "Native target cycling did not offer entry to the first wolf.");
            _driver.Tap(Key.Q);
            await _driver.Delay(0.1);
        }
        Require(View.Progression.Hp == initialHp, "Approach changed the healthy player's HP.");
        SetStage("enter-wolf");
        await Submit(FarmSemanticAction.EnterEncounter, "key-E", () => _driver.Tap(Key.E));
        await _driver.Until(() => View.IsInEncounter && EncounterCount == 1, "Native entry did not start the wolf encounter.");
        EntityId episode = View.Encounter!.EncounterId;
        _engagement = new { EncounterCount, HelperDistance = HelperDistance(), InitialHp = initialHp, Episode = episode.ToString() };
        await Capture("entered");
        await WaitForWolfDefeat();
        await _driver.Until(() => View.Care?.Offers.Any(value => value.CanAcknowledge) == true
            && CanChoose(FarmSemanticAction.AcknowledgeCare), "The healthy-start journey produced no admitted helper care.");
        await ReadPanel("care-offered", wolf);
        string helper = View.Care!.Offers.First(value => value.CanAcknowledge).HelperName;
        SetStage("accept-care");
        await Submit(FarmSemanticAction.AcknowledgeCare, "key-E", () => _driver.Tap(Key.E));
        await _driver.Until(() => View.EncounterOutcome is null && View.IsInEncounter && View.Progression!.Hp > 0
            && !_game.HasPendingAction, "Patient acknowledgment was not followed by real treatment and continuation.");
        Require(View.Encounter!.EncounterId == episode && EncounterCount == 1, "Care replaced the encounter.");
        int healedHp = View.Progression!.Hp;
        _acceptedCare = new { Helper = helper, HelperActorId = _game.CompanionActorId.ToString(), PatientActorId = View.PlayerActorId.ToString(), Episode = episode.ToString(), InitialHp = initialHp, HealedHp = healedHp,
            Seconds = _watch.Elapsed.TotalSeconds };
        await _driver.Until(() => Hud.GetNode<Label>("%Message").Text == $"Health restored: +{healedHp} HP.",
            "The receipt notice stayed pending after observed restoration.");
        await CheckHud("treated", wolf);
        await WaitForWolfDefeat();
        Require(View.Care is null || View.Care.Offers.All(value => !value.CanAcknowledge),
            "The spent finite dose was offered again after the second defeat.");
        await ReadPanel("second-outcome", wolf);
        SetStage("accept-final-outcome");
        await Submit(FarmSemanticAction.ReleaseEncounter, "key-E", () => _driver.Tap(Key.E));
        await WaitForTerminalCompletion();
        await Terminal("terminal", wolf);
        SetStage("pause");
        _driver.Tap(Key.Escape);
        await _driver.Until(() => _game.IsLocallyPaused, "The treated journey could not pause for save.");
        _checks.Add("Native full-health approach and entry, two real defeats and separate finite helper care resume the same encounter.");
        await Capture("final");
    }

    private async Task WaitForWolfDefeat()
    {
        SetStage("wait-for-wolf");
        var watch = Stopwatch.StartNew();
        while (View.EncounterOutcome is null)
        {
            Require(watch.Elapsed.TotalSeconds < 120 && _waits < 100, "The wolf fight did not resolve within its bound.");
            await _driver.Until(() => View.EncounterOutcome is not null || CanChoose(FarmSemanticAction.Wait),
                "Neither a lawful Wait nor a resolved outcome arrived.", 30);
            if (View.EncounterOutcome is not null) break;
            ulong timing = View.Encounter!.TimingRevision;
            _waits++;
            await Submit(FarmSemanticAction.Wait, "key-G", () => _driver.Tap(Key.G));
            await _driver.Until(() => View.EncounterOutcome is not null || !_game.HasPendingAction
                && View.Encounter is { } encounter && encounter.TimingRevision != timing, "Wait did not reach its next boundary.", 30);
        }
        Require(View.EncounterOutcome is { Standing: LocalOutcomeStanding.OpponentStands,
            Restriction: LocalOutcomeRestriction.Incapacitated } && View.Progression!.Hp == 0,
            "The wolf did not cause the player's real incapacity.");
    }
}
