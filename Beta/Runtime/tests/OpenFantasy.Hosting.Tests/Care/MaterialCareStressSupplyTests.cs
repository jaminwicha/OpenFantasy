using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using System.Text.Json;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class MaterialCareStressSupplyTests
{
    [Fact]
    public void Finite_doses_and_real_seeded_combat_support_hundreds_of_later_full_treatments()
    {
        var fixture = new OrdinaryCareArchiveFixture(materialCareStress: true);
        var initial = fixture.Genesis;
        var journey = fixture.Journey;
        var originalPotion = initial.Inventory.Custody.GetLot(journey.PotionLot);
        Assert.Equal(600UL, originalPotion.Quantity);
        Assert.Equal(60UL, initial.Inventory.Custody.GetLot(journey.BerryLot).Quantity);
        Assert.Empty(initial.Production!.Batches);
        Assert.Empty(initial.Production.ConsumedItems);
        Assert.Empty(initial.ResourceHistory!.History.Batches);
        Assert.Equal(100_000, initial.Progression!.GetActor(journey.Player).CurrentHp);
        Assert.NotNull(initial.Excavation);
        Assert.NotNull(initial.Construction);
        Assert.NotNull(initial.Reservoirs);
        Assert.NotNull(initial.LiquidCells);

        using var files = new StorageFiles();
        WorldSnapshot prepared;
        long earnedDamage;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, initial))
        {
            prepared = fixture.Prepare255(new CareWorldSession(authority));
            var bites = prepared.ResourceHistory!.Evidence.OfType<WorldEncounterResourceEvidence>()
                .Where(value => value.Request.ActorId == journey.Wolf).ToArray();
            Assert.Equal(254, bites.Length);
            Assert.All(bites, bite => Assert.Equal(journey.Player, bite.Resolution.TargetActorId));
            long damage = bites.Sum(value => (long)value.Resolution.TargetHpBefore - value.Resolution.TargetHpAfter);
            earnedDamage = damage;
            var patient = prepared.Progression!.GetActor(journey.Player);
            Assert.Equal(damage, 100_000L - patient.CurrentHp);
            Assert.InRange(patient.CurrentHp, 1, 100_000);
            Assert.True(damage >= 520L * 40, "All later full doses require this much actual earned injury.");
            Assert.Equal(0, prepared.Progression.GetActor(journey.Wolf).CurrentHp);
            Assert.Equal(initial.Inventory.Custody.ContentHash, prepared.Inventory.Custody.ContentHash);
            Assert.Equal(originalPotion, prepared.Inventory.Custody.GetLot(journey.PotionLot));
            Assert.Empty(prepared.Production!.ConsumedItems);
            Assert.Empty(prepared.Care!.Ordinary!.Uses);
            Assert.Equal(255, prepared.ResourceHistory.History.Batches.Count);
            Assert.Null(prepared.ResourceHistory.ArchiveAnchor);
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(prepared), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        if (Environment.GetEnvironmentVariable("OPENFANTASY_MATERIAL_CARE_STRESS_EVIDENCE") is { } output)
        {
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, "initial.ofc"), WorldSnapshotCodec.Encode(initial));
            File.WriteAllBytes(Path.Combine(output, "injured.ofc"), WorldSnapshotCodec.Encode(prepared));
            File.WriteAllText(Path.Combine(output, "earned-supply.json"), JsonSerializer.Serialize(new
            {
                calibration = "Finite fresh 600 raw potions, 60 berries, 100 attack wolf; original 40-HP remedy and recipe.",
                initialHash = initial.ContentHash.Value,
                injuredHash = prepared.ContentHash.Value,
                actualDamage = earnedDamage,
                actualBites = 254,
                committedWorldRevision = prepared.Revision,
                rawDoses = originalPotion.Quantity,
                consumedDoses = prepared.Production!.ConsumedItems.Count,
                resourceChanges = prepared.ResourceHistory!.History.Batches.Count,
                reopenedExactly = true,
                continuationQualified = false
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
