using OpenFantasy.Core.Care;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Names the exact scope of the first evidence binding. Conversation, waiting, movement, quest
/// acceptance, care acknowledgment, permission revocation and encounter choices are excluded by
/// contract and publish no observation. Material intents without a kind are refused before
/// interpretation, so an absent observation never implies that such an effect was covered.
/// </summary>
internal static class WorldMaterialEvidenceCoverage
{
    internal const ushort BoundProfileVersion = 26;

    internal static WorldMaterialEvidenceKind? Classify(WorldActionIntent intent) => intent switch
    {
        GatherIntent => WorldMaterialEvidenceKind.Gather,
        ExcavateIntent => WorldMaterialEvidenceKind.Excavation,
        WorkConstructionIntent => WorldMaterialEvidenceKind.Construction,
        CraftIntent => WorldMaterialEvidenceKind.Craft,
        GiveItemIntent => WorldMaterialEvidenceKind.Transfer,
        CompleteDeliveryQuestIntent => WorldMaterialEvidenceKind.QuestDelivery,
        UseRestorativeItemIntent => WorldMaterialEvidenceKind.RestorativeUse,
        AdministerRemedyIntent => WorldMaterialEvidenceKind.AftermathRemedy,
        _ => null
    };

    internal static void RequireProfile(WorldSnapshot world)
    {
        if (world.ProfileVersion != BoundProfileVersion)
        {
            throw new InvalidOperationException("Material evidence is bound only to the complete profile 26 world.");
        }
    }

    internal static void RequireCovered(WorldStepBatch batch)
    {
        foreach (WorldActionCommand action in batch.Actions)
        {
            if (action.Intent is ClimbIntent or ScheduleRunoffTransportIntent or RunoffCommitmentIntent)
            {
                throw new InvalidOperationException("This material evidence binding does not observe climbing, runoff or transport commitments.");
            }
        }
    }
}
