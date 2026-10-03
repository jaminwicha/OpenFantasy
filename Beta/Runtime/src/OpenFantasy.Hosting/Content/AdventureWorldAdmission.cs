using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.EncounterOutcome;

namespace OpenFantasy.Hosting.Content;

/// <summary>
/// Fresh inhabited adventure: the original cargo population and economy with controller, recovery
/// and scoped cooperation policies. Wren remains an autonomous resident. No lived save is upgraded.
/// </summary>
public static class AdventureWorldAdmission
{
    public const string SeedText = "wrens-adventure";
    public const string DefaultPlayerName = "Traveler";

    public static WorldSnapshot CreateEmbedded(string seedText, IReadOnlyList<string> playerNames)
    {
        WorldSnapshot cargo = CargoOutcomeWorldAdmission.CreateEmbedded(seedText, playerNames);
        WorldSnapshot controllers = WorldControllerAdmission.CreateFresh(cargo);
        RestorativeUseDefinition assistance = controllers.Care!.Ordinary!.Definitions.Single(definition =>
            definition.DeliveryMode == RestorativeDeliveryMode.LivingAssistance);
        EntityId recoveryId = EntityId.DeriveGenerated("openfantasy.authored-content"u8,
            "adventure-world.v1", "incapacitated_assistance", "definition", 0);
        var recovery = RestorativeUseDefinition.CreateIncapacitatedAssistance(recoveryId,
            assistance.RemedyDefinitionId, assistance.RemedyVersion, assistance.SupportedForms);

        // Defining a remedy creates neither medicine nor permission to treat another person.
        WorldSnapshot recoverable = WorldRecoveryAdmission.CreateFresh(controllers, [recovery], []);
        WorldSnapshot dispositions = WorldCareDispositionAdmission.CreateFresh(recoverable);
        WorldSnapshot permissions = WorldControlPermissionAdmission.CreateFresh(dispositions);
        return WorldDelegatedControlAdmission.CreateFresh(permissions);
    }
}
