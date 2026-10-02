using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;
using OpenFantasy.Hosting.Simulation.Retreat;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Networking;

/// <summary>Owns an existing admitted cargo world and its player admission. It never creates, upgrades or resets a save.</summary>
public sealed class CargoPlayerHost : IAsyncDisposable
{
    private readonly Dictionary<EntityId, TrustedCommandDelivery> _players;

    private CargoPlayerHost(LocalWorldSession session, WorldSnapshot state)
    {
        Session = session;
        _players = state.Residents.Where(actor => actor.ControllerKind == ResidentControllerKind.Player)
            .ToDictionary(actor => actor.ActorId, actor => DeliveryOf(state, actor.ActorId));
        Players = Array.AsReadOnly(_players.Keys.Order().ToArray());
        Admission = new PlayerAdmissionService(session, FarmPackageCatalog.LoadEmbedded());
    }

    public LocalWorldSession Session { get; }
    public PlayerAdmissionService Admission { get; }
    public ReadOnlyCollection<EntityId> Players { get; }

    public static Task<CargoPlayerHost> OpenExistingAsync(string database, CancellationToken cancellationToken = default) =>
        Task.Run(async () =>
        {
            if (!File.Exists(database)) throw new FileNotFoundException("An existing cargo world is required.", database);
            SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(database);
            LocalWorldSession? session = null;
            try
            {
                WorldSnapshot state = authority.ReadStateForHost();
                if (CargoOutcomeWorldAdmission.Find(state, NativeCargoWorld.Sources, RoadThreatSources.LoadEmbedded()) is null)
                    throw new InvalidDataException("The dedicated cargo host requires the admitted outcome-completion cargo world.");
                EntityId[] players = state.Residents.Where(actor => actor.ControllerKind == ResidentControllerKind.Player)
                    .Select(actor => actor.ActorId).Order().ToArray();
                if (players.Length is < 1 or > LocalWorldSession.MaximumPlayerConnections)
                    throw new InvalidDataException("The saved world must contain one to four admitted human players.");
                cancellationToken.ThrowIfCancellationRequested();
                session = new LocalWorldSession(authority, players[0], DeliveryOf(state, players[0]),
                    Array.Empty<LocalMaterialGiftPolicy>(), NativeCargoWorld.Configuration(state),
                    new LocalRegionObserver(players[0]), LocalRetreatConfiguration.None);
                var host = new CargoPlayerHost(session, state);
                await session.Player.DisposeAsync().ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return host;
            }
            catch
            {
                if (session is not null) await session.DisposeAsync().ConfigureAwait(false);
                else authority.Dispose();
                throw;
            }
        }, cancellationToken);

    public PlayerJoinCredential Issue(EntityId actor, TimeSpan lifetime)
    {
        if (!_players.TryGetValue(actor, out TrustedCommandDelivery? delivery)) throw new PlayerAdmissionException();
        return Admission.Issue(actor, delivery, lifetime, new LocalRegionObserver(actor));
    }

    private static TrustedCommandDelivery DeliveryOf(WorldSnapshot state, EntityId actor)
    {
        var control = state.Inventory.Controls.Single(value => value.ActorId == actor);
        return new(control.PrincipalId, control.SessionId);
    }

    public ValueTask DisposeAsync() => Session.DisposeAsync();
}
