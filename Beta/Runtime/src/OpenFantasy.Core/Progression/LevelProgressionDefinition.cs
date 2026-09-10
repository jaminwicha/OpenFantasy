using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Progression;

/// <summary>Finite pinned thresholds between levels. Residual experience remains retained at the last level.</summary>
public sealed class LevelProgressionDefinition
{
    public const int MaximumSupportedLevel = 256;

    public LevelProgressionDefinition(EntityId definitionId, string key, IEnumerable<ulong> thresholds)
    {
        ProgressionValidation.Text(key, 128, nameof(key));
        ArgumentNullException.ThrowIfNull(thresholds);
        List<ulong> values = [];
        foreach (ulong value in thresholds)
        {
            if (values.Count == MaximumSupportedLevel - 1 || value == 0)
            {
                throw new ArgumentException("Level thresholds must be bounded positive experience amounts.", nameof(thresholds));
            }
            values.Add(value);
        }
        DefinitionId = definitionId;
        Key = key;
        Thresholds = values.AsReadOnly();
        Version = ContentHash.Compute(ProgressionCodecV1.EncodeCurve(this));
    }

    public EntityId DefinitionId { get; }
    public string Key { get; }
    public ReadOnlyCollection<ulong> Thresholds { get; }
    public int MaximumLevel => Thresholds.Count + 1;
    public ContentHash Version { get; }

    public ulong RequiredExperience(int level)
    {
        if (level < 1 || level >= MaximumLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(level), "The last level has no next-level threshold.");
        }
        return Thresholds[level - 1];
    }
}
