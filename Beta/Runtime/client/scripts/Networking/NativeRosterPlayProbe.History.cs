using Godot;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client.Networking;

/// <summary>History discovery and inspection through actual visible controls, without remembered subject input.</summary>
internal sealed partial class NativeRosterPlayProbe
{
    private Node HistoryPanel => _game.GetNode("FarmHistoryPanel");
    private readonly List<object> _historyCaptures = [];
    private ulong _historyOperationStarted;
    private double HistorySecondsRemaining => 5 - (Time.GetTicksMsec() - _historyOperationStarted) / 1000.0;
    private Task HistoryUntil(Func<bool> predicate, string failure) => _driver.Until(predicate, failure, HistorySecondsRemaining);

    private async Task HistoryOperation(Func<Task> action)
    {
        _historyOperationStarted = Time.GetTicksMsec();
        int before = _submissions.Count;
        try
        {
            await action();
            MarketProbeDriver.Require(HistorySecondsRemaining > 0, "History input exceeded its unchanged five-second limit.");
            MarketProbeDriver.Require(_submissions.Count == before && !_game.HasPendingAction,
                "Read-only History browsing submitted a gameplay command.");
        }
        finally
        {
            WriteAtomic(Path.Combine(_directory, $"{_sequence:D4}.history.json"), new
            {
                Sequence = _sequence, Stage = _stage, Submissions = _submissions.Count,
                History = HistoryEvidence(), Captures = _historyCaptures
            });
        }
    }

    private async Task OpenHistoryLibrary(bool controller)
    {
        MarketProbeDriver.Require(!_game.IsWheelOpen && !_game.IsInventoryOpen && !_game.IsMarketOpen
            && !_game.IsRecipesOpen && !_game.IsCareOpen && !_game.HasPendingAction,
            "Close other inspections before opening History.");
        var selection = Selected.Selection;
        if (!_game.IsHistoryOpen)
        {
            if (controller) _driver.Joy(JoyButton.LeftStick);
            else _driver.Tap(Key.H);
        }
        await HistoryUntil(() => _game.IsHistoryOpen && !_game.History.IsBusy && _game.History.Page is not null,
            "The actual History entry point did not settle its explicit first page.");
        MarketProbeDriver.Require(_game.History.Selection == selection && Selected.Selection == selection,
            "History discovery changed its selected participant.");
    }

    private async Task CloseHistoryLibrary()
    {
        if (_game.IsHistoryOpen) await _driver.Click(HistoryPanel.GetNode<Button>("%CloseHistory"));
        await HistoryUntil(() => !_game.IsHistoryOpen && _game.History.Page is null && _game.History.Detail is null
            && _game.History.SelectedEntry is null, "Closing History retained private rows or inspected terms.");
    }

    private async Task SelectHistoryRow()
    {
        MarketProbeDriver.Require(_game.IsHistoryOpen && !_game.History.IsBusy && _game.History.Entries.Count > 0,
            "Discover a settled nonempty History page before choosing its first actual row.");
        var selection = Selected.Selection;
        var entry = _game.History.Entries[0];
        var row = Descendants(HistoryPanel).OfType<Button>().Single(button => button.HasMeta("history_kind")
            && button.HasMeta("history_id") && button.GetMeta("history_kind").AsString() == entry.Kind.ToString()
            && button.GetMeta("history_id").AsString() == entry.SubjectId.ToString());
        await _driver.Click(row);
        await HistoryUntil(() => _game.History.SelectedEntry == entry && _game.History.CanInspect,
            "The actual History row did not retain its discovered kind and identity.");
        MarketProbeDriver.Require(_game.History.Detail is null && Selected.Selection == selection,
            "Selecting a History row implicitly inspected it or changed actor scope.");
    }

    private async Task InspectHistoryRow(bool controller)
    {
        var entry = _game.History.SelectedEntry ?? throw new InvalidOperationException("Select a discovered History row first.");
        var selection = Selected.Selection;
        MarketProbeDriver.Require(_game.IsHistoryOpen && _game.History.CanInspect, "The selected History row is not inspectable.");
        var inspect = HistoryPanel.GetNode<Button>("%InspectHistory");
        if (controller)
        {
            for (int moves = 0; GetViewport().GuiGetFocusOwner() != inspect; moves++)
            {
                Control? previous = GetViewport().GuiGetFocusOwner();
                MarketProbeDriver.Require(previous is not null && moves < 16,
                    "Controller focus did not reach the actual History Inspect control.");
                _driver.Joy(JoyButton.DpadRight);
                await _driver.Frame();
                MarketProbeDriver.Require(GetViewport().GuiGetFocusOwner() != previous,
                    "The History focus chain did not advance with controller Right.");
            }
            MarketProbeDriver.Require(!inspect.Disabled && GetViewport().GuiGetFocusOwner() == inspect,
                "Controller inspection requires the enabled exact Inspect control to own focus.");
            _driver.Joy(JoyButton.A);
        }
        else await _driver.Click(inspect);
        await HistoryUntil(() => !_game.History.IsBusy && _game.History.Detail?.Entry == entry,
            "The actual Inspect button did not return the exact discovered terminal record.");
        var detail = _game.History.Detail!;
        MarketProbeDriver.Require(Selected.Selection == selection && _game.History.Selection == selection
            && (entry.Kind == LocalPrivateHistoryKind.MaterialProposal
                ? detail.MaterialProposal is { } proposal && proposal.ProposalId == entry.SubjectId
                    && proposal.ClosedAtSequence == entry.ClosedAtSequence && proposal.Status == entry.MaterialProposalStatus
                : detail.PaidQuote is { } quote && quote.QuoteId == entry.SubjectId
                    && quote.ClosedAtSequence == entry.ClosedAtSequence && quote.Status == entry.PaidQuoteStatus),
            "The historical detail does not agree with its typed discovered record.");
        await HoldHistoryRead();
    }

    private async Task HoldHistoryRead()
    {
        var selection = _game.History.Selection;
        var entry = _game.History.SelectedEntry;
        var detail = _game.History.Detail;
        bool drawn = DisplayServer.GetName() == "headless";
        void OnDraw() => drawn = true;
        RenderingServer.FramePostDraw += OnDraw;
        try { await HistoryUntil(() => drawn, "The inspected History terms did not cross the actual draw boundary."); }
        finally { RenderingServer.FramePostDraw -= OnDraw; }
        await _driver.Delay(0.25);
        MarketProbeDriver.Require(_game.IsHistoryOpen && _game.History.Selection == selection
            && _game.History.SelectedEntry == entry && _game.History.Detail == detail && !_game.History.IsBusy,
            "History scope or terms changed during the held post-draw reading interval.");
    }

    private async Task CaptureHistory(string name)
    {
        MarketProbeDriver.Require(name.Length is > 0 and <= 64 && _historyCaptures.Count < 16,
            "History capture names and count are bounded.");
        MarketProbeDriver.Require(_game.IsHistoryOpen, "Open History before capturing its visible controls.");
        await HoldHistoryRead();
        string suffix = "-history-" + name;
        await Capture(HistorySecondsRemaining, suffix);
        var viewport = GetViewport().GetVisibleRect();
        var controls = Descendants(HistoryPanel).OfType<Control>().Where(control => control.IsVisibleInTree()
            && (control is Button || control is Label && control.Name.ToString() is "HistoryTitle" or "HistoryNotice" or "HistoryDetails"))
            .ToArray();
        foreach (Control control in controls)
        {
            Rect2 bounds = control.GetGlobalRect();
            MarketProbeDriver.Require(bounds.Size.X > 0 && bounds.Size.Y > 0 && viewport.Encloses(bounds),
                "A visible History control lies outside the actual viewport: " + control.Name);
            for (Node? ancestor = control.GetParent(); ancestor is not null; ancestor = ancestor.GetParent())
                if (ancestor is ScrollContainer scroll)
                    MarketProbeDriver.Require(scroll.GetGlobalRect().Encloses(bounds), "A History control is clipped by its scroll container.");
            if (control is Label label)
                MarketProbeDriver.Require(label.GetLineCount() == label.GetVisibleLineCount(), "A History explanation has clipped lines.");
        }
        _historyCaptures.Add(new
        {
            Sequence = _sequence, Name = name, Image = $"{_sequence:D4}{suffix}.png", History = HistoryEvidence(),
            Viewport = Bounds(viewport), Controls = controls.Select(control => new
            {
                Name = control.Name.ToString(), Bounds = Bounds(control.GetGlobalRect()),
                Text = control is Label label ? label.Text : control is Button button ? button.Text : null,
                Disabled = control is Button action ? action.Disabled : (bool?)null,
                Kind = control.HasMeta("history_kind") ? control.GetMeta("history_kind").AsString() : null,
                Subject = control.HasMeta("history_id") ? control.GetMeta("history_id").AsString() : null
            }).ToArray()
        });
    }

    private async Task ObserveHistoryClosed()
    {
        await HistoryUntil(() => !_game.IsHistoryOpen && _game.History.Page is null && _game.History.Detail is null
            && _game.History.SelectedEntry is null, "Invalidated History still exposes another participant's private record.");
    }

    private object HistoryEvidence()
    {
        var history = _game.History;
        var observed = _game.CurrentSelection;
        var selection = history.Selection;
        return new
        {
            Open = _game.IsHistoryOpen, history.IsBusy, State = history.State.ToString(), history.Notice,
            history.ActorName, Actor = selection?.ActorId.ToString(), Port = selection?.PortId.ToString(),
            Generation = selection?.Generation, Capabilities = selection?.ControlWitness?.Capabilities.ToString(),
            InventoryDisclosed = observed?.ControlledView is null || observed.ControlledView.Inventory is not null,
            TradeDisclosed = observed?.ControlledView is null || observed.ControlledView.Trade is not null,
            ThroughRevision = history.Page?.ThroughRevision,
            Before = history.Page?.Request.Before, Next = history.Page?.Next,
            Entries = history.Entries.Select(HistoryEntryEvidence).ToArray(),
            Selected = history.SelectedEntry is { } entry ? HistoryEntryEvidence(entry) : null,
            Detail = history.Detail is { } detail ? new
            {
                Entry = HistoryEntryEvidence(detail.Entry),
                Proposal = detail.MaterialProposal is { } proposal ? new
                {
                    Subject = proposal.ProposalId.ToString(), Status = proposal.Status.ToString(), proposal.ClosedAtSequence,
                    proposal.ExpiresAtAdvancingStep, GrantHash = proposal.GrantHash.ToString(), Terms = GiftTerms(proposal.Terms)
                } : null,
                Quote = detail.PaidQuote is { } quote ? new
                {
                    Subject = quote.QuoteId.ToString(), Status = quote.Status.ToString(), quote.ClosedAtSequence,
                    Lot = quote.LotId.ToString(), quote.UnitPrice, quote.QuantityAllowance
                } : null
            } : null
        };
    }

    private static object HistoryEntryEvidence(LocalPrivateHistoryEntry entry) => new
    {
        Kind = entry.Kind.ToString(), Subject = entry.SubjectId.ToString(), Counterparty = entry.CounterpartyActorId.ToString(),
        entry.ClosedAtSequence, PaidQuoteStatus = entry.PaidQuoteStatus?.ToString(), MaterialProposalStatus = entry.MaterialProposalStatus?.ToString()
    };
}
