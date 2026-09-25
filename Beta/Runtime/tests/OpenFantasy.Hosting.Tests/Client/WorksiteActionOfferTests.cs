using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Client;

public sealed class WorksiteActionOfferTests
{
    [Fact]
    public void Mining_requires_the_admitted_domain_and_an_explicit_visible_site_target()
    {
        var fixture = new MiningFixture();
        foreach (WorldSnapshot legacy in new[] { fixture.Source.Basis, WorldServiceFixture.Genesis() })
        {
            EntityId player = Player(legacy);
            var offers = Offers(legacy, player).Create(new LocalWorldView(legacy, player), fixture.Target);
            Assert.DoesNotContain(FarmSemanticAction.Excavate, offers.Keys);
            Assert.DoesNotContain(FarmSemanticAction.Traverse, offers.Keys);
        }
        LocalWorldView view = fixture.View(fixture.Genesis);
        var factory = Offers(fixture.Genesis, fixture.Player);
        Assert.DoesNotContain(FarmSemanticAction.Excavate, factory.Create(view).Keys);
        var person = new FarmTarget(Speaker(fixture.Genesis), "Wren", FarmTargetKind.Person,
            fixture.Genesis.Space.GetActor(Speaker(fixture.Genesis)).Feet);
        Assert.DoesNotContain(FarmSemanticAction.Excavate, factory.Create(view, person).Keys);
        var missing = new FarmTarget(new(0x776f726b73697465, 900), "Missing face", FarmTargetKind.Excavation, null);
        FarmActionOffer unavailable = factory.Create(view, missing)[FarmSemanticAction.Excavate];
        Assert.False(unavailable.CanExecute);
        Assert.Equal(missing.Id, unavailable.TargetId);
        Assert.Null(unavailable.Intent);
        Assert.Contains("no longer visible", unavailable.BlockingReason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Mining_captures_exact_site_tool_storage_and_actor_revisions_after_real_effort_recovery()
    {
        var fixture = new MiningFixture();
        FarmActionOffer tired = fixture.ReadOffers(fixture.Genesis)[FarmSemanticAction.Excavate];
        Assert.False(tired.CanExecute);
        Assert.Contains("tired", tired.BlockingReason!, StringComparison.Ordinal);
        WorldSnapshot ready = fixture.Source.Earn(fixture.Genesis);
        byte[] bytes = WorldSnapshotCodec.Encode(ready);
        LocalWorldView view = fixture.View(ready);
        FarmActionOffer captured = fixture.ReadOffers(ready)[FarmSemanticAction.Excavate];
        var intent = Assert.IsType<ExcavateIntent>(captured.Intent);
        LocalExcavationView target = Assert.Single(view.Worksite!.ExcavationTargets);
        LocalExcavationToolView tool = Assert.Single(view.Worksite.Tools);
        Assert.True(captured.CanExecute);
        Assert.Equal("400 effort", captured.CostText);
        Assert.Equal(400U, view.Worksite.PlayerEffort);
        Assert.Equal(fixture.Target.Id, captured.TargetId);
        Assert.Equal((target.SiteId, target.SiteRevision, target.TerrainRevision),
            (intent.SiteId, intent.ExpectedSiteRevision, intent.ExpectedTerrainRevision));
        Assert.Equal((tool.LotId, tool.Revision, view.PlayerInventory.Revision),
            (intent.ToolLotId, intent.ExpectedToolLotRevision, intent.ExpectedDestinationRevision));
        WorldActionCommand command = captured.ToCommand(fixture.Source.Actions.NextIdentity());
        Assert.Same(intent, command.Intent);
        Assert.Equal(ready.Space.GetActor(fixture.Player).Revision, command.ExpectedActorRevision);
        Assert.Equal(ready.WorldId, command.WorldId);
        Assert.Equal(ready.BranchId, command.BranchId);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(ready));

        WorldStepDecision worked = fixture.Source.Step(ready, command);
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(worked.Receipts).Status);
        Assert.Equal(1UL, worked.Candidate.Excavation!.Sites.Single().CompletedWork);
        Assert.Same(intent, captured.Intent);
        Assert.Equal(0UL, intent.ExpectedSiteRevision);
        Assert.True(ready.Space.Regions.Single(region => region.Bounds.Contains(target.Cell)).GetCell(target.Cell).IsSolid);
    }

    [Fact]
    public void Captured_ledge_requests_preserve_the_other_axis_and_mining_is_blocked_on_the_upper_floor()
    {
        var fixture = new MiningFixture();
        WorldSnapshot state = fixture.Source.Earn(fixture.Genesis);
        state = fixture.Move(state, 32, 0);
        Assert.Equal(new SubunitPosition(160, 128, 0), state.Space.GetActor(fixture.Player).Feet);
        FarmActionOffer climbOut = fixture.ReadOffers(state)[FarmSemanticAction.Traverse];
        Assert.True(climbOut.CanExecute);
        Assert.Equal(new ClimbIntent(0, -256, 256), Assert.IsType<ClimbIntent>(climbOut.Intent));
        state = fixture.Apply(state, climbOut);
        Assert.Equal(new SubunitPosition(160, -128, 256), state.Space.GetActor(fixture.Player).Feet);
        state = fixture.Source.Earn(state);

        IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> upper = fixture.ReadOffers(state);
        Assert.False(upper[FarmSemanticAction.Excavate].CanExecute);
        Assert.Contains("lower work stance", upper[FarmSemanticAction.Excavate].BlockingReason!, StringComparison.Ordinal);
        FarmActionOffer climbDown = upper[FarmSemanticAction.Traverse];
        Assert.True(climbDown.CanExecute);
        Assert.Equal(new ClimbIntent(0, 256, -256), Assert.IsType<ClimbIntent>(climbDown.Intent));
        Assert.Equal(state.Space.GetActor(fixture.Player).Revision, climbDown.Scope.ExpectedActorRevision);
        state = fixture.Apply(state, climbDown);
        Assert.Equal(new SubunitPosition(160, 128, 0), state.Space.GetActor(fixture.Player).Feet);
    }

    [Fact]
    public void Site_selection_is_read_only_and_retains_a_missing_target_instead_of_selecting_another()
    {
        var fixture = new MiningFixture();
        WorldSnapshot state = fixture.Genesis;
        byte[] before = WorldSnapshotCodec.Encode(state);
        var selection = new FarmTargetSelection(Speaker(state));
        LocalWorldView view = fixture.View(state);
        selection.Refresh(view);
        for (int count = 0; selection.Selected?.Kind != FarmTargetKind.Excavation; count++)
        {
            Assert.True(count < view.Actors.Count + view.GatheringNodes.Count + view.Worksite!.ExcavationTargets.Count);
            selection.Cycle();
        }
        FarmTarget chosen = Assert.IsType<FarmTarget>(selection.Selected);
        Assert.Equal(fixture.Target.Id, chosen.Id);
        Assert.Equal(fixture.Target.Feet, chosen.Feet);
        selection.Refresh(fixture.View(fixture.Source.Basis));
        Assert.Equal(fixture.Target.Id, selection.Selected!.Id);
        Assert.Null(selection.Selected.Feet);
        Assert.Equal(before, WorldSnapshotCodec.Encode(state));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Open_traverse_slot_cannot_change_its_exit_or_actor_revision(bool changedActorRevision)
    {
        var fixture = new MiningFixture();
        WorldSnapshot state = fixture.Source.Earn(fixture.Genesis);
        IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> offers = fixture.ReadOffers(state);
        var wheel = new FarmQuickWheelModel(offers);
        Assert.Equal(FarmSemanticAction.Excavate, wheel.Slots[1]!.Action);
        Assert.Equal(FarmSemanticAction.Traverse, wheel.Slots[2]!.Action);
        wheel.Select(2);
        FarmActionOffer captured = wheel.Selected!.Offer;
        Assert.True(captured.CanExecute);
        Assert.Equal(FarmTargetKind.Excavation, wheel.Selected.PinnedTarget!.Kind);
        Assert.Equal(fixture.Target.Id, wheel.Selected.PinnedTarget.Id);
        var altered = offers.ToDictionary();
        altered[FarmSemanticAction.Traverse] = new FarmActionOffer(captured.Action,
            changedActorRevision ? captured.Scope with { ExpectedActorRevision = captured.Scope.ExpectedActorRevision + 1 } : captured.Scope,
            captured.Label, captured.TargetName, captured.TargetId, captured.CostText, captured.CompletionText,
            changedActorRevision ? captured.Intent : new ClimbIntent(-256, 0, 256), null);
        // A new preview can reflect another terrain contact or position. It cannot replace the
        // exact inspected exit under the same label and site while this wheel remains open.
        wheel.Refresh(_ => altered);
        Assert.Equal(2, wheel.SelectedIndex);
        Assert.False(wheel.Selected.Offer.CanExecute);
        Assert.Null(wheel.Selected.Offer.Intent);
        Assert.Equal(new ClimbIntent(0, -256, 256), captured.ToCommand(fixture.Source.Actions.NextIdentity()).Intent);
    }

    [Fact]
    public async Task Repeated_mining_offer_shares_one_command_and_unused_stale_offer_never_substitutes_current_revisions()
    {
        var fixture = new MiningFixture();
        using var files = new SessionFiles();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        WorldSnapshot ready = fixture.Genesis;
        for (int step = 0; step < 40; step++)
        {
            ready = authority.Commit(fixture.Source.Actions.Batch(ready, [], [])).Candidate;
        }
        var time = new ManualSessionTimeProvider();
        TrustedCommandDelivery delivery = ConstructionUpgradeFixture.Delivery(ready, fixture.Player);
        var world = new LocalWorldSession(authority, fixture.Player, delivery, time);
        await using var client = new FarmClientSession(world, delivery, Speaker(ready), Wood(ready), null);
        LocalWorldView view = world.ReadView();
        FarmActionOffer captured = client.CreateOffers(view, fixture.Target)[FarmSemanticAction.Excavate];
        FarmActionOffer unusedStale = client.CreateOffers(view, fixture.Target)[FarmSemanticAction.Excavate];
        Assert.True(captured.CanExecute);
        world.SetReady(true);
        Task<WorldActionReceipt> first = client.SubmitOffer(captured);
        Assert.Same(first, client.SubmitOffer(captured));
        await Tick(time);
        WorldActionReceipt applied = await first.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(CommandReceiptStatus.Applied, applied.Status);
        Assert.Equal(1UL, authority.ReadStateForHost().Excavation!.Sites.Single().CompletedWork);
        Assert.Same(first, client.SubmitOffer(captured));
        Assert.Equal(1UL, authority.InspectForHost().Journal.CommandOutcomeCount);

        Task<WorldActionReceipt> stale = client.SubmitOffer(unusedStale);
        await Tick(time);
        WorldActionReceipt rejected = await stale.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(WorldActionRejection.RevisionConflict, rejected.Rejection);
        Assert.NotEqual(applied.CommandId, rejected.CommandId);
        Assert.Equal(0UL, Assert.IsType<ExcavateIntent>(unusedStale.Intent).ExpectedSiteRevision);
        Assert.Equal(1UL, authority.ReadStateForHost().Excavation!.Sites.Single().CompletedWork);
        Assert.Equal(ready.Inventory.ContentHash, authority.ReadStateForHost().Inventory.ContentHash);
        Assert.Equal(2UL, authority.InspectForHost().Journal.CommandOutcomeCount);
        Assert.Same(stale, client.SubmitOffer(unusedStale));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Addressed_tasks_prioritize_the_selected_acceptance_and_do_not_rebind_an_open_accept_slot(bool selectSecond)
    {
        WorldSnapshot state = WorksiteFarmAdmission.CreateEmbedded("worksite-client-quests", "Traveler");
        var actions = new ConstructionUpgradeFixture();
        EntityId player = Player(state);
        EntityId speaker = Speaker(state);
        for (int count = 0; state.Space.QueryInteraction(player, speaker) != SpatialInteraction.Reachable; count++)
        {
            Assert.True(count < 128);
            SubunitPosition feet = state.Space.GetActor(player).Feet;
            SubunitPosition target = state.Space.GetActor(speaker).Feet;
            long dx = Math.Sign(target.X - feet.X) * WorldMovementInput.DisplacementPerStep;
            long dy = dx == 0 ? Math.Sign(target.Y - feet.Y) * WorldMovementInput.DisplacementPerStep : 0;
            state = new WorldStepProcessor().Decide(state, actions.Batch(state, [],
                [new(ConstructionUpgradeFixture.Delivery(state, player), player, dx, dy)])).Candidate;
        }
        state = Act(state, actions, new SpeakToActorIntent(speaker, state.Space.GetActor(speaker).Revision));
        LocalWorldView initial = new(state, player);
        Assert.Equal(2, initial.Quests.Count);
        var factory = Offers(state, player);
        var wheel = new FarmQuickWheelModel(factory.Create(initial));
        wheel.Select(3);
        FarmActionOffer original = wheel.Selected!.Offer;
        EntityId firstId = Assert.IsType<AcceptDeliveryQuestIntent>(original.Intent).QuestId;
        LocalQuestView selected = initial.Quests.OrderBy(quest => quest.DefinitionId).ElementAt(selectSecond ? 1 : 0);
        state = Act(state, actions, FarmQuestActions.Request(initial, selected));
        LocalWorldView accepted = new(state, player);
        Assert.Equal(selected.DefinitionId, FarmQuestActions.AddressedQuest(accepted)!.DefinitionId);
        FarmActionOffer delivery = factory.Create(accepted)[FarmSemanticAction.Give];
        Assert.Equal(selected.DefinitionId, Assert.IsType<CompleteDeliveryQuestIntent>(delivery.Intent).QuestId);
        state = Act(state, actions, delivery.Intent!);

        LocalWorldView completed = new(state, player);
        LocalQuestView remaining = Assert.Single(completed.Quests, quest => quest.Status is null);
        Assert.NotEqual(selected.DefinitionId, remaining.DefinitionId);
        Assert.Equal(remaining.DefinitionId, FarmQuestActions.AddressedQuest(completed)!.DefinitionId);
        FarmActionOffer fresh = factory.Create(completed)[FarmSemanticAction.Give];
        Assert.Equal(remaining.DefinitionId, Assert.IsType<AcceptDeliveryQuestIntent>(fresh.Intent).QuestId);
        Assert.True(fresh.CanExecute);
        wheel.Refresh(target => factory.Create(completed, target));
        Assert.Equal(firstId, Assert.IsType<AcceptDeliveryQuestIntent>(original.Intent).QuestId);
        if (selectSecond)
        {
            // The untouched first task is still the original inspected operation.
            Assert.True(wheel.Selected.Offer.CanExecute);
            Assert.Equal(firstId, Assert.IsType<AcceptDeliveryQuestIntent>(wheel.Selected.Offer.Intent).QuestId);
        }
        else
        {
            Assert.False(wheel.Selected.Offer.CanExecute);
            Assert.Null(wheel.Selected.Offer.Intent);
        }
    }

    [Fact]
    public async Task Worksite_opening_refuses_an_existing_legacy_save_without_replacing_it_or_retaining_its_lease()
    {
        using var files = new SessionFiles();
        WorldSnapshot legacy = GatheringFarmAdmission.CreateEmbedded("worksite-selection", "Traveler");
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, legacy))
        {
            Assert.Equal(legacy.ContentHash, authority.ReadStateForHost().ContentHash);
        }
        byte[] closed = File.ReadAllBytes(files.Path);
        await Assert.ThrowsAsync<InvalidDataException>(() => FarmClientSession.OpenWorksiteAsync(files.Path,
            "worksite-selection", "Traveler"));
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(legacy.ContentHash, reopened.ReadStateForHost().ContentHash);
    }

    private static EntityId Player(WorldSnapshot state) => state.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
    private static EntityId Speaker(WorldSnapshot state) => state.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Host).ActorId;
    private static EntityId Wood(WorldSnapshot state) => state.Quests?.Definitions[0].Item.DefinitionId
        ?? state.Inventory.Sources[0].Definition.DefinitionId;
    private static FarmActionOffers Offers(WorldSnapshot state, EntityId player) =>
        new(ConstructionUpgradeFixture.Delivery(state, player), Speaker(state), Wood(state), null);

    private static WorldSnapshot Act(WorldSnapshot state, ConstructionUpgradeFixture actions, WorldActionIntent intent)
    {
        EntityId player = Player(state);
        TrustedCommandDelivery delivery = ConstructionUpgradeFixture.Delivery(state, player);
        var command = new WorldActionCommand(actions.NextIdentity(), delivery.PrincipalId, delivery.SessionId, player,
            state.WorldId, state.BranchId, state.Space.GetActor(player).Revision, intent);
        WorldStepDecision decision = new WorldStepProcessor().Decide(state, actions.Batch(state, [command], []));
        Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
        return decision.Candidate;
    }

    private sealed class MiningFixture
    {
        internal ExcavationWorldFixture Source { get; } = new();
        internal EntityId Player => Source.Player;
        internal WorldSnapshot Genesis { get; }
        internal FarmTarget Target => new(Source.Site.SiteId, "Selected substrate face", FarmTargetKind.Excavation, Source.Site.Stance);

        internal MiningFixture() => Genesis = WorldConstructionPhysicalAdmission.CreateGenesis(
            WorldSteppedSupportAdmission.CreateGenesis(WorldConstructedSupportAdmission.CreateGenesis(Source.Genesis)));

        internal LocalWorldView View(WorldSnapshot state) => new(state, Player);
        internal IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> ReadOffers(WorldSnapshot state) =>
            Offers(state, Player).Create(View(state), Target);

        internal WorldSnapshot Apply(WorldSnapshot state, FarmActionOffer offer)
        {
            WorldStepDecision decision = Source.Step(state, offer.ToCommand(Source.Actions.NextIdentity()));
            Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
            return decision.Candidate;
        }

        internal WorldSnapshot Move(WorldSnapshot state, long dx, long dy) => new WorldStepProcessor().Decide(state,
            Source.Actions.Batch(state, [], [new(ConstructionUpgradeFixture.Delivery(state, Player), Player, dx, dy)])).Candidate;
    }
}
