using Godot;
using OpenFantasy.Client.Cargo;

namespace OpenFantasy.Client;

/// <summary>
/// The explicitly launched entry of the native cargo world. Normal play opens only the cargo save (the default user
/// save or a named one) with ordinary window focus. The disposable probe continues an exported fixture's existing
/// save; only that probe may render in the background. The default bootstrap and every other world stay unchanged.
/// </summary>
public partial class NativeCargoEntry : Node
{
    private const string ProbeArgument = "--cargo-probe";
    private const string OnboardingArgument = "--cargo-onboarding-probe";
    private const string BackgroundArgument = "--background-render";

    public override async void _Ready()
    {
        string[] arguments = OS.GetCmdlineUserArgs();
        FarmInputMap.InstallDefaults();
        bool background = arguments is [ProbeArgument, _, _, _, _, "keyboard" or "reopen", BackgroundArgument]
            or [OnboardingArgument, _, _, BackgroundArgument];
        if (arguments.Contains(BackgroundArgument) && !background)
        {
            Refuse("Background rendering requires the supported disposable cargo probe invocation.");
            return;
        }
        if (background)
        {
            arguments = arguments[..^1];
            GetWindow().Unfocusable = true;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (arguments is [ProbeArgument, var save, var report, var genesis, var metadata, var mode] && mode is "keyboard" or "reopen")
        {
            if (!File.Exists(save) || !File.Exists(genesis) || !File.Exists(metadata))
            {
                Refuse("The cargo probe requires its exported existing save, expected fresh genesis and metadata.");
                return;
            }
            var game = new FarmGame { CargoFixtureGenesisPath = genesis, SavePathOverride = save, StartPaused = true,
                BindWindowFocus = !background };
            AddChild(game);
            AddChild(new CargoRoutePlayProbe(game, report, genesis, metadata, mode));
        }
        else if (arguments is [OnboardingArgument, var freshSave, var onboardingReport])
        {
            // The ordinary production selection (fresh seed, create-or-same-world reopen) on a disposable new save path.
            var game = new FarmGame { CargoProfile = true, SavePathOverride = freshSave, StartPaused = true,
                BindWindowFocus = !background };
            var probe = new CargoOnboardingPlayProbe(game, freshSave, onboardingReport);
            AddChild(game);
            AddChild(probe);
        }
        else if (arguments is ["--cargo"])
        {
            AddChild(new FarmGame { CargoProfile = true });
        }
        else if (arguments is ["--cargo", var cargoSave])
        {
            AddChild(new FarmGame { CargoProfile = true, SavePathOverride = cargoSave });
        }
        else
        {
            Refuse("The cargo entry accepts --cargo [SAVE] or the disposable cargo probe arguments.");
        }
    }

    private void Refuse(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
