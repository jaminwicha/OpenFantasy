using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Worlds;

/// <summary>An explicit selection of owned ingredients for one pinned instantaneous operation.</summary>
public sealed record CraftIntent : WorldActionIntent
{
    public CraftIntent(CraftRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Request = request;
    }

    public CraftRequest Request { get; }
}
