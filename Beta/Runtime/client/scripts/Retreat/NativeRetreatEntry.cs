using Godot;

namespace OpenFantasy.Client;

/// <summary>
/// The explicitly launched entry of the retreat-capable cargo world. Normal play opens only that world's own save (the
/// default user save or a named one) with ordinary window focus. The disposable probe continues an exported fixture's
/// existing save; only that probe may render in the background. Every other entry and save keeps its behaviour.
/// </summary>
public partial class NativeRetreatEntry : Node
{
    private const string ProbeArgument = "--cargo-retreat-probe";
    private const string BackgroundArgument = "--background-render";

    public override async void _Ready()
    {
        string[] arguments = OS.GetCmdlineUserArgs();
        FarmInputMap.InstallDefaults();
        bool background = arguments is [ProbeArgument, _, _, _, _, "keyboard" or "controller" or "pointer" or "reopen", BackgroundArgument];
        if (arguments.Contains(BackgroundArgument) && !background)
        {
            Refuse("Background rendering requires the supported disposable retreat probe invocation.");
            return;
        }
        if (background)
        {
            arguments = arguments[..^1];
            GetWindow().Unfocusable = true;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (arguments is [ProbeArgument, var save, var report, var genesis, var metadata, var mode]
            && mode is "keyboard" or "controller" or "pointer" or "reopen")
        {
            if (!File.Exists(save) || !File.Exists(genesis) || !File.Exists(metadata))
            {
                Refuse("The retreat probe requires its exported existing save, expected fresh genesis and metadata.");
                return;
            }
            var game = new FarmGame { CargoRetreatFixtureGenesisPath = genesis, SavePathOverride = save, StartPaused = true,
                BindWindowFocus = !background };
            AddChild(game);
            AddChild(new RetreatPlayProbe(game, report, genesis, metadata, mode));
        }
        else if (arguments is ["--cargo-retreat"])
        {
            AddChild(new FarmGame { CargoRetreatProfile = true });
        }
        else if (arguments is ["--cargo-retreat", var retreatSave])
        {
            AddChild(new FarmGame { CargoRetreatProfile = true, SavePathOverride = retreatSave });
        }
        else
        {
            Refuse("The retreat entry accepts --cargo-retreat [SAVE] or the disposable retreat probe arguments.");
        }
    }

    private void Refuse(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
