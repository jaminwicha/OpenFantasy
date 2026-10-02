using OpenFantasy.Core;

namespace OpenFantasy.Hosting.Networking;

/// <summary>A bounded projection update. Revisions describe visible bytes, never private world commits.</summary>
public sealed class PlayerObservationFrame
{
    private readonly byte[] _replacement;

    internal PlayerObservationFrame(EntityId streamId, ulong revision, ulong baseRevision, int resultLength,
        int prefixLength, int suffixLength, ReadOnlySpan<byte> replacement)
    {
        StreamId = streamId;
        Revision = revision;
        BaseRevision = baseRevision;
        ResultLength = resultLength;
        PrefixLength = prefixLength;
        SuffixLength = suffixLength;
        _replacement = replacement.ToArray();
    }

    public EntityId StreamId { get; }
    public ulong Revision { get; }
    public ulong BaseRevision { get; }
    public bool IsFull => BaseRevision == 0;
    public int ResultLength { get; }
    public int PrefixLength { get; }
    public int SuffixLength { get; }
    public ReadOnlySpan<byte> Replacement => _replacement;

    internal byte[] Reconstruct(ReadOnlySpan<byte> basis)
    {
        if (IsFull) return _replacement.ToArray();
        if ((long)PrefixLength + SuffixLength > basis.Length)
            throw new FormatException("The observation delta exceeds its basis.");
        byte[] result = new byte[ResultLength];
        basis[..PrefixLength].CopyTo(result);
        _replacement.CopyTo(result, PrefixLength);
        basis[(basis.Length - SuffixLength)..].CopyTo(result.AsSpan(ResultLength - SuffixLength));
        return result;
    }
}
