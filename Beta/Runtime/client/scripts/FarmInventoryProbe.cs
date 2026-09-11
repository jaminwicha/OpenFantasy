using System.Diagnostics;
using System.Text.Json;
using Godot;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Checks the real pack against already gathered/crafted inventory in the native farm journey.</summary>
internal sealed class FarmInventoryProbe(FarmGame game, Node clock, string reportPath)
{
    private readonly List<string> _checks = [];
    private FarmInventoryScreen _screen = null!;
    private FarmHud _hud = null!;

    internal async Task<IReadOnlyList<string>> RunAsync()
    {
        _screen = game.GetChildren().OfType<FarmInventoryScreen>().Single();
        _hud = game.GetChildren().OfType<FarmHud>().Single();
        Tap(Key.Escape);
        await Until(() => game.IsLocallyPaused, "Pack comparison could not pause.");
        await Delay(0.2);
        string before = JsonSerializer.Serialize(View());
        _hud.GetNode<Button>("%OpenPack").GrabFocus();
        Tap(Key.I);
        await Until(() => game.IsInventoryOpen && _screen.IsOpen, "I did not open the pack.");
        await Delay(0.1);
        Require(_screen.GetNode<Label>("%PackTime").Text.StartsWith("World paused", StringComparison.Ordinal), "The pack misreported host pause.");
        Button[] items = Items();
        Require(items.Length == View().PlayerInventory.Lots.Where(lot => lot.Quantity > 0)
                .Select(lot => (lot.DefinitionId, lot.Provenance, lot.AvailableForDelivery)).Distinct().Count()
            && items.Any(button => button.Text.StartsWith("Rations", StringComparison.Ordinal))
            && items.Any(button => button.Text.StartsWith("Wood offcuts", StringComparison.Ordinal)),
            "The pack did not expose exactly its actual nonempty owned item groups and crafted outputs.");
        Require(_screen.GetNode<Label>("%PackSummary").Text.Contains($"{View().PlayerInventory.Quantity} units", StringComparison.Ordinal),
            "The pack total differs from the public inventory.");
        Capture("pack-paused");
        Button first = items.Single(button => button.HasFocus());
        for (int step = 0; step <= items.Length; step++)
        {
            Tap(Key.Tab);
            await Delay(0.03);
            Control focused = clock.GetViewport().GuiGetFocusOwner();
            Require(items.Any(button => button == focused) || focused == _screen.GetNode<Button>("%ClosePack"), "Tab escaped the pack's focus chain.");
        }
        Require(first.HasFocus(), "The pack focus chain did not wrap back to its first item.");
        Tap(Key.Down);
        await Delay(0.1);
        Button second = items.Single(button => button.HasFocus());
        Require(first != second, "Discrete keyboard navigation did not advance pack focus.");
        Require(_screen.GetNode<Label>("%ItemName").Text == second.Text.Split('\n')[0], "Focused details describe another item.");
        Shoulder(JoyButton.DpadDown);
        await Delay(0.1);
        Require(items.Single(button => button.HasFocus()) != second, "Discrete controller navigation did not advance pack focus.");
        Shoulder(JoyButton.A);
        Button rations = items.Single(button => button.Text.StartsWith("Rations", StringComparison.Ordinal));
        rations.EmitSignal(Control.SignalName.MouseEntered);
        await Delay(0.1);
        Require(_screen.GetNode<Label>("%ItemName").Text == "Rations", "Hover did not inspect the real rations.");
        Require(_screen.GetNode<Label>("%ItemDetails").Text.Contains("Owned: 2", StringComparison.Ordinal), "Ration detail lost actual quantity.");
        rations.EmitSignal(Button.SignalName.Pressed);
        foreach (Key key in new[] { Key.E, Key.F, Key.C, Key.G, Key.Q, Key.R }) Tap(key);
        await Delay(0.15);
        Require(before == JsonSerializer.Serialize(View()) && !game.HasPendingAction && game.CurrentTarget is null,
            "Pack inspection or covered field input changed the paused authority or target.");
        Capture("pack-inspection");
        _checks.Add("Pack counts, crafted outputs, keyboard/D-pad/hover/confirm inspection, trapped Tab focus and covered field shortcuts preserved the exact paused public view.");

        Shoulder(JoyButton.B);
        await Until(() => !game.IsInventoryOpen, "Controller Cancel did not close the pack.");
        Require(game.IsLocallyPaused && before == JsonSerializer.Serialize(View()), "Cancel resumed the paused world or crafted behind the pack.");
        Require(_hud.GetNode<Button>("%OpenPack").HasFocus(), "Closing the pack failed to restore its previous HUD focus.");
        Shoulder(JoyButton.Back);
        await Until(() => game.IsInventoryOpen, "View did not reopen the pack.");
        await Delay(0.1);
        Require(_screen.GetNode<Label>("%ItemName").Text == "Rations", "Reopening lost the selected item.");
        Tap(Key.Escape);
        await Until(() => !game.IsInventoryOpen, "Escape did not close the top pack layer.");
        Require(game.IsLocallyPaused, "Closing the pack released a different pause.");
        _checks.Add("B, View and Escape unwound only pack inspection, preserved its selected item and restored HUD focus without releasing pause.");

        Tap(Key.Escape);
        await Until(() => !game.IsLocallyPaused, "World did not resume after explicit second Escape.");
        _hud.GetNode<Button>("%OpenPack").EmitSignal(Button.SignalName.Pressed);
        await Until(() => game.IsInventoryOpen, "The HUD Pack button did not open inspection.");
        await Delay(0.15);
        LocalWorldView start = View();
        SubunitPosition position = Feet();
        KeyEvent(Key.D, true);
        await Delay(0.25);
        Require(View().Calendar != start.Calendar && Feet() == position, "Inventory stopped world time or let field movement through.");
        Require(_screen.GetNode<Label>("%PackTime").Text.StartsWith("World continuing", StringComparison.Ordinal), "Running host time was not shown.");
        Capture("pack-running");
        Tap(Key.I);
        await Until(() => !game.IsInventoryOpen, "I did not close the running pack.");
        await Delay(0.2);
        Require(Feet() == position, "Held navigation escaped when the pack closed.");
        KeyEvent(Key.D, false);
        await Delay(0.1);
        KeyEvent(Key.D, true);
        await Until(() => Feet() != position, "A fresh movement press did not resume field control.");
        KeyEvent(Key.D, false);
        await Delay(0.15);
        _checks.Add("The running pack allowed actual host time while stopping player movement; closing required release before a new field step.");

        Tap(Key.I);
        await Until(() => game.IsInventoryOpen, "Could not open the focus-recovery pack.");
        game.GetWindow().EmitSignal(Window.SignalName.FocusExited);
        await Delay(0.2);
        LocalWorldView held = View();
        Tap(Key.I);
        await Delay(0.15);
        Require(game.IsInventoryOpen && View().Calendar == held.Calendar, "Focus loss leaked input or advanced the held local clock.");
        Require(_screen.GetNode<Label>("%PackTime").Text.StartsWith("World paused", StringComparison.Ordinal), "Focus-loss host pause was not reflected.");
        game.GetWindow().EmitSignal(Window.SignalName.FocusEntered);
        await Delay(0.2);
        Require(game.IsInventoryOpen && View().Calendar != held.Calendar, "Focus return closed the pack or failed to restore the host policy.");
        Capture("pack-recovered");
        _screen.GetNode<Button>("%ClosePack").EmitSignal(Button.SignalName.Pressed);
        await Until(() => !game.IsInventoryOpen, "The visible Close control failed.");
        _checks.Add("Injected focus loss/return kept inspection open, reflected the host pause and restored time without replaying an action.");
        return _checks;
    }

    private Button[] Items() => _screen.GetNode<GridContainer>("%Items").GetChildren().OfType<Button>().ToArray();
    private LocalWorldView View() => game.CurrentView ?? throw new InvalidOperationException("The pack view disappeared.");
    private SubunitPosition Feet() => View().Actors.Single(actor => actor.ActorId == View().PlayerActorId).Feet;
    private async Task Until(Func<bool> condition, string failure)
    {
        Stopwatch wait = Stopwatch.StartNew();
        while (!condition())
        {
            Require(wait.Elapsed < TimeSpan.FromSeconds(6) && !game.HasFailed, failure);
            await clock.ToSignal(clock.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
    private async Task Delay(double seconds)
    {
        Stopwatch wait = Stopwatch.StartNew();
        while (wait.Elapsed.TotalSeconds < seconds) await clock.ToSignal(clock.GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private static void KeyEvent(Key key, bool pressed) => Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = pressed });
    private static void Tap(Key key) { KeyEvent(key, true); KeyEvent(key, false); }
    private static void Shoulder(JoyButton button)
    {
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = true });
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = false });
    }
    private void Capture(string suffix)
    {
        Rect2 viewport = clock.GetViewport().GetVisibleRect();
        Control[] controls = [_screen.GetNode<Label>("%ItemName"), _screen.GetNode<Label>("%ItemDetails"),
            _screen.GetNode<Label>("%PackTime"), _screen.GetNode<Button>("%ClosePack"), .. Items()];
        foreach (Control control in controls)
        {
            Rect2 bounds = control.GetGlobalRect();
            Require(viewport.Encloses(bounds), $"Pack control {control.Name} is clipped outside the viewport.");
        }
        if (DisplayServer.GetName() == "headless") return;
        using Image image = clock.GetViewport().GetTexture().GetImage();
        Require(image.SavePng(Path.ChangeExtension(reportPath, "." + suffix + ".png")) == Error.Ok, "Could not retain pack screenshot.");
    }
    private static void Require(bool condition, string failure)
    {
        if (!condition) throw new InvalidOperationException(failure);
    }
}
