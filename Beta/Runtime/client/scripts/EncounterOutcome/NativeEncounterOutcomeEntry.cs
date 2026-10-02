using Godot;

namespace OpenFantasy.Client;

/// <summary>
/// The explicitly launched entry of the outcome-completion cargo world (profile 35): the threatened cargo world whose
/// resolved encounters are completed by the world after every required player reads the outcome. Normal play opens only
/// that world's own save (the default user save or a named one) with ordinary window focus, or continues an exported
/// fixture's existing save with its expected fresh genesis. The disposable outcome probe continues an exported fixture;
/// only that probe may render in the background. Every other entry, selection and save keeps its profile and behaviour.
/// </summary>
public partial class NativeEncounterOutcomeEntry : Node
{
    private const string ProbeArgument = "--cargo-outcomes-probe";
    private const string BackgroundArgument = "--background-render";

    public override async void _Ready()
    {
        string[] arguments = OS.GetCmdlineUserArgs();
        FarmInputMap.InstallDefaults();
        bool background = arguments is [ProbeArgument, _, _, _, _, "victory" or "defeat" or "care" or "care-wheel" or "reopen" or "care-accepted" or "care-reopen", BackgroundArgument];
        if (arguments.Contains(BackgroundArgument) && !background)
        {
            Refuse("Background rendering requires the supported disposable outcome probe invocation.");
            return;
        }
        if (background)
        {
            // Explicit before any game window content exists: the probe window never takes OS focus.
            arguments = arguments[..^1];
            GetWindow().Unfocusable = true;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (arguments is [ProbeArgument, var probeSave, var report, var probeGenesis, var metadata, var mode]
            && mode is "victory" or "defeat" or "care" or "care-wheel" or "reopen" or "care-accepted" or "care-reopen")
        {
            if (!File.Exists(probeSave) || !File.Exists(probeGenesis) || !File.Exists(metadata))
            {
                Refuse("The outcome probe requires its exported existing save, expected fresh genesis and metadata.");
                return;
            }
            var probed = new FarmGame { CargoOutcomesFixtureGenesisPath = probeGenesis, SavePathOverride = probeSave, StartPaused = true,
                BindWindowFocus = !background };
            AddChild(probed);
            EncounterOutcomeOverlay overlay = EncounterOutcomeOverlay.Create(probed);
            AddChild(overlay);
            AddChild(new EncounterOutcomePlayProbe(probed, overlay, report, probeGenesis, metadata, mode));
            return;
        }
        FarmGame? game = arguments switch
        {
            ["--cargo-outcomes"] => new FarmGame { CargoOutcomesProfile = true },
            ["--cargo-outcomes", var save] => new FarmGame { CargoOutcomesProfile = true, SavePathOverride = save },
            ["--cargo-outcomes-fixture", var save, var genesis] when File.Exists(save) && File.Exists(genesis) =>
                new FarmGame { CargoOutcomesFixtureGenesisPath = genesis, SavePathOverride = save, StartPaused = true },
            _ => null
        };
        if (game is null)
        {
            Refuse("The outcome entry accepts --cargo-outcomes [SAVE], --cargo-outcomes-fixture <existing-save> <genesis> or the "
                + "disposable outcome probe arguments.");
            return;
        }
        AddChild(game);
        AddChild(EncounterOutcomeOverlay.Create(game));
    }

    private void Refuse(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
