using Godot;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

public partial class FarmGame
{
    private FarmCarePanel _carePanel = null!;
    private readonly FarmCareLibraryModel _careLibrary = new();
    private Task<WorldActionReceipt>? _careSubmission;
    private FarmActionScope? _careSubmissionScope;
    private ulong _careSubmissionLifetime;
    internal bool IsCareOpen => _input.CareOpen;
    internal FarmCareLibraryModel SelectedCareLibrary => _careLibrary;

    private void ReadyCare()
    {
        _carePanel = GD.Load<PackedScene>("res://scenes/FarmCarePanel.tscn").Instantiate<FarmCarePanel>();
        AddChild(_carePanel);
        _carePanel.ActionRequested += Perform;
        _carePanel.SelectionRequested += (identity, shownScope, lifetime) =>
        {
            if (!_input.Permits(Situation(), FarmSemanticAction.PrepareCare)
                || lifetime != _carePanel.PresentationLifetime || shownScope is null) return;
            if (RefreshCareChoice() && _careLibrary.Scope is { } current
                && FarmCareLibraryModel.SameLifetime(shownScope, current) && _careLibrary.Select(identity)) PresentCarePanel();
        };
    }

    private void OpenCare()
    {
        if (_session is null) return;
        try
        {
            var observation = _session.ReadSelection();
            ObserveActorSelection(observation);
            ObserveControlReturn(observation?.ControlStatus ?? ParticipantControl);
            if (!_input.Permits(Situation(), FarmSemanticAction.OpenCare)) return;
            _session.RefreshCare(_careLibrary, observation);
        }
        catch (InvalidOperationException error) { _careLibrary.Clear(); _hud.Message(error.Message); return; }
        if (_careLibrary.Entries.Count == 0) { _hud.Message("No ordinary care choices are available for this character."); return; }
        _careLibrary.DiscardReview();
        _input.SetCareOpen(true);
        _carePanel.Open(_careLibrary);
        _hud.SetCovered(true);
        try { SendMovement(0, 0); }
        catch (InvalidOperationException error) { CloseCare(); _hud.Message(error.Message); return; }
        PresentCarePanel();
        _carePanel.FocusSelection();
    }

    private void CloseCare()
    {
        _input.CaptureBoundary(Situation(), ReadWalkingInput());
        _input.SetCareOpen(false);
        _careLibrary.DiscardReview();
        _hud.SetCovered(false);
        _carePanel.Close();
    }

    private void RefreshCare(LocalSelectedPlayerObservation? observation)
    {
        if (!_input.CareOpen || _session is null) return;
        _session.RefreshCare(_careLibrary, observation);
        if (_careLibrary.Entries.Count == 0) { CloseCare(); return; }
        PresentCarePanel();
    }

    private void PresentCarePanel()
    {
        if (_session is null || !_input.CareOpen) return;
        _carePanel.Present(_careLibrary, _session.World.ReadRunState(), _session.IsRemote, _input.Paused,
            _input.Permits(Situation(), FarmSemanticAction.PrepareCare),
            _input.Permits(Situation(), FarmSemanticAction.ConfirmCare));
    }

    private void PrepareCare()
    {
        if (!_input.Permits(Situation(), FarmSemanticAction.PrepareCare)) return;
        // Review the displayed choice first. A newer observation may invalidate it, never replace it.
        _careLibrary.Prepare();
        if (RefreshCareChoice()) PresentCarePanel();
    }

    private void ConfirmCare()
    {
        var prepared = _careLibrary.PreparedOffer;
        if (!RefreshCareChoice() || !_input.Permits(Situation(), FarmSemanticAction.ConfirmCare)
            || prepared is null || !ReferenceEquals(prepared, _careLibrary.PreparedOffer)) return;
        _careLibrary.DiscardReview();
        Submit(prepared);
        if (_action is not null)
        {
            _careSubmission = _action;
            _careSubmissionScope = prepared.Scope;
            _careSubmissionLifetime = _carePanel.PresentationLifetime;
        }
        _careLibrary.DiscardReview(_action is null ? "The choice could not be submitted. Review it again."
            : "The reviewed choice was submitted. Waiting for its result…");
        PresentCarePanel();
    }

    private void CompleteCareAction(Task<WorldActionReceipt> action, string outcome)
    {
        if (!ReferenceEquals(action, _careSubmission)) return;
        _careSubmission = null;
        if (_input.CareOpen && _careSubmissionLifetime == _carePanel.PresentationLifetime
            && _careSubmissionScope is { } captured && _careLibrary.Scope is { } current
            && FarmCareLibraryModel.SameLifetime(captured, current)) _careLibrary.DiscardReview(outcome);
        _careSubmissionScope = null;
    }

    private bool RefreshCareChoice()
    {
        if (_session is null || !_input.CareOpen) return false;
        try
        {
            var observation = _session.ReadSelection();
            ObserveActorSelection(observation);
            ObserveControlReturn(observation?.ControlStatus ?? ParticipantControl);
            RefreshCare(observation);
            return _input.CareOpen;
        }
        catch (InvalidOperationException error) { InterruptCareReview("Care observation unavailable. " + error.Message); return false; }
    }

    private void InterruptCareReview(string reason)
    {
        _careLibrary.DiscardReview(reason);
        PresentCarePanel();
    }
}
