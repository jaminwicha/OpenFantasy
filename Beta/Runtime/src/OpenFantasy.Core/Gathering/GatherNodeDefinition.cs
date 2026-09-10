using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Gathering;

public sealed class GatherNodeDefinition
{
    public GatherNodeDefinition(EntityId definitionId, GatherNodeKind kind, CountedItemDefinition yieldDefinition)
    {
        ArgumentNullException.ThrowIfNull(yieldDefinition);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        DefinitionId = definitionId;
        Kind = kind;
        YieldDefinition = yieldDefinition;
        Version = ContentHash.Compute(GatheringCodecV1.EncodeDefinition(this));
    }
    public EntityId DefinitionId { get; }
    public GatherNodeKind Kind { get; }
    public CountedItemDefinition YieldDefinition { get; }
    public string DisplayName => Kind == GatherNodeKind.Oak ? "Oak" : "Berry Bush";
    public GatherToolKind RequiredToolKind => Kind == GatherNodeKind.Oak ? GatherToolKind.Axe : GatherToolKind.Sickle;
    public int MaxSwings => Kind == GatherNodeKind.Oak ? 5 : 4;
    public ulong RegrowthDelaySteps => Kind == GatherNodeKind.Oak ? 900UL : 700UL;
    public ulong StumpSteps => GatherRulesV1.StumpSteps;
    public ContentHash Version { get; }
}
