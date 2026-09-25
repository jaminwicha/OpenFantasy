using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using OpenFantasy.Core.Actors;
using static OpenFantasy.Core.Tests.Actors.BodyDecodingTestFrames;

namespace OpenFantasy.Core.Tests.Actors;

public sealed class BodyContractDecodingTests
{
    [Fact]
    public void All_independent_body_definitions_restore_exact_bytes_and_versions()
    {
        foreach (JsonElement record in ActorFixture.Read().GetProperty("definitions").EnumerateArray())
        {
            byte[] expected = Bytes(record);
            BodyDefinition restored = BodyContractDecodingV1.DecodeDefinition(expected);
            Assert.Equal(expected, BodyContractEncodingV1.EncodeDefinition(restored));
            Assert.Equal(record.GetProperty("versionSha256").GetString(), restored.Version.Value);
            Assert.Equal(record.GetProperty("parts").GetArrayLength(), restored.Parts.Count);
        }
    }

    [Fact]
    public void Independent_injuries_restore_actual_function_loss_missing_subtrees_and_scars()
    {
        foreach (JsonElement record in ActorFixture.Read().GetProperty("conditions").EnumerateArray())
        {
            BodyDefinition definition = DefinitionFor(record);
            BodyConditionSnapshot restored = BodyContractDecodingV1.DecodeCondition(Bytes(record), definition);
            Assert.Same(definition, restored.Definition);
            Assert.Equal(Bytes(record), BodyContractEncodingV1.EncodeCondition(restored));
            Assert.Equal(record.GetProperty("sha256").GetString(), restored.ComputeContentHash().Value);
            foreach (BodyFunction function in Enum.GetValues<BodyFunction>())
            {
                if (function is BodyFunction.Vocalization or BodyFunction.NaturalWeapon)
                {
                    Assert.Empty(restored.FunctionalSlots(function));
                    continue;
                }
                string[] expected = record.GetProperty("functionalSlots").GetProperty(((int)function).ToString(CultureInfo.InvariantCulture))
                    .EnumerateArray().Select(value => value.GetString()!).ToArray();
                Assert.Equal(expected, restored.FunctionalSlots(function).Select(slot => slot.Value));
            }
            int[] modes = record.GetProperty("locomotion").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            foreach (LocomotionMode mode in Enum.GetValues<LocomotionMode>())
            {
                Assert.Equal(modes.Contains((int)mode), restored.CanLocomote(mode));
            }
        }
    }

    [Fact]
    public void Independent_corporeal_spirit_and_tethered_bindings_restore_exact_optional_identities()
    {
        foreach (JsonElement record in ActorFixture.Read().GetProperty("bindings").EnumerateArray())
        {
            ActorFormBinding restored = BodyContractDecodingV1.DecodeBinding(Bytes(record), DefinitionFor(record));
            Assert.Equal(Bytes(record), BodyContractEncodingV1.EncodeBinding(restored));
            Assert.Equal(record.GetProperty("sha256").GetString(), restored.ComputeContentHash().Value);
            Assert.Equal(EntityId.Parse(record.GetProperty("actorId").GetString()), restored.ActorId);
            Assert.Equal((ActorFormKind)record.GetProperty("kind").GetInt32(), restored.Kind);
            Assert.Equal(OptionalIdentity(record, "physicalBodyId"), restored.PhysicalBodyId);
            Assert.Equal(OptionalIdentity(record, "tetherAnchorId"), restored.TetherAnchorId);
        }
    }

    [Fact]
    public void Conditions_and_bindings_require_definition_identity_and_complete_exact_version()
    {
        foreach (string family in new[] { "conditions", "bindings" })
        {
            JsonElement record = ActorFixture.Read().GetProperty(family)[0];
            BodyDefinition definition = DefinitionFor(record);
            var sameIdDifferentVersion = new BodyDefinition(definition.DefinitionId, definition.Nature,
                new BodyDimensions(definition.ClearanceDimensions.WidthSubunits + 1,
                    definition.ClearanceDimensions.DepthSubunits, definition.ClearanceDimensions.HeightSubunits),
                definition.Parts, definition.Locomotion);
            var wrongIdSameAnatomy = new BodyDefinition(new EntityId(99, 99), definition.Nature,
                definition.ClearanceDimensions, definition.Parts, definition.Locomotion);
            foreach (BodyDefinition mismatch in new[] { sameIdDifferentVersion, wrongIdSameAnatomy })
            {
                Assert.Throws<FormatException>(() => DecodePinned(family, Bytes(record), mismatch));
            }
            int hashIndex = family == "conditions" ? 4 : 5;
            byte[] hash = Fields(Bytes(record))[hashIndex];
            Assert.Throws<FormatException>(() => DecodePinned(family, Replace(Bytes(record), hashIndex, hash[..31]), definition));
            hash[0] ^= 1;
            Assert.Throws<FormatException>(() => DecodePinned(family, Replace(Bytes(record), hashIndex, hash), definition));
        }
    }

    [Fact]
    public void Part_order_duplicates_cycles_missing_parents_and_multiple_roots_reject()
    {
        byte[] original = Bytes(ActorFixture.Read().GetProperty("definitions")[0]);
        List<byte[]> parts = Fields(Fields(original)[5]);
        List<byte[]> reversed = [parts[0], .. parts.Skip(1).Reverse()];
        AssertBadDefinition(Replace(original, 5, Frame(reversed)));
        AssertBadDefinition(Replace(original, 5, Frame([.. parts, parts[1]])));
        foreach (byte[] parent in new[] { Text("missing-slot"), Text("head"), Array.Empty<byte>() })
        {
            List<byte[]> changed = [.. parts];
            changed[1] = Replace(changed[1], 2, parent);
            AssertBadDefinition(Replace(original, 5, Frame(changed)));
        }
        AssertBadDefinition(Replace(original, 5, Frame([parts[0]])));
        AssertBadDefinition(Replace(original, 5, Frame([parts[0], .. Enumerable.Repeat(parts[1], BodyDefinition.MaximumPartCount + 1)])));
    }

    [Fact]
    public void Function_and_locomotion_order_enums_and_anatomical_feasibility_reject()
    {
        byte[] original = Bytes(ActorFixture.Read().GetProperty("definitions")[0]);
        List<byte[]> parts = Fields(Fields(original)[5]);
        byte[] functionTag = Text("actor.body-functions");
        foreach (byte[] functions in new[] { Frame([functionTag, Number(2), Number(2)]),
            Frame([functionTag, Number(2), Number(1)]), Frame([functionTag, Number(12)]),
            Frame([functionTag, .. Enumerable.Repeat(Number(1), 11)]) })
        {
            List<byte[]> changed = [.. parts];
            changed[1] = Replace(changed[1], 6, functions);
            AssertBadDefinition(Replace(original, 5, Frame(changed)));
        }
        byte[] locomotionTag = Text("actor.locomotion-requirements");
        byte[] Row(ulong mode, ulong count) => Frame([Text("actor.locomotion"), Number(mode), Number(count)]);
        foreach (byte[] locomotion in new[] { Frame([locomotionTag, Row(6, 1)]),
            Frame([locomotionTag, Row(1, 0)]), Frame([locomotionTag, Row(1, 65)]),
            Frame([locomotionTag, Row(2, 1)]), Frame([locomotionTag, Row(1, 1), Row(1, 1)]),
            Frame([locomotionTag, Row(2, 1), Row(1, 1)]) })
        {
            AssertBadDefinition(Replace(original, 6, locomotion));
        }
        AssertBadDefinition(Replace(original, 3, Number(3)));
        AssertBadDefinition(Replace(original, 3, Number(2)));
    }

    [Fact]
    public void Invalid_slot_encoding_and_nonpositive_dimensions_reject()
    {
        byte[] original = Bytes(ActorFixture.Read().GetProperty("definitions")[0]);
        foreach (byte[] invalidSlot in new[] { Array.Empty<byte>(), Text("Head"), Text(new string('a', 65)),
            Text("héad"), new byte[] { 0xc0, 0x80 }, new byte[] { 0xff } })
        {
            List<byte[]> parts = Fields(Fields(original)[5]);
            parts[1] = Replace(parts[1], 1, invalidSlot);
            AssertBadDefinition(Replace(original, 5, Frame(parts)));
        }
        foreach (ulong dimension in new[] { 0UL, ulong.MaxValue })
        {
            byte[] clearance = Replace(Fields(original)[4], 1, Number(dimension));
            AssertBadDefinition(Replace(original, 4, clearance));
            List<byte[]> parts = Fields(Fields(original)[5]);
            parts[1] = Replace(parts[1], 3, Number(dimension));
            AssertBadDefinition(Replace(original, 5, Frame(parts)));
        }
    }

    [Fact]
    public void Injury_duplicates_order_unknown_slots_and_invalid_loss_metadata_reject()
    {
        JsonElement record = ActorFixture.Read().GetProperty("conditions").EnumerateArray()
            .First(value => value.GetProperty("injuries").GetArrayLength() > 0);
        byte[] original = Bytes(record);
        BodyDefinition definition = DefinitionFor(record);
        List<byte[]> injuries = Fields(Fields(original)[5]);
        byte[] injury = injuries[1];
        byte[] second = Replace(injury, 1, new EntityId(999, 999).ToByteArray());
        AssertBadCondition(Replace(original, 5, Frame([injuries[0], injury, injury])), definition);
        AssertBadCondition(Replace(original, 5, Frame([injuries[0], second, injury])), definition);
        AssertBadCondition(Replace(original, 5, Frame([injuries[0], .. Enumerable.Repeat(injury, 257)])), definition);
        foreach (byte[] invalid in new[] { Replace(injury, 3, Text("missing-slot")),
            Replace(injury, 4, Number(4)), Replace(Replace(injury, 4, Number(3)), 5, []),
            Replace(Replace(injury, 4, Number(1)), 5, Number(1)),
            Replace(Replace(injury, 4, Number(3)), 5, Number(11)),
            Replace(injury, 5, new byte[] { 1 }) })
        {
            AssertBadCondition(Replace(original, 5, Frame([injuries[0], invalid])), definition);
        }
        byte[] nonexistentFunction = Replace(Replace(Replace(injury, 3, Text("torso")), 4, Number(3)), 5, Number(1));
        AssertBadCondition(Replace(original, 5, Frame([injuries[0], nonexistentFunction])), definition);
    }

    [Fact]
    public void Binding_optional_fields_kind_nature_and_instance_aliases_reject()
    {
        foreach (JsonElement record in ActorFixture.Read().GetProperty("bindings").EnumerateArray())
        {
            byte[] original = Bytes(record);
            BodyDefinition definition = DefinitionFor(record);
            AssertBadBinding(Replace(original, 6, Number(4)), definition);
            AssertBadBinding(Replace(original, 3, Fields(original)[2]), definition);
            AssertBadBinding(Replace(original, 7, new byte[] { 1 }), definition);
            AssertBadBinding(Replace(original, 8, new byte[] { 1 }), definition);
            AssertBadBinding(Replace(Replace(original, 7, new EntityId(90, 90).ToByteArray()), 8,
                new EntityId(91, 91).ToByteArray()), definition);
            if (record.GetProperty("kind").GetInt32() == 1)
            {
                AssertBadBinding(Replace(original, 7, []), definition);
                AssertBadBinding(Replace(original, 7, Fields(original)[2]), definition);
                AssertBadBinding(Replace(Replace(original, 6, Number(2)), 7, []), definition);
            }
            else
            {
                AssertBadBinding(Replace(Replace(Replace(original, 6, Number(1)), 7,
                    new EntityId(90, 90).ToByteArray()), 8, []), definition);
            }
        }
    }

    [Fact]
    public void Maximum_valid_part_slots_dimensions_functions_and_injury_counts_restore()
    {
        BodySlotKey root = new("a" + new string('r', 63));
        BodyFunction[] functions = Enum.GetValues<BodyFunction>()
            .Where(value => value is not (BodyFunction.SpectralMotion or BodyFunction.SpectralPerception)).ToArray();
        BodyDimensions dimensions = new(long.MaxValue, long.MaxValue, long.MaxValue);
        BodyPartDefinition[] parts = [new(root, null, dimensions, functions),
            .. Enumerable.Range(1, 63).Select(index => new BodyPartDefinition(
                new BodySlotKey("b" + index.ToString("D2", CultureInfo.InvariantCulture) + new string('p', 61)), root, dimensions, functions))];
        var definition = new BodyDefinition(new EntityId(0, 991), BodyNature.Corporeal, dimensions, parts,
            [new(LocomotionMode.Ground, 64), new(LocomotionMode.Flight, 64), new(LocomotionMode.Swimming, 64), new(LocomotionMode.Slithering, 64)]);
        byte[] encoded = BodyContractEncodingV1.EncodeDefinition(definition);
        Assert.True(encoded.Length <= BodyContractDecodingV1.MaximumDefinitionByteLength);
        Assert.Equal(encoded, BodyContractEncodingV1.EncodeDefinition(BodyContractDecodingV1.DecodeDefinition(encoded)));
        var condition = new BodyConditionSnapshot(new EntityId(0, 992), definition,
            Enumerable.Range(0, 256).Select(index => new BodyInjury(new EntityId(1, (ulong)index), new EntityId(2, 0), root,
                BodyInjuryKind.FunctionLoss, BodyFunction.Sight)));
        byte[] conditionBytes = BodyContractEncodingV1.EncodeCondition(condition);
        BodyConditionSnapshot restored = BodyContractDecodingV1.DecodeCondition(conditionBytes, definition);
        Assert.Equal(conditionBytes, BodyContractEncodingV1.EncodeCondition(restored));
        Assert.Equal(256, restored.Injuries.Count);
        Assert.Equal(63, restored.FunctionalSlots(BodyFunction.Sight).Count);
    }

    [Fact]
    public void Present_zero_identity_remains_distinct_from_absent_optional_body()
    {
        BodyDefinition definition = ActorFixture.Definition("humanoid-candidate");
        ActorFormBinding binding = ActorFormBinding.Corporeal(new EntityId(0, 800), new EntityId(0, 801), default, definition);
        ActorFormBinding restored = BodyContractDecodingV1.DecodeBinding(BodyContractEncodingV1.EncodeBinding(binding), definition);
        Assert.True(restored.PhysicalBodyId.HasValue);
        Assert.Equal(default(EntityId), restored.PhysicalBodyId.Value);
    }

    [Fact]
    public void All_prefixes_top_level_corruptions_and_oversized_frames_reject()
    {
        foreach (string family in new[] { "definitions", "conditions", "bindings" })
        {
            JsonElement record = ActorFixture.Read().GetProperty(family)[0];
            byte[] original = Bytes(record);
            BodyDefinition definition = family == "definitions" ? ActorFixture.Definition(record) : DefinitionFor(record);
            for (int length = 0; length < original.Length; length++)
            {
                int prefix = length;
                Assert.Throws<FormatException>(() => DecodeAny(family, original[..prefix], definition));
            }
            byte[] badFrameVersion = (byte[])original.Clone();
            BinaryPrimitives.WriteUInt32BigEndian(badFrameVersion.AsSpan(4), 2);
            byte[] giantLength = (byte[])original.Clone();
            BinaryPrimitives.WriteUInt32BigEndian(giantLength.AsSpan(12), uint.MaxValue);
            foreach (byte[] malformed in new[] { badFrameVersion, giantLength, [.. original, 0],
                Replace(original, 1, Number(2)), Frame([.. Fields(original), Array.Empty<byte>()]),
                Replace(original, 0, Text("actor.unknown")), Replace(original, 2, new byte[15]) })
            {
                Assert.Throws<FormatException>(() => DecodeAny(family, malformed, definition));
            }
            int maximum = family switch { "definitions" => BodyContractDecodingV1.MaximumDefinitionByteLength,
                "conditions" => BodyContractDecodingV1.MaximumConditionByteLength, _ => BodyContractDecodingV1.MaximumBindingByteLength };
            Assert.Throws<FormatException>(() => DecodeAny(family, new byte[maximum + 1], definition));
        }
    }

    [Fact]
    public void Restored_values_own_input_bytes_and_null_definition_is_a_caller_error()
    {
        byte[] bytes = Bytes(ActorFixture.Read().GetProperty("definitions")[0]);
        BodyDefinition definition = BodyContractDecodingV1.DecodeDefinition(bytes);
        byte[] before = BodyContractEncodingV1.EncodeDefinition(definition);
        Array.Fill(bytes, (byte)0);
        Assert.Equal(before, BodyContractEncodingV1.EncodeDefinition(definition));
        Assert.Throws<NotSupportedException>(() => ((IList<BodyPartDefinition>)definition.Parts).Clear());
        Assert.Throws<ArgumentNullException>(() => BodyContractDecodingV1.DecodeCondition([], null!));
        Assert.Throws<ArgumentNullException>(() => BodyContractDecodingV1.DecodeBinding([], null!));
    }

    [Fact]
    public void Nested_trailing_bytes_and_unknown_row_tags_cannot_be_normalized_on_restore()
    {
        byte[] definition = Bytes(ActorFixture.Read().GetProperty("definitions")[0]);
        foreach (int index in new[] { 4, 5, 6 })
        {
            AssertBadDefinition(Replace(definition, index, [.. Fields(definition)[index], 0]));
        }
        List<byte[]> parts = Fields(Fields(definition)[5]);
        parts[1] = Replace(parts[1], 6, [.. Fields(parts[1])[6], 0]);
        AssertBadDefinition(Replace(definition, 5, Frame(parts)));
        parts = Fields(Fields(definition)[5]);
        parts[1] = Replace(parts[1], 0, Text("actor.unknown-part"));
        AssertBadDefinition(Replace(definition, 5, Frame(parts)));

        JsonElement record = ActorFixture.Read().GetProperty("conditions").EnumerateArray()
            .First(value => value.GetProperty("injuries").GetArrayLength() > 0);
        byte[] condition = Bytes(record);
        AssertBadCondition(Replace(condition, 5, [.. Fields(condition)[5], 0]), DefinitionFor(record));
        List<byte[]> injuries = Fields(Fields(condition)[5]);
        injuries[1] = [.. injuries[1], 0];
        AssertBadCondition(Replace(condition, 5, Frame(injuries)), DefinitionFor(record));
    }

    [Fact]
    public void Spectral_definition_rejects_physical_scars_missing_parts_and_absent_anatomical_functions()
    {
        JsonElement definitionRecord = ActorFixture.Read().GetProperty("definitions").EnumerateArray()
            .First(value => value.GetProperty("nature").GetInt32() == 2);
        BodyDefinition definition = ActorFixture.Definition(definitionRecord);
        byte[] condition = Bytes(ActorFixture.Read().GetProperty("conditions")[0]);
        condition = Replace(Replace(condition, 3, definition.DefinitionId.ToByteArray()), 4, definition.Version.ToByteArray());
        foreach ((ulong kind, byte[] lostFunction) in new[] { (1UL, Array.Empty<byte>()),
            (2UL, Array.Empty<byte>()), (3UL, Number(1)) })
        {
            byte[] injury = Frame([Text("actor.body-injury"), new EntityId(0, 900).ToByteArray(),
                new EntityId(0, 901).ToByteArray(), Text(definition.Parts[0].Slot.Value), Number(kind), lostFunction]);
            AssertBadCondition(Replace(condition, 5, Frame([Text("actor.body-injuries"), injury])), definition);
        }
    }

    [Fact]
    public void Explicit_vocalization_restores_and_is_independent_of_hearing_with_real_injury_suppression()
    {
        BodyDefinition definition = ActorFixture.Body([
            ActorFixture.Part("body"), ActorFixture.Part("ears", "body", BodyFunction.Hearing),
            ActorFixture.Part("jaw", "body"), ActorFixture.Part("voice", "jaw", BodyFunction.Vocalization)]);
        definition = BodyContractDecodingV1.DecodeDefinition(BodyContractEncodingV1.EncodeDefinition(definition));
        var intact = new BodyConditionSnapshot(new EntityId(0, 800), definition, []);
        var deaf = new BodyConditionSnapshot(intact.FormInstanceId, definition,
            [new(new EntityId(0, 1), new EntityId(0, 2), new BodySlotKey("ears"), BodyInjuryKind.FunctionLoss, BodyFunction.Hearing)]);
        var voiceLost = new BodyConditionSnapshot(intact.FormInstanceId, definition,
            [new(new EntityId(0, 3), new EntityId(0, 4), new BodySlotKey("voice"), BodyInjuryKind.FunctionLoss, BodyFunction.Vocalization)]);
        var jawMissing = new BodyConditionSnapshot(intact.FormInstanceId, definition,
            [new(new EntityId(0, 5), new EntityId(0, 6), new BodySlotKey("jaw"), BodyInjuryKind.MissingPart)]);
        var scar = new BodyConditionSnapshot(intact.FormInstanceId, definition,
            [new(new EntityId(0, 7), new EntityId(0, 8), new BodySlotKey("voice"), BodyInjuryKind.Scar)]);
        foreach (BodyConditionSnapshot condition in new[] { intact, deaf, voiceLost, jawMissing, scar })
        {
            BodyConditionSnapshot restored = BodyContractDecodingV1.DecodeCondition(BodyContractEncodingV1.EncodeCondition(condition), definition);
            Assert.Equal(condition.SupportsFunction(BodyFunction.Vocalization), restored.SupportsFunction(BodyFunction.Vocalization));
            Assert.Equal(condition.SupportsFunction(BodyFunction.Hearing), restored.SupportsFunction(BodyFunction.Hearing));
        }
        Assert.True(intact.SupportsFunction(BodyFunction.Vocalization));
        Assert.True(deaf.SupportsFunction(BodyFunction.Vocalization));
        Assert.False(deaf.SupportsFunction(BodyFunction.Hearing));
        Assert.False(voiceLost.SupportsFunction(BodyFunction.Vocalization));
        Assert.True(voiceLost.SupportsFunction(BodyFunction.Hearing));
        Assert.False(jawMissing.SupportsFunction(BodyFunction.Vocalization));
        Assert.True(scar.SupportsFunction(BodyFunction.Vocalization));
    }

    private static BodyDefinition DefinitionFor(JsonElement record) => ActorFixture.Definition(EntityId.Parse(record.GetProperty("definitionId").GetString()));
    private static EntityId? OptionalIdentity(JsonElement record, string name) => record.GetProperty(name).ValueKind == JsonValueKind.Null
        ? null : EntityId.Parse(record.GetProperty(name).GetString());
    private static void AssertBadDefinition(byte[] bytes) => Assert.Throws<FormatException>(() => BodyContractDecodingV1.DecodeDefinition(bytes));
    private static void AssertBadCondition(byte[] bytes, BodyDefinition definition) => Assert.Throws<FormatException>(() => BodyContractDecodingV1.DecodeCondition(bytes, definition));
    private static void AssertBadBinding(byte[] bytes, BodyDefinition definition) => Assert.Throws<FormatException>(() => BodyContractDecodingV1.DecodeBinding(bytes, definition));
    private static void DecodePinned(string family, byte[] bytes, BodyDefinition definition)
    {
        if (family == "conditions") BodyContractDecodingV1.DecodeCondition(bytes, definition);
        else BodyContractDecodingV1.DecodeBinding(bytes, definition);
    }
    private static void DecodeAny(string family, byte[] bytes, BodyDefinition definition)
    {
        if (family == "definitions") BodyContractDecodingV1.DecodeDefinition(bytes);
        else DecodePinned(family, bytes, definition);
    }
}
