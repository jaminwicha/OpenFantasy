using System.Security.Cryptography;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking;

/// <summary>Single producer's visible history; private root identity and commit counts never enter it.</summary>
public sealed class PlayerObservationStream
{
    private readonly EntityId _streamId = EntityId.FromBytes(RandomNumberGenerator.GetBytes(EntityId.ByteLength));
    private byte[]? _previous;
    private LocalWorldView? _lastView;
    private ulong _revision;

    public PlayerObservationFrame? Capture(LocalWorldView view, bool forceFull = false)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (ReferenceEquals(view, _lastView) && !forceFull) return null;
        // An older projected observation cannot assert a complete empty structure set.
        // Keep its omitted facet while fresh Host projections publish the complete construction view.
        byte[] current = PlayerViewCodec.EncodeWithCarePermissionDescriptions(view);
        bool unchanged = _previous is not null && current.AsSpan().SequenceEqual(_previous);
        if (unchanged && !forceFull)
        {
            _lastView = view;
            return null;
        }
        ulong revision = unchanged ? _revision : checked(_revision + 1);
        int prefix = 0, suffix = 0;
        if (_previous is not null && !forceFull)
        {
            int sharedLength = Math.Min(current.Length, _previous.Length);
            while (prefix < sharedLength && current[prefix] == _previous[prefix]) prefix++;
            while (suffix < sharedLength - prefix && current[current.Length - 1 - suffix] == _previous[_previous.Length - 1 - suffix]) suffix++;
        }
        bool full = _previous is null || forceFull || prefix + suffix == 0;
        var frame = new PlayerObservationFrame(_streamId, revision, full ? 0 : _revision, current.Length,
            prefix, suffix, current.AsSpan(prefix, current.Length - prefix - suffix));
        _previous = current;
        _revision = revision;
        _lastView = view;
        return frame;
    }
}
