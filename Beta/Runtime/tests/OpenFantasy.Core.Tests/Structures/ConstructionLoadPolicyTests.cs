using System.Buffers.Binary;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Tests.Construction;
using OpenFantasy.Core.Tests.Gathering;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Structures;

public sealed class ConstructionLoadPolicyTests
{
    private static readonly ContentHash Pin = ContentHash.Compute("load-policy-test"u8);
    private static readonly TerrainScope Scope = new(Id(1), Id(2), Id(3));

    [Fact]
    public void Complete_authored_catalogs_roundtrip_in_canonical_order_without_losing_fields()
    {
        ConstructionLoadPolicy policy = Asset();
        var reversed = new ConstructionLoadPolicy(policy.Scope, policy.Templates.Reverse(), policy.Foundations.Reverse(),
            new StructuralMassCatalog(policy.Masses.Bodies.Reverse(), policy.Masses.SupplementalItems.Reverse()));
        byte[] bytes = ConstructionLoadPolicyCodecV1.Encode(policy);
        Assert.Equal(bytes, ConstructionLoadPolicyCodecV1.Encode(reversed));
        Assert.Equal(ContentHash.Compute(bytes), policy.ContentHash);
        ConstructionLoadPolicy restored = Decode(bytes, policy.ContentHash);
        Assert.Equal(bytes, ConstructionLoadPolicyCodecV1.Encode(restored));
        Assert.Equal(policy.Scope, restored.Scope);
        Assert.Equal(policy.Foundations, restored.Foundations);
        Assert.Equal(policy.Masses.Bodies, restored.Masses.Bodies);
        Assert.Equal(policy.Masses.SupplementalItems, restored.Masses.SupplementalItems);
        Assert.Equal(new CellCoordinate(-2, 3, -1), restored.Foundations[0].Cell);
        Assert.Equal(ulong.MaxValue, restored.Foundations[0].Capacity.Value);
        Assert.Equal(ulong.MaxValue, restored.Masses.Bodies[0].Mass.Value);
        Assert.Equal(ulong.MaxValue, restored.Masses.SupplementalItems[0].MassPerUnit.Value);
        for (int index = 0; index < policy.Templates.Count; index++)
        {
            Assert.Equal(policy.Templates[index].Version, restored.Templates[index].Version);
            Assert.Equal(policy.Templates[index].Members, restored.Templates[index].Members);
            Assert.Equal(policy.Templates[index].Joints, restored.Templates[index].Joints);
            Assert.Equal(policy.Templates[index].Version, ContentHash.Compute(Fields(Fields(bytes)[2])[index + 1]));
        }
    }

    [Fact]
    public void Constructor_copies_collections_and_exposes_no_mutable_catalog()
    {
        ConstructionLoadPolicy original = Asset();
        List<ConstructionBearingTemplate> templates = [.. original.Templates];
        List<StableTerrainBearing> foundations = [.. original.Foundations];
        var policy = new ConstructionLoadPolicy(original.Scope, templates, foundations, original.Masses);
        templates.Clear();
        foundations.Clear();
        Assert.Equal(original.ContentHash, policy.ContentHash);
        Assert.Throws<NotSupportedException>(() => ((IList<ConstructionBearingTemplate>)policy.Templates).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<StableTerrainBearing>)policy.Foundations).Clear());
    }

    [Fact]
    public void Binding_validates_unbuilt_definition_and_uses_only_owned_catalogs()
    {
        WorldSnapshot world = ConstructionWorldFixture.Create();
        ConstructionLoadPolicy policy = ForWorld(world);
        Assert.All(world.Construction!.Projects, project => Assert.Null(project.Installed));
        ConstructionBearingModel model = Decode(ConstructionLoadPolicyCodecV1.Encode(policy), policy.ContentHash).Bind(world);
        ConstructionBearingModel reference = ConstructionBearingModel.Bind(world, [], policy.Foundations, policy.Masses);
        Assert.Equal(reference.InputPin, model.InputPin);
        Assert.Equal(StructuralLoadCodec.EncodeGraph(reference.Graph), StructuralLoadCodec.EncodeGraph(model.Graph));
        Assert.Empty(model.Members);
        Assert.Equal(world.Space.Actors.Count, model.Actors.Count);
        Assert.Equal(world.ContentHash, model.WorldHash);
    }

    [Fact]
    public void Binding_requires_exact_world_branch_and_realm_scope()
    {
        WorldSnapshot world = ConstructionWorldFixture.Create();
        ConstructionLoadPolicy policy = ForWorld(world);
        TerrainScope[] changed = [policy.Scope with { WorldId = Id(500) }, policy.Scope with { BranchId = Id(501) },
            policy.Scope with { RealmId = Id(502) }];
        foreach (TerrainScope scope in changed)
        {
            var other = new ConstructionLoadPolicy(scope, policy.Templates, policy.Foundations, policy.Masses);
            Assert.Throws<ArgumentException>(() => other.Bind(world));
        }
    }

    [Fact]
    public void Missing_unused_and_changed_definition_templates_refuse_even_before_installation()
    {
        WorldSnapshot world = ConstructionWorldFixture.Create();
        ConstructionLoadPolicy policy = ForWorld(world);
        ConstructionBearingTemplate template = Assert.Single(policy.Templates);
        var absent = new ConstructionBearingTemplate(Id(510), template.DefinitionVersion, template.Members, template.Joints);
        var stale = new ConstructionBearingTemplate(template.DefinitionId, ContentHash.Compute("changed-definition"u8),
            template.Members, template.Joints);
        foreach (ConstructionBearingTemplate[] templates in new[] { new[] { absent }, new[] { template, absent }, new[] { stale } })
        {
            var invalid = new ConstructionLoadPolicy(policy.Scope, templates, policy.Foundations, policy.Masses);
            Assert.Throws<ArgumentException>(() => invalid.Bind(world));
        }
        ConstructionDefinition original = world.Construction!.Definitions.Single();
        MillimetreBox[] shifted = original.Members.Select(value => new MillimetreBox(value.MinX + 1, value.MinY,
            value.MinZ, value.MaxX + 1, value.MaxY, value.MaxZ)).ToArray();
        var changed = new ConstructionDefinition(original.DefinitionId, original.Key, original.DisplayName,
            original.MaterialTransform, original.InstalledOutputIndex, original.RequiredTool, shifted,
            original.RequiredWork, original.WorkRate, original.StockInterpretation);
        Assert.NotEqual(original.Version, changed.Version);
        var changedGeometry = new ConstructionLoadPolicy(policy.Scope, [Template(changed)], policy.Foundations, policy.Masses);
        Assert.Throws<ArgumentException>(() => changedGeometry.Bind(world));
    }

    [Fact]
    public void Unbuilt_templates_cannot_hide_invalid_spans_or_joints_behind_installed_filter()
    {
        WorldSnapshot world = ConstructionWorldFixture.Create();
        ConstructionLoadPolicy policy = ForWorld(world);
        ConstructionBearingTemplate template = Assert.Single(policy.Templates);
        var shortSpans = new ConstructionBearingTemplate(template.DefinitionId, template.DefinitionVersion,
            template.Members.Select(value => value with { MaximumSpanMillimetres = 1 }), []);
        var groundJoint = new ConstructionBearingTemplate(template.DefinitionId, template.DefinitionVersion,
            template.Members, [new(0, 1, StructuralJointKind.VerticalBearing)]);
        foreach (ConstructionBearingTemplate invalidTemplate in new[] { shortSpans, groundJoint })
        {
            var invalid = new ConstructionLoadPolicy(policy.Scope, [invalidTemplate], policy.Foundations, policy.Masses);
            Assert.Throws<ArgumentException>(() => invalid.Bind(world));
        }
    }

    [Fact]
    public void Valid_face_bracing_cycles_are_rejected_before_installation_and_when_archived_with_matching_hash()
    {
        // The upper pair shares a real positive vertical face above a grounded base.
        // Either directed brace is geometrically meaningful; together they have no load order.
        ConstructionDefinition definition = ConstructionFixture.Definition(
            [new(0, 0, 0, 100, 100, 10), new(0, 0, 10, 50, 100, 54), new(50, 0, 10, 100, 100, 54)]);
        MillimetreBox first = definition.Members[1];
        MillimetreBox second = definition.Members[2];
        Assert.True(first.MinZ > 0 && second.MinZ > 0);
        Assert.Equal(first.MaxX, second.MinX);
        Assert.Equal(first.MinY, second.MinY);
        Assert.Equal(first.MaxY, second.MaxY);
        Assert.Equal(first.MinZ, second.MinZ);
        Assert.Equal(first.MaxZ, second.MaxZ);
        Assert.Equal(4_400L, (first.MaxY - first.MinY) * (first.MaxZ - first.MinZ));
        StructuralMemberRating[] ratings = definition.Members.Select((_, index) =>
            new StructuralMemberRating((uint)index, new(1_000_000), 1000)).ToArray();
        StructuralMemberJoint forward = new(1, 2, StructuralJointKind.BracedFace);
        StructuralMemberJoint reverse = new(2, 1, StructuralJointKind.BracedFace);
        ConstructionLoadPolicy basis = Asset();
        var cyclic = new ConstructionBearingTemplate(definition.DefinitionId, definition.Version, ratings, [forward, reverse]);
        Assert.Throws<ArgumentException>(() => new ConstructionLoadPolicy(Scope, [cyclic], basis.Foundations, basis.Masses));

        var acyclic = new ConstructionBearingTemplate(definition.DefinitionId, definition.Version, ratings, [forward]);
        var policy = new ConstructionLoadPolicy(Scope, [acyclic], basis.Foundations, basis.Masses);
        byte[] bytes = ConstructionLoadPolicyCodecV1.Encode(policy);
        byte[][] templates = Fields(Fields(bytes)[2]);
        byte[][] joints = Fields(Fields(templates[1])[4]);
        byte[] reverseRow = Frame([Fields(joints[1])[0], Number(2), Number(1), Number((ulong)StructuralJointKind.BracedFace)]);
        byte[] alteredTemplate = Replace(templates[1], 4, Frame([.. joints, reverseRow]));
        byte[] altered = Replace(bytes, 2, Replace(Fields(bytes)[2], 1, alteredTemplate));
        Assert.Throws<FormatException>(() => Decode(altered, ContentHash.Compute(altered)));
        Assert.Equal(bytes, ConstructionLoadPolicyCodecV1.Encode(Decode(bytes, policy.ContentHash)));
    }

    [Fact]
    public void Acyclic_unsupported_or_weak_templates_remain_admissible_for_load_resolution()
    {
        ConstructionLoadPolicy policy = Asset();
        ConstructionBearingTemplate template = policy.Templates[0];
        var unsupported = new ConstructionBearingTemplate(template.DefinitionId, template.DefinitionVersion,
            template.Members.Select(value => value with { Capacity = new(1), MinimumSupportCount = 32 }), []);
        var accepted = new ConstructionLoadPolicy(Scope, [unsupported], policy.Foundations, policy.Masses);
        Assert.Empty(Assert.Single(accepted.Templates).Joints);
        Assert.All(accepted.Templates[0].Members, value => Assert.Equal((ushort)32, value.MinimumSupportCount));
        Assert.Equal(accepted.ContentHash, Decode(ConstructionLoadPolicyCodecV1.Encode(accepted), accepted.ContentHash).ContentHash);
    }

    [Fact]
    public void Foundation_source_material_and_actor_body_pins_are_proved_against_actual_world()
    {
        WorldSnapshot world = ConstructionWorldFixture.Create();
        ConstructionLoadPolicy policy = ForWorld(world);
        StableTerrainBearing first = policy.Foundations[0];
        StableTerrainBearing[] changed =
        [
            new(first.ElementId, first.Cell, Id(520), first.Material, first.Capacity),
            new(first.ElementId, first.Cell, first.SourceId, new(Id(521), Pin), first.Capacity)
        ];
        foreach (StableTerrainBearing replacement in changed)
        {
            var invalid = new ConstructionLoadPolicy(policy.Scope, policy.Templates,
                policy.Foundations.Skip(1).Prepend(replacement), policy.Masses);
            Assert.Throws<ArgumentException>(() => invalid.Bind(world));
        }
        var wrongBody = new StructuralMassCatalog(policy.Masses.Bodies.Select(value =>
            new StructuralBodyMass(value.DefinitionId, Pin, value.Mass)), policy.Masses.SupplementalItems);
        var wrong = new ConstructionLoadPolicy(policy.Scope, policy.Templates, policy.Foundations, wrongBody);
        Assert.Throws<ArgumentException>(() => wrong.Bind(world));
    }

    [Fact]
    public void Every_changed_pin_rating_mass_source_or_geometry_changes_the_expected_archive_hash()
    {
        ConstructionLoadPolicy policy = Asset();
        ConstructionBearingTemplate template = policy.Templates[0];
        StableTerrainBearing anchor = policy.Foundations[0];
        StructuralBodyMass body = policy.Masses.Bodies[0];
        List<ConstructionLoadPolicy> different =
        [
            new(policy.Scope with { RealmId = Id(600) }, policy.Templates, policy.Foundations, policy.Masses),
            WithTemplate(new(template.DefinitionId, ContentHash.Compute("other-definition"u8), template.Members, template.Joints)),
            WithTemplate(new(template.DefinitionId, template.DefinitionVersion,
                template.Members.Select(value => value with { Capacity = new(17) }), template.Joints)),
            WithTemplate(new(template.DefinitionId, template.DefinitionVersion,
                template.Members.Select(value => value with { MaximumSpanMillimetres = 17 }), template.Joints)),
            WithTemplate(new(template.DefinitionId, template.DefinitionVersion, template.Members,
                [new(1, 0, StructuralJointKind.BracedFace)])),
            WithAnchor(new(anchor.ElementId, anchor.Cell, Id(601), anchor.Material, anchor.Capacity)),
            WithAnchor(new(anchor.ElementId, anchor.Cell.Offset(1, 0, 0), anchor.SourceId, anchor.Material, anchor.Capacity)),
            WithAnchor(new(anchor.ElementId, anchor.Cell, anchor.SourceId, anchor.Material, new(17))),
            WithBody(new(body.DefinitionId, ContentHash.Compute("other-body"u8), body.Mass)),
            WithBody(new(body.DefinitionId, body.BodyVersion, new(17))),
            new(policy.Scope, policy.Templates, policy.Foundations, new StructuralMassCatalog(policy.Masses.Bodies,
                policy.Masses.SupplementalItems.Select(value => new StructuralItemMass(value.Item, new(17)))))
        ];
        foreach (ConstructionLoadPolicy changed in different)
        {
            byte[] bytes = ConstructionLoadPolicyCodecV1.Encode(changed);
            Assert.NotEqual(policy.ContentHash, changed.ContentHash);
            Assert.Throws<FormatException>(() => Decode(bytes, policy.ContentHash));
            Assert.Equal(changed.ContentHash, Decode(bytes, changed.ContentHash).ContentHash);
        }

        ConstructionLoadPolicy WithTemplate(ConstructionBearingTemplate replacement) => new(policy.Scope,
            policy.Templates.Skip(1).Prepend(replacement), policy.Foundations, policy.Masses);
        ConstructionLoadPolicy WithAnchor(StableTerrainBearing replacement) => new(policy.Scope,
            policy.Templates, policy.Foundations.Skip(1).Prepend(replacement), policy.Masses);
        ConstructionLoadPolicy WithBody(StructuralBodyMass replacement) => new(policy.Scope, policy.Templates,
            policy.Foundations, new StructuralMassCatalog(policy.Masses.Bodies.Skip(1).Prepend(replacement), policy.Masses.SupplementalItems));
    }

    [Fact]
    public void Null_empty_duplicate_and_aliased_policy_roles_are_rejected()
    {
        ConstructionLoadPolicy policy = Asset();
        StableTerrainBearing first = policy.Foundations[0];
        Assert.Throws<ArgumentNullException>(() => new ConstructionLoadPolicy(null!, policy.Templates, policy.Foundations, policy.Masses));
        Assert.Throws<ArgumentNullException>(() => new ConstructionLoadPolicy(Scope, null!, policy.Foundations, policy.Masses));
        Assert.Throws<ArgumentNullException>(() => new ConstructionLoadPolicy(Scope, policy.Templates, null!, policy.Masses));
        Assert.Throws<ArgumentNullException>(() => new ConstructionLoadPolicy(Scope, policy.Templates, policy.Foundations, null!));
        Assert.Throws<ArgumentException>(() => new ConstructionLoadPolicy(Scope, [], policy.Foundations, policy.Masses));
        Assert.Throws<ArgumentException>(() => new ConstructionLoadPolicy(Scope, policy.Templates, [], policy.Masses));
        Assert.Throws<ArgumentException>(() => new ConstructionLoadPolicy(Scope, [null!], policy.Foundations, policy.Masses));
        Assert.Throws<ArgumentException>(() => new ConstructionLoadPolicy(Scope, policy.Templates, [null!], policy.Masses));
        Assert.Throws<ArgumentException>(() => new ConstructionLoadPolicy(Scope, [policy.Templates[0], policy.Templates[0]], policy.Foundations, policy.Masses));
        Assert.Throws<ArgumentException>(() => new ConstructionLoadPolicy(Scope, policy.Templates, [first, first], policy.Masses));
        Assert.Throws<ArgumentException>(() => new ConstructionLoadPolicy(Scope, policy.Templates,
            [first, new(Id(700), first.Cell, first.SourceId, first.Material, first.Capacity)], policy.Masses));
        Assert.Throws<ArgumentException>(() => new ConstructionLoadPolicy(Scope, policy.Templates,
            [first, new(Id(700), first.Cell.Offset(1, 0, 0), first.SourceId, first.Material, first.Capacity)], policy.Masses));
        Assert.Throws<ArgumentException>(() => new ConstructionLoadPolicy(Scope with { RealmId = Scope.WorldId },
            policy.Templates, policy.Foundations, policy.Masses));
        Assert.Throws<ArgumentException>(() => new ConstructionLoadPolicy(Scope, policy.Templates,
            [new(Scope.WorldId, first.Cell, first.SourceId, first.Material, first.Capacity)], policy.Masses));
        Assert.Throws<ArgumentException>(() => new ConstructionLoadPolicy(Scope, policy.Templates,
            [first, new(Id(701), first.Cell.Offset(1, 0, 0), first.SourceId, new(Id(702), Pin), first.Capacity)], policy.Masses));
        Assert.Throws<ArgumentException>(() => new ConstructionLoadPolicy(Scope, policy.Templates,
            [first, new(Id(701), first.Cell.Offset(1, 0, 0), Id(702),
                new(first.Material.DefinitionId, ContentHash.Compute("changed-material"u8)), first.Capacity)], policy.Masses));
    }

    [Theory]
    [InlineData(long.MaxValue, 0, 0)]
    [InlineData(0, long.MinValue, 0)]
    [InlineData(0, 0, long.MaxValue / 256)]
    public void Foundations_that_overflow_spatial_projection_are_rejected(long x, long y, long z)
    {
        ConstructionLoadPolicy policy = Asset();
        StableTerrainBearing first = policy.Foundations[0];
        Assert.Throws<OverflowException>(() => new ConstructionLoadPolicy(Scope, policy.Templates,
            [new(first.ElementId, new(x, y, z), first.SourceId, first.Material, first.Capacity)], policy.Masses));
    }

    [Fact]
    public void Infinite_catalogs_stop_after_the_first_excess_row()
    {
        ConstructionLoadPolicy policy = Asset();
        int templateReads = 0;
        int foundationReads = 0;
        IEnumerable<ConstructionBearingTemplate> Templates()
        {
            while (true)
            {
                Assert.True(templateReads++ <= ConstructionLoadPolicy.MaximumTemplates);
                yield return policy.Templates[0];
            }
        }
        IEnumerable<StableTerrainBearing> Foundations()
        {
            while (true)
            {
                Assert.True(foundationReads++ <= ConstructionLoadPolicy.MaximumFoundations);
                yield return policy.Foundations[0];
            }
        }
        Assert.Throws<ArgumentException>(() => new ConstructionLoadPolicy(Scope, Templates(), policy.Foundations, policy.Masses));
        Assert.Throws<ArgumentException>(() => new ConstructionLoadPolicy(Scope, policy.Templates, Foundations(), policy.Masses));
        Assert.Equal(ConstructionLoadPolicy.MaximumTemplates + 1, templateReads);
        Assert.Equal(ConstructionLoadPolicy.MaximumFoundations + 1, foundationReads);
    }

    [Fact]
    public void Maximum_catalogs_fit_the_declared_archive_bound()
    {
        StructuralMemberRating[] ratings = Enumerable.Range(0, ConstructionDefinition.MaximumMembers)
            .Select(index => new StructuralMemberRating((uint)index, new(ulong.MaxValue), 8000, 32)).ToArray();
        StructuralMemberJoint[] joints = ratings.SelectMany(upper => ratings.Where(lower => lower.MemberIndex < upper.MemberIndex)
            .Select(lower => new StructuralMemberJoint(upper.MemberIndex, lower.MemberIndex, StructuralJointKind.BracedFace))).ToArray();
        Assert.Equal(496, joints.Length);
        var policy = new ConstructionLoadPolicy(Scope,
            Enumerable.Range(0, ConstructionLoadPolicy.MaximumTemplates).Select(index => new ConstructionBearingTemplate(Id((ulong)index + 1_000), Pin, ratings, joints)),
            Enumerable.Range(0, ConstructionLoadPolicy.MaximumFoundations).Select(index =>
                new StableTerrainBearing(Id((ulong)index + 10_000), new(index, 0, -1), Id((ulong)index + 40_000), new(Id(801), Pin), new(ulong.MaxValue))),
            new StructuralMassCatalog(Enumerable.Range(0, StructuralMassCatalog.MaximumBodyDefinitions)
                .Select(index => new StructuralBodyMass(Id((ulong)index + 20_000), Pin, new(ulong.MaxValue))),
                Enumerable.Range(0, StructuralMassCatalog.MaximumSupplementalItems)
                    .Select(index => new StructuralItemMass(new(Id((ulong)index + 30_000), Pin), new(ulong.MaxValue)))));
        byte[] bytes = ConstructionLoadPolicyCodecV1.Encode(policy);
        Assert.InRange(bytes.Length, 1, ConstructionLoadPolicyCodecV1.MaximumEncodedByteLength);
        ConstructionLoadPolicy restored = Decode(bytes, policy.ContentHash);
        Assert.Equal(ConstructionLoadPolicy.MaximumTemplates, restored.Templates.Count);
        Assert.Equal(ConstructionLoadPolicy.MaximumFoundations, restored.Foundations.Count);
        Assert.Equal(StructuralMassCatalog.MaximumBodyDefinitions, restored.Masses.Bodies.Count);
        Assert.Equal(StructuralMassCatalog.MaximumSupplementalItems, restored.Masses.SupplementalItems.Count);
        Assert.Equal(bytes, ConstructionLoadPolicyCodecV1.Encode(restored));
    }

    [Fact]
    public void Matching_hash_does_not_waive_canonical_order_duplicates_or_numeric_bounds()
    {
        byte[] bytes = ConstructionLoadPolicyCodecV1.Encode(Asset());
        for (int collection = 2; collection <= 5; collection++)
        {
            byte[][] rows = Fields(Fields(bytes)[collection]);
            (rows[1], rows[2]) = (rows[2], rows[1]);
            Reject(Replace(bytes, collection, Frame(rows)));
            rows[2] = rows[1];
            Reject(Replace(bytes, collection, Frame(rows)));
        }
        byte[][] templates = Fields(Fields(bytes)[2]);
        byte[][] template = Fields(templates[1]);
        byte[][] ratings = Fields(template[3]);
        byte[] originalRating = ratings[1];
        foreach (int field in new[] { 1, 2, 3, 4 })
        {
            ratings[1] = Replace(originalRating, field, Number(field == 2 ? 0 : ulong.MaxValue));
            Reject(Replace(bytes, 2, Replace(Fields(bytes)[2], 1, Replace(templates[1], 3, Frame(ratings)))));
        }
        byte[][] joints = Fields(template[4]);
        joints[1] = Replace(joints[1], 3, Number(99));
        Reject(Replace(bytes, 2, Replace(Fields(bytes)[2], 1, Replace(templates[1], 4, Frame(joints)))));
        byte[][] bodies = Fields(Fields(bytes)[4]);
        bodies[1] = Replace(bodies[1], 3, Number(0));
        Reject(Replace(bytes, 4, Frame(bodies)));
        byte[][] items = Fields(Fields(bytes)[5]);
        items[1] = Replace(items[1], 2, new byte[31]);
        Reject(Replace(bytes, 5, Frame(items)));

        static void Reject(byte[] malformed) => Assert.Throws<FormatException>(() => Decode(malformed, ContentHash.Compute(malformed)));
    }

    [Fact]
    public void Truncation_trailing_fields_oversize_and_canceled_decode_refuse_publication()
    {
        ConstructionLoadPolicy policy = Asset();
        byte[] bytes = ConstructionLoadPolicyCodecV1.Encode(policy);
        for (int length = 0; length < bytes.Length; length++)
        {
            byte[] truncated = bytes[..length];
            Assert.Throws<FormatException>(() => Decode(truncated, ContentHash.Compute(truncated)));
        }
        byte[] trailing = [.. bytes, 0];
        Assert.Throws<FormatException>(() => Decode(trailing, ContentHash.Compute(trailing)));
        byte[] extraField = Frame([.. Fields(bytes), Number(1)]);
        Assert.Throws<FormatException>(() => Decode(extraField, ContentHash.Compute(extraField)));
        byte[] excessive = new byte[ConstructionLoadPolicyCodecV1.MaximumEncodedByteLength + 1];
        Assert.Throws<FormatException>(() => Decode(excessive, ContentHash.Compute(excessive)));
        Assert.Throws<ArgumentNullException>(() => Decode(bytes, null!));
        Assert.Throws<OperationCanceledException>(() => ConstructionLoadPolicyCodecV1.Decode(bytes, policy.ContentHash, new CancellationToken(true)));
    }

    private static ConstructionLoadPolicy Asset() => new(Scope,
        [new(Id(10), Pin, [new(0, new(ulong.MaxValue), 1000, 2), new(1, new(17), 500)], [new(1, 0, StructuralJointKind.VerticalBearing)]),
            new(Id(11), Pin, [new(0, new(23), 750)], [])],
        [new(Id(20), new(-2, 3, -1), Id(30), new(Id(40), Pin), new(ulong.MaxValue)),
            new(Id(21), new(4, -5, 0), Id(31), new(Id(40), Pin), new(19))],
        new StructuralMassCatalog([new(Id(50), Pin, new(ulong.MaxValue)), new(Id(51), Pin, new(29))],
            [new(new(Id(60), Pin), new(ulong.MaxValue)), new(new(Id(61), Pin), new(31))]));

    private static ConstructionLoadPolicy ForWorld(WorldSnapshot world)
    {
        TerrainRegionSnapshot terrain = Assert.Single(world.Space.Regions);
        List<StableTerrainBearing> foundations = [];
        for (long y = -1; y <= 1; y++)
        {
            for (long x = -1; x <= 3; x++)
            {
                TerrainCell cell = terrain.GetCell(new(x, y, 0));
                Assert.True(cell.IsSolid);
                foundations.Add(new(Id(100 + (ulong)foundations.Count), cell.Coordinate, cell.SourceId!.Value, cell.Material!, new(1_000_000)));
            }
        }
        var masses = new StructuralMassCatalog(world.Space.Actors.Select(actor =>
            new StructuralBodyMass(actor.Binding.DefinitionId, actor.Binding.DefinitionVersion, new(1_000))).DistinctBy(value => value.DefinitionId),
            [new(GatheringFixture.Axe, new(50)), new(world.Construction!.Definitions.Single().RequiredTool.Item, new(75))]);
        return new(world.Space.Scope, world.Construction.Definitions.Select(Template), foundations, masses);
    }

    private static ConstructionBearingTemplate Template(ConstructionDefinition definition) => new(definition.DefinitionId, definition.Version,
        definition.Members.Select((_, index) => new StructuralMemberRating((uint)index, new(1_000_000), 8000)), []);
    private static EntityId Id(ulong value) => new(0x6c6f6164706f6c, value);
    private static ConstructionLoadPolicy Decode(byte[] bytes, ContentHash expected) =>
        ConstructionLoadPolicyCodecV1.Decode(bytes, expected, TestContext.Current.CancellationToken);

    private static byte[][] Fields(byte[] bytes)
    {
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8)));
        byte[][] fields = new byte[count][];
        int offset = 12;
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
            offset += 4;
            fields[index] = bytes.AsSpan(offset, length).ToArray();
            offset += length;
        }
        Assert.Equal(bytes.Length, offset);
        return fields;
    }

    private static byte[] Replace(byte[] bytes, int index, byte[] value)
    {
        byte[][] fields = Fields(bytes);
        fields[index] = value;
        return Frame(fields);
    }

    private static byte[] Frame(byte[][] fields)
    {
        byte[] bytes = new byte[checked(12 + fields.Sum(value => checked(4 + value.Length)))];
        "OFC\0"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), checked((uint)fields.Length));
        int offset = 12;
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset), checked((uint)field.Length));
            offset += 4;
            field.CopyTo(bytes, offset);
            offset += field.Length;
        }
        return bytes;
    }

    private static byte[] Number(ulong value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }
}
