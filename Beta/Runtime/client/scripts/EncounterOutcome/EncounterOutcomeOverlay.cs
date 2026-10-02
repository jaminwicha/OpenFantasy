using Godot;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;

namespace OpenFantasy.Client;

/// <summary>
/// Reading panel of the outcome-completion world. Its editable layout lives in <c>EncounterOutcomeOverlay.tscn</c>: at the
/// upper left of the field below the HUD header, growing only to its content so it stays above the encounter footer, and
/// clear of the target bar and the centred combatants. While an outcome awaits reading it carries the full explanation
/// the compact HUD status and hint leave out: each participant's side and the condition the observer may see, what
/// accepting does and when the world completes, or why the reading is unavailable, and the care view's reason; the
/// choices themselves stay on the production encounter controls (E / A, wheel and HUD buttons). After a completed defeat
/// it offers the one deliberate exit, Save and leave, which closes the session normally: the save is never reset, erased
/// or overwritten.
/// </summary>
public partial class EncounterOutcomeOverlay : CanvasLayer
{
    internal const string ScenePath = "res://scenes/EncounterOutcomeOverlay.tscn";

    private FarmGame? _game;
    private PanelContainer _panel = null!;
    private Label _title = null!;
    private Label _body = null!;
    private Button _leave = null!;

    internal bool IsShowingOutcome => _panel.Visible;
    internal PanelContainer Panel => _panel;
    internal Button LeaveButton => _leave;

    /// <summary>Instantiates the scene-owned layout for one game; the game is the only dependency.</summary>
    internal static EncounterOutcomeOverlay Create(FarmGame game)
    {
        var overlay = GD.Load<PackedScene>(ScenePath).Instantiate<EncounterOutcomeOverlay>();
        overlay._game = game;
        return overlay;
    }

    public override void _Ready()
    {
        _panel = GetNode<PanelContainer>("%Panel");
        _title = GetNode<Label>("%Title");
        _body = GetNode<Label>("%Body");
        _leave = GetNode<Button>("%SaveAndLeave");
        _leave.Pressed += () => _game?.RequestClose();
    }

    public override void _Process(double delta)
    {
        if (_game?.CurrentView is not { } view || view.EncounterOutcome is not { } outcome)
        {
            _panel.Visible = false;
            return;
        }
        bool terminal = outcome.IsTerminal;
        bool reading = outcome.Stage != LocalOutcomeStage.Completed;
        _panel.Visible = terminal || reading;
        if (!_panel.Visible) return;
        _title.Text = EncounterOutcomeText.Title(outcome);
        _body.Text = EncounterOutcomeText.Card(outcome, view.Care);
        if (_leave.Visible != terminal)
        {
            _leave.Visible = terminal;
            if (terminal) _leave.GrabFocus();
        }
    }
}
