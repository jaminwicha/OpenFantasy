using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.NativeCargo;

namespace OpenFantasy.Hosting.Tests.Client.Cargo;

/// <summary>One fresh production cargo genesis shared by the read-only presentation tests.</summary>
internal static class CargoTestWorld
{
    private static readonly Lazy<WorldSnapshot> _genesis = new(() => NativeCargoWorld.CreateEmbedded("native-cargo-presentation", "Traveler"));

    internal static WorldSnapshot Genesis => _genesis.Value;

    internal static EntityId Player(WorldSnapshot world) =>
        world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;

    internal static CargoCircuitMarkets Markets(WorldSnapshot world) => CargoWorldAdmission.Find(world, NativeCargoWorld.Sources)!;

    internal static EntityId NewId() => EntityId.FromBytes(System.Security.Cryptography.RandomNumberGenerator.GetBytes(EntityId.ByteLength));
}
