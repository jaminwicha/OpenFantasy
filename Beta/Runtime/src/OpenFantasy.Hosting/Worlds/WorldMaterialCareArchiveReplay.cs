using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Matches every retrieved archive to the exact closing root proven by journal replay.</summary>
internal sealed class WorldMaterialCareArchiveReplay : IDisposable
{
    private readonly IEnumerator<StoredWorldMaterialCareArchive> _rows;
    private readonly ulong? _selectedOrdinal;
    private readonly EntityId? _selectedProposalId;
    private WorldMaterialCareArchiveAnchor? _previousAnchor;
    private bool _hasRow;

    internal WorldMaterialCareArchiveReplay(WorldDatabase database, ulong? selectedOrdinal,
        EntityId? selectedProposalId = null)
    {
        _rows = database.ReadMaterialCareArchives().GetEnumerator();
        _selectedOrdinal = selectedOrdinal;
        _selectedProposalId = selectedProposalId;
        _hasRow = _rows.MoveNext();
    }

    internal WorldMaterialCareArchiveSegment? Selected { get; private set; }
    internal ulong SelectedPublicationSequence { get; private set; }

    /// <summary>The selected proposal's terminal outcome, found in the one retrieved root that closed it.</summary>
    internal WorldMaterialProposalOutcome? SelectedProposalOutcome { get; private set; }

    /// <param name="closing">The replayed closing root whose canonical bytes are <paramref name="closingBytes"/>.</param>
    internal void Verify(WorldMaterialCareArchiveSegment? expected, StoredWorldCommit publication, byte[] closingBytes,
        WorldSnapshot closing)
    {
        if (expected is not null)
        {
            if (!_hasRow)
            {
                throw new InvalidDataException("The world is missing a required material and care archive.");
            }
            StoredWorldMaterialCareArchive stored = _rows.Current;
            if (stored.Reference != expected.Reference || stored.PublicationSequence != publication.Sequence
                || checked(stored.Reference.BoundarySequence + 1) != publication.Sequence
                || stored.Reference.BoundaryHash != publication.BeforeHash
                || !stored.State.AsSpan().SequenceEqual(closingBytes)
                || !stored.State.AsSpan().SequenceEqual(expected.Encode()))
            {
                throw new InvalidDataException("A stored material and care archive differs from its exact replay boundary.");
            }
            // Decode the retrieved bytes. A regenerated seal or a matching hash column is not retrieval.
            WorldMaterialCareArchiveSegment retrieved = WorldMaterialCareArchiveSegment.Decode(
                stored.Reference, stored.State, _previousAnchor);
            _previousAnchor = retrieved.Anchor;
            if (stored.Reference.Ordinal == _selectedOrdinal)
            {
                Selected = retrieved;
                SelectedPublicationSequence = stored.PublicationSequence;
            }
            SelectProposalOutcome(closing);
            _hasRow = _rows.MoveNext();
        }
        if (_hasRow && _rows.Current.PublicationSequence <= publication.Sequence)
        {
            throw new InvalidDataException("The world contains an unscheduled material and care archive.");
        }
    }

    // The retrieved bytes were just proven identical to this replayed root, so its owner is the
    // archived owner. A closed proposal leaves the live segment at exactly one cut.
    private void SelectProposalOutcome(WorldSnapshot closing)
    {
        if (_selectedProposalId is not { } proposalId
            || closing.Coordination?.Proposals?.FindOutcome(proposalId) is not { } outcome)
        {
            return;
        }
        if (SelectedProposalOutcome is not null)
        {
            throw new InvalidDataException("A proposal has more than one archived terminal outcome.");
        }
        SelectedProposalOutcome = outcome;
    }

    internal void Complete()
    {
        if (_hasRow)
        {
            throw new InvalidDataException("The world contains a material and care archive beyond its accepted journal.");
        }
    }

    public void Dispose() => _rows.Dispose();
}
