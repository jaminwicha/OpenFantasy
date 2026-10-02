using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Networking.PlayerObservations;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed partial class ConsentedWorldPublicationTests
{
    [Fact]
    public void PlayerObservation_preserves_every_lot_and_proposal_term_at_the_admitted_world_lot_bound()
    {
        var fixture = new ConsentedPotionFixture(rawLotTarget: CustodySnapshot.MaximumLots);
        WorldSnapshot world = WorldMaterialProposalAdmission.CreateFresh(fixture.Genesis);
        Assert.Equal(4096, world.Inventory.Custody.Lots.Count);
        Assert.Equal(world.ContentHash, WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(world)).ContentHash);
        var view = new LocalWorldView(world, fixture.Journey.Helper);
        Assert.NotNull(view.MaterialProposals?.Preparation);
        Assert.True(view.MaterialProposals.Preparation.Lots.Count > 3000);
        byte[] bytes = PlayerViewCodec.Encode(view);
        LocalWorldView decoded = PlayerViewCodec.Decode(bytes);
        Assert.Equal(view.PlayerInventory.Lots, decoded.PlayerInventory.Lots);
        Assert.Equal(view.MaterialProposals.Preparation.Lots, decoded.MaterialProposals!.Preparation!.Lots);
        TestContext.Current.TestOutputHelper!.WriteLine("MaximumLotObservationBytes=" + bytes.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlayerObservation_delivers_the_complete_admitted_maximum_inventory_to_its_authenticated_player(bool sockets)
    {
        var source = new ConsentedPotionFixture(rawLotTarget: CustodySnapshot.MaximumLots);
        WorldSnapshot basis = source.Genesis;
        EntityId actor = source.Journey.Helper;
        Assert.Equal(0UL, basis.Revision);
        Assert.Empty(basis.ResourceHistory!.History.Batches);
        Assert.Null(basis.ResourceHistory.ArchiveAnchor);
        Assert.Null(basis.ResourceHistory.CoordinatedAnchor);
        ContentHash admission = ContentHash.Compute(basis.ContentHash.ToByteArray().Concat(actor.ToByteArray()).ToArray());
        // Fresh authoring changes who drives the already-controlled helper. It does not alter
        // lived state, mint stock, bypass saved admission, or loosen any inventory bound.
        WorldResident[] residents = basis.Residents.Select(value => value.ActorId == actor
            ? new WorldResident(value.ActorId, value.InventoryCustodianId, value.DisplayName,
                ResidentControllerKind.Player, value.HomeFeet, value.WanderRadiusCells, value.Comprehension,
                value.ExperiencedTime, value.OpeningUtterance) : value).ToArray();
        var authored = new WorldSnapshot(basis.Inventory, basis.Space, residents, admission,
            basis.Calendar, basis.Revision, basis.AdvancingSteps, basis.Dialogues, basis.Gathering,
            basis.Production, basis.Progression, basis.Quests, basis.Construction, basis.Watershed,
            basis.Reservoirs is null ? basis.WatershedNetwork : null,
            basis.Transport is null ? basis.Reservoirs : null, basis.Transport, basis.Excavation,
            basis.ConstructionPhysical, basis.StructuralPolicy, basis.StructuralSettlementPolicy,
            basis.ConsequencePolicy, basis.PendingExcavation, basis.CoupledSettlementPolicy, basis.DescentMode,
            basis.ReservoirSeals, basis.LiquidCells, basis.Pacing, basis.EncounterActions,
            basis.EncounterLifecycle, basis.Care, new WorldResourceHistory(admission, basis.ResourceHistory.History, []),
            basis.Coordination, basis.Retreat, basis.EncounterOutcome);
        WorldSnapshot world = WorldMaterialProposalAdmission.CreateFresh(authored);
        Assert.Equal(4096, world.Inventory.Custody.Lots.Count);
        Assert.Equal(world.ContentHash, WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(world)).ContentHash);
        await using var fixture = await ObservationSessionFixture.Open(world, sockets: sockets, actor: actor);
        await fixture.Subscribe(1);
        await fixture.Ping(2);
        fixture.NetworkTime.Advance(PlayerCommandSession.ObservationInterval);
        PlayerObservationFrame frame = await fixture.ReceiveObservation();
        Assert.True(frame.Replacement.Length <= PlayerViewCodec.MaximumBodyBytes);
        var replica = new PlayerObservationReplica(world.WorldId, world.BranchId, actor);
        Assert.Equal(PlayerObservationApplyResult.Applied, replica.Apply(frame));
        LocalWorldView expected = fixture.Channel.Player.ReadView();
        LocalWorldView actual = replica.ReadView()!;
        Assert.True(actual.PlayerInventory.Lots.Count > 3000);
        Assert.Equal(expected.PlayerInventory.Lots, actual.PlayerInventory.Lots);
        Assert.Equal(expected.MaterialProposals!.Preparation!.Lots, actual.MaterialProposals!.Preparation!.Lots);
        Assert.Equal(0UL, fixture.Authority.ReadStateForHost().Revision);
        await fixture.Send(new PlayerMessage(PlayerMessageKind.Goodbye, 3, []));
        await fixture.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }
}
