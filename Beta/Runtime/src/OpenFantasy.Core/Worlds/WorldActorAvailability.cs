using OpenFantasy.Core.Quests;

namespace OpenFantasy.Core.Worlds;

/// <summary>Voluntary activity eligibility for the complete lifecycle interpretation.</summary>
public static class WorldActorAvailability
{
    public static bool CanCooperate(WorldSnapshot world, EntityId actorId) => world.EncounterLifecycle is null
        || world.Progression!.Actors.Any(actor => actor.ActorId == actorId && actor.CurrentHp > 0);

    public static bool CanAct(WorldSnapshot world, EntityId actorId) => CanCooperate(world, actorId)
        && (world.EncounterLifecycle is null || world.EncounterLifecycle.GetDebt(actorId).RemainingDelay == 0);

    internal static bool PermitsAction(WorldSnapshot world, WorldActionCommand command)
    {
        if (world.EncounterLifecycle is null) return true;
        if (!CanAct(world, command.ActorId)) return false;
        EntityId? counterpart = command.Intent switch
        {
            SpeakToActorIntent speak => speak.TargetActorId,
            GiveItemIntent give => give.TargetActorId,
            DeliveryQuestIntent quest => quest.GiverActorId,
            _ => null
        };
        return counterpart is null || CanCooperate(world, counterpart.Value);
    }
}
