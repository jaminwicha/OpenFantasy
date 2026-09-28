using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>
/// Detached observations reconstructed from one recorded commit after complete journal verification.
/// Head pins identify the verified source; they do not grant player knowledge or material authority.
/// No database, world root, archive anchor or history chain is retained.
/// </summary>
public sealed class RecordedWorldMaterialEvidence
{
    internal RecordedWorldMaterialEvidence(ulong sequence, ContentHash beforeHash, ContentHash afterHash,
        IEnumerable<WorldMaterialActionEvidence> evidence, ulong sourceHeadRevision, ContentHash sourceHeadHash)
    {
        Sequence = sequence;
        BeforeHash = beforeHash;
        AfterHash = afterHash;
        Evidence = Array.AsReadOnly(evidence.ToArray());
        SourceHeadRevision = sourceHeadRevision;
        SourceHeadHash = sourceHeadHash;
    }

    public ulong Sequence { get; }
    public ContentHash BeforeHash { get; }
    public ContentHash AfterHash { get; }
    public ReadOnlyCollection<WorldMaterialActionEvidence> Evidence { get; }
    public ulong SourceHeadRevision { get; }
    public ContentHash SourceHeadHash { get; }
}
