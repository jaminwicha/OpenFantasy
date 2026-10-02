using System.Buffers.Binary;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Hosting.Networking;

/// <summary>Exact bounded admission payloads carried inside the versioned player envelope.</summary>
public static class PlayerHandshakeCodec
{
    public const int CompatibilityBytes = 98;
    public const int HelloBytes = CompatibilityBytes + PlayerJoinCredential.ByteLength;
    public const int WelcomeBytes = CompatibilityBytes + EntityId.ByteLength * 3;

    public static PlayerMessage Hello(ulong requestId, PlayerWorldCompatibility compatibility, ReadOnlySpan<byte> credential)
    {
        if (requestId == 0) throw new ArgumentOutOfRangeException(nameof(requestId));
        if (credential.Length != PlayerJoinCredential.ByteLength) throw new ArgumentException("Invalid credential length.", nameof(credential));
        byte[] payload = new byte[HelloBytes];
        WriteCompatibility(compatibility, payload);
        credential.CopyTo(payload.AsSpan(CompatibilityBytes));
        return new PlayerMessage(PlayerMessageKind.Hello, requestId, payload);
    }

    public static PlayerWelcome ReadWelcome(PlayerMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Kind != PlayerMessageKind.Welcome || message.RequestId == 0 || message.Payload.Length != WelcomeBytes)
            throw new FormatException("Invalid player welcome.");
        ReadOnlySpan<byte> payload = message.Payload;
        return new PlayerWelcome(ReadCompatibility(payload[..CompatibilityBytes]),
            EntityId.FromBytes(payload.Slice(98, 16)),
            new TrustedCommandDelivery(EntityId.FromBytes(payload.Slice(114, 16)), EntityId.FromBytes(payload.Slice(130, 16))));
    }

    internal static PlayerMessage Welcome(ulong requestId, PlayerWorldCompatibility compatibility,
        EntityId actorId, TrustedCommandDelivery delivery)
    {
        byte[] payload = new byte[WelcomeBytes];
        WriteCompatibility(compatibility, payload);
        actorId.WriteBytes(payload.AsSpan(98));
        delivery.PrincipalId.WriteBytes(payload.AsSpan(114));
        delivery.SessionId.WriteBytes(payload.AsSpan(130));
        return new PlayerMessage(PlayerMessageKind.Welcome, requestId, payload);
    }

    internal static PlayerWorldCompatibility ReadHello(PlayerMessage message)
    {
        if (message.Kind != PlayerMessageKind.Hello || message.RequestId == 0 || message.Payload.Length != HelloBytes)
            throw new FormatException("Invalid player hello.");
        return ReadCompatibility(message.Payload[..CompatibilityBytes]);
    }

    private static void WriteCompatibility(PlayerWorldCompatibility compatibility, Span<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(compatibility);
        compatibility.WorldId.WriteBytes(bytes);
        compatibility.BranchId.WriteBytes(bytes[16..]);
        BinaryPrimitives.WriteUInt16BigEndian(bytes[32..], compatibility.ProfileVersion);
        compatibility.AdmissionVersion.ToByteArray().CopyTo(bytes[34..]);
        compatibility.PackagesVersion.ToByteArray().CopyTo(bytes[66..]);
    }

    private static PlayerWorldCompatibility ReadCompatibility(ReadOnlySpan<byte> bytes)
    {
        ushort profile = BinaryPrimitives.ReadUInt16BigEndian(bytes[32..]);
        if (profile == 0) throw new FormatException("Invalid player profile.");
        return new PlayerWorldCompatibility(EntityId.FromBytes(bytes[..16]), EntityId.FromBytes(bytes.Slice(16, 16)), profile,
            new ContentHash(Convert.ToHexString(bytes.Slice(34, 32)).ToLowerInvariant()),
            new ContentHash(Convert.ToHexString(bytes.Slice(66, 32)).ToLowerInvariant()));
    }
}

/// <summary>The server-selected binding; reading it alone does not authenticate the server.</summary>
public sealed record PlayerWelcome(PlayerWorldCompatibility Compatibility, EntityId ActorId, TrustedCommandDelivery Delivery);
