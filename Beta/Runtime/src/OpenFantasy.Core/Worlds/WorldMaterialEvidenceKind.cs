namespace OpenFantasy.Core.Worlds;

/// <summary>
/// The accepted material or work transition one observation describes. Partial work is actual work
/// even when custody and production are unchanged. Intents without a kind are never relabelled.
/// </summary>
public enum WorldMaterialEvidenceKind
{
    Gather = 1,
    Excavation = 2,
    Construction = 3,
    Craft = 4,
    /// <summary>An accepted direct gift between actors.</summary>
    Transfer = 5,
    /// <summary>An accepted quest completion; accepting the quest moves no material.</summary>
    QuestDelivery = 6,
    /// <summary>Ordinary finite self or assisted use during exploration.</summary>
    RestorativeUse = 7,
    /// <summary>The helper's administered dose after an acknowledged defeat.</summary>
    AftermathRemedy = 8
}
