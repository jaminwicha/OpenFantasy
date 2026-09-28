using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Tests.Gathering;
using OpenFantasy.Core.Tests.Quests;
using OpenFantasy.Core.Tests.Worlds;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Construction;

/// <summary>Actual legacy construction followed by standalone material work beside a genuinely advancing world.</summary>
internal sealed class ConstructionContinuationFixture
{
    internal static EntityId Id(ulong value) => new(0x636f6e74696e7565, value);
    internal static readonly EntityId Player = AdmissionFixture.Id(10);
    internal static readonly EntityId Builder = AdmissionFixture.Id(11);
    internal static readonly EntityId ProjectA = Id(1);
    internal static readonly EntityId ProjectB = Id(2);
    internal static readonly EntityId StoreB = Id(3);
    internal static readonly EntityId ReserveB = Id(4);
    internal static readonly EntityId QuestB = Id(5);
    internal static readonly EntityId WoodB = Id(6);
    internal static readonly EntityId Movable = Id(7);
    internal static readonly EntityId RecraftId = Id(8);
    internal static readonly EntityId UnreservedWood = Id(9);
    private static readonly Lazy<LegacyBoundary> EarnedBoundary = new(EarnBoundary);
    private static readonly Lazy<ConstructionContinuationFixture> EarnedReady = new(() =>
    {
        var journey = new ConstructionContinuationFixture();
        journey.WorkUntil(199);
        return journey;
    });
    private readonly HashSet<EntityId> _occupied;
    private ulong _next = 100_000;

    internal ConstructionContinuationFixture(bool readyToComplete = false)
    {
        if (readyToComplete)
        {
            var ready = EarnedReady.Value;
            SourceWorld = ready.SourceWorld;
            LegacyWorld = ready.LegacyWorld;
            Inventory = ready.Inventory;
            Space = ready.Space;
            State = ready.State;
            Production = ready.Production;
            _occupied = ready._occupied.ToHashSet();
            _next = ready._next;
            return;
        }
        var boundary = EarnedBoundary.Value;
        SourceWorld = boundary.World;
        LegacyWorld = boundary.World;
        Inventory = SourceWorld.Inventory;
        Space = SourceWorld.Space;
        State = MaterialConstructionState.Capture(SourceWorld);
        Production = SourceWorld.Production!.ContinueConstruction(Inventory, State, SourceWorld.Gathering);
        _occupied = boundary.Occupied.ToHashSet();
    }

    internal WorldSnapshot LegacyWorld { get; }
    internal WorldSnapshot SourceWorld { get; private set; }
    internal CountedAuthorityState Inventory { get; private set; }
    internal ProductionSnapshot Production { get; private set; }
    internal LocalSpatialSnapshot Space { get; private set; }
    internal MaterialConstructionState State { get; private set; }
    internal ConstructionSnapshot Construction => State.Construction;
    internal ConstructionPhysicalSnapshot Physical => State.Physical!;
    internal List<ClosedSegment> Closed { get; } = [];
    internal bool Occupied(EntityId id) => _occupied.Contains(id);
    internal EntityId NextId() => Id(_next++);

    internal void Advance(IEnumerable<WorldMovementInput>? movement = null)
    {
        EntityId cause = NextId();
        var batch = new WorldStepBatch(cause, SourceWorld.WorldId, SourceWorld.BranchId,
            SourceWorld.Revision, SourceWorld.AdvancingSteps, WorldBatchKind.AdvancingStep, movement ?? [], [], SourceWorld.ProfileVersion);
        var decision = new WorldStepProcessor().Decide(SourceWorld, batch, Occupied);
        SourceWorld = decision.Candidate;
        foreach (var identity in decision.Identities) Assert.True(_occupied.Add(identity.Identity));
    }

    internal WorldActionCommand Command(EntityId? command = null)
    {
        var project = Construction.FindProject(ProjectB)!;
        var control = Inventory.Controls.Single(value => value.ActorId == Builder);
        var tool = Inventory.Custody.GetLot(ConstructionWorldFixture.KitLot);
        return new(command ?? NextId(), control.PrincipalId, control.SessionId, Builder,
            SourceWorld.WorldId, SourceWorld.BranchId, Space.GetActor(Builder).Revision,
            new WorkConstructionIntent(project.ProjectId, project.Revision,
                Inventory.Custody.GetStorage(project.ProjectCustodianId).Revision, tool.LotId, tool.Revision));
    }

    internal MaterialConstructionPlan Plan(WorldActionCommand command, Func<EntityId, bool>? occupied = null,
        bool advancing = true, bool held = false) => new MaterialConstructionPlanner().Plan(SourceWorld, Inventory,
            Production, State, Space, command, advancing, held, occupied ?? Occupied);

    internal MaterialConstructionPlan Work()
    {
        Advance();
        var command = Command();
        var plan = Plan(command);
        Accept(plan, command.CommandId);
        return plan;
    }

    internal void WorkUntil(ulong revision)
    {
        while (Construction.FindProject(ProjectB)!.Revision < revision) Work();
    }

    internal void Accept(MaterialConstructionPlan plan, EntityId command)
    {
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Assert.True(_occupied.Add(command));
        foreach (var created in plan.CreatedIds) Assert.True(_occupied.Add(created));
        Inventory = plan.CandidateInventory;
        Production = plan.CandidateProduction;
        Space = plan.CandidateSpace;
        State = plan.CandidateState;
        Assert.Same(plan.CandidateConstruction, Construction);
        Assert.Same(plan.CandidatePhysical, Physical);
        Validate();
    }

    internal void Validate()
    {
        Production.ValidateForAuthority(Inventory);
        Construction.ValidateForMaterialContinuation(SourceWorld, Inventory, Production, Space, Physical);
        Physical.ValidateStableGeometry(Space);
    }

    internal void Cut()
    {
        Closed.Add(new(Inventory, Production, ProductionCodecV5.Encode(Production)));
        Production = Production.Continue(Inventory);
        Validate();
        Reopen();
    }

    internal void Reopen()
    {
        byte[] bytes = ProductionCodecV5.Encode(Production);
        Production = ProductionCodecV5.Decode(bytes, Production.Continuation!,
            Production.MaterialHistory!.GatherEvidence, Production.MaterialHistory.ConstructionEvidence);
        Assert.Equal(bytes, ProductionCodecV5.Encode(Production));
        Validate();
    }

    internal void TransferMovable()
    {
        var lot = Inventory.Custody.GetLot(Movable);
        EntityId destination = lot.CustodianId == AdmissionFixture.Id(60) ? AdmissionFixture.Id(61) : AdmissionFixture.Id(60);
        EntityId actor = Inventory.Policies.Single(value => value.CustodianId == lot.CustodianId).OwnerActorId;
        var control = Inventory.Controls.Single(value => value.ActorId == actor);
        var command = new CommandEnvelope(NextId(), control.PrincipalId, control.SessionId, actor,
            Inventory.WorldId, Inventory.BranchId,
            [new(RevisionTargetKind.Branch, Inventory.BranchId, Inventory.Custody.Revision),
             new(RevisionTargetKind.Entity, lot.LotId, lot.Revision),
             new(RevisionTargetKind.Entity, lot.CustodianId, Inventory.Custody.GetStorage(lot.CustodianId).Revision),
             new(RevisionTargetKind.Entity, destination, Inventory.Custody.GetStorage(destination).Revision)],
            new(lot.LotId, lot.CustodianId, destination, 1));
        var plan = new MaterialTransferPlanner().Plan(Inventory, Production, command,
            new(control.PrincipalId, control.SessionId), Occupied);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Inventory = plan.CandidateInventory;
        Production = plan.CandidateProduction;
        Assert.True(_occupied.Add(command.CommandId));
    }

    internal TransformedLot RecraftByproduct(EntityId projectId)
    {
        var project = Construction.FindProject(projectId)!;
        var lot = Production.Lots.Single(value => value.CustodianId == project.ProjectCustodianId && value.ReservedBy is null);
        var recipe = Production.Recipes.Single(value => value.DefinitionId == RecraftId);
        var control = Inventory.Controls.Single(value => value.ActorId == Builder);
        EntityId command = NextId();
        var request = new CraftRequest(recipe.DefinitionId, recipe.Version, Production.Revision, lot.CustodianId,
            Inventory.Custody.GetStorage(lot.CustodianId).Revision, [new(IngredientLotKind.Transformed, lot.LotId, lot.Revision, 2)]);
        var plan = new InstantCraftPlanner().Plan(Inventory, Production, new(control.PrincipalId, control.SessionId),
            Builder, command, request, Occupied);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Inventory = plan.CandidateInventory;
        Production = plan.CandidateProduction;
        Assert.True(_occupied.Add(command));
        Assert.True(_occupied.Add(EntityId.DeriveSpawned(command, 0)));
        Assert.True(_occupied.Add(EntityId.DeriveSpawned(command, 1)));
        Validate();
        return Production.Lots.Single(value => value.LotId == EntityId.DeriveSpawned(command, 1));
    }

    private static LegacyBoundary EarnBoundary()
    {
        var quest = QuestFixture.Create(funds: 400);
        var basis = quest.World;
        var original = basis.Inventory;
        var inventory = new CountedAuthorityState(new(original.WorldId, original.BranchId, 0,
            original.Custody.Storages.Append(new(StoreB, 6, 0)),
            original.Custody.Lots.Concat([
                new CountedSourceLot(WoodB, AdmissionFixture.Id(60), AdmissionFixture.Material, new(Id(20), 0, 6), 0),
                new CountedSourceLot(Movable, AdmissionFixture.Id(60), AdmissionFixture.Material, new(Id(21), 0, 1), 0),
                new CountedSourceLot(UnreservedWood, AdmissionFixture.Id(61), AdmissionFixture.Material, new(Id(22), 0, 6), 0)])),
            original.AdmittedUnitCapacity + 13, original.Actors, original.Controls,
            original.Policies.Select(value => new CustodianAccessPolicy(value.CustodianId, value.OwnerActorId,
                CustodyDepositRule.AnyControlledActor)).Append(new(StoreB, Builder, CustodyDepositRule.AnyControlledActor)),
            original.Sources.Concat([new AdmittedCountedSource(Id(20), AdmissionFixture.Material, 6),
                new AdmittedCountedSource(Id(21), AdmissionFixture.Material, 1),
                new AdmittedCountedSource(Id(22), AdmissionFixture.Material, 6)]), original.ReservationIds.Append(ReserveB));
        var second = new DeliveryQuestDefinition(QuestB, "second_guard", "Second guard", "Bring six more wood.",
            Builder, 1, AdmissionFixture.Material, 6, StoreB, ReserveB, quest.Definition.Reward);
        var quests = new QuestSnapshot(basis.WorldId, basis.BranchId, 0, [quest.Definition, second], []);
        var definition = ConstructionFixture.Definition(sourceItem: AdmissionFixture.Material);
        var secondRecipe = new CraftRecipeDefinition(Id(60), definition.MaterialTransform.PhysicalRecipe,
            definition.MaterialTransform.Bindings, definition.MaterialTransform.Allocations);
        var secondDefinition = new ConstructionDefinition(Id(61), "construction/second_guard", "Second seedling guard",
            secondRecipe, definition.InstalledOutputIndex, definition.RequiredTool, definition.Members,
            definition.RequiredWork, definition.WorkRate);
        GroundAccessRequirement[] access = [new(Builder, new(512, 0, 256), new(512, 256, 256)),
            new(Player, new(0, 256, 256), new(0, 512, 256))];
        var projects = new ConstructionSnapshot(basis.WorldId, basis.BranchId, 0, [definition, secondDefinition], [
            new(ProjectA, definition, Builder, Builder, quest.Definition.DefinitionId, QuestFixture.Project,
                QuestFixture.Reservation, new(660, -256, 256), new(512, 0, 256), access),
            new(ProjectB, secondDefinition, Builder, Builder, QuestB, StoreB, ReserveB,
                new(660, 256, 256), new(512, 0, 256), access)]);
        var tools = new WorldConstructionAdmission(ConstructionFixture.Pin, projects,
            [new(Builder, AdmissionFixture.Id(61), new(ConstructionFixture.Id(81), definition.RequiredTool.Item, 1),
                ConstructionWorldFixture.KitLot)]);
        inventory = tools.CreateGenesisInventory(inventory);
        var production = new ProductionSnapshot(basis.WorldId, basis.BranchId, 0,
            [definition.MaterialTransform, secondRecipe, RecraftRecipe(definition)], [], []);
        var target = new CellCoordinate(0, 0, 0);
        var terrain = basis.Space.Regions.Single().GetCell(target);
        var excavationSite = new ExcavationSite(Id(30), target, terrain.SourceId!.Value, Id(31), Player,
            definition.MaterialTransform.BindingFor(terrain.Material!), GatheringFixture.Axe, 2, 0, new(-128, 128, 0));
        inventory = new(inventory.Custody, inventory.AdmittedUnitCapacity + excavationSite.Quantity,
            inventory.Actors, inventory.Controls, inventory.Policies,
            inventory.Sources.Append(new(excavationSite.CountedSourceId, excavationSite.Material.Item, excavationSite.Quantity)),
            inventory.ReservationIds);
        WorldSnapshot world = new(inventory, basis.Space, basis.Residents, ConstructionFixture.Pin, basis.Calendar,
            0, 0, [], basis.Gathering, production, quest.Progression, quests, projects);
        world = WorldConstructionPhysicalAdmission.CreateGenesis(WorldSteppedSupportAdmission.CreateGenesis(
            WorldConstructedSupportAdmission.CreateGenesis(WorldExcavationAdmission.CreateGenesis(world, new([excavationSite])))));
        HashSet<EntityId> occupied = [];
        ulong sequence = 1000;
        WorldSnapshot Apply(WorldActionCommand command)
        {
            EntityId cause = Id(sequence++);
            var decision = new WorldStepProcessor().Decide(world, new(cause, world.WorldId, world.BranchId,
                world.Revision, world.AdvancingSteps, world.HasReadingHold ? WorldBatchKind.HeldInteraction
                    : WorldBatchKind.AdvancingStep, [], [command], world.ProfileVersion), occupied.Contains);
            Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
            foreach (var identity in decision.Identities) Assert.True(occupied.Add(identity.Identity));
            world = decision.Candidate;
            return world;
        }
        WorldActionCommand PlayerAction(WorldActionIntent intent) => Action(world, Player, Id(sequence++), intent);
        Apply(PlayerAction(new SpeakToActorIntent(Builder, world.Space.GetActor(Builder).Revision)));
        foreach (var supply in new[] { (quest.Definition, AdmissionFixture.Id(70)), (second, WoodB) })
        {
            var definitionOfQuest = supply.Item1;
            Apply(PlayerAction(new AcceptDeliveryQuestIntent(definitionOfQuest.DefinitionId, definitionOfQuest.Version,
                Builder, world.Space.GetActor(Builder).Revision, 0, world.Progression!.GetActor(Player).Revision)));
            Apply(PlayerAction(new CompleteDeliveryQuestIntent(definitionOfQuest.DefinitionId, definitionOfQuest.Version,
                Builder, world.Space.GetActor(Builder).Revision,
                world.Quests!.FindParticipation(Player, definitionOfQuest.DefinitionId)!.Revision,
                world.Progression!.GetActor(Player).Revision, world.Inventory.Custody.GetStorage(AdmissionFixture.Id(60)).Revision,
                [new(supply.Item2, world.Inventory.Custody.GetLot(supply.Item2).Revision, 6)])));
        }
        Apply(PlayerAction(new CloseDialogueIntent(Assert.Single(world.Dialogues).DialogueId)));
        for (int work = 0; work < 201; work++)
        {
            var project = world.Construction!.FindProject(work < 200 ? ProjectA : ProjectB)!;
            Apply(Action(world, Builder, Id(sequence++), new WorkConstructionIntent(project.ProjectId, project.Revision,
                world.Inventory.Custody.GetStorage(project.ProjectCustodianId).Revision,
                ConstructionWorldFixture.KitLot, world.Inventory.Custody.GetLot(ConstructionWorldFixture.KitLot).Revision)));
        }
        Assert.Equal(200UL, world.Construction!.FindProject(ProjectA)!.Revision);
        Assert.Equal(1UL, world.Construction.FindProject(ProjectB)!.Revision);
        Assert.Single(world.Production!.Batches);
        Assert.Equal(10, world.ConstructionPhysical!.Members.Count);
        Assert.Equal(world.ContentHash, WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(world)).ContentHash);
        return new(world, occupied.ToArray());
    }

    private static WorldActionCommand Action(WorldSnapshot world, EntityId actor, EntityId id, WorldActionIntent intent)
    {
        var control = world.Inventory.Controls.Single(value => value.ActorId == actor);
        return new(id, control.PrincipalId, control.SessionId, actor, world.WorldId, world.BranchId,
            world.Space.GetActor(actor).Revision, intent);
    }

    private static CraftRecipeDefinition RecraftRecipe(ConstructionDefinition definition)
    {
        var offcut = definition.MaterialTransform.BindingFor(definition.MaterialTransform.ItemFor(definition.MaterialTransform.Outputs[1]));
        var chip = new MaterialDefinition(new PackageId("construction-continuation-test"), Id(40), "joined_offcuts",
            new(20_000), new(50_000), new(1));
        var item = new CountedItemDefinition(Id(41), ConstructionFixture.Pin);
        var physical = new MaterialRecipeDefinition(new PackageId("construction-continuation-test"), Id(42), "join_offcuts",
            new(1), null, new MaterialCatalog([offcut.Physical, chip]),
            [new(RecipeMaterialRole.Input, offcut.Physical.Pin(), 2), new(RecipeMaterialRole.Output, chip.Pin(), 1)]);
        return new(RecraftId, physical, [offcut, new(item, chip)], [new(offcut.Item, 0, new(20_000))]);
    }

    internal sealed record ClosedSegment(CountedAuthorityState Inventory, ProductionSnapshot Production, byte[] Bytes);
    private sealed record LegacyBoundary(WorldSnapshot World, EntityId[] Occupied);
}
