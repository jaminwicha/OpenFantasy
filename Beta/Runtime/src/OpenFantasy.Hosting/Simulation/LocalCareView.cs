using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
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
    NeedsAttention,
    Treated,
    Cancelled,
    Declined
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
    internal LocalCareView(IEnumerable<LocalCareAcknowledgmentView> offers, LocalCareStatus status,
        EntityId? pendingAcknowledgmentId = null, EntityId? helperActorId = null, string? helperName = null)
        : this(offers, status, pendingAcknowledgmentId, helperActorId, helperName, null, null) { }

    internal LocalCareView(IEnumerable<LocalCareAcknowledgmentView> offers, LocalCareStatus status,
        EntityId? pendingAcknowledgmentId, EntityId? helperActorId, string? helperName,
        LocalCareDecisionView? decision, LocalCareClosureView? closure)
    {
        Offers = Array.AsReadOnly(offers.ToArray());
        Status = status;
        PendingAcknowledgmentId = pendingAcknowledgmentId;
        HelperActorId = helperActorId;
        HelperName = helperName;
        Decision = decision;
        Closure = closure;
    }

    public ReadOnlyCollection<LocalCareAcknowledgmentView> Offers { get; }
    public LocalCareStatus Status { get; }
    public EntityId? PendingAcknowledgmentId { get; }
    public EntityId? HelperActorId { get; }
    public string? HelperName { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LocalCareDecisionView? Decision { get; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LocalCareClosureView? Closure { get; }

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
            bool admitsDecisions = LocalProfileBindings.AdmitsCareDispositions(world);
            if (agreement.PatientActorId != playerActorId
                && (!admitsDecisions || agreement.HelperActorId != playerActorId))
            {
                return null;
            }
            WorldResident helper = world.GetResident(agreement.HelperActorId);
            LocalCareStatus status = helperSelectionOccupied ? LocalCareStatus.NeedsAttention
                : (admitsDecisions || LocalCareController.HostControl(world, helper.ActorId) is not null)
                    && WorldCareActions.PrepareRemedy(world) is not null
                    ? LocalCareStatus.Pending : LocalCareStatus.Unavailable;
            LocalCareDecisionView? decision = null;
            if (admitsDecisions)
            {
                var cancellation = new CancelPendingCareIntent(pending.AcknowledgmentId, pending.EncounterId, care.Revision);
                var decline = new DeclinePendingCareIntent(pending.AcknowledgmentId, pending.EncounterId, care.Revision);
                decision = new LocalCareDecisionView(agreement.PatientActorId,
                    world.GetResident(agreement.PatientActorId).DisplayName,
                    agreement.PatientActorId == playerActorId
                        && WorldCareActions.EvaluateDisposition(world, playerActorId, cancellation) == WorldActionRejection.None
                        ? cancellation : null,
                    agreement.HelperActorId == playerActorId
                        && WorldCareActions.EvaluateDisposition(world, playerActorId, decline) == WorldActionRejection.None
                        ? decline : null,
                    agreement.HelperActorId == playerActorId ? WorldCareActions.PrepareRemedy(world) : null);
            }
            return new LocalCareView([], status, pending.AcknowledgmentId, helper.ActorId, helper.DisplayName, decision, null);

        }
        if (care.SupportsDispositions && world.EncounterActions is { } current)
        {
            IEnumerable<CareClosedDefeatWitness> witnesses = care.Continuation is { } context ? context.ClosedDefeatWitnesses : [];
            foreach (CareAcknowledgment closed in care.Acknowledgments.Concat(witnesses.Select(value => value.Acknowledgment))
                .OrderByDescending(value => value.WorldSequence))
            {
                CareAgreement agreement = care.GetAgreement(closed.AgreementId);
                if (closed.EncounterId != current.Scope.EncounterId
                    || playerActorId != agreement.PatientActorId && playerActorId != agreement.HelperActorId) continue;
                // Treatment can resume this same episode. An older closure must not hide a later defeat.
                if (current.LastResolution is { Status: EncounterActionResolutionStatus.Damaged, TargetHpAfter: 0 } laterDefeat
                    && laterDefeat.TargetActorId == agreement.PatientActorId && laterDefeat.WorldSequence > closed.WorldSequence) continue;
                CareClosedDefeatWitness? witness = witnesses.SingleOrDefault(value => value.Acknowledgment.AcknowledgmentId == closed.AcknowledgmentId);
                CareDisposition? disposition = care.Dispositions.SingleOrDefault(value => value.AcknowledgmentId == closed.AcknowledgmentId)
                    ?? witness?.Disposition;
                bool treated = care.Treatments.Any(value => value.AcknowledgmentId == closed.AcknowledgmentId) || witness?.Treatment is not null;
                if (disposition is null && !treated) continue;
                LocalCareClosureKind kind = treated ? LocalCareClosureKind.Treated
                    : disposition!.Kind == CareDispositionKind.PatientCancelled
                        ? LocalCareClosureKind.PatientCancelled : LocalCareClosureKind.HelperDeclined;
                LocalCareStatus status = treated ? LocalCareStatus.Treated
                    : kind == LocalCareClosureKind.PatientCancelled ? LocalCareStatus.Cancelled : LocalCareStatus.Declined;
                return new LocalCareView([], status, null, helperActorId: agreement.HelperActorId,
                    helperName: world.GetResident(agreement.HelperActorId).DisplayName, decision: null,
                    closure: new LocalCareClosureView(closed.AcknowledgmentId, agreement.PatientActorId, kind));
            }
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
            if (!LocalProfileBindings.AdmitsCareDispositions(world)
                && rejection == WorldActionRejection.None && LocalCareController.HostControl(world, helper.ActorId) is null)
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
