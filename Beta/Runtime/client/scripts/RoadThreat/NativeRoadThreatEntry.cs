using Godot;

namespace OpenFantasy.Client;

/// <summary>
/// The explicitly launched entry of the threatened cargo world (the camp's Bandit Chief on the cargo lane). Normal play
/// opens only that world's own save (the default user save or a named one) with ordinary window focus. The disposable
/// probe continues an exported fixture's existing save; only that probe may render in the background. Every other entry
/// and save keeps its behaviour.
/// </summary>
public partial class NativeRoadThreatEntry : Node
{
    private const string ProbeArgument = "--cargo-threat-probe";
    private const string BackgroundArgument = "--background-render";

    public override async void _Ready()
    {
        string[] arguments = OS.GetCmdlineUserArgs();
        FarmInputMap.InstallDefaults();
        bool background = arguments is [ProbeArgument, _, _, _, _, "keyboard" or "controller" or "pointer" or "reopen", BackgroundArgument];
        if (arguments.Contains(BackgroundArgument) && !background)
        {
            Refuse("Background rendering requires the supported disposable road-threat probe invocation.");
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
                Refuse("The road-threat probe requires its exported existing save, expected fresh genesis and metadata.");
                return;
            }
            var game = new FarmGame { CargoThreatFixtureGenesisPath = genesis, SavePathOverride = save, StartPaused = true,
                BindWindowFocus = !background };
            AddChild(game);
            AddChild(new RoadThreatPlayProbe(game, report, genesis, metadata, mode));
        }
        else if (arguments is ["--cargo-threat"])
        {
            AddChild(new FarmGame { CargoThreatProfile = true });
        }
        else if (arguments is ["--cargo-threat", var threatSave])
        {
            AddChild(new FarmGame { CargoThreatProfile = true, SavePathOverride = threatSave });
        }
        else
        {
            Refuse("The road-threat entry accepts --cargo-threat [SAVE] or the disposable road-threat probe arguments.");
        }
    }

    private void Refuse(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
