using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Tests.Construction;
using OpenFantasy.Core.Tests.Progression;
using OpenFantasy.Core.Tests.Worlds;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Production
{
    public sealed class ProductionContinuationBoundaryTests
    {
        [Fact]
        public void Complete_world_refuses_material_only_continuation_without_reinterpreting_legacy_state()
        {
            var initial = ProductionWorldFixture.Create();
            var craft = ProductionWorldFixture.Craft(initial);
            var decision = new WorldStepProcessor().Decide(initial, ProductionWorldFixture.Batch(initial, 1300, craft));
            Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
            var world = decision.Candidate;
            byte[] before = WorldSnapshotCodec.Encode(world);
            var continued = world.Production!.Continue(world.Inventory);
            continued.ValidateForAuthority(world.Inventory);

            var error = Assert.Throws<ArgumentException>(() => new WorldSnapshot(world.Inventory, world.Space,
                world.Residents, world.AdmissionVersion, world.Calendar, world.Revision, world.AdvancingSteps,
                world.Dialogues, world.Gathering, continued));

            Assert.StartsWith("World admission requires a coordinated material, care and resource continuation profile.", error.Message);
            Assert.Equal(before, WorldSnapshotCodec.Encode(world));
            Assert.Equal(before, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(before)));
        }

        [Fact]
        public void Runoff_and_nested_archive_do_not_silently_drop_the_imported_context()
        {
            var request = Geography.WatershedRunoffTests.Create();
            var original = new WatershedRunoffPlanner().Prepare(request, TestContext.Current.CancellationToken);
            byte[] before = WatershedRunoffCodecV1.Encode(original);
            var inventory = request.Inventory;
            var inline = new ProductionSnapshot(inventory.WorldId, inventory.BranchId, 0, [], [], []);
            var production = inline.Continue(inventory);
            var admission = Assert.Throws<ArgumentException>(() => new WatershedRunoffRequest(request.Network, request.Mapping, inventory, production,
                request.ReservationId, request.OwnerActorId, request.Start, request.End, request.Material,
                request.Inputs, request.Terminals));
            Assert.StartsWith("Use the actual continued capacity owner to create a runoff request.", admission.Message);

            var qualifiedRequest = new WatershedRunoffRequest(request.Network, request.Mapping, inventory, inline,
                request.ReservationId, request.OwnerActorId, request.Start, request.End, request.Material,
                request.Inputs, request.Terminals);
            var capacity = WatershedRunoffCapacityState.Create(new WatershedRunoffPlanner()
                .Prepare(qualifiedRequest, TestContext.Current.CancellationToken));
            capacity = capacity.PlanContinue(TestContext.Current.CancellationToken).ApplyTo(capacity);
            var continuedRunoff = Assert.Single(capacity.Pending);
            var encoding = Assert.Throws<ArgumentException>(() => WatershedRunoffCodecV1.Encode(continuedRunoff));
            Assert.StartsWith("Geography archives require complete inline production history.", encoding.Message);
            byte[] explicitBytes = WatershedRunoffCodecV3.Encode(continuedRunoff);
            Assert.Equal(explicitBytes, WatershedRunoffCodecV3.Encode(WatershedRunoffCodecV3.Decode(explicitBytes,
                new GeographyProductionDependencies(capacity), TestContext.Current.CancellationToken)));
            Assert.Equal(before, WatershedRunoffCodecV1.Encode(original));
        }

        [Fact]
        public void Construction_cannot_use_a_partial_history_in_place_of_its_installed_batch_witnesses()
        {
            var world = ConstructionWorldFixture.Create();
            byte[] before = WorldSnapshotCodec.Encode(world);
            var continued = world.Production!.Continue(world.Inventory);
            var error = Assert.Throws<ArgumentException>(() => new ConstructionPlanner().Plan(world,
                world.Inventory, continued, world.Construction!, world.Space,
                ConstructionWorldFixture.Command(world), true, false, _ => false));
            Assert.StartsWith("Construction requires qualified archived producer and ingredient witnesses.", error.Message);
            Assert.Equal(before, WorldSnapshotCodec.Encode(world));
        }

        [Fact]
        public void Source_delivery_requires_its_own_continuation_transfer_contract()
        {
            var state = ProductionContinuationBasisFixture.Fresh()
                .Consume(IngredientLotKind.Source, InstantCraftFixture.Id(70), 1, 8000);
            var continued = state.Production.Continue(state.Inventory);
            byte[] before = AuthorityEncodingV1.Encode(state.Inventory);
            var lot = state.Inventory.Custody.GetLot(InstantCraftFixture.Id(70));
            var request = new SourceDeliveryRequest(lot.CustodianId,
                state.Inventory.Custody.GetStorage(lot.CustodianId).Revision, InstantCraftFixture.Id(61),
                lot.Definition, 1, InstantCraftFixture.Id(90), [new(lot.LotId, lot.Revision, 1)]);
            var error = Assert.Throws<ArgumentException>(() => new SourceDeliveryPlanner().Plan(state.Inventory,
                continued, new(InstantCraftFixture.Id(20), InstantCraftFixture.Id(30)), InstantCraftFixture.Id(10),
                InstantCraftFixture.Id(8001), request, _ => false));
            Assert.StartsWith("Source delivery requires an admitted continuation transfer contract.", error.Message);
            Assert.Equal(before, AuthorityEncodingV1.Encode(state.Inventory));
        }

        [Fact]
        public void A_material_cut_does_not_enable_unrecorded_healing()
        {
            var state = ProductionContinuationBasisFixture.Fresh()
                .Consume(IngredientLotKind.Source, InstantCraftFixture.Id(70), 1, 8000);
            var continued = state.Production.Continue(state.Inventory);
            var progression = ProgressionFixture.Create();
            var before = progression.ContentHash;
            var lot = state.Inventory.Custody.GetLot(InstantCraftFixture.Id(70));
            var dose = new ConsumeItemRequest(lot.CustodianId,
                state.Inventory.Custody.GetStorage(lot.CustodianId).Revision, continued.Revision,
                InstantCraftFixture.Id(10), lot.Definition, new(IngredientLotKind.Source, lot.LotId, lot.Revision, 1));
            var error = Assert.Throws<ArgumentException>(() => new RestorativeItemPlanner().Plan(state.Inventory,
                continued, progression, new(InstantCraftFixture.Id(20), InstantCraftFixture.Id(30)),
                InstantCraftFixture.Id(10), InstantCraftFixture.Id(8001), dose, 0, 40));
            Assert.StartsWith("Restorative use requires coordinated material and care continuation.", error.Message);
            Assert.Equal(before, progression.ContentHash);
            Assert.Single(state.Production.ConsumedItems);
        }
    }
}

namespace OpenFantasy.Core.Tests.Geography
{
    public sealed partial class TerrainMaterialRegistryTests
    {
        [Fact]
        public void Terrain_admission_refuses_a_standalone_material_cut_until_archived_coverage_is_integrated()
        {
            var original = Create().Registry;
            var admitted = WithConsumption(original);
            byte[] before = TerrainMaterialRegistryCodecV3.Encode(admitted);
            var continued = admitted.Production!.Continue(admitted.Inventory);
            var error = Assert.Throws<ArgumentException>(() => original.AttachProduction(continued));
            Assert.StartsWith("Terrain material admission requires complete inline production history.", error.Message);
            Assert.Equal(before, TerrainMaterialRegistryCodecV3.Encode(admitted));
        }
    }
}
