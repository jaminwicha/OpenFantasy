namespace OpenFantasy.Hosting.Networking;

/// <summary>
/// A secret bearer credential delivered through a trusted operator channel. Export explicitly for
/// a protected handshake; never log it, put it in a URL, or infer authority from its structure.
/// </summary>
public sealed class PlayerJoinCredential
{
    public const int ByteLength = 32;
    private readonly byte[] _secret;

    internal PlayerJoinCredential(ReadOnlySpan<byte> secret)
    {
        if (secret.Length != ByteLength) throw new ArgumentException("Invalid credential length.", nameof(secret));
        _secret = secret.ToArray();
    }

    public byte[] Export() => _secret.ToArray();
    public override string ToString() => "[player credential]";
}
