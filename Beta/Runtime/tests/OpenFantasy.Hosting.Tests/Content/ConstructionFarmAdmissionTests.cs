using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.Content;

public sealed class ConstructionFarmAdmissionTests
{
    [Fact]
    public void Adds_one_owned_finite_kit_and_one_counted_allowance_without_resetting_existing_state()
    {
        WorldSnapshot world = CreateQuestGenesis();
        byte[] before = WorldSnapshotCodec.Encode(world);
        ConstructionFarmComponents result = ConstructionFarmAdmission.CreateConstruction(world);
        Assert.Equal(before, WorldSnapshotCodec.Encode(world));
        WorldResident wren = Wren(world);
        ConstructionToolAdmission tool = Assert.Single(result.Admission.Tools);
        Assert.Equal(wren.ActorId, tool.OwnerActorId);
        Assert.Equal(wren.InventoryCustodianId, tool.CustodianId);
        Assert.Equal(1UL, tool.Source.UnitCapacity);
        Assert.Equal(1UL, tool.StorageCapacityIncrease);
        CountedSourceLot lot = result.Inventory.Custody.GetLot(tool.LotId);
        Assert.Equal(new SourceInterval(tool.Source.SourceId, 0, 1), lot.Source);
        Assert.Equal(tool.Source.Definition, lot.Definition);
        Assert.Null(lot.ReservedBy);
        Assert.Equal(0UL, lot.Revision);
        Assert.Equal(world.Inventory.AdmittedUnitCapacity + 1, result.Inventory.AdmittedUnitCapacity);
        Assert.Equal(299UL, result.Inventory.AdmittedUnitCapacity);
        Assert.Equal(world.Inventory.Custody.Revision + 1, result.Inventory.Custody.Revision);
        foreach (CustodianStorage storage in world.Inventory.Custody.Storages)
        {
            CustodianStorage actual = result.Inventory.Custody.GetStorage(storage.CustodianId);
            if (storage.CustodianId == wren.InventoryCustodianId)
            {
                Assert.Equal(storage.UnitCapacity + 1, actual.UnitCapacity);
                Assert.Equal(storage.Revision + 1, actual.Revision);
            }
            else { Assert.Same(storage, actual); }
        }
        foreach (CountedSourceLot existing in world.Inventory.Custody.Lots)
        {
            Assert.Same(existing, result.Inventory.Custody.GetLot(existing.LotId));
        }
        Assert.Equal(world.Inventory.ReservationIds, result.Inventory.ReservationIds);
        Assert.Equal(world.Inventory.Policies, result.Inventory.Policies);
        Assert.Equal(world.Inventory.Controls, result.Inventory.Controls);
        Assert.Equal(world.Production!.Batches, result.Production.Batches);
        Assert.Equal(world.Production.Lots, result.Production.Lots);
        Assert.Contains(world.Production.Recipes[0], result.Production.Recipes);
    }

    [Fact]
    public void A_full_existing_wren_bag_preserves_all_contents_and_gains_only_the_kit_allowance()
    {
        WorldSnapshot basis = CreateQuestGenesis();
        EntityId playerBag = Player(basis).InventoryCustodianId;
        EntityId wrenBag = Wren(basis).InventoryCustodianId;
        CountedSourceLot wood = basis.Inventory.Custody.Lots.Single(value => value.CustodianId == playerBag
            && value.Definition == basis.Quests!.Definitions[0].Item);
        Assert.Equal(8UL, wood.Quantity);
        Assert.Equal(8UL, basis.Inventory.Custody.GetStorage(wrenBag).UnitCapacity);
        var moved = new CountedSourceLot(wood.LotId, wrenBag, wood.Definition, wood.Source, 1);
        var custody = new CustodySnapshot(basis.WorldId, basis.BranchId, 1,
            basis.Inventory.Custody.Storages.Select(value => value.CustodianId == playerBag || value.CustodianId == wrenBag
                ? new CustodianStorage(value.CustodianId, value.UnitCapacity, 1) : value),
            basis.Inventory.Custody.Lots.Select(value => value.LotId == wood.LotId ? moved : value));
        var inventory = new CountedAuthorityState(custody, basis.Inventory.AdmittedUnitCapacity, basis.Inventory.Actors,
            basis.Inventory.Controls, basis.Inventory.Policies, basis.Inventory.Sources, basis.Inventory.ReservationIds);
        var world = new WorldSnapshot(inventory, basis.Space, basis.Residents, basis.AdmissionVersion, basis.Calendar,
            1, 0, basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests);
        ConstructionFarmComponents result = ConstructionFarmAdmission.CreateConstruction(world);
        Assert.Same(moved, result.Inventory.Custody.GetLot(moved.LotId));
        Assert.Equal(9UL, result.Inventory.Custody.GetStorage(wrenBag).UnitCapacity);
        Assert.Equal(2UL, result.Inventory.Custody.GetStorage(wrenBag).Revision);
        Assert.Equal(9UL, result.Inventory.Custody.Lots.Where(value => value.CustodianId == wrenBag).Aggregate(0UL, (sum, value) => sum + value.Quantity));
        Assert.Equal(2UL, result.Inventory.Custody.Revision);
        Assert.Equal(0UL, result.Construction.Projects[0].Progress.CompletedUnits);
    }

    [Fact]
    public void Exact_mass_and_ten_disjoint_members_reuse_original_wood_and_offcut_bindings()
    {
        WorldSnapshot world = CreateQuestGenesis();
        ConstructionDefinition construction = ConstructionFarmAdmission.CreateConstruction(world).Construction.Definitions[0];
        CraftRecipeDefinition recipe = construction.MaterialTransform;
        Assert.Equal(ConstructionStockInterpretation.AuthoredJoineryBlankStock, construction.StockInterpretation);
        Assert.Equal(200UL, construction.RequiredWork.Value);
        Assert.Equal(1UL, construction.WorkRate.Numerator);
        Assert.Equal(1800UL, construction.WorkRate.Denominator);
        Assert.Equal(10, construction.Members.Count);
        Assert.Equal(540000UL, construction.Members.Aggregate(0UL, (sum, value) => sum + value.VolumeMicrolitres));
        Assert.Equal(6UL, Assert.Single(recipe.Inputs).Count);
        Assert.Equal(new ulong[] { 1, 3 }, recipe.Outputs.Select(value => value.Count));
        ItemPhysicalBinding wood = recipe.Bindings.Single(value => value.Physical.Key == "wood");
        ItemPhysicalBinding offcut = recipe.Bindings.Single(value => value.Physical.Key == "wood_offcuts");
        Assert.Same(world.Production!.Recipes[0].Bindings.Single(value => value.Physical.Key == "wood"), wood);
        Assert.Same(world.Production.Recipes[0].Bindings.Single(value => value.Physical.Key == "wood_offcuts"), offcut);
        ItemPhysicalBinding guard = recipe.BindingFor(recipe.ItemFor(recipe.Outputs[0]));
        Assert.Equal(270000UL, guard.Physical.MassPerUnit.Value);
        Assert.Equal(540000UL, guard.Physical.VolumePerUnit.Value);
        Assert.Equal(10000UL, offcut.Physical.MassPerUnit.Value);
        Assert.Equal(300000UL, wood.Physical.MassPerUnit.Value * 6);
        Assert.Equal(new ulong[] { 270000, 10000 }, recipe.Allocations.Select(value => value.MassPerOutputUnit.Value));
        Assert.Equal(ConstructionToolOperation.CutCrossSlots, construction.RequiredTool.Operation);
        Assert.DoesNotContain(world.Gathering!.Tools, value => value.Definition == construction.RequiredTool.Item);
    }

    [Fact]
    public void Source_crop_site_and_both_actor_gate_paths_remain_clear_with_builder_at_work()
    {
        WorldSnapshot world = CreateQuestGenesis();
        ConstructionProject project = ConstructionFarmAdmission.CreateConstruction(world).Construction.Projects[0];
        Assert.Equal(new SubunitPosition(27213, 83072, 256), project.SiteFeet);
        Assert.Equal(new SubunitPosition(27264, 83328, 256), project.WorkFeet);
        Assert.Equal(ConstructionStockLocation.BuilderCarried, project.StockLocation);
        Assert.Equal(Wren(world).ActorId, project.OwnerActorId);
        Assert.Equal(project.OwnerActorId, project.BuilderActorId);
        Assert.Equal(world.Quests!.Definitions[0].DestinationCustodianId, project.ProjectCustodianId);
        Assert.NotEqual(Wren(world).InventoryCustodianId, project.ProjectCustodianId);
        Assert.Equal(4, project.RequiredAccess.Count);
        Assert.DoesNotContain(project.RequiredAccess, value => value.ActorId == Player(world).ActorId && value.From == project.WorkFeet);
        ActorPresence builder = world.Space.GetActor(project.BuilderActorId);
        var atWork = new ActorPresence(builder.Binding, builder.Condition, builder.Scope, project.WorkFeet, builder.Revision, builder.HomePlaceId);
        LocalSpatialSnapshot space = world.Space.WithActors(world.Space.Actors.Select(value => value.ActorId == builder.ActorId ? atWork : value));
        foreach (GroundAccessRequirement access in project.RequiredAccess)
        {
            Assert.Equal(GroundRouteStatus.Reachable, space.QueryGroundRoute(access.ActorId, access.From, access.To).Status);
        }
        var structure = project.Definition.CreateStructure(new EntityId(0, 8_000_001), project.SiteFeet);
        StructurePlacementResult placement = space.PrepareStructurePlacement(structure, project.RequiredAccess);
        Assert.Equal(StructurePlacementStatus.Prepared, placement.Status);
        Assert.Equal(SpatialOccupancy.BlockedStructure, placement.Candidate!.QueryOccupancy(builder.ActorId,
            new SubunitPosition(27264, 83072, 256)));
        foreach (GroundAccessRequirement access in project.RequiredAccess)
        {
            Assert.Equal(GroundRouteStatus.Reachable, placement.Candidate.QueryGroundRoute(access.ActorId, access.From, access.To).Status);
        }
        Assert.DoesNotContain(project.Definition.CreateStructure(new EntityId(0, 8_000_002), project.SiteFeet).Boxes,
            box => box.Minimum.X < 27213 || box.Maximum.X > 27316 || box.Minimum.Z < 256 || box.Maximum.Z > 319);
    }

    [Fact]
    public void Occupied_site_does_not_move_actor_or_install_free_geometry_during_admission()
    {
        WorldSnapshot original = CreateQuestGenesis();
        ActorPresence actor = original.Space.GetActor(Player(original).ActorId);
        var atSite = new ActorPresence(actor.Binding, actor.Condition, actor.Scope,
            new SubunitPosition(27264, 83072, 256), actor.Revision, actor.HomePlaceId);
        WorldSnapshot world = Copy(original, space: original.Space.WithActors(original.Space.Actors.Select(value => value.ActorId == actor.ActorId ? atSite : value)));
        byte[] bytes = WorldSnapshotCodec.Encode(world);
        ConstructionFarmComponents result = ConstructionFarmAdmission.CreateConstruction(world);
        ConstructionProject project = result.Construction.Projects[0];
        Assert.False(project.IsCompleted);
        Assert.Equal(0UL, project.Progress.CompletedUnits);
        Assert.Empty(result.Construction.GetStructures());
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(world));
        Assert.Equal(StructurePlacementStatus.BlockedActor, world.Space.PrepareStructurePlacement(
            project.Definition.CreateStructure(new EntityId(0, 8_000_003), project.SiteFeet), project.RequiredAccess).Status);
    }

    [Fact]
    public void Real_completed_and_paid_supply_agreement_retains_rights_and_progress_in_content_candidate()
    {
        WorldSnapshot world = CompleteQuest();
        byte[] before = WorldSnapshotCodec.Encode(world);
        ConstructionFarmComponents result = ConstructionFarmAdmission.CreateConstruction(world);
        Assert.Equal(before, WorldSnapshotCodec.Encode(world));
        Assert.Equal(ActorQuestStatus.Completed, Assert.Single(world.Quests!.Participations).Status);
        Assert.Equal(3, world.Progression!.GetActor(Player(world).ActorId).Level);
        Assert.Equal(200UL, world.Progression.Currency.GetActorAccount(Player(world).ActorId).Balance);
        DeliveryQuestDefinition quest = world.Quests.Definitions[0];
        CountedSourceLot reserved = Assert.Single(world.Inventory.Custody.Lots, value => value.ReservedBy == quest.MaterialReservationId);
        Assert.Equal(6UL, reserved.Quantity);
        Assert.Same(reserved, result.Inventory.Custody.GetLot(reserved.LotId));
        Assert.Equal(quest.DefinitionId, result.Construction.Projects[0].QuestId);
        Assert.Throws<ArgumentException>(() => ConstructionFarmAdmission.CreateGenesis(world));
    }

    [Fact]
    public void Fresh_factory_creates_profile_five_at_zero_and_refuses_reusing_it()
    {
        WorldSnapshot basis = CreateQuestGenesis();
        WorldSnapshot fresh = ConstructionFarmAdmission.CreateGenesis(basis);
        Assert.Equal((ushort)5, fresh.ProfileVersion);
        Assert.Equal(0UL, fresh.Revision);
        Assert.Equal(0UL, fresh.AdvancingSteps);
        Assert.Equal(0UL, fresh.Inventory.Custody.Revision);
        Assert.All(fresh.Inventory.Custody.Storages, value => Assert.Equal(0UL, value.Revision));
        Assert.Equal(basis.Calendar, fresh.Calendar);
        Assert.Same(basis.Space, fresh.Space);
        Assert.Same(basis.Gathering, fresh.Gathering);
        Assert.Same(basis.Progression, fresh.Progression);
        Assert.Same(basis.Quests, fresh.Quests);
        Assert.Empty(fresh.Space.Structures);
        Assert.Equal(WorldSnapshotCodec.Encode(fresh), WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(fresh))));
        Assert.Throws<ArgumentException>(() => ConstructionFarmAdmission.CreateGenesis(fresh));
        Assert.Throws<ArgumentException>(() => ConstructionFarmAdmission.CreateConstruction(fresh));
        Assert.Throws<ArgumentException>(() => ConstructionFarmAdmission.CreateGenesis(Copy(basis, revision: 1)));
        Assert.Throws<ArgumentException>(() => ConstructionFarmAdmission.CreateGenesis(Copy(basis, calendar: basis.Calendar.AddMilliseconds(1))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Fresh_factory_rejects_retained_terrain_edits_or_body_history_even_if_other_counters_are_zero(bool injury)
    {
        WorldSnapshot world = CreateQuestGenesis();
        LocalSpatialSnapshot space;
        if (injury)
        {
            ActorPresence actor = world.Space.Actors[0];
            var scar = new BodyInjury(new EntityId(0, 8_000_100), new EntityId(0, 8_000_101),
                actor.Condition.Definition.Parts[0].Slot, BodyInjuryKind.Scar);
            var condition = new BodyConditionSnapshot(actor.Binding.FormInstanceId, actor.Condition.Definition, [scar]);
            var changed = new ActorPresence(actor.Binding, condition, actor.Scope, actor.Feet, actor.Revision, actor.HomePlaceId);
            space = world.Space.WithActors(world.Space.Actors.Select(value => value.ActorId == actor.ActorId ? changed : value));
        }
        else
        {
            var region = world.Space.Regions[0];
            var changed = region.Excavate(region.Revision, [region.Bounds.Minimum]);
            space = new LocalSpatialSnapshot(world.Space.Scope, [changed], world.Space.Actors);
        }
        WorldSnapshot retained = Copy(world, space: space);
        byte[] before = WorldSnapshotCodec.Encode(retained);
        Assert.Throws<ArgumentException>(() => ConstructionFarmAdmission.CreateGenesis(retained));
        ConstructionFarmComponents admission = ConstructionFarmAdmission.CreateConstruction(retained);
        Assert.Equal(before, WorldSnapshotCodec.Encode(retained));
        Assert.Equal(0UL, admission.Construction.Projects[0].Progress.CompletedUnits);
    }

    [Fact]
    public void Repeated_embedded_and_filesystem_bytes_match_while_instance_ids_change_with_seed()
    {
        WorldSnapshot world = CreateQuestGenesis();
        ConstructionFarmComponents embedded = ConstructionFarmAdmission.CreateConstruction(world);
        ConstructionFarmComponents file = ConstructionFarmAdmission.CreateConstruction(FarmContentFixture.InstalledContentRoot(), world);
        Assert.Equal(AuthorityEncodingV1.Encode(embedded.Inventory), AuthorityEncodingV1.Encode(file.Inventory));
        Assert.Equal(ProductionCodecV1.Encode(embedded.Production), ProductionCodecV1.Encode(file.Production));
        Assert.Equal(ConstructionCodecV1.Encode(embedded.Construction), ConstructionCodecV1.Encode(file.Construction));
        ConstructionFarmComponents other = ConstructionFarmAdmission.CreateConstruction(CreateQuestGenesis("second frontier"));
        Assert.Equal(embedded.Construction.Definitions[0].Version, other.Construction.Definitions[0].Version);
        Assert.NotEqual(embedded.Construction.Projects[0].ProjectId, other.Construction.Projects[0].ProjectId);
        Assert.NotEqual(embedded.Admission.Tools[0].Source.SourceId, other.Admission.Tools[0].Source.SourceId);
        string[] names = typeof(ConstructionFarmAdmission).Assembly.GetManifestResourceNames().Select(value => value.Replace('\\', '/')).ToArray();
        Assert.Contains("OpenFantasy.Content/construction-farm/admission-v1.json", names);
        Assert.Contains("OpenFantasy.Content/construction-farm/source-support/WorldRenderer.cs.txt", names);
    }

    [Fact]
    public void Null_wrong_profiles_admission_and_persona_fail_without_borrowing_source_identity()
    {
        Assert.Throws<ArgumentNullException>(() => ConstructionFarmAdmission.CreateConstruction(null!));
        Assert.Throws<ArgumentNullException>(() => ConstructionFarmAdmission.CreateGenesis(null!));
        Assert.Throws<ArgumentException>(() => ConstructionFarmAdmission.CreateConstruction(GatheringFarmAdmission.CreateEmbedded("river", "Orin")));
        WorldSnapshot world = CreateQuestGenesis();
        Assert.Throws<ArgumentException>(() => ConstructionFarmAdmission.CreateConstruction(Copy(world, admission: ContentHash.Compute("wrong"u8))));
        WorldResident wren = Wren(world);
        var changed = new WorldResident(wren.ActorId, wren.InventoryCustodianId, "Another person", wren.ControllerKind,
            wren.HomeFeet, wren.WanderRadiusCells, wren.Comprehension, wren.ExperiencedTime, wren.OpeningUtterance);
        Assert.Throws<ArgumentException>(() => ConstructionFarmAdmission.CreateConstruction(Copy(world,
            residents: world.Residents.Select(value => value == wren ? changed : value))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void New_definition_or_instance_cannot_alias_an_existing_place(bool instance)
    {
        WorldSnapshot world = CreateQuestGenesis();
        ConstructionFarmComponents expected = ConstructionFarmAdmission.CreateConstruction(world);
        EntityId collision = instance ? expected.Admission.Tools[0].Source.SourceId : expected.Construction.Definitions[0].DefinitionId;
        ActorPresence actor = world.Space.Actors[0];
        var changed = new ActorPresence(actor.Binding, actor.Condition, actor.Scope, actor.Feet, actor.Revision, collision);
        var space = world.Space.WithActors(world.Space.Actors.Select(value => value.ActorId == actor.ActorId ? changed : value));
        Assert.Throws<InvalidDataException>(() => ConstructionFarmAdmission.CreateConstruction(Copy(world, space: space)));
    }

    [Theory]
    [InlineData("construction-farm/admission-v1.json")]
    [InlineData("construction-farm/source-support/WorldRenderer.cs.txt")]
    [InlineData("quest-farm/admission-v1.json")]
    [InlineData("quest-farm/source-support/Entity.cs.txt")]
    [InlineData("legacy/aldermere-first-loop/source-support/data/world.json")]
    [InlineData("legacy/aldermere-first-loop/source-support/data/items.json")]
    [InlineData("legacy/aldermere-first-loop/assets/tiles.png")]
    [InlineData("legacy/aldermere-first-loop/assets/units/node_berry.png")]
    public void Modified_manifest_sources_or_unselected_closure_files_are_rejected(string relative)
    {
        WorldSnapshot world = CreateQuestGenesis();
        using var files = new ConstructionContentFixture();
        File.AppendAllText(files.PathFor(relative), " ");
        Assert.Throws<InvalidDataException>(() => ConstructionFarmAdmission.CreateConstruction(files.ContentRoot, world));
    }

    [Fact]
    public void Missing_and_oversized_construction_manifest_do_not_fall_back()
    {
        WorldSnapshot world = CreateQuestGenesis();
        using var files = new ConstructionContentFixture();
        string path = files.PathFor("construction-farm/admission-v1.json");
        File.WriteAllBytes(path, new byte[16385]);
        Assert.Throws<InvalidDataException>(() => ConstructionFarmAdmission.CreateConstruction(files.ContentRoot, world));
        File.Delete(path);
        Assert.Throws<FileNotFoundException>(() => ConstructionFarmAdmission.CreateConstruction(files.ContentRoot, world));
    }

    internal static WorldSnapshot CreateQuestGenesis(string seed = "river")
    {
        WorldSnapshot gathered = GatheringFarmAdmission.CreateEmbedded(seed, "Orin");
        ProductionSnapshot production = ProductionFarmAdmission.CreateProduction(gathered);
        var produced = new WorldSnapshot(gathered.Inventory, gathered.Space, gathered.Residents,
            WorldProductionMigration.ComputeTargetAdmissionVersion(gathered.AdmissionVersion, production), gathered.Calendar,
            0, 0, gathered.Dialogues, gathered.Gathering, production);
        QuestFarmComponents quest = QuestFarmAdmission.CreateQuests(produced);
        return new WorldSnapshot(quest.Inventory, produced.Space, produced.Residents,
            WorldQuestMigration.ComputeTargetAdmissionVersion(produced.AdmissionVersion, quest.Admission), produced.Calendar,
            0, 0, produced.Dialogues, produced.Gathering, produced.Production, quest.Progression, quest.Quests);
    }

    private static WorldSnapshot CompleteQuest()
    {
        WorldSnapshot world = CreateQuestGenesis();
        WorldResident player = Player(world);
        ActorControlBinding control = world.Inventory.Controls.Single(value => value.ActorId == player.ActorId);
        var delivery = new TrustedCommandDelivery(control.PrincipalId, control.SessionId);
        var processor = new WorldStepProcessor();
        ulong cause = 8_010_000;
        foreach ((long x, long y) in Enumerable.Repeat((32L, 0L), 8).Concat(Enumerable.Repeat((0L, -32L), 24)))
        {
            world = processor.Decide(world, new WorldStepBatch(new EntityId(0, cause++), world.WorldId, world.BranchId,
                world.Revision, world.AdvancingSteps, WorldBatchKind.AdvancingStep,
                [new WorldMovementInput(delivery, player.ActorId, x, y)], [], world.ProfileVersion)).Candidate;
        }
        WorldSnapshot Act(WorldActionIntent intent)
        {
            var command = new WorldActionCommand(new EntityId(0, cause++), control.PrincipalId, control.SessionId,
                player.ActorId, world.WorldId, world.BranchId, world.Space.GetActor(player.ActorId).Revision, intent);
            WorldStepDecision decision = processor.Decide(world, new WorldStepBatch(new EntityId(0, cause++), world.WorldId, world.BranchId,
                world.Revision, world.AdvancingSteps, world.HasReadingHold ? WorldBatchKind.HeldInteraction : WorldBatchKind.AdvancingStep,
                [], [command], world.ProfileVersion));
            Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(decision.Receipts).Status);
            return decision.Candidate;
        }
        DeliveryQuestDefinition quest = world.Quests!.Definitions[0];
        world = Act(new SpeakToActorIntent(quest.GiverActorId, world.Space.GetActor(quest.GiverActorId).Revision));
        world = Act(new AcceptDeliveryQuestIntent(quest.DefinitionId, quest.Version, quest.GiverActorId,
            world.Space.GetActor(quest.GiverActorId).Revision, 0, world.Progression!.GetActor(player.ActorId).Revision));
        CountedSourceLot wood = world.Inventory.Custody.Lots.Single(value => value.CustodianId == player.InventoryCustodianId && value.Definition == quest.Item);
        world = Act(new CompleteDeliveryQuestIntent(quest.DefinitionId, quest.Version, quest.GiverActorId,
            world.Space.GetActor(quest.GiverActorId).Revision, world.Quests!.Participations[0].Revision,
            world.Progression!.GetActor(player.ActorId).Revision, world.Inventory.Custody.GetStorage(player.InventoryCustodianId).Revision,
            [new SourceLotSelection(wood.LotId, wood.Revision, 6)]));
        return world;
    }

    private static WorldResident Wren(WorldSnapshot world) => world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Host);
    private static WorldResident Player(WorldSnapshot world) => world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player);
    private static WorldSnapshot Copy(WorldSnapshot world, LocalSpatialSnapshot? space = null, ulong? revision = null,
        WorldInstant? calendar = null, ContentHash? admission = null, IEnumerable<WorldResident>? residents = null) =>
        new(world.Inventory, space ?? world.Space, residents ?? world.Residents, admission ?? world.AdmissionVersion,
            calendar ?? world.Calendar, revision ?? world.Revision, world.AdvancingSteps, world.Dialogues,
            world.Gathering, world.Production, world.Progression, world.Quests);

    private sealed class ConstructionContentFixture : IDisposable
    {
        private readonly FarmContentFixture _basis = new();
        internal ConstructionContentFixture()
        {
            string source = FarmContentFixture.InstalledContentRoot();
            foreach (string name in new[] { "adventure-farm", "production-farm", "quest-farm", "construction-farm" })
            {
                foreach (string file in Directory.EnumerateFiles(Path.Combine(source, name), "*", SearchOption.AllDirectories))
                {
                    string destination = PathFor(Path.GetRelativePath(source, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(file, destination);
                }
            }
        }
        internal string ContentRoot => _basis.ContentRoot;
        internal string PathFor(string relative) => _basis.PathFor(relative);
        public void Dispose() => _basis.Dispose();
    }
}
