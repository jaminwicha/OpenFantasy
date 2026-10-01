using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Content.Trading;

/// <summary>
/// The production native market world in a fresh save, driven through an actual local session with the production
/// trading configuration and Wren's gift preference. The player's actions use the same client offers as the native
/// HUD and stall panel; setup never injects stock, money or quotes.
/// </summary>
internal sealed class NativeMarketTestWorld : IAsyncDisposable
{
    internal const string SeedText = "encounter-lifecycle-world";
    internal const string PlayerName = "Traveler";
    private static readonly Lazy<WorldSnapshot> SharedGenesis = new(() => NativeTradingWorld.CreateEmbedded(SeedText, PlayerName));
    private readonly StorageFiles _files = new();
    private bool _stopped;

    internal NativeMarketTestWorld(bool ready = true)
    {
        Authority = SqliteWorldAuthority.CreateNew(_files.Path, Genesis);
        WorldSnapshot state = Authority.ReadStateForHost();
        Player = state.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        Stall = NativeTradingWorld.FindStall(state)!;
        Wren = Stall.MerchantActorId;
        PlayerDelivery = DeliveryOf(state, Player);
        Session = new LocalWorldSession(Authority, Player, PlayerDelivery, Time,
            [FarmClientSession.GiftPolicy(state, Wren, Player)], NativeTradingPolicy.Configuration(state));
        EntityId wood = state.Gathering!.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Oak).Definition.YieldDefinition.DefinitionId;
        EntityId berries = Stall.Commodity.DefinitionId;
        Client = new FarmClientSession(Session, PlayerDelivery, Wren, wood, berries);
        if (ready) Session.SetReady(true);
    }

    internal static WorldSnapshot Genesis => SharedGenesis.Value;
    internal string SavePath => _files.Path;
    internal SqliteWorldAuthority Authority { get; }
    internal ManualSessionTimeProvider Time { get; } = new();
    internal LocalWorldSession Session { get; }
    internal FarmClientSession Client { get; }
    internal TrustedCommandDelivery PlayerDelivery { get; }
    internal EntityId Player { get; }
    internal EntityId Wren { get; }
    internal MarketBinding Stall { get; }
    internal WorldSnapshot State => Authority.ReadStateForHost();
    internal LocalWorldView View => Session.ReadView();
    internal LocalMarketStallView StallView => View.Markets!.Stalls.Single();

    internal static TrustedCommandDelivery DeliveryOf(WorldSnapshot state, EntityId actor)
    {
        ActorControlBinding control = state.Inventory.Controls.Single(value => value.ActorId == actor);
        return new TrustedCommandDelivery(control.PrincipalId, control.SessionId);
    }

    internal ulong Balance(EntityId actor) => State.Progression!.Currency.GetActorAccount(actor).Balance;

    internal ulong Berries(EntityId actor)
    {
        WorldSnapshot state = State;
        EntityId bag = state.GetResident(actor).InventoryCustodianId;
        return state.Inventory.Custody.Lots.Where(lot => lot.CustodianId == bag && lot.Definition == Stall.Commodity)
            .Aggregate(0UL, (sum, lot) => sum + lot.Quantity);
    }

    internal Task Tick() => SessionFixture.Tick(Time);

    /// <summary>Asks through the session exactly as the stall panel does, then lets one cutoff answer.</summary>
    internal async Task<LocalTradeInquiryView> Ask(LocalTradeDirection direction, ulong units, EntityId? shownLot = null)
    {
        var inquiry = new LocalTradeInquiry(Stall.MarketId, Stall.Commodity, direction, units, shownLot);
        Assert.Equal(LocalTradeInquiryState.Waiting, (await Client.AskAsync(inquiry)).State);
        await Tick();
        return Session.ReadTradeInquiry()!;
    }

    /// <summary>Submits one presented client offer and completes it at the next cutoff.</summary>
    internal async Task<WorldActionReceipt> Submit(FarmActionOffer offer)
    {
        Task<WorldActionReceipt> pending = Client.SubmitOffer(offer);
        await Tick();
        await SessionFixture.Until(() => pending.IsCompleted);
        return await pending;
    }

    internal async Task<WorldActionReceipt> Submit(WorldActionCommand command)
    {
        Task<WorldActionReceipt> pending = Session.SubmitAction(command);
        await Tick();
        await SessionFixture.Until(() => pending.IsCompleted);
        return await pending;
    }

    internal IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> Offers(FarmMarketDraft? draft = null) =>
        Client.CreateOffers(View, null, null, draft);

    /// <summary>Talk, accept, deliver and close: Wren's ordinary quest pays its reward from her funded reserve.</summary>
    internal async Task EarnReward()
    {
        foreach (FarmSemanticAction action in new[] { FarmSemanticAction.Speak, FarmSemanticAction.Give,
            FarmSemanticAction.Give, FarmSemanticAction.CloseDialogue })
        {
            WorldActionReceipt receipt = await Submit(Offers()[action]);
            Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        }
        Assert.False(View.HasReadingHold);
    }

    /// <summary>
    /// Wren follows her funded construction commitment through ordinary session cutoffs, builds and walks home.
    /// Every step is selected by the session's own NPC controllers; nothing is teleported.
    /// </summary>
    internal async Task WaitForWrenAtHome()
    {
        WorldResident wren = State.GetResident(Wren);
        for (int step = 0; ; step++)
        {
            WorldSnapshot state = State;
            if (state.Construction!.Projects.All(project => project.IsCompleted) && state.Space.GetActor(Wren).Feet == wren.HomeFeet)
                return;
            Assert.True(step < 4_000, "Wren must finish her funded work and return home within bounded cutoffs.");
            await Tick();
        }
    }

    internal WorldActionCommand PlayerCommand(WorldActionIntent intent, EntityId commandId) => new(commandId,
        PlayerDelivery.PrincipalId, PlayerDelivery.SessionId, Player, State.WorldId, State.BranchId,
        State.Space.GetActor(Player).Revision, intent);

    internal static EntityId NewId() => EntityId.FromBytes(System.Security.Cryptography.RandomNumberGenerator.GetBytes(EntityId.ByteLength));

    internal async Task StopAsync()
    {
        if (_stopped) return;
        _stopped = true;
        await Session.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _files.Dispose();
    }
}
