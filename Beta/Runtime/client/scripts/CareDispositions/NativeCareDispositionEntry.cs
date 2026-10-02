using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Client;

internal sealed record CareDispositionFixtureSelection(string GenesisPath, EntityId PatientActorId,
    EntityId HelperActorId, EntityId ControlledActorId);

/// <summary>Explicit disposable profile 38 entry; every gameplay action remains ordinary native input.</summary>
public partial class NativeCareDispositionEntry : Node
{
    private const string ProbeArgument = "--care-disposition-probe";
    private const string BackgroundArgument = "--background-render";

    public override void _Ready()
    {
        try
        {
            string[] arguments = OS.GetCmdlineUserArgs();
            bool background = arguments is [ProbeArgument, _, _, _, _, _, BackgroundArgument];
            if (background) arguments = arguments[..^1];
            if (arguments is not [ProbeArgument, var save, var report, var genesis, var metadataPath, var mode]
                || !CareDispositionPlayProbe.AcceptsMode(mode))
                throw new InvalidOperationException("Expected --care-disposition-probe SAVE REPORT GENESIS METADATA MODE [--background-render].");
            if (!File.Exists(save) || !File.Exists(genesis) || !File.Exists(metadataPath))
                throw new FileNotFoundException("The care disposition probe requires its exported existing save, genesis and metadata.");
            bool startupNoFocus = ProjectSettings.GetSetting("display/window/size/no_focus", false).AsBool();
            bool initiallyFocused = DisplayServer.GetName() != "headless" && GetWindow().HasFocus();
            if (background)
            {
                if (!startupNoFocus || initiallyFocused)
                    throw new InvalidOperationException("Background rendering requires launch-configured no_focus and an unfocused window.");
                GetWindow().Unfocusable = true;
            }
            using var metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));
            JsonElement root = metadata.RootElement;
            if (root.GetProperty("Kind").GetString() != "native-care-disposition-fixture-v1"
                || root.GetProperty("Profile").GetUInt16() != WorldCareDispositionAdmission.ProfileVersion)
                throw new InvalidDataException("The metadata does not select a profile 38 care disposition fixture.");
            EntityId patient = EntityId.Parse(root.GetProperty("PatientActorId").GetString());
            EntityId helper = EntityId.Parse(root.GetProperty("HelperActorId").GetString());
            EntityId request = EntityId.Parse(root.GetProperty("RequestId").GetString());
            EntityId controlled = mode.Contains("decline", StringComparison.Ordinal) ? helper : patient;
            FarmInputMap.InstallDefaults();
            var game = new FarmGame
            {
                SavePathOverride = save, StartPaused = true, BindWindowFocus = !background,
                CareDispositionFixture = new(genesis, patient, helper, controlled)
            };
            AddChild(game);
            AddChild(new CareDispositionPlayProbe(game, report, mode, patient, helper, request,
                background, startupNoFocus, initiallyFocused));
        }
        catch (Exception error)
        {
            GD.PushError("Care disposition entry refused: " + error);
            GetTree().Quit(1);
        }
    }
}
