using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.Content;

public sealed class QuestFarmAdmissionTests
{
    [Fact]
    public void Preserves_actual_gathered_and_crafted_state_and_adds_only_empty_project_custody()
    {
        WorldSnapshot world = GatherAndCraft();
        byte[] before = WorldSnapshotCodec.Encode(world);
        QuestFarmComponents result = QuestFarmAdmission.CreateQuests(world);
        Assert.Equal(before, WorldSnapshotCodec.Encode(world));
        Assert.Equal(WorldQuestMigrationCodecV1.EncodeAdmission(QuestFarmAdmission.CreateQuests(Create()).Admission),
            WorldQuestMigrationCodecV1.EncodeAdmission(result.Admission));
        Assert.Single(world.Production!.Batches);
        Assert.Equal(2UL, world.Gathering!.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Berry).NextUnissuedUnit);
        Assert.NotEqual(0UL, world.Inventory.Custody.Revision);
        Assert.Equal(world.Inventory.Custody.Revision, result.Inventory.Custody.Revision);
        Assert.Equal(world.Inventory.AdmittedUnitCapacity, result.Inventory.AdmittedUnitCapacity);
        Assert.Equal(world.Inventory.Actors, result.Inventory.Actors);
        Assert.Equal(world.Inventory.Controls, result.Inventory.Controls);
        Assert.Equal(world.Inventory.Sources, result.Inventory.Sources);
        Assert.Equal(world.Inventory.Custody.Lots, result.Inventory.Custody.Lots);
        foreach (CustodianStorage original in world.Inventory.Custody.Storages)
        {
            Assert.Same(original, result.Inventory.Custody.GetStorage(original.CustodianId));
        }
        foreach (CustodianAccessPolicy original in world.Inventory.Policies)
        {
            Assert.Contains(original, result.Inventory.Policies);
        }
        DeliveryQuestDefinition quest = Assert.Single(result.Quests.Definitions);
        CustodianStorage project = result.Inventory.Custody.GetStorage(quest.DestinationCustodianId);
        Assert.Equal(6UL, project.UnitCapacity);
        Assert.Equal(0UL, project.Revision);
        Assert.DoesNotContain(result.Inventory.Custody.Lots, lot => lot.CustodianId == project.CustodianId);
        Assert.Equal(world.Inventory.Custody.Storages.Count + 1, result.Inventory.Custody.Storages.Count);
        Assert.Equal(world.Inventory.ReservationIds.Append(quest.MaterialReservationId).Order(), result.Inventory.ReservationIds);
        Assert.Equal(0UL, result.Quests.Revision);
        Assert.Empty(result.Quests.Participations);
        Assert.Equal(0UL, result.Progression.Revision);
        QuestProjectCustody admittedProject = Assert.Single(result.Admission.Projects);
        Assert.Equal(project.CustodianId, admittedProject.CustodianId);
        Assert.Equal(quest.MaterialReservationId, admittedProject.ReservationId);
        Assert.Equal(quest.GiverActorId, admittedProject.OwnerActorId);
        Assert.Equal(6UL, admittedProject.UnitCapacity);
        result.Quests.ValidateForAuthority(result.Inventory, result.Progression);
        world.Production.ValidateForAuthority(result.Inventory);
    }

    [Fact]
    public void Quest_retains_original_obligation_offer_giver_and_reward_with_separate_project_stock()
    {
        WorldSnapshot world = Create();
        QuestFarmComponents result = QuestFarmAdmission.CreateQuests(world);
        DeliveryQuestDefinition quest = Assert.Single(result.Quests.Definitions);
        Assert.Equal("wren_orchard", quest.Key);
        // Independent Python source/admission oracle pins the world/branch framing for seed "river".
        Assert.Equal("f73755cb5c792d4f8764077964cd65cd", quest.DefinitionId.ToString());
        Assert.Equal("ee0c80713a4af81aae497d81720aea69", quest.DestinationCustodianId.ToString());
        Assert.Equal("d04a4adebe4beb8b89bef83efd74e32b", quest.Reward.ReserveAccountId.ToString());
        Assert.Equal("Wren's Saplings", quest.Title);
        Assert.Equal("Six wood for a windbreak and my saplings might see spring.", quest.Description);
        WorldResident wren = world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Host);
        Assert.Equal(wren.ActorId, quest.GiverActorId);
        Assert.Equal(1U, quest.MinimumLevel);
        Assert.Equal(6UL, quest.Quantity);
        Assert.Equal("f20a24bbcf04b36ebcb90a1d04ca58c0", quest.Item.DefinitionId.ToString());
        Assert.Equal("c603d104fd21c40819d8dc0f6a4f5f3bc976936f7ac2c2cfe6ffefd62820614d", quest.Item.Version.Value);
        Assert.Equal(420UL, quest.Reward.Experience);
        Assert.Equal(200UL, quest.Reward.CurrencyAmount);
        Assert.NotEqual(wren.InventoryCustodianId, quest.DestinationCustodianId);
        CustodianAccessPolicy policy = Assert.Single(result.Inventory.Policies, value => value.CustodianId == quest.DestinationCustodianId);
        Assert.Equal(wren.ActorId, policy.OwnerActorId);
        Assert.Equal(CustodyDepositRule.AnyControlledActor, policy.DepositRule);
    }

    [Fact]
    public void Explicit_human_squire_progression_retains_source_base_growth_thresholds_and_full_initial_vitals()
    {
        WorldSnapshot world = Create();
        ActorProgressionState actor = QuestFarmAdmission.CreateQuests(world).Progression.GetActor(Player(world).ActorId);
        Assert.Equal("human_squire", actor.Profile.Key);
        Assert.Equal(ActorStatProfileKind.Class, actor.Profile.Kind);
        Assert.Equal(new ActorStatValues(60, 10, 12, 4, 8, 8), actor.Stats);
        Assert.Equal(new ActorStatModifiers(9, 2, 3, 1, 2, 1), actor.Profile.Growth);
        Assert.Equal(ActorStatModifiers.Zero, actor.Profile.HeritageModifiers);
        Assert.Equal(new ActorStatValues(78, 14, 18, 6, 12, 10), actor.Profile.Calculate(3));
        Assert.Equal(new ulong[] { 100, 282, 519, 800, 1118, 1469, 1852, 2262, 2700 }, actor.Curve.Thresholds);
        Assert.Equal(10, actor.Curve.MaximumLevel);
        Assert.Equal(1, actor.Level);
        Assert.Equal(0UL, actor.Experience);
        Assert.Equal(60, actor.CurrentHp);
        Assert.Equal(10, actor.CurrentMp);
    }

    [Fact]
    public void Wren_preserves_fixed_source_unassigned_stats_without_a_fictional_farmer_or_player_class()
    {
        WorldSnapshot world = Create();
        EntityId wren = world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Host).ActorId;
        ActorProgressionState actor = QuestFarmAdmission.CreateQuests(world).Progression.GetActor(wren);
        Assert.Equal("wren_unassigned", actor.Profile.Key);
        Assert.Equal("Unassigned", actor.Profile.DisplayName);
        Assert.Equal(ActorStatProfileKind.Unassigned, actor.Profile.Kind);
        Assert.Equal(new ActorStatValues(1, 0, 0, 0, 0, 1), actor.Stats);
        Assert.Equal(actor.Stats, actor.Profile.Calculate(10));
        Assert.Equal(ActorStatModifiers.Zero, actor.Profile.Growth);
        Assert.Equal(ActorStatModifiers.Zero, actor.Profile.HeritageModifiers);
        Assert.Equal(0UL, actor.Experience);
        Assert.Equal(1, actor.CurrentHp);
        Assert.Equal(0, actor.CurrentMp);
    }

    [Fact]
    public void Both_personal_balances_start_at_source_zero_and_only_the_authored_reserve_funds_the_reward()
    {
        WorldSnapshot world = Create();
        QuestFarmComponents result = QuestFarmAdmission.CreateQuests(world);
        CurrencyLedger ledger = result.Progression.Currency;
        Assert.Equal("gil", ledger.Currency.Key);
        Assert.Equal(200UL, ledger.AdmittedSupply);
        Assert.Equal(3, ledger.Accounts.Count);
        foreach (WorldResident actor in world.Residents)
        {
            Assert.Equal(0UL, ledger.GetActorAccount(actor.ActorId).Balance);
        }
        CurrencyAccount reserve = Assert.Single(ledger.Accounts, value => value.Kind == CurrencyAccountKind.Reserve);
        Assert.Null(reserve.ActorId);
        Assert.Equal(200UL, reserve.Balance);
        Assert.Equal(reserve.AccountId, result.Quests.Definitions[0].Reward.ReserveAccountId);
        Assert.DoesNotContain(result.Inventory.Custody.Storages, value => value.CustodianId == reserve.AccountId);
        Assert.DoesNotContain(result.Inventory.Sources, value => value.Definition.DefinitionId == ledger.Currency.DefinitionId);
    }

    [Fact]
    public void Exact_embedded_and_filesystem_admissions_match_and_definitions_remain_stable_across_world_seeds()
    {
        WorldSnapshot world = Create();
        QuestFarmComponents first = QuestFarmAdmission.CreateQuests(world);
        QuestFarmComponents repeated = QuestFarmAdmission.CreateQuests(world);
        QuestFarmComponents files = QuestFarmAdmission.CreateQuests(FarmContentFixture.InstalledContentRoot(), world);
        foreach (QuestFarmComponents other in new[] { repeated, files })
        {
            Assert.Equal(AuthorityEncodingV1.Encode(first.Inventory), AuthorityEncodingV1.Encode(other.Inventory));
            Assert.Equal(ProgressionCodecV1.Encode(first.Progression), ProgressionCodecV1.Encode(other.Progression));
            Assert.Equal(QuestCodecV1.Encode(first.Quests), QuestCodecV1.Encode(other.Quests));
        }
        QuestFarmComponents differentWorld = QuestFarmAdmission.CreateQuests(Create("another frontier"));
        Assert.NotEqual(first.Inventory.WorldId, differentWorld.Inventory.WorldId);
        Assert.Equal(first.Progression.Profiles.Select(value => value.Version), differentWorld.Progression.Profiles.Select(value => value.Version));
        Assert.Equal(first.Progression.Curves.Select(value => value.Version), differentWorld.Progression.Curves.Select(value => value.Version));
        Assert.Equal(first.Progression.Currency.Currency.Version, differentWorld.Progression.Currency.Currency.Version);
        Assert.NotEqual(first.Quests.Definitions[0].DefinitionId, differentWorld.Quests.Definitions[0].DefinitionId);
        Assert.NotEqual(first.Quests.Definitions[0].Reward.ReserveAccountId, differentWorld.Quests.Definitions[0].Reward.ReserveAccountId);
        Assert.Equal(QuestFarmAdmission.AdmissionVersion, first.AdmissionVersion);
        Assert.Equal(WorldQuestMigrationCodecV1.EncodeAdmission(first.Admission),
            WorldQuestMigrationCodecV1.EncodeAdmission(files.Admission));
        string[] names = typeof(QuestFarmAdmission).Assembly.GetManifestResourceNames().Select(value => value.Replace('\\', '/')).ToArray();
        Assert.Contains("OpenFantasy.Content/quest-farm/admission-v1.json", names);
        Assert.Contains("OpenFantasy.Content/quest-farm/source-support/Entity.cs.txt", names);
    }

    [Fact]
    public void Unsupported_profiles_null_and_wrong_admission_cannot_acquire_the_pinned_quest()
    {
        Assert.Throws<ArgumentNullException>(() => QuestFarmAdmission.CreateQuests(null!));
        Assert.Throws<ArgumentException>(() => QuestFarmAdmission.CreateQuests(FarmWorldAdmission.CreateEmbedded("river", "Orin")));
        Assert.Throws<ArgumentException>(() => QuestFarmAdmission.CreateQuests(GatheringFarmAdmission.CreateEmbedded("river", "Orin")));
        WorldSnapshot world = Create();
        var changed = new WorldSnapshot(world.Inventory, world.Space, world.Residents, ContentHash.Compute("wrong-admission"u8),
            world.Calendar, world.Revision, world.AdvancingSteps, world.Dialogues, world.Gathering, world.Production);
        Assert.Throws<ArgumentException>(() => QuestFarmAdmission.CreateQuests(changed));
    }

    [Fact]
    public void Changed_but_valid_host_persona_cannot_borrow_wrens_source_definition()
    {
        WorldSnapshot world = Create();
        WorldResident wren = world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Host);
        var changed = new WorldResident(wren.ActorId, wren.InventoryCustodianId, "Different person", wren.ControllerKind,
            wren.HomeFeet, wren.WanderRadiusCells, wren.Comprehension, wren.ExperiencedTime, wren.OpeningUtterance);
        var other = new WorldSnapshot(world.Inventory, world.Space, world.Residents.Select(value => value == wren ? changed : value),
            world.AdmissionVersion, world.Calendar, world.Revision, world.AdvancingSteps, world.Dialogues, world.Gathering, world.Production);
        Assert.Throws<ArgumentException>(() => QuestFarmAdmission.CreateQuests(other));
    }

    [Fact]
    public void A_profile_with_removed_production_definitions_cannot_reuse_the_original_admission_pin()
    {
        WorldSnapshot world = Create();
        var empty = new ProductionSnapshot(world.WorldId, world.BranchId, 0, [], [], []);
        var changed = new WorldSnapshot(world.Inventory, world.Space, world.Residents, world.AdmissionVersion,
            world.Calendar, world.Revision, world.AdvancingSteps, world.Dialogues, world.Gathering, empty);
        Assert.Throws<ArgumentException>(() => QuestFarmAdmission.CreateQuests(changed));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void New_definition_and_instance_roles_cannot_alias_an_existing_place(bool instance)
    {
        WorldSnapshot world = Create();
        QuestFarmComponents planned = QuestFarmAdmission.CreateQuests(world);
        EntityId collision = instance ? planned.Quests.Definitions[0].DestinationCustodianId : planned.Progression.Profiles[0].DefinitionId;
        ActorPresence actor = world.Space.Actors[0];
        var changed = new ActorPresence(actor.Binding, actor.Condition, actor.Scope, actor.Feet, actor.Revision, collision);
        var space = new LocalSpatialSnapshot(world.Space.Scope, world.Space.Regions,
            world.Space.Actors.Select(value => value.ActorId == actor.ActorId ? changed : value));
        var validWorld = new WorldSnapshot(world.Inventory, space, world.Residents, world.AdmissionVersion,
            world.Calendar, world.Revision, world.AdvancingSteps, world.Dialogues, world.Gathering, world.Production);
        Assert.Throws<InvalidDataException>(() => QuestFarmAdmission.CreateQuests(validWorld));
    }

    [Theory]
    [InlineData("quest-farm/admission-v1.json")]
    [InlineData("quest-farm/source-support/Entity.cs.txt")]
    [InlineData("production-farm/admission-v1.json")]
    [InlineData("legacy/aldermere-first-loop/records.json")]
    [InlineData("legacy/aldermere-first-loop/source-support/data/classes.json")]
    [InlineData("legacy/aldermere-first-loop/source-support/data/quests.json")]
    [InlineData("legacy/aldermere-first-loop/source-support/src/Sim/World.Systems.cs.txt")]
    [InlineData("legacy/aldermere-first-loop/assets/units/node_berry.png")]
    public void Changed_manifest_selected_sources_default_witnesses_or_unselected_original_closure_reject(string path)
    {
        WorldSnapshot world = Create();
        using var fixture = new QuestContentFixture();
        File.AppendAllText(fixture.PathFor(path), " ");
        Assert.Throws<InvalidDataException>(() => QuestFarmAdmission.CreateQuests(fixture.ContentRoot, world));
    }

    [Fact]
    public void Oversized_or_missing_new_manifest_rejects_without_a_default_or_partial_admission()
    {
        WorldSnapshot world = Create();
        using var fixture = new QuestContentFixture();
        string path = fixture.PathFor("quest-farm/admission-v1.json");
        File.WriteAllBytes(path, new byte[16_385]);
        Assert.Throws<InvalidDataException>(() => QuestFarmAdmission.CreateQuests(fixture.ContentRoot, world));
        File.Delete(path);
        Assert.Throws<FileNotFoundException>(() => QuestFarmAdmission.CreateQuests(fixture.ContentRoot, world));
    }

    private static WorldResident Player(WorldSnapshot world) => world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player);

    private static WorldSnapshot Create(string seed = "river")
    {
        WorldSnapshot gathered = GatheringFarmAdmission.CreateEmbedded(seed, "Orin");
        ProductionSnapshot production = ProductionFarmAdmission.CreateProduction(gathered);
        var request = new WorldProductionMigration(new EntityId(0, 990_000), gathered.WorldId, gathered.BranchId,
            gathered.Revision, gathered.AdvancingSteps, gathered.ContentHash, gathered.AdmissionVersion,
            WorldProductionMigration.ComputeTargetAdmissionVersion(gathered.AdmissionVersion, production), production);
        return new WorldProductionMigrationProcessor().Decide(gathered, request).Candidate;
    }

    private static WorldSnapshot GatherAndCraft()
    {
        WorldSnapshot world = Create();
        EntityId actor = Player(world).ActorId;
        ActorControlBinding control = world.Inventory.Controls.Single(value => value.ActorId == actor);
        var delivery = new TrustedCommandDelivery(control.PrincipalId, control.SessionId);
        var processor = new WorldStepProcessor();
        for (ulong index = 0; index < 80; index++)
        {
            var movement = new WorldMovementInput(delivery, actor, index < 24 ? 32 : 0, index < 24 ? 0 : 32);
            var step = new WorldStepBatch(new EntityId(0, 991_000 + index), world.WorldId, world.BranchId,
                world.Revision, world.AdvancingSteps, WorldBatchKind.AdvancingStep, [movement], [], world.ProfileVersion);
            world = processor.Decide(world, step).Candidate;
        }
        for (ulong index = 0; index < 2; index++)
        {
            GatherNodeState node = world.Gathering!.Nodes.Single(value => value.Definition.Kind == GatherNodeKind.Berry);
            GatherToolDefinition sickle = world.Gathering.Tools.Single(value => value.Kind == GatherToolKind.Sickle);
            CountedSourceLot tool = world.Inventory.Custody.Lots.Single(value => value.Definition == sickle.Definition);
            var action = new WorldActionCommand(new EntityId(0, 993_000 + index), control.PrincipalId, control.SessionId,
                actor, world.WorldId, world.BranchId, world.Space.GetActor(actor).Revision,
                new GatherIntent(node.NodeId, node.Revision, tool.LotId, tool.Revision));
            var step = new WorldStepBatch(new EntityId(0, 992_000 + index), world.WorldId, world.BranchId,
                world.Revision, world.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [action], world.ProfileVersion);
            WorldStepDecision decision = processor.Decide(world, step);
            Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(decision.Receipts).Status);
            world = decision.Candidate;
        }
        CraftRecipeDefinition recipe = Assert.Single(world.Production!.Recipes);
        CountedItemDefinition wood = recipe.Bindings.Single(value => value.Physical.Key == "wood").Item;
        CountedItemDefinition berries = recipe.Bindings.Single(value => value.Physical.Key == "berries").Item;
        EntityId storage = Player(world).InventoryCustodianId;
        var ingredients = world.Inventory.Custody.Lots.Where(value => value.CustodianId == storage && value.Definition == berries)
            .Select(value => new IngredientRequest(IngredientLotKind.Source, value.LotId, value.Revision, value.Source.Quantity)).ToList();
        CountedSourceLot woodLot = world.Inventory.Custody.Lots.Single(value => value.CustodianId == storage && value.Definition == wood);
        ingredients.Add(new IngredientRequest(IngredientLotKind.Source, woodLot.LotId, woodLot.Revision, 1));
        var craft = new WorldActionCommand(new EntityId(0, 994_001), control.PrincipalId, control.SessionId, actor,
            world.WorldId, world.BranchId, world.Space.GetActor(actor).Revision,
            new CraftIntent(new CraftRequest(recipe.DefinitionId, recipe.Version, world.Production.Revision,
                storage, world.Inventory.Custody.GetStorage(storage).Revision, ingredients)));
        var batch = new WorldStepBatch(new EntityId(0, 994_000), world.WorldId, world.BranchId, world.Revision,
            world.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [craft], world.ProfileVersion);
        WorldStepDecision crafted = processor.Decide(world, batch);
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(crafted.Receipts).Status);
        return crafted.Candidate;
    }

    private sealed class QuestContentFixture : IDisposable
    {
        private readonly FarmContentFixture _basis = new();
        internal QuestContentFixture()
        {
            string source = FarmContentFixture.InstalledContentRoot();
            foreach (string name in new[] { "adventure-farm", "production-farm", "quest-farm" })
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
