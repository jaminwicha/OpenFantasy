using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

public enum LocalCareStatus
{
    Available,
    Unavailable,
    Pending,
    NeedsAttention
}

/// <summary>A patient's exact acknowledgment choice; the helper retains control of the treatment.</summary>
public sealed record LocalCareAcknowledgmentView(EntityId HelperActorId, string HelperName,
    AcknowledgeCareIntent Intent, bool CanAcknowledge, WorldActionRejection Rejection);

/// <summary>
/// Public care choices and the patient's pending obligation. Helper custody, provenance and control
/// credentials remain private; availability neither spends a dose nor commits the helper to act.
/// </summary>
public sealed class LocalCareView
{
    private LocalCareView(IEnumerable<LocalCareAcknowledgmentView> offers, LocalCareStatus status,
        EntityId? pendingAcknowledgmentId = null, EntityId? helperActorId = null, string? helperName = null)
    {
        Offers = Array.AsReadOnly(offers.ToArray());
        Status = status;
        PendingAcknowledgmentId = pendingAcknowledgmentId;
        HelperActorId = helperActorId;
        HelperName = helperName;
    }

    public ReadOnlyCollection<LocalCareAcknowledgmentView> Offers { get; }
    public LocalCareStatus Status { get; }
    public EntityId? PendingAcknowledgmentId { get; }
    public EntityId? HelperActorId { get; }
    public string? HelperName { get; }

    internal static LocalCareView? Project(WorldSnapshot world, EntityId playerActorId,
        bool helperSelectionOccupied)
    {
        if (world.Care is not { } care)
        {
            return null;
        }
        if (care.Pending is { } pending)
        {
            CareAgreement agreement = care.GetAgreement(pending.AgreementId);
            if (agreement.PatientActorId != playerActorId)
            {
                return null;
            }
            WorldResident helper = world.GetResident(agreement.HelperActorId);
            LocalCareStatus status = helperSelectionOccupied ? LocalCareStatus.NeedsAttention
                : LocalCareController.HostControl(world, helper.ActorId) is not null && WorldCareActions.PrepareRemedy(world) is not null
                    ? LocalCareStatus.Pending : LocalCareStatus.Unavailable;
            return new LocalCareView([], status, pending.AcknowledgmentId, helper.ActorId, helper.DisplayName);
        }
        if (world.EncounterLifecycle is not { Phase: EncounterLifecyclePhase.Outcome } lifecycle
            || lifecycle.Actions is not
                { LastResolution: { Status: EncounterActionResolutionStatus.Damaged, TargetHpAfter: 0 } defeat } actions
            || defeat.TargetActorId != playerActorId || world.Progression!.GetActor(playerActorId).CurrentHp != 0)
        {
            return null;
        }
        List<LocalCareAcknowledgmentView> offers = [];
        foreach (CareAgreement agreement in care.Agreements.Where(value => value.PatientActorId == playerActorId))
        {
            WorldResident helper = world.GetResident(agreement.HelperActorId);
            var intent = new AcknowledgeCareIntent(agreement.AgreementId, actions.Scope.EncounterId,
                lifecycle.Revision, actions.ContentHash);
            WorldActionRejection rejection = WorldCareActions.EvaluateAcknowledgment(world, playerActorId, intent);
            if (rejection == WorldActionRejection.None && LocalCareController.HostControl(world, helper.ActorId) is null)
            {
                rejection = WorldActionRejection.Unavailable;
            }
            offers.Add(new LocalCareAcknowledgmentView(helper.ActorId, helper.DisplayName, intent,
                rejection == WorldActionRejection.None, rejection));
        }
        if (offers.Count == 0)
        {
            return null;
        }
        return new LocalCareView(offers, offers.Any(value => value.CanAcknowledge)
            ? LocalCareStatus.Available : LocalCareStatus.Unavailable);
    }

}
