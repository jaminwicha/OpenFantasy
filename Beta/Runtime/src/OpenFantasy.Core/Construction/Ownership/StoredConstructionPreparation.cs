using OpenFantasy.Core.Content;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Construction.Ownership;

internal sealed record StoredConstructionOwnerRead(ConstructionOwnerKey Key, ContentHash Hash, int ByteLength);

/// <summary>
/// Unpublished dependency loading for one exact selected batch. No slice earns time or acknowledges
/// an action. Completion requires all owners and the same admitted directory; failures cannot resume.
/// </summary>
public sealed class StoredConstructionPreparation
{
    private readonly StoredConstructionInterval _interval;
    private readonly WorldStepBatch _batch;
    private readonly TerrainScope _scope;
    private readonly StoredConstructionOwnerRead[] _reads;
    private readonly Func<EntityId, bool> _historicallyOccupied;
    private readonly Dictionary<ConstructionOwnerKey, byte[][]> _fields = [];
    private int _next;
    private long _bytes;
    private bool _unavailable;

    internal StoredConstructionPreparation(StoredConstructionInterval interval, WorldStepBatch batch,
        TerrainScope scope, StoredConstructionOwnerRead[] reads, Func<EntityId, bool> historicallyOccupied)
    {
        _interval = interval;
        _batch = batch;
        _scope = scope;
        _reads = reads;
        _historicallyOccupied = historicallyOccupied;
    }

    public int LoadedOwnerCount => _next;
    public int RequiredOwnerCount => _reads.Length;
    public bool IsReady => !_unavailable && _next == _reads.Length;

    /// <summary>Reads at most the supplied owner count; existing payload and aggregate byte limits still apply.</summary>
    public bool ReadNext(ContentHash currentDirectory, int maximumOwners, Func<ContentHash, byte[]> readOwner)
    {
        ArgumentNullException.ThrowIfNull(readOwner);
        if (maximumOwners < 1) throw new ArgumentOutOfRangeException(nameof(maximumOwners));
        RequireCurrent(currentDirectory);
        int stop = _next + Math.Min(maximumOwners, _reads.Length - _next);
        try
        {
            for (; _next < stop; _next++)
            {
                var read = _reads[_next];
                byte[] payload = readOwner(read.Hash) ?? throw new FormatException("A required work owner is missing.");
                _bytes = checked(_bytes + payload.Length);
                if (payload.Length != read.ByteLength || _bytes > ConstructionOwnerDirectory.MaximumTotalPayloadByteLength)
                    throw new FormatException("Required work owners differ from their admitted byte envelope.");
                _fields.Add(read.Key, StoredConstructionOwners.ReadFields(payload, read.Key, _scope, read.Hash));
            }
            return IsReady;
        }
        catch
        {
            _unavailable = true;
            throw;
        }
    }

    /// <summary>Prepares all selected actions and due effects once. Null requests complete ordinary admission.</summary>
    public StoredConstructionStep? Complete(ContentHash currentDirectory)
    {
        RequireCurrent(currentDirectory);
        if (!IsReady) throw new InvalidOperationException("Every mandatory dependency must be loaded before completing the boundary.");
        _unavailable = true;
        return _interval.PrepareLoaded(_batch, _fields, _historicallyOccupied);
    }

    private void RequireCurrent(ContentHash currentDirectory)
    {
        if (_unavailable) throw new InvalidOperationException("This preparation has completed or failed; capture a new one.");
        if (currentDirectory != _interval.DirectoryHash)
        {
            _unavailable = true;
            throw new InvalidOperationException("The admitted directory changed during preparation.");
        }
    }
}
