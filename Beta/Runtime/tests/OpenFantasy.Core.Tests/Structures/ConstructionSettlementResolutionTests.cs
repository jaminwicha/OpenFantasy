using System.Buffers.Binary;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Tests.Spatial;

namespace OpenFantasy.Core.Tests.Structures;

/// <summary>Supplied completed-domain fixtures; actual funding and authoritative continuation live in Hosting tests.</summary>
public sealed class ConstructionSettlementResolutionTests
{
    private static readonly ContentHash Pin = ContentHash.Compute("settlement-resolution-tests"u8);
    private static readonly StructuralSettlementPolicy Policy = new(256, 256, 256, 9800);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(16)]
    [InlineData(256)]
    public void Actual_work_slices_preserve_the_exact_synchronous_candidate_without_mutating_prior_states(int budget)
    {
        Fixture fixture = Create();
        ConstructionSettlementResult expected = new ConstructionSettlementResolver().Resolve(fixture.Space,
            [fixture.Candidate], fixture.Physical, Policy);
        ConstructionSettlementResolution initial = Begin(fixture);
        byte[] initialBytes = Encode(initial);
        ConstructionSettlementResolution state = initial;
        while (!state.IsComplete)
        {
            Assert.Throws<InvalidOperationException>(() => state.Result);
            ulong before = state.CompletedWorkUnits;
            ConstructionSettlementResolution previous = state;
            state = state.Advance(budget);
            Assert.Equal(Math.Min(4UL, before + (ulong)budget), state.CompletedWorkUnits);
            Assert.Equal(before, previous.CompletedWorkUnits);
            Assert.False(previous.IsComplete);
        }
        Assert.False(state.IsFailed);
        Assert.Null(state.FailureReason);
        Assert.Equal(4UL, state.CompletedWorkUnits); // One base, one member, one rider, one final actor contact.
        AssertSameResult(expected, state.Result);
        Assert.Same(state, state.Advance(1));
        Assert.Equal(initialBytes, Encode(initial));
        Assert.Equal(0UL, fixture.Physical.Revision);
        Assert.Equal(new SubunitPosition(128, 128, 288), fixture.Space.Actors.Single().Feet);
        Assert.False(fixture.Space.Regions.Single().GetCell(new(0, 0, 0)).IsExcavated);
        Assert.Throws<ArgumentOutOfRangeException>(() => initial.Advance(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Advance(ConstructionSettlementResolution.MaximumWorkBudget + 1));
    }

    [Fact]
    public void Every_real_cursor_reconstructs_and_resumes_with_exact_canonical_bytes()
    {
        Fixture fixture = Create();
        ConstructionSettlementResolution reference = Finish(Begin(fixture), 256);
        ConstructionSettlementResolution state = Begin(fixture);
        for (ulong work = 0; work <= 4; work++)
        {
            Assert.Equal(work, state.CompletedWorkUnits);
            byte[] bytes = Encode(state);
            Assert.InRange(bytes.Length, 1, ConstructionSettlementResolutionCodecV1.MaximumEncodedByteLength);
            ConstructionSettlementResolution restored = Decode(bytes, fixture);
            Assert.Equal(bytes, Encode(restored));
            Assert.Equal(work, restored.CompletedWorkUnits);
            Assert.Equal(state.IsComplete, restored.IsComplete);
            Assert.Equal(Encode(reference), Encode(Finish(restored, 2)));
            if (work == 4)
            {
                AssertSameResult(reference.Result, restored.Result);
            }
            else
            {
                state = restored.Advance(1);
            }
        }
    }

    [Fact]
    public void Thirty_two_members_and_sixty_four_riders_reach_the_proved_192_unit_bound_exactly()
    {
        Fixture fixture = Create(maximum: true);
        Assert.Equal(32, fixture.Physical.Members.Count);
        Assert.Equal(64, fixture.Space.Actors.Count);
        ConstructionSettlementResolution initial = Begin(fixture);
        ConstructionSettlementResolution oneUnit = Finish(initial, 1);
        ConstructionSettlementResolution full = Finish(initial, 256);
        Assert.Equal(192, ConstructionSettlementResolution.MaximumWorkUnits);
        Assert.Equal((ulong)ConstructionSettlementResolution.MaximumWorkUnits, oneUnit.CompletedWorkUnits);
        Assert.Equal(oneUnit.CompletedWorkUnits, full.CompletedWorkUnits);
        Assert.False(oneUnit.IsFailed);
        AssertSameResult(full.Result, oneUnit.Result);
        Assert.Equal(Encode(full), Encode(oneUnit));
        Assert.Equal(Encode(full), Encode(Decode(Encode(full), fixture)));
        Assert.All(full.Result.Physical.Members, member => Assert.Equal(new SubunitPosition(0, 0, -256), member.Displacement));
        Assert.All(full.Result.Space.Actors, actor => Assert.Equal(32L, actor.Feet.Z));
        Assert.Same(full, full.Advance(256));
    }

    [Fact]
    public void Archive_rejects_substituted_original_geometry_candidate_terrain_physical_state_or_policy()
    {
        Fixture fixture = Create();
        byte[] bytes = Encode(Begin(fixture).Advance(1));
        ActorPresence old = fixture.Space.Actors.Single();
        var shiftedActor = new ActorPresence(old.Binding, old.Condition, old.Scope, old.Feet.Offset(32, 0, 0), old.Revision + 1, old.HomePlaceId);
        LocalSpatialSnapshot shifted = fixture.Space.WithActors([shiftedActor]);
        Assert.Throws<FormatException>(() => ConstructionSettlementResolutionCodecV1.Decode(bytes,
            shifted, [fixture.Candidate], fixture.Physical, Policy, TestContext.Current.CancellationToken));
        TerrainRegionSnapshot changedTerrain = fixture.Candidate.Excavate(fixture.Candidate.Revision, [new(1, 0, 0)]);
        Assert.Throws<FormatException>(() => ConstructionSettlementResolutionCodecV1.Decode(bytes,
            fixture.Space, [changedTerrain], fixture.Physical, Policy, TestContext.Current.CancellationToken));
        ConstructionMemberState member = fixture.Physical.Members.Single();
        ConstructionPhysicalSnapshot weathered = fixture.Physical.WithMember(fixture.Physical.Revision,
            member.WithState(member.Revision, member.ConditionParts - 1, member.Displacement, member.Disposition));
        Assert.Throws<FormatException>(() => ConstructionSettlementResolutionCodecV1.Decode(bytes,
            fixture.Space, [fixture.Candidate], weathered, Policy, TestContext.Current.CancellationToken));
        Assert.Throws<FormatException>(() => ConstructionSettlementResolutionCodecV1.Decode(bytes,
            fixture.Space, [fixture.Candidate], fixture.Physical, new(256, 256, 256, 9700), TestContext.Current.CancellationToken));
        var differentContact = new LocalSpatialSnapshot(fixture.Space.Scope, fixture.Space.Regions, fixture.Space.Actors,
            fixture.Space.Structures, GroundSupportMode.TerrainAndConstruction);
        Assert.Throws<FormatException>(() => ConstructionSettlementResolutionCodecV1.Decode(bytes,
            differentContact, [fixture.Candidate], fixture.Physical, Policy, TestContext.Current.CancellationToken));
        byte[] wrongPin = (byte[])bytes.Clone();
        wrongPin[FieldOffset(wrongPin, 1)] ^= 1;
        Assert.Throws<FormatException>(() => Decode(wrongPin, fixture));
        Assert.Equal(bytes, Encode(Decode(bytes, fixture)));
    }

    [Fact]
    public void Malformed_frames_cannot_claim_impossible_work_terminal_status_or_extra_fields()
    {
        Fixture fixture = Create();
        byte[] partial = Encode(Begin(fixture).Advance(1));
        foreach (ulong work in new[] { 5UL, 192UL, 193UL, ulong.MaxValue })
        {
            Assert.Throws<FormatException>(() => Decode(NumberField(partial, 2, work), fixture));
        }
        foreach (ulong status in new[] { 1UL, 2UL, 3UL, ulong.MaxValue })
        {
            Assert.Throws<FormatException>(() => Decode(NumberField(partial, 3, status), fixture));
        }
        byte[] complete = Encode(Finish(Begin(fixture), 256));
        Assert.Throws<FormatException>(() => Decode(NumberField(complete, 3, 0), fixture));
        for (int length = 0; length < partial.Length; length++)
        {
            byte[] truncated = partial[..length];
            Assert.Throws<FormatException>(() => Decode(truncated, fixture));
        }
        Assert.Throws<FormatException>(() => Decode([.. partial, 0], fixture));
        Assert.Throws<FormatException>(() => Decode(new byte[ConstructionSettlementResolutionCodecV1.MaximumEncodedByteLength + 1], fixture));
        byte[] excessiveFields = (byte[])partial.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(excessiveFields.AsSpan(8), uint.MaxValue);
        Assert.Throws<FormatException>(() => Decode(excessiveFields, fixture));
        byte[] oversizedField = (byte[])partial.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(oversizedField.AsSpan(FieldOffset(oversizedField, 1) - 4), uint.MaxValue);
        Assert.Throws<FormatException>(() => Decode(oversizedField, fixture));
        byte[] wrongTag = (byte[])partial.Clone();
        wrongTag[FieldOffset(wrongTag, 0)] ^= 1;
        Assert.Throws<FormatException>(() => Decode(wrongTag, fixture));
    }

    [Fact]
    public void The_first_failed_real_contact_is_terminal_and_reconstructs_without_spending_more_work()
    {
        Fixture fixture = Create(missingLower: true);
        ConstructionSettlementResolution before = Begin(fixture);
        Assert.False(before.IsComplete);
        ConstructionSettlementResolution failed = before.Advance(1);
        Assert.True(failed.IsComplete);
        Assert.True(failed.IsFailed);
        Assert.NotNull(failed.FailureReason);
        Assert.Equal(1UL, failed.CompletedWorkUnits);
        Assert.Throws<InvalidOperationException>(() => failed.Result);
        Assert.Same(failed, failed.Advance(256));
        Assert.Equal(0UL, before.CompletedWorkUnits);
        byte[] bytes = Encode(failed);
        ConstructionSettlementResolution restored = Decode(bytes, fixture);
        Assert.True(restored.IsFailed);
        Assert.Equal(failed.FailureReason, restored.FailureReason);
        Assert.Equal(bytes, Encode(restored));
        Assert.Throws<FormatException>(() => Decode(NumberField(bytes, 2, 2), fixture));
        Assert.Throws<ArgumentException>(() => new ConstructionSettlementResolver().Resolve(fixture.Space,
            [fixture.Candidate], fixture.Physical, Policy));
        Assert.Equal(0UL, fixture.Physical.Revision);
    }

    [Fact]
    public void Canceled_restore_does_not_return_a_continuation()
    {
        Fixture fixture = Create();
        byte[] bytes = Encode(Begin(fixture).Advance(2));
        Assert.Throws<OperationCanceledException>(() => ConstructionSettlementResolutionCodecV1.Decode(bytes,
            fixture.Space, [fixture.Candidate], fixture.Physical, Policy, new CancellationToken(canceled: true)));
        Assert.Equal(bytes, Encode(Decode(bytes, fixture)));
    }

    private static ConstructionSettlementResolution Begin(Fixture fixture) =>
        ConstructionSettlementResolution.Begin(fixture.Space, [fixture.Candidate], fixture.Physical, Policy);

    private static byte[] Encode(ConstructionSettlementResolution state) => ConstructionSettlementResolutionCodecV1.Encode(state);

    private static ConstructionSettlementResolution Decode(byte[] bytes, Fixture fixture) =>
        ConstructionSettlementResolutionCodecV1.Decode(bytes, fixture.Space, [fixture.Candidate], fixture.Physical,
            Policy, TestContext.Current.CancellationToken);

    private static ConstructionSettlementResolution Finish(ConstructionSettlementResolution state, int budget)
    {
        for (int calls = 0; !state.IsComplete; calls++)
        {
            Assert.True(calls < ConstructionSettlementResolution.MaximumWorkUnits);
            ulong before = state.CompletedWorkUnits;
            state = state.Advance(budget);
            Assert.InRange(state.CompletedWorkUnits - before, 1UL, (ulong)budget);
        }
        return state;
    }

    private static void AssertSameResult(ConstructionSettlementResult expected, ConstructionSettlementResult actual)
    {
        Assert.Equal(ConstructionPhysicalCodecV1.Encode(expected.Physical), ConstructionPhysicalCodecV1.Encode(actual.Physical));
        Assert.Equal(expected.Space.Scope, actual.Space.Scope);
        Assert.Equal(expected.Space.GroundSupport, actual.Space.GroundSupport);
        Assert.Equal(expected.Space.Regions.Select(region => Convert.ToHexString(TerrainRegionCodecV1.Encode(region))),
            actual.Space.Regions.Select(region => Convert.ToHexString(TerrainRegionCodecV1.Encode(region))));
        Assert.Equal(expected.Space.Actors.Select(ActorState), actual.Space.Actors.Select(ActorState));
        Assert.Equal(expected.Space.Structures.SelectMany(structure => structure.Boxes.Select(box => (structure.StructureId, box))),
            actual.Space.Structures.SelectMany(structure => structure.Boxes.Select(box => (structure.StructureId, box))));
        Assert.Equal(expected.Space.Structures.SelectMany(structure => structure.BearingSurfaces.Select(surface => (structure.StructureId, surface))),
            actual.Space.Structures.SelectMany(structure => structure.BearingSurfaces.Select(surface => (structure.StructureId, surface))));
    }

    private static object ActorState(ActorPresence actor) => new
    {
        actor.ActorId, actor.Feet, actor.Revision, actor.HomePlaceId, actor.Scope,
        Binding = Convert.ToHexString(BodyContractEncodingV1.EncodeBinding(actor.Binding)),
        Condition = Convert.ToHexString(BodyContractEncodingV1.EncodeCondition(actor.Condition)),
        Body = Convert.ToHexString(BodyContractEncodingV1.EncodeDefinition(actor.Condition.Definition))
    };

    private sealed record Fixture(LocalSpatialSnapshot Space, TerrainRegionSnapshot Candidate, ConstructionPhysicalSnapshot Physical);

    private static Fixture Create(bool maximum = false, bool missingLower = false)
    {
        int width = maximum ? 8 : 1;
        int depth = maximum ? 4 : 1;
        int localX = maximum ? -4000 : 0;
        MillimetreBox[] members = Enumerable.Range(0, width).SelectMany(x => Enumerable.Range(0, depth)
            .Select(y => new MillimetreBox(localX + x * 1000, y * 1000, 0, localX + (x + 1) * 1000, (y + 1) * 1000, 125))).ToArray();
        ConstructionProject project = Project(members, maximum ? 1024 : 0);
        ConstructionPhysicalSnapshot physical = ConstructionPhysicalSnapshot.Create(new(SpatialFixture.Scope.WorldId,
            SpatialFixture.Scope.BranchId, 1, [project.Definition], [project]));
        var material = new CountedItemDefinition(Id(800), Pin);
        var terrain = new StratifiedTerrainGenerator().Generate(SpatialFixture.Scope, Id(801),
            new(0, 0, 1, new(material, material, material)), new(new(-1, -1, missingLower ? 0 : -1), width + 2, depth + 2, 5));
        BodyDefinition body = SpatialFixture.Body(32, 32, 64);
        ActorPresence[] actors = maximum
            ? Enumerable.Range(0, 64).Select(index => SpatialFixture.Actor((ulong)index + 1,
                new((index / 2 % width) * 256 + (index % 2 == 0 ? 80 : 176), (index / (2 * width)) * 256 + 128, 288), body)).ToArray()
            : [SpatialFixture.Actor(1, new(128, 128, 288), body)];
        var space = new LocalSpatialSnapshot(SpatialFixture.Scope, [terrain], actors, physical.GetStructures(),
            GroundSupportMode.SteppedTerrainAndConstruction);
        TerrainRegionSnapshot candidate = terrain.Excavate(terrain.Revision,
            Enumerable.Range(0, width).SelectMany(x => Enumerable.Range(0, depth).Select(y => new CellCoordinate(x, y, 0))));
        return new(space, candidate, physical);
    }

    private static ConstructionProject Project(MillimetreBox[] members, long originX)
    {
        ulong volume = members.Aggregate(0UL, (sum, member) => checked(sum + member.VolumeMicrolitres));
        var package = new PackageId("settlement-resolution-test");
        var input = new MaterialDefinition(package, Id(1), "input", new(1_000_000), new(volume), new(1));
        var output = new MaterialDefinition(package, Id(2), "output", new(1_000_000), new(volume), new(1));
        var inputItem = new CountedItemDefinition(Id(3), Pin);
        var outputItem = new CountedItemDefinition(Id(4), Pin);
        var materialRecipe = new MaterialRecipeDefinition(package, Id(5), "assemble", new(1), null,
            new([input, output]), [new(RecipeMaterialRole.Input, input.Pin(), 1), new(RecipeMaterialRole.Output, output.Pin(), 1)]);
        var recipe = new CraftRecipeDefinition(Id(6), materialRecipe, [new(inputItem, input), new(outputItem, output)],
            [new(inputItem, 0, new(1_000_000))]);
        var tool = new ConstructionToolDefinition(Id(7), new(Id(8), Pin), ConstructionToolOperation.CutCrossSlots);
        var definition = new ConstructionDefinition(Id(9), "settlement_resolution", "Settlement resolution specimen", recipe, 0,
            tool, members, new(1), new ExposureRate(Id(10), Pin, 1, ExposurePacingV1.ExplorationMillisecondsPerStep));
        var progress = ExposureProgress.Begin(Id(11), definition.WorkRate)
            .Advance(LocalExposureDuration.FromMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep));
        EntityId cause = Id(16);
        var installed = new InstalledStructure(EntityId.DeriveSpawned(cause, 2), EntityId.DeriveSpawned(cause, 1),
            0, EntityId.DeriveSpawned(cause, 0), cause);
        var site = new SubunitPosition(originX, 0, 256);
        SubunitPosition work = site.Offset(0, -256, 0);
        return new(Id(11), definition, Id(12), Id(12), Id(13), Id(14), Id(15), site, work,
            [new(Id(12), work, work.Offset(256, 0, 0))], 1, progress.Capture(), installed);
    }

    private static EntityId Id(ulong value) => new(0x736574746c657273, value);

    private static int FieldOffset(byte[] bytes, int ordinal)
    {
        int offset = 12;
        for (int index = 0; index < ordinal; index++)
        {
            offset += 4 + checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
        }
        return offset + 4;
    }

    private static byte[] NumberField(byte[] bytes, int ordinal, ulong value)
    {
        byte[] changed = (byte[])bytes.Clone();
        BinaryPrimitives.WriteUInt64BigEndian(changed.AsSpan(FieldOffset(changed, ordinal)), value);
        return changed;
    }
}
