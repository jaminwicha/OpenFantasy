using System.Buffers.Binary;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Tests.Construction;

public sealed class ConstructionPhysicalArchiveTests
{
    [Fact]
    public void Empty_initial_owner_roundtrips_without_serializing_a_second_construction_copy()
    {
        ConstructionSnapshot construction = Construction(installed: false);
        var physical = new ConstructionPhysicalSnapshot(construction, 0, []);
        byte[] bytes = ConstructionPhysicalCodecV1.Encode(physical);
        byte[][] fields = Fields(bytes);
        Assert.Equal(6, fields.Length);
        Assert.Equal("construction.physical.v1"u8.ToArray(), fields[0]);
        Assert.Equal(construction.ContentHash.ToByteArray(), fields[1]);
        Assert.Equal(construction.WorldId.ToByteArray(), fields[2]);
        Assert.Equal(construction.BranchId.ToByteArray(), fields[3]);
        Assert.Single(Fields(fields[5]));
        ConstructionPhysicalSnapshot restored = ConstructionPhysicalCodecV1.Decode(bytes, construction);
        Assert.Same(construction, restored.Construction);
        Assert.Empty(restored.Members);
        Assert.Equal(bytes, ConstructionPhysicalCodecV1.Encode(restored));
        Assert.Equal(ContentHash.Compute(bytes), restored.ContentHash);
    }

    [Fact]
    public void Complete_initial_members_have_canonical_bytes_independent_of_input_order()
    {
        ConstructionSnapshot construction = Construction();
        ConstructionPhysicalSnapshot physical = Initial(construction);
        byte[] bytes = ConstructionPhysicalCodecV1.Encode(physical);
        var reordered = new ConstructionPhysicalSnapshot(construction, 0, physical.Members.Reverse());
        Assert.Equal(bytes, ConstructionPhysicalCodecV1.Encode(reordered));
        ConstructionPhysicalSnapshot restored = ConstructionPhysicalCodecV1.Decode(bytes, construction);
        Assert.Equal(physical.Members.Select(value => value.ElementId), restored.Members.Select(value => value.ElementId));
        Assert.Equal(physical.ConstructionHash, restored.ConstructionHash);
        Assert.All(restored.Members, member =>
        {
            Assert.Equal(0UL, member.Revision);
            Assert.Equal(10_000U, member.ConditionParts);
            Assert.Equal(default, member.Displacement);
            Assert.Equal(ConstructionMemberDisposition.Standing, member.Disposition);
        });
        Assert.Equal(bytes, ConstructionPhysicalCodecV1.Encode(restored));
    }

    [Fact]
    public void Displaced_falling_and_resting_members_retain_signed_coordinates_and_exact_revisions()
    {
        ConstructionSnapshot construction = Construction();
        ConstructionMemberState[] members = Initial(construction).Members.Select(member => member.MemberIndex switch
        {
            0 => new ConstructionMemberState(member.StructureId, member.MemberIndex, 1, 7_500,
                new SubunitPosition(512, -256, -32), ConstructionMemberDisposition.Falling),
            1 => new ConstructionMemberState(member.StructureId, member.MemberIndex, 2, 0,
                new SubunitPosition(-512, 0, -64), ConstructionMemberDisposition.Resting),
            _ => member
        }).ToArray();
        var physical = new ConstructionPhysicalSnapshot(construction, 2, members);
        byte[] bytes = ConstructionPhysicalCodecV1.Encode(physical);
        ConstructionPhysicalSnapshot restored = ConstructionPhysicalCodecV1.Decode(bytes, construction);
        Assert.Equal(2UL, restored.Revision);
        for (int index = 0; index < members.Length; index++)
        {
            Assert.Equal(members[index].StructureId, restored.Members[index].StructureId);
            Assert.Equal(members[index].MemberIndex, restored.Members[index].MemberIndex);
            Assert.Equal(members[index].Revision, restored.Members[index].Revision);
            Assert.Equal(members[index].ConditionParts, restored.Members[index].ConditionParts);
            Assert.Equal(members[index].Displacement, restored.Members[index].Displacement);
            Assert.Equal(members[index].Disposition, restored.Members[index].Disposition);
        }
        byte[][] row = Fields(Fields(Fields(bytes)[5])[1]);
        Assert.Equal(-256L, BinaryPrimitives.ReadInt64BigEndian(row[6]));
        Assert.Equal(-32L, BinaryPrimitives.ReadInt64BigEndian(row[7]));
        Assert.Equal(bytes, ConstructionPhysicalCodecV1.Encode(restored));
        Assert.Equal(physical.ContentHash, restored.ContentHash);
    }

    [Fact]
    public void Trusted_construction_pin_and_world_branch_are_required_independently()
    {
        ConstructionSnapshot original = Construction();
        ConstructionSnapshot changedPlacement = Construction(siteX: 288);
        byte[] bytes = ConstructionPhysicalCodecV1.Encode(Initial(original));
        Assert.NotEqual(original.ContentHash, changedPlacement.ContentHash);
        Assert.Throws<FormatException>(() => ConstructionPhysicalCodecV1.Decode(bytes, changedPlacement));
        Assert.Throws<FormatException>(() => ConstructionPhysicalCodecV1.Decode(
            ReplaceField(bytes, 1, changedPlacement.ContentHash.ToByteArray()), original));
        Assert.Throws<FormatException>(() => ConstructionPhysicalCodecV1.Decode(ReplaceField(bytes, 2, Id(90).ToByteArray()), original));
        Assert.Throws<FormatException>(() => ConstructionPhysicalCodecV1.Decode(ReplaceField(bytes, 3, Id(91).ToByteArray()), original));
        Assert.Throws<FormatException>(() => ConstructionPhysicalCodecV1.Decode(ReplaceField(bytes, 1, new byte[31]), original));
    }

    [Fact]
    public void Unknown_structure_and_forged_or_overflowed_member_ordinals_are_rejected()
    {
        ConstructionSnapshot construction = Construction();
        byte[] bytes = ConstructionPhysicalCodecV1.Encode(Initial(construction));
        Assert.Throws<FormatException>(() => DecodeMemberChange(bytes, construction, 1, Id(99).ToByteArray()));
        Assert.Throws<FormatException>(() => DecodeMemberChange(bytes, construction, 2, Number((ulong)construction.Definitions.Single().Members.Count)));
        Assert.Throws<FormatException>(() => DecodeMemberChange(bytes, construction, 2, Number((ulong)uint.MaxValue + 1)));
    }

    [Fact]
    public void Missing_duplicate_and_noncanonical_member_rows_cannot_change_complete_ownership()
    {
        ConstructionSnapshot construction = Construction();
        byte[] bytes = ConstructionPhysicalCodecV1.Encode(Initial(construction));
        byte[][] rows = Fields(Fields(bytes)[5]);
        byte[][] missing = rows.Where((_, index) => index != 1).ToArray();
        Assert.Throws<FormatException>(() => ConstructionPhysicalCodecV1.Decode(ReplaceField(bytes, 5, Frame(missing)), construction));
        byte[][] duplicate = (byte[][])rows.Clone();
        duplicate[2] = duplicate[1];
        Assert.Throws<FormatException>(() => ConstructionPhysicalCodecV1.Decode(ReplaceField(bytes, 5, Frame(duplicate)), construction));
        byte[][] reordered = (byte[][])rows.Clone();
        (reordered[1], reordered[2]) = (reordered[2], reordered[1]);
        Assert.Throws<FormatException>(() => ConstructionPhysicalCodecV1.Decode(ReplaceField(bytes, 5, Frame(reordered)), construction));
    }

    [Fact]
    public void Invalid_initial_state_revision_condition_and_disposition_are_rejected()
    {
        ConstructionSnapshot construction = Construction();
        byte[] bytes = ConstructionPhysicalCodecV1.Encode(Initial(construction));
        Assert.Throws<FormatException>(() => DecodeMemberChange(bytes, construction, 3, Number(1)));
        Assert.Throws<FormatException>(() => DecodeMemberChange(bytes, construction, 4, Number(9_999)));
        Assert.Throws<FormatException>(() => DecodeMemberChange(bytes, construction, 4, Number(10_001)));
        Assert.Throws<FormatException>(() => DecodeMemberChange(bytes, construction, 4, Number((ulong)uint.MaxValue + 1)));
        Assert.Throws<FormatException>(() => DecodeMemberChange(bytes, construction, 5, Signed(1)));
        Assert.Throws<FormatException>(() => DecodeMemberChange(bytes, construction, 8, Number((ulong)ConstructionMemberDisposition.Falling)));
        Assert.Throws<FormatException>(() => DecodeMemberChange(bytes, construction, 8, Number(0)));
        Assert.Throws<FormatException>(() => DecodeMemberChange(bytes, construction, 8, Number(4)));
        Assert.Throws<FormatException>(() => DecodeMemberChange(bytes, construction, 8, Number(ulong.MaxValue)));
        Assert.Throws<FormatException>(() => DecodeMemberChange(bytes, construction, 8, new byte[7]));
    }

    [Fact]
    public void Displacement_overflow_is_rejected_after_valid_member_types_are_read()
    {
        ConstructionSnapshot construction = Construction();
        byte[] bytes = ConstructionPhysicalCodecV1.Encode(Initial(construction));
        byte[] row = Fields(Fields(bytes)[5])[1];
        row = ReplaceField(row, 3, Number(1));
        row = ReplaceField(row, 5, Signed(long.MaxValue));
        row = ReplaceField(row, 8, Number((ulong)ConstructionMemberDisposition.Falling));
        byte[] changed = ReplaceField(ReplaceField(bytes, 4, Number(1)), 5,
            ReplaceField(Fields(bytes)[5], 1, row));
        Assert.Throws<FormatException>(() => ConstructionPhysicalCodecV1.Decode(changed, construction));
    }

    [Fact]
    public void Collection_row_and_archive_bounds_are_enforced_before_semantic_reconstruction()
    {
        ConstructionSnapshot construction = Construction();
        byte[] bytes = ConstructionPhysicalCodecV1.Encode(Initial(construction));
        byte[][] rows = Fields(Fields(bytes)[5]);
        int maximum = ConstructionSnapshot.MaximumProjects * ConstructionDefinition.MaximumMembers;
        byte[][] excessive = [rows[0], .. Enumerable.Repeat(rows[1], maximum + 1)];
        byte[] changed = ReplaceField(bytes, 5, Frame(excessive));
        Assert.True(changed.Length < ConstructionPhysicalCodecV1.MaximumEncodedByteLength);
        Assert.Throws<FormatException>(() => ConstructionPhysicalCodecV1.Decode(changed, construction));
        Assert.Throws<FormatException>(() => ConstructionPhysicalCodecV1.Decode(
            ReplaceField(bytes, 5, ReplaceField(Fields(bytes)[5], 1, new byte[257])), construction));
        Assert.Throws<FormatException>(() => ConstructionPhysicalCodecV1.Decode(
            new byte[ConstructionPhysicalCodecV1.MaximumEncodedByteLength + 1], construction));
    }

    [Fact]
    public void Every_truncated_prefix_trailing_bytes_and_extra_fields_are_rejected()
    {
        ConstructionSnapshot construction = Construction();
        byte[] bytes = ConstructionPhysicalCodecV1.Encode(Initial(construction));
        for (int length = 0; length < bytes.Length; length++)
        {
            byte[] prefix = bytes[..length];
            Assert.Throws<FormatException>(() => ConstructionPhysicalCodecV1.Decode(prefix, construction));
        }
        Assert.Throws<FormatException>(() => ConstructionPhysicalCodecV1.Decode([.. bytes, 0], construction));
        Assert.Throws<FormatException>(() => ConstructionPhysicalCodecV1.Decode(Frame([.. Fields(bytes), []]), construction));
    }

    private static void DecodeMemberChange(byte[] bytes, ConstructionSnapshot construction, int memberField, byte[] replacement)
    {
        byte[] rows = Fields(bytes)[5];
        byte[] row = ReplaceField(Fields(rows)[1], memberField, replacement);
        _ = ConstructionPhysicalCodecV1.Decode(ReplaceField(bytes, 5, ReplaceField(rows, 1, row)), construction);
    }

    private static ConstructionPhysicalSnapshot Initial(ConstructionSnapshot construction) => new(construction, 0,
        construction.Projects.Where(project => project.Installed is not null).SelectMany(project =>
            project.Definition.Members.Select((_, index) => new ConstructionMemberState(project.Installed!.StructureId,
                (uint)index, 0, 10_000, default, ConstructionMemberDisposition.Standing))));

    private static ConstructionSnapshot Construction(bool installed = true, long siteX = 256)
    {
        ConstructionDefinition definition = ConstructionFixture.Definition();
        EntityId projectId = Id(10);
        EntityId builder = Id(11);
        EntityId completion = Id(12);
        ulong work = installed ? definition.RequiredWork.Value : 0;
        ExposureProgress progress = ExposureProgress.Begin(projectId, definition.WorkRate).Advance(
            LocalExposureDuration.FromMilliseconds(checked(work * definition.WorkRate.Denominator)));
        InstalledStructure? result = installed ? new InstalledStructure(
            EntityId.DeriveSpawned(completion, (ulong)definition.MaterialTransform.Outputs.Count + 1),
            EntityId.DeriveSpawned(completion, (ulong)definition.InstalledOutputIndex + 1), 0, Id(13), completion) : null;
        var project = new ConstructionProject(projectId, definition, builder, builder, Id(14), Id(15), Id(16),
            new(siteX, 0, 256), new(512, 0, 256), [new(builder, new(512, 0, 256), new(768, 0, 256))],
            work, progress.Capture(), result);
        return new ConstructionSnapshot(Id(1), Id(2), work, [definition], [project]);
    }

    private static EntityId Id(ulong value) => new(0x706879736963616c, value);

    // Independent OFC framing keeps malformed-byte tests outside the domain constructors.
    private static byte[][] Fields(byte[] frame)
    {
        Assert.True(frame.AsSpan(0, 4).SequenceEqual("OFC\0"u8));
        Assert.Equal(1U, BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(4)));
        byte[][] fields = new byte[checked((int)BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(8)))][];
        int offset = 12;
        for (int index = 0; index < fields.Length; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(offset)));
            offset = checked(offset + 4);
            fields[index] = frame.AsSpan(offset, length).ToArray();
            offset = checked(offset + length);
        }
        Assert.Equal(frame.Length, offset);
        return fields;
    }

    private static byte[] ReplaceField(byte[] frame, int index, byte[] value)
    {
        byte[][] fields = Fields(frame);
        fields[index] = value;
        return Frame(fields);
    }

    private static byte[] Frame(byte[][] fields)
    {
        byte[] frame = new byte[checked(12 + fields.Sum(field => checked(4 + field.Length)))];
        "OFC\0"u8.CopyTo(frame);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(8), checked((uint)fields.Length));
        int offset = 12;
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(offset), checked((uint)field.Length));
            offset = checked(offset + 4);
            field.CopyTo(frame, offset);
            offset = checked(offset + field.Length);
        }
        return frame;
    }

    private static byte[] Number(ulong value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] Signed(long value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }
}
