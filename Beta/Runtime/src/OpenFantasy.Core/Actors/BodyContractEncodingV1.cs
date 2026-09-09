using System.Buffers.Binary;

namespace OpenFantasy.Core.Actors;

/// <summary>Canonical hashing bytes for validated definitions, conditions and reference bindings.</summary>
public static class BodyContractEncodingV1
{
    public const ulong ContractVersion = 1;

    public static byte[] EncodeDefinition(BodyDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return CanonicalEncodingV1.Frame("actor.body-definition", Number(ContractVersion),
            definition.DefinitionId.ToByteArray(), Number((ulong)definition.Nature),
            CanonicalEncodingV1.Frame("actor.body-clearance", Signed(definition.ClearanceDimensions.WidthSubunits),
                Signed(definition.ClearanceDimensions.DepthSubunits), Signed(definition.ClearanceDimensions.HeightSubunits)),
            CanonicalEncodingV1.Frame("actor.body-parts", definition.Parts.Select(part => (ReadOnlyMemory<byte>)EncodePart(part)).ToArray()),
            CanonicalEncodingV1.Frame("actor.locomotion-requirements", definition.Locomotion.Select(requirement =>
                (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("actor.locomotion", Number((ulong)requirement.Mode),
                    Number((ulong)requirement.MinimumFunctionalParts))).ToArray()));
    }

    public static byte[] EncodeCondition(BodyConditionSnapshot condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return CanonicalEncodingV1.Frame("actor.body-condition", Number(ContractVersion),
            condition.FormInstanceId.ToByteArray(), condition.Definition.DefinitionId.ToByteArray(),
            condition.Definition.Version.ToByteArray(), CanonicalEncodingV1.Frame("actor.body-injuries",
                condition.Injuries.Select(injury => (ReadOnlyMemory<byte>)EncodeInjury(injury)).ToArray()));
    }

    public static byte[] EncodeBinding(ActorFormBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return CanonicalEncodingV1.Frame("actor.form-binding", Number(ContractVersion), binding.ActorId.ToByteArray(),
            binding.FormInstanceId.ToByteArray(), binding.DefinitionId.ToByteArray(), binding.DefinitionVersion.ToByteArray(),
            Number((ulong)binding.Kind), binding.PhysicalBodyId?.ToByteArray() ?? [], binding.TetherAnchorId?.ToByteArray() ?? []);
    }

    private static byte[] EncodePart(BodyPartDefinition part) => CanonicalEncodingV1.Frame("actor.body-part",
        Text(part.Slot.Value), part.ParentSlot is null ? [] : Text(part.ParentSlot.Value),
        Signed(part.Dimensions.WidthSubunits), Signed(part.Dimensions.DepthSubunits), Signed(part.Dimensions.HeightSubunits),
        CanonicalEncodingV1.Frame("actor.body-functions", part.Functions.Select(function => (ReadOnlyMemory<byte>)Number((ulong)function)).ToArray()));

    private static byte[] EncodeInjury(BodyInjury injury) => CanonicalEncodingV1.Frame("actor.body-injury",
        injury.InjuryId.ToByteArray(), injury.CauseId.ToByteArray(), Text(injury.Slot.Value), Number((ulong)injury.Kind),
        injury.LostFunction is BodyFunction function ? Number((ulong)function) : []);

    private static byte[] Text(string value) => CanonicalEncodingV1.Text(value, nameof(value));
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);

    private static byte[] Signed(long value)
    {
        byte[] bytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }
}
