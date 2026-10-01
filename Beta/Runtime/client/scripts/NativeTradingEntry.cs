using Godot;

namespace OpenFantasy.Client;

/// <summary>
/// The explicitly launched entry of the native market world. Normal play opens only the market save (the default
/// user save or a named one) with ordinary window focus. The disposable probe continues an earned fixture's
/// existing save. The default bootstrap and every earlier world selection stay unchanged.
/// </summary>
public partial class NativeTradingEntry : Node
{
    private const string ProbeArgument = "--trading-probe";
    private const string BoundaryArgument = "--trading-boundary-probe";
    private const string BackgroundArgument = "--background-render";

    public override async void _Ready()
    {
        string[] arguments = OS.GetCmdlineUserArgs();
        FarmInputMap.InstallDefaults();
        // Only the disposable probes may detach OS focus for controlled rendering; normal play keeps its focus policy.
        bool background = arguments is [ProbeArgument, _, _, _, "keyboard" or "controller" or "pointer"
            or "reopen-keyboard" or "reopen-controller" or "reopen-pointer", BackgroundArgument]
            or [BoundaryArgument, _, _, _, "expired" or "unfunded" or "moved", "keyboard", BackgroundArgument];
        if (arguments.Contains(BackgroundArgument) && !background)
        {
            Refuse("Background rendering requires the supported disposable market probe invocation.");
            return;
        }
        if (background)
        {
            arguments = arguments[..^1];
            GetWindow().Unfocusable = true;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (arguments is [ProbeArgument, var save, var report, var genesis, var mode]
            && mode is "keyboard" or "controller" or "pointer" or "reopen-keyboard" or "reopen-controller" or "reopen-pointer")
        {
            if (!File.Exists(save) || !File.Exists(genesis))
            {
                Refuse("The market probe requires its earned existing save and expected fresh genesis.");
                return;
            }
            var game = new FarmGame { MarketFixtureGenesisPath = genesis, SavePathOverride = save, StartPaused = true,
                BindWindowFocus = !background };
            AddChild(game);
            AddChild(new MarketTradingPlayProbe(game, report, genesis, mode));
        }
        else if (arguments is [BoundaryArgument, var boundarySave, var boundaryReport, var boundaryGenesis, var scenario, var route]
            && scenario is "expired" or "unfunded" or "moved" && route == "keyboard")
        {
            if (!File.Exists(boundarySave) || !File.Exists(boundaryGenesis))
            {
                Refuse("The market boundary probe requires its prepared existing save and expected fresh genesis.");
                return;
            }
            var game = new FarmGame { MarketFixtureGenesisPath = boundaryGenesis, SavePathOverride = boundarySave, StartPaused = true,
                BindWindowFocus = !background };
            AddChild(game);
            AddChild(new MarketBoundaryPlayProbe(game, boundaryReport, scenario, route));
        }
        else if (arguments is ["--markets"])
        {
            AddChild(new FarmGame { MarketsProfile = true });
        }
        else if (arguments is ["--markets", var marketSave])
        {
            AddChild(new FarmGame { MarketsProfile = true, SavePathOverride = marketSave });
        }
        else
        {
            Refuse("The market entry accepts --markets [SAVE] or the disposable market probe arguments.");
        }
    }

    private void Refuse(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
