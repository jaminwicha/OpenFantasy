using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Matches every retrieved archive to the exact closing root proven by journal replay.</summary>
internal sealed class WorldResourceArchiveReplay : IDisposable
{
    private readonly IEnumerator<StoredWorldResourceArchive> _rows;
    private readonly ulong? _selectedOrdinal;
    private WorldResourceArchiveAnchor? _previousAnchor;
    private bool _hasRow;

    internal WorldResourceArchiveReplay(WorldDatabase database, ulong? selectedOrdinal)
    {
        _rows = database.ReadResourceArchives().GetEnumerator();
        _selectedOrdinal = selectedOrdinal;
        _hasRow = _rows.MoveNext();
    }

    internal WorldResourceArchiveSegment? Selected { get; private set; }
    internal ulong SelectedPublicationSequence { get; private set; }

    internal void Verify(WorldResourceArchiveSegment? expected, StoredWorldCommit publication, byte[] closingBytes)
    {
        if (expected is not null)
        {
            if (!_hasRow)
            {
                throw new InvalidDataException("The world is missing a required resource archive.");
            }
            StoredWorldResourceArchive stored = _rows.Current;
            if (stored.Reference != expected.Reference || stored.PublicationSequence != publication.Sequence
                || checked(stored.Reference.BoundarySequence + 1) != publication.Sequence
                || stored.Reference.BoundaryHash != publication.BeforeHash
                || !stored.State.AsSpan().SequenceEqual(closingBytes)
                || !stored.State.AsSpan().SequenceEqual(expected.Encode()))
            {
                throw new InvalidDataException("A stored resource archive differs from its exact replay boundary.");
            }
            // Decode the retrieved bytes. A regenerated seal or a matching hash column is not retrieval.
            WorldResourceArchiveSegment retrieved = WorldResourceArchiveSegment.Decode(
                stored.Reference, stored.State, _previousAnchor);
            _previousAnchor = retrieved.Anchor;
            if (stored.Reference.Ordinal == _selectedOrdinal)
            {
                Selected = retrieved;
                SelectedPublicationSequence = stored.PublicationSequence;
            }
            _hasRow = _rows.MoveNext();
        }
        if (_hasRow && _rows.Current.PublicationSequence <= publication.Sequence)
        {
            throw new InvalidDataException("The world contains an unscheduled resource archive.");
        }
    }

    internal void Complete()
    {
        if (_hasRow)
        {
            throw new InvalidDataException("The world contains a resource archive beyond its accepted journal.");
        }
    }

    public void Dispose() => _rows.Dispose();
}
