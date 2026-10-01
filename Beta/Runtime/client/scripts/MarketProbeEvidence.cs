using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>
/// Shared steps and compact evidence for the disposable market probes. A public hash of the complete participant view
/// identifies each observation; only the facts a verifier or a timing diagnosis needs are copied beside it: step,
/// purse, actor positions and presence revisions, quotes, the player's lots and showing, the request and input
/// ownership. The retained save and journal stay the canonical record.
/// </summary>
internal static class MarketProbeEvidence
{
    /// <summary>The same hash the managed result tests recompute from the recorded world.</summary>
    internal static string PublicHash(LocalWorldView view) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(view))));

    internal static object Observe(string label, FarmGame game, LocalWorldView view, Viewport viewport)
    {
        LocalMarketStallView? stall = view.Markets?.Stalls.FirstOrDefault();
        return new
        {
            Label = label, Hash = PublicHash(view), Step = view.Markets?.AdvancingStep, Purse = view.Progression?.CurrencyBalance,
            Actors = view.Actors.Select(actor => new { Id = actor.ActorId.ToString(), actor.DisplayName, actor.Feet.X, actor.Feet.Y,
                actor.PresenceRevision }),
            Quotes = view.PaidQuotes.Select(quote => new { Id = quote.QuoteId.ToString(), Side = quote.Side.ToString(),
                Status = quote.Status.ToString(), quote.UnitPrice, quote.QuantityAllowance, quote.IssuedAtStep, quote.ExpiresAtStep,
                quote.ObservedIssuerRevision, quote.FilledQuantity }),
            Lots = stall?.OwnLots.Select(lot => new { Id = lot.LotId.ToString(), lot.Revision, lot.Quantity }),
            Showing = stall?.OwnShowing is { } shown ? new { Lot = shown.LotId.ToString(), shown.Units, shown.ExpiresAtStep, shown.IsLive } : null,
            InPerson = stall?.IsAvailableInPerson,
            Request = game.CurrentInquiry is { } inquiry ? new { State = inquiry.State.ToString(), inquiry.Inquiry.Quantity,
                Quote = inquiry.QuoteId?.ToString() } : null,
            Review = game.MarketDraft.Review is { } review ? new { Quote = review.Quote.QuoteId.ToString(), review.Units,
                review.Quote.ObservedIssuerRevision } : null,
            game.IsLocallyPaused, game.HasPendingAction, game.IsMarketOpen, game.IsWheelOpen,
            Focus = viewport.GuiGetFocusOwner()?.Name.ToString()
        };
    }

    /// <summary>Opens the stall from the wheel through the route: open, turn to the Market page, choose and confirm.</summary>
    internal static async Task OpenStall(MarketProbeDriver driver, FarmGame game, FarmQuickWheel wheel, FarmSemanticAction choice)
    {
        string route = driver.Route;
        if (route == "pointer") await driver.Click(game.GetNode<FarmHud>("FarmHud").GetNode<Button>("%OpenWheel"));
        else if (route == "controller") driver.Joy(JoyButton.RightStick);
        else driver.Tap(Key.V);
        await driver.Until(() => game.IsWheelOpen, "Wheel did not open.");
        MarketProbeDriver.Require(wheel.Model!.Selected is null, "Opening the wheel preselected a choice.");
        for (int page = 0; wheel.Model.PageName != "Market"; page++)
        {
            MarketProbeDriver.Require(page < 4, "The wheel has no Market page.");
            if (route == "pointer") await driver.Click(wheel.GetNode<Button>("%NextWheelPage"));
            else if (route == "controller") driver.Joy(JoyButton.RightShoulder);
            else driver.Tap(Key.Q);
            await driver.Frame();
        }
        int index = Array.FindIndex(wheel.Model.Slots.ToArray(), slot => slot?.Action == choice);
        MarketProbeDriver.Require(index >= 0, "Market sector absent.");
        if (route == "pointer") await driver.Point(wheel.GetNode<Control>("%Disc"), index);
        else if (route == "controller") driver.Joy(new[] { JoyButton.DpadUp, JoyButton.DpadRight, JoyButton.DpadDown, JoyButton.DpadLeft }[index]);
        else driver.Tap(new[] { Key.Up, Key.Right, Key.Down, Key.Left }[index]);
        await driver.Until(() => wheel.Model?.Selected?.Action == choice, "Market sector selection failed.");
        MarketProbeDriver.Require(wheel.Model!.Selected!.Offer.OpensPanel && wheel.Model.Selected.Offer.CanExecute,
            "The stall choice is unavailable.");
        await driver.Frame();
        if (route == "pointer") await driver.Click(wheel.GetNode<Button>("%ConfirmChoice"));
        else if (route == "controller") driver.Joy(JoyButton.A);
        else driver.Tap(Key.Enter);
        await driver.Until(() => game.IsMarketOpen && !game.IsWheelOpen, "The stall panel did not open from the wheel.");
        await driver.Frame();
    }
}
