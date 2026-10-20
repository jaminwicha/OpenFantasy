using Godot;

namespace OpenFantasy.Client;

public partial class FarmGame
{
    private FarmJournalPanel _journalPanel = null!;

    internal bool IsJournalOpen => _input.JournalOpen;
    internal FarmJournalPanel JournalPanel => _journalPanel;

    private void ReadyJournal()
    {
        _journalPanel = GD.Load<PackedScene>("res://scenes/FarmJournalPanel.tscn").Instantiate<FarmJournalPanel>();
        AddChild(_journalPanel);
        _journalPanel.CloseRequested += () => Perform(FarmSemanticAction.CloseJournal);
    }

    private void OpenJournal()
    {
        if (_session is null || !_input.Permits(Situation(), FarmSemanticAction.OpenJournal)) return;
        _input.SetJournalOpen(true);
        _journalPanel.Open(JournalPage.From(PresentedHomeView ?? _session.World.ReadView()));
    }

    private void CloseJournal()
    {
        if (!_input.JournalOpen) return;
        _input.SetJournalOpen(false);
        _journalPanel.Close();
    }

    /// <summary>The open journal follows the world as it changes; it never reads beyond the permitted view.</summary>
    private void PresentJournal()
    {
        if (_session is not null && _input.JournalOpen) _journalPanel.Present(JournalPage.From(PresentedHomeView ?? _session.World.ReadView()));
    }
}
