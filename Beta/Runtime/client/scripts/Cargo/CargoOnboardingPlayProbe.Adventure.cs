using Godot;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client.Cargo;

internal sealed partial class CargoOnboardingPlayProbe
{
    private async Task GatherAdventureSupplies()
    {
        foreach (GatherNodeKind kind in new[] { GatherNodeKind.Berry, GatherNodeKind.Oak })
        {
            LocalGatherNodeView node = View.GatheringNodes.Single(value => value.Kind == kind);
            _stage = "gather-" + kind;
            await WalkBeside(node.Feet);
            _driver.Tap(Key.R);
            int swings = kind == GatherNodeKind.Berry ? 2 : 1;
            for (int swing = 0; swing < swings; swing++)
            {
                await _driver.Until(() => _game.CurrentOffers.TryGetValue(FarmSemanticAction.Gather, out var offer)
                    && offer.CanExecute && offer.TargetId == node.NodeId && !_game.HasPendingAction,
                    "The actual nearby plant did not offer gathering.");
                uint remaining = View.GatheringNodes.Single(value => value.NodeId == node.NodeId).RemainingSwings;
                _driver.Tap(Key.F);
                await _driver.Until(() => !_game.HasPendingAction
                    && View.GatheringNodes.Single(value => value.NodeId == node.NodeId).RemainingSwings < remaining,
                    "Gather input did not spend the selected plant's finite yield.");
            }
            _checks.Add($"Ordinary F input gathered {kind} from its observed node {node.NodeId}.");
        }
        _stage = "craft-rations";
        _driver.Tap(Key.R);
        await _driver.Until(() => _game.CurrentOffers.TryGetValue(FarmSemanticAction.Craft, out var offer)
            && offer.CanExecute && !_game.HasPendingAction, "Gathered supplies did not permit ration crafting.");
        ulong revision = View.Crafting!.Revision;
        _driver.Tap(Key.C);
        await _driver.Until(() => !_game.HasPendingAction && View.Crafting!.Revision > revision,
            "Craft input did not produce a paid recipe transition.");
        _checks.Add("Ordinary C input crafted rations from owned finite supplies before funding Wren's project.");
        await Capture("adventure-supplies");
    }

    private async Task VerifyAdventureReopen()
    {
        _stage = "reopen";
        ulong balance = View.Progression!.CurrencyBalance;
        Require(balance > 0 && View.Construction.Any(project => project.IsCompleted),
            "The reopened adventure lost its earned reward or Wren's completed project.");
        var feet = Feet;
        await Capture("adventure-reopen");
        await _driver.Frame();
        await _driver.Frame();
        Require(_game.IsLocallyPaused && Feet == feet && View.Progression.CurrencyBalance == balance,
            "Paused reopening changed movement or the player's reward.");
        LocalHistoryVerification history = await _game.VerifyRecordedHistory().WaitAsync(TimeSpan.FromSeconds(30));
        Require(history.ReproducesHead, "The ordinary adventure failed its complete recorded-history verification.");
        _checks.Add("A separate paused opening retained the earned reward and completed construction; original-genesis history reproduces the head.");
    }
}
