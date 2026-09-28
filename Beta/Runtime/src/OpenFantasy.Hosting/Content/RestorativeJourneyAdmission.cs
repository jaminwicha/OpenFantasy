using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Fresh source-backed care with explicit ordinary delivery policies and revocable prior assistance.</summary>
public static class RestorativeJourneyAdmission
{
    public static WorldSnapshot CreateEmbedded(string seedText, string playerName) =>
        Admit(CareJourneyAdmission.CreateOrdinaryBasis(FarmContentFiles.Embedded(), null, seedText, playerName));

    public static WorldSnapshot Create(string contentRoot, string seedText, string playerName) =>
        Admit(CareJourneyAdmission.CreateOrdinaryBasis(new FarmContentFiles(contentRoot), contentRoot, seedText, playerName));

    private static WorldSnapshot Admit(WorldSnapshot basis)
    {
        EntityId patient = basis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        EntityId helper = basis.Quests!.Definitions.Select(value => value.GiverActorId).Distinct().Single();
        var remedy = basis.Care!.Remedies.Single();
        var form = basis.Space.GetActor(patient).Binding;
        var supported = new RestorativeFormBinding(form.DefinitionId, form.DefinitionVersion);
        var self = new RestorativeUseDefinition(Definition("ordinary/self-potion"), remedy.DefinitionId, remedy.Version,
            RestorativeDeliveryMode.SelfUse, [supported]);
        var assist = new RestorativeUseDefinition(Definition("ordinary/assisted-potion"), remedy.DefinitionId, remedy.Version,
            RestorativeDeliveryMode.LivingAssistance, [supported]);
        var permission = new CarePermission(CareJourneyAdmission.Instance(basis, "ordinary.assistance"), patient, helper,
            assist.DefinitionId, assist.Version);
        return WorldRestorativeAdmission.CreateFresh(basis, new OrdinaryCareSnapshot([self, assist], [permission], []));
    }

    private static EntityId Definition(string key) => CareJourneyAdmission.Definition(key);
}
