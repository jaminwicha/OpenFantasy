using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Host;

/// <summary>Privileged combat inspection; observations do not choose rolls or publish effects.</summary>
internal sealed class EncounterActionDiagnosticReport
{
    public EncounterActionDiagnosticReport(WorldSnapshot world)
    {
        var actions = world.EncounterActions ?? throw new ArgumentException("Encounter actions are required.");
        Hash = actions.ContentHash.Value;
        Revision = actions.Revision;
        RulesVersion = actions.RulesVersion.Value;
        RandomCounter = actions.Random.NextCounter;
        RandomExhausted = actions.Random.Exhausted;
        IsOutcome = WorldEncounterActions.IsOutcome(world);
        Combatants = actions.Combatants.Select(actor =>
        {
            var resources = world.Progression!.GetActor(actor.ActorId);
            return new
            {
                ActorId = actor.ActorId.ToString(), actor.Side, resources.CurrentHp, resources.CurrentMp,
                resources.Stats.MaxHp, IsIncapacitated = resources.CurrentHp == 0,
                Abilities = actor.AbilityIds.Select(id => id.ToString()).ToArray()
            };
        }).ToArray();
        LastResolution = actions.LastResolution is { } last ? new
        {
            last.WorldSequence, last.TimingRevision, ActorId = last.ActorId.ToString(),
            CommandId = last.DecisionCommandId.ToString(), TargetId = last.TargetActorId?.ToString(),
            AbilityId = last.AbilityDefinitionId?.ToString(), Status = last.Status.ToString(),
            last.Damage, last.TargetHpBefore, last.TargetHpAfter, last.ManaSpent, last.Delay, last.VariationPermille,
            Cancelled = last.CancelledDecisionIds.Select(id => id.ToString()).ToArray()
        } : null;
    }

    public string Hash { get; }
    public ulong Revision { get; }
    public string RulesVersion { get; }
    public ulong RandomCounter { get; }
    public bool RandomExhausted { get; }
    public bool IsOutcome { get; }
    public object Combatants { get; }
    public object? LastResolution { get; }
}
