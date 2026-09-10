using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;

namespace OpenFantasy.Core.Tests.Worlds;

internal static class StepFixture
{
    internal static WorldSnapshot Create()
    {
        WorldSnapshot initial = AdmissionFixture.Create(revision: 0, steps: 0);
        var body = new BodyDefinition(Id(40), BodyNature.Corporeal, new BodyDimensions(128, 128, 384),
            [new(new BodySlotKey("core"), null, new BodyDimensions(64, 64, 128),
                [BodyFunction.GroundContact, BodyFunction.Manipulation, BodyFunction.Hearing, BodyFunction.Vocalization])],
            [new(LocomotionMode.Ground, 1)]);
        ActorPresence[] actors = initial.Space.Actors.Select(actor =>
        {
            var binding = ActorFormBinding.Corporeal(actor.ActorId, actor.Binding.FormInstanceId,
                actor.Binding.PhysicalBodyId!.Value, body);
            return new ActorPresence(binding, new BodyConditionSnapshot(binding.FormInstanceId, body, []),
                actor.Scope, actor.Feet, actor.Revision, actor.HomePlaceId);
        }).ToArray();
        return new WorldSnapshot(initial.Inventory, new LocalSpatialSnapshot(initial.Space.Scope, initial.Space.Regions, actors),
            initial.Residents, initial.AdmissionVersion, new WorldInstant(0, 32_400_000), 0, 0);
    }

    internal static WorldActionCommand Action(WorldSnapshot world, ulong command, WorldActionIntent intent, ulong actor = 10)
    {
        ActorControlBinding control = world.Inventory.Controls.Single(value => value.ActorId == Id(actor));
        return new WorldActionCommand(Id(command), control.PrincipalId, control.SessionId, Id(actor), world.WorldId,
            world.BranchId, world.Space.GetActor(Id(actor)).Revision, intent);
    }

    internal static WorldMovementInput Move(WorldSnapshot world, ulong actor, long dx = 32, long dy = 0)
    {
        ActorControlBinding control = world.Inventory.Controls.Single(value => value.ActorId == Id(actor));
        return new WorldMovementInput(new TrustedCommandDelivery(control.PrincipalId, control.SessionId), Id(actor), dx, dy);
    }

    internal static WorldStepBatch Batch(WorldSnapshot world, ulong cause, WorldActionCommand[]? actions = null,
        WorldMovementInput[]? movement = null) => new(Id(cause), world.WorldId, world.BranchId,
            world.Revision, world.AdvancingSteps, world.HasReadingHold ? WorldBatchKind.HeldInteraction : WorldBatchKind.AdvancingStep,
            movement ?? [], actions ?? []);

    internal static WorldSnapshot Copy(WorldSnapshot world, LocalSpatialSnapshot? space = null,
        IEnumerable<WorldResident>? residents = null, WorldInstant? calendar = null,
        ulong? revision = null, ulong? steps = null) => new(world.Inventory, space ?? world.Space,
            residents ?? world.Residents, world.AdmissionVersion, calendar ?? world.Calendar,
            revision ?? world.Revision, steps ?? world.AdvancingSteps, world.Dialogues);
}
