using Godot;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client.Networking;

internal sealed partial class NativeRosterPlayProbe
{
    private FarmHud Hud => _game.GetChildren().OfType<FarmHud>().Single();

    private async Task TryPendingResume()
    {
        MarketProbeDriver.Require(_game.ParticipantControl.State == PlayerControlState.Pending,
            "Early Resume proof requires actual pending control.");
        _driver.Tap(Key.Escape);
        await _driver.Delay(0.2);
        MarketProbeDriver.Require(_game.IsLocallyPaused && _game.ParticipantControl.State == PlayerControlState.Pending,
            "An early native Resume must neither unpause nor accept future control.");
    }

    private async Task FocusReturn()
    {
        _game.InjectFixtureFocus(false);
        await _driver.Frame();
        _game.InjectFixtureFocus(true);
        await _driver.Delay(0.2);
        MarketProbeDriver.Require(_game.IsLocallyPaused && _game.ParticipantControl.State == PlayerControlState.Restored,
            "Focus restoration must not acknowledge control return.");
    }

    private static void SetRight(bool pressed)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Right, PhysicalKeycode = Key.Right, Pressed = pressed });
        Input.FlushBufferedEvents();
    }

    private async Task CaptureControl()
    {
        await Capture(); // Includes the bounded post-draw boundary before measuring visible controls.
        Rect2 viewport = GetViewport().GetVisibleRect();
        Control header = Hud.GetNode<Control>("Root/Header");
        Control footer = Hud.GetNode<Control>("Root/Footer");
        Button pause = Hud.GetNode<Button>("%Pause");
        Label message = Hud.GetNode<Label>("%Message");
        foreach (Control control in new Control[] { header, footer, pause, message })
            MarketProbeDriver.Require(control.IsVisibleInTree() && viewport.Encloses(control.GetGlobalRect()),
                $"{control.Name} must be fully visible.");
        bool pending = _game.ParticipantControl.State == PlayerControlState.Pending;
        MarketProbeDriver.Require(pause.Disabled == pending, "Resume availability must follow actual control.");
        if (pending)
            MarketProbeDriver.Require(message.Text.Contains("Finishing your character's committed action", StringComparison.Ordinal),
                "Pending control must explain accepted work instead of ordinary pause.");
        else if (_game.ParticipantControl.State == PlayerControlState.Restored)
            MarketProbeDriver.Require(message.Text.Contains("Control returned", StringComparison.Ordinal) && _game.IsLocallyPaused,
                "Restored control must ask for deliberate Resume.");
        Rect2? sprite = _game.Canvas.ActorSpriteViewportBounds(View.PlayerActorId);
        MarketProbeDriver.Require(sprite is { } bounds && Hud.ShowsFieldSprite(bounds),
            "The returning player's full sprite must clear the HUD.");
    }
}
