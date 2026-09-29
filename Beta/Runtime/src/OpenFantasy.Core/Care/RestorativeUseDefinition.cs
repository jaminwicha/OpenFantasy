using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Care;

public enum RestorativeDeliveryMode { SelfUse = 1, LivingAssistance = 2 }

/// <summary>An exact admitted form, not a species-name or inferred anatomical capability.</summary>
public sealed record RestorativeFormBinding
{
    public RestorativeFormBinding(EntityId definitionId, ContentHash version)
    {
        ArgumentNullException.ThrowIfNull(version);
        DefinitionId = definitionId;
        Version = version;
    }

    public EntityId DefinitionId { get; }
    public ContentHash Version { get; }
}

/// <summary>
/// Pinned ordinary delivery of an existing remedy to supported living forms. Positive HP and actual
/// manipulation/readiness remain world eligibility checks; this declares no swallowing anatomy.
/// </summary>
public sealed class RestorativeUseDefinition
{
    public const int MaximumSupportedForms = 16;
    public const ulong ExplorationDurationMilliseconds = 1_800;

    // Retained by this exact owner only; never serialized, shared by identity or copied to another definition.
    private ContentHash? _version;

    public RestorativeUseDefinition(EntityId definitionId, EntityId remedyDefinitionId, ContentHash remedyVersion,
        RestorativeDeliveryMode deliveryMode, IEnumerable<RestorativeFormBinding> supportedForms,
        ulong explorationMilliseconds = ExplorationDurationMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(remedyVersion);
        ArgumentNullException.ThrowIfNull(supportedForms);
        if (definitionId == remedyDefinitionId || !Enum.IsDefined(deliveryMode)
            || explorationMilliseconds != ExplorationDurationMilliseconds)
            throw new ArgumentException("Ordinary restorative use requires a distinct remedy and its explicit exploration delivery policy.");
        SortedDictionary<EntityId, RestorativeFormBinding> forms = [];
        foreach (var form in supportedForms)
        {
            ArgumentNullException.ThrowIfNull(form);
            if (forms.Count == MaximumSupportedForms || form.DefinitionId == definitionId
                || form.DefinitionId == remedyDefinitionId || !forms.TryAdd(form.DefinitionId, form))
                throw new ArgumentException("Ordinary delivery requires bounded unique exact form bindings.");
        }
        if (forms.Count == 0) throw new ArgumentException("Ordinary delivery must name at least one supported form.");
        DefinitionId = definitionId;
        RemedyDefinitionId = remedyDefinitionId;
        RemedyVersion = remedyVersion;
        DeliveryMode = deliveryMode;
        SupportedForms = Array.AsReadOnly(forms.Values.ToArray());
        ExplorationMilliseconds = explorationMilliseconds;
    }

    public EntityId DefinitionId { get; }
    public EntityId RemedyDefinitionId { get; }
    public ContentHash RemedyVersion { get; }
    public RestorativeDeliveryMode DeliveryMode { get; }
    public ReadOnlyCollection<RestorativeFormBinding> SupportedForms { get; }
    public ulong ExplorationMilliseconds { get; }
    /// <summary>The canonical digest of these immutable facts, encoded at the first read and then retained.</summary>
    public ContentHash Version
    {
        get
        {
            ContentHash? published = Volatile.Read(ref _version);
            if (published is not null)
            {
                return published;
            }

            ContentHash computed = ContentHash.Compute(CareCodecV2.EncodeUseDefinition(this));
            // Concurrent first readers may compute independently; publish one complete immutable digest.
            return Interlocked.CompareExchange(ref _version, computed, null) ?? computed;
        }
    }

    public bool SupportsForm(EntityId definitionId, ContentHash version) =>
        SupportedForms.Any(value => value.DefinitionId == definitionId && value.Version == version);
}
