namespace OpenFantasy.Core.Worlds;

/// <summary>Reads only a bounded exact frame tag and version before choosing its retained interpreter.</summary>
internal static class WorldCodecVersion
{
    internal static ulong Read(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> tag, int maximumBytes, int maximumFields)
    {
        var reader = new WorldFrameReader(bytes, maximumBytes, maximumFields);
        reader.Tag(tag);
        return reader.Number();
    }
}
