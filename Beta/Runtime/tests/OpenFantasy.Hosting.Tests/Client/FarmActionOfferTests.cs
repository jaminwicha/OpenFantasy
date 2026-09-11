using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.Client;

public sealed class FarmActionOfferTests
{
    private static readonly EntityId Player = Id(10);
    private static readonly EntityId Wren = Id(11);
    private static readonly WorldStepProcessor Processor = new();

    private static EntityId WoodDefinition(WorldSnapshot world) =>
        world.Inventory.Sources.Single(source => source.SourceId == Id(71)).Definition.DefinitionId;

    private static FarmActionOffers Offers(WorldSnapshot world, TrustedCommandDelivery? delivery = null) =>
        new(delivery ?? Delivery, Wren, WoodDefinition(world), null);

    private static LocalWorldView View(WorldSnapshot world) => new(world, Player);

    private static WorldStepDecision Apply(WorldSnapshot world, ulong cause, WorldActionCommand? action = null,
        WorldMovementInput? movement = null) => Processor.Decide(world, new WorldStepBatch(Id(cause), world.WorldId,
            world.BranchId, world.Revision, world.AdvancingSteps,
            world.HasReadingHold ? WorldBatchKind.HeldInteraction : WorldBatchKind.AdvancingStep,
            movement is null ? [] : [movement], action is null ? [] : [action], world.ProfileVersion));

    [Fact]
    public void GatherOfferKeepsItsCapturedPlantAndRevisionsThroughDepletionAndMovement()
    {
        WorldSnapshot world = GatheringWorldFixture.Create();
        byte[] before = WorldSnapshotCodec.Encode(world);
        FarmActionOffer captured = Offers(world).Create(View(world))[FarmSemanticAction.Gather];

        Assert.Equal(before, WorldSnapshotCodec.Encode(world));
        Assert.True(captured.CanExecute);
        Assert.Equal(GatheringWorldFixture.NodeId, captured.TargetId);
        Assert.Equal("Chop oak", captured.Label);
        Assert.Contains(GatherRulesV1.EffortPerSwing.ToString(), captured.CostText, StringComparison.Ordinal);
        var intent = Assert.IsType<GatherIntent>(captured.Intent);
        Assert.Equal((GatheringWorldFixture.NodeId, 0UL, GatheringWorldFixture.ToolLotId, 0UL),
            (intent.NodeId, intent.ExpectedNodeRevision, intent.ToolLotId, intent.ExpectedToolLotRevision));

        // Another swing depletes the plant and moves its revision; the player then walks out of reach.
        world = Apply(world, 40_001, GatheringWorldFixture.Swing(world)).Candidate;
        for (ulong step = 0; step < 24; step++)
        {
            world = Apply(world, 40_100 + step, movement: new WorldMovementInput(Delivery, Player, 0,
                WorldMovementInput.DisplacementPerStep)).Candidate;
        }
        LocalWorldView moved = View(world);
        Assert.False(FarmClientSession.CanReachGatherNode(moved, moved.GatheringNodes.Single()));
        FarmActionOffer fresh = Offers(world).Create(moved)[FarmSemanticAction.Gather];
        Assert.False(fresh.CanExecute);
        Assert.Equal(GatheringWorldFixture.NodeId, fresh.TargetId);
        Assert.NotNull(fresh.Intent);
        Assert.Same(intent, captured.Intent);

        // Submitting the captured offer reaches the host's ordinary rejection with no substituted target.
        uint swings = moved.GatheringNodes.Single().RemainingSwings;
        WorldStepDecision decision = Apply(world, 40_200, captured.ToCommand(Id(40_201)));
        Assert.Equal(WorldActionRejection.RevisionConflict, decision.Receipts.Single().Rejection);
        Assert.Equal(swings, View(decision.Candidate).GatheringNodes.Single().RemainingSwings);
    }

    [Fact]
    public void GiftOfferKeepsItsCapturedLotAndInventoryRevisionsAfterStockChanges()
    {
        WorldSnapshot world = GatheringWorldFixture.Create();
        LocalWorldView original = View(world);
        FarmActionOffer captured = Offers(world).Create(original)[FarmSemanticAction.Give];
        var gift = Assert.IsType<GiveItemIntent>(captured.Intent);
        Assert.Equal((Wren, Id(70), 0UL, original.PlayerInventory.Revision, 6UL),
            (gift.TargetActorId, gift.LotId, gift.ExpectedLotRevision, gift.ExpectedSourceRevision, gift.Quantity));
        Assert.Equal("6 wood", captured.CostText);

        world = Apply(world, 41_001, Give(world, 41_002, quantity: 1)).Candidate;
        LocalWorldView restocked = View(world);
        var reselected = Assert.IsType<GiveItemIntent>(Offers(world).Create(restocked)[FarmSemanticAction.Give].Intent);
        Assert.Equal(5UL, reselected.Quantity);
        Assert.NotEqual(gift.ExpectedLotRevision, reselected.ExpectedLotRevision);
        Assert.Same(gift, captured.Intent);

        ulong wood = restocked.PlayerInventory.Quantity;
        WorldStepDecision decision = Apply(world, 41_100, captured.ToCommand(Id(41_101)));
        // The host classifies the stale six-unit request against a five-unit lot; it is rejected, never resized.
        Assert.NotEqual(WorldActionRejection.None, decision.Receipts.Single().Rejection);
        Assert.Equal(wood, View(decision.Candidate).PlayerInventory.Quantity);
    }

    [Fact]
    public void QuestAcceptanceOfferStaysAcceptanceAfterTheQuestIsAccepted()
    {
        WorldSnapshot world = GatheringWorldFixture.Create();
        world = new WorldProductionMigrationProcessor().Decide(world, ProductionUpgradeFixture.Request(world)).Candidate;
        world = new WorldQuestMigrationProcessor().Decide(world, QuestUpgradeFixture.Request(world)).Candidate;
        FarmActionOffer beforeSpeaking = Offers(world).Create(View(world))[FarmSemanticAction.Give];
        Assert.Equal(FarmActionOffers.SpeakFirst, beforeSpeaking.BlockingReason);
        Assert.Null(beforeSpeaking.Intent);

        world = Apply(world, 42_001, Speak(world, 42_002)).Candidate;
        LocalWorldView addressed = View(world);
        FarmActionOffer acceptance = Offers(world).Create(addressed)[FarmSemanticAction.Give];
        Assert.Equal("Accept task", acceptance.Label);
        Assert.Equal(addressed.Dialogue!.DialogueId, acceptance.DialogueId);
        Assert.Equal(Wren, acceptance.TargetId);
        Assert.Contains("Reward: 420 XP and 200 gil", acceptance.DetailText, StringComparison.Ordinal);
        Assert.IsType<AcceptDeliveryQuestIntent>(acceptance.Intent);

        world = Apply(world, 42_003, QuestUpgradeFixture.Accept(world)).Candidate;
        FarmActionOffer delivery = Offers(world).Create(View(world))[FarmSemanticAction.Give];
        Assert.Equal("Deliver", delivery.Label);
        Assert.IsType<CompleteDeliveryQuestIntent>(delivery.Intent);
        Assert.IsType<AcceptDeliveryQuestIntent>(acceptance.Intent);

        ulong owned = View(world).PlayerInventory.Quantity;
        WorldStepDecision decision = Apply(world, 42_100, acceptance.ToCommand(Id(42_101)));
        Assert.Equal(WorldActionRejection.RevisionConflict, decision.Receipts.Single().Rejection);
        Assert.Equal(ActorQuestStatus.Accepted, View(decision.Candidate).Quests.Single().Status);
        Assert.Equal(owned, View(decision.Candidate).PlayerInventory.Quantity);
    }

    [Fact]
    public async Task SessionSubmitsCapturedOffersAndRefusesBlockedForeignOrClosedConversationOffers()
    {
        using var files = new SessionFiles();
        WorldSnapshot genesis = GatheringWorldFixture.Create();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var time = new ManualSessionTimeProvider();
        var world = new LocalWorldSession(authority, Player, Delivery, time);
        await using var session = new FarmClientSession(world, Delivery, Wren, WoodDefinition(genesis), null);
        world.SetReady(true);

        byte[] before = WorldSnapshotCodec.Encode(authority.ReadStateForHost());
        LocalWorldView view = session.World.ReadView();
        IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> offers = session.CreateOffers(view);
        Assert.Equal(before, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
        Assert.Same(view, session.World.ReadView());
        Assert.DoesNotContain(FarmSemanticAction.Craft, offers.Keys);

        // A blocked offer never becomes a command.
        FarmActionOffer close = offers[FarmSemanticAction.CloseDialogue];
        Assert.False(close.CanExecute);
        Assert.Equal(close.BlockingReason, (await Assert.ThrowsAsync<InvalidOperationException>(() => session.SubmitOffer(close))).Message);

        // An executable offer from another session is refused before submission.
        var foreign = new TrustedCommandDelivery(Id(101), Id(111));
        FarmActionOffer stranger = new FarmActionOffers(foreign, Wren, WoodDefinition(genesis), null).Create(view)[FarmSemanticAction.Gather];
        Assert.True(stranger.CanExecute);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SubmitOffer(stranger));
        Assert.Equal(before, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));

        // One captured offer is one command: retries return its original outcome without new receipts.
        FarmActionOffer gather = offers[FarmSemanticAction.Gather];
        Task<WorldActionReceipt> gathering = session.SubmitOffer(gather);
        await Tick(time);
        Assert.Equal(CommandReceiptStatus.Applied, (await gathering).Status);
        Assert.Equal(4U, session.World.ReadView().GatheringNodes.Single().RemainingSwings);
        Task<WorldActionReceipt> stale = session.SubmitOffer(gather);
        Assert.Same(gathering, stale);
        Assert.Equal((await gathering).ResultId, (await stale).ResultId);
        Assert.Equal(4U, session.World.ReadView().GatheringNodes.Single().RemainingSwings);

        // Conversation offers bind the addressed dialogue; a closed or reopened one is refused client-side.
        Task<WorldActionReceipt> speaking = session.SubmitOffer(session.CreateOffers(session.World.ReadView())[FarmSemanticAction.Speak]);
        await Tick(time);
        Assert.Equal(CommandReceiptStatus.Applied, (await speaking).Status);
        LocalWorldView conversation = session.World.ReadView();
        FarmActionOffer closing = session.CreateOffers(conversation)[FarmSemanticAction.CloseDialogue];
        FarmActionOffer unusedClosing = session.CreateOffers(conversation)[FarmSemanticAction.CloseDialogue];
        Assert.Equal(conversation.Dialogue!.DialogueId, closing.DialogueId);
        Assert.Equal(conversation.Dialogue.SpeakerName, closing.TargetName);
        Task<WorldActionReceipt> closed = session.SubmitOffer(closing);
        await Tick(time);
        Assert.Equal(CommandReceiptStatus.Applied, (await closed).Status);
        Assert.Null(session.World.ReadView().Dialogue);
        Assert.Same(closed, session.SubmitOffer(closing));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SubmitOffer(unusedClosing));
        Task<WorldActionReceipt> reopened = session.SubmitOffer(session.CreateOffers(session.World.ReadView())[FarmSemanticAction.Speak]);
        await Tick(time);
        Assert.Equal(CommandReceiptStatus.Applied, (await reopened).Status);
        Assert.NotEqual(closing.DialogueId, session.World.ReadView().Dialogue!.DialogueId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SubmitOffer(unusedClosing));
    }

    [Fact]
    public async Task SameSavedControlIdentityCannotCarryAnOfferAcrossClientReopen()
    {
        using var files = new SessionFiles();
        WorldSnapshot genesis = GatheringWorldFixture.Create();
        FarmActionOffer captured;
        var firstWorld = new LocalWorldSession(SqliteWorldAuthority.CreateNew(files.Path, genesis), Player,
            Delivery, new ManualSessionTimeProvider());
        await using (var first = new FarmClientSession(firstWorld, Delivery, Wren, WoodDefinition(genesis), null))
        {
            captured = first.CreateOffers(first.World.ReadView())[FarmSemanticAction.Gather];
            Assert.True(captured.CanExecute);
        }
        var authority = SqliteWorldAuthority.OpenExisting(files.Path);
        var reopenedWorld = new LocalWorldSession(authority, Player, Delivery, new ManualSessionTimeProvider());
        await using var reopened = new FarmClientSession(reopenedWorld, Delivery, Wren, WoodDefinition(genesis), null);
        Assert.True(captured.Scope.BelongsTo(Delivery, reopenedWorld.ReadView()));
        byte[] before = WorldSnapshotCodec.Encode(authority.ReadStateForHost());
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.SubmitOffer(captured));
        Assert.Equal(before, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
    }

    [Fact]
    public void UnaddressedQuestOffersContainNoUnprojectedRequirementOrReward()
    {
        WorldSnapshot world = GatheringWorldFixture.Create();
        world = new WorldProductionMigrationProcessor().Decide(world, ProductionUpgradeFixture.Request(world)).Candidate;
        world = new WorldQuestMigrationProcessor().Decide(world, QuestUpgradeFixture.Request(world)).Candidate;
        LocalWorldView unknown = View(world);
        Assert.Empty(unknown.Quests);
        FarmActionOffer offer = Offers(world).Create(unknown)[FarmSemanticAction.Give];
        Assert.False(offer.CanExecute);
        Assert.Null(offer.Intent);
        string details = string.Join(" ", offer.Label, offer.CostText, offer.DetailText, offer.BlockingReason);
        Assert.DoesNotContain("wood", details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("orchard", details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("420", details, StringComparison.Ordinal);
        Assert.DoesNotContain("200", details, StringComparison.Ordinal);
        Assert.DoesNotContain("6", details, StringComparison.Ordinal);
    }

    [Fact]
    public void TransformedStockCannotReplaceCapturedCraftInputsOrDisplaceLegalGiftWood()
    {
        // A synthetic conserved transform intentionally keeps the item definition while changing
        // provenance. Four transformed units must not eclipse the remaining two raw gift units.
        WorldSnapshot world = GatheringWorldFixture.Create();
        var wood = world.Inventory.Sources.Single(source => source.SourceId == Id(71)).Definition;
        var package = new PackageId("test.offer-provenance");
        var material = new MaterialDefinition(package, Id(44_001), "wood", new(10), new(10), new(1));
        var physical = new MaterialRecipeDefinition(package, Id(44_002), "craft_rations", new(1), null,
            new MaterialCatalog([material]),
            [new(RecipeMaterialRole.Input, material.Pin(), 4), new(RecipeMaterialRole.Output, material.Pin(), 4)]);
        var recipe = new CraftRecipeDefinition(Id(44_003), physical, [new(wood, material)], [new(wood, 0, new(10))]);
        var production = new ProductionSnapshot(world.WorldId, world.BranchId, 0, [recipe], [], []);
        var migration = new WorldProductionMigration(Id(44_004), world.WorldId, world.BranchId,
            world.Revision, world.AdvancingSteps, world.ContentHash, world.AdmissionVersion,
            WorldProductionMigration.ComputeTargetAdmissionVersion(world.AdmissionVersion, production), production);
        world = new WorldProductionMigrationProcessor().Decide(world, migration).Candidate;

        FarmActionOffer captured = Offers(world).Create(View(world))[FarmSemanticAction.Craft];
        var intent = Assert.IsType<CraftIntent>(captured.Intent);
        WorldStepDecision first = Apply(world, 44_005, captured.ToCommand(Id(44_006)));
        Assert.Equal(CommandReceiptStatus.Applied, first.Receipts.Single().Status);
        world = first.Candidate;
        LocalWorldView current = View(world);
        Assert.Equal(4UL, current.PlayerInventory.Lots.Single(lot => lot.Provenance == IngredientLotKind.Transformed).Quantity);
        Assert.True(FarmCrafting.HasIngredients(current));
        Assert.Same(intent, captured.Intent);
        var gift = Assert.IsType<GiveItemIntent>(Offers(world).Create(current)[FarmSemanticAction.Give].Intent);
        Assert.Equal(Id(70), gift.LotId);
        Assert.Equal(2UL, gift.Quantity);

        WorldStepDecision stale = Apply(world, 44_007, captured.ToCommand(Id(44_008)));
        Assert.Equal(CommandReceiptStatus.Rejected, stale.Receipts.Single().Status);
        Assert.Equal(world.Production!.ContentHash, stale.Candidate.Production!.ContentHash);
        Assert.Equal(current.PlayerInventory.Lots, View(stale.Candidate).PlayerInventory.Lots);
    }
}
