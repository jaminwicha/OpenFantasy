using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class BuilderCarriedLoadTests
{
    [Fact]
    public void Empty_spare_custody_is_valid_but_live_raw_material_requires_a_spatial_load_owner()
    {
        var stairs = new StairNavigationWorldFixture(physicalConstruction: true);
        WorldSnapshot original = stairs.Genesis;
        EntityId spare = new(0x756e6c6f63617465, 1);
        EntityId playerBag = original.GetResident(stairs.Player).InventoryCustodianId;
        CountedSourceLot wood = original.Inventory.Custody.Lots.Single(lot => lot.CustodianId == playerBag
            && lot.Definition == original.Quests!.Definitions.Single().Item);
        var storage = new CustodianStorage(spare, wood.Quantity, 0);
        var access = new CustodianAccessPolicy(spare, stairs.Player, CustodyDepositRule.OwnerOnly);

        // Authored supplied custody, not a claimed executed transfer: the original lot's exact source,
        // quantity, protections and identity remain intact, with only its custodian changed.
        WorldSnapshot empty = WithSpareCustody(moveWood: false);
        WorldSnapshot occupied = WithSpareCustody(moveWood: true);
        Assert.Equal((ushort)13, occupied.ProfileVersion);
        CountedSourceLot relocated = occupied.Inventory.Custody.GetLot(wood.LotId);
        Assert.Equal(spare, relocated.CustodianId);
        Assert.Equal(wood.Source, relocated.Source);
        Assert.Equal(wood.Quantity, relocated.Quantity);
        Assert.Equal(wood.Definition, relocated.Definition);
        Assert.Equal(wood.Protection, relocated.Protection);
        Assert.Equal(wood.ReservedBy, relocated.ReservedBy);
        var carried = WorldActorCarriage.Capture(occupied, stairs.Player);
        Assert.Equal(stairs.Player, occupied.Inventory.Policies.Single(policy => policy.CustodianId == spare).OwnerActorId);
        Assert.DoesNotContain(spare, carried.CustodianIds);
        Assert.DoesNotContain(carried.CountedLots, lot => lot.LotId == wood.LotId);
        byte[] thirteen = WorldSnapshotCodec.Encode(occupied);
        Assert.Equal(thirteen, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(thirteen)));

        ConstructionLoadPolicy policy = StructuralPolicyWorldFixture.PolicyFor(empty);
        WorldSnapshot admitted = WorldStructuralPolicyAdmission.CreateGenesis(empty, policy);
        Assert.Equal((ushort)14, admitted.ProfileVersion);
        Assert.True(admitted.IdentityIndex.Contains(spare));
        Assert.Equal(0UL, admitted.Inventory.Custody.GetStoredQuantity(spare));
        Assert.Equal(0UL, admitted.Production!.GetCombinedStoredQuantity(admitted.Inventory, spare));
        // All terrain, templates, bodies and material calibrations still bind in the older profile.
        Assert.Equal(occupied.Space.Actors.Count, policy.Bind(occupied).Actors.Count);
        Assert.Throws<ArgumentException>(() => WorldStructuralPolicyAdmission.CreateGenesis(occupied, policy));
        Assert.Equal(thirteen, WorldSnapshotCodec.Encode(occupied));

        WorldSnapshot WithSpareCustody(bool moveWood)
        {
            var custody = new CustodySnapshot(original.WorldId, original.BranchId, original.Inventory.Custody.Revision,
                original.Inventory.Custody.Storages.Append(storage), original.Inventory.Custody.Lots.Select(lot =>
                    moveWood && lot.LotId == wood.LotId
                        ? new CountedSourceLot(lot.LotId, spare, lot.Definition, lot.Source, lot.Revision, lot.Protection, lot.ReservedBy)
                        : lot));
            var inventory = new CountedAuthorityState(custody, original.Inventory.AdmittedUnitCapacity,
                original.Inventory.Actors, original.Inventory.Controls, original.Inventory.Policies.Append(access),
                original.Inventory.Sources, original.Inventory.ReservationIds);
            return new(inventory, original.Space, original.Residents, original.AdmissionVersion, original.Calendar, 0, 0,
                gathering: original.Gathering, production: original.Production, progression: original.Progression,
                quests: original.Quests, construction: original.Construction, excavation: original.Excavation,
                constructionPhysical: original.ConstructionPhysical);
        }
    }

    [Fact]
    public void Quest_delivery_cannot_hide_the_donors_material_mass_in_builder_carried_project_custody()
    {
        var stairs = new StairNavigationWorldFixture(physicalConstruction: true);
        WorldSnapshot original = stairs.Genesis;
        ActorPresence builder = original.Space.GetActor(stairs.Builder);
        // A supplied fresh starting position isolates load admission from the already qualified travel route.
        var space = original.Space.WithActors(original.Space.Actors.Select(actor => actor.ActorId == stairs.Player
            ? new ActorPresence(actor.Binding, actor.Condition, actor.Scope, builder.Feet.Offset(-256, 0, 0),
                actor.Revision, actor.HomePlaceId) : actor));
        WorldSnapshot basis = new(original.Inventory, space, original.Residents, original.AdmissionVersion,
            original.Calendar, 0, 0, gathering: original.Gathering, production: original.Production,
            progression: original.Progression, quests: original.Quests, construction: original.Construction,
            excavation: original.Excavation, constructionPhysical: original.ConstructionPhysical);
        Assert.Equal(SpatialInteraction.Reachable, space.QueryInteraction(stairs.Player, stairs.Builder));
        ConstructionProject project = Assert.Single(basis.Construction!.Projects);
        Assert.Equal(ConstructionStockLocation.BuilderCarried, project.StockLocation);
        Assert.DoesNotContain(basis.Inventory.Custody.Lots, lot => lot.CustodianId == project.ProjectCustodianId);
        ConstructionLoadPolicy generous = StructuralPolicyWorldFixture.PolicyFor(basis);
        ulong currentBuilderMass = checked((ulong)generous.Masses.ActorLoad(basis, stairs.Builder));
        var builderFloor = builder.Feet.Cell.Offset(0, 0, -1);
        Assert.Single(generous.Foundations, foundation => foundation.Cell == builderFloor);
        var limited = new ConstructionLoadPolicy(generous.Scope, generous.Templates,
            generous.Foundations.Select(foundation => foundation.Cell == builderFloor
                ? new StableTerrainBearing(foundation.ElementId, foundation.Cell, foundation.SourceId,
                    foundation.Material, new(currentBuilderMass)) : foundation), generous.Masses);
        WorldSnapshot constrainedGenesis = WorldStructuralPolicyAdmission.CreateGenesis(basis, limited);
        WorldSnapshot generousGenesis = WorldStructuralPolicyAdmission.CreateGenesis(basis, generous);

        using var constrainedFiles = new StorageFiles();
        using var generousFiles = new StorageFiles();
        using SqliteWorldAuthority constrained = SqliteWorldAuthority.CreateNew(constrainedFiles.Path, constrainedGenesis);
        using SqliteWorldAuthority control = SqliteWorldAuthority.CreateNew(generousFiles.Path, generousGenesis);
        WorldSnapshot before = AcceptQuest(stairs, constrained, constrainedGenesis);
        WorldSnapshot controlBefore = AcceptQuest(stairs, control, generousGenesis);
        DeliveryQuestDefinition quest = Assert.Single(before.Quests!.Definitions);
        Assert.Equal(stairs.Builder, quest.GiverActorId);
        Assert.Equal(project.ProjectCustodianId, quest.DestinationCustodianId);
        Assert.Equal(6UL, quest.Quantity);
        UInt128 deliveredMass = (UInt128)quest.Quantity
            * project.Definition.MaterialTransform.BindingFor(quest.Item).Physical.MassPerUnit.Value;
        UInt128 donorBefore = generous.Masses.ActorLoad(controlBefore, stairs.Player);
        UInt128 builderBefore = generous.Masses.ActorLoad(controlBefore, stairs.Builder);
        Assert.Equal((UInt128)currentBuilderMass, builderBefore);

        // Positive control executes the same delivery against a sufficient foundation rating.
        WorldStepDecision accepted = control.Commit(Completion(stairs, controlBefore));
        Assert.Equal(WorldActionRejection.None, Assert.Single(accepted.Receipts).Rejection);
        WorldSnapshot supplied = accepted.Candidate;
        Assert.Equal(ActorQuestStatus.Completed, Assert.Single(supplied.Quests!.Participations).Status);
        Assert.Equal(quest.Quantity, supplied.Inventory.Custody.Lots.Where(lot => lot.CustodianId == project.ProjectCustodianId)
            .Aggregate(0UL, (quantity, lot) => checked(quantity + lot.Quantity)));
        Assert.Equal(donorBefore - deliveredMass, generous.Masses.ActorLoad(supplied, stairs.Player));
        Assert.Equal(builderBefore + deliveredMass, generous.Masses.ActorLoad(supplied, stairs.Builder));
        Assert.Equal(donorBefore + builderBefore,
            generous.Masses.ActorLoad(supplied, stairs.Player) + generous.Masses.ActorLoad(supplied, stairs.Builder));
        Assert.True(supplied.Progression!.Currency.GetActorAccount(stairs.Player).Balance
            > controlBefore.Progression!.Currency.GetActorAccount(stairs.Player).Balance);
        AssertHeldClock(controlBefore, supplied);

        WorldStepDecision refused = constrained.Commit(Completion(stairs, before));
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(refused.Receipts).Rejection);
        WorldSnapshot after = refused.Candidate;
        AssertHeldClock(before, after);
        Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
        Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);
        Assert.Equal(before.Quests.ContentHash, after.Quests!.ContentHash);
        Assert.Equal(before.Construction!.ContentHash, after.Construction!.ContentHash);
        Assert.Equal(before.ConstructionPhysical!.ContentHash, after.ConstructionPhysical!.ContentHash);
        Assert.Equal(before.StructuralPolicy!.ContentHash, after.StructuralPolicy!.ContentHash);
        Assert.Equal(ActorQuestStatus.Accepted, Assert.Single(after.Quests.Participations).Status);
        Assert.DoesNotContain(after.Inventory.Custody.Lots, lot => lot.CustodianId == project.ProjectCustodianId);
        Assert.DoesNotContain(refused.Identities, identity => identity.Kind == WorldCommittedIdentityKind.SpawnedEntity);
        Assert.Equal((UInt128)currentBuilderMass, limited.Masses.ActorLoad(after, stairs.Builder));
        Assert.Equal(limited.Masses.ActorLoad(before, stairs.Player), limited.Masses.ActorLoad(after, stairs.Player));
        Assert.Equal(after.ContentHash, constrained.InspectForHost().State.ContentHash);
        Assert.Equal(after.ContentHash, constrained.ReadRecordedStateForHost(after.Revision).State.ContentHash);
    }

    private static WorldSnapshot AcceptQuest(StairNavigationWorldFixture stairs, SqliteWorldAuthority authority, WorldSnapshot genesis)
    {
        DeliveryQuestDefinition quest = Assert.Single(genesis.Quests!.Definitions);
        WorldSnapshot speaking = stairs.Actions.Act(authority, genesis, stairs.Player,
            new SpeakToActorIntent(quest.GiverActorId, genesis.Space.GetActor(quest.GiverActorId).Revision));
        Assert.True(speaking.HasReadingHold);
        Assert.Equal(genesis.AdvancingSteps + 1, speaking.AdvancingSteps);
        Assert.Equal(genesis.Calendar.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep), speaking.Calendar);
        WorldSnapshot accepted = stairs.Actions.Act(authority, speaking, stairs.Player,
            new AcceptDeliveryQuestIntent(quest.DefinitionId, quest.Version, quest.GiverActorId,
                speaking.Space.GetActor(quest.GiverActorId).Revision, 0,
                speaking.Progression!.GetActor(stairs.Player).Revision));
        Assert.Equal(ActorQuestStatus.Accepted, Assert.Single(accepted.Quests!.Participations).Status);
        AssertHeldClock(speaking, accepted);
        return accepted;
    }

    private static WorldStepBatch Completion(StairNavigationWorldFixture stairs, WorldSnapshot world)
    {
        DeliveryQuestDefinition quest = Assert.Single(world.Quests!.Definitions);
        EntityId bag = world.GetResident(stairs.Player).InventoryCustodianId;
        var wood = world.Inventory.Custody.Lots.Single(lot => lot.CustodianId == bag && lot.Definition == quest.Item);
        var delivery = ConstructionUpgradeFixture.Delivery(world, stairs.Player);
        var command = new WorldActionCommand(stairs.Actions.NextIdentity(), delivery.PrincipalId, delivery.SessionId,
            stairs.Player, world.WorldId, world.BranchId, world.Space.GetActor(stairs.Player).Revision,
            new CompleteDeliveryQuestIntent(quest.DefinitionId, quest.Version, quest.GiverActorId,
                world.Space.GetActor(quest.GiverActorId).Revision, Assert.Single(world.Quests.Participations).Revision,
                world.Progression!.GetActor(stairs.Player).Revision, world.Inventory.Custody.GetStorage(bag).Revision,
                [new(wood.LotId, wood.Revision, quest.Quantity)]));
        return stairs.Actions.Batch(world, [command], []);
    }

    private static void AssertHeldClock(WorldSnapshot before, WorldSnapshot after)
    {
        Assert.True(before.HasReadingHold);
        Assert.True(after.HasReadingHold);
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(before.Calendar, after.Calendar);
        Assert.Equal(before.AdvancingSteps, after.AdvancingSteps);
        Assert.Equal(before.Gathering!.ContentHash, after.Gathering!.ContentHash);
        Assert.Equal(before.Residents.Select(resident => (resident.ActorId, resident.ExperiencedTime)),
            after.Residents.Select(resident => (resident.ActorId, resident.ExperiencedTime)));
        Assert.Equal(before.Dialogues, after.Dialogues);
    }
}
