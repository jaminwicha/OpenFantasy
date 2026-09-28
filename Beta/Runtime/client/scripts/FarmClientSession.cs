using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Client;

/// <summary>Local authentication and content selection at startup; gameplay UI receives projections only.</summary>
internal sealed class FarmClientSession : IAsyncDisposable
{
    private readonly TrustedCommandDelivery _delivery;
    private readonly FarmActionOffers _offers;
    private readonly ConditionalWeakTable<FarmActionOffer, OfferedAction> _issuedOffers = new();

    private sealed class OfferedAction
    {
        internal Task<WorldActionReceipt>? Submission { get; set; }
    }

    internal FarmClientSession(LocalWorldSession world, TrustedCommandDelivery delivery,
        EntityId wrenActorId, EntityId woodDefinitionId, EntityId? berryDefinitionId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(delivery);
        World = world;
        _delivery = delivery;
        WrenActorId = wrenActorId;
        WoodDefinitionId = woodDefinitionId;
        BerryDefinitionId = berryDefinitionId;
        _offers = new FarmActionOffers(delivery, wrenActorId, woodDefinitionId, berryDefinitionId);
    }

    internal LocalWorldSession World { get; }
    internal EntityId WrenActorId { get; }
    internal EntityId WoodDefinitionId { get; }
    internal EntityId? BerryDefinitionId { get; }

    internal static Task<FarmClientSession> OpenAsync(string savePath, string seedText, string playerName,
        bool gatheringProfile, bool productionProfile = false, string? upgradeSourcePath = null,
        bool questProfile = false, string? gatheringUpgradeSourcePath = null,
        bool constructionProfile = false, string? productionUpgradeSourcePath = null) => Task.Run(() =>
    {
        WorldSnapshot genesis = gatheringProfile ? GatheringFarmAdmission.CreateEmbedded(seedText, playerName)
            : FarmWorldAdmission.CreateEmbedded(seedText, playerName);
        SqliteWorldAuthority authority = FarmSaveOpener.Open(savePath, genesis, productionProfile, upgradeSourcePath,
            questProfile, gatheringUpgradeSourcePath, constructionProfile, productionUpgradeSourcePath);
        return Attach(authority);
    });

    /// <summary>An explicit fresh-content entry; it never upgrades or opens a different farm profile.</summary>
    internal static Task<FarmClientSession> OpenWorksiteAsync(string savePath, string seedText, string playerName) => Task.Run(() =>
    {
        WorldSnapshot genesis = WorksiteFarmAdmission.CreateEmbedded(seedText, playerName);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(savePath))!);
        SqliteWorldAuthority authority = File.Exists(savePath)
            ? SqliteWorldAuthority.OpenExisting(savePath) : SqliteWorldAuthority.CreateNew(savePath, genesis);
        try
        {
            FarmSaveOpener.ValidateSelection(authority.InspectForHost().State, genesis);
        }
        catch
        {
            authority.Dispose();
            throw;
        }
        return Attach(authority);
    });

    /// <summary>Opens the finite authored watercourse without migrating an existing farm selection.</summary>
    internal static Task<FarmClientSession> OpenWatercourseAsync(string savePath, string seedText, string playerName) => Task.Run(() =>
    {
        WorldSnapshot genesis = WatercourseFarmAdmission.CreateEmbedded(seedText, playerName);
        return OpenSelection(savePath, genesis);
    });

    /// <summary>Opens the earned terrain breach as its own explicitly selected world.</summary>
    internal static Task<FarmClientSession> OpenBreachAsync(string savePath, string seedText, string playerName) => Task.Run(() =>
        OpenSelection(savePath, BreachFarmAdmission.CreateEmbedded(seedText, playerName)));

    /// <summary>Opens the source-backed encounter journey as a separate explicit save selection.</summary>
    internal static Task<FarmClientSession> OpenEncounterAsync(string savePath, string seedText, string playerName) => Task.Run(() =>
        OpenSelection(savePath, EncounterJourneyAdmission.CreateEmbedded(seedText, playerName)));

    /// <summary>Opens the care-enabled journey explicitly, without upgrading an earlier encounter save.</summary>
    internal static Task<FarmClientSession> OpenCareAsync(string savePath, string seedText, string playerName) => Task.Run(() =>
        OpenSelection(savePath, CareJourneyAdmission.CreateEmbedded(seedText, playerName)));

    internal static Task<FarmClientSession> OpenRestorativeAsync(string savePath, string seedText, string playerName) => Task.Run(() =>
        OpenSelection(savePath, RestorativeJourneyAdmission.CreateEmbedded(seedText, playerName)));

    private static FarmClientSession OpenSelection(string savePath, WorldSnapshot genesis)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(savePath))!);
        SqliteWorldAuthority authority = File.Exists(savePath)
            ? SqliteWorldAuthority.OpenExisting(savePath) : SqliteWorldAuthority.CreateNew(savePath, genesis);
        try
        {
            FarmSaveOpener.ValidateSelection(authority.InspectForHost().State, genesis);
        }
        catch
        {
            authority.Dispose();
            throw;
        }
        return Attach(authority);
    }

    private static FarmClientSession Attach(SqliteWorldAuthority authority)
    {
        try
        {
            WorldSnapshot current = authority.InspectForHost().State;
            WorldResident player = current.Residents.Single(person => person.ControllerKind == ResidentControllerKind.Player);
            EntityId? questGiver = current.Quests?.Definitions.Select(quest => (EntityId?)quest.GiverActorId).Distinct().SingleOrDefault();
            WorldResident wren = questGiver is { } giver
                ? current.GetResident(giver)
                : current.Residents.Single(person => person.ControllerKind == ResidentControllerKind.Host);
            ActorControlBinding control = current.Inventory.Controls.Single(value => value.ActorId == player.ActorId);
            var delivery = new TrustedCommandDelivery(control.PrincipalId, control.SessionId);
            EntityId wood = current.Gathering?.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Oak)
                .Definition.YieldDefinition.DefinitionId ?? current.Inventory.Sources.Single().Definition.DefinitionId;
            EntityId? berries = current.Gathering?.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Berry)
                .Definition.YieldDefinition.DefinitionId;
            var world = new LocalWorldSession(authority, player.ActorId, delivery);
            return new FarmClientSession(world, delivery, wren.ActorId, wood, berries);
        }
        catch
        {
            authority.Dispose();
            throw;
        }
    }

    /// <summary>Read-only: selecting targets, lots and quest operations from a snapshot changes nothing.</summary>
    internal IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> CreateOffers(LocalWorldView view, FarmTarget? selectedTarget = null)
    {
        IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> offers = _offers.Create(view, selectedTarget);
        foreach (FarmActionOffer offer in offers.Values)
        {
            _issuedOffers.Add(offer, new OfferedAction());
        }
        return offers;
    }

    /// <summary>
    /// Sends the offer's captured intent unchanged. Blocked, foreign-scope and closed-conversation
    /// offers are refused here before any command exists; stale revisions go to the host's ordinary rejection.
    /// </summary>
    internal Task<WorldActionReceipt> SubmitOffer(FarmActionOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        if (!offer.CanExecute)
        {
            throw new InvalidOperationException(offer.BlockingReason);
        }
        // Control identities persist in saves; this weak registration binds the presentation to
        // this particular client lifetime without retaining every discarded per-frame preview.
        if (!_issuedOffers.TryGetValue(offer, out OfferedAction? issued))
        {
            throw new InvalidOperationException("That action belongs to another client session.");
        }
        lock (issued)
        {
            // Retrying the same presentation returns the same command outcome, even after its
            // first effect changed the world. A fresh choice requires a newly presented offer.
            if (issued.Submission is not null) return issued.Submission;
            LocalWorldView current = World.ReadView();
            if (!offer.Scope.BelongsTo(_delivery, current))
            {
                throw new InvalidOperationException("That action belongs to another session, world or branch.");
            }
            if (offer.DialogueId is { } conversation && current.Dialogue?.DialogueId != conversation)
            {
                throw new InvalidOperationException("That conversation has ended; the action no longer applies.");
            }
            issued.Submission = World.SubmitAction(offer.ToCommand(NewCommandId()));
            return issued.Submission;
        }
    }

    internal Task<WorldActionReceipt> Speak(LocalWorldView view) => SubmitOffer(view, FarmSemanticAction.Speak);
    internal Task<WorldActionReceipt> GiveWood(LocalWorldView view) => SubmitOffer(view, FarmSemanticAction.Give);
    internal Task<WorldActionReceipt> Quest(LocalWorldView view) => SubmitOffer(view, FarmSemanticAction.Give);
    internal Task<WorldActionReceipt> CloseDialogue(LocalWorldView view) => SubmitOffer(view, FarmSemanticAction.CloseDialogue);
    internal Task<WorldActionReceipt> CraftRations(LocalWorldView view) => SubmitOffer(view, FarmSemanticAction.Craft);
    internal Task<WorldActionReceipt> Gather(LocalWorldView view) => SubmitOffer(view, FarmSemanticAction.Gather);

    internal ulong WoodQuantity(LocalWorldView view) => view.PlayerInventory.Lots
        .Where(lot => lot.DefinitionId == WoodDefinitionId).Aggregate(0UL, (sum, lot) => checked(sum + lot.Quantity));

    internal ulong BerryQuantity(LocalWorldView view) => view.PlayerInventory.Lots
        .Where(lot => lot.DefinitionId == BerryDefinitionId).Aggregate(0UL, (sum, lot) => checked(sum + lot.Quantity));

    internal static LocalGatherNodeView? NearestGatherNode(LocalWorldView view)
    {
        var feet = view.Actors.Single(actor => actor.ActorId == view.PlayerActorId).Feet;
        return view.GatheringNodes.Where(node => node.RemainingSwings > 0)
            .OrderBy(node => DistanceSquared(node, feet.X, feet.Y)).ThenBy(node => node.NodeId).FirstOrDefault();
    }

    internal static bool CanReachGatherNode(LocalWorldView view, LocalGatherNodeView node)
    {
        var feet = view.Actors.Single(actor => actor.ActorId == view.PlayerActorId).Feet;
        return feet.Z == node.Feet.Z && DistanceSquared(node, feet.X, feet.Y) <= 448L * 448;
    }

    private static Int128 DistanceSquared(LocalGatherNodeView node, long x, long y)
    {
        Int128 dx = (Int128)node.Feet.X - x;
        Int128 dy = (Int128)node.Feet.Y - y;
        return dx * dx + dy * dy;
    }

    /// <summary>Convenience wrappers read the same offers the HUD would present for that view.</summary>
    private Task<WorldActionReceipt> SubmitOffer(LocalWorldView view, FarmSemanticAction action)
    {
        if (!CreateOffers(view).TryGetValue(action, out FarmActionOffer? offer))
        {
            throw new InvalidOperationException("This world does not admit that action.");
        }
        return SubmitOffer(offer);
    }

    private static EntityId NewCommandId() => EntityId.FromBytes(RandomNumberGenerator.GetBytes(EntityId.ByteLength));

    public ValueTask DisposeAsync() => World.DisposeAsync();
}
