using System.Security.Cryptography;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
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
        EntityId wrenActorId, EntityId woodDefinitionId)
    {
        World = world;
        _delivery = delivery;
        WrenActorId = wrenActorId;
        WoodDefinitionId = woodDefinitionId;
    }

    internal LocalWorldSession World { get; }
    internal EntityId WrenActorId { get; }
    internal EntityId WoodDefinitionId { get; }

    internal static Task<FarmClientSession> OpenAsync(string savePath, string seedText, string playerName) => Task.Run(() =>
    {
        WorldSnapshot genesis = FarmWorldAdmission.CreateEmbedded(seedText, playerName);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(savePath))!);
        SqliteWorldAuthority authority = File.Exists(savePath)
            ? SqliteWorldAuthority.OpenExisting(savePath) : SqliteWorldAuthority.CreateNew(savePath, genesis);
        try
        {
            WorldSnapshot current = authority.InspectForHost().State;
            if (current.WorldId != genesis.WorldId || current.AdmissionVersion != genesis.AdmissionVersion)
            {
                throw new InvalidDataException("This save needs its original content version or an explicit migration.");
            }
            WorldResident player = current.Residents.Single(person => person.ControllerKind == ResidentControllerKind.Player);
            WorldResident wren = current.Residents.Single(person => person.ControllerKind == ResidentControllerKind.Host);
            ActorControlBinding control = current.Inventory.Controls.Single(value => value.ActorId == player.ActorId);
            var delivery = new TrustedCommandDelivery(control.PrincipalId, control.SessionId);
            var world = new LocalWorldSession(authority, player.ActorId, delivery);
            return new FarmClientSession(world, delivery, wren.ActorId, genesis.Inventory.Sources.Single().Definition.DefinitionId);
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
        var lot = view.PlayerInventory.Lots.FirstOrDefault(value => value.DefinitionId == WoodDefinitionId && value.Quantity >= 6)
            ?? throw new InvalidOperationException("You need six wood together to make this handover.");
        return World.SubmitAction(Command(view, new GiveItemIntent(wren.ActorId, wren.PresenceRevision,
            lot.LotId, lot.Revision, view.PlayerInventory.CustodianId, view.PlayerInventory.Revision, 6)));
    }

    internal Task<WorldActionReceipt> CloseDialogue(LocalWorldView view) => World.SubmitAction(Command(view,
        new CloseDialogueIntent(view.Dialogue?.DialogueId ?? throw new InvalidOperationException("There is no addressed conversation to close."))));

    internal ulong WoodQuantity(LocalWorldView view) => view.PlayerInventory.Lots
        .Where(lot => lot.DefinitionId == WoodDefinitionId).Aggregate(0UL, (sum, lot) => checked(sum + lot.Quantity));

    private WorldActionCommand Command(LocalWorldView view, WorldActionIntent intent) => new(
        EntityId.FromBytes(RandomNumberGenerator.GetBytes(EntityId.ByteLength)), _delivery.PrincipalId, _delivery.SessionId,
        view.PlayerActorId, view.WorldId, view.BranchId,
        view.Actors.Single(actor => actor.ActorId == view.PlayerActorId).PresenceRevision, intent);

    public ValueTask DisposeAsync() => World.DisposeAsync();
}
