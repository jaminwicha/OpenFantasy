using System.Runtime.InteropServices;
using System.Text.Json;
using Godot;
using OpenFantasy.Core;

namespace OpenFantasy.Client;

public partial class Bootstrap : Control
{
    public override async void _Ready()
    {
        string[] arguments = OS.GetCmdlineUserArgs();
        if (arguments is ["--authority-smoke", var database, var genesis, var command])
        {
            try
            {
                var report = await new DurableAuthoritySmoke().RunAsync(database, genesis, command);
                GD.Print(JsonSerializer.Serialize(report));
                GetTree().Quit();
            }
            catch (Exception error)
            {
                GD.PushError("Durable authority smoke failed: " + error);
                GetTree().Quit(1);
            }
            return;
        }

        WriteFoundationDiagnostic();

        if (arguments.Contains("--smoke-test"))
        {
            GetTree().Quit();
            return;
        }

        GetNode<Control>("Backdrop").Hide();
        GetNode<Control>("Center").Hide();
        FarmInputMap.InstallDefaults();
        if (arguments is ["--farm-lifecycle", var lifecycleSave, var lifecycleReport, var mode])
        {
            FarmLifecycleProbe.Start(this, lifecycleSave, lifecycleReport, mode);
        }
        else if (arguments is ["--farm-construction", var constructionSource, var constructionDestination,
            var constructionReport, var constructionMode] && constructionMode is "upgrade" or "fresh" or "finish" or "reopen")
        {
            bool validPaths = constructionMode == "upgrade"
                ? File.Exists(constructionSource) && !File.Exists(constructionDestination)
                : File.Exists(constructionDestination);
            if (!validPaths)
            {
                GD.PushError("The construction probe requires its exact retained source and stage destination.");
                GetTree().Quit(1);
                return;
            }
            var game = new FarmGame { SavePathOverride = constructionDestination,
                UpgradeSourcePathOverride = constructionSource, StartPaused = true,
                QuestProfile = true, ConstructionProfile = true };
            AddChild(game);
            AddChild(new FarmConstructionProbe(game, constructionReport, constructionMode));
        }
        else if (arguments is ["--farm-quests", var questSource, var questDestination, var questReport, var questMode]
            && questMode is "prepare" or "upgrade" or "reopen" or "fresh")
        {
            bool prepare = questMode == "prepare";
            bool validPaths = questMode switch
            {
                "prepare" or "fresh" => !File.Exists(questSource) && !File.Exists(questDestination),
                "upgrade" => File.Exists(questSource) && !File.Exists(questDestination),
                _ => File.Exists(questDestination)
            };
            if (!validPaths)
            {
                GD.PushError("The quest probe requires its exact fresh or retained stage paths.");
                GetTree().Quit(1);
                return;
            }
            var game = new FarmGame { SavePathOverride = prepare ? questSource : questDestination,
                UpgradeSourcePathOverride = questMode == "fresh" ? null : questSource,
                StartPaused = !prepare, QuestProfile = !prepare, ConstructionProfile = questMode == "fresh" };
            AddChild(game);
            AddChild(new FarmQuestProbe(game, questReport, questMode));
        }
        else if (arguments is ["--farm-production", var sourceSave, var productionSave, var productionReport, var productionMode]
            && productionMode is "prepare" or "upgrade" or "reopen")
        {
            bool prepare = productionMode == "prepare";
            bool pathsValid = productionMode switch
            {
                "prepare" => !File.Exists(sourceSave) && !File.Exists(productionSave),
                "upgrade" => File.Exists(sourceSave) && !File.Exists(productionSave),
                _ => File.Exists(productionSave)
            };
            if (!pathsValid)
            {
                GD.PushError("The production probe requires its exact fresh or retained stage paths.");
                GetTree().Quit(1);
                return;
            }
            var game = new FarmGame { SavePathOverride = prepare ? sourceSave : productionSave,
                UpgradeSourcePathOverride = sourceSave, StartPaused = !prepare, ProductionProfile = !prepare };
            AddChild(game);
            AddChild(new FarmProductionProbe(game, productionReport, productionMode));
        }
        else if (arguments is ["--farm-gathering", var gatherSave, var gatherReport, var gatherStage]
            && gatherStage is "create" or "reopen")
        {
            if (gatherStage == "create" && File.Exists(gatherSave) || gatherStage == "reopen" && !File.Exists(gatherSave))
            {
                GD.PushError("The gathering probe requires a fresh create path or an existing reopen path.");
                GetTree().Quit(1);
                return;
            }
            var game = new FarmGame { SavePathOverride = gatherSave, StartPaused = gatherStage == "reopen", ProductionProfile = false };
            AddChild(game);
            AddChild(new FarmGatheringProbe(game, gatherReport, gatherStage == "reopen"));
        }
        else if (arguments is ["--farm-walkthrough", var save, var reportPath, var stage] && stage is "create" or "reopen")
        {
            if (stage == "create" && File.Exists(save) || stage == "reopen" && !File.Exists(save))
            {
                GD.PushError("The farm walkthrough requires a fresh create path or an existing reopen path.");
                GetTree().Quit(1);
                return;
            }
            var game = new FarmGame { SavePathOverride = save, StartPaused = stage == "reopen", GatheringProfile = false };
            AddChild(game);
            AddChild(new FarmWalkthroughProbe(game, reportPath, stage == "reopen"));
        }
        else
        {
            AddChild(new FarmGame { GatheringProfile = !arguments.Contains("--legacy-farm"), QuestProfile = true, ConstructionProfile = true });
        }
    }

    private static void WriteFoundationDiagnostic()
    {
        var seed = new byte[32];
        seed[^1] = 42;
        var identity = EntityId.DeriveGenerated(seed, "world-v1", "0/0/0", "resident", 0);
        var random = new CounterRandomV1(seed, "rules-v1", "foundation", identity);
        var midnight = new WorldInstant(-1, WorldInstant.MillisecondsPerDay - 1).AddMilliseconds(1);

        GD.Print(JsonSerializer.Serialize(new
        {
            Contract = "foundation-v1",
            Runtime = RuntimeInformation.FrameworkDescription,
            Entity = identity.ToString(),
            Sample = random.NextUInt64(),
            Day = midnight.Days,
            Millisecond = midnight.MillisecondsWithinDay
        }));
    }
}
