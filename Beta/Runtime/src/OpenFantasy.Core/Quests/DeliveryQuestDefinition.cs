using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Progression;

namespace OpenFantasy.Core.Quests;

/// <summary>A pinned, actor-owned delivery obligation, with a funded reward and retained project materials.</summary>
public sealed class DeliveryQuestDefinition
{
    public DeliveryQuestDefinition(EntityId definitionId, string key, string title, string description,
        EntityId giverActorId, uint minimumLevel, CountedItemDefinition item, ulong quantity,
        EntityId destinationCustodianId, EntityId materialReservationId, ActorRewardDefinition reward)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(reward);
        ArgumentOutOfRangeException.ThrowIfZero(minimumLevel);
        ArgumentOutOfRangeException.ThrowIfZero(quantity);
        if (CanonicalEncodingV1.Text(key, nameof(key)).Length > 128
            || CanonicalEncodingV1.Text(title, nameof(title)).Length > 256
            || CanonicalEncodingV1.Text(description, nameof(description)).Length > 2048)
        {
            throw new ArgumentException("The delivery quest text exceeds its admitted bounds.");
        }
        DefinitionId = definitionId;
        Key = key;
        Title = title;
        Description = description;
        GiverActorId = giverActorId;
        MinimumLevel = minimumLevel;
        Item = item;
        Quantity = quantity;
        DestinationCustodianId = destinationCustodianId;
        MaterialReservationId = materialReservationId;
        Reward = reward;
    }

    public EntityId DefinitionId { get; }
    public string Key { get; }
    public string Title { get; }
    public string Description { get; }
    public EntityId GiverActorId { get; }
    public uint MinimumLevel { get; }
    public CountedItemDefinition Item { get; }
    public ulong Quantity { get; }
    public EntityId DestinationCustodianId { get; }
    public EntityId MaterialReservationId { get; }
    public ActorRewardDefinition Reward { get; }
    public ContentHash Version => ContentHash.Compute(QuestCodecV1.EncodeDefinition(this));
}
