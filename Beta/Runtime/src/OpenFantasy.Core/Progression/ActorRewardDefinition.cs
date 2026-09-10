using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Progression;

/// <summary>Pinned reward amounts supplied by admitted content, never trusted client quantities.</summary>
public sealed class ActorRewardDefinition
{
    public ActorRewardDefinition(EntityId definitionId, string key, ulong experience,
        CurrencyDefinition currency, EntityId reserveAccountId, ulong currencyAmount)
    {
        ProgressionValidation.Text(key, 128, nameof(key));
        ArgumentNullException.ThrowIfNull(currency);
        if (definitionId == currency.DefinitionId || definitionId == reserveAccountId
            || currency.DefinitionId == reserveAccountId)
        {
            throw new ArgumentException("Reward, currency and reserve identities must be distinct.");
        }
        DefinitionId = definitionId;
        Key = key;
        Experience = experience;
        Currency = currency;
        ReserveAccountId = reserveAccountId;
        CurrencyAmount = currencyAmount;
        Version = ContentHash.Compute(ProgressionCodecV1.EncodeReward(this));
    }

    public EntityId DefinitionId { get; }
    public string Key { get; }
    public ulong Experience { get; }
    public CurrencyDefinition Currency { get; }
    public EntityId ReserveAccountId { get; }
    public ulong CurrencyAmount { get; }
    public ContentHash Version { get; }
}
