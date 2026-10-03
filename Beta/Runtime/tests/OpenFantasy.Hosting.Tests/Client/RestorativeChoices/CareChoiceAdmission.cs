using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.WorldStorage;

namespace OpenFantasy.Hosting.Tests.Client.RestorativeChoices;

/// <summary>Fresh authored policies share finite original Potion stock; no live HP, earned item or permission is edited.</summary>
internal static class CareChoiceAdmission
{
    internal static OrdinaryCareWorldFixture Create(bool identicalTerms = false)
    {
        var basis = CareJourneyAdmission.CreateOrdinaryBasis(FarmContentFiles.Embedded(), null,
            EncounterJourneyWorldFixture.SeedText, EncounterJourneyWorldFixture.PlayerName);
        EntityId patient = basis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        EntityId helper = basis.Quests!.Definitions.Single().GiverActorId;
        var original = basis.Care!.Remedies.Single();
        var gentle = new RestorativeRemedyDefinition(Id(1), original.Item, identicalTerms ? original.RestoredHp : 10, original.RangeSubunits);
        var care = new CareSnapshot(basis.WorldId, basis.BranchId, 0, [original, gentle], basis.Care.Agreements, [], []);
        var admission = ContentHash.Compute(basis.ContentHash.ToByteArray().Concat(care.ContentHash.ToByteArray()).ToArray());
        var expanded = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
            care: care, resourceHistory: new WorldResourceHistory(admission, new ActorResourceHistory(basis.Progression!, 0, []), []));
        var form = basis.Space.GetActor(patient).Binding;
        RestorativeFormBinding[] forms = [new(form.DefinitionId, form.DefinitionVersion)];
        RestorativeUseDefinition[] definitions =
        [
            new(Id(2), original.DefinitionId, original.Version, RestorativeDeliveryMode.SelfUse, forms),
            new(Id(3), gentle.DefinitionId, gentle.Version, RestorativeDeliveryMode.SelfUse, forms),
            new(Id(4), original.DefinitionId, original.Version, RestorativeDeliveryMode.LivingAssistance, forms),
            new(Id(5), gentle.DefinitionId, gentle.Version, RestorativeDeliveryMode.LivingAssistance, forms)
        ];
        CarePermission[] permissions =
        [
            new(Id(6), patient, helper, definitions[2].DefinitionId, definitions[2].Version),
            new(Id(7), patient, helper, definitions[identicalTerms ? 2 : 3].DefinitionId, definitions[identicalTerms ? 2 : 3].Version)
        ];
        var genesis = WorldRestorativeAdmission.CreateFresh(expanded, new(definitions, permissions, []));
        Assert.Equal(basis.Inventory.ContentHash, genesis.Inventory.ContentHash);
        Assert.Equal(basis.Progression!.ContentHash, genesis.Progression!.ContentHash);
        Assert.Equal(basis.Production!.ContentHash, genesis.Production!.ContentHash);
        return new(genesis);
    }

    private static EntityId Id(ulong value) => new(0xcaeec001, value);
}
