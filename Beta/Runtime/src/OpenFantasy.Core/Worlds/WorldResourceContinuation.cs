using OpenFantasy.Core.Care;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Quests;

namespace OpenFantasy.Core.Worlds;

/// <summary>Reserves the complete next world batch before moving to an exact closed-boundary archive.</summary>
internal static class WorldResourceContinuation
{
    internal static (WorldSnapshot State, WorldResourceArchiveSegment? Sealed) Prepare(WorldSnapshot world, WorldStepBatch batch)
    {
        if (world.ResourceHistory is not { } resources) return (world, null);
        (int batches, int effects) = RequiredCapacity(batch);
        if (resources.History.Batches.Count + batches <= ActorResourceHistory.MaximumBatches
            && resources.History.ChangeCount + effects <= ActorResourceHistory.MaximumChanges)
            return (world, null);

        var segment = WorldResourceArchiveSegment.Seal(world);
        var continued = new WorldResourceHistory(world.AdmissionVersion,
            new ActorResourceHistory(world.Progression!, world.Revision, []), [], segment.Anchor);
        return (world.WithState(world.Inventory, world.Space, world.Residents, world.Calendar,
            world.Revision, world.AdvancingSteps, world.Dialogues, world.Gathering, world.Production,
            world.Progression, world.Quests, world.Construction, world.Reservoirs, world.Transport,
            world.Excavation, world.ConstructionPhysical, world.LiquidCells, world.Pacing,
            encounterLifecycle: world.EncounterLifecycle, care: world.Care, resourceHistory: continued), segment);
    }

    private static (int Batches, int Effects) RequiredCapacity(WorldStepBatch batch)
    {
        if (batch.Kind == WorldBatchKind.EncounterResolution) return (1, 2);
        if (batch.Kind == WorldBatchKind.Care)
            return batch.Actions.Any(command => command.Intent is AdministerRemedyIntent) ? (1, 1) : (0, 0);
        int rewards = batch.Actions.Count(command => command.Intent is CompleteDeliveryQuestIntent);
        return (rewards, rewards);
    }
}
