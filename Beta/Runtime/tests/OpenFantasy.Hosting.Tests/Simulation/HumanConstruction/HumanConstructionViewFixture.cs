using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.HumanConstruction;

internal sealed class HumanConstructionViewFixture : IDisposable
{
    private static readonly Lazy<WorldSnapshot> Fresh = new(() => HumanConstructionWorldAdmission.CreateEmbedded(
        "human-construction-view-tests", ["Birch", "Cedar", "Dawn"]));
    private readonly StorageFiles _files = new();
    private ulong _next = 1;
    internal HumanConstructionViewFixture() => Authority = SqliteWorldAuthority.CreateNew(_files.Path, Fresh.Value);
    internal SqliteWorldAuthority Authority { get; }
    internal WorldSnapshot State => Authority.ReadStateForHost();
    internal EntityId Owner => State.Construction!.Projects.Single().BuilderActorId;
    internal EntityId Helper => State.Residents.Where(actor => actor.ControllerKind == ResidentControllerKind.Player
        && actor.ActorId != Owner).Select(actor => actor.ActorId).Order().First();
    internal EntityId Next() => new(0x6373747275766965, _next++);
    internal TrustedCommandDelivery Delivery(EntityId actor)
    {
        var binding = State.Inventory.Controls.Single(value => value.ActorId == actor);
        return new(binding.PrincipalId, binding.SessionId);
    }
    internal WorldActionCommand Command(EntityId actor, WorldActionIntent intent, WorldControlWitness? witness = null)
    {
        var delivery = Delivery(witness?.GranteeActorId ?? actor);
        return new(Next(), delivery.PrincipalId, delivery.SessionId, actor, State.WorldId, State.BranchId,
            State.Space.GetActor(actor).Revision, intent, witness);
    }
    internal void Apply(WorldActionCommand command, WorldBatchKind? kind = null)
    {
        var state = State;
        var result = Authority.Commit(new(Next(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
            kind ?? (state.HasReadingHold ? WorldBatchKind.HeldInteraction : WorldBatchKind.AdvancingStep), [], [command], state.ProfileVersion));
        Assert.Equal(WorldActionRejection.None, Assert.Single(result.Receipts).Rejection);
    }
    internal void Tick(WorldMovementInput? movement = null)
    {
        var state = State;
        Authority.Commit(new(Next(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
            WorldBatchKind.AdvancingStep, movement is null ? [] : [movement], [], state.ProfileVersion));
    }
    internal void SupplyAndPosition()
    {
        var quest = State.Quests!.Definitions.Single();
        EntityId supplier = State.Residents.Where(actor => actor.ActorId != Owner).Select(actor => actor.ActorId)
            .First(actor => State.Inventory.Custody.Lots.Any(lot => lot.CustodianId == State.GetResident(actor).InventoryCustodianId
                && lot.Definition == quest.Item && lot.Quantity >= quest.Quantity));
        for (int steps = 0; State.Space.QueryInteraction(supplier, Owner) != SpatialInteraction.Reachable; steps++)
        {
            Assert.True(steps < 512, "The original supply approach must remain bounded.");
            var current = State.Space.GetActor(supplier).Feet;
            var target = State.Space.GetActor(Owner).Feet;
            long dx = Math.Sign(target.X - current.X) * WorldMovementInput.DisplacementPerStep;
            long dy = dx == 0 ? Math.Sign(target.Y - current.Y) * WorldMovementInput.DisplacementPerStep : 0;
            Tick(new(Delivery(supplier), supplier, dx, dy));
        }
        Apply(Command(supplier, new SpeakToActorIntent(Owner, State.Space.GetActor(Owner).Revision)));
        Apply(Command(supplier, new AcceptDeliveryQuestIntent(quest.DefinitionId, quest.Version, Owner,
            State.Space.GetActor(Owner).Revision, 0, State.Progression!.GetActor(supplier).Revision)));
        var bag = State.GetResident(supplier).InventoryCustodianId;
        var wood = State.Inventory.Custody.Lots.First(lot => lot.CustodianId == bag && lot.Definition == quest.Item
            && lot.Quantity >= quest.Quantity);
        Apply(Command(supplier, new CompleteDeliveryQuestIntent(quest.DefinitionId, quest.Version, Owner,
            State.Space.GetActor(Owner).Revision, 1, State.Progression!.GetActor(supplier).Revision,
            State.Inventory.Custody.GetStorage(bag).Revision, [new(wood.LotId, wood.Revision, quest.Quantity)])));
        Apply(Command(supplier, new CloseDialogueIntent(State.Dialogues.Single().DialogueId)));
        var destination = State.Construction!.Projects.Single().WorkFeet;
        for (int steps = 0; State.Space.GetActor(Owner).Feet != destination; steps++)
        {
            Assert.True(steps < 512, "The original construction stance must remain reachable.");
            var route = State.Space.QueryGroundRoute(Owner, destination);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            var next = route.NextWaypoint!.Value;
            var current = State.Space.GetActor(Owner).Feet;
            long dx = Math.Clamp(next.X - current.X, -32, 32);
            long dy = dx == 0 ? Math.Clamp(next.Y - current.Y, -32, 32) : 0;
            Tick(new(Delivery(Owner), Owner, dx, dy));
        }
        Ready(Owner);
        Assert.Null(State.Quests.FindParticipation(Owner, quest.DefinitionId));
    }
    internal void Ready(EntityId actor)
    {
        for (int steps = 0; !WorldActorAvailability.CanAct(State, actor); steps++)
        {
            Assert.True(steps < 128);
            Tick();
        }
    }
    internal WorldControlWitness Delegate(WorldControlCapabilities capabilities)
    {
        Apply(Command(Owner, new OfferControlPermissionIntent(Helper, 0, capabilities)), WorldBatchKind.ControlPermission);
        var slot = State.ControlPermissions!.Find(Owner, Helper)!;
        Apply(Command(Helper, new AcceptControlPermissionIntent(Owner, slot.Revision, slot.GrantCommandId!.Value, slot.TermsHash!)), WorldBatchKind.ControlPermission);
        Event(WorldControllerEventKind.Disconnected);
        var segment = Authority.BeginControllerRecoverySegment();
        Authority.CheckpointControllerAllowances(segment.Generation, segment.Allowances.Select(row =>
            row.ActorId == Owner ? row with { ElapsedHostMilliseconds = 30_000 } : row).ToArray());
        Event(WorldControllerEventKind.AllowanceExpired);
        var state = State;
        slot = state.ControlPermissions.Find(Owner, Helper)!;
        var control = state.Controllers!.Get(Owner);
        var witness = new WorldControlWitness(Owner, Helper, slot.GrantCommandId!.Value, slot.AcceptanceCommandId!.Value,
            slot.Revision, slot.Terms!.Capabilities, slot.TermsHash!, Next(), 1, state.Revision + 1, control.DisconnectId!.Value, control.Revision);
        Authority.Commit(new(witness.AssignmentId, state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
            WorldBatchKind.ControlAssignment, [], [], state.ProfileVersion,
            controlAssignmentEvent: new(Owner, WorldControlAssignmentEventKind.Acquire, 0, witness)));
        return witness;
    }
    private void Event(WorldControllerEventKind kind)
    {
        var state = State; var row = state.Controllers!.Get(Owner);
        var input = kind == WorldControllerEventKind.Disconnected
            ? new WorldControllerEvent(Owner, kind, row.Revision, reason: WorldControllerDisconnectReason.TransportLost)
            : new WorldControllerEvent(Owner, kind, row.Revision, row.DisconnectId, 30_000);
        Authority.Commit(new(Next(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
            WorldBatchKind.ControllerLifecycle, [], [], state.ProfileVersion, input));
    }
    internal void Complete()
    {
        while (!State.Construction!.Projects.Single().IsCompleted)
        {
            Ready(Owner);
            var work = Assert.Single(new LocalWorldView(State, Owner).ConstructionWork);
            Assert.True(work.CanWork, work.Blocker.ToString());
            Apply(Command(Owner, Assert.Single(work.Choices)));
        }
    }
    public void Dispose() { Authority.Dispose(); _files.Dispose(); }
}
