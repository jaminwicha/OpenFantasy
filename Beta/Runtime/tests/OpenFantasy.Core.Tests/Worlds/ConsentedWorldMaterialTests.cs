using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Tests.Gathering;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;
using Kind = OpenFantasy.Core.Authority.ConsentedMaterial.ConsentedMaterialLotKind;

namespace OpenFantasy.Core.Tests.Worlds;

/// <summary>
/// The paired material decision against a real admitted production world: actual crafting through
/// the world step, two controlled residents with bodies and positions, and their own submissions.
/// These older-profile diagnostic cases prove the decision and its evidence. Complete profile 28
/// publication is exercised by the Hosting assembly's complete-world fixtures.
/// </summary>
public sealed class ConsentedWorldMaterialTests
{
    private static readonly EntityId Player = Id(10);
    private static readonly EntityId Helper = Id(11);
    private static readonly EntityId PlayerBag = Id(60);
    private static readonly EntityId HelperBag = Id(61);
    private const string ProfileRefusal = "Paired material transfer requires its explicit complete-world profile.";

    [Fact]
    public void A_split_gift_of_crafted_stock_needs_both_controls_bodies_reach_and_records_exact_evidence()
    {
        WorldSnapshot world = Crafted(out TransformedLot lot);
        ProductionBatch producer = world.Production!.Batches.Single();
        var (offer, acceptance) = Pair(world, Terms(world, Kind.Produced, lot.LotId, lot.Definition, 1, lot.Revision), 1400);

        WorldConsentedMaterialPlan plan = Plan(world, offer, acceptance,
            WorldBatchKind.AdvancingStep);
        Assert.Equal((WorldActionRejection.None, WorldActionRejection.None), (plan.OfferRejection, plan.AcceptanceRejection));
        WorldConsentedTransferEvidence evidence = plan.Evidence!;
        EntityId child = EntityId.DeriveSpawned(offer.CommandId, 0);
        Assert.Equal((world.Revision + 1, child, producer.BatchId, lot.Output.OutputIndex, 0UL, 1UL),
            (evidence.WorldSequence, evidence.MovedLotId, evidence.RightsOwnerId, evidence.OutputIndex,
                evidence.MovedStart, evidence.MovedEnd));
        Assert.Equal((offer.CommandId, acceptance.CommandId), (evidence.CommandId, evidence.AcceptanceId));
        Assert.Equal((world.Inventory.ContentHash, world.Production.ContentHash, plan.CandidateInventory.ContentHash,
            plan.CandidateProduction.ContentHash), (evidence.OriginalInventoryHash, evidence.OriginalProductionHash,
            evidence.CandidateInventoryHash, evidence.CandidateProductionHash));

        TransformedLot moved = plan.CandidateProduction.Lots.Single(value => value.LotId == child);
        TransformedLot remainder = plan.CandidateProduction.Lots.Single(value => value.LotId == lot.LotId);
        Assert.Equal((HelperBag, producer.BatchId, 0UL, 1UL, 0UL),
            (moved.CustodianId, moved.Output.BatchId, moved.Output.Start, moved.Output.End, moved.Revision));
        Assert.Equal((PlayerBag, producer.BatchId, 1UL, 2UL, lot.Revision + 1),
            (remainder.CustodianId, remainder.Output.BatchId, remainder.Output.Start, remainder.Output.End, remainder.Revision));
        Assert.Equal((Player, PlayerBag), (producer.ActorId, producer.CustodianId));
        Assert.Equal(world.Production.Revision, plan.CandidateProduction.Revision);
        Assert.Equal(world.Inventory.Custody.Revision + 1, plan.CandidateInventory.Custody.Revision);
        Assert.Equal(world.Inventory.Policies, plan.CandidateInventory.Policies);

        var history = new WorldConsentedMaterialHistory([evidence]);
        plan.CandidateProduction.ValidateCustodyEvidence(history.ProducedMovements);
        Assert.Throws<ArgumentException>(() => plan.CandidateProduction.ValidateCustodyEvidence([]));
        Assert.Throws<ArgumentException>(() => world.Production.ValidateCustodyEvidence(history.ProducedMovements));
        Assert.Throws<ArgumentException>(() => history.Append(evidence));
        Assert.True(history.ContainsIdentity(child) && history.ContainsIdentity(acceptance.CommandId));

        // The candidate stays pinned to the exact owners it was decided against.
        var applied = plan.ApplyTo(world.Inventory, world.Production);
        Assert.Same(plan.CandidateProduction, applied.Production);
        Assert.Equal(CustodyConflict.StalePlan, Assert.Throws<CustodyConflictException>(
            () => plan.ApplyTo(plan.CandidateInventory, world.Production)).Conflict);
    }

    [Fact]
    public void A_host_controlled_resident_gives_onward_under_the_same_rules()
    {
        WorldSnapshot world = Crafted(out TransformedLot lot);
        var (offer, acceptance) = Pair(world, Terms(world, Kind.Produced, lot.LotId, lot.Definition, 2, lot.Revision), 1400);
        WorldConsentedMaterialPlan first = Plan(world, offer, acceptance,
            WorldBatchKind.AdvancingStep);
        Assert.True(first.IsApplied);
        Assert.Equal(lot.LotId, first.MovedLotId);

        // Test-assembled diagnostic successor; this older profile cannot publish paired commands.
        WorldSnapshot successor = With(world, first.CandidateInventory, first.CandidateProduction, world.Space,
            world.Revision + 1);
        TransformedLot held = successor.Production!.Lots.Single(value => value.LotId == lot.LotId);
        Assert.Equal((HelperBag, lot.Revision + 1), (held.CustodianId, held.Revision));
        Assert.Equal(ResidentControllerKind.Host, successor.GetResident(Helper).ControllerKind);
        var terms = new ConsentedMaterialTerms(successor.WorldId, successor.BranchId, Helper, Player, Kind.Produced,
            held.LotId, held.Definition, HelperBag, PlayerBag, 1, held.Revision);
        WorldActionCommand onward = StepFixture.Action(successor, 1500,
            new OfferMaterialIntent(terms, successor.Space.GetActor(Player).Revision), actor: 11);
        WorldActionCommand accepted = StepFixture.Action(successor, 1501,
            AcceptMaterialIntent.Approving(onward, successor.Space.GetActor(Helper).Revision), actor: 10);
        bool Closed(EntityId identity) => identity == offer.CommandId || identity == acceptance.CommandId;

        WorldConsentedMaterialPlan second = Plan(successor, onward, accepted,
            WorldBatchKind.AdvancingStep, Closed);
        Assert.True(second.IsApplied);
        var history = new WorldConsentedMaterialHistory([first.Evidence!, second.Evidence!]);
        second.CandidateProduction.ValidateCustodyEvidence(history.ProducedMovements);
        Assert.Throws<ArgumentException>(() => new WorldConsentedMaterialHistory([second.Evidence!, first.Evidence!]));

        // Identities closed by the earlier transaction stay occupied in either role.
        WorldActionCommand reused = new(offer.CommandId, onward.PrincipalId, onward.OriginSessionId, Helper,
            successor.WorldId, successor.BranchId, onward.ExpectedActorRevision, onward.Intent);
        WorldActionCommand rebound = StepFixture.Action(successor, 1502,
            AcceptMaterialIntent.Approving(reused, successor.Space.GetActor(Helper).Revision), actor: 10);
        AssertRefused(successor, reused, rebound, WorldActionRejection.Unavailable, WorldActionRejection.Unavailable, Closed);
    }

    [Fact]
    public void Control_is_decided_per_participant_before_any_availability_is_disclosed()
    {
        WorldSnapshot world = Crafted(out TransformedLot lot);
        ConsentedMaterialTerms terms = Terms(world, Kind.Produced, lot.LotId, lot.Definition, 1, lot.Revision);
        var (offer, acceptance) = Pair(world, terms, 1400);
        ActorControlBinding player = world.Inventory.Controls.Single(value => value.ActorId == Player);
        ActorControlBinding helper = world.Inventory.Controls.Single(value => value.ActorId == Helper);

        // Correct captured claims do not authenticate a delivery made through the other actor's session.
        var copiedClaims = new WorldConsentedMaterialPlanner().Plan(world, offer, acceptance,
            WorldBatchKind.AdvancingStep, new(player.PrincipalId, player.SessionId),
            new(player.PrincipalId, player.SessionId));
        Assert.Equal((WorldActionRejection.Unavailable, WorldActionRejection.NotAuthorized),
            (copiedClaims.OfferRejection, copiedClaims.AcceptanceRejection));

        // The sender's payload and control never stand in for the recipient's own submission.
        WorldActionCommand forgedAcceptance = new(acceptance.CommandId, player.PrincipalId, player.SessionId, Helper,
            world.WorldId, world.BranchId, acceptance.ExpectedActorRevision, acceptance.Intent);
        AssertRefused(world, offer, forgedAcceptance, WorldActionRejection.Unavailable, WorldActionRejection.NotAuthorized);
        WorldActionCommand forgedOffer = new(offer.CommandId, helper.PrincipalId, helper.SessionId, Player,
            world.WorldId, world.BranchId, offer.ExpectedActorRevision, offer.Intent);
        WorldActionCommand boundToForged = StepFixture.Action(world, 1401,
            AcceptMaterialIntent.Approving(forgedOffer, world.Space.GetActor(Player).Revision), actor: 11);
        AssertRefused(world, forgedOffer, boundToForged, WorldActionRejection.NotAuthorized, WorldActionRejection.Unavailable);

        // Unauthenticated submissions naming absent stock learn nothing about it.
        ConsentedMaterialTerms absent = Terms(world, Kind.Produced, Id(9999), lot.Definition, 1, 0);
        WorldActionCommand absentOffer = new(offer.CommandId, helper.PrincipalId, helper.SessionId, Player,
            world.WorldId, world.BranchId, offer.ExpectedActorRevision,
            new OfferMaterialIntent(absent, world.Space.GetActor(Helper).Revision));
        WorldActionCommand absentAcceptance = StepFixture.Action(world, 1401,
            AcceptMaterialIntent.Approving(absentOffer, world.Space.GetActor(Player).Revision), actor: 11);
        AssertRefused(world, absentOffer, absentAcceptance, WorldActionRejection.NotAuthorized, WorldActionRejection.Unavailable);

        WorldActionCommand otherTerms = StepFixture.Action(world, 1401, new AcceptMaterialIntent(offer.CommandId,
            ((OfferMaterialIntent)offer.Intent).ToMaterialCommand(offer).ComputeContentHash(),
            Terms(world, Kind.Produced, lot.LotId, lot.Definition, 2, lot.Revision),
            world.Space.GetActor(Player).Revision), actor: 11);
        AssertRefused(world, offer, otherTerms, WorldActionRejection.NotAuthorized, WorldActionRejection.NotAuthorized);
        WorldActionCommand otherOffer = StepFixture.Action(world, 1401, new AcceptMaterialIntent(Id(1499),
            ((AcceptMaterialIntent)acceptance.Intent).OfferHash, terms, world.Space.GetActor(Player).Revision), actor: 11);
        AssertRefused(world, offer, otherOffer, WorldActionRejection.NotAuthorized, WorldActionRejection.NotAuthorized);

        var notOwned = new ConsentedMaterialTerms(world.WorldId, world.BranchId, Player, Helper, Kind.Produced,
            lot.LotId, lot.Definition, HelperBag, PlayerBag, 1, lot.Revision);
        var (foreignOffer, foreignAcceptance) = Pair(world, notOwned, 1400);
        AssertRefused(world, foreignOffer, foreignAcceptance, WorldActionRejection.NotAuthorized,
            WorldActionRejection.NotAuthorized);
    }

    [Fact]
    public void Time_presence_body_and_stock_refusals_leave_the_valid_pair_applicable()
    {
        WorldSnapshot world = Crafted(out TransformedLot lot);
        ConsentedMaterialTerms terms = Terms(world, Kind.Produced, lot.LotId, lot.Definition, 1, lot.Revision);
        var (offer, acceptance) = Pair(world, terms, 1400);
        Assert.True(Plan(world, offer, acceptance, WorldBatchKind.AdvancingStep).IsApplied);

        WorldConsentedMaterialPlan held = Plan(world, offer, acceptance, WorldBatchKind.HeldInteraction);
        Assert.Equal((WorldActionRejection.Unavailable, WorldActionRejection.Unavailable),
            (held.OfferRejection, held.AcceptanceRejection));

        WorldActionCommand stalePresence = StepFixture.Action(world, 1400,
            new OfferMaterialIntent(terms, world.Space.GetActor(Helper).Revision + 1), actor: 10);
        WorldActionCommand acceptedStale = StepFixture.Action(world, 1401,
            AcceptMaterialIntent.Approving(stalePresence, world.Space.GetActor(Player).Revision), actor: 11);
        AssertRefused(world, stalePresence, acceptedStale, WorldActionRejection.RevisionConflict,
            WorldActionRejection.RevisionConflict);

        WorldSnapshot unable = With(world, world.Inventory, world.Production!, WithoutManipulation(world.Space, Helper),
            world.Revision);
        AssertRefused(unable, offer, acceptance, WorldActionRejection.Unavailable, WorldActionRejection.Unavailable);

        // The same residents, controls and approvals five cells apart on the same floor.
        WorldSnapshot apart = With(world, world.Inventory, world.Production!, new LocalSpatialSnapshot(world.Space.Scope,
            world.Space.Regions, world.Space.Actors.Select(actor => actor.ActorId != Helper ? actor
                : new ActorPresence(actor.Binding, actor.Condition, actor.Scope, new SubunitPosition(1280, 0, 256),
                    actor.Revision, actor.HomePlaceId))), world.Revision);
        Assert.NotEqual(SpatialInteraction.Reachable, apart.Space.QueryInteraction(Player, Helper));
        AssertRefused(apart, offer, acceptance, WorldActionRejection.OutOfReach, WorldActionRejection.OutOfReach);

        var (staleOffer, staleAcceptance) = Pair(world,
            Terms(world, Kind.Produced, lot.LotId, lot.Definition, 1, lot.Revision + 1), 1400);
        AssertRefused(world, staleOffer, staleAcceptance, WorldActionRejection.RevisionConflict,
            WorldActionRejection.RevisionConflict);
        var (excess, excessAcceptance) = Pair(world,
            Terms(world, Kind.Produced, lot.LotId, lot.Definition, 3, lot.Revision), 1400);
        AssertRefused(world, excess, excessAcceptance, WorldActionRejection.Unavailable, WorldActionRejection.Unavailable);

        // Raw stock uses the same paired boundary and the real custody planner.
        CountedSourceLot raw = world.Inventory.Custody.GetLot(Id(70));
        var (rawOffer, rawAcceptance) = Pair(world,
            Terms(world, Kind.CountedSource, raw.LotId, raw.Definition, 1, raw.Revision), 1400);
        WorldConsentedMaterialPlan moved = Plan(world, rawOffer, rawAcceptance, WorldBatchKind.AdvancingStep);
        Assert.True(moved.IsApplied);
        Assert.Null(moved.Evidence!.ToProducedMovement());
        Assert.Equal((raw.Source.SourceId, raw.Source.Start, raw.Source.Start + 1),
            (moved.Evidence.RightsOwnerId, moved.Evidence.MovedStart, moved.Evidence.MovedEnd));
        Assert.Equal(HelperBag, moved.CandidateInventory.Custody.GetLot(moved.MovedLotId!.Value).CustodianId);
    }

    [Fact]
    public void Every_admitted_step_profile_and_older_action_codec_refuses_the_paired_intents()
    {
        WorldSnapshot world = Crafted(out TransformedLot lot);
        var (offer, acceptance) = Pair(world, Terms(world, Kind.Produced, lot.LotId, lot.Definition, 1, lot.Revision), 1400);
        for (ushort profile = 1; profile <= 27; profile++)
        {
            foreach (WorldActionCommand command in new[] { offer, acceptance })
            {
                Assert.Equal(ProfileRefusal, Assert.Throws<ArgumentException>(() => new WorldStepBatch(Id(1300),
                    world.WorldId, world.BranchId, world.Revision, world.AdvancingSteps, WorldBatchKind.AdvancingStep,
                    [], [command], profile)).Message);
            }
        }
        var wrongWorld = new WorldStepBatch(Id(1300), world.WorldId, world.BranchId,
            world.Revision, world.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [offer, acceptance], 28);
        Assert.Throws<InvalidOperationException>(() => new WorldStepProcessor().Decide(world, wrongWorld));

        foreach (WorldActionCommand command in new[] { offer, acceptance })
        {
            byte[] bytes = WorldActionCodec.Encode(command);
            Assert.Equal(bytes, WorldActionCodecV15.Encode(command));
            WorldActionCommand decoded = WorldActionCodec.Decode(bytes);
            Assert.Equal(bytes, WorldActionCodec.Encode(decoded));
            Assert.Equal(command.ContentHash, decoded.ContentHash);
            Assert.Throws<FormatException>(() => WorldActionCodecV14.Decode(bytes));
            Assert.Throws<FormatException>(() => WorldActionCodecV1.Decode(bytes));
            Assert.Throws<ArgumentException>(() => WorldActionCodecV14.Encode(command));
            Assert.Throws<FormatException>(() => WorldActionCodecV15.Decode(bytes[..^1]));
        }
        Assert.NotEqual(offer.ContentHash, acceptance.ContentHash);
        Assert.Throws<FormatException>(() => WorldActionCodecV15.Decode(
            WorldActionCodec.Encode(ProductionWorldFixture.Craft(world, 1402))));
    }

    [Fact]
    public void Existing_world_admission_refuses_a_consented_production_context()
    {
        WorldSnapshot world = Crafted(out _);
        ProductionSnapshot consented = world.Production!.ContinueConsentedMaterial(world.Inventory);
        Assert.True(consented.Continuation!.SupportsConsentedTransfer);
        Assert.False(consented.Continuation.SupportsCompleteWorld);
        Assert.Equal("World admission requires a coordinated material, care and resource continuation profile. (Parameter 'production')",
            Assert.Throws<ArgumentException>(() => With(world, world.Inventory, consented, world.Space, world.Revision)).Message);

        // The admitted world is unchanged and still decodes under its own exact profile.
        byte[] bytes = WorldSnapshotCodec.Encode(world);
        Assert.Equal((ushort)3, world.ProfileVersion);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
    }

    [Fact]
    public void The_installed_output_of_real_construction_is_refused_while_its_byproduct_moves()
    {
        WorldSnapshot world = Construction.ConstructionWorldFixture.Create();
        for (ulong index = 0; index < 200; index++)
        {
            WorldStepDecision step = Construction.ConstructionWorldFixture.Step(world, 100 + index);
            Assert.Equal(WorldActionRejection.None, Assert.Single(step.Receipts).Rejection);
            world = step.Candidate;
        }
        var project = Assert.Single(world.Construction!.Projects);
        Assert.True(project.IsCompleted);
        Assert.Single(world.Space.Structures);
        TransformedLot installed = Assert.Single(world.Production!.Lots, lot => lot.ReservedBy is not null);
        Assert.Equal(project.ReservationId, installed.ReservedBy);
        TransformedLot byproduct = world.Production.Lots.First(lot => lot.ReservedBy is null
            && lot.CustodianId == installed.CustodianId);
        EntityId owner = world.Inventory.Policies.Single(policy => policy.CustodianId == installed.CustodianId).OwnerActorId;
        EntityId recipient = world.Residents.Single(resident => resident.ActorId != owner).ActorId;
        EntityId bag = world.GetResident(recipient).InventoryCustodianId;
        world = Approach(world, recipient, owner);

        (WorldActionCommand Offer, WorldActionCommand Acceptance) Give(TransformedLot lot, ulong quantity, ulong command)
        {
            var terms = new ConsentedMaterialTerms(world.WorldId, world.BranchId, owner, recipient, Kind.Produced,
                lot.LotId, lot.Definition, lot.CustodianId, bag, quantity, lot.Revision);
            WorldActionCommand offer = Submit(world, owner, command,
                new OfferMaterialIntent(terms, world.Space.GetActor(recipient).Revision));
            return (offer, Submit(world, recipient, command + 1,
                AcceptMaterialIntent.Approving(offer, world.Space.GetActor(owner).Revision)));
        }

        // Valid control: the same participants, storage and step move the unreserved byproduct.
        var (freeOffer, freeAcceptance) = Give(byproduct, 1, 60_000);
        WorldConsentedMaterialPlan moved = Plan(world, freeOffer, freeAcceptance,
            WorldBatchKind.AdvancingStep);
        Assert.Equal((WorldActionRejection.None, WorldActionRejection.None), (moved.OfferRejection, moved.AcceptanceRejection));
        moved.CandidateProduction.ValidateCustodyEvidence(
            new WorldConsentedMaterialHistory([moved.Evidence!]).ProducedMovements);
        Assert.Equal(project.ReservationId,
            moved.CandidateProduction.Lots.Single(lot => lot.LotId == installed.LotId).ReservedBy);

        var (offer, acceptance) = Give(installed, installed.Quantity, 60_002);
        AssertRefused(world, offer, acceptance, WorldActionRejection.Unavailable, WorldActionRejection.Unavailable);

        // The custody replay admits the real completion and refuses any movement of the installed lot.
        ProductionSnapshot completed = world.Production!;
        completed.ValidateCustodyEvidence([]);
        Assert.Equal("Custody evidence must move available unreserved stock from its actual custodian at the approved lot revision.",
            Assert.Throws<ArgumentException>(() => completed.ValidateCustodyEvidence([new ProducedCustodyMovement(
                installed.LotId, installed.Revision, installed.LotId, installed.CustodianId, bag, installed.Quantity)])).Message);
    }

    private static WorldActionCommand Submit(WorldSnapshot world, EntityId actor, ulong command, WorldActionIntent intent)
    {
        ActorControlBinding control = world.Inventory.Controls.Single(value => value.ActorId == actor);
        return new WorldActionCommand(Id(command), control.PrincipalId, control.SessionId, actor, world.WorldId,
            world.BranchId, world.Space.GetActor(actor).Revision, intent);
    }

    // Real movement samples through the world step until both residents are within reach.
    private static WorldSnapshot Approach(WorldSnapshot world, EntityId mover, EntityId target)
    {
        ActorControlBinding control = world.Inventory.Controls.Single(value => value.ActorId == mover);
        var delivery = new TrustedCommandDelivery(control.PrincipalId, control.SessionId);
        for (ulong step = 0; world.Space.QueryInteraction(mover, target) != SpatialInteraction.Reachable; step++)
        {
            Assert.True(step < 200, "The route between the residents exceeded its finite fixture budget.");
            SubunitPosition from = world.Space.GetActor(mover).Feet;
            SubunitPosition to = world.Space.GetActor(target).Feet;
            long dx = Math.Sign(to.X - from.X) * WorldMovementInput.DisplacementPerStep;
            long dy = dx == 0 ? Math.Sign(to.Y - from.Y) * WorldMovementInput.DisplacementPerStep : 0;
            Assert.True(dx != 0 || dy != 0);
            var batch = new WorldStepBatch(Id(50_000 + step), world.WorldId, world.BranchId, world.Revision,
                world.AdvancingSteps, WorldBatchKind.AdvancingStep, [new WorldMovementInput(delivery, mover, dx, dy)], [],
                world.ProfileVersion);
            WorldSnapshot next = new WorldStepProcessor().Decide(world, batch).Candidate;
            Assert.NotEqual(from, next.Space.GetActor(mover).Feet);
            world = next;
        }
        return world;
    }

    private static WorldSnapshot Crafted(out TransformedLot lot)
    {
        WorldSnapshot basis = GatheringFixture.Create(storageCapacity: 64);
        var package = new PackageId("world-consented-test");
        var input = new MaterialDefinition(package, Id(1001), "input", new MassMilligrams(10), new VolumeMicrolitres(20), new HardnessRating(1));
        var output = new MaterialDefinition(package, Id(1002), "portion", new MassMilligrams(10), new VolumeMicrolitres(15), new HardnessRating(2));
        var physical = new MaterialRecipeDefinition(package, Id(1003), "portion", new WorkUnits(1), null,
            new MaterialCatalog([input, output]), [new(RecipeMaterialRole.Input, input.Pin(), 2), new(RecipeMaterialRole.Output, output.Pin(), 2)]);
        var recipe = new CraftRecipeDefinition(Id(1005), physical, [new(Material, input), new(ProductionWorldFixture.Product, output)],
            [new(Material, 0, new MassMilligrams(10))]);
        var ledger = new ProductionSnapshot(basis.WorldId, basis.BranchId, 0, [recipe], [], []);
        var migration = new WorldProductionMigration(Id(1100), basis.WorldId, basis.BranchId, basis.Revision,
            basis.AdvancingSteps, basis.ContentHash, basis.AdmissionVersion,
            WorldProductionMigration.ComputeTargetAdmissionVersion(basis.AdmissionVersion, ledger), ledger);
        WorldSnapshot admitted = new WorldProductionMigrationProcessor().Decide(basis, migration).Candidate;

        // Actual crafting through the admitted world step.
        WorldStepDecision decision = new WorldStepProcessor().Decide(admitted,
            ProductionWorldFixture.Batch(admitted, 1300, ProductionWorldFixture.Craft(admitted, 1200)));
        Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
        WorldSnapshot world = decision.Candidate;
        lot = Assert.Single(world.Production!.Lots);
        Assert.Equal((PlayerBag, 0UL, 2UL, EntityId.DeriveSpawned(Id(1200), 1)),
            (lot.CustodianId, lot.Output.Start, lot.Output.End, lot.LotId));
        return world;
    }

    private static ConsentedMaterialTerms Terms(WorldSnapshot world, Kind kind, EntityId lotId,
        CountedItemDefinition definition, ulong quantity, ulong lotRevision) => new(world.WorldId, world.BranchId,
        Player, Helper, kind, lotId, definition, PlayerBag, HelperBag, quantity, lotRevision);

    private static (WorldActionCommand Offer, WorldActionCommand Acceptance) Pair(WorldSnapshot world,
        ConsentedMaterialTerms terms, ulong command)
    {
        WorldActionCommand offer = StepFixture.Action(world, command,
            new OfferMaterialIntent(terms, world.Space.GetActor(Helper).Revision), actor: 10);
        WorldActionCommand acceptance = StepFixture.Action(world, command + 1,
            AcceptMaterialIntent.Approving(offer, world.Space.GetActor(Player).Revision), actor: 11);
        return (offer, acceptance);
    }

    private static WorldConsentedMaterialPlan Plan(WorldSnapshot world, WorldActionCommand offer,
        WorldActionCommand acceptance, WorldBatchKind kind, Func<EntityId, bool>? occupied = null) =>
        new WorldConsentedMaterialPlanner().Plan(world, offer, acceptance, kind,
            Delivery(world, offer.ActorId), Delivery(world, acceptance.ActorId), occupied);

    // The harness supplies independently selected controller deliveries, never copies command claims.
    private static TrustedCommandDelivery Delivery(WorldSnapshot world, EntityId actor)
    {
        ActorControlBinding binding = world.Inventory.Controls.Single(value => value.ActorId == actor);
        return new TrustedCommandDelivery(binding.PrincipalId, binding.SessionId);
    }

    private static void AssertRefused(WorldSnapshot world, WorldActionCommand offer, WorldActionCommand acceptance,
        WorldActionRejection offerReason, WorldActionRejection acceptanceReason, Func<EntityId, bool>? occupied = null)
    {
        WorldConsentedMaterialPlan plan = Plan(world, offer, acceptance,
            WorldBatchKind.AdvancingStep, occupied);
        Assert.Equal((offerReason, acceptanceReason), (plan.OfferRejection, plan.AcceptanceRejection));
        Assert.False(plan.IsApplied);
        Assert.Null(plan.Evidence);
        Assert.Same(world.Inventory, plan.CandidateInventory);
        Assert.Same(world.Production, plan.CandidateProduction);
        Assert.Throws<InvalidOperationException>(() => plan.ApplyTo(world.Inventory, world.Production!));
    }

    private static WorldSnapshot With(WorldSnapshot world, CountedAuthorityState inventory, ProductionSnapshot production,
        LocalSpatialSnapshot space, ulong revision) => new(inventory, space, world.Residents, world.AdmissionVersion,
        world.Calendar, revision, world.AdvancingSteps, world.Dialogues, world.Gathering, production);

    private static LocalSpatialSnapshot WithoutManipulation(LocalSpatialSnapshot space, EntityId actorId)
    {
        var body = new BodyDefinition(Id(41), BodyNature.Corporeal, new BodyDimensions(128, 128, 384),
            [new(new BodySlotKey("core"), null, new BodyDimensions(64, 64, 128),
                [BodyFunction.GroundContact, BodyFunction.Hearing, BodyFunction.Vocalization])],
            [new(LocomotionMode.Ground, 1)]);
        return new LocalSpatialSnapshot(space.Scope, space.Regions, space.Actors.Select(actor =>
        {
            if (actor.ActorId != actorId) return actor;
            var binding = ActorFormBinding.Corporeal(actor.ActorId, actor.Binding.FormInstanceId,
                actor.Binding.PhysicalBodyId!.Value, body);
            return new ActorPresence(binding, new BodyConditionSnapshot(binding.FormInstanceId, body, []),
                actor.Scope, actor.Feet, actor.Revision, actor.HomePlaceId);
        }));
    }
}
