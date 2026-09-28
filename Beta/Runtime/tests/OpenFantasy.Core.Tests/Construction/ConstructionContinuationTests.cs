using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Tests.Worlds;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Construction.ConstructionContinuationFixture;

namespace OpenFantasy.Core.Tests.Construction;

public sealed class ConstructionContinuationTests
{
    [Fact]
    public void Actual_reserved_completion_and_byproduct_recraft_preserve_both_structures_through_two_cuts()
    {
        var journey = new ConstructionContinuationFixture();
        var original = journey.Construction.FindProject(ProjectA)!;
        var originalBatch = Assert.Single(journey.LegacyWorld.Production!.Batches);
        var originalMembers = journey.Physical.Members.ToArray();
        var originalReferences = originalMembers.Select(member => journey.Physical.GetMemberReference(member.StructureId, member.MemberIndex)).ToArray();
        var originalGuard = journey.Production.Lots.Single(lot => lot.LotId == original.Installed!.GuardLotId);
        Assert.Equivalent(originalBatch, Assert.Single(journey.Production.Continuation!.ConstructionBatches), true);
        Assert.Empty(journey.Production.Batches);
        Assert.False(journey.Production.SupportsConsumption); // The actual legacy physical profile is preserved.
        var partial = journey.Work();
        Assert.Null(partial.Evidence);
        Assert.Equal(2UL, journey.Construction.FindProject(ProjectB)!.Revision);
        Assert.Equal(1UL, journey.Production.TotalBatchCount);
        Assert.Equal(originalMembers, journey.Physical.Members);
        Assert.Equal(journey.LegacyWorld.ConstructionPhysical!.Revision, journey.Physical.Revision);
        journey.RecraftByproduct(ProjectA);
        journey.Cut();
        journey.WorkUntil(199);
        var completed = journey.Work();
        Assert.NotNull(completed.Evidence);
        var second = journey.Construction.FindProject(ProjectB)!;
        Assert.True(second.IsCompleted);
        Assert.Equal(200UL, second.Progress.CompletedUnits);
        Assert.Equal(360_000UL, second.Progress.Elapsed.TotalMilliseconds);
        Assert.Equal(400UL, journey.Construction.Revision);
        Assert.Equal(2, journey.Space.Structures.Count);
        Assert.Equal(20, journey.Physical.Members.Count);
        Assert.Equal(journey.LegacyWorld.ConstructionPhysical!.Revision + 1, journey.Physical.Revision);
        Assert.Equal(14, completed.CreatedIds.Count); // Batch, two outputs, structure, ten physical members.
        Assert.Single(journey.Production.MaterialHistory!.ConstructionEvidence);
        Assert.DoesNotContain(journey.Inventory.Custody.Lots, lot => lot.ReservedBy == second.ReservationId);
        Assert.Equal(6UL, completed.Evidence!.Batch.Ingredients.Aggregate(0UL, (sum, value) => sum + value.Quantity));
        journey.RecraftByproduct(ProjectB);
        journey.Cut();
        Assert.Equal(2, journey.Production.Continuation!.ConstructionBatches.Count);
        Assert.Equal(4UL, journey.Production.TotalBatchCount); // Two installed assemblies and two ordinary crafts.
        Assert.Empty(journey.Production.Batches);
        foreach (var member in originalMembers) Assert.Same(member, journey.Physical.GetMember(member.StructureId, member.MemberIndex));
        Assert.Equal(originalReferences, originalMembers.Select(member => journey.Physical.GetMemberReference(member.StructureId, member.MemberIndex)));
        Assert.Equivalent(originalGuard, journey.Production.Lots.Single(lot => lot.LotId == originalGuard.LotId), true);
        Assert.Equal(540_000UL, journey.Physical.Members.Aggregate(0UL, (sum, member) =>
            sum + journey.Physical.GetMemberReference(member.StructureId, member.MemberIndex).Mass.Value));
        Assert.All(journey.Construction.Projects, project => Assert.Single(journey.Production.Continuation!.ConstructionBatches,
            batch => batch.BatchId == project.Installed!.ProductionBatchId));
        AssertClosed(journey);
        journey.Reopen();
    }

    [Fact]
    public void Real_512_operation_window_refuses_completion_then_cut_preserves_permission_for_exact_completion()
    {
        var journey = new ConstructionContinuationFixture(readyToComplete: true);
        ulong productionBefore = journey.Production.Revision;
        ulong custodyBefore = journey.Inventory.Custody.Revision;
        for (int index = 0; index < MaterialOperationHistory.MaximumOperations; index++) journey.TransferMovable();
        Assert.Equal(512, journey.Production.MaterialHistory!.Operations.Count);
        Assert.Equal(productionBefore + 512, journey.Production.Revision);
        Assert.Equal(custodyBefore + 512, journey.Inventory.Custody.Revision);
        Assert.Equal(512UL, journey.Inventory.Custody.GetLot(Movable).Revision);
        journey.Advance();
        var command = journey.Command();
        var refused = journey.Plan(command);
        Assert.Equal(WorldActionRejection.Unavailable, refused.Rejection);
        AssertUnchanged(journey, refused);
        Assert.Equal(199UL, journey.Construction.FindProject(ProjectB)!.Revision);
        journey.Cut();
        var completed = journey.Plan(command); // Same request and time boundary: refusal spent neither.
        journey.Accept(completed, command.CommandId);
        Assert.Equal(productionBefore + 513, journey.Production.Revision);
        Assert.Equal(custodyBefore + 513, journey.Inventory.Custody.Revision);
        Assert.Single(journey.Production.MaterialHistory!.Operations);
        Assert.Equal(2UL, journey.Production.TotalBatchCount);
        journey.Cut();
        Assert.Equal(2, journey.Production.Continuation!.ConstructionBatches.Count);
        Assert.Equal(512UL, journey.Inventory.Custody.GetLot(Movable).Revision);
        AssertClosed(journey);
    }

    [Fact]
    public void Same_world_cutoff_and_nonadvancing_or_held_requests_never_buy_more_work()
    {
        var journey = new ConstructionContinuationFixture();
        Assert.Equal(WorldActionRejection.RevisionConflict, journey.Plan(journey.Command()).Rejection);
        journey.Work();
        var repeatedFrame = journey.Plan(journey.Command());
        Assert.Equal(WorldActionRejection.RevisionConflict, repeatedFrame.Rejection);
        AssertUnchanged(journey, repeatedFrame);
        journey.Advance();
        AssertUnchanged(journey, journey.Plan(journey.Command(), advancing: false));
        AssertUnchanged(journey, journey.Plan(journey.Command(), held: true));
        var stale = journey.Command();
        var old = (WorkConstructionIntent)stale.Intent;
        var altered = new WorldActionCommand(stale.CommandId, stale.PrincipalId, stale.OriginSessionId,
            stale.ActorId, stale.WorldId, stale.BranchId, stale.ExpectedActorRevision,
            old with { ExpectedProjectRevision = old.ExpectedProjectRevision - 1 });
        var stalePlan = journey.Plan(altered);
        Assert.Equal(WorldActionRejection.RevisionConflict, stalePlan.Rejection);
        AssertUnchanged(journey, stalePlan);
        Assert.Equal(WorldActionRejection.None, journey.Plan(stale).Rejection);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Historical_command_output_structure_and_member_collisions_preserve_every_owner(int collision)
    {
        var journey = new ConstructionContinuationFixture(readyToComplete: true);
        journey.Advance();
        var command = journey.Command();
        var eligible = journey.Plan(command);
        Assert.Equal(WorldActionRejection.None, eligible.Rejection);
        EntityId identity = collision == -1 ? command.CommandId : collision < 4
            ? EntityId.DeriveSpawned(command.CommandId, (ulong)collision)
            : ConstructionBearingModel.MemberIdentity(EntityId.DeriveSpawned(command.CommandId, 3), 0);
        var refused = journey.Plan(command, id => id == identity || journey.Occupied(id));
        Assert.Equal(WorldActionRejection.Unavailable, refused.Rejection);
        AssertUnchanged(journey, refused);
        Assert.Equal(199UL, journey.Construction.FindProject(ProjectB)!.Revision);
        Assert.Equal(10, journey.Physical.Members.Count);
    }

    [Fact]
    public void Genuine_builder_movement_cannot_be_overwritten_by_stale_supplied_space_and_actor_tokens()
    {
        var journey = new ConstructionContinuationFixture();
        var before = journey.Space.GetActor(Builder);
        journey.Advance([StepFixture.Move(journey.SourceWorld, 11, -32)]);
        var moved = journey.SourceWorld.Space.GetActor(Builder);
        Assert.NotEqual(before.Feet, moved.Feet);
        Assert.Equal(before.Revision + 1, moved.Revision);
        var oldCommand = journey.Command();
        Assert.Equal(before.Revision, oldCommand.ExpectedActorRevision);
        var refused = journey.Plan(oldCommand);
        AssertUnchanged(journey, refused);
    }

    [Fact]
    public void Otherwise_eligible_current_frame_cannot_change_the_captured_source_admission()
    {
        var journey = new ConstructionContinuationFixture();
        journey.Advance();
        var command = journey.Command();
        Assert.Equal(WorldActionRejection.None, journey.Plan(command).Rejection);
        var source = journey.SourceWorld;
        var alternate = new WorldSnapshot(source.Inventory, source.Space, source.Residents,
            ContentHash.Compute("unrelated-construction-admission"u8), source.Calendar, source.Revision,
            source.AdvancingSteps, source.Dialogues, source.Gathering, source.Production, source.Progression,
            source.Quests, source.Construction, excavation: source.Excavation, constructionPhysical: source.ConstructionPhysical);
        var refused = new MaterialConstructionPlanner().Plan(alternate, journey.Inventory, journey.Production,
            journey.State, journey.Space, command, true, false, journey.Occupied);
        AssertUnchanged(journey, refused);
    }

    [Fact]
    public void Ordinary_material_actions_cannot_spend_reserved_inputs_or_installed_output_or_enable_consumption()
    {
        var journey = new ConstructionContinuationFixture();
        var unfinished = journey.Construction.FindProject(ProjectB)!;
        var stock = journey.Inventory.Custody.Lots.Single(lot => lot.ReservedBy == unfinished.ReservationId);
        var control = journey.Inventory.Controls.Single(value => value.ActorId == Builder);
        var recipe = unfinished.Definition.MaterialTransform;
        var request = new CraftRequest(recipe.DefinitionId, recipe.Version, journey.Production.Revision,
            stock.CustodianId, journey.Inventory.Custody.GetStorage(stock.CustodianId).Revision,
            [new(IngredientLotKind.Source, stock.LotId, stock.Revision, stock.Quantity)]);
        var ordinary = new InstantCraftPlanner().Plan(journey.Inventory, journey.Production,
            new(control.PrincipalId, control.SessionId), Builder, journey.NextId(), request, journey.Occupied);
        Assert.Equal(WorldActionRejection.Unavailable, ordinary.Rejection);
        Assert.Same(journey.Inventory, ordinary.CandidateInventory);
        Assert.Same(journey.Production, ordinary.CandidateProduction);
        var freeWood = journey.Inventory.Custody.GetLot(UnreservedWood);
        Assert.Null(freeWood.ReservedBy);
        var freeRequest = new CraftRequest(recipe.DefinitionId, recipe.Version, journey.Production.Revision,
            freeWood.CustodianId, journey.Inventory.Custody.GetStorage(freeWood.CustodianId).Revision,
            [new(IngredientLotKind.Source, freeWood.LotId, freeWood.Revision, 6)]);
        var unqualified = new InstantCraftPlanner().Plan(journey.Inventory, journey.Production,
            new(control.PrincipalId, control.SessionId), Builder, journey.NextId(), freeRequest, journey.Occupied);
        Assert.Equal(WorldActionRejection.Unavailable, unqualified.Rejection);
        Assert.Same(journey.Inventory, unqualified.CandidateInventory);
        Assert.Same(journey.Production, unqualified.CandidateProduction);
        var first = journey.Construction.FindProject(ProjectA)!;
        var guard = journey.Production.Lots.Single(lot => lot.LotId == first.Installed!.GuardLotId);
        var byproduct = journey.Production.Lots.Single(lot => lot.CustodianId == first.ProjectCustodianId && lot.ReservedBy is null);
        foreach (var lot in new[] { guard, byproduct })
        {
            var consume = new ConsumeItemRequest(lot.CustodianId,
                journey.Inventory.Custody.GetStorage(lot.CustodianId).Revision, journey.Production.Revision, Player,
                lot.Definition, new(IngredientLotKind.Transformed, lot.LotId, lot.Revision, 1));
            var refused = new ItemConsumptionPlanner().Plan(journey.Inventory, journey.Production,
                new(control.PrincipalId, control.SessionId), Builder, journey.NextId(), consume, journey.Occupied);
            Assert.Equal(WorldActionRejection.Unavailable, refused.Rejection);
            Assert.Same(journey.Production, refused.CandidateProduction);
            Assert.Same(journey.Inventory, refused.CandidateInventory);
        }
        journey.RecraftByproduct(ProjectA);
    }

    internal static void AssertUnchanged(ConstructionContinuationFixture journey, MaterialConstructionPlan plan)
    {
        Assert.NotEqual(WorldActionRejection.None, plan.Rejection);
        Assert.Same(journey.Inventory, plan.CandidateInventory);
        Assert.Same(journey.Production, plan.CandidateProduction);
        Assert.Same(journey.Construction, plan.CandidateConstruction);
        Assert.Same(journey.Physical, plan.CandidatePhysical);
        Assert.Same(journey.Space, plan.CandidateSpace);
        Assert.Same(journey.State, plan.CandidateState);
        Assert.Null(plan.Evidence);
        Assert.Empty(plan.CreatedIds);
    }

    private static void AssertClosed(ConstructionContinuationFixture journey)
    {
        Assert.Equal(2, journey.Closed.Count);
        foreach (var closed in journey.Closed)
        {
            var restored = ProductionCodecV5.Decode(closed.Bytes, closed.Production.Continuation!,
                closed.Production.MaterialHistory!.GatherEvidence, closed.Production.MaterialHistory.ConstructionEvidence);
            restored.ValidateForAuthority(closed.Inventory);
            Assert.Equal(closed.Bytes, ProductionCodecV5.Encode(restored));
        }
    }
}
