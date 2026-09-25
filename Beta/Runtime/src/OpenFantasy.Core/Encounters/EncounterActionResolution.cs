using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Encounters;

public enum EncounterActionResolutionStatus
{
    Wait = 1,
    Damaged = 2,
    Invalidated = 3
}

/// <summary>A retained action-boundary outcome. Health and mana still belong to actor progression.</summary>
public sealed class EncounterActionResolution
{
    public const int MaximumCancelledDecisions = 64;

    public EncounterActionResolution(ulong worldSequence, ulong timingRevision, EntityId actorId, EntityId decisionCommandId,
        EntityId? targetActorId, EntityId? abilityDefinitionId, EncounterActionResolutionStatus status,
        uint damage, uint targetHpBefore, uint targetHpAfter, uint manaSpent, ulong delay, uint variationPermille,
        IEnumerable<EntityId> cancelledDecisionIds)
    {
        ArgumentNullException.ThrowIfNull(cancelledDecisionIds);
        if (worldSequence == 0 || timingRevision == 0 || delay == 0 || !Enum.IsDefined(status)
            || actorId == decisionCommandId || (targetActorId is null) != (abilityDefinitionId is null))
        {
            throw new ArgumentException("An action outcome requires positive boundaries and delay, distinct identities and paired target/ability.");
        }
        if (status == EncounterActionResolutionStatus.Damaged)
        {
            if (targetActorId is null || targetActorId == actorId || targetHpBefore == 0 || targetHpAfter >= targetHpBefore
                || damage != targetHpBefore - targetHpAfter || variationPermille is < 900 or > 1_100)
            {
                throw new ArgumentException("Physical damage must retain its actual positive HP loss and bounded variation.");
            }
        }
        else if (damage != 0 || targetHpBefore != 0 || targetHpAfter != 0 || manaSpent != 0 || variationPermille != 0
            || status == EncounterActionResolutionStatus.Wait && targetActorId is not null
            || status == EncounterActionResolutionStatus.Invalidated && (targetActorId is null || targetActorId == actorId))
        {
            throw new ArgumentException("Wait and invalidation spend no health, mana or damage draw; only invalidation retains a target and ability.");
        }
        SortedSet<EntityId> cancelled = [];
        foreach (EntityId identity in cancelledDecisionIds)
        {
            if (cancelled.Count == MaximumCancelledDecisions || identity == decisionCommandId || identity == actorId
                || identity == targetActorId || identity == abilityDefinitionId || !cancelled.Add(identity))
            {
                throw new ArgumentException("Cancelled choices must have bounded unique identities distinct from the resolved action.", nameof(cancelledDecisionIds));
            }
        }
        WorldSequence = worldSequence;
        TimingRevision = timingRevision;
        ActorId = actorId;
        DecisionCommandId = decisionCommandId;
        TargetActorId = targetActorId;
        AbilityDefinitionId = abilityDefinitionId;
        Status = status;
        Damage = damage;
        TargetHpBefore = targetHpBefore;
        TargetHpAfter = targetHpAfter;
        ManaSpent = manaSpent;
        Delay = delay;
        VariationPermille = variationPermille;
        CancelledDecisionIds = Array.AsReadOnly(cancelled.ToArray());
    }

    public ulong WorldSequence { get; }
    public ulong TimingRevision { get; }
    public EntityId ActorId { get; }
    public EntityId DecisionCommandId { get; }
    public EntityId? TargetActorId { get; }
    public EntityId? AbilityDefinitionId { get; }
    public EncounterActionResolutionStatus Status { get; }
    public uint Damage { get; }
    public uint TargetHpBefore { get; }
    public uint TargetHpAfter { get; }
    public uint ManaSpent { get; }
    public ulong Delay { get; }
    public uint VariationPermille { get; }
    public ReadOnlyCollection<EntityId> CancelledDecisionIds { get; }
}
