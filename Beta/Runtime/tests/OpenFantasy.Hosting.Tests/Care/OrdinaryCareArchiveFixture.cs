using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>
/// Explicit existing stress calibration earns 254 real bites and a victory before ordinary care.
/// Its large resource pools and speeds are archive-test parameters, not original game statistics.
/// </summary>
internal sealed class OrdinaryCareArchiveFixture
{
    internal OrdinaryCareArchiveFixture(bool materialCareStress = false)
    {
        Journey = new CareWorldFixture(pairedRewardBoundary: true, materialCareStress: materialCareStress);
        var basis = Journey.Genesis;
        var remedy = Assert.Single(basis.Care!.Remedies);
        var form = basis.Space.GetActor(Journey.Player).Binding;
        RestorativeFormBinding[] supported = [new(form.DefinitionId, form.DefinitionVersion)];
        Self = new(Id(1), remedy.DefinitionId, remedy.Version, RestorativeDeliveryMode.SelfUse, supported);
        Assistance = new(Id(2), remedy.DefinitionId, remedy.Version, RestorativeDeliveryMode.LivingAssistance, supported);
        Permission = new(Id(3), Journey.Player, Journey.Helper, Assistance.DefinitionId, Assistance.Version);
        Genesis = WorldRestorativeAdmission.CreateFresh(basis, new([Self, Assistance], [Permission], []));
    }

    internal CareWorldFixture Journey { get; }
    internal WorldSnapshot Genesis { get; }
    internal RestorativeUseDefinition Self { get; }
    internal RestorativeUseDefinition Assistance { get; }
    internal CarePermission Permission { get; }

    internal WorldSnapshot Prepare255(CareWorldSession session)
    {
        var state = new PairedRewardBoundaryJourney(Journey).Prepare(session);
        Assert.Equal(2, state.Dialogues.Count);
        var close = state.Dialogues.Select(dialogue => Journey.Command(state, dialogue.ListenerActorId,
            new CloseDialogueIntent(dialogue.DialogueId))).ToArray();
        state = CareWorldFixture.Commit(session, Journey.Batch(state, WorldBatchKind.HeldInteraction, close)).Candidate;
        Assert.Empty(state.Dialogues);
        Assert.False(state.HasReadingHold);
        Assert.Equal((ushort)26, state.ProfileVersion);
        Assert.Equal(255UL, state.Progression!.Revision);
        Assert.Equal(255, state.ResourceHistory!.History.Batches.Count);
        Assert.Null(state.ResourceHistory.ArchiveAnchor);
        Assert.Equal(254, state.ResourceHistory.Evidence.OfType<WorldEncounterResourceEvidence>()
            .Count(value => value.Request.ActorId == Journey.Wolf));
        Assert.Equal(1, state.ResourceHistory.Evidence.OfType<WorldEncounterResourceEvidence>()
            .Count(value => value.Request.ActorId == Journey.Player));
        Assert.InRange(state.Progression.GetActor(Journey.Player).CurrentHp, 1,
            state.Progression.GetActor(Journey.Player).Stats.MaxHp - 80);
        Assert.True(WorldActorAvailability.CanAct(state, Journey.Helper));
        Assert.True(WorldActorAvailability.CanAct(state, Journey.Player));
        Assert.Empty(state.Care!.Ordinary!.Uses);
        Assert.Empty(state.Production!.ConsumedItems);
        return state;
    }

    internal UseRestorativeItemIntent Aid(WorldSnapshot state) => Assert.IsType<UseRestorativeItemIntent>(
        WorldRestorativeActions.Prepare(state, Journey.Helper, Journey.Player, Assistance.DefinitionId, Permission.PermissionId));

    internal UseRestorativeItemIntent UnownedSelfDose(WorldSnapshot state)
    {
        // Deliberately selects the helper's actual lot without acquiring ownership. This is a
        // rejected potential resource writer, not a fabricated successful second restoration.
        var dose = state.Inventory.Custody.GetLot(Journey.PotionLot);
        var storage = state.Inventory.Custody.GetStorage(state.GetResident(Journey.Player).InventoryCustodianId);
        return new(Journey.Player, Self.DefinitionId, Self.Version, null, null, null,
            state.Space.GetActor(Journey.Player).Revision, state.Progression!.GetActor(Journey.Player).Revision,
            storage.Revision, state.Production!.Revision, new(IngredientLotKind.Source, dose.LotId, dose.Revision, 1));
    }

    internal WorldActionCommand Revoke(WorldSnapshot state)
    {
        var permission = state.Care!.Ordinary!.GetPermission(Permission.PermissionId);
        return Journey.Command(state, Journey.Player,
            new RevokeCarePermissionIntent(permission.PermissionId, permission.Revision, permission.GrantVersion));
    }

    private static EntityId Id(ulong value) => new(0xcafe260000000000, value);
}
