using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.RoadThreat;

namespace OpenFantasy.Hosting.Content;

/// <summary>
/// An explicitly selected, playable construction qualification world. Three new travelers join Wren
/// as a fourth human; her original project, kit and supply contract retain their exact ownership.
/// This fresh calibration neither changes the default population nor converts an existing save.
/// </summary>
public static class HumanConstructionWorldAdmission
{
    public const string SeedText = "human-construction-world";

    public static WorldSnapshot CreateEmbedded(string seedText, IReadOnlyList<string> threePlayerNames)
    {
        ArgumentNullException.ThrowIfNull(threePlayerNames);
        if (threePlayerNames.Count != 3)
        {
            throw new ArgumentException("This construction scenario requires three travelers and the retained builder.",
                nameof(threePlayerNames));
        }

        WorldSnapshot original = RoadThreatWorldAdmission.CreateEmbedded(seedText, threePlayerNames);
        EntityId builder = original.Construction!.Projects.Single().BuilderActorId;
        var calibration = new EncounterOutcomeCalibration(new Dictionary<EntityId, int>(), [builder]);
        WorldSnapshot outcomes = WorldEncounterOutcomeAdmission.CreateCalibratedFresh(original, calibration);
        WorldSnapshot controllers = WorldControllerAdmission.CreateFresh(outcomes);

        // Recovery is a declared prerequisite of scoped co-player control. This adds its policy,
        // not treatment, medicine, permission, restored health or a new source of construction material.
        RestorativeUseDefinition remedy = controllers.Care!.Ordinary!.Definitions.First(definition =>
            definition.DeliveryMode == RestorativeDeliveryMode.LivingAssistance);
        EntityId recoveryId = EntityId.DeriveGenerated("openfantasy.authored-content"u8,
            "human-construction-world.v1", "incapacitated_assistance", "definition", 0);
        var recovery = RestorativeUseDefinition.CreateIncapacitatedAssistance(recoveryId,
            remedy.RemedyDefinitionId, remedy.RemedyVersion, remedy.SupportedForms);
        WorldSnapshot recovered = WorldRecoveryAdmission.CreateFresh(controllers, [recovery], []);
        WorldSnapshot dispositions = WorldCareDispositionAdmission.CreateFresh(recovered);
        WorldSnapshot permissions = WorldControlPermissionAdmission.CreateFresh(dispositions);
        return WorldDelegatedControlAdmission.CreateFresh(permissions);
    }
}
