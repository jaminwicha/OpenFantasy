using System.Globalization;
using System.Text.Json;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Observations;

namespace OpenFantasy.Core.Tests.Observations;

public sealed class ResourceObservationTests
{
    private static EntityId Id(ulong value) => new(0, value);
    private static ObservationScope Scope(ulong actor = 5) => new(Id(3), Id(4), Id(actor));
    private static ObservationAudience Audience(ObservationScope? scope = null, ulong control = 7) =>
        new(Id(1), Id(2), scope ?? Scope(), control);
    private static ResourceObservationView View(ulong id = 11, ulong revision = 1,
        KnowledgeBasis basis = KnowledgeBasis.DirectObservation, ulong quantity = 6) =>
        new(Id(id), revision, Id(20), new CountedItemDefinition(Id(30),
            ContentHash.Compute("known-wood-definition-v1"u8)), quantity, basis, 900, new WorldInstant(-2, 7));
    private static CommittedResourceObservation Committed(ResourceObservationView? view = null,
        ObservationScope? scope = null, ulong commit = 10, ulong cause = 100) =>
        new(scope ?? Scope(), commit, Id(cause), view ?? View());
    private static ResourceKnowledgeSnapshot Snapshot(params CommittedResourceObservation[] entries) =>
        new(Scope(), Id(9), 4, 50, entries);
    private static ResourceObservationProjection Project(ResourceKnowledgeSnapshot snapshot) =>
        new ResourceObservationProjector().Project(Audience(), Audience(), snapshot);

    [Fact]
    public void IndependentPublicWireVectorsMatch()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "observations-v1.json")));
        foreach (JsonElement sample in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var entries = new List<CommittedResourceObservation>();
            foreach (JsonElement row in sample.GetProperty("entries").EnumerateArray())
            {
                JsonElement reporter = row.GetProperty("knownReporterId");
                var view = new ResourceObservationView(Id(row.GetProperty("disclosureId").GetUInt64()),
                    row.GetProperty("disclosureRevision").GetUInt64(), Id(row.GetProperty("knownSubjectId").GetUInt64()),
                    new CountedItemDefinition(Id(row.GetProperty("materialId").GetUInt64()),
                        new ContentHash(row.GetProperty("materialVersion").GetString()!)),
                    ulong.Parse(row.GetProperty("quantity").GetString()!, CultureInfo.InvariantCulture),
                    (KnowledgeBasis)row.GetProperty("basis").GetUInt64(), row.GetProperty("confidence").GetUInt32(),
                    new WorldInstant(long.Parse(row.GetProperty("acquiredDays").GetString()!, CultureInfo.InvariantCulture),
                        row.GetProperty("acquiredMilliseconds").GetInt32()),
                    reporter.ValueKind == JsonValueKind.Null ? null : Id(reporter.GetUInt64()));
                entries.Add(Committed(view));
            }

            ResourceObservationProjection projection = Project(new ResourceKnowledgeSnapshot(Scope(),
                Id(sample.GetProperty("epoch").GetUInt64()), sample.GetProperty("disclosureRevision").GetUInt64(),
                50, entries.AsEnumerable().Reverse()));
            Assert.Equal(sample.GetProperty("hex").GetString(),
                Convert.ToHexString(ObservationEncodingV1.Encode(projection)).ToLowerInvariant());
            Assert.Equal(sample.GetProperty("hash").GetString(), projection.ComputeContentHash().Value);
        }
    }

    [Fact]
    public void HiddenHeadAndCauseChangesCannotChangePublicBytesOrJson()
    {
        ResourceObservationView known = View();
        ResourceObservationProjection earlier = Project(new ResourceKnowledgeSnapshot(Scope(), Id(9), 1, 10,
            [Committed(known, commit: 10, cause: 100)]));
        ResourceObservationProjection later = Project(new ResourceKnowledgeSnapshot(Scope(), Id(9), 1, ulong.MaxValue,
            [Committed(known, commit: ulong.MaxValue, cause: 999)]));
        Assert.Equal(ObservationEncodingV1.Encode(earlier), ObservationEncodingV1.Encode(later));
        Assert.Equal(JsonSerializer.Serialize(earlier), JsonSerializer.Serialize(later));
        string json = JsonSerializer.Serialize(later);
        Assert.DoesNotContain("RecordedCommit", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PrivateSource", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PinnedCommit", json, StringComparison.Ordinal);
        Assert.Equal(Project(new ResourceKnowledgeSnapshot(Scope(), Id(9), 0, 0, [])).ComputeContentHash(),
            Project(new ResourceKnowledgeSnapshot(Scope(), Id(9), 0, ulong.MaxValue, [])).ComputeContentHash());
    }

    [Fact]
    public void EveryAudienceDimensionAndRevokedBindingRejectIdentically()
    {
        ObservationAudience request = Audience();
        ObservationAudience?[] bindings = [null,
            new(Id(999), Id(2), Scope(), 7), new(Id(1), Id(999), Scope(), 7),
            Audience(new ObservationScope(Id(999), Id(4), Id(5))),
            Audience(new ObservationScope(Id(3), Id(999), Id(5))), Audience(Scope(999)), Audience(control: 8)];
        foreach (ObservationAudience? binding in bindings)
        {
            ObservationAccessException failure = Assert.Throws<ObservationAccessException>(() =>
                new ResourceObservationProjector().Project(request, binding, Snapshot(Committed())));
            Assert.Equal("The observation view is unavailable.", failure.Message);
            Assert.DoesNotContain("999", failure.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ForeignJournalCannotBeProjectedUnderTheCurrentAudience()
    {
        ObservationScope[] foreignScopes = [Scope(99), new(Id(99), Id(4), Id(5)), new(Id(3), Id(99), Id(5))];
        foreach (ObservationScope scope in foreignScopes)
        {
            var journal = new ResourceKnowledgeSnapshot(scope, Id(9), 1, 50, [Committed(scope: scope)]);
            Assert.Throws<ObservationAccessException>(() => Project(journal));
        }
    }

    [Fact]
    public void EachActorKeepsItsOwnReportsAndKnowledgeWithoutBorrowing()
    {
        ResourceObservationProjection first = Project(Snapshot(Committed()));
        ObservationScope secondScope = Scope(6);
        var other = new ResourceKnowledgeSnapshot(secondScope, Id(8), 1, 50,
            [Committed(View(basis: KnowledgeBasis.Report, quantity: 99), secondScope)]);
        ResourceObservationProjection second = new ResourceObservationProjector().Project(
            Audience(secondScope), Audience(secondScope), other);
        Assert.Equal(6UL, Assert.Single(first.Entries).Quantity);
        Assert.Equal(99UL, Assert.Single(second.Entries).Quantity);
        Assert.Equal(KnowledgeBasis.Report, second.Entries[0].Basis);
        Assert.NotEqual(first.ComputeContentHash(), second.ComputeContentHash());
    }

    [Fact]
    public void StaleClaimsAndSubjectiveBasisRemainVisibleAsRecorded()
    {
        foreach (KnowledgeBasis basis in Enum.GetValues<KnowledgeBasis>())
        {
            ResourceObservationView known = View(basis: basis, quantity: 0);
            ResourceObservationView projected = Assert.Single(Project(Snapshot(Committed(known))).Entries);
            Assert.Equal(basis, projected.Basis);
            Assert.Equal(0UL, projected.Quantity);
            Assert.Equal(new WorldInstant(-2, 7), projected.AcquiredAt);
            Assert.Equal(900U, projected.Confidence);
        }
    }

    [Fact]
    public void FutureUncommittedForeignAndDuplicateRecordsRejectBeforeProjection()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Committed(commit: 0));
        Assert.Throws<ArgumentException>(() => Snapshot(Committed(commit: 51)));
        Assert.Throws<ArgumentException>(() => Snapshot(Committed(scope: Scope(6))));
        Assert.Throws<ArgumentException>(() => Snapshot(Committed(View(revision: 5))));
        Assert.Throws<ArgumentException>(() => Snapshot(Committed(), Committed(View(revision: 2))));
        Assert.Throws<ArgumentException>(() => Snapshot(Committed(), Committed(View(id: 12))));
        Assert.Throws<ArgumentException>(() => Committed(cause: 11));
    }

    [Fact]
    public void PrivateSourcesCannotAliasPublicIdentitiesAcrossRecordsOrAudience()
    {
        Assert.Throws<ArgumentException>(() => Snapshot(Committed(cause: 12),
            Committed(View(id: 12, revision: 2))));
        foreach (ulong identity in new ulong[] { 3, 4, 5, 9, 20, 30 })
        {
            Assert.Throws<ArgumentException>(() => Snapshot(Committed(cause: identity)));
        }

        ResourceObservationView value = View(basis: KnowledgeBasis.Report);
        var report = new ResourceObservationView(value.DisclosureId, 1, value.KnownSubjectId,
            value.KnownMaterial, 6, KnowledgeBasis.Report, 400, default, Id(100));
        Assert.Throws<ArgumentException>(() => Snapshot(Committed(report)));
        foreach (ulong identity in new ulong[] { 1, 2 })
        {
            Assert.Throws<ObservationAccessException>(() => Project(Snapshot(Committed(cause: identity))));
        }
    }

    [Fact]
    public void OrderingCopiesAndPublicCollectionsCannotMutateAcceptedKnowledge()
    {
        CommittedResourceObservation[] entries = [Committed(View(id: 12, revision: 3)), Committed()];
        ResourceKnowledgeSnapshot snapshot = Snapshot(entries);
        ContentHash expected = Project(snapshot).ComputeContentHash();
        entries[0] = Committed(View(id: 13, revision: 2));
        ResourceObservationProjection projection = Project(snapshot);
        Assert.Equal(expected, projection.ComputeContentHash());
        Assert.Equal(new ulong[] { 1, 3 }, projection.Entries.Select(entry => entry.DisclosureRevision));
        Assert.Throws<NotSupportedException>(() => ((IList<ResourceObservationView>)projection.Entries).Clear());
        byte[] encoded = ObservationEncodingV1.Encode(projection);
        encoded[0] ^= 1;
        Assert.Equal(expected, projection.ComputeContentHash());
    }

    [Fact]
    public void BoundsRejectWithoutUnboundedEnumerationOrSilentTruncation()
    {
        var entries = Enumerable.Range(1, ResourceKnowledgeSnapshot.MaximumEntries).Select(index =>
            Committed(View(id: (ulong)index + 1000, revision: (ulong)index))).ToArray();
        var maximum = new ResourceKnowledgeSnapshot(Scope(), Id(9), 1024, 50, entries);
        Assert.Equal(1024, Project(maximum).Entries.Count);
        int visited = 0;
        IEnumerable<CommittedResourceObservation> Endless()
        {
            while (true)
            {
                visited++;
                yield return Committed(View(id: (ulong)visited + 1000, revision: (ulong)visited));
            }
        }
        Assert.Throws<ArgumentException>(() => new ResourceKnowledgeSnapshot(Scope(), Id(9), ulong.MaxValue,
            50, Endless()));
        Assert.Equal(1025, visited);
    }

    [Fact]
    public void InvalidBandsRevisionsAndReporterCombinationsReject()
    {
        ResourceObservationView valid = View();
        Assert.Throws<ArgumentOutOfRangeException>(() => View(revision: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => View(basis: (KnowledgeBasis)0));
        Assert.Throws<ArgumentOutOfRangeException>(() => View(basis: (KnowledgeBasis)ulong.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceObservationView(Id(11), 1, Id(20),
            valid.KnownMaterial, 6, KnowledgeBasis.Report, 1001, default));
        Assert.Throws<ArgumentException>(() => new ResourceObservationView(Id(11), 1, Id(20),
            valid.KnownMaterial, 6, KnowledgeBasis.Inference, 0, default, Id(22)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Audience(control: 0));
    }

    [Fact]
    public void PresentZeroReporterIdentityIsDistinctFromAbsence()
    {
        ResourceObservationView absent = View(basis: KnowledgeBasis.Report);
        var present = new ResourceObservationView(absent.DisclosureId, absent.DisclosureRevision,
            absent.KnownSubjectId, absent.KnownMaterial, absent.Quantity, absent.Basis,
            absent.Confidence, absent.AcquiredAt, default(EntityId));
        Assert.NotEqual(Project(Snapshot(Committed(absent))).ComputeContentHash(),
            Project(Snapshot(Committed(present))).ComputeContentHash());
    }

    [Fact]
    public void FullReplacementCanClearKnownEntriesAndChangesItsOwnRevision()
    {
        ResourceObservationProjection old = Project(Snapshot(Committed()));
        ResourceObservationProjection cleared = Project(new ResourceKnowledgeSnapshot(Scope(), Id(9), 5, 60, []));
        Assert.Single(old.Entries);
        Assert.Empty(cleared.Entries);
        Assert.Equal(5UL, cleared.DisclosureRevision);
        Assert.NotEqual(old.ComputeContentHash(), cleared.ComputeContentHash());
        ResourceObservationProjection newEpoch = Project(new ResourceKnowledgeSnapshot(Scope(), Id(10), 0, 60, []));
        Assert.NotEqual(cleared.ComputeContentHash(), newEpoch.ComputeContentHash());
    }
}
