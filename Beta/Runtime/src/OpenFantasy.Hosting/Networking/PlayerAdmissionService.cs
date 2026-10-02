using System.Security.Cryptography;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking;

/// <summary>
/// Host-owned, bounded bearer admission for already-admitted player/control bindings. Transport
/// protection and trusted credential delivery are caller obligations. No client-supplied identity
/// creates authority, and no online account, resident or saved control assignment is created here.
/// </summary>
public sealed class PlayerAdmissionService
{
    public const int MaximumPendingHandshakes = 4;
    public static readonly TimeSpan HandshakeLimit = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan MaximumCredentialLifetime = TimeSpan.FromHours(24);
    private readonly object _gate = new();
    private readonly LocalWorldSession _host;
    private readonly TimeProvider _time;
    private readonly List<CredentialBinding> _credentials = [];
    private int _accepting;

    /// <param name="admittedPackages">The host's already admitted package set. Manifest resolution alone does not validate payloads.</param>
    public PlayerAdmissionService(LocalWorldSession host, IEnumerable<PackageManifest> admittedPackages, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
        _time = time ?? TimeProvider.System;
        Compatibility = host.DescribePlayerCompatibility(PlayerWorldCompatibility.ResolvePackages(admittedPackages));
    }

    public PlayerWorldCompatibility Compatibility { get; }

    /// <summary>Observes the owning world's lifetime without granting access to its authority or disposal.</summary>
    public Task HostCompletion => _host.Completion;

    /// <summary>Privileged operator operation. Reissuing invalidates an inactive actor's earlier credential.</summary>
    public PlayerJoinCredential Issue(EntityId actorId, TrustedCommandDelivery delivery, TimeSpan lifetime,
        Simulation.CargoPresentation.LocalRegionObserver? regions = null, PlayerPresentationBootstrap? presentation = null)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        if (lifetime <= TimeSpan.Zero || lifetime > MaximumCredentialLifetime) throw new ArgumentOutOfRangeException(nameof(lifetime));
        if (regions is not null && regions.PlayerActorId != actorId) throw new ArgumentException("A regional observer must belong to the credential's actor.", nameof(regions));
        if (presentation is not null && presentation.IsRegional != (regions is not null))
            throw new ArgumentException("Presentation geography must match its admitted regional observer.", nameof(presentation));
        _host.ValidatePlayerCredentialBinding(actorId, delivery, Compatibility);
        lock (_gate)
        {
            _credentials.RemoveAll(binding => !binding.Active && !IsValid(binding));
            CredentialBinding? previous = _credentials.Find(binding => binding.ActorId == actorId);
            if (previous?.Active == true) throw new PlayerAdmissionException();
            if (previous is not null) _credentials.Remove(previous);
            if (_credentials.Count == LocalWorldSession.MaximumPlayerConnections) throw new PlayerAdmissionException();
            byte[] secret = RandomNumberGenerator.GetBytes(PlayerJoinCredential.ByteLength);
            var credential = new PlayerJoinCredential(secret);
            _credentials.Add(new CredentialBinding(actorId, delivery, SHA256.HashData(secret), _time.GetTimestamp(), lifetime, regions, presentation));
            CryptographicOperations.ZeroMemory(secret);
            return credential;
        }
    }

    /// <summary>Stops future admission. It does not silently alter a connected actor's saved control or kick its current port.</summary>
    public void Revoke(EntityId actorId)
    {
        lock (_gate)
        {
            CredentialBinding? binding = _credentials.Find(value => value.ActorId == actorId);
            if (binding is not null) binding.Revoked = true;
        }
    }

    /// <summary>
    /// Takes ownership of the supplied protected transport. Returns an unready player after Welcome;
    /// on any failure closes the transport and any newly created port. The deadline includes Hello,
    /// actor admission and Welcome, and is never renewed by input or transport progress.
    /// </summary>
    public async Task<AuthenticatedPlayerChannel> AcceptAsync(PlayerMessageTransport transport, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transport);
        CredentialBinding? binding = null;
        LocalPlayerConnection? player = null;
        bool entered = false;
        long started = _time.GetTimestamp();
        using var deadline = new CancellationTokenSource(HandshakeLimit, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            lock (_gate)
            {
                if (_accepting == MaximumPendingHandshakes) throw new PlayerAdmissionException();
                _accepting++;
                entered = true;
            }
            byte[] bytes = await transport.ReceiveAsync(linked.Token).ConfigureAwait(false)
                ?? throw new PlayerAdmissionException();
            CheckDeadline(started, linked.Token);
            if (bytes.Length != PlayerMessageCodec.HeaderBytes + PlayerHandshakeCodec.HelloBytes)
                throw new PlayerAdmissionException();
            PlayerMessage hello = PlayerMessageCodec.Decode(bytes);
            if (PlayerHandshakeCodec.ReadHello(hello) != Compatibility) throw new PlayerAdmissionException();
            byte[] credentialHash = SHA256.HashData(hello.Payload[PlayerHandshakeCodec.CompatibilityBytes..]);
            lock (_gate)
            {
                binding = _credentials.Find(value => CryptographicOperations.FixedTimeEquals(value.Hash, credentialHash));
                if (binding is null || binding.Active || !IsValid(binding)) { binding = null; throw new PlayerAdmissionException(); }
                binding.Active = true;
            }
            player = await _host.ConnectNetworkPlayerAsync(binding.ActorId, binding.Delivery, Compatibility, linked.Token, binding.Regions).ConfigureAwait(false);
            CheckDeadline(started, linked.Token);
            lock (_gate) { if (!IsValid(binding)) throw new PlayerAdmissionException(); }
            PlayerMessage welcome = PlayerHandshakeCodec.Welcome(hello.RequestId, Compatibility, binding.ActorId, binding.Delivery);
            await transport.SendAsync(PlayerMessageCodec.Encode(welcome), linked.Token).ConfigureAwait(false);
            CheckDeadline(started, linked.Token);
            if (player.ReadRunState() == LocalWorldRunState.Unavailable) throw new PlayerAdmissionException();
            lock (_gate) { if (!IsValid(binding)) throw new PlayerAdmissionException(); }
            CredentialBinding accepted = binding;
            return new AuthenticatedPlayerChannel(player, transport, () => Release(accepted), binding.Presentation);
        }
        catch
        {
            try { await transport.DisposeAsync().ConfigureAwait(false); }
            finally
            {
                try { if (player is not null) await player.DisposeAsync().ConfigureAwait(false); }
                finally { if (binding is not null) Release(binding); }
            }
            throw;
        }
        finally { if (entered) { lock (_gate) { _accepting--; } } }
    }

    private bool IsValid(CredentialBinding binding) => !binding.Revoked
        && _time.GetElapsedTime(binding.IssuedAt, _time.GetTimestamp()) < binding.Lifetime;

    private void Release(CredentialBinding binding)
    {
        lock (_gate) { binding.Active = false; }
    }

    private void CheckDeadline(long started, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_time.GetElapsedTime(started, _time.GetTimestamp()) >= HandshakeLimit)
            throw new OperationCanceledException("The player handshake exceeded its deadline.", cancellationToken);
    }

    private sealed class CredentialBinding(EntityId actorId, TrustedCommandDelivery delivery, byte[] hash, long issuedAt, TimeSpan lifetime,
        Simulation.CargoPresentation.LocalRegionObserver? regions, PlayerPresentationBootstrap? presentation)
    {
        internal EntityId ActorId { get; } = actorId;
        internal TrustedCommandDelivery Delivery { get; } = delivery;
        internal byte[] Hash { get; } = hash;
        internal long IssuedAt { get; } = issuedAt;
        internal TimeSpan Lifetime { get; } = lifetime;
        internal Simulation.CargoPresentation.LocalRegionObserver? Regions { get; } = regions;
        internal PlayerPresentationBootstrap? Presentation { get; } = presentation;
        internal bool Revoked { get; set; }
        internal bool Active { get; set; }
    }
}

/// <summary>A transient connection refusal, never a world receipt or disclosure of credential details.</summary>
public sealed class PlayerAdmissionException() : Exception("The player connection was not admitted.");
