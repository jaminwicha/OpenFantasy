using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Care;

public static partial class CareCodecV2
{
    internal static byte[] EncodeUseDefinition(RestorativeUseDefinition value) => EncodeUseDefinition(value, false);

    internal static byte[] EncodeUseDefinition(RestorativeUseDefinition value, bool allowRecovery)
    {
        bool recovery = value.DeliveryMode == RestorativeDeliveryMode.IncapacitatedAssistance;
        if (recovery && !allowRecovery) throw new ArgumentException("Legacy care cannot encode an incapacitated assistance definition.");
        return Frame(recovery ? "care.use-definition.v2" : "care.use-definition.v1",
        value.DefinitionId.ToByteArray(), value.RemedyDefinitionId.ToByteArray(), value.RemedyVersion.ToByteArray(),
        Number((ulong)value.DeliveryMode), Collection("care.supported-forms", value.SupportedForms.Select(form =>
            Frame("care.form.v1", form.DefinitionId.ToByteArray(), form.Version.ToByteArray()))),
        Number(value.ExplorationMilliseconds));
    }

    internal static byte[] EncodePermissionGrant(CarePermission value) => Frame("care.permission-grant.v1",
        value.PermissionId.ToByteArray(), value.PatientActorId.ToByteArray(), value.HelperActorId.ToByteArray(),
        value.UseDefinitionId.ToByteArray(), value.UseDefinitionVersion.ToByteArray());

    internal static byte[] EncodePermission(CarePermission value) => Frame("care.permission.v1",
        EncodePermissionGrant(value), value.Revocation is { } revoked ? EncodeRevocation(revoked) : []);

    internal static byte[] EncodeRevocation(CarePermissionRevocation value) => Frame("care.permission-revocation.v1",
        value.RevocationId.ToByteArray(), value.CommandId.ToByteArray(), value.PermissionId.ToByteArray(),
        value.PatientActorId.ToByteArray(), Number(value.WorldSequence), Number(value.CommandOrdinal));

    internal static byte[] EncodeUse(RestorativeUse value) => EncodeUse(value, false);

    internal static byte[] EncodeUse(RestorativeUse value, bool allowRecovery)
    {
        bool recovery = value.HpBefore == 0;
        if (recovery && !allowRecovery) throw new ArgumentException("Legacy care cannot encode an incapacitated assistance use.");
        return Frame(recovery ? "care.ordinary-use.v2" : "care.ordinary-use.v1",
        value.UseId.ToByteArray(), value.CommandId.ToByteArray(), value.ActorId.ToByteArray(), value.RecipientActorId.ToByteArray(),
        value.UseDefinitionId.ToByteArray(), value.UseDefinitionVersion.ToByteArray(),
        value.PermissionId?.ToByteArray() ?? [], value.PermissionGrantVersion?.ToByteArray() ?? [],
        value.PermissionRevision is { } permissionRevision ? Number(permissionRevision) : [], value.ConsumptionId.ToByteArray(),
        Number(value.WorldSequence), Number(value.CommandOrdinal), Number(value.PatientRevisionBefore), Number((ulong)value.MaximumHp),
        Number((ulong)value.HpBefore), Number((ulong)value.HpAfter), Number(value.ExplorationMilliseconds));
    }

    internal static RestorativeUseDefinition DecodeUseDefinition(byte[] bytes) => DecodeUseDefinition(bytes, false);

    internal static RestorativeUseDefinition DecodeUseDefinition(byte[] bytes, bool allowRecovery)
    {
        var reader = new WorldFrameReader(bytes, MaximumUseDefinitionBytes, 7);
        ReadOnlySpan<byte> tag = reader.Field(64);
        bool recovery = tag.SequenceEqual("care.use-definition.v2"u8);
        if (recovery ? !allowRecovery : !tag.SequenceEqual("care.use-definition.v1"u8))
            throw new FormatException("Unsupported restorative definition interpretation.");
        EntityId id = reader.Identity(), remedy = reader.Identity();
        var version = Hash(reader.Exact(32));
        var delivery = (RestorativeDeliveryMode)checked((int)reader.Number());
        if (recovery != (delivery == RestorativeDeliveryMode.IncapacitatedAssistance))
            throw new FormatException("Recovery delivery requires its explicit definition interpretation.");
        var forms = ReadEntries(reader.Field(MaximumUseDefinitionBytes), "care.supported-forms"u8,
            RestorativeUseDefinition.MaximumSupportedForms, 128, DecodeForm);
        ulong milliseconds = reader.Number();
        reader.Complete();
        return recovery ? RestorativeUseDefinition.CreateIncapacitatedAssistance(id, remedy, version, forms, milliseconds)
            : new(id, remedy, version, delivery, forms, milliseconds);
    }

    private static RestorativeFormBinding DecodeForm(byte[] bytes)
    {
        var reader = new WorldFrameReader(bytes, 128, 3);
        reader.Tag("care.form.v1"u8);
        var value = new RestorativeFormBinding(reader.Identity(), Hash(reader.Exact(32)));
        reader.Complete();
        return value;
    }

    internal static CarePermission DecodePermission(byte[] bytes)
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

    internal static RestorativeUse DecodeUse(byte[] bytes) => DecodeUse(bytes, false);

    internal static RestorativeUse DecodeUse(byte[] bytes, bool allowRecovery)
    {
        var reader = new WorldFrameReader(bytes, MaximumUseBytes, 18);
        ReadOnlySpan<byte> tag = reader.Field(64);
        bool recovery = tag.SequenceEqual("care.ordinary-use.v2"u8);
        if (recovery ? !allowRecovery : !tag.SequenceEqual("care.ordinary-use.v1"u8))
            throw new FormatException("Unsupported restorative use interpretation.");
        EntityId id = reader.Identity(), command = reader.Identity(), actor = reader.Identity(), patient = reader.Identity(), definition = reader.Identity();
        var version = Hash(reader.Exact(32));
        var permissionBytes = reader.Field(16);
        EntityId? permission = permissionBytes.IsEmpty ? null : EntityId.FromBytes(permissionBytes);
        var grantBytes = reader.Field(32);
        ContentHash? grant = grantBytes.IsEmpty ? null : Hash(grantBytes);
        var revisionBytes = reader.Field(8);
        if (!revisionBytes.IsEmpty && revisionBytes.Length != 8) throw new FormatException("Permission revision has the wrong length.");
        ulong? permissionRevision = revisionBytes.IsEmpty ? null : BinaryPrimitives.ReadUInt64BigEndian(revisionBytes);
        EntityId consumption = reader.Identity();
        ulong sequence = reader.Number();
        uint ordinal = checked((uint)reader.Number());
        ulong patientRevision = reader.Number();
        int maximumHp = checked((int)reader.Number()), before = checked((int)reader.Number()), after = checked((int)reader.Number());
        ulong duration = reader.Number();
        var value = recovery
            ? RestorativeUse.CreateIncapacitatedAssistance(id, command, actor, patient, definition, version, permission,
                grant, permissionRevision, consumption, sequence, ordinal, patientRevision, maximumHp, before, after, duration)
            : new RestorativeUse(id, command, actor, patient, definition, version, permission, grant, permissionRevision,
                consumption, sequence, ordinal, patientRevision, maximumHp, before, after, duration);
        reader.Complete();
        return value;
    }
}
