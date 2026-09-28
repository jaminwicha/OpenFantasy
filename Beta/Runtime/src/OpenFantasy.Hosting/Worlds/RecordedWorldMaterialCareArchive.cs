using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Retrieved immutable archive evidence bound to its verified publication and source head.</summary>
public sealed class RecordedWorldMaterialCareArchive
{
    internal RecordedWorldMaterialCareArchive(WorldMaterialCareArchiveSegment segment, ulong publicationSequence,
        ulong sourceHeadRevision, ContentHash sourceHeadHash)
    {
        Segment = segment;
        PublicationSequence = publicationSequence;
        SourceHeadRevision = sourceHeadRevision;
        SourceHeadHash = sourceHeadHash;
    }

    public WorldMaterialCareArchiveSegment Segment { get; }
    public ulong PublicationSequence { get; }
    public ulong SourceHeadRevision { get; }
    public ContentHash SourceHeadHash { get; }
}
