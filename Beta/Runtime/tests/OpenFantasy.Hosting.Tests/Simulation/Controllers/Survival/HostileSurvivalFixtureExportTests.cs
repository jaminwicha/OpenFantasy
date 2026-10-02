using System.Text.Json;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.Controllers.Survival;

public sealed partial class HostileSurvivalJourneyTests
{
    [Fact]
    public async Task The_actual_first_paid_retreat_sample_reopens_as_an_unsafe_autonomous_return_fixture()
    {
        await using var fixture = await HostileSurvivalFixture.Open();
        var preparation = await PrepareFirstPaidSample(fixture);
        await using var observer = preparation.Observer;
        observer.SetReady(false);
        WorldSnapshot pending = fixture.State;
        var retreat = Assert.IsType<EncounterRetreatContinuation>(pending.Retreat!.Active);
        Assert.Equal(1, retreat.SamplesTaken);
        Assert.True(retreat.RemainingSamples > 0);
        Assert.Equal(preparation.SampleCharge, retreat.ReadinessCharged);
        Assert.Equal(WorldControllerMode.Autonomous, pending.Controllers!.Get(fixture.Actor).Mode);
        Assert.False(WorldControllerBoundaries.CanRestoreHuman(pending, fixture.Actor));
        await fixture.Tick(TimeSpan.FromMinutes(1));
        Assert.Equal(pending.ContentHash, fixture.State.ContentHash);
        byte[] genesisBytes = WorldSnapshotCodec.Encode(fixture.Genesis);
        byte[] pendingBytes = WorldSnapshotCodec.Encode(pending);
        await fixture.Host.StopAsync();
        using (var reopened = SqliteWorldAuthority.OpenExisting(fixture.Path, genesisBytes))
        {
            Assert.Equal(pendingBytes, WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
            Assert.Equal(WorldReceiptCodecV1.Encode(preparation.Receipt),
                WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(reopened.FindReceipt(retreat.Command, fixture.Delivery(fixture.Actor)))));
            Assert.True(reopened.IsIdentityOccupiedForHost(preparation.Disconnect));
            Assert.True(reopened.IsIdentityOccupiedForHost(preparation.InitialBite.DecisionCommandId));
            Assert.True(reopened.IsIdentityOccupiedForHost(retreat.Command.CommandId));
        }

        // The journey and reopening above are mandatory. Only writing the optional native packet
        // depends on the environment, so ordinary targeted execution still exercises the boundary.
        string? requested = Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_CONTROL_RETURN_FIXTURES");
        if (requested is null) return;
        string root = Path.GetFullPath(requested);
        Assert.False(Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any());
        Directory.CreateDirectory(root);
        File.Copy(fixture.Path, Path.Combine(root, "pending-retreat.sqlite"));
        File.WriteAllBytes(Path.Combine(root, "genesis.ofc"), genesisBytes);
        File.WriteAllBytes(Path.Combine(root, "pending-retreat.ofc"), pendingBytes);
        File.WriteAllBytes(Path.Combine(root, "retreat.command.ofc"), WorldActionCodec.Encode(retreat.Command));
        File.WriteAllBytes(Path.Combine(root, "retreat.receipt.ofc"), WorldReceiptCodecV1.Encode(preparation.Receipt));
        File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(new
        {
            Kind = "native-control-return-fixture-v1",
            Stage = "first-paid-retreat-sample-autonomous",
            Seed = "hostile-controller-survival",
            PlayerName = "Traveler",
            Calibration = "Only the second encounter wolf is admitted as a human observer; original HP, stats and finite stocks are unchanged.",
            Admission = "RoadThreatWorldAdmission -> WorldEncounterOutcomeAdmission.CreateCalibratedFresh -> WorldControllerAdmission.CreateFresh",
            pending.ProfileVersion,
            Actor = fixture.Actor.ToString(),
            Opponent = fixture.Opponent.ToString(),
            Observer = fixture.Observer.ToString(),
            Genesis = "genesis.ofc",
            GenesisHash = fixture.Genesis.ContentHash.ToString(),
            Database = "pending-retreat.sqlite",
            State = "pending-retreat.ofc",
            Command = "retreat.command.ofc",
            Receipt = "retreat.receipt.ofc",
            PrefixRevision = pending.Revision,
            PrefixHash = pending.ContentHash.ToString(),
            Disconnect = preparation.Disconnect.ToString(),
            ElapsedHostMilliseconds = 30_000,
            CommandId = retreat.Command.CommandId.ToString(),
            ReceiptSequence = preparation.Receipt.CommitSequence,
            FirstBiteCommandId = preparation.InitialBite.DecisionCommandId.ToString(),
            FirstBiteSequence = preparation.InitialBite.WorldSequence,
            FirstBiteDamage = preparation.InitialBite.Damage,
            retreat.SamplesTaken,
            retreat.RemainingSamples,
            retreat.ReadinessCharged,
            RouteSamples = retreat.Intent.Route.Count,
            PlayerHp = pending.Progression!.GetActor(fixture.Actor).CurrentHp,
            InventoryHash = pending.Inventory.ContentHash.ToString(),
            CustodyHash = pending.Inventory.Custody.ContentHash.ToString()
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
