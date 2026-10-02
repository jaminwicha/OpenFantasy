using System.Text.Json;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Care.DispositionContinuation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.NativeArchivedCareDispositions;

/// <summary>Exports an earned pending request after an actual complete-world cut, with its original human admission.</summary>
public sealed class NativeArchivedCareDispositionFixtureTests
{
    [Fact]
    public void Export_human_pending_request_after_a_normal_whole_world_archive_cut()
    {
        string? directory = Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_ARCHIVED_CARE_DISPOSITION_FIXTURES");
        Assert.SkipWhen(directory is null, "An explicit fresh archived native fixture destination is required.");
        Assert.False(Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any());
        Directory.CreateDirectory(directory);
        using var fixture = new DispositionContinuationFixture(humanHelper: true);
        string save = Path.Combine(directory, "save.sqlite");
        using (var source = fixture.CopyPending()) File.Copy(source.Path, save);
        WorldSnapshot carried;
        WorldMaterialCareArchiveReference reference;
        WorldActionCommand refused;
        WorldActionReceipt refusedReceipt;
        using (var authority = SqliteWorldAuthority.OpenExisting(save, WorldSnapshotCodec.Encode(fixture.Genesis)))
        {
            var decision = fixture.CutPendingWithRefusedRemedy(authority, out refused);
            refusedReceipt = Assert.Single(decision.Receipts);
            carried = decision.Candidate;
            var archive = Assert.IsType<WorldMaterialCareArchiveSegment>(decision.SealedMaterialCareSegment);
            reference = archive.Reference;
            Assert.Equal(1UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);
            Assert.Equal(WorldSnapshotCodec.Encode(fixture.Pending), authority.ReadMaterialCareArchiveForHost(0).Segment.Encode());
            Assert.Equivalent(fixture.Request, carried.Care!.Pending, strict: true);
            Assert.Equivalent(fixture.Request, carried.Care.Continuation!.Pending, strict: true);
            Assert.Equal(1UL, carried.Care.Continuation.Ordinal);
            Assert.Equal(0UL, carried.Care.Continuation.ArchivedAcknowledgments);
            Assert.Empty(carried.Care.Dispositions);
            Assert.Empty(carried.Care.Treatments);
            Assert.Equal(0, carried.Progression!.GetActor(fixture.Journey.Patient).CurrentHp);
            foreach (var actor in new[] { fixture.Journey.Patient, fixture.Journey.Helper })
            {
                Assert.Equal(ResidentControllerKind.Player, fixture.Genesis.GetResident(actor).ControllerKind);
                Assert.Equal(WorldControllerMode.Human, fixture.Genesis.Controllers!.Get(actor).Mode);
                Assert.Equal(WorldControllerMode.Human, carried.Controllers!.Get(actor).Mode);
            }
        }
        Assert.False(File.Exists(save + "-wal") || File.Exists(save + "-shm"));
        File.WriteAllBytes(Path.Combine(directory, "genesis.ofc"), WorldSnapshotCodec.Encode(fixture.Genesis));
        File.WriteAllBytes(Path.Combine(directory, "before.ofc"), WorldSnapshotCodec.Encode(carried));
        File.WriteAllText(Path.Combine(directory, "metadata.json"), JsonSerializer.Serialize(new
        {
            Kind = "native-care-disposition-fixture-v1", Profile = carried.ProfileVersion,
            PatientActorId = fixture.Journey.Patient.ToString(), HelperActorId = fixture.Journey.Helper.ToString(),
            RequestId = fixture.Request.AcknowledgmentId.ToString(), GenesisHash = fixture.Genesis.ContentHash.Value,
            BeforeHash = carried.ContentHash.Value, BeforeSequence = carried.Revision,
            ArchiveCount = 1, ArchiveOrdinal = reference.Ordinal, ArchiveBoundarySequence = reference.BoundarySequence,
            ArchiveBoundaryHash = reference.BoundaryHash.Value, ArchivePublicationSequence = carried.Revision,
            RefusedCutCommandBytes = Convert.ToHexString(WorldActionCodec.Encode(refused)),
            RefusedCutReceiptBytes = Convert.ToHexString(WorldReceiptCodecV1.Encode(refusedReceipt)),
            EarnedResourceBatches = fixture.Pending.ResourceHistory!.History.Batches.Count,
            Boundary = "Fresh human-helper calibration before history; 255 actual one-HP bites, patient acknowledgment, "
                + "then a normally scheduled whole-world cut carried the request. Native proof begins after that cut; "
                + "no native preparation/combat claim and no replacement of a lived controller."
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }
}
