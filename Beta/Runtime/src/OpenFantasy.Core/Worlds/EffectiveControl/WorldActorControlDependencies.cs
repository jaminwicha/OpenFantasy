using OpenFantasy.Core.Authority;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Exact current-control dependencies from an admitted world. This is neither transport authentication
/// nor permission to publish movement or complete previously accepted work.
/// </summary>
public sealed class WorldActorControlDependencies
{
    private readonly Selection _selection;

    private WorldActorControlDependencies(WorldSnapshot world, EntityId actorId, TrustedCommandDelivery delivery,
        WorldControlWitness? witness, WorldControlCapabilities required)
    {
        ActorId = actorId;
        Delivery = delivery;
        Witness = witness;
        RequiredCapabilities = required;
        _selection = Read(world, actorId, witness);
    }

    public EntityId ActorId { get; }
    public TrustedCommandDelivery Delivery { get; }
    public WorldControlWitness? Witness { get; }
    public WorldControlCapabilities RequiredCapabilities { get; }
    public bool IsAuthorized => _selection.Allows(ActorId, Delivery, Witness, RequiredCapabilities);

    public static WorldActorControlDependencies Capture(WorldSnapshot world, EntityId actorId,
        TrustedCommandDelivery delivery, WorldControlWitness? witness, WorldControlCapabilities required)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(delivery);
        return new(world, actorId, delivery, witness, required);
    }

    /// <summary>
    /// Revalidates the selected rows, including formerly absent rows. The owner must serialize this
    /// check with publication and independently validate every other action dependency.
    /// </summary>
    public void ValidateCurrent(WorldSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (_selection != Read(current, ActorId, Witness))
        {
            throw new InvalidOperationException("The prepared actor control dependencies changed.");
        }
    }

    // Ordinary authority uses the same value selection/rules without allocating a retained plan.
    internal static bool AllowsCurrent(WorldSnapshot world, EntityId actorId, TrustedCommandDelivery delivery,
        WorldControlWitness? witness, WorldControlCapabilities required) =>
        Read(world, actorId, witness).Allows(actorId, delivery, witness, required);

    private static Selection Read(WorldSnapshot world, EntityId actorId, WorldControlWitness? witness) => new(
        world.WorldId, world.BranchId, world.ProfileVersion,
        world.Inventory.FindControl(actorId),
        world.EffectiveControl is not null, world.EffectiveControl?.Find(actorId),
        witness is null ? null : world.Inventory.FindControl(witness.GranteeActorId),
        witness is null ? null : world.Controllers?.Assignments.FirstOrDefault(row => row.ActorId == witness.OwnerActorId),
        witness is null ? null : world.Controllers?.Assignments.FirstOrDefault(row => row.ActorId == witness.GranteeActorId),
        witness is null ? null : world.ControlPermissions?.Find(witness.OwnerActorId, witness.GranteeActorId));

    private readonly record struct Selection(EntityId WorldId, EntityId BranchId, ushort ProfileVersion,
        ActorControlBinding? OwnerBinding, bool HasEffectiveControl, WorldEffectiveControlSlot? EffectiveSlot,
        ActorControlBinding? GranteeBinding, WorldControllerAssignment? OwnerController,
        WorldControllerAssignment? GranteeController, WorldControlPermissionSlot? Permission)
    {
        internal bool Allows(EntityId actorId, TrustedCommandDelivery delivery, WorldControlWitness? witness,
            WorldControlCapabilities required)
        {
            if (witness is null)
            {
                return OwnerBinding is not null && OwnerBinding.PrincipalId == delivery.PrincipalId
                    && OwnerBinding.SessionId == delivery.SessionId && EffectiveSlot?.Witness is null;
            }
            return HasEffectiveControl && required != 0 && EffectiveSlot?.Witness == witness
                && WorldActorCommandAuthority.ValidateRecordedBindings(WorldId, BranchId, actorId, delivery,
                    witness, required, OwnerBinding, GranteeBinding)
                && WorldActorCommandAuthority.IsEligibleAssignment(OwnerController, GranteeController, Permission, witness);
        }
    }
}
