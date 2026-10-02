using System.Text.Json;
using System.Text.Json.Serialization;
using OpenFantasy.Core;

namespace OpenFantasy.Hosting.Networking;

/// <summary>Bounded canonical private-file encoding. Encoded bytes contain a secret and must never enter ordinary logs.</summary>
public static class PlayerInvitationCodec
{
    public const int Version = 1;
    public const int MaximumBytes = 4096;
    private static readonly JsonSerializerOptions Options = new()
    {
        MaxDepth = 8,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static byte[] Encode(PlayerConnectionInvitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        var body = new InvitationBody(Version, invitation.Endpoint.AbsoluteUri, invitation.Compatibility,
            invitation.ActorId, Convert.ToHexString(invitation.ExportCredential()).ToLowerInvariant());
        if (body.Endpoint.Length > 1024) throw new FormatException("Invitation endpoint is too long.");
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(body, Options);
        if (bytes.Length > MaximumBytes) throw new FormatException("Invitation exceeds its byte bound.");
        return bytes;
    }

    public static PlayerConnectionInvitation Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            if (bytes.IsEmpty || bytes.Length > MaximumBytes) throw new FormatException("Invalid invitation byte length.");
            InvitationBody body = JsonSerializer.Deserialize<InvitationBody>(bytes, Options)
                ?? throw new FormatException("Missing invitation.");
            if (body.Version != Version || body.Endpoint is null || body.Endpoint.Length > 1024
                || body.Compatibility is null || body.Credential is null || body.Credential.Length != PlayerJoinCredential.ByteLength * 2)
                throw new FormatException("Incomplete or unsupported invitation.");
            var invitation = new PlayerConnectionInvitation(new Uri(body.Endpoint, UriKind.Absolute),
                body.Compatibility, body.ActorId, Convert.FromHexString(body.Credential));
            if (!bytes.SequenceEqual(Encode(invitation))) throw new FormatException("Invitation must use its complete canonical form.");
            return invitation;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new FormatException("Invalid player invitation.", error);
        }
    }

    private sealed record InvitationBody(int Version, string Endpoint, PlayerWorldCompatibility Compatibility,
        EntityId ActorId, string Credential);
}
