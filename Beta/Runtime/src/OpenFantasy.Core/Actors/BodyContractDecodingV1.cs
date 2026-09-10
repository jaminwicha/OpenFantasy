using System.Buffers.Binary;

namespace OpenFantasy.Core.Actors;

/// <summary>Strict restoration of existing canonical anatomy, condition and form-reference bytes.</summary>
public static class BodyContractDecodingV1
{
    public const int MaximumDefinitionByteLength = 24_576;
    public const int MaximumConditionByteLength = 49_152;
    public const int MaximumBindingByteLength = 256;
    private const int MaximumClearanceByteLength = 80;
    private const int MaximumPartsByteLength = 24_000;
    private const int MaximumPartByteLength = 384;
    private const int MaximumFunctionsByteLength = 160;
    private const int MaximumLocomotionByteLength = 384;
    private const int MaximumLocomotionRequirementByteLength = 64;
    private const int MaximumInjuriesByteLength = 44_000;
    private const int MaximumInjuryByteLength = 192;
    private const int KnownFunctionCount = 10;
    private const int KnownLocomotionModeCount = 5;

    public static BodyDefinition DecodeDefinition(ReadOnlySpan<byte> bytes)
    {
        try
        {
            BodyContractFrameReader reader = new(bytes, MaximumDefinitionByteLength, 7, "actor.body-definition"u8);
            reader.ExpectVersion();
            EntityId identity = reader.ReadIdentity();
            ulong natureValue = reader.ReadUnsigned64();
            if (natureValue is not (1 or 2))
            {
                throw new FormatException("The body nature is unsupported.");
            }

            BodyDimensions clearance = ReadClearance(reader.ReadField(MaximumClearanceByteLength));
            List<BodyPartDefinition> parts = ReadParts(reader.ReadField(MaximumPartsByteLength));
            List<LocomotionRequirement> locomotion = ReadLocomotion(reader.ReadField(MaximumLocomotionByteLength));
            reader.Complete();
            return new BodyDefinition(identity, (BodyNature)natureValue, clearance, parts, locomotion);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException("The encoded body definition violates its value or topology invariants.", exception);
        }
    }

    public static BodyConditionSnapshot DecodeCondition(ReadOnlySpan<byte> bytes, BodyDefinition exactDefinition)
    {
        ArgumentNullException.ThrowIfNull(exactDefinition);
        try
        {
            BodyContractFrameReader reader = new(bytes, MaximumConditionByteLength, 6, "actor.body-condition"u8);
            reader.ExpectVersion();
            EntityId form = reader.ReadIdentity();
            RequireExactDefinition(ref reader, exactDefinition);
            List<BodyInjury> injuries = ReadInjuries(reader.ReadField(MaximumInjuriesByteLength));
            reader.Complete();
            return new BodyConditionSnapshot(form, exactDefinition, injuries);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException("The encoded condition violates body or injury invariants.", exception);
        }
    }

    public static ActorFormBinding DecodeBinding(ReadOnlySpan<byte> bytes, BodyDefinition exactDefinition)
    {
        ArgumentNullException.ThrowIfNull(exactDefinition);
        try
        {
            BodyContractFrameReader reader = new(bytes, MaximumBindingByteLength, 9, "actor.form-binding"u8);
            reader.ExpectVersion();
            EntityId actor = reader.ReadIdentity();
            EntityId form = reader.ReadIdentity();
            RequireExactDefinition(ref reader, exactDefinition);
            ulong kind = reader.ReadUnsigned64();
            EntityId? body = reader.ReadOptionalIdentity();
            EntityId? tether = reader.ReadOptionalIdentity();
            reader.Complete();
            return kind switch
            {
                1 when body.HasValue && !tether.HasValue => ActorFormBinding.Corporeal(actor, form, body.Value, exactDefinition),
                2 when !body.HasValue && !tether.HasValue => ActorFormBinding.IndependentSpirit(actor, form, exactDefinition),
                3 when !body.HasValue && tether.HasValue => ActorFormBinding.TetheredProjection(actor, form, tether.Value, exactDefinition),
                _ => throw new FormatException("The form kind and physical/tether fields are incompatible.")
            };
        }
        catch (ArgumentException exception)
        {
            throw new FormatException("The encoded binding violates body nature or instance identity invariants.", exception);
        }
    }

    private static void RequireExactDefinition(ref BodyContractFrameReader reader, BodyDefinition definition)
    {
        EntityId identity = reader.ReadIdentity();
        ReadOnlySpan<byte> version = reader.ReadExactField(32);
        if (identity != definition.DefinitionId || !version.SequenceEqual(definition.Version.ToByteArray()))
        {
            throw new FormatException("The encoded record requires a different exact body definition.");
        }
    }

    private static BodyDimensions ReadClearance(ReadOnlySpan<byte> bytes)
    {
        BodyContractFrameReader reader = new(bytes, MaximumClearanceByteLength, 4, "actor.body-clearance"u8);
        var dimensions = new BodyDimensions(reader.ReadSigned64(), reader.ReadSigned64(), reader.ReadSigned64());
        reader.Complete();
        return dimensions;
    }

    private static List<BodyPartDefinition> ReadParts(ReadOnlySpan<byte> bytes)
    {
        BodyContractFrameReader reader = new(bytes, MaximumPartsByteLength, BodyDefinition.MaximumPartCount + 1, "actor.body-parts"u8);
        List<BodyPartDefinition> parts = [];
        while (reader.RemainingFields > 0)
        {
            BodyContractFrameReader partReader = new(reader.ReadField(MaximumPartByteLength), MaximumPartByteLength, 7, "actor.body-part"u8);
            BodySlotKey slot = partReader.ReadSlot();
            BodySlotKey? parent = partReader.ReadOptionalSlot();
            var dimensions = new BodyDimensions(partReader.ReadSigned64(), partReader.ReadSigned64(), partReader.ReadSigned64());
            List<BodyFunction> functions = ReadFunctions(partReader.ReadField(MaximumFunctionsByteLength));
            partReader.Complete();
            if (parts.Count > 0 && StringComparer.Ordinal.Compare(parts[^1].Slot.Value, slot.Value) >= 0)
            {
                throw new FormatException("Body parts must be unique and in canonical slot order.");
            }

            parts.Add(new BodyPartDefinition(slot, parent, dimensions, functions));
        }

        reader.Complete();
        return parts;
    }

    private static List<BodyFunction> ReadFunctions(ReadOnlySpan<byte> bytes)
    {
        BodyContractFrameReader reader = new(bytes, MaximumFunctionsByteLength, KnownFunctionCount + 1, "actor.body-functions"u8);
        List<BodyFunction> functions = [];
        while (reader.RemainingFields > 0)
        {
            BodyFunction function = KnownFunction(reader.ReadUnsigned64());
            if (functions.Count > 0 && (ulong)functions[^1] >= (ulong)function)
            {
                throw new FormatException("Body functions must be unique and in canonical numeric order.");
            }

            functions.Add(function);
        }

        reader.Complete();
        return functions;
    }

    private static List<LocomotionRequirement> ReadLocomotion(ReadOnlySpan<byte> bytes)
    {
        BodyContractFrameReader reader = new(bytes, MaximumLocomotionByteLength, KnownLocomotionModeCount + 1,
            "actor.locomotion-requirements"u8);
        List<LocomotionRequirement> requirements = [];
        while (reader.RemainingFields > 0)
        {
            BodyContractFrameReader entry = new(reader.ReadField(MaximumLocomotionRequirementByteLength),
                MaximumLocomotionRequirementByteLength, 3, "actor.locomotion"u8);
            ulong mode = entry.ReadUnsigned64();
            ulong count = entry.ReadUnsigned64();
            entry.Complete();
            if (mode is < 1 or > KnownLocomotionModeCount || count is < 1 or > BodyDefinition.MaximumPartCount)
            {
                throw new FormatException("The locomotion mode or required part count is outside its supported range.");
            }

            if (requirements.Count > 0 && (ulong)requirements[^1].Mode >= mode)
            {
                throw new FormatException("Locomotion modes must be unique and in canonical numeric order.");
            }

            requirements.Add(new LocomotionRequirement((LocomotionMode)mode, (int)count));
        }

        reader.Complete();
        return requirements;
    }

    private static List<BodyInjury> ReadInjuries(ReadOnlySpan<byte> bytes)
    {
        BodyContractFrameReader reader = new(bytes, MaximumInjuriesByteLength, BodyConditionSnapshot.MaximumInjuryCount + 1,
            "actor.body-injuries"u8);
        List<BodyInjury> injuries = [];
        while (reader.RemainingFields > 0)
        {
            BodyContractFrameReader entry = new(reader.ReadField(MaximumInjuryByteLength), MaximumInjuryByteLength, 6,
                "actor.body-injury"u8);
            EntityId injuryId = entry.ReadIdentity();
            EntityId cause = entry.ReadIdentity();
            BodySlotKey slot = entry.ReadSlot();
            ulong kind = entry.ReadUnsigned64();
            ReadOnlySpan<byte> lostBytes = entry.ReadField(sizeof(ulong));
            BodyFunction? lostFunction = lostBytes.Length switch
            {
                0 => null,
                sizeof(ulong) => KnownFunction(BinaryPrimitives.ReadUInt64BigEndian(lostBytes)),
                _ => throw new FormatException("An optional lost function must be absent or eight bytes.")
            };
            entry.Complete();
            if (kind is < 1 or > 3)
            {
                throw new FormatException("The injury kind is unsupported.");
            }

            if (injuries.Count > 0 && injuries[^1].InjuryId >= injuryId)
            {
                throw new FormatException("Injuries must be unique and in canonical identity order.");
            }

            injuries.Add(new BodyInjury(injuryId, cause, slot, (BodyInjuryKind)kind, lostFunction));
        }

        reader.Complete();
        return injuries;
    }

    private static BodyFunction KnownFunction(ulong value) => value is >= 1 and <= KnownFunctionCount
        ? (BodyFunction)value : throw new FormatException("The anatomical function is unsupported.");
}
