using OpenFantasy.Core;

namespace OpenFantasy.Hosting.Networking;

/// <summary>A private operator-delivered invitation. Possession of its secret is required by the live host's admission.</summary>
public sealed class PlayerConnectionInvitation
{
    private readonly byte[] _credential;

    public PlayerConnectionInvitation(Uri endpoint, PlayerWorldCompatibility compatibility, EntityId actorId,
        ReadOnlySpan<byte> credential)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(compatibility);
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != "wss" || endpoint.AbsolutePath != "/player"
            || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0
            || string.IsNullOrEmpty(endpoint.Host) || endpoint.Port is < 1 or > 65535)
            throw new ArgumentException("A player invitation requires a TLS player endpoint without embedded secrets.", nameof(endpoint));
        if (credential.Length != PlayerJoinCredential.ByteLength)
            throw new ArgumentException("A player invitation requires one complete join credential.", nameof(credential));
        Endpoint = endpoint;
        Compatibility = compatibility;
        ActorId = actorId;
        _credential = credential.ToArray();
    }

    public Uri Endpoint { get; }
    public PlayerWorldCompatibility Compatibility { get; }
    public EntityId ActorId { get; }
    public byte[] ExportCredential() => _credential.ToArray();
    public override string ToString() => "[private player invitation]";
}
