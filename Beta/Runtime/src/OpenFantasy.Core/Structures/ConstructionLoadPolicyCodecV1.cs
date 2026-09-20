using System.Buffers.Binary;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Structures;

/// <summary>Canonical complete authored policy, accepted only under a caller's expected content hash.</summary>
public static class ConstructionLoadPolicyCodecV1
{
    public const int MaximumEncodedByteLength = 4 * 1024 * 1024;
    private const int MaximumTemplateBytes = 128 * 1024;
    private const int MaximumFoundationBytes = 256;
    private const int MaximumMassBytes = 160;
    private const int MaximumRatingBytes = 128;
    private const int MaximumJointBytes = 128;
    private const int MaximumJoints = ConstructionDefinition.MaximumMembers * ConstructionDefinition.MaximumMembers;

    public static byte[] Encode(ConstructionLoadPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        byte[] bytes = Frame("construction.load-policy.v1",
            Frame("structure.terrain-scope", Id(policy.Scope.WorldId), Id(policy.Scope.BranchId), Id(policy.Scope.RealmId)),
            Collection("structure.policy-templates", policy.Templates.Select(EncodeTemplate)),
            Collection("structure.policy-foundations", policy.Foundations.Select(value =>
                Frame("structure.stable-terrain-bearing", Id(value.ElementId), Signed(value.Cell.X), Signed(value.Cell.Y),
                    Signed(value.Cell.Z), Id(value.SourceId), Id(value.Material.DefinitionId), value.Material.Version.ToByteArray(),
                    Number(value.Capacity.Value)))),
            Collection("structure.policy-bodies", policy.Masses.Bodies.Select(value =>
                Frame("structure.body-mass", Id(value.DefinitionId), value.BodyVersion.ToByteArray(), Number(value.Mass.Value)))),
            Collection("structure.policy-items", policy.Masses.SupplementalItems.Select(value =>
                Frame("structure.item-mass", Id(value.Item.DefinitionId), value.Item.Version.ToByteArray(), Number(value.MassPerUnit.Value)))));
        if (bytes.Length > MaximumEncodedByteLength)
        {
            throw new ArgumentException("The construction load policy exceeds its retained byte bound.", nameof(policy));
        }
        return bytes;
    }

    public static ConstructionLoadPolicy Decode(ReadOnlySpan<byte> bytes, ContentHash expectedContentHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedContentHash);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 6);
            if (ContentHash.Compute(bytes) != expectedContentHash)
            {
                throw new FormatException("The construction load policy does not match the expected content hash.");
            }
            reader.Tag("construction.load-policy.v1"u8);
            var scopeReader = new WorldFrameReader(reader.Field(128), 128, 4);
            scopeReader.Tag("structure.terrain-scope"u8);
            var scope = new TerrainScope(scopeReader.Identity(), scopeReader.Identity(), scopeReader.Identity());
            scopeReader.Complete();
            ConstructionBearingTemplate[] templates = ReadTemplates(reader.Field(MaximumEncodedByteLength), cancellationToken);
            StableTerrainBearing[] foundations = ReadFoundations(reader.Field(MaximumEncodedByteLength), cancellationToken);
            StructuralBodyMass[] bodies = ReadBodies(reader.Field(MaximumEncodedByteLength), cancellationToken);
            StructuralItemMass[] items = ReadItems(reader.Field(MaximumEncodedByteLength), cancellationToken);
            reader.Complete();
            var policy = new ConstructionLoadPolicy(scope, templates, foundations, new StructuralMassCatalog(bodies, items));
            if (!Encode(policy).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The construction load policy is not canonical.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            return policy;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The construction load policy violates its catalog or value bounds.", error);
        }
    }

    private static byte[] EncodeTemplate(ConstructionBearingTemplate template) => Frame("construction.bearing-template.v1",
        Id(template.DefinitionId), template.DefinitionVersion.ToByteArray(),
        Collection("structure.member-ratings", template.Members.Select(value => Frame("structure.member-rating",
            Number(value.MemberIndex), Number(value.Capacity.Value), Number(value.MaximumSpanMillimetres), Number(value.MinimumSupportCount)))),
        Collection("structure.member-joints", template.Joints.Select(value => Frame("structure.member-joint",
            Number(value.SupportedMemberIndex), Number(value.SupportingMemberIndex), Number((ulong)value.Kind)))));

    private static ConstructionBearingTemplate[] ReadTemplates(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken)
    {
        var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, ConstructionLoadPolicy.MaximumTemplates + 1);
        reader.Tag("structure.policy-templates"u8);
        List<ConstructionBearingTemplate> values = [];
        while (reader.Remaining != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = new WorldFrameReader(reader.Field(MaximumTemplateBytes), MaximumTemplateBytes, 5);
            item.Tag("construction.bearing-template.v1"u8);
            EntityId definitionId = item.Identity();
            ContentHash version = Hash(ref item);
            StructuralMemberRating[] ratings = ReadRatings(item.Field(MaximumTemplateBytes), cancellationToken);
            StructuralMemberJoint[] joints = ReadJoints(item.Field(MaximumTemplateBytes), cancellationToken);
            item.Complete();
            values.Add(new ConstructionBearingTemplate(definitionId, version, ratings, joints));
        }
        reader.Complete();
        return values.ToArray();
    }

    private static StructuralMemberRating[] ReadRatings(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken)
    {
        var reader = new WorldFrameReader(bytes, MaximumTemplateBytes, ConstructionDefinition.MaximumMembers + 1);
        reader.Tag("structure.member-ratings"u8);
        List<StructuralMemberRating> values = [];
        while (reader.Remaining != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = new WorldFrameReader(reader.Field(MaximumRatingBytes), MaximumRatingBytes, 5);
            item.Tag("structure.member-rating"u8);
            values.Add(new(checked((uint)item.Number()), new(item.Number()), checked((uint)item.Number()), checked((ushort)item.Number())));
            item.Complete();
        }
        reader.Complete();
        return values.ToArray();
    }

    private static StructuralMemberJoint[] ReadJoints(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken)
    {
        var reader = new WorldFrameReader(bytes, MaximumTemplateBytes, MaximumJoints + 1);
        reader.Tag("structure.member-joints"u8);
        List<StructuralMemberJoint> values = [];
        while (reader.Remaining != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = new WorldFrameReader(reader.Field(MaximumJointBytes), MaximumJointBytes, 4);
            item.Tag("structure.member-joint"u8);
            uint supported = checked((uint)item.Number());
            uint supporting = checked((uint)item.Number());
            var kind = (StructuralJointKind)checked((int)item.Number());
            item.Complete();
            values.Add(new(supported, supporting, kind));
        }
        reader.Complete();
        return values.ToArray();
    }

    private static StableTerrainBearing[] ReadFoundations(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken)
    {
        var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, ConstructionLoadPolicy.MaximumFoundations + 1);
        reader.Tag("structure.policy-foundations"u8);
        List<StableTerrainBearing> values = [];
        while (reader.Remaining != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = new WorldFrameReader(reader.Field(MaximumFoundationBytes), MaximumFoundationBytes, 9);
            item.Tag("structure.stable-terrain-bearing"u8);
            EntityId id = item.Identity();
            var cell = new CellCoordinate(item.Signed(), item.Signed(), item.Signed());
            EntityId source = item.Identity();
            var material = new CountedItemDefinition(item.Identity(), Hash(ref item));
            values.Add(new(id, cell, source, material, new(item.Number())));
            item.Complete();
        }
        reader.Complete();
        return values.ToArray();
    }

    private static StructuralBodyMass[] ReadBodies(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken)
    {
        var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, StructuralMassCatalog.MaximumBodyDefinitions + 1);
        reader.Tag("structure.policy-bodies"u8);
        List<StructuralBodyMass> values = [];
        while (reader.Remaining != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = new WorldFrameReader(reader.Field(MaximumMassBytes), MaximumMassBytes, 4);
            item.Tag("structure.body-mass"u8);
            values.Add(new(item.Identity(), Hash(ref item), new(item.Number())));
            item.Complete();
        }
        reader.Complete();
        return values.ToArray();
    }

    private static StructuralItemMass[] ReadItems(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken)
    {
        var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, StructuralMassCatalog.MaximumSupplementalItems + 1);
        reader.Tag("structure.policy-items"u8);
        List<StructuralItemMass> values = [];
        while (reader.Remaining != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = new WorldFrameReader(reader.Field(MaximumMassBytes), MaximumMassBytes, 4);
            item.Tag("structure.item-mass"u8);
            values.Add(new(new(item.Identity(), Hash(ref item)), new(item.Number())));
            item.Complete();
        }
        reader.Complete();
        return values.ToArray();
    }

    private static byte[] Collection(string tag, IEnumerable<byte[]> rows) => Frame(tag, rows.Select(value => (ReadOnlyMemory<byte>)value).ToArray());
    private static byte[] Frame(string tag, params ReadOnlyMemory<byte>[] values) => CanonicalEncodingV1.Frame(tag, values);
    private static byte[] Id(EntityId value) => value.ToByteArray();
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    private static ContentHash Hash(ref WorldFrameReader reader) => new(Convert.ToHexString(reader.Exact(32)).ToLowerInvariant());

    private static byte[] Signed(long value)
    {
        byte[] bytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }
}
