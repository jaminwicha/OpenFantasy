using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client.Networking;

/// <summary>Ordinary home-character field keys; no selected-character permission or host shortcut.</summary>
internal sealed partial class NativeRosterPlayProbe
{
    private void RequireHomeField()
    {
        MarketProbeDriver.Require(Selected.Selection.ActorId == Selected.Selection.HomeActorId
            && !_game.IsInventoryOpen && !_game.IsWheelOpen && !ControlPanel.IsOpen && !_game.HasPendingAction,
            "Home field input requires the original character and closed panels.");
    }

    private async Task HomeTarget(EntityId target)
    {
        RequireHomeField();
        await Pause(false);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        _driver.Tap(Key.R);
        await _driver.Frame();
        for (int count = 0; _game.CurrentTarget?.Id != target; count++)
        {
            MarketProbeDriver.Require(count < 24 && watch.Elapsed.TotalSeconds < 5,
                "Native home-character target selection exceeded its bounded search.");
            _driver.Tap(Key.Q);
            await _driver.Frame();
        }
        MarketProbeDriver.Require(watch.Elapsed.TotalSeconds < 5, "Home target selection arrived too late.");
    }

    private async Task HomeAction(string name)
    {
        RequireHomeField();
        var (action, key) = name switch
        {
            "Speak" => (FarmSemanticAction.Speak, Key.E),
            "CloseDialogue" => (FarmSemanticAction.CloseDialogue, Key.E),
            "Give" => (FarmSemanticAction.Give, Key.G),
            "Gather" => (FarmSemanticAction.Gather, Key.F),
            "Craft" => (FarmSemanticAction.Craft, Key.C),
            _ => throw new InvalidDataException("Unsupported home-character field action.")
        };
        await Pause(false);
        await _driver.Until(() => !_game.HasPendingAction
            && _game.CurrentOffers.GetValueOrDefault(action) is { CanExecute: true } offer
            && offer.Scope.ExpectedActorRevision == View.Actors.Single(actor => actor.ActorId == View.PlayerActorId).PresenceRevision,
            "The actual home-character offer is unavailable: " + name);
        int before = _submissions.Count;
        _driver.Tap(key);
        await Applied(before);
        MarketProbeDriver.Require(_submissions[^1].Offer.Action == action,
            "The native field key submitted another action.");
        FarmActionOffer accepted = _submissions[^1].Offer;
        // A receipt can arrive before the observation carrying its effects. Do not start the
        // next action against the old node, craft or quest revision just because input is free.
        await _driver.Until(() => HomeEffectObserved(_game.CurrentSelection, accepted)
            && HomeEffectObserved(_game.PresentedSelection, accepted),
            "The accepted field action did not reach both the live and presented character.");
    }

    private static bool HomeEffectObserved(LocalSelectedPlayerObservation? observation, FarmActionOffer accepted)
    {
        if (observation is null || observation.Selection != accepted.Scope.Selection
            || observation.Selection.ActorId != observation.Selection.HomeActorId) return false;
        LocalWorldView view = observation.HomeView;
        return accepted.Intent switch
        {
            GatherIntent gather => view.GatheringNodes.Any(node => node.NodeId == gather.NodeId
                && node.Revision > gather.ExpectedNodeRevision),
            CraftIntent craft => view.Crafting?.Revision > craft.Request.ExpectedProductionRevision,
            DeliveryQuestIntent quest => view.Quests.Any(row => row.DefinitionId == quest.QuestId
                && row.ParticipationRevision > quest.ExpectedParticipationRevision),
            SpeakToActorIntent speak => view.Dialogue?.SpeakerActorId == speak.TargetActorId,
            CloseDialogueIntent => view.Dialogue is null,
            _ => false
        };
    }
}
