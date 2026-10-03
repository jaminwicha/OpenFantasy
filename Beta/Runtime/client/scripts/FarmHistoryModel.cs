using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.Client;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

internal enum FarmHistoryState { Closed, Unsupported, Denied, Loading, Ready, Empty, Busy, Failed }
internal sealed record FarmHistoryCapture(ulong Opening, ulong PageGeneration);
internal sealed record FarmHistoryDetail(LocalPrivateHistoryEntry Entry, PaidQuoteView? PaidQuote,
    MaterialProposalView? MaterialProposal);

/// <summary>
/// One explicit history visit, page and typed detail. Main-thread updates observe replies only;
/// another page or inspection always requires a new user request. Closed work remains owned until observed.
/// </summary>
internal sealed class FarmHistoryModel
{
    private IPrivateHistoryPlayerSession? _session;
    private LocalSelectedPlayerObservation? _observation;
    private PendingRead? _pending;
    private ulong _opening;
    private ulong _pageGeneration;
    private ulong _rowGeneration;
    private bool _draining;

    internal bool IsOpen { get; private set; }
    internal bool IsBusy => _pending is not null;
    internal bool CanRefresh => IsOpen && _observation is not null && !IsBusy && !_draining;
    internal bool CanNext => CanRefresh && Page?.Next is not null;
    internal bool CanInspect => CanRefresh && SelectedEntry is not null;
    internal FarmHistoryState State { get; private set; }
    internal string? Notice { get; private set; }
    internal string ActorName => Selection is { } selection ? Name(selection.ActorId) : "Selected character";
    internal LocalActorSelection? Selection => _observation?.Selection;
    internal FarmHistoryCapture Capture => new(_opening, _pageGeneration);
    internal LocalPrivateHistoryPage? Page { get; private set; }
    internal IReadOnlyList<LocalPrivateHistoryEntry> Entries => Page?.Entries ?? [];
    internal LocalPrivateHistoryEntry? SelectedEntry { get; private set; }
    internal FarmHistoryDetail? Detail { get; private set; }

    internal void Open(IPrivateHistoryPlayerSession? session)
    {
        Close();
        ObserveCompleted();
        if (_draining) return;
        IsOpen = true;
        _session = session;
        if (session is null || !session.SupportsPrivateHistoryDirectory)
        {
            State = FarmHistoryState.Unsupported;
            Notice = "History discovery is unavailable in this session.";
            return;
        }
        try
        {
            var observation = session.ReadPrivateHistoryObservation();
            if (!PermitsHistory(observation))
            {
                State = FarmHistoryState.Denied;
                Notice = "This character requires separate Private History permission.";
                return;
            }
            _observation = observation;
            if (IsBusy)
            {
                State = FarmHistoryState.Busy;
                Notice = "A previous history request owns the query. Choose Latest when it finishes.";
                return;
            }
            StartPage(new());
        }
        catch
        {
            _observation = null;
            State = FarmHistoryState.Failed;
            Notice = "History is unavailable. Close and reopen to try again.";
        }
    }

    internal void Update()
    {
        if (IsOpen && _observation is not null) RefreshObservation();
        ObserveCompleted();
    }

    internal bool Refresh(FarmHistoryCapture capture)
    {
        if (!Matches(capture) || !RefreshObservation() || !CanRefresh) return false;
        StartPage(new());
        return true;
    }

    internal bool Next(FarmHistoryCapture capture)
    {
        if (!Matches(capture) || !RefreshObservation() || !CanNext || Page?.Next is not { } next) return false;
        StartPage(new(next));
        return true;
    }

    internal bool Select(FarmHistoryCapture capture, LocalPrivateHistoryKind kind, EntityId subject)
    {
        if (!Matches(capture) || !RefreshObservation() || Page is null) return false;
        LocalPrivateHistoryEntry? selected = Entries.FirstOrDefault(entry => entry.Kind == kind && entry.SubjectId == subject);
        if (selected is null) return false;
        if (selected != SelectedEntry)
        {
            _rowGeneration = checked(_rowGeneration + 1);
            SelectedEntry = selected;
            Detail = null;
            State = FarmHistoryState.Ready;
            Notice = "Select Inspect to read this exact saved record.";
        }
        return true;
    }

    internal bool Inspect(FarmHistoryCapture capture, LocalPrivateHistoryKind kind, EntityId subject)
    {
        if (!Matches(capture) || !RefreshObservation() || !CanInspect
            || SelectedEntry is not { } entry || entry.Kind != kind || entry.SubjectId != subject) return false;
        Detail = null;
        _rowGeneration = checked(_rowGeneration + 1);
        var context = CaptureRead(null, entry);
        Task<Reply> task = ReadDetailAsync(_session!, context.Observation.Selection, entry);
        _pending = new(context, task);
        State = FarmHistoryState.Loading;
        Notice = "Reading saved terms…";
        return true;
    }

    internal void Close(string? notice = null)
    {
        _opening = checked(_opening + 1);
        _pageGeneration = checked(_pageGeneration + 1);
        _rowGeneration = checked(_rowGeneration + 1);
        IsOpen = false;
        _session = null;
        _observation = null;
        Page = null;
        SelectedEntry = null;
        Detail = null;
        State = FarmHistoryState.Closed;
        Notice = notice;
    }

    internal async Task DrainAsync()
    {
        _draining = true;
        Close();
        PendingRead? pending = _pending;
        if (pending is null) return;
        try { await pending.Task.ConfigureAwait(false); }
        catch { if (pending.Task.IsFaulted) _ = pending.Task.Exception; }
        if (ReferenceEquals(_pending, pending)) _pending = null;
    }

    internal string Name(EntityId actor)
    {
        var actors = _observation?.ControlledView is { } controlled ? controlled.Actors : _observation?.HomeView.Actors;
        return actors?.FirstOrDefault(value => value.ActorId == actor)?.DisplayName ?? $"Person {actor}";
    }

    internal string ItemName(EntityId definition)
    {
        var descriptions = _observation?.ControlledView is { } controlled
            ? controlled.MaterialDescriptions : _observation?.HomeView.MaterialDescriptions;
        return descriptions?.Find(definition)?.DisplayName ?? "Unidentified material";
    }

    private void StartPage(LocalPrivateHistoryPageRequest request)
    {
        _pageGeneration = checked(_pageGeneration + 1);
        _rowGeneration = checked(_rowGeneration + 1);
        Page = null;
        SelectedEntry = null;
        Detail = null;
        var context = CaptureRead(request, null);
        _pending = new(context, ReadPageAsync(_session!, context.Observation.Selection, request));
        State = FarmHistoryState.Loading;
        Notice = "Reading saved history…";
    }

    private ReadContext CaptureRead(LocalPrivateHistoryPageRequest? request, LocalPrivateHistoryEntry? entry) =>
        new(Capture, _rowGeneration, _observation!, request, entry);

    private bool Matches(FarmHistoryCapture capture) => IsOpen && capture == Capture;

    private bool RefreshObservation()
    {
        if (!IsOpen || _session is null || _observation is null) return false;
        try
        {
            if (!_session.SupportsPrivateHistoryDirectory)
            {
                Close("History is no longer available in this session.");
                return false;
            }
            var current = _session.ReadPrivateHistoryObservation();
            if (!SameSelection(_observation, current) || !PermitsHistory(current))
            {
                Close("The selected character or history permission changed. Open History again.");
                return false;
            }
            _observation = current;
            return true;
        }
        catch
        {
            Close("History is no longer available. Open History again when the session is ready.");
            return false;
        }
    }

    private void ObserveCompleted()
    {
        if (_pending is not { Task.IsCompleted: true } pending) return;
        _pending = null;
        Reply? reply = null;
        Exception? failure = null;
        try { reply = pending.Task.GetAwaiter().GetResult(); }
        catch (Exception error) { failure = error; }
        if (!Owns(pending.Context)) return;
        if (failure is not null)
        {
            State = failure is PlayerRequestRefusedException { Refusal: PlayerRequestRefusal.Busy }
                ? FarmHistoryState.Busy : FarmHistoryState.Failed;
            Notice = State == FarmHistoryState.Busy
                ? "The session is handling another request. Try this history choice again when ready."
                : "History could not be read. Choose Latest or Inspect to try again.";
            return;
        }
        try { Apply(pending.Context, reply!); }
        catch
        {
            Detail = null;
            State = FarmHistoryState.Failed;
            Notice = "The reply does not match the displayed history request. Choose Latest to try again.";
        }
    }

    private bool Owns(ReadContext context) => Matches(context.Capture) && _observation is not null
        && SameSelection(context.Observation, _observation) && PermitsHistory(_observation)
        && (context.Entry is null || context.RowGeneration == _rowGeneration && context.Entry == SelectedEntry);

    private void Apply(ReadContext context, Reply reply)
    {
        if (reply is PageReply page && context.Request is { } request)
        {
            page.Result.ValidateFor(_observation!, LocalSelectedQueryOperation.ReadPrivateHistory);
            page.Result.Value.ValidateFor(request);
            if (page.Result.Value.Entries.Any(entry => entry.CounterpartyActorId == _observation!.Selection.ActorId))
                throw new InvalidDataException("A participant cannot be their own history counterparty.");
            Page = page.Result.Value;
            State = Page.Entries.Count == 0 ? FarmHistoryState.Empty : FarmHistoryState.Ready;
            Notice = Page.Entries.Count == 0 ? "No completed exchanges or gifts are recorded in this part of history."
                : "Choose a record, then Inspect its saved terms.";
            return;
        }
        if (context.Entry is not { } entry) throw new InvalidDataException("Missing history row.");
        if (reply is QuoteReply quote)
        {
            quote.Result.ValidateFor(_observation!, LocalSelectedQueryOperation.InspectPaidQuoteHistory, entry.SubjectId);
            if (quote.Result.Value is { } value)
            {
                ValidateDetail(entry, value, _observation!.Selection.ActorId);
                Detail = new(entry, value, null);
            }
        }
        else if (reply is ProposalReply proposal)
        {
            proposal.Result.ValidateFor(_observation!, LocalSelectedQueryOperation.InspectMaterialProposalHistory, entry.SubjectId);
            if (proposal.Result.Value is { } value)
            {
                ValidateDetail(entry, value, _observation!.Selection.ActorId);
                Detail = new(entry, null, value);
            }
        }
        else throw new InvalidDataException("The history reply has the wrong kind.");
        State = FarmHistoryState.Ready;
        Notice = Detail is null ? "This recorded subject is unavailable to this character." : "Saved terms; viewing changes nothing.";
    }

    private static void ValidateDetail(LocalPrivateHistoryEntry entry, PaidQuoteView quote, EntityId actor)
    {
        bool issuer = quote.IssuerActorId == actor;
        if (entry.Kind != LocalPrivateHistoryKind.PaidQuote || quote.QuoteId != entry.SubjectId
            || quote.Status == PaidQuoteStatus.Open || quote.Status != entry.PaidQuoteStatus
            || quote.ClosedAtSequence != entry.ClosedAtSequence || quote.ObservedIssuerRevision is not null
            || !issuer && quote.CounterpartyActorId != actor || quote.ViewerIsIssuer != issuer
            || (issuer ? quote.CounterpartyActorId : quote.IssuerActorId) != entry.CounterpartyActorId)
            throw new InvalidDataException("The quote does not match the displayed terminal history row.");
    }

    private static void ValidateDetail(LocalPrivateHistoryEntry entry, MaterialProposalView proposal, EntityId actor)
    {
        bool sender = proposal.Terms.SenderActorId == actor;
        if (entry.Kind != LocalPrivateHistoryKind.MaterialProposal || proposal.ProposalId != entry.SubjectId
            || proposal.Status == MaterialProposalStatus.Open || proposal.Status != entry.MaterialProposalStatus
            || proposal.ClosedAtSequence != entry.ClosedAtSequence || proposal.SenderPresenceRevision is not null
            || !sender && proposal.Terms.RecipientActorId != actor || proposal.ViewerIsSender != sender
            || (sender ? proposal.Terms.RecipientActorId : proposal.Terms.SenderActorId) != entry.CounterpartyActorId)
            throw new InvalidDataException("The proposal does not match the displayed terminal history row.");
    }

    private static bool SameSelection(LocalSelectedPlayerObservation left, LocalSelectedPlayerObservation right) =>
        left.Selection == right.Selection && left.HomeView.WorldId == right.HomeView.WorldId
        && left.HomeView.BranchId == right.HomeView.BranchId;

    private static bool PermitsHistory(LocalSelectedPlayerObservation observation)
    {
        var selection = observation.Selection;
        if (selection.HomeActorId != observation.HomeView.PlayerActorId) return false;
        if (selection.ControlWitness is null)
            return selection.ActorId == selection.HomeActorId && observation.ControlledView is null;
        var witness = selection.ControlWitness;
        return witness.OwnerActorId == selection.ActorId && witness.GranteeActorId == selection.HomeActorId
            && (witness.Capabilities & WorldControlCapabilities.PrivateHistory) != 0
            && observation.ControlledView is { } controlled && controlled.ActorId == selection.ActorId
            && controlled.WorldId == observation.HomeView.WorldId && controlled.BranchId == observation.HomeView.BranchId
            && (controlled.Capabilities & WorldControlCapabilities.PrivateHistory) != 0;
    }

    private static async Task<Reply> ReadPageAsync(IPrivateHistoryPlayerSession session,
        LocalActorSelection selection, LocalPrivateHistoryPageRequest request) =>
        new PageReply(await session.ReadSelectedPrivateHistoryAsync(selection, request).ConfigureAwait(false));

    private static async Task<Reply> ReadDetailAsync(IPrivateHistoryPlayerSession session,
        LocalActorSelection selection, LocalPrivateHistoryEntry entry) => entry.Kind switch
        {
            LocalPrivateHistoryKind.PaidQuote => new QuoteReply(await session.InspectSelectedPaidQuoteHistoryAsync(selection, entry.SubjectId).ConfigureAwait(false)),
            LocalPrivateHistoryKind.MaterialProposal => new ProposalReply(await session.InspectSelectedMaterialProposalHistoryAsync(selection, entry.SubjectId).ConfigureAwait(false)),
            _ => throw new ArgumentException("Unknown history record kind.", nameof(entry))
        };

    private sealed record ReadContext(FarmHistoryCapture Capture, ulong RowGeneration,
        LocalSelectedPlayerObservation Observation, LocalPrivateHistoryPageRequest? Request, LocalPrivateHistoryEntry? Entry);
    private sealed record PendingRead(ReadContext Context, Task<Reply> Task);
    private abstract record Reply;
    private sealed record PageReply(LocalSelectedQueryResult<LocalPrivateHistoryPage> Result) : Reply;
    private sealed record QuoteReply(LocalSelectedQueryResult<PaidQuoteView?> Result) : Reply;
    private sealed record ProposalReply(LocalSelectedQueryResult<MaterialProposalView?> Result) : Reply;
}
