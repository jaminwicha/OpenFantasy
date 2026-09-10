using System.Text;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Creates the exact supported farm genesis from installed, hash-pinned content.</summary>
public static class FarmWorldAdmission
{
    public const int MaximumSeedByteLength = 256;
    public const int MaximumPlayerNameByteLength = 128;
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    /// <summary>
    /// Builds a deterministic candidate without creating a save or replacing committed state.
    /// Content root is the directory containing packages, legacy and native-farm.
    /// Seed and player name are exact UTF-8 text, never silently normalized.
    /// </summary>
    public static WorldSnapshot Create(string contentRoot, string seedText, string playerName)
    {
        byte[] seed = ValidatedText(seedText, MaximumSeedByteLength, nameof(seedText));
        ValidatedText(playerName, MaximumPlayerNameByteLength, nameof(playerName));
        return Create(new FarmContentFiles(contentRoot), seed, playerName);
    }

    /// <summary>Uses the same exact content pins from readonly assembly resources, including packaged exports.</summary>
    public static WorldSnapshot CreateEmbedded(string seedText, string playerName)
    {
        byte[] seed = ValidatedText(seedText, MaximumSeedByteLength, nameof(seedText));
        ValidatedText(playerName, MaximumPlayerNameByteLength, nameof(playerName));
        return Create(FarmContentFiles.Embedded(), seed, playerName);
    }

    private static WorldSnapshot Create(FarmContentFiles files, byte[] seed, string playerName)
    {
        FarmAdmissionDefinition definition = FarmAdmissionDefinition.Load(files);
        FarmSourcePacket source = FarmSourcePacket.Load(files, definition.Root);
        definition.ValidateSources(source);
        return new FarmWorldBuilder(definition, source, seed).Build(playerName);
    }

    private static byte[] ValidatedText(string value, int maximumBytes, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > maximumBytes || value.Any(char.IsControl))
        {
            throw new ArgumentException("Farm input text exceeds its bound or contains control characters.", parameterName);
        }
        byte[] bytes = _strictUtf8.GetBytes(value);
        if (bytes.Length > maximumBytes)
        {
            throw new ArgumentException("Farm input text exceeds its UTF-8 byte bound.", parameterName);
        }
        return bytes;
    }
}
