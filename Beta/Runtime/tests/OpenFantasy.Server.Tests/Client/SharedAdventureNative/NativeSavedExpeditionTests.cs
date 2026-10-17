using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Host;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests;

public sealed class NativeSavedExpeditionTests
{
    public static bool Configured => NativeSharedAdventureTests.Configured;

    public static bool ArrivalConfigured => Configured
        && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENFANTASY_ARRIVED_SHARED_SAVE"));

    [Fact(SkipUnless = nameof(Configured), Skip = "Requires Godot and the exact retained shared opening.")]
    public Task Earned_two_person_save_continues_through_trade_collective_withdrawal_care_and_return() => Run(arrivalOnly: false);

    [Fact(SkipUnless = nameof(ArrivalConfigured), Skip = "Requires the exact retained arrival save and Godot.")]
    public Task Second_town_browser_explicitly_changes_stall_and_sells_the_earned_cargo() => Run(arrivalOnly: true);

    private static async Task Run(bool arrivalOnly)
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ServerWorld.Cancellation);
        int deadlineSeconds = arrivalOnly ? 180 : 900;
        bound.CancelAfter(TimeSpan.FromSeconds(deadlineSeconds));
        var watch = Stopwatch.StartNew();
        string sourceSave = Environment.GetEnvironmentVariable(arrivalOnly
            ? "OPENFANTASY_ARRIVED_SHARED_SAVE" : "OPENFANTASY_EARNED_SHARED_SAVE")
            ?? throw new InvalidOperationException("Name the retained earned shared save explicitly.");
        string output = Path.Combine(Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_PLAYER_EVIDENCE")
            ?? throw new InvalidOperationException("Retained evidence is required."), arrivalOnly ? "saved-sale" : "saved-expedition");
        Directory.CreateDirectory(output);
        byte[] originalBytes = File.ReadAllBytes(sourceSave);
        string sourceHash = arrivalOnly
            ? "f5a4c71631f9a1bd5549f6961364297c1dbadd294bc85188b40c2f9e2809b361"
            : "cf21a8be1d99dc39aa2b856e8be0857c0c7b01b210a45911dd9fbab93e2c7a21";
        Assert.Equal(sourceHash, Convert.ToHexStringLower(SHA256.HashData(originalBytes)));
        string sourceCopy = Path.Combine(output, "source.sqlite3");
        File.Copy(sourceSave, sourceCopy, overwrite: false);
        string database = Path.Combine(output, "world.sqlite3");
        if (arrivalOnly) File.Copy(sourceCopy, database, overwrite: false);
        else
        {
            string request = Path.Combine(output, "transition.bin");
            using var operatorReport = new StringWriter();
            var app = new HostApplication(operatorReport);
            Assert.Equal(0, app.Run(["--prepare-party-withdrawal", sourceCopy, request]));
            Assert.Equal(0, app.Run(["--enable-party-withdrawal", sourceCopy, database, request]));
            File.WriteAllText(Path.Combine(output, "migration.jsonl"), operatorReport.ToString());
        }
        Assert.Equal(originalBytes, File.ReadAllBytes(sourceCopy));
        WorldSnapshot genesis, basis;
        using (var authority = SqliteWorldAuthority.OpenExisting(database))
        {
            basis = authority.ReadStateForHost();
            genesis = authority.ReadRecordedStateForHost(0).State;
            if (!arrivalOnly) Assert.Equal(916UL, basis.Revision);
            Assert.Equal(41, basis.ProfileVersion);
            if (arrivalOnly) Assert.Equal(EncounterLifecyclePhase.Exploration, basis.EncounterLifecycle!.Phase);
        }
        File.WriteAllBytes(Path.Combine(output, "genesis.ofc"), WorldSnapshotCodec.Encode(genesis));
        EntityId lead = genesis.Excavation!.Sites.Single().OwnerActorId;
        EntityId helper = genesis.Residents.Single(actor => actor.ControllerKind == ResidentControllerKind.Player && actor.ActorId != lead).ActorId;
        EntityId[] humans = [lead, helper];
        EntityId chief = genesis.EncounterLifecycle!.Scenarios.Single(row => row.TargetActorId == lead
            && genesis.GetResident(row.InitiatingActorId).ControllerKind == ResidentControllerKind.Host).InitiatingActorId;
        NativeCargoStall marla = NativeCargoWorld.Stalls(genesis).Single(row => row.MerchantSourceId == CargoCircuitSources.OriginMerchantId);
        NativeCargoStall kegga = NativeCargoWorld.Stalls(genesis).Single(row => row.MerchantSourceId == CargoCircuitSources.DestinationMerchantId);
        CargoCorridor corridor = NativeCargoWorld.Corridor(genesis);
        using var certificate = ServerWorld.CreateCertificate();
        string publicCertificate = Path.Combine(output, "public.cer");
        File.WriteAllBytes(publicCertificate, certificate.Export(X509ContentType.Cert));
        using var files = new ServerFiles();
        string project = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../client"));
        string background = NativeRosterProcess.BackgroundProject(project, files.DirectoryPath);
        var peers = new Dictionary<EntityId, NativeRosterProcess>();
        var all = new List<NativeRosterProcess>();
        var stages = new List<object>();
        string stage = "opening";
        EntityId cargo = default;
        Dictionary<EntityId, ulong> supplies = [];
        try
        {
            await using (var host = await CargoPlayerHost.OpenExistingAsync(database, bound.Token))
            await using (var server = await PlayerWebSocketServer.StartAsync(host.Admission,
                new(IPAddress.Loopback, 0, certificate), bound.Token))
            {
                foreach (EntityId actor in humans)
                {
                    var invitation = new PlayerConnectionInvitation(server.Endpoint, host.Admission.Compatibility, actor,
                        host.Issue(actor, TimeSpan.FromMinutes(16)).Export());
                    string path = Path.Combine(files.DirectoryPath, actor + ".invitation.json");
                    PlayerInvitationFile.Create(path, invitation);
                    var peer = new NativeRosterProcess(Path.Combine(output, actor.ToString()), path, publicCertificate,
                        actor == lead ? background : project, actor == lead, bound.Token, expedition: true);
                    peers.Add(actor, peer);
                    all.Add(peer);
                }
                await Task.WhenAll(peers.Values.Select(peer => peer.Opened()));
                Assert.Equal(arrivalOnly ? 180UL : 200UL, peers[lead].View.Progression!.CurrencyBalance);
                Assert.Equal(0UL, peers[helper].View.Progression!.CurrencyBalance);
                Assert.All(peers.Values, peer => Assert.NotNull(peer.View.Retreat!.Withdrawal));
                supplies = peers[lead].View.Markets!.Stalls.Single(row => row.MarketId == marla.MarketId)
                    .OwnLots.Where(row => !arrivalOnly || row.Quantity == 2).ToDictionary(row => row.LotId, row => row.Quantity);
                Assert.Equal(2UL, supplies.Values.Aggregate(0UL, (sum, units) => sum + units));
                var travel = new NativeExpeditionTravel(basis, peers, lead, Observe, bound.Token);
                if (arrivalOnly)
                {
                    Assert.All(peers.Values, peer => Assert.False(peer.View.IsInEncounter));
                    cargo = peers[lead].View.Markets!.Stalls.Single(row => row.MarketId == marla.MarketId)
                        .OwnLots.Single(row => !supplies.ContainsKey(row.LotId) && row.Quantity == 1).LotId;
                    await Send(lead, "resume");
                    // UI drafts are not saved. Reproduce the old explicit Marla choice through ordinary browsing.
                    await Send(lead, "trade-browse");
                    await Send(lead, "trade-stall", target: marla.MarketId);
                    await Send(lead, "trade-close");
                    Mark("retained-arrival-with-deliberate-previous-stall");
                }
                else
                {
                    Mark("original-earned-state-and-explicit-transition");

                    stage = "buy-at-marla";
                    await ToOrigin(lead, 0);
                    await Send(lead, "resume");
                    await Send(lead, "trade-open");
                    await Send(lead, "trade-stall", target: marla.MarketId);
                    await Send(lead, "trade-ask");
                    await Send(lead, "trade-review");
                    await Send(lead, "capture-trade", "earned-purchase");
                    await Send(lead, "trade-confirm", "Applied");
                    cargo = peers[lead].View.Markets!.Stalls.Single(row => row.MarketId == marla.MarketId)
                        .OwnLots.Single(row => !supplies.ContainsKey(row.LotId)).LotId;
                    Assert.Equal(180UL, peers[lead].View.Progression!.CurrencyBalance);
                    await Send(lead, "trade-close");
                    await Send(lead, "pause");
                    Mark("earned-purchase-retaining-crafted-supplies");

                    stage = "two-person-outbound";
                    var road = new CellCoordinate(346, 318, corridor.Corner.Z);
                    await ToOrigin(helper, -1, -2);
                    await travel.Lanes((helper, [corridor.OriginApproach.Offset(-1, -2, 0),
                        corridor.Corner.Offset(-2, -2, 0), road.Offset(-2, 0, 0)]),
                        (lead, [corridor.OriginApproach, corridor.Corner, road]));
                    Mark("both-arrived-at-road-with-owned-cargo");
                    await Send(lead, "capture");

                    stage = "hostile-approach";
                    var healthy = humans.ToDictionary(actor => actor, actor => peers[actor].View.Progression!.Hp);
                    // Each positive participant must be in the chief's actual400-subunit entry range.
                    // Stage the helper north of the camp while the lead is still safely south.
                    var helperHere = NativeExpeditionTravel.Cell(travel.Feet(helper));
                    await travel.Lane(helper, helperHere, new(343, helperHere.Y, road.Z),
                        new(343, 309, road.Z), new(347, 309, road.Z), new(347, 310, road.Z));
                    for (int cell = 317; cell >= 311 && !peers[lead].View.IsInEncounter; cell--)
                    {
                        var current = travel.Feet(lead);
                        var target = current.Offset(0, CargoCorridor.FeetAt(new(346, cell, road.Z)).Y - current.Y, 0);
                        await travel.Send(lead, target, allowEncounter: true);
                    }
                    Assert.True(peers[lead].View.IsInEncounter, "The actual chief did not initiate the assembled road encounter.");
                    Assert.Contains(peers[lead].View.Encounter!.EncounterId, genesis.EncounterLifecycle.Scenarios
                        .Where(row => row.InitiatingActorId == chief)
                        .Select(row => EncounterEpisodeIdentity.Derive(basis.EncounterLifecycle!, row.DefinitionId)));
                    await new NativeExpeditionEncounter(peers, lead, Mark, value => stage = value, bound.Token)
                        .Complete(healthy);

                    stage = "detour-and-sale";
                    await travel.Lanes(new[] { helper, lead }.Select(actor => {
                        var here = NativeExpeditionTravel.Cell(travel.Feet(actor));
                        var rejoin = new CellCoordinate(actor == lead ? 346 : 344, 303, here.Z);
                        return (actor, new CellCoordinate[] { here, new(actor == lead ? 343 : 341, here.Y, here.Z),
                            new(actor == lead ? 343 : 341, 303, here.Z), rejoin,
                            corridor.DestinationApproach.Offset(actor == lead ? 0 : -2, 0, 0) });
                    }).ToArray());
                }
                stage = "second-town-sale";
                await Send(lead, "resume");
                await Send(lead, "trade-browse");
                await Send(lead, "trade-stall", target: kegga.MarketId);
                await Send(lead, "trade-direction", "Sell");
                await Send(lead, "trade-lot", target: cargo);
                await Send(lead, "trade-show");
                await Send(lead, "trade-ask");
                await Send(lead, "trade-review");
                await Send(lead, "capture-trade", "cross-town-sale");
                await Send(lead, "trade-confirm", "Applied");
                Assert.Equal(218UL, peers[lead].View.Progression!.CurrencyBalance);
                await Send(lead, "trade-close");
                Mark("carried-original-purchase-sold-at-second-town");

                Assert.DoesNotContain(peers[lead].View.PlayerInventory.Lots, row => row.LotId == cargo);
                if (!arrivalOnly)
                {
                    stage = "return-home";
                    await travel.Lanes(humans.Select(actor => {
                        var here = NativeExpeditionTravel.Cell(travel.Feet(actor));
                        long x = actor == lead ? 346 : 344;
                        long detour = actor == lead ? 343 : 341;
                        return (actor, new CellCoordinate[] { here, new(x, 303, here.Z), new(detour, 303, here.Z),
                            new(detour, 318, here.Z), new(x, 318, here.Z), corridor.Corner.Offset(x - 346, actor == lead ? 0 : -2, 0),
                            corridor.OriginApproach.Offset(actor == lead ? 0 : -1, actor == lead ? 0 : -2, 0) });
                    }).ToArray());
                    foreach (EntityId actor in humans)
                    {
                        var home = NativeExpeditionTravel.Cell(genesis.Space.GetActor(actor).Feet);
                        await travel.Lane(actor, NativeExpeditionTravel.Cell(travel.Feet(actor)),
                            new(home.X, corridor.OriginApproach.Y, home.Z), home);
                    }
                }
                Assert.All(peers.Values, peer => Assert.True(peer.View.Construction.Single().IsCompleted));
                foreach (var lot in supplies)
                    Assert.Equal(lot.Value, peers[lead].View.Markets!.Stalls.Single(row => row.MarketId == marla.MarketId)
                        .OwnLots.Single(row => row.LotId == lot.Key).Quantity);
                await Send(lead, "capture");
                Mark(arrivalOnly ? "sale-complete-with-earned-supplies-retained"
                    : "both-returned-with-earned-supplies-and-construction-retained");
                foreach (EntityId actor in humans) await peers[actor].Send("close");

                async Task ToOrigin(EntityId actor, long offset, long rowOffset = 0)
                {
                    var here = NativeExpeditionTravel.Cell(travel.Feet(actor));
                    var target = corridor.OriginApproach.Offset(offset, rowOffset, 0);
                    await travel.Lane(actor, here, new(here.X, target.Y, here.Z), target);
                }

            }
            WorldStepBatch[] batches;
            using (var journal = WorldDatabase.Open(database, create: false))
                batches = journal.ReadJournal(basis.Revision).Select(row => WorldStepCodec.Decode(row.AcceptedBatch)).ToArray();
            var movement = batches.SelectMany(batch => batch.Movement).Where(row => humans.Contains(row.ActorId)).ToArray();
            if (arrivalOnly) Assert.Empty(movement);
            else Assert.All(humans, actor => Assert.Contains(movement, row => row.ActorId == actor));
            Assert.All(movement, row =>
            {
                Assert.Equal(WorldMovementInput.DisplacementPerStep, Math.Abs(row.DeltaX) + Math.Abs(row.DeltaY));
                Assert.Null(row.ControlWitness);
                var control = genesis.Inventory.Controls.Single(value => value.ActorId == row.ActorId);
                Assert.Equal(new TrustedCommandDelivery(control.PrincipalId, control.SessionId), row.Delivery);
            });
            var hostileActions = batches.SelectMany(batch => batch.Actions).Where(command => command.ActorId == chief
                && command.Intent is QueueEncounterAbilityIntent).ToArray();
            if (arrivalOnly) Assert.Empty(hostileActions);
            else Assert.NotEmpty(hostileActions);
            using var saved = SqliteWorldAuthority.OpenExisting(database, WorldSnapshotCodec.Encode(genesis));
            var final = saved.ReadStateForHost();
            Assert.Equal(final.ContentHash, saved.ReadRecordedStateForHost(final.Revision).State.ContentHash);
            Assert.Equal(genesis.AdmissionVersion, final.AdmissionVersion);
            foreach (var row in all.SelectMany(peer => peer.Submissions))
            {
                var command = WorldActionCodec.Decode(Convert.FromHexString(row.GetProperty("Command").GetString()!));
                Assert.Contains(command.ActorId, humans);
                Assert.Null(command.ControlWitness);
                var receipt = saved.FindReceipt(command, new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId));
                Assert.NotNull(receipt);
                Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
                Assert.False(row.GetProperty("IsRetry").GetBoolean());
                Assert.Equal(Convert.FromHexString(row.GetProperty("Receipt").GetString()!), WorldReceiptCodecV1.Encode(receipt));
            }
            Assert.Equal(originalBytes, File.ReadAllBytes(sourceSave));
            bound.Token.ThrowIfCancellationRequested();
            Assert.True(watch.Elapsed.TotalSeconds < deadlineSeconds);
            Write(arrivalOnly ? "sale.json" : "journey.json", new { Status = "passed",
                Kind = arrivalOnly ? "saved-second-town-sale-only" : "complete-earned-expedition",
                Seconds = watch.Elapsed.TotalSeconds, BasisRevision = basis.Revision, final.Revision,
                HeadHash = final.ContentHash.Value, SourceHash = sourceHash, Chief = chief.ToString(),
                NativeMovementSamples = movement.Length, HostileActions = hostileActions.Length, Stages = stages,
                Limitations = arrivalOnly ? "Second-town sale only; no connected outbound/return acceptance."
                    : "Two native clients; one rendered. No physical/human/scale acceptance. Construction was already complete before departure." });
        }
        catch (Exception error)
        {
            Write("journey.failed.json", new { Stage = stage, Error = error.ToString(), Seconds = watch.Elapsed.TotalSeconds, Stages = stages });
            throw;
        }
        finally { foreach (var peer in all) await peer.DisposeAsync(); }

        Task Observe() => Task.WhenAll(peers.Values.Select(peer => peer.Send("observe")));
        async Task Send(EntityId actor, string verb, string? direction = null, EntityId? target = null)
        {
            await peers[actor].Send(verb, direction, target);
            if (verb is "wait" or "strike" or "retreat" or "cancel-withdrawal")
                await NativeEncounterObservationBarrier.Reach(peers.Values.ToArray(), peers[actor].View, bound.Token);
            else await Observe();
        }
        void Mark(string name)
        {
            stages.Add(new { Stage = name, Seconds = watch.Elapsed.TotalSeconds,
                Players = peers.Select(pair => new { Actor = pair.Key.ToString(), pair.Value.View.Progression!.Hp,
                    pair.Value.View.Progression.CurrencyBalance, pair.Value.View.IsInEncounter,
                    Feet = pair.Value.View.Actors.Single(row => row.ActorId == pair.Key).Feet }).ToArray() });
            Write("progress.json", stages);
        }
        void Write(string name, object value) => File.WriteAllText(Path.Combine(output, name),
            JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }
}
