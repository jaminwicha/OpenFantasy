using System.Buffers.Binary;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using static OpenFantasy.Core.Tests.Actors.BodyDecodingTestFrames;
using static OpenFantasy.Core.Tests.Care.CareModelFixture;
using static OpenFantasy.Core.Tests.Care.OrdinaryCareFixture;

namespace OpenFantasy.Core.Tests.Care;

public sealed class OrdinaryCareCodecTests
{
    [Fact]
    public void Dispatcher_preserves_legacy_bytes_and_both_explicit_codecs_refuse_wrong_ownership()
    {
        var legacy = CareModelFixture.History(2);
        byte[] bytes = CareCodecV1.Encode(legacy);
        Assert.Equal(bytes, CareCodec.Encode(legacy));
        Assert.Equal(bytes, CareCodec.Encode(CareCodec.Decode(bytes)));
        Assert.False(CareCodec.Decode(bytes).SupportsOrdinary);
        Assert.Throws<ArgumentException>(() => CareCodecV2.Encode(legacy));
        Assert.Throws<FormatException>(() => CareCodecV2.Decode(bytes));
        var ordinary = CreateOrdinary();
        Assert.Throws<ArgumentException>(() => CareCodecV1.Encode(ordinary));
        Assert.Throws<FormatException>(() => CareCodecV1.Decode(CareCodec.Encode(ordinary)));
        Assert.NotEqual(legacy.ContentHash, ordinary.ContentHash);
    }

    [Fact]
    public void Canonical_restore_preserves_original_grant_later_revocation_and_complete_resource_witness()
    {
        var state = CreateOrdinary(Ordinary().RecordUse(Use()).Revoke(Revocation()), CareModelFixture.History(1));
        byte[] bytes = CareCodec.Encode(state);
        var restored = CareCodec.Decode(bytes);
        Assert.Equal(bytes, CareCodecV2.Encode(restored));
        Assert.Equal(state.ContentHash, restored.ContentHash);
        Assert.Equal(4UL, restored.Revision);
        Assert.Equal(2UL, restored.Ordinary!.Revision);
        var permission = Assert.Single(restored.Ordinary.Permissions);
        Assert.Equal(Permission().GrantVersion, permission.GrantVersion);
        Assert.Equal(1UL, permission.Revision);
        var revoked = Assert.IsType<CarePermissionRevocation>(permission.Revocation);
        Assert.Equal((20UL, 1U, Patient), (revoked.WorldSequence, revoked.CommandOrdinal, revoked.PatientActorId));
        var use = Assert.Single(restored.Ordinary.Uses);
        Assert.Equal((Helper, Patient, 20UL, 0U, 4UL, 5UL, 10, 50, 60, 1_800UL),
            (use.ActorId, use.RecipientActorId, use.WorldSequence, use.CommandOrdinal, use.PatientRevisionBefore,
                use.PatientRevisionAfter, use.HpBefore, use.HpAfter, use.MaximumHp, use.ExplorationMilliseconds));
        Assert.Equal(0UL, use.PermissionRevision);
        Assert.Equal(EntityId.DeriveSpawned(use.CommandId, 0), use.ConsumptionId);
        Assert.Equal(EntityId.DeriveSpawned(use.CommandId, 1), use.UseId);
        Assert.Single(restored.Acknowledgments);
        Assert.Single(restored.Treatments);
    }

    [Fact]
    public void Self_only_owner_restores_without_invented_permission_or_aftermath_record()
    {
        var ordinary = new OrdinaryCareSnapshot([Definition(true, 501)], [], [Use(self: true)]);
        var state = new CareSnapshot(World, Branch, 1, [Remedy()], [], [], [], ordinary);
        var restored = CareCodec.Decode(CareCodec.Encode(state));
        Assert.Empty(restored.Agreements);
        Assert.Empty(restored.Ordinary!.Permissions);
        var use = Assert.Single(restored.Ordinary.Uses);
        Assert.Null(use.PermissionId);
        Assert.Null(use.PermissionGrantVersion);
        Assert.Null(use.PermissionRevision);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("root-revision")]
    [InlineData("ordinary-revision")]
    [InlineData("definition-pin")]
    [InlineData("form-pin")]
    [InlineData("delivery")]
    [InlineData("policy-time")]
    [InlineData("grant-pin")]
    [InlineData("use-identity")]
    [InlineData("consumption-identity")]
    [InlineData("permission-revision")]
    [InlineData("permission-grant")]
    [InlineData("living-hp")]
    [InlineData("restoration")]
    [InlineData("effect-time")]
    [InlineData("resource-overflow")]
    [InlineData("revocation-order")]
    [InlineData("revocation-patient")]
    public void Corrupted_domain_and_cross_owner_witnesses_fail_closed(string corruption)
    {
        var root = Fields(CareCodec.Encode(CreateOrdinary(Ordinary([Use()], Revocation()))));
        var ordinary = Fields(root[9]);
        var definitions = Fields(ordinary[3]);
        var definition = Fields(definitions[1]);
        var forms = Fields(definition[5]);
        var form = Fields(forms[1]);
        var permissions = Fields(ordinary[4]);
        var permission = Fields(permissions[1]);
        var grant = Fields(permission[1]);
        var revocation = Fields(permission[2]);
        var uses = Fields(ordinary[5]);
        var use = Fields(uses[1]);
        switch (corruption)
        {
            case "version": root[1] = Number(3); break;
            case "root-revision": root[4] = Number(1); break;
            case "ordinary-revision": ordinary[2] = Number(1); break;
            case "definition-pin": definition[3][0] ^= 1; break;
            case "form-pin": form[2][0] ^= 1; break;
            case "delivery": definition[4] = Number(3); break;
            case "policy-time": definition[6] = Number(0); break;
            case "grant-pin": grant[5][0] ^= 1; break;
            case "use-identity": use[1] = Id(99).ToByteArray(); break;
            case "consumption-identity": use[10] = Id(99).ToByteArray(); break;
            case "permission-revision": use[9] = Number(1); break;
            case "permission-grant": use[8][0] ^= 1; break;
            case "living-hp": use[15] = Number(0); break;
            case "restoration": use[16] = Number(49); break;
            case "effect-time": use[17] = Number(0); break;
            case "resource-overflow": use[13] = Number(ulong.MaxValue); break;
            case "revocation-order": revocation[6] = Number(0); break;
            case "revocation-patient": revocation[4] = Id(99).ToByteArray(); break;
            default: throw new InvalidOperationException("Unknown ordinary care corruption.");
        }
        forms[1] = Frame(form); definition[5] = Frame(forms); definitions[1] = Frame(definition); ordinary[3] = Frame(definitions);
        permission[1] = Frame(grant); permission[2] = Frame(revocation); permissions[1] = Frame(permission); ordinary[4] = Frame(permissions);
        uses[1] = Frame(use); ordinary[5] = Frame(uses); root[9] = Frame(ordinary);
        Assert.Throws<FormatException>(() => CareCodec.Decode(Frame(root)));
    }

    [Fact]
    public void Noncanonical_order_duplicate_fields_truncation_and_oversized_frames_refuse()
    {
        byte[] bytes = CareCodec.Encode(CreateOrdinary(Ordinary([Use()], Revocation())));
        foreach (int length in new[] { 0, 11, 12, 80, bytes.Length / 2, bytes.Length - 1 })
            Assert.Throws<FormatException>(() => CareCodec.Decode(bytes[..length]));
        Assert.Throws<FormatException>(() => CareCodec.Decode([.. bytes, 0]));
        Assert.Throws<FormatException>(() => CareCodec.Decode(new byte[CareCodec.MaximumEncodedByteLength + 1]));
        var wrongCount = bytes.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(wrongCount.AsSpan(8), uint.MaxValue);
        Assert.Throws<FormatException>(() => CareCodec.Decode(wrongCount));
        var root = Fields(bytes);
        var ordinary = Fields(root[9]);
        var definitions = Fields(ordinary[3]);
        ordinary[3] = Frame([definitions[0], definitions[2], definitions[1]]); root[9] = Frame(ordinary);
        Assert.Throws<FormatException>(() => CareCodec.Decode(Frame(root)));
        ordinary[3] = Frame([definitions[0], definitions[1], definitions[1]]); root[9] = Frame(ordinary);
        Assert.Throws<FormatException>(() => CareCodec.Decode(Frame(root)));
        ordinary[3] = Frame([definitions[0], new byte[2_049]]); root[9] = Frame(ordinary);
        Assert.Throws<FormatException>(() => CareCodec.Decode(Frame(root)));
        ordinary[3] = Frame([definitions[0], .. Enumerable.Repeat(definitions[1], OrdinaryCareSnapshot.MaximumDefinitions + 1)]);
        root[9] = Frame(ordinary);
        Assert.Throws<FormatException>(() => CareCodec.Decode(Frame(root)));
    }

    [Fact]
    public void Maximum_ordinary_catalogs_forms_uses_and_revocations_fit_the_declared_bound()
    {
        var forms = Enumerable.Range(0, RestorativeUseDefinition.MaximumSupportedForms)
            .Select(index => new RestorativeFormBinding(Id(900 + (ulong)index), Form.Version)).ToArray();
        var definitions = Enumerable.Range(0, OrdinaryCareSnapshot.MaximumDefinitions)
            .Select(index => Definition(index != 0, 500 + (ulong)index, forms)).ToArray();
        var permissions = Enumerable.Range(0, OrdinaryCareSnapshot.MaximumPermissions).Select(index =>
        {
            EntityId permission = Id(700 + (ulong)index), command = Id(30_000 + (ulong)index);
            return new CarePermission(permission, Patient, Helper, definitions[0].DefinitionId, definitions[0].Version,
                new(EntityId.DeriveSpawned(command, 0), command, permission, Patient, 500 + (ulong)index, 0));
        }).ToArray();
        var uses = Enumerable.Range(0, OrdinaryCareSnapshot.MaximumUses).Select(index =>
            Use(self: true, sequence: 100 + (ulong)index, command: 20_000 + (ulong)index,
                patientRevision: 2 * (ulong)index, definition: definitions[1])).ToArray();
        var state = CreateOrdinary(new(definitions, permissions, uses), CareModelFixture.History(1));
        byte[] bytes = CareCodec.Encode(state);
        Assert.True(bytes.Length < CareCodec.MaximumEncodedByteLength);
        var restored = CareCodec.Decode(bytes);
        Assert.Equal(OrdinaryCareSnapshot.MaximumUses, restored.Ordinary!.Uses.Count);
        Assert.Equal(OrdinaryCareSnapshot.MaximumPermissions, restored.Ordinary.Permissions.Count);
        Assert.All(restored.Ordinary.Definitions, value => Assert.Equal(RestorativeUseDefinition.MaximumSupportedForms, value.SupportedForms.Count));
        Assert.Equal(bytes, CareCodec.Encode(restored));
        Assert.Throws<ArgumentException>(() => new OrdinaryCareSnapshot(definitions, permissions,
            uses.Append(Use(self: true, sequence: 600, command: 40_000, definition: definitions[1]))));
    }
}
