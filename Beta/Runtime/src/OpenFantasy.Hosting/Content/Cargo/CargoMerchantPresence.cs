using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content.Cargo;

/// <summary>
/// The corporeal presence a retained merchant receives at its own source tile: the world's shared admitted
/// humanoid anatomy (no new body calibration), a home-place identity derived from its retained source place
/// and identities derived from the world and branch. It grants no stock, money, control or utterance; those
/// belong to the owners a complete fresh admission binds.
/// </summary>
public sealed class CargoMerchantPresence
{
    private const string IdentityProfile = "cargo-world";

    private CargoMerchantPresence(CargoSourceMerchant source, ActorPresence presence)
    {
        Source = source;
        Presence = presence;
    }

    public CargoSourceMerchant Source { get; }
    public ActorPresence Presence { get; }
    public EntityId ActorId => Presence.ActorId;

    /// <summary>Places the merchant at its source tile in the world's scope, using the body of an existing humanoid.</summary>
    public static CargoMerchantPresence AtSourceTile(WorldSnapshot world, CargoSourceMerchant source, EntityId bodySourceActorId,
        long feetZ)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(source);
        BodyDefinition body = world.Space.GetActor(bodySourceActorId).Condition.Definition;
        EntityId actor = Identity(world, source.SourceId + ".actor");
        ActorFormBinding binding = ActorFormBinding.Corporeal(actor, Identity(world, source.SourceId + ".form"),
            Identity(world, source.SourceId + ".physical-body"), body);
        var condition = new BodyConditionSnapshot(binding.FormInstanceId, body, []);
        SubunitPosition feet = CargoCorridor.FeetAt(new CellCoordinate(source.TileX, source.TileY, feetZ / SubunitPosition.SubunitsPerCell - 1));
        var presence = new ActorPresence(binding, condition, world.Space.Scope, feet, 0, PlaceIdentity(world, source.Place));
        return new CargoMerchantPresence(source, presence);
    }

    /// <summary>The home-place identity of a retained source place in this world.</summary>
    public static EntityId PlaceIdentity(WorldSnapshot world, CargoSourcePlace place) =>
        Identity(world, "place." + place.SourceId);

    private static EntityId Identity(WorldSnapshot world, string role) =>
        ConstructionFarmIdentityScope.InstanceId(world, IdentityProfile, role);
}
