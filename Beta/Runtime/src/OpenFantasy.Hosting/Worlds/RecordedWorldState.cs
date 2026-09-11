using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>
/// Immutable state at one exact recorded commit, bound to the verified source head at preparation.
/// This privileged result grants neither player visibility nor authority to travel or fork a world.
/// </summary>
public sealed class RecordedWorldState
{
    internal RecordedWorldState(WorldSnapshot state, ulong sourceHeadRevision, ContentHash sourceHeadHash)
    {
        State = state;
        SourceHeadRevision = sourceHeadRevision;
        SourceHeadHash = sourceHeadHash;
    }

    public WorldSnapshot State { get; }
    public ContentHash StateHash => State.ContentHash;
    public ulong SourceHeadRevision { get; }
    public ContentHash SourceHeadHash { get; }
}
