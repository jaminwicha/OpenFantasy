using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using static OpenFantasy.Core.Tests.Actors.BodyDecodingTestFrames;
using static OpenFantasy.Core.Tests.Care.CareModelFixture;
using static OpenFantasy.Core.Tests.Care.OrdinaryCareFixture;

namespace OpenFantasy.Core.Tests.Care;

/// <summary>
/// Retained digests must equal the record bytes the unchanged public care codec writes for the same
/// owner. Expected values are therefore cut from complete encoded snapshots, never from a getter.
/// </summary>
public sealed class CareDefinitionDigestTests
{
    private const int ConcurrentReaders = 8;
    private const int ConcurrentRounds = 32;
    private static readonly TimeSpan ReaderStartLimit = TimeSpan.FromSeconds(30);

    [Fact]
    public void Cold_and_repeated_reads_equal_the_unchanged_encoder_digest_for_every_getter()
    {
        var remedy = Remedy();
        ContentHash remedyCold = remedy.Version;
        var agreement = new CareAgreement(Id(200), Patient, Helper, remedy.DefinitionId, remedyCold);
        ContentHash agreementCold = agreement.Version;
        var definition = Definition();
        ContentHash definitionCold = definition.Version;
        var permission = new CarePermission(Id(700), Patient, Helper, definition.DefinitionId, definitionCold);
        ContentHash grantCold = permission.GrantVersion;
        ContentHash completeCold = permission.Version;
        var revoked = new CarePermission(Id(700), Patient, Helper, definition.DefinitionId, definitionCold, Revocation());
        // The complete version is read first here, so neither getter depends on the other's retained value.
        ContentHash revokedCompleteCold = revoked.Version;
        ContentHash revokedGrantCold = revoked.GrantVersion;

        Assert.Equal(ContentHash.Compute(EncodedRemedy(remedy)), remedyCold);
        Assert.Equal(ContentHash.Compute(EncodedAgreement(agreement, remedy)), agreementCold);
        Assert.Equal(ContentHash.Compute(EncodedUseDefinition(definition, remedy)), definitionCold);
        Assert.Equal(ContentHash.Compute(EncodedGrant(permission, definition)), grantCold);
        Assert.Equal(ContentHash.Compute(EncodedPermission(permission, definition)), completeCold);
        Assert.Equal(ContentHash.Compute(EncodedGrant(revoked, definition)), revokedGrantCold);
        Assert.Equal(ContentHash.Compute(EncodedPermission(revoked, definition)), revokedCompleteCold);

        for (int read = 0; read < 3; read++)
        {
            Assert.Same(remedyCold, remedy.Version);
            Assert.Same(agreementCold, agreement.Version);
            Assert.Same(definitionCold, definition.Version);
            Assert.Same(grantCold, permission.GrantVersion);
            Assert.Same(completeCold, permission.Version);
            Assert.Same(revokedGrantCold, revoked.GrantVersion);
            Assert.Same(revokedCompleteCold, revoked.Version);
        }
        Assert.NotEqual(grantCold, completeCold);
        Assert.NotEqual(revokedGrantCold, revokedCompleteCold);
    }

    [Fact]
    public void Simultaneous_cold_readers_receive_one_complete_published_digest()
    {
        var pinnedRemedy = Remedy();
        var pinnedDefinition = Definition();
        ContentHash remedyExpected = ContentHash.Compute(EncodedRemedy(pinnedRemedy));
        ContentHash agreementExpected = ContentHash.Compute(EncodedAgreement(Agreement(), pinnedRemedy));
        ContentHash definitionExpected = ContentHash.Compute(EncodedUseDefinition(pinnedDefinition, pinnedRemedy));
        ContentHash grantExpected = ContentHash.Compute(EncodedGrant(Permission(), pinnedDefinition));
        ContentHash revokedExpected = ContentHash.Compute(EncodedPermission(Permission(Revocation()), pinnedDefinition));

        for (int round = 0; round < ConcurrentRounds; round++)
        {
            // Every round constructs owners no reader has touched; the fixture pins come from separate objects.
            var remedy = new RestorativeRemedyDefinition(Id(100), new CountedItemDefinition(Id(101), ItemVersion), 40, 256);
            var agreement = new CareAgreement(Id(200), Patient, Helper, Id(100), remedyExpected);
            var definition = new RestorativeUseDefinition(Id(500), Id(100), remedyExpected,
                RestorativeDeliveryMode.LivingAssistance, [Form]);
            var revoked = new CarePermission(Id(700), Patient, Helper, Id(500), definitionExpected, Revocation());

            AssertOnePublishedDigest(remedyExpected, () => remedy.Version);
            AssertOnePublishedDigest(agreementExpected, () => agreement.Version);
            AssertOnePublishedDigest(definitionExpected, () => definition.Version);
            AssertOnePublishedDigest(revokedExpected, () => revoked.Version);
            AssertOnePublishedDigest(grantExpected, () => revoked.GrantVersion);
        }
    }

    [Fact]
    public void Revocation_changes_only_the_complete_version_and_leaves_the_original_grant_unchanged()
    {
        var definition = Definition();
        var original = new CarePermission(Id(700), Patient, Helper, definition.DefinitionId, definition.Version);
        ContentHash grant = original.GrantVersion;
        ContentHash active = original.Version;

        var revoked = original.Revoke(Revocation());

        Assert.NotSame(original, revoked);
        Assert.False(revoked.IsActive);
        Assert.Equal(1UL, revoked.Revision);
        Assert.Equal(grant, revoked.GrantVersion);
        Assert.NotEqual(active, revoked.Version);
        Assert.Equal(ContentHash.Compute(EncodedGrant(revoked, definition)), revoked.GrantVersion);
        Assert.Equal(ContentHash.Compute(EncodedPermission(revoked, definition)), revoked.Version);

        Assert.True(original.IsActive);
        Assert.Null(original.Revocation);
        Assert.Equal(0UL, original.Revision);
        Assert.Same(grant, original.GrantVersion);
        Assert.Same(active, original.Version);
        Assert.Equal(ContentHash.Compute(EncodedPermission(original, definition)), original.Version);

        // An original never read before its revocation still encodes its own active facts afterwards.
        var unread = new CarePermission(Id(700), Patient, Helper, definition.DefinitionId, definition.Version);
        var unreadRevoked = unread.Revoke(Revocation());
        Assert.Equal(revoked.Version, unreadRevoked.Version);
        Assert.Equal(active, unread.Version);
        Assert.Equal(grant, unread.GrantVersion);
        Assert.Equal(grant, unreadRevoked.GrantVersion);

        // Another revocation position of the same permission identity is another complete version.
        var later = original.Revoke(Revocation(sequence: 21));
        Assert.NotEqual(revoked.Version, later.Version);
        Assert.Equal(grant, later.GrantVersion);

        // An equal grant digest is a description, not authority: the revoked owner admits no later assistance.
        Assert.Throws<InvalidOperationException>(() => revoked.Revoke(Revocation(sequence: 21)));
        Assert.Throws<ArgumentException>(() => new OrdinaryCareSnapshot([definition], [revoked],
            [Use(sequence: 21, definition: definition, permission: revoked)]));
    }

    [Fact]
    public void Constructor_input_mutation_cannot_alter_supported_forms_or_a_digest()
    {
        var second = new RestorativeFormBinding(Id(901), ContentHash.Compute("second living form"u8));
        var replacement = new RestorativeFormBinding(Id(902), ContentHash.Compute("replacement living form"u8));
        RestorativeFormBinding[] admitted = [Form, second];
        ContentHash expected = ContentHash.Compute(EncodedUseDefinition(Definition(forms: [Form, second]), Remedy()));
        List<RestorativeFormBinding> list = [Form, second];
        RestorativeFormBinding[] array = [Form, second];
        var fromList = Definition(forms: list);
        var fromArray = Definition(forms: array);

        // Changed before any read: the first encoding must describe the constructor's own copy.
        list[0] = replacement;
        list.Add(new RestorativeFormBinding(Id(903), Form.Version));
        array[1] = replacement;
        Assert.Equal(expected, fromList.Version);
        Assert.Equal(expected, fromArray.Version);
        Assert.Equal(admitted, fromList.SupportedForms);
        Assert.Equal(admitted, fromArray.SupportedForms);

        // Changed again after publication: neither the retained digest nor the forms follow the caller.
        list.Clear();
        array[0] = replacement;
        Assert.Equal(expected, fromList.Version);
        Assert.Equal(expected, fromArray.Version);
        Assert.Equal(admitted, fromList.SupportedForms);
        Assert.Equal(admitted, fromArray.SupportedForms);
        Assert.True(fromArray.SupportsForm(second.DefinitionId, second.Version));
        Assert.False(fromArray.SupportsForm(replacement.DefinitionId, replacement.Version));
        Assert.Equal(expected, ContentHash.Compute(EncodedUseDefinition(fromList, Remedy())));

        Assert.Throws<NotSupportedException>(() => ((IList<RestorativeFormBinding>)fromList.SupportedForms)[0] = replacement);
        Assert.Throws<NotSupportedException>(() => ((ICollection<RestorativeFormBinding>)fromList.SupportedForms).Add(replacement));
        Assert.Throws<NotSupportedException>(() => ((ICollection<RestorativeFormBinding>)fromList.SupportedForms).Clear());
        Assert.Equal(expected, fromList.Version);

        // Input order is not a retained fact; the canonical form order is.
        Assert.Equal(expected, Definition(forms: [second, Form]).Version);
    }

    [Fact]
    public void Same_identity_with_differing_rule_or_scope_facts_cannot_reuse_a_digest()
    {
        var remedy = Remedy();
        var strongerRemedy = Remedy(hp: 41);
        RestorativeRemedyDefinition[] remedies =
        [
            remedy,
            strongerRemedy,
            Remedy(range: 257),
            new RestorativeRemedyDefinition(Id(100), new CountedItemDefinition(Id(101),
                ContentHash.Compute("changed care model item"u8)), 40, 256),
            new RestorativeRemedyDefinition(Id(100), new CountedItemDefinition(Id(102), ItemVersion), 40, 256),
        ];
        Assert.All(remedies, value => Assert.Equal(Id(100), value.DefinitionId));
        AssertPairwiseDistinct(remedies.Select(value => value.Version));
        Assert.All(remedies, value => Assert.Equal(ContentHash.Compute(EncodedRemedy(value)), value.Version));

        CareAgreement[] agreements =
        [
            Agreement(),
            Agreement(patient: Id(12)),
            Agreement(remedy: strongerRemedy),
            new CareAgreement(Id(200), Patient, Id(12), remedy.DefinitionId, remedy.Version),
        ];
        Assert.All(agreements, value => Assert.Equal(Id(200), value.AgreementId));
        AssertPairwiseDistinct(agreements.Select(value => value.Version));
        Assert.All(agreements, value => Assert.Equal(ContentHash.Compute(EncodedAgreement(value,
            remedies.Single(candidate => candidate.Version == value.RemedyVersion))), value.Version));

        var definition = Definition();
        var widened = Definition(forms: [Form, new RestorativeFormBinding(Id(901), Form.Version)]);
        RestorativeUseDefinition[] definitions =
        [
            definition,
            widened,
            Definition(self: true),
            Definition(forms: [new RestorativeFormBinding(Form.DefinitionId, ContentHash.Compute("changed living form"u8))]),
            new RestorativeUseDefinition(Id(500), strongerRemedy.DefinitionId, strongerRemedy.Version,
                RestorativeDeliveryMode.LivingAssistance, [Form]),
        ];
        Assert.All(definitions, value => Assert.Equal(Id(500), value.DefinitionId));
        AssertPairwiseDistinct(definitions.Select(value => value.Version));
        Assert.All(definitions, value => Assert.Equal(ContentHash.Compute(EncodedUseDefinition(value,
            remedies.Single(candidate => candidate.Version == value.RemedyVersion))), value.Version));

        CarePermission[] permissions =
        [
            new CarePermission(Id(700), Patient, Helper, definition.DefinitionId, definition.Version),
            new CarePermission(Id(700), Patient, Id(12), definition.DefinitionId, definition.Version),
            new CarePermission(Id(700), Id(12), Helper, definition.DefinitionId, definition.Version),
            new CarePermission(Id(700), Patient, Helper, widened.DefinitionId, widened.Version),
        ];
        Assert.All(permissions, value => Assert.Equal(Id(700), value.PermissionId));
        AssertPairwiseDistinct(permissions.Select(value => value.GrantVersion));
        AssertPairwiseDistinct(permissions.Select(value => value.Version));
        AssertPairwiseDistinct(permissions.Select(value => value.Revoke(new CarePermissionRevocation(
            EntityId.DeriveSpawned(Id(10_001), 0), Id(10_001), Id(700), value.PatientActorId, 20, 1)).Version));
        Assert.All(permissions, value =>
        {
            var pinned = definitions.Single(candidate => candidate.Version == value.UseDefinitionVersion);
            Assert.Equal(ContentHash.Compute(EncodedGrant(value, pinned)), value.GrantVersion);
            Assert.Equal(ContentHash.Compute(EncodedPermission(value, pinned)), value.Version);
            Assert.NotEqual(value.GrantVersion, value.Version);
        });

        // Reading every contrasting owner leaves the first owners' retained digests their own.
        Assert.Equal(ContentHash.Compute(EncodedRemedy(Remedy())), remedy.Version);
        Assert.Equal(ContentHash.Compute(EncodedUseDefinition(Definition(), Remedy())), definition.Version);
    }

    [Fact]
    public void Retained_digests_are_not_serialized_and_restored_owners_encode_their_own()
    {
        var state = CreateOrdinary(Ordinary(revocation: Revocation()));
        byte[] before = CareCodec.Encode(state);
        var original = Assert.Single(state.Ordinary!.Permissions);
        ContentHash complete = original.Version;
        ContentHash grant = original.GrantVersion;
        Assert.Equal(before, CareCodec.Encode(state));

        var restored = CareCodec.Decode(before);
        var permission = Assert.Single(restored.Ordinary!.Permissions);
        Assert.NotSame(original, permission);
        Assert.Equal(complete, permission.Version);
        Assert.Equal(grant, permission.GrantVersion);
        Assert.Equal(Permission().GrantVersion, permission.GrantVersion);
        Assert.NotEqual(Permission().Version, permission.Version);
        Assert.Equal(state.GetRemedy(Id(100)).Version, restored.GetRemedy(Id(100)).Version);
        Assert.Equal(state.GetAgreement(Id(200)).Version, restored.GetAgreement(Id(200)).Version);
        Assert.Equal(state.Ordinary.GetDefinition(Id(500)).Version, restored.Ordinary.GetDefinition(Id(500)).Version);
        Assert.Equal(before, CareCodec.Encode(restored));
    }

    private static void AssertOnePublishedDigest(ContentHash expected, Func<ContentHash> read)
    {
        using var start = new Barrier(ConcurrentReaders);
        var observed = new ContentHash?[ConcurrentReaders];
        var failures = new Exception?[ConcurrentReaders];
        Thread[] readers = Enumerable.Range(0, ConcurrentReaders).Select(index => new Thread(() =>
        {
            try
            {
                Assert.True(start.SignalAndWait(ReaderStartLimit));
                observed[index] = read();
            }
            catch (Exception error)
            {
                failures[index] = error;
            }
        }) { IsBackground = true }).ToArray();
        foreach (Thread reader in readers) reader.Start();
        foreach (Thread reader in readers)
        {
            Assert.True(reader.Join(ReaderStartLimit), "Concurrent digest reader exceeded its completion bound.");
        }

        Assert.All(failures, failure => Assert.Null(failure));
        ContentHash published = Assert.IsType<ContentHash>(observed[0]);
        Assert.Equal(expected, published);
        Assert.All(observed, value => Assert.Same(published, value));
        Assert.Same(published, read());
    }

    private static void AssertPairwiseDistinct(IEnumerable<ContentHash> digests)
    {
        ContentHash[] values = digests.ToArray();
        Assert.Equal(values.Length, values.Distinct().Count());
    }

    private static byte[] EncodedRemedy(RestorativeRemedyDefinition remedy) =>
        Fields(AftermathRoot(remedy, Agreement(remedy: remedy))[5])[1];

    private static byte[] EncodedAgreement(CareAgreement agreement, RestorativeRemedyDefinition remedy) =>
        Fields(AftermathRoot(remedy, agreement)[6])[1];

    private static byte[] EncodedUseDefinition(RestorativeUseDefinition definition, RestorativeRemedyDefinition remedy) =>
        Fields(OrdinaryOwner(remedy, definition, [])[3])[1];

    private static byte[] EncodedPermission(CarePermission permission, RestorativeUseDefinition definition) =>
        Fields(OrdinaryOwner(Remedy(), definition, [permission])[4])[1];

    private static byte[] EncodedGrant(CarePermission permission, RestorativeUseDefinition definition) =>
        Fields(EncodedPermission(permission, definition))[1];

    private static List<byte[]> AftermathRoot(RestorativeRemedyDefinition remedy, CareAgreement agreement) =>
        Fields(CareCodecV1.Encode(CareModelFixture.Create(remedies: [remedy], agreements: [agreement])));

    private static List<byte[]> OrdinaryOwner(RestorativeRemedyDefinition remedy, RestorativeUseDefinition definition,
        IEnumerable<CarePermission> permissions)
    {
        var aftermath = CareModelFixture.Create(remedies: [remedy], agreements: [Agreement(remedy: remedy)]);
        var state = CreateOrdinary(new OrdinaryCareSnapshot([definition], permissions, []), aftermath);
        return Fields(Fields(CareCodec.Encode(state))[9]);
    }
}
