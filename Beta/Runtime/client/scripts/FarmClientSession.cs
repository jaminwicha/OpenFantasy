using System.Security.Cryptography;
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

    private FarmClientSession(LocalWorldSession world, TrustedCommandDelivery delivery,
        EntityId wrenActorId, EntityId woodDefinitionId, EntityId? berryDefinitionId)
    {
        World = world;
        _delivery = delivery;
        WrenActorId = wrenActorId;
        WoodDefinitionId = woodDefinitionId;
        BerryDefinitionId = berryDefinitionId;
    }

    internal LocalWorldSession World { get; }
    internal EntityId WrenActorId { get; }
    internal EntityId WoodDefinitionId { get; }
    internal EntityId? BerryDefinitionId { get; }

    internal static Task<FarmClientSession> OpenAsync(string savePath, string seedText, string playerName,
        bool gatheringProfile, bool productionProfile = false, string? upgradeSourcePath = null,
        bool questProfile = false, string? gatheringUpgradeSourcePath = null) => Task.Run(() =>
    {
        WorldSnapshot genesis = gatheringProfile ? GatheringFarmAdmission.CreateEmbedded(seedText, playerName)
            : FarmWorldAdmission.CreateEmbedded(seedText, playerName);
        SqliteWorldAuthority authority = FarmSaveOpener.Open(savePath, genesis, productionProfile, upgradeSourcePath,
            questProfile, gatheringUpgradeSourcePath);
        try
        {
            WorldSnapshot current = authority.InspectForHost().State;
            WorldResident player = current.Residents.Single(person => person.ControllerKind == ResidentControllerKind.Player);
            WorldResident wren = current.Residents.Single(person => person.ControllerKind == ResidentControllerKind.Host);
            ActorControlBinding control = current.Inventory.Controls.Single(value => value.ActorId == player.ActorId);
            var delivery = new TrustedCommandDelivery(control.PrincipalId, control.SessionId);
            EntityId wood = current.Gathering?.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Oak)
                .Definition.YieldDefinition.DefinitionId ?? genesis.Inventory.Sources.Single().Definition.DefinitionId;
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
    });

    internal Task<WorldActionReceipt> Speak(LocalWorldView view)
    {
        var wren = view.Actors.Single(actor => actor.ActorId == WrenActorId);
        return World.SubmitAction(Command(view, new SpeakToActorIntent(wren.ActorId, wren.PresenceRevision)));
    }

    internal Task<WorldActionReceipt> GiveWood(LocalWorldView view)
    {
        var wren = view.Actors.Single(actor => actor.ActorId == WrenActorId);
        var lot = view.PlayerInventory.Lots.Where(value => value.DefinitionId == WoodDefinitionId)
            .OrderByDescending(value => value.Quantity).ThenBy(value => value.LotId).FirstOrDefault()
            ?? throw new InvalidOperationException("You have no wood in your pack.");
        return World.SubmitAction(Command(view, new GiveItemIntent(wren.ActorId, wren.PresenceRevision,
            lot.LotId, lot.Revision, view.PlayerInventory.CustodianId, view.PlayerInventory.Revision, Math.Min(6UL, lot.Quantity))));
    }

    internal Task<WorldActionReceipt> CloseDialogue(LocalWorldView view) => World.SubmitAction(Command(view,
        new CloseDialogueIntent(view.Dialogue?.DialogueId ?? throw new InvalidOperationException("There is no addressed conversation to close."))));

    internal Task<WorldActionReceipt> CraftRations(LocalWorldView view) =>
        World.SubmitAction(Command(view, new CraftIntent(FarmCrafting.RationsRequest(view))));

    internal Task<WorldActionReceipt> Quest(LocalWorldView view) =>
        World.SubmitAction(Command(view, FarmQuestActions.Request(view)));

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

    internal Task<WorldActionReceipt> Gather(LocalWorldView view)
    {
        var node = NearestGatherNode(view) ?? throw new InvalidOperationException("These plants need time to recover.");
        var tool = view.GatheringTools.FirstOrDefault(value => value.Kind == node.RequiredTool)
            ?? throw new InvalidOperationException("You need the right tool in your pack.");
        return World.SubmitAction(Command(view, new GatherIntent(node.NodeId, node.Revision, tool.LotId, tool.Revision)));
    }

    private WorldActionCommand Command(LocalWorldView view, WorldActionIntent intent) => new(
        EntityId.FromBytes(RandomNumberGenerator.GetBytes(EntityId.ByteLength)), _delivery.PrincipalId, _delivery.SessionId,
        view.PlayerActorId, view.WorldId, view.BranchId,
        view.Actors.Single(actor => actor.ActorId == view.PlayerActorId).PresenceRevision, intent);

    public ValueTask DisposeAsync() => World.DisposeAsync();
}
