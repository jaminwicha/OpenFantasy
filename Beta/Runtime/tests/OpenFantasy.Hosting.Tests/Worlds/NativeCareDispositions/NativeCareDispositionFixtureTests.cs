using System.Text.Json;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Care.Dispositions;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.NativeCareDispositions;

/// <summary>Exports real pending care earned through movement, combat and patient acknowledgment.</summary>
public sealed class NativeCareDispositionFixtureTests
{
    [Fact]
    public void Export_genuine_pending_request_with_separately_human_helper()
    {
        string? directory = Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_CARE_DISPOSITION_FIXTURES");
        Assert.SkipWhen(directory is null, "An explicit fresh native fixture destination is required.");
        Assert.False(Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any());
        Directory.CreateDirectory(directory);
        var fixture = new CareDispositionJournalFixture(humanHelper: true);
        WorldSnapshot pending;
        string save = Path.Combine(directory, "save.sqlite");
        using (var authority = SqliteWorldAuthority.CreateNew(save, fixture.Genesis))
        {
            pending = fixture.RequestAfterDefeat(authority);
            Assert.NotNull(pending.Care!.Pending);
            Assert.Equal(0, pending.Progression!.GetActor(fixture.Journey.Patient).CurrentHp);
            Assert.Empty(pending.Care.Dispositions);
            Assert.Empty(pending.Care.Treatments);
        }
        File.WriteAllBytes(Path.Combine(directory, "genesis.ofc"), WorldSnapshotCodec.Encode(fixture.Genesis));
        File.WriteAllBytes(Path.Combine(directory, "before.ofc"), WorldSnapshotCodec.Encode(pending));
        File.WriteAllText(Path.Combine(directory, "metadata.json"), JsonSerializer.Serialize(new
        {
            Kind = "native-care-disposition-fixture-v1", Profile = pending.ProfileVersion,
            PatientActorId = fixture.Journey.Patient.ToString(), HelperActorId = fixture.Journey.Helper.ToString(),
            RequestId = pending.Care!.Pending!.AcknowledgmentId.ToString(),
            GenesisHash = fixture.Genesis.ContentHash.Value, BeforeHash = pending.ContentHash.Value,
            BeforeSequence = pending.Revision,
            Boundary = "Explicit fresh human-helper calibration; managed lawful movement, real combat and acknowledgment. Native proof starts pending; no native approach/combat claim."
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }
}
