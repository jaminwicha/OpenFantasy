using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Content.EncounterOutcome;

/// <summary>
/// The explicit profile-35 selection: a fresh admission over the unchanged threatened world, its own codecs and
/// identity, declared calibrations with distinct admissions, and refusal (never upgrade) of other selections' saves.
/// </summary>
public sealed class CargoOutcomeAdmissionTests
{
    private const string Seed = "outcome-admission";
    private const string Name = "Traveler";

    [Fact]
    public void ProductionSelectionIsAFreshProfile35OverTheUnchangedThreatenedWorld()
    {
        WorldSnapshot threatened = RoadThreatWorldAdmission.CreateEmbedded(Seed, Name);
        WorldSnapshot genesis = CargoOutcomeWorldAdmission.CreateEmbedded(Seed, Name);
        Assert.Equal((ushort)34, threatened.ProfileVersion);
        Assert.Equal(WorldEncounterOutcomeAdmission.ProfileVersion, genesis.ProfileVersion);
        Assert.Equal(EncounterOutcomeState.Pin, genesis.EncounterOutcome!.RulesPin);
        Assert.Equal(0UL, genesis.EncounterOutcome.FinalizedCount);
        Assert.Empty(genesis.EncounterOutcome.Acknowledgments);
        Assert.Null(genesis.EncounterOutcome.Last);
        Assert.NotEqual(threatened.AdmissionVersion, genesis.AdmissionVersion);
        // Every basis owner except the admission-bound resource baseline is carried unchanged.
        Assert.Equal(threatened.Residents.Select(value => (value.ActorId, value.ControllerKind)),
            genesis.Residents.Select(value => (value.ActorId, value.ControllerKind)));
        Assert.Equal(threatened.Retreat!.RulesPin, genesis.Retreat!.RulesPin);
        Assert.Equal(threatened.EncounterLifecycle!.ContentHash, genesis.EncounterLifecycle!.ContentHash);

        byte[] bytes = WorldSnapshotCodec.Encode(genesis);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
        Assert.Equal(bytes, WorldSnapshotCodecV35.Encode(WorldSnapshotCodecV35.Decode(bytes)));
        Assert.ThrowsAny<FormatException>(() => WorldSnapshotCodecV34.Decode(bytes));
        Assert.ThrowsAny<FormatException>(() => WorldSnapshotCodecV35.Decode(WorldSnapshotCodec.Encode(threatened)));
        Assert.ThrowsAny<FormatException>(() => WorldSnapshotCodec.Decode(bytes[..^1]));

        CargoCircuitSources cargo = CargoCircuitSources.LoadEmbedded();
        RoadThreatSources threat = RoadThreatSources.LoadEmbedded();
        Assert.NotNull(CargoOutcomeWorldAdmission.Find(genesis, cargo, threat));
        Assert.Null(CargoOutcomeWorldAdmission.Find(threatened, cargo, threat));
        Assert.Null(RoadThreatWorldAdmission.Find(genesis, cargo, threat));
        Assert.True(CargoOutcomeWorldAdmission.IsOutcomeCargoWorld(genesis, cargo));
        Assert.Equal(genesis.ContentHash, CargoOutcomeWorldAdmission.CreateEmbedded(Seed, Name).ContentHash);
    }

    [Fact]
    public void AdmissionRefusesAnythingButAnUnplayedRetreatGenesis()
    {
        WorldSnapshot genesis = CargoOutcomeWorldAdmission.CreateEmbedded(Seed, Name);
        Assert.Throws<ArgumentException>(() => WorldEncounterOutcomeAdmission.CreateFresh(genesis));
        Assert.Throws<ArgumentException>(() => WorldEncounterOutcomeAdmission.CreateFresh(
            CargoWorldAdmission.CreateEmbedded(Seed, Name)));
    }

    [Fact]
    public void CalibrationsAreDeclaredWoundOnlyAndKeepTheirOwnAdmission()
    {
        WorldSnapshot production = CargoOutcomeWorldAdmission.CreateEmbedded(Seed, Name);
        WorldSnapshot wounded = CargoOutcomeWorldAdmission.CreateWoundedChief(Seed, Name, 1);
        RoadThreatBinding chief = CargoOutcomeWorldAdmission.Find(wounded, CargoCircuitSources.LoadEmbedded(), RoadThreatSources.LoadEmbedded())!;
        Assert.Equal(1, wounded.Progression!.GetActor(chief.ChiefActorId).CurrentHp);
        Assert.Equal(800, production.Progression!.GetActor(chief.ChiefActorId).CurrentHp);
        Assert.NotEqual(production.AdmissionVersion, wounded.AdmissionVersion);
        Assert.NotEqual(CargoOutcomeWorldAdmission.CreateWoundedChief(Seed, Name, 2).AdmissionVersion, wounded.AdmissionVersion);
        Assert.Equal(WorldEncounterOutcomeAdmission.ProfileVersion, wounded.ProfileVersion);

        WorldSnapshot threatened = RoadThreatWorldAdmission.CreateEmbedded(Seed, Name);
        EntityId player = threatened.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        int playerHp = threatened.Progression!.GetActor(player).CurrentHp;
        Assert.Throws<ArgumentException>(() => WorldEncounterOutcomeAdmission.CreateCalibratedFresh(threatened,
            new EncounterOutcomeCalibration(new Dictionary<EntityId, int> { [player] = 0 }, [])));
        Assert.Throws<ArgumentException>(() => WorldEncounterOutcomeAdmission.CreateCalibratedFresh(threatened,
            new EncounterOutcomeCalibration(new Dictionary<EntityId, int> { [player] = playerHp + 1 }, [])));
        Assert.Throws<ArgumentException>(() => WorldEncounterOutcomeAdmission.CreateCalibratedFresh(threatened,
            new EncounterOutcomeCalibration(new Dictionary<EntityId, int>(), [player])));
        Assert.Throws<ArgumentException>(() => WorldEncounterOutcomeAdmission.CreateCalibratedFresh(threatened,
            new EncounterOutcomeCalibration(new Dictionary<EntityId, int>(), [])));

        WorldSnapshot humans = CargoOutcomeWorldAdmission.CreateTwoHumanWolves(Seed, Name, 1);
        Assert.Equal(3, humans.Residents.Count(value => value.ControllerKind == ResidentControllerKind.Player));
        Assert.NotEqual(production.AdmissionVersion, humans.AdmissionVersion);
    }

    [Fact]
    public void SavesOfOtherSelectionsAreRefusedAndNeverUpgraded()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"openfantasy-outcome-admission-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            WorldSnapshot threatened = RoadThreatWorldAdmission.CreateEmbedded(Seed, Name);
            WorldSnapshot genesis = CargoOutcomeWorldAdmission.CreateEmbedded(Seed, Name);
            string outcomeSave = Path.Combine(directory, "outcome.sqlite");
            string threatSave = Path.Combine(directory, "threat.sqlite");
            SqliteWorldAuthority.CreateNew(outcomeSave, genesis).Dispose();
            SqliteWorldAuthority.CreateNew(threatSave, threatened).Dispose();
            Assert.ThrowsAny<Exception>(() => SqliteWorldAuthority.OpenExisting(outcomeSave, WorldSnapshotCodec.Encode(threatened)).Dispose());
            Assert.ThrowsAny<Exception>(() => SqliteWorldAuthority.OpenExisting(threatSave, WorldSnapshotCodec.Encode(genesis)).Dispose());
            using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(threatSave, WorldSnapshotCodec.Encode(threatened)))
            {
                Assert.Equal((ushort)34, reopened.InspectForHost().State.ProfileVersion);
            }
            using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(outcomeSave, WorldSnapshotCodec.Encode(genesis)))
            {
                Assert.Equal(WorldEncounterOutcomeAdmission.ProfileVersion, reopened.InspectForHost().State.ProfileVersion);
            }
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); }
            catch (IOException) { }
        }
    }
}
