using OpenFantasy.Core.Construction.Ownership;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>
/// An unpublished, authority-affine construction batch. Only its owning authority may load or publish
/// it. It holds no transaction or lock between calls and is not a durable preparation cursor.
/// </summary>
internal sealed class StoredConstructionPublication
{
    internal StoredConstructionPublication(SqliteWorldAuthority authority, StoredConstructionInterval source,
        WorldStepBatch batch, StoredConstructionPreparation preparation)
    {
        Authority = authority;
        Source = source;
        Batch = batch;
        Preparation = preparation;
    }

    internal SqliteWorldAuthority Authority { get; }
    internal StoredConstructionInterval Source { get; }
    internal WorldStepBatch Batch { get; }
    internal StoredConstructionPreparation Preparation { get; }
    internal int LoadedOwnerCount => Preparation.LoadedOwnerCount;
    internal int RequiredOwnerCount => Preparation.RequiredOwnerCount;
    internal bool IsReady => !IsRetired && Preparation.IsReady;
    internal bool IsRetired { get; private set; }
    internal void Retire() => IsRetired = true;
}
