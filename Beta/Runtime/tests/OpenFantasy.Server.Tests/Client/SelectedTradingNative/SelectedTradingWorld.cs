using System.Net;
using System.Security.Cryptography.X509Certificates;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests.SelectedTradingNative;

/// <summary>Original four-human cargo admission, with earned quest funding and journaled ordinary travel.</summary>
internal sealed class SelectedTradingWorld : IAsyncDisposable
{
    private readonly ServerFiles _files = new();
    private readonly List<string> _preparation = [];
    private ulong _identity = 1;
    private bool _stopped;
    internal string Database => Path.Combine(_files.DirectoryPath, "selected-trading.sqlite3");
    internal X509Certificate2 Certificate { get; } = ServerWorld.CreateCertificate();
    internal WorldSnapshot Genesis { get; }
    internal WorldSnapshot Prepared { get; private set; }
    internal EntityId[] Actors { get; }
    internal EntityId Owner => Actors[0];
    internal EntityId Grantee => Actors[1];
    internal CargoCircuitMarkets Markets { get; }
    internal CargoPlayerHost Host { get; private set; } = null!;
    internal PlayerWebSocketServer Server { get; private set; } = null!;
    internal string Description => string.Join("\n", _preparation);

    private SelectedTradingWorld()
    {
        string[] names = ["Amber", "Birch", "Cedar", "Dawn"];
        var controllers = WorldControllerAdmission.CreateFresh(CargoOutcomeWorldAdmission.CreateEmbedded(
            CargoOutcomeWorldAdmission.SeedText, names));
        var remedy = controllers.Care!.Ordinary!.Definitions.First(value => value.DeliveryMode == RestorativeDeliveryMode.LivingAssistance);
        var recovery = RestorativeUseDefinition.CreateIncapacitatedAssistance(new(0xcc74726400000000, 1),
            remedy.RemedyDefinitionId, remedy.RemedyVersion, remedy.SupportedForms);
        Genesis = WorldDelegatedControlAdmission.CreateFresh(WorldControlPermissionAdmission.CreateFresh(
            WorldCareDispositionAdmission.CreateFresh(WorldRecoveryAdmission.CreateFresh(controllers, [recovery], []))));
        Prepared = Genesis;
        Actors = names.Select(name => Genesis.Residents.Single(actor => actor.DisplayName == name).ActorId).ToArray();
        Markets = CargoWorldAdmission.Find(Genesis, CargoCircuitSources.LoadEmbedded())!;
        Assert.Equal(4, Actors.Length);
    }

    internal static async Task<SelectedTradingWorld> Open(string output)
    {
        var world = new SelectedTradingWorld();
        File.WriteAllBytes(Path.Combine(output, "genesis.ofc"), WorldSnapshotCodec.Encode(world.Genesis));
        try
        {
            using (var authority = SqliteWorldAuthority.CreateNew(world.Database, world.Genesis))
            {
                world.Fund(authority);
                var state = authority.ReadStateForHost();
                CellCoordinate standing = state.Space.GetActor(world.Owner).Feet.Cell;
                var corridor = CargoCorridor.CreateCircuit(state.Space.Regions.Single(region => region.Bounds.Contains(standing)).Bounds,
                    CargoCircuitSources.LoadEmbedded());
                var farmStart = new CellCoordinate(standing.X, standing.Y, standing.Z - 1);
                foreach (var cell in CargoCorridor.Lane([farmStart,
                    new CellCoordinate(farmStart.X, corridor.OriginApproach.Y, farmStart.Z), corridor.OriginApproach]))
                    world.Walk(authority, CargoCorridor.FeetAt(cell));
                world.Prepared = authority.ReadStateForHost();
                Assert.NotNull(WorldMarketLocality.FindAvailable(world.Prepared, world.Owner, world.Markets.Origin.MarketId));
                Assert.Equal(world.Genesis.Production!.ContentHash, world.Prepared.Production!.ContentHash);
                Assert.Equal(0UL, world.Genesis.Progression!.Currency.GetActorAccount(world.Owner).Balance);
                Assert.Equal(world.Genesis.Quests!.Definitions.Single().Reward.CurrencyAmount,
                    world.Prepared.Progression!.Currency.GetActorAccount(world.Owner).Balance);
                Assert.Empty(world.Prepared.Coordination!.PaidExchange!.Open);
                Assert.Empty(world.Prepared.Coordination.PaidExchange.Outcomes);
                Assert.True(WorldActorAvailability.CanAct(world.Prepared, world.Owner));
                File.WriteAllBytes(Path.Combine(output, "prepared.ofc"), WorldSnapshotCodec.Encode(world.Prepared));
                File.WriteAllText(Path.Combine(output, "preparation.txt"), world.Description);
            }
            world.Host = await CargoPlayerHost.OpenExistingAsync(world.Database, ServerWorld.Cancellation);
            world.Server = await PlayerWebSocketServer.StartAsync(world.Host.Admission,
                new(IPAddress.Loopback, 0, world.Certificate), ServerWorld.Cancellation);
            return world;
        }
        catch { await world.DisposeAsync(); throw; }
    }

    private void Fund(SqliteWorldAuthority authority)
    {
        var state = authority.ReadStateForHost();
        var quest = Assert.Single(state.Quests!.Definitions);
        EntityId giver = quest.GiverActorId;
        state = Act(authority, new SpeakToActorIntent(giver, state.Space.GetActor(giver).Revision), WorldBatchKind.AdvancingStep);
        state = Act(authority, new AcceptDeliveryQuestIntent(quest.DefinitionId, quest.Version, giver,
            state.Space.GetActor(giver).Revision, 0, state.Progression!.GetActor(Owner).Revision), WorldBatchKind.HeldInteraction);
        EntityId bag = state.GetResident(Owner).InventoryCustodianId;
        var lot = state.Inventory.Custody.Lots.Single(value => value.CustodianId == bag && value.Definition == quest.Item);
        state = Act(authority, new CompleteDeliveryQuestIntent(quest.DefinitionId, quest.Version, giver,
            state.Space.GetActor(giver).Revision, 1, state.Progression!.GetActor(Owner).Revision,
            state.Inventory.Custody.GetStorage(bag).Revision, [new(lot.LotId, lot.Revision, quest.Quantity)]), WorldBatchKind.HeldInteraction);
        state = Act(authority, new CloseDialogueIntent(state.Dialogues.Single().DialogueId), WorldBatchKind.HeldInteraction);
        for (int steps = 0; !WorldActorAvailability.CanAct(state, Owner); steps++)
        {
            Assert.True(steps < 64, "Ordinary quest debt did not retire within 64 steps.");
            state = authority.Commit(new(NextId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
                WorldBatchKind.AdvancingStep, [], [], state.ProfileVersion)).Candidate;
        }
        _preparation.Add($"Owner {Owner} spoke, accepted and delivered Wren's original funded quest, then closed dialogue in four ordinary commands; balance {state.Progression!.Currency.GetActorAccount(Owner).Balance}.");
    }

    private WorldSnapshot Act(SqliteWorldAuthority authority, WorldActionIntent intent, WorldBatchKind kind)
    {
        var state = authority.ReadStateForHost();
        var control = state.Inventory.Controls.Single(value => value.ActorId == Owner);
        var command = new WorldActionCommand(NextId(), control.PrincipalId, control.SessionId, Owner,
            state.WorldId, state.BranchId, state.Space.GetActor(Owner).Revision, intent);
        var decision = authority.Commit(new(NextId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
            kind, [], [command], state.ProfileVersion));
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(decision.Receipts).Status);
        return decision.Candidate;
    }

    private void Walk(SqliteWorldAuthority authority, SubunitPosition destination)
    {
        var state = authority.ReadStateForHost();
        var origin = state.Space.GetActor(Owner).Feet;
        var binding = state.Inventory.Controls.Single(row => row.ActorId == Owner);
        var delivery = new TrustedCommandDelivery(binding.PrincipalId, binding.SessionId);
        int steps = 0;
        while (state.Space.GetActor(Owner).Feet != destination)
        {
            Assert.True(steps++ < 512, "Ordinary cargo approach exceeded its finite leg bound.");
            var actor = state.Space.GetActor(Owner);
            var route = state.Space.QueryGroundRoute(Owner, destination);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            var next = Assert.IsType<SubunitPosition>(route.NextWaypoint);
            long dx = Math.Clamp(next.X - actor.Feet.X, -32, 32);
            long dy = dx == 0 ? Math.Clamp(next.Y - actor.Feet.Y, -32, 32) : 0;
            state = authority.Commit(new(NextId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
                WorldBatchKind.AdvancingStep, [new(delivery, Owner, dx, dy)], [], state.ProfileVersion)).Candidate;
            Assert.Equal(actor.Feet.Offset(dx, dy, 0), state.Space.GetActor(Owner).Feet);
        }
        _preparation.Add($"Owner {Owner}: {origin} → {destination}, {steps} ordinary movement commits; no native travel claim.");
    }

    private EntityId NextId() => new(0xcc74726500000000, _identity++);
    internal async Task Stop()
    {
        if (_stopped) return;
        if (Server is not null) await Server.DisposeAsync();
        if (Host is not null) await Host.DisposeAsync();
        _stopped = true;
    }
    public async ValueTask DisposeAsync()
    {
        try { await Stop(); }
        finally { Certificate.Dispose(); _files.Dispose(); }
    }
}
