using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Progression;

/// <summary>A pinned denomination. Balances count whole units; there is no floating exchange-rate conversion.</summary>
public sealed class CurrencyDefinition
{
    public CurrencyDefinition(EntityId definitionId, string key, string displayName, string unitName)
    {
        ProgressionValidation.Text(key, 128, nameof(key));
        ProgressionValidation.Text(displayName, 128, nameof(displayName));
        ProgressionValidation.Text(unitName, 64, nameof(unitName));
        DefinitionId = definitionId;
        Key = key;
        DisplayName = displayName;
        UnitName = unitName;
        Version = ContentHash.Compute(ProgressionCodecV1.EncodeCurrency(this));
    }

    public EntityId DefinitionId { get; }
    public string Key { get; }
    public string DisplayName { get; }
    public string UnitName { get; }
    public ContentHash Version { get; }
}
