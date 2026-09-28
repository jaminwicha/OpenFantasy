using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Care;

public static partial class CareCodecV2
{
    internal static byte[] EncodeUseDefinition(RestorativeUseDefinition value) => Frame("care.use-definition.v1",
        value.DefinitionId.ToByteArray(), value.RemedyDefinitionId.ToByteArray(), value.RemedyVersion.ToByteArray(),
        Number((ulong)value.DeliveryMode), Collection("care.supported-forms", value.SupportedForms.Select(form =>
            Frame("care.form.v1", form.DefinitionId.ToByteArray(), form.Version.ToByteArray()))),
        Number(value.ExplorationMilliseconds));

    internal static byte[] EncodePermissionGrant(CarePermission value) => Frame("care.permission-grant.v1",
        value.PermissionId.ToByteArray(), value.PatientActorId.ToByteArray(), value.HelperActorId.ToByteArray(),
        value.UseDefinitionId.ToByteArray(), value.UseDefinitionVersion.ToByteArray());

    internal static byte[] EncodePermission(CarePermission value) => Frame("care.permission.v1",
        EncodePermissionGrant(value), value.Revocation is { } revoked ? EncodeRevocation(revoked) : []);

    internal static byte[] EncodeRevocation(CarePermissionRevocation value) => Frame("care.permission-revocation.v1",
        value.RevocationId.ToByteArray(), value.CommandId.ToByteArray(), value.PermissionId.ToByteArray(),
        value.PatientActorId.ToByteArray(), Number(value.WorldSequence), Number(value.CommandOrdinal));

    internal static byte[] EncodeUse(RestorativeUse value) => Frame("care.ordinary-use.v1",
        value.UseId.ToByteArray(), value.CommandId.ToByteArray(), value.ActorId.ToByteArray(), value.RecipientActorId.ToByteArray(),
        value.UseDefinitionId.ToByteArray(), value.UseDefinitionVersion.ToByteArray(),
        value.PermissionId?.ToByteArray() ?? [], value.PermissionGrantVersion?.ToByteArray() ?? [],
        value.PermissionRevision is { } permissionRevision ? Number(permissionRevision) : [], value.ConsumptionId.ToByteArray(),
        Number(value.WorldSequence), Number(value.CommandOrdinal), Number(value.PatientRevisionBefore), Number((ulong)value.MaximumHp),
        Number((ulong)value.HpBefore), Number((ulong)value.HpAfter), Number(value.ExplorationMilliseconds));

    private static RestorativeUseDefinition DecodeUseDefinition(byte[] bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumUseDefinitionBytes, 7);
        reader.Tag("care.use-definition.v1"u8);
        EntityId id = reader.Identity(), remedy = reader.Identity();
        var version = Hash(reader.Exact(32));
        var delivery = (RestorativeDeliveryMode)checked((int)reader.Number());
        var forms = ReadEntries(reader.Field(MaximumUseDefinitionBytes), "care.supported-forms"u8,
            RestorativeUseDefinition.MaximumSupportedForms, 128, DecodeForm);
        ulong milliseconds = reader.Number();
        reader.Complete();
        return new(id, remedy, version, delivery, forms, milliseconds);
    }

    private static RestorativeFormBinding DecodeForm(byte[] bytes)
    {
        var reader = new WorldFrameReader(bytes, 128, 3);
        reader.Tag("care.form.v1"u8);
        var value = new RestorativeFormBinding(reader.Identity(), Hash(reader.Exact(32)));
        reader.Complete();
        return value;
    }

    private static CarePermission DecodePermission(byte[] bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumPermissionBytes, 3);
        reader.Tag("care.permission.v1"u8);
        var grant = new WorldFrameReader(reader.Field(512), 512, 6);
        grant.Tag("care.permission-grant.v1"u8);
        EntityId id = grant.Identity(), patient = grant.Identity(), helper = grant.Identity(), definition = grant.Identity();
        var version = Hash(grant.Exact(32));
        grant.Complete();
        var revokedBytes = reader.Field(MaximumRevocationBytes);
        var revoked = revokedBytes.IsEmpty ? null : DecodeRevocation(revokedBytes);
        reader.Complete();
        return new(id, patient, helper, definition, version, revoked);
    }

    private static CarePermissionRevocation DecodeRevocation(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumRevocationBytes, 7);
        reader.Tag("care.permission-revocation.v1"u8);
        var value = new CarePermissionRevocation(reader.Identity(), reader.Identity(), reader.Identity(), reader.Identity(),
            reader.Number(), checked((uint)reader.Number()));
        reader.Complete();
        return value;
    }

    private static RestorativeUse DecodeUse(byte[] bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumUseBytes, 18);
        reader.Tag("care.ordinary-use.v1"u8);
        EntityId id = reader.Identity(), command = reader.Identity(), actor = reader.Identity(), patient = reader.Identity(), definition = reader.Identity();
        var version = Hash(reader.Exact(32));
        var permissionBytes = reader.Field(16);
        EntityId? permission = permissionBytes.IsEmpty ? null : EntityId.FromBytes(permissionBytes);
        var grantBytes = reader.Field(32);
        ContentHash? grant = grantBytes.IsEmpty ? null : Hash(grantBytes);
        var revisionBytes = reader.Field(8);
        if (!revisionBytes.IsEmpty && revisionBytes.Length != 8) throw new FormatException("Permission revision has the wrong length.");
        ulong? permissionRevision = revisionBytes.IsEmpty ? null : BinaryPrimitives.ReadUInt64BigEndian(revisionBytes);
        var value = new RestorativeUse(id, command, actor, patient, definition, version, permission, grant, permissionRevision,
            reader.Identity(), reader.Number(), checked((uint)reader.Number()), reader.Number(), checked((int)reader.Number()),
            checked((int)reader.Number()), checked((int)reader.Number()), reader.Number());
        reader.Complete();
        return value;
    }
}
