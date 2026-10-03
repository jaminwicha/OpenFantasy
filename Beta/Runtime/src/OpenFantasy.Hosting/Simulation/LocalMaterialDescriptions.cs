using System.Collections.ObjectModel;
using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Public names for one exact admitted item pin, with no stock, effect or use permission.</summary>
public sealed record LocalMaterialDescription
{
    public LocalMaterialDescription(EntityId definitionId, ContentHash itemVersion, string key, string displayName)
    {
        ArgumentNullException.ThrowIfNull(itemVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (definitionId == default || key.Length > 160 || key.Any(character =>
                !char.IsAsciiLetterLower(character) && !char.IsAsciiDigit(character) && character is not '_' and not '/' and not '-' and not '.')
            || displayName != displayName.Trim() || Encoding.UTF8.GetByteCount(displayName) > 256
            || displayName.Any(char.IsControl))
        {
            throw new ArgumentException("A public item descriptor requires a canonical bounded key and readable name.");
        }
        DefinitionId = definitionId;
        ItemVersion = itemVersion;
        Key = key;
        DisplayName = displayName;
    }

    public EntityId DefinitionId { get; }
    public ContentHash ItemVersion { get; }
    public string Key { get; }
    public string DisplayName { get; }
}

/// <summary>Only descriptors relevant to this observation's disclosed facets; absence means unknown.</summary>
public sealed class LocalMaterialDescriptions
{
    public const int MaximumItems = 256;

    public LocalMaterialDescriptions(IEnumerable<LocalMaterialDescription> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var captured = new Dictionary<EntityId, LocalMaterialDescription>();
        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (captured.Count == MaximumItems || !captured.TryAdd(item.DefinitionId, item))
                throw new ArgumentException("Public material descriptions must be bounded and unique by definition.", nameof(items));
        }
        Items = Array.AsReadOnly(captured.Values.OrderBy(item => item.DefinitionId).ToArray());
    }

    public ReadOnlyCollection<LocalMaterialDescription> Items { get; }
    public LocalMaterialDescription? Find(EntityId definitionId) => Items.FirstOrDefault(item => item.DefinitionId == definitionId);
}
